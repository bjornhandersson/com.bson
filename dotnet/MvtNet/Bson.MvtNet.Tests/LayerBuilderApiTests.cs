using System.Text.Json;
using VectorTile;

namespace Bson.MvtNet.Tests;

/// <summary>
/// Behaviour added in 1.1: caller-supplied ids, nullable and typed attribute
/// dictionaries, the point buffer, single-feature multi-part lines and strict
/// GeoJSON parsing.
/// </summary>
public class LayerBuilderApiTests
{
    // Stockholm-area tile, same as TileBuilderTests
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    private const double Lat = 59.3281936;
    private const double Lng = 18.0440866;

    private static Tile Parse(TileBuilder builder) => Tile.Parser.ParseFrom(builder.Build());

    [Test]
    public void FeatureIds_AutoIncrementPerLayer()
    {
        var builder = new TileBuilder(Z, X, Y);
        builder.Layer("a").AddPoint(Lat, Lng).AddPoint(Lat, Lng);
        builder.Layer("b").AddPoint(Lat, Lng);

        var tile = Parse(builder);

        Assert.That(tile.Layers[0].Features.Select(f => f.Id), Is.EqualTo(new ulong[] { 1, 2 }));
        Assert.That(tile.Layers[1].Features[0].Id, Is.EqualTo(1UL));
    }

    [Test]
    public void FeatureIds_CallerSupplied_AreUsedForEveryGeometryType()
    {
        var ring = new (double, double)[] { (59.34, 18.03), (59.33, 18.06), (59.32, 18.04) };
        var hole = new (double, double)[] { (59.332, 18.042), (59.331, 18.046), (59.330, 18.043) };

        var builder = new TileBuilder(Z, X, Y);
        builder
            .Layer("mixed")
            .AddPoint(Lat, Lng, id: 42)
            .AddPoint(Lat, Lng, new Dictionary<string, object> { ["n"] = 1 }, id: 43)
            .AddLineString(new (double, double)[] { (59.334, 18.03), (59.3326, 18.0649) }, id: 44)
            .AddLineString(new (double, double)[] { (59.334, 18.03), (59.3326, 18.0649) }, new Dictionary<string, object> { ["n"] = 1 }, id: 45)
            .AddPolygon(ring, id: 46)
            .AddPolygon(ring, new Dictionary<string, object> { ["n"] = 1 }, id: 47)
            .AddPolygon(ring, new[] { hole }, id: 48)
            .AddPolygon(ring, new[] { hole }, new Dictionary<string, object> { ["n"] = 1 }, id: 49);

        var ids = Parse(builder).Layers[0].Features.Select(f => f.Id).ToArray();

        Assert.That(ids, Is.EqualTo(new ulong[] { 42, 43, 44, 45, 46, 47, 48, 49 }));
    }

    [Test]
    public void Attributes_NullableDictionary_CompilesWithoutWarningsAndDropsNulls()
    {
        double? speed = null;
        var attrs = new Dictionary<string, object?> { ["speed"] = speed, ["name"] = "x" };

        var builder = new TileBuilder(Z, X, Y);
        builder.Layer("a").AddPoint(Lat, Lng, attrs);

        var layer = Parse(builder).Layers[0];

        Assert.That(layer.Keys, Is.EqualTo(new[] { "name" }));
        Assert.That(layer.Features[0].Tags, Is.EqualTo(new uint[] { 0, 0 }));
    }

    [Test]
    public void Attributes_TypedDictionaries_AreAccepted()
    {
        var builder = new TileBuilder(Z, X, Y);
        builder
            .Layer("a")
            .AddPoint(Lat, Lng, new Dictionary<string, string> { ["name"] = "Stockholm" })
            .AddPoint(Lat, Lng, new Dictionary<string, int> { ["rank"] = 1 })
            .AddPoint(Lat, Lng, new[] { new KeyValuePair<string, object>("ok", true) });

        var layer = Parse(builder).Layers[0];

        Assert.That(layer.Keys, Is.EqualTo(new[] { "name", "rank", "ok" }));
        Assert.That(layer.Values[0].StringValue, Is.EqualTo("Stockholm"));
        Assert.That(layer.Values[1].SintValue, Is.EqualTo(1));
        Assert.That(layer.Values[2].BoolValue, Is.True);
    }

    [Test]
    public void Attributes_UnsupportedType_ThrowsFromAddPoint()
    {
        var builder = new TileBuilder(Z, X, Y);

        Assert.Throws<ArgumentException>(() =>
            builder.Layer("a").AddPoint(Lat, Lng, new Dictionary<string, object> { ["t"] = TimeSpan.Zero })
        );
    }

    [Test]
    public void AddPoint_JustOutsideTile_IsKeptWithinBuffer()
    {
        var bounds = TileMath.GetTileBounds(Z, X, Y);
        double lngSpan = bounds.East - bounds.West;
        double midLat = (bounds.North + bounds.South) / 2;

        var builder = new TileBuilder(Z, X, Y);
        builder
            .Layer("p")
            .AddPoint(midLat, bounds.East + lngSpan * 0.02) // 2% past the east edge: inside the 5% buffer
            .AddPoint(midLat, bounds.East + lngSpan * 0.10); // 10% past: dropped

        var features = Parse(builder).Layers[0].Features;

        Assert.That(features, Has.Count.EqualTo(1));
        var x = TestGeometry.DecodeParts(features[0].Geometry)[0][0].X;
        Assert.That(x, Is.GreaterThan(4096).And.LessThanOrEqualTo(4096 + 4096 * 0.05));
    }

