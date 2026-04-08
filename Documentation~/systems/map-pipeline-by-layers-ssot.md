# Islands.PCG — Map Pipeline by Layers SSoT

Status: Active (implemented slice only)
Authority: Primary subsystem authority for implemented Map Pipeline by Layers behavior.
Scope: Implemented F0–Phase N5.b runtime truth and active contracts for Map Pipeline by Layers.
Out of scope: Phase N5.c+ roadmap work, legacy tilemap map generation, sample-only inspector convenience.

## Purpose
This document governs the implemented and test-gated truth of the Map Pipeline by Layers subsystem.

## Boundary
This SSoT covers only the currently implemented vertical slice:
- F0 Context + contracts
- F1 Map lantern skeleton
- F2 Base terrain (F2b reformed: ellipse + domain-warp silhouette; F2c: optional external shape input)
- F3 Hills + topology
- F4 Shore + ShallowWater
- F5 Vegetation
- F6 Traversal (Walkable + Stairs)
- Phase G Morphology (LandCore + CoastDist)
- Phase H Visualization (PCGViewMode enum; scalar field color-ramp view; per-layer preset colors)
- Phase H1 Composite Visualization (PCGMapCompositeVisualization; CPU Texture2D composite; CompositeLayerSlot)
- Phase H2 Data Export (MapDataExport; MapExporter2D)
- Phase H2b Tilemap Adapter (TilemapAdapter2D; TilemapLayerEntry; PCGMapTilemapSample;
  Islands.PCG.Adapters.Tilemap separate asmdef)
- Phase H2c–H7 Tilemap Visualization, Procedural Tiles, MapGenerationPreset, TilesetConfig,
  Multi-layer Tilemaps, Rule Tiles, Map Navigation Sample
- Phase J2 Height Redistribution
- Phase N2 Spline Remapping + Post-N2 Fixes
- Phase N4 Noise Settings Infrastructure + F2 Noise Upgrade
- Phase F3b Height-Coherent Hills
- Phase N5.a Base Shape Selector
- Phase N5.b Noise Settings Assets (NoiseSettingsAsset; TerrainNoiseSettings extensions;
  PropertyDrawer; configuration override pattern)

`MapLayerId.Paths` is registered but not yet written; its authoritative write belongs in Phase O.

## Subsystem intent
Map Pipeline by Layers is a general deterministic map-generation pipeline built on mask/field layers inside a `MapContext2D`, executed through deterministic `IMapStage2D` stages, with rendering/spawning deferred to adapters.

## Active contracts
### Registries
Current `MapLayerId`:
- Land
- DeepWater
- ShallowWater
- HillsL1
- HillsL2
- Paths *(registered; not yet written — write deferred to Phase O)*
- Stairs
- Vegetation
- Walkable
- LandEdge
- LandInterior
- LandCore *(Phase G)*

Current `MapFieldId`:
- Height
- Moisture *(registered; not yet written — write deferred to Phase M)*
- CoastDist *(Phase G)*

### Inputs
`MapInputs`
- Seed is sanitized to >= 1
- Domain is explicit
- Tunables are deterministic
- `ShapeInput` (optional, default = `MapShapeInput.None`): when `HasShape = false` (default),
  `Stage_BaseTerrain2D` uses the F2b internal ellipse+warp silhouette; existing goldens unaffected.

`MapShapeInput` *(F2c)*
- `HasShape`: false = use internal ellipse+warp path; true = use external mask.
- `Mask` (MaskGrid2D): ON cells are land-eligible; OFF cells forced to water. Valid only when `HasShape = true`.
- Caller owns and disposes the mask; `MapInputs` holds by value.
- Dimensions must match the pipeline domain; `Stage_BaseTerrain2D` throws `ArgumentException` on mismatch.

