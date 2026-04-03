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

## Examples

### Point with attributes

```csharp
var layer = tile.Layer("events");
layer.AddPoint(59.328, 18.044, new Dictionary<string, object>
{
    ["name"] = "Arrival",
    ["timestamp"] = 1712150400L,
    ["speed"] = 42.5
});
```

### LineString

```csharp
var layer = tile.Layer("tracks");
layer.AddLineString(new (double Lat, double Lng)[]
{
    (59.334, 18.030),
    (59.328, 18.044),
    (59.332, 18.065),
});
```

### Polygon

```csharp
var layer = tile.Layer("geofences");
layer.AddPolygon(new (double Lat, double Lng)[]
{
    (59.338, 18.025),
    (59.338, 18.075),
    (59.315, 18.075),
    (59.315, 18.025),
});
```

### Multiple layers in one tile

```csharp
var tile = new TileBuilder(z, x, y);

tile.Layer("pois").AddPoint(59.328, 18.044);

tile.Layer("routes").AddLineString(new (double, double)[]
{
    (59.334, 18.030),
    (59.328, 18.044),
});

tile.Layer("zones").AddPolygon(new (double, double)[]
{
    (59.338, 18.025),
    (59.338, 18.075),
    (59.315, 18.075),
    (59.315, 18.025),
});

byte[] mvt = tile.Build();
```

### Serving tiles

```csharp
app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", (int z, int x, int y) =>
{
    var tile = new TileBuilder(z, x, y);
    // add your features...
    return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
});
```

