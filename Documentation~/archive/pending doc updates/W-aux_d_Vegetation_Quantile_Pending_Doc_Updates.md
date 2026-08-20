# W-aux.d — Vegetation quantile mapping: pending doc updates

Status: **APPLIED — 2026-08-20.** 10 of 11 items applied; 1 superseded.
Superseded: **§7.2** (next batch = threshold-mapping audit) by the closure of W-aux.e.
Corrected at application time: **§6** claimed `coverage-matrix.md` needed no change because
it maps concepts to owners. The file also carries a "Test coverage and known gaps" section,
so the gates and gaps from `W-aux_c_Blocks_2_3` §2 landed there.
Note: both vegetation goldens recorded in §2.2 and §3 were re-anchored **again** by W-aux.f
(`Height` value change); the governed documents carry the post-W-aux.f values and the full
chain. §9 (tension with project instructions) was recorded, not acted on — project
instructions are user-owned.
Targets written: `map-pipeline-by-layers-ssot.md`, `changelog-ssot.md`, `CURRENT_STATE.md`,
`PCG_Roadmap.md`.
Applied by: Islands.PCG documentation-application session, 2026-08-20. See
`Application_Ledger_2026-08-20.md`.
This file is consumed input and is **not authority**.
Date: 2026-08-19
Batch label `W-aux.d` is proposed, not authoritative — it follows the `W-aux.c` lineage
because that batch produced the measured evidence that opened this one. Rename freely.

---

## 0. What this batch changed, in one paragraph

`Stage_Vegetation2D` used `noise01 >= 1 - vegetationDensity` as its per-cell acceptance
rule. That mapping treated the fBm sample as if it were uniformly distributed over
`[0, 1]`. It is not: measured over the eligible population at three seeds, the field
occupies roughly `[0.28, 0.77]` with mean ~0.51 and ~71% of its mass inside a 0.20-wide
window. Thresholds above ~0.77 accepted **zero** cells at every seed tested, which is why
Tundra (threshold 0.95) and TemperateDesert (0.95) never vegetated and Shrubland (0.75)
caught only a 32-cell sliver. The batch replaces the absolute threshold with a **global
quantile cut** computed over the eligible population, leaving the noise field, its salt,
frequency, octaves and quantization untouched. Spatial character is unchanged; only the
cut point moves.

---

## 1. Evidence status — all closed

Verified in session 2026-08-19:

- **Probe histograms at three seeds** (56, 8, 243; res 256), pasted by the user.
  Eligible-population support: `[0.2861, 0.7568]` / `[0.2832, 0.7676]` / `[0.2939,
  0.7451]`; means 0.5056 / 0.5250 / 0.5159. No mass in buckets 0 or 1023 at any seed.
- **Post-change `Log Map Stats` at three seeds**, pasted by the user. Seed 56 matches the
  pre-change prediction exactly: `Vegetation` 4127, `pctOfEligible` 19.88,
  `eligible` 20757.
- **Compilation clean and EditMode Run All green** — confirmed by the user, twice
  (before and after the `ref`-locals fix in the M2a-9 gate).
- **New M2a golden re-anchored:** `0x6AB1192251196B17`. Legacy golden
  `0xE7876A1519EC45D3` unchanged, as intended.
- **Visual smoke test performed and passed** — user-confirmed clear improvement.

One reporting discrepancy, resolved, worth recording so nobody chases it:
the probe's `pctOfBiome` divides by the biome's **eligible** cells; the exporter's
`vegetationByBiome.pctOfBiome` divides by **all** cells of that biome. TemperateDesert
seed 56: 481/3750 = 12.83% (probe) vs 481/4288 = 11.22% (exporter). Both correct, different
denominators. Biomes with zero peak-blocked cells agree exactly (Tundra 2.18%,
Shrubland 24.90%).

---

## 2. `map-pipeline-by-layers-ssot.md` — F5 contracts (MANDATORY)

### 2.1 Replace the acceptance-mapping bullet

**Locate**, in `### F5 vegetation contracts (M2.a; M2a-3 reformulated W-aux.c block 3)`:

```
- per-cell acceptance threshold is `1 - BiomeDef.vegetationDensity` compared against
  the stage's fBm sample. **This mapping is not a per-cell probability**: measured
  coverage is a step, not a line (density 0.85 → 100%, 0.65 → 95.5%, 0.60 → 91.8%,
  0.25 → 0.26%, 0.05 → 0%; seed 56, res 256, 2026-08-19). Tests must not assume
  coverage is proportional to density.
```

