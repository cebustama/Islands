# Islands.PCG — Phase E Progress Report (E1–E2 — Corridor First + Room First BSP)

Date: 2026-01-29  
Scope: Phase E (port remaining legacy dungeon strategies to pure grid-based implementations using `MaskGrid2D`).  
Status: **E1 complete** · **E2 complete** · Next: **E3 (Room Grid — layout-only minimal slice)**

---

## Phase E goal (context)

Port the remaining legacy dungeon *strategies* into **pure grid writers**:

- Core algorithms operate on `MaskGrid2D` / `NativeArray` only (**no Tilemaps, no HashSets**).
- Determinism is guaranteed by **seed-driven** `Unity.Mathematics.Random` (**no GUID-based shuffles**).
- Each strategy must be validated via:
  1) **Lantern** visual inspection (no flicker across frames),
  2) **EditMode tests**,
  3) **SnapshotHash64** + **golden hash gate** (stable regression checks).

---

## What we implemented (E1 recap — Corridor First)

**Delivered earlier in Phase E (E1):**
- `CorridorFirstDungeon2D` as a grid-only strategy (carving corridors first, then attaching rooms / cleanup per the port plan).
- Shared layout utilities:
  - `LayoutSeedUtil` (seed sanitation + RNG creation)
  - `MaskNeighborOps2D` (read-only neighborhood queries, dead-end support)
- Lean dungeon-only Lantern testbed: `PCGDungeonVisualization` (supports dungeon layout strategies only).

(See the earlier E1 progress report file for the full breakdown.)

---

## What we implemented (E2 — Room First BSP)

E2 ports the **Room First (BSP)** pipeline to grids:

1) BSP partition the full domain into leaf rects (pure layout).
2) For each leaf: shrink by padding → stamp a rectangular room (`RectFillGenerator.FillRect`).
3) Connect consecutive room centers with corridors (`MaskRasterOps2D.DrawLine` or Manhattan “L”).
4) All writes are OOB-safe (clamp / skip), and RNG usage is seed-only.

### 1) New runtime: deterministic BSP partitioner (pure layout)
**File:** `Runtime/PCG/Layout/Bsp/BspPartition2D.cs`

- Deterministically splits an integer rect using `Unity.Mathematics.Random`.
- Outputs a leaf list into a caller-provided `NativeArray<IntRect2D>` (no allocations).
- Safety rules:
  - Never split if it would create a child smaller than `minLeafSize`.
  - Clamp split ranges; if no valid split exists → stop splitting that node.
  - All math is integer and domain-bounded.

### 2) New runtime: Room First BSP strategy (carving into `MaskGrid2D`)
**File:** `Runtime/PCG/Layout/RoomFirstBspDungeon2D.cs`

**Core API (as implemented):**
```csharp
public static void Generate(
    ref MaskGrid2D mask,
    ref Unity.Mathematics.Random rng,
    in RoomFirstBspConfig cfg,
    NativeArray<BspPartition2D.IntRect2D> scratchLeaves,
    NativeArray<int2> outRoomCenters,
    out int leafCount,
    out int placedRooms)
```

**Config surface (RoomFirstBspConfig):**
- `int splitIterations`
- `int2 minLeafSize`
- `int roomPadding`
- `int corridorBrushRadius`
- `bool connectWithManhattan`
- `bool clearBeforeGenerate`

**OOB / safety rules (carving):**
- Rooms: leaf rect is padded and clamped to the domain; degenerate rooms are skipped.
- Corridors: all corridor points are drawn using OOB-safe raster ops (clip/ignore outside domain).
- The strategy never writes outside `mask.Domain`.

### 3) Tests: determinism + golden hash gate
**File:** `Tests/EditMode/RoomFirstBspDungeon2DTests.cs`

**Covers:**
- Same seed + same config ⇒ same `SnapshotHash64` (determinism).
- Golden gate constant for stable regression:
  - `0x23A63F312B9CDF98`

### 4) Lantern wiring: `PCGDungeonVisualization` supports RoomFirstBsp
**File:** `Runtime/PCG/Samples/PCGDungeonVisualization.cs`

- Added `StrategyMode.RoomFirstBsp`.
- Added inspector config block + dirty tracking (mirrors existing CorridorFirst wiring).
- Allocates and reuses scratch arrays (`scratchLeaves`, `outRoomCenters`) and calls:
  - `RoomFirstBspDungeon2D.Generate(...)`
  - `PackFromMaskAndUpload(resolution)` for GPU instancing display.

---

## Extra work done while completing E2 (important)

### A) Fixed GPU instancing buffer contract regression
While wiring E2 we found a rendering regression (layouts appeared “truncated” at normal resolutions).

**Root cause:**
- `_Noise` buffer was accidentally allocated as `dataLength` elements of `float4` (stride 16),
  but the shader indexes `_Noise[unity_InstanceID]` as **one float per instance** (count == `resolution*resolution`).

**Fix (restored working contract):**
- Allocate `_Noise` as `ComputeBuffer(dataLength * 4, sizeof(float))` so count == `resolution²`.

This fix restored correct rendering for *all* strategies (D2/D3/D5/E1/E2) inside the Lantern.

