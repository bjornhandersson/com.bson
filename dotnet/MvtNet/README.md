# MvtNet

[![NuGet](https://img.shields.io/nuget/v/Bson.MvtNet?label=nuget)](https://www.nuget.org/packages/Bson.MvtNet)
[![CI](https://github.com/bjornhandersson/com.bson/actions/workflows/ci.yml/badge.svg)](https://github.com/bjornhandersson/com.bson/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/bjornhandersson/com.bson/blob/master/LICENSE)

Encode [Mapbox Vector Tiles](https://github.com/mapbox/vector-tile-spec) from plain C#. No GIS stack, no PostGIS, no Mapnik.

```
dotnet add package Bson.MvtNet
```

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("trucks").AddPoint(lat, lng, new() { ["speed"] = 82.5 });
return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
```

[![10,000 cities served as vector tiles by MvtNet](https://raw.githubusercontent.com/bjornhandersson/com.bson/master/docs/img/mvtnet-cities.jpg)](https://github.com/bjornhandersson/com.bson/tree/master/dotnet/MvtNet/MvtNet.Demo)

<p align="center"><em>10,000 cities encoded on the fly and rendered with MapLibre. Run it yourself with <code>dotnet run --project MvtNet.Demo</code>.</em></p>

## Show 100k delivery trucks on a map

```csharp
app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", async (int z, int x, int y, FleetDb db) =>
{
    var prefixes = TileGeohash.GetPrefixes(z, x, y);  // geohash → SQL
    var vehicles = await db.GetByGeohashPrefixes(prefixes);

    var layer = new TileBuilder(z, x, y).Layer("vehicles");
    foreach (var v in vehicles)
    {
        layer.AddPoint(v.Lat, v.Lng, new Dictionary<string, object>
        {
            ["id"] = v.Id,
            ["speed"] = v.Speed,
            ["status"] = v.Status,
        });
    }

    return Results.Bytes(layer.Tile.Build(), "application/vnd.mapbox-vector-tile");
});
```

`TileGeohash.GetPrefixes` turns a tile into `WHERE geohash LIKE 'u6s%'` queries.
Works with MySQL, Postgres, DynamoDB — anything with a geohash column.

## Draw a GPS track across the country

A track might span 200 tiles. Each tile only encodes its visible segment — clipping is automatic.

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

Ring coordinates don't need to repeat the first point — MvtNet closes the ring.

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
    ["type"] = "express",
});
```

## Already got GeoJSON?

Pass it to `AddGeoJson`:

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("data").AddGeoJson(geoJson);
return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
```

## Query millions of rows without PostGIS

Got a geohash column? `TileGeohash` gives you the exact prefixes to query — turns a tile request into a fast index scan.

```csharp
app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", async (int z, int x, int y, DbConnection db) =>
{
    var prefixes = TileGeohash.GetPrefixes(z, x, y);
    var tile = new TileBuilder(z, x, y);
    var layer = tile.Layer("events");

    foreach (var prefix in prefixes)
    {
        var rows = await db.QueryAsync<Event>(
            "SELECT lat, lng, name FROM events WHERE geohash LIKE @p",
            new { p = prefix + "%" });

        foreach (var row in rows)
        {
            layer.AddPoint(row.Lat, row.Lng, new Dictionary<string, object>
            {
                ["name"] = row.Name,
            });
        }
    }

    return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
});
```

## What it handles for you

- **Projection** — WGS84 lat/lng to tile pixel coordinates
- **Clipping** — lines and polygon rings crossing tile boundaries are cut cleanly, so oversized geometry never reaches the renderer
- **Encoding** — MVT spec v2.1 protobuf, delta-encoded geometry, interned tags
- **Attributes** — string, bool, integers, float/double, decimal and enums become tags; null values are dropped
- **Winding order** — polygon rings are normalized to the spec's orientation (exterior clockwise, holes counter-clockwise), whatever order you pass them in
- **GeoJSON ingestion** — drop a whole `FeatureCollection` onto a layer in one call
- **Geohash bridge** — tile z/x/y to geohash prefixes for indexed DB queries
