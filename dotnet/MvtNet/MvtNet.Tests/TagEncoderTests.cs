namespace MvtNet.Tests;

public class TagEncoderTests
{
    [Test]
    public void Encode_ProducesCorrectIndexPairs()
    {
        var encoder = new TagEncoder();
        var tags = encoder.Encode(
            new Dictionary<string, object> { ["name"] = "Stockholm", ["population"] = 1000000L }
        );

        // key 0 = "name", value 0 = "Stockholm"
        // key 1 = "population", value 1 = 1000000
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

        // "name" key is reused (index 0), "B" is new value (index 1)
        Assert.That(tags, Is.EqualTo(new uint[] { 0, 1 }));
        Assert.That(encoder.Keys, Has.Count.EqualTo(1));
        Assert.That(encoder.Values, Has.Count.EqualTo(2));
    }
}
