# Procedural noise and sampling: a technical reference for game developers

This document is a foundational reference covering the fourteen core techniques that underpin procedural content generation in games — from the pseudorandom primitives that seed every pipeline, through the noise functions that sculpt continuous scalar fields, to the composition operators that combine them into usable game data. Each technique section covers the problem it solves, its algorithm at implementation depth, key variants, failure modes, and how downstream systems consume its output. Sources are cited inline throughout; the closing section organizes them by type.

---

## 1. Seeded PRNG and coordinate hashing

### The statefulness problem in procedural worlds

Sequential pseudorandom number generators — LCGs, xorshift, Mersenne Twister, the PCG family — maintain internal state and produce numbers in a fixed order. This creates fundamental problems for procedural content generation. **State dependency** means generating content at position (100, 200) requires knowing how many random numbers were consumed before that point. Parallelism is blocked because multiple threads cannot safely share a single PRNG without locks. Order dependency means the same world seed generates differently depending on which chunk the player visits first. Coordinate hashing solves all of these by being stateless and random-access: given the same `(x, y, z, seed)` tuple, it always returns the same pseudorandom output regardless of query order or thread. As Squirrel Eiserloh described in his GDC 2017 talk "Noise-Based RNG," this is equivalent to looking up a value in an infinitely large table of previously rolled random numbers — except the table never exists in memory.

### Core algorithms: sequential PRNGs vs. position hashing

