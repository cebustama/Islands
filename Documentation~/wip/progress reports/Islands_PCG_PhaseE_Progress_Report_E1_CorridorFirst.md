# Islands.PCG — Phase E Progress Report (E1 — Corridor First)

Date: 2026-01-29  
Scope: Phase E (port remaining legacy dungeon strategies to pure grids) — **E1 implemented (Corridor First), integration + visualization testbed added**

---

## Phase E goal (context)

Port remaining legacy dungeon strategies into **pure grid operations**:
- Core algorithms operate on `MaskGrid2D` / `NativeArray` only (no Tilemaps, no HashSets).
- Determinism is guaranteed by **seed-driven** `Unity.Mathematics.Random` (no GUID-based shuffles).
- Each strategy must be validated via:
  1) **Lantern** visual inspection,
  2) **EditMode tests**,
  3) **SnapshotHash64** + **golden hash gate** for stable regression checks.

Phase E strategies:
- **E1**: Corridor First (this report)
- **E2**: Room First (BSP)
- **E3**: Room Grid (layout-only minimal slice)

---

## Why Corridor First was chosen first (recap)

Corridor First is the cleanest first port because it:
- only needs `MaskGrid2D` + existing raster ops (line/rect stamping),
- has a clear **corridors → rooms** flow that fits the Lantern + SnapshotHash64 gates,
- forces the key determinism fix early (seeded Fisher–Yates instead of GUID shuffles),
- unlocks shared Phase-E utilities (`LayoutSeedUtil` + `MaskNeighborOps2D`) that BSP and RoomGrid will reuse.

---

## What we implemented (E1)

### 1) Shared Phase-E utilities (new runtime files)

#### A) `LayoutSeedUtil`
**Purpose:** centralize seed sanitation and RNG creation (determinism contract).

- Ensures all layout strategies can consistently do:
  - `seed <= 0` → clamp to `1`
  - `new Unity.Mathematics.Random((uint)seed)` with no non-deterministic sources.

**Used by:** `CorridorFirstDungeon2D` (and intended for BSP + RoomGrid next).

#### B) `MaskNeighborOps2D`
**Purpose:** lightweight, grid-only neighborhood queries to support dead-end detection and local topology checks.

- Designed as **read-only mask helpers** (no tilemaps, no allocations).
- Used to detect dead ends (e.g., floor cells with exactly 1 cardinal neighbor).

**Used by:** `CorridorFirstDungeon2D` dead-end room stamping, and intended for future morphology/topology passes.

---

### 2) New strategy: `CorridorFirstDungeon2D` (grid-only)

**Intent (legacy parity):**
Carve corridors first, then place rooms at corridor endpoints and optionally at dead ends.

**Public API:**
- `public struct CorridorFirstConfig`
- `public static void Generate(ref MaskGrid2D mask, ref Random rng, in CorridorFirstConfig cfg, NativeArray<int2> scratchCorridorEndpoints, NativeArray<int2> outRoomCenters, out int placedRooms)`

**Key behavior notes:**
- **No allocations** inside `Generate`: caller provides scratch arrays (`scratchCorridorEndpoints`, `outRoomCenters`).
- **OOB-safe**: all writes are clamped/skipped to keep generation safe at any resolution.
- **Deterministic endpoint selection**: seeded shuffle / selection via RNG (no GUID randomness).
- **Rooms placement knobs**:
  - `roomSpawnCount > 0` → pick exactly N unique endpoints (seeded)
  - `roomSpawnCount <= 0` → per-endpoint chance via `roomSpawnChance`

**Outputs:**
- `mask`: filled floor cells (corridors + rooms)
- `placedRooms`: count of successfully stamped rooms

---

### 3) Lantern / visualization integration

To keep the original mask demo class from growing indefinitely, we introduced a dedicated dungeon testbed.

#### A) New lean visualizer: `PCGDungeonVisualization`
**Purpose:** strategy-focused Lantern testbed that only includes dungeon strategies (SRP).

