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
**Done. Sequenced after Phase H4.**

Separates pipeline layers across multiple stacked Unity Tilemaps on the same Grid, enabling
transparency overlays and dedicated physics layers. Prerequisite for a physically navigable map.

- **New type `TilemapLayerGroup`** (`Runtime/PCG/Adapters/Tilemap/TilemapLayerGroup.cs`):
  `[Serializable]` sealed class. Fields: `Tilemap`, `PriorityTable` (`TilemapLayerEntry[]`),
  `FallbackTile`, `ClearFirst` (default true), `FlipY`. Groups with null Tilemap or null
  PriorityTable are silently skipped by `ApplyLayered`.
- **`TilemapAdapter2D.ApplyLayered(MapDataExport, TilemapLayerGroup[])`**: iterates groups,
  calls existing `Apply()` per group. Null group elements silently skipped. Null export or
  groups array throws `ArgumentNullException`. Adapters-last invariant preserved.
- **`TilemapAdapter2D.SetupCollider(Tilemap)`**: idempotent — adds `Rigidbody2D` (Static),
  `TilemapCollider2D`, and `CompositeCollider2D` to the collider Tilemap's GameObject if
  not already present. Wires `usedByComposite = true`. Safe to call on every rebuild.
- **`PCGMapTilemapVisualization` patched** (H5 multi-layer section):
  - New Inspector fields: `enableMultiLayer` (bool), `overlayTilemap`, `colliderTilemap`,
    `colliderTile` (TileBase), `enableColliderAutoSetup` (bool).
  - Dirty tracking: 5 new cached fields; `ParamsChanged()` extended.
  - `StampMultiLayer()`: builds base / overlay / collider `TilemapLayerGroup` instances using
    static partition arrays, calls `ApplyLayered`.
  - `FilterTable(source, ids)`: filters the resolved priority table to a layer-id subset,
    preserving order. Used to route base vs. overlay layers to their respective Tilemaps.
  - `BuildColliderTable(tile, ids)`: maps all non-walkable layer IDs to a single sentinel tile.
  - Single-tilemap path (`enableMultiLayer = false`) is fully backward compatible.
  - Console log extended: `multiLayer={enableMultiLayer}` added.
  - Layer partition: Base = {DeepWater, ShallowWater, Land, LandCore, LandEdge};
    Overlay = {Vegetation, HillsL1, HillsL2, Stairs};
    Collider = {DeepWater, ShallowWater, HillsL2}.
- 4 new EditMode tests green (`TilemapAdapter2DTests.cs`, now 11 total):
  - `ApplyLayered_NullExport_ThrowsArgumentNullException`
  - `ApplyLayered_NullGroups_ThrowsArgumentNullException`
  - `ApplyLayered_NullTilemapInGroup_IsSkippedSilently`
  - `ApplyLayered_TwoGroups_StampIndependentLayers`
- Pure adapter/sample-side. No new runtime contracts, no new MapLayerId/MapFieldId entries.
  Adapters-last invariant preserved.

### Phase H7 — Map Navigation Sample
**Done. Sequenced after Phase H5.**

Adds a minimal playable character to the PCG map tilemap scene to validate end-to-end
integration: pipeline → tilemap layers → physics colliders → character movement. No new
pipeline stages, MapLayerId, or MapFieldId. Pure sample-side.

- **`MapPlayerController2D`** new sample MonoBehaviour
  (`Runtime/PCG/Samples/PCG Map Tilemap/MapPlayerController2D.cs`):
  - `Rigidbody2D`-based 4-directional movement (Dynamic body, `GravityScale = 0`).
  - `Update` reads `Input.GetAxisRaw`; `FixedUpdate` sets `rb.linearVelocity`.
  - Inspector tunables: `moveSpeed` (default 5f), optional `SpriteRenderer` for
    horizontal flip, optional `frontSprite`/`backSprite` for North/South facing.
  - **Resolved at implementation:** Dynamic body + `linearVelocity` (not Kinematic +
    `MovePosition`) — natural collision response against CompositeCollider2D.
  - **Resolved:** `CircleCollider2D` (radius 0.3) — smooth wall sliding, no corner catch.
