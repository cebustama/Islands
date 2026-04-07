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
- Phase H: done
- Phase H1: done
- Phase H2: done
- Phase H2b: done
- Phase H2c: done
- Phase H2d: done
- Phase H3: done
- Phase H4: done
- Phase H5: done
- Phase H7: done
- Phase H6: done
- Phase H8: later (planning only)
- Phase F4b: done
- Phase F4c: done
- Phase N2: done
- Phase N4: done
- Phase F3b: done
- Phase N5: later (planning only — new)
- Phase I: later
- Phase I2: later (planning only)
- Phase J: later (planning only)
- Phase J2: later (planning only — new)
- Phase K: later (planning / exploratory only)
- Phase L: later (planning only)
- Phase M: later (planning only)
- Phase M2: later (planning only — new)
- Phase N: later (planning only)
- Phase O: later (planning only)
- Phase P: later (planning only — new)
- Phase T1: planning (design complete — adapter track)
- Phase W: later (planning / exploratory only)

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

## Documentary note on Phase Design Documents
Phases that require detailed specification (stage contracts, data structures, invariants,
test plans) have dedicated design documents under `planning/active/design/`. These carry
planning authority for their phase — the same authority level as this roadmap (not
implementation truth until built and recorded in the SSoT).

Phases that are simple enough to describe in a few bullet points remain inline here.
A phase entry in this roadmap always includes intent, status, dependencies, and a pointer
to its design doc if one exists. The design doc contains the implementation-depth detail.

| Phase | Design document | Status |
|-------|----------------|--------|
| Phase M | [`Phase_M_Design.md`](design/Phase_M_Design.md) | Complete |
| Phase W | [`Phase_W_Design.md`](design/Phase_W_Design.md) | Complete |
| Phase M2 | [`Phase_M2_Design.md`](design/Phase_M2_Design.md) | Complete |
| Phase L | [`Phase_L_Design.md`](design/Phase_L_Design.md) | Complete |
| Phase T1 | [`Phase_T1_Design.md`](design/Phase_T1_Design.md) | Complete |

## Resolved design decisions (2026-04-06)

The following design questions, previously recorded as open, were resolved via cross-check
analysis against six game worldgen pipelines (Minecraft, Dwarf Fortress, No Man's Sky,
RimWorld, Cataclysm:DDA, Tangledeep) and the PCG technique reports (1a–6b).

**Decision 1 — Biome output format:** `MapFieldId.Biome` integer scalar field.
Each cell stores a biome ID (integer encoded as float). A `BiomeDef[]` lookup table
defines biome properties (name, vegetation density, tile palette, etc.). This matches
the standard "region map" pattern from Pass 5a and the approach used by Minecraft (6D
parameter → biome enum), Dwarf Fortress (multi-variable threshold → biome type), and
RimWorld (BiomeWorker score competition). Blend weights for biome transitions are
deferred — hard boundaries first, blending as a later refinement if needed.

**Decision 2 — River representation:** Both `MapFieldId.FlowAccumulation` (scalar field)
and `MapLayerId.Rivers` (mask, derived by thresholding the field). The flow-accumulation
field is the simulation output, encoding upstream drainage area per cell. The river mask
is derived from it by thresholding and serves as the rendering/gameplay output. This
matches the standard pipeline from Pass 2b (Priority-Flood → D8 → flow accumulation →
threshold) and the Dwarf Fortress approach (flow volumes + visible rivers as separate
outputs). `MapFieldId.FlowDirection` is computed during Phase L but not persisted as a
registered field unless a downstream consumer requires it.

**Decision 3 — Lake modeling:** Distinct `MapLayerId.Lakes` mask layer. Lakes are inland
water bodies not connected to the map border — detected by connected-component analysis
on NOT-Land cells after DeepWater (border-connected) and ShallowWater (land-adjacent ring)
are already classified. This matches Pass 5a's explicit recommendation ("flood fill from
border through water identifies ocean; remaining water components are lakes") and avoids
breaking the existing F4 ShallowWater contract. Lakes have distinct gameplay semantics
(fishing, settlement proximity, freshwater source) from coastal ShallowWater.

**Decision 4 — Voronoi cells and world tiles are separate concepts.** Phase J's Voronoi
partitioning operates *within* a local map to create biome regions — the player walks
through these regions and sees biome transitions, but the Voronoi structure itself is
invisible. Phase W's world map uses a *rectangular grid* (a low-resolution `MapContext2D`),
where each cell is a world tile that can be zoomed into for local generation. Phase K's
Voronoi operates at a coarser geological scale to define tectonic plates that drive world-
tile elevation properties, not world-tile boundaries.
This means:
- Phase J does not need to be designed for Phase W compatibility.
- Phase W's world grid is rectangular, preserving the grid-first invariant at all scales.
- Voronoi is a *technique applied within maps* (Phase J: biome regions) and *across the
  world* (Phase K: geological plates), not the world-tile structure itself.
- The `MapShapeInput` hook (Phase F2c) remains the integration point between world and
  local scales — a world tile's coastal geometry is injected as a shape mask regardless
  of whether the world grid is rectangular or otherwise.

**Decision 5 — Temperature field ownership:** Part of Phase M. Phase M becomes a
"Climate + Biome" phase that writes three new fields: `MapFieldId.Temperature`,
`MapFieldId.Moisture`, and `MapFieldId.Biome`. Temperature is derived from elevation
(Height field), position-based latitude proxy (Y-axis), and coastal proximity (CoastDist),
following the standard formula: `base_temp - latitude_factor - (elevation × lapse_rate)
+ noise_perturbation`. This matches the canonical biome pipeline from Pass 5a:
"compute temperature → compute moisture → assign biomes (Whittaker lookup)."

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
- F4b Shore Depth Tunable
  - `Stage_Shore2D` gains `ShallowWaterDepth01` field (default 0.0 = original 1-cell ring)
  - When > 0, water cells with Height >= (waterThreshold − depth) also marked ShallowWater
  - Adjacency ring always included. 5 new shore tests.
