# Phase E Planning Report — Port remaining dungeon strategies (Grid-only)

**Project:** Islands-style PCG Toolkit (Burst/SIMD-ready, deterministic)  
**Phase:** E (Next)  
**Date:** 2026-01-29  
**Prepared for:** Continuation after Phase D completion

---

## 0) Phase E Goal

Port the remaining legacy dungeon *strategies* into **pure grid writers** that operate on:

- `MaskGrid2D` (primary mask output; 0/1)
- deterministic RNG (`Unity.Mathematics.Random`, seed-driven)
- **no Tilemaps / no HashSets** in core generation (adapters are outputs only)

Each strategy port follows the same reliability pattern:

1) **Runtime**: new strategy class with `Config` + deterministic entrypoint (`Generate`/`Carve`)  
2) **Lantern**: new `SourceMode` for fast human visual validation  
3) **Tests**: EditMode determinism + regression gates (`SnapshotHash64()` + golden hash)

---

## 1) Global Acceptance Criteria (applies to every strategy)

A strategy is “ported” when **all** are true:

### Runtime
- A single public entrypoint exists:
  - `public static void Generate(ref MaskGrid2D mask, ref Random rng, in Config cfg, ...)`
- No exceptions on out-of-bounds writes (safe-operator style)
- No non-deterministic sources (no `Guid.NewGuid`, no `DateTime`, etc.)

### Lantern
- `PCGMaskVisualization` has a `SourceMode.<Strategy>`
- Inspector exposes the strategy’s minimal config + seed
- Same inputs reproduce **the exact same image** across runs

### Tests
- Same seed/config ⇒ same `SnapshotHash64()`
- Different seed ⇒ typically different hash (sanity; not logically guaranteed)
- Golden hash gate exists and is locked (recommended)

---

## 2) Phase E Modules & File Actions Overview

### New (Runtime)
- `Runtime/PCG/Layout/LayoutSeedUtil.cs`
- `Runtime/PCG/Layout/MaskNeighborOps2D.cs`
- `Runtime/PCG/Layout/CorridorFirstDungeon2D.cs`
- `Runtime/PCG/Layout/Bsp/BspPartition2D.cs`
- `Runtime/PCG/Layout/RoomFirstBspDungeon2D.cs`
- `Runtime/PCG/Layout/RoomGridDungeon2D.cs`
- (Optional) `Runtime/PCG/Layout/PointConnectors2D.cs`

### Modify (Lantern)
- `Runtime/PCG/Samples/PCGMaskVisualization.cs`
  - Add `SourceMode` entries
  - Add inspector config blocks + dirty tracking
  - Add switch cases to call each new strategy

### New (Tests)
- `Tests/EditMode/CorridorFirstDungeon2DTests.cs`
- `Tests/EditMode/RoomFirstBspDungeon2DTests.cs`
- `Tests/EditMode/RoomGridDungeon2DTests.cs`
- (Optional but recommended) `Tests/EditMode/DungeonSeedSetSnapshotTests.cs`

---

## 3) E0 — Shared utilities (small, high ROI)

### Objective
Standardize seed handling + neighbor/dead-end logic to speed up E1–E3 and keep consistent behavior.

### New file: `LayoutSeedUtil.cs`
**Responsibility:** Create deterministic RNG from int seed using your convention.

**API**
- `public static Random CreateRng(int seed)`
  - clamps seed to `>= 1`

### New file: `MaskNeighborOps2D.cs`
**Responsibility:** OOB-safe neighbor queries (4-neighborhood) for dead-ends, corridor logic, etc.

**APIs**
- `public static int CountCardinalOn(in MaskGrid2D mask, int2 p)`
- `public static bool IsDeadEnd4(in MaskGrid2D mask, int2 p)`

### Optional file: `PointConnectors2D.cs`
**Responsibility:** Corridor connectors (Manhattan L, direct line) using existing raster ops.

