# W-aux.c — Pending Documentation Updates

Status: **APPLIED — 2026-08-20.** 15 of 17 items applied; 1 dropped by user decision,
1 resolved elsewhere.
Dropped: **§3** (`reference/default-preset-calibration.md`) — user decision 2026-08-20; the
preset is still being tuned and is deliberately not documented yet. The reference was removed
from `coverage-matrix.md` rather than left dangling.
Superseded, not applied as written: **§2.4** (height composition ceiling) by W-aux.f §1/§2;
**§2.5** (vegetation density semantics attributed to noise grain) by W-aux.d, which measured
the cause to be value mapping; **§8.1** (`DeepWater`) resolved by X1.a §4; **§8.4**
(`hillsL2_fraction`) already applied in W-aux.c block 3.
Targets written: `changelog-ssot.md`, `CURRENT_STATE.md`, `coverage-matrix.md`,
`SSoT_INDEX.md`, `PCG_Roadmap.md`.
Applied by: Islands.PCG documentation-application session, 2026-08-20. See
`Application_Ledger_2026-08-20.md` for the item-by-item disposition.
This file is consumed input and is **not authority**.
Session (origin): Islands.PCG — W-aux.c block 1 (instrumentation + `Default` recalibration), 2026-08-19.
Scope: register the block-1 instrumentation surface, the recalibrated `Default_MapPreset`,
the measured outcome of the calibration run, and the one lever that provably cannot close
its DoD criterion inside this batch.

Blocks 2 (preset wizard) and 3 (per-biome vegetation) are **not** covered here — they
have not been implemented. Their doc updates are produced at their own closure.

Items are independent unless noted. Applying them is user-controlled.

---

## 0. Evidence base

Everything below is measured in-session on 2026-08-19:

- `Log Map Stats (JSON to Console)` on `PCGMapTilemapVisualization`, seed 56, res 256,
  hydrology ON, preset `Default_MapPreset`. Full JSON pasted into the session transcript.
- `Log Preset (JSON)` on `Default_MapPreset` (full preset dump, same session).
- Golden hash line emitted by the same run.
- User-confirmed: clean compile, EditMode Run All green, after the block-1 diffs.

Code read in-session with line anchors: `MapStatsExporter2D.cs`, `MapDataExport.cs`,
`MapGenerationPreset.cs`, `Stage_Vegetation2D.cs`, `BiomeTable.cs`,
`PCGMapTilemapVisualization.cs` L703–716, `Islands.PCG.Editor.asmdef`,
`MapGenerationPresetTests.cs`.

---

## 1. `Documentation~/changelog-ssot.md`

New entry. The file is newest-first: insert **immediately after the EXT-3 coverage-gap
blockquote and before the `## W-aux.b` heading**.

