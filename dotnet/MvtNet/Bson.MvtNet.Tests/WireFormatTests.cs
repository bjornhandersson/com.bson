using Google.Protobuf;
using VectorTile;

namespace Bson.MvtNet.Tests;

public class WireFormatTests
{
    private const int Z = 12;
    private const int X = 2253;
    private const int Y = 1204;

    private enum Kind
    {
        Alpha,
        Beta,
    }

    private static TileBuilder RichTile(uint extent = 4096)
    {
        var builder = new TileBuilder(Z, X, Y, extent);

        builder
            .Layer("点 layer")
            .AddPoint(59.3281936, 18.0440866)
            .AddPoint(
                59.3281936,
                18.0440866,
                new Dictionary<string, object>
                {
                    ["名前"] = "Stockholm ✓",
                    ["c"] = 'x',
                    ["kind"] = Kind.Beta,
                    ["on"] = true,
                    ["off"] = false,
                    ["i"] = -7,
                    ["l"] = long.MaxValue,
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
                },
                id: ulong.MaxValue
            )
            .AddPoint(59.3281936, 18.0440866, new Dictionary<string, string> { ["名前"] = "again" }, id: 0);

        var bounds = TileMath.GetTileBounds(Z, X, Y);
        double midLat = (bounds.North + bounds.South) / 2;
        double lngSpan = bounds.East - bounds.West;
        builder
            .Layer("lines")
            .AddLineString(
                new (double, double)[]
                {
                    (midLat, bounds.West + lngSpan * 0.2),
                    (bounds.North + 1.0, bounds.West + lngSpan * 0.3),
                    (bounds.North + 1.0, bounds.West + lngSpan * 0.7),
                    (midLat, bounds.West + lngSpan * 0.8),
                },
                new Dictionary<string, object> { ["name"] = "u" }
            )
            .AddLineString(new (double, double)[] { (59.334, 18.03), (59.3326, 18.0649) }, id: 5);

        var outer = new (double, double)[] { (59.34, 18.03), (59.34, 18.07), (59.32, 18.07), (59.32, 18.03) };
        var hole = new (double, double)[] { (59.335, 18.04), (59.335, 18.06), (59.325, 18.06), (59.325, 18.04) };
        builder.Layer("polys").AddPolygon(outer, new[] { hole }, new Dictionary<string, object> { ["z"] = 1 });

        builder.Layer("empty");

        return builder;
    }

    [Test]
    public void Build_RoundTripsThroughReferenceImplementation_ByteForByte()
    {
        var bytes = RichTile().Build();

        var parsed = Tile.Parser.ParseFrom(bytes);

        Assert.That(parsed.ToByteArray(), Is.EqualTo(bytes));
    }

    [Test]
    public void Build_CustomExtent_RoundTripsByteForByte()
    {
        var bytes = RichTile(extent: 512).Build();

        var parsed = Tile.Parser.ParseFrom(bytes);

        Assert.That(parsed.ToByteArray(), Is.EqualTo(bytes));
        Assert.That(parsed.Layers.Select(l => l.Extent), Is.All.EqualTo(512u));
    }

    [Test]
    public void Build_LargeLayer_RoundTripsByteForByte()
    {
        var builder = new TileBuilder(Z, X, Y);
        var layer = builder.Layer("big");
        for (int i = 0; i < 5000; i++)
        {
            layer.AddPoint(
                59.3281936,
                18.0440866,
                new Dictionary<string, object> { ["i"] = i, ["name"] = $"vehicle-{i}", ["speed"] = i * 0.5 }
            );
        }

        var bytes = builder.Build();
        var parsed = Tile.Parser.ParseFrom(bytes);

        Assert.That(bytes.Length, Is.GreaterThan(16383));
        Assert.That(parsed.Layers[0].Features, Has.Count.EqualTo(5000));
        Assert.That(parsed.ToByteArray(), Is.EqualTo(bytes));
    }

