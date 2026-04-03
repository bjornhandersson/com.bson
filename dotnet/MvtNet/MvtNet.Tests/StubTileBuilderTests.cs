using VectorTile;

namespace MvtNet.Tests;

public class StubTileBuilderTests
{
    [Test]
    public void BuildXTile_ReturnsValidMvtBytes()
    {
        var bytes = StubTileBuilder.BuildXTile();

        Assert.That(bytes, Is.Not.Null);
        Assert.That(bytes.Length, Is.GreaterThan(0));

        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Name, Is.EqualTo("stub"));
        Assert.That(tile.Layers[0].Version, Is.EqualTo(2));
        Assert.That(tile.Layers[0].Extent, Is.EqualTo(4096));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(2));

        foreach (var feature in tile.Layers[0].Features)
        {
            Assert.That(feature.Type, Is.EqualTo(Tile.Types.GeomType.Linestring));
        }
    }
}

public class ZigZagTests
{
    [TestCase(0, ExpectedResult = 0u)]
    [TestCase(-1, ExpectedResult = 1u)]
    [TestCase(1, ExpectedResult = 2u)]
    [TestCase(-2, ExpectedResult = 3u)]
    [TestCase(2, ExpectedResult = 4u)]
    [TestCase(int.MaxValue, ExpectedResult = (uint)int.MaxValue * 2)]
    public uint ZigZag_EncodesCorrectly(int value)
    {
        return GeometryEncoder.ZigZag(value);
    }
}

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

public class TileMathTests
{
    [Test]
    public void GetTileBounds_Zoom0_CoversWorld()
    {
        var bounds = TileMath.GetTileBounds(0, 0, 0);

        Assert.That(bounds.West, Is.EqualTo(-180.0).Within(0.001));
        Assert.That(bounds.East, Is.EqualTo(180.0).Within(0.001));
        Assert.That(bounds.North, Is.EqualTo(85.051).Within(0.01));
        Assert.That(bounds.South, Is.EqualTo(-85.051).Within(0.01));
    }

    [Test]
    public void ProjectPoint_StockholmInCorrectTile()
    {
        double lat = 59.3281936;
        double lng = 18.0440866;
        int z = 10;

        // Stockholm at z10 should be in tile 563/301 (standard slippy map)
        var coord = TileMath.ProjectPoint(lat, lng, z, 563, 301);

        Assert.That(coord, Is.Not.Null);
        Assert.That(coord!.Value.X, Is.InRange(0, 4096));
        Assert.That(coord!.Value.Y, Is.InRange(0, 4096));
    }

    [Test]
    public void ProjectPoint_OutsideTile_ReturnsNull()
    {
        // Stockholm should not be in tile 0/0 at z10
        var coord = TileMath.ProjectPoint(59.3281936, 18.0440866, 10, 0, 0);

        Assert.That(coord, Is.Null);
    }

    [Test]
    public void Contains_StockholmInCorrectTile()
    {
        Assert.That(TileMath.Contains(59.3281936, 18.0440866, 10, 563, 301), Is.True);
        Assert.That(TileMath.Contains(59.3281936, 18.0440866, 10, 0, 0), Is.False);
    }
}

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

public class TagEncoderTests
{
    [Test]
    public void Encode_ProducesCorrectIndexPairs()
    {
        var encoder = new TagEncoder();
        var tags = encoder.Encode(
            new Dictionary<string, object> { ["name"] = "Stockholm", ["population"] = 1000000L }
        );

        // key 0 = "name", value 0 = "Stockholm"
        // key 1 = "population", value 1 = 1000000
        Assert.That(tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(encoder.Keys, Has.Count.EqualTo(2));
        Assert.That(encoder.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_DeduplicatesKeysAndValues()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["name"] = "A" });
        var tags = encoder.Encode(new Dictionary<string, object> { ["name"] = "B" });

        // "name" key is reused (index 0), "B" is new value (index 1)
        Assert.That(tags, Is.EqualTo(new uint[] { 0, 1 }));
        Assert.That(encoder.Keys, Has.Count.EqualTo(1));
        Assert.That(encoder.Values, Has.Count.EqualTo(2));
    }
}

public class TileBuilderTests
{
    [Test]
    public void Build_StockholmPoint_ProducesValidTile()
    {
        var builder = new TileBuilder(10, 563, 301);
        var layer = builder.Layer("points");
        bool added = layer.AddPoint(
            59.3281936,
            18.0440866,
            new Dictionary<string, object> { ["name"] = "Stockholm" }
        );

        Assert.That(added, Is.True);

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Name, Is.EqualTo("points"));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));

        var feature = tile.Layers[0].Features[0];
        Assert.That(feature.Type, Is.EqualTo(Tile.Types.GeomType.Point));
        Assert.That(feature.Tags, Has.Count.EqualTo(2)); // key index + value index
        Assert.That(tile.Layers[0].Keys[0], Is.EqualTo("name"));
        Assert.That(tile.Layers[0].Values[0].StringValue, Is.EqualTo("Stockholm"));
    }

    [Test]
    public void Build_PointOutsideTile_EmptyLayer()
    {
        var builder = new TileBuilder(10, 0, 0);
        var layer = builder.Layer("points");
        bool added = layer.AddPoint(59.3281936, 18.0440866);

        Assert.That(added, Is.False);

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }
}