```markdown
## W-aux.c (block 1) — Calibration instrumentation + `Default` recalibration
Date: 2026-08-19

**Instrumentation (additive, diagnostics-only).** `MapStatsExporter2D` gained three
sections and a second `ToJson` overload taking the run's effective `waterThreshold01`.
The single-argument overload is preserved and reports the new sections as
`"present": false`.

- `landHeightHistogram` — 10 equal buckets over `[waterThreshold01, 1.0]` restricted to
  `Land`, plus `underThreshold` and an `exactlyOne` counter (exact `h == 1f`).
- `biomeByBand` — 13 biomes x {plain, hillsL1, hillsL2}, plus band totals. Bands are
  disjoint (`L2 ? 2 : L1 ? 1 : 0`), verified against the run: plain 7343 + L1 7755 +
  L2 7097 = Land 22195.
- `vegetationEligibility` — the funnel `LandInterior -> minus HillsL2 -> minus
  zero-density biomes -> vegetated`, mirroring the vegetation stage's policy as
  implemented at this date.

Rationale: `waterThreshold01` is passed in rather than added to `MapDataExport`. The
export contract carries data, not tunables; widening it to serve a diagnostic would
change a surface consumed by adapters and tests. Call site
(`PCGMapTilemapVisualization.LogMapStats`) resolves `preset != null ? preset.waterThreshold01
: waterThreshold01`, the same resolution it already performs for dirty tracking.

**Golden-neutral by construction.** The exporter is button-driven and outside the rebuild
path; `MapPipelineRunner2DGoldenM2Tests` builds from `MapTunables2D.Default` and never
reads a preset. Confirmed by clean compile + EditMode Run All green after the change.

**`Default_MapPreset` recalibrated.** Values changed to break the height plateau
diagnosed on 2026-08-19: `islandSmoothFrom01` 0.40 -> 0, `islandSmoothTo01` 0.70 -> 1.0,
`islandRadius01` 0.34 -> 0.42, `islandAspectRatio` 1.0 -> 1.45, `warpAmplitude01` 0 -> 0.46,
`heightRedistributionExponent` 0.9 -> 1.3, `hillsL1_fraction` 0.34 -> 0.42,
`hillsL2_fraction` 0.38 -> 0.62, `hillsNoiseBlend` 0 -> 0.35, terrain noise
Perlin f8/o4 -> Simplex f4/o5 (amplitude 0.22 unchanged), `baseTemperature` 0.70 -> 0.82,
`lapseRate` 0.50 -> 0.62, `coastalMoistureBonus` 0.30 -> 0.40, `coastDecayRate` 0.55 -> 0.40,
`moistureNoiseAmplitude` 0.30 -> 0.38.

**Measured outcome (not a golden).** seed 56, res 256, hydrology ON:

| | before (2026-08-19 pre-fix) | after |
|---|---|---|
| `Land` | 19714 (30.08%) | 22195 (33.87%) |
| `HillsL2` % of Land | 63.2% | **32.0%** |
| Land cells at exactly 1.0 | not instrumented | **2601 (11.72% of Land)** |
| `Vegetation` | 156 (0.24%) | **1887 (2.88%)** |
| biomes non-zero | 9 | 10 (`Grassland` appears, 12 cells) |
| `Height` mean | 0.382685 | 0.398682 |

**Finding recorded: the plateau changed cause, and one DoD criterion is unreachable in
this batch.** `islandSmoothFrom01 = 0` removed the geometric plateau — histogram buckets
0-8 are flat at 7.4%-9.6%, as a smooth dome predicts. The residual 11.72% at exactly 1.0
is *clamp saturation*: `h01 = mask01 * (1 + (n - 0.5) * terrainAmp)` with
`terrainAmp = 0.22` reaches a 1.11x multiplier, so any cell with `mask01 >= ~0.90`
can exceed 1.0 and be clipped. Predicted ~11-12%, measured 11.72%.

Consequence: **no monotone height remap can break it.** `heightRedistributionExponent`,
`heightRemapCurve` and `heightQuantSteps` all apply after the clamp and all satisfy
`f(1) = 1`; a set of cells that already collapsed to one value cannot be re-separated by
redistributing below it. Lowering `terrainAmp` reduces saturation only by reducing relief.
The only lever that removes the ceiling instead of redistributing under it is the deferred
`heightScale` change (`h01 = mask01 * heightScale * (1 + noise)`), which is a core change
in two stages and breaks all base-terrain goldens.

Recorded per DoD: criterion "< 5% of land at exactly 1.0" **not met, 11.72% measured**;
failing lever identified as height composition, not preset tuning.

**Reference goldens for the recalibrated `Default`** (res 256, seed 56 — a calibration
reference, not a gate; no test consumes these):
`Land=E2AA0E2C0DF77A50 LandCore=F532044FA5F8CA17 Rivers=0207A6D3D8571A71
Lakes=4AE4FA4CE128DE82 Height=196AF8D87C1D19EC CoastDist=D42E6A25948E95BA
Temperature=2A03BEBEFF59EE4A Moisture=40B88763F80C4851 Biome=9C63E816DF9FD0CA
FlowAccum=782B1DC3502E64A0`

Note: this does **not** close the Phase W §3 blocker in `Phase_W_Pending_Doc_Updates.md`.
That reserved slot is for the W.a golden at res **64** with the pre-W-aux.b preset; these
hashes are res 256 with a different preset and are not substitutable.

- No new subsystem SSoTs created. No authority decisions changed.
- No governed contract changed: `MapStatsExporter2D` is an inspection surface, not a
  contract-bearing stage.
```

---

## 2. `CURRENT_STATE.md`

### 2.1 — Status date and slice line

`CURRENT_STATE.md` L3.

```diff
-Status date: 2026-08-18 (Phase W in progress — W.a, W-aux.a and W-aux.b closed)
+Status date: 2026-08-19 (Phase W in progress — W.a, W-aux.a, W-aux.b closed;
+W-aux.c block 1 closed, blocks 2-3 open)
```

L13, implemented-slice enumeration: append `+ W-aux.c (block 1: instrumentation)` to the
existing chain.