### Tunables
`MapTunables2D`
- `shapeMode` — `IslandShapeMode` enum: Ellipse (default), Rectangle, NoShape, Custom.
  Selects built-in base shape generator. Overridden when `MapShapeInput.HasShape` is true.
  Ellipse = pre-N5.a behavior (bit-identical). Rectangle = axis-aligned with Chebyshev
  distance + smoothstep. NoShape = height IS pure noise, water threshold carves coastlines.
  Custom = Ellipse fallback (sprite-to-mask bridge is visualization-side). *(N5.a)*
- `islandRadius01`
- `waterThreshold01`
- `islandSmoothFrom01`
- `islandSmoothTo01`
- `islandAspectRatio` — ellipse/rectangle aspect ratio; 1.0 = circle/square, >1 = wide, <1 = tall; clamped [0.25, 4.0] *(F2b)*
  For Rectangle mode: controls width/height ratio (halfExtentX = radius * aspect, halfExtentY = radius).
- `warpAmplitude01` — domain warp amplitude as fraction of min(w,h); 0.0 = no warp; clamped [0, 1] *(F2b)*
  Applied to Ellipse and Rectangle modes. NoShape ignores warp geometrically.
  Warp noise arrays always filled regardless of mode (stable downstream state).
- `terrainNoise` — `TerrainNoiseSettings` struct for height perturbation noise. *(N4)*
- `warpNoise` — `TerrainNoiseSettings` struct for domain warp noise. *(N4)*
- `heightQuantSteps` — height quantization steps (0 = none, 1024 = smooth). *(N4)*
- `hillsThresholdL1` — Height threshold for HillsL1 slopes; [0..1], default 0.65. *(F3b)*
- `hillsThresholdL2` — Height threshold for HillsL2 peaks; [0..1], default 0.80. Clamped >= hillsThresholdL1. *(F3b)*
- `heightRedistributionExponent` — power-curve exponent; 1.0 = identity. *(J2)*
- `heightRemapSpline` — piecewise-linear height remap; default = identity. *(N2)*
- deterministic clamp/order rules apply to all fields
- stage-specific tunables stay on the stage unless they clearly become map-wide contracts

### Configuration Assets (N5.b)

`NoiseSettingsAsset` (`Runtime/Layout/Maps/NoiseSettingsAsset.cs`)
- ScriptableObject wrapping a single `TerrainNoiseSettings` struct.
- `[CreateAssetMenu]` (Islands/PCG/Noise Settings, order 110).
- Override-at-resolve pattern: when assigned to a `MapGenerationPreset` or visualization
  component noise slot, the asset's settings replace inline values. When null, inline
  values are used unchanged.
- Resolution chain (terrain noise example):
  1. `MapGenerationPreset.terrainNoiseAsset` assigned → use asset's `Settings`.
  2. `terrainNoiseAsset` null → use `MapGenerationPreset.terrainNoiseSettings` inline struct.
  3. No preset assigned on component → check component's own `terrainNoiseAsset` → component's inline struct.
- Same pattern for warp noise (`warpNoiseAsset` / `warpNoiseSettings`).
- Final resolved `TerrainNoiseSettings` is passed to `MapTunables2D` and consumed by
  `MapNoiseBridge2D.FillNoise01`.

`TerrainNoiseSettings` extensions (N5.b — carried, not yet functional until N5.c):
- `WorleyDistanceMetric` enum (Euclidean, SmoothEuclidean, Chebyshev) — default Euclidean.
- `WorleyFunction` enum (F1, F2, F2MinusF1, CellAsIslands) — default F1.
- `FractalMode` enum (Standard, Ridged) — default Standard.
- `ridgedOffset` (float, 1.0) — ridged multifractal offset.
- `ridgedGain` (float, 2.0) — ridged multifractal gain.

All defaults produce identity behavior (no change to noise output). Fields become
functional when N5.c adds corresponding `case` branches in `MapNoiseBridge2D.FillNoise01`.

### Run context
`MapContext2D`
- owns layer/field memory
- stable index-based registries
- single run RNG
- deterministic allocation/clear rules
- throws on missing layer/field access (`GetLayer`, `GetField`)

### Runner
`MapPipelineRunner2D`
- stable array-order stage execution
- resets run state through `BeginRun`

