namespace MvtNet;

/// <summary>
/// Converts WGS84 coordinates to tile-local coordinates using Web Mercator projection.
/// </summary>
public static class TileMath
{
    public const uint DefaultExtent = 4096;

    /// <summary>
    /// Returns the WGS84 bounding box for a tile at the given z/x/y.
    /// </summary>
    public static TileBounds GetTileBounds(int z, int x, int y)
    {
        double n = Math.Pow(2, z);
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

        // Normalize longitude within tile (0..1)
        double tx = (lng - bounds.West) / (bounds.East - bounds.West);

        // Normalize latitude within tile using Mercator projection (0..1)
        double worldY = LatToMercatorY(lat);
        double northY = LatToMercatorY(bounds.North);
        double southY = LatToMercatorY(bounds.South);
        double ty = (worldY - northY) / (southY - northY);

        int px = Math.Clamp((int)Math.Round(tx * extent), 0, (int)extent);
        int py = Math.Clamp((int)Math.Round(ty * extent), 0, (int)extent);

        return new TileCoord(px, py);
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

    private static double LatFromY(int y, double n)
    {
        double latRad = Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * y / n)));
        return latRad * 180.0 / Math.PI;
    }

    private static double LatToMercatorY(double lat)
    {
        double latRad = lat * Math.PI / 180.0;
        return Math.Log(Math.Tan(Math.PI / 4.0 + latRad / 2.0));
    }
}

public readonly record struct TileBounds(double North, double South, double East, double West);

public readonly record struct TileCoord(int X, int Y);