- F4c Mid-Water Layer
  - New `MapLayerId.MidWater = 12` (append-only, COUNT 12 → 13)
  - `Stage_Shore2D` gains `MidWaterDepth01` field; writes MidWater when > 0
  - `MidWater ⊆ NOT Land`, `MidWater ∩ ShallowWater == ∅`
  - 3-band water depth: ShallowWater (shallowest) → MidWater → DeepWater (deepest)
  - `TilesetConfig` priority: DeepWater → MidWater → ShallowWater → Land → ...
  - MidWater added to base layers and collider layers (non-walkable)
  - New Inspector slider + MapGenerationPreset field. 4 new MidWater tests.
  - Default 0 = no MidWater layer. No golden changes at defaults.
- F5 Vegetation
  - implemented `Stage_Vegetation2D`
  - `Vegetation ⊆ LandInterior`; excludes `HillsL2` peaks; noise-threshold coverage
  - `MapFieldId.Moisture` write deferred to Phase M
  - added F5 stage + pipeline goldens
  - updated lantern with `enableVegetationStage` toggle and `stagesF5` array
- F6 Walkable + Stairs (Traversal)
  - implemented `Stage_Traversal2D`
  - `Walkable` = `(Land OR ShallowWater) AND NOT HillsL2`; `Stairs` = HillsL1/HillsL2 boundary ring
  - `Stairs ⊆ Walkable`; both disjoint from `HillsL2`
  - ShallowWater included in Walkable (post-N2 Issue 3); player can wade in shallow water
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

### Phase H — Extract + Adapters (Visualization)
**Done.**

- `PCGMapVisualization` patched with `PCGViewMode` enum and scalar field visualization:
  - `PCGViewMode` enum: `MaskLayer` (existing binary ON/OFF) / `ScalarField` (normalized color ramp).
  - `viewMode` Inspector field selects active mode; `viewField` + `scalarMin`/`scalarMax` govern scalar view.
  - `PackFromFieldAndUpload`: packs normalized scalar values into the existing GPU float buffer.
    No shader changes — existing maskOffColor/maskOnColor lerp provides the ramp.
  - Per-layer preset ON colors: `useLayerPresetColors` toggle + `layerPresetOnColors Color[12]` array.
- `MapContext2D` extended with additive `GetField(MapFieldId)` method (mirrors `GetLayer`).
- No new `MapLayerId` or `MapFieldId`. All changes are sample-side only.
- Height and CoastDist are now directly visualizable in the lantern.

### Phase H1 — Composite Map Visualization (Editor)
**Done.**

- New `PCGMapCompositeVisualization` sample component alongside the existing single-layer lantern.
- Renders a full overworld-style map in the editor by compositing all active layers into a single
  `Texture2D`, one cell at a time, using a priority-ordered color table.
- Compositing priority (low → high, later entries overwrite earlier):
  DeepWater → ShallowWater → Land → Vegetation → HillsL1 → HillsL2 → Stairs → LandEdge → LandCore
  (exact order and colors to be finalized at implementation time; tuneable via Inspector).
- Scalar fields (Height, CoastDist) optionally blended as tint overlays on top of the layer composite.
- Pure sample-side; no runtime contract or stage changes. Adapters-last invariant preserved.
- Does not replace the single-layer diagnostic lantern (`PCGMapVisualization`); both coexist.
- Provides the "design iteration" view: tweak seed/tunables and immediately see a readable map.
- Prerequisite for intuitive design work on all future phases (Biome coloring, POI markers, etc.).

### Phase H2 — Data Export / Map Adapters
**Done.**

- Completes the "Adapters" half of the original Phase H intent (Phase H covered visualization only).
- `MapDataExport` sealed class: managed snapshot of a completed `MapContext2D`.
  Holds `bool[]` per created layer and `float[]` per created field (row-major, index = x + y * Width).
  Lifetime independent of source context. Access via `HasLayer`/`GetLayer`/`GetCell` and
  `HasField`/`GetField`/`GetValue`. Absent slots return null / throw with a clear message.
- `MapExporter2D` static adapter: reads context, copies native → managed, returns `MapDataExport`.
  Adapters-last: read-only, never writes to context. Exports all present layers/fields automatically.
  Deterministic. Extensible: later phases (Biome, POI, Paths) are automatically exported.
- 14 tests: empty export, layer/field round-trip fidelity, determinism, snapshot independence, guards.
- Key decisions: managed class output (not struct/ScriptableObject); all-present scope; static adapter.
- No new `MapLayerId`, `MapFieldId`, or runtime stage contracts. Adapters-last invariant preserved.

### Phase H2b — Tilemap Adapter
**Done.**

- Natural extension of Phase H2. Provides the first fully playable game map from the pipeline output.
- `TilemapLayerEntry` `[Serializable]` struct: maps one `MapLayerId` to one `TileBase` asset.
- `TilemapAdapter2D` static adapter: reads `MapDataExport`, stamps a Unity `Tilemap` via a
  caller-supplied `TilemapLayerEntry[]` priority table (rendering priority only; low→high;
  last match per cell wins). Parameters: fallbackTile, clearFirst, flipY.
  Absent layers and null tile entries silently skipped. Deterministic.
- `PCGMapTilemapSample` sample MonoBehaviour: runs full pipeline on Start or Inspector context menu.
  Exposes seed, resolution, tunables, priority table in Inspector. Calls Export → Apply.
