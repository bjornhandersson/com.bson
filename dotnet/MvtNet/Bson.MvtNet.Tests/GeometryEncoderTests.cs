namespace Bson.MvtNet.Tests;

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

    [Test]
    public void EncodeLineString_TooFewPoints_Throws()
    {
        TileCoord[] coords = [new(10, 20)];

        Assert.Throws<ArgumentException>(() => GeometryEncoder.EncodeLineString(coords));
    }

    [Test]
    public void EncodePolygon_TooFewPoints_Throws()
    {
        TileCoord[] ring = [new(0, 0), new(100, 0)];

        Assert.Throws<ArgumentException>(() => GeometryEncoder.EncodePolygon(ring));
    }

    [Test]
    public void EncodePoint_AtOrigin()
    {
        var commands = GeometryEncoder.EncodePoint(0, 0);

        // MoveTo(count=1)=9, zigzag(0)=0, zigzag(0)=0
        Assert.That(commands, Is.EqualTo(new uint[] { 9, 0, 0 }));
    }

    [Test]
    public void EncodePoint_NegativeCoords()
    {
        var commands = GeometryEncoder.EncodePoint(-10, -20);

        // zigzag(-10)=19, zigzag(-20)=39
        Assert.That(commands, Is.EqualTo(new uint[] { 9, 19, 39 }));
    }

    [Test]
    public void SignedArea_ClockwiseOnScreen_IsPositive()
    {
        // Y grows downward: (0,0) -> (100,0) -> (100,100) -> (0,100) is clockwise on screen
        TileCoord[] ring = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];

        Assert.That(GeometryEncoder.SignedArea(ring), Is.EqualTo(20000.0));
    }

    [Test]
    public void SignedArea_CounterClockwiseOnScreen_IsNegative()
    {
        TileCoord[] ring = [new(0, 0), new(0, 100), new(100, 100), new(100, 0)];

        Assert.That(GeometryEncoder.SignedArea(ring), Is.EqualTo(-20000.0));
    }

    [Test]
    public void Orient_Exterior_ReversesCounterClockwiseRing()
    {
        TileCoord[] ring = [new(0, 0), new(0, 100), new(100, 100), new(100, 0)];

        GeometryEncoder.Orient(ring, exterior: true);

        Assert.That(GeometryEncoder.SignedArea(ring), Is.Positive);
        Assert.That(ring, Is.EqualTo(new TileCoord[] { new(100, 0), new(100, 100), new(0, 100), new(0, 0) }));
    }

    [Test]
    public void Orient_Exterior_LeavesClockwiseRingUntouched()
    {
        TileCoord[] ring = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];
        var original = ring.ToArray();

        GeometryEncoder.Orient(ring, exterior: true);

        Assert.That(ring, Is.EqualTo(original));
    }

    [Test]
    public void Orient_Interior_ReversesClockwiseRing()
    {
        TileCoord[] ring = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];

        GeometryEncoder.Orient(ring, exterior: false);

        Assert.That(GeometryEncoder.SignedArea(ring), Is.Negative);
    }

    [Test]
    public void Orient_ZeroAreaRing_IsUnchanged()
    {
        TileCoord[] ring = [new(0, 0), new(50, 50), new(100, 100)];
        var original = ring.ToArray();

        GeometryEncoder.Orient(ring, exterior: true);

        Assert.That(ring, Is.EqualTo(original));
    }
}
