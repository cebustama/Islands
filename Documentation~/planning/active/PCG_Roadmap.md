# Islands.PCG — Active Roadmap

Status: Active planning  
Authority: Planning only (not implementation truth)  
Scope: Sequencing and future work for the new Islands.PCG pipeline.

## Rule
This document is not implementation authority.
Implemented truth lives in subsystem SSoTs and governed reference/support docs where explicitly assigned.

## Prioritisation posture (stated 2026-08-19)

Islands.PCG is in design and implementation. It is not in production and no shipped
content depends on any particular map. Therefore:

- **Pipeline quality, expressiveness and interesting output take priority over the
  stability of previously generated maps.** "This breaks the goldens" is not, by itself, an
  argument against a change. A golden is a change detector, not a desirable property: it
  reports that something moved and leaves the judgement to us. Re-anchoring goldens is
  mechanical work and is costed as such.
- **This does not relax determinism.** Same seed + same tunables + same version ⇒ same map
  remains a hard invariant. The two are often confused and are opposites in effect:
  determinism is precisely what makes breaking old maps cheap. Without it you cannot
  reproduce a bug you saw, cannot compare two configurations, and cannot measure whether a
  change improved anything. The W-aux.d diagnosis depended entirely on being able to
  re-derive one specific field from one specific seed, and its validation depended on
  re-running the same pipeline at three.
- **Proposals must state which kind of objection they are answering.** When an option is
  rejected, the rejection is labelled either *expressiveness* (a design judgement, open to
  argument) or *re-anchoring cost* (no longer a valid reason on its own). Example from
  W-aux.d: per-biome quantiles were rejected on expressiveness — they destroy the contrast
  between biomes, so a forest would stop reading as greener than a tundra — not because of
  their golden impact.
- Scope discipline still applies. This posture licenses *ambitious* batches, not *wide*
  ones. One problem per batch remains the rule.

## Standing thread — parameter legibility

Not a batch. A named thread, so the finding does not decay into folklore. Pipeline
parameters do not have legible effects, and preset calibration is done by turning knobs and
looking, so a knob that lies costs a full measure-and-diagnose cycle each time.

Four independent, measured demonstrations (2026-08-19):

| Parameter | What it promises | What it did (measured) |
|---|---|---|
| `terrainAmp` | more interior relief | non-monotone: past `1/(1+amp/2)` of mask, more amplitude meant more clamp saturation — a *flatter* top. **Cause removed by W-aux.f.** |
| `heightRedistributionExponent`, `heightRemapCurve` | reshape the height distribution | were inert over the saturated set — monotone with `f(1)=1`, and a collapsed pre-image cannot be re-separated. **Effective again after W-aux.f.** |
| `vegetationDensity` | per-cell vegetation rate | absolute threshold against a narrow noise distribution: 95.5 % coverage at 0.65, 0.26 % at 0.25. **Fixed by W-aux.d** (global quantile cut); the per-biome caveat M2a-9(d) is the residue. |
| `biomeBaseTemperature` | land temperature | land ceiling is `base − lapse·waterThreshold01`, three coupled parameters; none of the three names the coupling. **Still true.** |

**Delivery mechanism: X1 diagnostics rules.** Three of the four are now surfaced
automatically at authoring time (`R4`, `R6`, `R5` respectively) instead of costing a
measure-and-diagnose cycle. Future legibility findings therefore have an obvious home: a new
rule in `MapGenerationPresetDiagnostics`, labelled `measured` or `inferred` per the X1
standing constraint. Two of the shipped rules now carry historical backing after W-aux.d and
W-aux.f — re-basing them is unscheduled work on this thread, not a defect.

Structural causes identified, both documented in `CURRENT_STATE.md`: multiplicative
composition against a hard ceiling (resolved in W-aux.f) and derived-from-derived thresholds
(the N5.e hills remap, which is why the `derived` JSON block exists at all). A cheap
concrete step, if the thread is ever picked up: extend that `derived` block with
`landTemperatureCeiling` and `reachableWhittakerRows`, both computable from fields already
present, both additive and golden-neutral.

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
- Phase H8: done
- Phase H8b: optional (planning only)
- Phase F4b: done
- Phase F4c: done
- Phase N2: done
- Phase N4: done
- Phase F3b: done
- Phase N5: done
  - N5.a: done
  - N5.b: done
  - N5.c: done
  - N5.d: done
  - N5.e: done
- Phase N6: done
- Phase I: later
- Phase I2: later (planning only)
- Phase J: later (planning only)
- Phase J2: later (planning only — new)
- Phase K: later (planning / exploratory only)
- Phase L: done
- Phase M: done
- M-fix.a: done (Biome Tunables Inspector Wiring + M-fix.c folded in)
- M-fix.b: resolved (deferred by design — no downstream consumer needs EffectiveElevation)
- M-fix.c: done (folded into M-fix.a)
- Phase M2: done (M2.a + M2.b complete, all golden-captured)
- Phase N: later (planning only)
- Phase O: later (planning only)
- Phase P: paused (resumes with rubric derived from Phase W output)
- Phase T1: planning (design complete — adapter track)
- Phase W: active (W.a complete 2026-08-09; W-aux.a…W-aux.g closed 2026-08-17…20;
  next: W.b — scope defined 2026-08-20)
  - W-aux.a: done (measurement — statistics exporter + measured preset baselines)
  - W-aux.b: done (submarine relief — first core change of the milestone, identity by default)
  - W-aux.c: done (calibration instrumentation, preset wizard, per-biome vegetation policy)
  - W-aux.d: done (vegetation quantile mapping — goldens re-anchored by design)
  - W-aux.e: done (threshold-mapping audit — measurement only, no code change)
  - W-aux.f: done (height ceiling de-saturation — 29 goldens re-anchored, land topology identical)
  - W-aux.g: done (hills window recalibration — area-quantile thresholds, 10 goldens re-anchored)
- Phase V: done (V.a + V.b — read-only inspection tooling, all smoke-validated)
- Phase Q: done (adapter-side biome-conditional tile selection; Q-fix.a + Q-aux.a folded in)
- Phase Q2: planning (sibling of Q — composite-condition tile selection; adapter track)
- Phase X1: active (authoring track — X1.a complete 2026-08-19; X1.b deferred)
  - X1.a: done (preset diagnostics + preset diff, Editor-only, golden-neutral)
  - X1.b: deferred (objective-driven guidance — blocked on measured runs beyond
    vegetation and height)

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
test plans) have dedicated design documents under `planning/active/`. These carry
planning authority for their phase — the same authority level as this roadmap (not
implementation truth until built and recorded in the SSoT).

Phases that are simple enough to describe in a few bullet points remain inline here.
A phase entry in this roadmap always includes intent, status, dependencies, and a pointer
to its design doc if one exists. The design doc contains the implementation-depth detail.

| Phase | Design document | Status |
|-------|----------------|--------|
| Phase M | [`Phase_M_Design.md`](Phase_M_Design.md) | Complete |
| Phase W | [`Phase_W_Design.md`](Phase_W_Design.md) | Complete |
| Phase M2 | [`Phase_M2_Design.md`](Phase_M2_Design.md) | Complete (M2.a + M2.b) |
| Phase L | [`Phase_L_Design.md`](Phase_L_Design.md) | Complete |
| Phase T1 | [`Phase_T1_Design.md`](Phase_T1_Design.md) | Complete |
| Phase H8 | [`Phase_H8_Design.md`](Phase_H8_Design.md) | Complete (implemented) |
| Phase V | [`Phase_V_Design.md`](Phase_V_Design.md) | Complete (V.a + V.b implemented) |
| Phase Q | [`Phase_Q_Design.md`](Phase_Q_Design.md) | Complete — implemented |

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

