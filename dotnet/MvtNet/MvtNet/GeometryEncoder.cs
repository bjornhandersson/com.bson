namespace MvtNet;

/// <summary>
/// Encodes MVT geometry commands (MoveTo, LineTo, ClosePath) with zigzag + delta encoding.
/// </summary>
public static class GeometryEncoder
{
    private const uint MoveToId = 1;
    private const uint LineToId = 2;
    private const uint ClosePathId = 7;

    /// <summary>
    /// Encodes a Point geometry (single MoveTo command).
    /// </summary>
    public static uint[] EncodePoint(int x, int y)
    {
        return
        [
            CommandInteger(MoveToId, 1),
            ZigZag(x),
            ZigZag(y)
        ];
    }

    /// <summary>
    /// Encodes a LineString geometry (MoveTo + LineTo commands with delta encoding).
    /// </summary>
    public static uint[] EncodeLineString(ReadOnlySpan<TileCoord> coords)
    {
        if (coords.Length < 2)
        {
            throw new ArgumentException("LineString requires at least 2 coordinates.", nameof(coords));
        }

        var commands = new List<uint>(3 + (coords.Length - 1) * 2 + 1);

        // MoveTo first point
        commands.Add(CommandInteger(MoveToId, 1));
        commands.Add(ZigZag(coords[0].X));
        commands.Add(ZigZag(coords[0].Y));

        // LineTo remaining points (delta from previous)
        commands.Add(CommandInteger(LineToId, (uint)(coords.Length - 1)));
        int prevX = coords[0].X;
        int prevY = coords[0].Y;
        for (int i = 1; i < coords.Length; i++)
        {
            commands.Add(ZigZag(coords[i].X - prevX));
            commands.Add(ZigZag(coords[i].Y - prevY));
            prevX = coords[i].X;
            prevY = coords[i].Y;
        }

        return commands.ToArray();
    }

    /// <summary>
    /// Encodes a Polygon geometry (MoveTo + LineTo + ClosePath, with delta encoding).
    /// The ring should NOT repeat the first point — ClosePath handles closure.
    /// </summary>
    public static uint[] EncodePolygon(ReadOnlySpan<TileCoord> ring)
    {
        if (ring.Length < 3)
        {
            throw new ArgumentException("Polygon ring requires at least 3 coordinates.", nameof(ring));
        }

        var commands = new List<uint>(3 + (ring.Length - 1) * 2 + 2);

        // MoveTo first point
        commands.Add(CommandInteger(MoveToId, 1));
        commands.Add(ZigZag(ring[0].X));
        commands.Add(ZigZag(ring[0].Y));

        // LineTo remaining points (delta from previous)
        commands.Add(CommandInteger(LineToId, (uint)(ring.Length - 1)));
        int prevX = ring[0].X;
        int prevY = ring[0].Y;
        for (int i = 1; i < ring.Length; i++)
        {
            commands.Add(ZigZag(ring[i].X - prevX));
            commands.Add(ZigZag(ring[i].Y - prevY));
            prevX = ring[i].X;
            prevY = ring[i].Y;
        }

        // ClosePath
        commands.Add(CommandInteger(ClosePathId, 1));

        return commands.ToArray();
    }

    /// <summary>
    /// Encodes a command integer: (id &amp; 0x7) | (count &lt;&lt; 3)
    /// </summary>
    public static uint CommandInteger(uint commandId, uint count)
    {
        return (commandId & 0x7) | (count << 3);
    }

    /// <summary>
    /// Zigzag-encodes a signed integer.
    /// </summary>
    public static uint ZigZag(int value)
    {
        return (uint)((value << 1) ^ (value >> 31));
    }
}
