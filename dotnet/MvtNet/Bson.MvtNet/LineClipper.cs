namespace Bson.MvtNet;

/// <summary>
/// Clips a LineString to a rectangular region defined by (min, min) to (max, max).
/// Returns a list of clipped segments — each segment is a separate continuous run
/// of coordinates inside the clip region.
/// </summary>
internal static class LineClipper
{
    private const double DefaultBufferFraction = 0.05;

    /// <summary>
    /// Clips a projected LineString to the tile extent plus a buffer margin.
    /// Returns zero or more clipped segments.
    /// </summary>
    public static List<TileCoord[]> Clip(
        TileCoord[] coords,
        uint extent,
        double bufferFraction = DefaultBufferFraction
    )
    {
        double buffer = extent * bufferFraction;
        double min = -buffer;
        double max = extent + buffer;

        return ClipToRect(coords, min, min, max, max);
    }

    private static List<TileCoord[]> ClipToRect(
        TileCoord[] coords,
        double minX,
        double minY,
        double maxX,
        double maxY
    )
    {
        var segments = new List<TileCoord[]>();
        var current = new List<TileCoord>();

        for (int i = 0; i < coords.Length - 1; i++)
        {
            var a = coords[i];
            var b = coords[i + 1];

            var clipped = ClipSegment(a.X, a.Y, b.X, b.Y, minX, minY, maxX, maxY);

            if (clipped is null)
            {
                // Segment is entirely outside — flush current run
                if (current.Count >= 2)
                {
                    segments.Add(current.ToArray());
                }
                current.Clear();
                continue;
            }

            var (cx1, cy1, cx2, cy2) = clipped.Value;
            var clippedA = new TileCoord(cx1, cy1);
            var clippedB = new TileCoord(cx2, cy2);

            if (current.Count == 0)
            {
                current.Add(clippedA);
            }
            else if (current[^1].X != clippedA.X || current[^1].Y != clippedA.Y)
            {
                // Discontinuity — the clipped start doesn't match previous end
                if (current.Count >= 2)
                {
                    segments.Add(current.ToArray());
                }
                current.Clear();
                current.Add(clippedA);
            }

            current.Add(clippedB);
        }

        if (current.Count >= 2)
        {
            segments.Add(current.ToArray());
        }

        return segments;
    }

    /// <summary>
    /// Cohen-Sutherland line clipping. Returns clipped segment or null if entirely outside.
    /// </summary>
    private static (int X1, int Y1, int X2, int Y2)? ClipSegment(
        double x1,
        double y1,
        double x2,
        double y2,
        double minX,
        double minY,
        double maxX,
        double maxY
    )
    {
        int code1 = ComputeCode(x1, y1, minX, minY, maxX, maxY);
        int code2 = ComputeCode(x2, y2, minX, minY, maxX, maxY);

        while (true)
        {
            if ((code1 | code2) == 0)
            {
                // Both inside
                return (
                    (int)Math.Round(x1),
                    (int)Math.Round(y1),
                    (int)Math.Round(x2),
                    (int)Math.Round(y2)
                );
            }

            if ((code1 & code2) != 0)
            {
                // Both outside same side
                return null;
            }

            int codeOut = code1 != 0 ? code1 : code2;
            double x,
                y;

            if ((codeOut & 8) != 0) // above (y > maxY)
            {
                x = x1 + (x2 - x1) * (maxY - y1) / (y2 - y1);
                y = maxY;
            }
            else if ((codeOut & 4) != 0) // below (y < minY)
            {
                x = x1 + (x2 - x1) * (minY - y1) / (y2 - y1);
                y = minY;
            }
            else if ((codeOut & 2) != 0) // right (x > maxX)
            {
                y = y1 + (y2 - y1) * (maxX - x1) / (x2 - x1);
                x = maxX;
            }
            else // left (x < minX)
            {
                y = y1 + (y2 - y1) * (minX - x1) / (x2 - x1);
                x = minX;
            }

            if (codeOut == code1)
            {
                x1 = x;
                y1 = y;
                code1 = ComputeCode(x1, y1, minX, minY, maxX, maxY);
            }
            else
            {
                x2 = x;
                y2 = y;
                code2 = ComputeCode(x2, y2, minX, minY, maxX, maxY);
            }
        }
    }

    private static int ComputeCode(
        double x,
        double y,
        double minX,
        double minY,
        double maxX,
        double maxY
    )
    {
        int code = 0;
        if (x < minX)
        {
            code |= 1;
        } // left
        if (x > maxX)
        {
            code |= 2;
        } // right
        if (y < minY)
        {
            code |= 4;
        } // below
        if (y > maxY)
        {
            code |= 8;
        } // above
        return code;
    }
}
