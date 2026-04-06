# Noise Composition Improvements — Design Roadmap

Status: Planning only (not implementation authority)  
Authority: Planning reference for noise runtime and map pipeline composition extensions.  
Supplements the noise cross-reference analysis and integrates with the existing PCG Roadmap.  
Home: `Documentation~/planning/active/design/` (recommended)

---

## 1. Intent

The Islands noise runtime implements a strong primitive layer (SmallXXHash, Sample4 with
derivatives, Perlin/Simplex/Voronoi kernels, fractal accumulation) but the composition
layer — the operators that transform raw noise into terrain variety — has three identified
gaps. Cross-referencing six game worldgen pipelines (Minecraft, Dwarf Fortress, No Man's Sky,
RimWorld, Cataclysm:DDA, Tangledeep) against the PCG technique reports (1a, 1b, 2a, 6b)
surfaced three high-value techniques that Islands either lacks entirely or supports only
partially.

This roadmap sequences those three improvements from simplest to most complex, specifies
where each integrates with the existing noise runtime and map pipeline contracts, and
identifies which downstream phases consume each addition.

---

## 2. The Three Improvements

| # | Technique | Current Status | Impact | Complexity |
|---|-----------|---------------|--------|------------|
| N1 | Power Redistribution | **Done (Phase J2)** | Medium | Trivial |
| N2 | Spline Remapping | **Done (Phase N2)** | Very High | Low–Medium |
| N3 | Ridged Multifractal (full Musgrave) | Partial (Turbulence wrapper only) | High | Medium |

### Sequencing rationale

**N1 before N2 before N3.** N1 is already designed (Phase J2) and is a single-line math
transformation. N2 (spline remapping) is a small, self-contained operator that immediately
benefits every scalar field in the pipeline. N3 (ridged multifractal) requires changes to
the noise runtime's fractal accumulation or a new composition path, making it the most
architecturally involved.

N1 and N2 have no dependencies on each other or on N3. N3 has no dependency on N1 or N2.
All three can be implemented independently; the sequencing is based on impact-per-effort.

---

## 3. N1 — Power Redistribution

### Status: Done (Phase J2)

Implemented as part of Phase J2. The implementation chose inline post-processing inside
`Stage_BaseTerrain2D.Execute()` (not a separate stage), applied after height quantization
and before the Land threshold:

- New tunable: `MapTunables2D.heightRedistributionExponent` (default 1.0, clamped [0.5, 4.0])
- Applied as `pow(height01, exponent)` with a `!= 1.0f` guard for zero-cost default path
- Default 1.0 preserves all existing golden hashes (pow(x, 1) == x by identity)
- Exposed in `MapGenerationPreset` and all three visualization components' Inspectors
- Dwarf Fortress uses a "non-linear parabola" for the same purpose (confirmed by Tarn Adams)

