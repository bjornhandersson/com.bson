using VectorTile;

namespace MvtNet.Tests;

public class TileBuilderTests
{
    // All Stockholm-area tests use z12 tile 2253/1204
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    [Test]
    public void Build_Point_ProducesValidTile()
    {
        var builder = new TileBuilder(Z, X, Y);
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
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));

        var feature = tile.Layers[0].Features[0];
        Assert.That(feature.Type, Is.EqualTo(Tile.Types.GeomType.Point));
        Assert.That(feature.Geometry, Has.Count.EqualTo(3)); // MoveTo + x + y
        Assert.That(tile.Layers[0].Keys[0], Is.EqualTo("name"));
        Assert.That(tile.Layers[0].Values[0].StringValue, Is.EqualTo("Stockholm"));
    }

    [Test]
    public void Build_LineString_ProducesValidTile()
    {
        var builder = new TileBuilder(Z, X, Y);
        var layer = builder.Layer("tracks");

        // A walk from Norrmalm → Stockholm center → Östermalm
        (double Lat, double Lng)[] route =
        [
            (59.3340, 18.0300),
            (59.3281936, 18.0440866),
            (59.3326, 18.0649),
        ];

        bool added = layer.AddLineString(
            route,
            new Dictionary<string, object> { ["name"] = "Walk", ["distance"] = 2.5 }
        );

        Assert.That(added, Is.True);

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Name, Is.EqualTo("tracks"));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));

        var feature = tile.Layers[0].Features[0];
        Assert.That(feature.Type, Is.EqualTo(Tile.Types.GeomType.Linestring));
        // MoveTo(1) + x + y + LineTo(count=2) + dx + dy + dx + dy = 8 commands
        Assert.That(feature.Geometry, Has.Count.EqualTo(8));
        Assert.That(feature.Tags, Has.Count.EqualTo(4)); // 2 key-value pairs
        Assert.That(tile.Layers[0].Keys[0], Is.EqualTo("name"));
        Assert.That(tile.Layers[0].Keys[1], Is.EqualTo("distance"));
    }

    [Test]
    public void Build_Polygon_ProducesValidTile()
    {
        var builder = new TileBuilder(Z, X, Y);
        var layer = builder.Layer("geofences");

        // Triangle: Norrmalm → Östermalm → Södermalm (ClosePath closes it)
        (double Lat, double Lng)[] ring =
        [
            (59.3340, 18.0300),
            (59.3326, 18.0649),
            (59.3190, 18.0686),
        ];

        bool added = layer.AddPolygon(
            ring,
            new Dictionary<string, object> { ["name"] = "Central Stockholm", ["restricted"] = true }
        );

        Assert.That(added, Is.True);

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Name, Is.EqualTo("geofences"));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));

        var feature = tile.Layers[0].Features[0];
        Assert.That(feature.Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
        // MoveTo(1) + x + y + LineTo(count=2) + dx + dy + dx + dy + ClosePath(1) = 9 commands
        Assert.That(feature.Geometry, Has.Count.EqualTo(9));
        Assert.That(feature.Tags, Has.Count.EqualTo(4));
        Assert.That(tile.Layers[0].Keys[1], Is.EqualTo("restricted"));
        Assert.That(tile.Layers[0].Values[1].BoolValue, Is.True);
    }

    [Test]
    public void Build_MultipleFeatureTypes_InSeparateLayers()
    {
        var builder = new TileBuilder(Z, X, Y);

        // Point layer
        var points = builder.Layer("points");
        points.AddPoint(59.3281936, 18.0440866, new Dictionary<string, object> { ["name"] = "Stockholm" });

        // LineString layer
        var tracks = builder.Layer("tracks");
        tracks.AddLineString(
            new (double, double)[] { (59.3340, 18.0300), (59.3326, 18.0649) },
            new Dictionary<string, object> { ["name"] = "Route A" }
        );

        // Polygon layer
        var geofences = builder.Layer("geofences");
        geofences.AddPolygon(
            new (double, double)[] { (59.3340, 18.0300), (59.3326, 18.0649), (59.3190, 18.0686) },
            new Dictionary<string, object> { ["name"] = "Zone 1" }
        );

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(3));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Point));
        Assert.That(tile.Layers[1].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Linestring));
        Assert.That(tile.Layers[2].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
    }

    [Test]
    public void Build_MultipleFeaturesInSameLayer()
    {
        var builder = new TileBuilder(Z, X, Y);
        var layer = builder.Layer("points");

        layer.AddPoint(59.3281936, 18.0440866, new Dictionary<string, object> { ["name"] = "Stockholm" });
        layer.AddPoint(59.3326, 18.0649, new Dictionary<string, object> { ["name"] = "Östermalm" });
        layer.AddPoint(59.3190, 18.0686, new Dictionary<string, object> { ["name"] = "Södermalm" });

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(3));

        // Keys should be deduplicated — only one "name" key
        Assert.That(tile.Layers[0].Keys, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Values, Has.Count.EqualTo(3));
    }

    [Test]
    public void Build_PointOutsideTile_NotAdded()
    {
        var builder = new TileBuilder(10, 0, 0);
        var layer = builder.Layer("points");
        bool added = layer.AddPoint(59.3281936, 18.0440866);

        Assert.That(added, Is.False);

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void Build_LineStringTooFewPointsInTile_NotAdded()
    {
        var builder = new TileBuilder(10, 0, 0);
        var layer = builder.Layer("tracks");

        // Both points outside tile 0/0 at z10
        bool added = layer.AddLineString(
            new (double, double)[] { (59.3340, 18.0300), (59.3326, 18.0649) }
        );

        Assert.That(added, Is.False);
    }

    [Test]
    public void Build_PolygonTooFewPointsInTile_NotAdded()
    {
        var builder = new TileBuilder(10, 0, 0);
        var layer = builder.Layer("geofences");

        // All points outside tile 0/0 at z10
        bool added = layer.AddPolygon(
            new (double, double)[] { (59.3340, 18.0300), (59.3326, 18.0649), (59.3190, 18.0686) }
        );

        Assert.That(added, Is.False);
    }
}
