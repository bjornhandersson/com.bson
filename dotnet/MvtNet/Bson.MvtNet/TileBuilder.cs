#if !NETSTANDARD2_0
using System.Runtime.InteropServices;
#endif
using System.Text.Json;
using Google.Protobuf;
using VectorTile;
// The generated MVT message type, aliased so LayerBuilder.Tile can keep its name.
using ProtoTile = VectorTile.Tile;

namespace Bson.MvtNet;

/// <summary>
/// Builds an MVT tile from WGS84 features for a given z/x/y tile address.
/// Not thread-safe: build one tile per request, on one thread.
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when z, x or y is outside its valid range, or extent is zero.</exception>
    public TileBuilder(int z, int x, int y, uint extent = TileMath.DefaultExtent)
    {
        if (extent == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(extent), extent, "extent must be greater than zero.");
        }

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
    public byte[] Build() => BuildMessage().ToByteArray();

    /// <summary>
    /// Serializes all layers into an MVT protobuf and writes it to
    /// <paramref name="output"/>, avoiding the intermediate byte array. Handy
    /// for writing straight to an HTTP response body.
    /// </summary>
    public void Build(Stream output)
    {
        if (output is null)
        {
            throw new ArgumentNullException(nameof(output));
        }
        BuildMessage().WriteTo(output);
    }

    private ProtoTile BuildMessage()
    {
        var tile = new ProtoTile();

        foreach (var layer in _layers.Values)
        {
            tile.Layers.Add(layer.BuildLayer());
        }

        return tile;
    }
}

/// <summary>
/// Adds features to one named layer of a tile.
/// <para>
/// <b>Attributes</b> are any sequence of key/value pairs, typically a
/// <c>Dictionary&lt;string, object&gt;</c>. Values may be string, bool, any
/// integer primitive, float, double, decimal (stored as double), char, Guid,
/// DateTime and DateTimeOffset (stored as ISO 8601 strings) or an enum (stored
/// by name). Pairs with a null value are dropped. Any other value type throws
/// <see cref="ArgumentException"/>.
/// </para>
/// <para>
/// <b>Feature ids</b> are assigned 1, 2, 3, … per layer unless you pass
/// <c>id</c>. Supply your own ids when the client needs to address features,
/// for example MapLibre's <c>setFeatureState</c>. Ids should be unique within a
/// layer, so pass them for every feature or for none.
/// </para>
/// <para>
/// Coordinate sequences are accepted as any <see cref="IEnumerable{T}"/>;
/// arrays are used directly without copying.
/// </para>
/// </summary>
public sealed class LayerBuilder
{
    private readonly string _name;
    private readonly uint _extent;
    private readonly double _clipBuffer;
    private readonly TileProjectionContext _ctx;
    private readonly TagEncoder _tags = new();
    private readonly List<ProtoTile.Types.Feature> _features = new();
    private ulong _nextId = 1;
    private readonly TileBuilder _tile;

    internal LayerBuilder(TileBuilder tile, string name, int z, int x, int y, uint extent)
    {
        _tile = tile;
        _name = name;
        _extent = extent;
        _clipBuffer = extent * TileMath.DefaultClipBufferFraction;
        _ctx = TileMath.CreateProjectionContext(z, x, y, extent);
    }

    // ---- Points ----------------------------------------------------------

    /// <summary>
    /// Adds a point feature at the given WGS84 coordinate, without attributes.
    /// Points inside the tile or its small buffer margin are kept, so symbols
    /// at tile seams are not cut off; anything further out is silently skipped.
    /// </summary>
    /// <param name="lat">Latitude in degrees.</param>
    /// <param name="lng">Longitude in degrees.</param>
    /// <param name="id">Feature id. Auto-assigned when null.</param>
    public LayerBuilder AddPoint(double lat, double lng, ulong? id = null) =>
        AddPoint<object>(lat, lng, null, id);

