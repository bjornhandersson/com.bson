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
    public void DecodeParts_MultiPointExampleFromSpec_RecoversBothPoints()
    {
        var parts = TestGeometry.DecodeParts(new uint[] { 17, 10, 14, 3, 9 });

        Assert.That(parts, Has.Count.EqualTo(1));
        Assert.That(parts[0], Is.EqualTo(new TileCoord[] { new(5, 7), new(3, 2) }));
    }

    [Test]
    public void EncodeLineString_LineStringExampleFromSpec_MatchesExactly()
    {
        TileCoord[] line = [new(2, 2), new(2, 10), new(10, 10)];

        var encoded = GeometryEncoder.EncodeLineString(line);

        Assert.That(encoded, Is.EqualTo(new uint[] { 9, 4, 4, 18, 0, 16, 16, 0 }));
    }

    [Test]
    public void EncodeMultiLineString_MultiLineStringExampleFromSpec_MatchesExactly()
    {
        TileCoord[] first = [new(2, 2), new(2, 10), new(10, 10)];
        TileCoord[] second = [new(1, 1), new(3, 5)];

        var encoded = GeometryEncoder.EncodeMultiLineString([first, second]);

        Assert.That(encoded, Is.EqualTo(new uint[] { 9, 4, 4, 18, 0, 16, 16, 0, 9, 17, 17, 10, 4, 8 }));
    }

    [Test]
    public void EncodePolygon_PolygonExampleFromSpec_MatchesExactly()
    {
        TileCoord[] ring = [new(3, 6), new(8, 12), new(20, 34)];

        var encoded = GeometryEncoder.EncodePolygon(ring);

        Assert.That(encoded, Is.EqualTo(new uint[] { 9, 6, 12, 18, 10, 12, 24, 44, 15 }));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(-1)]
    [TestCase(25)]
    [TestCase(-9)]
    [TestCase(4095)]
    [TestCase(-4096)]
    [TestCase(int.MaxValue)]
    [TestCase(int.MinValue)]
    public void ZigZag_DecodesWithTheFormulaFromSpec(int value)
    {
        uint parameterInteger = GeometryEncoder.ZigZag(value);

        int decoded = (int)(parameterInteger >> 1) ^ -(int)(parameterInteger & 1);

        Assert.That(decoded, Is.EqualTo(value));
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
