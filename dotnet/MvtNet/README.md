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

## Examples

### Delivery fleet tracker

Show live vehicle positions, each with speed and status. The frontend renders
them differently based on attributes — MvtNet just encodes whatever you throw at it.

```csharp
app.MapGet("/tiles/{z:int}/{x:int}/{y:int}", async (int z, int x, int y, FleetDb db) =>
{
    var vehicles = await db.GetVehiclesInTile(z, x, y);
    var layer = new TileBuilder(z, x, y).Layer("vehicles");

    foreach (var v in vehicles)
    {
        layer.AddPoint(v.Lat, v.Lng, new Dictionary<string, object>
        {
            ["id"] = v.Id,
            ["speed"] = v.Speed,
            ["status"] = v.Status, // "delivering", "idle", "returning"
        });
    }

    return Results.Bytes(layer.Build(), "application/vnd.mapbox-vector-tile");
});
```

### GPS track replay

Encode a recorded GPS track as a LineString. MvtNet clips it to tile boundaries
automatically — a track crossing 50 tiles only encodes the visible segment per tile.

```csharp
var tile = new TileBuilder(z, x, y);
var track = gpxFile.Points.Select(p => (p.Lat, p.Lng)).ToArray();

tile.Layer("track").AddLineString(track, new Dictionary<string, object>
{
    ["name"] = "Morning run",
    ["distance_km"] = 12.4,
});
```

### Geofence zones

Draw delivery zones, restricted areas, or coverage polygons. Ring coordinates
don't need to repeat the first point — MvtNet closes the ring for you.

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

### Geohash-indexed database queries

Got millions of rows with a geohash column? `TileGeohash` gives you the exact
prefixes to query — turns a tile request into a fast `WHERE geohash LIKE 'prefix%'` scan.

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

Works with any geohash-indexed store (MySQL, Postgres, DynamoDB) — no PostGIS required.

