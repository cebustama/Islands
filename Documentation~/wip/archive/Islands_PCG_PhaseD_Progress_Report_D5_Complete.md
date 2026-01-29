# Islands.PCG — Phase D Progress Report (D0–D5)

Date: 2026-01-28  
Scope: Phase D (first dungeon strategy in pure grids) — **D0 + D1 + D2 + D3 + D4 + D5 completed (Phase D complete)**

---

## Phase D goal (context)

Port the first dungeon strategy from the legacy tilemap pipeline into **pure grid operations**:
- Core algorithms operate on `MaskGrid2D` / `GridDomain2D` only (no Tilemaps).
- Determinism is guaranteed by seed-driven `Unity.Mathematics.Random`.
- Outputs are validated via:
  1) **Lantern** visual inspection (fast human sanity checks),
  2) **EditMode tests**,
  3) **SnapshotHash64** for stable regression/snapshot checks.

---

## D0 — RandomWalk behavioral contract locked (DONE)

### Goal
Remove ambiguity before implementation so later parity + determinism checks are meaningful.

### Final locked contract (normative)
**IteratedRandomWalk2D** operates on a `MaskGrid2D` in a `GridDomain2D`. Given a seed and a `RandomWalkConfig2D`, it executes `iterations` walks. Each walk advances exactly `walkLength` steps, choosing **cardinal directions** with bias (`skewX`, `skewY`). Each visited cell is marked as floor (true); an optional `brushRadius` exists (defaults to 0). If a proposed step goes out of bounds, apply **Bounce**: re-pick a direction up to `maxRetries`; if no valid step is found, apply `fallback = StopEarly` and terminate that walk. Between walks, with probability `randomStartChance`, the start position is selected **uniformly** among existing floor cells; otherwise the walk continues from the last position. With the same seed + same config + same domain, the result is deterministic.

### Decisions explicitly chosen
- Neighborhood: **Cardinal (4-dir)** (8-dir deferred)
- OOB policy: **Bounce**, `maxRetries=8`, `fallback=StopEarly`
- Restart policy: random restart samples **uniformly among ON cells**
- Brush policy: `brushRadius=0` by default (stamps allowed later)

### D0 acceptance (done checks)
- Rules are explainable in one paragraph (above).
- All behavior is representable as config fields (no hidden logic).
- Determinism statement is explicit.

---

## D1 — Direction picking (deterministic) (DONE)

### Goal
Replicate legacy `skewX/skewY` semantics **without** `UnityEngine.Random`, using seed-driven `Unity.Mathematics.Random`.

### What was implemented
**`Direction2D`** provides:
- `Cardinal` directions (order: +X, -X, +Y, -Y)
- `PickCardinal(ref Random rng)`
- `PickSkewedCardinal(ref Random rng, float skewX, float skewY)`

**`PickSkewedCardinal` semantics**
1) Axis choice is 50/50 (horizontal vs vertical) using `rng.NextFloat() < 0.5f`.  
2) Sign choice is biased by:
   - `rightChance = 0.5 + 0.5*skewX`
   - `upChance    = 0.5 + 0.5*skewY`
   with `skewX/skewY` clamped to [-1, +1] for robustness.

### Tests added (EditMode)
Determinism + bias sanity tests:
- Same seed ⇒ same first N directions
- Positive skewX favors right; negative skewY favors down

### Files added/modified
- `Runtime/PCG/Layout/Direction2D.cs`
- `Tests/EditMode/Islands.PCG.Tests.EditMode.asmdef`
- `Tests/EditMode/Direction2DTests.cs`

### D1 acceptance (done checks)
- Same seed ⇒ same first N directions (determinism gate).
- Skew produces the expected directional bias (sanity gate).
- No dependency on `UnityEngine.Random` in the direction picker.

---

## D2 — SimpleRandomWalk “carver” that writes into MaskGrid2D (DONE)

### Goal
Get a **single** deterministic walk producing a visible “drunk line” mask, **pure grids only** (no Tilemaps),
and hook it into the existing Lantern visualization path.