    [Test]
    public void Build_FieldPresenceAndValues_MatchWhatWasAdded()
    {
        var tile = Tile.Parser.ParseFrom(RichTile().Build());

        Assert.That(tile.Layers.Select(l => l.Name), Is.EqualTo(new[] { "点 layer", "lines", "polys", "empty" }));

        var points = tile.Layers[0];
        Assert.That(points.Version, Is.EqualTo(2u));
        Assert.That(points.HasExtent, Is.True);
        Assert.That(points.Extent, Is.EqualTo(4096u));
        Assert.That(points.Features.Select(f => f.HasId), Is.All.True);
        Assert.That(points.Features.Select(f => f.Id), Is.EqualTo(new[] { 1UL, ulong.MaxValue, 0UL }));
        Assert.That(points.Features.Select(f => f.Type), Is.All.EqualTo(Tile.Types.GeomType.Point));
        Assert.That(points.Features[0].Tags, Is.Empty);
        Assert.That(points.Features[1].Tags, Has.Count.EqualTo(40));
        Assert.That(points.Features[2].Tags, Is.EqualTo(new uint[] { 0, 20 }));

        Assert.That(points.Keys[0], Is.EqualTo("名前"));
        Assert.That(points.Values[0].StringValue, Is.EqualTo("Stockholm ✓"));
        Assert.That(points.Values[1].StringValue, Is.EqualTo("x"));
        Assert.That(points.Values[2].StringValue, Is.EqualTo("Beta"));
        Assert.That(points.Values[3].BoolValue, Is.True);
        Assert.That(points.Values[4].BoolValue, Is.False);
        Assert.That(points.Values[5].SintValue, Is.EqualTo(-7));
        Assert.That(points.Values[6].SintValue, Is.EqualTo(long.MaxValue));
        Assert.That(points.Values[7].SintValue, Is.EqualTo(-3));
        Assert.That(points.Values[8].SintValue, Is.EqualTo(200));
        Assert.That(points.Values[9].SintValue, Is.EqualTo(-100));
        Assert.That(points.Values[10].SintValue, Is.EqualTo(60000));
        Assert.That(points.Values[11].SintValue, Is.EqualTo(uint.MaxValue));
        Assert.That(points.Values[12].UintValue, Is.EqualTo(ulong.MaxValue));
        Assert.That(points.Values[13].FloatValue, Is.EqualTo(1.5f));
        Assert.That(points.Values[14].DoubleValue, Is.EqualTo(-2.25));
        Assert.That(points.Values[15].DoubleValue, Is.EqualTo(19.99));
        Assert.That(points.Values[16].StringValue, Is.EqualTo("11111111-2222-3333-4444-555555555555"));
        Assert.That(points.Values[17].StringValue, Is.EqualTo("2026-09-05T12:30:00.0000000Z"));
        Assert.That(points.Values[18].StringValue, Is.EqualTo("2026-09-05T14:30:00.0000000+02:00"));
        Assert.That(points.Values[19].StringValue, Has.Length.EqualTo(300));
        Assert.That(points.Values[20].StringValue, Is.EqualTo("again"));

        var lines = tile.Layers[1];
        Assert.That(lines.Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Linestring));
        Assert.That(TestGeometry.DecodeParts(lines.Features[0].Geometry), Has.Count.EqualTo(2));
        Assert.That(lines.Features[1].Id, Is.EqualTo(5UL));

        var polys = tile.Layers[2];
        Assert.That(polys.Features[0].Type, Is.EqualTo(Tile.Types.GeomType.Polygon));
        Assert.That(TestGeometry.DecodeRings(polys.Features[0].Geometry), Has.Count.EqualTo(2));

        var empty = tile.Layers[3];
        Assert.That(empty.Features, Is.Empty);
        Assert.That(empty.Keys, Is.Empty);
        Assert.That(empty.Version, Is.EqualTo(2u));
    }

    [Test]
    public void Build_EmptyTile_IsZeroBytes()
    {
        Assert.That(new TileBuilder(Z, X, Y).Build(), Is.Empty);
    }

    [Test]
    public void BuildToStream_MatchesBuild()
    {
        var builder = RichTile();
        using var stream = new MemoryStream();

        builder.Build(stream);

        Assert.That(stream.ToArray(), Is.EqualTo(builder.Build()));
    }

    [Test]
    public void Build_UnsupportedAttribute_LeavesNoPartialFeature()
    {
        var builder = new TileBuilder(Z, X, Y);
        var layer = builder.Layer("t").AddPoint(59.3281936, 18.0440866);

        Assert.Throws<ArgumentException>(() =>
            layer.AddPoint(59.3281936, 18.0440866, new Dictionary<string, object> { ["bad"] = TimeSpan.Zero })
        );
        layer.AddPoint(59.3281936, 18.0440866);

        var parsed = Tile.Parser.ParseFrom(builder.Build());

        Assert.That(parsed.Layers[0].Features.Select(f => f.Id), Is.EqualTo(new ulong[] { 1, 2 }));
        Assert.That(parsed.ToByteArray(), Is.EqualTo(builder.Build()));
    }
}
