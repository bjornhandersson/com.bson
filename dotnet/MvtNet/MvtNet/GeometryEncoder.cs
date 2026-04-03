namespace MvtNet;

/// <summary>
/// Encodes MVT geometry commands (MoveTo, LineTo, ClosePath) with zigzag + delta encoding.
/// </summary>
internal static class GeometryEncoder
{
    private const uint MoveToId = 1;
    private const uint LineToId = 2;
    private const uint ClosePathId = 7;

    /// <summary>
    /// Encodes a Point geometry (single MoveTo command).
    /// </summary>
    public static uint[] EncodePoint(int x, int y)
    {
        return new[] { CommandInteger(MoveToId, 1), ZigZag(x), ZigZag(y) };
    }

    /// <summary>
    /// Encodes a LineString geometry (MoveTo + LineTo commands with delta encoding).
    /// </summary>
    public static uint[] EncodeLineString(ReadOnlySpan<TileCoord> coords)
    {
        if (coords.Length < 2)
        {
            throw new ArgumentException(
                "LineString requires at least 2 coordinates.",
                nameof(coords)
            );
        }

        // Layout: MoveTo(1) + x + y + LineTo(1) + (n-1) * (dx + dy)
        int count = 3 + 1 + (coords.Length - 1) * 2;
        var commands = new uint[count];
        int pos = 0;

        // MoveTo first point
        commands[pos++] = CommandInteger(MoveToId, 1);
        commands[pos++] = ZigZag(coords[0].X);
        commands[pos++] = ZigZag(coords[0].Y);

        // LineTo remaining points (delta from previous)
        commands[pos++] = CommandInteger(LineToId, (uint)(coords.Length - 1));
        int prevX = coords[0].X;
        int prevY = coords[0].Y;
        for (int i = 1; i < coords.Length; i++)
        {
            commands[pos++] = ZigZag(coords[i].X - prevX);
            commands[pos++] = ZigZag(coords[i].Y - prevY);
            prevX = coords[i].X;
            prevY = coords[i].Y;
        }

        return commands;
    }

    /// <summary>
    /// Encodes a Polygon geometry (MoveTo + LineTo + ClosePath, with delta encoding).
    /// The ring should NOT repeat the first point — ClosePath handles closure.
    /// </summary>
    public static uint[] EncodePolygon(ReadOnlySpan<TileCoord> ring)
    {
        if (ring.Length < 3)
        {
            throw new ArgumentException(
                "Polygon ring requires at least 3 coordinates.",
                nameof(ring)
            );
        }

        // Layout: MoveTo(1) + x + y + LineTo(1) + (n-1) * (dx + dy) + ClosePath(1)
        int count = 3 + 1 + (ring.Length - 1) * 2 + 1;
        var commands = new uint[count];
        int pos = 0;

        // MoveTo first point
        commands[pos++] = CommandInteger(MoveToId, 1);
        commands[pos++] = ZigZag(ring[0].X);
        commands[pos++] = ZigZag(ring[0].Y);

        // LineTo remaining points (delta from previous)
        commands[pos++] = CommandInteger(LineToId, (uint)(ring.Length - 1));
        int prevX = ring[0].X;
        int prevY = ring[0].Y;
        for (int i = 1; i < ring.Length; i++)
        {
            commands[pos++] = ZigZag(ring[i].X - prevX);
            commands[pos++] = ZigZag(ring[i].Y - prevY);
            prevX = ring[i].X;
            prevY = ring[i].Y;
        }

        // ClosePath
        commands[pos++] = CommandInteger(ClosePathId, 1);

        return commands;
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
