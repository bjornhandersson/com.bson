namespace Bson.MvtNet.Tests;

public class TagEncoderTests
{
    [Test]
    public void Encode_ProducesCorrectIndexPairs()
    {
        var encoder = new TagEncoder();
        var tags = encoder.Encode(
            new Dictionary<string, object> { ["name"] = "Stockholm", ["population"] = 1000000L }
        );

        Assert.That(tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(encoder.Keys, Has.Count.EqualTo(2));
        Assert.That(encoder.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_DeduplicatesKeysAndValues()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["name"] = "A" });
        var tags = encoder.Encode(new Dictionary<string, object> { ["name"] = "B" });

        Assert.That(tags, Is.EqualTo(new uint[] { 0, 1 }));
        Assert.That(encoder.Keys, Has.Count.EqualTo(1));
        Assert.That(encoder.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_DoubleValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["speed"] = 42.5 });

        Assert.That(encoder.Values[0].DoubleValue, Is.EqualTo(42.5));
    }

    [Test]
    public void Encode_FloatValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["temp"] = 3.14f });

        Assert.That(encoder.Values[0].FloatValue, Is.EqualTo(3.14f).Within(0.001));
    }

    [Test]
    public void Encode_IntValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["count"] = 42 });

        Assert.That(encoder.Values[0].SintValue, Is.EqualTo(42));
    }

    [Test]
    public void Encode_BoolValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["active"] = true });

        Assert.That(encoder.Values[0].BoolValue, Is.True);
    }

    [Test]
    public void Encode_DeduplicatesSameDoubleValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["a"] = 1.5 });
        encoder.Encode(new Dictionary<string, object> { ["b"] = 1.5 });

        Assert.That(encoder.Values, Has.Count.EqualTo(1));
    }

    [Test]
    public void Encode_UnsupportedType_Throws()
    {
        var encoder = new TagEncoder();

        Assert.Throws<ArgumentException>(() =>
            encoder.Encode(new Dictionary<string, object> { ["tags"] = new[] { "a", "b" } })
        );
    }

    [Test]
    public void Encode_GuidAndDates_BecomeIso8601Strings()
    {
        var encoder = new TagEncoder();
        var guid = Guid.NewGuid();
        var utc = new DateTime(2026, 9, 5, 12, 30, 0, DateTimeKind.Utc);
        var offset = new DateTimeOffset(2026, 9, 5, 14, 30, 0, TimeSpan.FromHours(2));

        encoder.Encode(
            new Dictionary<string, object>
            {
                ["id"] = guid,
                ["seen"] = utc,
                ["local"] = offset,
                ["grade"] = 'A',
            }
        );

        Assert.That(encoder.Values[0].StringValue, Is.EqualTo(guid.ToString()));
        Assert.That(encoder.Values[1].StringValue, Is.EqualTo("2026-09-05T12:30:00.0000000Z"));
        Assert.That(encoder.Values[2].StringValue, Is.EqualTo("2026-09-05T14:30:00.0000000+02:00"));
        Assert.That(encoder.Values[3].StringValue, Is.EqualTo("A"));
    }

    [Test]
    public void Encode_TypedDictionary_Works()
    {
        var encoder = new TagEncoder();
        var tags = encoder.Encode(new Dictionary<string, double> { ["speed"] = 82.5 });

        Assert.That(tags, Is.EqualTo(new uint[] { 0, 0 }));
        Assert.That(encoder.Values[0].DoubleValue, Is.EqualTo(82.5));
    }

    [Test]
    public void Encode_NullValueInDictionary_IsSkipped()
    {
        var encoder = new TagEncoder();
        var tags = encoder.Encode(
            new Dictionary<string, object>
            {
                ["name"] = "Stockholm",
                ["nickname"] = null!,
                ["population"] = 1000000L,
            }
        );

        Assert.That(tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(encoder.Keys, Is.EqualTo(new[] { "name", "population" }));
        Assert.That(encoder.Values, Has.Count.EqualTo(2));
    }

    [Test]
    public void Encode_NullValueInLazyEnumerable_IsSkipped()
    {
        var encoder = new TagEncoder();
        IEnumerable<KeyValuePair<string, object>> Attributes()
        {
            yield return new("a", 1);
            yield return new("b", null!);
            yield return new("c", true);
        }

        var tags = encoder.Encode(Attributes());

        Assert.That(tags, Is.EqualTo(new uint[] { 0, 0, 1, 1 }));
        Assert.That(encoder.Keys, Is.EqualTo(new[] { "a", "c" }));
    }

    [Test]
    public void Encode_AllValuesNull_ProducesNoTags()
    {
        var encoder = new TagEncoder();
        var tags = encoder.Encode(new Dictionary<string, object> { ["x"] = null! });

        Assert.That(tags, Is.Empty);
        Assert.That(encoder.Keys, Is.Empty);
        Assert.That(encoder.Values, Is.Empty);
    }

    private enum Status
    {
        Active,
        Idle,
    }

    [Test]
    public void Encode_SmallIntegerTypes_BecomeSintValues()
    {
        var encoder = new TagEncoder();
        encoder.Encode(
            new Dictionary<string, object>
            {
                ["a"] = (byte)1,
                ["b"] = (sbyte)-2,
                ["c"] = (short)-3,
                ["d"] = (ushort)4,
                ["e"] = 5u,
            }
        );

        Assert.That(encoder.Values.Select(v => v.SintValue), Is.EqualTo(new long[] { 1, -2, -3, 4, 5 }));
    }

    [Test]
    public void Encode_ULongValue_BecomesUintValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["big"] = ulong.MaxValue });

        Assert.That(encoder.Values[0].UintValue, Is.EqualTo(ulong.MaxValue));
    }

    [Test]
    public void Encode_DecimalValue_BecomesDoubleValue()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["price"] = 19.99m });

        Assert.That(encoder.Values[0].DoubleValue, Is.EqualTo(19.99));
    }

    [Test]
    public void Encode_EnumValue_BecomesItsName()
    {
        var encoder = new TagEncoder();
        encoder.Encode(new Dictionary<string, object> { ["status"] = Status.Idle });

        Assert.That(encoder.Values[0].StringValue, Is.EqualTo("Idle"));
    }
}