- **`CameraFollow2D`** new sample MonoBehaviour
  (`Runtime/PCG/Samples/PCG Map Tilemap/CameraFollow2D.cs`):
  - Simple `LateUpdate` + `Vector3.SmoothDamp` follow.
  - **Resolved:** No Cinemachine dependency — avoids a package dep for a sample scene.
- **Scene setup** (documented in `reference/map-tilemap-scene-setup.md`):
  - Player GameObject: `Rigidbody2D` (Dynamic, GravityScale=0, FreezeRotation Z) +
    `CircleCollider2D` + `MapPlayerController2D`.
  - Physics layers: `Player` ↔ `MapCollider` enabled; all other cross-layer pairs disabled.
  - Collider Tilemap: H5 `SetupCollider()` — non-walkable cells (DeepWater, ShallowWater,
    HillsL2) carry the sentinel tile and drive `CompositeCollider2D`.
- **Validation targets** (confirmed by smoke test):
  - Player blocked by water and mountain (HillsL2) — H5 collision layer works.
  - Player walks freely on land — F6 Walkable mask correctly excluded from collider.
  - Seed change regenerates map and collision shapes update without restarting Play.
  - Camera tracks player smoothly.
  - Diagonal wall sliding works without corner-catching.
- **No EditMode tests** — navigation is inherently a PlayMode concern. Smoke-test checklist
  (7 items) replaces unit tests.
- Pure sample-side. No new runtime contracts. Adapters-last invariant preserved.

### Phase H6 — Rule Tiles / Context-aware Tile Selection
### Done
- `TilesetConfig.LayerEntry` extended with `ruleTile` field (TileBase).
- Tile resolution priority (H6): ruleTile > animatedTile > tile > null.
- `ComputeTilesetConfigHash()` extended to include `ruleTile` InstanceID.
- Rule Tile assets support Animation output per-rule for animated transitions.
- Pure adapter/sample-side. No new runtime stage contracts.

### Phase H8 — Mega-Tiles (2×2 Large Terrain Sprites)
**Later (planning only).**

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
- **Relationship to Phase W (resolved):** Voronoi cells are *not* world tiles. Phase J
  operates within a local map to create biome regions. Phase W's world map uses a rectangular
  grid. These are separate concepts at separate scales. Phase J does not need to be designed
  for Phase W compatibility.
- Archipelago support (Level 3 of the island shape vision): multiple disconnected land masses
  can be produced by compositing multiple Phase F2c shape inputs before the Land threshold pass,
  or by running independent per-island pipelines and merging outputs. Voronoi cell boundaries
  from Phase J are a natural driver for multi-island layout.

### Phase J2 — Height Redistribution
**Later (planning only). Can be implemented any time after current H-series.**

Applies a non-linear power curve to the Height field to reshape elevation distribution,
concentrating detail at low elevations (broader plains) while allowing dramatic peaks.

- New optional tunable on `MapTunables2D`: `heightRedistributionExponent` (default 1.0 = no
  change, clamped [0.5, 4.0]). Applied as `pow(height01, exponent)` after the Height field
  is written by `Stage_BaseTerrain2D`.
- Exponents > 1.0 flatten lowlands and steepen peaks; < 1.0 does the reverse.
- Dwarf Fortress uses a "non-linear parabola" for the same purpose (confirmed by Tarn Adams).
  Pass 2a documents this as power redistribution: `pow(e·1.2, exponent)` with exponent 2.5–3.0.
- Implementation: a new `Stage_HeightRedistribution2D` or an inline post-processing step in
  `Stage_BaseTerrain2D`. Deterministic, no RNG, no new MapLayerId/MapFieldId.
- Low cost, high visual impact. Single-line math transformation.
- Golden hashes for all downstream stages will change when exponent ≠ 1.0.
  Default 1.0 preserves existing goldens.

### Phase K — Simulation / Dynamics (Plate Tectonics)
Planning / exploratory only. Not implementation authority.

