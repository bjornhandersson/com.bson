using VectorTile;

namespace MvtNet.Tests;

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