### 2.2 — Inspection surface (anchor: the `W-aux.a — Islands.PCG.Inspection.MapStatsExporter2D` block, ~L66)

Append to that block:

```markdown
- **W-aux.c block 1 — three calibration sections added. DONE 2026-08-19.**
  `landHeightHistogram`, `biomeByBand`, `vegetationEligibility`. Second `ToJson` overload
  takes the run's effective `waterThreshold01` (not carried by `MapDataExport` — the
  export contract holds data, not tunables). Single-argument overload preserved; the new
  sections report `"present": false` when the threshold is unknown. Still button-driven,
  still O(cells), still outside the rebuild path. Not test-covered — see
  `coverage-matrix.md`.
```

### 2.3 — Drift fix: `islandRadius01 = 0.33` (anchor: the Cold-ceiling analysis, ~L367)

Standing drift, carried unverified into this session and now resolvable. No preset in the
tree has 0.33; `Default_MapPreset` now has **0.42**.

```diff
-1. **`latitudeEffect` cannot cool a centred island.** With `islandRadius01 = 0.33` the
+1. **`latitudeEffect` cannot cool a centred island.** With a centred island of
+   `islandRadius01 ~ 0.33-0.42` the
```

and append to that section:

```markdown
**Re-measured 2026-08-19 (`Default_MapPreset` @256, seed 56).** The Hot-row ceiling on
land is confirmed, with new numbers. Land temperature ceiling before coastal moderation
and noise is `baseTemperature - lapseRate * waterThreshold01 = 0.82 - 0.62 * 0.472 = 0.527`;
with the maximum coastal term (+0.1) and noise (+0.05) it reaches ~0.68, still below the
Hot threshold 0.75. The measured `Temperature.max = 0.873757` belongs to **water**
(runtime tooltip: DeepWater cell, `h = 0.190`, `T = 0.800`), not to land. `SubtropicalDesert`,
`TropicalSeasonalForest` and `TropicalRainforest` therefore sit at 0 cells by arithmetic,
not by defect. Standing decision unchanged: 10 non-zero biomes with real submarine relief
is preferred over 13 with a flat ocean.
```

### 2.4 — New subsection: height composition ceiling

Add near the existing "Duplicated base terrain implementation" structural-fact block (~L52):

```markdown
- **Height composition has a hard ceiling (structural fact, measured 2026-08-19).**
  `Stage_BaseTerrain2D` composes `h01 = mask01 + (n - 0.5) * terrainAmp * mask01`, i.e.
  `mask01 * (1 + (n - 0.5) * terrainAmp)`. The noise is multiplicative and centred on 1.0,
  so it cannot lift terrain above the mask — it only modulates it, and cells with
  `mask01 >= ~1/(1 + terrainAmp/2)` saturate and clamp to 1.0. Measured with
  `terrainAmp = 0.22`: **11.72% of land at exactly 1.0** even with the geometric plateau
  fully removed (`islandSmoothFrom01 = 0`).
  Consequence: any post-composition remap (`heightRedistributionExponent`,
  `heightRemapCurve`, `heightQuantSteps`) is powerless against it — all are monotone with
  `f(1) = 1`, and a collapsed pre-image cannot be re-separated. Raising `terrainAmp` for
  more interior relief *increases* saturation. The clean fix is a `heightScale` factor
  (`h01 = mask01 * heightScale * (1 + noise)`, `heightScale ~ 0.85-0.90`), which is a core
  change in **two** stages (`Stage_BaseTerrain2D` and its mirror
  `BaseTerrainStage_Configurable`) and breaks all base-terrain goldens. Not done; see
  `PCG_Roadmap.md`.
```

### 2.5 — Vegetation noise granularity (new known-limitation entry)

```markdown
- **`vegetationDensity` is not a per-cell probability at current noise settings
  (measured 2026-08-19).** `Stage_Vegetation2D` samples at `NoiseFrequency = 4` with 3
  octaves; on a 256x256 map that is ~64-cell lobes. Against a field that is near-constant
  across a compact biome, `threshold = 1 - density` behaves as all-or-nothing rather than
  as a rate. Measured: `TemperateForest` accepted **95.54%** at density 0.65, `Shrubland`
  **0.26%** at density 0.25. Visually the mask reads as continuous coastal ribbons, not
  scatter. This is a calibration constraint, not a contract violation — coverage
  monotonicity (M2a-9) is a statistical contract and is not disproved by per-biome
  deviation. Raising the frequency would make density behave as intended but changes the
  spatial character and breaks vegetation goldens; not in scope for W-aux.c.
```