> **Partially superseded (W-aux.g, 2026-08-20).** The classifier described below is unchanged,
> but the *tunables* are not: `hillsThresholdL1` / `hillsThresholdL2` no longer exist as raw
> Height values. F3b′ replaced them with `hillsL1` / `hillsL2` as area fractions of `Land`,
> resolved to per-run thresholds by an order statistic. Read the threshold contract in
> `map-pipeline-by-layers-ssot.md` §F3b′, not the tunable descriptions below.

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
- **Measured limitation and its resolution (W-aux.e → W-aux.f).** Between 2026-08-19 and
  2026-08-20 the peak-fraction target was *unreachable*, and the reason was not the
  classifier. `hillsL1/L2` are fractions of the height *range* (N5.e remap), never fractions
  of land area, and the field they cut carried a degenerate atom at `Height == 1.0` holding
  3.7–11.7 % of `Land` depending on seed: the realized peak fraction spanned 17.6–33.1 % at
  fixed thresholds, and no threshold could express a peak fraction below the atom. Treating
  the 15–30 % window as a specification during that period would have sent a reader hunting
  for a bug in a correct classifier.
  **W-aux.f removed the atom, and W-aux.g resolved the window.** After W-aux.f, fixed
  thresholds still measured 9.86 / 9.36 / 2.60 % of `Land` at seeds 56 / 8 / 243 — a 3.8×
  spread, because the seed-varying height maximum leaves dead space at the top of the range
  the fractions are taken over. W-aux.g replaced the range remap with per-run area quantiles;
  the band now measures 20.13 / 20.30 / 20.20 % against a 20 % target. The threshold contract
  is `map-pipeline-by-layers-ssot.md` §F3b′; this bullet is history.
- Depends on: Phase N4 (richer Height field makes threshold-based hills interesting).
- No new `MapLayerId` or `MapFieldId`.

### Phase N5 — Noise & Shape Configuration
**Done. N5.a–e complete. Sequenced after Phase F3b (done).**

Consolidates four related configuration improvements into one phase. N5.a–c are
independently implementable; N5.b and N5.c have natural synergy (asset format includes
N3 fields, N3 implementation makes them functional). N5.d is independent of a–c.

#### N5.a — Base Shape Selector
**Done.**

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
**Done.**

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
**Done.**

Makes the N5.b `TerrainNoiseSettings` struct fields functional in the noise runtime.
Implements the N3 ridged multifractal algorithm.

- **Parameterized Worley dispatch** (design decision: parameterized family, not separate enum
  entries). The existing `TerrainNoiseType.Worley` enum entry is driven by
  `WorleyDistanceMetric` (Euclidean, SmoothEuclidean, Chebyshev) × `WorleyFunction`
  (F1, F2, F2MinusF1, CellAsIslands) = 12 generic `Voronoi2D<>` instantiations dispatched
  via `MapNoiseBridge2D.FillWorleyNoise01` (flat switch on `metric * 4 + function`).
  No new `TerrainNoiseType` enum entries. All 12 combinations accessible from Inspector via
  the two Worley dropdowns added in N5.b. Default (Euclidean + F1) = pre-N5.c Worley case.
- **N3 — Ridged Multifractal implementation.** Resolves the Option A vs C decision from the
  Noise Composition Improvements Roadmap (now archived — all items resolved).
  **Decision: Option A1 — extend `Noise.Settings`, new accumulation method in noise runtime.**
  `FractalMode` enum migrated from `Islands.PCG.Layout.Maps` to `Islands` namespace (Noise.cs).
  `Noise.Settings` extended with `FractalMode fractalMode`, `float ridgedOffset`, `float ridgedGain`.
  `Noise.GetFractalNoise<N>()` branches on `fractalMode`: Standard = unchanged fBm (early return);
  Ridged = private `GetRidgedFractalNoise<N>()` implementing Musgrave algorithm:
  `signal = (offset - abs(noise))^2`, inter-octave feedback via `weight = clamp(signal * gain, 0, 1)`.
  Canonical defaults: offset=1.0, gain=2.0. ~35 lines. Applies to all `INoise` types.
  Standard mode with any noise type produces identical output to pre-N5.c (golden safe at defaults).
- **Voronoi-aware normalization.** `FillNoise01Core` takes a `remapBipolar` parameter. Gradient
  noise (Perlin, Simplex, Value) uses `n * 0.5 + 0.5` (bipolar [-1,1] → [0,1]). Voronoi noise
  uses direct `saturate(n)` since distances are non-negative. Fixes the bright-bias where Worley
  output was compressed into [0.5, 1.0].
- **CellAsIslands for archipelago generation:** Each Voronoi cell becomes a distinct rounded
  island. Combined with NoShape mode (N5.a), creates natural archipelagos from noise alone.
- **Assembly references:** `Islands.PCG.Editor` and `Islands.PCG.Tests.EditMode` asmdefs updated
  to reference `Islands.Runtime` directly for `FractalMode` resolution.
- 22 new tests in `MapNoiseBridge2DTests.cs`.
- Medium complexity. Noise runtime modified (governed reference surface); changes are additive
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

### Phase N6 — Noise Preview Visualization
**Done. Sequenced after Phase N5 (done).**

Replaced the scalar overlay system on `PCGMapTilemapVisualization` with a Texture2D +
SpriteRenderer approach. Two independent overlay slots for A/B comparison of pipeline
fields and raw noise patterns.

- **`ScalarOverlaySource` enum:** Height, CoastDist, Moisture (pipeline fields);
  TerrainNoise, WarpNoiseX, WarpNoiseY, HillsNoise (noise previews via
  `MapNoiseBridge2D.FillNoise01` with exact stage-matching salts).
- **`ScalarOverlayRenderer` helper:** manages child GameObject + SpriteRenderer + Texture2D
  per overlay slot. `FilterMode.Point`, `HideFlags.DontSave`. Aligns to tilemap via
  `layoutGrid.cellSize` scaling.
- **Performance:** one `tex.Apply()` per overlay replaces 65K `SetTile()` at 256×256.
- **Legacy removed:** heatmap tilemap path, per-cell tint path, all associated fields.
- Tilemap viz only (per visualization maintenance policy).
- 2 new files, 2 modified files.
- No new `MapLayerId` or `MapFieldId`.
- Depends on: Phase N4 (MapNoiseBridge2D), Phase N5.d (hills noise salt).

### Phase H8 — Mega-Tiles (2×2 Large Terrain Sprites)
**Done.**
**See [`Phase_H8_Design.md`](Phase_H8_Design.md) for full design, tradeoff analysis,
coordinate mapping, and visual smoke test protocol.**

Replaces clusters of same-type tiles with large multi-cell sprite groups.
First target: 2×2 HillsL2 clusters → single large mountain sprite (TL/TR/BL/BR quadrants).

Resolved decisions:
- **Adapter pre-pass** (not RuleTile, not custom TileBase). `MegaTileScanner` reads
  `MapDataExport` (read-only), produces `MegaTilePlacement[]`; `MegaTileStamper` overwrites
  claimed cells on the tilemap after standard `TilemapAdapter2D.Apply()`.
- **Strict 4/4 qualification** — only blocks where all four cells have the target layer
  set qualify. No mask mutation. Adapters-last preserved.
- **Greedy top-left row-major scan** — deterministic, O(W×H), claimed cells excluded
  from subsequent checks. Shared claimed array across all rules.
- **Overwrite stamp order** — standard Apply first, mega-tile stamper second. No changes
  to existing Apply contract.
- **Generic `MegaTileRule` struct** — `{ MapLayerId targetLayer, TileBase quadrantTL/TR/BL/BR }`.
  Multiple rules evaluated in priority order. Extensible to Vegetation, future layers.
- **Inspector fields on `PCGMapTilemapVisualization`** — `enableMegaTiles` bool +
  `MegaTileRule[]` array. FNV-1a dirty hash extended.
- **No new MapLayerId / MapFieldId / runtime stage.** Pure adapter-side in
  `Islands.PCG.Adapters.Tilemap`.
- **Block size locked to 2×2.** Multi-dimension support deferred to optional Phase H8b.
- Art blocked on quadrant sprites. Implemented with procedural placeholder sprites;
  art swap is a single TileBase asset assignment.

Files:
- `MegaTilePlacement.cs`, `MegaTileScanner.cs`, `MegaTileStamper.cs`, `MegaTileRule.cs`
  in `Runtime/PCG/Adapters/Tilemap/`.
