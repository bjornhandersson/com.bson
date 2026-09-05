using BenchmarkDotNet.Attributes;

namespace Bson.MvtNet.Benchmarks;

/// <summary>
/// Polygon encoding, including the oversized rings that make clipping matter.
/// </summary>
[MemoryDiagnoser]
public class PolygonBenchmarks
{
    [Params(50, 500)]
    public int Rings;

    private (double Lat, double Lng)[][] _small = null!;
    private (double Lat, double Lng)[][] _oversized = null!;
    private List<(double Lat, double Lng)[]> _holes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _small = BenchmarkData.SmallRings(Rings);
        _oversized = BenchmarkData.OversizedRings(Rings);
        _holes = new List<(double Lat, double Lng)[]>();
        for (int i = 1; i < Math.Min(5, _small.Length); i++)
        {
            _holes.Add(_small[i]);
        }
    }

    [Benchmark(Baseline = true, Description = "Rings inside the tile")]
    public byte[] SmallRings()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("zones");
        foreach (var ring in _small)
        {
            layer.AddPolygon(ring);
        }

        return tile.Build();
    }

    /// <summary>
    /// Rings far larger than the tile, the shape of real timezone or boundary
    /// data. Most vertices are clipped away.
    /// </summary>
    [Benchmark(Description = "Rings far larger than the tile")]
    public byte[] OversizedRings()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("zones");
        foreach (var ring in _oversized)
        {
            layer.AddPolygon(ring);
        }

        return tile.Build();
    }

    /// <summary>
    /// Same ring count as the cases above, each carrying holes, so the numbers
    /// are comparable rather than a fixed one-off.
    /// </summary>
    [Benchmark(Description = "Rings with holes")]
    public byte[] WithHoles()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("zones");
        for (int i = 0; i < _small.Length; i++)
        {
            layer.AddPolygon(_small[i], _holes);
        }

        return tile.Build();
    }
}