**Replace with:**

```
- M2a-9 (reformulated, W-aux.d — global quantile cut). Per-cell acceptance on the
  biome path is a quantile of the vegetation noise field, not an absolute threshold.
  Let `E` be the eligible population (`LandInterior` ∧ valid biome sentinel ∧
  `vegetationDensity > 0` ∧ not blocked by the peak policy) and let
  `bucket(c) = clamp(floor(noise01(c) * QuantSteps), 0, QuantSteps-1)` with
  `QuantSteps = 1024`. For density `d`, `cut(d)` is the largest bucket `k` such that
  `|{ c ∈ E : bucket(c) >= k }| >= ceil(d * |E|)`.
  - (a) **Exactness.** `c ∈ E` is vegetated ⟺ `bucket(c) >= cut(density(c))`.
  - (b) **Nesting.** `d1 > d2 ⟹ cut(d1) <= cut(d2)`, so accepted sets are nested over
    `E`. This is a deterministic set-inclusion property, replacing the former
    statistical "denser biomes cover more" formulation, which held by accident while
    the mapping was broken.
  - (c) **Floor.** Realized coverage over `E` is `>= d`, never below. Whole buckets are
    accepted, so the excess is bounded by the **population share of the cut bucket**,
    not by `1/QuantSteps`. Measured at seeds 56/8/243, res 256, 2026-08-19: realized
    coverage per nominal density is 5.00–5.04, 15.05–15.17, 25.24–25.30, 60.16–60.38,
    65.08–65.29, 85.16–85.22. Max excess +0.38 pp.
  - (d) **NOT guaranteed: per-biome coverage equal to `d`.** Biomes are cut against the
    global distribution, so biome placement that correlates with the noise field moves
    per-biome coverage away from nominal in either direction. Demonstrated: Tundra and
    TemperateDesert share `d = 0.05`, and their realized coverage differs by 3–5× with
    the ordering flipping by seed — seed 56 gives Desert 11.22% vs Tundra 2.18%, seed
    243 gives Desert 3.16% vs Tundra 10.48%, on populations of thousands of cells.
    Do not read a per-biome percentage as a calibration error without checking the
    biome's population size first: biomes with a handful of cells (Grassland: 12, 15
    and 0 cells at the three seeds tested) report 0.00% for ordinary small-sample
    reasons, not because of this clause.
  - (e) **COUPLING — contract surface, not an implementation detail.** `cut(d)` is a
    function of `E`. Any change to the eligibility policy — including flipping
    `vegetatesOnPeaks` on a single biome, or changing one biome's density to or from
    zero — shifts the threshold of **every** biome. A single-biome edit is expected to
    produce a whole-map diff. This is by design.
  - Legacy path (Biome field absent): absolute threshold `0.40`, no quantile, no
    coupling. Preserved as the fallback witness; it is a separate early-out in the
    stage, not a branch inside the main loop, so the two semantics cannot drift.
- the measured distribution that motivates the above: over `E` the field occupies
  `[0.2861, 0.7568]` / `[0.2832, 0.7676]` / `[0.2939, 0.7451]` at seeds 56 / 8 / 243
  (res 256), means 0.5056 / 0.5250 / 0.5159, with ~71% of mass inside a 0.20-wide
  window. Any absolute threshold above ~0.77 accepts zero cells at every seed tested,
  regardless of biome or terrain. The shape is stable across seeds; this was not a
  seed-56 peculiarity.
- aggregate stability after the change (res 256): `Vegetation` is 6.30% / 6.22% / 6.88%
  of the map at seeds 56 / 8 / 243. The quantile fixes the aggregate by construction;
  a large swing here would indicate the histogram is being built over the wrong
  population.
- the noise field itself is unchanged by W-aux.d: same salt `0xB7C2F1A4`, frequency 4,
  3 octaves, lacunarity 2, persistence 0.5, `quantSteps` 1024. Sample consumption is
  one `FillSimplexPerlin01` call per `Execute`, as before.
- cost profile: two passes over the domain (histogram, then apply) plus
  `O(BiomeType.COUNT * QuantSteps)` for the cuts. Previously one pass.
- **reporting note.** `MapStatsExporter2D.vegetationByBiome.pctOfBiome` divides by all
  cells of the biome, including cells blocked by the peak policy. It is therefore not
  directly comparable to `d`, nor to a per-biome figure computed over the eligible
  population. Compare like with like.
```

Also update the section heading:

