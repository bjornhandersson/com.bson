# MvtNet — Plan

## Goal

**MvtNet** is the core deliverable — a standalone C# library that encodes WGS84 geographic features into Mapbox Vector Tiles (MVT spec v2.1).

MvtNet is NOT a map server. It produces MVT tiles meant to be used as a **custom overlay layer** on top of a standard map (OpenStreetMap, Mapbox, etc.).

The Demo API and UI exist solely as a test harness to exercise and visually verify the library.

### What MvtNet does

Given a tile address (z/x/y) and a set of features:
1. Determines the geographic bounds of that tile
2. Finds which features fall within those bounds
3. Projects matching WGS84 coordinates into tile-local integer coordinates
4. Encodes them as a valid MVT protobuf tile

## Data Sources

| Type | Input format | MVT geometry |
|------|-------------|--------------|
| Tracks | Ordered WGS84 points | LineString |
| POIs | WGS84 point + description | Point + attributes |
| Geofences | WGS84 polygons | Polygon |
| Events | WGS84 points + attributes | Point + attributes |

**Design principle:** MvtNet operates strictly on WGS84 coordinates and MVT-native geometry types. Any upstream conversions (geohash → lat/lng, circle → polygon approximation, etc.) are the caller's responsibility. A helper for circle-to-polygon may be added later as a convenience, but is not part of the core encoding API.

## Project Structure

```
MvtNet/
├── MvtNet.sln                  # Solution file
├── MvtNet/                     # THE LIBRARY — standalone MVT encoding (future NuGet)
│   ├── Proto/vector_tile.proto  # MVT spec v2.1 protobuf definition
│   └── MvtNet.csproj
├── MvtNet.Tests/               # Unit tests for the library
│   └── MvtNet.Tests.csproj
├── MvtNet.Demo/                    # Test harness — .NET 10 minimal API, serves /tiles/{z}/{x}/{y}
│   └── MvtNet.Demo.csproj
├── MvtNet.Demo.UI/                 # Test harness — Next.js + MapLibre GL JS, visualizes overlay
│   └── package.json
├── .vscode/                    # VS Code debug config for MvtNet.Demo
│   ├── launch.json
│   └── tasks.json
└── MVT_INIT_PLAN.md
```

## Tech

