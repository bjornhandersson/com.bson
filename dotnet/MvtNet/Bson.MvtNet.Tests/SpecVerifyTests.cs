using System.Runtime.CompilerServices;
using Google.Protobuf;
using VectorTile;

namespace Bson.MvtNet.Tests;

/// <summary>
/// Byte-for-byte verification against the reference tiles in <c>SpecVerify/*.mvt</c>.
/// Each fixture was produced by the encoder, decoded with the reference protobuf
/// implementation, checked against the MUST rules of the Mapbox Vector Tile 2.1
/// spec (https://github.com/mapbox/vector-tile-spec/tree/master/2.1) and frozen.
/// To refresh after an intentional output change, run the tests once with
/// <c>MVTNET_UPDATE_SPEC_VERIFY=1</c> and review the .mvt diff before committing.
/// </summary>
public class SpecVerifyTests
{
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    private static readonly TileBounds Bounds = TileMath.GetTileBounds(Z, X, Y);
    private static readonly double LatSpan = Bounds.North - Bounds.South;
    private static readonly double LngSpan = Bounds.East - Bounds.West;
    private static readonly double MidLat = (Bounds.North + Bounds.South) / 2;

    private enum Status
    {
        Moving,
        Idle,
    }

    [Test]
    public void Points_EveryTagType_ExplicitAndAutoIds()
    {
        var tile = new TileBuilder(Z, X, Y);
        var layer = tile.Layer("vehicles");

        layer.AddPoint(lat: 59.3281936, lng: 18.0440866);
        layer.AddPoint(
            lat: 59.33,
            lng: 18.05,
            new Dictionary<string, object?>
            {
                ["name"] = "Stockholm ✓ 点",
                ["c"] = 'x',
                ["status"] = Status.Idle,
                ["on"] = true,
                ["off"] = false,
                ["i"] = -7,
                ["l"] = long.MaxValue,
                ["lmin"] = long.MinValue,
                ["s"] = (short)-3,
                ["b"] = (byte)200,
                ["sb"] = (sbyte)-100,
                ["us"] = (ushort)60000,
                ["ui"] = uint.MaxValue,
                ["ul"] = ulong.MaxValue,
                ["f"] = 1.5f,
                ["d"] = -2.25,
                ["m"] = 19.99m,
                ["g"] = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                ["dt"] = new DateTime(2026, 9, 5, 12, 30, 0, DateTimeKind.Utc),
                ["dto"] = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.FromHours(2)),
                ["long"] = new string('x', 300),
                ["nothing"] = null,
            },
            id: ulong.MaxValue
        );
        layer.AddPoint(lat: 59.331, lng: 18.051, new Dictionary<string, string> { ["name"] = "Stockholm ✓ 点" }, id: 0);
        layer.AddPoint(lat: 59.332, lng: 18.052, new Dictionary<string, object> { ["i"] = 7L, ["j"] = 7 });
        layer.AddPoint(lat: 0.0, lng: 0.0);
        layer.AddPoint(lat: 59.333, lng: 18.053);

