# Phase W.b — Pending documentation updates

Status: **APPLIED — 2026-08-21. Closed; ready to archive.** All 15 edits across the five
target documents were applied and verified in-session (each anchor located exactly once
before substitution; each REPLACE / INSERTED TEXT confirmed present exactly once after).
No item is blocked and no item remains unapplied.

Applicability precondition, confirmed 2026-08-21: the W.b **code** is applied and the
EditMode suite is green — user confirmation. The code surface was additionally verified by
direct file read this session (`MapGenerationPreset.cs`, `MapGenerationPresetJsonImporter.cs`,
`MapGenerationPresetTests.cs`, `MapGenerationPresetJsonRoundTripTests.cs`,
`MapGenerationPresetWizard.cs`, `PCGMapTilemapVisualization.cs`); the *green* status is user
confirmation only, not re-run here.

**One deviation from the queue text, applied by user decision 2026-08-21.** In §4.3, the
third observation's source references were corrected against the files before insertion:
`PCGMapVisualization` L445–454 → **L452–461**, `PCGMapCompositeVisualization` L405–414 →
**L407–416**, and "the twelve `biome*` climate fields" → "the **ten**", which is the
number actually assigned in both components. The substantive claim — neither component
resolves biome climate from the preset — was verified and is unchanged. See
`Application_Ledger_2026-08-21.md` §3.

Three items (§2.3, §4.3, §5) carried a prose-described insertion point rather than a
literal anchor. Each was resolved to a unique literal anchor before applying; the anchors
used are recorded in `Application_Ledger_2026-08-21.md` §2.

Each item below is a literal search/replace pair or an insert anchor, in the same idiom as
`Phase_W_Pending_Doc_Updates.md`. Retained for the record; **do not re-apply.**

## Evidence backing these updates (session 2026-08-20)

- 30 search/replace pairs applied across 9 files (see §File manifest below).
- Clean compile + EditMode Run All green — user confirmation, 2026-08-20.
- Console goldens seed 243 res 256 identical before/after; no unit test edited —
  user confirmation, 2026-08-20.
- Preset JSON export shows the five new keys and the narrowed `stageTogglesNote`;
  run header shows `hydro=True` propagated from the asset — console log, same session.

---

## 1. `PCG_Roadmap.md`

### 1.1 — Correct the `MapTunables2D` claim (option (a), user decision 2026-08-20)

Roadmap L1276–1280 asserts the three `hydro*` values enter the `MapTunables2D`
constructor. Session evidence contradicts this: `Stage_Hydrology2D` reads `epsilon`,
`riverThresholdFraction` and `minLakeArea` as instance fields, never from
`inputs.Tunables`; the existing wiring (`PCGMapTilemapVisualization` L545–547) already
assigns them that way. Planning superseded by evidence.

```
SEARCH:
Two destinations, with different consequences. Stage toggles sit beside the six already in
the preset and do not enter `MapTunables2D`, so they cannot move a golden. The three
`hydro*` values are algorithm tunables and **do** enter the `MapTunables2D` constructor;
identity defaults are the strategy that let W-aux.b close without breaking a single golden,
and the same discipline applies here.

REPLACE:
Two destinations, with different consequences. Stage toggles sit beside the six already in
the preset and do not enter `MapTunables2D`, so they cannot move a golden.

**Corrected at W.b execution (2026-08-20).** This entry previously stated that the three
`hydro*` values enter the `MapTunables2D` constructor. They do not, and were not made to:
`Stage_Hydrology2D` reads `epsilon` / `riverThresholdFraction` / `minLakeArea` as instance
fields, never from `inputs.Tunables`, and the pre-existing wiring already assigned them
that way. Routing them through `MapTunables2D` would have added three constructor
parameters no consumer reads, on a struct every golden traverses. The planning claim is
superseded by implementation evidence; identity defaults remained the strategy and no
golden moved.
```

