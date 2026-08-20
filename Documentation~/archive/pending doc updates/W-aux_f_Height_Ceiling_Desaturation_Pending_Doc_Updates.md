# W-aux.f — Height ceiling de-saturation: pending doc updates

Status: **APPLIED — 2026-08-20.** All 8 items applied; 1 retargeted, 1 partially resolved.
Retargeted: **§7** (shadow defaults in test fixtures) was proposed for `method_notes.md`.
That document governs *skill construction method* and has an append-only change discipline
with no section a Unity test-fixture rule can occupy. The rule was applied in full to
`SSoT_CONTRACTS.md` instead, adjacent to the §6 calibration inventory that depends on it.
`method_notes.md` is unchanged. Reversible if the original target is preferred.
§6 placed in `SSoT_CONTRACTS.md` per user decision (its own recommendation: three of the six
entry points fall outside the F2 slice).
**§8 partially resolved as option 1** — the probe's formula coupling is registered in
`map-pipeline-by-layers-ssot.md` §F2. Promotion to a governed diagnostic surface, or a
removal date, remains **undecided**; do not read this header as closing that question.
The four carried-forward UNVERIFIED items are recorded, still labelled, in
`CURRENT_STATE.md` §Open observations.
Targets written: `map-pipeline-by-layers-ssot.md`, `CURRENT_STATE.md`, `changelog-ssot.md`,
`PCG_Roadmap.md`, `SSoT_CONTRACTS.md`.
Applied by: Islands.PCG documentation-application session, 2026-08-20. See
`Application_Ledger_2026-08-20.md`.
This file is consumed input and is **not authority**.
Date: 2026-08-20
Batch label `W-aux.f` is proposed, not authoritative — it follows the `W-aux.e` lineage
because that audit produced the measured verdict that opened this one. Rename freely.

Queue note: this file joins four earlier unapplied queues — `W-aux_c_Pending_Doc_Updates.md`,
`W-aux_c_Blocks_2_3_Pending_Doc_Updates.md`, `W-aux_d_Vegetation_Quantile_Pending_Doc_Updates.md`
and `W-aux_e_Threshold_Mapping_Audit_Pending_Doc_Updates.md`. Apply in lineage order
(c → d → e → f); this file's edits do not depend on theirs, but the changelog entries read
wrong out of order. `Phase_Q_Pending_Doc_Updates.md` §3.1 and `Phase_W_Pending_Doc_Updates.md` §3
remain blocked from their own sessions and are untouched by this batch.

Items are independent unless noted. Applying them is user-controlled.

---

## 0. What this batch changed, in one paragraph

`Stage_BaseTerrain2D` computed `h01 = mask01 + (n − 0.5)·terrainAmp·mask01` and then
`saturate`d. The perturbation is centred **on the mask value**, so in the island core every
cell with `n > 0.5` overflowed 1.0 and was clipped, producing a flat plateau at exactly
`Height == 1.0` (measured: 11.72 / 11.62 / 3.74 % of Land at seeds 56 / 8 / 243). The fix
normalizes the perturbed value by its own theoretical maximum, `1 + terrainAmp/2`, so 1.0 is
reachable only at `n == 1` instead of by clipping. The whole land field is thereby rescaled
by a constant factor, which required recalibrating `waterThreshold01` at every entry point
so the coastline would not move. It did not move: Land, DeepWater, LandCore, LandEdge,
LandInterior and Lakes goldens are **bit-identical** across every fixture. 29 golden
constants that depend on the *value* of Height were re-anchored.

---

## 1. `map-pipeline-by-layers-ssot.md` — §F2 base terrain contracts

**Why.** This is the slice contract for the stage whose output range semantics changed. The
document currently lists the tunables `Stage_BaseTerrain2D` reads and the layers it writes,
but says nothing about how Height is composed or what its ceiling means.

**Proposed edit** — in `### F2 base terrain contracts`, extend the first block:

```
`Stage_BaseTerrain2D` (F2b shape pipeline)
- reads tunables: `islandRadius01`, `waterThreshold01`, `islandSmoothFrom01/To01`,
  `islandAspectRatio`, `warpAmplitude01`, `terrainNoise.amplitude`
- writes `Height` (ScalarField2D), `Land` (MaskGrid2D), `DeepWater` (MaskGrid2D)
- `DeepWater` = border-connected NOT Land (deterministic flood fill)
- `DeepWater ∩ Land == ∅`
- **(W-aux.f)** height perturbation is normalized by `(1 + terrainAmp/2)`, the theoretical
  maximum of `mask01·(1 + (n − 0.5)·terrainAmp)`. `Height == 1.0` is therefore reachable
  only at `n == 1` (measure ≈ 0 for normalized fBm), never by clipping. `terrainAmp == 0`
  gives a normalization factor of exactly `1f`, so unperturbed presets are bit-identical.
- **(W-aux.f)** `waterThreshold01` is calibrated against the normalized field. When
  `terrainNoise.amplitude` or `heightRedistributionExponent` change, the threshold that
  preserves a given coastline moves by `(1 + amplitude/2)^(−exponent)`. This is *not*
  applied automatically: a silently self-adjusting threshold would hide recalibration from
  the goldens.
```

**Note — no change needed elsewhere in this document.** The `NoShape` path (`h01 = n`) does
not reach the normalized expression and its contract is unaffected.

---

## 2. `CURRENT_STATE.md` — close the atom known-issue

**Why.** `CURRENT_STATE.md` is the implemented baseline. The W-aux.e audit left an open
known-issue (the `Height == 1.0` atom, verdict *defect*). It is now closed with measured
evidence and must stop being described as open.

**Proposed edit** — replace the known-issue entry (or add, if the W-aux.e queue has not been
applied yet, in which case apply this text instead of that one):

```
### Height ceiling saturation — RESOLVED (W-aux.f, 2026-08-20)

The `Height == 1.0` plateau reported by the W-aux.e audit is gone. `Stage_BaseTerrain2D`
now normalizes the perturbed height by `(1 + terrainAmp/2)` instead of relying on
`math.saturate` to absorb the overflow.

Measured with the `LogHeightHistogram` probe, `Default_MapPreset`, res 256, seeds 56 / 8 / 243:

| | before | after |
|---|---|---|
| cells at `Height == 1.0` | 2601 / 2602 / 748 | **0 / 0 / 0** |
| cells at `Height > 1.0` | 0 | 0 |
| max `Height` | 1.0 | 0.95957 / 0.96334 / 0.93824 |
| min `Height` (sea floor) | 0.09440 | 0.08242 |
| min `Height` on land | — | 0.41232 |
| shaping-chain mirror mismatch | 0 | 0 |

The former atom fanned out into the top band as predicted: in seed 56 the cells above the
theoretical destination (0.8731 post-pow) total ≈ 2580 against the 2601 of the original
atom (bin-interpolated, not cell-exact).

`saturate` is retained as a guard; it is a no-op except for floating-point rounding.
```

---

## 3. `CURRENT_STATE.md` — recalibrated defaults

**Why.** Four code-level defaults and one asset changed value. These are implemented truth
and a reader of the baseline will otherwise see numbers that no longer exist.

**Proposed edit** — add to the tunables / preset section:

```
### `waterThreshold01` recalibration (W-aux.f)

The height normalization rescales the land field by a constant, so every calibrated water
threshold moved by `(1 + amplitude/2)^(−exponent)` to hold its coastline in place:

| Entry point | before | after | factor inputs |
|---|---|---|---|
| `MapTunables2D.Default` | 0.50 | **0.42553192** | amp 0.35, exp 1.0 |
| `MapGenerationPreset` (class field default) | 0.50 | **0.42553192** | mirrors the above |
| `PCGMapVisualization` (serialized default) | 0.50 | **0.42553192** | mirrors the above |
| `PCGMapCompositeVisualization` (serialized default) | 0.50 | **0.42553192** | mirrors the above |
| `PCGMapTilemapVisualization` (serialized default) | 0.50 | **0.42553192** | mirrors the above |
| `Default_MapPreset.asset` | 0.472 | **0.412119** | amp 0.22, exp 1.3 |

Serialized component instances in existing scenes keep their own saved values; only newly
added components pick up the new default.

Verified consequence: **Land topology is bit-identical.** `Land`, `DeepWater`, `LandCore`,
`LandEdge`, `LandInterior` and `Lakes` goldens passed unchanged in every fixture — ellipse,
rectangle and shape-input — across the full EditMode suite.
```

**Note — no change needed** to the existing `Height == 0 on water is a preset property`
section. Its reasoning survives: `mask01 == 0` still yields `h01 == 0`, because the
normalization is a multiplication.