    /// <summary>
    /// Adds a point feature at the given WGS84 coordinate.
    /// Points inside the tile or its small buffer margin are kept, so symbols
    /// at tile seams are not cut off; anything further out is silently skipped.
    /// </summary>
    /// <param name="lat">Latitude in degrees.</param>
    /// <param name="lng">Longitude in degrees.</param>
    /// <param name="attributes">Key/value pairs that become the feature's tags. See the class remarks for supported value types.</param>
    /// <param name="id">Feature id. Auto-assigned when null.</param>
    public LayerBuilder AddPoint<TValue>(
        double lat,
        double lng,
        IEnumerable<KeyValuePair<string, TValue>>? attributes,
        ulong? id = null
    )
    {
        if (!TileMath.TryProjectWithinBuffer(lat, lng, _ctx, _clipBuffer, out var coord))
        {
            return this;
        }

        var feature = NewFeature(ProtoTile.Types.GeomType.Point, id);

        feature.Geometry.AddRange(GeometryEncoder.EncodePoint(coord.X, coord.Y));

        if (attributes is not null)
        {
            _tags.EncodeInto(attributes, feature.Tags);
        }

        _features.Add(feature);
        return this;
    }

    // ---- Lines -----------------------------------------------------------

    /// <summary>
    /// Adds a LineString feature from WGS84 coordinates, without attributes.
    /// See <see cref="AddLineString{TValue}"/>.
    /// </summary>
    public LayerBuilder AddLineString(IEnumerable<(double Lat, double Lng)> coords, ulong? id = null) =>
        AddLineString<object>(coords, null, id);

    /// <summary>
    /// Adds a LineString feature from WGS84 coordinates.
    /// The line is clipped to the tile extent (with a buffer margin) so only
    /// the visible portion is encoded. If the line leaves and re-enters the
    /// tile, the visible parts are encoded as one MultiLineString feature that
    /// keeps a single id and one set of tags. Silently skipped if the geometry
    /// doesn't overlap the tile at all.
    /// </summary>
    /// <param name="coords">At least two (lat, lng) positions.</param>
    /// <param name="attributes">Key/value pairs that become the feature's tags. See the class remarks for supported value types.</param>
    /// <param name="id">Feature id. Auto-assigned when null.</param>
    public LayerBuilder AddLineString<TValue>(
        IEnumerable<(double Lat, double Lng)> coords,
        IEnumerable<KeyValuePair<string, TValue>>? attributes,
        ulong? id = null
    )
    {
        if (coords is null)
        {
            throw new ArgumentNullException(nameof(coords));
        }
        var points = Materialize(coords);

        if (points.Length < 2)
        {
            return this;
        }

        if (!OverlapsTile(points))
        {
            return this;
        }

        var tileCoords = ProjectAll(points);
        var clippedSegments = LineClipper.Clip(tileCoords, _extent);

        clippedSegments.RemoveAll(static s => s.Length < 2);
        if (clippedSegments.Count == 0)
        {
            return this;
        }

        var feature = NewFeature(ProtoTile.Types.GeomType.Linestring, id);

        feature.Geometry.AddRange(
            clippedSegments.Count == 1
                ? GeometryEncoder.EncodeLineString(clippedSegments[0])
                : GeometryEncoder.EncodeMultiLineString(clippedSegments)
        );

        if (attributes is not null)
        {
            _tags.EncodeInto(attributes, feature.Tags);
        }

        _features.Add(feature);
        return this;
    }

    // ---- Polygons --------------------------------------------------------

    /// <summary>
    /// Adds a Polygon feature from a single WGS84 ring, without attributes.
    /// See <see cref="AddPolygon{TValue}(IEnumerable{ValueTuple{double, double}}, IEnumerable{KeyValuePair{string, TValue}}, ulong?)"/>.
    /// </summary>
    public LayerBuilder AddPolygon(IEnumerable<(double Lat, double Lng)> ring, ulong? id = null) =>
        AddPolygon<object>(ring, null, id);