### 1.2 — Close W.b

```
SEARCH:
**W.b — parameter surface consolidation (scope defined 2026-08-20, not started).**

REPLACE:
**W.b — parameter surface consolidation (complete 2026-08-20).**
```

Insert after the W.b scope block (after the `HelpBox` paragraph ending
"the tool would be lying about its own limitations."):

```
INSERT AFTER:
never having written it: the tool would be lying about its own limitations.

INSERTED TEXT:

**Outcome (2026-08-20).** Seven fields adjudicated, five promoted to
`MapGenerationPreset`: `enableRegionsStage`, `enableHydrologyStage`,
`hydroRiverThresholdFraction`, `hydroMinLakeArea`, and
`Stage_Vegetation2D.moistureModulation` (as `vegetationMoistureModulation`). Two
retained with reason: `hydroEpsilon` stays component-scoped (Priority-Flood numeric
plumbing, not an authoring knob) and `Stage_Regions2D.SpeckThreshold` stays stage-local
(constant since inception, underpins invariant R-8, no authoring demand). The wizard
`HelpBox` was narrowed rather than retired, since one field still is not carried.
Additive JSON schema extension, not a serialization break. Zero goldens moved; no unit
test edited. See `changelog-ssot.md` §W.b.
```

### 1.3 — Header status line

```
SEARCH:
- Phase W: active (W.a complete 2026-08-09; W-aux.a…W-aux.g closed 2026-08-17…20;
  next: W.b — scope defined 2026-08-20)

REPLACE:
- Phase W: active (W.a complete 2026-08-09; W-aux.a…W-aux.g closed 2026-08-17…20;
  W.b closed 2026-08-20; next: W-aux.h — hydrology probe)
```

```
SEARCH:
**Active. W.a complete 2026-08-09; W-aux.a through W-aux.g closed. Next: W.b.**

REPLACE:
**Active. W.a complete 2026-08-09; W-aux.a through W-aux.g and W.b closed.
Next: W-aux.h (hydrology probe), then W.c.**
```

---

## 2. `map-pipeline-by-layers-ssot.md`

### 2.1 — §Configuration Assets (N5.b): record the promoted surface

Insert before the `TilesetConfig` block in §Configuration Assets (N5.b):

```
INSERT BEFORE:
`TilesetConfig` — multi-layer stamping maintenance rule:

INSERTED TEXT:
`MapGenerationPreset` — W.b parameter surface (2026-08-20).
- Stage toggles carried: hills, shore, vegetation, traversal, morphology, biome,
  **regions**, **hydrology** (the last two promoted at W.b).
- Hydrology tunables carried: `hydroRiverThresholdFraction`, `hydroMinLakeArea`.
  **`hydroEpsilon` is deliberately not carried** — Priority-Flood numeric plumbing,
  not an authoring parameter (M2.a verdict). It stays a component field on
  `PCGMapTilemapVisualization`; `PCGMapVisualization` hardcodes `1e-5f`.
- Vegetation: `vegetationMoistureModulation` → `Stage_Vegetation2D.moistureModulation`.
- Resolution follows the override-at-resolve pattern: components read
  `preset != null ? preset.X : inlineX` and assign to stage instances. Promoted fields
  are NOT routed through `MapTunables2D` — the consuming stages read instance fields,
  not `inputs.Tunables`.
- Dirty-tracking consequence: each promoted field carries a `last*` entry compared by
  effective value. Editing a field on an assigned preset does not change the preset
  reference, so without the per-field comparison the map would not regenerate.
- `Stage_Regions2D.SpeckThreshold` remains stage-local by M2.a verdict (constant since
  inception; underpins invariant R-8).

```

### 2.2 — §M2a-9: amend for the now-reachable modulation

The exactness and floor clauses were written when `moistureModulation` was unreachable.
It is now preset-authorable, so the clauses must name the condition they hold under.