**Locate:** `### F5 vegetation contracts (M2.a; M2a-3 reformulated W-aux.c block 3)`
**Replace with:** `### F5 vegetation contracts (M2.a; M2a-3 reformulated W-aux.c block 3; M2a-9 reformulated W-aux.d)`

### 2.2 Test-gated behavior section

**Locate:**

```
- F5 vegetation goldens (StageVegetation2DTests.cs, 64×64, seed 12345):
  legacy path `0xE7876A1519EC45D3` (unchanged since M2.a);
  biome path `0x5B1DB3468075FFDC` (**re-anchored W-aux.c block 3**, was
  `0x41BB2F99C2BE043D`). The re-anchor is the intended consequence of the M2a-3
  reformulation, not a regression.
- M2a-3 is asserted in two forms by `AssertSubsetInvariants(ref ctx, globalHillsL2Exclusion)`:
  strict emptiness on the legacy path, per-biome `vegetatesOnPeaks` on the biome path.
```

**Replace with:**

```
- F5 vegetation goldens (StageVegetation2DTests.cs, 64×64, seed 12345):
  legacy path `0xE7876A1519EC45D3` (unchanged since M2.a — the legacy path was touched
  by neither W-aux.c block 3 nor W-aux.d, and this golden holding green is the evidence
  of that);
  biome path `0x6AB1192251196B17` (**re-anchored W-aux.d**, was `0x5B1DB3468075FFDC`,
  before that `0x41BB2F99C2BE043D`). The re-anchor is the intended consequence of the
  M2a-9 reformulation, not a regression.
- M2a-3 is asserted in two forms by `AssertSubsetInvariants(ref ctx, globalHillsL2Exclusion)`:
  strict emptiness on the legacy path, per-biome `vegetatesOnPeaks` on the biome path.
- M2a-9 is asserted by `M2a_QuantileCut_IsExact_Nested_AndAboveNominal`, which replaces
  `M2a_CoverageMonotonicity_DenseBiomesExceedSparseBiomes`. The old test compared two
  biome pairs with a ≥8-cell tolerance and passed throughout the period the mapping was
  broken; it is removed rather than relaxed. The new gate recomputes the noise field
  from `Stage_Vegetation2D`'s public constants, rebuilds the eligible histogram and the
  cut independently of the stage, and compares **cell by cell**, then checks nesting
  (b) and the coverage floor (c). Deducing the cut from the lowest vegetated bucket
  would be unsound — a biome with no cell sitting exactly at the cut reports a cut above
  the real one — which is why the gate reimplements the rule instead of observing it.
```

---

## 3. `changelog-ssot.md`

Insert as the newest entry, above `## W-aux.b`:

```
## W-aux.d — Vegetation quantile mapping
Date: 2026-08-19

**Golden re-anchor, intended.** `Stage_Vegetation2D` replaces the absolute acceptance
threshold `1 - vegetationDensity` with a global quantile cut over the eligible
population. The noise field is untouched (same salt, frequency, octaves, lacunarity,
persistence, quantization); only the cut point moves, so spatial character is preserved.

Cause, measured 2026-08-19 at seeds 56 / 8 / 243, res 256 (temporary probe): the fBm
field occupies roughly `[0.28, 0.77]` over the eligible population with ~71% of its mass
in a 0.20-wide window, at every seed tested. Absolute thresholds above ~0.77 accepted
zero cells, so low-density biomes were unreachable by construction — not
under-vegetated, impossible. This also explains why W-aux.c block 3 unlocked 6120 peak
cells (5642 Tundra) and produced zero additional Tundra vegetation: the failure was in
value mapping, not spatial grain.

| golden (64×64, seed 12345) | before | after |
|---|---|---|
| `Vegetation` legacy path | `0xE7876A1519EC45D3` | `0xE7876A1519EC45D3` (unchanged) |
| `Vegetation` biome path | `0x5B1DB3468075FFDC` | `0x6AB1192251196B17` |

Measured effect, res 256, before → after (before figures are seed 56 only; the
pre-change pipeline was not re-run at seeds 8 and 243):

| | seed 56 | seed 8 | seed 243 |
|---|---|---|---|
| `Vegetation` count | 1893 → 4127 | — → 4079 | — → 4506 |
| `Vegetation` % of map | 2.89 → 6.30 | — → 6.22 | — → 6.88 |
| `pctOfEligible` | 9.12 → 19.88 | — → 20.17 | — → 25.74 |
| Tundra `pctOfBiome` | 0.00 → 2.18 | — → 7.39 | — → 10.48 |
| Shrubland `pctOfBiome` | 0.26 → 24.90 | — → 21.98 | — → 24.09 |
| biomes with vegetation | 4 → 6 | — → 5 | — → 6 |

Tundra moving from a hard zero at every seed to a seed-dependent 2–10% is the headline:
the biome now responds to generation instead of being unreachable.

`M2a_CoverageMonotonicity_DenseBiomesExceedSparseBiomes` removed; replaced by
`M2a_QuantileCut_IsExact_Nested_AndAboveNominal`. Contract M2a-9 reformulated in the
stage header and in `map-pipeline-by-layers-ssot.md`.

`BiomeTable` densities were deliberately **not** recalibrated in this batch: the
quantile mapping changes what the numbers mean, and recalibration should follow
measurement, not precede it.
```

