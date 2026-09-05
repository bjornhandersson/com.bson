namespace Bson.MvtNet.Tests;

public class ProtoBufferTests
{
    [TestCase(0u, new byte[] { 0x00 })]
    [TestCase(1u, new byte[] { 0x01 })]
    [TestCase(127u, new byte[] { 0x7F })]
    [TestCase(128u, new byte[] { 0x80, 0x01 })]
    [TestCase(300u, new byte[] { 0xAC, 0x02 })]
    [TestCase(16384u, new byte[] { 0x80, 0x80, 0x01 })]
    [TestCase(uint.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F })]
    public void WriteVarint_Uint_MatchesSpec(uint value, byte[] expected)
    {
        var buffer = new ProtoBuffer();

        buffer.WriteVarint(value);

        Assert.That(buffer.ToArray(), Is.EqualTo(expected));
        Assert.That(ProtoBuffer.VarintSize(value), Is.EqualTo(expected.Length));
    }

    [Test]
    public void WriteVarint_Ulong_MaxValue_IsTenBytes()
    {
        var buffer = new ProtoBuffer();

        buffer.WriteVarint(ulong.MaxValue);

        Assert.That(
            buffer.ToArray(),
            Is.EqualTo(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 })
        );
        Assert.That(ProtoBuffer.VarintSize(ulong.MaxValue), Is.EqualTo(10));
    }

    [Test]
    public void WriteVarint_IntoExactlySizedBuffer_DoesNotGrow()
    {
        var buffer = new ProtoBuffer(capacity: 2);

        buffer.WriteVarint(300u);
        var first = buffer.ToArray();
        var second = buffer.ToArray();

        Assert.That(first, Is.EqualTo(new byte[] { 0xAC, 0x02 }));
        Assert.That(ReferenceEquals(first, second), Is.True, "exactly full buffer should be returned as-is");
    }

    [Test]
    public void WriteFixed_IsLittleEndian()
    {
        var buffer = new ProtoBuffer();

        buffer.WriteFixed32(0x04030201u);
        buffer.WriteFixed64(0x0807060504030201UL);

        Assert.That(
            buffer.ToArray(),
            Is.EqualTo(new byte[] { 1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 7, 8 })
        );
    }

    [Test]
    public void WriteString_PrefixesUtf8Length()
    {
        var buffer = new ProtoBuffer();

        buffer.WriteString("é");

        Assert.That(buffer.ToArray(), Is.EqualTo(new byte[] { 0x02, 0xC3, 0xA9 }));
    }

    [Test]
    public void WritePacked_PrefixesByteLengthNotCount()
    {
        var buffer = new ProtoBuffer();
        var values = new uint[] { 1, 300 };

        buffer.WritePacked(values);

        Assert.That(ProtoBuffer.PackedSize(values), Is.EqualTo(3));
        Assert.That(buffer.ToArray(), Is.EqualTo(new byte[] { 0x03, 0x01, 0xAC, 0x02 }));
    }

    [Test]
    public void Write_GrowsAcrossManyAppends()
    {
        var buffer = new ProtoBuffer(0);
        var chunk = new ProtoBuffer();
        chunk.WriteString(new string('a', 1000));

        for (int i = 0; i < 100; i++)
        {
            buffer.Write(chunk);
        }

        Assert.That(buffer.Count, Is.EqualTo(100 * 1002));
        Assert.That(buffer.ToArray(), Has.Length.EqualTo(100 * 1002));
    }
}
