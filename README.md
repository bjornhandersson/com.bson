# com.bson

Open-source libraries for geospatial indexing, concurrent processing, and map tile encoding. Published on NuGet, MIT licensed.

[![NuGet](https://img.shields.io/nuget/v/Bson.MvtNet?label=MvtNet)](https://www.nuget.org/packages/Bson.MvtNet)
[![NuGet](https://img.shields.io/nuget/v/Bson.HilbertIndex?label=HilbertIndex)](https://www.nuget.org/packages/Bson.HilbertIndex)
[![NuGet](https://img.shields.io/nuget/v/Bson.Dispatcher?label=Dispatcher)](https://www.nuget.org/packages/Bson.Dispatcher)
[![CI](https://github.com/bjornhandersson/com.bson/actions/workflows/ci.yml/badge.svg)](https://github.com/bjornhandersson/com.bson/actions/workflows/ci.yml)

---

## MvtNet

Encode [Mapbox Vector Tiles](https://github.com/mapbox/vector-tile-spec) from plain C#. No GIS stack, no PostGIS, no Mapnik.

```
dotnet add package Bson.MvtNet
```

```csharp
var tile = new TileBuilder(z, x, y);
tile.Layer("trucks").AddPoint(lat, lng, new Dictionary<string, object> { ["speed"] = 82.5 });
return Results.Bytes(tile.Build(), "application/vnd.mapbox-vector-tile");
```

Projection, clipping, and protobuf encoding happen automatically. Bridge tile coordinates to geohash prefixes for fast SQL lookups with `TileGeohash`. Targets netstandard2.0 and net6.0.

[Read more &rarr;](dotnet/MvtNet/README.md)

---

## HilbertIndex

Fast geospatial indexing using Hilbert space-filling curves. Nearby points get similar index values &mdash; perfect for spatial queries, zoom levels, and delivery zones.

```
dotnet add package Bson.HilbertIndex
```

```csharp
var index = new HilbertIndex<Restaurant>(
    restaurants, r => new Coordinate(r.Lng, r.Lat));

var nearby = index.NearestNeighbors(new Coordinate(18.07, 59.33), count: 5);
```

Five resolution levels from city-block (~40 m) to global (~10 m precision on a billion-cell grid).

[Read more &rarr;](dotnet/HilbertIndex/README.md)

---

## Dispatcher

Parallel async task processing with strict ordering per partition key.

```
dotnet add package Bson.Dispatcher
```

```csharp
await using var dispatcher = new AsyncDispatcher();

await dispatcher.EnqueueAsync("order-123", async ct =>
{
    await HandleOrder(ct);
});
```

Same key = sequential. Different keys = parallel. Configurable partitions, backpressure, and timeout handling.

[Read more &rarr;](dotnet/Dispatcher/README.md)

---

## Also in this repo

| Project | Language | What it does |
|---------|----------|--------------|
| [Raft](dotnet/Raft/README.md) | C# | Raft consensus algorithm &mdash; leader election, log replication, deterministic tests |
| [PiBrew](python/brew/README.md) | Python | Raspberry Pi brewing controller with PID temperature control and web UI |
| [ipforward](rust/ipforward/) | Rust | High-performance UDP packet forwarder for IoT (100k+ devices) |

## License

[MIT](LICENSE)