**APIs**
- `ConnectL(ref MaskGrid2D mask, int2 a, int2 b, int brushRadius, bool value=true)`  
- `ConnectLine(ref MaskGrid2D mask, int2 a, int2 b, int brushRadius, bool value=true)`

### Done when
- Utilities compile and at least one strategy uses them.
- (Optional) small unit tests exist for dead-end detection.

---

## 4) E1 — Corridor First (grid-only)

### Legacy intent
Carve corridors first, then place rooms at corridor endpoints / dead ends.
Important determinism fix: replace any `Guid.NewGuid` shuffle with seeded Fisher–Yates.

### New file: `CorridorFirstDungeon2D.cs`

#### Config
`public struct CorridorFirstConfig`
- `int corridorCount`
- `int corridorLengthMin, corridorLengthMax`
- `int corridorBrushRadius`
- room placement knobs:
  - `int roomSpawnCount` **or** `float roomSpawnChance`
  - `int2 roomSizeMin, roomSizeMax` (if stamping rect rooms)
  - or `int roomWalkLengthMin, roomWalkLengthMax` (if using RW rooms)
- `bool clearBeforeGenerate`

#### API
`public static void Generate(ref MaskGrid2D mask, ref Random rng, in CorridorFirstConfig cfg, NativeArray<int2> outRoomCenters, out int placedRooms)`

#### Algorithm (minimal stable slice)
1) If `clearBeforeGenerate` ⇒ `mask.Clear()`
2) Corridor loop (`corridorCount`):
   - pick start point deterministically (e.g., random in margins)
   - step a random direction and carve length range
   - carve by stamping (disc) each step OR draw segments with `DrawLine`
3) Collect candidate room points:
   - corridor ends and/or turning points
4) Place rooms on candidates:
   - deterministic selection order or seeded shuffle (Fisher–Yates)
   - carve room (rect stamp or RW blob)
5) Dead-end pass:
   - scan for dead ends (`IsDeadEnd4`) and carve missing rooms

### Lantern changes
Modify `PCGMaskVisualization.cs`:
- Add `SourceMode.CorridorFirstMask`
- Add inspector params for the config + seed + dirty tracking
- Add switch case calling `CorridorFirstDungeon2D.Generate(...)`

### Tests
New file: `CorridorFirstDungeon2DTests.cs`
- same seed/config ⇒ same hash
- different seed sanity
- golden gate

### Requirements fulfilled
- Strategy works without Tilemaps
- Deterministic (seeded shuffle, no GUID)
- Validated via Lantern + SnapshotHash gates

---

## 5) E2 — Room First (BSP) (grid-only)

### Legacy intent
Partition domain (BSP), stamp rooms in leaves, connect room centers with corridors.

### New file(s)
- `Runtime/PCG/Layout/Bsp/BspPartition2D.cs`
- `Runtime/PCG/Layout/RoomFirstBspDungeon2D.cs`

#### BSP partitioning
**Responsibility:** deterministic splitting of an integer rect into leaf rects.
Output should be a leaf list (e.g., `NativeList<IntRect>` or `NativeArray<IntRect>`).

#### Config
`public struct RoomFirstBspConfig`
- `int splitIterations` (or `int maxLeafCount`)
- `int2 minLeafSize`
- `int roomPadding`
- `int corridorBrushRadius`
- `bool connectWithManhattan`
- `bool clearBeforeGenerate`

#### API
`public static void Generate(ref MaskGrid2D mask, ref Random rng, in RoomFirstBspConfig cfg, NativeArray<int2> outRoomCenters, out int placedRooms)`

#### Algorithm (minimal stable slice)
1) Optional `mask.Clear()`
2) BSP split (deterministic via `rng`)
3) For each leaf:
   - compute room rect (leaf shrunk by padding)
   - stamp room (`RectFillGenerator.FillRect`)
   - record center