### F2 base terrain contracts
`Stage_BaseTerrain2D` (F2b shape pipeline)
- reads tunables: `islandRadius01`, `waterThreshold01`, `islandSmoothFrom01/To01`,
  `islandAspectRatio`, `warpAmplitude01`
- writes `Height` (ScalarField2D), `Land` (MaskGrid2D), `DeepWater` (MaskGrid2D)
- shape pipeline per pixel:
  1. Sample warpX, warpY from two coarse noise grids (WarpCellSize=16, lower frequency)
  2. Displace pixel sampling point: `pw = p + float2(wx, wy) * warpAmplitude01 * min(w,h)`
  3. Ellipse distance from displaced point to center: `distSq = v.x² / aspect² + v.y²`
  4. Smoothstep radial falloff on `distSq`
  5. Add height perturbation noise (NoiseCellSize=8) inside island only
- RNG consumption order: island noise → warpX noise → warpY noise (stable regardless of tunables)
- `aspect=1.0 + warp=0.0` => geometrically identical circle to pre-F2b implementation
- `DeepWater` = border-connected NOT Land (deterministic flood fill)
- `DeepWater ∩ Land == ∅`
- does not introduce new `MapLayerId` or `MapFieldId` entries

`Stage_BaseTerrain2D` (F2c shape-input path — opt-in via `MapInputs.ShapeInput.HasShape = true`)
- All three RNG arrays (island noise, warpX, warpY) still filled in identical order; downstream RNG state unchanged.
- `mask01 = shape.GetUnchecked(x, y) ? 1f : 0f` replaces the ellipse+warp distance computation.
- Noise perturbation still applied: `h01 = mask01 + (n - 0.5) * NoiseAmplitude * mask01`.
- `Land ⊆ shape mask`: no Land cell exists outside the provided shape (guaranteed by construction).
- F2b goldens unchanged. F2c goldens locked for center-circle test shape (64×64, seed=12345, radius=20).

`Stage_BaseTerrain2D` (N5.a shape mode selection)
- `shapeMode` from `MapTunables2D.shapeMode` selects built-in shape generator.
  Priority: `MapShapeInput.HasShape == true` takes unconditional precedence over shapeMode.
- **Ellipse** (default): F2b path unchanged. Radial smoothstep + domain warp. Bit-identical
  to pre-N5.a output.
- **Rectangle**: Chebyshev-normalized edge distance to axis-aligned rectangle.
  `halfExtentX = radius * aspect`, `halfExtentY = radius`.
  `rectDist01 = max(|v.x| / halfExtentX, |v.y| / halfExtentY)`.
  `smoothstep(fromSq, toSq, rectDist01^2)`. Domain warp displaces before evaluation.
  Reuses existing tunables (radius, aspect, smoothFrom/To, warp).
- **NoShape**: `h01 = n` (noise sample IS the height). No mask, no perturbation formula.
  Water threshold alone carves coastlines. Produces continent-like shapes from noise.
  Warp arrays filled but not read (downstream state unchanged).
- **Custom**: Falls back to Ellipse when no external shape is provided. Signals
  Inspector/visualization consumers to show a Texture2D slot for sprite-to-mask rasterization.
- No new `MapLayerId` or `MapFieldId`.
- No golden break at defaults (Ellipse). New goldens locked for Rectangle and NoShape modes.

### F3 / F3b — Hills + Topology (Stage_Hills2D)

**Pipeline position:** After Stage_BaseTerrain2D (F2), before Stage_Shore2D (F4).

**Reads (read-only):**
- `Land` (MapLayerId 0) — eligibility mask
- `Height` (MapFieldId 0) — elevation source for threshold classification

**Writes:**
- `HillsL1` (MapLayerId 7) — passable slopes: `Land AND Height >= hillsThresholdL1 AND NOT HillsL2`
- `HillsL2` (MapLayerId 8) — impassable peaks: `Land AND Height >= hillsThresholdL2`
- `LandEdge` (MapLayerId 9) — `Land AND 4-adjacent-to-any-non-Land-cell`
- `LandInterior` (MapLayerId 10) — `Land AND NOT LandEdge`

