# Islands.PCG — Phase F Progress Report (F0–F2 — Map Pipeline Contracts + Map Lantern + Base Terrain)

Date: 2026-02-03  
Scope: Phase F (Map Pipeline by Layers — core/headless pipeline + Lantern-style visualization).  
Status: **F0 complete** · **F1 complete** · **F2 complete** · Next: **F3 (Hills + topology)**

---

## Phase F goal (context)

Build a **general, deterministic, grid-first map generation pipeline** that produces multiple layers (`MaskGrid2D`) and fields (`ScalarField2D`) in a **headless** way, with:

- **Stage contracts** (explicit inputs + context, no adapters required).
- **Determinism discipline**:
  - seed-driven `Unity.Mathematics.Random` only,
  - no nondeterministic iteration sources (no `HashSet`/unordered `Dictionary` traversal),
  - golden hash regression gates.
- A dedicated **Lantern-style visualization** to inspect layers (GPU instancing, like the dungeon Lantern).

---

## What we implemented (F0 — Context + contracts)

F0 establishes the **core API surface** and the **data ownership model** for Phase F.

### 1) Stable registries for layers + fields (index-based, deterministic)
**File:** `Runtime/Layout/Maps/MapIds2D.cs`

- `MapLayerId` (stable enum ordering): at minimum `Land`, plus placeholders like `DeepWater`, `ShallowWater`, etc.
- `MapFieldId` (stable enum ordering): at minimum `Height`, plus placeholder fields as needed.

**Determinism note:** registry lookup is **array index-based** (not `Dictionary`), so stage behavior does not depend on hash iteration order.

### 2) Inputs model (immutable, sanitized deterministically)
**Files:**  
- `Runtime/Layout/Maps/MapInputs.cs`  
- `Runtime/Layout/Maps/MapTunables2D.cs`

- `MapInputs` carries:
  - `Seed` (sanitized to `>= 1`),
  - `Domain` (`GridDomain2D`),
  - `Tunables` (`MapTunables2D`).
- `MapTunables2D` clamps values deterministically (`math.clamp` + ordered ranges).

### 3) Headless run context (SSoT for a pipeline run)
**File:** `Runtime/Layout/Maps/MapContext2D.cs`

`MapContext2D` is the in-memory “truth” for a run:

- Owns the memory for **layers** (`MaskGrid2D[]`) and **fields** (`ScalarField2D[]`).
- Provides **lazy allocation** via:
  - `ref MaskGrid2D EnsureLayer(MapLayerId id, bool clearToZero = true)`
  - `ref ScalarField2D EnsureField(MapFieldId id, bool clearToZero = true)`
- Provides strict accessors:
  - `ref MaskGrid2D GetLayer(MapLayerId id)`
  - `ref ScalarField2D GetField(MapFieldId id)`
- Provides run control:
  - `BeginRun(in MapInputs inputs, bool clearLayers = true)`
  - `ClearAllCreatedLayers()`
  - `Dispose()`

**Determinism hard rule (implemented):**
- No uninitialized memory may leak into layers/fields: `EnsureLayer/EnsureField` must create and/or clear deterministically, or hashes/goldens drift.

### 4) Stage contract + deterministic runner
**Files:**  
- `Runtime/Layout/Maps/IMapStage2D.cs`  
- `Runtime/Layout/Maps/MapPipelineRunner2D.cs`

- `IMapStage2D`:
  - `string Name { get; }`
  - `void Execute(ref MapContext2D ctx, in MapInputs inputs)`
- `MapPipelineRunner2D` executes a stage array in **stable index order**:
  - `Run(ref MapContext2D ctx, in MapInputs inputs, IMapStage2D[] stages, bool clearLayers = true)`
  - `RunNew(in MapInputs inputs, IMapStage2D[] stages, Allocator allocator, bool clearLayers = true)`

---

## What we implemented (F1 — Map Lantern skeleton)

F1 provides a working **Lantern-style** map visualization aligned with the dungeon Lantern’s GPU instancing model.

### `PCGMapVisualization` (MaskGrid2D layer viewer)
**File:** `Samples/PCGMapVisualization.cs`

Capabilities delivered:
- Runs a headless `MapPipelineRunner2D` into a `MapContext2D`.
- Displays a selected `MaskGrid2D` layer (`viewLayer`) by uploading packed float data to `_Noise`.
- Safe fallback: missing layers render as all-off (zeros upload).
- Dirty tracking: seed/resolution/layer selection changes trigger regeneration + reupload.

---

## What we implemented (F2 — Base Terrain vertical slice)

F2 delivers a **minimal “island-like”** base terrain stage that writes:

- **Field:** `Height` (`ScalarField2D`, normalized `[0..1]`)
- **Layer:** `Land` (`MaskGrid2D`) via `Height >= waterThreshold01`
- **Layer:** `DeepWater` (`MaskGrid2D`) via deterministic border-connected water classification

### 1) Stage: base terrain (Height + Land + DeepWater)
**File:** `Runtime/Layout/Maps/Stages/Stage_BaseTerrain2D.cs`

**Algorithm (high level):**
- Builds a stable island silhouette from **radial falloff** (smoothstep between `islandSmoothFrom01/to01`) with a small **coarse value-noise** perturbation (seed-driven, stable consumption count).
- Writes `Height` (0..1), then thresholds into `Land`.
- Computes `DeepWater` as **border-connected NOT Land** using a deterministic flood fill.