---

## 3. `reference/default-preset-calibration.md`

**File not available in project knowledge this session — anchors unverified.** The update
is required but the exact diff cannot be produced without the file. Proposed content:

- Replace the `Default` value table with the 2026-08-19 recalibrated values (full
  `ToJson()` dump is in the W-aux.c rehydration prompt and in the changelog entry above).
- Add the measured before/after table from §1.
- Add the three DoD outcomes with their measured values, including the two that failed.
- Record `hillsL2_fraction 0.62 -> ~0.65` as the identified micro-adjustment to bring
  `HillsL2` from 32.0% into the 15-30% window, **untested**.

---

## 4. `coverage-matrix.md`

Declare the gap rather than leaving it implicit:

```markdown
| `MapStatsExporter2D` (W-aux.a + W-aux.c block 1) | **No test coverage.** Diagnostics
surface, outside the rebuild path, no golden depends on it. Accepted gap: a wrong number
here misleads calibration but cannot change generation output. Revisit if any gate ever
consumes exporter output. |
```

---

## 5. `SSoT_INDEX.md` — standing drift, still open

L17. Detected 2026-08-18, **not** corrected; re-confirmed present 2026-08-19.

```diff
-- `systems/map-pipeline-by-layers-ssot.md` (implemented slice currently F0–F3)
+- `systems/map-pipeline-by-layers-ssot.md` (implemented slice currently F0–N6 + M + M2 + L + V + Q + W; see `CURRENT_STATE.md` for the exact chain)
```

Rationale for the phrasing: pointing at `CURRENT_STATE.md` instead of inlining the chain
stops the index from drifting again at every batch. The index owns authority order, not
implementation state.

---

## 6. `PCG_Roadmap.md`

Roadmaps are planning, not implementation authority. Two entries:

- W-aux.c: block 1 done 2026-08-19; blocks 2 (preset wizard) and 3 (per-biome vegetation)
  open. W.b still sequenced after W-aux.c.
- New proposed batch **W-aux.d — `heightScale`**: promote the deferred block-4 lever to a
  batch of its own, now that it has measured justification (11.72% clamp saturation, and
  the proof that no remap can address it). Scope: `Stage_BaseTerrain2D` + its mirror
  `BaseTerrainStage_Configurable` + preset field + re-anchoring all base-terrain goldens.
  Not scheduled; sequencing is a user decision.

---

## 8. Observations recorded without an owner

These surfaced in the 2026-08-19 session and had **no home outside the W-aux.c rehydration
prompt**, which is a session artifact and dies at batch closure. Recorded here so they
survive. None is fixed; each states where it should be verified and why it was not
pursued now.

Proposed target: a `## Open observations` section in `CURRENT_STATE.md`, or
`open_patterns.md` if the project prefers to keep unowned items out of the current-state
document. **This is an authority question, not a content question — surface it to the
user rather than picking.**

### 8.1 — `DeepWater` may be a superset of the water bands (UNVERIFIED)

Measured 2026-08-19: total water = 65536 − 22195 = **43341**. Reported:
`DeepWater` 42994, `ShallowWater` 3177, `MidWater` 6974 — sum 53145, far above the total.
`DeepWater` + 347 = total water exactly.

Reading: `DeepWater` appears to mark *all* water minus a small remainder, with
`ShallowWater` and `MidWater` layered on top as refinements, rather than being the
isolated deep band. If confirmed, any "% of deep ocean" read from that layer is wrong,
including in `MapStatsExporter2D` output and in any calibration reasoning built on it.

Not verified against `Stage_Shore2D`. Not pursued: outside W-aux.c scope, and it affects
interpretation of a diagnostic, not generation output. **Verify before using `DeepWater`
counts for any calibration decision.**

### 8.2 — `Stage_Vegetation2D.moistureModulation` may be unreachable (UNVERIFIED)

Public field, default `0.0f`, gates the moisture modulation at L136-141. Whether anything
in the pipeline construction path assigns it was **not checked**. If nothing does, it is
the same pathology as the five component-scoped fields and `Stage_Regions2D.SpeckThreshold`:
a tunable that exists in code but cannot be reached from a preset.

**Belongs in the W.b field inventory**, not as a loose item — W.b is the batch that moves
component-scoped tunables into the preset, and this is one more of them. Adding it there
costs nothing; discovering it after W.b closes costs a second pass.

### 8.3 — Vegetation noise frequency as a fix candidate (NOT SCOPED)