**Tunables (on MapTunables2D):**
- `hillsThresholdL1` (float, [0..1], default 0.65) — Height value above which Land cells become HillsL1 slopes.
- `hillsThresholdL2` (float, [0..1], default 0.80) — Height value above which Land cells become HillsL2 peaks. Clamped >= hillsThresholdL1.

**Subset invariants:**
- `HillsL2 ⊆ Land`
- `HillsL1 ⊆ Land`
- `HillsL1 ∩ HillsL2 == ∅` (disjoint — a cell is L1 or L2, never both)
- `LandEdge ∪ LandInterior == Land`
- `LandEdge ∩ LandInterior == ∅`

**RNG / Noise:** None. Zero ctx.Rng consumption. No MapNoiseBridge2D usage.
Purely algorithmic: reads Height field, classifies by threshold. LandEdge/LandInterior
derived by MaskTopologyOps2D.ExtractEdgeAndInterior4 (4-neighbor boundary detection).

**No-mutate:** Does not modify Land, DeepWater, or Height.

**Phase history:** Original F3 used independent SimplexPerlin noise via MapNoiseBridge2D
(salt 0xA511E9B3) on LandInterior with MaskTopologyOps2D neighbor counting. HillsL2 was
a subset of HillsL1 (overlap). F3b (2026-04-08) replaced this with height-threshold
classification, making hills spatially coherent with the Height field. Full golden break
for F3+ hashes.
`MaskTopologyOps2D`
- 4-neighborhood/cardinal topology only
- out-of-bounds neighbors count as OFF
- row-major scan
- optional connected components use stable row-major discovery order

`Stage_Hills2D`
- reads `Land` and `DeepWater`
- writes `LandEdge`, `LandInterior`, `HillsL1`, `HillsL2`
- `LandEdge ∪ LandInterior == Land`
- `LandEdge ∩ LandInterior == ∅`
- `HillsL1 ⊆ LandInterior`
- `HillsL2 ⊆ HillsL1`
- does not mutate `Land`, `DeepWater`, or `Height`

`MapNoiseBridge2D`
- acts as a narrow deterministic bridge from map pipeline code to the shared Noise runtime
- does not introduce new authoritative fields
- is support infrastructure inside the implemented slice, not a separate promoted subsystem authority

### F4 shore contracts
`Stage_Shore2D`
- reads `Land` (read-only)
- reads `Height` (read-only; only when `ShallowWaterDepth01 > 0` or `MidWaterDepth01 > 0`)
- writes `ShallowWater`
- writes `MidWater` (only when `MidWaterDepth01 > 0`, F4c)
- when `ShallowWaterDepth01 == 0` (default):
  `ShallowWater` = NOT Land AND (4-adjacent to at least one Land cell)
  Ring thickness is exactly 1 cell (4-adjacent only).
- when `ShallowWaterDepth01 > 0` (F4b):
  `ShallowWater` = NOT Land AND (4-adjacent to at least one Land cell
  OR Height >= waterThreshold − ShallowWaterDepth01)
  Adjacency ring always included; depth band extends coverage.
- when `MidWaterDepth01 > 0` (F4c):
  `MidWater` = NOT Land AND NOT ShallowWater AND Height >= waterThreshold − MidWaterDepth01
  MidWaterDepth01 must be > ShallowWaterDepth01 for a visible band.
  When == 0 (default), MidWater layer is not allocated.
- `ShallowWater ⊆ NOT Land`
- `ShallowWater ∩ Land == ∅`
- `MidWater ⊆ NOT Land`
- `MidWater ∩ Land == ∅`
- `MidWater ∩ ShallowWater == ∅`
- `ShallowWater ∩ DeepWater` is intentionally non-empty
- does not mutate `Land`, `DeepWater`, or `Height`
- does not consume `ctx.Rng` (no noise, no randomness)

