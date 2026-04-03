using Google.Protobuf;
using VectorTile;

namespace MvtNet;

/// <summary>
/// Phase 1 stub — produces an MVT tile with an X shape (two diagonal LineStrings spanning the full extent).
/// Will be replaced by proper encoding in Phase 2.
/// </summary>
public static class StubTileBuilder
{
    private const uint Extent = 4096;

    public static byte[] BuildXTile()
    {
        var tile = new Tile();

        var layer = new Tile.Types.Layer
        {
            Name = "stub",
            Version = 2,
            Extent = Extent
        };

        // Line 1: top-left (0,0) → bottom-right (4096,4096)
        layer.Features.Add(CreateLineFeature(1, 0, 0, Extent, Extent));

        // Line 2: bottom-left (0,4096) → top-right (4096,0)
        layer.Features.Add(CreateLineFeature(2, 0, Extent, Extent, 0));

        tile.Layers.Add(layer);

        return tile.ToByteArray();
    }

    private static Tile.Types.Feature CreateLineFeature(ulong id, uint x1, uint y1, uint x2, uint y2)
    {
        var feature = new Tile.Types.Feature
        {
            Id = id,
            Type = Tile.Types.GeomType.Linestring
        };

        // MoveTo(x1, y1)
        feature.Geometry.Add(CommandInteger(1, 1)); // MoveTo, count=1
        feature.Geometry.Add(ParameterInteger((int)x1));
        feature.Geometry.Add(ParameterInteger((int)y1));

        // LineTo(x2, y2) — delta from previous point
        feature.Geometry.Add(CommandInteger(2, 1)); // LineTo, count=1
        feature.Geometry.Add(ParameterInteger((int)x2 - (int)x1));
        feature.Geometry.Add(ParameterInteger((int)y2 - (int)y1));

        return feature;
    }

    /// <summary>
    /// Encodes a command integer: (id &amp; 0x7) | (count &lt;&lt; 3)
    /// </summary>
    private static uint CommandInteger(uint commandId, uint count)
    {
        return (commandId & 0x7) | (count << 3);
    }

    /// <summary>
    /// Zigzag-encodes a signed integer for MVT geometry parameters.
    /// </summary>
    private static uint ParameterInteger(int value)
    {
        return (uint)((value << 1) ^ (value >> 31));
    }
}
