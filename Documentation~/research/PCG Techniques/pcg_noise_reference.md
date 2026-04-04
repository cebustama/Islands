# PCG Noise & Randomness Foundations — Technical Reference
### Research Pass 1: Noise, Scalar Fields, and Randomness

---

## Table of Contents

1. [Technique Cards](#technique-cards)
   - [Seeded PRNG & Coordinate Hashing](#1-seeded-prng--coordinate-based-hashing)
   - [Weighted Random Selection & Shuffle Bags](#2-weighted-random-selection--shuffle-bags)
   - [Poisson Disk Sampling](#3-poisson-disk-sampling)
   - [Stratified & Blue Noise Sampling](#4-stratified--blue-noise-sampling)
   - [Value Noise](#5-value-noise)
   - [Perlin Noise (Classic & Improved)](#6-perlin-noise-classic--improved)
   - [Simplex Noise & OpenSimplex](#7-simplex-noise--opensimplex)
   - [Worley / Voronoi Noise](#8-worley--voronoi-noise)
   - [Fractal Brownian Motion (fBm)](#9-fractal-brownian-motion-fbm)
   - [Ridged Multifractal Noise](#10-ridged-multifractal-noise)
   - [Turbulence](#11-turbulence)
   - [Domain Warping](#12-domain-warping)
   - [Layered Masks & Scalar Field Composition](#13-layered-masks--scalar-field-composition)
   - [Signed Distance Fields in PCG](#14-signed-distance-fields-in-pcg)
2. [Comparisons](#comparisons)
3. [Data Structures Produced](#data-structures-produced)
4. [Source Landscape](#source-landscape)

---

## TECHNIQUE CARDS

---

### 1. Seeded PRNG & Coordinate-Based Hashing

**Category:** Foundational Randomness / Noise Infrastructure

#### Core Purpose
Traditional sequential PRNGs (like Mersenne Twister or `rand()`) produce a stream of numbers that must be consumed in order. This is fine for a single sequence, but breaks when you need unordered access, deterministic parallelism, or the ability to jump to a position in the sequence. Coordinate-based hashing solves this by treating each (x, y, z, seed) tuple as a *direct input* to a hash function — like looking up a value in an infinitely large table of pre-generated random numbers. The critical property is that the same inputs always return the same output, with no state.

#### Typical Game-Dev Use Cases
- Seeding per-chunk or per-tile procedural generation deterministically
- Generating random loot/enemies for a location without sequential dependency
- Network-safe generation (server and client generate identical content independently)
- Record/playback and save-state support
- Lock-free parallel world generation

#### Inputs and Outputs
- **Input:** One or more integer coordinates (x, y, z, w) and an optional integer seed
- **Output:** A 32-bit (or 64-bit) unsigned integer of well-scrambled bits, convertible to float [0,1] or [-1,1]

#### Core Algorithm — SquirrelNoise5

The canonical modern implementation is Squirrel Eiserloh's SquirrelNoise5, presented at GDC 2017 ("Math for Game Programmers: Noise-Based RNG") and revised after Peter Schmidt-Nielsen identified a weakness in SquirrelNoise3.

The 1D base function (C-like pseudocode):

```cpp
uint32_t SquirrelNoise5(int32_t positionX, uint32_t seed) {
    constexpr uint32_t SQ5_BIT_NOISE1 = 0xd2a98b26; // crafted bit patterns
    constexpr uint32_t SQ5_BIT_NOISE2 = 0xa884f197;
    constexpr uint32_t SQ5_BIT_NOISE3 = 0x6C736F4B;
    constexpr uint32_t SQ5_BIT_NOISE4 = 0xB79F3ABB;
    constexpr uint32_t SQ5_BIT_NOISE5 = 0x1b56c4f5;

    uint32_t mangledBits = (uint32_t) positionX;
    mangledBits *= SQ5_BIT_NOISE1;
    mangledBits += seed;
    mangledBits ^= (mangledBits >> 9);
    mangledBits += SQ5_BIT_NOISE2;
    mangledBits ^= (mangledBits >> 11);
    mangledBits *= SQ5_BIT_NOISE3;
    mangledBits ^= (mangledBits >> 13);
    mangledBits += SQ5_BIT_NOISE4;
    mangledBits ^= (mangledBits >> 15);
    mangledBits *= SQ5_BIT_NOISE5;
    mangledBits ^= (mangledBits >> 17);
    return mangledBits;
}
```

**Step by step:**
1. Cast the signed integer position to unsigned
2. Multiply by a crafted constant to diffuse bits
3. Add the seed to introduce variation between worlds
4. Apply a sequence of XOR-shift operations interlaced with multiplications and additions
5. Each round further scrambles the relationship between input bits and output bits
6. Return the result — all input bits influence all output bits

**Multi-dimensional extension:** Hash coordinates down to a single integer before passing in. The standard approach encodes (x, y) as `x + y * LARGE_PRIME` or `Get2dNoiseUint(x, y, seed)` which uses: `Get1dNoiseUint(x + (PRIME * y), seed)`.

**Float conversion:** `(float)result * (1.0f / 0xFFFFFFFF)` maps to [0,1]. For [-1,1], map to `(float)result * (2.0f / 0xFFFFFFFF) - 1.0f`.

#### Variants
- **SquirrelNoise3 (original GDC 2017):** Uses constants `0xb5297a4d`, `0x68e31da4`, `0x1b56c4e9`. Had a weakness where high position values caused repetition because high input bits didn't influence low output bits enough.
- **SquirrelNoise5:** Revised with 5 constants and stronger diffusion. Ensured worst-case bit influence is ~49.99% (essentially ideal).
- **xxHash / MurmurHash:** Cryptographic-lineage hashes used for similar purposes; higher quality but slower. Generally overkill for game PCG.
- **Integer lattice hashing (classic):** Many older noise implementations use `perm[x & 255]` lookup tables (Perlin's original approach). Deterministic but limited period (256) without seeding.

#### Strengths
- O(1) per sample, fully parallelizable
- No state to save/restore — only inputs needed
- Trivially seedable for separate independent streams
- Extends to arbitrary dimensions with no quality penalty
- Very fast (handful of multiply/XOR operations)

#### Weaknesses
- SquirrelNoise5 produces integers, not gradients — it's the *substrate*, not a complete noise function
- Multi-dimensional coordinate collisions are theoretically possible (though rare with good primes)
- Not cryptographically secure

#### Failure Modes / Common Artifacts
- Using weak constants → visible bit patterns or periodicities (the SquirrelNoise3 issue)
- Poor dimensional encoding (e.g., simply adding x+y) → diagonal symmetry
- Integer overflow mishandled in languages without unsigned arithmetic → incorrect results

#### Combinations
Serves as the foundation for every other technique in this document. Value noise, Perlin noise, and Worley noise all require a per-lattice-point random value — SquirrelNoise provides that in a stateless, seedable form.

#### 2D vs 3D / Grid vs Continuous
Intrinsically grid-based. The output is a single value at an integer lattice point. Continuous-domain noise is constructed *on top* of this by interpolating between lattice values.

#### Performance Notes
SquirrelNoise5 is documented as being competitive with Mersenne Twister in throughput while offering superior properties. Benchmarks vary by platform; expect 2–5 ns per 1D sample on modern x86. Multi-dimensional versions pay a small additional multiply per extra dimension.

#### Best Sources
- Eiserloh, Squirrel. "Math for Game Programmers: Noise-Based RNG." GDC 2017. [GDC Vault](https://www.gdcvault.com/play/1024365/Math-for-Game-Programmers-Noise)
- SquirrelNoise5 source: [GitHub Gist](https://gist.github.com/kevinmoran/0198d8e9de0da7057abe8b8b34d50f86)
- Go port with documentation: [squirrelnoise5-go](https://github.com/miltoncandelero/squirrelnoise5-go)

---

### 2. Weighted Random Selection & Shuffle Bags

**Category:** Discrete Randomness / Distribution Control

#### Core Purpose
Uniform random selection from a set gives every item the same probability regardless of desired frequency. Weighted selection allows designer control over relative probability. Shuffle bags solve a separate problem: preventing streaks (getting the same item 5 times in a row from a "10% chance" pool), which are statistically expected from true random but feel unfair to players.

#### Typical Game-Dev Use Cases
- Loot tables (rare items with low weight, common items with high weight)
- Enemy spawn pools
- Music track selection (no repeats)
- Tile variant selection
- Ability proc systems that "feel" fair

#### Inputs and Outputs
**Weighted selection:**
- Input: A set of items with associated weights (unnormalized probabilities); a random float in [0,1]
- Output: One selected item

**Shuffle bag:**
- Input: A set of items (optionally with counts); a request for one item
- Output: One item; internal state (remaining items in bag)

#### Core Algorithm

**Weighted Random — Linear Scan:**
```
total_weight = sum of all weights
r = random() * total_weight
cumulative = 0
for each item i with weight w[i]:
    cumulative += w[i]
    if r <= cumulative:
        return item[i]
```
O(n) per query. Correct and simple.

**Weighted Random — Alias Method (Vose, 1991):**
O(1) per query after O(n) preprocessing. Builds two arrays: `Prob[]` and `Alias[]`. Each "slot" represents one item and may contain a second alias item. A query picks a slot uniformly, then flips a biased coin to return the primary or alias item.

Setup:
1. Normalize weights to n (multiply each by n/total_weight)
2. Partition items into "underfull" (weight < 1) and "overfull" (weight ≥ 1) lists
3. Repeatedly pair one underfull item with one overfull item, filling the underfull slot and placing the overflow in the alias. Adjust the overfull item's remaining weight.

Query:
```
i = floor(random() * n)
r = random()
if r < Prob[i]: return Primary[i]
else: return Alias[i]
```

**Shuffle Bag:**
```
bag = all items (with multiplicity if needed)
shuffle(bag) // Fisher-Yates
index = 0

function draw():
    if index >= len(bag):
        shuffle(bag)
        index = 0
    item = bag[index]
    index++
    return item
```
Fisher-Yates shuffle: iterate backward through the array, swapping each element with a randomly chosen earlier element (inclusive of itself).

#### Variants
- **Acceptance-rejection sampling:** Generate a candidate and accept/reject based on probability. Simple but inefficient for highly non-uniform distributions.
- **Inverse CDF:** Precompute a cumulative distribution function, binary search with random sample. O(log n) per query, works well for large tables.
- **Deck of cards (pure shuffle bag):** Bag contains exactly one of each item; reshuffle when empty. Guarantees each item appears exactly once per cycle.
- **Biased deck:** Items appear multiple times in the deck proportional to desired frequency. Combines weighted and shuffle-bag properties.

#### Strengths
- Gives designers direct control over feel, not just probability
- Shuffle bag eliminates the frustrating "dry spells" inherent to pure random
- Alias method achieves O(1) lookup with minimal memory

#### Weaknesses
- Shuffle bag has memory (earlier results affect later ones) — affects statistical independence assumptions
- Players may learn the pattern if bag is too small
- Weighted selection without seeding is not reproducible across runs

#### Failure Modes
- Normalizing weights as integers loses precision with many small-weight items
- Shuffle bag with a single item endlessly returns that item on each reshuffle — add a guard
- Alias method with weights of zero requires special handling

#### Combinations
Pairs with seeded hashing to make draw results deterministic per seed. Often used alongside Poisson disk sampling (sample a weighted distribution at spatially-distributed points).

#### Performance Notes
Alias method preprocessing is O(n), queries are O(1). Fisher-Yates shuffle is O(n). For most game use-cases (loot tables of < 100 items), linear scan is fine.

#### Best Sources
- Vose, Michael D. "A Linear Algorithm for Generating Random Numbers with a Given Distribution." IEEE Transactions on Software Engineering, 1991.
- Darts, Dice, and Coins: [keithschwarz.com](https://www.keithschwarz.com/darts-dice-coins/) — the clearest explanation of the Alias method
- Eiserloh GDC 2017 (above) — discusses weighted RNG in the context of noise-based systems

---

### 3. Poisson Disk Sampling

**Category:** Point Distribution / Spatial Sampling

#### Core Purpose
Purely random point placement clusters excessively — large empty areas and dense clumps appear simultaneously. Poisson disk sampling enforces a minimum separation distance `r` between any two points while keeping placement random within that constraint. The result is a "blue noise" distribution: no clumping, no excessive regularity, but no discernible pattern either.

#### Typical Game-Dev Use Cases
- Tree/rock/vegetation placement in terrain
- Enemy spawn points that don't stack on top of each other
- Collectible distribution
- Texture/decal placement
- Sampling for rendering (anti-aliasing, ambient occlusion)
- Dungeon room center generation (separation constraint)

#### Inputs and Outputs
- **Input:** Domain dimensions (width × height), minimum distance `r` between samples, rejection constant `k` (default 30)
- **Output:** A list of 2D (or nD) points satisfying the minimum distance constraint; approaches maximum packing density

#### Core Algorithm — Bridson's Fast Poisson Disk Sampling (SIGGRAPH 2007)

Published by Robert Bridson and runs in O(n) time for n output samples.

**Step 0 — Initialize grid:**
Create a background grid with cell size `r / sqrt(2)` (for 2D). This guarantees each cell contains at most one sample. Store sample indices (−1 = empty).

**Step 1 — First sample:**
Choose an initial sample x₀ randomly from the domain. Add it to the grid and to the **active list** (a queue of samples that might still generate neighbors).

**Step 2 — Main loop:**
While the active list is not empty:
1. Pick a random sample `xᵢ` from the active list
2. Generate up to `k` candidate points, each chosen uniformly from the annulus between radius `r` and `2r` around `xᵢ`
3. For each candidate `c`:
   - Check that `c` is within domain bounds
   - Check that no existing sample within distance `r` exists (examine only the ~5–9 nearby grid cells, not all samples)
   - If valid: add `c` to the grid and active list
4. If no valid candidate found after `k` tries: remove `xᵢ` from the active list

**Step 3:** Active list is empty. Return all samples.

**Annulus sampling** (uniform distribution within annulus): Generate angle θ uniformly in [0, 2π] and radius ρ uniformly in [r, 2r], giving point `(xᵢ.x + ρ·cos θ, xᵢ.y + ρ·sin θ)`.

#### Variants
- **Dart throwing (naive):** Generate random points; reject those within `r` of existing ones. O(n²) or worse. Produces maximal Poisson disk if run long enough but impractically slow.
- **Mitchell's Best Candidate:** Generate `k` random candidates per sample, keep the one farthest from existing samples. O(n·k). Produces good results but not true Poisson disk.
- **Tiled Poisson Disk:** Precompute multiple tiles of Poisson disk distributions; stamp them across a domain. Constant-time per region but introduces subtle repetition.
- **Variable-radius Poisson disk:** Minimum distance `r` varies across the domain (e.g., denser near paths). Implemented by checking the `r` at both the sample and candidate positions.
- **Improved Bridson (Extreme Learning, 2019):** Modified candidate generation within the annulus to produce more tightly packed, denser distributions; up to 20× faster by reducing failed rejections.
- **Weighted sample elimination (Cem Yuksel):** Start with random oversampling, then eliminate points that violate minimum distance. More flexible, no need to specify radius upfront.

#### Strengths
- O(n) runtime (Bridson's version)
- Produces visually natural "organic" distributions
- Easily adapts to non-rectangular domains via `in_area` callback
- No memory artifacts across tiles (unlike regular grids)

#### Weaknesses
- Offline algorithm — does not stream one point at a time
- Requires knowing domain size upfront
- For runtime generation, must accept some startup cost
- Tiled variants introduce low-frequency patterns at tile borders

#### Failure Modes
- Too-small `k` (< 10) causes sparse coverage — the algorithm terminates too early
- Grid cell size too large → fails the "at most one sample per cell" invariant → correctness breaks
- Radius `r` specified as world-space units but domain queried in tile-space → scale mismatch → either empty or too-dense

#### Combinations
- Use a seeded PRNG for the random number stream → reproducible Poisson disk per seed
- Use as input to Worley noise (place feature points via Poisson disk for more uniform cell sizes)
- Use variable-radius version with a noise mask to control density (denser forests in certain biomes)

#### 2D vs 3D
The algorithm generalizes to arbitrary dimensions. In nD, grid cell size is `r / sqrt(n)`. The `k` annulus candidates are drawn from the n-ball shell. In 3D, check 27 neighboring grid cells instead of 9.

#### Performance Notes
- Bridson's algorithm: O(n) time and space for n output points
- Practical runtime: milliseconds for thousands of points in 2D
- For streaming (procedural chunk generation), precompute tiles or run on a separate thread

#### Best Sources
- Bridson, Robert. "Fast Poisson Disk Sampling in Arbitrary Dimensions." SIGGRAPH 2007. [PDF](https://www.cs.ubc.ca/~rbridson/docs/bridson-siggraph07-poissondisk.pdf)
- Step-by-step tutorial: [sighack.com](https://sighack.com/post/poisson-disk-sampling-bridsons-algorithm)
- Improved variant: [extremelearning.com.au](https://extremelearning.com.au/an-improved-version-of-bridsons-algorithm-n-for-poisson-disc-sampling/)
- Interactive visualization: [jasondavies.com/poisson-disc](https://www.jasondavies.com/poisson-disc/)

---

### 4. Stratified & Blue Noise Sampling

**Category:** Sampling Theory / Point Distribution

#### Core Purpose
A unified framing for a family of techniques that reduce clumping and improve coverage in sample distributions. *Stratification* divides the domain into a regular grid of strata and places one sample per stratum. *Blue noise* is the frequency-domain characterization of distributions (like Poisson disk) that suppress low-frequency clustering. Both aim to outperform purely random (white noise) sampling, which has equal power at all frequencies.

#### Typical Game-Dev Use Cases
- Anti-aliasing sample patterns
- Shadow map sample kernels
- Ambient occlusion ray generation
- Randomized point distributions for decals or particles
- Procedural tile variation (one variant per grid stratum, randomly chosen within)

#### Inputs and Outputs
**Stratified:**
- Input: Domain, number of strata (n × m grid), one RNG per stratum
- Output: n×m points, each jittered within its stratum

**Blue noise:**
- Input: Target frequency spectrum (characterized by minimum distance `r`)
- Output: Point set whose power spectrum has minimal low-frequency content

#### Core Algorithm

**Stratified Jittered Sampling (2D):**
1. Divide [0,1]² into n×m cells of size (1/n × 1/m)
2. For cell (i, j): generate sample at (i/n + rand()/n, j/m + rand()/m)
3. Result: guaranteed one sample per cell, but random within cell

**Latin Hypercube Sampling:**
1. Divide each axis into n strata
2. Place one sample per stratum on each axis, but shuffle the stratum assignments across axes
3. Guarantees marginal uniformity in each dimension, not joint coverage

**Blue noise via Poisson disk:** Poisson disk distributions (see above) are the canonical form of blue noise. Their power spectrum shows a "void" at low frequencies.

**Void-and-cluster blue noise (Ulichney, 1988):** A precomputed threshold matrix where samples are placed in order of cluster priority. Used for ordered dithering and rendering pipelines, less directly for PCG.

#### Variants
- **Halton sequences / Sobol sequences:** Quasi-random (low-discrepancy) sequences. Provide uniform coverage but are deterministic rather than random — appropriate for integration, not organic-looking PCG.
- **Jittered grid:** Stratified sampling without shuffle; most common in shaders.
- **Multi-jittered sampling (Chiu, Shirley, Wang 1994):** Combines stratified and Latin hypercube properties.

#### Strengths
- Superior variance reduction vs. uniform random for Monte Carlo integration
- Visually more even distributions
- Predictable maximum gap size

#### Weaknesses
- Stratified approaches require knowing sample count upfront
- True blue noise generation (Poisson disk) is comparatively expensive
- Quasi-random sequences are not suited for content that needs to look random

#### Failure Modes
- Using stratified sampling with too few strata → no improvement over uniform random
- Mistaking low-discrepancy sequences for random → non-random, structured appearance in spatial content

#### Combinations
Poisson disk sampling is the game-dev manifestation of blue noise. Stratified grids are used inside shaders to improve per-pixel sampling quality. Combine stratified initial seeds with hash-based per-cell random variation for simple organic tile placement.

#### Best Sources
- Shirley, Peter. "Nonuniform Random Point Sets via the Retraction Map." Computer Graphics Forum, 1991.
- Pharr, Matt & Humphreys, Greg. "Physically Based Rendering" — extensive treatment of sampling theory
- Ulichney, Robert. "Dithering with Blue Noise." Proc. IEEE, 1988.

---

### 5. Value Noise

**Category:** Gradient-Free Continuous Noise

#### Core Purpose
The simplest way to convert discrete random hash values at integer lattice points into a smooth continuous signal. Assign a random scalar to each lattice corner, then interpolate between corners. Computationally cheap; requires no gradient computation.

#### Typical Game-Dev Use Cases
- Low-cost noise where quality artifacts are acceptable (e.g., distant terrain, textures viewed at angle)
- Noise in shaders where gradient noise is too expensive
- Roughness/metalness variation in material shaders
- Simple cloud/fog masks
- Educational baseline before implementing Perlin noise

#### Inputs and Outputs
- **Input:** Continuous coordinate (x [,y [,z]])
- **Output:** Scalar float in approximately [−1, 1] (exact range depends on interpolant)

#### Core Algorithm (2D)

```
function valueNoise(x, y):
    ix = floor(x); iy = floor(y)
    fx = fract(x); fy = fract(y)

    // Smoothstep (quintic preferred):
    ux = fade(fx); uy = fade(fy)

    // Random values at 4 corners of unit cell:
    a = hash(ix,   iy)
    b = hash(ix+1, iy)
    c = hash(ix,   iy+1)
    d = hash(ix+1, iy+1)

    // Bilinear interpolation:
    return lerp(lerp(a, b, ux), lerp(c, d, ux), uy)
```

Where `hash()` returns a float in [0,1] via SquirrelNoise or equivalent, and `fade(t)` is:
- Cubic: `3t² - 2t³` (Hermite / smoothstep) — simple but has non-zero second derivatives at boundaries
- Quintic: `6t⁵ - 15t⁴ + 10t³` (Perlin's improved fade) — zero first *and* second derivatives at 0 and 1

In 3D, extend to trilinear interpolation over 8 corners.

#### Variants
- **Bicubic value noise:** Fit a cubic polynomial through lattice values in each dimension. Smoother than bilinear but more expensive.
- **Value noise with analytical derivatives:** Inigo Quilez derived analytical gradient formulas for smooth value noise, enabling gradient-based domain warping without finite differences.
- **Gradient value noise:** Hybrid between value and gradient noise — use hash for gradient direction rather than pure scalar. Closer to Perlin noise.

#### Strengths
- Simpler to implement than Perlin noise (no gradient computation)
- Fast: just hash + interpolation
- Trivially seedable

#### Weaknesses
- **Block artifacts / blobby appearance:** Interpolation between scalar corners produces visible axis-aligned "blobs" at the grid scale. This is value noise's most notorious defect.
- Axis-aligned anisotropy: patterns tend to align with x/y axes
- Lower visual quality per octave compared to gradient noise

#### Failure Modes
- Using linear interpolation (lerp without smoothstep) → visible grid lines at cell boundaries (C⁰ continuity only)
- Hash values not uniformly distributed → biased brightness
- Low-frequency banding if hash period is too short (e.g., 256-element perm table with no seeding)

#### Combinations
- Foundation of fBm (summed octaves)
- Can replace Perlin noise in fBm stacks where quality differences are imperceptible at small scales
- Used as the `noise()` function inside Inigo Quilez's domain warping examples

#### 2D vs 3D
Direct extension to 3D by adding a third interpolation axis. Corner count doubles per dimension (2, 4, 8, 16 for 1D–4D). This makes value noise significantly cheaper than gradient noise in high dimensions because the dot product (gradient evaluation) is replaced by a scalar lookup.

#### Performance Notes
In 2D: 4 hash calls + 3 lerps + 2 fade evaluations. Very fast. In GLSL/HLSL, sin-based hash substitutes allow purely computational value noise with no texture lookups.

#### Best Sources
- The Book of Shaders, Chapter 11: [thebookofshaders.com/11](https://thebookofshaders.com/11/)
- Inigo Quilez value noise with derivatives: [iquilezles.org/articles/morenoise](https://iquilezles.org/articles/morenoise/)
- Catlike Coding noise tutorial series (Jasper Flick): [catlikecoding.com/unity/tutorials/noise](https://catlikecoding.com/unity/tutorials/noise/)

---

### 6. Perlin Noise (Classic & Improved)

**Category:** Gradient Noise

#### Core Purpose
Ken Perlin invented gradient noise in 1983 (published 1985) while working on Tron at MAGI. It addresses value noise's blobs by placing random *gradient vectors* (not scalar values) at lattice corners, then computing a dot product between the gradient and the offset vector from corner to query point. The resulting noise is smoother, more isotropic, and lacks the blocky artifacts of value noise.

#### Typical Game-Dev Use Cases
- Height maps and terrain
- Cloud and fog textures
- Procedural textures (marble, wood, stone — often via fBm)
- Normal map generation
- Animated effects (fire, water surface)
- Mask generation for biome blending

#### Inputs and Outputs
- **Input:** Continuous coordinate (x [,y [,z [,w]]])
- **Output:** Scalar float in approximately [−1, 1]

#### Core Algorithm — Improved Perlin Noise (2002)

Perlin revised his original 1985 algorithm in 2002 with two key improvements: a quintic fade function (eliminating second-derivative discontinuities) and a simplified gradient selection scheme.

**Step 1 — Integer and fractional parts:**
```
ix = floor(x) & 255;  fx = x - floor(x)
iy = floor(y) & 255;  fy = y - floor(y)
iz = floor(z) & 255;  fz = z - floor(z)
```
The `& 255` wraps coordinates to the period-256 permutation table.

**Step 2 — Fade curves (quintic):**
```
u = fade(fx);  v = fade(fy);  w = fade(fz)
fade(t) = t * t * t * (t * (t * 6 - 15) + 10)
```

**Step 3 — Hash corners:**
For 3D, hash each of 8 corners of the unit cell using a 512-element permutation table `p[]` (a shuffled 0–255 repeated twice to avoid modular wraparound):
```
A  = p[ix] + iy;   AA = p[A] + iz;   AB = p[A+1] + iz
B  = p[ix+1] + iy; BA = p[B] + iz;   BB = p[B+1] + iz
```

**Step 4 — Gradient selection:**
Each hash result maps to one of 12 gradient vectors (the midpoints of a cube's edges): (±1,±1,0), (±1,0,±1), (0,±1,±1). The `grad(hash, x, y, z)` function computes the dot product of the selected gradient with the offset vector.

**Step 5 — Trilinear interpolation:**
```
lerp(w,
  lerp(v,
    lerp(u, grad(p[AA], fx, fy, fz),  grad(p[BA], fx-1, fy, fz)),
    lerp(u, grad(p[AB], fx, fy-1, fz), grad(p[BB], fx-1, fy-1, fz))),
  lerp(v,
    lerp(u, grad(p[AA+1], fx, fy, fz-1), grad(p[BA+1], fx-1, fy, fz-1)),
    lerp(u, grad(p[AB+1], fx, fy-1, fz-1), grad(p[BB+1], fx-1, fy-1, fz-1))))
```

#### Variants
- **Classic Perlin (1985):** Uses cubic Hermite fade `3t² - 2t³` and 16 gradient vectors. Produces second-derivative discontinuities at lattice points (visible in accelerating simulations).
- **Improved Perlin (2002):** Quintic fade, 12 gradients. Eliminates second-derivative discontinuities. Slightly more expensive fade but correct.
- **Tileable Perlin:** Use `(x % period)` in the permutation lookup to make the noise tile seamlessly.
- **Periodic Perlin (GPU-friendly):** Replace permutation table with a hash function; enables seeding.
- **Analytical derivative Perlin:** Compute ∂noise/∂x, ∂noise/∂y analytically (not with finite differences). Used for physically-based erosion and domain warping.

#### Strengths
- Smooth, isotropic appearance (much better than value noise)
- Well-understood and extensively documented
- Hardware-friendly (efficient GLSL/HLSL implementations exist)
- Tileable variant available
- Analytical derivatives computable

#### Weaknesses
- **Axis-aligned directional artifacts:** The 12-gradient scheme (and especially the classic 16-gradient scheme) has slight preferential alignment along diagonal axes. Visible in high-octave fBm as faint grid-aligned banding.
- Period 256 (must be doubled to 512 for the classic table approach without additional tricks)
- Gradients computed per cell, not per point — "lumpy" at lattice scale in low-octave use
- More complex to implement correctly than value noise

#### Failure Modes
- Using classic cubic fade → bumpy appearance at high zoom (second-derivative visible)
- Not doubling the permutation table → index overflow and artifacts at x=128 boundary
- Forgetting to normalize the gradient set → non-unit gradients distort amplitude
- Seeding by simply rotating the `perm[]` array → poor statistical properties; prefer hash-based gradients

#### Combinations
- fBm (canonical combination — see below)
- Ridged multifractal (abs + invert of Perlin)
- Turbulence (abs of Perlin)
- Domain warping input and output
- Layered with Worley noise for hybrid textures

#### 2D vs 3D
- 2D: 4 corners, bilinear interpolation. Common in terrain heightmaps.
- 3D: 8 corners, trilinear interpolation. Used for volumetric effects, 3D world textures, animated noise (use z = time).
- 4D: 16 corners. Used for tileable animated noise (time as 4th dimension with period wrapping).

#### Performance Notes
- CPU: ~20–50 ns per 3D sample (with table lookup). Much faster with SIMD.
- GPU: Excellent parallelism. McEwan & Gustavson's GLSL implementation achieves competitive performance without texture lookups (purely computational).
- 2D is roughly 2× faster than 3D due to halving the corner count.

#### Best Sources
- Perlin, Ken. "An Image Synthesizer." SIGGRAPH 1985. Original paper.
- Perlin, Ken. "Improving Noise." SIGGRAPH 2002. [Reference implementation](https://cs.nyu.edu/~perlin/noise/)
- Gustavson, Stefan. "Simplex Noise Demystified." 2005. (Covers classic Perlin before introducing simplex.) [PDF](https://cgvr.cs.uni-bremen.de/teaching/cg_literatur/simplexnoise.pdf)
- Catlike Coding: [catlikecoding.com/unity/tutorials/noise](https://catlikecoding.com/unity/tutorials/noise/)
- The Book of Shaders, Ch 11: [thebookofshaders.com/11](https://thebookofshaders.com/11/)

---

### 7. Simplex Noise & OpenSimplex

**Category:** Gradient Noise (Simplex-lattice)

#### Core Purpose
Ken Perlin designed simplex noise (2001) to address specific limitations of classic gradient noise, primarily: the O(2ⁿ) scaling with dimension (16 corners in 3D → 81 in 4D → 256 in 5D), visible directional artifacts from the hypercubic lattice, and the non-continuity of the gradient. Simplex noise uses a simplex lattice (triangles in 2D, tetrahedra in 3D) which has n+1 corners instead of 2ⁿ, dramatically reducing computation in high dimensions.

#### Typical Game-Dev Use Cases
- Same as Perlin noise, but preferred for 4D+ use cases
- Animated noise (using time as 3rd or 4th dimension)
- GPU shader noise (fewer multiplications)
- Terrain, clouds, water, procedural textures

#### Inputs and Outputs
- **Input:** Continuous coordinate (x [,y [,z [,w]]])
- **Output:** Scalar float in approximately [−1, 1]

#### Core Algorithm (2D Simplex)

**Four steps** as identified by Gustavson (2005) and Wikipedia:

**Step 1 — Coordinate skewing:**
Transform (x,y) to a skewed grid where the simplex lattice aligns with a regular grid. For 2D, skew factor F = (√3−1)/2:
```
s = (x + y) * F
xs = x + s;  ys = y + s
i = floor(xs);  j = floor(ys)
```

**Step 2 — Simplicial subdivision:**
Find internal fractional coordinates within the skewed cell:
```
t = (i + j) * G  // G = (3−√3)/6 (unskew factor)
X0 = i - t;  Y0 = j - t   // unskewed corner 0
x0 = x - X0;  y0 = y - Y0
```
Determine which simplex (upper or lower triangle) the point lies in:
```
if x0 > y0: (i1,j1) = (1,0)  // lower triangle
else:        (i1,j1) = (0,1)  // upper triangle
```

**Step 3 — Gradient selection:**
Hash each of the 3 simplex corners to select a gradient from a small set. In 2D, Gustavson uses 8 gradients at 45° intervals. The hash is computed with a permutation table (or hash function).

**Step 4 — Kernel summation:**
For each corner contribution, compute:
```
t = 0.5 - x_corner² - y_corner²
if t < 0: contribution = 0
else: contribution = t⁴ * dot(gradient, (x_corner, y_corner))
```
Sum contributions and scale to approximately [−1, 1].

The radially symmetric falloff kernel `t⁴` (or `t²`, depending on implementation) ensures smooth blending between corners without interpolation functions.

In **3D**, the process extends to a tetrahedral lattice with 4 corners. In **4D**, a pentachoron with 5 corners. Corner count is always n+1, not 2ⁿ.

#### OpenSimplex Noise

Kurt Spencer created OpenSimplex in 2014 as a patent-free alternative. The simplex noise patent (US 6,867,776) specifically covered the use of simplex noise for 3D and higher texture synthesis. It **expired January 8, 2022**, so the patent concern is now moot.

OpenSimplex uses a different lattice arrangement (the A*ₙ lattice rather than the standard simplex subdivision) and a different gradient set, producing slightly different visual characteristics — generally considered smoother with less "orientation bias" than the original simplex.

**OpenSimplex2 (2021, Kurt Spencer):** Further revised with two variants:
- `OpenSimplex2F` ("fast"): Simplex-based, optimized for 3D/4D
- `OpenSimplex2S` ("smooth"): Uses a larger kernel for smoother output, more like smoothed value noise

#### Variants
- **Simplex noise (Perlin 2001):** Original, patented (now expired). Canonical algorithm.
- **Gustavson's implementation:** Reference Java implementation; backported from GLSL.
- **McEwan & Gustavson GLSL (2012):** Purely computational (no textures), designed for GPU.
- **OpenSimplex (Spencer 2014):** Patent-free alternative, slightly different visual character.
- **OpenSimplex2 (Spencer 2021):** Modern, well-maintained, recommended for new projects.

#### Strengths
- No directional artifacts (visually isotropic)
- O(n+1) corner evaluations vs O(2ⁿ) for Perlin
- Continuous gradient everywhere (almost — discontinuities exist but at lattice vertices, not boundaries)
- Well-defined, cheap analytical gradient
- Patent expired; OpenSimplex2 available for existing constraints

#### Weaknesses
- More complex to implement correctly than Perlin noise
- Visual character is slightly different from Perlin — not a drop-in replacement without tuning frequency
- OpenSimplex2 may produce slightly different output than original simplex

#### Failure Modes
- Incorrect unskew factor → distorted simplex geometry → artifacts
- Wrong kernel radius (t threshold) → missing contributions or over-counting
- Integer coordinate overflow at large world positions (use 64-bit indices for large worlds)

#### Combinations
Same as Perlin noise. Interchangeable in fBm, ridged multifractal, domain warping pipelines.

#### 2D vs 3D
The simplex lattice advantage is most pronounced in 3D+ (4 corners vs 8 for Perlin) and 4D+ (5 corners vs 16). In 2D, the advantage is smaller (3 corners vs 4 for Perlin), though the gradient set improvement still matters.

#### Performance Notes
- 2D simplex: ~15–20% faster than 2D Perlin in typical implementations
- 3D simplex: ~30–40% fewer operations than 3D Perlin
- 4D: The advantage is dramatic (~5 corners vs 16)
- GPU: McEwan/Gustavson's purely-computational GLSL is competitive with texture-based implementations

#### Best Sources
- Gustavson, Stefan. "Simplex Noise Demystified." 2005. [PDF](https://cgvr.cs.uni-bremen.de/teaching/cg_literatur/simplexnoise.pdf) ← Primary algorithmic reference
- Wikipedia: "Simplex Noise" — clear step-by-step with formulas
- OpenSimplex2: [github.com/KdotJPG/OpenSimplex2](https://github.com/KdotJPG/OpenSimplex2)
- McEwan, Gustavson et al. "Efficient Computational Noise in GLSL." arxiv.org/abs/1204.1461
- The Book of Shaders, Ch 11: [thebookofshaders.com/11](https://thebookofshaders.com/11/)

---

### 8. Worley / Voronoi Noise

**Category:** Distance-Field Noise / Cellular Noise

#### Core Purpose
Instead of interpolating random values or gradients at lattice corners, Worley noise computes distances from a query point to a set of randomly scattered *feature points*. The result looks like cell-like, organic structures — reptile scales, stone cracking patterns, water surface cells, lava, foam. Steven Worley introduced it in 1996 as "A Cellular Texture Basis Function."

#### Typical Game-Dev Use Cases
- Stone, tile, cracked earth, ice textures
- Biological cell patterns (skin, scales, coral)
- Water caustics approximation
- Cave system masks (F2-F1 → tunnels)
- Voronoi region coloring for territory/zone maps
- Adding organic detail to fBm terrain
- Generating city block patterns

#### Inputs and Outputs
- **Input:** Continuous coordinate (x [,y [,z]]); jitter factor (0=regular grid, 1=fully random)
- **Output:** F1 (distance to nearest feature point), F2 (distance to second-nearest), or combinations thereof

#### Core Algorithm

**Step 1 — Grid of feature points:**
Divide space into a regular grid of cells. In each cell, place one feature point at a random position within the cell. The random offset is generated by hashing the cell's integer coordinates. The `jitter` parameter scales this offset:
```
feature_point(cellX, cellY) = vec2(cellX, cellY) + jitter * hash2d(cellX, cellY)
```

**Step 2 — Find nearest feature points:**
For a query point `p`, identify its cell and search the 3×3 neighborhood of cells (9 total in 2D; 27 in 3D):
```
ix = floor(p.x);  iy = floor(p.y)
F1 = F2 = INFINITY

for dy in [-1, 0, 1]:
    for dx in [-1, 0, 1]:
        cellX = ix + dx;  cellY = iy + dy
        fp = feature_point(cellX, cellY)
        d = distance(p, fp)  // or Manhattan, Chebyshev, etc.
        if d < F1:
            F2 = F1;  F1 = d
        elif d < F2:
            F2 = d
```
The 3×3 search is sufficient because with jitter ≤ 1, no feature point can be farther than 2 cells from the query point's cell. Gustavson's 2011 GLSL optimization reduces this to 2×2 for F1, with minor artifacts at cell edges.

**Step 3 — Compute output:**
- `F1`: smooth cell interiors, value 0 at feature point
- `F2`: more complex cells
- `F2 - F1`: highlights Voronoi edges (value ≈ 0 on boundary, positive inside)
- `F1 * F2`: product often produces interesting intermediate patterns
- Weighted combinations of F1..F4 produce Worley's original "approximately 40 basis functions"

**Distance metrics:**
- Euclidean: `sqrt(dx² + dy²)` — smooth, natural
- Manhattan: `|dx| + |dy|` — diamond-shaped cells, blocky
- Chebyshev: `max(|dx|, |dy|)` — square cells
- Custom metrics: `|dx|^p + |dy|^p` — intermediate shapes

#### Variants
- **Worley noise (original 1996):** Used a Poisson distribution of feature points (variable density per cell), handled with early exit logic. More computationally expensive; one feature point per cell is the modern simplification.
- **2×2 GLSL variant (Gustavson 2011):** Search only 4 neighboring cells in 2D. ~2× faster, minor discontinuities at far cell borders.
- **Voronoi diagram:** Discrete coloring — assign each cell a random color based on the ID of the nearest feature point. Produces polygonal regions.
- **Smooth Voronoi:** Replace hard nearest-neighbor assignment with a smooth blend using `exp(-k*d)` weighting. No hard boundaries.
- **Layered/fractal Worley:** Sum octaves of Worley noise: `G(x) = Σ 2⁻ⁱ F₁(2ⁱx)`. Adds fine cellular detail.
- **Generalized Worley (Ian Henry):** Replace point-SDFs with other SDF primitives (squares, hexagons). Creates structured cellular noise.

#### Strengths
- Visually distinctive — complementary to gradient noise
- F2-F1 naturally extracts edges
- Easily parameterized (jitter, distance metric)
- GPU-friendly (no table lookups, purely computational)

#### Weaknesses
- Sharp discontinuities in F1 (not everywhere differentiable — only smooth except on Voronoi edges)
- Jitter = 1 is maximum useful value; beyond 1, cells begin to "steal" space from adjacent cells
- 3×3 search is expensive in 3D (27 cells); 2×2 optimization introduces artifacts

#### Failure Modes
- Jitter = 0 → perfect grid, all structure is regular — usually not desired
- F2-F1 with F2 undefined (only 1 neighbor found) → undefined output; ensure sufficient search radius
- Low jitter values → hexagonal grid-like appearance
- Using Manhattan/Chebyshev distance without expecting non-smooth Euclidean cells

#### Combinations
- Use Worley F1 as a mask to modulate Perlin/fBm
- Combine Worley and Perlin via addition or multiplication for hybrid textures
- Worley cells as biome seeds (each cell = one biome zone)
- Ridged Worley: `1 - F1` → raised cell centers, sunken edges

#### 2D vs 3D
Extends directly. 2D: 9-cell search. 3D: 27-cell search. Distance metrics behave identically. 3D Worley is commonly used for volumetric effects (clouds, smoke texture, subsurface scattering).

#### Performance Notes
2D Worley with 3×3 search: 9 hash evaluations + 9 distance computations per sample. Faster than Perlin with gradient evaluation but not drastically so. In shaders, the 2×2 Gustavson variant cuts the work roughly in half.

#### Best Sources
- Worley, Steven. "A Cellular Texture Basis Function." SIGGRAPH 1996. ← Primary paper
- The Book of Shaders, Ch 12: [thebookofshaders.com/12](https://thebookofshaders.com/12/)
- Gustavson, Stefan. "Cellular Noise in GLSL Implementation Notes." 2011. [PDF](https://itn-web.it.liu.se/~stegu76/GLSL-cellular/GLSL-cellular-notes.pdf)
- Generalized Worley: [ianthehenry.com/posts/generalized-worley-noise](https://ianthehenry.com/posts/generalized-worley-noise/)
- Inigo Quilez Voronoi articles: [iquilezles.org/articles/voronoise](https://iquilezles.org/articles/voronoise)

---

### 9. Fractal Brownian Motion (fBm)

**Category:** Fractal Composition

#### Core Purpose
A single octave of noise has a characteristic frequency and lacks detail at other scales. Real natural phenomena (terrain, clouds, turbulence) exhibit self-similarity across many scales — the coastline at 1km looks statistically similar to a 1m segment. fBm achieves this by summing multiple octaves of the same noise function, each octave at a higher frequency and lower amplitude. This produces a signal whose power spectrum follows a power law (1/fᵝ).

#### Typical Game-Dev Use Cases
- Terrain heightmaps (the canonical use)
- Cloud/fog density fields
- Procedural marble/wood grain textures
- Water surface height
- Procedural color variation in foliage/rock
- Mask generation with naturalistic shapes

#### Inputs and Outputs
- **Input:** Continuous coordinate `p`; octave count `N`; lacunarity `L` (frequency multiplier per octave, typically 2.0); gain `G` (amplitude multiplier per octave, typically 0.5); base noise function
- **Output:** Scalar float, range approximately [−1, 1] after normalization

#### Core Algorithm

```
function fbm(p, octaves, lacunarity, gain):
    value = 0.0
    amplitude = 0.5        // start amplitude (contributes half weight)
    frequency = 1.0        // start frequency
    maxAmplitude = 0.0     // for normalization

    for i in 0..octaves:
        value += amplitude * noise(p * frequency)
        maxAmplitude += amplitude
        amplitude *= gain
        frequency *= lacunarity

    return value / maxAmplitude  // normalize to [−1, 1]
```

The geometric series of amplitudes `{0.5, 0.25, 0.125, ...}` with gain = 0.5 corresponds to a Hurst exponent H = 1, which Inigo Quilez identifies as the standard for terrain (G = 2⁻ᴴ → G = 0.5). This produces a −9dB/octave power spectrum ("yellow noise" / "brown noise"), which matches empirical measurements of real mountain profiles.

**Domain rotation (Quilez optimization):**
Applying a small rotation matrix to `p` between octaves (e.g., 45°) prevents octave frequency peaks from aligning, reducing visible patterns:
```
mat2 rot = mat2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
p = rot * p * lacunarity + shift;
```

**Parameter guide:**
- `lacunarity`: Controls rate of frequency increase. 2.0 = each octave is exactly one musical "octave" higher. Values 1.8–2.2 add subtle variation.
- `gain` (persistence): Controls how quickly high-frequency detail diminishes. 0.5 = natural. > 0.5 = noisy/rough. < 0.5 = smooth.
- `octaves`: More octaves = more fine detail, with diminishing returns. 6–8 octaves typically sufficient before the noise becomes imperceptible.

#### Variants
- **Heterogeneous fBm (Musgrave):** Amplitude of each octave is modulated by the current accumulated value. Produces self-reinforcing roughness — high areas get rougher, low areas get smoother.
- **Multifractal fBm:** Amplitude at each octave is the *product* of previous contributions. Creates extreme local variation.
- **Hybrid multifractal (Musgrave/Bryce):** Combines additive and multiplicative octave weighting for terrain that has both smooth valleys and rough ridges.
- **Spectral synthesis:** Build the power spectrum directly in frequency domain; inverse FFT. Produces true fBm statistics but is non-local and requires tileability constraints.

#### Strengths
- Produces visually convincing natural-looking detail across scales
- Fully parameterized — octaves, lacunarity, gain give precise control
- Works with any underlying noise function
- Self-similar: changing scale doesn't change the statistical character

#### Weaknesses
- Isotropy: standard fBm looks the same in all directions — real terrain has directionality (ridgelines, prevailing erosion direction)
- Cannot easily represent discontinuities or sharp features
- Amplitude normalization is approximate — actual output range varies with octave count

#### Failure Modes
- Too many octaves with gain ≥ 0.5 → noisy, incoherent output
- Lacunarity exactly 2.0 → octave frequencies are harmonically related → subtle but visible beating patterns. Use 2.01 or add slight rotation.
- Gain > 1.0 → each octave is *louder* than the last → signal diverges
- Not normalizing output → domain-warped fBm stacks can produce values well outside [−1, 1]

#### Combinations
- Applied to any base noise (value, Perlin, simplex, Worley)
- Foundation of ridged multifractal and turbulence (fBm variants)
- Used as the warp field in domain warping
- Modulated by a mask field (biome moisture → fBm roughness scale)

#### Performance Notes
fBm cost scales linearly with octaves. N octaves of Perlin 3D = N × (3D Perlin cost). GPU-friendly: loop can be unrolled for fixed octave count. The rotation matrix between octaves adds a 2×2 matrix multiply per octave — minimal overhead.

#### Best Sources
- Inigo Quilez, "fBm article": [iquilezles.org/articles/fbm](https://iquilezles.org/articles/fbm) ← Deep theoretical treatment including Hurst exponent
- The Book of Shaders, Ch 13: [thebookofshaders.com/13](https://thebookofshaders.com/13/)
- Musgrave, F. Kenton. "Procedural Fractal Terrains." In "Texturing and Modeling: A Procedural Approach" (3rd ed.), Chapter 16.
- Musgrave GDC-era notes: [classes.cs.uchicago.edu](https://www.classes.cs.uchicago.edu/archive/2015/fall/23700-1/final-project/MusgraveTerrain00.pdf)

---

### 10. Ridged Multifractal Noise

**Category:** Fractal Composition (nonlinear variant)

#### Core Purpose
Standard fBm produces relatively symmetric up-and-down variation. Real mountain ridges and rock formations are asymmetric: sharp peaks, rounded valleys. Ridged multifractal noise achieves this by taking the absolute value of each noise octave (creating a "V" shape — a crease — instead of a smooth sine-like curve) and inverting it (turning the V into a Λ — a ridge). It was formalized by F. Kenton Musgrave and appears in Bryce 4's terrain presets.

#### Typical Game-Dev Use Cases
- Mountain ridgelines
- Canyon/crack formations
- Volcanic landscapes
- Craggy rock surface textures
- Marble veining (inverted)

#### Inputs and Outputs
- **Input:** Continuous coordinate `p`; octaves; lacunarity; gain; offset (controls ridge height/sharpness)
- **Output:** Scalar float; by convention normalized but often unnormalized in implementations

#### Core Algorithm

Two levels of complexity: the simple "ridged fBm" approximation and Musgrave's full multifractal with weight feedback.

**Simple ridged noise (common in shaders):**
```
function ridgedFbm(p, octaves, lacunarity, gain, offset):
    value = 0.0
    amplitude = 0.5

    for i in 0..octaves:
        n = abs(noise(p))         // fold negative values → V-shapes
        n = offset - n            // invert so V becomes Λ (ridge)
        n = n * n                 // sharpen the ridge
        value += n * amplitude
        p *= lacunarity
        amplitude *= gain
    return value
```

**Musgrave's ridged multifractal (with weight feedback):**
```
function ridgedMultifractal(p, octaves, lacunarity, gain, offset, H):
    // Precompute spectral weights (power-law decay)
    weights[i] = lacunarity^(-i * H) for i in 0..octaves

    // First octave
    n = abs(noise(p));  signal = offset - n;  signal *= signal
    result = signal;    weight = 1.0

    p *= lacunarity

    for i in 1..octaves:
        weight = signal * gain   // feedback: rough areas feed more weight to next octave
        weight = clamp(weight, 0, 1)
        n = abs(noise(p))
        signal = offset - n
        signal *= signal
        signal *= weight         // scale this octave's contribution by the feedback weight
        result += signal * weights[i]
        p *= lacunarity
    return result
```

The **weight feedback** is the key: if the previous octave produced a large signal (a ridge), the current octave receives a large weight — the ridge becomes even rougher. If the previous octave was flat (a valley), the current octave is damped — valleys stay smooth. This is the origin of the realistic mountain-detail distribution described in Book of Shaders.

#### Variants
- **"Ridge" (Book of Shaders shorthand):** Simple `n = offset - abs(noise(p)); n = n*n;` without feedback loop
- **Billow noise:** Similar to ridged but without the squaring — produces rounded, puffy shapes (clouds)
- **Hybrid multifractal:** Alternates between additive (low frequencies) and multiplicative (high frequencies) combination

#### Strengths
- Dramatically more natural-looking mountainous terrain than standard fBm
- Self-organizing detail: roughness concentrates at ridgelines naturally
- Offset parameter directly controls sharpness of ridges

#### Weaknesses
- Harder to normalize — output range depends heavily on parameters
- More parameters to tune (offset + gain interact subtly)
- Weight feedback loop introduces implicit coupling between octaves that can cause unexpected behavior

#### Failure Modes
- `offset` too high (> 2.0 for typical Perlin) → "impossible" spikes
- `offset` too low → near-zero everywhere after inversion
- `gain` > 1.0 in feedback version → weight explodes; clamp is essential
- First octave not initialized carefully → incorrect weight initialization cascades

#### Combinations
- Replace the base `noise()` with simplex or OpenSimplex for cleaner ridges
- Combine with domain warping for twisted, eroded ridges
- Use as a mask: high ridged-multifractal values → snow/rock biome
- Blend with standard fBm (high elevations use ridged, low elevations use fBm)

#### Performance Notes
Same cost as fBm per octave, plus a multiply for the weight. Negligible overhead. N = 8 octaves is typical for terrain; N = 4 for texture detail.

#### Best Sources
- Musgrave, F.K. "Procedural Fractal Terrains." In Texturing & Modeling (3rd ed.), Ch 16
- Book of Shaders, Ch 13: [thebookofshaders.com/13](https://thebookofshaders.com/13/)
- Red Blob Games (ridgenoise function): [redblobgames.com/maps/terrain-from-noise](https://www.redblobgames.com/maps/terrain-from-noise/)
- libnoise RidgedMulti documentation: [libnoise.sourceforge.net](http://libnoise.sourceforge.net/)

---

### 11. Turbulence

**Category:** Fractal Composition (absolute-value fBm variant)

#### Core Purpose
Classical turbulence (as defined by Perlin in his original 1985 paper and the "Hypertexture" work) is a simple fBm variant: sum the *absolute values* of octaves rather than raw signed values. The effect is a field full of "creases" — sharp V-shaped ridges rather than smooth hills and valleys. It was Perlin's original technique for marble veining (`sin(x + turbulence(p))`) and fire/smoke textures.

Note: turbulence is the *unsigned* cousin of ridged multifractal. Ridged multifractal additionally inverts and squares each term; turbulence just takes abs() before summing.

#### Typical Game-Dev Use Cases
- Marble texture (classic: `sin(x + turbulence(p))`)
- Fire and smoke texture inputs
- Procedural cloud texture with shredded edges
- Water caustic patterns
- Any texture where creases/veins are desired

#### Inputs and Outputs
- **Input:** Continuous coordinate `p`; octaves; lacunarity; gain
- **Output:** Positive scalar (always ≥ 0 due to abs)

#### Core Algorithm

```
function turbulence(p, octaves, lacunarity, gain):
    value = 0.0
    amplitude = 1.0

    for i in 0..octaves:
        value += abs(noise(p)) * amplitude
        p *= lacunarity
        amplitude *= gain
    return value
```

**Marble texture example:**
```
marble(p) = sin(p.x * frequency + turbulence(p) * scale)
```
The turbulence displaces the position along the x-axis, creating vein-like patterns.

#### Variants
- **Signed turbulence:** Replace `abs(noise)` with `noise` — equivalent to fBm
- **Billow turbulence:** `2 * abs(noise) - 1` — maps abs noise to [−1, 1], producing puffy hills

#### Strengths
- Trivially simple modification of fBm
- Creases add naturalistic organic texture character
- Output is always positive — useful as a direct mask without remapping

#### Weaknesses
- Creases are C⁰ (not differentiable) — visible as hard kinks in rendered surfaces if used for normals
- Output range not symmetric — harder to blend with signed fBm fields
- Less controllable than ridged multifractal (no offset/sharpness parameters)

#### Failure Modes
- Using turbulence as a heightmap directly → the creases appear as sharp-bottomed valleys which may be unconvincing for terrain (ridged multifractal handles this better)
- Not scaling amplitude per octave → high-frequency octaves dominate

#### Combinations
- Classic combination: turbulence → sin(x + turb) = marble
- Turbulence as displacement for domain warping
- Mix turbulence with smooth fBm: low-elevation areas smooth, high-elevation areas turbulent

#### Performance Notes
Identical to fBm with a single `abs()` per iteration (negligible).

#### Best Sources
- Perlin, Ken. "An Image Synthesizer." SIGGRAPH 1985. (Original turbulence description)
- Book of Shaders Ch 13 (turbulence formula): [thebookofshaders.com/13](https://thebookofshaders.com/13/)

---

### 12. Domain Warping

**Category:** Scalar Field Transformation

#### Core Purpose
Domain warping (also called domain distortion) displaces the input coordinates of one noise function using the output of another noise function before evaluation. The result is dramatically more complex and organic-looking patterns — swirling, flowing structures that look like rock strata, flowing water, or fluid turbulence — at the cost of only a few additional noise evaluations.

The technique was used as early as 1984 (Perlin's marble texture is a simple 1D domain warp), but the layered fBm-on-fBm formulation was systematized and popularized by Inigo Quilez in a 2002 article.

#### Typical Game-Dev Use Cases
- Organic terrain with flowing erosion-like patterns
- Lava/magma textures
- Painted/streaked cave wall textures
- Flowing water/river effects
- Smoke and flame patterns
- Procedural alien/biomechanical textures

#### Inputs and Outputs
- **Input:** Continuous coordinate `p`; noise functions f, g (typically fBm); warp scale factors
- **Output:** Scalar float (same as the base noise)

#### Core Algorithm — Quilez's Layered Domain Warping

The key insight is that g(p) returns a scalar, but we need a 2D displacement vector. The solution is to call a 1D noise function twice with different seed offsets:

**Single warp (one layer):**
```
q = vec2(fbm(p + vec2(0.0, 0.0)),
         fbm(p + vec2(5.2, 1.3)))   // different offsets → independent values

result = fbm(p + scale * q)
```

**Double warp (two layers — Quilez's "warp.htm" example):**
```
q = vec2(fbm(p + vec2(0.0, 0.0)),
         fbm(p + vec2(5.2, 1.3)))

r = vec2(fbm(p + 4.0*q + vec2(1.7, 9.2)),
         fbm(p + 4.0*q + vec2(8.3, 2.8)))

result = fbm(p + 4.0*r)
```

Each layer of warping introduces increasing complexity. The magic numbers (5.2, 1.3, 1.7, 9.2, etc.) are arbitrary offsets ensuring the two fBm calls receive uncorrelated inputs — their exact values don't matter, but they should differ enough to break correlation.

**Mathematical formulation:**
Define pattern as the composition:
```
f(p + g(p + h(p)))
```
where f, g, h are fBm functions. Each `+` is a vector addition with a warp scale factor.

#### Variants
- **Simple domain warp:** Use a *single* noise function to perturb coordinates: `f(p + noise_scale * vec2(noise(p), noise(p + 43.5)))`. Fast but less complex.
- **Gradient-based domain warp:** Warp along the gradient of the noise field. Produces directional flow rather than swirling. Used for simulating water flow.
- **Texture coordinate warp:** Apply domain warping to UV coordinates before texture sampling — produces flowing, animated effects on static textures.
- **Progressive animation:** Let `p = p + time * flowDir` — the warp field is static but the sampling point moves through it.
- **Quilez "mod domain warp":** Apply different warp vectors to mirrored quadrants.

#### Strengths
- Dramatically increases visual complexity per additional noise evaluations
- Produces organic-looking results (flow, erosion simulation)
- Fully continuous and differentiable (if the constituent noises are)
- Parameterized (warp scale, number of layers)

#### Weaknesses
- Hard to control or predict — small parameter changes can dramatically alter output
- Expensive: 2-layer warping requires 5 fBm evaluations
- No clear mapping from output to intuitive parameters (not "more erosion" — it's math)
- Coloring / interpreting the output requires additional color mapping

#### Failure Modes
- Warp scale too large → chaotic, incoherent output
- No offset variation between the two fBm channels → both return the same value → warp is diagonal (no rotation)
- Using non-fBm noise as warp input → high-frequency warping → aliasing

#### Combinations
- The defining combination of fBm + fBm + fBm
- Output used as height, then domain-warp again at a different scale (multi-scale erosion)
- Blend domain-warped and straight fBm based on altitude mask
- Use with signed distance fields to create organic implicit surfaces

#### Performance Notes
Each additional warp layer adds N × (cost of one fBm) where N is the octave count. Typical 2-layer warp with 6-octave fBm: ~18 noise evaluations per pixel. On GPU, this is entirely feasible in realtime.

#### Best Sources
- Inigo Quilez, "Domain Warping": [iquilezles.org/articles/warp](https://iquilezles.org/articles/warp) ← Essential primary source
- The Book of Shaders, Ch 13 (domain warping section): [thebookofshaders.com/13](https://thebookofshaders.com/13/)
- Interactive introduction: [st4yho.me/domain-warping-an-interactive-introduction](https://st4yho.me/domain-warping-an-interactive-introduction/)
- Sebastian Lague: "Procedural Landmass Generation" series — implements domain warping in Unity

---

### 13. Layered Masks & Scalar Field Composition

**Category:** Scalar Field Operations

#### Core Purpose
Individual noise functions are outputs of a single technique. Real PCG pipelines combine multiple scalar fields through arithmetic and logical operations to produce complex spatial control signals. A "mask" is a scalar field in [0,1] used to spatially modulate another field or to control blending between two fields. Field composition is the algebra of combining these outputs.

#### Typical Game-Dev Use Cases
- Blending between two terrain types based on altitude and moisture
- Restricting tree placement to areas where slope < threshold AND altitude < threshold
- Creating shoreline transition zones (water depth mask × wave height)
- Controlling biome color variation
- Shaping noise output (e.g., multiply fBm by a radial gradient to create island shapes)
- Combining multiple noise layers for layered rock strata

#### Inputs and Outputs
- **Input:** Two or more scalar fields (continuous functions → float); operation type
- **Output:** A new scalar field (function → float)

#### Operations Taxonomy

**Arithmetic:**
- `add(A, B)`: Sum. Amplitudes combine; can exceed [0,1] range without normalization
- `multiply(A, B)`: Mask application. B dampens A where B is low. B * A is A with amplitude modulated by B.
- `lerp(A, B, t)`: Smooth blend. `A * (1-t) + B * t`. `t` can itself be a noise field.
- `smoothstep(A, lo, hi)`: Remap A into [0,1] with smooth easing. Converts a continuous gradient to a smooth binary mask.

**Nonlinear shaping:**
- `pow(A, exp)`: Exponentiation. Exp > 1 → pushes values toward 0 (redistributes terrain elevation). Exp < 1 → pushes toward 1 (raises sea level).
- `abs(A - 0.5) * 2`: Fold — creates symmetric banding
- `1 - A`: Inversion. Mountains become valleys.

**Boolean-like:**
- `step(A, threshold)`: Hard binary mask (0 below threshold, 1 above)
- `max(A, B)`: Union-like (higher value wins)
- `min(A, B)`: Intersection-like (lower value wins)
- `smoothmax / smoothmin`: Inigo Quilez's smooth union operators — polynomial blend near the junction

**Redistribution:**
- **Elevation redistribution (Amitp @ Red Blob Games):** `e = pow(e, exponent)` applied after normalization. Critical for controlling island vs. continent coastline character.
- **Terrace function:** `round(e * n) / n` for n levels → terraced elevations

#### Common Composition Patterns

**Island mask:**
```
island_mask = 1 - distance_from_center²
elevation = fBm(p) * island_mask
```

**Biome blending:**
```
mountain_noise = ridged_multifractal(p)
plains_noise   = fbm(p, octaves=4, gain=0.3)
mountain_mask  = smoothstep(elevation, 0.6, 0.8)
final          = lerp(plains_noise, mountain_noise, mountain_mask)
```

**Slope-based mask:**
```
height_L = heightmap(p - epsilon)
height_R = heightmap(p + epsilon)
slope    = abs(height_R - height_L) / (2 * epsilon)
tree_mask = step(slope, 0.3) * step(0.3, elevation) * step(elevation, 0.7)
```

#### Signed Distance Fields in Composition
SDFs define the distance from any point to the nearest surface of a shape. `sdf > 0` = outside, `sdf < 0` = inside, `sdf = 0` = on surface. In scalar field composition, SDFs serve as:
- **Domain masks:** `step(sdf(p), 0)` → binary shape mask
- **Smooth transition zones:** `smoothstep(sdf(p), -r, r)` → soft edge
- **Blend weights:** `lerp(A, B, smoothstep(sdf(p), -r, r))` → blend two fields across an SDF boundary

#### Strengths
- Compositional: simple operations combine into arbitrarily complex behaviors
- Fully continuous: output fields remain smooth if inputs are smooth
- Parameters are designer-legible (moisture threshold, elevation blend range)
- Separates concerns: terrain height generation vs. biome assignment vs. vegetation control

#### Weaknesses
- Composition chains can be difficult to debug — intermediate values invisible
- No guarantee of output range after arithmetic — normalization required
- High-arity blends (many layers) quickly become expensive

#### Performance Notes
Scalar field operations themselves are trivially cheap. The cost is dominated by the underlying noise evaluations feeding each field.

#### Best Sources
- Red Blob Games, "Making maps with noise": [redblobgames.com/maps/terrain-from-noise](https://www.redblobgames.com/maps/terrain-from-noise/)
- Inigo Quilez, "distance functions": [iquilezles.org/articles/distfunctions](https://iquilezles.org/articles/distfunctions)

---

### 14. Signed Distance Fields in PCG

**Category:** Spatial Query / Implicit Geometry

#### Core Purpose
A signed distance field (SDF) at point `p` returns the shortest distance from `p` to a surface. Positive = outside, negative = inside, zero = exactly on the surface. SDFs enable smooth boolean operations between shapes (smooth union, smooth intersection), procedural level geometry, and collision masks. In PCG they serve as shape generators and blend masks.

#### Typical Game-Dev Use Cases
- Cave entrance shape masks
- Procedural room boundary definition
- Softening hard biome/terrain transitions
- Procedural rock/pillar placement shapes
- SDF-based fonts and UI in shaders
- Smooth union of many small shapes (e.g., scattered boulders → unified rock outcrop)

#### Inputs and Outputs
- **Input:** Query point `p`; shape parameters
- **Output:** Signed scalar distance (negative = inside)

#### Core Operations (Quilez SDF Primitives)

```glsl
// Sphere SDF
float sdSphere(vec3 p, float r) { return length(p) - r; }

// Box SDF
float sdBox(vec3 p, vec3 b) {
    vec3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

// Smooth union (Quilez)
float smin(float a, float b, float k) {
    float h = clamp(0.5 + 0.5*(b-a)/k, 0.0, 1.0);
    return mix(b, a, h) - k*h*(1.0-h);
}

// Smooth subtraction
float smax(float a, float b, float k) { return -smin(-a, -b, k); }
```

#### Using SDFs with Noise
SDFs and noise combine naturally:
1. **Noise-displaced SDF:** `sdf(p + noise_offset * fbm(p))` → organic, irregular surface
2. **SDF-gated noise:** `step(sdf(p), 0) * noise(p)` → noise only inside a shape
3. **SDF as blend weight:** `lerp(A, B, smoothstep(sdf(p), -r, r))` → smooth transition at boundary
4. **Noise IS an SDF:** Inigo Quilez demonstrated a noise function constructed from a grid of sphere SDFs that is itself an SDF — raymarching-safe, combinable via smin.

#### Performance Notes
Simple SDFs (sphere, box) are O(1). Complex SDF compositions with many primitives can be expensive, especially inside raymarching loops. For PCG (not raymarching), SDFs used as masks are evaluated once per pixel/sample — cheap.

#### Best Sources
- Inigo Quilez, "distance functions": [iquilezles.org/articles/distfunctions](https://iquilezles.org/articles/distfunctions) ← Complete catalog of 2D and 3D SDF primitives
- Inigo Quilez, "smooth minimum": [iquilezles.org/articles/smin](https://iquilezles.org/articles/smin)
- Sebastian Lague, "Coding Adventure: Marching Cubes" — SDF-based terrain in Unity

---

## COMPARISONS

---

### Perlin vs Simplex vs OpenSimplex vs Worley

| Property | Perlin (Classic/Improved) | Simplex | OpenSimplex2 | Worley/Cellular |
|---|---|---|---|---|
| **Lattice type** | Hypercubic (square/cube) | Simplex (triangle/tetrahedron) | A*ₙ lattice | Jittered grid of feature points |
| **Corners evaluated** | 2ⁿ (4 in 2D, 8 in 3D) | n+1 (3 in 2D, 4 in 3D) | n+1 | 9 in 2D, 27 in 3D |
| **Gradient type** | Fixed set of n-dim gradients | Fixed set of simplex-face normals | Larger, more uniform gradient set | N/A (distance-based) |
| **Directional artifacts** | Noticeable at low octaves (diagonal bias) | Minimal | Minimal (better than original simplex) | Grid artifacts if jitter < 1 |
| **C¹ continuity** | Yes (quintic fade) | Partial (kernel discontinuity at vertices) | Yes | No (discontinuous at Voronoi edges) |
| **Visual character** | Smooth hills and valleys, slightly boxy | Rounder, more isotropic hills | Similar to simplex, slightly smoother | Cell-like, organic, "scale/stone" |
| **3D cost** | 8 corner evaluations | 4 corner evaluations | 4 corner evaluations | 27 cell searches |
| **4D cost** | 16 corners | 5 corners | 5 corners | 81 cell searches |
| **Patent status** | Expired (original); Improved is unpatented | Expired Jan 2022 | Never patented | Never patented |
| **Best for** | Terrain, clouds, textures — the standard | High-dim noise, GPU performance | Same as simplex, clean API | Organic textures, cell-based patterns |
| **Worst for** | 4D+, anisotropy-sensitive cases | Implementations needing exact Perlin look | Exact drop-in Perlin replacement | Smooth gradients, terrain shape |

**When to choose:**
- **Perlin:** When you need exact Perlin-compatible output, have an existing pipeline, or are working in 2D–3D
- **Simplex / OpenSimplex2:** New projects, 4D animation (time as dimension), GPU shaders, when directional artifacts are objectionable
- **OpenSimplex2 specifically:** When you want the most modern, well-maintained patent-free implementation
- **Worley:** When you want cellular/organic character rather than smooth flow; when building texture layers on top of Perlin/fBm terrain

---

### Random Placement vs Poisson Disk Sampling

| Property | Pure Random (Uniform) | Poisson Disk Sampling |
|---|---|---|
| **Distance guarantee** | None — points can be arbitrarily close | Hard minimum distance `r` between all points |
| **Coverage** | Statistically uniform on average, highly variable locally | Near-maximal coverage of domain |
| **Clustering** | Severe — Poisson clumping is expected and visible | Eliminated by design |
| **Implementation** | Trivial (one RNG call per point) | Bridson's algorithm: O(n), moderate complexity |
| **Online/streaming** | Yes — generate one point at a time | No — requires domain size upfront |
| **Parallelizable** | Trivially | Requires careful spatial partitioning |
| **Memory** | O(1) | O(n) for grid + point list |
| **Visual quality** | Highly variable — can look wrong | Consistently "organic" |
| **Tileability** | Easy | Requires precomputed tiles or careful stitching |
| **Parameter control** | Count only | Minimum distance `r` + rejection limit `k` |

**When to use pure random:**
- When point count is very low (< 5–10 points in the domain)
- When implementation simplicity dominates
- When clustering is acceptable or even desired (e.g., random grass clumps)
- When streaming (online) generation is required
- For initial prototyping before quality tuning

**When to use Poisson disk:**
- Vegetation and object placement (trees, rocks) where stacking is visually wrong
- Enemy/pickup placement where spacing matters for fairness
- Any case where even coverage is expected (players notice when trees clump)
- Rendering sample kernels where clumping increases noise/variance

---

## DATA STRUCTURES PRODUCED

### Scalar Field
The most common output. A function from ℝⁿ → ℝ (or discretized as a 2D/3D array of floats). Produced by all noise functions. Consumed by terrain generators, biome systems, texture samplers, and scalar field composition operations.

- **Heightmap:** 2D scalar field → terrain elevation. Typically float[width][height] or a texture.
- **Mask:** 2D scalar field remapped to [0,1] used as a blend weight. Often derived from thresholded noise.
- **Density field:** 3D scalar field → volumetric density (for marching cubes / raymarching).

### Point Set
Produced by Poisson disk sampling, pure random placement, and Worley feature points. An unordered list of n-dimensional coordinate vectors with no implicit connectivity.

- **Consumed by:** Voronoi tessellation, Worley noise construction, object placement systems, weighted selection systems (pick point, then decide what to place)

### Gradient Field
A vector field (ℝⁿ → ℝⁿ) — the spatial derivative of a scalar field. Produced analytically by gradient noise (Perlin, simplex) or numerically via finite differences from any scalar field. Consumed by: domain warping, flow simulation, erosion simulation, normal map generation.

### Distance Field / SDF
A scalar field where the value is the signed distance to a surface. Produced by SDF primitive functions and combinations thereof. Consumed by: shape masking, blend weight computation, raymarching renderers, collision detection.

### Permutation Table / Hash Array
The internal data structure of traditional Perlin noise. A 256-element (doubled to 512) permutation of integers 0–255. Acts as a lookup table for lattice point gradients. Consumed entirely internally; not exposed to downstream systems.

### Weighted Distribution Table
Produced by the Alias method's preprocessing step. Two arrays (Prob[], Alias[]) of length n. Consumed by O(1) weighted random selection queries.

---

## SOURCE LANDSCAPE

### Primary Papers
| Source | Value |
|---|---|
| Perlin, "An Image Synthesizer," SIGGRAPH 1985 | Original gradient noise — historically essential, algorithm is superseded |
| Perlin, "Improving Noise," SIGGRAPH 2002 | Quintic fade + 12 gradients — the actual algorithm to implement |
| Worley, "A Cellular Texture Basis Function," SIGGRAPH 1996 | Original cellular noise — one of the most readable noise papers |
| Bridson, "Fast Poisson Disk Sampling in Arbitrary Dimensions," SIGGRAPH 2007 | Single-page, self-contained, complete algorithm |
| Gustavson, "Simplex Noise Demystified," 2005 | The clearest explanation of both Perlin and simplex; includes working code |
| McEwan, Gustavson et al., "Efficient Computational Noise in GLSL," 2012 | GPU implementation without textures; peer-reviewed |
| Gustavson, "Cellular Noise in GLSL," 2011 | GPU Worley with 2×2 optimization |

### Tutorial Sites
| Source | Value |
|---|---|
| [thebookofshaders.com](https://thebookofshaders.com), Ch 10–13 | Interactive GLSL noise tutorials; visually clear, progressive |
| [iquilezles.org/articles](https://iquilezles.org/articles) | Quilez's own articles on fBm, domain warping, SDFs, Voronoi — dense, mathematically rigorous |
| [redblobgames.com/maps/terrain-from-noise](https://www.redblobgames.com/maps/terrain-from-noise/) | Excellent interactive treatment of noise for map generation; covers redistribution, masks |
| [catlikecoding.com/unity/tutorials/noise](https://catlikecoding.com/unity/tutorials/noise/) | Step-by-step Unity C# implementation of value, Perlin, and derivative noise — good code |

### GDC Talks
| Source | Value |
|---|---|
| Eiserloh, "Math for Game Programmers: Noise-Based RNG," GDC 2017 | Essential for understanding coordinate hashing vs sequential PRNG |
| Quilez, "Rendering Worlds with Two Triangles," Nvscene 2008 | Practical application of all noise techniques in realtime rendering |

### Books
| Source | Value |
|---|---|
| Ebert et al., "Texturing and Modeling: A Procedural Approach" (3rd ed.) | The reference book. Ch 2 (noise), Ch 16 (Musgrave multifractals). Dated but foundational. |
| Shiffman, "The Nature of Code" | Accessible treatment of randomness, Perlin noise; good for beginners |
| Pharr et al., "Physically Based Rendering" | Deep treatment of sampling theory (stratified, blue noise) in a rendering context |

### Repositories
| Source | Value |
|---|---|
| [github.com/Auburn/FastNoiseLite](https://github.com/Auburn/FastNoiseLite) | Modern, multi-language noise library (Perlin, Simplex, Worley, fBm, domain warp). Production-quality code to read. |
| [github.com/KdotJPG/OpenSimplex2](https://github.com/KdotJPG/OpenSimplex2) | Reference implementation of OpenSimplex2; multiple languages |
| [SquirrelNoise5 Gist](https://gist.github.com/kevinmoran/0198d8e9de0da7057abe8b8b34d50f86) | Reference C++ implementation of SquirrelNoise5 |
| [shadertoy.com](https://www.shadertoy.com) (search: fbm, domain warp, cellular) | Live-editable GLSL implementations; Quilez's own shaders are canonical |

### Video Series
| Source | Value |
|---|---|
| Sebastian Lague, "Procedural Landmass Generation" (YouTube) | Unity-based, covers heightmaps, fBm, falloff maps, biome blending |
| Brackeys, "Procedural Terrain Generation" | More accessible Unity intro, less mathematical depth |
| Inigo Quilez (YouTube, shadertoy.com) | Advanced; shows all techniques applied in real shader code |

---

*Document compiled for PCG Research Pass 1. Scope: noise and randomness fundamentals only. Higher-level systems (terrain pipelines, dungeon generation, biome assignment, WFC) are covered in separate passes.*
