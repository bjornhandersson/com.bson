using System.Text;
using System.Text.Json;

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
    private readonly List<LayerBuilder> _layerOrder = new();

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
            _layerOrder.Add(layer);
        }

        return layer;
    }

    /// <summary>
    /// Serializes all layers into an MVT protobuf, ready to be served as
    /// <c>application/vnd.mapbox-vector-tile</c>.
    /// </summary>
    public byte[] Build()
    {
        int total = 0;
        foreach (var layer in _layerOrder)
        {
            total += layer.FramedSize();
        }

        if (total == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new ProtoBuffer(total);
        foreach (var layer in _layerOrder)
        {
            layer.WriteTo(buffer);
        }

        return buffer.ToArray();
    }

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

        var scratch = new ProtoBuffer(64);
        foreach (var layer in _layerOrder)
        {
            layer.WriteTo(output, scratch);
        }
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
    private readonly ProtoBuffer _features = new();
    private readonly ProtoBuffer _tagScratch = new();
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

        AddFeature(GeomType.Point, id, GeometryEncoder.EncodePoint(coord.X, coord.Y), attributes);
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

        var geometry =
            clippedSegments.Count == 1
                ? GeometryEncoder.EncodeLineString(clippedSegments[0])
                : GeometryEncoder.EncodeMultiLineString(clippedSegments);

        AddFeature(GeomType.LineString, id, geometry, attributes);
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

        AddFeature(GeomType.Polygon, id, GeometryEncoder.EncodePolygon(tileCoords), attributes);
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

            var holeCoords = PolygonClipper.Clip(ProjectAll(hole), _extent);
            bool holeOutsideTile = holeCoords.Length < 3;
            if (holeOutsideTile)
            {
                continue;
            }

            GeometryEncoder.Orient(holeCoords, exterior: false);
            rings.Add(holeCoords);
        }

        AddFeature(GeomType.Polygon, id, GeometryEncoder.EncodePolygon(rings), attributes);
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

        return AddGeoJson(() => JsonDocument.Parse(json), strict);
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

        return AddGeoJson(() => JsonDocument.Parse(utf8Json), strict);
    }

    private LayerBuilder AddGeoJson(Func<JsonDocument> parse, bool strict)
    {
        try
        {
            using var doc = parse();
            GeoJsonReader.Read(this, doc.RootElement, strict);
        }
        catch (JsonException) when (!strict) { }

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

    private const byte TileLayersTag = (3 << 3) | ProtoBuffer.LengthDelimited;
    private const byte LayerNameTag = (1 << 3) | ProtoBuffer.LengthDelimited;
    private const byte LayerFeaturesTag = (2 << 3) | ProtoBuffer.LengthDelimited;
    private const byte LayerExtentTag = (5 << 3) | ProtoBuffer.Varint;
    private const byte LayerVersionTag = (15 << 3) | ProtoBuffer.Varint;
    private const byte FeatureIdTag = (1 << 3) | ProtoBuffer.Varint;
    private const byte FeatureTagsTag = (2 << 3) | ProtoBuffer.LengthDelimited;
    private const byte FeatureTypeTag = (3 << 3) | ProtoBuffer.Varint;
    private const byte FeatureGeometryTag = (4 << 3) | ProtoBuffer.LengthDelimited;
    private const byte LayerVersion = 2;

    private void AddFeature<TValue>(
        GeomType type,
        ulong? id,
        uint[] geometry,
        IEnumerable<KeyValuePair<string, TValue>>? attributes
    )
    {
        _tagScratch.Clear();
        if (attributes is not null)
        {
            _tags.WriteTags(attributes, _tagScratch);
        }

        ulong featureId = id ?? _nextId++;
        int tagBytes = _tagScratch.Count;
        int geometryBytes = ProtoBuffer.PackedSize(geometry);

        int body =
            1 + ProtoBuffer.VarintSize(featureId)
            + 1 + sizeof(byte)
            + 1 + ProtoBuffer.VarintSize((ulong)geometryBytes) + geometryBytes;
        if (tagBytes > 0)
        {
            body += 1 + ProtoBuffer.VarintSize((ulong)tagBytes) + tagBytes;
        }

        _features.WriteByte(LayerFeaturesTag);
        _features.WriteVarint((ulong)body);
        _features.WriteByte(FeatureIdTag);
        _features.WriteVarint(featureId);
        if (tagBytes > 0)
        {
            _features.WriteByte(FeatureTagsTag);
            _features.WriteVarint((ulong)tagBytes);
            _features.Write(_tagScratch);
        }
        _features.WriteByte(FeatureTypeTag);
        _features.WriteByte((byte)type);
        _features.WriteByte(FeatureGeometryTag);
        _features.WritePacked(geometry);
    }

    private static ReadOnlySpan<(double Lat, double Lng)> Materialize(
        IEnumerable<(double Lat, double Lng)> coords
    )
    {
        if (coords is (double Lat, double Lng)[] array)
        {
            return array;
        }

        return coords.ToArray();
    }

    private bool OverlapsTile(ReadOnlySpan<(double Lat, double Lng)> coords)
    {
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

        var (minX, minY) = TileMath.ProjectUnrounded(maxLat, minLng, _ctx);
        var (maxX, maxY) = TileMath.ProjectUnrounded(minLat, maxLng, _ctx);
        double min = -_clipBuffer;
        double max = _extent + _clipBuffer;

        return maxX >= min && minX <= max && maxY >= min && minY <= max;
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

    private int BodySize()
    {
        int nameBytes = Encoding.UTF8.GetByteCount(_name);
        return 1 + ProtoBuffer.VarintSize((ulong)nameBytes) + nameBytes
            + _features.Count
            + _tags.EncodedKeys.Count
            + _tags.EncodedValues.Count
            + 1 + ProtoBuffer.VarintSize(_extent)
            + 1 + sizeof(byte);
    }

    internal int FramedSize()
    {
        int body = BodySize();
        return 1 + ProtoBuffer.VarintSize((ulong)body) + body;
    }

    internal void WriteTo(ProtoBuffer output)
    {
        WriteHeader(output);
        output.Write(_features);
        output.Write(_tags.EncodedKeys);
        output.Write(_tags.EncodedValues);
        WriteTrailer(output);
    }

    internal void WriteTo(Stream output, ProtoBuffer scratch)
    {
        scratch.Clear();
        WriteHeader(scratch);
        scratch.WriteTo(output);

        _features.WriteTo(output);
        _tags.EncodedKeys.WriteTo(output);
        _tags.EncodedValues.WriteTo(output);

        scratch.Clear();
        WriteTrailer(scratch);
        scratch.WriteTo(output);
    }

    private void WriteHeader(ProtoBuffer output)
    {
        output.WriteByte(TileLayersTag);
        output.WriteVarint((ulong)BodySize());
        output.WriteByte(LayerNameTag);
        output.WriteString(_name);
    }

    private void WriteTrailer(ProtoBuffer output)
    {
        output.WriteByte(LayerExtentTag);
        output.WriteVarint(_extent);
        output.WriteByte(LayerVersionTag);
        output.WriteByte(LayerVersion);
    }
}

internal enum GeomType : byte
{
    Unknown = 0,
    Point = 1,
    LineString = 2,
    Polygon = 3,
}
