# W-aux.e — Threshold-mapping audit (`Stage_Hills2D` / `Stage_BaseTerrain2D`): pending doc updates

Status: **APPLIED — 2026-08-20.** 5 of 8 items applied; 3 superseded.
Superseded: **§3.2** (the `Height` atom recorded as an *open* known issue) by W-aux.f §2 —
`CURRENT_STATE.md` carries one block, in the past tense, marked RESOLVED, rather than an open
issue immediately followed by its closure; **§4.2** (next batch = W-aux.f) by W-aux.f's own
closure; **§5** (SSoT edit deliberately deferred) by W-aux.f §1, which made the edit.
Merged at application time: **§4.1** (roadmap F3b window unreachable) with W-aux.f §5 (window
reachable, with measured cuts) into a single F3b block — applied separately they would have
left the roadmap asserting both.
§3.1's status date was superseded by W-aux.f's.
Targets written: `changelog-ssot.md`, `CURRENT_STATE.md`, `PCG_Roadmap.md`.
Applied by: Islands.PCG documentation-application session, 2026-08-20. See
`Application_Ledger_2026-08-20.md`.
This file is consumed input and is **not authority**.
Date: 2026-08-19
Batch label `W-aux.e` is proposed, not authoritative — it follows the `W-aux.d` lineage
because that batch produced the generalizable lesson that opened this one. Rename freely.

Queue note: this file joins two earlier unapplied queues — `W-aux_c_Blocks_2_3_Pending_Doc_Updates.md`
(and `W-aux_c_Pending_Doc_Updates.md`) and `W-aux_d_Vegetation_Quantile_Pending_Doc_Updates.md`.
Apply in lineage order (c → d → e); this file's edits do not depend on theirs, but the
changelog entries read wrong out of order.

---

## 0. What this batch did, in one paragraph

W-aux.d established that an absolute threshold compared against a non-uniform field is not
a fraction, however the variable is named. Two threshold consumers remained with that shape:
`Stage_Hills2D` (`Height >= hillsThresholdL1/L2`) and `Stage_BaseTerrain2D`
(`Land = Height >= waterThreshold01`). This batch audited both **by measurement**, with a
temporary adapter-side probe that re-derives the BaseTerrain shaping chain and validates
itself against the exported `Height` field. It changed **no core code, no tunables, no
contracts and no goldens**. The verdict is not the one the analogy predicted: the water
threshold is a *feature*, the hills classifier is *correct and exonerated*, and the real
defect is upstream in the height formula, which manufactures a degenerate atom of cells
pinned at exactly `Height == 1.0`.

---

## 1. Evidence status — closed

Verified in session 2026-08-19:

- **Compilation clean + EditMode Run All green** — confirmed by the user.
- **`[hprobe]` logs at three seeds** (56, 8, 243; res 256; preset `Default_MapPreset`),
  pasted by the user. The probe's shaping-chain mirror reports
  `mismatch vs exported Height: 0 cells (maxAbsDiff=0.00E+000)` at **all three seeds** —
  the re-derivation is byte-exact, so the attribution below is measurement, not inference.
- **`Log Map Stats` at the same three seeds**, pasted by the user, consistent with the probe.
- **Effective tunables of the reference run** (from the preset JSON `derived` block and the
  probe header, agreeing exactly): `waterThreshold01` 0.472, `heightRedistributionExponent`
  1.3, `hillsL1/L2_fraction` 0.42 / 0.65 → `hillsThresholdL1_effective` **0.69376**,
  `hillsThresholdL2_effective` **0.892816**, `hillsNoiseBlend` 0.35, terrain noise amplitude
  0.22, `heightQuantSteps` 1024, shape Ellipse.
  Note: these are **not** the `MapTunables2D` defaults (0.30 / 0.43 → 0.6304 / 0.7893) that
  earlier notes assumed.
- **Goldens captured this session** (not asserted unchanged — no prior value was available
  in-session to compare against): seed 56 `Height=196AF8D87C1D19EC`, seed 8
  `Height=58707DC13667740C`, seed 243 `Height=8067C0763C948648`.

### Measured results

Saturation, `Height == 1.0` exactly:

| | seed 56 | seed 8 | seed 243 |
|---|---|---|---|
| count | 2601 | 2602 | 748 |
| % of `Land` | 11.72 | 11.62 | 3.74 |
| attributed to `saturate`-clip (`raw > 1`) | 2601 | 2602 | 748 |
| attributed to `pow` / spline / `raw == 1` / other | 0 | 0 | 0 |

`HillsL2` fractions and the cuts that would realize the declared 15–30% window:

| | seed 56 | seed 8 | seed 243 | spread |
|---|---|---|---|---|
| band `Height >= thL2` (0.8928), % of `Land` | 32.41 | 33.10 | 17.60 | 15.5 pts |
| exported `HillsL2` layer (with `blend` 0.35), % of `Land` | 30.07 | 33.67 | 25.42 | 8.3 pts |
| Height cut realizing 15% of `Land` | 0.9844 | 0.9814 | 0.9111 | 0.073 |
| Height cut realizing 30% of `Land` | 0.9092 | 0.9063 | 0.8418 | 0.067 |

`Land` drift at fixed `waterThreshold01` = 0.472 (% of domain):

| variant | seed 56 | seed 8 | seed 243 |
|---|---|---|---|
| actual (pow 1.3) | 33.87 | 34.16 | 30.50 |
| pow 1.0 | 38.48 | 38.87 | 35.54 |
| pow 0.75 | 42.60 | 44.04 | 41.25 |
| pow 1.5 | 31.53 | 31.49 | 27.79 |
| spline on vs off | identical at every seed | identical | identical |

---

## 2. `changelog-ssot.md` — new entry (MANDATORY)

**Locate** the first `##` heading in the file:

```
## W-aux.b — Submarine relief calibration reference
```

**Insert immediately before it** (newest-first ordering, matching the file's existing
convention). If the W-aux.c and W-aux.d entries are applied first, insert this one above
those instead:

```markdown
## W-aux.e — Threshold-mapping audit (measurement batch, no code change)
Date: 2026-08-19

**No core code, tunable, contract or golden changed.** An audit that asked whether
`Stage_Hills2D` and `Stage_BaseTerrain2D` carried the same defect W-aux.d fixed in
vegetation. Answer: neither, and something worse sits upstream of both.

Instrument: `LogHeightHistogram()` on `PCGMapTilemapVisualization` (+ Inspector button),
adapter-side, temporary. It re-derives the BaseTerrain shaping chain from the preset's
effective tunables and reports a cell-by-cell mismatch count against the exported `Height`
field; **0 mismatches, maxAbsDiff 0, at all three seeds**, so its attribution is
measurement rather than reading of code.

Measured, res 256, seeds 56 / 8 / 243, preset `Default_MapPreset`
(wt 0.472, pow 1.3, thL1_eff 0.69376, thL2_eff 0.892816, blend 0.35, terrain amp 0.22):

| | 56 | 8 | 243 |
|---|---|---|---|
| `Height == 1.0` exactly, % of `Land` | 11.72 | 11.62 | 3.74 |
| attributed to pre-quantization `saturate` clip | 100% | 100% | 100% |
| band `Height >= thL2`, % of `Land` | 32.41 | 33.10 | 17.60 |
| cut realizing 15% / 30% of `Land` | 0.9844 / 0.9092 | 0.9814 / 0.9063 | 0.9111 / 0.8418 |
| `Land` % at pow 1.3 → 1.0 → 0.75 → 1.5 | 33.87 / 38.48 / 42.60 / 31.53 | 34.16 / 38.87 / 44.04 / 31.49 | 30.50 / 35.54 / 41.25 / 27.79 |

**Verdicts.**
- `waterThreshold01` — **feature.** Nothing presents it as a fraction; it is a coordinate,
  and `pow` is a documented reshaper whose purpose is to move mass. The coupling measured
  above is the expected behaviour of that pair, and `Land %` is already exported for
  calibration.
- Height formula in `Stage_BaseTerrain2D` — **defect.** `h01 = mask01 + (n − 0.5)·amp·mask01`
  centres the perturbation **on the mask value**, so in the island core (`mask01 = 1`) every
  sample with `n > 0.5` overflows and is clipped to exactly 1.0. The atom's size is the core
  area — a silhouette/warp property that varies by seed — not a property of relief.
- `Stage_Hills2D` — **inherited defect; classifier exonerated.** It implements its contract
  faithfully. The declared 15–30% peak window is unachievable and drifts 8–15 points across
  seeds because the field's ceiling is degenerate: with 11.6–11.7% of `Land` tied at 1.0
  (seeds 56, 8), **no threshold can select a peak fraction below ~11.6%** — the tie is taken
  whole or not at all.

**Two side findings, recorded so nobody re-derives them.**
- Fixing the `Noise.GetFractalNoise` normalization (`amplitudeSum` accumulated after
  `amplitude *= persistence`; ÷0.875 instead of ÷1.75 at 3 octaves) would **not** shrink the
  atom: it scales `|n − 0.5|` but not its sign, and the clip discards magnitude. Only
  `P(n > 0.5)` in the core matters. The normalization remains separate debt.
- The reference preset's `heightRemapCurve` (two linear nodes) is **functionally identity**:
  the sweep shows spline-on and spline-off producing identical `Land` and `HillsL2` at every
  seed, yet `ScalarSpline.IsIdentity` reports `false` (conservative flag after
  `FromAnimationCurve` sampling). Harmless — costs one pass of `Evaluate` per cell.
- `Height.min` = 0.0944 at all three seeds is the W-aux.b sea floor,
  `(0.35 − 0.15) · 0.472`, not noise. Identical minima across seeds are expected.

Options for the follow-up batch were costed, not implemented. Decision recorded 2026-08-19:
**option A** (reformulate the perturbation so the core has headroom) proceeds to design.
Rejected: quantile-cut on Hills (cannot split the 1.0 tie without arbitrary, spatially
biased tie-breaking — *ineffective*); fBm normalization fix as a remedy for this defect
(*ineffective*, see above); declaring the plateaus a feature (*expressiveness*).
```