        VerifyAgainstFixture("points-every-tag-type", tile);
    }

    [Test]
    public void Points_HighAndLowCardinalityTags_Interning()
    {
        var tile = new TileBuilder(Z, X, Y);
        var layer = tile.Layer("fleet");
        var random = new DeterministicRandom(seed: 20260905);
        string[] statuses = ["moving", "idle", "stopped", "offline"];

        for (int i = 0; i < 300; i++)
        {
            layer.AddPoint(
                lat: Bounds.South + LatSpan * random.NextDouble(),
                lng: Bounds.West + LngSpan * random.NextDouble(),
                new Dictionary<string, object>
                {
                    ["vehicle"] = 100000 + i,
                    ["speed"] = Math.Round(random.NextDouble() * 120, 1),
                    ["status"] = statuses[i % statuses.Length],
                    ["depot"] = "depot-" + i % 7,
                    ["heading"] = (float)(i % 360),
                },
                id: (ulong)(1000 + i)
            );
        }

        VerifyAgainstFixture("points-interning", tile);
    }

    [Test]
    public void Lines_LeavingAndReenteringTile_BufferedAndSamePixelVertices()
    {
        var tile = new TileBuilder(Z, X, Y);
        var layer = tile.Layer("tracks");

        layer.AddLineString(
            [
                (MidLat, Bounds.West + LngSpan * 0.2),
                (Bounds.North + LatSpan, Bounds.West + LngSpan * 0.3),
                (Bounds.North + LatSpan, Bounds.West + LngSpan * 0.7),
                (MidLat, Bounds.West + LngSpan * 0.8),
            ],
            new Dictionary<string, object> { ["name"] = "u-turn", ["speed"] = 42.0 }
        );
        layer.AddLineString(
            [(59.334, 18.03), (59.334000001, 18.030000001), (59.334000002, 18.030000002), (59.3326, 18.0649)],
            id: 5
        );
        layer.AddLineString(
            [(MidLat, Bounds.East + LngSpan * 0.02), (Bounds.South, Bounds.East + LngSpan * 0.03)]
        );
        layer.AddLineString(
            [
                (Bounds.North + LatSpan * 3, Bounds.West - LngSpan * 3),
                (Bounds.South - LatSpan * 3, Bounds.East + LngSpan * 3),
            ],
            new Dictionary<string, object> { ["name"] = "diagonal" }
        );
        layer.AddLineString([(0, 0), (1, 1)]);

        VerifyAgainstFixture("lines-clipped", tile);
    }

    [Test]
    public void Lines_LongTrackAtLowZoom_CollapsesToDistinctPixels()
    {
        var tile = new TileBuilder(z: 2, x: 2, y: 1);
        var track = new (double Lat, double Lng)[1000];
        for (int i = 0; i < track.Length; i++)
        {
            double t = i / (track.Length - 1.0);
            track[i] = (59.33 + (58.59 - 59.33) * t + Math.Sin(t * 40) * 0.004, 18.04 + (16.18 - 18.04) * t);
        }

        tile.Layer("tracks").AddLineString(track, new Dictionary<string, object> { ["name"] = "low-zoom" });

        VerifyAgainstFixture("lines-low-zoom", tile);
    }

    [Test]
    public void Polygons_ClippedRingsHolesAndWinding()
    {
        var tile = new TileBuilder(Z, X, Y);
        var layer = tile.Layer("zones");

        layer.AddPolygon(
            [(59.32, 18.03), (59.32, 18.07), (59.34, 18.07), (59.34, 18.03)],
            new Dictionary<string, object> { ["z"] = 1 }
        );
        layer.AddPolygon(
            outer:
            [
                (Bounds.North - LatSpan * 0.2, Bounds.West + LngSpan * 0.5),
                (Bounds.North - LatSpan * 0.2, Bounds.East + LngSpan * 0.5),
                (Bounds.South + LatSpan * 0.2, Bounds.East + LngSpan * 0.5),
                (Bounds.South + LatSpan * 0.2, Bounds.West + LngSpan * 0.5),
            ],
            holes:
            [
                [
                    (Bounds.North - LatSpan * 0.4, Bounds.West + LngSpan * 0.9),
                    (Bounds.North - LatSpan * 0.4, Bounds.East + LngSpan * 0.1),
                    (Bounds.South + LatSpan * 0.4, Bounds.East + LngSpan * 0.1),
                    (Bounds.South + LatSpan * 0.4, Bounds.West + LngSpan * 0.9),
                ],
                [
                    (Bounds.North - LatSpan * 0.4, Bounds.East + LngSpan * 0.3),
                    (Bounds.North - LatSpan * 0.4, Bounds.East + LngSpan * 0.4),
                    (Bounds.South + LatSpan * 0.4, Bounds.East + LngSpan * 0.4),
                ],
            ],
            new Dictionary<string, object> { ["z"] = 2 },
            id: 77
        );
        layer.AddPolygon(Ellipse(centerLat: 59.33, centerLng: 18.05, latRadius: 5.0, lngRadius: 9.0, vertices: 64), new Dictionary<string, object> { ["z"] = 3 });
        layer.AddPolygon(
            [
                (Bounds.South - LatSpan * 0.01, Bounds.West + LngSpan * 0.4),
                (Bounds.South - LatSpan * 0.01, Bounds.West + LngSpan * 0.6),
                (Bounds.South - LatSpan * 0.04, Bounds.West + LngSpan * 0.5),
            ]
        );
        layer.AddPolygon([(0, 0), (0, 1), (1, 1)]);
        layer.AddPolygon([(59.33, 18.05), (59.3300000001, 18.05), (59.33, 18.0500000001)]);

        VerifyAgainstFixture("polygons-clipped", tile);
    }

    [Test]
    public void GeoJson_EveryGeometryType_ScalarPropertiesOnly()
    {
        const string json = """
            {"type":"FeatureCollection","features":[
              {"type":"Feature","id":"ignored","properties":{"name":"pt","n":1,"x":2.5,"ok":true,"no":false,"nul":null,"obj":{"a":1},"arr":[1,2]},
               "geometry":{"type":"Point","coordinates":[18.0440866,59.3281936]}},
              {"type":"Feature","properties":{"name":"mp"},
               "geometry":{"type":"MultiPoint","coordinates":[[18.05,59.33],[18.06,59.331]]}},
              {"type":"Feature","properties":{"name":"ls","n":1},
               "geometry":{"type":"LineString","coordinates":[[18.03,59.334],[18.0649,59.3326],[18.07,59.33]]}},
              {"type":"Feature","properties":{"name":"mls"},
               "geometry":{"type":"MultiLineString","coordinates":[[[18.03,59.32],[18.04,59.325]],[[18.05,59.32],[18.06,59.325]]]}},
              {"type":"Feature","properties":{"name":"poly","n":9007199254740993},
               "geometry":{"type":"Polygon","coordinates":[[[18.03,59.32],[18.07,59.32],[18.07,59.34],[18.03,59.34],[18.03,59.32]],
                                                            [[18.04,59.325],[18.06,59.325],[18.06,59.335],[18.04,59.335],[18.04,59.325]]]}},
              {"type":"Feature","properties":{"name":"mpoly"},
               "geometry":{"type":"MultiPolygon","coordinates":[[[[18.031,59.321],[18.035,59.321],[18.035,59.323],[18.031,59.321]]],
                                                                 [[[18.041,59.321],[18.045,59.321],[18.045,59.323],[18.041,59.321]]]]}},
              {"type":"Feature","properties":{"name":"gc"},
               "geometry":{"type":"GeometryCollection","geometries":[
                  {"type":"Point","coordinates":[18.05,59.335]},
                  {"type":"LineString","coordinates":[[18.05,59.335],[18.055,59.336]]}]}},
              {"type":"Feature","properties":{"name":"unlocated"},"geometry":null},
              {"type":"Feature","properties":{"name":"far away"},"geometry":{"type":"Point","coordinates":[0,0]}}
            ]}
            """;

        var tile = new TileBuilder(Z, X, Y);
        tile.Layer("geojson").AddGeoJson(json, strict: true);

        VerifyAgainstFixture("geojson-feature-collection", tile);
    }

    [Test]
    public void Layers_CustomExtentEmptyLayerAndWorldTile()
    {
        var tile = new TileBuilder(z: 0, x: 0, y: 0, extent: 512);
        tile.Layer("empty");

        tile.Layer("cities")
            .AddPoint(lat: 59.3293, lng: 18.0686, new Dictionary<string, object> { ["name"] = "Stockholm" })
            .AddPoint(lat: -33.8688, lng: 151.2093, new Dictionary<string, object> { ["name"] = "Sydney" })
            .AddPoint(lat: 64.1466, lng: -21.9426, new Dictionary<string, object> { ["name"] = "Reykjavík" })
            .AddPoint(lat: 89.9, lng: 0.0, new Dictionary<string, object> { ["name"] = "near pole" });
        tile.Layer("land")
            .AddPolygon([(70, -10), (70, 40), (35, 40), (35, -10)], new Dictionary<string, object> { ["name"] = "europe-ish" });
        tile.Layer("cities").AddPoint(lat: 35.6762, lng: 139.6503, new Dictionary<string, object> { ["name"] = "Tokyo" });
        tile.Layer("land").AddLineString([(60, 179.9), (-60, 179.9)]);

        VerifyAgainstFixture("layers-extent-world", tile);
    }

    private static void VerifyAgainstFixture(string name, TileBuilder tile)
    {
        var bytes = tile.Build();
        var decoded = Tile.Parser.ParseFrom(bytes);

        Assert.That(decoded.ToByteArray(), Is.EqualTo(bytes));
        SpecConformance.Validate(decoded);

        if (Environment.GetEnvironmentVariable("MVTNET_UPDATE_SPEC_VERIFY") == "1")
        {
            File.WriteAllBytes(Path.Combine(SourceFixtureDirectory(), $"{name}.mvt"), bytes);
            Assert.Pass($"Updated {name}.mvt ({bytes.Length} bytes).");
        }

        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "SpecVerify", $"{name}.mvt");
        Assert.That(File.Exists(path), Is.True, $"Missing {path}. Run once with MVTNET_UPDATE_SPEC_VERIFY=1 to create it.");
        Assert.That(bytes, Is.EqualTo(File.ReadAllBytes(path)));
    }

    private static string SourceFixtureDirectory([CallerFilePath] string thisFile = "") =>
        Path.Combine(Path.GetDirectoryName(thisFile)!, "SpecVerify");

    private static (double Lat, double Lng)[] Ellipse(double centerLat, double centerLng, double latRadius, double lngRadius, int vertices)
    {
        var ring = new (double Lat, double Lng)[vertices];
        for (int i = 0; i < vertices; i++)
        {
            double angle = 2 * Math.PI * i / vertices;
            ring[i] = (centerLat + Math.Sin(angle) * latRadius, centerLng + Math.Cos(angle) * lngRadius);
        }

        return ring;
    }

    private sealed class DeterministicRandom(uint seed)
    {
        private uint _state = seed;

        public double NextDouble()
        {
            _state = _state * 1664525u + 1013904223u;
            return _state / 4294967296.0;
        }
    }
}
