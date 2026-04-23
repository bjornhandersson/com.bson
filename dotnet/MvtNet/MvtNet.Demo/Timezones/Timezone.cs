using System.Text.Json;

namespace MvtNet.Demo;

public record TimezonePolygon(string Name, double UtcOffset, (double Lat, double Lng)[] Ring);

public static class TimezoneFeed
{
    private const string SourceUrl =
        "https://raw.githubusercontent.com/nvkelso/natural-earth-vector/master/geojson/ne_10m_time_zones.geojson";

    private static readonly string CacheFile = Path.Combine(
        AppContext.BaseDirectory,
        "Timezones",
        "timezones.geojson"
    );

    public static async Task<List<TimezonePolygon>> LoadAsync()
    {
        if (!File.Exists(CacheFile))
        {
            Console.WriteLine("Downloading timezone boundaries (one-time)...");
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromSeconds(120);
            var bytes = await http.GetByteArrayAsync(SourceUrl);
            await File.WriteAllBytesAsync(CacheFile, bytes);
        }

        return ParseGeoJson(await File.ReadAllBytesAsync(CacheFile));
    }

    private static List<TimezonePolygon> ParseGeoJson(byte[] data)
    {
        using var doc = JsonDocument.Parse(data);
        var polygons = new List<TimezonePolygon>();

        foreach (var feature in doc.RootElement.GetProperty("features").EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var geom = feature.GetProperty("geometry");
            var geomType = geom.GetProperty("type").GetString();

            var name =
                TryGetString(props, "name") ?? TryGetString(props, "tz_name1st") ?? "Unknown";
            var utcOffset = TryGetDouble(props, "zone") ?? TryGetDouble(props, "utc_format") ?? 0;

            var coords = geom.GetProperty("coordinates");

            if (geomType == "Polygon")
            {
                AddPolygon(polygons, coords, name, utcOffset);
            }
            else if (geomType == "MultiPolygon")
            {
                foreach (var polygon in coords.EnumerateArray())
                {
                    AddPolygon(polygons, polygon, name, utcOffset);
                }
            }
        }

        return polygons;
    }

    private static void AddPolygon(
        List<TimezonePolygon> list,
        JsonElement polygonCoords,
        string name,
        double utcOffset
    )
    {
        var ring = ParseRing(polygonCoords[0]);
        ring = Simplify(ring, maxPoints: 200);
        if (ring.Length >= 3)
        {
            list.Add(new TimezonePolygon(name, utcOffset, ring));
        }
    }

    private static (double Lat, double Lng)[] ParseRing(JsonElement ringElement)
    {
        var points = new List<(double Lat, double Lng)>();
        foreach (var coord in ringElement.EnumerateArray())
        {
            var lng = coord[0].GetDouble();
            var lat = coord[1].GetDouble();
            points.Add((lat, lng));
        }

        // GeoJSON repeats the first point to close the ring — MvtNet does this automatically
        if (points.Count > 1 && points[0] == points[^1])
        {
            points.RemoveAt(points.Count - 1);
        }

        return points.ToArray();
    }

    private static (double Lat, double Lng)[] Simplify(
        (double Lat, double Lng)[] ring,
        int maxPoints
    )
    {
        if (ring.Length <= maxPoints)
        {
            return ring;
        }

        // Keep every Nth point, always including first and last
        double step = (double)(ring.Length - 1) / (maxPoints - 1);
        var result = new (double Lat, double Lng)[maxPoints];
        for (int i = 0; i < maxPoints; i++)
        {
            result[i] = ring[(int)(i * step)];
        }

        return result;
    }

    private static string? TryGetString(JsonElement props, string key)
    {
        if (props.TryGetProperty(key, out var val) && val.ValueKind == JsonValueKind.String)
        {
            return val.GetString();
        }

        return null;
    }

    private static double? TryGetDouble(JsonElement props, string key)
    {
        if (!props.TryGetProperty(key, out var val))
        {
            return null;
        }

        if (val.ValueKind == JsonValueKind.Number)
        {
            return val.GetDouble();
        }

        // Some datasets store offset as string like "-5" or "+5:30"
        if (val.ValueKind == JsonValueKind.String)
        {
            if (double.TryParse(val.GetString(), out var d))
            {
                return d;
            }
        }

        return null;
    }
}
