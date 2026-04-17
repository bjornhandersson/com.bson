using VectorTile;

namespace Bson.MvtNet.Tests;

public class TileBuilderTests
{
    // All Stockholm-area tests use z12 tile 2253/1204
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    [Test]
    public void Build_Point_ProducesValidTile()
    {
        var bytes = new TileBuilder(Z, X, Y)
            .Layer("points")
            .AddPoint(
                59.3281936,
                18.0440866,
                new Dictionary<string, object> { ["name"] = "Stockholm" }
            )
            .Build();

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

        // A walk from Norrmalm → Stockholm center → Östermalm
        builder
            .Layer("tracks")
            .AddLineString(
                new (double, double)[]
                {
                    (59.3340, 18.0300),
                    (59.3281936, 18.0440866),
                    (59.3326, 18.0649),
                },
                new Dictionary<string, object> { ["name"] = "Walk", ["distance"] = 2.5 }
            );

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

        // Triangle: Norrmalm → Östermalm → Södermalm (ClosePath closes it)
        builder
            .Layer("geofences")
            .AddPolygon(
                new (double, double)[]
                {
                    (59.3340, 18.0300),
                    (59.3326, 18.0649),
                    (59.3190, 18.0686),
                },
                new Dictionary<string, object>
                {
                    ["name"] = "Central Stockholm",
                    ["restricted"] = true,
                }
            );

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

        builder
            .Layer("points")
            .AddPoint(
                59.3281936,
                18.0440866,
                new Dictionary<string, object> { ["name"] = "Stockholm" }
            );

        builder
            .Layer("tracks")
            .AddLineString(
                new (double, double)[] { (59.3340, 18.0300), (59.3326, 18.0649) },
                new Dictionary<string, object> { ["name"] = "Route A" }
            );

        builder
            .Layer("geofences")
            .AddPolygon(
                new (double, double)[]
                {
                    (59.3340, 18.0300),
                    (59.3326, 18.0649),
                    (59.3190, 18.0686),
                },
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

        builder
            .Layer("points")
            .AddPoint(
                59.3281936,
                18.0440866,
                new Dictionary<string, object> { ["name"] = "Stockholm" }
            )
            .AddPoint(59.3326, 18.0649, new Dictionary<string, object> { ["name"] = "Östermalm" })
            .AddPoint(59.3190, 18.0686, new Dictionary<string, object> { ["name"] = "Södermalm" });

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
        builder.Layer("points").AddPoint(59.3281936, 18.0440866);

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void Build_LineStringOutsideTile_NotAdded()
    {
        var builder = new TileBuilder(10, 0, 0);
        builder
            .Layer("tracks")
            .AddLineString(new (double, double)[] { (59.3340, 18.0300), (59.3326, 18.0649) });

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void Build_PolygonOutsideTile_NotAdded()
    {
        var builder = new TileBuilder(10, 0, 0);
        builder
            .Layer("geofences")
            .AddPolygon(
                new (double, double)[]
                {
                    (59.3340, 18.0300),
                    (59.3326, 18.0649),
                    (59.3190, 18.0686),
                }
            );

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void Build_EmptyTile_ProducesValidBytes()
    {
        var builder = new TileBuilder(Z, X, Y);
        builder.Layer("empty");

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void Build_FluentChaining_ProducesValidTile()
    {
        var bytes = new TileBuilder(Z, X, Y)
            .Layer("pois")
            .AddPoint(59.3281936, 18.0440866)
            .AddPoint(59.3326, 18.0649)
            .AddPoint(59.3190, 18.0686)
            .Build();

        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(3));
    }

    [Test]
    public void Build_LineStringCrossingTileBoundary_Rendered()
    {
        // z14 tile 9013/4818 contains Stockholm center
        // The line extends well beyond the tile in both directions
        int z = 14;
        int x = 9013;
        int y = 4818;

        var builder = new TileBuilder(z, x, y);

        builder
            .Layer("tracks")
            .AddLineString(
                new (double, double)[]
                {
                    (59.34, 17.90), // west of tile
                    (59.32, 18.20), // east of tile
                }
            );

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        // The line overlaps the tile so it should be included
        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));

        var geom = tile.Layers[0].Features[0].Geometry;
        Assert.That(geom, Has.Count.GreaterThan(0));
    }

    [Test]
    public void Build_PolygonCrossingTileBoundary_Rendered()
    {
        int z = 14;
        int x = 9013;
        int y = 4818;

        var builder = new TileBuilder(z, x, y);

        // Polygon larger than the tile
        builder
            .Layer("zones")
            .AddPolygon(
                new (double, double)[]
                {
                    (59.35, 17.90),
                    (59.35, 18.20),
                    (59.30, 18.20),
                    (59.30, 17.90),
                }
            );

        var bytes = builder.Build();
        var tile = Tile.Parser.ParseFrom(bytes);

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
    }
}