    /// <summary>
    /// Adds a Polygon feature from a single WGS84 ring (no holes).
    /// The ring should NOT repeat the first point and may be given in either
    /// winding order; it is normalized to the orientation the MVT spec requires.
    /// The ring is clipped to the tile plus a small buffer, so polygons crossing
    /// tile boundaries render correctly and never emit runaway coordinates.
    /// </summary>
    /// <param name="ring">At least three (lat, lng) positions.</param>
    /// <param name="attributes">Key/value pairs that become the feature's tags. See the class remarks for supported value types.</param>
    /// <param name="id">Feature id. Auto-assigned when null.</param>
    public LayerBuilder AddPolygon<TValue>(
        IEnumerable<(double Lat, double Lng)> ring,
        IEnumerable<KeyValuePair<string, TValue>>? attributes,
        ulong? id = null
    )
    {
        if (ring is null)
        {
            throw new ArgumentNullException(nameof(ring));
        }
        var points = Materialize(ring);

        if (points.Length < 3)
        {
            return this;
        }

        if (!OverlapsTile(points))
        {
            return this;
        }

        var tileCoords = PolygonClipper.Clip(ProjectAll(points), _extent);

        if (tileCoords.Length < 3)
        {
            return this;
        }

        GeometryEncoder.Orient(tileCoords, exterior: true);

        var feature = NewFeature(ProtoTile.Types.GeomType.Polygon, id);

        feature.Geometry.AddRange(GeometryEncoder.EncodePolygon(tileCoords));

        if (attributes is not null)
        {
            _tags.EncodeInto(attributes, feature.Tags);
        }

        _features.Add(feature);
        return this;
    }

    /// <summary>
    /// Adds a Polygon feature with one or more holes, without attributes.
    /// See <see cref="AddPolygon{TValue}(IEnumerable{ValueTuple{double, double}}, IEnumerable{IEnumerable{ValueTuple{double, double}}}, IEnumerable{KeyValuePair{string, TValue}}, ulong?)"/>.
    /// </summary>
    public LayerBuilder AddPolygon(
        IEnumerable<(double Lat, double Lng)> outer,
        IEnumerable<IEnumerable<(double Lat, double Lng)>> holes,
        ulong? id = null
    ) => AddPolygon<object>(outer, holes, null, id);

    /// <summary>
    /// Adds a Polygon feature with one or more holes. The outer ring and each
    /// hole should NOT repeat the first point and may be given in either winding
    /// order; rings are normalized to the orientation the MVT spec requires.
    /// Rings with fewer than 3 points are silently dropped; if the outer ring is
    /// invalid the whole feature is skipped.
    /// </summary>
    /// <param name="outer">The exterior ring, at least three (lat, lng) positions.</param>
    /// <param name="holes">Interior rings.</param>
    /// <param name="attributes">Key/value pairs that become the feature's tags. See the class remarks for supported value types.</param>
    /// <param name="id">Feature id. Auto-assigned when null.</param>
    public LayerBuilder AddPolygon<TValue>(
        IEnumerable<(double Lat, double Lng)> outer,
        IEnumerable<IEnumerable<(double Lat, double Lng)>> holes,
        IEnumerable<KeyValuePair<string, TValue>>? attributes,
        ulong? id = null
    )
    {
        if (outer is null)
        {
            throw new ArgumentNullException(nameof(outer));
        }
        if (holes is null)
        {
            throw new ArgumentNullException(nameof(holes));
        }
        var outerPoints = Materialize(outer);

        if (outerPoints.Length < 3)
        {
            return this;
        }

        if (!OverlapsTile(outerPoints))
        {
            return this;
        }

        var outerCoords = PolygonClipper.Clip(ProjectAll(outerPoints), _extent);

        if (outerCoords.Length < 3)
        {
            return this;
        }

        GeometryEncoder.Orient(outerCoords, exterior: true);

        var rings = new List<TileCoord[]> { outerCoords };
        foreach (var holeEnumerable in holes)
        {
            var hole = Materialize(holeEnumerable);
            if (hole.Length < 3)
            {
                continue;
            }

            // A hole clipped away entirely just means it lay outside this tile.
            var holeCoords = PolygonClipper.Clip(ProjectAll(hole), _extent);
            if (holeCoords.Length < 3)
            {
                continue;
            }

            GeometryEncoder.Orient(holeCoords, exterior: false);
            rings.Add(holeCoords);
        }

        var feature = NewFeature(ProtoTile.Types.GeomType.Polygon, id);
        feature.Geometry.AddRange(GeometryEncoder.EncodePolygon(rings));

        if (attributes is not null)
        {
            _tags.EncodeInto(attributes, feature.Tags);
        }

        _features.Add(feature);
        return this;
    }

