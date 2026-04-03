# LineString Clipping — Design

## Problem

When a long LineString (e.g. 3000 points over 200km) crosses a tile at high zoom, the current code projects and encodes all 3000 points into that tile. Most points are far outside the visible area — wasted CPU and wasted bytes on the wire.

Every pan/zoom fires ~10 tile requests. Each one encodes the full geometry. This adds up fast.

## Decision: always-on clipping

Clipping is always on. No opt-in flag. The caller should not have to care — they pass WGS84 coordinates and the library handles the rest.

A consumer who pre-clips their data will get double-clipped, which is harmless (just a no-op on already-clipped geometry).

## Scope

LineString clipping first. Polygon clipping (Sutherland-Hodgman) later — it's more complex and less urgent since polygons tend to have fewer vertices than long routes.

Points don't need clipping — they're already skip-or-include.

## How it works

Before encoding a LineString, clip it to the tile extent plus a small buffer margin. The buffer prevents visual pop-in/pop-out at tile edges.

### Buffer

Clip at the tile boundary with a margin (e.g. 5% of extent ≈ 200 units at extent 4096). This gives clean rendering at tile edges without encoding geometry far outside the visible area.

### Splitting

When a line enters and leaves a tile multiple times (e.g. a zigzag route), emit multiple separate LineString features — one per segment inside the clip region.

Reason: a single MVT LineString must be continuous. Disconnected segments aren't valid. Multiple features in the same layer render identically in MapLibre, so it looks seamless to the viewer.

### Where it lives

Inside `LayerBuilder.AddLineString()`. The caller's API doesn't change.

Flow:
1. Caller passes WGS84 coordinates
2. Check bounding box overlap with tile (existing)
3. Project all points to tile-local coordinates (existing)
4. **NEW:** Clip the projected line to tile extent + buffer
5. Emit one LineString feature per clipped segment
6. Encode and add to layer

## Algorithm

Cohen-Sutherland or Liang-Barsky style line-segment clipping against a rectangular region (0 - buffer, 0 - buffer) to (extent + buffer, extent + buffer).

Walk the projected coordinates segment by segment:
- If both endpoints inside: keep the segment
- If one inside, one outside: clip at the boundary, emit up to the intersection
- If both outside but line crosses the region: clip both ends
- If both outside and line doesn't cross: skip

Collect consecutive inside/clipped segments into runs. Each run becomes a separate LineString feature.

## Demo

Update `MvtNet.Demo.Api` and `MvtNet.Demo.UI` to show two parallel 3000-point routes (~200km, Stockholm → Norrköping):

- **Red route** — with clipping (the new behavior)
- **Blue route** — without clipping (current behavior, using a raw unclipped layer for comparison)

The two routes are offset by a small distance so they're visually distinguishable side by side. This lets you compare tile sizes in the network tab and verify they render identically.

## Benchmark

The `MvtNet.Benchmarks` project already has:
- `LineString (3000 pts, 200km, zoomed in)` — z14 tile, worst case
- `LineString (3000 pts, 200km, zoomed out)` — z6 tile, full route

Run before and after clipping to measure the improvement.
