using System.Text.Json;

namespace Bson.MvtNet;

// Lenient by default: anything malformed is skipped. In strict mode the same
// conditions throw FormatException so callers can see why nothing rendered.
internal static class GeoJsonReader
{
    public static void Read(LayerBuilder layer, JsonElement root, bool strict)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            Fail(strict, "GeoJSON root must be an object.");
            return;
        }

        if (!TryGetType(root, out var type))
        {
            Fail(strict, "GeoJSON object is missing a string \"type\" member.");
            return;
        }

        switch (type)
        {
            case "FeatureCollection":
                ReadFeatureCollection(layer, root, strict);
                return;
            case "Feature":
                ReadFeature(layer, root, strict);
                return;
            default:
                ReadGeometry(layer, root, null, strict);
                return;
        }
    }

    private static void ReadFeatureCollection(LayerBuilder layer, JsonElement el, bool strict)
    {
        if (!el.TryGetProperty("features", out var feats) || feats.ValueKind != JsonValueKind.Array)
        {
            Fail(strict, "FeatureCollection is missing a \"features\" array.");
            return;
        }

        // Reused across features; each feature's tags are consumed before the
        // next one is read.
        var propertyBuffer = new Dictionary<string, object>();

        foreach (var feature in feats.EnumerateArray())
        {
            if (strict)
            {
                ReadFeature(layer, feature, strict, propertyBuffer);
                continue;
            }

            try
            {
                ReadFeature(layer, feature, strict, propertyBuffer);
            }
            catch
            {
                // skip a malformed feature, keep going
            }
        }
    }

    private static void ReadFeature(
        LayerBuilder layer,
        JsonElement el,
        bool strict,
        Dictionary<string, object>? propertyBuffer = null
    )
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            Fail(strict, "Feature must be an object.");
            return;
        }

        if (!el.TryGetProperty("geometry", out var geom))
        {
            Fail(strict, "Feature is missing a \"geometry\" member.");
            return;
        }

        // An unlocated feature; valid GeoJSON, nothing to draw.
        if (geom.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        Dictionary<string, object>? attrs = null;
        if (
            el.TryGetProperty("properties", out var props)
            && props.ValueKind == JsonValueKind.Object
        )
        {
            attrs = ExtractScalarProperties(props, propertyBuffer);
        }

        ReadGeometry(layer, geom, attrs, strict);
    }

    private static void ReadGeometry(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            Fail(strict, "Geometry must be an object.");
            return;
        }

        if (!TryGetType(el, out var type))
        {
            Fail(strict, "Geometry is missing a string \"type\" member.");
            return;
        }

        switch (type)
        {
            case "Point":
                ReadPoint(layer, el, attrs, strict);
                return;
            case "MultiPoint":
                ReadMultiPoint(layer, el, attrs, strict);
                return;
            case "LineString":
                ReadLineString(layer, el, attrs, strict);
                return;
            case "MultiLineString":
                ReadMultiLineString(layer, el, attrs, strict);
                return;
            case "Polygon":
                ReadPolygon(layer, el, attrs, strict);
                return;
            case "MultiPolygon":
                ReadMultiPolygon(layer, el, attrs, strict);
                return;
            case "GeometryCollection":
                ReadGeometryCollection(layer, el, attrs, strict);
                return;
            default:
                Fail(strict, $"Unknown GeoJSON type \"{type}\".");
                return;
        }
    }

    private static void ReadPoint(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (!TryGetCoordinates(el, strict, out var coords))
        {
            return;
        }

        if (TryReadCoord(coords, strict, out var lat, out var lng))
        {
            layer.AddPoint(lat, lng, attrs);
        }
    }

    private static void ReadMultiPoint(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (!TryGetCoordinateArray(el, strict, out var coords))
        {
            return;
        }

        foreach (var p in coords.EnumerateArray())
        {
            if (TryReadCoord(p, strict, out var lat, out var lng))
            {
                layer.AddPoint(lat, lng, attrs);
            }
        }
    }

    private static void ReadLineString(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (!TryGetCoordinates(el, strict, out var coords))
        {
            return;
        }

        var line = ReadCoordArray(coords, strict);
        if (line.Length < 2)
        {
            Fail(strict, "LineString needs at least 2 positions.");
            return;
        }

        layer.AddLineString(line, attrs);
    }

    private static void ReadMultiLineString(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (!TryGetCoordinateArray(el, strict, out var coords))
        {
            return;
        }

        foreach (var lineEl in coords.EnumerateArray())
        {
            var line = ReadCoordArray(lineEl, strict);
            if (line.Length < 2)
            {
                Fail(strict, "LineString needs at least 2 positions.");
                continue;
            }

            layer.AddLineString(line, attrs);
        }
    }

    private static void ReadPolygon(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (!TryGetCoordinates(el, strict, out var coords))
        {
            return;
        }

        EmitPolygon(layer, coords, attrs, strict);
    }

    private static void ReadMultiPolygon(
        LayerBuilder layer,
        JsonElement el,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (!TryGetCoordinateArray(el, strict, out var coords))
        {
            return;
        }

        foreach (var poly in coords.EnumerateArray())
        {
            EmitPolygon(layer, poly, attrs, strict);
        }
    }

    private static void EmitPolygon(
        LayerBuilder layer,
        JsonElement polygonEl,
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (polygonEl.ValueKind != JsonValueKind.Array)
        {
            Fail(strict, "Polygon coordinates must be an array of rings.");
            return;
        }

        var ringEnumerator = polygonEl.EnumerateArray();
        if (!ringEnumerator.MoveNext())
        {
            Fail(strict, "Polygon has no rings.");
            return;
        }

        var outer = ReadRing(ringEnumerator.Current, strict);
        if (outer.Length < 3)
        {
            Fail(strict, "Polygon ring needs at least 3 distinct positions.");
            return;
        }

        List<(double Lat, double Lng)[]>? holes = null;
        while (ringEnumerator.MoveNext())
        {
            var hole = ReadRing(ringEnumerator.Current, strict);
            if (hole.Length < 3)
            {
                Fail(strict, "Polygon ring needs at least 3 distinct positions.");
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
        Dictionary<string, object>? attrs,
        bool strict
    )
    {
        if (
            !el.TryGetProperty("geometries", out var geoms)
            || geoms.ValueKind != JsonValueKind.Array
        )
        {
            Fail(strict, "GeometryCollection is missing a \"geometries\" array.");
            return;
        }

        foreach (var geom in geoms.EnumerateArray())
        {
            ReadGeometry(layer, geom, attrs, strict);
        }
    }

    private static (double Lat, double Lng)[] ReadCoordArray(JsonElement coords, bool strict)
    {
        if (coords.ValueKind != JsonValueKind.Array)
        {
            Fail(strict, "Coordinates must be an array of positions.");
            return Array.Empty<(double Lat, double Lng)>();
        }

        var result = new (double Lat, double Lng)[coords.GetArrayLength()];
        int count = 0;
        foreach (var p in coords.EnumerateArray())
        {
            if (TryReadCoord(p, strict, out var lat, out var lng))
            {
                result[count++] = (lat, lng);
            }
        }

        if (count != result.Length)
        {
            Array.Resize(ref result, count);
        }

        return result;
    }

    private static (double Lat, double Lng)[] ReadRing(JsonElement ringEl, bool strict)
    {
        var pts = ReadCoordArray(ringEl, strict);
        if (pts.Length > 1 && pts[0] == pts[pts.Length - 1])
        {
            Array.Resize(ref pts, pts.Length - 1);
        }
        return pts;
    }

    private static bool TryGetType(JsonElement el, out string type)
    {
        if (el.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
        {
            type = typeEl.GetString()!;
            return true;
        }

        type = "";
        return false;
    }

    private static bool TryGetCoordinates(JsonElement geom, bool strict, out JsonElement coords)
    {
        if (geom.TryGetProperty("coordinates", out coords))
        {
            return true;
        }

        Fail(strict, "Geometry is missing a \"coordinates\" member.");
        coords = default;
        return false;
    }

    private static bool TryGetCoordinateArray(JsonElement geom, bool strict, out JsonElement coords)
    {
        if (!TryGetCoordinates(geom, strict, out coords))
        {
            return false;
        }

        if (coords.ValueKind != JsonValueKind.Array)
        {
            Fail(strict, "Coordinates must be an array.");
            return false;
        }

        return true;
    }

    private static bool TryReadCoord(JsonElement el, bool strict, out double lat, out double lng)
    {
        lat = 0;
        lng = 0;
        if (el.ValueKind != JsonValueKind.Array || el.GetArrayLength() < 2)
        {
            Fail(strict, "A position must be an array of at least [longitude, latitude].");
            return false;
        }

        var lngEl = el[0];
        var latEl = el[1];
        if (lngEl.ValueKind != JsonValueKind.Number || latEl.ValueKind != JsonValueKind.Number)
        {
            Fail(strict, "Position coordinates must be numbers.");
            return false;
        }

        lng = lngEl.GetDouble();
        lat = latEl.GetDouble();
        return true;
    }

    private static Dictionary<string, object>? ExtractScalarProperties(
        JsonElement props,
        Dictionary<string, object>? propertyBuffer = null
    )
    {
        Dictionary<string, object>? attrs = null;
        propertyBuffer?.Clear();
        foreach (var prop in props.EnumerateObject())
        {
            object? value = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                // The cast keeps integers as integers; without it the
                // conditional unifies to double.
                JsonValueKind.Number => prop.Value.TryGetInt64(out var l)
                    ? (object)l
                    : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            };

            if (value is null)
            {
                continue;
            }

            attrs ??= propertyBuffer ?? new Dictionary<string, object>();
            attrs[prop.Name] = value;
        }

        return attrs;
    }

    private static void Fail(bool strict, string message)
    {
        if (strict)
        {
            throw new FormatException(message);
        }
    }
}
