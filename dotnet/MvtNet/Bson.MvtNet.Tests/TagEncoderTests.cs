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
            encoder.Encode(new Dictionary<string, object> { ["date"] = DateTime.Now })
        );
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
}
