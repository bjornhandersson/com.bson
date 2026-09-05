using System.Text;
using BenchmarkDotNet.Attributes;

namespace Bson.MvtNet.Benchmarks;

/// <summary>
/// The ingestion path, where JSON parsing sits in front of the encoder. Compared
/// against building the same tile from native calls to show what parsing costs.
/// </summary>
[MemoryDiagnoser]
public class GeoJsonBenchmarks
{
    [Params(1_000, 10_000)]
    public int Features;

    private string _json = null!;
    private byte[] _utf8 = null!;
    private (double Lat, double Lng)[] _points = null!;
    private Dictionary<string, object>[] _attributes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _json = BenchmarkData.PointFeatureCollection(Features);
        _utf8 = Encoding.UTF8.GetBytes(_json);
        _points = BenchmarkData.PointsInTile(Features);
        _attributes = BenchmarkData.VehicleAttributes(Features);
    }

    [Benchmark(Description = "AddGeoJson from string")]
    public byte[] FromString()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        tile.Layer("vehicles").AddGeoJson(_json);
        return tile.Build();
    }

    [Benchmark(Description = "AddGeoJson from UTF-8 stream")]
    public byte[] FromStream()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        using var ms = new MemoryStream(_utf8, writable: false);
        tile.Layer("vehicles").AddGeoJson(ms);
        return tile.Build();
    }

    /// <summary>The same tile built directly, so the delta is the parsing cost.</summary>
    [Benchmark(Baseline = true, Description = "Equivalent tile without GeoJSON")]
    public byte[] NativeCalls()
    {
        var tile = new TileBuilder(BenchmarkData.Z, BenchmarkData.X, BenchmarkData.Y);
        var layer = tile.Layer("vehicles");
        for (int i = 0; i < _points.Length; i++)
        {
            layer.AddPoint(_points[i].Lat, _points[i].Lng, _attributes[i]);
        }

        return tile.Build();
    }
}
