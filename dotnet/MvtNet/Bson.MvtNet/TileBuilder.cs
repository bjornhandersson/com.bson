using System.Text.Json;
using Google.Protobuf;
using VectorTile;

namespace Bson.MvtNet;

/// <summary>
/// Builds an MVT tile from WGS84 features for a given z/x/y tile address.
/// </summary>
public sealed class TileBuilder
{
    private readonly int _z;
    private readonly int _x;
    private readonly int _y;
    private readonly uint _extent;
    private readonly Dictionary<string, LayerBuilder> _layers = new();

    /// <summary>
    /// Creates a builder for the tile at the given XYZ address.
    /// </summary>
    /// <param name="z">Zoom level, 0 to 30.</param>
    /// <param name="x">Tile column, 0 to 2^z - 1.</param>
    /// <param name="y">Tile row (XYZ scheme, 0 at the north), 0 to 2^z - 1.</param>
    /// <param name="extent">Tile-local coordinate resolution. The spec default of 4096 suits almost all uses.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when z, x or y is outside its valid range.</exception>
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

    /// <summary>
    /// Returns the layer with the given name, creating it on first use. Calling
    /// this again with the same name returns the same layer, so features from
    /// several sources can be added to one layer.
    /// </summary>
    public LayerBuilder Layer(string name)
    {
        if (!_layers.TryGetValue(name, out var layer))
        {
            layer = new LayerBuilder(this, name, _z, _x, _y, _extent);
            _layers[name] = layer;
        }

        return layer;
    }

    /// <summary>
    /// Serializes all layers into an MVT protobuf, ready to be served as
    /// <c>application/vnd.mapbox-vector-tile</c>.
    /// </summary>
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

/// <summary>
/// Adds features to one named layer of a tile. Attribute values may be string,
/// bool, int, long, float or double; pairs with a null value are dropped, and
/// any other type throws <see cref="ArgumentException"/>.
/// </summary>
public sealed class LayerBuilder
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
    /// The ring should NOT repeat the first point and may be given in either
    /// winding order; it is normalized to the orientation the MVT spec requires.
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
        GeometryEncoder.Orient(tileCoords, exterior: true);

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
    /// Adds a Polygon feature with one or more holes. The outer ring and each
    /// hole should NOT repeat the first point and may be given in either winding
    /// order; rings are normalized to the orientation the MVT spec requires.
    /// Rings with fewer than 3 points are silently dropped; if the outer ring is
    /// invalid the whole feature is skipped.
    /// </summary>
    public LayerBuilder AddPolygon(
        ReadOnlySpan<(double Lat, double Lng)> outer,
        IReadOnlyList<(double Lat, double Lng)[]> holes,
        IEnumerable<KeyValuePair<string, object>>? attributes = null
    )
    {
        if (outer.Length < 3)
        {
            return this;
        }

        if (!OverlapsTile(outer))
        {
            return this;
        }

        var outerCoords = ProjectAll(outer);
        GeometryEncoder.Orient(outerCoords, exterior: true);

        var rings = new List<TileCoord[]>(1 + holes.Count) { outerCoords };
        for (int i = 0; i < holes.Count; i++)
        {
            var hole = holes[i];
            if (hole.Length < 3)
            {
                continue;
            }
            var holeCoords = ProjectAll(hole);
            GeometryEncoder.Orient(holeCoords, exterior: false);
            rings.Add(holeCoords);
        }

        var feature = new Tile.Types.Feature { Id = _nextId++, Type = Tile.Types.GeomType.Polygon };
        feature.Geometry.AddRange(GeometryEncoder.EncodePolygon(rings));

        if (attributes is not null)
        {
            feature.Tags.AddRange(_tags.Encode(attributes));
        }

        _features.Add(feature);
        return this;
    }

    /// <summary>
    /// Ingests a GeoJSON document (FeatureCollection, Feature, or bare geometry)
    /// and adds each feature to this layer. Top-level scalar `properties` become
    /// MVT tags; nested objects, arrays, and source feature ids are dropped.
    /// Multi-geometries and GeometryCollection are flattened to N MVT features
    /// sharing the same tags. Malformed input is silently skipped.
    /// </summary>
    public LayerBuilder AddGeoJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return this;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            GeoJsonReader.Read(this, doc.RootElement);
        }
        catch (JsonException)
        {
            // skip malformed input
        }

        return this;
    }

    /// <summary>
    /// Ingests a GeoJSON document from a UTF-8 stream. See AddGeoJson(string)
    /// for semantics.
    /// </summary>
    public LayerBuilder AddGeoJson(Stream utf8Json)
    {
        try
        {
            using var doc = JsonDocument.Parse(utf8Json);
            GeoJsonReader.Read(this, doc.RootElement);
        }
        catch (JsonException)
        {
            // skip malformed input
        }

        return this;
    }

    /// <summary>
    /// Ingests a GeoJSON document from an already-parsed JsonElement. Use this
    /// path to avoid re-parsing the same document across many tile builds.
    /// See AddGeoJson(string) for semantics.
    /// </summary>
    public LayerBuilder AddGeoJson(JsonElement element)
    {
        GeoJsonReader.Read(this, element);
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