### F5 vegetation contracts
`Stage_Vegetation2D`
- reads `LandInterior` (read-only), `HillsL2` (read-only), `ShallowWater` (read-only)
- writes `Vegetation`
- `Vegetation ⊆ Land`
- `Vegetation ⊆ LandInterior` (the LandEdge ring is always excluded)
- `Vegetation ∩ HillsL2 == ∅` (no vegetation on hill peaks)
- `Vegetation ∩ ShallowWater == ∅` (implied by `⊆ LandInterior`; stated explicitly for contract clarity)
- noise: SimplexPerlin01 via `MapNoiseBridge2D`, fixed salt `0xB7C2F1A4u`, threshold 0.40f
- does not mutate `Land`, `LandInterior`, `HillsL2`, `ShallowWater`, or `Height`
- does not write `MapFieldId.Moisture` (deferred to Phase M)

### F6 traversal contracts
`Stage_Traversal2D`
- reads `Land` (read-only), `HillsL1` (read-only), `HillsL2` (read-only)
- writes `Walkable`, `Stairs`
- `Walkable` = `Land AND NOT HillsL2`
- `Walkable ⊆ Land`
- `Walkable ∩ HillsL2 == ∅`
- `HillsL1` cells (slopes) are included in `Walkable`; only `HillsL2` peaks are excluded
- `Stairs` = cells in `HillsL1 AND NOT HillsL2` that are 4-adjacent to at least one `HillsL2` cell
- `Stairs ⊆ HillsL1`
- `Stairs ∩ HillsL2 == ∅`
- `Stairs` may be empty if `HillsL2` is empty (e.g. flat maps); this is not a defect
- `Stairs ⊆ Walkable` (all Stairs cells satisfy `Land AND NOT HillsL2` by construction)
- does not mutate `Land`, `HillsL1`, `HillsL2`, `ShallowWater`, `Vegetation`, or `Height`
- does not consume `ctx.Rng` (no noise, no randomness)
- does not write `MapLayerId.Paths` (deferred to Phase O)

### Phase G morphology contracts
`MaskMorphologyOps2D`
- 4-neighborhood only; out-of-bounds neighbors count as OFF
- row-major scans; fixed neighbor visit order W/E/S/N
- no unordered collections
- `Erode4Once`: a cell is ON in dst iff ON in src AND all 4 cardinal neighbors are ON in src
- `Erode4(radius)`: repeated single-cell erosion; radius 0 copies src unchanged; allocates one Temp buffer for ping-pong
- `BfsDistanceField`: multi-source BFS from seed cells through passable cells; seeds enqueued row-major; distances written as float; unreached cells receive -1f sentinel

`Stage_Morphology2D`
- reads `Land` (read-only), `LandEdge` (read-only)
- writes `LandCore` (MapLayerId 11), `CoastDist` (MapFieldId 2)
- stage-local tunables: `ErodeRadius` (default 3), `CoastDistMax` (default 0 = auto: min(w,h)/2)
- `LandCore ⊆ Land`
- `LandCore ⊆ LandInterior` (guaranteed when ErodeRadius >= 1; erosion removes all edge cells)
- `LandCore.CountOnes() < Land.CountOnes()` for any non-degenerate island
- `CoastDist == 0f` at all `LandEdge` cells
- `CoastDist > 0f` at all `LandInterior` cells reachable within CoastDistMax steps
- `CoastDist == -1f` at all non-Land cells and any Land cells beyond CoastDistMax
- does not consume `ctx.Rng` (no noise, no randomness)
- does not mutate `Land`, `LandEdge`, `LandInterior`, `HillsL1`, `HillsL2`,
  `ShallowWater`, `Vegetation`, `Walkable`, `Stairs`, or `Height`

