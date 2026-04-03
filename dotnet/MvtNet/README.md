# MvtNet

A C# library that encodes WGS84 geographic features into [Mapbox Vector Tiles](https://github.com/mapbox/vector-tile-spec) (MVT spec v2.1).

Give it a tile address and some coordinates. Get back MVT bytes.

## Usage

```csharp
var tile = new TileBuilder(z: 10, x: 563, y: 301);

tile.Layer("pois")
    .AddPoint(59.328, 18.044, new Dictionary<string, object>
    {
        ["name"] = "Stockholm"
    });

byte[] mvt = tile.Build();
```

Serve it from any HTTP endpoint:

```csharp
app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", (int z, int x, int y) =>
{
    var tile = new TileBuilder(z, x, y);
    tile.Layer("pois").AddPoint(59.328, 18.044);
    return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
});
```

## Geometry types

- **Point** -- `AddPoint(lat, lng)`
- **LineString** -- `AddLineString(coords)`
- **Polygon** -- `AddPolygon(ring)`

All methods take WGS84 coordinates and optional attributes. Points outside the tile are automatically skipped.

## Install

```
dotnet add package MvtNet
```

Targets .NET 10. Single dependency: `Google.Protobuf`.

## License

MIT
