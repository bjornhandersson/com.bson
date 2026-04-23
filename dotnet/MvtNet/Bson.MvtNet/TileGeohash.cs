namespace Bson.MvtNet;

/// <summary>
/// Translates tile coordinates (z/x/y) into geohash prefixes for efficient
/// database queries against geohash-indexed tables.
/// </summary>
public static class TileGeohash
{
    /// <summary>
    /// Returns the set of geohash prefixes that cover the tile's bounding box.
    /// Use these for <c>WHERE geohash LIKE 'prefix%'</c> queries.
    /// </summary>
    public static IReadOnlyList<string> GetPrefixes(int z, int x, int y)
    {
        int precision = GetPrecision(z);
        var bounds = TileMath.GetTileBounds(z, x, y);

        return Geohash.GetCovering(bounds.South, bounds.North, bounds.West, bounds.East, precision);
    }

    /// <summary>
    /// Maps zoom level to geohash precision.
    /// Tuned for 4–16 prefixes per tile.
    /// </summary>
    public static int GetPrecision(int z)
    {
        // Geohash cell sizes vs tile widths (at equator):
        // P2: ~5.6°×11.25°   z4 tile: ~22.5° → ~4×2=8 cells
        // P3: ~1.4°×1.4°     z7 tile: ~2.8°  → ~2×2=4 cells
        // P4: ~0.18°×0.35°   z9 tile: ~0.7°  → ~4×2=8 cells
        // P5: ~0.044°        z12 tile: ~0.088° → ~2×2=4 cells
        // P6: ~0.006°×0.011° z14 tile: ~0.022° → ~4×2=8 cells
        // P7: ~0.001°        z17 tile: ~0.003° → ~3×3=9 cells
        return z switch
        {
            < 4 => 1,
            < 7 => 2,
            < 9 => 3,
            < 12 => 4,
            < 14 => 5,
            < 17 => 6,
            _ => 7,
        };
    }
}