4) Connect centers:
   - simplest: connect in placement order
   - better: connect nearest-neighbor chain
   - corridor: Manhattan L (2 DrawLine calls) or 1 direct DrawLine

### Lantern changes
- Add `SourceMode.RoomFirstBspMask`
- Add inspector config + dirty tracking
- Add generation case

### Tests
New file: `RoomFirstBspDungeon2DTests.cs`
- same seed/config ⇒ same hash
- golden gate

### Requirements fulfilled
- BSP room partition strategy ported to MaskGrid2D
- Corridors carved with raster ops you trust
- Deterministic + gated

---

## 6) E3 — Room Grid (layout-only minimal slice)

### Legacy intent
A “grid of rooms” driven by a coordinate-path (often random-walk-like), then connect rooms.

### New file
- `Runtime/PCG/Layout/RoomGridDungeon2D.cs`

#### Config
`public struct RoomGridConfig`
- `int roomCount`
- `int2 cellStride` (spacing between centers in mask coords)
- `int2 roomSizeMin, roomSizeMax`
- `int corridorBrushRadius`
- `bool clearBeforeGenerate`
- (optional) `int placementAttemptsPerRoom` (if non-overlap on mask is desired)

#### API
`public static void Generate(ref MaskGrid2D mask, ref Random rng, in RoomGridConfig cfg, NativeArray<int2> outRoomCenters, out int placedRooms)`

#### Algorithm (minimal stable slice)
1) Optional `mask.Clear()`
2) Build a coarse coord path deterministically (room-to-room stepping)
3) Convert coords → mask-space centers (using stride)
4) Stamp rooms (rect)
5) Connect consecutive centers (`DrawLine` or Manhattan L)

#### Metadata (explicitly deferred)
If later you want parity with a room manager / door graph, add optional outputs:
- `NativeList<RoomNode2D>` (bounds + center)
- `NativeList<RoomEdge2D>` (connectivity, corridor endpoints)

### Lantern changes
- Add `SourceMode.RoomGridMask` + config + case

### Tests
New file: `RoomGridDungeon2DTests.cs`
- same seed/config ⇒ same hash
- golden gate

### Requirements fulfilled
- Strategy ported at the layout level with a clean grid-only core
- Deterministic and debuggable

---

## 7) E4 — Seed-set regression suite (recommended once E1–E3 exist)

### Objective
One consolidated gate that locks multiple strategies across curated seeds/configs.
This is the “engine-grade” proof that future optimizations didn’t drift behavior.

### New file
- `Tests/EditMode/DungeonSeedSetSnapshotTests.cs`

### Contents
- For each strategy: 3–5 (seed, cfg) pairs with locked expected hashes.

### Done when
- One test run tells you instantly if anything changed anywhere.

---

## 8) Suggested implementation order

1) **E0 Utilities** (tiny)
2) **E1 Corridor First** (fast, high value, determinism-critical)
3) **E2 Room First BSP** (introduces partitioning)
4) **E3 Room Grid** (minimal slice; metadata later)
5) **E4 Seed-set suite** (locks everything)

---

## 9) Phase E Completion Gate

Phase E is complete when:
- E1, E2, E3 runtime ports exist and operate on `MaskGrid2D`
- Lantern supports all three strategies (visual QA)
- Each strategy has determinism tests + locked golden hash
- (Optional but preferred) seed-set suite is in place

---

## Appendix: Reused toolbox (no changes required)

- `MaskGrid2D` (core grid mask)
- `MaskRasterOps2D.DrawLine(...)` (corridors)
- `MaskRasterOps2D.StampDisc(...)` (brush stamping)
- `RectFillGenerator.FillRect(...)` (rooms)
- `IteratedRandomWalk2D.Carve(...)` (organic rooms, optional)
- `PCGMaskVisualization` (Lantern path)
- `MaskGrid2D.SnapshotHash64()` (determinism gates)