---

## 4. `changelog-ssot.md` — W-aux.f entry

**Why.** Governed changelog; a 29-constant golden break must be registered with its cause
and its values.

**Proposed entry:**

```
## W-aux.f — Height ceiling de-saturation (2026-08-20)

**Cause.** `Stage_BaseTerrain2D` centred the height perturbation on the mask value, so the
island core clipped against 1.0 and lost all relief information. Diagnosed in W-aux.e,
fixed here by normalizing with `(1 + terrainAmp/2)`.

**Code changed.** `Stage_BaseTerrain2D` (1 site), `BaseTerrainStage_Configurable` (2 sites,
one per shape branch), `PCGMapTilemapVisualization.LogHeightHistogram` (1 site, the probe's
shaping-chain re-derivation). `waterThreshold01` recalibrated at 6 entry points (see
CURRENT_STATE). Test fixtures in `StageBaseTerrain2DTests` (Rectangle, Custom) changed from
hardcoded literals to `MapTunables2D.Default.waterThreshold01`; `MapGenerationPresetTests`
L62 re-pinned and its remap-arithmetic test now sets its own threshold explicitly.

**Golden break.** Intentional. Land topology unaffected; every re-anchored constant depends
on the *value* of Height.

| File | Constant | before | after |
|---|---|---|---|
| `MapPipelineRunner2DGoldenF3Tests` | HillsL1 | `0xD8B3DCF4A4AC3BA0` | `0xCB0433856C6A4A3B` |
| `MapPipelineRunner2DGoldenF3Tests` | HillsL2 | `0x29F3B1EA4B818E4F` | `0xA6E4DF1C6BE62012` |
| `MapPipelineRunner2DGoldenF5Tests` | Vegetation | `0x7929C1150266E4B5` | `0x7466D4574597051C` |
| `MapPipelineRunner2DGoldenF6Tests` | Walkable | `0xA9A213FFB5842CF7` | `0x1B6B5A207F0961FA` |
| `MapPipelineRunner2DGoldenF6Tests` | Stairs | `0x678993F8298D975F` | `0x169920A412C17B67` |
| `MapPipelineRunner2DGoldenLTests` | FlowAccumulation | `0xD549F3F32D57C771` | `0xFF413C94785FCEAC` |
| `MapPipelineRunner2DGoldenLTests` | Rivers | `0x7BE94E82265A2FEA` | `0xA14DDE84505F6876` |
| `MapPipelineRunner2DGoldenLMTests` | FlowAccumulation | `0xD549F3F32D57C771` | `0xFF413C94785FCEAC` |
| `MapPipelineRunner2DGoldenLMTests` | Moisture | `0x0A2EFAAF55D33A97` | `0x4A046696491A7B1C` |
| `MapPipelineRunner2DGoldenLMTests` | Temperature | `0x4A10E758A2C6AD88` | `0xBFB50453C73FF496` |
| `MapPipelineRunner2DGoldenLMTests` | Biome | `0x0051483ABC36882A` | `0x4651014815F02263` |
| `MapPipelineRunner2DGoldenLMTests` | Rivers | `0x7BE94E82265A2FEA` | `0xA14DDE84505F6876` |
| `MapPipelineRunner2DGoldenMTests` | Temperature | `0xB21849253CD4A4A5` | `0x4AC0E124479485A9` |
| `MapPipelineRunner2DGoldenMTests` | Biome | `0x83F2BC89009F7C13` | `0xA7E6C1ECCB9C9AB3` |
| `MapPipelineRunner2DGoldenM2Tests` | Temperature | `0xB21849253CD4A4A5` | `0x4AC0E124479485A9` |
| `MapPipelineRunner2DGoldenM2Tests` | Biome | `0x83F2BC89009F7C13` | `0xA7E6C1ECCB9C9AB3` |
| `MapPipelineRunner2DGoldenM2bTests` | BiomeRegionId | `0x6CAE7B67362E5D3D` | `0x7B6642295E08D0CD` |
| `MapPipelineRunner2DGoldenM2bTests` | Temperature (cross-check) | `0xB21849253CD4A4A5` | `0x4AC0E124479485A9` |
| `MapPipelineRunner2DGoldenM2bTests` | Biome (cross-check) | `0x83F2BC89009F7C13` | `0xA7E6C1ECCB9C9AB3` |
| `StageBiome2DTests` | Temperature | `0x6D4398BDE2385AF8` | `0x2BD9F557217BC216` |
| `StageBiome2DTests` | Biome | `0xF20A7D056CEB37F3` | `0x25FC55783B618FF3` |
| `StageHills2DTests` | HillsL1 | `0xD8B3DCF4A4AC3BA0` | `0xCB0433856C6A4A3B` |
| `StageHills2DTests` | HillsL2 | `0x29F3B1EA4B818E4F` | `0xA6E4DF1C6BE62012` |
| `StageHydrology2DTests` | FlowAccumulation | `0xD549F3F32D57C771` | `0xFF413C94785FCEAC` |
| `StageHydrology2DTests` | Rivers | `0x7BE94E82265A2FEA` | `0xA14DDE84505F6876` |
| `StageTraversal2DTests` | Walkable | `0xB726EAA2F984C49B` | `0x3C9A669DF4449546` |
| `StageTraversal2DTests` | Stairs | `0x74183135BE3C8913` | `0xED7D577500BFB901` |
| `StageVegetation2DTests` | Vegetation (Legacy) | `0xE7876A1519EC45D3` | `0x6CDCB0E869BB070F` |
| `StageVegetation2DTests` | Vegetation (M2a) | `0x6AB1192251196B17` | `0x6D433B1023A09BB6` |

Stage-level and pipeline-level constants for the same layer agree exactly (HillsL1/L2,
Temperature, Biome, Rivers, FlowAccumulation), confirming the change propagated
consistently through the stage hierarchy.

**Preset reference goldens.** `Default_MapPreset`, res 256 — the `Height` hashes captured
in W-aux.e (`196AF8D87C1D19EC` / `58707DC13667740C` / `8067C0763C948648` for seeds
56 / 8 / 243) are superseded. New values were not captured in this batch.
```

