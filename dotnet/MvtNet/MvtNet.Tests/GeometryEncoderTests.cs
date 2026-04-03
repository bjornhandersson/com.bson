namespace MvtNet.Tests;

public class GeometryEncoderTests
{
    [Test]
    public void EncodePoint_ProducesCorrectCommands()
    {
        var commands = GeometryEncoder.EncodePoint(25, 17);

        // MoveTo(1, count=1) = 9, zigzag(25) = 50, zigzag(17) = 34
        Assert.That(commands, Is.EqualTo(new uint[] { 9, 50, 34 }));
    }

    [Test]
    public void EncodeLineString_ProducesDeltaEncodedCommands()
    {
        TileCoord[] coords = [new(10, 20), new(30, 40)];
        var commands = GeometryEncoder.EncodeLineString(coords);

        // MoveTo(count=1)=9, zigzag(10)=20, zigzag(20)=40
        // LineTo(count=1)=10, zigzag(20)=40, zigzag(20)=40
        Assert.That(commands, Is.EqualTo(new uint[] { 9, 20, 40, 10, 40, 40 }));
    }

    [Test]
    public void EncodePolygon_EndsWithClosePath()
    {
        TileCoord[] ring = [new(0, 0), new(100, 0), new(100, 100)];
        var commands = GeometryEncoder.EncodePolygon(ring);

        // Last command should be ClosePath (7 & 0x7) | (1 << 3) = 15
        Assert.That(commands[^1], Is.EqualTo(15u));
    }
}