- `MegaTileScannerTests.cs` in `Runtime/PCG/Tests/EditMode/PCG/Maps/` (10 tests).
- `PCGMapTilemapVisualization.cs` and `PCGMapTilemapVisualizationEditor.cs` modified.

### Phase H8b — Multi-Dimension Mega-Tiles
**Optional (planning only). Sequenced after H8. Not a prerequisite for any other phase.**

Generalizes the H8 scanner/stamper from a fixed 2×2 block to configurable block sizes.
Target dimensions: 1×2, 2×1, 2×3, 3×2, 3×3.

- **`MegaTileRule` extended** with `blockWidth` / `blockHeight` fields (default 2/2 for
  backward compatibility). `TileBase[]` flat row-major array replaces the four named
  quadrant fields (a 2×3 block needs 6 tiles, a 3×3 needs 9).
- **`MegaTileScanner.ScanOneRule` generalized** from checking 4 cells to checking W×H
  cells in nested loops. The greedy row-major scan and shared claimed array remain
  unchanged. Algorithm stays O(W×H) per rule.
- **`MegaTilePlacement` extended** — carries block dimensions (or derives them from rule
  index lookup).
- **`MegaTileStamper` generalized** — iterates the tile array row-major with flipY
  mapping extended to arbitrary block height.
- **Greedy scan bias consideration:** For larger blocks (3×3), the top-left greedy bias
  becomes more visible. Empirical testing across seed ranges should validate visual
  acceptability. If the bias is problematic, a grid-aligned scan (even-coordinate origins)
  can be offered as an alternative mode via a `ScanMode` enum on the rule.
- **Orientation for non-square blocks:** 1×2 and 2×1 are distinct rules (not auto-rotated).
  A 1×2 rule scans for vertical pairs; a 2×1 rule scans for horizontal pairs.
- **Inspector UX:** The tile array size is `blockWidth × blockHeight`. The Editor can
  validate that the array length matches the declared dimensions.
- Pure adapter-side. No new MapLayerId, MapFieldId, or runtime stage contracts.
  Adapters-last invariant preserved.
- Depends on: Phase H8 (done).
- **Trigger:** Implement when art assets with non-2×2 sprite groups become available, or
  when gameplay/visual needs require varied mega-tile dimensions.

### Phase T1 — PCG Map Mesh Visualization
**Planning. Design complete. Sequenced on adapter track, parallel to mainline.**
**See [`Phase_T1_Design.md`](Phase_T1_Design.md) for detailed design.**

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
**Done.** Implemented, test-gated, golden hashes captured, visual smoke tests 1–4 passed.
**See [`Phase_L_Design.md`](Phase_L_Design.md) for detailed design and post-delivery notes (§17).**

Single `IMapStage2D` (`Stage_Hydrology2D`) with two sub-stages: L.1 Rivers (Priority-Flood
depression fill → D8 flow direction → flow accumulation → fractional-threshold extraction)
and L.2 Lakes (three-way boolean exclusion of non-Land, non-DeepWater, non-River cells;
optional BFS size filter). New registry entries: `MapLayerId.Rivers=13`, `MapLayerId.Lakes=14`
(COUNT→15); `MapFieldId.FlowAccumulation=6` (COUNT→7). New operator `HeightFieldHydrologyOps2D`
(four static deterministic methods: `FillDepressions`, `ComputeFlowDirectionsD8`,
`AccumulateFlow`, `ExtractRivers`). Zero RNG. Stage gated by `enableHydrologyStage`
(default = false, opt-in only). Pipeline position: after Morphology, before Biome.
L→M moisture coupling via `riverMoistureBonus` + `riverFlowNorm` is active when the
hydrology gate is enabled and bit-identical to the pre-L baseline when disabled.

- Depends on: F2 (Height field). Optional dependency on Phase K (elevation).
- Goldens: `StageHydrology2DTests.cs`, `MapPipelineRunner2DGoldenLTests.cs`,
  `MapPipelineRunner2DGoldenLMTests.cs`.

### L-fix.a (revised) — Multi-layer Routing Partitions for Rivers and Lakes
**Done.** No golden break. No new stages, fields, or layers. Implementation surfaced
and validated by Phase V.a smoke testing 2026-04-15.

`PCGMapTilemapVisualization.StampMultiLayer()` routes layers through three static
arrays (`s_baseLayers`, `s_overlayLayers`, `s_colliderLayers`). Rivers and Lakes
were absent from all three after Phase L shipped, so they were silently dropped in
multi-layer mode. A previously documented L-fix.a entry described an alternative
routing (Rivers as overlay, Lakes as base) but was never committed.

**Implemented fix:** `MapLayerId.Lakes` and `MapLayerId.Rivers` appended to
`s_baseLayers` in that order — both render on the base tilemap, with Rivers
winning on confluence (later-entry-paints-over rule). `MapLayerId.Lakes` added to
`s_colliderLayers` — lakes block movement; rivers remain passable by design (no
bridges/fords required). `s_overlayLayers` unchanged.

1 file modified: `PCGMapTilemapVisualization.cs`. Maintenance rule re-affirmed for
future phases adding new `MapLayerId` entries — see `CURRENT_STATE.md`
Visualization Maintenance Policy section.

**By design (not a defect):** Stage_Vegetation does not exclude Rivers/Lakes from
its eligibility set. Cells with both `Vegetation` and `Rivers` set represent
fertile river-valley ecology and are addressed at the sprite-asset layer
(transparency around grass tufts so the river tile shows through). Same approach
for high-elevation river-source visibility through hill overlay sprites. No
`Stage_Vegetation` contract change. No pipeline-stage change.

**Deferred (non-blocking):** the same routing-omission bug class may exist in
`PCGMapVisualization.cs` (non-tilemap GPU lantern variant) — it does not host a
Tilemap and does not consume the partition arrays, but its layer color tables
should be audited for the same Rivers/Lakes coverage.

### Phase M — Climate & Biome Classification
**Done.** Implemented, test-gated, golden hashes captured, smoke test passed.
**See [`Phase_M_Design.md`](Phase_M_Design.md) for detailed design.**

Single `IMapStage2D` (Stage_Biome2D) with three sub-stages: M.1 Temperature, M.2 Moisture,
M.3 Biome. `MapFieldId` extended: Temperature=3, Biome=4 (COUNT→5). 12 ecological biomes
(`BiomeType` byte enum) + Unclassified sentinel + Beach override. 4×4 Whittaker lookup
(`BiomeTable`). Zero ctx.Rng; noise via MapNoiseBridge2D coordinate hashing. 11 stage-local
tunables. Pipeline position: after Stage_Morphology2D (G). 13 unit tests + full pipeline
golden (3 field hashes) + non-invalidation test.

**Smoke test observations:**
- Temperature: PASS. Gradient tracks Height correctly; steep smoothstep transitions produce
  sharp temperature boundaries (correct — lapse rate amplifies the height S-curve).
- Moisture: PASS with tuning flag. Formula correct but noise amplitude (0.5) drowns the
  coast-to-interior gradient (~0.18 range). Biome output healthy despite this.
- Biome: PASS. Clear discrete bands, Beach ring on warm coasts, cold peaks, water sentinel=0.

### M-fix.a — Biome Tunables Inspector Wiring
**Done.** M-fix.c folded in. Golden break — re-capture all M hashes.

10 biome climate tunables promoted from hardcoded Stage_Biome2D defaults to
Inspector-accessible serialized fields on PCGMapTilemapVisualization and
MapGenerationPreset. Same pattern as shallowWaterDepth01 (stage-local feeding).
Editor conditionally hides biome fields when enableBiomeStage is off.
Moisture defaults adjusted: coastalMoistureBonus 0.3→0.5, coastDecayRate 0.15→0.3,
moistureNoiseAmplitude 0.5→0.3 (coast gradient now visible).

4 files modified: Stage_Biome2D.cs, PCGMapTilemapVisualization.cs,
MapGenerationPreset.cs, PCGMapTilemapVisualizationEditor.cs.
No new stages, layers, or fields.