---

## 5. `PCG_Roadmap.md` — next batch and reopened option

**Why.** Roadmap is planning, not authority, but two things changed for the next batch and
one previously-closed option reopened.

**Proposed edit:**

```
- **Hills window recalibration** (opens after W-aux.f). The declared HillsL2 window of
  15–30 % of Land was *unachievable* before the fix: fractions below the atom size could
  not be reached by any threshold. It is now achievable. Measured after the fix
  (`Default_MapPreset`, res 256): band `>= thL2_eff` = 10.11 / 9.74 / 2.82 % of Land at
  seeds 56 / 8 / 243; the cut for 15 % is 0.8584 / 0.8574 / 0.7949.
- Two levers are available that did not exist before:
  - `heightRedistributionExponent`. Probe sweep at seed 56, effective thresholds held
    fixed: pow 1.3 → 10.11 %, **pow 1.0 → 16.45 %** (inside the nominal window),
    pow 0.75 → 22.27 %, pow 1.5 → 8.22 %.
  - **Quantile mapping in Hills, previously discarded, is available again.** It was
    rejected in W-aux.e because it could not split the atom's tie without an arbitrary
    spatially-biased tiebreak. With no atom there is no tie. The 3.6× spread between
    seeds (10.11 % vs 2.82 %) is exactly the symptom a quantile addresses by construction.
```

---

## 6. NEW — calibration entry-point inventory (needs a governed home)

**Why.** This batch spent most of its effort discovering, one failing test at a time, that
`waterThreshold01` is declared in six places in code plus every preset asset. That inventory
currently exists only as session output. The next person to change the height formula will
rediscover it the same expensive way.

**Open decision — where it belongs.** Two candidates:

- `SSoT_CONTRACTS.md` — the inventory is cross-cutting (core struct + samples + adapters +
  tests), which matches this document's scope.
- `map-pipeline-by-layers-ssot.md` §F2 — the inventory is specific to one field's
  calibration, which matches that section's scope.

Recommendation: `SSoT_CONTRACTS.md`, because three of the six entry points are outside the
F2 slice entirely.

**Proposed content** (place under the chosen heading):

