using BenchmarkDotNet.Attributes;

namespace Bson.MvtNet.Benchmarks;

/// <summary>
/// The core fleet-tracking workload: many points with per-feature attributes,
/// which is what a tile endpoint actually does per request.
/// </summary>
[MemoryDiagnoser]
public class PointBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int Features;

    private (double Lat, double Lng)[] _points = null!;
    private Dictionary<string, object>[] _attributes = null!;
    private Dictionary<string, object> _sharedAttributes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _points = BenchmarkData.PointsInTile(Features);
        _attributes = BenchmarkData.VehicleAttributes(Features);
        _sharedAttributes = new Dictionary<string, object>
        {
            ["status"] = "moving",
            ["depot"] = "depot-1",
        };
    }

    [Benchmark(Baseline = true, Description = "Points, no attributes")]
    public byte[] Bare()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("vehicles");
        for (int i = 0; i < _points.Length; i++)
        {
            layer.AddPoint(_points[i].Lat, _points[i].Lng);
        }

        return tile.Build();
    }

    [Benchmark(Description = "Points, per-feature attributes")]
    public byte[] PerFeatureAttributes()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("vehicles");
        for (int i = 0; i < _points.Length; i++)
        {
            layer.AddPoint(_points[i].Lat, _points[i].Lng, _attributes[i]);
        }

        return tile.Build();
    }

    /// <summary>
    /// Every feature shares one attribute set, so the tag encoder interns
    /// everything. The gap against the case above is the cost of tag interning
    /// on high-cardinality values.
    /// </summary>
    [Benchmark(Description = "Points, shared attributes (all interned)")]
    public byte[] SharedAttributes()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("vehicles");
        for (int i = 0; i < _points.Length; i++)
        {
            layer.AddPoint(_points[i].Lat, _points[i].Lng, _sharedAttributes);
        }

        return tile.Build();
    }
}
