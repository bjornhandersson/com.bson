namespace Bson.MvtNet;

internal static class GeometryEncoder
{
    private const uint MoveToId = 1;
    private const uint LineToId = 2;
    private const uint ClosePathId = 7;

    public static uint[] EncodePoint(int x, int y)
    {
        return new[] { CommandInteger(MoveToId, 1), ZigZag(x), ZigZag(y) };
    }

    public static uint[] EncodeLineString(ReadOnlySpan<TileCoord> coords)
    {
        if (coords.Length < 2)
        {
            throw new ArgumentException(
                "LineString requires at least 2 coordinates.",
                nameof(coords)
            );
        }

        // Layout: MoveTo(1) + x + y + LineTo(n-1) + (n-1) * (dx + dy)
        int count = 3 + 1 + (coords.Length - 1) * 2;
        var commands = new uint[count];
        int pos = 0;

        commands[pos++] = CommandInteger(MoveToId, 1);
        commands[pos++] = ZigZag(coords[0].X);
        commands[pos++] = ZigZag(coords[0].Y);

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

    // Each part is a MoveTo + LineTo run; the cursor carries over between parts.
    public static uint[] EncodeMultiLineString(IReadOnlyList<TileCoord[]> parts)
    {
        if (parts.Count == 0)
        {
            throw new ArgumentException("MultiLineString requires at least one part.", nameof(parts));
        }

        int count = 0;
        for (int p = 0; p < parts.Count; p++)
        {
            if (parts[p].Length < 2)
            {
                throw new ArgumentException(
                    "LineString requires at least 2 coordinates.",
                    nameof(parts)
                );
            }
            count += 3 + 1 + (parts[p].Length - 1) * 2;
        }

        var commands = new uint[count];
        int pos = 0;
        int prevX = 0;
        int prevY = 0;

        for (int p = 0; p < parts.Count; p++)
        {
            var part = parts[p];

            commands[pos++] = CommandInteger(MoveToId, 1);
            commands[pos++] = ZigZag(part[0].X - prevX);
            commands[pos++] = ZigZag(part[0].Y - prevY);
            prevX = part[0].X;
            prevY = part[0].Y;

            commands[pos++] = CommandInteger(LineToId, (uint)(part.Length - 1));
            for (int i = 1; i < part.Length; i++)
            {
                commands[pos++] = ZigZag(part[i].X - prevX);
                commands[pos++] = ZigZag(part[i].Y - prevY);
                prevX = part[i].X;
                prevY = part[i].Y;
            }
        }

        return commands;
    }

    // The ring must not repeat its first point; ClosePath closes it.
    public static uint[] EncodePolygon(ReadOnlySpan<TileCoord> ring)
    {
        if (ring.Length < 3)
        {
            throw new ArgumentException(
                "Polygon ring requires at least 3 coordinates.",
                nameof(ring)
            );
        }

        // Layout: MoveTo(1) + x + y + LineTo(n-1) + (n-1) * (dx + dy) + ClosePath(1)
        int count = 3 + 1 + (ring.Length - 1) * 2 + 1;
        var commands = new uint[count];
        int pos = 0;

        commands[pos++] = CommandInteger(MoveToId, 1);
        commands[pos++] = ZigZag(ring[0].X);
        commands[pos++] = ZigZag(ring[0].Y);

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

        commands[pos++] = CommandInteger(ClosePathId, 1);

        return commands;
    }

    // First ring is the outer boundary, the rest are holes.
    public static uint[] EncodePolygon(IReadOnlyList<TileCoord[]> rings)
    {
        if (rings.Count == 0)
        {
            throw new ArgumentException("Polygon requires at least one ring.", nameof(rings));
        }

        int count = 0;
        for (int r = 0; r < rings.Count; r++)
        {
            var ring = rings[r];
            if (ring.Length < 3)
            {
                throw new ArgumentException(
                    "Polygon ring requires at least 3 coordinates.",
                    nameof(rings)
                );
            }
            count += 3 + 1 + (ring.Length - 1) * 2 + 1;
        }

        var commands = new uint[count];
        int pos = 0;
        int prevX = 0;
        int prevY = 0;

        for (int r = 0; r < rings.Count; r++)
        {
            var ring = rings[r];

            commands[pos++] = CommandInteger(MoveToId, 1);
            commands[pos++] = ZigZag(ring[0].X - prevX);
            commands[pos++] = ZigZag(ring[0].Y - prevY);
            int ringStartX = ring[0].X;
            int ringStartY = ring[0].Y;
            prevX = ring[0].X;
            prevY = ring[0].Y;

            commands[pos++] = CommandInteger(LineToId, (uint)(ring.Length - 1));
            for (int i = 1; i < ring.Length; i++)
            {
                commands[pos++] = ZigZag(ring[i].X - prevX);
                commands[pos++] = ZigZag(ring[i].Y - prevY);
                prevX = ring[i].X;
                prevY = ring[i].Y;
            }

            commands[pos++] = CommandInteger(ClosePathId, 1);
            prevX = ringStartX;
            prevY = ringStartY;
        }

        return commands;
    }

    // Shoelace sum, so twice the area. Y grows downward, which makes a positive
    // result clockwise on screen: the winding MVT requires of exterior rings.
    public static double SignedArea(ReadOnlySpan<TileCoord> ring)
    {
        double sum = 0;
        for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
        {
            sum += (double)ring[j].X * ring[i].Y - (double)ring[i].X * ring[j].Y;
        }

        return sum;
    }

    public static void Orient(TileCoord[] ring, bool exterior)
    {
        double area = SignedArea(ring);
        if (exterior ? area < 0 : area > 0)
        {
            Array.Reverse(ring);
        }
    }

    public static uint CommandInteger(uint commandId, uint count)
    {
        return (commandId & 0x7) | (count << 3);
    }

    public static uint ZigZag(int value)
    {
        return (uint)((value << 1) ^ (value >> 31));
    }
}