### Phase H2 adapter contracts
`MapDataExport`
- Managed snapshot of a completed `MapContext2D`. Lifetime independent of the source context.
- Indexing: row-major, `index = x + y * Width` — consistent with `GridDomain2D.Index`.
- `HasLayer(id)` / `HasField(id)`: true iff the layer/field was created in the source context.
- `GetLayer(id)` → `bool[]`: throws `InvalidOperationException` if absent.
- `GetCell(id, x, y)` → `bool`: throws `ArgumentOutOfRangeException` on OOB.
- `GetField(id)` → `float[]`: throws `InvalidOperationException` if absent.
- `GetValue(id, x, y)` → `float`: throws `ArgumentOutOfRangeException` on OOB.
- Instantiated only via `MapExporter2D.Export`; constructor is `internal`.

`MapExporter2D`
- Static adapter; read-only. Does not write to or modify the context.
- Exports all layers/fields present in the context; absent ones produce null slots.
- Layer copy: row-major scan via `MaskGrid2D.GetUnchecked(x, y)` (bounds-guaranteed by domain).
- Field copy: flat index scan via `ScalarField2D.Values[j]`.
- Deterministic: same context state ⇒ identical export output.
- Extensible: new `MapLayerId`/`MapFieldId` entries are automatically exported without contract changes.
- Throws `ArgumentNullException` if context is null.
- Adapters-last invariant: this is the boundary between the headless pipeline and downstream consumers.
  Game systems, Unity adapters, and tools read `MapDataExport`; they do not touch `MapContext2D`.

### Phase H2b tilemap adapter contracts
`TilemapLayerEntry`
- `[Serializable]` struct. Maps one `MapLayerId` to one `TileBase` asset.
- Entries with a null `Tile` field are silently skipped during Apply.

`TilemapAdapter2D`
- Static adapter; read-only consumer of `MapDataExport`. Never writes to pipeline state.
- `Apply(MapDataExport, Tilemap, TilemapLayerEntry[], TileBase fallback, bool clearFirst, bool flipY)`
- Priority: entries evaluated low→high (array order); last matching layer per cell wins.
  This is rendering priority only; pipeline generation order is unaffected.
- Absent layers (not in export): silently skipped; no exception.
- `clearFirst = true` (default): calls `ClearAllTiles()` before stamping.
- `flipY = true`: tile placed at `(x, Height-1-y, 0)` instead of `(x, y, 0)`.
- Deterministic: same export + same priority table ⇒ identical tilemap output.
- Lives in `Islands.PCG.Adapters.Tilemap` assembly (separate from `Islands.PCG.Runtime`).

## Implemented surface
### F0
- `MapIds2D`
- `MapInputs`
- `MapTunables2D`
- `MapContext2D`
- `IMapStage2D`
- `MapPipelineRunner2D`

### F1
- `PCGMapVisualization` mask-layer lantern
- layer selection
- missing-layer all-off fallback
- dirty regeneration

### F2
- `Stage_BaseTerrain2D` (F2b: ellipse + domain-warp silhouette; F2c: optional external shape input)
- `MapShapeInput` *(F2c)*
- `MaskFloodFillOps2D`
- authoritative outputs:
  - `Height` field
  - `Land` mask
  - `DeepWater` mask

### F3
- `MaskTopologyOps2D`
- `MapNoiseBridge2D`
- `Stage_Hills2D`
- authoritative outputs:
  - `LandEdge` mask
  - `LandInterior` mask
  - `HillsL1` mask
  - `HillsL2` mask
- lantern support for hills/topology layer inspection

### F4
- `Stage_Shore2D`
- authoritative outputs:
  - `ShallowWater` mask

### F5
- `Stage_Vegetation2D`
- authoritative outputs:
  - `Vegetation` mask
- lantern support for vegetation layer inspection (`enableVegetationStage` toggle, `stagesF5` array)

### F6
- `Stage_Traversal2D`
- authoritative outputs:
  - `Walkable` mask
  - `Stairs` mask
- lantern support for traversal layer inspection (`enableTraversalStage` toggle, `stagesF6` array)

### Phase G
- `MaskMorphologyOps2D`
- `Stage_Morphology2D`
- authoritative outputs:
  - `LandCore` mask (MapLayerId 11)
  - `CoastDist` field (MapFieldId 2)