---

## 3. `CURRENT_STATE.md` — known issue + status date (MANDATORY)

### 3.1 Status date

**Locate:**

```
Status date: 2026-08-18 (Phase W in progress — W.a, W-aux.a and W-aux.b closed)
```

**Replace with:**

```
Status date: 2026-08-19 (Phase W in progress — W.a, W-aux.a, W-aux.b, W-aux.c, W-aux.d closed; W-aux.e audit closed, no code change)
```

*(If the W-aux.c / W-aux.d doc queues are applied first and already rewrote this line,
keep their wording and append `; W-aux.e audit closed, no code change`.)*

### 3.2 Known issue — new block

**Locate** the start of the section listing what package development just resolved:

```
## What current package development just resolved
```

**Insert immediately before it:**

```markdown
## Known issue — `Height` saturation atom (measured 2026-08-19, unfixed)

`Stage_BaseTerrain2D` computes `h01 = mask01 + (n − 0.5) · terrainAmp · mask01` and then
`saturate`s. The perturbation is centred **on the mask value**, so wherever the island mask
reaches 1.0 — the whole core — any noise sample above 0.5 overflows and is clipped to
exactly 1.0. `BaseTerrainStage_Configurable` mirrors this exactly, so the Tilemap adapter,
the exporter statistics and the console goldens all measure the same behaviour.

Measured at res 256, preset `Default_MapPreset` (terrain amplitude 0.22), seeds 56 / 8 / 243:
`Height == 1.0` holds for **11.72 / 11.62 / 3.74 %** of `Land`, and **100 %** of those cells
are attributable to that clip (`pow` 0, spline 0, at every seed) via a shaping-chain mirror
that matched the exported field cell-for-cell.

**Why it matters beyond aesthetics.** Those cells are degenerate in height: there is no
relief there to classify, and any threshold below 1.0 takes the entire block at once. The
size of the block is the *core area* — a property of silhouette and warp that varies by seed
— so every consumer of `Height` inherits a seed-dependent flat plateau: `Stage_Hills2D`
(peak fraction swings 8–15 points across seeds and cannot express any fraction below the
atom), `Temperature` via the lapse rate, and hydrology, which must route flow across a
perfectly level surface.

Not a defect of `Stage_Hills2D`, whose classification is faithful to its contract, and not a
defect of `waterThreshold01`, which is a coordinate and never claimed to be a fraction.
Fix decided (2026-08-19) but not implemented: reformulate the perturbation so the core has
headroom. Breaks the `Height` golden and every descendant; re-anchoring is planned as its
own step.
```

### 3.3 Temporary probes — new block

**Insert** immediately after the block added in 3.2:

```markdown
## Temporary measurement probes (adapter-side, retained by decision 2026-08-19)

Two non-governed probes live on `PCGMapTilemapVisualization`, each with an Inspector button
on `PCGMapTilemapVisualizationEditor`:

- `LogVegetationNoiseHistogram()` (W-aux.d) — vegetation noise distribution across three
  populations plus a quantile-cut preview per biome.
- `LogHeightHistogram()` (W-aux.e) — `Height` distribution across three populations
  (all / `Land` / below the water threshold), saturation attribution by shaping step,
  the cuts that would realize a target `HillsL2` fraction, and a pow/spline sweep of `Land`
  drift at fixed `waterThreshold01`. Its self-check reports a mismatch count against the
  exported field and declares itself invalid rather than reporting untrustworthy numbers.

Both mirror private constants or arithmetic from the stages they measure and will drift
silently if those stages change; each carries a `TEMPORARY` header saying so. Retained
deliberately — the intent is to feed this data into graphed visualizations later — rather
than deleted after their originating batch. They read the last-built context: no rebuild,
no RNG, no core dependency.
```

---

## 4. `PCG_Roadmap.md` — window correction + next batch (MANDATORY)

Roadmaps are not implementation authority; these edits stop the roadmap from asserting a
target that measurement has shown to be unreachable, and register the follow-up batch.

### 4.1 Correct the Phase F3b hills-threshold description

**Locate**, in the Phase F3b section:

```
- **Visual smoke test:** Verify the heatmap overlay now visually matches hill placement.
  HillsL2 cells should correspond to the brightest Height values. Adjusting the thresholds
  should visibly move the hill/mountain boundary.
```

**Insert immediately after that block:**

```markdown
- **Measured limitation (W-aux.e, 2026-08-19) — the peak-fraction target is unreachable
  with the current `Height` field.** `hillsL1/L2` are fractions of the height *range*
  (N5.e remap), never fractions of land area, and the field they cut has a degenerate atom
  at 1.0 holding 3.7–11.7 % of `Land` depending on seed. Consequences, measured at three
  seeds: the realized peak fraction spans 17.6–33.1 % at fixed thresholds, and no threshold
  can express a peak fraction below the atom (~11.6 % at seeds 56 and 8). Any 15–30 % peak
  window is therefore not a specification the current stage can satisfy, and treating it as
  one will send the reader hunting for a bug in the classifier, which is correct. Revisit
  this target only after the `Height` saturation atom is removed.
```

### 4.2 Register the follow-up batch

**Locate** the status snapshot heading:

```
## Current status snapshot
```

**Insert immediately before it:**

```markdown
## Next batch — W-aux.f: de-saturating the `Height` ceiling

Decided 2026-08-19 on the evidence of the W-aux.e audit; design not yet done.
Reformulate the height perturbation in `Stage_BaseTerrain2D` so the island core has
headroom, removing the atom at `Height == 1.0`. Variant selection (normalize `raw` by its
theoretical maximum vs. recentre the core below 1.0) is the first open decision of that
batch. `BaseTerrainStage_Configurable` must change in the same step or the adapter and the
governed pipeline diverge silently — no test asserts their equality today.

Known cost, accepted under the standing priority stance: the `Height` golden and every
descendant break; re-anchoring is mechanical and is planned as its own ordered step.
`waterThreshold01` will need compensating (analytically, or by recalibration) because the
reformulation lowers heights, shrinking `Land` at a fixed threshold. Hills recalibration is
explicitly **not** part of that batch — it happens afterwards, against a measured healthy
field.
```

---

## 5. `map-pipeline-by-layers-ssot.md` — deliberately deferred, not forgotten

No edit proposed. The audit changed no contract: `Stage_Hills2D` and `Stage_BaseTerrain2D`
behave exactly as their F2 / F3b contracts describe, which is the finding. The saturation
atom is a measured *property of the field* rather than a contract term, and it is recorded
in `CURRENT_STATE.md` (§3.2) where implemented truth belongs.

The F2 contracts **will** need editing in the follow-up batch, when the height formula
actually changes. Writing a description of the current formula into the SSoT now, one batch
before replacing it, would create a stale paragraph and a second place to remember to fix.

---

## 6. Decisions recorded this session

| Decision | Value | Recorded where |
|---|---|---|
| Fix direction for the saturation atom | **Option A** — reformulate the perturbation so the core has headroom. Variant (A1 normalize vs A2 recentre) still open. | §2 changelog, §4.2 roadmap |
| Fate of the two temporary probes | **Retained**, deliberately, for future graphed visualizations. Not deleted with their batches. | §3.3 CURRENT_STATE |
| Hills recalibration | Deferred to after the field is fixed and re-measured. One rule per batch. | §4.2 roadmap |
| Doc updates from W-aux.c / W-aux.d / W-aux.e | Still queued, unapplied by user decision. | header of this file |

---

## 7. Files created and modified in the W-aux.e session

Modified (user-applied, evidence: stack traces in the pasted logs):

| Path | Change |
|---|---|
| `Runtime/PCG/Adapters/Tilemap/PCGMapTilemapVisualization.cs` | added `LogHeightHistogram()` + its bucket helper reuse; temporary, non-governed |
| `Editor/Inspectors/PCGMapTilemapVisualizationEditor.cs` | added `Log Height Histogram (TEMP)` button |

Created: this file.

Unchanged, and that is the batch result rather than an omission: every `Stage_*.cs`, every
core type, every tunable, every preset, every test, every golden.

---

## 8. What this file does NOT claim

- That the goldens are unchanged. They were **captured** this session
  (`Height` 196AF8D87C1D19EC / 58707DC13667740C / 8067C0763C948648); no prior value was
  available in-session for comparison. The probe cannot move them by construction — it only
  reads a built context — but that is an argument, not a measurement.
- That the atom's effect on `Temperature` or hydrology has been measured. It follows from
  the lapse-rate and flow-routing formulas; it was not instrumented.
- That option A's effect on `Land` (with `waterThreshold01` compensated) has been measured.
  It has not; that is the first measurement of the follow-up batch.
- That the W-aux.c / W-aux.d doc queues have been applied. They have not, as of 2026-08-19.
