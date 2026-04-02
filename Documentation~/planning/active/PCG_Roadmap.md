# Islands.PCG — Active Roadmap

Status: Active planning  
Authority: Planning only (not implementation truth)  
Scope: Sequencing and future work for the new Islands.PCG pipeline.

## Rule
This document is not implementation authority.
Implemented truth lives in subsystem SSoTs and governed reference/support docs where explicitly assigned.

## Current status snapshot
- Phase A: done
- Phase B: done
- Phase C: done
- Phase D: done
- Phase E: implemented / test-gated support surface
  - E1: done
  - E2: implemented
  - E3: fully locked
  - E4: seed-set regression complete
- Phase F: done
  - F0: done
  - F1: done
  - F2: done
  - F3: done
  - F4: done
  - F5: done
  - F6: done
- Phase G: done
- Phase F2b: done
- Phase F2c: done
- Phase H: next
- Phase I: later
- Phase J: later (planning only)
- Phase K: later (planning / exploratory only)
- Phase L: later (planning only)
- Phase M: later (planning only)
- Phase N: later (planning only)
- Phase O: later (planning only)

## Documentary note on Layout Strategies
Layout strategies are currently treated as a governed deep reference / staged support surface under PCG.
They do not currently function as a separate subsystem SSoT.
See `reference/pcg-layout-strategies-reference.md` for deep per-strategy behavior and gates.

## Documentary note on Noise / Meshes / Surfaces / Shaders
After Batch 6:
- Noise remains governed reference / staged support rather than a subsystem SSoT.
- Meshes remains governed reference / staged support rather than a subsystem SSoT.
- Surfaces remains governed reference / staged support rather than a subsystem SSoT.
- Shaders remains governed reference / support only.
This roadmap may mention those surfaces as planning dependencies or support infrastructure, but that does not promote them into subsystem authority.

## Phase F — Map Pipeline by Layers
### Done
- F0 Context + contracts
- F1 Map lantern
- F2 Base terrain (`Height`, `Land`, `DeepWater`)
- F3 Hills + topology
  - appended topology layer IDs
  - added `MaskTopologyOps2D`
  - implemented `Stage_Hills2D`
  - integrated Noise via `MapNoiseBridge2D`
  - added F3 stage + pipeline goldens
  - updated lantern for hills/topology inspection
- F4 Shore + ShallowWater
  - implemented `Stage_Shore2D`
  - deterministic 1-cell shallow-water ring around all Land cells
  - `ShallowWater ∩ DeepWater` intentionally non-empty (see SSoT contracts)
  - added F4 stage + pipeline goldens
- F5 Vegetation
  - implemented `Stage_Vegetation2D`
  - `Vegetation ⊆ LandInterior`; excludes `HillsL2` peaks; noise-threshold coverage
  - `MapFieldId.Moisture` write deferred to Phase M
  - added F5 stage + pipeline goldens
  - updated lantern with `enableVegetationStage` toggle and `stagesF5` array
- F6 Walkable + Stairs (Traversal)
  - implemented `Stage_Traversal2D`
  - `Walkable` = `Land AND NOT HillsL2`; `Stairs` = HillsL1/HillsL2 boundary ring
  - `Stairs ⊆ Walkable`; both disjoint from `HillsL2`
  - `MapLayerId.Paths` write deferred to Phase O
  - added F6 stage + pipeline goldens
  - updated lantern with `enableTraversalStage` toggle and `stagesF6` array

## Phase G — Morphology (LandCore + CoastDist)
### Done
- Added `MaskMorphologyOps2D`: deterministic 4-neighborhood erosion (`Erode4`, `Erode4Once`) and
  multi-source BFS distance field (`BfsDistanceField`)
- Implemented `Stage_Morphology2D`
- Authoritative outputs: `LandCore` mask, `CoastDist` scalar field
- New append-only IDs: `MapLayerId.LandCore = 11`, `MapFieldId.CoastDist = 2`
- Stage tunables are stage-local: `ErodeRadius` (default 3), `CoastDistMax` (default 0 = auto)
- Contracts: `LandCore ⊆ LandInterior ⊆ Land`; `CoastDist == 0` at coast, increases inland,
  `-1f` at water and cells beyond CoastDistMax