- lantern support for morphology layer inspection (`enableMorphologyStage` toggle, `stagesG` array)

### Phase H
- `PCGMapVisualization` extended with `PCGViewMode` enum (`MaskLayer` / `ScalarField`)
- `MapContext2D` extended with `GetField(MapFieldId)` public method
- scalar field view mode: `viewField`, `scalarMin`, `scalarMax`; normalization via `PackFromFieldAndUpload`
- per-layer preset ON colors: `useLayerPresetColors` toggle, `layerPresetOnColors Color[12]`
- sample-side only; no new MapLayerId, MapFieldId, or runtime stage contracts

### Phase H1
- `PCGMapCompositeVisualization` — new sample component alongside the diagnostic lantern
- composites all active layers into a CPU `Texture2D` via `SetPixels32`; `FilterMode.Point`
- priority order (low → high): DeepWater → ShallowWater → Land → LandCore → Vegetation
  → HillsL1 → HillsL2 → Stairs → LandEdge
- `CompositeLayerSlot` `[Serializable]` struct: label + color + enabled, one per MapLayerId
- optional multiplicative scalar-field tint overlay (Height or CoastDist)
- composite pixel hash (FNV-1a over Color32) logged per rebuild as informal visual golden
- sample-side only; no new MapLayerId, MapFieldId, or runtime stage contracts

### Phase H2
- `MapDataExport` — managed snapshot class (`Runtime/PCG/Layout/Maps/MapDataExport.cs`)
- `MapExporter2D` — static read-only adapter (`Runtime/PCG/Layout/Maps/MapExporter2D.cs`)
- adapters-last boundary: `MapDataExport` is the governed interface between the headless pipeline
  and all downstream consumers (game systems, Unity adapters, tools)
- no new MapLayerId, MapFieldId, or runtime stage contracts

### Phase H2b
- `TilemapLayerEntry` — serializable struct (`Runtime/PCG/Adapters/Tilemap/TilemapLayerEntry.cs`)
- `TilemapAdapter2D` — static read-only adapter (`Runtime/PCG/Adapters/Tilemap/TilemapAdapter2D.cs`)
- `PCGMapTilemapSample` — sample MonoBehaviour (`Runtime/PCG/Adapters/Tilemap/PCGMapTilemapSample.cs`)
- `Islands.PCG.Adapters.Tilemap.asmdef` — separate assembly; references `Islands.PCG.Runtime`;
  keeps Unity.Tilemaps dependency isolated from the headless runtime
- scene: `Runtime/PCG/Samples/PCG Map Tilemap/PCG Map Tilemap.unity`
- adapters-last invariant preserved; no new MapLayerId, MapFieldId, or runtime stage contracts

### Phase N5.b
- `NoiseSettingsAsset` — ScriptableObject noise configuration asset (`Runtime/Layout/Maps/NoiseSettingsAsset.cs`)
- `TerrainNoiseSettings` extended with `WorleyDistanceMetric`, `WorleyFunction`, `FractalMode`,
  `ridgedOffset`, `ridgedGain` (carried, not functional until N5.c)
- `IEquatable<TerrainNoiseSettings>` — struct equality for dirty-tracking
- `TerrainNoiseSettingsDrawer` — Editor-only PropertyDrawer with conditional visibility
- `MapGenerationPreset` extended with `terrainNoiseAsset` and `warpNoiseAsset` slots;
  refactored to embedded `TerrainNoiseSettings` structs (serialization break)
- All three visualization components refactored to embedded noise structs + asset slots
- `PCGMapTilemapVisualizationEditor` updated for noise asset slot drawing
- no new MapLayerId, MapFieldId, or runtime stage contracts

## Determinism rules
- stable seed sanitation
- stable registry ordering
- no uninitialized layer/field memory
- stage execution order is array order
- row-major scans
- deterministic flood fill queue/neighbor ordering
- deterministic topology neighbor semantics (4-neighborhood)
- stable connected-components discovery order when labeling is used
- snapshot-hash gates for masks (MaskGrid2D.SnapshotHash64)
- FNV-1a float-bit hash gates for scalar fields (HashScalarField in test helpers)
- warp noise arrays always consumed from ctx.Rng regardless of warpAmplitude01 value (stable RNG consumption count)
- export determinism: same context state ⇒ identical MapDataExport output (managed array values)
- tilemap adapter determinism: same export + same priority table ⇒ identical Tilemap output

