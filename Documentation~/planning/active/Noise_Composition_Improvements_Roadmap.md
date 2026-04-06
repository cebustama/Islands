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
| N1 | Power Redistribution | **Already planned as Phase J2** | Medium | Trivial |
| N2 | Spline Remapping | Not implemented | Very High | Low–Medium |
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

### Status: Already planned as Phase J2

Phase J2 in the PCG Roadmap already fully specifies this improvement:

- New tunable: `MapTunables2D.heightRedistributionExponent` (default 1.0, clamped [0.5, 4.0])
- Applied as `pow(height01, exponent)` after the Height field is written
- Default 1.0 preserves existing goldens; exponents > 1.0 flatten lowlands and sharpen peaks
- Dwarf Fortress uses a "non-linear parabola" for the same purpose (confirmed by Tarn Adams)
- Implementation: inline post-processing step or new `Stage_HeightRedistribution2D`
- Can be implemented any time after current H-series

**No new design work needed.** Phase J2 is complete as specified. This document references
it for completeness and to confirm it covers the technique identified in the cross-reference.

### Open question (minor)

Phase J2 applies redistribution to Height only. The same operator could benefit Temperature
and Moisture in Phase M (e.g., redistributing moisture to cluster more values near 0 or 1,
producing sharper biome boundaries). This could be generalised as a `FieldRedistribution`
utility rather than a Height-specific stage. This is a low-stakes decision that can be
resolved at implementation time — the math is identical regardless of which field it targets.

---

## 4. N2 — Spline Remapping

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

### Proposed data structure

```csharp
/// <summary>
/// Piecewise-linear or Catmull-Rom spline for scalar field remapping.
/// Immutable after construction. Deterministic evaluation.
/// </summary>
public readonly struct ScalarSpline
{
    // Control points sorted by input value. Minimum 2 points.
    // First point's input should be 0 (or the field's minimum).
    // Last point's input should be 1 (or the field's maximum).
    private readonly float[] inputs;   // sorted ascending
    private readonly float[] outputs;  // corresponding output values

    /// <summary>
    /// Evaluate the spline at a given input value.
    /// Values below the first control point clamp to the first output.
    /// Values above the last control point clamp to the last output.
    /// </summary>
    public float Evaluate(float t) { /* piecewise lerp or Catmull-Rom */ }
}
```

**Design decision — linear vs. cubic interpolation:**

| Option | Pros | Cons |
|--------|------|------|
| Piecewise linear | Simple, fast, no overshoot, predictable | Visible kinks at control points; needs more points for smooth curves |
| Catmull-Rom cubic | Smooth C1 curves from few points; fewer control points needed | Potential overshoot; slightly more complex; tangent computation |

**Recommendation:** Start with piecewise linear. It is trivially correct, deterministic,
and sufficient for the initial use cases (height redistribution, climate field shaping).
Catmull-Rom can be added as a second interpolation mode later if smooth curves are needed.
RimWorld uses piecewise linear (`SimpleCurve`) for all its remapping; Minecraft uses cubic
splines. Either works.

### Integration points

**As a field post-processor (primary use):**
```csharp
// In a map stage, after writing a field:
ScalarSpline heightSpline = tunables.heightSpline; // from MapTunables2D or preset
for (int i = 0; i < field.Length; i++)
    field[i] = heightSpline.Evaluate(field[i]);
```

**As a MapTunables2D / MapGenerationPreset field:**
```csharp
// MapTunables2D extended with optional spline overrides:
public ScalarSpline heightSpline;       // default = identity (no remapping)
public ScalarSpline temperatureSpline;  // Phase M
public ScalarSpline moistureSpline;     // Phase M
```

**As a standalone utility (no new stage needed):**
The spline does not need its own pipeline stage. It is an operator that stages invoke
when they want to reshape their output. This is analogous to how `MaskMorphologyOps2D`
provides erosion/dilation operators that stages call — not a stage itself.

### Downstream consumers

| Phase | How spline remapping is consumed |
|-------|--------------------------------|
| J2 (Height Redistribution) | `ScalarSpline` subsumes `pow(h, exp)` — J2 could use a spline instead of a power curve, or both could coexist as separate tunables |
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

### Relationship to Phase J2

Phase J2 proposes `pow(height, exponent)` as a single-parameter height redistribution.
`ScalarSpline` is strictly more general. Two options:

**Option A — Coexist:** Keep J2's power-curve tunable AND add a spline tunable. The power
curve is the "quick knob" (one number); the spline is the "full control" (multiple points).
If both are specified, apply power curve first, then spline (or vice versa — resolve at
implementation time).

**Option B — Subsume:** Replace J2's power-curve with a spline. Provide preset spline
constructors: `ScalarSpline.PowerCurve(exponent)` generates the equivalent control points.
J2 becomes "use the spline with a power-curve preset."

**Recommendation:** Option A for initial implementation (simpler, preserves J2 as designed),
with the understanding that the spline is the more general tool.

### Test plan

- Identity spline preserves input values exactly (float equality).
- Known control points produce expected outputs at control point inputs.
- Interpolated values between control points are correct (piecewise linear: exact midpoint).
- Clamping at boundaries: input below first point → first output; above last → last output.
- Golden hash preservation: default identity spline produces identical pipeline output.

### Estimated scope

Small. The data structure is ~50 lines. Integration into one stage (Height) is ~10 lines.
The bulk of work is deciding which tunables to expose and updating golden hashes for
non-default spline configurations.

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
NOW (already designed)
  └─ N1: Power Redistribution ──── Phase J2 (ready to implement)

NEXT (new, low complexity)
  └─ N2: Spline Remapping ──────── New ScalarSpline utility + MapTunables integration
                                    Can be implemented any time; no phase dependencies.
                                    Highest impact-per-effort of the three.

LATER (new, medium complexity)
  └─ N3: Ridged Multifractal ───── Noise runtime extension or standalone operator.
                                    Natural trigger: when Phase K or Phase W begins.
                                    Design decision (A/B/C) resolved at that time.
```

### Dependency map

```
N1 (Phase J2)  ──→  consumed by: Height field, optionally Temperature/Moisture
                     no dependency on N2 or N3

N2 (Spline)    ──→  consumed by: Height, Temperature, Moisture, any future field
                     no dependency on N1 or N3
                     subsumes N1 if desired (Option B in §4)

N3 (Ridged)    ──→  consumed by: Phase K (mountains), Phase W (local elevation)
                     no dependency on N1 or N2
                     benefits from N2 (spline remap of ridged output)
```

All three are independently implementable. N2 is recommended next because it has the
broadest applicability and lowest risk.

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
