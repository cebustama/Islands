# Procedural noise composition and field operations

The six techniques in this pass transform raw noise primitives into the rich, controllable scalar fields that drive terrain, biomes, and placement in procedural content generation. **Fractal Brownian Motion** (fBm) is the foundational composition technique — a weighted summation of noise octaves that produces naturalistic fractal detail. Every other technique in this pass either modifies fBm's summation loop (ridged multifractal, turbulence), feeds its output back into its own domain (domain warping), combines its outputs with algebraic operators (scalar field composition), or defines shapes through distance rather than density (signed distance fields). Together, these six techniques constitute the compositional layer that sits between raw noise generation and downstream terrain/biome/placement systems. A developer who has implemented the noise primitives from Pass 1a and masters the techniques below can generate virtually any procedural landscape or texture pattern found in modern games.

---

## 1. Fractal Brownian motion builds detail through octave summation

A single evaluation of Perlin or simplex noise produces a smooth signal with energy concentrated in a narrow frequency band. Natural phenomena — mountain ranges, coastlines, cloud formations — exhibit detail across many scales simultaneously. fBm bridges this gap by summing multiple noise evaluations at geometrically increasing frequencies and geometrically decreasing amplitudes, producing a signal whose power spectrum follows a controllable power law. The technique exists as a distinct primitive because no single noise function can replicate this multi-scale self-similarity; it must be constructed through explicit summation (Quilez, "fBm" article, iquilezles.org/articles/fbm/; Musgrave, *Texturing and Modeling*, 3rd ed., Ch. 16).

The algorithm takes as input a point **p** (in any dimension), a noise function `noise()`, and four parameters: **octaves** *N* (integer, typically 4–15), **lacunarity** *λ* (frequency multiplier per octave, typically ~2.0), **gain** *G* (amplitude multiplier per octave, typically 0.5), and optionally a **Hurst exponent** *H* from which gain is derived. It outputs a single scalar value. The theoretical formulation sums noise contributions whose amplitudes decay as a power law of frequency:

```
fBm(p) = Σ_{i=0}^{N-1} noise(λ^i · p) · (λ^i)^{-H}
```

In practice, the power-law weights `(λ^i)^{-H}` are replaced by an iterative geometric series driven by gain *G*, where **G = 2^{-H}** when lacunarity equals 2 (Quilez, "fBm" article). This relationship is derived directly: since the amplitude at octave *i* is `λ^{-iH}`, and with λ=2 this becomes `2^{-iH} = (2^{-H})^i = G^i`. The canonical iterative implementation is:

```glsl
float fbm(vec2 x, float H, int octaves) {
    float G = exp2(-H);
    float f = 1.0;
    float a = 1.0;
    float t = 0.0;
    for (int i = 0; i < octaves; i++) {
        t += a * noise(f * x);
        f *= 2.0;
        a *= G;
    }
    return t;
}
```

(Quilez, "fBm" article). For normalized output, divide by the sum of amplitudes: `(1 - G^N) / (1 - G)` for a geometric series. When G = 0.5 and N = 4, this normalizer is 0.9375 (Quilez, domain warping shader, Shadertoy MdX3Rr).

The Hurst exponent controls the roughness of the resulting signal. **H = 0** (G = 1) produces **pink noise** with equal energy per octave — maximally rough, ~−3 dB/octave spectral slope. **H = 0.5** (G ≈ 0.707) produces **Brownian noise** at −6 dB/octave. **H = 1** (G = 0.5) produces what Quilez calls **"yellow noise"** at −9 dB/octave — the standard for terrain generation (Quilez, "fBm" article). Quilez validated this empirically by photographing mountain silhouettes, extracting their profiles as 1D signals, computing FFTs, and finding spectral slopes consistent with −9 dB/octave and H = 1 (Quilez, "fBm" article). The physical intuition: mountains that are twice as tall are also twice as wide at their base, implying isotropic self-similarity, which corresponds exactly to G = 0.5.

The primary variants of fBm differ in how they handle the per-octave contribution:

**Standard fBm** sums octaves independently, producing homogeneous roughness everywhere. Every region of the output has statistically identical detail regardless of local amplitude. This is the version shown above and is by far the most common.

**Heterogeneous terrain fBm** (Musgrave, *Texturing and Modeling*, Ch. 16, `Hetero_Terrain` function) scales each octave's contribution by the current accumulated value, so that low-lying areas (valleys) receive less high-frequency detail and high areas (peaks) receive more:

```c
// First octave
value = offset + Noise3(point);
// Subsequent octaves
for (i = 1; i < octaves; i++) {
    increment = (Noise3(point) + offset) * exponent_array[i];
    increment *= value;  // scale by current altitude
    value += increment;
}
```

(Musgrave, `musgrave.c`, Copyright 1994, hosted at Purdue/Ebert). The `offset` parameter (~1.0) shifts noise into a positive range so that the multiplicative feedback produces meaningful variation. Musgrave acknowledges that equating "valley" with "low accumulated value" is mathematically imprecise — valleys are defined by derivatives, not values — but the visual result is compelling nonetheless (Musgrave, *Texturing and Modeling*, Ch. 16).

**Derivative-damped fBm** (Quilez, "Value Noise Derivatives" article, iquilezles.org/articles/morenoise/) accumulates noise derivatives alongside values and uses their magnitude to attenuate subsequent octaves:

```glsl
const mat2 m = mat2(0.80, -0.60, 0.60, 0.80);
float terrain(in vec2 p) {
    float a = 0.0;
    float b = 1.0;
    vec2  d = vec2(0.0);
    for (int i = 0; i < 15; i++) {
        vec3 n = noised(p);       // .x = value, .yz = derivatives
        d += n.yz;                // accumulate derivatives
        a += b * n.x / (1.0 + dot(d, d));  // dampen by slope
        b *= 0.5;
        p = m * p * 2.0;
    }
    return a;
}
```

(Quilez, "morenoise" article). The division by `(1.0 + dot(d, d))` suppresses high-frequency detail in steep areas and preserves it in flat areas, producing erosion-like effects without an explicit erosion simulation. This variant also returns the accumulated derivative vector, enabling **analytic normal computation** without finite differences — up to 6× faster for shaded terrain (Quilez, "morenoise" article).