```
### Calibration entry points for `waterThreshold01`

Any change to the `Height` composition formula invalidates every calibrated water threshold.
All of these must be updated together:

1. `MapTunables2D.Default` — the struct default; what every EditMode fixture uses.
2. `MapGenerationPreset` class field default — must equal (1), enforced by
   `ToTunables_DefaultPreset_MatchesMapTunables2DDefault`.
3–5. Serialized defaults on `PCGMapVisualization`, `PCGMapCompositeVisualization`,
   `PCGMapTilemapVisualization`. Affect newly added components only.
6. Every `MapGenerationPreset` asset on disk, each with its own tuned value.

Test fixtures that mean "Default but X" must derive the threshold from
`MapTunables2D.Default.waterThreshold01` rather than copy the literal. Test fixtures that
assert arithmetic over the threshold must set it explicitly instead of inheriting it.
```

---

## 7. NEW — test-suite hygiene note (`method_notes.md`)

**Why.** The same failure mode appeared five times in this batch and cost more time than the
fix itself. It generalizes beyond this workstream.

**Proposed content:**

```
### Shadow defaults in test fixtures

A fixture that means "the defaults, but with X" and expresses it by copying the default
values as literals is a *shadow default*. While nobody touches the real default, the copy
agrees and nothing is visible. When the default is recalibrated, the fixture silently
becomes a different configuration while still claiming to be the same one.

The dangerous case is not the failing test — it is the symmetric one, where the copy drifts
in a direction that happens not to change the asserted output. The test then stays green
while comparing two different things.

Rule: a test may inherit a default **or** assert arithmetic over it, never both.
- Comparing against `Default` → derive the value from `Default`.
- Asserting hand-computed arithmetic → set the input explicitly inside the test.
- Pinning a default's value → hardcode it; that is the test's whole purpose.

Instances found and fixed in W-aux.f: `StageBaseTerrain2DTests.RectangleTunables()`, the
inline tunables in `N5a_Custom_WithoutShapeInput_MatchesEllipse`,
`MapGenerationPresetTests` L62 and `ToTunables_HillsL1L2_AreForwardedAsRelativeFractions`,
and (green but weakened) `StageHills2DTests.N5d_BlendPositive_DiffersFromBlend0`.

Deliberately left alone: `StageBaseTerrain2DTests.NoShapeTunables()` — the NoShape path does
not consume the changed formula, and compensating it would break green goldens for no reason.
```

---

## 8. Probe status — open decision, not a doc update

`PCGMapTilemapVisualization.LogHeightHistogram` now holds the **third copy** of the height
composition formula (its adapter-side shaping-chain re-derivation). It is temporary,
ungoverned instrumentation that the user has chosen to keep.

Its `mismatch` counter is what caught the desynchronization in this batch and is the reason
the attribution output can be trusted at all. But while it exists, the formula lives in three
places, and only two of them are governed.

Options, in order of preference:
1. Register the coupling in §1 above (one line: the probe mirrors the F2 formula and must be
   updated with it).
2. Schedule the probe for removal with an explicit date.
3. Promote the probe to a governed diagnostic surface with its own contract.

**Unresolved. Do not treat any of these as decided.**

---

## Evidence register

Everything in this file was produced in the 2026-08-20 session and confirmed by the user or
by direct file reading:

- Clean compile and EditMode Run All **green** after the final three constants — user-confirmed.
- `LogHeightHistogram` output at seeds 56 / 8 / 243, res 256, `Default_MapPreset`, with
  `mismatch = 0` and `saturate-clip = 0` — user-pasted logs.
- The 29 old → new hash pairs — read from the failure messages of three consecutive runs;
  decimal values converted to hex by script.
- Formula copy inventory (3 files, 4 call sites, 3 scale declarations, perfect symmetry) —
  `rg` output pasted by the user.
- Test fixture contents (`StageBaseTerrain2DTests`, `StageHills2DTests`,
  `MapGenerationPresetTests`, F2/F3 golden tests) — files read directly.

**Carried forward UNVERIFIED:**

- Effect of the fix on Temperature magnitude in gameplay terms (land is now ~12.7 % lower on
  unclipped cells; with `lapseRate = 0.62` this warms the core). The hash changed; the
  *perceptual* effect was never inspected.
- Whether the optional hygiene edits to `StageHills2DTests.N5d_BlendPositive_DiffersFromBlend0`
  and the explanatory comment on `NoShapeTunables()` were applied.
- No visual smoke test was run on the new Height field. Only histogram statistics and hashes.
- New `Height` field goldens for `Default_MapPreset` at res 256 were **not** captured.