- Depends on Phase J (Voronoi region partitioning) as the static foundation.
- Uses a *coarser* Voronoi partition than Phase J (5–8 cells) to define tectonic plates
  at geological scale. This is a separate Voronoi pass from Phase J's biome regions —
  it operates at world scale to shape continent/island geometry and mountain placement,
  not within a local map for biome boundaries.
- Initial intent: simple plate-movement simulation derived from Voronoi cells,
  with collision zones used to drive elevation or hills-layer outputs via the existing
  layered pipeline contracts.
- Dynamic / simulation work is not scoped or sequenced beyond this intent statement.
- This phase may be subdivided or deferred as Phase J matures.
- Plate-derived landmasses can be injected into the pipeline via the Phase F2c shape-input
  mechanism, enabling plate collision zones to drive both terrain silhouettes and hill layers.

### Phase L — Hydrology
Planning only.

- **Rivers:** flow-path generation from high elevation toward coast.
  Implementation pipeline (from Pass 2b): Priority-Flood depression filling (Barnes 2014,
  +ε for gradient preservation) on the Height field → D8 flow directions (steepest downslope
  neighbor) → flow accumulation (traverse highest-to-lowest, sum upstream counts) → threshold
  to extract river segments.
  - **Outputs (resolved):**
    - `MapFieldId.FlowAccumulation` — scalar field encoding upstream drainage area per cell.
      Primary simulation output. Consumed by Phase M for moisture derivation.
    - `MapLayerId.Rivers` — binary mask derived by thresholding FlowAccumulation.
      Primary rendering/gameplay output. Multiple threshold levels may produce river width
      variants (stream vs. river) at adapter time.
  - `MapFieldId.FlowDirection` (D8 direction enum per cell) is computed during this phase
    but not persisted as a registered field. It is an intermediate data structure consumed
    only within Stage_Rivers2D. If a downstream consumer later requires it, it can be
    promoted to a registered field at that time.
- **Lakes (resolved):** Distinct `MapLayerId.Lakes` mask layer.
  Detection: connected-component analysis on NOT-Land cells that are not DeepWater
  (border-connected) and not ShallowWater (land-adjacent ring). Any remaining water
  component is a lake. Uses existing `MaskFloodFillOps2D` infrastructure.
  Lakes have distinct gameplay semantics from coastal ShallowWater: freshwater source,
  settlement proximity bonus, fishing, visual variety.
- Depends on: `Height` field (F2), `Land` + `DeepWater` + `ShallowWater` (F2/F4),
  `MaskFloodFillOps2D` (F2).
- New append-only IDs: `MapLayerId.Rivers`, `MapLayerId.Lakes`,
  `MapFieldId.FlowAccumulation`.

### Phase M — Climate & Biome Classification
Planning only. **See [`Phase_M_Design.md`](design/Phase_M_Design.md) for detailed
stage contracts, data structures, Whittaker table, invariants, and test plan.**

Phase M is the "Climate + Biome" phase. It produces three new scalar fields (Temperature,
Moisture, Biome) and enables all downstream biome-aware work. This is the single most
impactful phase for world variety — it transforms a monochrome island into a multi-region
landscape with distinct ecological character.

- **Sub-stage M.1 — Temperature field:**
  New append-only `MapFieldId.Temperature`. Derived from:
  - Height field (elevation-based lapse rate: ~6.5°C per 1,000m equivalent)
  - Y-axis position as latitude proxy (top of map = cold, bottom = warm, or configurable)
  - CoastDist (maritime moderation: coastal cells have more moderate temperatures)
  - Optional noise perturbation for local variation
  - Formula: `base_temp - latitude_factor - (elevation × lapse_rate)
    + coast_moderation + noise_perturbation`
  - No RNG consumption required if noise uses coordinate hashing via MapNoiseBridge2D.

- **Sub-stage M.2 — Moisture field:**
  First authoritative write to `MapFieldId.Moisture` (registered but never written until now).
  Derived from:
  - CoastDist (coastal cells wetter, inland cells drier)
  - FlowAccumulation from Phase L (proximity to rivers increases moisture)
  - Optional noise perturbation for local variation
  - If Phase L is not yet implemented, moisture falls back to CoastDist + noise only.
  - Phase L dependency is optional — Phase M can run without rivers, producing less
    ecologically rich but still functional moisture values.