### What was implemented

#### 1) Mask raster stamping primitive (disc brush)
Added `MaskRasterOps2D.StampDisc(ref MaskGrid2D dst, int cx, int cy, int radius, bool value=true)`:
- radius 0 ⇒ single cell set (still bounds-safe)
- radius > 0 ⇒ bounding box scan and `dx²+dy² <= r²` fill
- out-of-bounds writes are skipped/clipped (safe operator)

#### 2) SimpleRandomWalk2D core algorithm
Added `SimpleRandomWalk2D.Walk(...)`:
- Inputs: `start`, `walkLength`, `brushRadius`, `skewX/skewY`, `maxRetries`
- Behavior:
  - carve start cell
  - for each step: pick biased cardinal dir → **Bounce** retries → **StopEarly** fallback
  - carving uses `SetUnchecked` for radius 0, or `StampDisc` for radius > 0
- Determinism: **only** `Unity.Mathematics.Random` (seed-driven)

#### 3) Lantern wiring (PCGMaskVisualization mode)
`PCGMaskVisualization` gained `SourceMode.SimpleRandomWalkMask` + inspector params + dirty tracking.
It allocates/reuses `MaskGrid2D`, runs the walk on parameter changes, then packs + uploads for instanced rendering.

### D2 acceptance (done checks)
- Lantern shows a continuous-ish “trail” starting at `walkStart`
- Increasing `walkLength` increases (or maintains) `mask.CountOnes()` (sanity metric)
- Same seed + same params + same resolution ⇒ identical output (determinism)

### Files added/modified (D2)
- **Added:** `Runtime/PCG/Operators/MaskRasterOps2D.cs` (initially `StampDisc`)
- **Added:** `Runtime/PCG/Layout/SimpleRandomWalk2D.cs`
- **Modified:** `Runtime/PCG/Samples/PCGMaskVisualization.cs` (new mode + wiring + dirty tracking)
- **Added:** `Tests/EditMode/SimpleRandomWalk2DTests.cs`

---

## D3 — IteratedRandomWalk2D (the actual strategy) (DONE)

### Goal
Match the legacy dungeon concept: **multiple walks**, optional restart from **existing floor**, accumulating into one `MaskGrid2D`.

### What was implemented

#### 1) Iterated strategy
Added `IteratedRandomWalk2D.Carve(...)`:
- Loops `i in 0..iterations-1`
- Picks `walkLength`:
  - if `walkLengthMin == walkLengthMax`, uses fixed length (**no RNG consumed**)
  - else samples `rng.NextInt(min, max+1)` (inclusive max)
- Picks `start` per-iteration:
  - `i==0`: always uses provided `start` (**no restart RNG roll**)
  - `i>0`: if `randomStartChance > 0` and there are ON cells, rolls `rng.NextFloat() < randomStartChance`
    - if true: picks a random ON cell uniformly
    - else: continues from previous `end`
- Runs `SimpleRandomWalk2D.Walk(...)`, accumulating into `dst`
- Returns the final `end` position

**Parity rule (important):**
When `iterations=1` and `walkLengthMin==walkLengthMax` and `randomStartChance==0`,
the method avoids consuming extra RNG so the internal direction sequence aligns with D2 behavior.

#### 2) “Pick random ON cell” helper on MaskGrid2D
Added `MaskGrid2D.TryGetRandomSetBit(ref Random rng, out int2 cell)`:
- Uniform selection among set bits using popcount + k-th set bit selection.

#### 3) Snapshot hash promoted to runtime
Added `MaskGrid2D.SnapshotHash64(...)` as a runtime API (word-wise FNV-1a 64-bit),
to support fast determinism/regression checks without iterating all cells.

Implementation detail: `MaskGrid2D` was made `partial` and the hash lives in `MaskGrid2D.Hash.cs`
to keep the main file from ballooning.

#### 4) Lantern wiring (IteratedRandomWalkMask)
`PCGMaskVisualization` gained `SourceMode.IteratedRandomWalkMask` + inspector params + dirty tracking:
- `walkIterations`, `walkLengthMin`, `walkLengthMax`, `walkRandomStartChance`
- shares the same seed/start/brush/skew/maxRetries/clear flags
- clamps inputs to safe ranges before carving

