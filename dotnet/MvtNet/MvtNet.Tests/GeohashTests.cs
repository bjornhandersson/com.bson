namespace Bson.MvtNet.Tests;

public class GeohashTests
{
    [Test]
    public void Encode_Stockholm_ReturnsKnownGeohash()
    {
        // Stockholm: 59.3281936, 18.0440866 → "u6scd" at precision 5
        string hash = Geohash.Encode(59.3281936, 18.0440866, 5);

        Assert.That(hash, Is.EqualTo("u6scd"));
    }

    [Test]
    public void Encode_London_ReturnsKnownGeohash()
    {
        // London: 51.5074, -0.1278 → "gcpvj" at precision 5
        string hash = Geohash.Encode(51.5074, -0.1278, 5);

        Assert.That(hash, Is.EqualTo("gcpvj"));
    }

    [Test]
    public void Encode_Precision1Through8_IncreasingLength()
    {
        for (int p = 1; p <= 8; p++)
        {
            string hash = Geohash.Encode(59.3281936, 18.0440866, p);
            Assert.That(hash.Length, Is.EqualTo(p));
        }
    }

    [Test]
    public void Encode_LongerPrecision_RefinesPrevious()
    {
        string p3 = Geohash.Encode(59.3281936, 18.0440866, 3);
        string p5 = Geohash.Encode(59.3281936, 18.0440866, 5);

        Assert.That(p5, Does.StartWith(p3));
    }

    [Test]
    public void Decode_RoundTrip_PointInsideBounds()
    {
        double lat = 59.3281936;
        double lng = 18.0440866;
        string hash = Geohash.Encode(lat, lng, 7);

        var bounds = Geohash.Decode(hash);

        Assert.That(lat, Is.InRange(bounds.South, bounds.North));
        Assert.That(lng, Is.InRange(bounds.West, bounds.East));
    }

    [Test]
    public void Decode_HigherPrecision_SmallerBounds()
    {
        var b3 = Geohash.Decode(Geohash.Encode(59.33, 18.04, 3));
        var b7 = Geohash.Decode(Geohash.Encode(59.33, 18.04, 7));

        double w3 = b3.East - b3.West;
        double w7 = b7.East - b7.West;

        Assert.That(w7, Is.LessThan(w3));
    }

    [Test]
    public void Encode_NegativeCoordinates_Works()
    {
        // Buenos Aires: -34.6037, -58.3816
        string hash = Geohash.Encode(-34.6037, -58.3816, 5);

        Assert.That(hash.Length, Is.EqualTo(5));

        var bounds = Geohash.Decode(hash);
        Assert.That(bounds.South, Is.LessThanOrEqualTo(-34.6037));
        Assert.That(bounds.North, Is.GreaterThanOrEqualTo(-34.6037));
        Assert.That(bounds.West, Is.LessThanOrEqualTo(-58.3816));
        Assert.That(bounds.East, Is.GreaterThanOrEqualTo(-58.3816));
    }

    [Test]
    public void Encode_InvalidPrecision_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Geohash.Encode(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Geohash.Encode(0, 0, 13));
    }

    [Test]
    public void Decode_InvalidCharacter_Throws()
    {
        Assert.Throws<ArgumentException>(() => Geohash.Decode("u6sc!"));
    }

    [Test]
    public void Decode_OriginPoint_Works()
    {
        string hash = Geohash.Encode(0, 0, 5);
        var bounds = Geohash.Decode(hash);

        Assert.That(bounds.South, Is.LessThanOrEqualTo(0.0));
        Assert.That(bounds.North, Is.GreaterThanOrEqualTo(0.0));
        Assert.That(bounds.West, Is.LessThanOrEqualTo(0.0));
        Assert.That(bounds.East, Is.GreaterThanOrEqualTo(0.0));
    }
}