### B) Recorded the full “MaskGrid2D → GPU Instancing” display pipeline in the SSoT
We added an authoritative technical breakdown explaining:

- How cells map to `unity_InstanceID`.
- How `Visualization` uploads `_Positions`, `_Normals`, `_Noise` and how ShaderGraph + `PCGDungeonGPU.hlsl`
  use `ConfigureProcedural()` + `GetNoiseColor()` to render the grid.
- Explicit extension points for future adapters (Texture, Mesh).

**SSoT updated:** `Islands_PCG_Pipeline_SSoT_v0_1_11.md`

---

## Files added / modified (E2 delta)

### New (E2)
- `Runtime/PCG/Layout/Bsp/BspPartition2D.cs`
- `Runtime/PCG/Layout/RoomFirstBspDungeon2D.cs`
- `Tests/EditMode/RoomFirstBspDungeon2DTests.cs`

### Modified (E2)
- `Runtime/PCG/Samples/PCGDungeonVisualization.cs`
  - add `StrategyMode.RoomFirstBsp`
  - config + dirty tracking + scratch arrays + generation case
  - fix `_Noise` buffer contract (count == `resolution²` floats)

### Documentation updated
- `Islands_PCG_Pipeline_SSoT_v0_1_11.md` (adds E2 + GPU instancing technical breakdown)

---

## Phase E status gates

### E1 — Corridor First
✅ Visual gate (Lantern)  
✅ Test gate(s) present (EditMode + golden hash) *(per Phase E tracking / earlier completion)*  
✅ Contract gates (grid-only, seed-only RNG, OOB-safe)

### E2 — Room First BSP
✅ Visual gates (Lantern): stable, deterministic; seed toggles and Manhattan/direct toggles behave as expected  
✅ Test gates: determinism + golden hash `0x23A63F312B9CDF98`  
✅ Contract gates: grid-only, seed-only RNG, OOB-safe

---

## What comes next: E3 — Room Grid (layout-only minimal slice)

### Intent
A “grid of rooms” driven by a discrete placement process (often path-based), then corridors connecting rooms.
This is intentionally a **minimal stable slice** focused on clean grid-only layout behavior.

### Planned deliverables (per Phase E plan)
**New runtime file**
- `Runtime/PCG/Layout/RoomGridDungeon2D.cs`

**Config (expected)**
`public struct RoomGridConfig`
- `int roomCount`
- `int2 cellStride` (spacing between centers in mask coords)
- `int2 roomSizeMin, roomSizeMax`
- `int corridorBrushRadius`
- `bool clearBeforeGenerate`
- (optional) `int placementAttemptsPerRoom` (only if you want to avoid overlaps in the minimal slice)

**Lantern**
- Add `StrategyMode.RoomGrid` (or equivalent) + inspector config + dirty tracking in `PCGDungeonVisualization`.
- Allocate scratch arrays for centers and (optionally) scratch for placement / occupancy checks.
- Call `RoomGridDungeon2D.Generate(...)` then `PackFromMaskAndUpload(resolution)`.

**Tests**
- `Tests/EditMode/RoomGridDungeon2DTests.cs`
  - same seed/config ⇒ same hash
  - golden hash gate

### E3 acceptance gates
- Visual (Lantern): stable (no flicker), deterministic; changing seed changes the layout deterministically.
- Test: determinism + golden hash pass.
- Contract: grid-only, seed-only RNG, OOB-safe stamping + corridors.

---

## Recommended file set to attach to start E3 cleanly (minimal + sufficient)

1) `Islands_PCG_Pipeline_SSoT_v0_1_11.md` (authoritative state + GPU instancing record)  
2) `PhaseE_Planning_Report.md` (Phase E step plan, E3 intent + gates)  
3) `Runtime/PCG/Samples/PCGDungeonVisualization.cs` (Lantern wiring target)  
4) `Runtime/PCG/Samples/Visualization.cs` (base instancing + buffer contracts)  
5) `Runtime/PCG/Grids/MaskGrid2D.cs`  
6) `Runtime/PCG/Grids/MaskGrid2D.Hash.cs` (SnapshotHash64 contract)  
7) `Runtime/PCG/Operators/MaskRasterOps2D.cs` (DrawLine/StampDisc etc)  
8) `Runtime/PCG/Generators/RectFillGenerator.cs` (room stamping)  
9) `Runtime/PCG/Layout/RoomFirstBspDungeon2D.cs` (closest reference style for room stamping + corridor links)  
10) `PCGDungeonGPU.hlsl` (rendering contract reference; buffer expectations)

Optional but helpful if you expect to mirror placement logic / neighborhood checks:
- `Runtime/PCG/Layout/MaskNeighborOps2D.cs`
- `Runtime/PCG/Layout/LayoutSeedUtil.cs`

---

## Recommended next immediate step

Implement `RoomGridDungeon2D` as a **pure grid writer** with the smallest stable behavior:
- generate `roomCount` centers (grid/path-based),
- stamp rooms (rect),
- connect sequentially,
- return centers (for debugging + tests),
then wire it into `PCGDungeonVisualization` and add the determinism + golden gate tests.
