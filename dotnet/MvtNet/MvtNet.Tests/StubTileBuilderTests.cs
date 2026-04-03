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
