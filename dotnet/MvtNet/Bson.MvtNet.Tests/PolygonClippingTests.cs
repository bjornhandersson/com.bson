using VectorTile;

namespace Bson.MvtNet.Tests;

public class PolygonClippingTests
{
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    // 5% of the 4096 extent, matching TileMath.DefaultClipBufferFraction.
    private const int Buffer = 205;

    private static List<TileCoord[]> RingsOf(byte[] tileBytes)
    {
        var geometry = Tile.Parser.ParseFrom(tileBytes).Layers[0].Features[0].Geometry;
        return TestGeometry.DecodeRings(geometry);
    }

    [Test]
    public void AddPolygon_RingInsideTile_IsLeftAlone()
    {
        // A small Stockholm block sits well within the tile, so clipping must
        // not move or drop any of its vertices.
        var ring = new[]
        {
            (59.3290, 18.0430),
            (59.3290, 18.0450),
            (59.3270, 18.0450),
            (59.3270, 18.0430),
        };

        var bytes = new TileBuilder(Z, X, Y).Layer("blocks").AddPolygon(ring).Build();
        var rings = RingsOf(bytes);

        Assert.That(rings, Has.Count.EqualTo(1));
        Assert.That(rings[0], Has.Length.EqualTo(4));
        Assert.That(rings[0].All(p => p.X is >= 0 and <= 4096 && p.Y is >= 0 and <= 4096), Is.True);
    }

    [Test]
    public void AddPolygon_RingReachingThePoles_StaysWithinTheBuffer()
    {
        // This is the timezone case: a ring running pole to pole projects to
        // Mercator Y values far outside the tile. Unclipped it overflowed the
        // 16-bit vertex buffers renderers use, drawing wedges across the tile.
        var ring = new[] { (85.0, 17.0), (85.0, 19.0), (-85.0, 19.0), (-85.0, 17.0) };

        var bytes = new TileBuilder(Z, X, Y).Layer("zones").AddPolygon(ring).Build();
        var rings = RingsOf(bytes);

        Assert.That(rings, Has.Count.EqualTo(1));
        Assert.That(rings[0], Is.Not.Empty);

        var worst = rings[0].Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
        Assert.That(worst, Is.LessThanOrEqualTo(4096 + Buffer));
        Assert.That(worst, Is.LessThan(short.MaxValue));
    }

    [Test]
    public void AddPolygon_RingLargerThanTile_IsClippedToTilePlusBuffer()
    {
        var ring = new[] { (60.0, 17.0), (60.0, 19.0), (58.0, 19.0), (58.0, 17.0) };

        var bytes = new TileBuilder(Z, X, Y).Layer("zones").AddPolygon(ring).Build();
        var rings = RingsOf(bytes);

        Assert.That(rings, Has.Count.EqualTo(1));
        foreach (var p in rings[0])
        {
            Assert.That(p.X, Is.InRange(-Buffer, 4096 + Buffer));
            Assert.That(p.Y, Is.InRange(-Buffer, 4096 + Buffer));
        }
    }

    [Test]
    public void AddPolygon_ClippedRing_KeepsExteriorWindingOrder()
    {
        var ring = new[] { (60.0, 17.0), (60.0, 19.0), (58.0, 19.0), (58.0, 17.0) };

        var bytes = new TileBuilder(Z, X, Y).Layer("zones").AddPolygon(ring).Build();
        var rings = RingsOf(bytes);

        // MVT requires a positive signed area for exterior rings.
        Assert.That(GeometryEncoder.SignedArea(rings[0]), Is.GreaterThan(0));
    }

    [Test]
    public void AddPolygon_HoleOutsideTile_IsDropped()
    {
        var outer = new[]
        {
            (59.3300, 18.0400),
            (59.3300, 18.0500),
            (59.3200, 18.0500),
            (59.3200, 18.0400),
        };
        // Sitting in the Atlantic, nowhere near the Stockholm tile.
        var faraway = new List<List<(double Lat, double Lng)>>
        {
            new()
            {
                (0.0, -30.0),
                (0.0, -29.0),
                (-1.0, -29.0),
                (-1.0, -30.0),
            },
        };

        var bytes = new TileBuilder(Z, X, Y).Layer("zones").AddPolygon(outer, faraway).Build();
        var rings = RingsOf(bytes);

        Assert.That(rings, Has.Count.EqualTo(1), "the out-of-tile hole should be dropped");
    }

    [Test]
    public void AddPolygon_HoleInsideTile_IsKeptAndWoundAsInterior()
    {
        var outer = new[]
        {
            (59.3300, 18.0400),
            (59.3300, 18.0500),
            (59.3200, 18.0500),
            (59.3200, 18.0400),
        };
        var holes = new List<List<(double Lat, double Lng)>>
        {
            new()
            {
                (59.3280, 18.0430),
                (59.3280, 18.0470),
                (59.3220, 18.0470),
                (59.3220, 18.0430),
            },
        };

        var bytes = new TileBuilder(Z, X, Y).Layer("zones").AddPolygon(outer, holes).Build();
        var rings = RingsOf(bytes);

        Assert.That(rings, Has.Count.EqualTo(2));
        Assert.That(GeometryEncoder.SignedArea(rings[0]), Is.GreaterThan(0));
        Assert.That(GeometryEncoder.SignedArea(rings[1]), Is.LessThan(0));
    }

    [Test]
    public void AddPolygon_RingEntirelyOutsideTile_AddsNoFeature()
    {
        var ring = new[] { (0.0, -30.0), (0.0, -29.0), (-1.0, -29.0), (-1.0, -30.0) };

        var bytes = new TileBuilder(Z, X, Y).Layer("zones").AddPolygon(ring).Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        // The layer itself is created by calling Layer(); what matters is that
        // the out-of-tile ring contributes no feature.
        Assert.That(tile.Layers[0].Features, Is.Empty);
    }
}