- Lives in `Runtime/PCG/Adapters/Tilemap/` under `Islands.PCG.Adapters.Tilemap.asmdef`
  (separate from `Islands.PCG.Runtime` to keep headless core Unity.Tilemaps-free).
- Scene: `Runtime/PCG/Samples/PCG Map Tilemap/PCG Map Tilemap.unity`.
- 10 EditMode tests: null guards, empty table, priority resolution, missing layer, clearFirst,
  flipY coordinate mirroring, determinism gate.
- Adapters-last invariant preserved. No new MapLayerId/MapFieldId.
- Key decisions resolved: caller-configurable priority table; separate asmdef; pre-authored tile
  assets; static Apply + thin sample MonoBehaviour.

### Phase H2c — Live Tilemap Visualization
**Done.**

- Natural evolution of `PCGMapTilemapSample` from "generate once on Start" to a live interactive
  editor tool.
- `PCGMapTilemapVisualization` `[ExecuteAlways]` MonoBehaviour: runs the full pipeline on every
  Inspector change (dirty tracking via FNV-1a priority table hash + per-field comparison).
  `MapContext2D` held `Persistent` across frames; reallocated only when resolution changes.
  Console log per rebuild: seed, resolution, stage flags, tilesStamped/total.
- `Islands.PCG.Adapters.Tilemap.asmdef` gains `Unity.Mathematics` reference.
- Pure sample-side. No new runtime contracts or MapLayerId/MapFieldId.

### Phase H2d — Procedural Tile Generation
**Done.**

- `ProceduralTileEntry` `[Serializable]` struct: maps one `MapLayerId` to a solid `Color`.
- `ProceduralTileFactory` static class: generates and caches runtime `Tile` assets from solid colors;
  shared white 1×1 backing sprite (`FilterMode.Point`); `Color32` cache key.
  `BuildPriorityTable` converts a `ProceduralTileEntry[]` into a `TilemapLayerEntry[]`.
  `ClearCache()` releases all cached instances.
- `PCGMapTilemapVisualization` patched: `useProceduralTiles` toggle, `ProceduralTileEntry[]
  proceduralColorTable`, `proceduralFallbackColor`. FNV-1a dirty hash over color table.
  Three `[ContextMenu]` palette presets: Classic (Natural), Prototyping (Debug), Twilight (Moody).
- 13 EditMode tests green. Adapters-last preserved. No new MapLayerId/MapFieldId.

### Phase H3 — Sample Infrastructure (Presets & Configuration)
**Done.**

- `MapGenerationPreset` ScriptableObject (`Runtime/PCG/Samples/Presets/MapGenerationPreset.cs`):
  seed, resolution, stage toggles (Hills/Shore/Veg/Traversal/Morphology), all F2 tunables
  (islandRadius01, waterThreshold01, islandSmoothFrom01, islandSmoothTo01, islandAspectRatio,
  warpAmplitude01), noise settings (noiseCellSize, noiseAmplitude, quantSteps), clearBeforeRun.
  `ToTunables()` produces a `MapTunables2D` from the preset's shape fields.
  Phase N4: noise fields replaced by TerrainNoiseSettings (terrain + warp + heightQuantSteps).
- `TilesetConfig` ScriptableObject (`Runtime/PCG/Adapters/Tilemap/TilesetConfig.cs`):
  one LayerEntry (label + TileBase + enabled toggle) per MapLayerId + fallback TileBase.
  `ToLayerEntries()` converts to `TilemapLayerEntry[]` for TilemapAdapter2D.
  Guards on length mismatch; logs warning and returns null (caller falls back to inline array).
- Both SOs use override-at-resolve pattern on all four visualization/sample components.
  Inline fields remain active and backward compatible when slots are null.
  Tile resolution priority: Procedural > TilesetConfig > inline priority table.
- Architecture improvement: `Islands.PCG.Samples.Shared` asmdef (thin, MapGenerationPreset only)
  + `Islands.PCG.Samples` asmdef (all PCG sample components). Islands.PCG.Runtime is now clean.
- Recommended asset storage:
  MapGenerationPreset .asset files → Runtime/PCG/Samples/Presets/
  TilesetConfig .asset files       → Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/
- 12 EditMode tests green. No new MapLayerId/MapFieldId/runtime contracts. Adapters-last preserved.

### Phase H4 — Animated Tiles
**Done. Sequenced after Phase H3.**

- `TilesetConfig.LayerEntry` extended with an optional `animatedTile` (`TileBase`) field.
  Tile resolution priority in `ToLayerEntries()`: `enabled + animatedTile → animatedTile`;
  `enabled + tile → tile`; `!enabled → skip`. 2 new tests. No golden impact.

### Phase H5 — Multi-layer Tilemap + Collider Integration
**Done. Sequenced after Phase H4.**

- `PCGMapTilemapVisualization` extended with `enableMultiLayer` toggle and three tilemap slots.
  Base layers (DeepWater/MidWater/ShallowWater/Land/LandCore/LandEdge), overlay layers
  (Vegetation/HillsL1/HillsL2/Stairs), collider layers (DeepWater/MidWater/HillsL2).
  TilemapAdapter2D extended with `ApplyLayered(export, TilemapLayerGroup[])`.
  Collider setup: CompositeCollider2D + Rigidbody2D (Static, Simulated).
  Pure adapter/sample-side. No runtime contract changes.

### Phase H7 — Map Navigation Sample
**Done. Sequenced after Phase H5.**

