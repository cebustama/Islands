# SSoT Contracts

Status: Active
Purpose: Cross-cutting package contracts and governance-relevant technical rules.

## Documentary contracts
- Implemented truth must not live primarily in `wip/`.
- Planning must not be used as implementation authority.
- Reference docs may explain a system, but they do not overrule subsystem SSoTs.
- Historical docs must state their role explicitly once superseded.

## Package boundary contracts
- The governed docs root for this package is `Documentation~/`.
- Root `README.md` is a package entrypoint, not the governance spine.
- No second governance spine should be created under `Runtime/`, `Editor/`, or subsystem code folders.

## PCG cross-cutting technical contracts
- Determinism is a package-level expectation for the active PCG path.
- Stable execution order and stable hashing/golden verification are first-class governance concerns.
- Core PCG runtime follows a grid-first, adapters-last architecture.
- Legacy map-generation documents do not define the new PCG runtime unless explicitly re-promoted.

## PCG configuration override contracts (N5.b)
- ScriptableObject configuration assets (`MapGenerationPreset`, `NoiseSettingsAsset`) follow
  the override-at-resolve pattern: when an asset is assigned to a slot, the asset's values
  replace inline Inspector values. When the slot is null, inline values are used unchanged.
- Resolution is deterministic and happens at the point of use (ToTunables, BuildTunables),
  never lazily or asynchronously.
- Serialization format changes on governed configuration types must be documented in the
  changelog with explicit "serialization break" notice.

## PCG stage-field dependency contracts (M2.a)
- Stages may declare optional field reads gated by `MapContext2D.IsFieldCreated(MapFieldId)`.
  When a declared optional input is absent, the stage must fall back to a documented,
  deterministic legacy behavior (Option A fallback) — never skip silently, never throw.
- Stage-local tunables (constants or fields owned by a single stage) are permitted and must
  not be promoted to `MapTunables2D` or `MapGenerationPreset` unless they become cross-stage
  or user-authored. Defaults must preserve prior behavior when a stage is reordered or
  gains new optional inputs.
- Stage reordering that introduces a new upstream field producer (e.g. `Stage_Biome2D`
  placed before `Stage_Vegetation2D` in M2.a) must be accompanied by a companion pipeline
  golden test asserting that pre-reorder stage outputs (Land, LandCore, Height, CoastDist,
  etc.) are unchanged. See `MapPipelineRunner2DGoldenM2Tests.DoesNotInvalidate_G_Goldens`.
- When a stage gains biome-aware or field-aware behavior, it must carry a dual-golden test
  pattern: one golden for the legacy (field-absent) path and one for the field-aware path.
  Both goldens are locked post-capture. See `StageVegetation2DTests` constants
  `ExpectedVegetationHash64_Legacy` and `ExpectedVegetationHash64_M2a`.
- Biome-driven suppression contracts: any stage consuming `MapFieldId.Biome` must honor
  (a) water/Unclassified sentinel suppression — cells outside `MapLayerId.Land` receive no
  stage output, and (b) zero-density biome suppression — biomes whose authored density
  parameter is zero (e.g. `BiomeType.Snow` with `vegetationDensity = 0`) receive no stage
  output. These are enforced as hard invariants (see `StageVegetation2DTests` M2a-7 and
  M2a-8).

## PCG stage-field overlay contracts (M2.b)

### Overlay region field contract
`MapFieldId.BiomeRegionId` (value 5) encodes contiguous biome regions as integer IDs stored
as floats. The sentinel value is 0 — assigned to all water/non-Land cells and to any land
cell whose biome is the Unclassified sentinel. Land cells receive a 1-based integer ID
(1, 2, 3, …) uniquely identifying a contiguous same-biome region within a single map run.
IDs are compact but not necessarily consecutive after speck merging.

### Speck merge tie-break rule
Specks (regions below the minimum-size threshold) are merged into the largest 4-adjacent
neighbour by cell count. When two or more neighbours share the same maximum count, the
tie is broken by lowest anchor index in row-major order (index = `x + y * width` of the
region's first discovered cell). This rule is deterministic and must not be changed without
re-capturing all M2.b golden hashes.

### Cross-seed stability (R-7 explicit non-goal)
`MapFieldId.BiomeRegionId` values are **intra-map stable only**. The same physical region
on two maps generated with different seeds will receive different integer IDs. Region IDs
must not be persisted, serialised, or compared across seeds. This is an explicit non-goal:
guaranteeing cross-seed region identity would require a stable region-key scheme (e.g.
biome type + spatial anchor hash) that is not implemented and not planned.
Any consumer that requires stable cross-seed region keys must define and own that mapping
layer independently, outside the PCG pipeline.

## Notes
This file is intentionally small at the start of migration.
It should grow only when a rule clearly spans multiple subsystem authorities.