---

## 4. `CURRENT_STATE.md`

Append `W-aux.d` to the implemented-slice enumeration wherever `W-aux.c` currently
terminates it, and add:

```
Stage_Vegetation2D accepts cells by a global quantile cut over the eligible population
(W-aux.d). Biome `vegetationDensity` values now mean what their names say — a fraction
of the eligible population — with the per-biome caveat in M2a-9(d). Densities have not
been recalibrated since the mapping changed. Measured aggregate: `Vegetation` is
6.2–6.9% of the map at res 256 across seeds 56 / 8 / 243.
```

---

## 5. `W-aux_c_Blocks_2_3_Pending_Doc_Updates.md`

Append, so the recalibration batch inherits measured inputs rather than reopening this:

```
### Superseded by W-aux.d (2026-08-19)

The density→coverage step function recorded here is explained and resolved: it was the
cumulative of a narrow unimodal noise distribution read at five points, two of them
outside the field's support. The spatial-grain hypothesis is refuted by measurement and
must not be re-derived.

Inputs handed to the future `BiomeTable` recalibration batch, measured at seeds
56 / 8 / 243, res 256:

- **Same density, different outcome.** Tundra and TemperateDesert both sit at `d = 0.05`
  and realize 2.18 / 11.22 (seed 56), 7.39 / 4.39 (seed 8), 10.48 / 3.16 (seed 243).
  The ordering flips with the seed on populations of thousands of cells. This is
  M2a-9(d) in action and is the real calibration question: densities are being applied
  to biomes whose placement correlates with the noise field.
- **Two biomes are effectively absent from this preset, which is a separate problem from
  calibration.** Grassland has 12 / 15 / 0 cells and TemperateRainforest 11 / 0 / 6
  across the three seeds. Their 0.00% readings are small-sample arithmetic, not
  starvation by the quantile cut. Recalibrating their densities would change nothing.
  The question they raise belongs to `Stage_Biome2D` and the temperature/moisture bands,
  not to `Stage_Vegetation2D`: `biomesNonZero` is 10 / 8 / 8 out of 13, and
  SubtropicalDesert, TropicalSeasonalForest and TropicalRainforest are at zero in all
  three. An earlier draft of this document attributed Grassland's zero to the quantile
  cut; that attribution was wrong and is corrected here.
- `hillsL2_fraction` 0.65 → `HillsL2` = 30.07% / 33.67% / 25.42% of `Land` at the three
  seeds, against a declared 15–30% window. **Still open**, and now measured to swing 8
  points with the seed under fixed thresholds — see the Hills/BaseTerrain threshold audit.
```

---

## 6. `coverage-matrix.md` — NO CHANGE

Checked this session: `coverage-matrix.md` maps *concepts to their documentary owner*,
not tests to their gates. Nothing in it changes. The test-coverage delta belongs in
`map-pipeline-by-layers-ssot.md` §2.2 above. Recorded here so the next session does not
re-derive that this file was skipped by oversight.

---

## 7. `PCG_Roadmap.md` — prioritisation posture, next batch, parked items

Roadmaps are planning, not implementation authority.

### 7.1 Prioritisation posture (new section, near the top)

