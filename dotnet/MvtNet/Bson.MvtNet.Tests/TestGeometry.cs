namespace Bson.MvtNet.Tests;

/// <summary>
/// Shared helpers for reading encoded MVT geometry back in tests.
/// </summary>
internal static class TestGeometry
{
    public static List<TileCoord[]> DecodeRings(IReadOnlyList<uint> geometry)
    {
        var rings = new List<TileCoord[]>();
        var current = new List<TileCoord>();
        int x = 0,
            y = 0;
        int ringStartX = 0,
            ringStartY = 0;
        int i = 0;

        static int Unzig(uint v) => (int)(v >> 1) ^ -(int)(v & 1);

        while (i < geometry.Count)
        {
            uint cmd = geometry[i++];
            uint id = cmd & 0x7;
            uint count = cmd >> 3;

            switch (id)
            {
                case 1: // MoveTo
                case 2: // LineTo
                    for (uint n = 0; n < count; n++)
                    {
                        x += Unzig(geometry[i++]);
                        y += Unzig(geometry[i++]);

                        if (current.Count == 0)
                        {
                            ringStartX = x;
                            ringStartY = y;
                        }

                        current.Add(new TileCoord(x, y));
                    }
                    break;
                case 7: // ClosePath
                    rings.Add(current.ToArray());
                    current.Clear();
                    x = ringStartX;
                    y = ringStartY;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown command id {id}");
            }
        }

        return rings;
    }
}