- `MapPlayerController2D` new sample MonoBehaviour: 4-directional top-down character controller.
  Dynamic Rigidbody2D + CircleCollider2D (radius 0.3). Blocked by CompositeCollider2D
  (DeepWater, MidWater, HillsL2). Walks freely on Land, ShallowWater, Vegetation, HillsL1.
- `CameraFollow2D` new sample MonoBehaviour: LateUpdate + Vector3.SmoothDamp follow.
- Pure sample-side. No new runtime contracts. Adapters-last invariant preserved.

### Phase H6 — Rule Tiles / Context-aware Tile Selection
### Done
- `TilesetConfig.LayerEntry` extended with `ruleTile` field (TileBase).
- Tile resolution priority (H6): ruleTile > animatedTile > tile > null.
- `ComputeTilesetConfigHash()` extended to include `ruleTile` InstanceID.
- Rule Tile assets support Animation output per-rule for animated transitions.
- Pure adapter/sample-side. No new runtime stage contracts.

### Post-N2 Fixes (Issues 1–3)
**Done.**

Three issues discovered during N2 integration testing:

- **Issue 1 — Overlay tint white-map fix:** `ApplyScalarOverlayTint` no longer clobbers
  procedural/art tile colors when scalar overlay is disabled. Guarded early-out; reset
  pass via `TileFlags.LockColor` on enabled→disabled transition. Tracked via `_overlayWasApplied`.
- **Issue 2 — Scalar heatmap tilemap:** Dedicated `scalarHeatmapTilemap` slot with 256-step
  quantized color palette and `heatmapAlpha` control (via `Tilemap.color` alpha). Stamps
  solid-color procedural tiles from `ProceduralTileFactory` onto a separate tilemap. Per-cell
  tint fallback preserved when heatmap tilemap is null. `PCGMapTilemapVisualizationEditor`
  extended with `scalarHeatmapTilemap` and `heatmapAlpha` fields.
  Scene setup: heatmap tilemap must be under its own Grid (supports independent cell size);
  sorting order above base tilemap. Reference: `reference/scalar-heatmap-scene-setup.md`.
- **Issue 3 — ShallowWater walkability:** `Stage_Traversal2D` contract changed from
  `Walkable = Land AND NOT HillsL2` to `Walkable = (Land OR ShallowWater) AND NOT HillsL2`.
  Stage now reads `MapLayerId.ShallowWater` (read-only). `Stairs ⊆ Walkable` still holds.
  `ShallowWater` removed from `s_colliderLayers` in `PCGMapTilemapVisualization` (adapter-side).
  F6+ golden hashes updated.
- Issues 1 & 2 are adapter/sample-side only. Issue 3 is a runtime contract change.
  No new `MapLayerId` or `MapFieldId`.

### Phase N4 — Noise Settings Infrastructure + F2 Noise Upgrade
**Done.** Implemented 2026-04-07.

Replaced the manual value-noise approach in `Stage_BaseTerrain2D` (coarse grid of
`ctx.Rng.NextFloat()` values with bilinear interpolation) with proper noise evaluation
from the Islands noise runtime via `MapNoiseBridge2D.FillNoise01`.

- **New `TerrainNoiseSettings` serializable struct** with fields: `noiseType` enum
  (Perlin/Simplex/Value/Worley), `frequency` (1–32), `octaves` (1–6), `lacunarity` (2–4),
  `persistence` (0–1), `amplitude` (0–1). Defaults: Perlin, freq 8, 4 oct, amp 0.35.
- **Separate warp noise settings**: independent `TerrainNoiseSettings` for domain warp.
  Defaults: Perlin, freq 4, 1 oct, amp 1.0 (scaled by `warpAmplitude01`).
- **`heightQuantSteps`** promoted from hardcoded constant to `MapTunables2D` tunable.
- **`MapNoiseBridge2D.FillNoise01`**: new generic dispatch method supporting all four noise
  types. `FillSimplexPerlin01` preserved unchanged for F3/F5 backward compatibility.
- **`Stage_BaseTerrain2D` rewritten**: all `ctx.Rng` consumption eliminated. Three
  `MapNoiseBridge2D.FillNoise01` calls with stage salts. `BilinearSample` removed.
  Noise arrays now at full cell resolution (w×h) instead of coarse grid.
- **All visualization consumers updated**: PCGMapTilemapVisualization, PCGMapCompositeVisualization,
  PCGMapVisualization. MapGenerationPreset extended. Custom Editor updated.
  Heatmap tilemap ghost-tile bug fixed (ClearAllTiles before stamp on resolution change).
- **RNG impact:** ctx.Rng advances zero steps in the entire pipeline. The RNG fragility
  problem is permanently eliminated.
- **Golden impact:** Full break. All hashes change.
- Depends on: existing noise runtime.
- No new `MapLayerId` or `MapFieldId`.

### Phase F3b — Height-Coherent Hills (Clean Break)
**Done.**

Replaces the topology-based `Stage_Hills2D` with height-threshold classification.
Hills are derived directly from the Height field, so mountains appear where terrain is
highest. This resolves the Height/Hills spatial disconnect identified during post-N2
heatmap visualization.

- **New contract:**
  - `HillsL2` = `Land AND Height >= hillsThresholdL2` (impassable mountain peaks)
  - `HillsL1` = `Land AND Height >= hillsThresholdL1 AND NOT HillsL2` (passable slopes)
  - `LandInterior` = `Land AND NOT (4-adjacent to any non-Land cell)` (unchanged)
  - `LandEdge` = `Land AND NOT LandInterior` (unchanged)
  - All existing subset invariants preserved:
    `HillsL2 ⊆ HillsL1-or-HillsL2 ⊆ Land`; `HillsL1 ∩ HillsL2 == ∅`;
    `LandEdge ∩ LandInterior == ∅`; `LandEdge ∪ LandInterior == Land`.