```
SEARCH:
  - (a) **Exactness.** `c ∈ E` is vegetated ⟺ `bucket(c) >= cut(density(c))`.

REPLACE:
  - (a) **Exactness.** `c ∈ E` is vegetated ⟺ `bucket(c) >= cut(density(c))`.
    Holds for `moistureModulation = 0` — the default, and the configuration every
    vegetation golden and gate is captured under.
```

```
SEARCH:
  - (e) **COUPLING — contract surface, not an implementation detail.**

REPLACE:
  - (d-bis) **Moisture modulation (W.b — reachable since 2026-08-20).**
    `Stage_Vegetation2D.moistureModulation` became preset-authorable at W.b
    (`MapGenerationPreset.vegetationMoistureModulation`, default 0 = disabled). A
    non-zero value shifts the cut **per cell** rather than applying one cut to the
    population: `bucketShift = round(moistureModulation * (moisture - 0.5) * QuantSteps)`,
    `effectiveCut = clamp(cut - bucketShift, 0, QuantSteps)`. Wetter cells get a lower
    cut, drier cells a higher one. This **suspends (a) and (c) by construction** — the
    cut is no longer uniform over `E`, so neither exactness against a single `cut(d)`
    nor the coverage floor survives. It remains fully deterministic: modulation reads
    the `Moisture` field, which requires `Stage_Biome2D` to run before
    `Stage_Vegetation2D` (M2.a order); when `Moisture` is absent the branch is inert
    regardless of the value. Nesting (b) is unaffected — it is a property of `cut(d)`,
    which modulation shifts uniformly per cell rather than reordering.
    No golden or gate is captured with a non-zero value; doing so requires re-anchoring
    as its own step.
  - (e) **COUPLING — contract surface, not an implementation detail.**
```

### 2.3 — §Phase L — Hydrology: record the authoring coupling

Insert at the end of the `Stage_Hydrology2D` block in §Phase L — Hydrology:

```
INSERTED TEXT:
**Authoring coupling (W.b).** `riverThresholdFraction` is preset-authorable. The river
mask is `FlowAccumulation >= totalLandCells × riverThresholdFraction`, so the fraction
sets river *count*, not river *shape*. `MapGenerationPreset.biomeRiverFlowNorm = 0`
means "auto", and auto is defined as `totalLandCells × 0.02` — the **default** fraction,
not the configured one. Changing `riverThresholdFraction` while leaving
`biomeRiverFlowNorm` at 0 therefore normalizes river moisture against the wrong divisor:
lowering the fraction saturates `riverMoistureBonus` on cells that are not rivers.
Authors changing one must set the other explicitly. This asymmetry is recorded, not
fixed — resolving it means redefining "auto", which is a contract change.
```

---

## 3. `SSoT_CONTRACTS.md`

### 3.1 — §N5.b: additive schema extension is not a serialization break

```
SEARCH:
- Serialization format changes on governed configuration types must be documented in the
  changelog with explicit "serialization break" notice.

REPLACE:
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
```

### 3.2 — §M2.a: record the W.b verdicts

```
SEARCH:
- Stage-local tunables (constants or fields owned by a single stage) are permitted and must
  not be promoted to `MapTunables2D` or `MapGenerationPreset` unless they become cross-stage
  or user-authored. Defaults must preserve prior behavior when a stage is reordered or
  gains new optional inputs.

REPLACE:
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
```

---

## 4. `CURRENT_STATE.md` — §Open observations

### 4.1 — Close: `moistureModulation`