### D3 acceptance (done checks)
- `iterations=1`, `walkLengthMin==walkLengthMax`, `randomStartChance=0` matches D2 behavior.
- Increasing `iterations` increases density/spread (qualitative check in Lantern).
- Same seed + same config + same resolution ⇒ identical output (SnapshotHash64 stable).

### Tests added (EditMode)
- `IteratedRandomWalk2DTests`:
  - Same seed + same config ⇒ identical hash/mask
  - `iterations=1` parity sanity (no extra RNG consumption for parity configuration)
  - `iterations > 1` monotonic-ish sanity (`CountOnes()` does not decrease)
- `MaskGrid2DRandomSetBitTests`:
  - determinism of picking with fixed seed
  - returned cells are always ON

### Files added/modified (D3)
- **Added:** `Runtime/PCG/Layout/IteratedRandomWalk2D.cs`
- **Modified:** `Runtime/PCG/Grids/MaskGrid2D.cs` (made `partial`, added `TryGetRandomSetBit`)
- **Added:** `Runtime/PCG/Grids/MaskGrid2D.Hash.cs` (adds `SnapshotHash64`)
- **Modified:** `Runtime/PCG/Samples/PCGMaskVisualization.cs` (new mode + wiring + dirty tracking)
- **Added:** `Tests/EditMode/IteratedRandomWalk2DTests.cs`
- **Added:** `Tests/EditMode/MaskGrid2DRandomSetBitTests.cs`

---

## D4 — Raster room/shape primitives (grid-only) (DONE)

### Goal
Cover the legacy `ShapeAlgorithms` overlap used by dungeon strategies (corridors/rooms) by providing
a **minimal, deterministic, safe** raster-shape API operating directly on `MaskGrid2D`.

---

### D4.0 — Raster Shapes Contract locked (DONE)

#### Final locked contract (normative)
All raster shape writers in `Islands.PCG` operate on `MaskGrid2D` deterministically and in a “safe operator” style.

- **Endpoint-inclusive:** `DrawLine` always carves both A and B when in-bounds.
- **Brush semantics (forward-compatible):** `brushRadius` means “disc stamp per line point”.
  - `brushRadius == 0` ⇒ stamp a single cell.
  - `brushRadius > 0` ⇒ stamp a filled disc of that radius per line raster point.
  - Future: keep the same traversal semantics, swap the per-point stamp primitive.
- **OOB policy:** operations never throw due to OOB coordinates; writes outside the domain are skipped/clipped.

---

### D4.1 — Add `DrawLine` to `MaskRasterOps2D` (DONE)

#### What was implemented
Added:

- `MaskRasterOps2D.DrawLine(ref MaskGrid2D dst, int2 a, int2 b, int brushRadius = 0, bool value = true)`

Implementation:
- Bresenham integer rasterizer (single-loop, all octants).
- Endpoint-inclusive by stamping before the break condition.
- Safe operator:
  - `brushRadius==0` writes only if point is in bounds.
  - `brushRadius>0` uses `StampDisc` which clamps internally.
- Throws only for invalid API usage (`brushRadius < 0`).

---

### D4.2 — EditMode tests for line correctness + determinism (DONE)

#### What was added
Created `DrawLineTests.cs` with these checks:

1) **Endpoint inclusion**
- Draw from (2,2) to (10,2), radius 0.
- Assert both endpoints are ON.

2) **Reversal invariance**
- Draw A→B into mask1 and B→A into mask2.
- Assert `SnapshotHash64()` equal for both.

3) **Axis-aligned count sanity**
- For (2,2)→(10,2), radius 0, assert ones count equals `abs(10-2)+1`.

4) **Brush growth**
- Same line with brushRadius=2 yields `CountOnes()` strictly greater than brushRadius=0 version.

---

### D4.3 — Lantern visual modes for raster shapes (DONE)

