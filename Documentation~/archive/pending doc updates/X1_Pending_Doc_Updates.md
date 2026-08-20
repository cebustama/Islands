# Phase X1 — Pending Documentation Updates

Status: **APPLIED — 2026-08-20.** All 9 items applied; 1 reduced in scope by verification.
Reduced: **§2.3** made its `DeepWater` correction conditional on a wrong statement existing in
`CURRENT_STATE.md`. Checked 2026-08-20: no such statement exists — the file already described
`DeepWater` as border-connected flood fill. The item therefore landed as an *interpretation*
note ("% of sea (border-connected water)", never "% of deep ocean"), not as a correction.
§2.1's status date was superseded by W-aux.f's.
Added at application time: the measured backing of rules **R4** and **R6** predates W-aux.d
and W-aux.f, and their subjects have changed. The governed record says so explicitly; the
rules themselves are unmodified and re-basing them is code work, not documentation.
Targets written: `PCG_Roadmap.md`, `CURRENT_STATE.md`, `changelog-ssot.md`,
`coverage-matrix.md`.
Applied by: Islands.PCG documentation-application session, 2026-08-20. See
`Application_Ledger_2026-08-20.md`.
This file is consumed input and is **not authority**.
Session (origin): Islands.PCG — Phase X1.a (preset diagnostics, authoring track), 2026-08-19.
Scope: register `Phase X1 — Authoring Tools` as a new named track, record the X1.a
diagnostics surface as implemented truth, and close one standing UNVERIFIED item
inherited from `W-aux.c` (`DeepWater`).

X1.b (objective-driven guidance) is **not** covered here — not implemented. Its doc
updates are produced at its own closure.

Items are independent unless noted. Applying them is user-controlled.

---

## 0. Evidence base

Verified in-session on 2026-08-19:

- Clean compile + EditMode Run All green after the X1.a files, **user-confirmed twice**
  (after the diagnostics batch, and again after the scroll/console-log patch).
- Wizard screenshots, `Default_MapPreset` loaded: diagnostics panel reporting 3 findings
  (`R4.ClampSaturationPlateau`, `R5.HotBandUnreachable`, `R6.ZeroCoverageDensities`), each
  carrying its `[measured: seed 56, res 256, Default_MapPreset, 2026-08-19]` label; and the
  diff panel reporting 34 changed lines for `Showcase → Default_MapPreset`.
- 10 new EditMode tests green (one per rule, a clean preset firing nothing, three diff gates).

Code read in-session with line anchors: `MapGenerationPresetWizard.cs` (full),
`MapGenerationPresetJsonImporter.cs` L1–90, `MapGenerationPreset.cs` (fields + `ToTunables`
L410–434), `Stage_Biome2D.cs` L200–235 (temperature composition), `BiomeTable.cs`
(bands + `Definitions`), `Stage_Vegetation2D.cs` L116–159, `TerrainNoiseSettings.cs` L106–113,
`Stage_Shore2D.cs` L10–41, `Stage_BaseTerrain2D.cs` L19, `PCG_Roadmap.md` L70–110.

Measured claims embedded in the shipped rule texts are **inherited, not re-measured here**:
they come from the W-aux.c blocks 2–3 run (seed 56, res 256, `Default_MapPreset`, 2026-08-19)
and are named as such inside each finding.

---

## 1. `planning/active/PCG_Roadmap.md` — **the only mandatory item**

Today X1.a code exists with no phase registered anywhere. This is the update that closes
that gap.

### 1.1 — Phase list (anchor: the block listing `Phase W`, `Phase V`, `Phase Q`, `Phase Q2`, ~L70–77)

Insert after the `Phase Q2` line:

```markdown
- Phase X1: active (authoring track — X1.a complete 2026-08-19; X1.b deferred)
  - X1.a: done (preset diagnostics + preset diff, Editor-only, golden-neutral)
  - X1.b: deferred (objective-driven guidance — blocked on measured runs beyond
    vegetation and height)
```

### 1.2 — New phase entry

Placement: alongside the other phase descriptions, order per the file's existing convention.

```markdown
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
`MapGenerationPresetWizard`, plus a filtered preset-to-preset JSON diff.

**X1.b — Objective-driven guidance (deferred).** "I want more vegetation / bigger
mountains / more rivers." Blocked on measured evidence: today only vegetation density and
height composition have backing runs. Rivers, lakes, biome shape and relief have none, so
any guidance on them would be `inferred` at best.
```

---

## 2. `CURRENT_STATE.md`

### 2.1 — Status date