- **Sub-stage M.3 — Biome classification:**
  New append-only `MapFieldId.Biome`. **Output format (resolved):** integer scalar field
  where each cell stores a biome ID (integer encoded as float). A `BiomeDef[]` lookup table
  (initially a code-side array; may be promoted to ScriptableObject later) defines biome
  properties: name, vegetation density multiplier, tile palette reference, etc.
  Classification uses a Whittaker-style Temperature × Moisture lookup table, with Height
  for altitude-band overrides (alpine, beach) and optionally RegionId from Phase J for
  macro-region coherence. If Phase J is not yet implemented, biome classification operates
  per-cell from climate fields alone — noisier boundaries but functional.
  - Phase J dependency is optional — Phase M can run without Voronoi regions.

- **Downstream effects:**
  - `Stage_Vegetation2D` (F5) should be refactored after Phase M to accept biome ID and
    moisture as additional inputs, replacing the single noise threshold with per-biome
    coverage parameters. This is Phase M2 (see below).
  - `TilesetConfig` (Phase H3) can be extended with biome-conditional tile entries:
    same MapLayerId, different tile per biome region. Unlocks visually distinct island
    regions (tropical, temperate, arid) from a single pipeline run.
  - Phase N (POI Placement) gains biome-based suitability constraints.
- Depends on: F2 (Height), F4 (ShallowWater for shore detection), Phase G (CoastDist),
  optionally Phase J (RegionId), optionally Phase L (FlowAccumulation for moisture).
- New append-only IDs: `MapFieldId.Temperature`, `MapFieldId.Biome`.
  `MapFieldId.Moisture` is already registered — Phase M is its confirmed write owner.

### Phase M2 — Biome-Aware Vegetation & Region Naming
Planning only. Sequenced after Phase M.
**See [`Phase_M2_Design.md`](design/Phase_M2_Design.md) for detailed stage contracts,
pipeline reordering rationale, `ScalarFieldCcaOps2D` operator, `RegionMetadata` struct,
invariants, and test plan.**

Two independent sub-tasks that consume Phase M's biome output:

- **M2.a — Biome-aware vegetation refactor:**
  Moves `Stage_Vegetation2D` after `Stage_Biome2D` in the pipeline (no intermediate stage
  reads Vegetation). Replaces the single global noise threshold (0.40f) with per-biome
  `BiomeDef.vegetationDensity`. Same noise salt and spatial pattern; only the threshold
  changes per cell. No new MapLayerId/MapFieldId.

- **M2.b — Contiguous region detection and naming:**
  CCA (flood fill, 4-connected) on same-biome cells produces labeled contiguous regions.
  Small regions (< `minRegionArea` tunable, default 8 cells) merged into largest adjacent
  neighbor. Region naming is adapter-side, not pipeline.
  - New operator: `ScalarFieldCcaOps2D` (multi-label CCA on scalar fields).
  - New append-only ID: `MapFieldId.NamedRegionId = 5` (COUNT → 6).
  - Output: `RegionMetadata[]` table (region ID → biome type, area, centroid, bounding box),
    attached to `MapContext2D` via metadata bag.

- Depends on: Phase M (Biome, Moisture fields), F5 (Stage_Vegetation2D, modified in place).

### Phase N — World-Site / POI Placement
Planning only.

- Produces site-selection masks and placement coordinates for RPG-style POI types:
  coastal villages/cities (suitability: `LandEdge` / ShallowWater-adjacent land),
  forest dungeons (suitability: `Vegetation` + forest biome from Phase M),
  cave/mine entrances (suitability: `Stairs` — HillsL1/HillsL2 boundary cells),
  lakeside settlements (suitability: `Lakes`-adjacent land from Phase L),
  open-plains camps (suitability: `Walkable AND NOT HillsL1 AND NOT Vegetation`), etc.
- Uses suitability / exclusion logic derived from terrain layers
  (`Land`, `Hills`, `Shore`, `Walkable`, `Stairs`, biome outputs, etc.).
