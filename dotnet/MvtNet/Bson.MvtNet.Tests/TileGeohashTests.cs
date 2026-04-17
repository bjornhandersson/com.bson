namespace Bson.MvtNet.Tests;

public class TileGeohashTests
{
    [Test]
    public void GetPrefixes_Stockholm_Z12_ReturnsPrefixes()
    {
        // Stockholm tile at z12 — should return geohash prefixes covering the tile
        var prefixes = TileGeohash.GetPrefixes(12, 2253, 1204);

        Assert.That(prefixes, Is.Not.Empty);
        Assert.That(prefixes.Count, Is.InRange(1, 30));

        // Stockholm is in the "u6sc" area — at least one prefix should start with "u6sc"
        Assert.That(prefixes.Any(p => p.StartsWith("u6sc")), Is.True);
    }

    [Test]
    public void GetPrefixes_AllSamePrecision()
    {
        var prefixes = TileGeohash.GetPrefixes(12, 2253, 1204);

        int expectedPrecision = TileGeohash.GetPrecision(12);
        Assert.That(prefixes.All(p => p.Length == expectedPrecision), Is.True);
    }

    [Test]
    public void GetPrefixes_LowZoom_ShorterPrefixes()
    {
        var lowZoom = TileGeohash.GetPrefixes(4, 9, 4);
        var highZoom = TileGeohash.GetPrefixes(14, 9137, 4818);

        Assert.That(lowZoom[0].Length, Is.LessThan(highZoom[0].Length));
    }

    [Test]
    public void GetPrefixes_CoversTileBounds()
    {
        int z = 12,
            x = 2253,
            y = 1204;
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var tileBounds = TileMath.GetTileBounds(z, x, y);

        // The center of the tile should be covered by at least one prefix
        double centerLat = (tileBounds.North + tileBounds.South) / 2;
        double centerLng = (tileBounds.East + tileBounds.West) / 2;
        int precision = TileGeohash.GetPrecision(z);
        string centerHash = Geohash.Encode(centerLat, centerLng, precision);

        Assert.That(prefixes, Does.Contain(centerHash));
    }

    [Test]
    public void GetPrefixes_CornersCovered()
    {
        int z = 12,
            x = 2253,
            y = 1204;
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var bounds = TileMath.GetTileBounds(z, x, y);
        int precision = TileGeohash.GetPrecision(z);

        // Nudge slightly inward from corners to stay inside tile
        double eps = 0.0001;
        string sw = Geohash.Encode(bounds.South + eps, bounds.West + eps, precision);
        string ne = Geohash.Encode(bounds.North - eps, bounds.East - eps, precision);

        Assert.That(prefixes, Does.Contain(sw));
        Assert.That(prefixes, Does.Contain(ne));
    }

    [Test]
    public void GetPrefixes_NoDuplicates()
    {
        var prefixes = TileGeohash.GetPrefixes(12, 2253, 1204);

        Assert.That(prefixes.Distinct().Count(), Is.EqualTo(prefixes.Count));
    }

    [Test]
    public void GetRange_Stockholm_Z12_ValidRange()
    {
        var range = TileGeohash.GetRange(12, 2253, 1204);

        Assert.That(range.Min, Is.Not.Empty);
        Assert.That(range.Max, Is.Not.Empty);
        Assert.That(string.CompareOrdinal(range.Min, range.Max), Is.LessThanOrEqualTo(0));
    }

    [Test]
    public void GetRange_ContainsAllPrefixes()
    {
        int z = 12,
            x = 2253,
            y = 1204;
        var prefixes = TileGeohash.GetPrefixes(z, x, y);
        var range = TileGeohash.GetRange(z, x, y);

        // Every prefix should fall within the range
        foreach (var prefix in prefixes)
        {
            Assert.That(
                string.CompareOrdinal(prefix, range.Min),
                Is.GreaterThanOrEqualTo(0),
                $"Prefix {prefix} is below range min {range.Min}"
            );
            Assert.That(
                string.CompareOrdinal(prefix, range.Max),
                Is.LessThanOrEqualTo(0),
                $"Prefix {prefix} is above range max {range.Max}"
            );
        }
    }

    [Test]
    public void GetPrecision_ZoomMapping()
    {
        Assert.That(TileGeohash.GetPrecision(0), Is.EqualTo(1));
        Assert.That(TileGeohash.GetPrecision(3), Is.EqualTo(1));
        Assert.That(TileGeohash.GetPrecision(4), Is.EqualTo(2));
        Assert.That(TileGeohash.GetPrecision(6), Is.EqualTo(2));
        Assert.That(TileGeohash.GetPrecision(7), Is.EqualTo(3));
        Assert.That(TileGeohash.GetPrecision(9), Is.EqualTo(4));
        Assert.That(TileGeohash.GetPrecision(11), Is.EqualTo(4));
        Assert.That(TileGeohash.GetPrecision(12), Is.EqualTo(5));
        Assert.That(TileGeohash.GetPrecision(14), Is.EqualTo(6));
        Assert.That(TileGeohash.GetPrecision(17), Is.EqualTo(7));
    }

    [Test]
    public void GetPrefixes_VariousZoomLevels_ReasonableCount()
    {
        // Across zoom levels, prefix count should stay manageable
        int[] zooms = { 4, 7, 10, 12, 14 };
        foreach (int z in zooms)
        {
            int x = (int)(Math.Pow(2, z) * (18.04 + 180) / 360);
            int y = (int)(
                Math.Pow(2, z)
                * (
                    1.0
                    - Math.Log(
                        Math.Tan(59.33 * Math.PI / 180) + 1.0 / Math.Cos(59.33 * Math.PI / 180)
                    ) / Math.PI
                )
                / 2.0
            );

            var prefixes = TileGeohash.GetPrefixes(z, x, y);

            Assert.That(
                prefixes.Count,
                Is.InRange(1, 50),
                $"z{z}: got {prefixes.Count} prefixes, expected 1–50"
            );
        }
    }

    [Test]
    public void GetPrefixes_NearAntimeridian_Works()
    {
        // Tile near the antimeridian (180°) — Fiji area, z8
        int z = 8,
            x = 255,
            y = 128;
        var prefixes = TileGeohash.GetPrefixes(z, x, y);

        Assert.That(prefixes, Is.Not.Empty);
    }

    [Test]
    public void GetPrefixes_NearPole_Works()
    {
        // Tile near the north pole, z4
        int z = 4,
            x = 8,
            y = 0;
        var prefixes = TileGeohash.GetPrefixes(z, x, y);

        Assert.That(prefixes, Is.Not.Empty);
    }
}