**Side-effect cleanup:** The three duplicate `BaseTerrainStage_Configurable` nested classes
(one per visualization component) were consolidated into a single shared class at
`Runtime/PCG/Samples/Presets/BaseTerrainStage_Configurable.cs` in the
`Islands.PCG.Samples.Shared` asmdef. The consolidation threshold ("factor out when 3+
consumers exist") was met. `Islands.PCG.Samples.Shared.asmdef` gained `Unity.Mathematics`
and `Unity.Collections` references to support the extracted class.

### Open question (minor)

Phase J2 applies redistribution to Height only. The same operator could benefit Temperature
and Moisture in Phase M (e.g., redistributing moisture to cluster more values near 0 or 1,
producing sharper biome boundaries). This could be generalised as a `FieldRedistribution`
utility rather than a Height-specific stage. This is a low-stakes decision that can be
resolved at implementation time — the math is identical regardless of which field it targets.

---

## 4. N2 — Spline Remapping

### Status: Done (Phase N2)

Implemented as Phase N2. Design decisions resolved:

- `ScalarSpline` readonly struct at `Runtime/PCG/Fields/ScalarSpline.cs` (piecewise-linear,
  ~170 lines). Immutable, defensive-copy constructor, binary-search evaluation, `IsIdentity`
  fast-path guard, `Identity`/`PowerCurve`/`FromAnimationCurve` factories.
- `AnimationCurve` on `MapGenerationPreset` (`heightRemapCurve`), bridged to `ScalarSpline`
  on `MapTunables2D` (`heightRemapSpline`) via `FromAnimationCurve(curve, 16)` in `ToTunables()`.
- Coexists with J2 pow() redistribution (Option A: pow first, spline second).
  Application order: quantize → pow() → spline → Land threshold.
- Identity default preserves all existing golden hashes. `default(ScalarSpline)` has null
  arrays, `IsIdentity` returns true, `Evaluate()` returns input unchanged — zero-cost path.
- 22 new EditMode tests in `ScalarSplineTests.cs`.
- `PCGMapTilemapVisualization` refactored: priority table removed, scalar field overlay
  added (per-cell `Tilemap.SetColor` tint with auto-default ranges), custom Editor hides
  preset-controlled fields when a MapGenerationPreset is assigned.
- New `Islands.PCG.Editor` asmdef at `Editor/Inspectors/`.

### Why this matters

Spline remapping is the technique with the highest impact-to-complexity ratio that Islands
currently lacks. Minecraft's entire terrain variety depends on it: the same noise values
produce flat plains OR dramatic peaks depending on which spline curve reshapes them. Without
spline remapping, noise fields can only be transformed by linear scaling, power curves, or
smoothstep — none of which can produce the arbitrary, designer-tunable response curves that
make terrain interesting.

Every game with rich terrain variety uses either spline remapping (Minecraft), simulation
post-processing (Dwarf Fortress), or both. Islands does not plan simulation (it is not a
simulation engine), so spline remapping is the primary available path to terrain variety.

### What it is

A spline remap takes a scalar input value (typically [0, 1]) and maps it through a piecewise
curve to produce a reshaped output value. The curve is defined by a small number of control
points. Between control points, values are interpolated (linearly, or via cubic Hermite /
Catmull-Rom for smooth curves).

```
input:  0.0  0.2  0.4  0.6  0.8  1.0
output: 0.0  0.05 0.1  0.3  0.7  1.0   ← flattens lowlands, steepens peaks
```

This is strictly more general than power redistribution: `pow(x, exp)` is a single
fixed-shape curve, while a spline can produce any monotonic (or non-monotonic) mapping.

### Scope and placement

**This is a map pipeline composition operator, not a noise runtime primitive.**

The noise runtime's job is to produce raw scalar fields. Spline remapping operates on those
fields after they are sampled. It belongs in the map pipeline layer — alongside the existing
threshold, mask, and morphology operators — not inside `Noise.GetFractalNoise<N>()`.

Concretely, a spline remap is consumed by map stages that want to reshape a field before
using it. It is the same category of operation as `pow(height, exponent)` (Phase J2) but
more general.

### Downstream consumers

| Phase | How spline remapping is consumed |
|-------|--------------------------------|
| J2 (Height Redistribution) | Coexists: pow() is the "quick knob," spline is "full control." Pow first, spline second. |
| M (Climate & Biome) | Temperature and Moisture splines produce naturalistic climate gradients; reshape noise perturbation curves |
| W (World-to-Local) | Local elevation reinflation: world tile's hilliness selects a spline that reshapes local noise into appropriate terrain character |
| General | Any future scalar field benefits from having an optional spline remap |

### Invariants

- `ScalarSpline` with all control points mapping input to identical output (identity spline)
  must produce mathematically identical results to no remapping. This preserves existing
  golden hashes when splines are defaulted.
- Evaluation must be deterministic — no floating-point order-of-operations variance.
- No RNG consumption. Pure function of input value and control points.
- No new `MapLayerId` or `MapFieldId`.

---

## 5. N3 — Ridged Multifractal (Full Musgrave Variant)

### Why this matters

Ridged multifractal is the only noise composition technique that directly produces
mountain-ridge terrain with heterogeneous roughness — jagged ridgelines with fine detail
separated by smooth valley floors. Standard fBm cannot produce this; it creates uniform
roughness everywhere. RimWorld blends Perlin (continental shape) with RidgedMultifractal
(mountain ridges) for world elevation. This is directly relevant to Islands' Phase K
(plate tectonics / mountain ranges) and Phase W (local map elevation reinflation).

### What Islands currently has

The noise runtime provides:

- `Turbulence` gradient wrapper: applies `abs()` to noise value. This gives the `|noise|`
  building block but does NOT implement the full ridged multifractal algorithm.
- `Noise.GetFractalNoise<N>()`: implements standard fBm octave summation with geometric
  amplitude decay. Does not support inter-octave feedback (signal-dependent amplitude).

The full Musgrave ridged multifractal requires TWO things that standard fBm lacks:

1. **Signal transformation per octave:** `offset - abs(noise(p))`, squared
2. **Inter-octave feedback:** each octave's amplitude is weighted by the previous octave's
   signal value: `weight = clamp(signal * gain, 0, 1)`. This is the key differentiator —
   it produces heterogeneous terrain where rough areas become progressively rougher.

### Design decision: where to implement

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **A — New fractal mode in noise runtime** | Add a `RidgedMultifractal` path alongside the existing fBm in `Noise.GetFractalNoise<N>()` | Clean API; SIMD-friendly; reusable across all consumers; matches the existing generic noise pattern | Modifies governed reference surface; needs careful design to avoid disrupting existing `Noise.Settings` / `INoise` contracts |
| **B — Composition in map stage code** | Implement ridged multifractal as a loop in a map stage, calling single-octave noise evaluations and managing feedback manually | No noise runtime changes; purely additive; simpler review | Duplicates fractal logic outside the noise runtime; harder to SIMD; not reusable for non-map consumers |
| **C — New standalone operator** | Create a `RidgedMultifractalOps` utility that wraps the noise runtime's single-octave evaluation | Middle ground; additive; reusable; does not modify noise runtime internals | Slightly awkward API (wrapping rather than extending) |

**Recommendation:** Option A if the noise runtime's contracts can absorb it cleanly. The
existing `Noise.Settings` already carries `octaves`, `lacunarity`, `persistence` — adding
an enum for fractal mode (`Standard`, `Ridged`) plus two additional parameters (`offset`,
`gain`) is a natural extension. The `GetFractalNoise<N>()` implementation would branch on
the mode enum and use a different accumulation loop for ridged.

If Option A risks disrupting the noise runtime's governed reference status, Option C is the
safe fallback — it wraps the runtime without modifying it.

**This is an unresolved design decision.** It should be resolved when the technique is
needed (Phase K or Phase W), not preemptively.

### Musgrave algorithm (implementation reference)

From PCG Technique Report 1b, verified against Musgrave's `musgrave.c`:

```
function RidgedMultifractal(point, H, lacunarity, octaves, offset, gain):
    // Precompute spectral weights
    for i in 0..octaves-1:
        exponent_array[i] = pow(lacunarity, -H * i)

    // First octave (no feedback)
    signal = offset - abs(noise(point))
    signal = signal * signal
    result = signal
    weight = 1.0

    // Subsequent octaves with feedback
    frequency = lacunarity
    for i in 1..octaves-1:
        point = point * lacunarity
        weight = clamp(signal * gain, 0, 1)
        signal = offset - abs(noise(point))
        signal = signal * signal
        signal = signal * weight
        result = result + signal * exponent_array[i]

    return result
```

**Canonical parameters (Musgrave):** H=1.0, offset=1.0, gain=2.0, lacunarity=2.0.

### Parameters to expose

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `fractalMode` | enum | `Standard` | `Standard` = existing fBm; `Ridged` = Musgrave ridged |
| `offset` | float | 1.0 | Only used when mode=Ridged. Controls ridge sharpness. |
| `ridgedGain` | float | 2.0 | Only used when mode=Ridged. Controls heterogeneity feedback. Named `ridgedGain` to avoid collision with existing `persistence`/`gain` terminology. |

`H` (Hurst exponent) is already derivable from `persistence` in the existing `Noise.Settings`:
`H = -log2(persistence)`. No new parameter needed for H.

### Integration with the map pipeline

Ridged multifractal is consumed through the same `MapNoiseBridge2D` path as standard fBm.
The bridge would accept an extended settings struct (or a new `NoiseCompositionSettings`)
that includes the fractal mode and ridged-specific parameters.

**Primary consumer pattern (RimWorld-style blended elevation):**
```
elevation = lerp(
    fbm_perlin(point, settings_lowlands),
    ridged_multifractal(point, settings_mountains),
    control_noise(point, settings_selector)
)
```

This is scalar field composition (technique 2.7 from the cross-reference). The control
noise selects whether each region uses the smooth fBm path or the ridged mountain path.
RimWorld does exactly this: `Blend(Perlin, RidgedMulti, controlPerlin)`.

### Downstream consumers

| Phase | How ridged multifractal is consumed |
|-------|-------------------------------------|
| K (Plate Tectonics) | Mountain ranges at plate collision zones. Ridged multifractal is the canonical technique for this. |
| W (World-to-Local) | Local elevation reinflation blending smooth lowlands with ridged mountains, gated by world tile's hilliness. Directly matches RimWorld's pattern. |
| General (Height) | Even without K/W, blending ridged noise into the Height field improves mountain quality in the current single-island pipeline. |

### Invariants

- `fractalMode = Standard` must produce identical output to the current `GetFractalNoise<N>()`
  implementation. Existing golden hashes must not change.
- Ridged mode output must be deterministic for the same inputs.
- SIMD lane consistency: all 4 lanes must evaluate the same fractal mode (no per-lane branching
  on mode). This is naturally satisfied since mode is a per-evaluation setting, not per-sample.

### Test plan

- Standard mode produces identical output to pre-change implementation (golden hash parity).
- Ridged mode with canonical Musgrave parameters (H=1.0, offset=1.0, gain=2.0) produces
  expected visual character: sharp ridges, heterogeneous detail.
- Ridged mode is deterministic: same seed + settings → identical output.
- Edge cases: offset=0 (degenerate), gain=0 (no feedback, reduces to turbulence-like).

### Estimated scope

Medium. The algorithm itself is straightforward (~30 lines of accumulation loop). The
integration work is in deciding where it lives (noise runtime vs. standalone operator),
extending `Noise.Settings` or creating a parallel settings struct, and ensuring SIMD
compatibility. Estimated: 1–2 focused sessions.

---

## 6. Sequencing Summary

```
DONE
  └─ N1: Power Redistribution ──── Phase J2 (implemented)
                                    Inline pow() in Stage_BaseTerrain2D + shared
                                    BaseTerrainStage_Configurable for visualizations.

DONE
  └─ N2: Spline Remapping ──────── Phase N2 (implemented)
                                    ScalarSpline utility + MapTunables2D + AnimationCurve
                                    bridge. Coexists with pow() (Option A).

LATER (new, medium complexity)
  └─ N3: Ridged Multifractal ───── Noise runtime extension or standalone operator.
                                    Natural trigger: when Phase K or Phase W begins.
                                    Design decision (A/B/C) resolved at that time.
```

### Dependency map

```
N1 (Phase J2)  ──→  DONE. Consumed by: Height field.
                     Optionally extendable to Temperature/Moisture (Phase M).

N2 (Phase N2)  ──→  DONE. Consumed by: Height field (initial).
                     Future: Temperature, Moisture, any scalar field.
                     Coexists with N1 (pow first, spline second).

N3 (Ridged)    ──→  consumed by: Phase K (mountains), Phase W (local elevation)
                     no dependency on N1 or N2
                     benefits from N2 (spline remap of ridged output)
```

N1 and N2 are done. N3 is the only remaining item — deferred to Phase K or Phase W.

---

## 7. What This Roadmap Does NOT Cover

- **Domain warping improvements:** Islands already has simple warp (Phase F2b). Full
  Quilez-style double warping is valuable but is a composition pattern, not a new
  primitive. It can be implemented in stage code using existing noise evaluation.
  Not urgent enough to warrant a dedicated roadmap item.

- **Noise competition (per-type highest-wins):** Useful for sub-biome variation
  (RimWorld's stone-type pattern). This is a map pipeline composition technique,
  not a noise runtime change. Worth noting for Phase M2 but not scoped here.

- **Data-driven noise configuration:** Important for toolkit/modding long-term.
  Not critical until the pipeline is feature-complete. Out of scope here.

- **Derivative-damped fBm / heterogeneous terrain fBm:** Advanced fBm variants.
  Islands' `Sample4` carries derivatives, making these feasible. But standard fBm +
  spline remapping covers most needs. Deferred.
