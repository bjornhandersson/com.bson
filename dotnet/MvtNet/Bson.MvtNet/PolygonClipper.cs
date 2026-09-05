namespace Bson.MvtNet;

internal static class PolygonClipper
{
    public static TileCoord[] Clip(
        TileCoord[] ring,
        uint extent,
        double bufferFraction = TileMath.DefaultClipBufferFraction
    )
    {
        double buffer = extent * bufferFraction;
        double min = -buffer;
        double max = extent + buffer;

        var unrounded = new List<(double X, double Y)>(ring.Length + 8);
        foreach (var p in ring)
        {
            unrounded.Add((p.X, p.Y));
        }

        unrounded = ClipToEdgeSutherlandHodgman(unrounded, Edge.Left, min, max);
        unrounded = ClipToEdgeSutherlandHodgman(unrounded, Edge.Right, min, max);
        unrounded = ClipToEdgeSutherlandHodgman(unrounded, Edge.Bottom, min, max);
        unrounded = ClipToEdgeSutherlandHodgman(unrounded, Edge.Top, min, max);

        if (unrounded.Count < 3)
        {
            return Array.Empty<TileCoord>();
        }

        return RoundOnce(unrounded);
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

    private static List<(double X, double Y)> ClipToEdgeSutherlandHodgman(
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

    private static TileCoord[] RoundOnce(List<(double X, double Y)> points)
    {
        var result = new List<TileCoord>(points.Count);

        foreach (var p in points)
        {
            var coord = new TileCoord((int)Math.Round(p.X), (int)Math.Round(p.Y));
            if (result.Count > 0 && result[result.Count - 1] == coord)
            {
                continue;
            }

            result.Add(coord);
        }

        while (result.Count > 1 && result[result.Count - 1] == result[0])
        {
            result.RemoveAt(result.Count - 1);
        }

        return result.Count < 3 ? Array.Empty<TileCoord>() : result.ToArray();
    }
}
