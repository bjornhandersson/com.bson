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
    public bool AddPoint(
        double lat,
        double lng,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        var coord = TileMath.ProjectPoint(lat, lng, _z, _x, _y, _extent);
        if (coord is null)
        {
            return false;
        }

        var feature = new Tile.Types.Feature { Id = _nextId++, Type = Tile.Types.GeomType.Point };

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
    /// All coordinates are projected (even outside tile bounds) so segments crossing
    /// tile boundaries render correctly. Returns false if the geometry doesn't
    /// overlap the tile at all.
    /// </summary>
    public bool AddLineString(
        ReadOnlySpan<(double Lat, double Lng)> coords,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        if (coords.Length < 2)
        {
            return false;
        }

        if (!OverlapsTile(coords))
        {
            return false;
        }

        var tileCoords = ProjectAll(coords);

        var feature = new Tile.Types.Feature
        {
            Id = _nextId++,
            Type = Tile.Types.GeomType.Linestring,
        };

        feature.Geometry.AddRange(GeometryEncoder.EncodeLineString(tileCoords));

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
    /// All coordinates are projected (even outside tile bounds) so polygons crossing
    /// tile boundaries render correctly.
    /// </summary>
    public bool AddPolygon(
        ReadOnlySpan<(double Lat, double Lng)> ring,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        if (ring.Length < 3)
        {
            return false;
        }

        if (!OverlapsTile(ring))
        {
            return false;
        }

        var tileCoords = ProjectAll(ring);

        var feature = new Tile.Types.Feature { Id = _nextId++, Type = Tile.Types.GeomType.Polygon };

        feature.Geometry.AddRange(GeometryEncoder.EncodePolygon(tileCoords));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return true;
    }

    /// <summary>
    /// Checks whether the bounding box of the coordinates overlaps the tile.
    /// </summary>
    private bool OverlapsTile(ReadOnlySpan<(double Lat, double Lng)> coords)
    {
        var tileBounds = TileMath.GetTileBounds(_z, _x, _y);

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
            result[i] = TileMath.ProjectPointUnclamped(
                coords[i].Lat,
                coords[i].Lng,
                _z,
                _x,
                _y,
                _extent
            );
        }

        return result;
    }

    internal Tile.Types.Layer Build()
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
