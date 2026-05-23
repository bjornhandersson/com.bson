using System.Text.Json;

namespace Bson.MvtNet;

/// <summary>
/// Walks a parsed GeoJSON document and dispatches geometries to a LayerBuilder.
/// FeatureCollection / Feature / bare Geometry are all accepted. Multi-geometries
/// and GeometryCollection are flattened to N features sharing the source
/// feature's tags. Source feature ids and nested properties are dropped.
/// Malformed or unknown input is silently skipped.
/// </summary>
internal static class GeoJsonReader
{
    public static void Read(LayerBuilder layer, JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (
            !root.TryGetProperty("type", out var typeEl)
            || typeEl.ValueKind != JsonValueKind.String
        )
        {
            return;
        }

        switch (typeEl.GetString())
        {
            case "FeatureCollection":
                ReadFeatureCollection(layer, root);
                return;
            case "Feature":
                ReadFeature(layer, root);
                return;
            default:
                ReadGeometry(layer, root, null);
                return;
        }
    }

    private static void ReadFeatureCollection(LayerBuilder layer, JsonElement el)
    {
        if (!el.TryGetProperty("features", out var feats) || feats.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var feature in feats.EnumerateArray())
        {
            try
            {
                ReadFeature(layer, feature);
            }
            catch
            {
                // skip a malformed feature, keep going
            }
        }
    }

    private static void ReadFeature(LayerBuilder layer, JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!el.TryGetProperty("geometry", out var geom))
        {
            return;
        }

        Dictionary<string, object>? attrs = null;
        if (
            el.TryGetProperty("properties", out var props)
            && props.ValueKind == JsonValueKind.Object
        )
        {
            attrs = ExtractScalarProperties(props);
        }

        ReadGeometry(layer, geom, attrs);
    }

    private static void ReadGeometry(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (!el.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return;
        }

        switch (typeEl.GetString())
        {
            case "Point":
                ReadPoint(layer, el, attrs);
                return;
            case "MultiPoint":
                ReadMultiPoint(layer, el, attrs);
                return;
            case "LineString":
                ReadLineString(layer, el, attrs);
                return;
            case "MultiLineString":
                ReadMultiLineString(layer, el, attrs);
                return;
            case "Polygon":
                ReadPolygon(layer, el, attrs);
                return;
            case "MultiPolygon":
                ReadMultiPolygon(layer, el, attrs);
                return;
            case "GeometryCollection":
                ReadGeometryCollection(layer, el, attrs);
                return;
        }
    }

    private static void ReadPoint(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (!TryGetCoordinates(el, out var coords))
        {
            return;
        }

        if (TryReadCoord(coords, out var lat, out var lng))
        {
            layer.AddPoint(lat, lng, attrs);
        }
    }

    private static void ReadMultiPoint(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (!TryGetCoordinates(el, out var coords) || coords.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var p in coords.EnumerateArray())
        {
            if (TryReadCoord(p, out var lat, out var lng))
            {
                layer.AddPoint(lat, lng, attrs);
            }
        }
    }

    private static void ReadLineString(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (!TryGetCoordinates(el, out var coords))
        {
            return;
        }

        var line = ReadCoordArray(coords);
        if (line.Length >= 2)
        {
            layer.AddLineString(line, attrs);
        }
    }

    private static void ReadMultiLineString(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (!TryGetCoordinates(el, out var coords) || coords.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var lineEl in coords.EnumerateArray())
        {
            var line = ReadCoordArray(lineEl);
            if (line.Length >= 2)
            {
                layer.AddLineString(line, attrs);
            }
        }
    }

    private static void ReadPolygon(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (!TryGetCoordinates(el, out var coords))
        {
            return;
        }

        EmitPolygon(layer, coords, attrs);
    }

    private static void ReadMultiPolygon(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (!TryGetCoordinates(el, out var coords) || coords.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var poly in coords.EnumerateArray())
        {
            EmitPolygon(layer, poly, attrs);
        }
    }

    private static void EmitPolygon(
        LayerBuilder layer,
        JsonElement polygonEl,
        Dictionary<string, object>? attrs
    )
    {
        if (polygonEl.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var ringEnumerator = polygonEl.EnumerateArray();
        if (!ringEnumerator.MoveNext())
        {
            return;
        }

        var outer = ReadRing(ringEnumerator.Current);
        if (outer.Length < 3)
        {
            return;
        }

        List<(double Lat, double Lng)[]>? holes = null;
        while (ringEnumerator.MoveNext())
        {
            var hole = ReadRing(ringEnumerator.Current);
            if (hole.Length < 3)
            {
                continue;
            }
            holes ??= new List<(double Lat, double Lng)[]>();
            holes.Add(hole);
        }

        if (holes is null)
        {
            layer.AddPolygon(outer, attrs);
            return;
        }

        layer.AddPolygon(outer, holes, attrs);
    }

    private static void ReadGeometryCollection(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs
    )
    {
        if (
            !el.TryGetProperty("geometries", out var geoms)
            || geoms.ValueKind != JsonValueKind.Array
        )
        {
            return;
        }

        foreach (var geom in geoms.EnumerateArray())
        {
            ReadGeometry(layer, geom, attrs);
        }
    }

    private static (double Lat, double Lng)[] ReadCoordArray(JsonElement coords)
    {
        if (coords.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<(double Lat, double Lng)>();
        }

        var result = new List<(double Lat, double Lng)>(coords.GetArrayLength());
        foreach (var p in coords.EnumerateArray())
        {
            if (TryReadCoord(p, out var lat, out var lng))
            {
                result.Add((lat, lng));
            }
        }

        return result.ToArray();
    }

    private static (double Lat, double Lng)[] ReadRing(JsonElement ringEl)
    {
        var pts = ReadCoordArray(ringEl);
        if (pts.Length > 1 && pts[0] == pts[^1])
        {
            Array.Resize(ref pts, pts.Length - 1);
        }
        return pts;
    }

    private static bool TryGetCoordinates(JsonElement geom, out JsonElement coords)
    {
        if (geom.TryGetProperty("coordinates", out coords))
        {
            return true;
        }
        coords = default;
        return false;
    }

    private static bool TryReadCoord(JsonElement el, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() < 2)
        {
            return false;
        }

        var lngEl = el[0];
        var latEl = el[1];
        if (lngEl.ValueKind != JsonValueKind.Number || latEl.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        lng = lngEl.GetDouble();
        lat = latEl.GetDouble();
        return true;
    }

    private static Dictionary<string, object>? ExtractScalarProperties(JsonElement props)
    {
        Dictionary<string, object>? attrs = null;
        foreach (var prop in props.EnumerateObject())
        {
            object? value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetInt64(out var l)
                    ? l
                    : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };

            if (value is null)
            {
                continue;
            }

            attrs ??= new Dictionary<string, object>();
            attrs[prop.Name] = value;
        }

        return attrs;
    }
}
