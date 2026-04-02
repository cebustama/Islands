# Islands.PCG — Map Pipeline by Layers SSoT

Status: Active (implemented slice only)
Authority: Primary subsystem authority for implemented Map Pipeline by Layers behavior.
Scope: Implemented F0–Phase G + F2c runtime truth and active contracts for Map Pipeline by Layers.
Out of scope: Phase H+ roadmap work, legacy tilemap map generation, sample-only inspector convenience.

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

Anything from Phase H onward is planning only and belongs in `planning/active/PCG_Roadmap.md`.
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
- `islandRadius01`
- `waterThreshold01`
- `islandSmoothFrom01`
- `islandSmoothTo01`
- `islandAspectRatio` — ellipse aspect ratio; 1.0 = circle, >1 = wide, <1 = tall; clamped [0.25, 4.0] *(F2b)*
- `warpAmplitude01` — domain warp amplitude as fraction of min(w,h); 0.0 = no warp; clamped [0, 1] *(F2b)*
  Warp noise arrays always consumed from ctx.Rng regardless of value (stable RNG consumption count).
- deterministic clamp/order rules apply to all fields
- stage-specific tunables stay on the stage unless they clearly become map-wide contracts

### Run context
`MapContext2D`
- owns layer/field memory
- stable index-based registries
- single run RNG
- deterministic allocation/clear rules
- throws on missing layer/field access

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

### F3 topology + hills contracts
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
- writes `ShallowWater`
- `ShallowWater` = NOT Land AND (4-adjacent to at least one Land cell)
- `ShallowWater ⊆ NOT Land`
- `ShallowWater ∩ Land == ∅`
- `ShallowWater ∩ DeepWater` is intentionally non-empty: coastal cells are simultaneously
  DeepWater (border-connected ocean, F2) and ShallowWater (land-adjacent ring, F4)
- ring thickness is exactly 1 cell (4-adjacent only); no tunable
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

## Known limitations
- Lantern remains mask-first and single-layer-first
- Height visualization is not yet governed
- CoastDist (ScalarField2D) is not directly visualizable via the current lantern (shows all-OFF if selected as view layer); verified via golden tests only
- Composite per-layer color styling is sample-side convenience, not subsystem truth
- F2/F3 keep some internal constants fixed for golden stability (NoiseCellSize, WarpCellSize, NoiseAmplitude, QuantSteps)
- `MapFieldId.Moisture` is registered but not yet written; ownership deferred to Phase M
- `MapLayerId.Paths` is registered but not yet written; ownership deferred to Phase O

## Not governed here
- Phase H+ roadmap work
- `Paths` layer write (deferred to Phase O — requires Phase N POI placement as prerequisite)
- Legacy tilemap generation documents