- **New stage-local tunables:**
  - `hillsThresholdL1` (float, 0–1, default ~0.65): Height value above which Land cells
    become HillsL1 slopes.
  - `hillsThresholdL2` (float, 0–1, default ~0.80): Height value above which Land cells
    become HillsL2 peaks. Must be > hillsThresholdL1.
  - Exposed on `MapGenerationPreset` and visualization Inspectors.
- **`MaskTopologyOps2D` and `MapNoiseBridge2D` usage removed from Stage_Hills2D.** The
  stage no longer generates its own noise field or performs topology analysis for hill
  placement. It reads Height (from F2) and classifies cells by threshold. LandInterior
  and LandEdge derivation (4-neighbor boundary detection) remains.
- **RNG consumption:** Zero (continues N4 pattern).
- **Stairs impact:** `Stage_Traversal2D` (F6) already derives Stairs as
  `HillsL1 AND NOT HillsL2 AND 4-adjacent-to-HillsL2`. This contract is unchanged —
  the Stairs ring naturally forms at the height threshold boundary.
- **Golden impact:** All F3+ hashes change. Full re-lock required.
- **Visual smoke test:** Verify the heatmap overlay now visually matches hill placement.
  HillsL2 cells should correspond to the brightest Height values. Adjusting the thresholds
  should visibly move the hill/mountain boundary.
- Depends on: Phase N4 (richer Height field makes threshold-based hills interesting).
- No new `MapLayerId` or `MapFieldId`.

### Phase N5 — Noise & Shape Configuration
**Next. Sequenced after Phase F3b (done), before Phase H8.**

Consolidates four related configuration improvements into one phase. N5.a–c are
independently implementable; N5.b and N5.c have natural synergy (asset format includes
N3 fields, N3 implementation makes them functional). N5.d is independent of a–c.

#### N5.a — Base Shape Selector

Adds an Inspector-facing shape mode enum to `Stage_BaseTerrain2D` / `MapGenerationPreset`,
replacing the current implicit "always ellipse unless F2c shape input is provided" behavior.

- **`IslandShapeMode` enum:** `Ellipse` (current default), `Rectangle`, `NoShape`, `Custom`.
  - **Ellipse:** Current F2b behavior — radial smoothstep falloff + domain warp. Unchanged.
  - **Rectangle:** Axis-aligned rectangle with configurable margin and optional edge smoothing.
    `mask01 = smoothstep(edgeDist)` where edgeDist is min distance to any rectangle edge.
  - **NoShape (raw noise + threshold):** `mask01 = 1.0` for every cell. The height field is
    pure noise; the water threshold alone carves coastlines. Produces continent-like shapes
    entirely from noise — the pattern used by Minecraft and RimWorld for world maps. Simplest
    addition; highest creative impact.
  - **Custom:** Exposes the existing F2c `MapShapeInput` path in the Inspector via a sprite-to-mask
    bridge. Assign a `Texture2D` (or `Sprite`) and it is rasterized to a `MaskGrid2D` at pipeline
    resolution. Enables hand-painted island silhouettes.
- **Inspector integration:** Shape mode enum on `MapGenerationPreset` + all visualization components.
  Ellipse-specific fields (radius, aspect, smoothFrom/To) hidden when mode is not Ellipse.
  Warp settings remain available for all modes (warp can distort any base shape).
- **Parametric shapes (future):** Polar-coordinate shapes (star, blob, n-gon) can be added as
  additional enum values later. Each is a function `(angle, radius) → mask01`.
- Low-medium complexity. No new `MapLayerId` or `MapFieldId`.
- Depends on: Phase N4 (NoShape mode requires the richer noise field to produce interesting terrain).

#### N5.b — Noise Settings Assets (ScriptableObject)

Adds a `NoiseSettingsAsset` ScriptableObject that wraps `TerrainNoiseSettings`, following the
same override-at-resolve pattern as `MapGenerationPreset`: when assigned, the asset's settings
are used; when null, inline Inspector fields are the fallback.

- **`NoiseSettingsAsset`** ScriptableObject containing one `TerrainNoiseSettings` struct.
  Created via `[CreateAssetMenu]`. Reusable across presets and components.
- **`MapGenerationPreset` extended** with two optional `NoiseSettingsAsset` slots:
  `terrainNoiseAsset` and `warpNoiseAsset`. When assigned, `ToTunables()` reads from the
  asset; when null, reads from the existing inline fields (fully backward compatible).
- **All visualization consumers updated** with the same override-at-resolve pattern.
- **Polymorphism evaluation (resolved: not needed).** All noise types share the same base
  parameters (frequency, octaves, lacunarity, persistence, amplitude). Type-specific fields
  are small additions: Worley adds 2 params (distance metric enum, function enum), N3 adds
  3 params (fractalMode, offset, ridgedGain). A single flat struct with conditional Inspector
  visibility (custom PropertyDrawer hides irrelevant fields based on `noiseType`) is simpler,
  more serialization-friendly, and more Inspector-friendly than a SO class hierarchy.
- **`TerrainNoiseSettings` extended** with:
  - `WorleyDistanceMetric` enum (Euclidean, SmoothEuclidean, Chebyshev) — shown when noiseType is
    Worley. Default: Euclidean.
  - `WorleyFunction` enum (F1, F2, F2MinusF1, CellAsIslands) — shown when noiseType is Worley.
    Default: F1.
  - `FractalMode` enum (Standard, Ridged) — shown for all fBm-based types. Default: Standard.
  - `ridgedOffset` (float, default 1.0) — shown when FractalMode is Ridged.
  - `ridgedGain` (float, default 2.0) — shown when FractalMode is Ridged.