A **Linear Congruential Generator** uses the recurrence `X_{n+1} = (a * X_n + c) mod m`. With typical constants (Numerical Recipes: a=1664525, c=1013904223, m=2³²), it has a period of 2³² but a critical flaw: the b-th bit has period only 2^b, making low bits highly predictable. The **xorshift** family (`x ^= x << a; x ^= x >> b; x ^= x << c`) gives all bits full period but fails BigCrush at any state size (Vigna, prng.di.unimi.it). **PCG** (O'Neill, 2014) combines an LCG state transition with an output permutation function; PCG-XSH-RR uses 64-bit state → 32-bit output via a data-dependent rotation, passing BigCrush with minimal state.

**Coordinate hashing** takes the opposite approach: a pure function from position to random value, with no mutable state. Squirrel Eiserloh's **Squirrel3** hash (GDC 2017) follows a multiply → add seed → xor-shift right → add constant → xor-shift left → multiply → xor-shift right pattern using three carefully chosen bit-noise constants (`0xb5297a4d`, `0x68e31da4`, `0x1b56c4e9`). The improved **SquirrelNoise5** (2021, SquirrelNoise5.hpp by Kevin Moran, CC-BY-3.0) uses five constants with progressively increasing right-shift amounts (9, 11, 13, 15, 17), ensuring all input bits influence all output bits. Peter Schmidt-Nielsen identified that Squirrel3 had insufficient bit mixing at extremely high position values; SquirrelNoise5 fixes this with worst-case influence of 49.99% (vs. ideal 50%).

Multi-dimensional hashing collapses N-D coordinates to 1D using large prime multipliers: `hash(x + PRIME1 * y + PRIME2 * z, seed)` where the primes (e.g., 198491317, 6542989) are chosen to have non-boring bit patterns and avoid axis-aligned correlation. Jasper Flick's Catlike Coding series takes a different approach entirely, implementing **SmallXXHash** based on Yann Collet's XXH32 algorithm. The avalanche step uses `value ^= value >> 15; value *= primeB; value ^= value >> 13; value *= primeC; value ^= value >> 16`. Unlike a permutation table, SmallXXHash is seedable, has unlimited domain, and can be vectorized with SIMD because it requires no array lookups (Catlike Coding, "Hashing" tutorial).

### Failure modes and downstream pairing

Poor hash functions are the primary risk. Simple operations like `hash = position * constant` produce obvious stripes. Eiserloh noted that many hash functions found online "are hugely biased or terribly patterned, e.g. having bits which are on (or off) 75% or even 100% of the time." Other pitfalls include **modulo bias** when converting hash output to a range (`hash % n` is biased when n doesn't evenly divide 2³²; use Lemire's method for correctness), **floating-point precision loss** for values above 2²⁴ (float32 has only 23 mantissa bits), and **axis-aligned artifacts** from poorly chosen dimensional primes. Coordinate hashes feed directly into every noise function (providing the pseudorandom lattice values that noise interpolates between), chunk-based world generation (each chunk independently generates content from `hash(cx, cy, seed)`), and deterministic multiplayer (all clients produce identical content from a shared seed without synchronizing state).

---

## 2. Weighted random selection and shuffle bags

### Why uniform randomness is rarely what games need

Games constantly select items from sets where items have unequal probabilities — loot tables, spawn rates, biome selection, event triggering. Uniform selection gives every item equal chance, which is almost never desired. Furthermore, even correctly weighted random selection produces **streaks**: a player might receive the same rare drop three times consecutively while never seeing a common one. Weighted random selection solves the probability distribution problem; shuffle bags solve the streak problem by guaranteeing that within each cycle, observed frequencies match desired proportions exactly.

### Three algorithms for weighted selection

**Linear scan** (O(n) per selection) accumulates weights and returns the first item whose cumulative weight exceeds a random float in `[0, sum(weights))`. It needs no preprocessing and O(1) space, and works well for n < ~100. Sorting weights in descending order improves average-case performance. **Binary search on CDF** (O(n) setup, O(log n) per selection) precomputes a cumulative distribution array and binary-searches for the target value. This is the best general-purpose method for medium collections. **Vose's Alias Method** (Vose, 1991; originally Walker, 1977) achieves **O(1) per selection** after O(n) preprocessing. It constructs two parallel arrays — `Prob[]` and `Alias[]` — by partitioning items into "small" (probability < 1/n) and "large" (probability ≥ 1/n) worklists, then pairing each small item with a large item to fill exactly one "column" of probability. Selection generates a single random float, uses its integer part to pick a column and its fractional part to choose between the column's two items. This is optimal for high-throughput scenarios like spawning thousands of entities per frame.

### Shuffle bags and Fisher-Yates

A **shuffle bag** is a container pre-filled with items in exact desired proportions (e.g., 6 gold, 3 silver, 1 diamond for 60%/30%/10% weights). Items are drawn without replacement until exhausted, then the bag is reshuffled and refilled. The Tetris Guideline uses exactly this: a 7-piece bag shuffled via Fisher-Yates, ensuring all seven tetrominoes appear once per cycle. The internal shuffle must use the **Fisher-Yates algorithm** (`for i from n-1 downto 1: j = random in [0, i]; swap(array[i], array[j])`) which produces an unbiased permutation in O(n). The naive alternative (`j = random in [0, n-1]` for each i) generates n^n code paths mapping to n! permutations, creating measurable bias — for a 3-element array, 27 paths map to 6 permutations, making some permutations 50% more likely than others (Jeff Atwood, "The Danger of Naïveté," Coding Horror).

### Failure modes and practical variants

For weighted selection: floating-point weights that don't sum cleanly can make the last item unreachable; multiplying by 1000 and using integers avoids this. The alias method can accumulate numerical errors during partitioning, causing Prob values to exceed 1.0. For shuffle bags: the primary risk is **predictability near bag end** — when only 1–2 items remain, the next draw is obvious. Mitigations include using larger bags or refilling at half-empty rather than fully depleted. **Cross-boundary artifacts** occur when the last item of one cycle and the first of the next happen to match, producing a double despite the bag's anti-streak guarantee. Both techniques pair naturally with coordinate hashing: using `hash(position, seed)` to drive weighted selection makes loot tables deterministic per location, supporting save/load and multiplayer consistency.

---

## 3. Poisson disk sampling

### The natural spacing problem

Many PCG tasks — tree placement, building distribution, star fields — require points that look random yet maintain a minimum separation distance **r**. Pure random placement produces visible clumps and gaps. Regular grids look artificial. Poisson disk sampling produces distributions where **no two points are closer than r** while appearing organically irregular. The resulting point sets exhibit blue noise spectral characteristics: random at high frequencies, uniform at low frequencies, with no preferred directions.

### Bridson's O(n) algorithm

Robert Bridson's 2007 SIGGRAPH sketch "Fast Poisson Disk Sampling in Arbitrary Dimensions" is a single-page paper describing an algorithm that generates N samples in O(N) time, trivially generalizable to any dimension. **Step 0**: Create a background grid with cell size `r/√n` (where n is the number of dimensions), guaranteeing at most one sample per cell. This grid enables O(1) neighbor lookups — in 2D, a 5×5 neighborhood of cells suffices to catch all points within distance r. **Step 1**: Place one random initial sample and add it to an "active list." **Step 2**: While the active list is non-empty, choose a random active sample, generate up to **k candidate points** uniformly distributed in the spherical annulus between radius r and 2r around it, and test each candidate against nearby samples via the grid. If a candidate passes, emit it and add it to the active list. If all k candidates fail, remove the active sample. The algorithm executes exactly 2N−1 iterations to produce N samples, with each iteration costing O(k) — and k is a constant (typically 30), so total time is O(N).

Prior methods were either limited to 2D with complex geometric data structures (Dunbar & Humphreys 2006, "scalloped sectors") or impractically slow. **Naive dart-throwing** (Dippé & Wold 1985) randomly places candidates across the entire domain and rejects those violating minimum distance; as coverage increases, the rejection rate approaches 100%, producing O(n²) behavior that cannot guarantee a maximal distribution in finite time. Mitchell's **best-candidate algorithm** (1991) generates m candidates per point and selects the one farthest from existing samples — it approximates blue noise in O(n²) time and produces progressive samples (any prefix is also blue noise) but is significantly slower than Bridson's approach.

### Variants and deterministic generation

**Lloyd's relaxation** takes any point set, constructs its Voronoi diagram, moves each point to its cell's centroid, and repeats. It converges toward a centroidal Voronoi tessellation with very regular spacing but doesn't control minimum distance directly. Amit Patel (Red Blob Games) notes that boundary points drift inward since they have neighbors on only one side. **Martin Roberts' improved Bridson** (2019, extremelearning.com.au) samples candidates along the annulus surface rather than volume, claiming ~20× speedup with tighter packing. For PCG, deterministic results are essential: Bridson's algorithm is made seedable by using a seeded PRNG (e.g., PCG, xorshift) for all random operations. For chunk-based worlds, each chunk seeds its PRNG from chunk coordinates, with boundary reconciliation between adjacent chunks.

### Failure modes

**Boundary effects** cause visible edge artifacts; extending the sampling domain beyond the visible region or using toroidal boundary conditions mitigates this. **Insufficient k** (e.g., k < 5) terminates prematurely with visible gaps and non-maximal coverage; k=30 is the standard default with diminishing returns above (Red Blob Games). The **Nebraska Problem** (Casey Muratori, 2014) describes visible "parallel branches" with streaky gaps between them — an artifact of Bridson's front-propagation nature when packing is tight. Muratori ultimately used a curved jittered grid as a workaround. For most PCG applications, k=10–15 represents a good quality/speed tradeoff (Red Blob Games, "2D Point Sets").

---

## 4. Stratified sampling and blue noise

### Jittered grids: simple but fundamentally limited

**Stratified sampling** divides the domain into a regular grid of cells (strata) and places exactly one random sample within each cell. The jitter parameter controls how far from center each sample can move (0 = regular grid, 1 = fully random within cell). Amit Patel's interactive analysis (Red Blob Games) reveals a fundamental tension: **angle isotropy** (no preferred directions) requires jitter ≥ 0.9, while **distance uniformity** (avoiding too-close or too-far outliers) requires jitter ≤ 0.6. Since 0.9 > 0.6, no single jitter value simultaneously achieves both goals. This is the core limitation of jittered grids compared to true Poisson disk sampling. Hexagonal grid jitter produces slightly better isotropy than square grids. Multi-jittered sampling (Chiu et al. 1994) and Pixar's Progressive Multi-Jittered sequences achieve low sampling error comparable to quasi-random sequences while maintaining blue noise properties.

### Blue noise: spectral characterization

**Blue noise** refers to spatial distributions whose power spectrum has minimal low-frequency content. The radial average of the Fourier transform shows near-zero energy below a characteristic frequency, then rises sharply — this low-frequency gap distinguishes blue noise from white noise (which has flat power at all frequencies, producing visible clumps and gaps at all scales). Blue noise is also **isotropic** (rotationally symmetric spectrum, no preferred directions) and has a flat high-frequency region above the transition frequency. The photoreceptors in primate retinas arrange in a blue noise pattern (Yellott, 1983), suggesting evolutionary optimization for perceptual quality. Poisson disk sampling is the primary method for generating point sets with blue noise characteristics; stratified sampling approximates blue noise but retains grid-aligned artifacts.

### The void-and-cluster algorithm

The **void-and-cluster algorithm** (Robert Ulichney, 1993) generates blue noise **threshold maps** — textures where pixel values encode rank ordering — and is the gold standard for blue noise mask generation. It works in four phases: (1) generate an initial binary pattern with ~10% pixel coverage; (2) iteratively remove the "tightest cluster" point (highest Gaussian-weighted neighbor energy) and assign decreasing ranks; (3) iteratively place samples at the "largest void" (lowest-energy empty location) and assign increasing ranks until half-filled; (4) continue filling remaining empty pixels by largest-void ordering. The Gaussian sigma (typically σ ≈ 1.9) controls spatial scale, and **toroidal distance** ensures seamless tiling. The algorithm produces progressive blue noise: any threshold of the texture yields a blue noise point set. Bart Wronski (2021) demonstrated a ~50-line simplified implementation using NumPy/JAX. Christoph Peters (momentsingraphics.de) provides freely available pre-computed blue noise textures widely used in game rendering (Unity's post-processing stack uses them). The patent (US 5,535,020) expired circa 2013.

### Blue noise in PCG vs. rendering

In rendering, blue noise textures serve as threshold maps for dithering, distributing quantization error as imperceptible high-frequency noise. In PCG, blue noise describes **point sets** for object placement. A pre-computed blue noise texture enables a compact trick: threshold it at different levels to extract varying-density blue noise point sets from a single 128×128 image — **256 different blue noise distributions** from one compact data file (per Red Blob Games analysis). Combined with a Perlin noise density map, this produces variable-density natural-looking placement at near-zero runtime cost.

---

## 5. Value noise

### The simplest coherent noise

Value noise is **lattice-based noise** that assigns pseudorandom scalar values at integer lattice coordinates and interpolates between them. It solves the most basic coherent noise problem: producing a smooth, continuous random signal from discrete random data. Catlike Coding describes it precisely: "Value noise is lattice noise that defines constant values at the lattice points. Interpolation of these values produces a smooth pattern, but the lattice is still quite obvious."

### Algorithm

For input point P = (x, y) in 2D: compute integer cell coordinates `i = floor(x), j = floor(y)` and fractional position `f_x = fract(x), f_y = fract(y)`. Fetch the four corner values `a = hash(i,j), b = hash(i+1,j), c = hash(i,j+1), d = hash(i+1,j+1)`. Apply a fade function to the fractional coordinates: `u = fade(f_x), v = fade(f_y)`. Bilinearly interpolate: `result = lerp(lerp(a, b, u), lerp(c, d, u), v)`. In 3D, this expands to 8 hash lookups and 7 lerps (trilinear interpolation). Inigo Quilez provides the analytical expansion for 3D value noise as a polynomial: `n(u,v,w) = k0 + k1·u + k2·v + k3·w + k4·u·v + k5·v·w + k6·w·u + k7·u·v·w` where k0–k7 are linear combinations of the 8 corner values.

The choice of fade function matters enormously. **Linear interpolation** (`f(t) = t`) is C⁰ only and produces sharp visible edges at lattice boundaries. **Cubic Hermite** (`f(t) = 3t² − 2t³`, equivalent to GLSL `smoothstep()`) is C¹ — first derivative is zero at endpoints, but second derivative is discontinuous (6 at t=0, −6 at t=1). **Quintic** (`f(t) = 6t⁵ − 15t⁴ + 10t³`) is C² — both first and second derivatives are zero at endpoints. The quintic curve eliminates visible crease artifacts when noise derivatives are used for bump mapping or normal computation. Quilez derives analytical derivatives of value noise, enabling normal computation without expensive central differences — he used this for his "Elevated" 4k demo terrain, achieving up to 6× speedup for volumetric raymarching (Inigo Quilez, "Value Noise Derivatives," iquilezles.org/articles/morenoise/).

### The grid artifact problem

Value noise's fundamental flaw is that **extrema occur at lattice points** — the random values live on the grid, and interpolation always occurs along axis-aligned directions. This produces visible square/rectangular patterns that reveal the underlying grid structure. The artifacts are especially pronounced in 2D heightmaps and become more visible when the noise is used as a base for fBm. Gradient noise (Perlin noise) specifically addresses this problem. Despite this limitation, value noise remains useful when its grid character is acceptable or deliberately desired, and its lower computational cost (no dot products needed) makes it attractive for performance-critical applications.

---

## 6. Perlin noise (classic and improved)

### Historical context and the gradient lattice insight

Ken Perlin developed gradient noise in 1982–1983 while working at MAGI on Disney's *Tron*, frustrated with the "machine-like" look of CGI. He published the algorithm at SIGGRAPH 1985 as "An Image Synthesizer" and received an Academy Award for Technical Achievement in 1997. The core insight: instead of storing random *values* at lattice points (as value noise does), store random **gradient vectors**. The noise value at any point is computed by hashing surrounding lattice coordinates to select gradients, computing dot products between those gradients and offset vectors from each lattice point to the sample point, and smoothly interpolating the results. **The noise function equals zero at every lattice point** (because the offset vector from a lattice point to itself is zero, and `dot(gradient, zero) = 0`). This forces all interesting variation between lattice points, distributing visual features away from the grid structure.

### Classic Perlin noise (1985)

The algorithm uses a **256-entry permutation table** containing integers 0–255 in random shuffled order, doubled to 512 entries to avoid overflow. The hash function for 3D is `P[P[P[x] + y] + z]`, producing a pseudorandom integer for every integer coordinate. In the classic 1985 version, this hash indexed into **256 pseudo-random gradient vectors** distributed on a 3D sphere, and interpolation used the **cubic Hermite curve** `3t² − 2t³`. Perlin himself identified two deficiencies (GPU Gems, Chapter 5): the cubic interpolant has discontinuous second derivative (6 at t=0, −6 at t=1), causing **visible creases at lattice boundaries** in bump-mapped surfaces; and the 256 random gradients had irregular distribution — some bunched up while others were sparse, causing "splotchy appearance."

### Improved Perlin noise (2002)

Perlin's 2002 SIGGRAPH paper "Improving Noise" made three changes. **First**, the interpolation curve changed to the **quintic** `6t⁵ − 15t⁴ + 10t³`, which is C² continuous (both first and second derivatives vanish at t=0 and t=1). Perlin noted "the visual improvement can be seen as a reduction in the 4×4-grid-like artifacts." Efficient evaluation: `t * t * t * (t * (t * 6 - 15) + 10)`. **Second**, the 256 random gradients were replaced with just **12 gradient vectors** — the midpoints of the 12 edges of a cube: `(±1,±1,0), (±1,0,±1), (0,±1,±1)`. Padded to 16 (to allow selection via `& 15` instead of `% 12`), this eliminates splotchiness because "none of the 12 gradient directions is too near any others." The fixed set also makes dot products trivially cheap — each reduces to one or two additions since all components are 0 or ±1 (e.g., `dot((x,y,z), (1,1,0)) = x + y`). **Third**, a second permutation table lookup was eliminated; the hash directly selects one of 16 gradient cases via `& 15`.

### Tradeoffs and artifacts

Scratchapixel and Eastfarthing.com note that the 12-gradient set, while eliminating splotchiness, can introduce subtle **axis-aligned runs** visible in 2D slices of 3D noise. Because the gradients align along xy, xz, and yz planes, a 2D slice at z=0 means each gradient has only 8 effective directions biased toward cardinals, which can produce 45° artifacts. Catlike Coding's modern approach avoids the permutation table entirely, using SmallXXHash4 for SIMD-friendly computation and generating gradient vectors via an octahedron-based distribution rather than Perlin's 12-vector set. In practice, improved Perlin remains the most widely implemented gradient noise, and its artifacts are largely invisible once layered into fBm or subjected to domain warping.

---

## 7. Simplex and OpenSimplex noise

### The simplex grid and O(N²) scaling

Ken Perlin designed simplex noise (2001) to overcome classic Perlin's exponential scaling: evaluating classic noise in N dimensions requires 2^N gradient evaluations (hypercube corners), while a simplex has only **N+1 corners**. Stefan Gustavson's 2005 paper "Simplex noise demystified" provides the definitive accessible explanation. A simplex is the simplest shape that tiles N-dimensional space: line segments in 1D, equilateral triangles in 2D, tetrahedra in 3D. The coordinate space is transformed via a **skew factor** `F_N = (√(N+1) − 1)/N` so that the simplicial grid maps onto an axis-aligned hypercubic grid for easy cell identification. Concrete values: F₂ ≈ 0.3660, F₃ = 1/3, F₄ ≈ 0.3090. The inverse **unskew factor** is `G_N = (1 − 1/√(N+1))/N`.

Once inside a skewed hypercube cell, the containing simplex is determined by sorting the internal coordinates by magnitude — each ordering corresponds to a unique simplex. In 2D: if x₀ > y₀, the lower triangle with corners (0,0)→(1,0)→(1,1); otherwise the upper triangle. In 3D: 6 possible orderings = 6 simplices per cube. Instead of sequential axis-aligned interpolation, simplex noise uses **direct summation of surflet contributions** from each corner with a radially symmetric kernel: `contribution_i = max(0, r² − d²)⁴ × dot(gradient_i, displacement_i)`, where r² is a radius parameter (Perlin uses 0.6 for visual quality, accepting minor discontinuities vs. strict 0.5 for C¹ continuity). The kernel radius ensures each vertex's influence reaches zero before crossing into adjacent simplices, so only N+1 corners contribute. Results are scaled to approximately [-1, 1] by a final multiplier (70.0 in 2D, 32.0 in 3D per Gustavson's code).

### OpenSimplex and OpenSimplex2

Ken Perlin filed **US Patent 6,867,776** on simplex noise in 2001 (granted 2005, expired January 8, 2022). Kurt Spencer (KdotJPG) developed **OpenSimplex** in September 2014 as a clean-room, patent-free alternative, released into the public domain. The key algorithmic difference: where simplex noise squashes a hypercubic honeycomb down the main diagonal, OpenSimplex swaps the skew/inverse-skew factors and uses a **stretched** honeycomb. In 3D, simplex uses a tetragonal disphenoid honeycomb while OpenSimplex uses a **tetrahedral-octahedral honeycomb** — a fundamentally different lattice. OpenSimplex uses a larger kernel radius than simplex noise, so more vertices contribute to each evaluation, producing smoother results at slightly higher computational cost (~6% slower in FastNoiseSIMD benchmarks).

Legacy OpenSimplex had quality problems in higher dimensions — different grid layout produced inconsistent contrast in 3D/4D and visible diagonal bands in 5D+. **OpenSimplex2** (released ~January 2020, github.com/KdotJPG/OpenSimplex2) addressed these with congruent lattice layouts and revised gradient tables. It provides two variants: **OpenSimplex2F** (fast, most similar to simplex, approximately as fast as common simplex implementations; in 3D constructs a rotated body-centered-cubic grid as union of two rotated cubic grids, finding 2 closest points on each) and **OpenSimplex2S** (smoother, finds 4 closest points per BCC half-lattice for 8 total; recommended for ridged noise). Both variants offer orientation-aware functions: `noise3_ImproveXY` for better isotropy in the XY plane (recommended when Z is height), `noise3_ImproveXZ` for XZ-primary planes.

### Artifacts across variants

Simplex noise shows slight **triangular/hexagonal artifacts** in 2D at certain frequencies due to the simplicial grid. OpenSimplex2F has the most noticeable diagonal artifacts of the OpenSimplex variants and a "very dotty appearance" in 4D. OpenSimplex2S is smoother but more expensive. For most PCG applications post-2022 (patent expiration), standard simplex noise or OpenSimplex2F is the recommended default for smooth coherent noise, with OpenSimplex2S preferred when ridged noise or quality-critical smooth noise is needed.

---

## 8. Worley / Voronoi noise

### Cellular texture basis functions

Steven Worley's 1996 SIGGRAPH paper "A Cellular Texture Basis Function" introduced a noise function complementing Perlin noise, producing textures resembling "flagstone-like tiled areas, organic crusty skin, crumpled paper, ice, rock, mountain ranges, and craters." The algorithm scatters feature points across space (one or more per grid cell), then for each evaluation point computes distances to the nearest N feature points. The outputs — **F1** (distance to nearest), **F2** (second nearest), **F3** (third nearest) — can be used individually or combined to produce diverse visual patterns. Grid-based acceleration makes computation O(1) per evaluation: in 2D, only the containing cell and its 8 neighbors need checking (9 cells total); in 3D, 27 cells.

### Distance functions and their visual character

**F1** (distance to nearest point) produces a bubbly, cellular appearance with circular gradients inside convex cells, with value zero at cell centers — ideal for biological cells, bubble wrap, rounded cobblestones, and caustics. **F2-F1** (difference between second and first nearest distances) produces thin lines at cell boundaries where F1 ≈ F2, creating veiny/cracked textures suitable for dried mud, leaf veins, reptile skin, and tile grout. However, as Inigo Quilez warns (iquilezles.org/articles/voronoilines/), **"F2-F1 is not a distance really, as it expands and contracts depending on the distance between the two cell points on each side of the edge."** Simple F2-F1 thresholding produces non-uniform line widths. Quilez provides a mathematically correct algorithm that projects the evaluation point onto the perpendicular bisector of the two nearest cell points for true edge distance.

The choice of distance metric dramatically alters visual character. **Euclidean** distance produces natural, organic circular cells. **Manhattan** (L₁: |Δx| + |Δy|) produces diamond-shaped cells with axis-aligned features. **Chebyshev** (L∞: max(|Δx|, |Δy|)) produces square cells — Catlike Coding notes that "an axis-aligned plane sampling 3D Chebyshev noise produces square regions of uniform color," giving it a distinctly artificial look. Quilez's **Voronoise** (iquilezles.org/articles/voronoise/) provides a unified framework that parameterizes continuously between noise and Voronoi: parameter u (0→1) controls jittering, parameter v (0→1) controls the metric from hard min to smooth interpolation.

### Smooth Voronoi and failure modes

Standard Worley noise has **discontinuous derivatives at cell edges** because the min() operation is not differentiable. Quilez's smooth Voronoi (iquilezles.org/articles/smoothvoronoi/) replaces min() with smooth approximations — exponential (`-log(exp(-k·a) + exp(-k·b)) / k`) or polynomial smooth min — at the cost of slightly higher computation. This is critical when Worley noise is used as input to normal mapping or erosion simulation. Common failure modes include flat regions when the maximum possible distance exceeds the search radius (mitigated by placing two points per cell, per Catlike Coding), and the 2×2 GPU optimization (Gustavson 2011) that can create discontinuity artifacts at tile edges. Houdini benchmarks place Worley at approximately **1.8× the cost of Perlin** (SideFX docs), making it the most expensive of the standard noise types but justified by its unique cellular character.

---

## 9. Fractal Brownian motion (fBm)

### The octave summation principle

fBm is a spectral synthesis technique that sums multiple octaves of a base noise function at increasing frequencies and decreasing amplitudes: `fBm(p) = Σ amplitude_i × noise(frequency_i × p)`. Each octave doubles in frequency (lacunarity = 2.0) and halves in amplitude (gain/persistence = 0.5) by default, producing a self-similar fractal structure that matches the **1/f power spectrum** observed in natural phenomena — mountain ranges, coastlines, cloud boundaries. Musgrave's canonical implementation (F. Kenton Musgrave, *Texturing and Modeling: A Procedural Approach*, 1994) parameterizes this via the **Hurst exponent H** (0 to 1), where `gain = lacunarity^(-H)`. At H=1.0 (the CG favorite), energy decays at 9 dB/octave producing smooth, positively correlated noise. At H=0.5, the spectrum matches true Brownian motion. As Quilez explains, "if the memory is positively correlated, changes in a given direction will tend to produce future changes in the same direction, and the path will then be smoother" (iquilezles.org/articles/fbm/).

### Implementation details and normalization

The standard parameters are **4–8 octaves** (typically 4–6 for most game applications), **lacunarity 2.0**, and **gain 0.5**. Musgrave's code supports fractional octaves by weighting the last octave by the fractional remainder. Each octave should use a **different hash seed or coordinate offset** to avoid visible convergence at the origin — Catlike Coding uses `hash + octaveIndex` to decorrelate octaves. Normalization divides by the sum of amplitudes: `(1 − persistence^N)/(1 − persistence)`. The PBR Book (Physically Based Rendering) implements antialiased fBm using derivative-based octave clamping: `n = Clamp(-1 - 0.5 × Log2(len2), 0, maxOctaves)` and uses a lacunarity of **1.99 instead of 2.0** to reduce artifacts from noise being zero at integer lattice points. Each octave evaluates the underlying noise function once, so total cost is N × (base noise cost).

### Failure modes

**Homogeneity** is the principal limitation: fBm is statistically identical everywhere, lacking the heterogeneity of real terrain where mountain ridges are rough and valleys are smooth. Ridged multifractal noise (Section 10) and derivative-based domain warping address this. **Aliasing** occurs when octaves exceed the Nyquist limit of the output grid — high-frequency detail below pixel size becomes speckle. Octave counts must be band-limited based on view distance or pixel footprint. The fBm function is the foundation upon which turbulence, ridged noise, and domain warping are built.

---

## 10. Ridged multifractal noise

### Sharp ridges from inverted absolute noise

Created by F. Kenton Musgrave (Copyright 1994), ridged multifractal noise modifies the fBm summation with three operations per octave: take the **absolute value** of the noise (creating creases at zero crossings), **invert** it by subtracting from an offset (turning creases into ridges), and **square** the result (sharpening the ridges). The core per-octave computation is: `signal = offset − |noise(p)|; signal = signal²`. Musgrave's recommended starting parameters are H=1.0, offset=1.0, gain=2.0, lacunarity=2.0 with 4–8 octaves.

### The weight feedback mechanism

The critical distinguishing feature is **heterogeneous feedback**: each octave's amplitude is modulated by the previous octave's signal value. `weight = clamp(previous_signal × gain, 0.0, 1.0); current_signal *= weight`. Areas near ridge peaks (high signal) receive more high-frequency detail, while valleys (low signal) become progressively smoother. This creates the heterogeneity that standard fBm lacks — mimicking how mountain ridges have detailed rocky features while valleys are smoother. As The Book of Shaders describes, "the sharp valleys [from turbulence] are turned upside down to create sharp ridges." The visual result resembles mountain ranges, tectonic boundaries, and vein patterns.

### Failure modes and parameter sensitivity

Without the `clamp(weight, 0, 1)` guard, values can diverge — the clamp is essential for numerical stability. The five parameters (H, lacunarity, octaves, offset, gain) make the function tedious to art-direct (noted by BlenderDiplom). **Gain sensitivity** is particularly problematic: values above 4 amplify the feedback loop aggressively, producing extreme contrast. The offset parameter shifts the "phase" of the ridge/valley structure rather than simply raising a baseline. Performance is identical to fBm — N noise evaluations per sample with negligible additional ALU for abs, subtract, multiply, and clamp — making it a near-free upgrade when ridge-like features are desired.

---

## 11. Turbulence

### Perlin's absolute-value noise

Turbulence, originally described by Ken Perlin (1985), modifies fBm by summing the **absolute value** of noise at each octave: `turbulence(p) = Σ amplitude_i × |noise(frequency_i × p)|`. Perlin's own description: "The application of the absolute value causes a bounce or crease in the noise function in all the places where its value crosses zero. When these modified noise functions are then summed over many scales, the result is visual cusps — discontinuities in gradient — at all scales and in all directions. The visual appearance is consistent with a licking flame-like effect." Catlike Coding implements this as a generic wrapper using `EvaluateAfterInterpolation` that applies abs() per octave before summation, not to the final result.

### Visual character and the marble connection

Unlike signed fBm (output approximately [-1, 1]), turbulence produces values in **[0, 1]** with visible creases at zero-crossings — billowy, cloud-like patterns. The classic marble texture synthesis (Perlin 1985) uses turbulence to phase-shift a sine wave: `marble(x,y,z) = sin(f × (x + a × turbulence(x,y,z)))`, where f is stripe frequency and a is distortion amount. The turbulence warps the regular stripe pattern, creating convincing veined marble. This is arguably the earliest example of domain warping (Section 12).

### Fundamental limitation: gradient discontinuity

The abs() operation introduces C⁰ but not C¹ continuity — first-derivative discontinuities at every zero crossing. This means **theoretically infinite frequency content**, making perfect antialiasing impossible. The PBR Book explicitly warns: "the first-derivative discontinuities introduced by taking the absolute value introduce infinitely high-frequency content, so [antialiasing] efforts can't hope to be perfectly successful." Turbulence is more prone to aliasing than fBm and may require higher sampling rates or careful octave limiting at distance. Like standard fBm, turbulence is statistically homogeneous and lacks the heterogeneity of real turbulent flow. Within the noise composition family, turbulence sits between fBm and ridged multifractal: fBm produces smooth hills, turbulence produces billowy creases, and ridged noise inverts those creases into sharp peaks with heterogeneous feedback.

---

## 12. Domain warping

### Noise-distorted noise

Domain warping uses noise to distort the input coordinates of another noise function before evaluation. The basic form is `f(p + noise(p))` — evaluate noise at the sample point to produce a displacement, then evaluate the base function at the displaced position. Inigo Quilez (iquilezles.org/articles/warp/) calls this "a very common technique in computer graphics for generating procedural textures and geometry... used since 1984, when Ken Perlin himself created his first procedural marble texture."

### Multi-level warping formulations

Quilez's formulation uses independent noise evaluations for each axis of displacement. **Level 1**: `q = (fbm(p + offset_a), fbm(p + offset_b)); result = fbm(p + 4.0 × q)`. The offset constants (e.g., vec2(5.2, 1.3) and vec2(0.0, 0.0)) ensure the x and y displacement channels are uncorrelated. The multiplier 4.0 controls warp strength. **Level 2** nests further: `r = (fbm(p + 4.0×q + offset_c), fbm(p + 4.0×q + offset_d)); result = fbm(p + 4.0 × r)`. The extreme case, `fbm(p + fbm(p + fbm(p)))`, produces increasingly organic, complex folding. Musgrave's earlier **Variable Lacunarity Noise** (1994) applied the same principle: evaluate a vector noise at a misregistered coordinate, scale the result, add it to the original position, and evaluate scalar noise at the displaced point.

Quilez's derivative-based domain warping (iquilezles.org/articles/morenoise/) injects analytical noise derivatives into the fBm loop: `d += n.yz; a += b × n.x / (1 + dot(d, d))`, where accumulated derivatives dampen subsequent octave contributions. This creates **heterogeneous terrain** — flat areas AND rough areas in the same field — and "simulates different erosion-like effects and creates some rich variety of shapes" (Quilez, 2008). A rotation matrix applied per octave (`p = m × p × 2.0`) decorrelates the layers.

### Artifacts and performance cost

**Concentric artifacts** appear when warp strength exceeds the noise feature size — the terrain wraps around peaks of the displacement noise, producing visible bullseye patterns. **Over-distortion** makes terrain look "painterly" with "strange peninsulas" rather than realistic coastlines. Using the same noise function for both warp and base creates self-referential patterns; Quilez uses distinct instances with different offsets. **Performance is multiplicatively expensive**: a 2-level warp with 6-octave fBm requires `(2×2 + 2 + 1) × 6 = 30` noise evaluations per sample (two fBm calls for x/y displacement at each of two warp levels, plus the final fBm call, each with 6 octaves). Domain warping is typically the most expensive procedural technique in a pipeline but produces uniquely organic, flowing results — geological strata, smoke plumes, lava flows — unachievable by simpler noise composition.

---

## 13. Layered masks and scalar field composition

### Scalar fields as the universal PCG data type

A scalar field in PCG is a function `f: ℝⁿ → ℝ` mapping every point to a single floating-point value — typically stored as a 2D array of floats (heightmaps, moisture maps) or 3D array (voxel densities). Range conventions vary: [0,1] or [-1,1] depending on the noise library. Interpretation is purely contextual: the same array of floats can represent elevation, moisture, temperature, or material presence depending on how downstream systems consume it. The power of scalar field composition lies in combining simple operations — addition, multiplication, min, max, threshold — to build complex behaviors from simple parts.

### Combination operations and mask generation

**Addition** layers features together (the basis of fBm's octave summation). **Multiplication** provides masking and modulation — one field controls another's amplitude, ensuring (for example) that fine detail only appears on mountain ridges. **Min and max** implement boolean operations when treating fields as implicit surfaces: union = max(a, b), intersection = min(a, b), subtraction = min(a, −b) for fields where higher values mean "more solid." **Smoothstep thresholding** converts a continuous field to a mask with gradual transition: `smoothstep(edge0, edge1, x) = t² × (3 − 2t)` where `t = clamp((x − edge0)/(edge1 − edge0), 0, 1)`. Perlin's quintic smootherstep `6t⁵ − 15t⁴ + 10t³` provides C² continuity for higher-quality transitions (Ken Perlin, "Improving Noise," 2002). Hard thresholding (`step()`) produces binary masks for collision, pathfinding, and spawn rules.

### The stacking pipeline

A typical PCG scalar field pipeline follows a pattern: **base shape** (radial falloff, SDF primitive) → **noise detail** (fBm, ridged, Worley) → **combine** (multiply, lerp, add) → **redistribute** (power curve to flatten valleys or sharpen peaks) → **threshold** (smoothstep to biome boundaries) → **output** (2D array of enum or float values). Amit Patel's island generation tutorial (Red Blob Games, terrain-from-noise) demonstrates this concretely: a "Square Bump" distance function `d = 1 − (1 − nx²) × (1 − ny²)` combined with noise via `elevation = lerp(noise_value, 1 − d, mix_parameter)`. For biome classification, two independent scalar fields — elevation and moisture — serve as lookup coordinates into a Whittaker-style biome table: `biome = lookup[elevation][moisture]`, with each field generated from fBm with independent seeds.

### Practical composition patterns

Ridged noise benefits from **multiplicative amplitude modulation** where lower octaves' values gate higher octaves: `e1 = 0.5 × ridgenoise(2 × p) × e0`, ensuring fine detail appears only on ridges. Cave systems use **3D noise thresholded to binary** — voxels where `fbm_3d(x,y,z) > threshold` become solid, others become air. Advanced cave "worms" (Voxel Tools documentation) square the noise and threshold near zero to get path-like patterns, then modulate the threshold along the Y axis with a parabola `y² − 1` and subtract from the terrain SDF using smooth subtraction. Combining different noise types serves different scales: large-scale Perlin/Simplex for continent outlines, Worley for rocky detail and cell patterns, ridged noise for mountain ridges — all composited via the operations above.

---

## 14. Signed distance fields for procedural generation

### SDFs as continuous geometry descriptions

A signed distance field is a scalar function `f: ℝⁿ → ℝ` returning the signed Euclidean distance from a point to the nearest surface: **negative inside**, **positive outside**, **zero at the boundary**. The gradient of a proper SDF has unit magnitude everywhere (`‖∇f(p)‖ = 1`, the Eikonal equation). For PCG, SDFs bridge analytical geometry and discrete representations — they can be sampled at any resolution, combined with other SDFs via trivial operations, warped with noise, and converted to meshes. Inigo Quilez (iquilezles.org/articles/distfunctions/ and /distfunctions2d/) is the definitive reference, providing exact distance functions for dozens of 2D and 3D primitives.

### Primitives and operations

The fundamental 2D primitives: **circle** `length(p) − r`, **box** `length(max(abs(p) − b, 0)) + min(max(abs(p).x − b.x, abs(p).y − b.y), 0)`, **line segment** using projected parametric distance. In 3D: **sphere** `length(p) − r`, **box** with the same max/min pattern extended to three dimensions. **Rounding** any SDF by subtracting a constant `sdShape(p) − r` moves the iso-surface outward and rounds corners. **Onion/annular** shells via `abs(sdShape(p)) − r`. CSG operations are single-line functions: **union** = `min(d1, d2)`, **intersection** = `max(d1, d2)`, **subtraction** = `max(−d1, d2)`. Quilez notes an important caveat: union and XOR produce true SDFs in the exterior, but subtraction and intersection produce only **distance bounds** (not exact distances) in the interior.

### Smooth blending with smin

The **smooth minimum** replaces `min()` to create organic blending between primitives — "as if the primitives were made of clay" (Quilez). Parameter k controls blend radius in distance units. The most common variant is the **polynomial quadratic smooth min**: `h = max(k − |a−b|, 0)/k; return min(a,b) − h²×k×0.25`. The widely cited alternative formulation: `h = clamp(0.5 + 0.5×(b−a)/k, 0, 1); return mix(b, a, h) − k×h×(1−h)`. Quilez also provides cubic (smoother C¹ transition), exponential (non-local blending, infinite support), and root variants. Smooth CSG operations build directly from smin: `opSmoothUnion(d1, d2, k) = smin(d1, d2, k)`, `opSmoothSubtraction(d1, d2, k) = smax(−d1, d2, k)`. The smin function can also return a blend factor for interpolating materials between shapes (iquilezles.org/articles/smin/).

### SDFs in PCG pipelines

Complex level geometry can be defined as compositions of SDF primitives: ground planes united with cylinders (pillars), smooth-subtracted spheres (caves), and noise-displaced surfaces (rocky walls). **Cave generation** with SDF + noise (Voxel Tools): compute base terrain SDF, compute squared 3D noise for worm-like paths near zero, modulate threshold by height, and smooth-subtract from terrain. Quilez demonstrated building entire 3D scenes — human figures, marine life, landscapes — purely from SDF primitives and smooth blending, achieving infinite resolution with no stored geometry. SDFs relate directly to noise fields: a thresholded noise field defines an implicit surface, though distances aren't strictly Euclidean (which matters for raymarching step sizes but not for mesh extraction). The hybrid approach — SDF primitives for large-scale structure plus noise for organic detail, combined via smooth min/max — is increasingly standard in voxel engines and procedural modeling tools. Conversion to mesh uses **marching squares** (2D) or **marching cubes** (3D, Lorensen & Cline 1987): for each grid cell, classify vertices as inside/outside, look up triangle configuration from a 256-entry table, and interpolate vertex positions along edges where the field crosses zero.

---

## Perlin vs. Simplex vs. OpenSimplex vs. Worley

These four noise types differ fundamentally in grid structure, computational complexity, and visual character. **Classic/Improved Perlin** uses a hypercubic grid with 2^N gradient evaluations per point (4 in 2D, 8 in 3D, 16 in 4D) and sequential axis-aligned interpolation via a quintic fade curve. **Simplex** uses a simplicial grid (triangles in 2D, tetrahedra in 3D) with only N+1 gradient evaluations — achieving O(N²) scaling vs. Perlin's O(2^N). It replaces interpolation with direct summation of radially symmetric surflet contributions using a `(r² − d²)⁴` kernel. **OpenSimplex2** uses different lattice structures (tetrahedral-octahedral honeycomb in 3D, body-centered cubic grid) with orientation-aware variants; OpenSimplex2F approximates simplex speed while OpenSimplex2S provides smoother output at ~2× cost. **Worley** uses scattered feature points in a regular grid with distance-based output rather than gradient-based output, requiring 3^N cell checks per evaluation (9 in 2D, 27 in 3D) — Houdini benchmarks it at **1.8× Perlin's cost**.

Visually, Perlin produces smooth organic blobs with subtle axis-aligned artifacts (especially in 2D slices of 3D noise using the 12-gradient set). Simplex is similarly smooth but can exhibit slight triangular/hexagonal grid artifacts in 2D. OpenSimplex2F looks most like simplex with minor diagonal artifacts; OpenSimplex2S is the smoothest of the gradient noises. Worley's character is fundamentally different — cellular and bubbly (F1), or veiny and cracked (F2-F1). **When to choose**: for general-purpose smooth noise, **OpenSimplex2F** (with ImproveXZ for terrain) offers the best quality/speed balance and is patent-free. Use **OpenSimplex2S** for ridged noise where the smoother kernel avoids artifacts. Use **Worley F1** (Euclidean) for biological cells, caustics, and cobblestone patterns. Use **Worley F2-F1** (with Quilez's correct edge distance) for crack and vein networks. In 4D+ (e.g., animated 3D noise via time as fourth dimension), simplex/OpenSimplex's O(N²) scaling makes high dimensions tractable where Perlin's O(2^N) becomes prohibitive.

---

## Random placement vs. Poisson disk sampling

**Random placement** (white noise point distribution) scatters points uniformly and independently. It is trivially cheap — a single random coordinate per point — but produces visible clumps and gaps at all scales because each point is placed without regard to existing points. Its flat power spectrum means every spatial frequency is equally represented, including the low frequencies that the human visual system is most sensitive to. **Poisson disk sampling** enforces a minimum distance r between all points, producing blue noise distributions that are random at high frequencies but uniform at low frequencies. The quality difference is dramatic: random placement of 1000 trees produces obviously clumped forests with barren clearings, while Poisson disk sampling produces natural-looking coverage with organic irregularity.

Bridson's algorithm achieves **O(N) time** — the same asymptotic cost as random placement — making the quality improvement essentially free at scale. Implementation complexity is modest (~50–100 lines vs. 1 line for random placement). The tradeoff is that Poisson disk sampling controls spacing (minimum distance r) but not point count directly, while random placement controls count directly. For PCG, Poisson disk sampling is the clear winner for any placement task where visual quality matters — vegetation, buildings, resources, enemies, stars. Random placement remains appropriate for prototyping, for effects where clustering is acceptable (particle systems, debris), and for very large sample counts where statistical convergence dominates perceptual quality. Stratified/jittered grids sit between these extremes: O(N) generation with direct count control and partial blue noise properties, but with unavoidable grid-aligned artifacts.

---

## What these techniques produce and how downstream systems consume it

The fourteen techniques in this document produce four fundamental data types. **Scalar fields** (2D float arrays, typically 256²–1024² at 4 bytes per sample) are the most common output — heightmaps from fBm, density fields from ridged/turbulence noise, moisture/temperature maps for biome classification. Downstream, they feed into renderers (as vertex displacement or texture), physics systems (as collision heightmaps), biome classifiers (via threshold lookup against Whittaker-style tables), and mesh extraction (marching cubes on 3D scalar fields). **Point sets** (arrays of 2D/3D coordinates) are produced by Poisson disk sampling, stratified sampling, and Worley's feature point generation. They serve as object placement positions (trees, buildings, resources), Voronoi cell centers for region partitioning, or input to Delaunay triangulation for connectivity graphs. **Binary masks** (boolean arrays or bit-packed grids, ~8 KB for 256²) result from thresholding scalar fields and define walkability grids for pathfinding, collision regions, spawn rules, and biome boundaries. **Distance fields** (signed float arrays with the Eikonal property) produced by SDF primitives and operations serve raymarching renderers, collision detection (query any point for distance-to-surface), soft shadow computation (Quilez's penumbra technique), and font rendering.