Note: 10 tunables wired, not 11 — beachMinTemperature lives on BiomeTable as
static readonly, not on Stage_Biome2D.

### M-fix.b — Effective Elevation Field
**Resolved (deferred by design).** No implementation needed.

Temperature reads continuous Height because elevation is continuous. Discrete terrain
tiers (HillsL1/HillsL2) are gameplay/visual abstractions for movement cost and tile art,
not climate inputs. The 0.02 temperature gap between adjacent Height values at a hill
boundary is correct physics — nearby cells at similar elevation have similar temperature.

Analysis of downstream consumers found zero stages that would benefit from an
EffectiveElevation field: M2.a reads Biome/Moisture (not Height), M2.b reads Biome only,
Phase W reads world-scale Height (continuous is correct at that scale), Phase L requires
continuous Height for flow routing (a staircase field would break Priority-Flood).

If a designer later wants sharp temperature breaks at hill boundaries, implement as an
inline A/B toggle (`useEffectiveElevation`) in Stage_Biome2D — no new MapFieldId or
stage required. The decision is explicitly reversible.

### M-fix.c — Moisture Default Tuning
**Done (folded into M-fix.a).**

Defaults changed in Stage_Biome2D: coastalMoistureBonus 0.3→0.5, coastDecayRate 0.15→0.3,
moistureNoiseAmplitude 0.5→0.3.

### Phase M2 — Biome-Aware Vegetation & Region Naming
**Confirmed (after M). Design complete.**
**See [`Phase_M2_Design.md`](Phase_M2_Design.md) for detailed design.**

- **M2.a — Biome-aware vegetation refactor.** ✅ Complete (golden-captured).
  Per-cell threshold from `BiomeTable`; stage-local `moistureModulation=0`
  default; Option A fallback `LegacyThreshold=0.40f` when biome layer absent;
  pipeline reorder so `Stage_Vegetation2D` runs after `Stage_Biome2D`;
  three viz classes gained `stagesM2a` lantern entry; dual-golden test
  pattern in `StageVegetation2DTests`; new `MapPipelineRunner2DGoldenM2Tests.cs`;
  M-fix.a/c goldens re-captured as side effect (5 constants across
  `StageBiome2DTests.cs` and `MapPipelineRunner2DGoldenMTests.cs`).
  See `CURRENT_STATE.md` and `map-pipeline-by-layers-ssot.md` F5 contract.
- **M2.b — Contiguous region detection and naming.** ✅ Complete (golden-captured).
  CCA over `Biome` field → contiguous region ids → deterministic naming via
  `RegionNameRegistry2D` + `RegionNameTableAsset`. `MapFieldId.BiomeRegionId = 5`
  (COUNT → 6). All four viz classes patched; `ScalarOverlaySource.BiomeRegionId = 5`.
  Full-pipeline golden captured in `MapPipelineRunner2DGoldenM2bTests.cs`.
  See `CURRENT_STATE.md` and `map-pipeline-by-layers-ssot.md` M2.b contract.
- Depends on: Phase M, F5.

### Phase V — Runtime Inspection UI
**Done (V.a + V.b). All smoke-validated.**
**See [`Phase_V_Design.md`](Phase_V_Design.md) for detailed design.**

Runtime inspection overlay for the PCG pipeline. Read-only sample/tooling phase — produces
no authored data, does not participate in golden tests or determinism gates. Does not replace
edit-mode visualizations (`PCGMapVisualization`, `PCGMapCompositeVisualization`,
`PCGMapTilemapVisualization`) — runs alongside them.

**V.a — Hover Tooltip. ✅ Implemented 2026-04-15.** Mouse hover over the rendered tilemap
→ live readout of grid coordinates, layer membership, and all authored scalar fields
(Height, CoastDist, Temperature, Moisture, Biome name + ID, BiomeRegionId if present, etc.).
UI displays in a screen-space canvas panel. Uses a small `IMapContextSource` interface so
any existing visualization can expose its live `MapContext2D`. One-line interface
implementation added to each existing viz class.
- New asmdef: `Islands.PCG.Inspection`.
- New files: `IMapContextSource.cs`, `PCGHoverTooltip.cs`, `MapCameraController2D.cs`
  (sample-side smoke-rig free-cam, also consumes `IMapContextSource`).
- Tests: 4 unit tests in `IMapContextSourceTryWorldToCellTests.cs` (round-trip flipY=
  true/false, out-of-bounds rejection, regen monotonicity). All green.
- Smoke test §10 acceptance per `Phase_V_Design.md` §11.1: green.
- Surfaced and validated the L-fix.a routing partition omission as concrete
  value-of-tooling evidence.

**V.b — Per-Cell Overlay System. ✅ Implemented 2026-04-26.** `PCGRuntimeOverlay`
MonoBehaviour with two independent display modes:
- **Color overlay** — per-cell discrete color from `BiomeColorPalette` SO (Biome) or
  deterministic FNV-1a hash-color (BiomeRegionId). Uses an owned
  `ScalarOverlayRenderer` instance via `SetDataDirect`.
- **Text overlay** — per-cell numeric label for pipeline fields (Height, CoastDist,
  Moisture, Temperature, Biome, BiomeRegionId, FlowAccumulation). World-space Canvas +
  TextMeshProUGUI (URP 2D compatible). View-aware with 64×64 hard cap. α decision:
  noise/derived sources render nothing.

V.b files: `PCGRuntimeOverlay.cs`, `BiomeColorPalette.cs`, `BiomeColorPaletteTests.cs`,
`ScalarOverlayRenderer.cs` (modified — `SetDataDirect`, promoted to public).
`BiomeColorPalette-Default.asset` via context menu. `Adapters.Tilemap.asmdef` gained
`Unity.TextMeshPro` reference. No new asmdef.
Smoke tests §11.2 (text) and §11.3 (color): all green.

Authority note: Phase V is roadmap-scoped only. It is **not** implementation authority and
**not** a governed surface. Detailed contracts live in `Phase_V_Design.md` (planning
authority for the phase).

### Phase Q — Biome-Conditional Tile Selection
**Done (Q + Q-fix.a + Q-aux.a, 2026-08-09). Adapter track. Independent of Phase N/O/P/W path.**
**See [`Phase_Q_Design.md`](Phase_Q_Design.md) for detailed design.**