- **Custom PropertyDrawer** for `TerrainNoiseSettings`: hides Worley fields when type is not
  Worley; hides ridged fields when fractalMode is Standard. Same drawer used in SO Inspector,
  MapGenerationPreset Inspector, and visualization component Inspectors.
- Medium complexity. No new `MapLayerId` or `MapFieldId`.

#### N5.c — Extended Noise Palette + Ridged Multifractal (N3)

Populates the extended `TerrainNoiseType` enum and implements the N3 ridged multifractal
algorithm in the noise runtime.

- **Extended `TerrainNoiseType` enum** (additions beyond the N4 set):
  - `SmoothWorley` → `Voronoi2D<LatticeNormal, SmoothWorley, F1>` (blended cell boundaries)
  - `Chebyshev` → `Voronoi2D<LatticeNormal, Chebyshev, F1>` (diamond/square cells)
  - `WorleyF2` → `Voronoi2D<..., F2>` (second-nearest distance)
  - `WorleyF2MinusF1` → `Voronoi2D<..., F2MinusF1>` (edge ridges — "cracked earth")
  - `CellAsIslands` → `Voronoi2D<SmoothWorley, CellAsIslands>` (each cell = island plateau)
  Each is one enum entry + one `case` in `MapNoiseBridge2D.FillNoise01`. The Worley sub-options
  from N5.b provide finer control (distance metric × function) for the Worley family.
- **N3 — Ridged Multifractal implementation.** Resolves the long-standing Option A vs C decision
  from the Noise Composition Improvements Roadmap.
  **Decision: Option A — new fractal mode in the noise runtime.**
  `Noise.GetFractalNoise<N>()` gains a `FractalMode` parameter. When `Standard`, behavior is
  unchanged (existing fBm). When `Ridged`, the accumulation loop uses the Musgrave algorithm:
  `signal = (offset - abs(noise))^2`, inter-octave feedback via `weight = clamp(signal * gain, 0, 1)`.
  Canonical defaults: offset=1.0, gain=2.0.
  `Noise.Settings` extended with `FractalMode fractalMode`, `float ridgedOffset`, `float ridgedGain`.
  Standard mode with any noise type produces identical output to pre-N5 (golden safe at defaults).
  `MapNoiseBridge2D.FillNoise01` passes fractalMode through to the runtime.
  ~30 lines of algorithm code in the accumulation loop. SIMD-compatible (mode is per-evaluation,
  not per-lane).
- **CellAsIslands for archipelago generation:** Particularly interesting for Phase J — each Voronoi
  cell becomes a distinct rounded island. Combined with NoShape mode (N5.a), this creates
  natural archipelagos from noise alone without needing explicit multi-island logic.
- Medium complexity. Noise runtime is modified (governed reference surface); changes are additive
  and backward-compatible at default settings.
- Depends on: Phase N4 (bridge infrastructure), N5.b (settings struct carries the new fields).

#### N5.d — Hills Noise Modulation

Adds optional per-cell noise offset to the height-threshold hills classification (F3b),
breaking the 1:1 correspondence between Height contour lines and hill boundaries.

- **New tunable on `MapTunables2D`:** `hillsNoiseBlend` (float, [0..1], default 0.0).
  - 0.0 = pure height-threshold (current F3b behavior, golden-safe).
  - 0.5 = moderate noise modulation — thresholds shift ±noise, producing organic hill
    boundaries that loosely follow height but with irregular edges.
  - 1.0 = maximum noise influence — hills become a blend of height and independent noise.
- **Implementation in `Stage_Hills2D`:** When `hillsNoiseBlend > 0`, fill one noise array
  via `MapNoiseBridge2D.FillNoise01` (new stage salt, configurable frequency/octaves via
  a dedicated `TerrainNoiseSettings` or reusing existing noise settings). Per-cell effective
  thresholds: `effThL1 = thL1 - blend * (noise - 0.5) * range`,
  `effThL2 = thL2 - blend * (noise - 0.5) * range` where `range` controls modulation depth.
- **Contracts unchanged:** HillsL1/L2 subset and disjointness invariants preserved (the
  classification logic is the same, only the threshold values vary per cell).
- **Exposed on** `MapGenerationPreset` + all visualization Inspectors (one float slider).
- Low complexity. No new `MapLayerId` or `MapFieldId`. Independent of N5.a–c.
- Depends on: Phase F3b (done), Phase N4 (MapNoiseBridge2D infrastructure).

### Phase H8 — Mega-Tiles (2×2 Large Terrain Sprites)
**Later. Sequenced after Phase N5.**

Replaces clusters of same-type tiles with large multi-cell sprite groups.
First target: 2×2 HillsL2 clusters → single large mountain sprite.

- **Scan rule:** when a 2×2 block contains 3+ HillsL2 cells, the entire 2×2 block
  is replaced by a 4-quadrant large mountain sprite (TL/TR/BL/BR).
- **Candidate approaches** (to evaluate at implementation):
  - **Rule Tile with Extended Neighbor** — Unity's RuleTile supports extending the
    neighbor check beyond 3×3. Each cell in a qualifying 2×2 cluster gets a rule
    that identifies its quadrant position and assigns the correct sub-sprite.
  - **Adapter pre-pass** — scan the exported HillsL2 mask for 2×2 clusters before
    stamping. Mark qualifying cells with their quadrant, then stamp the large sprite
    parts. Keeps the logic in adapter code rather than Rule Tile config.
  - **Custom TileBase subclass** — a `MegaTile` that overrides `GetTileData` and
    checks its 2×2 neighborhood at render time.