Several failure modes are well-documented. **Lattice alignment** occurs when lacunarity is exactly 2.0: the zeros and peaks of successive octaves superpose on the noise lattice, creating visible regularity. The fix is to detune lacunarity slightly — values like 2.01, 2.03, or 1.99 — so that octave features drift relative to each other (Quilez, "fBm" article). In 2D, an additional fix is to **rotate the domain** between octaves using a constant rotation matrix. Quilez uses `mat2(0.80, -0.60, 0.60, 0.80)`, a rotation of approximately 36.87°, applied at each octave (Quilez, unrolled fBm code; verified across multiple Shadertoy implementations). **Cubic interpolation artifacts** manifest as visible discontinuities in fBm when the underlying noise uses cubic (Hermite) rather than quintic interpolation, because cubic interpolation has discontinuous second derivatives. The fix is to use Perlin's improved quintic smoothstep `6t^5 − 15t^4 + 10t^3` (Quilez, "morenoise" article; Perlin, "Improving Noise," 2002). **Octave correlation near the origin** arises when all octaves sample the same noise function at `(0,0)` — they produce identical values there. Fixes include adding per-octave offsets to coordinates, using separate seeds per octave, or rotating between octaves (Red Blob Games, "Making maps with noise," redblobgames.com/maps/terrain-from-noise/; Catlike Coding, "Noise Variants" tutorial, which uses `hash + octaveIndex`). **Aliasing** occurs when high-frequency octaves produce detail below the pixel/sample resolution. The standard mitigation is to reduce the octave count for distant samples or substitute the average noise value (≈0) for octaves above the Nyquist limit.

fBm outputs a **scalar field** — a single floating-point value per spatial coordinate. In a game context, this is typically stored as a 2D floating-point array (heightmap) at terrain-chunk resolution, or evaluated on-the-fly in a shader. Downstream, this scalar field serves as the primary elevation source for terrain mesh generation, as input to biome classification (e.g., the elevation axis of a Whittaker diagram), and as the base signal for further composition via domain warping or mask blending. When the derivative-returning variant is used, the output is a **gradient field** (scalar + 2D or 3D gradient vector), which feeds directly into normal computation for lighting, slope-based texturing, and erosion-aware biome assignment.

---

## 2. Ridged multifractal noise creates heterogeneous terrain with sharp crests

Standard fBm produces terrain with uniform roughness — statistically, every region looks the same. Real mountain ranges exhibit **heterogeneous** roughness: jagged ridgelines with fine detail, separated by smooth valley floors. Ridged multifractal noise addresses this by transforming the noise signal within the summation loop to create sharp ridges and by feeding each octave's signal strength into the next octave's amplitude, producing a landscape where rough areas become progressively rougher and smooth areas stay smooth. It exists as a distinct primitive because neither the signal transformation (abs-invert-square) nor the inter-octave feedback can be replicated by adjusting fBm parameters alone (Musgrave, *Texturing and Modeling*, 3rd ed., Ch. 16).

The algorithm takes the same inputs as fBm — a point **p**, noise function, octaves, lacunarity, and H — plus two additional parameters: **offset** (typically 1.0) and **gain** (typically 2.0). The spectral weights array is precomputed identically to Musgrave's fBm: `exponent_array[i] = pow(frequency, -H)` where frequency starts at 1.0 and multiplies by lacunarity each step. The core loop, reproduced verbatim from Musgrave's original `musgrave.c` (Copyright 1994, F. Kenton Musgrave; source hosted at engineering.purdue.edu/~ebertd/texture/):

```c
/* First octave */
signal = Noise3(point);
if (signal < 0.0) signal = -signal;   // Step 1: absolute value
signal = offset - signal;              // Step 2: invert and translate
signal *= signal;                      // Step 3: square for sharpness
result = signal;
weight = 1.0;

for (i = 1; i < octaves; i++) {
    point.x *= lacunarity;
    point.y *= lacunarity;
    point.z *= lacunarity;

    weight = signal * gain;            // Step 4: feedback
    if (weight > 1.0) weight = 1.0;   // Clamp to [0, 1]
    if (weight < 0.0) weight = 0.0;

    signal = Noise3(point);
    if (signal < 0.0) signal = -signal;
    signal = offset - signal;
    signal *= signal;
    signal *= weight;                  // Step 5: apply feedback
    result += signal * exponent_array[i];
}
```

The four transformations within the loop each serve a specific purpose. **Step 1 (absolute value)** folds the noise at its zero crossings, creating V-shaped creases where the signal would otherwise pass smoothly through zero. **Step 2 (offset − signal)** inverts these creases into peaks: the zero-crossings of the original noise become the ridgelines of the output. The offset parameter controls the baseline height; with offset ≈ 1.0 and noise in [−1, 1], the result maps to [0, 1] with maxima at the original zero-crossings. **Step 3 (squaring)** sharpens the ridges by concentrating energy near the peaks and widening the valleys — a mild nonlinearity that dramatically increases visual drama. **Steps 4–5 (feedback weighting)** are the critical innovation: the current octave's squared signal, multiplied by the gain parameter and clamped to [0, 1], becomes the amplitude weight for the *next* octave. Where the previous octave produced a strong signal (near a ridge), subsequent octaves contribute at full amplitude, adding fine detail to the ridgeline. Where the previous octave produced a weak signal (in a valley), subsequent octaves are suppressed, leaving the valley smooth. The gain parameter (default 2.0) controls feedback strength: higher gain makes ridges noisier (Musgrave, `musgrave.c`).

The recommended starting parameters are **H = 1.0, offset = 1.0, gain = 2.0** (Musgrave, comment in `musgrave.c`). H = 1.0 produces spectral weights of `1/lacunarity^i`, giving each octave proportionally less energy — identical weighting to standard fBm with G = 0.5 when lacunarity = 2.

