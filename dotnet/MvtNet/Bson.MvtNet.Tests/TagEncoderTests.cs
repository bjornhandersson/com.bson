using VectorTile;

namespace Bson.MvtNet.Tests;

public class TagEncoderTests
{
    private static Tile.Types.Layer Encode<TValue>(
        params IEnumerable<KeyValuePair<string, TValue>>[] attributeSets
    )
    {
        var builder = new TileBuilder(12, 2253, 1204);
        var layer = builder.Layer("t");
        foreach (var attributes in attributeSets)
        {
            layer.AddPoint(59.3281936, 18.0440866, attributes);
        }

        return Tile.Parser.ParseFrom(builder.Build()).Layers[0];
    }

    [Test]
    public void Encode_ProducesCorrectIndexPairs()
    {
        var layer = Encode(
            new Dictionary<string, object> { ["name"] = "Stockholm", ["population"] = 1000000L }
        );

        Assert.That(layer.Features[0].Tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(layer.Keys, Has.Count.EqualTo(2));
        Assert.That(layer.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_DeduplicatesKeysAndValues()
    {
        var layer = Encode(
            new Dictionary<string, object> { ["name"] = "A" },
            new Dictionary<string, object> { ["name"] = "B" }
        );

        Assert.That(layer.Features[1].Tags, Is.EqualTo(new uint[] { 0, 1 }));
        Assert.That(layer.Keys, Has.Count.EqualTo(1));
        Assert.That(layer.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_DoubleValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["speed"] = 42.5 });

        Assert.That(layer.Values[0].DoubleValue, Is.EqualTo(42.5));
    }

    [Test]
    public void Encode_FloatValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["temp"] = 3.14f });

        Assert.That(layer.Values[0].FloatValue, Is.EqualTo(3.14f).Within(0.001));
    }

    [Test]
    public void Encode_IntValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["count"] = 42 });

        Assert.That(layer.Values[0].SintValue, Is.EqualTo(42));
    }

    [Test]
    public void Encode_NegativeIntValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["delta"] = -1L, ["min"] = long.MinValue });

        Assert.That(layer.Values[0].SintValue, Is.EqualTo(-1));
        Assert.That(layer.Values[1].SintValue, Is.EqualTo(long.MinValue));
    }

    [Test]
    public void Encode_BoolValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["active"] = true });

        Assert.That(layer.Values[0].BoolValue, Is.True);
    }

    [Test]
    public void Encode_DeduplicatesSameDoubleValue()
    {
        var layer = Encode(
            new Dictionary<string, object> { ["a"] = 1.5 },
            new Dictionary<string, object> { ["b"] = 1.5 }
        );

        Assert.That(layer.Values, Has.Count.EqualTo(1));
    }

    [Test]
    public void Encode_UnsupportedType_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Encode(new Dictionary<string, object> { ["tags"] = new[] { "a", "b" } })
        );
    }

    [Test]
    public void Encode_GuidAndDates_BecomeIso8601Strings()
    {
        var guid = Guid.NewGuid();
        var utc = new DateTime(2026, 9, 5, 12, 30, 0, DateTimeKind.Utc);
        var offset = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.FromHours(2));

        var layer = Encode(
            new Dictionary<string, object>
            {
                ["id"] = guid,
                ["seen"] = utc,
                ["local"] = offset,
                ["grade"] = 'A',
            }
        );

        Assert.That(layer.Values[0].StringValue, Is.EqualTo(guid.ToString()));
        Assert.That(layer.Values[1].StringValue, Is.EqualTo("2026-09-05T12:30:00.0000000Z"));
        Assert.That(layer.Values[2].StringValue, Is.EqualTo("2026-09-05T14:30:00.0000000+02:00"));
        Assert.That(layer.Values[3].StringValue, Is.EqualTo("A"));
    }

    [Test]
    public void Encode_TypedDictionary_Works()
    {
        var layer = Encode(new Dictionary<string, double> { ["speed"] = 82.5 });

        Assert.That(layer.Features[0].Tags, Is.EqualTo(new uint[] { 0, 0 }));
        Assert.That(layer.Values[0].DoubleValue, Is.EqualTo(82.5));
    }

    [Test]
    public void Encode_NullValueInDictionary_IsSkipped()
    {
        var layer = Encode(
            new Dictionary<string, object>
            {
                ["name"] = "Stockholm",
                ["nickname"] = null!,
                ["population"] = 1000000L,
            }
        );

        Assert.That(layer.Features[0].Tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(layer.Keys, Is.EqualTo(new[] { "name", "population" }));
        Assert.That(layer.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_NullValueInLazyEnumerable_IsSkipped()
    {
        static IEnumerable<KeyValuePair<string, object>> Attributes()
        {
            yield return new("a", 1);
            yield return new("b", null!);
            yield return new("c", true);
        }

        var layer = Encode(Attributes());

        Assert.That(layer.Features[0].Tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(layer.Keys, Is.EqualTo(new[] { "a", "c" }));
    }

    [Test]
    public void Encode_AllValuesNull_ProducesNoTags()
    {
        var layer = Encode(new Dictionary<string, object> { ["x"] = null! });

        Assert.That(layer.Features[0].Tags, Is.Empty);
        Assert.That(layer.Keys, Is.Empty);
        Assert.That(layer.Values, Is.Empty);
    }

    private enum Status
    {
        Active,
        Idle,
    }

    [Test]
    public void Encode_SmallIntegerTypes_BecomeSintValues()
    {
        var layer = Encode(
            new Dictionary<string, object>
            {
                ["a"] = (byte)1,
                ["b"] = (sbyte)-2,
                ["c"] = (short)-3,
                ["d"] = (ushort)4,
                ["e"] = 5u,
            }
        );

        Assert.That(layer.Values.Select(v => v.SintValue), Is.EqualTo(new long[] { 1, -2, -3, 4, 5 }));
    }

    [Test]
    public void Encode_ULongValue_BecomesUintValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["big"] = ulong.MaxValue });

        Assert.That(layer.Values[0].UintValue, Is.EqualTo(ulong.MaxValue));
    }

    [Test]
    public void Encode_DecimalValue_BecomesDoubleValue()
    {
        var layer = Encode(new Dictionary<string, object> { ["price"] = 19.99m });

        Assert.That(layer.Values[0].DoubleValue, Is.EqualTo(19.99));
    }

    [Test]
    public void Encode_EnumValue_BecomesItsName()
    {
        var layer = Encode(new Dictionary<string, object> { ["status"] = Status.Idle });

        Assert.That(layer.Values[0].StringValue, Is.EqualTo("Idle"));
    }

    [Test]
    public void Encode_ManyDistinctValues_IndexesPastOneByte()
    {
        var sets = Enumerable
            .Range(0, 300)
            .Select(i => new Dictionary<string, object> { [$"k{i}"] = $"v{i}" })
            .ToArray();

        var layer = Encode(sets);

        Assert.That(layer.Keys, Has.Count.EqualTo(300));
        Assert.That(layer.Features[299].Tags, Is.EqualTo(new uint[] { 299, 299 }));
        Assert.That(layer.Values[299].StringValue, Is.EqualTo("v299"));
    }
}
