using Google.Protobuf;
using VectorTile;

namespace Bson.MvtNet;

/// <summary>
/// Builds an MVT tile from WGS84 features for a given z/x/y tile address.
/// </summary>
public class TileBuilder
{
    private readonly int _z;
    private readonly int _x;
    private readonly int _y;
    private readonly uint _extent;
    private readonly Dictionary<string, LayerBuilder> _layers = new();

    public TileBuilder(int z, int x, int y, uint extent = TileMath.DefaultExtent)
    {
        if (z < 0 || z > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(z), z, "z must be in [0, 30].");
        }

        int maxCoord = (1 << z) - 1;

        if (x < 0 || x > maxCoord)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                x,
                $"x must be in [0, {maxCoord}] for z={z}."
            );
        }

        if (y < 0 || y > maxCoord)
        {
            throw new ArgumentOutOfRangeException(
                nameof(y),
                y,
                $"y must be in [0, {maxCoord}] for z={z}."
            );
        }

        _z = z;
        _x = x;
        _y = y;
        _extent = extent;
    }

    public LayerBuilder Layer(string name)
    {
        if (!_layers.TryGetValue(name, out var layer))
        {
            layer = new LayerBuilder(this, name, _z, _x, _y, _extent);
            _layers[name] = layer;
        }

        return layer;
    }

    public byte[] Build()
    {
        var tile = new Tile();

        foreach (var layer in _layers.Values)
        {
            tile.Layers.Add(layer.BuildLayer());
        }

        return tile.ToByteArray();
    }
}

public class LayerBuilder
{
    private readonly string _name;
    private readonly uint _extent;
    private readonly TileProjectionContext _ctx;
    private readonly TagEncoder _tags = new();
    private readonly List<Tile.Types.Feature> _features = new();
    private ulong _nextId = 1;
    private readonly TileBuilder _tile;

    internal LayerBuilder(TileBuilder tile, string name, int z, int x, int y, uint extent)
    {
        _tile = tile;
        _name = name;
        _extent = extent;
        _ctx = TileMath.CreateProjectionContext(z, x, y, extent);
    }

    /// <summary>
    /// Adds a point feature at the given WGS84 coordinate.
    /// Silently skipped if the point is outside this tile.
    /// </summary>
    public LayerBuilder AddPoint(
        double lat,
        double lng,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        var bounds = _ctx.Bounds;
        if (lat < bounds.South || lat > bounds.North || lng < bounds.West || lng > bounds.East)
        {
            return this;
        }

        var coord = TileMath.ProjectWithContext(lat, lng, _ctx);

        var feature = new Tile.Types.Feature { Id = _nextId++, Type = Tile.Types.GeomType.Point };

        feature.Geometry.AddRange(GeometryEncoder.EncodePoint(coord.X, coord.Y));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return this;
    }

    /// <summary>
    /// Adds a LineString feature from WGS84 coordinates.
    /// The line is clipped to the tile extent (with a buffer margin) so only
    /// the visible portion is encoded. If the line crosses the tile multiple
    /// times, multiple features are emitted. Silently skipped if the geometry
    /// doesn't overlap the tile at all.
    /// </summary>
    public LayerBuilder AddLineString(
        ReadOnlySpan<(double Lat, double Lng)> coords,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        if (coords.Length < 2)
        {
            return this;
        }

        if (!OverlapsTile(coords))
        {
            return this;
        }

        var tileCoords = ProjectAll(coords);

        uint[]? encodedTags = null;
        if (attributes is not null)
        {
            encodedTags = _tags.Encode(attributes);
        }

        var clippedSegments = LineClipper.Clip(tileCoords, _extent);

        foreach (var segment in clippedSegments)
        {
            if (segment.Length < 2)
            {
                continue;
            }

            AddLineFeature(segment, encodedTags);
        }

        return this;
    }

    /// <summary>
    /// Adds a Polygon feature from WGS84 coordinates (outer ring only for now).
    /// The ring should NOT repeat the first point.
    /// All coordinates are projected (even outside tile bounds) so polygons crossing
    /// tile boundaries render correctly.
    /// </summary>
    public LayerBuilder AddPolygon(
        ReadOnlySpan<(double Lat, double Lng)> ring,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        if (ring.Length < 3)
        {
            return this;
        }

        if (!OverlapsTile(ring))
        {
            return this;
        }

        var tileCoords = ProjectAll(ring);

        var feature = new Tile.Types.Feature { Id = _nextId++, Type = Tile.Types.GeomType.Polygon };

        feature.Geometry.AddRange(GeometryEncoder.EncodePolygon(tileCoords));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return this;
    }

    /// <summary>
    /// Checks whether the bounding box of the coordinates overlaps the tile.
    /// </summary>
    private bool OverlapsTile(ReadOnlySpan<(double Lat, double Lng)> coords)
    {
        var tileBounds = _ctx.Bounds;

        double minLat = double.MaxValue,
            maxLat = double.MinValue;
        double minLng = double.MaxValue,
            maxLng = double.MinValue;

        foreach (var (lat, lng) in coords)
        {
            if (lat < minLat)
            {
                minLat = lat;
            }
            if (lat > maxLat)
            {
                maxLat = lat;
            }
            if (lng < minLng)
            {
                minLng = lng;
            }
            if (lng > maxLng)
            {
                maxLng = lng;
            }
        }

        return maxLat >= tileBounds.South
            && minLat <= tileBounds.North
            && maxLng >= tileBounds.West
            && minLng <= tileBounds.East;
    }

    private TileCoord[] ProjectAll(ReadOnlySpan<(double Lat, double Lng)> coords)
    {
        var result = new TileCoord[coords.Length];
        for (int i = 0; i < coords.Length; i++)
        {
            result[i] = TileMath.ProjectWithContext(coords[i].Lat, coords[i].Lng, _ctx);
        }

        return result;
    }

    private void AddLineFeature(TileCoord[] coords, uint[]? encodedTags)
    {
        var feature = new Tile.Types.Feature
        {
            Id = _nextId++,
            Type = Tile.Types.GeomType.Linestring,
        };

        feature.Geometry.AddRange(GeometryEncoder.EncodeLineString(coords));

        if (encodedTags is not null)
        {
            feature.Tags.AddRange(encodedTags);
        }

        _features.Add(feature);
    }

    /// <summary>
    /// Builds the tile. Shortcut for calling Build() on the parent TileBuilder.
    /// </summary>
    public byte[] Build() => _tile.Build();

    internal Tile.Types.Layer BuildLayer()
    {
        var layer = new Tile.Types.Layer
        {
            Name = _name,
            Version = 2,
            Extent = _extent,
        };

        layer.Keys.AddRange(_tags.Keys);
        layer.Values.AddRange(_tags.Values);
        layer.Features.AddRange(_features);

        return layer;
    }
}