Bump the status date to 2026-08-19 if the file carries one and X1.a is applied.

### 2.2 — New subsection (proposed placement: with the other Editor/inspection surfaces,
near the `W-aux.a — Islands.PCG.Inspection.MapStatsExporter2D` block)

```markdown
### X1.a — `Islands.PCG.Editor.MapGenerationPresetDiagnostics`

Pure Editor-side preset analysis: `preset → List<PresetFinding>`. No pipeline execution,
no asset mutation, no UI dependency. Golden-neutral: nothing in this surface can alter
generation output.

Six rules, all computed from preset fields plus static tables:

| Rule | Fires when | Backing |
|---|---|---|
| `R1.DegenerateFalloff` | `islandSmoothFrom01 >= islandSmoothTo01` | inferred |
| `R2.InvisibleMidWaterBand` | `midWaterDepth01 > 0` and `<= shallowWaterDepth01` | inferred |
| `R3.InertHillsNoise` | hills asset assigned, `hillsNoiseBlend == 0`, hills stage on | inferred |
| `R4.ClampSaturationPlateau` | `islandSmoothFrom01 < 0.05` with terrain amplitude `> 0` | measured |
| `R5.HotBandUnreachable` | land temperature ceiling `< 0.75` (Hot band lower bound) | measured |
| `R6.ZeroCoverageDensities` | vegetation stage on, biomes with `0 < density < 0.4` | measured |

`R5` ceiling model: `base − lapse·waterThreshold + coastModeration + 0.5·tempNoiseAmp`,
mirroring `Stage_Biome2D.ComputeTemperature` (latitude only subtracts; coast moderation
peaks at `coastDist = 0`; noise contributes at most half its amplitude).

`R4` onset model: `h01 = mask01·(1 + (n − 0.5)·amp)` clamped at 1, so saturation begins at
`mask01 >= 1/(1 + 0.5·amp)`. No monotone remap can remove it — redistribution exponent,
remap curve and quantization all act after the clamp and satisfy `f(1) = 1`.

`PresetFinding` carries `RuleId`, severity, `Backing` (`Measured` / `Inferred`) and, for
measured findings, the named run. The type makes an unbacked measured claim unrepresentable
rather than merely discouraged.

**Preset diff.** `DiffJson(a, b)` compares `ToJson()` output line-wise, excluding the same
non-reimportable surface the round-trip test excludes (`asset`, `stageTogglesNote`, the
`derived` block). Known limitation: duplicate identical lines are compared as a set, so a
moved duplicate is not reported.

Gate: `MapGenerationPresetDiagnosticsTests` — one preset per rule, a clean preset that
fires nothing, three diff gates. Green 2026-08-19.

Known gap, unchanged from W-aux.c block 2: **the wizard UI itself has no test.** The
importer and the diagnostics logic are tested; window behavior (import lifecycle, Undo,
panel rendering, scrolling, console log) is verified by manual inspection only.
```

### 2.3 — Correction to the `DeepWater` reading (see §4 below)

If `CURRENT_STATE.md` carries any statement treating `DeepWater` as a depth band, correct
it per §4. Not verified in-session whether such a statement exists — **check before
applying.**

---

## 3. `Documentation~/changelog-ssot.md`

New entry, newest-first, inserted above the most recent block:

```markdown
## X1.a — Preset diagnostics (authoring track)
Date: 2026-08-19

First phase of the authoring track. Editor-only, golden-neutral, no core runtime change.

**New:** `MapGenerationPresetDiagnostics` — pure `preset → List<PresetFinding>` with six
deterministic rules (degenerate falloff, invisible mid-water band, inert hills noise,
clamp-saturation plateau, Hot-band unreachability, sub-step vegetation densities). Every
finding is labeled `measured` or `inferred`; measured findings name their backing run.

**New:** filtered preset-to-preset JSON diff, reusing the round-trip test's exclusion
criterion (`asset`, `stageTogglesNote`, `derived`).

**Modified:** `MapGenerationPresetWizard` — diagnostics panel, compare-preset slot, diff
panel, scrollable output region, and a console dump for copy-paste. The W.b `HelpBox`
is retained: W.b has not closed, so the declared gap is still true.

**Gate:** `MapGenerationPresetDiagnosticsTests`, 10 tests, green.

Rationale for the pure-class split: an `EditorWindow` is expensive to test and a
diagnostics rule is trivial to test. Same separation already proven by
`MapGenerationPresetJsonImporter`.

Rationale for the labeling rule: the measured density→coverage curve (0.25 → 0.26%
coverage) demonstrates that intuition about this pipeline's parameters is unreliable by
orders of magnitude. Tooling that emits confident wrong advice is worse than tooling that
stays silent.
```

