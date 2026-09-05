using System.Text.Json;
using VectorTile;

namespace Bson.MvtNet.Tests;

public class GeoJsonReaderTests
{
    // Stockholm-area tile, same as TileBuilderTests
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    [Test]
    public void AddGeoJson_Point_AddsOneFeature()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                "properties": { "name": "Stockholm" }
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Point));
        Assert.That(tile.Layers[0].Keys, Has.Member("name"));
    }

    [Test]
    public void AddGeoJson_LineString_AddsOneFeature()
    {
        const string json = """
            {
                "type": "LineString",
                "coordinates": [[18.0300, 59.3340], [18.0649, 59.3326]]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Linestring));
    }

    [Test]
    public void AddGeoJson_Polygon_AddsOneFeature()
    {
        const string json = """
            {
                "type": "Polygon",
                "coordinates": [[
                    [18.0300, 59.3340],
                    [18.0649, 59.3326],
                    [18.0686, 59.3190],
                    [18.0300, 59.3340]
                ]]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
    }

    [Test]
    public void AddGeoJson_PolygonWithHole_EncodesBothRings()
    {
        const string json = """
            {
                "type": "Polygon",
                "coordinates": [
                    [[18.0300, 59.3400], [18.0700, 59.3400], [18.0700, 59.3200], [18.0300, 59.3200], [18.0300, 59.3400]],
                    [[18.0400, 59.3350], [18.0600, 59.3350], [18.0600, 59.3250], [18.0400, 59.3250], [18.0400, 59.3350]]
                ]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));
        var feature = tile.Layers[0].Features[0];

        // Two rings of 4 points each: 11 + 11 = 22 commands
        Assert.That(feature.Geometry, Has.Count.EqualTo(22));
    }

    [Test]
    public void AddGeoJson_MultiPoint_FlattensToManyFeatures()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": {
                    "type": "MultiPoint",
                    "coordinates": [
                        [18.0440866, 59.3281936],
                        [18.0649, 59.3326],
                        [18.0686, 59.3190]
                    ]
                },
                "properties": { "kind": "city" }
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(3));
        foreach (var f in tile.Layers[0].Features)
        {
            Assert.That(f.Type, Is.EqualTo(Tile.Types.GeomType.Point));
            // Each flattened feature carries the source feature's properties.
            Assert.That(f.Tags, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void AddGeoJson_MultiPolygon_FlattensToManyFeatures()
    {
        const string json = """
            {
                "type": "MultiPolygon",
                "coordinates": [
                    [[[18.0300, 59.3340], [18.0649, 59.3326], [18.0686, 59.3190], [18.0300, 59.3340]]],
                    [[[18.0400, 59.3300], [18.0500, 59.3300], [18.0500, 59.3250], [18.0400, 59.3300]]]
                ]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(2));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
        Assert.That(tile.Layers[0].Features[1].Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
    }

    [Test]
    public void AddGeoJson_GeometryCollection_FlattensMixedTypes()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": {
                    "type": "GeometryCollection",
                    "geometries": [
                        { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                        { "type": "LineString", "coordinates": [[18.0300, 59.3340], [18.0649, 59.3326]] }
                    ]
                },
                "properties": { "name": "mix" }
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(2));
        Assert.That(tile.Layers[0].Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Point));
        Assert.That(tile.Layers[0].Features[1].Type, Is.EqualTo(Tile.Types.GeomType.Linestring));
    }

    [Test]
    public void AddGeoJson_FeatureCollection_AddsAllFeatures()
    {
        const string json = """
            {
                "type": "FeatureCollection",
                "features": [
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                        "properties": { "name": "A" }
                    },
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0649, 59.3326] },
                        "properties": { "name": "B" }
                    }
                ]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(2));
        Assert.That(tile.Layers[0].Keys, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddGeoJson_ScalarProperties_BecomeTags()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                "properties": {
                    "name": "Stockholm",
                    "population": 975551,
                    "density": 5200.5,
                    "is_capital": true
                }
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));
        var keys = tile.Layers[0].Keys;
        var values = tile.Layers[0].Values;

        Assert.That(keys, Has.Member("name"));
        Assert.That(keys, Has.Member("population"));
        Assert.That(keys, Has.Member("density"));
        Assert.That(keys, Has.Member("is_capital"));
        Assert.That(values, Has.Count.EqualTo(4));
    }

    [Test]
    public void AddGeoJson_NestedProperties_AreDropped()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                "properties": {
                    "name": "Stockholm",
                    "metadata": { "source": "OSM" },
                    "tags": ["a", "b"]
                }
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Keys, Has.Count.EqualTo(1));
        Assert.That(tile.Layers[0].Keys[0], Is.EqualTo("name"));
    }

    [Test]
    public void AddGeoJson_SourceId_IsDropped()
    {
        const string json = """
            {
                "type": "Feature",
                "id": 42,
                "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                "properties": {}
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features[0].Id, Is.Not.EqualTo(42UL));
    }

    [Test]
    public void AddGeoJson_MalformedJson_IsSilentlySkipped()
    {
        var tile = Build(layer => layer.AddGeoJson("{ not json"));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void AddGeoJson_UnknownGeometryType_IsSilentlySkipped()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": { "type": "Tesseract", "coordinates": [1, 2, 3, 4] },
                "properties": {}
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void AddGeoJson_NullGeometry_IsSilentlySkipped()
    {
        const string json = """
            {
                "type": "Feature",
                "geometry": null,
                "properties": { "name": "ghost" }
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(0));
    }

    [Test]
    public void AddGeoJson_BadFeatureInCollection_DoesNotStopOthers()
    {
        const string json = """
            {
                "type": "FeatureCollection",
                "features": [
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                        "properties": { "name": "A" }
                    },
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": "not an array" },
                        "properties": { "name": "B" }
                    },
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0649, 59.3326] },
                        "properties": { "name": "C" }
                    }
                ]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(2));
    }

    [Test]
    public void AddGeoJson_JsonElementOverload_BehavesTheSame()
    {
        const string json = """
            {
                "type": "Point",
                "coordinates": [18.0440866, 59.3281936]
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var tile = Build(layer => layer.AddGeoJson(doc.RootElement));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddGeoJson_StreamOverload_BehavesTheSame()
    {
        const string json = """
            {
                "type": "Point",
                "coordinates": [18.0440866, 59.3281936]
            }
            """;

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var tile = Build(layer => layer.AddGeoJson(stream));

        Assert.That(tile.Layers[0].Features, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddGeoJson_FeaturesWithDifferentProperties_DoNotShareTags()
    {
        // The reader reuses one dictionary across a collection, so a feature
        // must not inherit tags from the feature before it. Property sets here
        // shrink and then go disjoint on purpose.
        const string json = """
            {
                "type": "FeatureCollection",
                "features": [
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] },
                        "properties": { "name": "A", "speed": 10 }
                    },
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0649, 59.3326] },
                        "properties": { "name": "B" }
                    },
                    {
                        "type": "Feature",
                        "geometry": { "type": "Point", "coordinates": [18.0500, 59.3300] },
                        "properties": { "depot": "north" }
                    }
                ]
            }
            """;

        var tile = Build(layer => layer.AddGeoJson(json));
        var layerMsg = tile.Layers[0];

        Assert.That(layerMsg.Features, Has.Count.EqualTo(3));
        Assert.That(TagsOf(layerMsg, 0), Is.EqualTo(new Dictionary<string, string>
        {
            ["name"] = "A",
            ["speed"] = "10",
        }));
        Assert.That(TagsOf(layerMsg, 1), Is.EqualTo(new Dictionary<string, string>
        {
            ["name"] = "B",
        }));
        Assert.That(TagsOf(layerMsg, 2), Is.EqualTo(new Dictionary<string, string>
        {
            ["depot"] = "north",
        }));
    }

    /// <summary>Resolves one feature's tag index pairs into key/value text.</summary>
    private static Dictionary<string, string> TagsOf(Tile.Types.Layer layer, int featureIndex)
    {
        var tags = layer.Features[featureIndex].Tags;
        var result = new Dictionary<string, string>();
        for (int i = 0; i + 1 < tags.Count; i += 2)
        {
            var value = layer.Values[(int)tags[i + 1]];
            result[layer.Keys[(int)tags[i]]] = value.HasStringValue
                ? value.StringValue
                : value.HasSintValue
                    ? value.SintValue.ToString()
                    : value.ToString();
        }

        return result;
    }

    private static Tile Build(Action<LayerBuilder> configure)
    {
        var builder = new TileBuilder(Z, X, Y);
        var layer = builder.Layer("test");
        configure(layer);
        return Tile.Parser.ParseFrom(builder.Build());
    }
}