```
## Prioritisation posture (stated 2026-08-19)

Islands.PCG is in design and implementation. It is not in production and no shipped
content depends on any particular map. Therefore:

- **Pipeline quality, expressiveness and interesting output take priority over the
  stability of previously generated maps.** "This breaks the goldens" is not, by
  itself, an argument against a change. A golden is a change detector, not a desirable
  property: it reports that something moved and leaves the judgement to us. Re-anchoring
  goldens is mechanical work and is costed as such.
- **This does not relax determinism.** Same seed + same tunables + same version ⇒ same
  map remains a hard invariant. The two are often confused and are opposites in effect:
  determinism is precisely what makes breaking old maps cheap. Without it you cannot
  reproduce a bug you saw, cannot compare two configurations, and cannot measure whether
  a change improved anything. The W-aux.d diagnosis depended entirely on being able to
  re-derive one specific field from one specific seed, and its validation depended on
  re-running the same pipeline at three.
- **Proposals must state which kind of objection they are answering.** When an option is
  rejected, the rejection is labelled either *expressiveness* (a design judgement, open
  to argument) or *re-anchoring cost* (no longer a valid reason on its own). Example
  from W-aux.d: per-biome quantiles were rejected on expressiveness — they destroy the
  contrast between biomes, so a forest would stop reading as greener than a tundra —
  not because of their golden impact.
- Scope discipline still applies. This posture licenses *ambitious* batches, not *wide*
  ones. One problem per batch remains the rule.
```

### 7.2 Next batch proposal

```
Next: threshold-mapping audit of `Stage_Hills2D` and `Stage_BaseTerrain2D`.

W-aux.d found that an absolute threshold compared against a non-uniform field is not a
fraction. Both remaining threshold consumers have the same shape:
`Stage_Hills2D` classifies on `Height >= hillsThresholdL1/L2`, and
`Stage_BaseTerrain2D` sets `Land = Height >= waterThreshold01`. Partial evidence is
already in hand from the W-aux.d verification runs (res 256, seeds 56 / 8 / 243, fixed
tunables): `HillsL2` is 30.07% / 33.67% / 25.42% of `Land` against a declared 15–30%
window, while `Land` itself is a much steadier 33.87% / 34.16% / 30.50% of the map. The
two stages may well earn different verdicts.

Not a foregone conclusion: unlike the vegetation noise field, `Height` passes through
deliberate shaping (quantization, pow redistribution, spline remap, island mask) whose
whole purpose is to control its distribution. An absolute threshold on a deliberately
shaped field may be a feature. The audit's job is to measure and decide, not to apply
the vegetation fix by analogy.
```

### 7.3 Parked items (add or extend)

```
- **Per-layer inspection overlay.** Observed during the W-aux.d smoke test: there is no
  clean way to display a single mask layer (e.g. `Vegetation`) in isolation over the
  runtime map. Layer isolation is step 2 of the standing visual smoke test protocol, so
  the protocol currently asks for something the tooling does not make easy. Adapter-side
  work only; touches `ScalarOverlayRenderer` / `PCGRuntimeOverlay` /
  `PCGMapCompositeVisualization`. Small, and it pays for itself on every future stage.
- **Biome coverage.** `biomesNonZero` is 10 / 8 / 8 of 13 at seeds 56 / 8 / 243, with
  SubtropicalDesert, TropicalSeasonalForest and TropicalRainforest at zero in all three,
  and Grassland and TemperateRainforest in the single or double digits of cells. Surfaced
  by W-aux.d verification; belongs to `Stage_Biome2D` and its temperature/moisture bands,
  not to vegetation. Not scheduled.
```

---

## 8. Temporary instrumentation — decide before closing

`LogVegetationNoiseHistogram()` and `VegProbeCutBucket()` in
`PCGMapTilemapVisualization.cs`, plus the `Log Vegetation Noise Histogram (TEMP)` button
in `PCGMapTilemapVisualizationEditor.cs`, were added as throwaway probes. They duplicate
`Stage_Vegetation2D`'s noise constants by value, which is now unnecessary — those
constants are `public const`. Three options, user's call:

1. **Delete.** Cleanest. The batch is closed and the measurement is recorded.
2. **Keep as-is.** Useful for the recalibration batch, but the duplicated literals will
   drift eventually.
3. **Keep and de-duplicate** — point the probe at `Stage_Vegetation2D.NoiseSeedSalt` and
   friends. Cheap, and turns a throwaway into a permanent diagnostic.

Recommendation: (3). The same probe shape is about to be needed for `Height` in the
threshold audit, and a generalized version is worth more than a deleted one.

---

## 9. Recorded, not proposed as an edit: project instructions

The prioritisation posture in §7.1 sits in tension with the standing project rule
"prefer concrete, conservative, traceable decisions and small precise changes". It does
not replace it — small and traceable still wins — but it removes one specific
conservative reflex: avoiding a promising change because it invalidates existing
generated output. The project instructions are user-owned; no edit is proposed here.
The tension is recorded so a future session finds it stated rather than inferring a
contradiction.
