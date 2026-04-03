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

### Phase 1 — Scaffold
- [ ] 1. Create `MvtNet.sln`
- [ ] 2. Create `MvtNet/` class library with `Proto/vector_tile.proto` + protobuf code gen
- [ ] 3. Create `MvtNet.Tests/` test project, reference MvtNet
- [ ] 4. Create `MvtNet.Demo/` minimal API, reference MvtNet, stub `GET /tiles/{z}/{x}/{y}`
- [ ] 5. Create `MvtNet.Demo.UI/` Next.js app
- [ ] 6. Add MapLibre GL JS + free OSM raster base layer (`tile.openstreetmap.org`) for demo context
- [ ] 7. Add custom MVT overlay layer pointed at Demo API
- [ ] 8. Stub tiles return real MVT tile with X shape (encoded by MvtNet)
- [ ] 9. Proxy `/tiles/*` → Demo API via Next.js rewrites
- [ ] 10. Add `.vscode/launch.json` + `tasks.json` for debugging MvtNet.Demo
- [ ] 11. Verify: map loads, all tiles hit backend, all render X shape

### Phase 2 — Core Encoding
- [ ] 11. Tile math — WGS84 → Web Mercator → tile coordinates (extent 4096)
- [ ] 12. Geometry encoding — MoveTo, LineTo, ClosePath, zigzag, delta
- [ ] 13. Tag encoding — key/value dictionaries, index pairs
- [ ] 14. Protobuf tile assembly + serialization
- [ ] 15. Wire sample point (59.3281936, 18.0440866) into Demo API
- [ ] 16. Unit tests (tile math, zigzag, geometry commands, full roundtrip)
- [ ] 17. Verify: point renders on Stockholm base map in UI

### Phase 3 — Public API + NuGet
- [ ] 18. Design clean public API surface
- [ ] 19. Ensure no ASP.NET dependencies in MvtNet
- [ ] 20. Add NuGet package metadata
- [ ] 21. Verify: `dotnet pack` produces valid .nupkg

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

## Phase 4 — TBD

_To be defined._
