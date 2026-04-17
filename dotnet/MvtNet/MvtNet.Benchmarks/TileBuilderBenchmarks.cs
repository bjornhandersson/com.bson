using BenchmarkDotNet.Attributes;
using Bson.MvtNet;

namespace Bson.MvtNet.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class TileBuilderBenchmarks
{
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    private static readonly Dictionary<string, object> PointAttrs = new()
    {
        ["name"] = "Stockholm",
        ["speed"] = 42.5,
        ["active"] = true,
    };

    private static readonly (double, double)[] LineCoords =
    {
        (59.3340, 18.0300),
        (59.3310, 18.0400),
        (59.3281936, 18.0440866),
        (59.3260, 18.0550),
        (59.3326, 18.0649),
    };

    // ~200km route: Stockholm (59.33, 18.04) → Norrköping (58.59, 16.18)
    // 3000 points evenly spaced along the path
    private static readonly (double, double)[] LongRoute = GenerateLongRoute();

    // A tile near the midpoint of the route at z14
    private const int LongRouteZ = 14;
    private const int LongRouteX = 8960;
    private const int LongRouteY = 4850;

    private static (double, double)[] GenerateLongRoute()
    {
        const double startLat = 59.33,
            startLng = 18.04;
        const double endLat = 58.59,
            endLng = 16.18;
        const int count = 3000;

        var route = new (double, double)[count];
        for (int i = 0; i < count; i++)
        {
            double t = (double)i / (count - 1);
            route[i] = (startLat + (endLat - startLat) * t, startLng + (endLng - startLng) * t);
        }
        return route;
    }

    private static readonly (double, double)[] PolygonRing =
    {
        (59.3380, 18.0250),
        (59.3380, 18.0750),
        (59.3150, 18.0750),
        (59.3150, 18.0250),
    };

    [Benchmark(Description = "Single point")]
    public byte[] SinglePoint()
    {
        return new TileBuilder(Z, X, Y).Layer("points").AddPoint(59.3281936, 18.0440866).Build();
    }

    [Benchmark(Description = "Point with attributes")]
    public byte[] PointWithAttributes()
    {
        return new TileBuilder(Z, X, Y)
            .Layer("points")
            .AddPoint(59.3281936, 18.0440866, PointAttrs)
            .Build();
    }

    [Benchmark(Description = "10 points")]
    public byte[] TenPoints()
    {
        var tile = new TileBuilder(Z, X, Y);
        var layer = tile.Layer("points");
        for (int i = 0; i < 10; i++)
        {
            layer.AddPoint(59.328 + i * 0.001, 18.044 + i * 0.001, PointAttrs);
        }
        return tile.Build();
    }

    [Benchmark(Description = "100 points")]
    public byte[] HundredPoints()
    {
        var tile = new TileBuilder(Z, X, Y);
        var layer = tile.Layer("points");
        for (int i = 0; i < 100; i++)
        {
            layer.AddPoint(59.320 + i * 0.0002, 18.030 + i * 0.0004, PointAttrs);
        }
        return tile.Build();
    }

    [Benchmark(Description = "LineString (5 pts)")]
    public byte[] LineString5()
    {
        var tile = new TileBuilder(Z, X, Y);
        tile.Layer("tracks").AddLineString(LineCoords);
        return tile.Build();
    }

    [Benchmark(Description = "Polygon (4 pts)")]
    public byte[] Polygon4()
    {
        var tile = new TileBuilder(Z, X, Y);
        tile.Layer("zones").AddPolygon(PolygonRing);
        return tile.Build();
    }

    [Benchmark(Description = "Mixed tile (realistic)")]
    public byte[] MixedTile()
    {
        var tile = new TileBuilder(Z, X, Y);

        var points = tile.Layer("points");
        points.AddPoint(59.3281936, 18.0440866, PointAttrs);
        points.AddPoint(59.3326, 18.0649, PointAttrs);
        points.AddPoint(59.3190, 18.0686, PointAttrs);
        points.AddPoint(59.3340, 18.0300, PointAttrs);

        tile.Layer("tracks").AddLineString(LineCoords);
        tile.Layer("zones").AddPolygon(PolygonRing);

        return tile.Build();
    }

    [Benchmark(Description = "LineString (3000 pts, 200km, zoomed in)")]
    public byte[] LongLineStringZoomedIn()
    {
        var tile = new TileBuilder(LongRouteZ, LongRouteX, LongRouteY);
        tile.Layer("tracks").AddLineString(LongRoute);
        return tile.Build();
    }

    [Benchmark(Description = "LineString (3000 pts, 200km, zoomed out)")]
    public byte[] LongLineStringZoomedOut()
    {
        // z6 — entire route fits in one tile
        var tile = new TileBuilder(6, 34, 18);
        tile.Layer("tracks").AddLineString(LongRoute);
        return tile.Build();
    }

    [Benchmark(Description = "Point outside tile (skip)")]
    public byte[] PointOutsideTile()
    {
        return new TileBuilder(10, 0, 0).Layer("points").AddPoint(59.3281936, 18.0440866).Build();
    }
}