Two major variants exist. The **Hybrid Multifractal** (`HybridMultifractal` in Musgrave's code, recommended starting parameters H = 0.25, offset = 0.7) combines additive and multiplicative accumulation:

```c
result = (Noise3(point) + offset) * exponent_array[0];
weight = result;
for (i = 1; i < octaves; i++) {
    if (weight > 1.0) weight = 1.0;
    signal = (Noise3(point) + offset) * exponent_array[i];
    result += weight * signal;
    weight *= signal;  // multiplicative cascade
}
```

(Musgrave, `musgrave.c`). This variant does not apply the abs-invert-square transformation, instead relying purely on multiplicative feedback to create heterogeneity. The result is less dramatically ridged but produces convincing mountain terrain with variable roughness. The **Hetero_Terrain** variant scales each increment by the current accumulated altitude rather than the previous octave's signal, producing smooth valleys and rough peaks through a simpler mechanism (described in the fBm section above).

A simplified **ridged noise** formulation, common in tutorials and shader code, omits the feedback mechanism entirely:

```glsl
float ridgenoise(float nx, float ny) {
    return 2.0 * (0.5 - abs(0.5 - noise(nx, ny)));
}
```

(Red Blob Games, "Making maps with noise"). This produces ridge-like features but with homogeneous roughness — it lacks the heterogeneous quality of Musgrave's full formulation. Red Blob Games demonstrates amplitude modulation as a partial substitute: multiplying each octave by the accumulated value of previous octaves (`e1 = 0.5 * ridgenoise(2*nx, 2*ny) * e0`), which approximates the feedback mechanism (Red Blob Games, "Making maps with noise").

Several failure modes are specific to ridged multifractal noise. **First-octave dominance** occurs in the hybrid variant: because the first octave is unweighted and directly determines the initial weight value, its frequency and character visibly dominate the output, producing broad low-frequency bands (Musgrave, *Texturing and Modeling*, Ch. 16). **Gain sensitivity** makes the output fragile to parameter changes — values much above 2.0 produce excessively noisy ridges, while values below 1.0 effectively disable feedback and collapse the output toward standard fBm. The clamping at [0, 1] prevents numerical divergence but introduces hard transitions in the weight field. **Lacunarity alignment** produces the same lattice-superposition artifacts as in fBm; detuning to 1.92 or 2.02 is equally necessary here. **Offset misunderstanding** is a common implementation error: changing offset does not simply raise or lower terrain — it shifts *where ridges form* relative to the noise lattice, because it changes which noise values map to the ridge peaks after the abs-invert step.

Ridged multifractal noise outputs a scalar field, typically stored as a heightmap. It is most commonly used as the primary terrain elevation source when dramatic, mountain-range-like topography is desired. It can be blended with standard fBm via scalar field composition (technique 5) to produce terrain with ridged mountains in some areas and gentle rolling hills in others, using a mask noise to control the blend weight. The heterogeneous roughness makes it particularly valuable for terrain that will be viewed at multiple scales, as ridgelines maintain visual interest at close range while valleys provide navigable space.

---

## 3. Turbulence produces billowy patterns through absolute-value folding

Turbulence addresses a specific aesthetic limitation of fBm: because fBm's noise contributions pass smoothly through zero, the resulting signal has a symmetric, cloud-like quality that lacks the sharp creases seen in phenomena like fire, billowing smoke, and marble veining. Perlin introduced the turbulence function in his 1985 SIGGRAPH paper "An Image Synthesizer" (ACM SIGGRAPH Computer Graphics, Vol. 19, No. 3, pp. 287–296) as a companion to the noise function itself. It exists as a distinct primitive rather than a parameter of fBm because the absolute-value operation fundamentally changes the mathematical properties of the output — introducing derivative discontinuities and infinite frequency content that cannot be achieved by adjusting fBm's H, G, or octave count.

The mathematical formulation differs from fBm by exactly one operation — wrapping each octave's noise evaluation in `abs()`:

```
turbulence(p) = Σ_{i=0}^{N-1} ω^i · |noise(λ^i · p)|
```

where ω is the amplitude falloff (persistence, typically 0.5) and λ is the lacunarity (typically ~2.0) (Perlin, "An Image Synthesizer," 1985; PBR Book, 3rd ed., Section 10.6). The implementation from the PBR Book (pbrt) uses a slightly detuned lacunarity of 1.99 to avoid lattice alignment:

```cpp
Float sum = 0, lambda = 1, o = 1;
for (int i = 0; i < octaves; ++i) {
    sum += o * std::abs(Noise(lambda * p));
    lambda *= 1.99f;
    o *= omega;
}
```

(Pharr, Jakob, Humphreys, *Physically Based Rendering*, 3rd ed.). For antialiasing, the PBR Book substitutes the average value of `|noise|` — approximately **0.2** — for octaves above the estimated Nyquist frequency, since these octaves would alias rather than contribute useful detail.

The `abs()` operation "folds" each noise octave at its zero crossings. Where fBm's noise smoothly transitions through zero (producing a local minimum or maximum after summation), turbulence creates a **V-shaped cusp** — a sharp crease at every zero crossing of every octave. When summed across multiple octaves, these creases at different scales produce the characteristic billowy, turbulent appearance. The visual quality is asymmetric in a way that fBm is not: positive turbulence values produce **round peaks with narrow valleys** (cumulus-cloud-like), while using turbulence as a negative displacement produces **sharp ridges with wide valleys** (Catlike Coding, "Noise Variants" tutorial; The Book of Shaders, Ch. 13). The output is always non-negative, unlike fBm which is centered at zero.

Perlin's most famous application of turbulence is **marble texture generation**:

```
value = sin(point.x + amplitude * turbulence(point));
color  = spline(white, blue, value);
```

(Perlin, "An Image Synthesizer," 1985; also expressed as `.01 * stripes(x + 2 * turbulence(x,y,z,1), 1.6)` in Perlin's notation). Here, turbulence warps the coordinate fed to a periodic function, creating the characteristic veined banding of marble. The sine function provides the regular layered structure, and turbulence organically distorts these layers. This is technically a domain warping application (technique 4), but the specific combination of turbulence-as-warper with a periodic base function is so canonical that it is inseparable from turbulence's historical identity.

Turbulence has no major implementation variants beyond the choice of base noise function (Perlin, simplex, value), but it relates to two adjacent techniques. **Billowed noise** is a single octave of turbulence: `abs(noise(p))` — useful when the folding effect is desired without multi-octave summation (de Carpentier, "Scape Procedural Basics," decarpentier.nl). **Ridged noise** (technique 2) applies an additional inversion to billowed noise: `offset - abs(noise(p))`, turning creases into peaks. Turbulence can thus be understood as the unsigned, additive analog of the signed, feedback-driven ridged multifractal: both begin with `abs(noise)`, but turbulence sums these values directly while ridged multifractal inverts, squares, and feeds back.

The failure modes of turbulence stem directly from the `abs()` operation. **First-derivative discontinuities** at every zero crossing mean the function is only C⁰ continuous — continuous in value but not in slope. The derivative jumps abruptly at these points, creating visible creases in bump-mapped or lit surfaces. **Infinite frequency content** results from these cusps: a V-shaped discontinuity has energy at arbitrarily high frequencies, so turbulence cannot be perfectly band-limited. The PBR Book states explicitly: "The first-derivative discontinuities in the function introduce infinitely high-frequency content, so efforts can't hope to be perfect" (PBR Book, 3rd ed., Section 10.6). This makes turbulence prone to **temporal aliasing** (flickering) in animation and **spatial aliasing** at grazing viewing angles. Mitigation strategies include clamping the octave count based on screen-space frequency, substituting average values for super-Nyquist octaves, or using turbulence only for low-frequency warping where aliasing is not visible. **Non-zero mean** is a practical issue: because `|noise|` averages to approximately 0.2 rather than 0 (PBR Book), turbulence accumulates a positive bias that grows with octave count. Applications expecting a zero-centered signal must subtract this bias explicitly. Finally, **analytic derivatives cannot be computed through `abs()`** because the function is non-differentiable at zero crossings, limiting turbulence's use in techniques that require noise gradients (Quilez, "morenoise" article, where derivative-based fBm explicitly avoids abs-type transformations).

Turbulence outputs a non-negative scalar field. In terrain generation, it is most often used indirectly — as a domain warper for periodic base functions (marble, wood grain patterns) or as a contributor to composite elevation fields where its billowy character adds variety. It feeds into biome texturing (cloud maps, fire effects) and procedural material generation more than into elevation, because its always-positive, cusp-heavy character is less suited to naturalistic heightfield terrain than fBm or ridged noise.

---

## 4. Domain warping distorts space itself for organic complexity

Noise-based patterns generated by fBm, ridged noise, or turbulence share a common limitation: their features follow the statistical structure of the underlying noise lattice. No matter how many octaves are summed or how parameters are tuned, the output retains a certain "noise-like" regularity that experienced eyes recognize as procedural. Domain warping breaks this regularity by **distorting the input coordinates** before evaluating the pattern function, producing organic, flowing, geologically plausible forms that cannot be achieved by parameter tuning alone. Quilez describes the technique as having been "used since 1984, when Ken Perlin himself created his first procedural marble texture" (Quilez, "Domain Warping" article, iquilezles.org/articles/warp/, 2002).

The general principle is straightforward: given a pattern function `f(p)`, replace the input with a distorted version: **f(p + h(p))**, where `h(p)` is a displacement function. When `h` is zero, the output is the original pattern; when `h` is nonzero, every point in space is shifted before evaluation, "pinching, stretching, twisting, and bending" the pattern (Quilez, "warp" article). For PCG applications, both `f` and `h` are typically fBm-based, producing abstract but highly organic results.

The implementation requires constructing a **vector-valued displacement** from scalar fBm calls. Since fBm outputs a scalar but spatial displacement requires a vector (2D for 2D patterns, 3D for 3D), the standard approach evaluates two (or three) independent scalar fBm functions at different offsets to construct each displacement component. Quilez's canonical single-warp implementation:

```glsl
float pattern(in vec2 p) {
    vec2 q = vec2( fbm( p + vec2(0.0, 0.0) ),
                   fbm( p + vec2(5.2, 1.3) ) );
    return fbm( p + 4.0 * q );
}
```

(Quilez, "warp" article). The offset vectors `vec2(0.0, 0.0)` and `vec2(5.2, 1.3)` have no special meaning — they simply ensure that the two fBm evaluations sample different regions of noise space, producing uncorrelated displacement components. The **warp amplitude** factor `4.0` controls displacement strength: larger values produce more extreme distortion. The output is a scalar — the final fBm evaluation at the warped coordinate.

The **double-warp** (two-level) variant nests the process, producing dramatically more complex patterns:

```glsl
float pattern(in vec2 p, out vec2 q, out vec2 r) {
    q.x = fbm( p + vec2(0.0, 0.0) );
    q.y = fbm( p + vec2(5.2, 1.3) );

    r.x = fbm( p + 4.0*q + vec2(1.7, 9.2) );
    r.y = fbm( p + 4.0*q + vec2(8.3, 2.8) );

    return fbm( p + 4.0*r );
}
```

(Quilez, "warp" article). Here, the first displacement `q` warps the coordinates used to compute the second displacement `r`, which in turn warps the coordinates for the final evaluation. The `out` parameters expose the intermediate displacement vectors for use in **coloring**: Quilez demonstrates mapping color palettes based on the magnitude of `q` and the components of `r` to produce richly varied imagery from a single scalar pattern (Quilez, "warp" article; Shadertoy view/4s23zz). Quilez notes: "we got three fBM functions that do change the internal structure of our final image, so why not use those too to get some extra coloring."

The recursive form of domain warping can be expressed compactly as `f(p) = fbm(p + fbm(p + fbm(p)))` — each nesting level applies one warp (The Book of Shaders, Ch. 13; Quilez, "warp" article). Each additional level multiplies the computational cost: the double-warp version requires approximately 5 full fBm evaluations (2 for `q`, 2 for `r`, 1 final), each of which internally sums N octaves of noise. With 4-octave fBm, this means roughly 20 noise evaluations per output sample.

The key implementation variants are:

**Single-warp vs. double-warp**, as shown above. Single warp produces gentle organic distortion; double warp produces the dramatically swirling, marble-like patterns that are domain warping's signature. The visual difference is substantial — single warp is often subtle enough to serve as terrain enhancement, while double warp creates alien, painterly effects better suited to textures or stylized maps.

**Iterative warping with falloff** generalizes the nesting to an arbitrary number of iterations with decreasing warp strength:

```javascript
function getWarpedPosition(x, y) {
    let scale = 1;
    for (let i = 0; i < numWarps; i++) {
        const dx = warpSize * noiseX(x, y);
        const dy = warpSize * noiseY(x, y);
        x += scale * dx;
        y += scale * dy;
        scale *= falloff;  // 0 < falloff < 1
    }
    return [x, y];
}
```

The `falloff` parameter ensures convergence; without it, repeated warping at full strength can push coordinates arbitrarily far from their original positions. Using two **different** noise functions for the X and Y displacement components is essential — using the same function constrains displacement to a diagonal line (unconfirmed as specific to any primary source, but consistent with the geometric principle that identical X and Y displacements produce only 45° movement).

**Derivative-aware warping** uses the gradient of noise rather than its value for displacement. This is referenced in The Book of Shaders, Ch. 13, citing Perlin and Neyret's "flow noise" concept. The displacement follows the noise gradient, producing flowing, stream-like distortion patterns rather than the random organic distortion of value-based warping.

The primary failure modes are **over-warping** and **coordinate explosion**. When warp amplitude is too large relative to the noise frequency, features stretch beyond recognition and the output degenerates into featureless noise soup. The fix is to keep the warp amplitude in proportion to the spatial scale of the base pattern — Quilez's factor of 4.0 works well for fBm with a base frequency of ~1.0, but must be adjusted if the base frequency changes. **Periodic repetition** can emerge when the warp displacement is large enough to shift coordinates by a full noise period, causing distant features to appear locally — visible as unexpected self-similarity. Keeping warp amplitude below one noise wavelength avoids this. **Performance cost** is a practical concern: each warp level multiplies the noise evaluation budget, and double-warp with high octave counts can exceed real-time budgets. Reducing octave count in the displacement fBm (e.g., 2–3 octaves for `q` and `r`, full octaves only for the final evaluation) is a standard optimization.

Domain warping outputs a scalar field (the final fBm value) plus optionally the intermediate displacement vectors `q` and `r`. The scalar field integrates directly as terrain elevation or texture density; the displacement vectors provide additional variation channels for coloring, biome assignment, or material blending. In terrain pipelines, domain warping is typically applied to the elevation field to break up fBm regularity, producing coastlines, mountain ranges, and geological strata with more naturalistic, non-isotropic shapes. In texture generation, it is the primary technique for marble, agate, and organic-tissue effects.

---

## 5. Layered masks and scalar field composition combine fields into complex worlds

The techniques above each produce a single scalar field. Real game worlds require many such fields — elevation, moisture, temperature, cave density, vegetation probability — combined through algebraic operations to produce the final terrain shape, biome map, and placement rules. Scalar field composition is the systematic practice of combining these fields using operators that are well-understood in terms of their visual and numerical behavior. It exists as a distinct "technique" rather than being absorbed into the individual noise methods because the **composition operators themselves** — lerp, multiply, min, max, power, smoothstep, spline — have their own parameter spaces, failure modes, and design implications that require deliberate attention.

The core operators, in order of frequency of use in PCG pipelines, are:

**Additive composition** is the operator within fBm itself — octaves of noise summed with weights. Outside fBm, addition combines independent terrain features: `elevation = baseTerrain(p) + mountainDetail(p) + riverCarving(p)`. Each additive term must be scaled to contribute an appropriate proportion of the total range; un-normalized addition is the most common source of out-of-range values.

**Multiplicative composition (masking)** uses one field to scale another: `result = baseField(p) * mask(p)`. When the mask is binary (0 or 1), this is hard masking; when it is continuous [0, 1], this is soft masking. The practical consequence is that regions where the mask is near zero have their base field suppressed regardless of its value. This is the fundamental mechanism for biome-specific terrain: multiply mountain-noise by a mountain-biome mask and flatland-noise by a flatland mask, then sum.

**Linear interpolation (lerp)** blends two fields using a third as the blend weight: `result = lerp(fieldA(p), fieldB(p), t(p))` where `t ∈ [0, 1]`. This guarantees the output stays within the range of the two input fields — a property that raw addition lacks. Minecraft's terrain system uses this extensively: a "selector noise" interpolates between a `low_noise` terrain field and a `high_noise` terrain field, with selector values below 0 choosing low_noise entirely and above 1 choosing high_noise entirely (Minecraft Wiki, "Noise router" documentation).

**Power redistribution** reshapes a field's value distribution without changing its spatial structure: `result = pow(elevation, exponent)`. With exponent > 1, mid-range values are pushed downward, creating **flat valleys with sharp peaks** — the single most impactful post-processing step for making fBm-based terrain look natural (Red Blob Games, "Making maps with noise"). With exponent < 1, the effect inverts: peaks are flattened and valleys are raised. Red Blob Games recommends using a fudge factor near 1.0 to fine-tune: `pow(e * 1.2, exponent)` (Red Blob Games, "Making maps with noise").

**Smoothstep remapping** passes a field through an S-curve to create sharper transitions: `result = smoothstep(edge0, edge1, field(p))`. This is the standard technique for **mask generation from continuous noise** — converting a continuous field into a soft binary mask with a controlled transition width. The transition band `[edge0, edge1]` determines where the mask transitions from 0 to 1. Using the quintic smoothstep variant `(6t^5 − 15t^4 + 10t^3)` gives smoother derivatives at the transition boundaries (Perlin, "Improving Noise," 2002).

**Min and max** select the lower or higher value from two fields: `min(fieldA, fieldB)` carves the lower surface (useful for river valleys cutting into terrain), while `max(fieldA, fieldB)` takes the higher (useful for adding mesa plateaus). Sharp min/max produce creased intersections; smooth min/max (technique 6) produce blended transitions.

**Spline remapping** maps field values through an artist-defined curve. Minecraft's post-1.18 terrain system uses cubic spline functions to remap noise values (continentalness, erosion, peaks-and-valleys) to terrain height, enabling dramatic terrain variety that decouples noise statistics from terrain shape (Minecraft Wiki, "Noise router" documentation). Sebastian Lague's Unity tutorials use AnimationCurve objects for the same purpose (Lague, "Procedural Landmass Generation" series).

The standard biome-generation pattern uses **multiple independent noise fields as axes in a classification space**. The simplest version uses two fields — elevation and moisture — following ecologist Robert Whittaker's biome classification model. Red Blob Games provides the canonical lookup:

```javascript
function biome(e, m) {
    if (e < 0.1) return OCEAN;
    if (e < 0.12) return BEACH;
    if (e > 0.8) {
        if (m < 0.33) return SCORCHED;
        if (m < 0.66) return BARE;
        return SNOW;
    }
    // ... additional elevation/moisture bands
}
```

(Red Blob Games, "Making maps with noise"). Minecraft extends this to five noise dimensions — continentalness, erosion, peaks-and-valleys, temperature, and humidity — with biomes defined as regions in this 5D parameter space (Minecraft Wiki, "Multi-noise biome source"). Each biome specifies acceptable ranges across all five parameters, and the closest-matching biome is selected per position. For **terrain blending at biome borders**, a weighted-average approach computes per-biome terrain heights and blends them by biome weight:

```pseudocode
finalHeight = 0
for each (biome, weight) in getBiomeWeights(elevation, moisture, temperature):
    biomeHeight = biome.heightCurve.evaluate(elevation)
    biomeDetail = fbm(p * biome.detailFreq, biome.octaves)
    finalHeight += weight * (biomeHeight + biome.detailAmp * biomeDetail)
```

Weights are computed by proximity in parameter space and normalized to sum to 1.0, ensuring smooth transitions.

**Distance-based masks (falloff maps)** force terrain to a specific value at world boundaries, creating islands or continent edges:

```
d = 1 - (1 - nx²) * (1 - ny²)     // Square bump: 0 at center, 1 at edges
elevation = lerp(elevation, 1 - d, mixFactor)
```

(Red Blob Games, "Making maps with noise"). Sebastian Lague's implementation subtracts a falloff value from noise: `finalHeight = clamp(noiseValue - falloffValue, 0, 1)` (Lague, "Procedural Landmass Generation" series). The distance function can be Euclidean, Chebyshev, or any SDF (technique 6), connecting scalar field composition directly to the distance field pipeline.

The failure modes of scalar field composition are distinct from those of individual noise techniques. **Seam artifacts at biome borders** arise from hard thresholding: if biome A uses 6 octaves of ridged noise and biome B uses 4 octaves of smooth fBm, and the transition is abrupt, the discontinuity in terrain character is visually jarring even if the terrain heights are close. The fix is smooth blending over a wide transition zone — tens to hundreds of world units. **Dynamic range collapse** occurs when multiple masks are multiplied together: `mask1 * mask2 * mask3` pushes most values toward zero, leaving only a tiny fraction of the world with nonzero values. The fix is to use additive blending with weight normalization rather than multiplicative cascading. **Self-similarity fatigue** arises when the same fBm function drives both elevation and moisture (or uses correlated seeds): biome boundaries follow elevation contours exactly, producing an unrealistic banded appearance. The fix is to use genuinely independent noise fields with different seeds, frequencies, and offsets for each compositional axis (Red Blob Games, "Making maps with noise"; verified importance of independent seeds). **Floating-point precision loss** manifests at large world coordinates when low-frequency noise octaves sample very large coordinate values, causing the fractional component used by the noise function to lose precision. GPU Gems 3 (Ch. 1, Ryan Geiss) documents this artifact and recommends manual trilinear interpolation with full float precision for the lowest-frequency octaves.

Scalar field composition outputs **composite scalar fields** (heightmaps, density fields, probability maps) and **discrete classification maps** (biome IDs, terrain-type enumerations). The scalar fields feed directly into terrain mesh generation (heightmap → vertex displacement, or density field → marching cubes). Classification maps drive material/texture selection, vegetation placement rules, and AI navigation parameters. The composition pipeline is where artist control is most concentrated — through choice of operators, mask shapes, spline curves, and blending widths — making it the most iterated-upon layer in a PCG system.

---

## 6. Signed distance fields define shapes through proximity

Where scalar fields encode density or elevation — values whose magnitude is meaningful only relative to thresholds — signed distance fields (SDFs) encode **geometric proximity**: the value at any point is the shortest distance to the nearest surface, negative inside and positive outside. This geometric meaning makes SDFs uniquely powerful for PCG operations that require Boolean shape combination (union, intersection, subtraction), smooth blending between shapes, and infinite repetition of geometric primitives. SDFs exist as a distinct primitive in PCG because no amount of noise composition can produce guaranteed-geometric operations like "merge these two cave systems with a smooth 10-meter blend radius." SDFs provide that guarantee (Quilez, "Distance Functions" article, iquilezles.org/articles/distfunctions/; Quilez, "Smooth Minimum" article, iquilezles.org/articles/smin/).

The PCG-relevant SDF primitives are shapes used as building blocks for terrain features, dungeon rooms, and placement masks. Each takes a sample point **p** and shape parameters, returning a signed float. The most useful 2D primitives for map generation:

```glsl
// Circle — exact
float sdCircle(vec2 p, float r) { return length(p) - r; }

// Box — exact
float sdBox(vec2 p, vec2 b) {
    vec2 d = abs(p) - b;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
}

// Line segment — exact
float sdSegment(vec2 p, vec2 a, vec2 b) {
    vec2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h);
}
```

(Quilez, "Distance Functions 2D" article, iquilezles.org/articles/distfunctions2d/). In 3D, the sphere (`length(p) - r`), box, capsule (line segment with radius), and plane (`dot(p, n) + h`) are the most frequently used for volumetric terrain carving. All primitives are centered at the origin; translation is achieved by transforming the sample point (`sdCircle(p - center, r)`), and rotation by applying an inverse rotation matrix to **p**. **Non-uniform scaling is forbidden** — it destroys the unit-gradient property that makes the distance field valid (Quilez, "distfunctions" article).

The Boolean combination operations are the core reason SDFs matter for PCG:

```glsl
float opUnion(float a, float b)        { return min(a, b); }
float opSubtraction(float a, float b)   { return max(-a, b); }
float opIntersection(float a, float b)  { return max(a, b); }
```

(Quilez, "distfunctions" article). Union merges two shapes (useful for combining cave chambers), subtraction carves one shape from another (tunnels through terrain), and intersection retains only the overlap (constraining features to a region). A critical caveat: **these operations produce correct SDFs only in the exterior** (positive distances). Interior distances become incorrect after union of overlapping shapes, and exterior distances become incorrect after subtraction and intersection. Quilez documents this limitation, and it remains an unsolved problem requiring case-specific workarounds (Quilez, "Interior Distances" article; GM Shaders confirmation).

**Smooth Boolean operations** replace the hard `min`/`max` with smooth minimum/maximum functions, creating blended transitions between shapes instead of sharp creases. The **quadratic polynomial smooth minimum** is the most widely used variant:

```glsl
float opSmoothUnion(float a, float b, float k) {
    float h = max(k - abs(a - b), 0.0) / k;
    return min(a, b) - h * h * k * (1.0 / 4.0);
}
```

(Quilez, "smin" article; the normalization constant `k *= 4.0` is absorbed into the formula). The equivalent classic form using `clamp` and `mix`:

```glsl
float opSmoothUnion(float a, float b, float k) {
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) - k * h * (1.0 - h);
}
```

(Quilez, "smin" article). The parameter **k** controls the blend radius in world units — larger k produces wider, more gradual blending. When `|a - b| > k`, the output equals `min(a, b)` exactly (no distortion outside the blend region). Smooth subtraction and intersection are derived from smooth union: `opSmoothSubtraction(a, b, k) = -opSmoothUnion(a, -b, k)` and `opSmoothIntersection(a, b, k) = -opSmoothUnion(-a, -b, k)` (Quilez, "smin" article).

Quilez documents several smooth minimum variants, each with different mathematical properties:

- **Quadratic polynomial** (C¹ continuous): Fast, locally supported, conservative (never overestimates distance). **Not associative** — evaluation order matters. Normalization constant: ×4.0. The recommended default (Quilez, "smin" article: "In general I use the quadratic polynomial").
- **Cubic polynomial** (C² continuous): Same structure but with `h³` instead of `h²`, producing smoother blending that avoids lighting artifacts at the blend boundary. Normalization constant: ×6.0. Suggested by Shadertoy user TinyTexel (Quilez, "smin" article).
- **Exponential**: Uses `exp2`/`log2` — **associative** (evaluation order doesn't matter), making it suitable for Voronoi constructions where order varies. However, it is **non-local**: it distorts the distance field everywhere in space, not just near the blend region. Normalization constant: ×1.0 (Quilez, "smin" article).
- **Circular**: Produces a geometrically perfect circular fillet profile. Locally supported and associative, but **can overestimate** distances (non-conservative), making it unsafe for raymarching. Normalization constant: ×1/(1 − √0.5) (Quilez, "smin" article).

For PCG, the smooth minimum also provides a **blend factor** for material interpolation — a value from 0 to 1 indicating how much each input shape contributes at the blend point:

```glsl
vec2 smin(float a, float b, float k) {
    float h = 1.0 - min(abs(a - b) / (4.0 * k), 1.0);
    float w = h * h;
    float m = w * 0.5;
    float s = w * k;
    return (a < b) ? vec2(a - s, m) : vec2(b - s, 1.0 - m);
}
```

(Quilez, "smin" article). The `.y` component can drive per-pixel material blending at shape junctions — critical for making merged cave chambers look natural rather than painted.

The PCG-relevant **domain operations** on SDFs include:

**Rounding** (`sdShape(p) - r`) expands a shape outward by radius `r`, rounding all edges. **Onion/shell** (`abs(sdShape(p)) - thickness`) hollows a shape into a shell of uniform thickness — directly useful for generating cave walls or architectural outlines. **Domain repetition** (`p = mod(p + s/2, s) - s/2`) tiles a shape infinitely in space with spacing `s`, creating corridors, pillar grids, or lattice structures from a single primitive. Limited repetition using `clamp(round(p/s), -count, count)` constrains the repetition to a finite count (Quilez, "distfunctions" article). **Displacement** (`sdShape(p) + fbm(p)`) adds noise-based surface detail to geometric shapes — the bridge between SDF primitives and noise-based techniques. This is how cave walls get their rocky texture: the SDF provides the macro shape, and fBm adds micro-scale roughness.

The failure modes specific to SDFs in PCG contexts are important to understand. **Interior distance corruption** after Boolean operations means that if you use an SDF's interior distance for anything meaningful (e.g., calculating wall thickness or interior ambient occlusion), the values will be wrong after union/subtraction operations. For PCG uses where the SDF is converted to a mesh (via marching cubes or contour tracing) and then discarded, this is often acceptable — as James Stanley notes, "any unpleasantness in your broken distance function is forgotten as soon as you turn it into a mesh" (incoherency.co.uk). **Non-exact SDF after smooth operations** means the gradient magnitude drops below 1.0 in blend regions. For raymarching this means slower convergence; for PCG mesh extraction it means the zero-isosurface is slightly shifted from its ideal position, producing minor geometric inaccuracy near blend regions. **Non-associativity of polynomial smooth min** means that `smin(a, smin(b, c)) ≠ smin(smin(a, b), c)` — the order in which shapes are combined affects the result. For fixed scene descriptions (dungeon layouts, cave networks) this is irrelevant since order is deterministic, but for streaming or incremental generation where shapes arrive in variable order, the exponential smooth min should be used instead (Quilez, "smin" article).

SDFs output a **distance field**: a scalar value per spatial coordinate where the value represents signed Euclidean distance to the nearest surface. In a game context, this is stored as a 2D or 3D grid of floats (or as a function evaluated on demand). The zero-isosurface of the distance field defines geometry; the field's gradient at the surface defines the surface normal. Downstream, SDFs feed into terrain mesh generation (via marching cubes/squares for the zero-isosurface), collision detection (the distance value directly gives penetration depth), proximity queries (e.g., spawning foam where water is within distance `d` of a riverbank, as documented in Flax Engine's Global SDF feature), and mask generation (thresholding the SDF at various distances produces concentric mask bands for biome transitions, cliff texturing, or shore effects). SDFs composed from primitives and smooth operations can also serve as the falloff masks consumed by technique 5's scalar field composition — providing artist-controllable geometric shapes that noise fields alone cannot produce.

---

## Data structures produced by Passes 1a and 1b combined

**Scalar field.** A function mapping spatial coordinates to a single floating-point value, representing elevation, density, moisture, temperature, or any continuous quantity. In a game context, scalar fields are stored as 2D `float[]` arrays at chunk resolution (typically 64×64 to 256×256 per chunk), as 3D voxel grids (`float[][][]`) for volumetric terrain, or evaluated procedurally per-sample in shaders. Every noise primitive from Pass 1a (value noise, Perlin noise, simplex noise, Worley noise) produces a scalar field, and every compositional technique in Pass 1b (fBm, ridged multifractal, turbulence, domain warping, scalar field composition) consumes and produces scalar fields. Downstream, scalar fields drive terrain mesh generation (heightmap vertex displacement or marching cubes isosurface extraction), biome classification (as axes in Whittaker-style lookup tables), and object placement (as density or probability maps that modulate spawn rates).

**Point set.** An unordered collection of spatial coordinates, typically generated by Poisson disk sampling (Pass 1a) and stored as a list or spatial hash of 2D/3D positions. Point sets represent candidate locations for object placement — trees, rocks, buildings, enemies. Downstream passes filter point sets against scalar field masks (e.g., rejecting points where slope exceeds a threshold or biome type is incompatible) and assign object types based on local field values.

**Gradient field.** A scalar field augmented with its spatial derivatives — a value plus a 2D or 3D vector per sample point. Produced by derivative-returning noise functions (Pass 1a) and derivative-aware fBm (Pass 1b, Quilez's `noised` variant). Stored as `vec3` (2D: value + 2 derivatives) or `vec4` (3D: value + 3 derivatives) per sample. Gradient fields enable analytic normal computation for terrain lighting without finite differences, slope-based texture blending, and erosion simulation where water flow follows the gradient direction.

**Distance field.** A scalar field where the value at each point represents signed Euclidean distance to the nearest surface boundary — negative inside, positive outside, zero at the surface. Produced by SDF primitives and Boolean/smooth operations (Pass 1b). Stored as a 2D or 3D `float[]` grid, or evaluated procedurally. Distance fields feed into mesh extraction (marching cubes/squares at the zero-isosurface), collision detection (distance value = penetration depth), proximity-based effects (shore foam, cliff moss, corridor lighting falloff), and serve as geometric masks for scalar field composition.

**Weighted distribution table.** A discrete probability distribution mapping categories (biome types, object types, material IDs) to weights, often parameterized by position. Produced by biome classification functions that consume multiple scalar fields and output per-biome weights (Pass 1b, scalar field composition). Stored as small arrays of (category, weight) pairs per sample point or per chunk, normalized to sum to 1.0. Consumed by placement passes (selecting which tree species to spawn), terrain blending passes (weighting per-biome heightmaps), and material passes (blending texture layers).

---

## Source landscape

**Primary papers and books.** Ken Perlin's "An Image Synthesizer" (SIGGRAPH 1985) is the origin of both the noise function and the turbulence function; it is the only source for the original marble-texture formulation and the only place where turbulence is motivated from first principles rather than presented as a recipe. F. Kenton Musgrave's Chapter 16 in *Texturing and Modeling: A Procedural Approach* (3rd edition, 2003) is the definitive reference for ridged multifractal, hybrid multifractal, and heterogeneous terrain algorithms; no other source provides the full family of multifractal variants with both pseudocode and parameter guidance. Perlin's "Improving Noise" (SIGGRAPH 2002) is essential for understanding why quintic interpolation eliminates second-derivative artifacts in fBm.

**Tutorial series.** Inigo Quilez's article collection at iquilezles.org is the single most important implementation reference for this pass — the fBm article uniquely provides spectral analysis linking Hurst exponent to noise color and empirical mountain-profile validation; the domain warping article provides the only canonical progressive code examples (plain → single-warp → double-warp); the SDF primitives and smooth minimum articles provide the definitive GLSL implementations used across the industry. Red Blob Games' "Making maps with noise" (redblobgames.com/maps/terrain-from-noise/) is uniquely valuable for its interactive demonstrations of octave composition, power redistribution, and two-axis biome classification — it is the best source for understanding scalar field composition as a design tool rather than a mathematical abstraction. The Book of Shaders (Chapters 12–13, thebookofshaders.com) provides interactive GLSL editors for fBm, turbulence, and domain warping, making it the best environment for building intuition through experimentation. Catlike Coding's Pseudorandom Noise series by Jasper Flick is the most thorough implementation walkthrough for C#/Unity developers, covering per-octave seed offsetting and the generic turbulence wrapper pattern that other tutorials omit.

**GDC talks and GPU references.** Ryan Geiss's GPU Gems 3 Chapter 1 (NVIDIA) is the definitive reference for real-time volumetric terrain using 3D density functions with domain warping and hand-painted macro control textures — no other source covers the full pipeline from density function to marching cubes with that level of production detail. Media Molecule's GDC presentations on *Dreams* document the largest-scale production use of polynomial smooth minimum for SDF-based sculpting.

**Interactive tools.** Shadertoy (shadertoy.com) hosts Quilez's reference implementations — MdX3Rr (fBm terrain with derivatives), 4s23zz (domain warping with coloring), Xds3zN (SDF primitive gallery) — where every formula can be modified and re-rendered in real time. FastNoiseLite (github.com/Auburn/FastNoiseLite) is the most widely used multi-language noise library, implementing fBm, ridged multifractal, and domain warping with consistent APIs across C, C++, C#, Java, JavaScript, Rust, Go, GLSL, and HLSL.

**Repositories.** Musgrave's original `musgrave.c` (hosted at engineering.purdue.edu/~ebertd/texture/) is the only machine-readable primary source for the ridged multifractal and hybrid multifractal algorithms as Musgrave defined them — all other implementations are derived from or verified against this file. The Minecraft Wiki's noise router documentation (minecraft.wiki) provides the most detailed public documentation of a shipping AAA game's complete noise composition pipeline, including spline remapping, 5D biome selection, and density function DAG architecture.

---

## Conclusion

The six techniques in this pass form a compositional grammar built on a single principle: raw noise is a material, not a product. fBm transforms noise into naturalistic multi-scale signals by exploiting the spectral self-similarity that real geological processes produce. Ridged multifractal and turbulence each modify fBm's summation loop with a single nonlinearity — `offset − |signal|` and `|signal|` respectively — but the consequences diverge sharply: heterogeneous mountain terrain versus billowy, cusp-ridden textures. Domain warping escapes the statistical regularity inherent to any noise function by feeding noise back into its own coordinate space, producing organic forms that no parameter adjustment can replicate. Scalar field composition provides the algebraic operators — lerp, multiply, smoothstep, spline — that combine these individual fields into a unified world with biomes, transitions, and artist control. SDFs complete the toolkit by contributing geometric precision where noise offers only statistical texture: crisp Boolean operations, smooth shape blending with quantified blend radii, and exact distance queries for downstream systems.

The deepest insight for implementation is that **these techniques compose recursively**. Domain warping feeds fBm into fBm; ridged multifractal feeds one octave's signal into the next octave's amplitude; scalar field composition feeds SDF masks into noise blending weights. The developer's primary task is not implementing any one technique — each is straightforward — but understanding which compositions produce which visual signatures, and building a pipeline where these compositions can be authored, previewed, and iterated upon quickly. The data structures leaving this pass (scalar fields, gradient fields, distance fields, classification maps) are the inputs to every downstream system — terrain meshing, biome assignment, erosion simulation, object placement — making the quality and controllability of this compositional layer the single largest determinant of a procedural world's visual identity.