- .NET 10
- Google.Protobuf for protobuf serialization
- [MVT spec v2.1](https://github.com/mapbox/vector-tile-spec) (extent 4096, Web Mercator, standard tile scheme — y-origin top-left, as used by Mapbox/MapLibre/OSM)
- Next.js (TypeScript) + MapLibre GL JS for the UI harness

## Sample Data

- Single point: **59.3281936, 18.0440866** (Stockholm)

---

## TODO — Ordered Checklist

### Phase 1 — Scaffold ✅
- [x] 1. Create `MvtNet.sln`
- [x] 2. Create `MvtNet/` class library with `Proto/vector_tile.proto` + protobuf code gen
- [x] 3. Create `MvtNet.Tests/` test project, reference MvtNet
- [x] 4. Create `MvtNet.Demo/` minimal API, reference MvtNet, stub `GET /tiles/{z}/{x}/{y}`
- [x] 5. Create `MvtNet.Demo.UI/` Next.js app
- [x] 6. Add MapLibre GL JS + free OSM raster base layer (`tile.openstreetmap.org`) for demo context
- [x] 7. Add custom MVT overlay layer pointed at Demo API
- [x] 8. Stub tiles return real MVT tile with X shape (encoded by MvtNet)
- [x] 9. Proxy `/tiles/*` → Demo API via Next.js rewrites
- [x] 10. Add `.vscode/launch.json` + `tasks.json` for debugging MvtNet.Demo
- [x] 11. Verify: map loads, all tiles hit backend, all render X shape

### Phase 2 — Core Encoding ✅
- [x] 12. Tile math — WGS84 → Web Mercator → tile coordinates (extent 4096)
- [x] 13. Geometry encoding — MoveTo, LineTo, ClosePath, zigzag, delta
- [x] 14. Tag encoding — key/value dictionaries, index pairs
- [x] 15. Protobuf tile assembly + serialization
- [x] 16. Wire sample point (59.3281936, 18.0440866) into Demo API
- [x] 17. Unit tests (tile math, zigzag, geometry commands, full roundtrip)
- [x] 18. Verify: point renders on Stockholm base map in UI

### Phase 3 — Public API + NuGet ✅
- [x] 19. Design clean public API surface
- [x] 20. Ensure no ASP.NET dependencies in MvtNet
- [x] 21. Add NuGet package metadata
- [x] 22. Verify: `dotnet pack` produces valid .nupkg

### Phase 4 — LineString Clipping ✅
- [x] 23. Cohen-Sutherland/Liang-Barsky line-segment clipping with buffer
- [x] 24. Integrate into `LayerBuilder.AddLineString()`
- [x] 25. Multi-segment splitting for lines crossing tile multiple times
- [x] 26. Unit tests + benchmarks
- [x] 27. Demo: clipped vs unclipped routes side by side

### Phase 5 — Tile-to-Geohash Query Helper ← **CURRENT**
- [x] 28. Implement `TileGeohash.GetPrefixes(z, x, y)`
- [x] 29. Zoom-to-precision mapping
- [x] 30. Geohash cell enumeration over tile bounding box
- [x] 31. `TileGeohash.GetRange(z, x, y)` for `BETWEEN` queries (same false-positive-discard pattern as Hilbert Index)
- [x] 32. Unit tests across zoom levels and latitudes
- [x] 33. Fake in-memory data store keyed by geohash, seeded with ~20-30 Stockholm POIs
- [ ] 33b. Performance validation: ensure query + encode pipeline handles 100,000 POIs in a Stockholm-sized area
- [x] 34. Demo API queries store via `TileGeohash.GetPrefixes()` → prefix matching
- [x] 35. UI renders POI overlay from geohash-queried data

---

## Phase 1 — Scaffold All Projects + End-to-End Pipeline

Stand up all three projects so the full pipeline works: UI → API → MvtNet library → MVT bytes → rendered overlay tile.

### Steps

1. Create `MvtNet.sln` at the root
2. Create `MvtNet/` class library (`net10.0`) with `Proto/vector_tile.proto` and protobuf code generation via `Google.Protobuf` + `Grpc.Tools`
3. Create `MvtNet.Demo/` minimal API (`net10.0`), references MvtNet, serves `GET /tiles/{z}/{x}/{y}`
4. Create `MvtNet.Demo.UI/` — Next.js (TypeScript) via `create-next-app`
5. Add MapLibre GL JS for the map component (standard setup for rendering MVT overlay tiles)
6. Standard base map (OpenStreetMap) as the background
7. Custom MVT **overlay layer** sourced from Demo API on top of the base map
8. Stub tiles return a real MVT tile encoded by MvtNet containing an X shape (two crossing line segments centered in the tile) — confirms the full encoding pipeline works
9. Next.js `rewrites` in `next.config.ts` to proxy `/tiles/*` → Demo API
10. ~~Sidebar/toolbar to toggle layers~~ — deferred to Phase 2; Phase 1 is POC only

### How to run

- `cd MvtNet.Demo.UI && npm run dev` → `http://localhost:3000`
- Demo API: `dotnet run --project MvtNet.Demo` → `http://localhost:5000`
- Tile requests proxied: `localhost:3000/tiles/{z}/{x}/{y}` → `localhost:5000/tiles/{z}/{x}/{y}`

### Done when

- Map loads with OpenStreetMap base, is pannable and zoomable
- Our custom overlay layer sits on top of the base map
- Every overlay tile request hits the backend (visible in network tab / server logs)
- All overlay tiles render an X shape — confirms end-to-end encoding pipeline works

---

## Decisions

- **X shape (Phase 1 stub):** Full tile extent (0,0 → 4096,4096), two LineString features in one layer
- **Demo API transport:** HTTP only for dev (`http://localhost:5000`)
- **Protobuf codegen:** Standard build-time generation via `Grpc.Tools` (`.proto` in project, C# generated on build)
- **Test framework:** NUnit
- **Next.js:** App Router (default), npm

## Notes

- **Clipping:** When features cross tile boundaries, they should be clipped to the tile extent (with a small buffer for clean rendering at edges). Not in scope for initial phases — to be addressed once basic encoding works.
- **Spatial filtering:** Bounding-box check for now. Spatial indexing (R-tree etc.) can be added later if performance requires it.
- **Geometry type order:** Point first (Phase 2), then LineString and Polygon. No strict order for the latter two — all will be implemented eventually.

---

## Phase 2 — MvtNet Core Encoding

Implement the actual MVT encoding in the library.

### Steps

1. Tile math — WGS84 (lat/lng) → Web Mercator → tile coordinates (z/x/y → extent 4096)
2. Geometry encoding — MoveTo, LineTo, ClosePath with zigzag + delta encoding
3. Tag encoding — key/value dictionary per layer, feature tags as index pairs
4. Protobuf tile assembly — layers, features, serialization to bytes
5. Hardcoded sample point in Demo API (59.3281936, 18.0440866)
6. Unit tests — validate encoding against known-good MVT bytes (tile math, zigzag, geometry commands, full tile roundtrip)

### Done when

- Demo API returns valid MVT tiles containing the sample point
- UI renders the point as an overlay on the Stockholm base map
- Tests pass

---

## Phase 3 — Library Public API + NuGet Packaging

Clean up the library surface for external consumers.

### Steps

1. Design clean public API for callers to build tiles from WGS84 features
2. Ensure MvtNet has no ASP.NET dependencies
3. NuGet package metadata (package id, description, license, etc.)
4. `dotnet pack` produces a valid .nupkg

### Done when

- MvtNet is a self-contained, documented library ready to publish

---

## Phase 4 — LineString Clipping

Clip LineStrings to the tile extent so only geometry inside (plus a small buffer) is encoded. Always on — callers pass WGS84 coordinates and the library handles the rest.

### Context

A long LineString (e.g. 3000 points over 200km) crossing a tile at high zoom currently projects and encodes all points. Most are far outside the visible area — wasted CPU and bytes. Every pan/zoom fires ~10 tile requests, each encoding full geometry.

### Steps

1. Implement Cohen-Sutherland/Liang-Barsky line-segment clipping against the tile rect (with ~5% buffer ≈ 200 units at extent 4096)
2. Integrate into `LayerBuilder.AddLineString()` — clip projected coordinates before encoding
3. Emit multiple LineString features when a line enters/leaves the tile multiple times (one per continuous segment inside the clip region)
4. Unit tests — clipping correctness, multi-segment splitting, buffer margin, edge cases (fully inside, fully outside, crossing)
5. Update Demo API + UI: two parallel 3000-point routes (Stockholm → Norrköping), red (clipped) vs blue (unclipped) for visual comparison
6. Benchmark before/after using `MvtNet.Benchmarks` (z14 worst case, z6 full route)

### Design notes

- **Buffer:** 5% of extent (~200 units) prevents visual pop-in at tile edges
- **Splitting:** Disconnected segments become separate LineString features in the same layer — renders seamlessly in MapLibre
- **Where:** Inside `LayerBuilder.AddLineString()`, caller API unchanged
- **Scope:** LineString only. Polygon clipping (Sutherland-Hodgman) deferred — fewer vertices, less urgent. Points are already skip-or-include.

### Done when

- LineStrings are clipped to tile extent + buffer automatically
- Multi-crossing lines produce correct separate segments
- Demo shows clipped vs unclipped routes side by side
- Benchmarks show improvement at high zoom
- Tests pass

---

## Phase 5 — Tile-to-Geohash Spatial Query Helper

Utility to translate tile coordinates (z/x/y) into geohash prefixes for efficient database queries. Lets callers with geohash-indexed data (MySQL, DynamoDB, etc.) select exactly the rows that overlap a tile — no full table scan, no PostGIS required.

### Why this matters

A geohash-indexed table is a common, portable pattern. The missing piece is knowing *which* geohash prefixes to query for a given tile. This utility bridges tile coordinates and geohash-based storage.

### Steps

1. Implement `TileGeohash.GetPrefixes(z, x, y)` → returns the set of geohash prefixes covering the tile's bounding box
2. Zoom-to-precision mapping — pick geohash precision so cells are smaller than the tile (yields manageable 4–16 prefixes per tile)
3. Enumerate all geohash cells at the chosen precision that overlap the tile's WGS84 bounding box
4. Optional: `TileGeohash.GetRange(z, x, y)` → returns min/max geohash for a single `BETWEEN` range query (faster but includes some false positives outside the tile — harmless since encoding filters them)
5. Unit tests in dedicated `TileGeohashTests.cs` — verify correct prefixes at various zoom levels and tile positions, edge cases at antimeridian and poles
6. In-memory fake data store for Demo API — `Dictionary<string, List<Poi>>` keyed by geohash, seeded with sample POIs around Stockholm
7. Demo API queries the store using `TileGeohash.GetPrefixes()` → `WHERE geohash LIKE 'prefix%'` pattern (iterate prefixes, collect matching entries)
8. UI renders POIs as point overlay — confirms the full flow: tile request → geohash prefix lookup → filtered query → MVT encoding → render

### Design notes

- **Geohash encoding from scratch:** No external dependency. Implement base32 geohash encode/decode in MvtNet. Stable algorithm, no reason to pull in a package.
- **No direct bit-mapping:** Tiles use Web Mercator, geohashes use equirectangular lat/lng — the grids don't align, especially at high latitudes. Bounding-box overlap enumeration is the correct approach.
- **Standalone utility, not baked into the encoding pipeline:** `TileGeohash` is a pure helper — caller gets prefixes, queries their DB, passes results to MvtNet for encoding. Two separate steps, no coupling.
- **Database-agnostic:** Returns prefixes/ranges as strings. The caller builds the SQL (`WHERE geohash LIKE 'u6sc%'` or `WHERE geohash BETWEEN 'u6sc' AND 'u6sg'`).
- **False positives are fine:** Features outside the tile but inside a matching geohash cell get projected and filtered out during encoding — this is already handled.
- **Low zoom = short prefixes:** At low zoom levels (z0–z5) geohash prefixes are 1–2 chars, potentially matching millions of rows. Callers should set a min zoom per layer (standard practice — no reason to render individual POIs at continent scale) and/or cap query results.
- **Zoom-to-precision cutoffs:** z4→2, z7→3, z9→4, z12→5, z14→6, z17→7. Tuned for 4–16 prefixes per tile.
- **Precision table (approximate):**

| Geohash precision | Cell size         | Typical zoom range |
|-------------------|-------------------|--------------------|
| 3                 | ~156km × 156km    | z4–z6              |
| 4                 | ~39km × 19.5km    | z6–z8              |
| 5                 | ~4.9km × 4.9km    | z8–z10             |
| 6                 | ~1.2km × 0.6km    | z10–z12            |
| 7                 | ~153m × 153m      | z12–z15            |
| 8                 | ~38m × 19m        | z15–z17            |

### Done when

- `TileGeohash.GetPrefixes(z, x, y)` returns correct geohash prefixes for any tile
- Demo API uses geohash prefix queries
- Tests pass across zoom levels and latitudes

---

## Phase 6 — TBD

_To be defined._
