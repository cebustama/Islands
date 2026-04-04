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
- Phase H5: next
- Phase H6: later (planning only)
- Phase I: later
- Phase I2: later (planning only)
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
  editor tool, matching the experience of `PCGMapCompositeVisualization`.
- New `PCGMapTilemapVisualization` sample component alongside the existing `PCGMapTilemapSample`.
- `[ExecuteAlways]`: regenerates in the Editor without entering Play mode, same pattern as
  `PCGMapVisualization` and `PCGMapCompositeVisualization`.
- Dirty tracking on all tunables: seed, resolution, all `MapTunables2D` fields,
  stage toggles (Hills, Shore, Vegetation, Traversal, Morphology), flipY, priority table
  (FNV-1a hash over LayerId + tile InstanceID), fallbackTile reference.
- Full Inspector exposure: all customization options applied to a live Tilemap output.
  Change any parameter → island regenerates immediately.
- Context kept alive between runs (`Allocator.Persistent`; reallocated only on resolution change).
- Console log on each rebuild: seed + tilesStamped count as informal regression baseline.
- Lives in `Runtime/PCG/Adapters/Tilemap/` — same assembly as H2b. No new asmdef.
- `Islands.PCG.Adapters.Tilemap.asmdef` gains `"Unity.Mathematics"` reference.
- Smoke tests passed. LandCore priority ordering confirmed (must sit above Land, below Vegetation).
- Pure sample-side. No new runtime contracts, no new MapLayerId/MapFieldId.
- Coexists with `PCGMapTilemapSample`.

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
  `enabled + tile → tile`; `enabled + both null → null`; `disabled → null`.
  Backward compatible: existing `.asset` files and all pre-H4 code paths are unaffected.
- `PCGMapTilemapVisualization.ComputeTilesetConfigHash()` extended to include `animatedTile`
  InstanceID per entry in the FNV-1a loop. Inspector edits to the animated tile slot now
  trigger real-time rebuild, matching the existing static tile and enabled-toggle behavior.
- Natural candidates: DeepWater (ocean waves), ShallowWater (coastal ripples), Stairs (shimmer).
  No limit on which layers receive animated tiles — any `LayerEntry` accepts an `AnimatedTile`.
- Requires 2D Tilemap Extras package (`com.unity.2d.tilemap.extras`), installed via Package
  Manager. Asmdef changes not required: `animatedTile` is typed as `TileBase` (engine base type).
- Tile import workflow: per `Documentation~/reference/tileset-import-guide.md`; sprite sheet
  sliced into frames, frames assigned to `AnimatedTile` sprite array in Inspector.
- 3 new EditMode tests green (total `TilesetConfigTests`: 17). Pure adapter/sample-side.
  No new runtime contracts, no new MapLayerId/MapFieldId entries.

### Phase H5 — Multi-layer Tilemap & Collider Integration
**Next. Sequenced after Phase H4.**

Separates pipeline layers across multiple stacked Unity Tilemaps on the same Grid, enabling
transparency overlays and dedicated physics layers. Prerequisite for a physically navigable map.

- **Multi-layer rendering**: separate Tilemaps per logical group on the same Grid:
  - *Base layer* (opaque): DeepWater / ShallowWater / Land / LandCore / LandEdge
  - *Overlay layer* (with transparency support): Vegetation / HillsL1 / HillsL2 / Stairs
  - *Collider layer* (invisible, physics only): non-walkable cells (HillsL2 + water)
  - Vegetation trees and hills render *over* the base terrain tile rather than replacing it,
    matching the DW / classic JRPG aesthetic more faithfully and enabling visual layering.
- **`TilemapAdapter2D` extension**: new `ApplyLayered()` overload accepting a descriptor that
  maps each logical layer group to its target `Tilemap`. Adapters-last invariant preserved;
  still reads `MapDataExport` only, never touches `MapContext2D`.
- **Tilemap Collider integration**: auto-setup `TilemapCollider2D` + `CompositeCollider2D` on
  the collider Tilemap, driven by the non-walkable mask (HillsL2 + water) from the export.
  First step toward a physically navigable game map.
- Design decisions to resolve at implementation time: exact layer grouping; transparency shader
  requirements (URP Lit vs. Unlit); whether the collider tilemap is authored or generated.
- Pure adapter/sample-side. No new runtime contracts or MapLayerId/MapFieldId entries.

### Phase H6 — Rule Tiles / Context-aware Tile Selection
**Later (planning only). Sequenced after Phase H5.**

Replaces flat single-sprite tiles with context-aware variants that select the correct sprite based
on neighbor tile types, the single largest visual quality jump available without changing the pipeline.

- Unity's `RuleTile` (from 2D Tilemap Extras): define neighbor-matching rules per tile type;
  Unity applies the correct sprite variant at stamp time.
- Highest impact on: ShallowWater/Land boundary (beach corner and edge transitions), Vegetation
  clusters (interior vs. edge vs. isolated tree), HillsL1/HillsL2 (slope directionality).
- Requires additional sprite variants per tile type (e.g. DW-style corner and edge art for each
  terrain type). Significant art-production dependency — deferred until art direction is settled.
- `TilesetConfig` SO (Phase H3) extended with a `RuleTile` asset slot per layer entry.
- Pipeline and adapter code changes are small; the art requirement dominates the schedule.
- Pure adapter/sample-side. No new runtime contracts.

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
- Downstream adapter extension: after Phase M, `TilesetConfig` (Phase H3) can be extended with
  biome-conditional tile entries — same layer, different tile per biome region. Unlocks visually
  distinct island regions (tropical, temperate, arid) from a single pipeline run.

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