```
SEARCH:
- **`Stage_Vegetation2D.moistureModulation` may be unreachable.** Public field, default
  `0.0f`, gates the moisture modulation. Whether anything in the pipeline construction path
  assigns it was never checked. If nothing does, it is the same pathology as the five
  component-scoped fields and `Stage_Regions2D.SpeckThreshold`: a tunable that exists in code
  but cannot be reached from a preset. **Belongs to the W.b field inventory.**

REPLACE:
- ~~**`Stage_Vegetation2D.moistureModulation` may be unreachable.**~~ **RESOLVED at W.b
  (2026-08-20).** Confirmed unreachable — zero assignments anywhere in the corpus outside
  its own declaration, so with the `> 0f` guard the branch had been inert since it was
  written. Promoted to `MapGenerationPreset.vegetationMoistureModulation`, default 0, which
  keeps it inert until authored. M2a-9 amended to name the suspension of clauses (a) and (c)
  under non-zero modulation.
```

### 4.2 — Close: `waterThreshold01`

```
SEARCH:
- **`Default_MapPreset.waterThreshold01` may carry the wrong calibration value.** The asset
  reads **0.425532**,

REPLACE:
- ~~**`Default_MapPreset.waterThreshold01` may carry the wrong calibration value.**~~
  **RESOLVED 2026-08-20** — the asset was corrected to **0.412119** by user decision, the
  value the arithmetic below predicted. Confirmed in the exported preset JSON
  (`waterAndShore.waterThreshold01: 0.412119`) and in the `hprobe` header
  (`wt=0.4121`, `underThreshold: 0`). Original finding retained for the record:
  the asset read **0.425532**,
```

### 4.3 — Add two observations

Append to §Open observations:

```
INSERTED TEXT:
- **River threshold is a fraction of *total* land, but this island is not one basin.**
  `Rivers = FlowAccumulation >= totalLandCells × riverThresholdFraction`. At seed 243 res
  256 that is `19991 × 0.02 ≈ 400`, while `FlowAccumulation.max = 1259` — the largest
  basin on the map drains ~6% of the land, so 34 cells clear the threshold and the map
  shows one river's final reach. Inferred cause, not verified: `warpAmplitude01 = 0.46`
  on an ellipse produces lobes that each drain to their own coast, so total land is the
  wrong divisor for a per-basin quantity. The correct divisor would be the largest
  basin, which is a contract change. **Belongs to the W-aux.h probe.** Measured at one
  seed only; three-seed measurement pending.
- **`Lakes = 0` at seed 243 with `minLakeArea = 0`** — nothing was filtered, so the zero
  is real, not a filtering artifact. Whether the shaping chain
  (`seaFloorLevel01 = 0.35`, `seaFloorAmplitude01 = 0.3`) can produce enclosed sub-threshold
  basins at all under `Default_MapPreset` has never been measured. Belongs to W-aux.h.
- **`PCGMapVisualization` and `PCGMapCompositeVisualization` ignore the preset for biome
  climate.** Both assign the twelve `biome*` climate fields and `enableBiomeStage` from
  inline component fields with no `preset != null ?` ternary
  (`PCGMapVisualization` L445–454, `PCGMapCompositeVisualization` L405–414), while
  `PCGMapTilemapVisualization` resolves all of them from the preset. The same preset
  therefore produces different climate on different components. Surfaced during the W.b
  inventory; deliberately not fixed there (out of batch scope). Verified by reading the
  three components, 2026-08-20.
```

---

## 5. `changelog-ssot.md` — new entry

Insert above `## W-aux.g`:

```
INSERTED TEXT:
## W.b — Parameter surface consolidation (component/stage fields → preset)
Date: 2026-08-20

**Cause.** Five tunables lived only on the visualization components and two more only on
stage instances, so a `MapGenerationPreset` was not a complete description of a map.
`Stage_Vegetation2D.moistureModulation` was worse than component-scoped: verified this
session to have zero assignments anywhere in the corpus, meaning an implemented feature
had been unreachable since it was written. Phase W's later steps assume a preset fully
describes a map, so the gap was closed before W.c.

**Verdicts.** Adjudicated per field, not by uniformity — five promoted, two retained.
See `SSoT_CONTRACTS.md` §M2.a for the table and reasons. Retained: `hydroEpsilon`
(numeric plumbing of the depression solver) and `Stage_Regions2D.SpeckThreshold`
(constant since inception, underpins R-8).

**Destination.** Preset with component→stage wiring, **not** `MapTunables2D`. The
consuming stages read instance fields, never `inputs.Tunables`; routing through the
tunables struct would have added three constructor parameters no consumer reads, on a
struct every golden traverses. This corrects a `PCG_Roadmap.md` claim to the contrary —
planning superseded by implementation evidence.

**Serialization.** Additive extension, **not a serialization break**: five new keys
(`stageToggles.regions`, `stageToggles.hydrology`, `hydrology.riverThresholdFraction`,
`hydrology.minLakeArea`, `vegetation.moistureModulation`). Absent keys import as
`AbsentPreserved`, so pre-W.b JSON stays importable and existing assets stay valid.
`stageTogglesNote` narrowed from five fields to one. `MapGenerationPresetWizard`'s
`HelpBox` narrowed to `hydroEpsilon` rather than retired, since one field genuinely is
still not carried.

**Behavior.** No golden moved and no unit test was edited. Every promoted default equals
the value the pipeline effectively used before promotion, asserted against fresh stage
instances by `Defaults_WbPromotedFields_MatchPrePromotionEffectiveValues`. Console
goldens at seed 243 res 256 identical before and after — user confirmation, 2026-08-20.

**Scene-level caveat (not covered by any test).** The preset now wins over the
component's inline `enableRegionsStage` / `enableHydrologyStage`. A saved scene whose
component values differed from its assigned preset's changes behavior. No golden can
detect this; it is an authoring-surface consequence, recorded deliberately.

**Contract amendment.** `map-pipeline-by-layers-ssot.md` §M2a-9 gains clause (d-bis):
non-zero `moistureModulation` suspends (a) exactness and (c) floor by construction, since
the cut stops being uniform over the eligible population. Nesting (b) is unaffected. No
golden is captured with a non-zero value.

**Files.** 30 search/replace pairs across 9 files — see §File manifest in
`W_b_Pending_Doc_Updates.md`.

```

---

## File manifest — session 2026-08-20

**Created:** none. (This document and the W-aux.h rehydration prompt are session
deliverables, not package files.)

**Modified (9 files, 30 search/replace pairs):**

| File | Pairs | What changed |
|---|---|---|
| `MapGenerationPreset.cs` | 6 | header doc; 2 toggles; hydrology + vegetation field blocks; `ToJson()` toggle block + note; `ToJson()` hydrology + vegetation blocks |
| `MapGenerationPresetJsonImporter.cs` | 2 | 2 toggle keys; 3 value keys |
| `PCGMapTilemapVisualization.cs` | 5 | effective toggles; hydro + vegetation wiring; `last*` decl; `CacheParams`; `ParamsChanged` |
| `PCGMapVisualization.cs` | 5 | wiring + `eRegions`; stage selection; `last*` decls; `CacheParams`; `ParamsChanged` |
| `PCGMapCompositeVisualization.cs` | 5 | effective toggle; stage selection; vegetation wiring; `last*` decl + `CacheParams`; `ParamsChanged` |
| `MapGenerationPresetWizard.cs` | 1 | `HelpBox` narrowed from five fields to `hydroEpsilon` |
| `Stage_Vegetation2D.cs` | 1 | doc only — field is now assigned; points at the M2a-9 amendment |
| `MapGenerationPresetTests.cs` | 3 | `using` for Stages; header doc; defaults-match gate |
| `MapGenerationPresetJsonRoundTripTests.cs` | 2 | non-default fixture gains the five promoted fields |

**Documents pending (this file):** `PCG_Roadmap.md`, `map-pipeline-by-layers-ssot.md`,
`SSoT_CONTRACTS.md`, `CURRENT_STATE.md`, `changelog-ssot.md`.