    [Test]
    public void AddPoint_AtPole_IsDropped()
    {
        var builder = new TileBuilder(0, 0, 0);
        builder.Layer("p").AddPoint(90, 0).AddPoint(double.NaN, 0).AddPoint(-90, 0);

        Assert.That(Parse(builder).Layers[0].Features, Is.Empty);
    }

    [Test]
    public void AddLineString_LeavingAndReenteringTile_IsOneMultiLineStringFeature()
    {
        var bounds = TileMath.GetTileBounds(Z, X, Y);
        double midLat = (bounds.North + bounds.South) / 2;
        double lngSpan = bounds.East - bounds.West;

        // In, out to the north, back in: a "U" over the top edge.
        var line = new (double, double)[]
        {
            (midLat, bounds.West + lngSpan * 0.2),
            (bounds.North + 1.0, bounds.West + lngSpan * 0.3),
            (bounds.North + 1.0, bounds.West + lngSpan * 0.7),
            (midLat, bounds.West + lngSpan * 0.8),
        };

        var builder = new TileBuilder(Z, X, Y);
        builder.Layer("l").AddLineString(line, new Dictionary<string, object> { ["name"] = "u" }, id: 7);

        var layer = Parse(builder).Layers[0];

        Assert.That(layer.Features, Has.Count.EqualTo(1));
        Assert.That(layer.Features[0].Id, Is.EqualTo(7UL));
        Assert.That(layer.Features[0].Tags, Is.EqualTo(new uint[] { 0, 0 }));

        var parts = TestGeometry.DecodeParts(layer.Features[0].Geometry);
        Assert.That(parts, Has.Count.EqualTo(2));
        Assert.That(parts.All(p => p.Length >= 2), Is.True);
    }

    [Test]
    public void AddGeoJson_Lenient_SwallowsBadJson()
    {
        var builder = new TileBuilder(Z, X, Y);
        builder.Layer("g").AddGeoJson("{ not json");

        Assert.That(Parse(builder).Layers[0].Features, Is.Empty);
    }

    [Test]
    public void AddGeoJson_Strict_ThrowsOnBadJson()
    {
        var layer = new TileBuilder(Z, X, Y).Layer("g");

        Assert.Catch<JsonException>(() => layer.AddGeoJson("{ not json", strict: true));
    }

    [TestCase("""{ "type": "Feature", "properties": {} }""", "geometry")]
    [TestCase("""{ "type": "Feature", "geometry": { "type": "Point" } }""", "coordinates")]
    [TestCase("""{ "type": "Feature", "geometry": { "type": "Point", "coordinates": ["a", "b"] } }""", "numbers")]
    [TestCase("""{ "type": "Feature", "geometry": { "type": "Blob", "coordinates": [] } }""", "Blob")]
    [TestCase("""{ "type": "FeatureCollection", "features": [ { "type": "Feature", "geometry": { "type": "LineString", "coordinates": [[18.0, 59.3]] } } ] }""", "LineString")]
    [TestCase("""{ "type": "Polygon", "coordinates": [[[18.0, 59.3], [18.1, 59.3], [18.0, 59.3]]] }""", "ring")]
    public void AddGeoJson_Strict_ThrowsFormatExceptionDescribingProblem(string json, string expectedWord)
    {
        var layer = new TileBuilder(Z, X, Y).Layer("g");

        var ex = Assert.Throws<FormatException>(() => layer.AddGeoJson(json, strict: true));
        Assert.That(ex!.Message, Does.Contain(expectedWord));
    }

    [Test]
    public void AddGeoJson_Strict_AcceptsNullGeometryAndValidInput()
    {
        const string json = """
            {
                "type": "FeatureCollection",
                "features": [
                    { "type": "Feature", "geometry": null, "properties": { "name": "nowhere" } },
                    { "type": "Feature", "geometry": { "type": "Point", "coordinates": [18.0440866, 59.3281936] }, "properties": { "name": "Stockholm" } }
                ]
            }
            """;

        var builder = new TileBuilder(Z, X, Y);
        builder.Layer("g").AddGeoJson(json, strict: true);

        Assert.That(Parse(builder).Layers[0].Features, Has.Count.EqualTo(1));
    }

    [Test]
    public void AddGeoJson_Strict_FromStreamAndElement()
    {
        const string json = """{ "type": "Point", "coordinates": [18.0440866, 59.3281936] }""";

        var builder = new TileBuilder(Z, X, Y);
        using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        using var doc = JsonDocument.Parse(json);
        builder.Layer("g").AddGeoJson(ms, strict: true).AddGeoJson(doc.RootElement, strict: true);

        Assert.That(Parse(builder).Layers[0].Features, Has.Count.EqualTo(2));
        Assert.Throws<FormatException>(() =>
            builder.Layer("g").AddGeoJson(JsonDocument.Parse("[1,2]").RootElement, strict: true)
        );
    }
}
