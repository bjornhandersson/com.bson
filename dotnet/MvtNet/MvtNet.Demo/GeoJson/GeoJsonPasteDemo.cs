using System.Text.Json;
using Bson.MvtNet;

namespace MvtNet.Demo;

public static class GeoJsonPasteDemo
{
    private static (JsonDocument Doc, double[]? Bounds, int Features)? _current;

    public static void Map(WebApplication app)
    {
        app.MapPost(
            "/geojson",
            async (HttpRequest req) =>
            {
                using var reader = new StreamReader(req.Body);
                var text = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return Results.BadRequest(new { error = "empty body" });
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(text);
                }
                catch (JsonException ex)
                {
                    return Results.BadRequest(new { error = ex.Message });
                }

                var bounds = ComputeBounds(doc.RootElement);
                var features = CountFeatures(doc.RootElement);

                _current?.Doc.Dispose();
                _current = (doc, bounds, features);

                return Results.Ok(new { bounds, features });
            }
        );

        app.MapGet(
            "/tiles/geojson/{z:int}/{x:int}/{y:int}",
            (int z, int x, int y) =>
            {
                var tile = new TileBuilder(z, x, y);
                if (_current is { } cur)
                {
                    tile.Layer("geojson").AddGeoJson(cur.Doc.RootElement);
                }
                return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
            }
        );
    }

    private static double[]? ComputeBounds(JsonElement root)
    {
        var b = new BoundsAccumulator();
        WalkForCoordinates(root, b);
        if (!b.Any)
        {
            return null;
        }
        return new[] { b.MinLng, b.MinLat, b.MaxLng, b.MaxLat };
    }

    private static int CountFeatures(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }
        if (
            !root.TryGetProperty("type", out var typeEl)
            || typeEl.ValueKind != JsonValueKind.String
        )
        {
            return 0;
        }
        if (
            typeEl.GetString() == "FeatureCollection"
            && root.TryGetProperty("features", out var feats)
            && feats.ValueKind == JsonValueKind.Array
        )
        {
            return feats.GetArrayLength();
        }
        return 1;
    }

    private static void WalkForCoordinates(JsonElement el, BoundsAccumulator b)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (el.TryGetProperty("coordinates", out var coords))
        {
            ConsumeCoords(coords, b);
        }

        if (el.TryGetProperty("features", out var feats) && feats.ValueKind == JsonValueKind.Array)
        {
            foreach (var f in feats.EnumerateArray())
            {
                WalkForCoordinates(f, b);
            }
        }

        if (el.TryGetProperty("geometry", out var g))
        {
            WalkForCoordinates(g, b);
        }

        if (el.TryGetProperty("geometries", out var gs) && gs.ValueKind == JsonValueKind.Array)
        {
            foreach (var geom in gs.EnumerateArray())
            {
                WalkForCoordinates(geom, b);
            }
        }
    }

    private static void ConsumeCoords(JsonElement el, BoundsAccumulator b)
    {
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() == 0)
        {
            return;
        }

        var first = el[0];
        if (first.ValueKind == JsonValueKind.Number && el.GetArrayLength() >= 2)
        {
            var second = el[1];
            if (second.ValueKind != JsonValueKind.Number)
            {
                return;
            }
            b.Add(first.GetDouble(), second.GetDouble());
            return;
        }

        foreach (var inner in el.EnumerateArray())
        {
            ConsumeCoords(inner, b);
        }
    }

    private sealed class BoundsAccumulator
    {
        public double MinLng = double.MaxValue;
        public double MaxLng = double.MinValue;
        public double MinLat = double.MaxValue;
        public double MaxLat = double.MinValue;
        public bool Any;

        public void Add(double lng, double lat)
        {
            if (lng < MinLng)
            {
                MinLng = lng;
            }
            if (lng > MaxLng)
            {
                MaxLng = lng;
            }
            if (lat < MinLat)
            {
                MinLat = lat;
            }
            if (lat > MaxLat)
            {
                MaxLat = lat;
            }
            Any = true;
        }
    }
}
