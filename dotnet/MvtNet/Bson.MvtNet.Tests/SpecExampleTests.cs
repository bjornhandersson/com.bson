namespace Bson.MvtNet.Tests;

public class SpecExampleTests
{
    private static readonly TileCoord[] FirstExterior =
    {
        new(0, 0), new(10, 0), new(10, 10), new(0, 10),
    };

    private static readonly TileCoord[] SecondExterior =
    {
        new(11, 11), new(20, 11), new(20, 20), new(11, 20),
    };

    private static readonly TileCoord[] SecondInterior =
    {
        new(13, 13), new(13, 17), new(17, 17), new(17, 13),
    };

    private static readonly uint[] SpecEncoding =
    {
        9, 0, 0, 26, 20, 0, 0, 20, 19, 0, 15,
        9, 22, 2, 26, 18, 0, 0, 18, 17, 0, 15,
        9, 4, 13, 26, 0, 8, 8, 0, 0, 7, 15,
    };

    [Test]
    public void EncodePolygon_MultiPolygonExampleFromSpec_MatchesExactly()
    {
        var encoded = GeometryEncoder.EncodePolygon(new[] { FirstExterior, SecondExterior, SecondInterior });

        Assert.That(encoded, Is.EqualTo(SpecEncoding));
    }

    [Test]
    public void DecodeRings_MultiPolygonExampleFromSpec_RecoversEveryRing()
    {
        var rings = TestGeometry.DecodeRings(SpecEncoding);

        Assert.That(rings, Is.EqualTo(new[] { FirstExterior, SecondExterior, SecondInterior }));
    }

    [Test]
    public void EncodePolygon_HoleAfterClosePath_IsRelativeToLastVertexNotRingStart()
    {
        var outer = new TileCoord[] { new(0, 0), new(100, 0), new(100, 100), new(0, 100) };
        var hole = new TileCoord[] { new(10, 10), new(10, 20), new(20, 20), new(20, 10) };

        var rings = TestGeometry.DecodeRings(GeometryEncoder.EncodePolygon(new[] { outer, hole }));

        Assert.That(rings[1], Is.EqualTo(hole));
    }
}