- No noise, no RNG consumption
- Added Phase G stage + pipeline golden gates
- Updated lantern with `enableMorphologyStage` toggle and `stagesG` array

## Later phases

### Phase F2b — Island Shape Reform: Organic Silhouettes
### Done
- Replaced circular radial falloff in `Stage_BaseTerrain2D` with ellipse + domain-warp silhouette.
- New `MapTunables2D` fields: `islandAspectRatio` (clamped [0.25, 4.0], default 1.0),
  `warpAmplitude01` (clamped [0, 1], default 0.0).
- Warp uses two coarse noise grids (WarpCellSize=16) always consumed from ctx.Rng (stable RNG count).
  RNG consumption order: island noise → warpX → warpY. Stable regardless of tunable values.
- aspect=1.0 + warp=0.0 => geometrically identical circle to pre-F2b; goldens differ.
- No new `MapLayerId` or `MapFieldId` entries.
- All F2–Phase G golden hashes re-locked in one migration pass. Phase G goldens locked for first time.
- `PCGMapVisualization` patched: new Inspector fields `islandAspectRatio`, `warpAmplitude01` under
  "F2 Tunables (Island Shape — Ellipse + Warp)" header; `BaseTerrainStage_Configurable` updated to
  mirror Stage_BaseTerrain2D exactly.
- This is Level 1 of the island shape vision: varied single-island outlines.

### Phase F2c — Arbitrary Shape Input (Mask / Image / Voronoi)
**Done.**

- `MapShapeInput` companion struct added (`Runtime/PCG/Layout/Maps/MapShapeInput.cs`).
  `HasShape` flag + `MaskGrid2D Mask`; default (`None`) preserves F2b ellipse+warp path unchanged.
- `MapInputs` extended with optional 4th constructor parameter (backward compatible; all existing call sites unchanged).
- `Stage_BaseTerrain2D` shape-input branch: `GetUnchecked(x,y)` bool lookup replaces ellipse+warp when `HasShape=true`.
  All three RNG arrays still filled in identical order, preserving downstream stage determinism.
  Dimension guard throws `ArgumentException` on mismatch.
- No new `MapLayerId` or `MapFieldId` entries. F2b goldens unchanged. F2c goldens locked.
- No `PCGMapVisualization` patch required; lantern always runs F2b path; shape-path visual testing deferred to editor tooling.
- This is Level 2 of the island shape vision: arbitrary silhouettes as pipeline inputs.
- Feeds into: Phase K (Plate Tectonics landmasses may use this to inject Voronoi-derived shapes).

### Phase H — Extract + Adapters
**Next.**

- Governed home for colored multi-layer visualization (per-layer color mapping).
- Colored layer visualization is a sample/adapter enhancement and is not authoritative runtime.
- It may be implemented earlier as a sample-side improvement during F5–F6 development without
  waiting for Phase H, provided it does not introduce core runtime dependencies.
- Phase H is also the natural place to govern scalar field visualization (e.g. CoastDist, Height
  as a color ramp), which is currently not supported by the mask-first lantern.

### Phase I
Burst / SIMD upgrades

### Phase J — Region Generation (static Voronoi)
Planning only.

- Static Voronoi-cell region partitioning using the existing Noise Voronoi support surface
  (`Noise.Voronoi.cs`, `Noise.Voronoi.Distance.cs`, `Noise.Voronoi.Function.cs`).
- Expected implementation path: a new `MapVoronoiBridge2D` (or extended bridge) following
  the `MapNoiseBridge2D` pattern; a new `Stage_Regions2D`; new append-only `MapLayerId` entries.