Supported strategies:
- Simple Random Walk (D2)
- Iterated Random Walk (D3)
- Rooms + Corridors (D5)
- Corridor First (E1)

Notes:
- Minimal dirty tracking (only checks parameters for the active strategy).
- Still uses the same GPU instancing buffer path (`_Noise`) + palette (`_MaskOffColor`, `_MaskOnColor`).
- Added **tooltips for every serialized field** to make inspector-driven testing fast.

#### B) Optional: `PCGMaskVisualization` wiring
Corridor First was also wired as a `SourceMode` option in the legacy `PCGMaskVisualization`
(to preserve parity with existing Lantern setups), but the recommended path going forward is
`PCGDungeonVisualization` for dungeon strategy testing.

---

## Files added / modified

### New
- `Runtime/PCG/Layout/LayoutSeedUtil.cs`
- `Runtime/PCG/Layout/MaskNeighborOps2D.cs`
- `Runtime/PCG/Layout/CorridorFirstDungeon2D.cs`
- `Runtime/PCG/Samples/PCGDungeonVisualization.cs` *(dungeon-only Lantern testbed)*

### Modified (optional / legacy Lantern)
- `Runtime/PCG/Samples/PCGMaskVisualization.cs`
  - add `SourceMode.CorridorFirstMask`
  - inspector config + dirty tracking
  - generation switch-case calling `CorridorFirstDungeon2D.Generate(...)`

---

## What is already “done” for E1

✅ Runtime corridor-first generator exists (grid-only)  
✅ Shared utilities exist (`LayoutSeedUtil`, `MaskNeighborOps2D`)  
✅ Lantern testbed exists (`PCGDungeonVisualization`)  
✅ Inspector tooltips exist for strategy parameters  
✅ Determinism contract maintained (seed-driven RNG only)  
✅ OOB-safe behavior (clamp/skip) maintained

---

## What remains to *complete* E1 (gates)

To declare **E1 complete**, we still need the standard parity gates (unless you already added them in your repo):

1) **EditMode test file**: `CorridorFirstDungeon2DTests.cs`
   - generate at a fixed resolution with a fixed config + seed
   - compute `SnapshotHash64`
   - assert equals the golden value

2) **Golden hash gate workflow**
   - add a dedicated “golden” test (or golden data table) so regressions are caught immediately

3) **(Optional) Invariant tests**
   - “no OOB exceptions”
   - “mask has at least N ones”
   - “dead-end room stamping increases room count under deterministic config” (light sanity)

---

## What comes next (Phase E roadmap)

### E2 — Room First (BSP)
Goal: Port the legacy “room-first BSP” layout into grids.

Likely components:
- `BspPartition2D` (pure layout: splits rectangles deterministically)
- `RoomFirstBspDungeon2D` (carves rooms then corridors between partitions)
- Reuse:
  - `LayoutSeedUtil` for deterministic RNG
  - `RectFillGenerator` for room stamping
  - `MaskRasterOps2D.DrawLine` for corridor carving
  - `MaskNeighborOps2D` for dead-end / adjacency checks if needed

Gates:
- Lantern mode in `PCGDungeonVisualization`
- SnapshotHash64 EditMode test + golden

### E3 — Room Grid (layout-only minimal slice)
Goal: Minimal “room-grid” layout strategy (even if it only returns room rectangles / centers first).

Likely approach:
- Start as **layout-only** (produce placed room rects / centers deterministically)
- Then add carving into `MaskGrid2D` with `RectFillGenerator`
- Optional corridor linking rules later

Gates:
- Lantern mode showing carved rooms (even without corridors is acceptable for the minimal slice)
- SnapshotHash64 EditMode test + golden

---

## Recommended next immediate step

If you want E1 to be “ship-ready” in the same standard as Phase D:

➡️ Add `CorridorFirstDungeon2DTests.cs` + golden hash gate, then mark E1 complete in the SSoT / progress tracking.

After that, proceed to **E2 (BSP)** — it will reuse the utilities introduced here and should be a smooth incremental port.

