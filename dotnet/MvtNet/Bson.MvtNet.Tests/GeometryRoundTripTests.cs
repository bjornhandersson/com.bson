namespace Bson.MvtNet.Tests;

public class GeometryRoundTripTests
{
    private static readonly TileCoord[] Outer =
    {
        new(100, 100),
        new(200, 100),
        new(200, 200),
    };

    private static readonly TileCoord[] Hole =
    {
        new(120, 120),
        new(140, 120),
        new(140, 140),
    };

    [Test]
    public void SingleRing_RoundTripsToTheSameCoordinates()
    {
        var rings = TestGeometry.DecodeRings(GeometryEncoder.EncodePolygon(Outer));

        Assert.That(rings, Has.Count.EqualTo(1));
        Assert.That(rings[0], Is.EqualTo(Outer));
    }

    [Test]
    public void RingsAfterTheFirst_RoundTripToTheSameCoordinates()
    {
        var geometry = GeometryEncoder.EncodePolygon(new List<TileCoord[]> { Outer, Hole });
        var rings = TestGeometry.DecodeRings(geometry);

        Assert.That(rings, Has.Count.EqualTo(2));
        Assert.That(rings[0], Is.EqualTo(Outer));
        Assert.That(rings[1], Is.EqualTo(Hole), "the second ring must not be offset by the first");
    }

    [Test]
    public void ThreeRings_AllRoundTripToTheSameCoordinates()
    {
        var third = new[] { new TileCoord(160, 160), new TileCoord(180, 160), new TileCoord(180, 180) };
        var geometry = GeometryEncoder.EncodePolygon(new List<TileCoord[]> { Outer, Hole, third });

        var rings = TestGeometry.DecodeRings(geometry);

        Assert.That(rings, Has.Count.EqualTo(3));
        Assert.That(rings[0], Is.EqualTo(Outer));
        Assert.That(rings[1], Is.EqualTo(Hole));
        Assert.That(rings[2], Is.EqualTo(third));
    }
}