- Does not require promoting Noise to a subsystem SSoT.
- Noise remains governed reference / staged support.
- Prerequisite for Phase K.
- Archipelago support (Level 3 of the island shape vision): multiple disconnected land masses
  can be produced by compositing multiple Phase F2c shape inputs before the Land threshold pass,
  or by running independent per-island pipelines and merging outputs. Voronoi cell boundaries
  from Phase J are a natural driver for multi-island layout.

### Phase K — Simulation / Dynamics (Plate Tectonics)
Planning / exploratory only. Not implementation authority.

- Depends on Phase J (Voronoi region partitioning) as the static foundation.
- Initial intent: simple plate-movement simulation derived from Voronoi cells,
  with collision zones used to drive elevation or hills-layer outputs via the existing
  layered pipeline contracts.
- Dynamic / simulation work is not scoped or sequenced beyond this intent statement.
- This phase may be subdivided or deferred as Phase J matures.
- Plate-derived landmasses can be injected into the pipeline via the Phase F2c shape-input
  mechanism, enabling plate collision zones to drive both terrain silhouettes and hill layers.

### Phase L — Hydrology
Planning only.

- Rivers: flow-path generation from high elevation toward coast,
  producing a Rivers mask layer (new append-only `MapLayerId`).
- Lakes: detection and labeling of enclosed water bodies not covered by `DeepWater`
  (border-connected) or `ShallowWater` (F4).
- Open design question: distinct `Lakes` layer vs. enclosed `ShallowWater` cells.
- Open design question: rivers as a mask layer vs. a flow-accumulation scalar field.
- Depends on: `Height` field (F2), `Land` + `ShallowWater` (F4).

### Phase M — Biome / Region Classification
Planning only.

- Classifies cells into ecological / biome categories using `Height`, `Moisture`
  (already registered as `MapFieldId.Moisture`), shore proximity (F4), and optionally
  Voronoi region identity (Phase J).
- Natural owner of the first authoritative write to `MapFieldId.Moisture`
  (F5 does not produce it; ownership confirmed deferred to Phase M).
- Produces biome classification outputs (new layer IDs or scalar field, design TBD).
- Enables biome-aware downstream work: vegetation density, river likelihood, POI suitability.
- Depends on: F4 (shore), F5 (vegetation layer as upstream context), optionally Phase J.

### Phase N — World-Site / POI Placement
Planning only.

- Produces site-selection masks and placement coordinates for RPG-style POI types:
  coastal villages/cities (suitability: `LandEdge` / ShallowWater-adjacent land),
  forest dungeons (suitability: `Vegetation`),
  cave/mine entrances (suitability: `Stairs` — HillsL1/HillsL2 boundary cells),
  open-plains camps (suitability: `Walkable AND NOT HillsL1 AND NOT Vegetation`), etc.
- Uses suitability / exclusion logic derived from terrain layers
  (`Land`, `Hills`, `Shore`, `Walkable`, `Stairs`, biome outputs, etc.).
- Outputs are headless placement descriptors: typed coordinates and masks, not GameObjects
  or scene content. Adapters-last boundary must be preserved.
- Downstream adapters (existing dungeon layout generators, future village / ruin generators)
  consume these placement outputs as adapters. Those generators remain layout strategies /
  adapters, not core pipeline stages.
- Depends on: F6 (Walkable, Stairs), Phase L (Hydrology), Phase M (Biome).

### Phase O — Traversal Network / Paths
Planning only.

- Produces a `Paths` mask connecting POI sites across walkable terrain.
  `MapLayerId.Paths = 5` is already registered in `MapIds2D`; ownership deferred to this phase.
- Algorithm options (to be resolved at implementation time): A* or flood-corridor on `Walkable`,
  noise-corridor traces seeded from POI coordinate pairs, skeleton extraction, etc.
- POI anchor coordinates from Phase N are the required inputs; implementing Paths before Phase N
  would produce paths with no meaningful endpoints.
- Depends on: F6 (Walkable, Stairs), Phase N (POI placement).

## Legacy relationship
Legacy map-generation documents are conceptual reference only for the new pipeline.
The active authoritative direction is masks/fields + adapters-last + deterministic headless stages.