In practice, PCG pipelines chain these data types: coordinate hashing produces pseudorandom values → noise functions (Perlin, Simplex, Worley) interpolate them into smooth scalar fields → fBm/ridged/turbulence layer octaves into fractal fields → domain warping distorts those fields for organic character → scalar field composition (multiply, add, smooth min) combines multiple layers → thresholding produces masks and biome assignments → SDFs define large-scale geometry → marching cubes extracts meshes → Poisson disk sampling places objects on the resulting surfaces. **Chunked grids** (typically 32³ or 64³ voxels) enable streaming and level-of-detail. **GPU textures** (R32F or R16F format) store scalar fields for shader-side sampling. **Octree compression** reduces memory for sparse SDF volumes. The analytical form (function/shader code, measured in bytes) is increasingly preferred over baked data for SDF-based content, offering infinite resolution and trivial CSG at the cost of runtime evaluation.

---

## Source landscape

### Foundational papers

Ken Perlin's "An Image Synthesizer" (SIGGRAPH 1985, pp. 287–296) introduced gradient noise and turbulence; his "Improving Noise" (ACM Transactions on Graphics 21(3), 2002, pp. 681–682) fixed the interpolation curve and gradient set. Steven Worley's "A Cellular Texture Basis Function" (SIGGRAPH 1996, pp. 291–294) introduced distance-based cellular noise. Robert Bridson's "Fast Poisson Disk Sampling in Arbitrary Dimensions" (SIGGRAPH 2007 Sketches) presented the O(N) algorithm in a single-page paper. Stefan Gustavson's "Simplex noise demystified" (Linköping University, 2005) remains the definitive accessible explanation of simplex noise. F. Kenton Musgrave's source code in *Texturing and Modeling: A Procedural Approach* (Ebert et al., Academic Press) defines the canonical implementations of fBm, ridged multifractal, and heterogeneous terrain noise. Melissa O'Neill's "PCG: A Family of Simple Fast Space-Efficient Statistically Good Algorithms" (Harvey Mudd College, 2014) established the PCG PRNG family.

