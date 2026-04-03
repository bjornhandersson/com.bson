namespace MvtNet.Tests;

public class CommandIntegerTests
{
    [Test]
    public void MoveTo_Count1()
    {
        // MoveTo = 1, count = 1 → (1 & 0x7) | (1 << 3) = 1 | 8 = 9
        Assert.That(GeometryEncoder.CommandInteger(1, 1), Is.EqualTo(9u));
    }

    [Test]
    public void LineTo_Count3()
    {
        // LineTo = 2, count = 3 → (2 & 0x7) | (3 << 3) = 2 | 24 = 26
        Assert.That(GeometryEncoder.CommandInteger(2, 3), Is.EqualTo(26u));
    }

    [Test]
    public void ClosePath_Count1()
    {
        // ClosePath = 7, count = 1 → (7 & 0x7) | (1 << 3) = 7 | 8 = 15
        Assert.That(GeometryEncoder.CommandInteger(7, 1), Is.EqualTo(15u));
    }
}
