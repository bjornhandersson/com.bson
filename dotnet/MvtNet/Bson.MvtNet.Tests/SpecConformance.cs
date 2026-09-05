using VectorTile;

namespace Bson.MvtNet.Tests;

/// <summary>
/// Asserts the MUST rules of the Mapbox Vector Tile 2.1 spec against a decoded
/// tile: https://github.com/mapbox/vector-tile-spec/tree/master/2.1
/// Used by <see cref="SpecVerifyTests"/> so a fixture is known-good, not just known.
/// </summary>
internal static class SpecConformance
{
    private const uint MoveTo = 1;
    private const uint LineTo = 2;
    private const uint ClosePath = 7;

    private static int DecodeParameter(uint parameterInteger) =>
        (int)(parameterInteger >> 1) ^ -(int)(parameterInteger & 1);

    public static void Validate(Tile tile)
    {
        var names = new HashSet<string>();
        foreach (var layer in tile.Layers)
        {
            Assert.That(names.Add(layer.Name), Is.True, $"Duplicate layer name \"{layer.Name}\"");
            ValidateLayer(layer);
        }
    }

    private static void ValidateLayer(Tile.Types.Layer layer)
    {
        Assert.That(layer.Version, Is.EqualTo(2u), "4.1: version MUST be 2");
        Assert.That(layer.Name, Is.Not.Empty, "4.1: layer name MUST be present");
        Assert.That(layer.HasExtent && layer.Extent > 0, Is.True, "4.1: extent MUST be present");

        Assert.That(layer.Keys, Is.Unique, "4.4: keys are interned once per layer");
        foreach (var value in layer.Values)
        {
            int set =
                (value.HasStringValue ? 1 : 0)
                + (value.HasFloatValue ? 1 : 0)
                + (value.HasDoubleValue ? 1 : 0)
                + (value.HasIntValue ? 1 : 0)
                + (value.HasUintValue ? 1 : 0)
                + (value.HasSintValue ? 1 : 0)
                + (value.HasBoolValue ? 1 : 0);
            Assert.That(set, Is.EqualTo(1), "4.4: a value MUST contain exactly one field");
        }

        foreach (var feature in layer.Features)
        {
            ValidateTags(feature, layer);
            ValidateGeometry(feature);
        }
    }

    private static void ValidateTags(Tile.Types.Feature feature, Tile.Types.Layer layer)
    {
        Assert.That(feature.Tags.Count % 2, Is.EqualTo(0), "4.4: tags are key/value index pairs");
        var keys = new HashSet<uint>();
        for (int i = 0; i < feature.Tags.Count; i += 2)
        {
            Assert.That(feature.Tags[i], Is.LessThan((uint)layer.Keys.Count), "4.4: key index in range");
            Assert.That(feature.Tags[i + 1], Is.LessThan((uint)layer.Values.Count), "4.4: value index in range");
            Assert.That(keys.Add(feature.Tags[i]), Is.True, "4.4: key index MUST be unique within a feature");
        }
    }

    private static void ValidateGeometry(Tile.Types.Feature feature)
    {
        var g = feature.Geometry;
        Assert.That(g, Is.Not.Empty, "4.3: geometry MUST be present");

        switch (feature.Type)
        {
            case Tile.Types.GeomType.Point:
                ValidatePoint(g);
                break;
            case Tile.Types.GeomType.Linestring:
                ValidateLineString(g);
                break;
            case Tile.Types.GeomType.Polygon:
                ValidatePolygon(g);
                break;
            default:
                Assert.Fail("4.3.4: geometry type MUST be POINT, LINESTRING or POLYGON");
                break;
        }
    }

    private static void ValidatePoint(IReadOnlyList<uint> g)
    {
        Assert.That(g[0] & 0x7, Is.EqualTo(MoveTo));
        uint count = g[0] >> 3;
        Assert.That(count, Is.GreaterThan(0u));
        Assert.That(g.Count, Is.EqualTo(1 + 2 * count), "Point geometry is exactly one MoveTo");
    }

    private static void ValidateLineString(IReadOnlyList<uint> g)
    {
        int i = 0;
        while (i < g.Count)
        {
            Assert.That(g[i] & 0x7, Is.EqualTo(MoveTo), $"LineString: expected MoveTo at {i}");
            Assert.That(g[i] >> 3, Is.EqualTo(1u), "LineString: MoveTo count MUST be 1");
            i += 3;

            Assert.That(i, Is.LessThan(g.Count), "LineString: MoveTo MUST be followed by LineTo");
            Assert.That(g[i] & 0x7, Is.EqualTo(LineTo), $"LineString: expected LineTo at {i}");
            uint count = g[i] >> 3;
            Assert.That(count, Is.GreaterThan(0u), "LineString: LineTo count MUST be > 0");
            i++;
            for (uint n = 0; n < count; n++, i += 2)
            {
                Assert.That(i + 1, Is.LessThan(g.Count), "LineString: truncated LineTo parameters");
                Assert.That(g[i] != 0 || g[i + 1] != 0, Is.True, "4.3.3.2: LineTo MUST NOT be zero-length");
            }
        }
    }

    private static void ValidatePolygon(IReadOnlyList<uint> g)
    {
        int i = 0;
        int x = 0;
        int y = 0;
        bool sawExterior = false;

        while (i < g.Count)
        {
            var ring = new List<TileCoord>();

            Assert.That(g[i] & 0x7, Is.EqualTo(MoveTo), $"Polygon: expected MoveTo at {i}");
            Assert.That(g[i] >> 3, Is.EqualTo(1u), "Polygon: MoveTo count MUST be 1");
            x += DecodeParameter(g[i + 1]);
            y += DecodeParameter(g[i + 2]);
            ring.Add(new TileCoord(x, y));
            i += 3;

            Assert.That(g[i] & 0x7, Is.EqualTo(LineTo), $"Polygon: expected LineTo at {i}");
            uint count = g[i] >> 3;
            Assert.That(count, Is.GreaterThan(1u), "Polygon: LineTo count MUST be > 1");
            i++;
            for (uint n = 0; n < count; n++, i += 2)
            {
                Assert.That(g[i] != 0 || g[i + 1] != 0, Is.True, "4.3.3.2: LineTo MUST NOT be zero-length");
                x += DecodeParameter(g[i]);
                y += DecodeParameter(g[i + 1]);
                ring.Add(new TileCoord(x, y));
            }

            Assert.That(i, Is.LessThan(g.Count), "Polygon: ring MUST end with ClosePath");
            Assert.That(g[i], Is.EqualTo((ClosePath & 0x7) | (1u << 3)), "Polygon: ClosePath count MUST be 1");
            i++;

            double area = GeometryEncoder.SignedArea(ring.ToArray());
            Assert.That(area, Is.Not.EqualTo(0), "Polygon: ring MUST NOT have zero area");
            if (!sawExterior)
            {
                Assert.That(area, Is.GreaterThan(0), "Polygon: first ring MUST be exterior (positive area)");
                sawExterior = true;
            }
        }
    }
}