**Determinism notes:**
- RNG consumption is stable (row-major fill of a coarse noise grid).
- No unordered traversal (row-major loops + deterministic queue flood fill).
- Height is optionally **quantized** (fixed step count) to reduce threshold jitter sensitivity.

### 2) Operator: deterministic flood fill (border-connected water)
**File:** `Runtime/Operators/MaskFloodFillOps2D.cs`

- API: `FloodFillBorderConnected_NotSolid(ref MaskGrid2D solid, ref MaskGrid2D dstVisited)`
- Contract: treats `solid==true` as blocked; visits border-connected cells where `solid==false`.
- OOB-safe: never reads outside the grid.

### 3) Lantern update (F2.3)
**File:** `Samples/PCGMapVisualization.cs`

- Default debug pattern now runs **`Stage_BaseTerrain2D`**.
- `View Layer` supports inspecting at least `Land` and `DeepWater`.
- Micro-step: exposes key `MapTunables2D` parameters in Inspector (e.g., `islandRadius01`, `waterThreshold01`), enabling rapid iteration without touching core.

---

## Tests & gates (EditMode)

### Stage-level gates
**Files:**
- `Tests/EditMode/Maps/StageBaseTerrain2DTests.cs`
- `Tests/EditMode/Operators/MaskFloodFillOps2DTests.cs`

What they cover:
- Determinism of base terrain stage outputs (same seed/domain/tunables ⇒ same hashes).
- Golden hash gates for `Land` and `DeepWater`.
- Flood fill invariants and OOB safety (border-connected behavior is stable).

### Pipeline-level golden update (F2.2 — Option A)
We **keep** the original “trivial rect” pipeline golden as an infrastructure drift alarm **and** add a new pipeline-level golden for the real F2 stage.

**Add file:**
- `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF2Tests.cs`

**Test concept:**
- stages = `[ new Stage_BaseTerrain2D() ]`
- golden = `Land.SnapshotHash64()` (optionally also lock `DeepWater`)

---

## Files added / modified (F2 delta)

### New (F2 runtime)
- `Runtime/Layout/Maps/Stages/Stage_BaseTerrain2D.cs`
- `Runtime/Operators/MaskFloodFillOps2D.cs`

### New / updated tests
- `Tests/EditMode/Maps/StageBaseTerrain2DTests.cs`
- `Tests/EditMode/Operators/MaskFloodFillOps2DTests.cs`
- `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF2Tests.cs` *(added; Option A)*

### Modified (minimal)
- `Samples/PCGMapVisualization.cs` (runs F2 stage; exposes tunables in Inspector)

---

## Phase F status gates

### F0 — Contracts + headless execution ✅
- Grid-first, headless core types (`MapInputs`, `MapContext2D`, `IMapStage2D`, runner)
- Determinism discipline enforced (seed sanitation, stable registries, no uninitialized memory)
- Test gates (EditMode determinism + golden hash)

### F1 — Map Lantern skeleton ✅
- Visual gate: Lantern displays deterministic mask output via GPU instancing
- Layer selection works; missing layers render as all-off

### F2 — Base Terrain stage ✅
- `Height` field written deterministically
- `Land` + `DeepWater` masks produced deterministically and OOB-safe
- Stage-level goldens locked
- Pipeline-level golden added (Option A) while keeping the trivial golden

---

## Known limitations (intentional at this milestone)

- The lantern is still **mask-first** (fields like `Height` are produced but not visualized yet).
- `Stage_BaseTerrain2D` keeps some noise constants internal for golden stability; tunable exposure can expand later as needed.

---

## What comes next (F3 — Hills + topology)

**F3 target deliverables:**
- Derive `HillsL1` / `HillsL2` masks from `Height` + (optional) additional noise/bias.
- Compute basic topology masks:
  - hill **edges** vs **interior** (8-neighbor rules, OOB-safe),
  - invariants like `HillsL2 ⊆ HillsL1`.
- Tests:
  - stage determinism + goldens (HillsL1, HillsL2, and optionally edges/interiors),
  - invariants tests (subset, exclusions with water, etc.).
- Lantern:
  - show `HillsL1` / `HillsL2` (and optionally edges) as selectable layers.

---

## Recommended file set to attach to start F3 cleanly (minimal + sufficient)

1) `Islands_PCG_Pipeline_SSoT_v0_1_16.md` (authoritative Phase F overview)  
2) `Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.2_2026-02-03.md` (Phase F steps + exit criteria)  
3) `Map_Generation_SSoT_v0.1.2_2026-01-29.md` (reference for hills intent/topology invariants)  
4) `Runtime/Layout/Maps/MapContext2D.cs`  
5) `Runtime/Layout/Maps/MapPipelineRunner2D.cs`  
6) `Runtime/Layout/Maps/IMapStage2D.cs`  
7) `Runtime/Layout/Maps/Stages/Stage_BaseTerrain2D.cs`  
8) `Runtime/Operators/MaskFloodFillOps2D.cs`  
9) `Samples/PCGMapVisualization.cs`  
10) `Runtime/Grids/MaskGrid2D.cs` + `MaskGrid2D.Hash.cs`