#### What was implemented
Updated `PCGMaskVisualization`:
- Added two `SourceMode`s:
  - `RasterDiscMask`
  - `RasterLineMask`
- Added inspector parameters:
  - Disc: `discCenter (Vector2Int)`, `discRadius`
  - Line: `lineA (Vector2Int)`, `lineB (Vector2Int)`, `lineBrushRadius`
- In `UpdateVisualization`:
  - `mask.Clear()`
  - `RasterDiscMask` ⇒ `MaskRasterOps2D.StampDisc(...)`
  - `RasterLineMask` ⇒ `MaskRasterOps2D.DrawLine(...)`
  - Reuses the existing pack + upload path
- Added mode-specific dirty tracking (`rasterDirty`) so it only recomputes when inputs change.

#### Visual acceptance checks (passed)
- Disc looks symmetric; r=1 looks “plus-like”, r=2 looks round-ish.
- Diagonal line shows no holes; brush makes continuous corridor.
- Same inputs produce the same image across runs.

---

### D4.5 — D4 “Completion Gate” (PASSED)

D4 is complete when all of the following are true:

1) `StampDisc` + `DrawLine` exist and are safe for OOB inputs  
   ✅ Implemented safe operator behavior (skip/clamp), no OOB throws.

2) EditMode tests cover endpoint inclusion + reversal invariance + brush growth + stable hash  
   ✅ `DrawLineTests.cs` implements all four checks.

3) Lantern can display a disc and a line (with brush) and correctness is immediately visible  
   ✅ `PCGMaskVisualization` includes `RasterDiscMask` and `RasterLineMask`, with inspector params and live updates.

---



---

## D5 — Rooms + corridors composition (grid-only) (DONE)

### Goal
Build a **minimal, stable “room + corridor” composer** operating purely on `MaskGrid2D` so we can:
- validate a second, more “dungeon-like” pipeline slice in Lantern,
- add deterministic regression gates (SnapshotHash64),
- keep all logic Tilemap-free and data-oriented.

This is intentionally a “first slice” (placement-order corridor chaining) that becomes the foundation for more advanced strategies later.

### What was implemented

#### 1) Grid-only runtime composer
Added `RoomsCorridorsComposer2D` with:

- `RoomsCorridorsConfig` (struct) defining:
  - `roomCount`
  - `roomSizeMin`, `roomSizeMax` (inclusive width/height ranges)
  - `placementAttemptsPerRoom`
  - `roomPadding`
  - `corridorBrushRadius`
  - `clearBeforeGenerate`
  - `allowOverlap`

- `RoomsCorridorsComposer2D.Generate(...)`:
  - Clears the mask if configured.
  - Places up to `roomCount` rooms:
    - Samples size and center within padded domain margins.
    - Converts to rect bounds and stamps using `RectFillGenerator.FillRect(...)`.
    - Stores each placed room center in `outRoomCenters[i]`.
    - If `allowOverlap == false`, performs a cheap “area empty” check before stamping.
  - Connects rooms in **placement order**:
    - For `i = 1..placedRooms-1`, connect `centers[i-1] -> centers[i]` with
      `MaskRasterOps2D.DrawLine(..., brushRadius: cfg.corridorBrushRadius)`.

Determinism is guaranteed because all sampling uses `Unity.Mathematics.Random` passed by ref.

#### 2) Lantern wiring (visual validation path)
Updated `PCGMaskVisualization`:
- Added `SourceMode.RoomsCorridorsMask`.
- Added inspector parameters (seed, room count, size min/max, attempts, padding, corridor radius, clear toggle).
- Added dirty tracking for those parameters.
- In the Rooms+Corridors case:
  - Creates `Random rng = new Random((uint)math.max(seed, 1));`
  - Allocates `NativeArray<int2> centers` (Temp) sized to `roomCount`.
  - Calls `RoomsCorridorsComposer2D.Generate(...)` and captures `placedRooms`.
  - Packs + uploads via the existing trusted “Lantern” path.

(Optional debug) `placedRooms` can be logged to immediately see when constraints are preventing placement.

