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
- **Additive key extension is not a serialization break.** Adding a key to
  `MapGenerationPreset.ToJson()` plus its importer-table entry, where absent keys import
  as `AbsentPreserved` and the field's default preserves prior behavior, does not require
  the "serialization break" notice — old JSON remains importable and old assets remain
  valid. It must still be recorded in the changelog as an additive extension naming the
  keys. Applied at W.b for five keys: `stageToggles.regions`, `stageToggles.hydrology`,
  `hydrology.riverThresholdFraction`, `hydrology.minLakeArea`,
  `vegetation.moistureModulation`.
- `ToJson()` and the importer table change **together**. The round-trip gate
  (`MapGenerationPresetJsonRoundTripTests`) fails while only one has changed; that is the
  gate working. A promoted field must also be added to the non-default fixture, or the
  round-trip can pass by matching defaults.

## PCG stage-field dependency contracts (M2.a)
- Stages may declare optional field reads gated by `MapContext2D.IsFieldCreated(MapFieldId)`.
  When a declared optional input is absent, the stage must fall back to a documented,
  deterministic legacy behavior (Option A fallback) — never skip silently, never throw.
- Stage-local tunables (constants or fields owned by a single stage) are permitted and must
  not be promoted to `MapTunables2D` or `MapGenerationPreset` unless they become cross-stage
  or user-authored. Defaults must preserve prior behavior when a stage is reordered or
  gains new optional inputs.
- **Promotion is adjudicated per field, with a stated reason. Uniformity is not a
  reason.** Verdicts recorded at W.b (2026-08-20):
  | Field | Verdict | Reason |
  |---|---|---|
  | `enableRegionsStage` | → preset | authoring decision of the same class as the six toggles already carried |
  | `enableHydrologyStage` | → preset | same; default `false` keeps it golden-safe |
  | `hydroRiverThresholdFraction` | → preset | cross-stage: `biomeRiverFlowNorm = 0` (auto) is defined against it, so the preset already depended on a value it could not author |
  | `hydroMinLakeArea` | → preset | already user-authored on one component; leaving it out made the same preset generate different hydrology per component |
  | `hydroEpsilon` | stays component-scoped | numeric plumbing of the depression solver, not a design parameter |
  | `Stage_Vegetation2D.moistureModulation` | → preset | implemented feature unreachable from any construction path; default 0 keeps it inert |
  | `Stage_Regions2D.SpeckThreshold` | stays stage-local | constant since inception, underpins invariant R-8, no authoring demand |
- **Promotion of a previously unreachable field is an authoring-surface change, not a
  refactor.** Its preset default must equal the value the pipeline effectively used
  before promotion, asserted against a fresh stage instance rather than a literal — see
  `MapGenerationPresetTests.Defaults_WbPromotedFields_MatchPrePromotionEffectiveValues`.
  A literal would drift silently when the stage default changes.
- **Tooling that declares a gap must be retired or narrowed in the batch that closes it.**
  `MapGenerationPresetWizard`'s `HelpBox` listing the unreachable fields was narrowed at
  W.b to the one field still not carried (`hydroEpsilon`).
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

## PCG calibration entry-point contracts (W-aux.f)

### Calibration entry points for `waterThreshold01`

Any change to the `Height` composition formula invalidates every calibrated water threshold.
All of these must be updated together:

1. `MapTunables2D.Default` — the struct default; what every EditMode fixture uses.
2. `MapGenerationPreset` class field default — must equal (1), enforced by
   `ToTunables_DefaultPreset_MatchesMapTunables2DDefault`.
3–5. Serialized defaults on `PCGMapVisualization`, `PCGMapCompositeVisualization`,
   `PCGMapTilemapVisualization`. Affect newly added components only; component instances
   already saved in a scene keep their own values.
6. Every `MapGenerationPreset` asset on disk, each with its own tuned value.

The compensating factor is `(1 + terrainNoise.amplitude/2)^(−heightRedistributionExponent)`;
it is applied by hand, never automatically, because a self-adjusting threshold would hide a
recalibration from the goldens. Applied values for the 2026-08-20 recalibration are recorded
in `CURRENT_STATE.md`; the composition contract itself lives in
`systems/map-pipeline-by-layers-ssot.md` §F2.

**Decoupled consumer (W-aux.g).** `Stage_Hills2D` used to be an indirect consumer of
`waterThreshold01` through the N5.e remap, which built its thresholds on the interval
`[waterThreshold01, 1.0]`. F3b′ replaced that remap with per-run area quantiles, so Hills
no longer moves when the threshold is recalibrated. Hills is not on this list.

Test fixtures that mean "Default but X" must derive the threshold from
`MapTunables2D.Default.waterThreshold01` rather than copy the literal. Test fixtures that
assert arithmetic over the threshold must set it explicitly instead of inheriting it. The
general form of that requirement is the shadow-defaults rule below.

### Shadow defaults in test fixtures (W-aux.f)

A fixture that means "the defaults, but with X" and expresses it by copying the default
values as literals is a **shadow default**. While nobody touches the real default, the copy
agrees and nothing is visible. When the default is recalibrated, the fixture silently becomes
a different configuration while still claiming to be the same one.

The dangerous case is not the failing test — it is the symmetric one, where the copy drifts
in a direction that happens not to change the asserted output. The test then stays green
while comparing two different things.

Rule: **a test may inherit a default or assert arithmetic over it, never both.**
- Comparing against `Default` → derive the value from `Default`.
- Asserting hand-computed arithmetic → set the input explicitly inside the test.
- Pinning a default's value → hardcode it; that is the test's whole purpose.

Instances found and fixed in W-aux.f: `StageBaseTerrain2DTests.RectangleTunables()`, the
inline tunables in `N5a_Custom_WithoutShapeInput_MatchesEllipse`,
`MapGenerationPresetTests` L62 and `ToTunables_HillsL1L2_AreForwardedAsRelativeFractions`
(the latter renamed `..._AreForwardedAsAreaFractions` in W-aux.g),
and (green but weakened) `StageHills2DTests.N5d_BlendPositive_DiffersFromBlend0`. This
failure mode appeared five times in one batch and cost more time than the fix itself.

Deliberately left alone: `StageBaseTerrain2DTests.NoShapeTunables()` — the NoShape path does
not consume the changed formula, and compensating it would break green goldens for no
reason.

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
