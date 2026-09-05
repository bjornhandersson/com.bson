using BenchmarkDotNet.Attributes;

namespace Bson.MvtNet.Benchmarks;

/// <summary>
/// Long routes, measured both where the whole line fits the tile and where most
/// of it is discarded. The gap between the two is what clipping costs and saves.
/// </summary>
[MemoryDiagnoser]
public class LineStringBenchmarks
{
    [Params(1_000, 10_000)]
    public int Vertices;

    private (double Lat, double Lng)[] _route = null!;

    [GlobalSetup]
    public void Setup() => _route = BenchmarkData.Route(Vertices);

    /// <summary>z6, where the entire 200km route falls inside one tile.</summary>
    [Benchmark(Baseline = true, Description = "Route fits the tile")]
    public byte[] WhollyInside()
    {
        var tile = new TileBuilder(6, 34, 18);
        tile.Layer("tracks").AddLineString(_route);
        return tile.Build();
    }

    /// <summary>z14 near the midpoint, where most of the route is clipped away.</summary>
    [Benchmark(Description = "Route mostly clipped away")]
    public byte[] MostlyClipped()
    {
        var tile = new TileBuilder(14, 8960, 4850);
        tile.Layer("tracks").AddLineString(_route);
        return tile.Build();
    }

    /// <summary>A tile the route never reaches, so the bounds check short-circuits.</summary>
    [Benchmark(Description = "Route misses the tile entirely")]
    public byte[] Missed()
    {
        var tile = new TileBuilder(14, 1000, 1000);
        tile.Layer("tracks").AddLineString(_route);
        return tile.Build();
    }
}
