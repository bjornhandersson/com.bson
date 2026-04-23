# REVIEW001 — MvtNet Library Fix Plan

Issues identified in code review. Ordered by priority: bugs first, then design, then minor.

---

## Revision log

| Rev | Date | Change |
|-----|------|--------|
| 1 | 2026-04-23 | Initial plan from code review |

---

## 1. Fix float tag value encoding (bug)

**File:** `TagEncoder.cs`

`float` values are promoted to `double` before storage, causing precision loss (e.g. `3.14f` → `3.140000104904175`). MVT spec has a dedicated `float_value` field. Store `float` in `FloatValue` rather than `DoubleValue`. Update the per-type deduplication dictionary accordingly (`float` key, not `double`).

Covers: separate dedup dict for float, separate `GetOrAddFloatValue` method, updated switch arm.

---

## 2. Fix wrong comment in GeometryEncoder (bug)

**File:** `GeometryEncoder.cs:33`

Comment says `LineTo(1) + (n-1) * (dx + dy)` but the code emits `LineTo(n-1)`. Fix the comment to match the actual encoding.

---

## 3. Fix or remove `LayerBuilder.Build()` shortcut (design)

**File:** `TileBuilder.cs`

`LayerBuilder.Build()` silently delegates to the parent tile's `Build()`, which serialises all layers — including any not yet fully populated. Options:

- **Remove** the shortcut and require callers to use `TileBuilder.Build()` directly.
- **Rename** to `BuildTile()` to make the scope clear.

Do not keep the current name — it implies single-layer scope.

---

## 4. Fix or remove `TileGeohash.GetRange` (design)

**File:** `TileGeohash.cs`

The BETWEEN range derived from SW and NE geohash corners is Z-curve ordered, not spatially monotonic. At low zoom the range spans most of the Earth. Options:

- **Remove the method** — `GetPrefixes` is always safer and the performance difference is marginal.
- **Restrict it to z ≥ 12** where the false positive set is small, and throw for lower zooms.
- **Improve it** by computing an envelope from all four corners and using the min/max of those four hashes.

Lean toward removal unless there is a known caller that needs it.

---

## 5. Add z/x/y validation to `TileBuilder` (minor)

**File:** `TileBuilder.cs`

Invalid tile addresses (e.g. `x >= 2^z`) produce bad projection silently. Add a guard in the constructor:

- `z` in `[0, 30]`
- `x` in `[0, 2^z - 1]`
- `y` in `[0, 2^z - 1]`

Throw `ArgumentOutOfRangeException` with a descriptive message.

---

## 6. Cache Mercator Y in `TileMath.ProjectWithBounds` (minor)

**File:** `TileMath.cs`

The private `ProjectWithBounds` method re-derives `LatToMercatorY(bounds.North)` and `LatToMercatorY(bounds.South)` on every call. The public `ProjectPoint` and `ProjectPointUnclamped` both go through this path. Refactor them to create and use a `TileProjectionContext` internally, consistent with how `LayerBuilder` works.

---

## 7. Document `OverlapsTile` as an optimistic bbox filter (minor)

**File:** `TileBuilder.cs`

Add a single comment clarifying that `OverlapsTile` is a bbox pre-filter, not an exact intersection test. The clipper handles exact exclusion downstream. No behaviour change.

---

## Out of scope for this plan

- **Polygon inner rings / holes** — requires a new API surface and winding-order enforcement; track separately.
- **`uint64` tag values** — no known need; skip for now.