---

## 4. Closing `W-aux.c` §8.1 — `DeepWater` is **not** a depth band (RESOLVED)

`W-aux_c_Pending_Doc_Updates.md` §8.1 recorded, as UNVERIFIED, that `DeepWater` appeared to
be a superset of the water bands rather than the isolated deep band. **Resolved this
session by code inspection**, not by a measured run:

- `Stage_BaseTerrain2D.cs` L19: `DeepWater = border-connected NOT Land (stable flood fill)`.
  It is the sea — every water cell reachable from the map border — not a depth classification.
- `Stage_Shore2D.cs` L26 states as an invariant: `ShallowWater ∩ DeepWater is intentionally
  non-empty`; L27: the stage does not mutate `DeepWater`. Shallow and mid are refinements
  layered on top of the sea, so the three counts overlap by design and their sum exceeding
  total water is expected, not a bug.
- Consistent with the measured numbers: `DeepWater` 42994 vs total water 43341. The 347-cell
  remainder is water **not** connected to the border — inland water. That reading is
  `inferred` arithmetic, not verified against a run.

**Consequence to record wherever `DeepWater` is described:** any "% of deep ocean" derived
from this layer is wrong. The correct phrasing is "% of sea (border-connected water)".
Applies to `MapStatsExporter2D` output interpretation and to any calibration reasoning
built on it. This does not change generation output — it corrects interpretation only.

Proposed home: fold into the same doc that describes the water layers (§2 of this file's
`CURRENT_STATE.md` target if that is where water layers live), and mark
`W-aux.c` §8.1 as resolved with a pointer here rather than deleting it.

---

## 5. `coverage-matrix.md` (optional)

If the authoring track is to be navigable from the matrix, add one row in the existing
format:

```markdown
| Authoring tools (preset diagnostics, preset diff) | `planning/active/PCG_Roadmap.md` (Phase X1) + `CURRENT_STATE.md` | planning + implemented truth | Active |
```

Deliberately **not** proposed: a separate SSoT for authoring tools. The surface is one
Editor class and one window; promoting it to subsystem authority would create an authority
boundary with nothing behind it.

---

## 6. Relation to `W-aux.c` §9 — parameter legibility track

X1.a is the first concrete answer to the standing concern recorded in `W-aux.c` §9. Three of
the four illegible parameters catalogued there are now surfaced automatically at authoring
time instead of costing a measure-and-diagnose cycle:

| §9 entry | X1.a rule |
|---|---|
| `terrainAmp` non-monotone past `1/(1+amp/2)` | `R4.ClampSaturationPlateau` |
| `heightRedistributionExponent` / `heightRemapCurve` inert over the saturated set | folded into `R4`'s text |
| `vegetationDensity` is not a per-cell rate | `R6.ZeroCoverageDensities` |
| land temperature ceiling couples three unnamed parameters | `R5.HotBandUnreachable` |

If §9 is registered as a standing thread, note there that diagnostics rules are its
delivery mechanism, so future legibility findings have an obvious home.

---

## 7. Not changed, and why

- **No pipeline SSoT changes.** `map-pipeline-by-layers-ssot.md`, `pcg-core-ssot.md` and
  `SSoT_CONTRACTS.md` are untouched: X1.a reads preset fields and static tables, and
  produces text. No contract, invariant, layer, field or ordering is affected.
- **The W.b `HelpBox` stays.** Verified this session: the five component-scoped fields
  (`enableRegionsStage`, `enableHydrologyStage`, `hydroEpsilon`,
  `hydroRiverThresholdFraction`, `hydroMinLakeArea`) are absent from `MapGenerationPreset.cs`,
  and `PCG_Roadmap.md` L72 still reads `W.b next`. The warning is true. **When W.b closes,
  removing it is part of W.b's closure, not a future X1 iteration** — otherwise the wizard
  starts lying about its own gap.
- **No water advice shipped.** No rule mentions water depth beyond `R2`, which reasons about
  preset band geometry only and never reads `DeepWater`.
- **`heightScale` and vegetation `NoiseFrequency` untouched**, per standing session constraint.
- **`hillsL2_fraction` 0.65 → 30.07% of Land** (marginally outside the 15–30% window) remains
  an open calibration decision from W-aux.c. Deliberately **not** turned into a diagnostics
  rule: it is a calibration target, not a preset contradiction, and encoding a target in a
  contradiction detector would blur what a finding means.