- Outputs are headless placement descriptors: typed coordinates and masks, not GameObjects
  or scene content. Adapters-last boundary must be preserved.
- Downstream adapters (existing dungeon layout generators, future village / ruin generators)
  consume these placement outputs as adapters. Those generators remain layout strategies /
  adapters, not core pipeline stages.
- Depends on: F6 (Walkable, Stairs), Phase L (Hydrology — Rivers, Lakes), Phase M (Biome).

### Phase O — Traversal Network / Paths
Planning only.

- Produces a `Paths` mask connecting POI sites across walkable terrain.
  `MapLayerId.Paths = 5` is already registered in `MapIds2D`; ownership deferred to this phase.
- Algorithm options (to be resolved at implementation time): A* or flood-corridor on `Walkable`,
  noise-corridor traces seeded from POI coordinate pairs, skeleton extraction, etc.
- Rivers from Phase L are obstacles that paths must bridge or avoid — path cost should
  increase at River cells, with optional bridge placement at crossing points.
- POI anchor coordinates from Phase N are the required inputs; implementing Paths before Phase N
  would produce paths with no meaningful endpoints.
- Depends on: F6 (Walkable, Stairs), Phase L (Rivers as obstacles), Phase N (POI placement).

### Phase P — Pipeline Validation / World Rejection
**Later (planning only). Recommended before Phase W; can be implemented any time after Phase M.**

Adds a validate-and-retry mechanism to the pipeline runner, ensuring generated maps meet
minimum quality requirements before being accepted. Only Dwarf Fortress implements this
among the six researched games, but it is critical for any game that needs design-compliant
output — the probability of degenerate maps increases as the pipeline gains more stages.

- New `IMapValidator2D` interface with `bool Validate(MapContext2D ctx)` method.
- Initial validators (composable, run in sequence):
  - Minimum land percentage (e.g., ≥ 15% of total cells are Land)
  - Minimum biome diversity (e.g., ≥ 2 distinct biome types present, after Phase M)
  - Connectivity check (all Land cells reachable — single connected landmass, or all
    landmasses have minimum area threshold)
- `MapPipelineRunner2D` extended with an optional validate-or-retry loop:
  max N attempts (tunable, default 10), incrementing the seed on each retry.
  Same seed always produces the same map (determinism preserved per seed).
  If all attempts fail, the last result is returned with a logged warning.
- The user-facing seed may differ from the internal seed if rejection occurs.
  This is documented behavior, matching DF's approach.
- No new MapLayerId/MapFieldId. Infrastructure-level change to the runner.

### Phase W — Hierarchical World-to-Local Generation (Zoom-In)
Planning / exploratory only. Not implementation authority.
**See [`Phase_W_Design.md`](design/Phase_W_Design.md) for detailed architectural design,
`WorldTileContext` struct, world map structure, and Phase J/K/M interaction model.**

Enables selecting a tile on an overworld map and generating a full-resolution local map
parametrised by that tile's world-scale properties — the zoom-in pattern used by RimWorld
and Dwarf Fortress. This is the architectural prerequisite for any game that distinguishes
a world view from a playable local map.

- **World map structure (resolved):** rectangular grid — a low-resolution `MapContext2D`.
  Preserves grid-first invariant at all scales.
- **`WorldTileContext` handoff struct:** carries LocalSeed, ElevationEnvelope, ShorelineMask,
  BiomeType, HillinessIntensity, MoistureLevel, TemperatureLevel per world tile.
- **Architectural hook:** `MapShapeInput` (Phase F2c) is the primary integration point.
- **Relationship to Phase J and K (resolved):** Three independent spatial structures at
  three scales — world grid (W), local Voronoi (J), geological Voronoi (K).
- **Boundary matching:** Deferred to Phase W2.
- Dependencies: Phase F2c (done), Phase M (biome data), optionally Phase K and Phase L.

## Legacy relationship
Legacy map-generation documents are conceptual reference only for the new pipeline.
The active authoritative direction is masks/fields + adapters-last + deterministic headless stages.