- Extensible beyond mountains: forest groves (2×2 Vegetation), town clusters, etc.
- Pure adapter/sample-side. No new MapLayerId, MapFieldId, or runtime stage contracts.
- Depends on: Phase H6 (Rule Tiles), tileset art with 2×2 large mountain variants.

### Phase T1 — PCG Map Mesh Visualization
**Planning. Design complete. Sequenced on adapter track, parallel to mainline.**
**See [`Phase_T1_Design.md`](design/Phase_T1_Design.md) for detailed design.**

3D mesh visualization adapter for the PCG map pipeline. Reads `MapDataExport` and
produces a Unity `Mesh` with height-displaced vertices and per-layer vertex colors.
Design-iteration companion to the existing tilemap visualization — not a gameplay surface.

- **`MeshColorEntry`** `[Serializable]` struct: maps `MapLayerId` → `Color`. Lives in
  mesh adapter asmdef (independent of tilemap adapter; no inter-adapter coupling).
- **`MeshAdapter2D`** static adapter: reads `MapDataExport`, builds `Mesh`.
  Vertex grid: `(i * cellSize, Height[i,j] * heightScale, j * cellSize)` (XZ plane,
  height in Y — 3D standard). Triangle grid: 2 tris per quad, CCW winding. Vertex
  colors: layer priority, last match wins (same logic as `TilemapAdapter2D`).
  `RecalculateNormals()` + `RecalculateBounds()`. `IndexFormat.UInt32` when > 65535
  vertices. Deterministic. Null export throws. Missing Height → flat mesh.
- **`PCGMapMeshVisualization`** `[ExecuteAlways]` MonoBehaviour: runs full pipeline on
  Inspector change (dirty tracking, same pattern as `PCGMapTilemapVisualization`).
  Inspector: `MeshFilter` target, `MapGenerationPreset` slot (shared with tilemap viz),
  `Material` slot (fallback to `Shader.Find("Unlit/VertexColor")`), `heightScale`,
  `cellSize`, `MeshColorEntry[]` color table, fallback color. Preset-controlled fields
  hidden by custom Editor when preset assigned.
- **`PCGMapMeshVisualizationEditor`** custom Inspector: hides preset-controlled fields.
  Lives in `Islands.PCG.Editor` asmdef (existing).
- **`Islands.PCG.Adapters.Mesh.asmdef`**: references `Islands.PCG.Runtime`,
  `Islands.PCG.Samples.Shared`, `Unity.Mathematics`. Does not reference tilemap adapter.
- Scene: `Runtime/PCG/Samples/PCG Map Mesh/PCG Map Mesh.unity`. Both tilemap and mesh
  viz share the same `MapGenerationPreset`; changing seed updates both simultaneously.
- 11 EditMode tests: null guards, vertex/index counts, determinism, color priority,
  height scaling, cell sizing, UInt32 format.
- Depends on: Phase H2 (MapDataExport), Phase H3 (MapGenerationPreset). Both done.
- Does not block or depend on N4, F3b, or any mainline pipeline phase.
- No new `MapLayerId`, `MapFieldId`, or runtime contracts. Pure adapter/sample-side.
  Adapters-last invariant preserved.

### Phase I
Burst / SIMD upgrades

### Phase I2 — GPU Shader Composite Visualization
**Later (planning only). Depends on Phase I.**

- GPU-based equivalent of the Phase H1 `Texture2D` composite, implemented as a custom shader
  or ShaderGraph that receives all layer data as GPU buffers and blends per-cell colors on the GPU.
- Motivation: the Phase H1 CPU `Texture2D` approach has a per-frame cost proportional to
  map resolution; the GPU path eliminates the CPU readback and upload entirely.
- Natural home under the existing PCG Map ShaderGraph / HLSL infrastructure (governed reference).
- Sequenced after Phase I because: (a) Burst-optimized stage execution reduces the time the CPU
  spends running the pipeline, and (b) the GPU buffer packing pattern from the existing lantern
  is already proven — Phase I2 extends it to multi-layer composite rather than redesigning it.
- Does not change runtime contracts or stage outputs; purely a rendering path upgrade.
- The Phase H1 CPU composite remains available as a fallback for platforms without compute support.
- Design questions to resolve at implementation time: per-layer buffer layout (one bool buffer
  per layer vs. a packed bitmask buffer); shader blending strategy (priority switch vs. additive
  tint); whether scalar field overlays (Height, CoastDist tint) are included in this phase.

### Phase J — Region Generation (static Voronoi)
Planning only.

- Static Voronoi-cell region partitioning *within a local map*, producing irregular biome-scale
  regions that downstream Phase M uses for biome classification. The Voronoi structure is
  invisible to the player — it is the mechanism that determines "this area is forest, that
  area is grassland," not a visible grid or boundary.
- Uses the existing Noise Voronoi support surface
  (`Noise.Voronoi.cs`, `Noise.Voronoi.Distance.cs`, `Noise.Voronoi.Function.cs`).
- Expected implementation path: a new `MapVoronoiBridge2D` (or extended bridge) following
  the `MapNoiseBridge2D` pattern; a new `Stage_Regions2D`; a new append-only
  `MapFieldId.RegionId` storing per-cell integer region identity.
- Does not require promoting Noise to a subsystem SSoT.
  Noise remains governed reference / staged support.
- Prerequisite for Phase K.
- Archipelago support: Phase J can optionally use Voronoi to partition the map into distinct
  land masses (one Voronoi cell = one island), especially when combined with N5.a NoShape mode
  and N5.c CellAsIslands noise.

### Phase J2 — Height Redistribution
**Done.**

