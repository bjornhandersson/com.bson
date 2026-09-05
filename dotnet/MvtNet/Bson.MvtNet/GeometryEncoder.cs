namespace Bson.MvtNet;

internal static class GeometryEncoder
{
    private const uint MoveToId = 1;
    private const uint LineToId = 2;
    private const uint ClosePathId = 7;

    private const int CommandSize = 1;
    private const int PointSize = 2;

    public static uint[] EncodePoint(int x, int y)
    {
        return new[] { CommandInteger(MoveToId, 1), ZigZag(x), ZigZag(y) };
    }

    public static uint[] EncodeLineString(ReadOnlySpan<TileCoord> coords)
    {
        RequireLineString(coords, nameof(coords));

        var writer = new CommandWriter(PathSize(coords.Length, closed: false));
        writer.WritePath(coords, closed: false);
        return writer.Commands;
    }

    public static uint[] EncodeMultiLineString(IReadOnlyList<TileCoord[]> parts)
    {
        if (parts.Count == 0)
        {
            throw new ArgumentException("MultiLineString requires at least one part.", nameof(parts));
        }

        int size = 0;
        foreach (var part in parts)
        {
            RequireLineString(part, nameof(parts));
            size += PathSize(part.Length, closed: false);
        }

        var writer = new CommandWriter(size);
        foreach (var part in parts)
        {
            writer.WritePath(part, closed: false);
        }

        return writer.Commands;
    }

    public static uint[] EncodePolygon(ReadOnlySpan<TileCoord> ring)
    {
        RequireRing(ring, nameof(ring));

        var writer = new CommandWriter(PathSize(ring.Length, closed: true));
        writer.WritePath(ring, closed: true);
        return writer.Commands;
    }

    public static uint[] EncodePolygon(IReadOnlyList<TileCoord[]> rings)
    {
        if (rings.Count == 0)
        {
            throw new ArgumentException("Polygon requires at least one ring.", nameof(rings));
        }

        int size = 0;
        foreach (var ring in rings)
        {
            RequireRing(ring, nameof(rings));
            size += PathSize(ring.Length, closed: true);
        }

        var writer = new CommandWriter(size);
        foreach (var ring in rings)
        {
            writer.WritePath(ring, closed: true);
        }

        return writer.Commands;
    }

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
        bool clockwiseOnScreen = area > 0;
        bool counterClockwiseOnScreen = area < 0;

        if (exterior ? counterClockwiseOnScreen : clockwiseOnScreen)
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

    private static int PathSize(int points, bool closed)
    {
        int moveTo = CommandSize + PointSize;
        int lineTo = CommandSize + (points - 1) * PointSize;
        int closePath = closed ? CommandSize : 0;
        return moveTo + lineTo + closePath;
    }

    private static void RequireLineString(ReadOnlySpan<TileCoord> coords, string paramName)
    {
        if (coords.Length < 2)
        {
            throw new ArgumentException("LineString requires at least 2 coordinates.", paramName);
        }
    }

    private static void RequireRing(ReadOnlySpan<TileCoord> ring, string paramName)
    {
        if (ring.Length < 3)
        {
            throw new ArgumentException("Polygon ring requires at least 3 coordinates.", paramName);
        }
    }

    private sealed class CommandWriter
    {
        private readonly uint[] _commands;
        private int _position;
        private int _cursorX;
        private int _cursorY;

        public CommandWriter(int size)
        {
            _commands = new uint[size];
        }

        public uint[] Commands => _commands;

        public void WritePath(ReadOnlySpan<TileCoord> path, bool closed)
        {
            WriteCommand(MoveToId, 1);
            WritePoint(path[0]);

            WriteCommand(LineToId, (uint)(path.Length - 1));
            for (int i = 1; i < path.Length; i++)
            {
                WritePoint(path[i]);
            }

            if (closed)
            {
                WriteCommand(ClosePathId, 1);
            }
        }

        private void WriteCommand(uint id, uint count)
        {
            _commands[_position++] = CommandInteger(id, count);
        }

        private void WritePoint(TileCoord point)
        {
            _commands[_position++] = ZigZag(point.X - _cursorX);
            _commands[_position++] = ZigZag(point.Y - _cursorY);
            _cursorX = point.X;
            _cursorY = point.Y;
        }
    }
}
