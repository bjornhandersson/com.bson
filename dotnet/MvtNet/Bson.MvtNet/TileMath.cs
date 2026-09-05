namespace Bson.MvtNet;

/// <summary>
/// Converts WGS84 coordinates to tile-local coordinates using Web Mercator projection.
/// </summary>
internal static class TileMath
{
    public const uint DefaultExtent = 4096;

    /// <summary>
    /// Returns the WGS84 bounding box for a tile at the given z/x/y.
    /// </summary>
    public static TileBounds GetTileBounds(int z, int x, int y)
    {
        double n = 1 << z;
        double west = x / n * 360.0 - 180.0;
        double east = (x + 1) / n * 360.0 - 180.0;
        double north = LatFromY(y, n);
        double south = LatFromY(y + 1, n);

        return new TileBounds(north, south, east, west);
    }

    /// <summary>
    /// Projects a WGS84 point into tile-local integer coordinates (0..extent).
    /// Returns null if the point is outside the tile bounds.
    /// </summary>
    public static TileCoord? ProjectPoint(
        double lat,
        double lng,
        int z,
        int x,
        int y,
        uint extent = DefaultExtent
    )
    {
        var bounds = GetTileBounds(z, x, y);

        if (lat < bounds.South || lat > bounds.North || lng < bounds.West || lng > bounds.East)
        {
            return null;
        }

        return ProjectWithBounds(lat, lng, bounds, extent);
    }

    /// <summary>
    /// Projects a WGS84 point into tile-local coordinates without bounds checking.
    /// Coordinates may be negative or exceed the extent — this is valid for MVT
    /// geometry that crosses tile boundaries (LineStrings, Polygons).
    /// </summary>
    internal static TileCoord ProjectPointUnclamped(
        double lat,
        double lng,
        int z,
        int x,
        int y,
        uint extent = DefaultExtent
    )
    {
        var bounds = GetTileBounds(z, x, y);
        return ProjectWithBounds(lat, lng, bounds, extent);
    }

    /// <summary>
    /// Projects a WGS84 point using pre-computed tile bounds and Mercator Y values.
    /// Use this when projecting many points on the same tile to avoid redundant work.
    /// </summary>
    internal static TileCoord ProjectWithContext(
        double lat,
        double lng,
        in TileProjectionContext ctx
    )
    {
        double tx = (lng - ctx.West) * ctx.InvLngSpan;
        double worldY = LatToMercatorY(lat);
        double ty = (worldY - ctx.NorthY) * ctx.InvMercatorSpan;

        int px = (int)Math.Round(tx * ctx.Extent);
        int py = (int)Math.Round(ty * ctx.Extent);

        return new TileCoord(px, py);
    }

    /// <summary>
    /// Creates a reusable projection context for a tile. Pre-computes bounds and
    /// Mercator Y values so they aren't recalculated per point.
    /// </summary>
    internal static TileProjectionContext CreateProjectionContext(
        int z,
        int x,
        int y,
        uint extent = DefaultExtent
    )
    {
        var bounds = GetTileBounds(z, x, y);
        double northY = LatToMercatorY(bounds.North);
        double southY = LatToMercatorY(bounds.South);

        return new TileProjectionContext(
            bounds,
            extent,
            northY,
            1.0 / (bounds.East - bounds.West),
            1.0 / (southY - northY)
        );
    }

    /// <summary>
    /// Checks whether a WGS84 point falls within the given tile.
    /// </summary>
    public static bool Contains(double lat, double lng, int z, int x, int y)
    {
        var bounds = GetTileBounds(z, x, y);
        return lat >= bounds.South
            && lat <= bounds.North
            && lng >= bounds.West
            && lng <= bounds.East;
    }

    private static TileCoord ProjectWithBounds(
        double lat,
        double lng,
        TileBounds bounds,
        uint extent
    )
    {
        double tx = (lng - bounds.West) / (bounds.East - bounds.West);

        double worldY = LatToMercatorY(lat);
        double northY = LatToMercatorY(bounds.North);
        double southY = LatToMercatorY(bounds.South);
        double ty = (worldY - northY) / (southY - northY);

        int px = (int)Math.Round(tx * extent);
        int py = (int)Math.Round(ty * extent);

        return new TileCoord(px, py);
    }

    private static double LatFromY(int y, double n)
    {
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * y / n)));
        return latRad * 180.0 / Math.PI;
    }

    internal static double LatToMercatorY(double lat)
    {
        double latRad = lat * Math.PI / 180.0;
        return Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0));
    }
}

/// <summary>
/// Pre-computed projection parameters for a tile. Avoids recalculating bounds
/// and Mercator transforms when projecting many points on the same tile.
/// </summary>
internal readonly record struct TileProjectionContext(
    TileBounds Bounds,
    uint Extent,
    double NorthY,
    double InvLngSpan,
    double InvMercatorSpan
)
{
    public double West => Bounds.West;
}

internal readonly record struct TileBounds(double North, double South, double East, double West);

internal readonly record struct TileCoord(int X, int Y);
