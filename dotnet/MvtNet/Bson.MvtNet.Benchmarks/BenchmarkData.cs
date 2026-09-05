using System.Globalization;
using System.Text;

namespace Bson.MvtNet.Benchmarks;

/// <summary>
/// Deterministic workload generators. Everything here runs in GlobalSetup so
/// the measured region contains encoding work only, never data construction.
/// </summary>
internal static class BenchmarkData
{
    // A z12 tile over central Stockholm, and its geographic bounds.
    public const int Z = 12;
    public const int X = 2253;
    public const int Y = 1204;

    public const double SouthLat = 59.310;
    public const double NorthLat = 59.355;
    public const double WestLng = 18.000;
    public const double EastLng = 18.088;

    private const int Seed = 20260905;

    public static (double Lat, double Lng)[] PointsInTile(int count)
    {
        var rng = new Random(Seed);
        var pts = new (double, double)[count];
        for (int i = 0; i < count; i++)
        {
            pts[i] = (
                SouthLat + rng.NextDouble() * (NorthLat - SouthLat),
                WestLng + rng.NextDouble() * (EastLng - WestLng)
            );
        }

        return pts;
    }

    /// <summary>
    /// One attribute set per feature, the way a real service builds them.
    /// Mixes low-cardinality values that the tag encoder can intern with
    /// high-cardinality ones that it cannot.
    /// </summary>
    public static Dictionary<string, object>[] VehicleAttributes(int count)
    {
        var rng = new Random(Seed);
        var statuses = new[] { "moving", "idle", "stopped", "offline" };
        var attrs = new Dictionary<string, object>[count];
        for (int i = 0; i < count; i++)
        {
            attrs[i] = new Dictionary<string, object>
            {
                ["id"] = 100000 + i, // unique, never interned
                ["speed"] = Math.Round(rng.NextDouble() * 120, 1),
                ["status"] = statuses[rng.Next(statuses.Length)], // interned
                ["depot"] = "depot-" + rng.Next(8), // mostly interned
            };
        }

        return attrs;
    }

    /// <summary>
    /// A route of the given length across roughly 200km. At a high zoom most of
    /// it lies outside the tile, which is what exercises the line clipper.
    /// </summary>
    public static (double Lat, double Lng)[] Route(int count)
    {
        const double startLat = 59.33,
            startLng = 18.04;
        const double endLat = 58.59,
            endLng = 16.18;

        var route = new (double, double)[count];
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / (count - 1);
            // A little lateral wobble so segments are not collinear.
            double wobble = Math.Sin(t * 40) * 0.004;
            route[i] = (
                startLat + (endLat - startLat) * t + wobble,
                startLng + (endLng - startLng) * t + wobble
            );
        }

        return route;
    }

    /// <summary>
    /// Rings small enough to sit inside the tile. Nothing to clip, so this
    /// isolates projection and encoding.
    /// </summary>
    public static (double Lat, double Lng)[][] SmallRings(int count)
    {
        var rng = new Random(Seed);
        var rings = new (double, double)[count][];
        for (int i = 0; i < count; i++)
        {
            double lat = SouthLat + rng.NextDouble() * (NorthLat - SouthLat);
            double lng = WestLng + rng.NextDouble() * (EastLng - WestLng);
            rings[i] = Circle(lat, lng, 0.002, 24);
        }

        return rings;
    }

    /// <summary>
    /// Rings far larger than the tile, most of their vertices outside it. This
    /// is the timezone-shaped workload that the polygon clipper exists for.
    /// </summary>
    public static (double Lat, double Lng)[][] OversizedRings(int count)
    {
        var rng = new Random(Seed);
        var rings = new (double, double)[count][];
        for (int i = 0; i < count; i++)
        {
            double lat = SouthLat + rng.NextDouble() * (NorthLat - SouthLat);
            double lng = WestLng + rng.NextDouble() * (EastLng - WestLng);
            rings[i] = Circle(lat, lng, 12.0, 256);
        }

        return rings;
    }

    /// <summary>A closed ring approximating a circle, first point not repeated.</summary>
    private static (double Lat, double Lng)[] Circle(
        double lat,
        double lng,
        double radiusDeg,
        int segments
    )
    {
        var ring = new (double, double)[segments];
        for (int i = 0; i < segments; i++)
        {
            double a = 2 * Math.PI * i / segments;
            ring[i] = (lat + Math.Sin(a) * radiusDeg, lng + Math.Cos(a) * radiusDeg * 0.5);
        }

        return ring;
    }

    /// <summary>
    /// A GeoJSON FeatureCollection of points with properties, as a service would
    /// receive it from a database or an upstream API.
    /// </summary>
    public static string PointFeatureCollection(int count)
    {
        var pts = PointsInTile(count);
        var attrs = VehicleAttributes(count);
        var inv = CultureInfo.InvariantCulture;

        var sb = new StringBuilder(count * 160);
        sb.Append("{\"type\":\"FeatureCollection\",\"features\":[");
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append("{\"type\":\"Feature\",\"properties\":{")
                .Append("\"id\":")
                .Append(((int)attrs[i]["id"]).ToString(inv))
                .Append(",\"speed\":")
                .Append(((double)attrs[i]["speed"]).ToString(inv))
                .Append(",\"status\":\"")
                .Append((string)attrs[i]["status"])
                .Append("\"},\"geometry\":{\"type\":\"Point\",\"coordinates\":[")
                .Append(pts[i].Lng.ToString(inv))
                .Append(',')
                .Append(pts[i].Lat.ToString(inv))
                .Append("]}}");
        }

        sb.Append("]}");
        return sb.ToString();
    }
}
