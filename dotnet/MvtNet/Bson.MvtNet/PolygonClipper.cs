namespace Bson.MvtNet;

/// <summary>
/// Clips a polygon ring to the tile extent plus a buffer margin using the
/// Sutherland-Hodgman algorithm.
/// <para>
/// Unclipped rings are a rendering hazard, not just a size problem. A ring that
/// reaches the poles projects to Mercator Y values tens of thousands of units
/// outside the tile, and renderers that hold fill vertices in 16-bit integers
/// wrap those coordinates, drawing wedges across the tile.
/// </para>
/// </summary>
internal static class PolygonClipper
{
    /// <summary>
    /// Clips a ring to the tile extent plus a buffer. The ring must not repeat
    /// its first point, and the result follows the same convention. Returns an
    /// empty array when the ring falls entirely outside the clip region, or
    /// when clipping leaves fewer than three distinct points.
    /// </summary>
    public static TileCoord[] Clip(
        TileCoord[] ring,
        uint extent,
        double bufferFraction = TileMath.DefaultClipBufferFraction
    )
    {
        double buffer = extent * bufferFraction;
        double min = -buffer;
        double max = extent + buffer;

        // Clipping runs in floating point across all four edges so that
        // intersections are not re-rounded on every pass.
        var current = new List<(double X, double Y)>(ring.Length + 8);
        foreach (var p in ring)
        {
            current.Add((p.X, p.Y));
        }

        current = ClipToEdge(current, Edge.Left, min, max);
        current = ClipToEdge(current, Edge.Right, min, max);
        current = ClipToEdge(current, Edge.Bottom, min, max);
        current = ClipToEdge(current, Edge.Top, min, max);

        if (current.Count < 3)
        {
            return Array.Empty<TileCoord>();
        }

        return Snap(current);
    }

    private enum Edge
    {
        Left,
        Right,
        Bottom,
        Top,
    }

    private static bool Inside((double X, double Y) p, Edge edge, double min, double max) =>
        edge switch
        {
            Edge.Left => p.X >= min,
            Edge.Right => p.X <= max,
            Edge.Bottom => p.Y >= min,
            _ => p.Y <= max,
        };

    /// <summary>
    /// Splits the segment a-b where it crosses the given edge.
    /// </summary>
    private static (double X, double Y) Intersect(
        (double X, double Y) a,
        (double X, double Y) b,
        Edge edge,
        double min,
        double max
    )
    {
        switch (edge)
        {
            case Edge.Left:
            case Edge.Right:
            {
                double bound = edge == Edge.Left ? min : max;
                double dx = b.X - a.X;
                double t = dx == 0 ? 0 : (bound - a.X) / dx;
                return (bound, a.Y + (b.Y - a.Y) * t);
            }
            default:
            {
                double bound = edge == Edge.Bottom ? min : max;
                double dy = b.Y - a.Y;
                double t = dy == 0 ? 0 : (bound - a.Y) / dy;
                return (a.X + (b.X - a.X) * t, bound);
            }
        }
    }

    private static List<(double X, double Y)> ClipToEdge(
        List<(double X, double Y)> input,
        Edge edge,
        double min,
        double max
    )
    {
        var output = new List<(double X, double Y)>(input.Count + 4);
        if (input.Count == 0)
        {
            return output;
        }

        for (int i = 0; i < input.Count; i++)
        {
            var cur = input[i];
            var prev = input[(i + input.Count - 1) % input.Count];

            bool curIn = Inside(cur, edge, min, max);
            bool prevIn = Inside(prev, edge, min, max);

            if (curIn)
            {
                if (!prevIn)
                {
                    output.Add(Intersect(prev, cur, edge, min, max));
                }

                output.Add(cur);
            }
            else if (prevIn)
            {
                output.Add(Intersect(prev, cur, edge, min, max));
            }
        }

        return output;
    }

    /// <summary>
    /// Rounds to integer tile coordinates and drops points that collapse onto
    /// their neighbour, including a last point that lands on the first — MVT
    /// rings are closed by ClosePath, never by repeating a vertex.
    /// </summary>
    private static TileCoord[] Snap(List<(double X, double Y)> points)
    {
        var result = new List<TileCoord>(points.Count);

        foreach (var p in points)
        {
            var coord = new TileCoord((int)Math.Round(p.X), (int)Math.Round(p.Y));
            if (result.Count > 0 && result[^1] == coord)
            {
                continue;
            }

            result.Add(coord);
        }

        while (result.Count > 1 && result[^1] == result[0])
        {
            result.RemoveAt(result.Count - 1);
        }

        return result.Count < 3 ? Array.Empty<TileCoord>() : result.ToArray();
    }
}