Closes the documented but unimplemented gap between Phase M (produces `MapFieldId.Biome`
per cell) and the tilemap adapter (currently ignores it). `Phase_M_Design.md` §9 and
`Phase_M2_Design.md` §7 both list `TilesetConfig` as a `Biome` consumer ("biome-conditional
tile entries"); `Resolved design decisions` Decision 1 references `BiomeDef[]` carrying a
"tile palette" property. Today `TilesetConfig.LayerEntry` and `TilemapAdapter2D` contain
zero biome references — the field is produced and ignored. Phase Q wires the consumer.

**Intent.** Per-cell tile selection that varies by biome:
- Snow / ice ground tiles in `Snow` and `Tundra` cells.
- Sand / rock ground in `SubtropicalDesert`, `TemperateDesert`.
- Grass / lush ground in `TemperateForest`, `Grassland`, `TropicalRainforest`.
- Snowy mountain peaks in cold-biome `HillsL2` cells.
- Vegetation sprite variety: pines in boreal, palms in tropical, cacti in desert,
  broadleaf in temperate — driven from the existing single `MapLayerId.Vegetation`
  mask, no layer split.

**Scope.** Pure adapter-side. No new `MapLayerId`. No new `MapFieldId`. No pipeline
stage. No `MapPipelineRunner2D` modification. The adapter reads the existing `Biome`
field and routes layer→tile resolution through a biome-aware `TilesetConfig` extension.

**Mechanism choices — resolved in `Phase_Q_Design.md`:**
- Mechanism: `BiomeTileOverride` ScriptableObject wrapping a base `TilesetConfig` (Q-DD-1).
- Per-layer scope: any layer; recommended set documented (Q-DD-5).
- Fallback when a biome has no entry: three-level fallthrough, no magenta sentinel (Q-DD-6).

**Soft dependency: biome transition blending.** Currently a Tier 1 unbuilt item per
`technique_integration_matrix.md` ("Biome transition blending — NONE"). Without it,
biome boundaries produce sharp tile changes. Phase Q ships acceptably without it
(hard transitions match the current `Biome` field's hard-boundary nature); blending
is a separate later refinement that improves Q's visual output.

**Delivered files:** `BiomeTileOverride.cs` (`Runtime/PCG/Adapters/Tilemap/`);
`TilemapAdapter2D.cs`, `PCGMapTilemapVisualization.cs`,
`PCGMapTilemapVisualizationEditor.cs` (modified);
`BiomeTileOverrideTests.cs` (`Tests/EditMode/PCG/Adapters/Tilemap/`);
`BiomeTileOverridePlaceholderGenerator.cs` (`Editor/Inspectors/`, Q-aux.a, editor-only).

**Dependencies:** Phase M (done). No others required.

**Non-goals:** No new pipeline contract. No new field or layer. No biome blending
(separate future work). No vegetation layer split. No region-aware tile selection
(M2.b's `BiomeRegionId` is not consumed here — region is a different axis).

#### Done

- `BiomeTileOverride` SO: additive per-biome tile overrides over a base `TilesetConfig`;
  flat `TileBase[]` lookup sized `BiomeType.COUNT × MapLayerId.COUNT`, O(1) per cell.
- `TilemapAdapter2D.ApplyBiomeAware` / `ApplyLayeredBiomeAware`; a null override delegates
  to the pre-Q path, so pre-Q output is byte-identical.
- `PCGMapTilemapVisualization`: `biomeTileOverride` slot, `StampMultiLayerBiomeAware`,
  override-content hashing for dirty tracking.
- **Q-fix.a** — four adapter-side defects found on review of the previously unregistered
  implementation (Q-BUG-1 null base tile skipped overrides; Q-BUG-2 collider group routed
  through the biome-aware path; Q-BUG-3 `(int)` truncation vs. `Mathf.RoundToInt`;
  Q-BUG-4 hash block after the early return). All fixed; `BiomeTileOverrideTests` 13 → 16.
- **Q-aux.a** — `BiomeTileOverridePlaceholderGenerator`, editor-only placeholder tile
  generation so a biome-conditional setup can be smoke-tested before any real art exists.
- Zero new `MapLayerId`, zero new `MapFieldId`, zero new stages, zero
  `MapPipelineRunner2D` change, zero golden impact.

See `CURRENT_STATE.md` (Phase Q resolution block) for the implemented detail and
`reference/tileset-import-guide.md` §Phase 7 for the authoring workflow.

Authority note: this roadmap entry is roadmap-scoped only. It is **not** implementation
authority. Design authority for the phase is `Phase_Q_Design.md`; implemented truth is
recorded in `CURRENT_STATE.md`.

### Phase Q2 — Composite-Condition Tile Selection
**Planning only. Sequenced on adapter track. Independent of Phase N/O/P/W path. Sibling of Phase Q (can ship in either order; share architecture flavor, do not depend on each other).**

Closes the gap surfaced during V.a smoke testing (2026-04-15): some visually meaningful
tile types are derived from the **simultaneous presence of two or more `MapLayerId`s at
a cell**, not from any single layer or biome. The pipeline already produces all the
truth needed; the adapter has no system to consume layer combinations.

**Worked examples (catalog of likely consumers):**

| Composite | Condition | Notes |
|---|---|---|
| Waterfall / rapids | `Rivers ∧ HillsL2` | Originating motivation. River dropping over a high cliff. |
| Potholes / marmitas | `Rivers ∧ HighFlow ∧ drop-proximity` | Fluvial erosion features. Prereq candidate: slope/gradient field (possible L2). |
| Bridge | `Rivers ∧ Paths` | Phase O dependency. River + path = crossing structure. |
| Ford | `Rivers ∧ ShallowWater` (or `Rivers ∧ low FlowAccumulation`) | Shallow river crossing. |
| Cliff | `LandEdge ∧ HillsL2` | High coastal terrain. |
| Wetland | `Vegetation ∧ adjacent_to(Rivers ∨ Lakes)` | Future ecology pass. Adjacency adds scope. |
| River mouth | `Rivers ∧ ShallowWater` | River meeting sea. |

Five-plus likely consumers within already-roadmapped work. Justifies a real system
rather than per-feature hardcoding.

**Scope.** Pure adapter-side. **No new `MapLayerId`. No new `MapFieldId`. No pipeline
stage. No `MapPipelineRunner2D` modification. No golden break.** A composite tile is a
*derived view* over existing layer truth; promoting derived values to `MapLayerId` would
mean unnecessary golden recapture and partition-array updates for every new composite.

**Design pattern (deferred to `Phase_Q2_Design.md` when activated):** mirror the Phase
H8 `MegaTileRule` ScriptableObject pattern. Sketch:

```csharp
[CreateAssetMenu(menuName = "Islands/PCG/Composite Tile Rule")]
public sealed class CompositeTileRule : ScriptableObject
{
    public string label;                  // "Waterfall"
    public MapLayerId[] requireAll;       // {Rivers, HillsL2}
    public MapLayerId[] requireNone;      // optional: {Lakes}
    public TileBase tile;                 // sprite to stamp
    public CompositeTargetTilemap target; // enum: BaseOverride / Overlay / CompositeOverlay
    public bool addCollider;              // optional opt-in
}
```

Plus `CompositeTileScanner` + `CompositeTileStamper` mirroring H8's `MegaTileScanner` /
`MegaTileStamper`. Runs as an adapter post-pass after the existing
`StampMultiLayer` and any H8 mega-tile pass. Wired via a new `compositeRules:
CompositeTileRule[]` field on `PCGMapTilemapVisualization` plus a new
`compositeOverlayTilemap` slot.

**Open design questions** (resolved in `Phase_Q2_Design.md` when activated):

1. **Render order / target tilemap.** Composite tiles likely want their own tilemap to
   draw on top of the overlay tilemap (so a waterfall is visible above hills).
   `CompositeTargetTilemap` enum vs. explicit `Tilemap` reference per rule.
2. **Collider opt-in.** Bridges affect walkability; cliffs may block; waterfalls don't.
   Per-rule `addCollider` flag with optional `compositeColliderTilemap` slot.
3. **Suppression.** Should a waterfall cell suppress vegetation rendering on the overlay
   tilemap underneath? Per-rule `suppressOverlayLayers: MapLayerId[]` field, or accept
   visual stacking and rely on sprite alpha (consistent with the river-valley ecology
   approach from the L-fix.a session).
4. **Adjacency conditions.** "Wetland" needs `adjacent_to(Rivers ∨ Lakes)`, not just
   "is Rivers". Either restrict Q2 to per-cell-only conditions and defer adjacency to
   a Phase Q3, or include a `requireAdjacency: AdjacencyCondition[]` field at extra
   complexity cost.
5. **Multi-rule precedence.** What if both "Bridge" and "Waterfall" match a cell?
   Rule order in the array (last wins / first wins / explicit priority).
6. **V.a tooltip integration.** Composite tiles aren't `MapLayerId`s. Should the
   tooltip show "would render as: Waterfall"? Probably out of scope for Q2; consider
   for a later V.c.

**Expected files:** `Phase_Q2_Design.md` (when activated). New
`CompositeTileRule.cs`, `CompositeTileScanner.cs`, `CompositeTileStamper.cs` under
`Adapters/Tilemap/`. Extension to `PCGMapTilemapVisualization.cs` for the rules array,
the new tilemap slot, and the post-pass invocation. Possibly starter rule assets
(`CompositeTileRule-Waterfall.asset`, etc.) under `PCG Map Tilemap/CompositeRules/`.

**Dependencies:** None hard. Softly benefits from Phase L (rivers/lakes) and Phase H8
(mega-tile pattern reference). Independent of Phase Q (biome-conditional) — they share
architecture-flavor but operate on different axes (layer composition vs. biome lookup).

**Non-goals:** No new pipeline contract. No new field or layer. No golden coverage
(adapter-side, derived view). No determinism gate.

Authority note: Phase Q2 is roadmap-scoped only. It is **not** implementation authority
and **not** a governed surface. Design document `Phase_Q2_Design.md` to be written when
the phase activates. Do not promote into SSoTs until designed.

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
**Active. W.a complete 2026-08-09; W-aux.a through W-aux.g closed. Next: W.b.**
Not implementation authority.
**See [`Phase_W_Design.md`](Phase_W_Design.md) for detailed design.**

- World map = low-resolution `MapContext2D` rectangular grid.
- `WorldTileContext` handoff struct.
- `MapShapeInput` (F2c) as integration point.
- Dependencies: F2c (done), Phase M (done), Phase L (done, optional), Phase K (optional).

**Sequencing deviation (2026-08-09):** Phase W proceeds before Phase P. Rationale: no
rubric yet exists for what distinguishes a good world from a bad one; building
`IMapValidator2D` without criteria would produce a retry loop with no semantics. The
rubric will be distilled from observing W-generated worlds. Phase P is paused, not
cancelled; it resumes with criteria derived from W output.

**W.a — complete (2026-08-09).** 64×64 world-scale pipeline run from a
`MapGenerationPreset`, visually smoke-validated. World identity is `shapeMode = Ellipse`.
The console golden for the run is captured but not yet registered in a governed surface.

**W.b — parameter surface consolidation (scope defined 2026-08-20, not started).**
This entry is the single definition of W.b's scope; other documents point here rather than
restating it.

W.b promotes the tunables that exist in code but cannot be reached from a
`MapGenerationPreset`. It is **not** a step of the world→local zoom sequence — that arc
continues at W.c (the F2c shape-mask builder) and is unaffected by W.b. W.b comes first
because a world is many maps: configuring them one GameObject at a time does not scale, and
every later W step assumes a preset is a complete description of a map.

In scope — the five component-scoped fields, today living on the visualization components:
`enableRegionsStage`, `enableHydrologyStage`, `hydroEpsilon`, `hydroRiverThresholdFraction`,
`hydroMinLakeArea`. Two candidates of the same class, to be decided at batch start rather
than assumed: `Stage_Regions2D.SpeckThreshold` (a stage constant, not a component field) and
`Stage_Vegetation2D.moistureModulation` (a public field never verified as assigned by
anything in the construction path — see `CURRENT_STATE.md` §Open observations).

Two destinations, with different consequences. Stage toggles sit beside the six already in
the preset and do not enter `MapTunables2D`, so they cannot move a golden. The three
`hydro*` values are algorithm tunables and **do** enter the `MapTunables2D` constructor;
identity defaults are the strategy that let W-aux.b close without breaking a single golden,
and the same discipline applies here.

Closure conditions beyond the code: `ToJson()` and the importer table change **together**
(the round-trip gate fails until both do — that is the gate working, not a bug), and the
wizard's `HelpBox` declaring this gap is retired in the same batch. Leaving it is worse than
never having written it: the tool would be lying about its own limitations.

**W-aux track (opened 2026-08-17):** measurement and calibration sub-batches running
alongside Phase W's world→local sequence.
- **W-aux.a — complete (2026-08-17).** Read-only statistics exporter + Inspector button +
  measured preset baselines. Adapter/inspection-side only; no core changes, no golden
  breaks (W.a goldens re-captured identical).
- **W-aux.b — submarine relief (closed 2026-08-18).** Sea-floor relief in
  `Stage_BaseTerrain2D` + mirror, two tunables defaulting to identity, hard clamp below
  `waterThreshold01`. `ShallowWater` and `MidWater` now denote depth bands rather than
  adjacency. First non-empty `Lakes`. **No golden was broken** — the identity default made
  re-anchoring unnecessary, correcting the batch's original scoping assumption.
- **W-aux.c — instrumentation, preset authoring, per-biome vegetation (closed 2026-08-19).**
  Three blocks: `MapStatsExporter2D` calibration sections + `Default_MapPreset`
  recalibration; `MapGenerationPresetWizard` + JSON importer in `Islands.PCG.Editor`;
  `BiomeDef.vegetatesOnPeaks` with contract M2a-3 reformulated. Vegetation goldens
  re-anchored by design.
- **W-aux.d — vegetation quantile mapping (closed 2026-08-19).** `Stage_Vegetation2D`
  accepts by a global quantile cut over the eligible population instead of the absolute
  threshold `1 − vegetationDensity`, which had put low-density biomes outside the noise
  field's support entirely. Contract M2a-9 reformulated. `BiomeTable` densities deliberately
  **not** recalibrated in that batch — the mapping changed what the numbers mean, and
  recalibration should follow measurement.
- **W-aux.e — threshold-mapping audit (closed 2026-08-19, no code change).** Audited
  `Stage_Hills2D` and `Stage_BaseTerrain2D` for the defect W-aux.d had just fixed. Verdicts:
  `waterThreshold01` is a feature; the hills classifier is exonerated; the defect was
  upstream in the height composition formula. Produced the diagnosis that opened W-aux.f.
- **W-aux.f — height ceiling de-saturation (closed 2026-08-20).** Height perturbation
  normalized by `(1 + terrainAmp/2)`, removing the `Height == 1.0` atom. `waterThreshold01`
  recalibrated at six entry points so the coastline would not move — and it did not: land
  topology is bit-identical. 29 golden constants depending on the *value* of Height were
  re-anchored. Accepted under the prioritisation posture above.
- **W-aux.g — hills window recalibration (closed 2026-08-20).** `hillsL1` / `hillsL2`
  redefined as area fractions of `Land`, resolved per run by an order statistic over the Land
  height population (`HillsThresholdOps2D`), replacing the N5.e range remap. The band moved
  from 9.86 / 9.36 / 2.60 % (3.8× spread) to 20.13 / 20.30 / 20.20 % at seeds 56 / 8 / 243.
  Defaults 0.55 / 0.20; 10 goldens re-anchored; suite green; visual smoke test passed.
  Fixed-threshold recalibration was rejected as **ineffective** (the dispersion is structural)
  and moving `heightRedistributionExponent` as **out of proportion** (it reshapes the whole
  field and would force a second `waterThreshold01` recalibration). Contract in
  `map-pipeline-by-layers-ssot.md` §F3b′; entry in `changelog-ssot.md`.
  Two follow-ups were opened rather than absorbed, both recorded in `CURRENT_STATE.md`
  §Open observations: the exported `HillsL2` layer is still seed-dispersed because of
  `hillsNoiseBlend`, and `Default_MapPreset.waterThreshold01` appears to carry the code
  default's compensated value instead of its own.
  Related and separate: `BiomeTable` vegetation densities have not been recalibrated since
  W-aux.d changed what they mean.

**Authority note:** the W-aux track is roadmap-scoped only. It is **not** implementation
authority.

### Phase X1 — Authoring Tools

First phase of the **authoring track**: Editor-side tools that help a designer reach an
intended map, as distinct from the pipeline track (which changes what the pipeline
produces) and the adapter track (T1, Q2). Parallel by construction — X1 neither blocks
nor is blocked by W.b.

Standing constraints for every X1 iteration:
- Editor-only. Golden-neutral. No core runtime or pipeline dependency.
- The wizard does not run the pipeline (X1.a architecture decision; revisit only as an
  explicit decision, never as UI drift).
- **Every statement the tooling shows to the user is labeled `measured` or `inferred`.
  `measured` requires a named run.** A plausible-but-false hint costs the designer a full
  measure-and-diagnose cycle; silence costs nothing.

**X1.a — Preset diagnostics (done, 2026-08-19).** Pure `preset → List<PresetFinding>`
analysis, six deterministic rules computed without running the pipeline, rendered in
`MapGenerationPresetWizard`, plus a filtered preset-to-preset JSON diff. See
`CURRENT_STATE.md` for the implemented surface. Two of the six rules now carry historical
measured backing (see the parameter-legibility thread); re-basing them is unscheduled.

**X1.b — Objective-driven guidance (deferred).** "I want more vegetation / bigger
mountains / more rivers." Blocked on measured evidence: today only vegetation density and
height composition have backing runs. Rivers, lakes, biome shape and relief have none, so
any guidance on them would be `inferred` at best.

**W.b coupling.** The wizard's `HelpBox` declaring the five component-scoped fields absent
from `MapGenerationPreset` is true today. **Removing it is part of W.b's closure**, not a
future X1 iteration — otherwise the wizard starts lying about its own gap.

## Legacy relationship
Legacy map-generation documents are conceptual reference only for the new pipeline.
The active authoritative direction is masks/fields + adapters-last + deterministic headless stages.

---

## Parking Lot — Speculative Ideas

Items below are not planned work. They are design seeds worth preserving for when their parent phase activates.

### PL-1 — Tectonic-line archipelago distribution (Phase W)
**Date:** 2026-04-25  
**Idea:** When distributing multiple islands on a world map, trace fracture/fault lines using noised splines across the grid and seed island origins along those lines. This produces geologically plausible archipelago chains (Ring-of-Fire pattern, mid-ocean ridges) instead of uniform random scatter.  
**Reference:** Real-world earthquake distribution maps show that volcanic island chains (Indonesia, Japan, Philippines, Hawaii) cluster along tectonic plate boundaries.  
**Relevance:** Phase W world-map generation — island placement strategy.  
**Status:** Unvalidated seed. No implementation work.

### PL-2 — Moisture-driven vegetation oasis clustering (Phase V / Vegetation)
**Date:** 2026-04-25  
**Idea:** In arid biomes, vegetation should cluster tightly around water sources (rivers, lakes, springs) rather than scattering uniformly. Use the moisture/hydrology field as an attraction mask: high moisture → dense vegetation, low moisture → barren. The transition should be sharp, not gradual — real oases have an abrupt green-to-sand edge driven by root reach to the water table.  
**Technique sketch:** Threshold the moisture field at a biome-dependent cutoff. Feed the thresholded mask as a density multiplier into vegetation scatter. Optionally erode/dilate the mask by 1–2 cells to control fringe width.  
**Reference:** Sahara desert oases — aerial photography shows palm clusters forming tight linear or blob shapes along wadis and spring-fed pools, with near-zero vegetation beyond 20–50m of the water edge.  
**Relevance:** `Stage_Vegetation2D` density modulation, `Stage_Hydrology2D` moisture field consumption. Potentially Phase V (vegetation refinement) or Phase L (lakes as oasis seeds).  
**Status:** Unvalidated seed. No implementation work.

### PL-3 — Salt flat (salar) biome with mineral deposit patterns (Phase M / Biome)
**Date:** 2026-04-26  
**Idea:** Add a salt flat / salar biome type for arid islands or world-map regions. Unlike sand deserts (dune-dominated, height-driven), salares are defined by near-zero elevation variance and rich surface texture variation: salt crust, mineral pools, sulfur deposits, shallow brine channels. The visual identity comes from color and pattern, not topology.  
**Key patterns to model:**  
- **Mineral blobs:** Organic-looking deposits that grow outward from nucleation points. Could use cellular automata growth or Voronoi-seeded radial expansion with noised edges. The yellow/green sulfur formations at Salar de Gorbea exhibit lobular shapes with concentric color rings (core → edge → water fringe).  
- **Salt channels:** Thin meandering paths through flat crust, much narrower than rivers. Could reuse hydrology flow-trace at very low gradient with a width cap of 1–2 cells.  
- **Endorheic pools:** Shallow water bodies with no outlet — water collects and evaporates, leaving mineral rings. Seed as local minima in a nearly-flat heightfield, color by mineral type.  
- **Abrupt color banding:** Transitions between salt, sulfur, water, and mineral zones are sharp (1–3 cell edges), not blended. Biome-transition blending should be suppressed or minimal within a salar.  
**Reference:** Salar de Gorbea and Salar de Atacama, Chile — aerial photography showing sulfur blob formations, brine channels, and mineral pool systems at high altitude.  
**Relevance:** `BiomeType` extension, `Stage_Biome2D` terrain-texture assignment, potentially `Stage_Vegetation2D` (salares are nearly vegetation-free — acts as a vegetation suppression zone). Phase M biome palette or a future "exotic biomes" pass.  
**Status:** Unvalidated seed. No implementation work.

### PL-4 — Glacial features: ice tongues, floating ice, fjords (Phase M / Biome + Shore)
**Date:** 2026-04-26  
**Idea:** Support glacial/subpolar island archetypes with three interrelated features:  
1. **Glacier tongues:** Above a biome-dependent snow line, cells are ice/snow. Where valleys descend below the snow line, extend ice downslope as "glacier tongue" masks — flood-fill or flow-trace from the snow cap following steepest-descent until elevation drops below a melt threshold or reaches water. Width narrows as it descends (1–3 cells at terminus).  
2. **Floating ice scatter:** In water cells adjacent to glacier termini, scatter ice chunk objects with density inversely proportional to distance from the glacier front. Could use a simple distance field from glacier-water boundary cells as a density mask, with Poisson-disk or jittered placement.  
3. **Fjord coastline shaping:** Glacial islands have narrow steep-walled inlets rather than smooth beaches. Could be achieved by carving thin channels into the coastline during `Stage_Shore2D` or `Stage_Morphology2D` — erode 1–2 cell wide cuts perpendicular to the coast at select points, then deepen them.  
**Supporting effects:**  
- **Elevation-band biomes:** Forest → bare rock → snow/ice as strict altitude bands. The snow line threshold becomes a biome parameter.  
- **Glacial water color:** Water cells near glacier termini get a "silty" tag or separate field value, enabling the tilemap adapter to use milky turquoise tiles instead of deep blue.  
- **Moraine sediment fans:** At glacier-water contact points, a small fan of sediment/gravel extends into the water (1–3 cells), creating small islets or shoals.  
**Reference:** Patagonian glaciers (Golfo Elefantes, Strait of Magellan) — glacier tongues calving into fjords, floating ice fields, turquoise glacial water, forest-to-ice elevation banding.  
**Relevance:** `BiomeType` snow/ice extension, `Stage_Hills2D` or `Stage_Morphology2D` for fjord carving, `Stage_Vegetation2D` suppression above snow line, object scatter for floating ice. Likely a late-stage "climate archetype" feature after core biomes are stable.  
**Status:** Unvalidated seed. No implementation work.

### PL-5 — Terrain terracing and stepped cliffs (Phase J2 / Noise Composition)
**Date:** 2026-04-26  
**Idea:** Add a "terracing" or "posterization" post-process to the height field that quantizes continuous elevation into discrete steps, producing flat plateaus separated by sharp vertical drops. This creates the layered basalt-cliff look seen in Icelandic landscapes (Dynjandi, Westfjords) and volcanic islands.  
**Technique sketch:** After fBm height generation but before land threshold, apply `floor(height * N) / N` where N controls the number of terrace levels. Alternatively, use a staircase spline via `ScalarSpline` (already implemented in Phase N2) with flat segments connected by steep ramps. The spline approach gives artistic control over where steps fall and how sharp the edges are. Could be biome-gated — only apply in volcanic/basalt biome regions.  
**Interaction with Q2 waterfalls:** Terraced terrain + rivers = natural waterfall sites at every terrace edge. Phase Q2's `Rivers ∧ HillsL2` composite condition would fire more frequently and more plausibly on terraced terrain than on smooth gradients.  
**Reference:** Dynjandi waterfall, Iceland — cascading falls over horizontally layered basalt producing staircase topography. Also: Giant's Causeway, Iguazú Falls, tepui mesa formations.  
**Relevance:** `Stage_BaseTerrain2D` or a new post-terrain shaping pass. Could leverage existing `ScalarSpline` infrastructure (N2). Feeds directly into Phase Q2 waterfall visual quality.  
**Status:** Unvalidated seed. No implementation work.

### PL-6 — Domain offset (X, Y) (Phase W / Base Terrain)
**Date:** 2026-08-18 (extended 2026-08-19)
**Idea:** A tunable offset for the generated island/map within the domain.
`Stage_BaseTerrain2D` hardcodes `center = new float2(w * 0.5f, h * 0.5f)`, so the island
cannot be shifted. Confirmed by the user as a wanted feature rather than a hypothetical.
**Relevance:** `Stage_BaseTerrain2D`, `MapTunables2D`. Sits alongside — or competes with —
Phase W's F2c mask mechanism.

**Two distinct readings, not yet separated (2026-08-19).** The request has been made in
two different registers and they are not the same feature:
- **(i) Shape offset.** Move the *island silhouette* within a fixed domain — the ellipse
  centre becomes a tunable. Cheap: one `float2` on `MapTunables2D`, consumed at the
  existing `center` site in `Stage_BaseTerrain2D` (and its mirror). Full regeneration.
  Does not change what noise is sampled, only where the falloff is anchored.
- **(ii) Window offset.** Move the *sampling window* over a conceptually infinite noise
  field — the domain becomes a viewport on a larger continuous world. This is what makes
  panning meaningful at runtime, and it is a different mechanism: the noise sample
  coordinate becomes `(x + offsetX, y + offsetY)` rather than `x, y`, and every
  neighbourhood-dependent stage (morphology `CoastDist`, hydrology flow accumulation,
  regions CCA) sees a *different* neighbourhood at the window edge. Determinism survives
  per-window; continuity across windows does not, unless edge handling is designed.

Reading (i) is a small, safe tunable. Reading (ii) overlaps heavily with Phase W's
world→local mechanism and with PL-7 — deciding between them should not be done casually.
**Status:** Idea only, no design. See `CURRENT_STATE.md` "What is not settled yet".

### PL-7 — Continuous zoom (Phase W)
**Date:** 2026-08-18 (extended 2026-08-19)
**Idea:** Generate the same world "from higher up" or "closer in" via a configurable scale
field, sampling a *window* of the same continuous field at a different offset or scale.
**Relevance:** Conceptually competes with the F2c binary-mask zoom mechanism of Phase W;
shares the domain-offset prerequisite with PL-6 (reading (ii)).

**Known obstacle (2026-08-19): zoom is not detail-neutral.** Two independent findings
already recorded make a naive scale tunable produce a *different world*, not the same
world closer up:
- **Climate is not scale-invariant.** `coastModerationStrength`, `coastDecayRate` and
  `coastalMoistureBonus` divide by `CoastDist` measured in cells, so the same tunables at
  a different resolution produce a different biome distribution (measured: `Showcase`
  64 → 128 took TemperateDesert from 1 to 291 cells). See `CURRENT_STATE.md`, "Measured
  calibration baselines and climate reachability".
- **River extraction is fraction-of-land-cells based.** `ExtractRivers` uses
  `riverThresholdFraction` against the land-cell count, which auto-scales with
  resolution — a different property from the climate terms and not obviously compatible
  with them under a shared zoom factor.

A usable zoom therefore needs a *policy* for which tunables are cell-denominated and must
be rescaled, which are domain-relative and must not be, and which are neither. That policy
does not exist and is a prerequisite, not an implementation detail.
**Status:** Idea only, no design.

### PL-7b — Runtime pan / zoom control surface (Phase V / Adapter)
**Date:** 2026-08-19
**Idea:** Expose PL-6 and PL-7 as runtime controls on the sample component rather than
Editor-only tunables, so the map can be panned and zoomed in play mode.
**Why this is a separate item:** PL-6 and PL-7 are *generation* questions; this is a
*cost* question. Today every parameter change triggers a full pipeline rebuild through
`PCGMapTilemapVisualization`'s dirty-tracking path (`MapPipelineRunner2D.Run` →
`MapExporter2D.Export` → tilemap stamp). Pan and zoom are continuous gestures: driving a
full O(cells) regeneration per frame from a scroll wheel is not viable at 256×256, and is
the reason PL-9 stops being optional the moment this item is taken seriously.
**Open questions, none answered:** whether panning regenerates or reuses a cached larger
field; whether zoom levels are discrete (a small set of pre-generated resolutions) or
continuous; whether the tilemap adapter can stamp incrementally rather than clearing and
re-stamping; whether a coarse preview field is generated during the gesture and the full
field only on release.
**Status:** Idea only, no design. Hard prerequisite: PL-6 reading (ii) and PL-7's rescale
policy must be resolved first — there is no point optimising the delivery of a mechanism
that is not specified.

### PL-8 — Runtime overlay toggles (Phase V / Inspection)
**Date:** 2026-08-18
**Idea:** Simple in-play UI to switch scalar overlays on and off, with overlays
precomputed (or computed in parallel) so toggling does not recompute per tile.
**Relevance:** `PCGRuntimeOverlay`, `ScalarOverlayRenderer`. A V.c-flavoured refinement.
**Status:** Idea only, no design.

### PL-9 — Generation performance work (cross-cutting)
**Date:** 2026-08-18 (extended 2026-08-19)
**Idea:** The noise library carries optimisations (Burst jobs, CPU parallelism, GPU paths)
that the map pipeline does not currently use.
**Constraint:** would need explicit determinism gates before any parallel path is
accepted — the grid-first deterministic invariant is not negotiable for a speedup.

**Promoted from "nice to have" to "prerequisite of PL-7b" (2026-08-19.)** As long as
generation is Editor-triggered and occasional, its cost is invisible. A runtime pan/zoom
control surface makes it the dominant cost. Recording the shape of the problem so the
evaluation is not started from zero:

- **Not all stages parallelise the same way.** `Stage_BaseTerrain2D`, `Stage_Hills2D` and
  `Stage_Biome2D` are per-cell functions of coordinate-hashed noise and already-written
  fields — embarrassingly parallel, and the noise runtime already has Burst paths for
  exactly this shape. `Stage_Morphology2D` (multi-source BFS), `Stage_Hydrology2D`
  (Priority-Flood with a `SortedSet` min-heap, then a descending-height sort, then
  downstream propagation) and `Stage_Regions2D` (connected-component analysis with
  ordered speck merging) are inherently sequential and carry explicit row-major
  determinism contracts. Parallelising the first group is a different project from
  touching the second, and the second is where the ordering invariants live.
- **Measurement before optimisation.** No per-stage timing exists. `MapStatsExporter2D`
  (W-aux.a) is the precedent for how to add a read-only diagnostic without touching core:
  a static function plus an Inspector button, never wired into the per-rebuild path.
- **The determinism gate is the deliverable, not the speedup.** Any parallel path needs a
  test asserting bit-identical output against the sequential one, on the same seed and
  domain. That test is cheap to write now and expensive to retrofit after a rewrite —
  it is the same shape as the already-proposed `BaseTerrainMirrorParityTests`.
- **The mirror problem compounds this.** `PCGMapTilemapVisualization` runs
  `BaseTerrainStage_Configurable`, not the governed stage. Any performance work on base
  terrain has to be done twice or the duplication has to be resolved first.

**Status:** Idea only, no design. Evaluation not started.

### PL-10 — Per-layer inspection overlay (Phase V / Adapter)
**Date:** 2026-08-19  
**Idea:** There is no clean way to display a single mask layer (e.g. `Vegetation`) in
isolation over the runtime map. Layer isolation is step 2 of the standing visual smoke test
protocol, so the protocol currently asks for something the tooling does not make easy.
Observed during the W-aux.d smoke test.  
**Scope:** Adapter-side only; touches `ScalarOverlayRenderer` / `PCGRuntimeOverlay` /
`PCGMapCompositeVisualization`. Small, and it pays for itself on every future stage.  
**Status:** Idea only, no design.

### PL-11 — Biome coverage at the reference preset (Phase M / Biome)
**Date:** 2026-08-19  
**Idea:** `biomesNonZero` is 10 / 8 / 8 of 13 at seeds 56 / 8 / 243, with SubtropicalDesert,
TropicalSeasonalForest and TropicalRainforest at zero in all three, and Grassland and
TemperateRainforest in the single or double digits of cells. Surfaced by W-aux.d
verification. Belongs to `Stage_Biome2D` and its temperature/moisture bands, not to
vegetation — recalibrating vegetation densities for those biomes would change nothing.  
**Status:** Idea only, no design. Not scheduled.