- `MapTunables2D.heightRedistributionExponent` (default 1.0, clamped [0.5, 4.0]).
- Applied as `pow(height01, exponent)` inside `Stage_BaseTerrain2D` after quantization,
  before Land threshold. Guarded with `!= 1.0f` for zero-cost default path.
- `BaseTerrainStage_Configurable` consolidated from three duplicate nested classes into
  a single shared class at `Runtime/PCG/Samples/Presets/`.
- `MapGenerationPreset` gains `heightRedistributionExponent`.
- Default 1.0 = identity (all existing goldens unchanged).

### Phase N2 — Spline Remapping
**Done.**

- New `ScalarSpline` readonly struct (`Runtime/PCG/Fields/ScalarSpline.cs`): piecewise-linear
  evaluation, immutable, `IsIdentity` fast-path, `FromAnimationCurve` bridge factory.
- `MapTunables2D.heightRemapSpline` field. `MapGenerationPreset.heightRemapCurve`
  (`AnimationCurve`) bridged via `ScalarSpline.FromAnimationCurve` in `ToTunables()`.
- Applied in `Stage_BaseTerrain2D` after pow() redistribution, before Land threshold.
- Coexists with J2 pow() (pow first, spline second). 22 new EditMode tests.
- Identity default preserves all existing golden hashes.

### Phase K — Plate Tectonics / Geological Structure
Planning / exploratory only.

- Coarser (5–8 cell) static Voronoi partition for tectonic plates at world scale.
- Uses existing Voronoi support surface.
- Convergent boundary → mountain ranges (ridged multifractal from N3/N5.c); divergent
  boundary → rift valleys; transform boundary → offset coastlines.
- Feeds into Phase W (world tile elevation properties).
- Does not require Phase J; operates at a different scale.
- Archipelago intent: multiple islands generated as distinct Voronoi plate fragments,
  each injected via MapShapeInput (F2c).

### Phase L — Hydrology (Rivers & Lakes)
**Planning only.**
**See [`Phase_L_Design.md`](design/Phase_L_Design.md) for detailed design.**

- Fills height depressions via Priority-Flood.
- D8 steepest-descent flow routing.
- Flow accumulation scalar field (`MapFieldId.FlowAccumulation`).
- River mask by thresholding (`MapLayerId.Rivers`).
- Lake detection by CCA on non-land, non-DeepWater cells (`MapLayerId.Lakes`).
- Depends on: F2 (Height field). Optional dependency on Phase K (elevation).

### Phase M — Climate & Biome Classification
**Planning only.**
**See [`Phase_M_Design.md`](design/Phase_M_Design.md) for detailed stage contracts,
sub-stage breakdown, `BiomeDef` struct, Whittaker lookup, invariants, and test plan.**

Writes three new fields:
- `MapFieldId.Temperature` — elevation-based lapse rate + latitude proxy + coast moderation + noise
- `MapFieldId.Moisture` — coast proximity + river proximity (Phase L) + noise
- `MapFieldId.Biome` — Whittaker-style Temperature × Moisture lookup

Sub-stages:
- **Sub-stage M.1 — Temperature field:**
  `base_temp - latitude_factor - (elevation × lapse_rate) + coast_moderation + noise_perturbation`
  No RNG consumption required if noise uses coordinate hashing via MapNoiseBridge2D.

- **Sub-stage M.2 — Moisture field:**
  First authoritative write to `MapFieldId.Moisture` (registered but never written until now).
  CoastDist + FlowAccumulation (Phase L, optional) + noise.

- **Sub-stage M.3 — Biome classification:**
  Whittaker Temperature × Moisture lookup. `BiomeDef[]` code-side table.

- **Downstream effects:**
  - `Stage_Vegetation2D` refactored in Phase M2 for per-biome coverage.
  - `TilesetConfig` extended with biome-conditional entries.
  - Phase N (POI Placement) gains biome suitability constraints.
- Depends on: F2 (Height), F4 (ShallowWater), Phase G (CoastDist),
  optionally Phase J (RegionId), optionally Phase L (FlowAccumulation).

### Phase M2 — Biome-Aware Vegetation & Region Naming
Planning only. Sequenced after Phase M.
**See [`Phase_M2_Design.md`](design/Phase_M2_Design.md) for detailed design.**

- **M2.a — Biome-aware vegetation refactor.**
- **M2.b — Contiguous region detection and naming.**
- Depends on: Phase M, F5.

### Phase N — World-Site / POI Placement
Planning only.

- Produces site-selection masks and placement coordinates for RPG-style POI types.
- Depends on: F6 (Walkable, Stairs), Phase L (Hydrology), Phase M (Biome).

### Phase O — Traversal Network / Paths
Planning only.

- Produces a `Paths` mask connecting POI sites across walkable terrain.
- Depends on: F6, Phase L (Rivers as obstacles), Phase N (POI placement).

### Phase P — Pipeline Validation / World Rejection
**Later (planning only). Recommended before Phase W; can be implemented any time after Phase M.**

- `IMapValidator2D` interface with validate-and-retry loop.
- Depends on: at least Phase M for biome diversity validation.

### Phase W — Hierarchical World-to-Local Generation (Zoom-In)
Planning / exploratory only. Not implementation authority.
**See [`Phase_W_Design.md`](design/Phase_W_Design.md) for detailed design.**

- World map = low-resolution `MapContext2D` rectangular grid.
- `WorldTileContext` handoff struct.
- `MapShapeInput` (F2c) as integration point.
- Dependencies: F2c (done), Phase M, optionally Phase K and Phase L.

## Legacy relationship
Legacy map-generation documents are conceptual reference only for the new pipeline.
The active authoritative direction is masks/fields + adapters-last + deterministic headless stages.
