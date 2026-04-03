using Google.Protobuf;
using VectorTile;

namespace MvtNet;

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
        _z = z;
        _x = x;
        _y = y;
        _extent = extent;
    }

    public LayerBuilder Layer(string name)
    {
        if (!_layers.TryGetValue(name, out var layer))
        {
            layer = new LayerBuilder(name, _z, _x, _y, _extent);
            _layers[name] = layer;
        }

        return layer;
    }

    public byte[] Build()
    {
        var tile = new Tile();

        foreach (var layer in _layers.Values)
        {
            tile.Layers.Add(layer.Build());
        }

        return tile.ToByteArray();
    }
}

public class LayerBuilder
{
    private readonly string _name;
    private readonly int _z;
    private readonly int _x;
    private readonly int _y;
    private readonly uint _extent;
    private readonly TagEncoder _tags = new();
    private readonly List<Tile.Types.Feature> _features = new();
    private ulong _nextId = 1;

    internal LayerBuilder(string name, int z, int x, int y, uint extent)
    {
        _name = name;
        _z = z;
        _x = x;
        _y = y;
        _extent = extent;
    }

    /// <summary>
    /// Adds a point feature at the given WGS84 coordinate.
    /// Returns false if the point is outside this tile.
    /// </summary>
    public bool AddPoint(double lat, double lng, IEnumerable<KeyValuePair<string, object>>? attributes = null)
    {
        var coord = TileMath.ProjectPoint(lat, lng, _z, _x, _y, _extent);
        if (coord is null)
        {
            return false;
        }

        var feature = new Tile.Types.Feature
        {
            Id = _nextId++,
            Type = Tile.Types.GeomType.Point
        };

        feature.Geometry.AddRange(GeometryEncoder.EncodePoint(coord.Value.X, coord.Value.Y));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return true;
    }

    /// <summary>
    /// Adds a LineString feature from WGS84 coordinates.
    /// Points outside the tile are dropped. Returns false if fewer than 2 points remain.
    /// </summary>
    public bool AddLineString(ReadOnlySpan<(double Lat, double Lng)> coords, IEnumerable<KeyValuePair<string, object>>? attributes = null)
    {
        var tileCoords = new List<TileCoord>();
        foreach (var (lat, lng) in coords)
        {
            var tc = TileMath.ProjectPoint(lat, lng, _z, _x, _y, _extent);
            if (tc is not null)
            {
                tileCoords.Add(tc.Value);
            }
        }

        if (tileCoords.Count < 2)
        {
            return false;
        }

        var feature = new Tile.Types.Feature
        {
            Id = _nextId++,
            Type = Tile.Types.GeomType.Linestring
        };

        feature.Geometry.AddRange(GeometryEncoder.EncodeLineString(tileCoords.ToArray()));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return true;
    }

    /// <summary>
    /// Adds a Polygon feature from WGS84 coordinates (outer ring only for now).
    /// The ring should NOT repeat the first point.
    /// </summary>
    public bool AddPolygon(ReadOnlySpan<(double Lat, double Lng)> ring, IEnumerable<KeyValuePair<string, object>>? attributes = null)
    {
        var tileCoords = new List<TileCoord>();
        foreach (var (lat, lng) in ring)
        {
            var tc = TileMath.ProjectPoint(lat, lng, _z, _x, _y, _extent);
            if (tc is not null)
            {
                tileCoords.Add(tc.Value);
            }
        }

        if (tileCoords.Count < 3)
        {
            return false;
        }

        var feature = new Tile.Types.Feature
        {
            Id = _nextId++,
            Type = Tile.Types.GeomType.Polygon
        };

        feature.Geometry.AddRange(GeometryEncoder.EncodePolygon(tileCoords.ToArray()));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return true;
    }

    internal Tile.Types.Layer Build()
    {
        var layer = new Tile.Types.Layer
        {
            Name = _name,
            Version = 2,
            Extent = _extent
        };

        layer.Keys.AddRange(_tags.Keys);
        layer.Values.AddRange(_tags.Values);
        layer.Features.AddRange(_features);

        return layer;
    }
}