`Stage_Vegetation2D` uses `NoiseFrequency = 4` / 3 octaves (constants, L48-49). This is
the mechanical cause of the density-semantics failure recorded in §2.5. Raising it would
make `vegetationDensity` behave as the per-cell rate it claims to be.

Cost: changes the spatial character of vegetation from continuous lobes to scatter, and
breaks all vegetation goldens. Not a W-aux.c change; block 3 already re-anchors those
goldens for a different reason, so **doing both at once would confound the measurement**.
Recorded as a candidate for a later batch, after block 3 has been measured on its own.

### 8.4 — `hillsL2_fraction` micro-adjustment (UNVERIFIED, at risk of being lost)

`hillsL2_fraction` 0.62 → **~0.65** brings `HillsL2` from the measured 32.0% into the
15-30% DoD window. Estimated from the histogram cumulative, **not run**.

Recorded here in addition to §3 because §3 targets `default-preset-calibration.md`, which
was **not available in project knowledge** this session and therefore has no verified
anchor. Duplication is deliberate: a calibration finding whose only home is an unanchored
document is a finding that will be lost.

### 8.5 — `Showcase (MapGenerationPreset)` absent from the package tree (UNVERIFIED)

Carried forward from earlier sessions, re-confirmed as still unresolved. The W-aux.b
calibration reference in `changelog-ssot.md` is stated against `Showcase @256`, so if that
asset no longer exists the reference has no reproducible subject. Low priority; verify
when convenient.

---

## 9. Proposed track — parameter legibility

**Not a batch. A named thread**, so the finding does not decay into folklore. Proposed
home: `PCG_Roadmap.md` as a standing concern, or `open_patterns.md`.

The 2026-08-19 session produced four independent, measured demonstrations that pipeline
parameters do not have legible effects. This matters because preset calibration is done by
turning knobs and looking, and a knob that lies costs a full measure-and-diagnose cycle
each time.

| Parameter | What it promises | What it does (measured 2026-08-19) |
|---|---|---|
| `terrainAmp` (0.22) | more interior relief | non-monotone: past ~`1/(1+amp/2)` of mask, more amplitude means more clamp saturation, i.e. a *flatter* top. 11.72% of land pinned at 1.0. |
| `heightRedistributionExponent`, `heightRemapCurve` | reshape the height distribution | inert over that same 11.72% — all monotone with `f(1)=1`, and a collapsed pre-image cannot be re-separated. |
| `vegetationDensity` | per-cell vegetation rate | threshold against a ~64-cell-lobe field: `TemperateForest` 95.54% at 0.65, `Shrubland` 0.26% at 0.25. |
| `biomeBaseTemperature` | land temperature | land ceiling is `base − lapse·waterThreshold01`, three coupled parameters; none of the three names the coupling. |

Two structural causes, both already documented above: multiplicative-then-clamped height
composition (§2.4), and derived-from-derived thresholds (the N5.e hills remap, which is
why the `derived` JSON block exists at all).

**Cheapest concrete step, if the user wants one.** The `derived` block of
`MapGenerationPreset.ToJson()` (L540-544) already exists for exactly this purpose: it
surfaces values the pipeline consumes but the Inspector never shows. It currently carries
two. Candidates to add, each computable from fields already present, each additive and
golden-neutral:

- `heightSaturationMaskThreshold` = `1 / (1 + terrainAmp/2)` — the `mask01` above which
  cells can clamp to 1.0. Turns §2.4 from an in-session derivation into a readable number.
- `landTemperatureCeiling` = `baseTemperature − lapseRate·waterThreshold01` (noting the
  coastal and noise terms as additive bounds) — turns the Hot-row exclusion into a readable
  number instead of an argument re-litigated every calibration session.
- `reachableWhittakerRows` — the temperature rows attainable on land given the above.

**Not proposed for W-aux.c.** It is a real scope addition, it touches `ToJson()` which the
block-2 wizard must round-trip, and adding derived fields mid-block-2 would move the
round-trip target. Sequence it after block 2 closes, or not at all.

---

---

## 10. Not changed, and why

- `map-pipeline-by-layers-ssot.md` — block 1 touches no governed stage contract. Block 3
  **will** touch it (contract M2a-3, `Vegetation ∩ HillsL2 == ∅`); that update belongs to
  the block-3 closure, not here.
- `SSoT_CONTRACTS.md` — no cross-cutting contract affected.
- `Phase_W_Design.md` — W-aux.c is an intercalated calibration batch, not a change to the
  Phase W design.
