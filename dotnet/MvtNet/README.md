# MvtNet

WGS84 coordinates in, [MVT](https://github.com/mapbox/vector-tile-spec) bytes out. That's it.

```
dotnet add package MvtNet
```

## Quick start

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("pois").AddPoint(59.328, 18.044);
byte[] mvt = tile.Build();
```

## Supported geometry

- **Point** — markers, POIs, events
- **LineString** — routes, tracks, paths
- **Polygon** — zones, geofences, areas

All methods take WGS84 coordinates and optional key/value attributes.
Geometry that crosses tile boundaries just works.

## Geohash queries

Got data indexed by geohash? `TileGeohash` tells you which prefixes to query for a given tile.

```csharp
var prefixes = TileGeohash.GetPrefixes(z, x, y);
// → use with WHERE geohash LIKE 'prefix%'
```

Works with any geohash-indexed store — no PostGIS required.

## Demo

The `MvtNet.Demo.Api` + `MvtNet.Demo.UI` projects show a live map with 50k POIs, a route, and a polygon — all served as vector tiles.

```bash
# Terminal 1 — API
cd MvtNet.Demo.Api
dotnet run

# Terminal 2 — UI
cd MvtNet.Demo.UI
npm install
npm run dev
```

Open http://localhost:3000 and zoom around.
