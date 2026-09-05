# MvtNet

[![NuGet](https://img.shields.io/nuget/v/Bson.MvtNet?label=nuget)](https://www.nuget.org/packages/Bson.MvtNet)
[![CI](https://github.com/bjornhandersson/com.bson/actions/workflows/ci.yml/badge.svg)](https://github.com/bjornhandersson/com.bson/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/bjornhandersson/com.bson/blob/master/LICENSE)

Encode [Mapbox Vector Tiles](https://github.com/mapbox/vector-tile-spec) from plain C#. No GIS stack, no PostGIS, no Mapnik, no dependencies.

```
dotnet add package Bson.MvtNet
```

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("trucks").AddPoint(lat, lng, new Dictionary<string, object> { ["speed"] = 82.5 });
return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
```

[![10,000 cities served as vector tiles by MvtNet](https://raw.githubusercontent.com/bjornhandersson/com.bson/master/docs/img/mvtnet-cities.jpg)](https://github.com/bjornhandersson/com.bson/tree/master/dotnet/MvtNet/MvtNet.Demo)

<p align="center"><em>10,000 cities encoded on the fly and rendered with MapLibre. Run it yourself with <code>dotnet run --project MvtNet.Demo</code>.</em></p>

Works on .NET 6+, .NET Framework 4.6.1+, Mono and Unity (netstandard2.0). The protobuf wire format is written by hand, so the .NET 6+ build has no package dependencies and the netstandard2.0 build needs only `System.Memory` and `System.Text.Json`.

## Serve 100k vehicles straight from your database

A tile request becomes a handful of `WHERE geohash LIKE 'u6s%'` prefix queries.
Any database with a geohash column works: MySQL, Postgres, SQL Server, DynamoDB, Cosmos.

```csharp
app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", async (int z, int x, int y, FleetDb db) =>
{
    var prefixes = TileGeohash.GetPrefixes(z, x, y);   // tile → geohash prefixes
    var vehicles = await db.GetByGeohashPrefixes(prefixes);

    var layer = new TileBuilder(z, x, y).Layer("vehicles");
    foreach (var v in vehicles)
    {
        layer.AddPoint(v.Lat, v.Lng, new Dictionary<string, object>
        {
            ["speed"] = v.Speed,
            ["status"] = v.Status,
        }, id: v.Id);
    }

    return Results.Bytes(layer.Tile.Build(), "application/vnd.mapbox-vector-tile");
});
```

Index the vehicle rows on the geohash column and every tile is a fast range scan.
Encode the column with `Geohash.Encode(lat, lng, precision: 7)` when you write the row.

## Show it on a map

Point any MVT client at the endpoint. With [MapLibre GL JS](https://maplibre.org/):

```js
map.addSource("fleet", {
  type: "vector",
  tiles: [window.location.origin + "/tiles/{z}/{x}/{y}"],
});

map.addLayer({
  id: "vehicles",
  type: "circle",
  source: "fleet",
  "source-layer": "vehicles",              // the name you passed to tile.Layer(...)
  paint: {
    "circle-color": ["match", ["get", "status"], "moving", "#22c55e", "#f59e0b"],
    "circle-radius": 5,
  },
});

// Feature ids flow through, so hover and selection state just work:
map.on("mousemove", "vehicles", (e) =>
  map.setFeatureState({ source: "fleet", sourceLayer: "vehicles", id: e.features[0].id }, { hover: true }));
```

Mapbox GL, OpenLayers, Leaflet.VectorGrid and QGIS read the same tiles.

## Draw a GPS track across the country

A track might span 200 tiles. Each tile encodes only its visible part, and a track that leaves and re-enters a tile stays one feature.

```csharp
var tile = new TileBuilder(z, x, y);
var track = gpxFile.Points.Select(p => (p.Lat, p.Lng));

tile.Layer("track").AddLineString(track, new Dictionary<string, object>
{
    ["name"] = "Morning run",
    ["distance_km"] = 12.4,
});
```

## Mark a no-fly zone

Rings don't need to repeat the first point, and either winding order is fine. Pass holes as a second argument.

```csharp
var tile = new TileBuilder(z, x, y);

tile.Layer("zones").AddPolygon(new (double, double)[]
{
    (59.338, 18.025),
    (59.338, 18.075),
    (59.315, 18.075),
    (59.315, 18.025),
}, new Dictionary<string, object>
{
    ["name"] = "Zone A",
    ["type"] = "restricted",
});
```

## Already got GeoJSON?

Pass it to `AddGeoJson`. FeatureCollections, single Features and bare geometries all work; scalar `properties` become tags.

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("data").AddGeoJson(geoJson);
return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
```

Parse once and reuse the `JsonElement` when the same document feeds many tiles.
By default malformed input is skipped, which suits untrusted uploads. Pass `strict: true` to get a `JsonException` or `FormatException` that says what is wrong.

## Attributes and feature ids

Attributes are any sequence of key/value pairs. A `Dictionary<string, object>` is typical, but `Dictionary<string, string>`, `Dictionary<string, object?>` or a list of `KeyValuePair` work too, without nullability warnings.

| Value type | Stored as |
|---|---|
| `string`, `char`, enum | string (enums by name) |
| `bool` | bool |
| `int`, `long`, `short`, `byte`, `sbyte`, `ushort`, `uint` | signed int |
| `ulong` | unsigned int |
| `float` | float |
| `double`, `decimal` | double |
| `Guid`, `DateTime`, `DateTimeOffset` | ISO 8601 string |
| `null` | dropped |
| anything else | `ArgumentException` |

Feature ids are assigned 1, 2, 3, … per layer. Pass `id:` to use your own, which is what MapLibre's `setFeatureState` and `promoteId`-free hover effects need. Ids should be unique within a layer, so supply them for every feature or for none.

## What it handles for you

- **Projection** — WGS84 lat/lng to tile pixel coordinates
- **Clipping** — lines and polygon rings crossing tile boundaries are cut to the tile plus a 5% buffer, so oversized geometry never reaches the renderer and fills meet cleanly at seams. Points in the buffer are kept so icons and labels aren't cut off at tile edges
- **Encoding** — MVT spec v2.1 protobuf, delta-encoded geometry, interned keys and values
- **Winding order** — polygon rings are normalized to the spec's orientation, whatever order you pass them in
- **GeoJSON ingestion** — drop a whole `FeatureCollection` onto a layer in one call
- **Geohash bridge** — tile z/x/y to geohash prefixes for indexed DB queries

## Performance

Measured with BenchmarkDotNet on an Apple M5, .NET 10. Numbers are for building one tile.

| Scenario | Time | Allocated |
|---|---:|---:|
| 1,000 points, no attributes | 31 µs | 78 KB |
| 1,000 points, 3 attributes each | 114 µs | 305 KB |
| 10,000 points, 3 attributes each | 1.7 ms | 2.7 MB |
| 1,000-vertex route fully inside the tile | 11 µs | 26 KB |
| 1,000-vertex route missing the tile | 0.7 µs | 1.3 KB |
| 500 polygons, each larger than the tile | 1.4 ms | 6.6 MB |
| `TileGeohash.GetPrefixes` | 0.5 µs | 800 B |

Features are encoded as they are added, so `Build()` is a size pass and one copy: 7 µs for a 5,000-feature tile, allocating only the returned array. `Build(Stream)` writes straight to the response and allocates nothing beyond a 120-byte scratch buffer. Run `dotnet run -c Release --project Bson.MvtNet.Benchmarks` to reproduce.

## Serving tiles well

- Set the content type to `application/vnd.mapbox-vector-tile`.
- Tiles are ideal cache targets. Send `Cache-Control: public, max-age=...` sized to how fast your data changes, and add an `ETag` if the data has a version.
- Return an empty tile (a `TileBuilder` with no features) rather than 404 for areas with no data. Clients handle both, but an empty tile caches better.
- One `TileBuilder` per request. It is not thread-safe and is meant to be short-lived.

## Limitations

- **Encoder only.** MvtNet does not decode tiles. The tests decode with `Google.Protobuf` and the schema in `Bson.MvtNet.Tests/Proto` if you need to read one back.
- **Multi-geometries flatten.** GeoJSON `MultiPoint`, `MultiPolygon` and `GeometryCollection` become one MVT feature per part, all sharing the same tags. A line that is clipped into several pieces stays one feature.
- **2D only.** Altitude in GeoJSON positions is ignored.
- **No antimeridian handling.** Geometry that crosses ±180° longitude is not split.
- **Web Mercator only.** Tiles follow the XYZ scheme (y = 0 at the north), as used by MapLibre, Mapbox and OpenLayers.

## Release notes

### 1.2.1

- Fixed: consecutive vertices that land on the same tile pixel produced zero-length LineTo steps, which the spec forbids. A 1,000-point GPS track at low zoom now encodes only the handful of pixels it actually covers.
- Fixed: the fast overlap check used the bare tile bounds while the clippers use the tile plus its 5% buffer, so a line or polygon lying just outside the tile edge was dropped while a point at the same position was kept.
- Fixed: vertices far outside the tile at high zoom could wrap to the wrong side on x64 .NET 8 and .NET Framework. Projection now saturates.

### 1.2.0

- Google.Protobuf is no longer a dependency. Tiles are written straight to the protobuf wire format, feature by feature, as they are added. The .NET 6+ package has no dependencies at all.
- Encoding is 1.5x to 3.5x faster and allocates about half as much. `Build()` went from 500 µs to 7 µs on a 5,000-feature tile, and `Build(Stream)` no longer allocates.
- Fixed: after `ClosePath` the geometry cursor was reset to the ring's first vertex, but the spec keeps it at the last vertex. Holes and any ring after the first were decoded shifted by that difference. Polygons without holes were unaffected.
- Layers are serialized in the order they were first created.
- An attribute of an unsupported type still throws `ArgumentException`, and now also leaves the feature id sequence untouched.

Tiles without polygon holes are byte-for-byte identical to 1.1.0 output. If your project relied on MvtNet to bring in Google.Protobuf transitively, add the package reference yourself.

### 1.1.0

- `AddPoint`, `AddLineString` and `AddPolygon` take an optional `id` for caller-supplied feature ids.
- Attribute parameters are generic over the value type, so `Dictionary<string, object?>` and typed dictionaries compile without nullability warnings.
- `Guid`, `DateTime`, `DateTimeOffset` and `char` are accepted as attribute values.
- Points within the 5% tile buffer are now kept, matching lines and polygons. Previously they were dropped at the exact tile edge.
- A line that leaves and re-enters a tile is encoded as one MultiLineString feature instead of several features.
- `AddGeoJson` gained a `strict` flag that throws on malformed input instead of skipping it.
- The NuGet package now ships this README and an icon.

Source-compatible with 1.0.x. Calls that relied on target-typed `new()` for attributes were never valid C# and need an explicit `Dictionary<string, object>`.
