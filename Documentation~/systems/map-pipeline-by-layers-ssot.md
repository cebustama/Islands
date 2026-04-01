# Islands.PCG — Map Pipeline by Layers SSoT

Status: Active (implemented slice only)
Authority: Primary subsystem authority for implemented Map Pipeline by Layers behavior.
Scope: Implemented F0–F3 runtime truth and active contracts for Map Pipeline by Layers.
Out of scope: F4–F6 roadmap work, legacy tilemap map generation, sample-only inspector convenience.

## Purpose
This document governs the implemented and test-gated truth of the Map Pipeline by Layers subsystem.

## Boundary
This SSoT covers only the currently implemented vertical slice:
- F0 Context + contracts
- F1 Map lantern skeleton
- F2 Base terrain
- F3 Hills + topology

Anything from F4 onward is planning only and belongs in `planning/active/PCG_Roadmap.md`.

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
- Paths
- Stairs
- Vegetation
- Walkable
- LandEdge
- LandInterior

Current `MapFieldId`:
- Height
- Moisture

### Inputs
`MapInputs`
- Seed is sanitized to >= 1
- Domain is explicit
- Tunables are deterministic

### Tunables
`MapTunables2D`
- `islandRadius01`
- `waterThreshold01`
- `islandSmoothFrom01`
- `islandSmoothTo01`
- deterministic clamp/order rules
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

### F3 topology + hills contracts
`MaskTopologyOps2D`
- 4-neighborhood/cardinal topology only
- out-of-bounds neighbors count as OFF
- row-major scans only
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
- `Stage_BaseTerrain2D`
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

## Determinism rules
- stable seed sanitation
- stable registry ordering
- no uninitialized layer/field memory
- stage execution order is array order
- row-major scans
- deterministic flood fill queue/neighbor ordering
- deterministic topology neighbor semantics (4-neighborhood)
- stable connected-components discovery order when labeling is used
- snapshot-hash gates for masks

## Test-gated behavior
- trivial pipeline determinism + golden
- base terrain stage determinism
- base terrain invariants
- base terrain stage goldens
- F2 pipeline golden
- topology ops micro-tests
- hills stage determinism
- hills stage invariants
- F3 stage goldens
- F3 pipeline golden

## Known limitations
- Lantern remains mask-first and single-layer-first
- Height visualization is not yet governed
- Composite per-layer color styling is sample-side convenience, not subsystem truth
- F2/F3 keep some internal constants fixed for golden stability

## Not governed here
- F4 Shore + shallow water
- F5 Vegetation + fixups
- F6 Paths / stairs / placement
- Legacy tilemap generation documents