    // ---- GeoJSON ---------------------------------------------------------

    /// <summary>
    /// Ingests a GeoJSON document (FeatureCollection, Feature, or bare geometry)
    /// and adds each feature to this layer. Top-level scalar <c>properties</c>
    /// become MVT tags; nested objects, arrays, and source feature ids are
    /// dropped. Multi-geometries and GeometryCollection are flattened to N MVT
    /// features sharing the same tags.
    /// </summary>
    /// <param name="json">The GeoJSON text.</param>
    /// <param name="strict">
    /// When false (the default) malformed JSON and invalid features are
    /// silently skipped, which suits untrusted input. When true, malformed JSON
    /// throws <see cref="JsonException"/> and the first invalid feature throws
    /// <see cref="FormatException"/> describing the problem.
    /// </param>
    public LayerBuilder AddGeoJson(string json, bool strict = false)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            if (strict)
            {
                throw new FormatException("GeoJSON input is empty.");
            }
            return this;
        }

        if (strict)
        {
            using var strictDoc = JsonDocument.Parse(json);
            GeoJsonReader.Read(this, strictDoc.RootElement, strict: true);
            return this;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            GeoJsonReader.Read(this, doc.RootElement, strict: false);
        }
        catch (JsonException)
        {
            // skip malformed input
        }

        return this;
    }

    /// <summary>
    /// Ingests a GeoJSON document from a UTF-8 stream. See
    /// <see cref="AddGeoJson(string, bool)"/> for semantics.
    /// </summary>
    public LayerBuilder AddGeoJson(Stream utf8Json, bool strict = false)
    {
        if (utf8Json is null)
        {
            throw new ArgumentNullException(nameof(utf8Json));
        }

        if (strict)
        {
            using var strictDoc = JsonDocument.Parse(utf8Json);
            GeoJsonReader.Read(this, strictDoc.RootElement, strict: true);
            return this;
        }

        try
        {
            using var doc = JsonDocument.Parse(utf8Json);
            GeoJsonReader.Read(this, doc.RootElement, strict: false);
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
    /// See <see cref="AddGeoJson(string, bool)"/> for semantics.
    /// </summary>
    public LayerBuilder AddGeoJson(JsonElement element, bool strict = false)
    {
        GeoJsonReader.Read(this, element, strict);
        return this;
    }

    // ---- Internals -------------------------------------------------------

    private ProtoTile.Types.Feature NewFeature(ProtoTile.Types.GeomType type, ulong? id) =>
        new() { Id = id ?? _nextId++, Type = type };

    private static ReadOnlySpan<(double Lat, double Lng)> Materialize(
        IEnumerable<(double Lat, double Lng)> coords
    )
    {
        if (coords is (double Lat, double Lng)[] array)
        {
            return array;
        }

#if !NETSTANDARD2_0
        if (coords is List<(double Lat, double Lng)> list)
        {
            return CollectionsMarshal.AsSpan(list);
        }
#endif

        return coords.ToArray();
    }

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

    /// <summary>
    /// The tile this layer belongs to. Use it to add further layers, or to
    /// serialize once every layer is populated: <c>layer.Tile.Build()</c>.
    /// </summary>
    public TileBuilder Tile => _tile;

    /// <summary>
    /// Builds the whole tile, not just this layer.
    /// </summary>
    [Obsolete(
        "Builds the entire tile, not just this layer, which is the opposite of how it reads. Use Tile.Build() instead."
    )]
    public byte[] Build() => _tile.Build();

    /// <summary>
    /// Builds the whole tile into a stream, not just this layer.
    /// </summary>
    [Obsolete(
        "Builds the entire tile, not just this layer, which is the opposite of how it reads. Use Tile.Build(output) instead."
    )]
    public void Build(Stream output) => _tile.Build(output);

    internal ProtoTile.Types.Layer BuildLayer()
    {
        var layer = new ProtoTile.Types.Layer
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