### Tutorial series and interactive references

Jasper Flick's **Catlike Coding** noise series (catlikecoding.com/unity/tutorials/pseudorandom-noise/) covers hashing, value noise, Perlin noise, simplex noise, Voronoi noise, and noise variants with complete Unity/C# implementations using SmallXXHash and SIMD vectorization. Amit Patel's **Red Blob Games** (redblobgames.com) provides interactive explorations of noise-based terrain generation, island shaping, Poisson disk sampling, jittered grids, and Voronoi maps — the terrain-from-noise article is particularly authoritative on scalar field composition and fBm parameterization. **Inigo Quilez** (iquilezles.org/articles/) maintains the definitive collection of SDF primitives and operations, smooth minimum variants, Voronoi edge computation, smooth Voronoi, domain warping, noise derivatives, and fBm — his Shadertoy implementations serve as ground-truth references for shader-based noise. **The Book of Shaders** (thebookofshaders.com, Chapters 11–13) provides accessible GLSL-based introductions to noise, fBm, turbulence, and cellular noise. The **PBR Book** (pbr-book.org) covers antialiased fBm and turbulence implementation with derivative-based octave clamping.

### Talks and video

Squirrel Eiserloh's **GDC 2017 talk** "Math for Game Programmers: Noise-Based RNG" (GDC Vault) is the primary reference for coordinate hashing in games, introducing the Squirrel3 hash function and the concept of stateless noise-based randomness. **Sebastian Lague's** YouTube series on procedural generation covers Perlin noise terrain, mesh generation, and erosion simulation with clear visual explanations. The improved **SquirrelNoise5** hash (2021) is documented in Kevin Moran's GitHub gist (SquirrelNoise5.hpp, CC-BY-3.0).

### Repositories and implementations

**KdotJPG/OpenSimplex2** (github.com/KdotJPG/OpenSimplex2) provides OpenSimplex2F and OpenSimplex2S in Java, C#, GLSL, and other languages with orientation-aware 3D functions. **Auburn/FastNoise2** (GitHub) offers SIMD-optimized implementations of all major noise types with a node-graph composition system. McEwan et al.'s "Efficient computational noise in GLSL" (arXiv:1204.1461, 2012) provides texture-free GPU noise implementations. Christoph Peters' **free blue noise textures** (momentsingraphics.de/BlueNoise.html) are widely used in game rendering. Alan Wolfe's **demofox.org** blog provides detailed implementation walkthroughs of void-and-cluster blue noise generation, Mitchell's best-candidate algorithm, and blue noise sampling theory. Bart Wronski's ~50-line **NumPy/JAX void-and-cluster** implementation (bartwronski.com, 2021) demonstrates practical blue noise texture generation.