#### 3) EditMode tests (determinism + regression gates)
Created `RoomsCorridorsComposer2DTests.cs` with:
1) **Same seed/config ⇒ same hash** (two masks, identical inputs, `SnapshotHash64()` equal).
2) **Different seed ⇒ different hash** (sanity; implemented robustly by checking multiple seeds).
3) **Golden hash gate**: fixed config+seed with an expected constant hash locked in, so any behavioral drift is caught immediately.

### D5 acceptance (done checks)
- Lantern:
  - Changing `roomsSeed` changes layout.
  - Same seed/config is stable across runs.
  - `roomsRoomCount` increases visible room count (within placement constraints).
- Tests:
  - Determinism tests pass consistently.
  - Golden hash gate is locked and green.

### Files added/modified (D5)
- **Added:** `Runtime/PCG/Layout/RoomsCorridorsComposer2D.cs`
- **Added:** `Tests/EditMode/RoomsCorridorsComposer2DTests.cs`
- **Modified:** `Runtime/PCG/Samples/PCGMaskVisualization.cs` (new mode + params + dirty tracking + generation call)


## Phase D status summary

✅ **D0–D5 complete (Phase D complete)**: We now have:
- A fully working **grid-only random walk dungeon strategy** (simple + iterated),
- Minimal **raster primitives** required by dungeon ports (`StampDisc`, `DrawLine`, rect fill reuse),
- A grid-only **Rooms+Corridors composer** (`RoomsCorridorsComposer2D`) to generate room layouts and corridor connections,
- Determinism + regression gates via `SnapshotHash64` (including golden hash tests),
- Lantern visualization modes covering random walks, raster shapes, and rooms+corridors.

---

## What’s next (Phase E — Port remaining dungeon strategies)

Phase E goal: port the remaining legacy dungeon strategies into **pure grids**, reusing the Phase D toolbox (fields/grids, raster ops, deterministic RNG, snapshot hashing, Lantern modes).

Recommended Phase E loop per strategy:
1) **Runtime port**: add/port the core algorithm to operate on `MaskGrid2D` (and `ScalarField2D`/`Sdf2D` only if needed).
2) **Lantern mode**: add a `SourceMode` + inspector params + dirty tracking to visualize it immediately.
3) **Tests**:
   - Same seed/config ⇒ same hash
   - Different seed ⇒ different hash (sanity)
   - Golden hash gate for one representative config
4) **Parity notes**: document any intentional differences vs the legacy tilemap strategy.

Suggested ordering (pragmatic):
- Strategies that are already “mask-first” (carving / stamping), because they map directly to `MaskGrid2D`.
- Then strategies that need a post-process (morphology / wall bitmasks), which will be Phase F.

After Phase E:
- **Phase F — Morphology + walls bitmasks** (engine-grade post-process): dilate/erode/open/close and neighbor bitmask wall classification + LUT mapping to tile IDs.

---

## Appendix — Key files touched across Phase D

Runtime:
- `Runtime/PCG/Operators/MaskRasterOps2D.cs` (StampDisc, DrawLine)
- `Runtime/PCG/Layout/Direction2D.cs`
- `Runtime/PCG/Layout/SimpleRandomWalk2D.cs`
- `Runtime/PCG/Layout/IteratedRandomWalk2D.cs`
- `Runtime/PCG/Layout/RoomsCorridorsComposer2D.cs`
- `Runtime/PCG/Grids/MaskGrid2D.cs` (+ partial + random set-bit helper)
- `Runtime/PCG/Grids/MaskGrid2D.Hash.cs` (SnapshotHash64)
- `Runtime/PCG/Samples/PCGMaskVisualization.cs` (Lantern modes: D2/D3/D4/D5)

Tests (EditMode):
- `Direction2DTests.cs`
- `SimpleRandomWalk2DTests.cs`
- `IteratedRandomWalk2DTests.cs`
- `MaskGrid2DRandomSetBitTests.cs`
- `DrawLineTests.cs` (D4.2)
- `RoomsCorridorsComposer2DTests.cs` (D5)
