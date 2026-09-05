using BenchmarkDotNet.Attributes;

namespace Bson.MvtNet.Benchmarks;

/// <summary>
/// Serialization and the geohash bridge: the two things a tile endpoint does
/// around the encoding itself.
/// </summary>
[MemoryDiagnoser]
public class OutputBenchmarks
{
    private const int Features = 5_000;

    private TileBuilder _tile = null!;
    private MemoryStream _sink = null!;

    [GlobalSetup]
    public void Setup()
    {
        var points = BenchmarkData.PointsInTile(Features);
        var attrs = BenchmarkData.VehicleAttributes(Features);

        _tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = _tile.Layer("vehicles");
        for (int i = 0; i < points.Length; i++)
        {
            layer.AddPoint(points[i].Lat, points[i].Lng, attrs[i]);
        }

        _sink = new MemoryStream(capacity: 1 << 20);
    }

    [Benchmark(Baseline = true, Description = "Build to byte[]")]
    public byte[] ToArray() => _tile.Build();

    [Benchmark(Description = "Build to stream")]
    public long ToStream()
    {
        _sink.SetLength(0);
        _tile.Build(_sink);
        return _sink.Length;
    }

    [Benchmark(Description = "TileGeohash.GetPrefixes")]
    public int Prefixes() =>
        TileGeohash.GetPrefixes(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y).Count;
}
