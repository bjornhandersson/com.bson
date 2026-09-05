namespace Bson.MvtNet;

internal static class TileMath
{
    public const uint DefaultExtent = 4096;

    // Kept outside the tile so fills and strokes meet across tile seams.
    internal const double DefaultClipBufferFraction = 0.05;

    public static TileBounds GetTileBounds(int z, int x, int y)
    {
        double n = 1 << z;
        double west = x / n * 360.0 - 180.0;
        double east = (x + 1) / n * 360.0 - 180.0;
        double north = LatFromY(y, n);
        double south = LatFromY(y + 1, n);

        return new TileBounds(north, south, east, west);
    }

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

    // Projects a point and reports whether it lands inside the tile extent
    // widened by `buffer` on every side. Out-of-range latitudes (NaN or
    // beyond the Mercator limit) fail the range check and are rejected.
    internal static bool TryProjectWithinBuffer(
        double lat,
        double lng,
        in TileProjectionContext ctx,
        double buffer,
        out TileCoord coord
    )
    {
        double px = (lng - ctx.West) * ctx.InvLngSpan * ctx.Extent;
        double py = (LatToMercatorY(lat) - ctx.NorthY) * ctx.InvMercatorSpan * ctx.Extent;

        double min = -buffer;
        double max = ctx.Extent + buffer;

        if (!(px >= min && px <= max && py >= min && py <= max))
        {
            coord = default;
            return false;
        }

        coord = new TileCoord((int)Math.Round(px), (int)Math.Round(py));
        return true;
    }

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
