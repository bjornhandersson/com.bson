# MvtNet

WGS84 coordinates in, [MVT](https://github.com/mapbox/vector-tile-spec) bytes out. That's it.

```
dotnet add package MvtNet
```

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("pois").AddPoint(59.328, 18.044);
byte[] mvt = tile.Build();
```

## Supported geometry

- **Point** — markers, POIs, events
- **LineString** — routes, tracks, paths
- **Polygon** — zones, geofences, areas

All methods take WGS84 coordinates and optional key/value attributes. Geometry that crosses tile boundaries just works.

```csharp
layer.AddPoint(lat, lng);
layer.AddLineString(coords);
layer.AddPolygon(ring);
```

MVT spec v2.1. .NET 10. One dependency (`Google.Protobuf`). MIT.