## Test-gated behavior
- trivial pipeline determinism + golden
- base terrain stage determinism
- base terrain invariants
- base terrain stage goldens (Land, DeepWater)
- F2 pipeline golden
- F2c shape-path determinism
- F2c shape-path invariant (Land ⊆ shape mask)
- F2c stage goldens (Land, DeepWater — shape path)
- F2c pipeline golden
- topology ops micro-tests
- hills stage determinism
- hills stage invariants
- F3 stage goldens
- F3 pipeline golden
- shore stage determinism
- shore stage invariants (disjoint from Land, adjacency check, no-mutate)
- F4 stage golden
- F4b depth=0 matches adjacency-only (golden unchanged)
- F4b depth>0 produces wider band
- F4b depth>0 determinism
- F4b depth>0 disjoint from Land
- F4b depth>0 no-mutate
- F4c MidWater produces cells when depth>0
- F4c MidWater disjoint from Land and ShallowWater
- F4c MidWater determinism
- F4c MidWater not allocated when depth=0
- F4 pipeline golden
- vegetation stage determinism
- vegetation stage invariants (subset checks: Land, LandInterior; disjoint checks: HillsL2, ShallowWater; no-mutate)
- F5 stage golden
- F5 pipeline golden
- traversal stage determinism
- traversal stage invariants (subset checks: Walkable ⊆ Land, Stairs ⊆ HillsL1; disjoint checks: Walkable ∩ HillsL2, Stairs ∩ HillsL2; Stairs ⊆ Walkable; no-mutate)
- F6 stage goldens (Walkable, Stairs)
- F6 pipeline golden
- morphology stage determinism
- morphology stage invariants (LandCore ⊆ Land, LandCore ⊆ LandInterior, LandCore smaller than Land; CoastDist == 0 at LandEdge, CoastDist > 0 at reachable LandInterior, CoastDist == -1 at non-Land; no-mutate)
- Phase G stage goldens (LandCore mask hash, CoastDist field hash)
- Phase G pipeline golden
- Phase H2 export: empty-context export; layer round-trip fidelity (diagonal, all-ones, all-zeros);
  field round-trip fidelity (gradient); determinism; snapshot independence; null/absent/OOB guards
- Phase H2b tilemap adapter: null guards (export, tilemap, table); empty table + null fallback;
  empty table + fallback fills all cells; priority resolution (LandCore over Land);
  missing layer silently skipped; clearFirst=false preserves existing tiles;
  flipY mirrors Y coordinate; determinism gate
- Phase N5.b: NoiseSettingsAsset override resolution (asset → inline); IEquatable identity
  and inequality; new field defaults

## Known limitations
- Scalar field normalization range (scalarMin/scalarMax) is inspector-settable but not auto-ranged; CoastDist max distance varies by map size and CoastDistMax tunable
- Composite per-layer color styling is sample-side convenience, not subsystem truth
- F2/F3 noise parameters are now configurable via `TerrainNoiseSettings` (N4) and
  `NoiseSettingsAsset` (N5.b). Extended Worley/ridged fields are carried but not
  functional until N5.c adds runtime branches.
- `MapFieldId.Moisture` is registered but not yet written; ownership deferred to Phase M
- `MapLayerId.Paths` is registered but not yet written; ownership deferred to Phase O
- `PCGMapTilemapSample` regenerates only on Start or ContextMenu; live editor regeneration deferred to Phase H2c

## Not governed here
- Phase N5.c+ roadmap work (Extended Noise Palette + Ridged Multifractal and beyond)
- `Paths` layer write (deferred to Phase O — requires Phase N POI placement as prerequisite)
- Legacy tilemap generation documents
