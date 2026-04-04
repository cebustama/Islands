# Procedural Content Generation Technique Library for Videogames

## A Deep Technical Reference

---

# A. Executive Summary

Procedural content generation (PCG) in videogames rests on a relatively small number of foundational technique families that combine in well-understood ways to produce terrain, dungeons, biomes, vegetation, and world structure. This report catalogues those families, their algorithmic details, their inputs and outputs, their variants, and how they compose into generation pipelines.

## The Foundational Technique Families

**1. Noise and Scalar Fields** — The single most broadly useful PCG family. Coherent noise functions (Perlin, Simplex, Worley) and their derivatives (fractal Brownian motion, ridged multifractals, domain warping) produce the continuous scalar fields that underpin nearly all terrain, biome, and environmental generation. Almost every PCG pipeline begins with noise.

**2. Randomness, Determinism, and Sampling** — The infrastructure layer. Seeded PRNGs, coordinate-based hashing, shuffle bags, and Poisson disk sampling provide the controlled randomness that every other technique depends on. Not glamorous, but critical to get right for reproducibility and quality.

**3. Spatial Partitioning and Layout** — Voronoi diagrams, BSP trees, quadtrees, and region graphs divide space into meaningful units. These are the organizational backbone for dungeons, biome regions, settlement layouts, and world maps.

**4. Simulation and Erosion** — Hydraulic and thermal erosion transform noise-generated terrain into something that looks geologically plausible. River carving and drainage simulation add the features that make terrain feel real rather than synthetic.

**5. Rule-Based and Constraint-Based Generation** — Wave Function Collapse (WFC), L-systems, grammars, and autotiling impose local or structural rules to produce coherent output from modular pieces. These excel at structured content like buildings, dungeons, and tilesets.

**6. Graph-Based World Structure** — Overworld graphs, lock-and-key structures, critical path generation, and region connectivity graphs handle the progression and navigability aspects of generated worlds.

## Which Techniques Are Most Broadly Useful

For **world/terrain generation**: noise (fBm, ridged multifractal, domain warping) + erosion + Voronoi regions for biomes.

For **dungeon generation**: BSP, cellular automata, graph-driven room placement, random walks.

For **biome assignment**: layered noise fields (temperature, moisture, altitude) + Whittaker-style lookup.

For **object/vegetation placement**: Poisson disk sampling + density fields + slope/biome filtering.

For **tile-based content**: WFC, autotiling, Wang tiles.

For **progression-aware spaces**: graph-based layout with lock-and-key constraints.

---

# B. Source Landscape

## Tutorial / Educational (Highest Value)

| Source | Focus | Why It Stands Out |
|--------|-------|-------------------|
| **Red Blob Games** (Amit Patel) | Map generation, pathfinding, Voronoi, noise, hex grids | Gold standard for interactive algorithm explanation. The polygon map generation article is foundational. Explains *why* each decision is made, not just how. |
| **Catlike Coding** (Jasper Flick) | Noise (value, Perlin, simplex, Voronoi), procedural meshes, surfaces | Exceptionally rigorous Unity-based tutorials. Covers noise from first principles with full implementation detail including vectorization. |
| **Sebastian Lague** | Terrain generation, hydraulic erosion, mesh generation, marching cubes | Video tutorials with open-source Unity projects. Hydraulic erosion video/repo is the most widely referenced implementation. |
| **Inigo Quilez** | SDF, noise math, distance functions, ray marching | Canonical reference for noise mathematics, distance fields, and domain warping. Articles are terse but precise. |

## Technical / Dev Blog

| Source | Focus | Why It Stands Out |
|--------|-------|-------------------|
| **Boris the Brave** (Adam Newgas) | WFC, constraint-based generation, Tessera/DeBroglie | Definitive WFC resource. Explains constraint propagation from first principles before arriving at WFC. Practical tips for game integration. |
| **RogueBasin** | Dungeon algorithms, cellular automata, BSP, roguelike techniques | Community wiki with pseudocode for many classic algorithms. BSP dungeon and cellular automata cave articles are standard references. |
| **Antonios Liapis** | Dungeon generation taxonomy, agent-based diggers, PCG book chapters | Academic but practical. Provides structured comparison of dungeon generation methods with pseudocode. |
| **Herbert Wolverson** (Bracket Productions) | Roguelike development, Rust, multiple dungeon algorithms | "Hands-On Rust" and the online roguelike tutorial implement BSP, cellular automata, drunkard's walk, WFC, and more in a single comparative framework. |

## Talk / Presentation

| Source | Focus |
|--------|-------|
| **GDC Vault** — various PCG talks | No Man's Sky (terrain, biomes), Caves of Qud (WFC), Dwarf Fortress (world simulation), Spelunky (level design) |
| **Roguelike Celebration** talks | Broad PCG community; accessible talks on specific techniques |
| **Brian Bucklew** (GDC 2019, Roguelike Celebration 2019) | WFC in production (Caves of Qud). Discusses overfitting, homogeneity, and combining WFC with constructive methods |
| **Oskar Stålberg** | WFC in 3D for Bad North. Demonstrates large tiles, smooth curves, island generation |

## Repository / Code Reference

| Source | Focus |
|--------|-------|
| **mxgmn/WaveFunctionCollapse** (GitHub) | Original WFC implementation. README explains overlapping and simple tiled models |
| **SebLague/Hydraulic-Erosion** (GitHub) | Clean Unity implementation of particle-based hydraulic erosion |
| **SebLague/Procedural-Landmass-Generation** (GitHub) | Chunk-based terrain with noise, LOD, threading |
| **redblobgames/mapgen4** (GitHub) | Voronoi-based map generation with real-time erosion, wind, rainfall simulation |
| **BorisTheBrave/DeBroglie** (GitHub) | Production WFC library (C#) with backtracking, constraints, hex grid support |

## Paper / Research

| Source | Focus |
|--------|-------|
| **Ken Perlin** (1985, 2002) | Original Perlin noise; improved Perlin noise. Foundation of all gradient noise |
| **Robert Bridson** (2007) | Fast Poisson Disk Sampling in Arbitrary Dimensions. The O(n) algorithm everyone uses |
| **Maxim Gumin** (2016) | WFC algorithm. Based on Paul Merrell's model synthesis work |
| **Hans Theobald Beyer** | Implementation of hydraulic erosion methods. Technical basis for Lague's implementation |
| **Stefan Gustavson** | Simplex noise demystified. Clearest explanation of simplex noise internals |

---

# C. Taxonomy of PCG Techniques

## Technique Family Tree

```
PCG Techniques
├── Field-Based Techniques
│   ├── Noise Functions (Value, Perlin, Simplex, Worley)
│   ├── Noise Composition (fBm, ridged multifractal, turbulence)
│   ├── Domain Warping
│   ├── Distance Fields / SDFs
│   └── Layered Masks and Scalar Fields
│
├── Partitioning / Layout Techniques
│   ├── Voronoi Diagrams / Delaunay Triangulation
│   ├── Binary Space Partitioning (BSP)
│   ├── Quadtrees / Octrees
│   ├── Region Graphs
│   └── Connected Component Analysis
│
├── Graph-Based Techniques
│   ├── Overworld / Region Connectivity Graphs
│   ├── Lock-and-Key Structures
│   ├── Critical Path Generation
│   ├── Mission Graphs
│   └── Minimum Spanning Trees / Delaunay for Connectivity
│
├── Simulation / Erosion Techniques
│   ├── Hydraulic Erosion (particle-based)
│   ├── Thermal Erosion
│   ├── River / Drainage Simulation
│   └── Tectonic / Plate Simulation
│
├── Rule / Constraint-Based Techniques
│   ├── Wave Function Collapse (WFC)
│   ├── L-Systems
│   ├── Shape Grammars
│   ├── Cellular Automata
│   ├── Wang Tiles
│   └── Autotiling
│
├── Scattering / Placement Techniques
│   ├── Poisson Disk Sampling
│   ├── Density Field Sampling
│   ├── Stratified / Jittered Sampling
│   └── Hierarchical Scattering
│
└── Multi-Scale / Hierarchical Techniques
    ├── Chunk-Based Generation
    ├── LOD-Aware Generation
    ├── World-Map-to-Local-Map Pipelines
    └── Coordinate-Based Deterministic Regeneration
```

## How Families Relate

Field-based techniques produce the **raw data** (scalar fields, heightmaps, masks). Partitioning techniques create **spatial structure** from that data. Graph-based techniques impose **progression and connectivity** logic. Simulation techniques **refine** field data toward physical plausibility. Rule/constraint techniques generate **structured local content** (tiles, modules, rooms). Scattering techniques **populate** the world with objects. Multi-scale techniques **manage complexity** across different zoom levels and streaming boundaries.

A typical generation pipeline flows roughly: **noise → fields → partitioning → simulation → placement → rule-based detailing → graph-based connectivity verification**.

---

# D. Technique Cards

---

## D.1 — Seeded Pseudorandom Number Generation

**Category:** Randomness & Determinism
**Core Purpose:** Provide reproducible random number sequences from a seed value, enabling deterministic world generation.

**Typical Use Cases:** Every PCG system. Seeds allow players to share worlds, developers to reproduce bugs, and chunk systems to regenerate terrain on demand.

**Inputs:** Integer seed value, optional position/coordinate for hashing.
**Outputs:** Sequence of pseudorandom numbers (integers or floats in [0,1]).

**Core Algorithm Idea:** A mathematical function that produces a sequence of values that appear statistically random but are fully determined by the initial seed. Each call advances internal state.

**Step-by-Step:**
1. Initialize generator with seed value
2. Each call transforms internal state via bitwise/arithmetic operations
3. Output is derived from current state
4. State advances, ensuring next call produces different value

**Common Variants:**
- **Linear Congruential Generators (LCG):** Fast, poor quality. `state = (a * state + c) mod m`. Common in older games.
- **Mersenne Twister:** High quality, large state (624 words). Standard in many languages.
- **xoshiro / xorshift families:** Modern, fast, good quality. Preferred for games.
- **Squirrel Eiserloh's "SquirrelNoise":** Hash-based, position-dependent. Converts (position, seed) directly to a pseudorandom value without sequential state. Excellent for coordinate-based generation. Presented at GDC 2017.
- **PCG family (Melissa O'Neill):** Compact state, excellent statistical quality, permutation-based.

**Strengths:** Determinism enables reproducibility, save/load, multiplayer sync, streaming.
**Weaknesses:** Poor generators produce visible patterns. Sequential generators require careful management of call order for determinism.

**Failure Modes:** Using `Math.random()` or system RNG breaks determinism. Changing generation order silently changes all subsequent output. Float precision issues across platforms.

**Combinations:** Foundation for everything. Hash-based variants (SquirrelNoise) pair especially well with chunk generation and coordinate-based systems.

**Key Insight:** For PCG, **hash-based / position-based randomness** (where you convert a coordinate directly to a random value) is often more useful than sequential PRNGs, because it allows random access to any location without generating the sequence up to that point.

**Difficulty:** Understanding: Low. Implementation: Low-Medium (easy to use poorly).

**Best Sources:** Squirrel Eiserloh, GDC 2017 "Noise-Based RNG"; Melissa O'Neill, PCG paper; Red Blob Games discussions on seeding.

---

## D.2 — Poisson Disk Sampling

**Category:** Sampling & Placement
**Core Purpose:** Generate a set of random points where no two points are closer than a minimum distance, producing a natural-looking "blue noise" distribution.

**Typical Use Cases:** Foliage placement, rock scattering, building placement, NPC spawn points, star fields, any situation where random-but-evenly-spaced points are needed.

**Inputs:** Domain bounds, minimum distance `r` between samples, rejection limit `k` (typically 30).
**Outputs:** Set of 2D (or nD) points satisfying the minimum distance constraint.

**Core Algorithm Idea (Bridson's Algorithm):**
1. Create background grid with cell size `r/√n` (ensures at most one sample per cell)
2. Place first sample randomly, add to active list
3. While active list is non-empty:
   a. Pick random active sample
   b. Generate up to `k` candidate points in annulus [r, 2r] around it
   c. For each candidate, check grid neighbors for distance violations
   d. If valid, add to points and active list
   e. If all `k` candidates rejected, remove from active list
4. Return all accepted points

**Common Variants:**
- **Bridson's (2007):** O(n) in number of output points. The standard. Simple, fast, easy to implement.
- **Dart throwing:** Naive rejection sampling. Exponentially slow as density increases.
- **Variable-radius Poisson disk:** Minimum distance varies spatially (e.g., modulated by a density map). Requires cell size based on maximum radius; otherwise same algorithm.
- **Weighted sample elimination:** Start with many random points, iteratively remove those too close. Different quality characteristics.
- **Parallel/tiled approaches:** Split domain into independent tiles, merge with gap-filling. Useful for multi-threaded generation.

**Strengths:** Produces visually pleasing, natural distributions. No clumping, no grid artifacts. O(n) with Bridson's algorithm. Simple to implement.

**Weaknesses:** Not trivially tileable (points near edges need neighbor awareness). Sequential nature of Bridson's algorithm limits parallelism. Variable-radius version is more complex.

**Failure Modes:** Forgetting to check all neighboring grid cells (must check 5×5 neighborhood in 2D, not just 3×3 — though 3×3 around the 2-cell radius is sufficient with `r/√2` cell size). Using too small `k` produces sparser-than-maximal distributions.

**Combinations:** Poisson disk points → slope/biome filtering → object instantiation. Often combined with density fields: multiply `r` by inverse of density to get variable spacing.

**2D vs 3D:** Works identically in 3D (cell size `r/√3`, check 3×3×3 neighborhood). Volume scattering for particle systems, 3D foliage, asteroid fields.

**Grid vs Continuous:** Inherently continuous-space. The background grid is an acceleration structure, not a constraint on output positions.

**Difficulty:** Understanding: Low. Implementation: Low (Bridson's is ~50 lines of code).

**Performance:** O(n) in output size. Very fast. Grid lookup makes neighbor checking O(1).

**Best Sources:** Bridson (2007) original paper (1 page!); Sebastian Lague video; Dev.Mag tutorial by Herman Tulleken; Red Blob Games discussion.

---

## D.3 — Value Noise

**Category:** Noise & Scalar Fields
**Core Purpose:** Generate smooth, continuous pseudorandom scalar fields by interpolating random values assigned to integer lattice points.

**Typical Use Cases:** Basic terrain heightmaps, texture variation, simple displacement. Often the first noise type people learn.

**Inputs:** Continuous coordinate(s) (1D, 2D, or 3D), seed/hash function, frequency.
**Outputs:** Single scalar value per input coordinate, typically in range [-1, 1] or [0, 1].

**Core Algorithm Idea:**
1. For input point, find surrounding lattice cell (floor of coordinates)
2. Hash each lattice corner to get a pseudorandom value
3. Interpolate between corner values using the fractional position within the cell
4. Use smooth interpolation (Hermite/quintic) to avoid discontinuities at cell boundaries

**Step-by-Step (2D):**
1. Given point (x, y), compute integer corners: (⌊x⌋, ⌊y⌋), etc.
2. Hash each of the 4 corners to get random values v00, v01, v10, v11
3. Compute fractional parts fx = x - ⌊x⌋, fy = y - ⌊y⌋
4. Apply smoothstep: sx = 3fx² - 2fx³ (or quintic: 6fx⁵ - 15fx⁴ + 10fx³)
5. Bilinearly interpolate: lerp(lerp(v00, v10, sx), lerp(v01, v11, sx), sy)

**Common Variants:**
- **Linear interpolation:** Produces visible seams at cell boundaries. Not recommended.
- **Hermite (smoothstep) interpolation:** Smooth, but first derivative is discontinuous at boundaries. Adequate for many uses.
- **Quintic interpolation:** Smooth first and second derivatives. Preferred for quality.

**Strengths:** Simplest noise to understand and implement. Good teaching tool.

**Weaknesses:** Produces axis-aligned artifacts (blocky appearance even with smooth interpolation). Less natural-looking than gradient noise. The interpolation structure is visible.

**Failure Modes:** Grid-aligned patterns visible at low frequencies. Using linear interpolation produces obvious cell boundaries.

**Combinations:** Rarely used alone in production. Usually replaced by Perlin or Simplex noise. Can be layered via fBm like any noise.

**2D vs 3D:** Extends naturally to 3D (trilinear interpolation of 8 corners). Cost scales with 2^n corners.

**Difficulty:** Understanding: Low. Implementation: Low.

**Performance:** Fast. 2^n hash lookups and interpolation per sample. Cheaper than gradient noise.

**Best Sources:** Catlike Coding "Value Noise" tutorial; various introductory noise tutorials.

---

## D.4 — Perlin Noise

**Category:** Noise & Scalar Fields
**Core Purpose:** Generate smooth, continuous pseudorandom scalar fields using gradient vectors at lattice points, producing more natural-looking patterns than value noise.

**Typical Use Cases:** Terrain heightmaps, cloud textures, bump maps, displacement, virtually any continuous random variation in games.

**Inputs:** Continuous coordinate(s), seed, frequency.
**Outputs:** Scalar value per coordinate, approximately in range [-1, 1] (exact range depends on dimension and implementation).

**Core Algorithm Idea:**
1. Find enclosing lattice cell
2. Assign a pseudorandom **gradient vector** to each lattice corner (via hashing)
3. Compute **dot product** of each gradient with the vector from that corner to the sample point
4. Interpolate the dot products using smoothstep/quintic curves

**Step-by-Step (2D):**
1. Given (x, y), find cell corners
2. Hash each corner to select a gradient vector from a predefined set (e.g., 12 vectors for 3D, or unit circle directions for 2D)
3. For each corner, compute offset vector from corner to (x, y)
4. Dot product of gradient and offset gives corner contribution
5. Apply quintic fade curves to fractional coordinates
6. Bilinearly interpolate the four dot products

**Common Variants:**
- **Classic Perlin (1985):** Used a permutation table and linear interpolation. Suffered from visible axis-aligned artifacts.
- **Improved Perlin (2002):** Ken Perlin's revision. Uses quintic interpolation (6t⁵ - 15t⁴ + 10t³), better gradient selection. Standard reference implementation.
- **Various gradient sets:** Different implementations use different gradient vectors. Affects output range and isotropy.

**Strengths:** Produces natural-looking, isotropic patterns without obvious grid alignment. Well-understood, broadly supported. Enormous ecosystem of tools and references.

**Weaknesses:** Slightly more expensive than value noise. The permutation table approach is not seed-friendly (requires rebuilding or hashing). Some implementations have subtle directional bias. Range is not exactly [-1, 1], making normalization tricky.

**Failure Modes:** Using the classic (1985) version produces visible axis-aligned artifacts. Not normalizing output properly causes subtle range issues in downstream processing. The permutation table approach makes seeding non-trivial.

**Combinations:** Foundation for fBm, ridged multifractal, turbulence, domain warping. Combined with everything.

**2D vs 3D:** Works in any dimension. 3D uses 8 corners with 3D gradients. Cost increases with dimension.

**Grid vs Continuous:** Lattice-based but outputs continuous values. The grid is internal structure.

**Difficulty:** Understanding: Medium (the gradient/dot-product step is the key insight). Implementation: Medium.

**Performance:** Moderate. More expensive than value noise due to gradient computation and dot products. Still very fast — millions of samples per frame on modern hardware.

**Best Sources:** Ken Perlin's 2002 paper; Catlike Coding Perlin noise tutorial; Inigo Quilez noise articles; Hugo Elias's classic (but simplified) explanation.

---

## D.5 — Simplex Noise

**Category:** Noise & Scalar Fields
**Core Purpose:** Alternative to Perlin noise that uses a simplex grid (triangles in 2D, tetrahedra in 3D) instead of a hypercube grid, potentially offering better isotropy and lower computational cost in higher dimensions.

**Typical Use Cases:** Same as Perlin noise. Sometimes preferred for performance in 3D+ or for aesthetic differences.

**Inputs / Outputs:** Same as Perlin noise.

**Core Algorithm Idea:**
1. Skew input coordinates to transform the simplex grid into a regular grid (for easier lattice lookup)
2. Determine which simplex within the skewed cell contains the sample point
3. For each corner of the simplex (3 in 2D, 4 in 3D), compute gradient contribution using a radial kernel (falloff function) rather than interpolation
4. Sum all contributions

**Key Difference from Perlin:** Instead of interpolating between all 2^n hypercube corners, simplex noise sums kernel-weighted contributions from only n+1 simplex corners. This means 3 corners in 2D (vs 4 for Perlin) and 4 in 3D (vs 8).

**Common Variants:**
- **Original Simplex (Perlin 2001):** Was patented (US Patent 6,867,776 B2) for 3D and above. Patent expired January 2022.
- **OpenSimplex (Kurt Spencer 2014):** Created to avoid the patent. Uses a different lattice and kernel. Slightly different visual character. Available in OpenSimplex and OpenSimplex2 variants.
- **OpenSimplex2 (2020+):** Improved version with better quality and performance.

**Strengths:** Fewer artifacts than Perlin noise along axes. O(n²) complexity per dimension vs O(2^n) for Perlin. Lower computational cost in high dimensions. No interpolation step — uses additive kernels instead.

**Weaknesses:** More complex to implement than Perlin noise. Skewing step is non-intuitive. OpenSimplex variants have slightly different visual character that may or may not be preferred. Some implementations have subtle quality issues.

**Failure Modes:** Incorrect skewing constants produce broken patterns. The simplex traversal order is fiddly to get right in 3D+.

**Difficulty:** Understanding: Medium-High. Implementation: Medium-High.

**Best Sources:** Catlike Coding Simplex Noise tutorial (most rigorous); Stefan Gustavson "Simplex noise demystified"; Ken Perlin's original paper.

---

## D.6 — Worley / Voronoi Noise

**Category:** Noise & Scalar Fields
**Core Purpose:** Generate cellular, organic patterns by computing distances to randomly placed feature points within a lattice grid.

**Typical Use Cases:** Stone/rock textures, cracked earth, cell patterns, biological structures, crystal formations. Also used for region boundaries and stylized terrain.

**Inputs:** Continuous coordinate(s), seed, frequency, distance metric choice.
**Outputs:** Distance value(s) — typically F1 (distance to nearest point), F2 (distance to second nearest), or combinations like F2-F1.

**Core Algorithm Idea:**
1. Partition space into grid cells
2. Place one or more random feature points in each cell
3. For a sample point, check the containing cell and all neighbors
4. Find the distances to the nearest feature points
5. Output desired distance value or combination

**Step-by-Step (2D):**
1. Given (x, y), find containing grid cell
2. For the 3×3 neighborhood of cells:
   a. Hash cell coordinates to determine feature point positions within each cell
   b. Compute distance from sample point to each feature point
3. Track the closest (F1) and second-closest (F2) distances
4. Output: F1 for basic Voronoi, F2-F1 for cell edges, F1*F2 for other effects

**Common Variants:**
- **F1 (nearest distance):** Produces bulging cell shapes. Classic Voronoi look.
- **F2-F1 (edge detection):** Highlights cell boundaries. Produces vein/crack patterns.
- **F2 (second nearest):** Produces rounded, overlapping cell shapes.
- **Different distance metrics:** Euclidean (round cells), Manhattan (diamond cells), Chebyshev (square cells). Each produces dramatically different patterns.
- **Multiple points per cell:** More points = smaller, more irregular cells.

**Strengths:** Produces unique cellular patterns impossible with gradient noise. Very versatile through distance metric and Fn combination choices. Natural-looking for geological and biological patterns.

**Weaknesses:** More expensive than Perlin/Simplex (must check all neighboring cells). Can produce visible grid artifacts if not enough neighbors are checked.

**Failure Modes:** Not checking enough neighboring cells causes discontinuities at cell boundaries. With only one point per cell, patterns can look too regular.

**Combinations:** Combined with Perlin/fBm for terrain variation. F2-F1 makes excellent river/crack networks when thresholded. F1 used as distance field for region-based effects.

**Difficulty:** Understanding: Medium. Implementation: Medium.

**Best Sources:** Catlike Coding Voronoi Noise tutorial; Inigo Quilez articles on Voronoi; Steven Worley's original 1996 paper.

---

## D.7 — Fractal Brownian Motion (fBm)

**Category:** Noise Composition
**Core Purpose:** Layer multiple octaves of noise at increasing frequencies and decreasing amplitudes to create rich, multi-scale detail from a single noise function.

**Typical Use Cases:** Terrain heightmaps, cloud textures, virtually any natural-looking noise pattern. The single most common noise composition technique.

**Inputs:** Base noise function, number of octaves, lacunarity (frequency multiplier per octave, typically 2.0), persistence/gain (amplitude multiplier per octave, typically 0.5), initial frequency and amplitude.
**Outputs:** Scalar field with multi-scale detail.

**Core Algorithm:**
```
result = 0
frequency = initialFrequency
amplitude = initialAmplitude
for each octave:
    result += amplitude * noise(position * frequency)
    frequency *= lacunarity
    amplitude *= persistence
return result
```

**Key Parameters:**
- **Octaves:** Number of layers. More = more fine detail. 4-8 typical for terrain.
- **Lacunarity:** How much frequency increases per octave. 2.0 is standard (each octave is double the frequency).
- **Persistence (gain):** How much amplitude decreases per octave. 0.5 is standard (each octave is half the amplitude). Lower values = smoother. Higher values = rougher.

**Strengths:** Simple, predictable, highly controllable. Produces natural-looking fractal detail. Works with any base noise function.

**Weaknesses:** Linearly accumulates artifacts from the base noise. Can look "samey" — the classic "Perlin terrain" look. Adding octaves increases cost linearly.

**Failure Modes:** Too many octaves at high persistence produces noisy, spiky terrain. Too few octaves looks smooth and artificial. Terrain generated purely with fBm looks like a cloud, not like real terrain — it lacks geological features.

**Combinations:** Base layer for nearly all terrain generation. Combined with erosion to add realism. Combined with masks for biome variation. Domain warping of fBm produces more organic shapes.

**Difficulty:** Understanding: Low. Implementation: Low (literally a for loop over noise calls).

**Best Sources:** Red Blob Games "Making maps with noise"; Inigo Quilez fBm articles; any noise tutorial.

---

## D.8 — Ridged Multifractal Noise

**Category:** Noise Composition
**Core Purpose:** Variant of fBm that creates sharp ridges and crests, useful for mountain ranges and other geological features.

**Inputs:** Same as fBm, plus an offset parameter.
**Outputs:** Scalar field with prominent ridge features.

**Core Algorithm:**
```
result = 0
frequency = initialFrequency
amplitude = initialAmplitude
for each octave:
    signal = offset - abs(noise(position * frequency))
    signal = signal * signal    // sharpen ridges
    result += signal * amplitude
    frequency *= lacunarity
    amplitude *= persistence * signal  // weight by previous octave
return result
```

**Key Difference from fBm:** Taking the absolute value of noise and subtracting from an offset creates sharp creases at zero-crossings. Squaring sharpens these into ridges. Weighting each octave's amplitude by the previous signal creates detail concentrated in valleys/ridges.

**Strengths:** Produces dramatic mountain ridges, veins, and sharp features that fBm cannot. Very effective for mountainous terrain.

**Weaknesses:** Harder to control than fBm. The amplitude-weighting-by-signal makes behavior less predictable. Can produce very extreme values requiring careful normalization.

**Failure Modes:** Without careful parameter tuning, produces excessively spiky terrain. The amplitude feedback loop can cause numerical instability with many octaves.

**Combinations:** Often blended with regular fBm: use ridged noise for mountainous regions, fBm for plains. The blend can be controlled by a mask.

**Difficulty:** Understanding: Medium. Implementation: Low-Medium.

**Best Sources:** Inigo Quilez; Mussgrave's "Texturing and Modeling: A Procedural Approach" (original description).

---

## D.9 — Domain Warping

**Category:** Noise Composition
**Core Purpose:** Feed noise into itself by using one noise function to distort the input coordinates of another, producing organic, swirling, flowing patterns.

**Typical Use Cases:** Organic terrain shapes, fantasy/alien landscapes, cloud formations, marble textures, lava flows.

**Inputs:** Base noise function, warping noise function(s), warp strength.
**Outputs:** Distorted scalar field.

**Core Algorithm:**
```
// Simple single-layer warp:
result = noise(position + warpStrength * vec2(
    noise(position + offset1),
    noise(position + offset2)
))

// Multi-layer warp (Inigo Quilez style):
q = vec2(noise(p + c1), noise(p + c2))
r = vec2(noise(p + scale*q + c3), noise(p + scale*q + c4))
result = noise(p + scale*r)
```

**Key Insight:** The offsets (c1, c2, c3, c4) prevent the warping noise from correlating with the warped noise, which would produce artifacts.

**Strengths:** Produces highly organic, non-repetitive patterns. Breaks up the regularity of standard noise. Can be layered for increasingly complex warping. Relatively cheap to add character to noise.

**Weaknesses:** Doubles or triples noise evaluation cost per layer of warping. Can produce unpredictable results — hard to art-direct. Excessive warping creates mushy, unreadable output.

**Failure Modes:** Without offsets, warping noise correlates with base noise, producing blotchy artifacts. Too much warp strength destroys all coherent structure.

**Difficulty:** Understanding: Medium. Implementation: Low.

**Best Sources:** Inigo Quilez "Domain Warping" article (canonical reference with beautiful examples and exact formulas).

---

## D.10 — Heightmap Terrain Generation

**Category:** Terrain & Landform Generation
**Core Purpose:** Represent terrain as a 2D grid of elevation values, generating height data from noise and other sources.

**Typical Use Cases:** Outdoor terrain in virtually any 3D game with landscapes. The dominant terrain representation in games.

**Inputs:** Grid dimensions, noise parameters, optional masks, erosion parameters.
**Outputs:** 2D array of height values (the heightmap), typically normalized to [0,1] or in world-space units.

**Core Algorithm (typical pipeline):**
1. Generate base terrain shape using fBm noise
2. Apply continent/island mask (distance from center, or separate noise layer)
3. Add ridged multifractal for mountains
4. Optionally apply domain warping for organic coastlines
5. Apply hydraulic and/or thermal erosion
6. Generate mesh by interpreting heightmap as vertex Y positions
7. Apply texturing based on height, slope, and biome data

**Common Variants:**
- **Pure noise:** fBm directly as heightmap. Simple but generic-looking.
- **Masked/shaped noise:** Multiply noise by a falloff mask for islands, or add noise to a designer-painted base shape.
- **Multi-layer blending:** Different noise types for different height bands (fBm for lowlands, ridged for mountains).
- **Stamp-based:** Pre-authored height stamps blended at random positions over base noise. Breaks repetition.

**Strengths:** Simple data structure (2D array). Easy to process, erode, texture, and render. Mature tooling and ecosystem. LOD-friendly.

**Weaknesses:** Cannot represent overhangs, caves, arches, or any features where the terrain folds over itself. Limited to "2.5D" geometry. Single-layer by definition.

**Failure Modes:** Pure noise heightmaps look like clouds, not terrain. Without erosion, mountains lack drainage features. Without island masks, terrain extends infinitely without meaningful coastlines.

**Combinations:** Noise → heightmap → erosion → texturing → vegetation placement. Voronoi regions for biome assignment on the heightmap.

**2D vs 3D:** Inherently a 2D representation of a 3D surface. For true 3D terrain (caves, overhangs), voxel approaches are needed.

**Difficulty:** Understanding: Low. Implementation: Low-Medium (basic), Medium-High (with erosion pipeline).

**Best Sources:** Sebastian Lague's Procedural Landmass Generation series; Red Blob Games "Making maps with noise"; Catlike Coding surface tutorials.

---

## D.11 — Hydraulic Erosion (Particle-Based)

**Category:** Simulation & Erosion
**Core Purpose:** Simulate the effect of water flowing over terrain, carving valleys, depositing sediment, and creating natural-looking drainage patterns.

**Typical Use Cases:** Post-processing heightmap terrain to add realism. Creates valleys, riverbeds, alluvial fans, and smooths terrain in physically plausible ways.

**Inputs:** Heightmap, number of water droplets (iterations), erosion parameters (erosion rate, deposition rate, evaporation rate, sediment capacity, inertia, minimum slope).
**Outputs:** Modified heightmap with erosion features.

**Core Algorithm (particle-based / droplet simulation):**
```
for each droplet:
    position = random point on heightmap
    velocity = 0
    water = 1
    sediment = 0
    for each step (up to max lifetime):
        compute gradient at position (via bilinear interpolation)
        update direction: newDir = oldDir * inertia + gradient * (1 - inertia)
        move to new position along direction
        compute height difference between old and new position
        if moving uphill (in a pit):
            deposit sediment to fill pit
        else:
            compute sediment capacity = max(-heightDiff, minSlope) * velocity * water * capacityFactor
            if carrying more than capacity:
                deposit fraction of excess
            else:
                erode fraction of deficit (from surrounding heightmap cells)
        update velocity: sqrt(velocity² + heightDiff * gravity)
        evaporate water: water *= (1 - evaporationRate)
```

**Key Parameters:**
- **Inertia:** How much the droplet continues in its current direction vs following the gradient. Higher inertia = straighter paths.
- **Sediment capacity:** How much material a droplet can carry. Proportional to speed and water volume.
- **Erosion/deposition rates:** Control aggressiveness of terrain modification.
- **Erosion radius:** How wide an area each droplet affects. Larger = smoother but slower.

**Strengths:** Produces dramatically more realistic terrain than noise alone. Creates natural-looking valleys, ridges, and drainage patterns. Relatively simple to implement. The Sebastian Lague implementation is well-documented and widely used.

**Weaknesses:** Computationally expensive (tens of thousands to millions of droplets needed). Sequential by nature (though GPU implementations exist). Results depend heavily on parameter tuning. Can create unnaturally deep channels if parameters are wrong.

**Failure Modes:** Too few iterations = barely visible effect. Too many = terrain becomes a flat plain with deep canyons. Droplets getting stuck in local minima create circular pits. Inconsistent erosion radius creates visible seam-like artifacts.

**Common Simplifications in Tutorials:** Most tutorials (including Lague's) omit thermal erosion, use simplified sediment transport, and don't model water accumulation. Real erosion involves vastly more complex fluid dynamics.

**Combinations:** Always applied to noise-generated heightmaps. Often followed by river placement along the drainage paths created by erosion.

**Difficulty:** Understanding: Medium. Implementation: Medium. Tuning: High.

**Performance:** CPU: ~10-50ms for 10k droplets on a 256×256 heightmap. GPU compute shader implementations are 10-100× faster.

**Best Sources:** Sebastian Lague "Coding Adventure: Hydraulic Erosion" (video + GitHub); Hans Theobald Beyer's technical paper; Ranmantaru blog post on water erosion.

---

## D.12 — Cellular Automata (Caves)

**Category:** Rule-Based Generation
**Core Purpose:** Generate organic, cave-like structures by iteratively applying local rules to a grid of cells, similar to Conway's Game of Life.

**Typical Use Cases:** Cave systems, organic dungeon layouts, natural-looking terrain boundaries, coral formations.

**Inputs:** Grid dimensions, initial fill percentage (typically 45-55%), rule parameters (birth/death thresholds), number of iterations.
**Outputs:** Binary grid (wall/floor) or multi-state grid.

**Core Algorithm (B678/S345678 — the "4-5 rule"):**
1. Initialize grid: each cell is randomly wall (with probability ~45%) or floor
2. For each iteration:
   a. For each cell, count wall neighbors in Moore neighborhood (8 surrounding cells)
   b. Apply rule: cell becomes wall if wall_neighbors ≥ 5 (or if it was wall and wall_neighbors ≥ 4)
   c. Write results to new grid (double-buffered)
3. Repeat for 4-7 iterations

**Common Variants:**
- **Standard 4-5 rule:** A cell becomes wall if it has ≥ 5 wall neighbors, or if it was already wall and has ≥ 4 wall neighbors. Produces smooth, cave-like shapes.
- **Different thresholds:** B5678/S45678 produces more open caves. B678/S345678 produces tighter passages.
- **Multi-pass with different rules:** First pass creates large structures, second pass with different rules smooths them.
- **Flood-fill cleanup:** After CA, find connected components and keep only the largest, or connect disjoint caves with tunnels.

**Strengths:** Extremely simple to implement. Produces organic, natural-looking cave shapes. Fast (grid operations). Highly tunable through initial density and rule parameters.

**Weaknesses:** No guarantee of connectivity — caves may be disjoint. No control over cave structure (number of rooms, passages, etc.). Results are local — no global structure.

**Failure Modes:** Wrong initial density or rules produce either solid walls or empty space. Disconnected caves require post-processing to connect. Small isolated pockets need cleanup.

**Combinations:** Cellular automata + flood fill + corridor connection = complete cave dungeon. Can be combined with BSP or room placement: generate rooms via BSP, then use CA to make the rooms/corridors look organic.

**2D vs 3D:** Extends naturally to 3D (26-cell neighborhood). 3D CA caves can be extracted with marching cubes.

**Difficulty:** Understanding: Low. Implementation: Low.

**Performance:** Very fast. Grid operations are cache-friendly and trivially parallelizable.

**Best Sources:** RogueBasin "Cellular Automata Method for Generating Random Cave-Like Levels"; Jim Babcock's article; Johnson L. (2010) "Cellular automata for real-time generation of infinite cave levels".

---

## D.13 — BSP Dungeon Generation

**Category:** Partitioning / Layout
**Core Purpose:** Generate non-overlapping dungeon rooms by recursively subdividing space using a binary tree, then placing rooms in leaf nodes and connecting them via corridors.

**Typical Use Cases:** Roguelike dungeons, structured interior spaces, office/building layouts.

**Inputs:** Map dimensions, minimum/maximum room size, recursion depth, split ratio constraints.
**Outputs:** Set of rooms (rectangles) and corridors connecting them. Binary tree structure that encodes spatial hierarchy.

**Core Algorithm:**
1. Start with entire map as root rectangle
2. Recursively split each rectangle:
   a. Choose split direction (horizontal or vertical, often alternating or random)
   b. Choose split position (random within constraints to ensure minimum room size)
   c. Create two child rectangles
3. Stop splitting when rectangles approach desired room size (depth limit or size threshold)
4. Place a room of random size within each leaf rectangle (smaller than the leaf, to leave wall space)
5. Connect rooms: traverse tree bottom-up, connecting sibling leaf rooms with corridors
   - If rooms share a face, use straight corridor
   - Otherwise, use L-shaped or Z-shaped corridor
6. Optionally add extra connections for loops

**Common Variants:**
- **Classic BSP (RogueBasin):** Described above. Produces well-organized dungeons with guaranteed non-overlap.
- **Modified split ratios:** Constrained to 0.45-0.55 for uniform rooms, or 0.1-0.9 for varied room sizes.
- **Iterative subdivision:** Instead of tree recursion, repeatedly pick random rectangles from a list and subdivide. Produces more varied layouts.
- **Hybrid BSP + CA:** Use BSP for room placement, then apply cellular automata to make rooms and corridors look organic.

**Strengths:** Guaranteed non-overlapping rooms. Tree structure naturally provides connectivity (siblings are always connected). Produces organized, navigable layouts. Easy to control room density and size.

**Weaknesses:** Produces rectangular, grid-aligned rooms — can feel artificial. Corridors follow tree structure, so layout can feel predictable. No natural support for circular rooms, irregular shapes, or open areas.

**Failure Modes:** Too-deep recursion creates tiny rooms. Not enough recursion creates few large rooms. Split positions too close to edges create sliver rooms. Corridor connection logic can produce overlapping corridors if not careful.

**Combinations:** BSP rooms + cellular automata smoothing. BSP layout + graph-based connectivity for additional corridors. BSP structure for progression/theming (subtrees = dungeon sections).

**Difficulty:** Understanding: Low-Medium. Implementation: Medium.

**Best Sources:** RogueBasin "Basic BSP Dungeon generation" (JICE); Herbert Wolverson's Rust roguelike tutorial; Antonios Liapis' PCG dungeon chapter.

---

## D.14 — Random Walk / Drunkard's Walk

**Category:** Dungeon & Interior Generation
**Core Purpose:** Generate organic, meandering cave or dungeon layouts by simulating one or more agents that carve passages as they walk randomly through a grid.

**Typical Use Cases:** Cave generation, organic dungeon passages, natural-looking tunnel networks.

**Inputs:** Grid dimensions, number of steps, number of agents, target floor percentage, room placement probability.
**Outputs:** Binary grid (carved/uncarved), or a set of carved positions.

**Core Algorithm:**
1. Start with grid filled with walls
2. Place walker at center (or random position)
3. For each step:
   a. Choose random direction (up/down/left/right)
   b. Move walker
   c. Carve current cell to floor
   d. Optionally: with some probability, carve a room at current position
4. Continue until target percentage of map is carved, or step limit reached

**Common Variants:**
- **Single walker:** Simple drunkard's walk. Produces winding passages.
- **Multiple walkers:** Start at same or different positions. Creates more distributed layouts faster.
- **Agent with increasing room probability:** The longer the agent walks without placing a room, the higher the probability of placing one. Creates dungeon-like room distributions.
- **Directional bias:** Walker has inertia — higher probability of continuing in current direction. Produces longer corridors.
- **Tunneler agents:** Agents with specific behaviors (dig rooms, dig corridors, change direction with varying probabilities). More controllable than pure random walk.

**Strengths:** Produces very organic layouts. Guarantees connectivity (everything is carved by the same continuous walk). Simple to implement and understand.

**Weaknesses:** Unpredictable layout — hard to control room count, corridor length, or overall shape. Can produce very uneven distributions (clustered in some areas, sparse in others). Slow to fill large maps (random walk revisits cells frequently).

**Failure Modes:** Single walker can produce very concentrated layouts with long tendrils. Target fill percentage might never be reached efficiently. No guarantee of interesting gameplay structure.

**Combinations:** Random walk for organic base shape + room placement for structure + connectivity graph for gameplay analysis.

**Difficulty:** Understanding: Very Low. Implementation: Very Low.

**Best Sources:** RogueBasin drunkard's walk articles; Herbert Wolverson's roguelike tutorial; Antonios Liapis' agent-based dungeon generation.

---

## D.15 — Voronoi Partitioning

**Category:** Partitioning / Layout
**Core Purpose:** Divide space into regions based on proximity to a set of seed points, creating a natural-looking cellular partition useful for biomes, territories, terrain regions, and map structure.

**Typical Use Cases:** Biome regions, political boundaries, terrain zones, map structure (Red Blob Games' polygon map generation), crystal/stone patterns, continent shapes.

**Inputs:** Set of seed points, distance metric (usually Euclidean).
**Outputs:** Set of convex polygonal regions (Voronoi cells), each associated with one seed point. The dual graph (Delaunay triangulation) connects neighboring seeds.

**Core Algorithm Approaches:**
- **Brute force:** For each pixel/cell, find nearest seed point. O(n×m) for n seeds and m pixels. Simple, slow.
- **Fortune's algorithm:** O(n log n) sweep line algorithm. Produces exact Voronoi diagram. Complex to implement.
- **Delaunay triangulation → dual:** Compute Delaunay triangulation (e.g., via Delaunator library), then derive Voronoi as the dual graph. Most practical approach for game use.
- **Jump flooding (GPU):** Parallel algorithm for computing approximate Voronoi on GPU. Fast for real-time applications.
- **Lloyd's relaxation:** Iteratively move each seed to the centroid of its Voronoi cell. Produces more uniform cell sizes. Key technique in Red Blob Games' map generation.

**Key Concept — Delaunay/Voronoi Duality:** The Delaunay triangulation connects points whose Voronoi cells share an edge. This duality means computing one gives you the other, and provides a natural adjacency graph for the regions.

**Strengths:** Natural-looking organic partitions. Adjacency graph comes for free. Lloyd relaxation allows control over cell regularity. Foundational for polygon-based map generation.

**Weaknesses:** Vanilla Voronoi cells can be very irregular in size and shape. Lloyd relaxation needed for visual quality but adds computation. Exact computation (Fortune's) is complex to implement correctly.

**Failure Modes:** Without relaxation, some cells can be extremely elongated or tiny. Edge cases in Delaunay computation (collinear/cocircular points) can cause crashes in naive implementations.

**Combinations:** Voronoi regions + noise-based elevation per region + graph connectivity = Red Blob Games-style map generation. Voronoi cells as biome regions with graph-based adjacency for neighbor blending. Voronoi + Poisson disk sampling for seed points = well-spaced regular regions.

**Difficulty:** Understanding: Medium. Implementation: Low (using Delaunator), High (implementing Fortune's from scratch).

**Best Sources:** Red Blob Games polygon map generation article (foundational); Red Blob Games mapgen4; Delaunator library documentation.

---

## D.16 — Wave Function Collapse (WFC)

**Category:** Rule / Constraint-Based Generation
**Core Purpose:** Generate tile-based content (2D or 3D) that locally resembles a given sample or follows specified adjacency rules, using constraint propagation to ensure consistency.

**Typical Use Cases:** Tile-based level generation, 3D modular building/terrain assembly, pixel art generation, dungeon rooms, city blocks, any modular content with adjacency rules.

**Inputs:**
- **Overlapping model:** A sample image and pattern size N.
- **Simple tiled (adjacent) model:** A tile set with explicit adjacency rules (which tiles can be placed next to which, on which sides). This is the variant most commonly used in games.

**Outputs:** A grid where each cell contains exactly one tile, and all adjacency constraints are satisfied.

**Core Algorithm:**
1. Initialize grid: every cell starts with all tiles as possible (maximum entropy)
2. **Observe:** Select the cell with lowest entropy (fewest remaining possibilities). Collapse it: randomly choose one tile from its possibilities (weighted by frequency/probability).
3. **Propagate:** For each neighbor of the collapsed cell, remove tiles that are incompatible with the newly chosen tile. For each changed neighbor, propagate further to *its* neighbors. Continue until no more changes occur.
4. **Check for contradiction:** If any cell has zero possibilities, the generation has failed. Options: restart, or backtrack.
5. Repeat from step 2 until all cells are collapsed.

**Common Variants:**
- **Overlapping model:** Extracts N×N patterns from a sample image and uses pattern adjacency from the sample. More automatic, less control.
- **Simple tiled model:** User defines tiles and adjacency rules explicitly (often via socket/connector labels on tile edges). More control, more setup work.
- **3D tiled model:** Same algorithm, extended to 6 directions instead of 4. Used by Oskar Stålberg (Bad North) and Boris the Brave (Tessera).
- **With backtracking:** On contradiction, revert to a saved state and try a different choice. Dramatically reduces failure rate but more complex.
- **With constraints:** Additional non-local constraints (path connectivity, border conditions, fixed tiles) added to basic WFC. Boris the Brave's DeBroglie supports these.

**Strengths:** Produces locally coherent output that respects all specified adjacency rules. Very flexible — can generate any type of content expressible as tiles with adjacency. Easy to add variety by expanding the tile set. Designer-friendly: artists define tiles, algorithm does the rest.

**Weaknesses:** No global structure — output looks locally correct but may lack large-scale coherence (no guarantee of a "main room" or "critical path"). Contradiction rate increases with tile set complexity and grid size. Can be slow for large grids or complex tile sets due to exponential worst-case. Designing good tile sets with proper adjacency rules is itself a skill.

**Failure Modes:** Contradictions (cells with zero possibilities). Overfitting to sample in overlapping model. Homogeneous output (all tiles used equally, no biome differentiation). Disconnected regions in the output.

**Combinations:** WFC + designer-authored constraints (fixed tiles, path requirements). WFC for local detail + higher-level graph structure for global layout. WFC + biome pre-filtering (disable tiles not appropriate for current biome).

**2D vs 3D:** Algorithm is identical; only the number of directions changes (4 vs 6). 3D dramatically increases tile set complexity and contradiction rate.

**Difficulty:** Understanding: Medium-High (requires understanding constraint propagation). Implementation: Medium (basic version), High (with backtracking and constraints).

**Performance:** Varies enormously with tile set complexity. Simple tile sets on small grids: milliseconds. Complex 3D tile sets on large grids: seconds to minutes. Contradiction and restart can make time unpredictable.

**Best Sources:** Boris the Brave "Wave Function Collapse Explained" (best first-principles explanation); Boris the Brave "WFC Tips and Tricks" (practical game integration); Maxim Gumin's original GitHub (mxgmn/WaveFunctionCollapse); Brian Bucklew GDC 2019 talk (Caves of Qud).

---

## D.17 — L-Systems

**Category:** Rule / Constraint-Based Generation
**Core Purpose:** Generate branching, self-similar structures by iteratively applying string-rewriting rules, then interpreting the resulting string as drawing commands.

**Typical Use Cases:** Trees and vegetation, road networks, river systems, fractal patterns, coral, lightning, any branching organic structure.

**Inputs:** Axiom (starting string), production rules (rewriting rules), number of iterations, interpretation parameters (step length, angle).
**Outputs:** Sequence of drawing/placement commands; when interpreted with a turtle graphics system, produces a geometric structure.

**Core Algorithm:**
1. Start with axiom string (e.g., "F")
2. For each iteration, apply production rules to every character simultaneously:
   - Rule: F → F[+F]F[-F]F (each F is replaced)
3. After all iterations, interpret final string as turtle graphics:
   - F = move forward and draw
   - + = turn right by angle
   - - = turn left by angle
   - [ = push state (position + direction) onto stack
   - ] = pop state from stack

**Common Variants:**
- **Deterministic L-systems (D0L):** Each symbol has exactly one production rule. Same input → same output. Good for teaching, limited variety.
- **Stochastic L-systems:** Each symbol can have multiple production rules, chosen probabilistically. Produces variety.
- **Parametric L-systems:** Symbols carry parameters (e.g., segment length, thickness). Rules can include mathematical expressions on parameters. Required for realistic tree generation.
- **Context-sensitive L-systems:** Rules can depend on neighboring symbols. More expressive but more complex.
- **Open L-systems:** The system interacts with its environment (e.g., a tree that responds to obstacles or light). Used in research but complex for games.

**Strengths:** Produces beautiful branching structures with minimal rule specification. Self-similarity creates natural-looking organic forms. Very compact representation. Well-studied mathematically.

**Weaknesses:** Difficult to control global shape (rules operate locally). Exponential string growth with iterations. Interpretation step can be complex for 3D. Designing rules to produce specific shapes is non-intuitive.

**Failure Modes:** Too many iterations = exponential blowup. Rules that produce unbalanced brackets = stack errors. Without stochastic variation, output looks artificially regular.

**Combinations:** L-systems for tree skeleton + mesh/texturing pipeline. L-systems for road network + settlement placement at nodes. L-systems for river systems + erosion.

**Difficulty:** Understanding: Medium. Implementation: Low (basic), Medium (parametric/stochastic), High (context-sensitive).

**Best Sources:** Prusinkiewicz & Lindenmayer "The Algorithmic Beauty of Plants" (the classic reference, available free online); various tutorial implementations.

---

## D.18 — Autotiling

**Category:** Rule-Based / Tile-Based Generation
**Core Purpose:** Automatically select the correct tile variant (corner, edge, straight, junction) based on the state of neighboring cells, creating seamless transitions between tile types.

**Typical Use Cases:** 2D tile map rendering, terrain transitions, wall/floor boundaries, water edges, road intersections.

**Inputs:** Tile grid with per-cell state (e.g., grass/water/wall), tile set containing all variants for each transition type.
**Outputs:** Tile grid with specific tile variants assigned for correct visual transitions.

**Core Algorithm (bitmask / bitflag approach):**
1. For each cell, examine the state of neighboring cells
2. Assign a bit to each neighbor direction (N=1, E=2, S=4, W=8 for 4-connected; add diagonals for 8-connected)
3. Compute bitmask from neighbors that share the same type
4. Use bitmask as index into a tile lookup table

**Tile Set Sizes:**
- **4-connected (cardinal only):** 2⁴ = 16 possible configurations → 16 tiles (some can share, practical minimum ~15)
- **8-connected (with diagonals):** 2⁸ = 256 possible configurations → 47 unique tiles after symmetry reduction (Wang tile-like approach)

**Common Variants:**
- **Simple 4-bit:** Only considers cardinal neighbors. 16 tiles. Adequate for simple tilesets.
- **8-bit with diagonal awareness:** Full corner handling. 47 tiles. Needed for polished visual results.
- **Terrain blending:** Multiple terrain types transition smoothly. Requires many more tiles or shader-based blending.
- **Wang tiles approach:** Edge-labeled tiles where matching is based on edge colors/types. More flexible but requires careful tile set design.

**Strengths:** Handles all visual transitions automatically once tile set is created. Deterministic and fast.

**Weaknesses:** Tile set creation is labor-intensive (especially 47-tile sets). Each new terrain type multiplies the required tiles. Doesn't handle more than binary transitions easily.

**Combinations:** Cellular automata or noise → binary grid → autotiling for rendering. WFC naturally includes adjacency management and can replace explicit autotiling in some cases.

**Difficulty:** Understanding: Low. Implementation: Low (4-bit), Medium (8-bit with proper tile set).

**Best Sources:** Red Blob Games articles on tile transitions; Boris the Brave articles; various game development wikis and blog posts on bitmask autotiling.

---

## D.19 — Marching Squares / Marching Cubes

**Category:** Terrain & Landform Generation (Mesh Extraction)
**Core Purpose:** Extract a polygon mesh from a scalar field (2D contour or 3D isosurface) by examining field values at grid vertices and generating geometry based on which vertices are above/below a threshold.

**Typical Use Cases:** Voxel terrain meshing (Minecraft-style smooth terrain), cave mesh generation, metaball rendering, SDF visualization, smooth terrain from heightmap contours.

**Inputs:** Scalar field on a regular grid (2D or 3D), iso-level threshold.
**Outputs:** Polygon mesh (triangles) approximating the iso-surface.

**Core Algorithm (Marching Cubes, 3D):**
1. For each cube in the grid (defined by 8 corner vertices):
   a. Evaluate scalar field at each corner
   b. Determine which corners are above/below the threshold (8 bits → 256 cases)
   c. Look up the triangle configuration from a pre-computed table (reduced to 15 unique cases by symmetry)
   d. Interpolate vertex positions along edges where the field crosses the threshold
   e. Emit triangles

**Common Variants:**
- **Marching Squares (2D):** 4 corners, 16 cases. Used for 2D contour extraction.
- **Classic Marching Cubes (Lorensen & Cline 1987):** Standard algorithm. Has ambiguity in some cases (face ambiguity) that can produce holes.
- **Marching Tetrahedra:** Divides each cube into tetrahedra. No ambiguity issues but more triangles.
- **Dual Contouring:** Uses Hermite data (field values + gradients) to place vertices inside cells rather than on edges. Produces sharper features, supports sharp edges. More complex.
- **Surface Nets:** Simpler dual method. Places vertices at averaged edge-crossing positions. Smoother than marching cubes but can't preserve sharp features.
- **Transvoxel:** Designed for LOD transitions in voxel terrain. Adds special transition cells where chunk LODs differ.

**Strengths:** Produces smooth meshes from volumetric data. Handles overhangs, caves, tunnels — anything expressible as a 3D scalar field. Well-understood with many implementations available.

**Weaknesses:** High polygon count. Mesh quality depends on grid resolution. Classic MC can produce small, degenerate triangles. Doesn't preserve sharp features without dual methods. Performance-intensive for real-time modification.

**Failure Modes:** Ambiguous cases in classic MC can create holes. LOD transitions between chunks produce seams without special handling (Transvoxel). Very thin features near grid resolution produce noisy geometry.

**Combinations:** Noise → 3D scalar field → marching cubes → mesh. SDF editing (add/remove material) → re-mesh. Voxel chunk system + marching cubes + LOD.

**Difficulty:** Understanding: Medium. Implementation: Medium (with lookup tables), High (dual contouring, transvoxel).

**Performance:** Moderate to expensive. Each cell requires table lookup and potentially multiple triangle emissions. GPU implementations exist for real-time use.

**Best Sources:** Sebastian Lague marching cubes videos; Paul Bourke's marching cubes reference (lookup tables and implementation); Transvoxel algorithm documentation by Eric Lengyel.

---

## D.20 — Biome Assignment (Temperature-Moisture Model)

**Category:** Biomes, Climate, and Ecological Distribution
**Core Purpose:** Assign biome types to map regions based on continuous climate parameters, typically temperature and moisture, using a lookup table or diagram inspired by the Whittaker classification.

**Typical Use Cases:** World map biome coloring, terrain texturing, vegetation type selection, climate-driven content variation.

**Inputs:** Temperature field (scalar map), moisture/rainfall field (scalar map), optionally altitude field. These are typically generated from noise or simulation.
**Outputs:** Biome type per cell/region (e.g., desert, forest, tundra, jungle).

**Core Algorithm:**
1. Generate temperature field: typically decreases with latitude and altitude.
   - `temperature = baseTemp - latitudeFactor * |latitude| - altitudeFactor * altitude`
   - Add noise for variation
2. Generate moisture field: can be noise-based, distance-from-water, or wind simulation.
   - Simple: noise layer, possibly modified by distance to coast
   - Advanced: simulate wind carrying moisture from ocean, dropping rain at mountains (rainshadow)
3. For each cell, look up biome from a 2D table indexed by (temperature, moisture):
   - High temp + low moisture → desert
   - High temp + high moisture → tropical forest
   - Low temp + low moisture → tundra
   - Low temp + high moisture → taiga/boreal forest
   - etc.

**Common Variants:**
- **Whittaker diagram lookup:** Discretized version of the ecologist's biome classification. Most common approach.
- **Altitude-based override:** High altitude → mountain/alpine regardless of temp/moisture.
- **Voronoi region-based:** Assign biomes per Voronoi cell rather than per pixel. Gives cleaner region boundaries.
- **Noise-based biome selection:** Skip the temperature/moisture model entirely; use separate noise layers to define biome probability fields. Simpler but less "realistic."
- **Wind/rainshadow simulation:** Red Blob Games' mapgen4 simulates wind carrying moisture, creating realistic rainshadow effects behind mountains.

**Strengths:** Produces plausible-feeling biome distributions. Easy to understand and implement. Whittaker diagram provides a scientifically grounded framework. Highly tunable.

**Weaknesses:** Can feel formulaic — real biome distribution is influenced by many more factors. Sharp biome boundaries need blending/transition logic. The temperature/moisture model is a simplification.

**Failure Modes:** Biome boundaries that exactly follow latitude lines look artificial. Without noise perturbation, the distribution is too regular. Altitude-based temperature without local variation produces ring-shaped biomes around mountains.

**Combinations:** Noise → temperature/moisture fields → Whittaker lookup → biome map → vegetation/texture selection → object placement filtering.

**Difficulty:** Understanding: Low. Implementation: Low.

**Best Sources:** Red Blob Games polygon map generation (biome section); Red Blob Games "Making maps with noise" (biome tables); Minecraft biome generation documentation.

---

# E. Variants and Comparisons

## E.1 — Perlin vs Simplex vs OpenSimplex vs Worley

| Property | Perlin (Improved) | Simplex | OpenSimplex2 | Worley |
|----------|------------------|---------|--------------|--------|
| Grid type | Hypercube | Simplex | Permuted simplex | Hypercube |
| Corners per cell | 2^n (4 in 2D, 8 in 3D) | n+1 (3 in 2D, 4 in 3D) | n+1 | Variable (1+ per cell, check neighbors) |
| Method | Gradient dot product + interpolation | Kernel summation | Kernel summation | Distance to nearest point(s) |
| Visual character | Smooth, slightly axis-aligned | Smooth, more isotropic | Smooth, slightly different texture | Cellular, organic |
| Axis artifacts | Slight, especially at low frequency | Minimal | Minimal | None (distance-based) |
| Cost (2D) | ~4 gradient evals + interpolation | ~3 kernel evals | ~3 kernel evals | ~9 cells × points-per-cell distance calcs |
| Cost (3D) | ~8 gradient evals + trilinear | ~4 kernel evals | ~4 kernel evals | ~27 cells × points distance calcs |
| Patent issues | None | Expired Jan 2022 | Never patented | None |
| Best for | General purpose terrain/texture | General purpose, higher dimensions | General purpose (preferred open alternative) | Cellular patterns, region shapes |

**When to choose which:**
- **Perlin:** Default choice. Well-understood, widely supported, good quality.
- **Simplex:** When performance in 3D+ matters, or when axis-alignment artifacts in Perlin are problematic.
- **OpenSimplex2:** When you want simplex-like quality with a clearly unencumbered implementation.
- **Worley:** When you need cellular, organic, cracked, or region-based patterns.

## E.2 — Heightmap Caves vs Cellular Automata Caves vs Voxel Caves

| Property | Heightmap (floor/ceiling) | Cellular Automata (2D) | Voxel (3D) |
|----------|--------------------------|----------------------|-------------|
| Representation | Two heightmaps (floor height, ceiling height) | 2D binary grid | 3D scalar/binary field |
| Dimensionality | 2.5D (no true overlapping passages) | 2D (top-down) | Full 3D |
| Overhangs | Yes (via ceiling map) | No | Yes |
| Vertical passages | Limited | No (single layer) | Yes |
| Complexity | Low | Very Low | High |
| Visual quality | Smooth, realistic | Organic 2D shapes | Full 3D cave systems |
| Typical use | Simple cave sections in heightmap terrain | Roguelike cave levels | Minecraft/voxel games, 3D cave systems |
| Meshing | Standard heightmap mesh | Tile-based rendering | Marching cubes or greedy meshing |
| Performance | Fast | Very fast | Expensive |

**When to choose:**
- **Heightmap caves:** Quick and adequate for games that already use heightmap terrain. Cannot do true 3D cave networks.
- **Cellular automata:** Best for 2D games or top-down views. Simple, fast, excellent organic shapes. Standard for roguelikes.
- **Voxel caves:** When you need true 3D cave systems with vertical passages, stalactites, multiple levels. Much more complex.

## E.3 — BSP vs Graph-Based Dungeon Generation

| Property | BSP | Graph-Based |
|----------|-----|-------------|
| Room overlap prevention | Guaranteed by spatial partitioning | Must be handled separately (collision checking) |
| Room shapes | Rectangular (fits in BSP cells) | Any shape |
| Layout character | Grid-aligned, organized | More organic, varied |
| Connectivity | Tree-based (siblings connected) | Arbitrary (designer-specified graph topology) |
| Global structure control | Limited (tree structure) | High (graph encodes progression, branching, loops) |
| Progression/gating | Possible via tree hierarchy | Natural fit (graph edges = connections, nodes = rooms) |
| Implementation complexity | Medium | Medium-High |
| Best for | Classic roguelike dungeons, structured layouts | Progression-aware dungeons, metroidvanias, Zelda-style |

**Key insight:** BSP provides spatial guarantees (no overlap) but limited structural control. Graph-based provides structural control (critical path, branches, locks/keys) but requires separate spatial layout (placing graph nodes as rooms in space without overlap).

**Hybrid approach:** Use a graph to define dungeon structure (rooms, connections, key/lock dependencies), then use spatial layout algorithms (force-directed placement, constraint solving) to position rooms in space without overlap, then connect with corridors.

## E.4 — Random Placement vs Poisson Disk Sampling

| Property | Uniform Random | Grid + Jitter | Poisson Disk |
|----------|---------------|---------------|--------------|
| Distribution | Clumpy (clusters and gaps) | Regular with slight variation | Even with natural variation |
| Minimum spacing | None guaranteed | Approximately grid spacing | Guaranteed minimum distance |
| Visual quality | Poor (unnatural clumping) | Decent (visible grid bias) | Excellent (natural, organic) |
| Cost | O(n) | O(n) | O(n) with Bridson's |
| Implementation | Trivial | Trivial | Simple (Bridson's ~50 lines) |
| Density control | Direct (number of points) | Direct (grid resolution) | Indirect (minimum distance) |

**Recommendation:** Always use Poisson disk sampling for object placement in games unless there is a specific reason not to. The visual quality improvement over uniform random is dramatic, and Bridson's algorithm is simple and fast.

## E.5 — Grammar Systems vs Wave Function Collapse

| Property | Grammars (L-Systems, Shape Grammars) | Wave Function Collapse |
|----------|-------------------------------------|----------------------|
| Core approach | Sequential rule application (rewriting) | Constraint propagation + random collapse |
| Input specification | Production rules + axiom | Tile set + adjacency rules (or sample image) |
| Output type | Strings/trees → geometry (branching, hierarchical) | Grids/tilemaps (flat, non-hierarchical) |
| Global structure | Natural (rules encode hierarchy) | Weak (local constraints only) |
| Local coherence | Depends on rule design | Strong (guaranteed by adjacency rules) |
| Best for | Branching structures (trees, roads, buildings) | Tile-based content (levels, terrain, textures) |
| Failure modes | Exponential growth, hard to control global shape | Contradictions, lack of global structure |
| Designer control | Through rule design (non-intuitive) | Through tile set design (visual, intuitive) |

## E.6 — Erosion: Approximation vs Full Simulation

| Property | Simple Approximation | Particle-Based (Lague-style) | Grid-Based Shallow Water |
|----------|---------------------|------------------------------|-------------------------|
| Method | Blur/smooth heightmap with slope weighting | Simulate individual water droplets | Solve fluid equations on grid |
| Realism | Low (softens terrain, no valleys) | Medium (creates valleys, deposits sediment) | High (water accumulation, proper flow) |
| Cost | Very cheap | Moderate (10k-1M droplets) | Expensive (iterative fluid simulation) |
| Implementation | Trivial | Medium | High |
| Visual quality | Minimal improvement | Dramatic improvement | Most realistic |
| Rivers | No | Implicit (drainage paths visible) | Explicit water simulation |

**Recommendation for games:** Particle-based erosion (Lague-style) offers the best tradeoff of quality vs. complexity vs. performance for most game applications.

---

# F. Techniques by Game Problem

## F.1 — Generating Continents and Coastlines

**Most relevant techniques:**
1. **fBm noise + island mask:** Multiply noise by a radial falloff function to create island shapes. Simple, effective.
2. **Domain warping:** Distort the mask boundaries for organic coastlines.
3. **Voronoi regions:** Use Voronoi cells as continent basis (Red Blob Games approach). Assign land/water based on distance from center or noise.
4. **Threshold-based:** Generate fBm, threshold at sea level. Produces fractal coastlines naturally.

**Typical pipeline:** fBm noise → domain warp → threshold at sea level → flood fill to identify continents → smooth/clean small islands → assign regions.

**Key tradeoff:** Noise-thresholding produces fractal coastlines but gives little control over continent count or shape. Voronoi-based approaches give more structural control but require more setup.

## F.2 — Generating Mountains and Terrain Relief

**Most relevant techniques:**
1. **Ridged multifractal noise:** Sharp ridges for mountain ranges.
2. **fBm at varying parameters:** Different lacunarity/persistence for different terrain types.
3. **Hydraulic erosion:** Transforms smooth noise mountains into realistic-looking terrain with valleys.
4. **Thermal erosion:** Removes steep slopes, creating scree and talus.
5. **Domain warping:** Breaks up regular noise patterns for more varied mountain shapes.
6. **Height stamps:** Pre-authored mountain shapes blended into the terrain at random positions.

**Typical pipeline:** Base fBm → ridged multifractal for mountains (blended by altitude mask) → domain warping → hydraulic erosion → thermal erosion smoothing.

## F.3 — Generating Rivers and Drainage

**Most relevant techniques:**
1. **Drainage basin simulation:** Compute water flow direction per cell (towards lowest neighbor), accumulate flow, threshold to define rivers.
2. **Erosion-carved channels:** Run hydraulic erosion and identify the resulting drainage paths.
3. **A* or Dijkstra pathfinding:** Find paths from high to low points, following steepest descent.
4. **Pre-computed drainage (Red Blob Games mapgen4):** Rivers follow the Voronoi dual graph edges, flowing downhill. Generated simultaneously with terrain.

**Key insight:** Rivers are best generated drainage-basin-first rather than carved after terrain. Computing where water *would* flow and then having terrain reflect that produces much better results than trying to cut rivers into existing terrain.

**Typical pipeline:** Heightmap → compute flow direction per cell → accumulate flow → threshold to define river cells → optionally carve river channels → assign water rendering.

## F.4 — Generating Caves

**Most relevant techniques (see comparison in E.2):**
1. **Cellular automata:** Best for 2D cave levels (roguelikes).
2. **3D noise thresholding:** For voxel cave systems. Use 3D noise, threshold to create solid/empty.
3. **Worm/tunnel agents:** Carve tunnel paths through solid rock.
4. **Random walk:** Simple organic tunnels.
5. **BSP + CA hybrid:** BSP for room structure, CA for organic cave feel.

## F.5 — Generating Dungeons

**Most relevant techniques:**
1. **BSP:** Structured, non-overlapping rectangular rooms.
2. **Room-and-corridor (growing):** Place rooms randomly, connect with pathfinding.
3. **Graph-driven layout:** Define dungeon graph (rooms, connections, keys, locks), then spatially realize it.
4. **Cellular automata:** For organic/cave-style dungeons.
5. **Random walk + room placement:** Drunkard's walk for tunnels, rooms placed along the path.
6. **WFC:** For tile-based dungeon detail and room interior layout.
7. **Agent diggers:** Multiple agents with behavioral rules for carving.

**Tradeoff spectrum:** BSP (most structured, least organic) → room-and-corridor → graph-driven → agent-based → random walk → cellular automata (most organic, least structured).

## F.6 — Generating Biomes

See Technique Card D.20 (Temperature-Moisture Model).

**Typical pipeline:** Noise → temperature field + moisture field → Whittaker lookup → biome assignment → per-biome texture/vegetation/content selection.

## F.7 — Generating Villages and Settlements

**Most relevant techniques:**
1. **Voronoi partitioning:** Divide settlement area into plots.
2. **L-systems / shape grammars:** Generate building footprints and road networks.
3. **Grid-based placement:** Simple grid with variation.
4. **WFC:** Assemble buildings from modular pieces.
5. **Road network first:** Generate road network (L-system or graph), place buildings along roads.

## F.8 — Generating Roads and Paths

**Most relevant techniques:**
1. **A* / Dijkstra on cost map:** Find path that minimizes total cost (slope, terrain type, distance).
2. **L-systems:** Generate branching road networks.
3. **Minimum spanning tree:** Connect settlement nodes with minimum total road length.
4. **Spline fitting:** Smooth A*-generated paths with Catmull-Rom or Bezier splines.
5. **Flow field following:** Roads follow terrain gradients (valleys, passes).

## F.9 — Placing Vegetation and Props

**Most relevant techniques:**
1. **Poisson disk sampling:** Even spacing, natural look.
2. **Density field modulation:** Vary Poisson radius based on biome density map.
3. **Slope filtering:** No trees on steep slopes, no grass underwater.
4. **Exclusion masks:** No vegetation on roads, buildings, water.
5. **Hierarchical scattering:** Large trees first (coarse Poisson), then smaller plants (fine Poisson in remaining space).
6. **Noise-based density:** Use noise to create patches/clusters of vegetation.

**Typical pipeline:** Biome map → density field per vegetation type → Poisson disk sampling with variable radius → slope/exclusion filtering → height snapping to terrain → random rotation/scale.

---

# G. Composition Patterns

## G.1 — Noise + Masks + Biome Assignment

**Why it works:** Noise provides the base variation, masks carve meaningful shapes (islands, regions), and biome assignment gives semantic meaning to the terrain.

**Stage order:**
1. Generate multiple noise fields (base elevation, temperature, moisture)
2. Generate masks (island falloff, mountain zone, ocean mask)
3. Multiply/combine noise with masks
4. Feed resulting fields into biome lookup table
5. Output: heightmap + biome map

**Intermediate data:** All stages operate on 2D scalar fields of the same resolution.

**Common mistakes:** Applying masks after biome assignment (should be before). Using correlated noise for temperature and moisture (use different seeds/offsets). Not normalizing fields before biome lookup.

## G.2 — Heightmap + Erosion + Rivers

**Why it works:** Noise creates the macro shape, erosion adds micro-scale geological realism, and river placement follows the drainage patterns that erosion creates.

**Stage order:**
1. Generate heightmap from noise (fBm + ridged multifractal)
2. Apply hydraulic erosion (particle-based)
3. Optionally apply thermal erosion
4. Compute drainage flow from eroded heightmap
5. Threshold flow accumulation to define rivers
6. Optionally carve river channels deeper

**Intermediate data:** Heightmap (2D float array) at all stages. Erosion modifies it in place. Flow accumulation produces a separate scalar field.

**Common mistakes:** Running erosion before applying continent masks (erodes ocean floor pointlessly). Not enough erosion iterations (visible but not impactful). Running river computation on pre-erosion heightmap (rivers don't match eroded valleys).

## G.3 — Voronoi Regions + Graph Connectivity

**Why it works:** Voronoi provides natural-looking spatial regions. The Delaunay dual gives an adjacency graph for free. This graph enables pathfinding, region connectivity analysis, and game logic (borders, trade routes, etc.).

**Stage order:**
1. Generate seed points (random, Poisson disk, or placed)
2. Compute Voronoi diagram (via Delaunay triangulation)
3. Optionally Lloyd-relax for more uniform cells
4. Assign properties to regions (elevation, biome, etc.) using noise or rules
5. Use Delaunay edges as adjacency graph for connectivity, pathfinding, river flow

**Intermediate data:** Point set → Voronoi cells (polygons) + Delaunay graph (adjacency).

**Common mistakes:** Not relaxing (cells too irregular). Forgetting that Voronoi is the dual of Delaunay (computing them separately wastes work).

## G.4 — Cellular Automata + Cleanup Pass

**Why it works:** CA produces organic shapes but with disconnected regions and noise. A cleanup pass ensures the result is playable.

**Stage order:**
1. Initialize random binary grid
2. Run CA for 4-7 iterations
3. Flood fill to find connected components
4. Keep largest component (or connect components with tunnels)
5. Remove isolated single-cell walls/floors
6. Optionally smooth boundaries with one more CA pass

**Intermediate data:** Binary grid throughout. Connected component labels after flood fill.

**Common mistakes:** Skipping the connectivity check (producing unreachable areas). Running too many CA iterations (caves become too smooth and blob-like).

## G.5 — WFC + Designer-Authored Constraints

**Why it works:** WFC handles local coherence automatically, while designer constraints provide global structure (entry/exit points, required features, path connectivity).

**Stage order:**
1. Define tile set with adjacency rules
2. Set boundary constraints (edges of the map)
3. Fix specific cells (entry/exit points, key rooms)
4. Add path constraint (Boris the Brave's technique: ensure a connected path exists between specified points)
5. Run WFC with backtracking
6. Post-process: add gameplay elements, decorations

**Common mistakes:** Over-constraining (too many fixed tiles cause contradictions). Under-constraining (output has no global coherence). Not using backtracking (generator fails frequently on complex tile sets).

## G.6 — World Graph + Local Terrain Realization

**Why it works:** The world graph defines high-level structure (regions, connections, progression), while local generation fills in the detail. This separates concerns and enables streaming.

**Stage order:**
1. Generate abstract world graph (nodes = regions, edges = connections)
2. Assign properties to nodes (biome, difficulty, content type)
3. Layout nodes spatially (force-directed or constraint-based)
4. For each region, generate local terrain/content using region properties as parameters
5. Connect regions at boundaries (shared edges match in terrain, style)

**Intermediate data:** Graph (nodes + edges + properties) → spatial layout (positions + shapes) → local heightmaps/tilemaps per region.

**Common mistakes:** Inconsistent boundaries between regions (seams). Not propagating enough information from graph to local generation (local terrain ignores world context).

---

# H. Inputs and Outputs Library

## Data Structures Used in PCG

| Data Structure | Description | Produced By | Consumed By |
|---------------|-------------|-------------|-------------|
| **Scalar Field** | 2D array of float values (heightmap, temperature map, etc.) | Noise functions, fBm, erosion, simulation | Thresholding, biome lookup, terrain meshing, erosion |
| **Binary Mask** | 2D array of boolean values | Thresholding scalar fields, cellular automata | Masking operations, flood fill, region extraction |
| **Tile Grid** | 2D array of tile indices | WFC, autotiling, manual design | Rendering, collision, gameplay logic |
| **Voxel Grid** | 3D array of values (density, material) | 3D noise, CSG operations, erosion | Marching cubes, greedy meshing, collision |
| **Graph** | Nodes + edges (adjacency list or matrix) | Voronoi dual (Delaunay), BSP tree, room connectivity, world structure | Pathfinding, connectivity analysis, progression gating |
| **Point Set** | List of 2D/3D positions | Poisson disk sampling, random placement, Voronoi seeds | Object instantiation, Voronoi computation, spatial queries |
| **Spline** | Control points defining a smooth curve | Road/river pathfinding, L-system output | Mesh generation, object placement along path |
| **Region Map** | 2D array mapping each cell to a region ID | Voronoi assignment, flood fill, biome assignment | Per-region processing, boundary detection, blending |
| **Adjacency Map** | Map of region_id → set of neighboring region_ids | Voronoi dual, tile adjacency analysis | Connectivity checking, border effects, graph operations |
| **Biome Map** | 2D array of biome type per cell | Temperature-moisture lookup, region assignment | Texture selection, vegetation rules, content filtering |
| **SDF (Signed Distance Field)** | Scalar field where value = distance to nearest surface (negative inside) | Distance computation from shapes, marching methods | Smooth blending, collision, CSG operations |

## Common Conversions

- **Scalar field → binary mask:** Threshold at a value.
- **Binary mask → region map:** Flood fill to label connected components.
- **Point set → Voronoi regions → region map:** Compute Voronoi, rasterize to grid.
- **Graph → spatial layout:** Force-directed placement or constraint solving.
- **Scalar field → mesh:** Marching squares/cubes.
- **Tile grid → mesh/rendering:** Standard tile rendering.
- **Heightmap → normal map:** Compute gradients (Sobel filter or finite differences).
- **Heightmap → slope map:** Magnitude of gradient.

---

# I. Learning Roadmap

## Recommended Learning Sequence

### Phase 1: Foundations (1-2 weeks)
**Learn:** Seeded PRNGs, coordinate-based hashing, basic random sampling.
**Why first:** Everything else depends on controlled randomness. Understanding determinism prevents endless debugging later.
**Key source:** Squirrel Eiserloh's GDC talk on noise-based RNG.

### Phase 2: Noise and Scalar Fields (2-3 weeks)
**Learn:** Value noise → Perlin noise → fBm → ridged multifractal → domain warping → Worley noise.
**Why next:** Noise is the most broadly useful PCG primitive. Almost every system described later uses noise as input.
**Key sources:** Catlike Coding noise tutorials (most rigorous); Red Blob Games "Making maps with noise" (most accessible); Inigo Quilez articles (mathematical depth).

### Phase 3: Terrain and Biomes (2-3 weeks)
**Learn:** Heightmap generation, island masks, terrain layering, basic biome assignment (temperature-moisture model), basic erosion.
**Why next:** Terrain generation is the most common first application of noise. It builds directly on Phase 2 and produces visible, satisfying results.
**Key sources:** Sebastian Lague's procedural landmass series; Red Blob Games polygon map generation.

### Phase 4: Spatial Partitioning and Layout (1-2 weeks)
**Learn:** Voronoi/Delaunay, BSP trees, region graphs.
**Why next:** These structural techniques are needed for dungeons, settlement layout, and biome regions. They complement the field-based techniques from Phase 2-3.
**Key sources:** Red Blob Games Voronoi articles; RogueBasin BSP dungeon tutorial.

### Phase 5: Dungeons and Caves (2-3 weeks)
**Learn:** BSP dungeons, cellular automata caves, room-and-corridor methods, drunkard's walk, graph-based dungeon structure.
**Why next:** Dungeons are the second most common PCG application after terrain. They use the partitioning techniques from Phase 4.
**Key sources:** RogueBasin articles; Herbert Wolverson's roguelike tutorial; Antonios Liapis' dungeon chapter.

### Phase 6: Rule-Based and Constraint-Based (2-3 weeks)
**Learn:** Cellular automata (deeper), autotiling, Wang tiles, WFC, L-systems basics.
**Why next:** These are more specialized but extremely powerful for structured content. WFC in particular is increasingly important. Requires understanding of constraint propagation, which is a more advanced concept.
**Key sources:** Boris the Brave's WFC series (essential); Maxim Gumin's WFC repo.

### Phase 7: Placement and Scattering (1 week)
**Learn:** Poisson disk sampling, density fields, slope/exclusion filtering, hierarchical scattering.
**Why next:** Object placement is the finishing layer of most PCG pipelines. Uses all previous knowledge.
**Key source:** Bridson's paper (one page); any Poisson disk tutorial.

### Phase 8: Multi-Scale and Hierarchical (2-3 weeks)
**Learn:** Chunk-based generation, LOD, coordinate-based regeneration, world-map-to-local-map pipelines.
**Why next:** These are architectural patterns that span entire generation systems. Require understanding of the individual techniques they orchestrate.
**Key sources:** Sebastian Lague's LOD/chunking work; game-specific case studies.

### Phase 9: Advanced Simulation (ongoing)
**Learn:** Hydraulic erosion (detailed), thermal erosion, river simulation, tectonic simulation, more sophisticated biome simulation.
**Why last:** These are refinement techniques that improve quality but add significant complexity. They build on terrain knowledge from Phase 3.
**Key sources:** Hans Theobald Beyer's paper; Sebastian Lague's erosion video; Red Blob Games mapgen4 (wind/rainfall simulation).

---

# J. Open Questions and Gaps

## Commonly Oversimplified Techniques

1. **Perlin noise.** Most "Perlin noise" tutorials actually implement value noise or use the pre-2002 version. The improved Perlin noise (2002) with quintic interpolation and better gradient selection is what should be taught, but the classic version persists.

2. **Hydraulic erosion.** Tutorials (including Lague's, which is excellent for what it covers) implement a simplified droplet model. Real erosion involves water accumulation, stream power equations, multiple sediment types, bank erosion, and timescales that span orders of magnitude. The droplet model is a useful approximation, not a simulation.

3. **fBm.** Often presented as the *only* noise composition technique. Ridged multifractal, turbulence, and domain warping are rarely covered in the same tutorials, giving the impression that fBm is sufficient for all terrain.

4. **Biome generation.** Most tutorials present a simple noise → threshold → biome approach. The Whittaker temperature-moisture model is better but still simplified. Real-world biome distribution involves soil types, historical climate, species competition, and other factors that games almost never model.

## Commonly Misunderstood Techniques

1. **WFC is not magic.** Many developers expect WFC to produce globally coherent levels from local rules. It produces locally coherent output but has no concept of rooms, critical paths, or progression. Global structure must be added via additional constraints or higher-level orchestration.

2. **Noise is not terrain.** Raw noise (even fBm) produces cloud-like shapes, not terrain. Terrain requires additional processing: erosion, masking, feature stamping, altitude-aware layering. Many first attempts at terrain generation fail because the developer expects noise alone to look like mountains.

3. **BSP dungeons are not the only option.** BSP is overrepresented in tutorials relative to its usefulness. For many games, graph-based or agent-based approaches produce more interesting results.

## Poorly Documented at Implementation Level

1. **River generation.** Most articles describe the *concept* of drainage basins and river networks but few provide step-by-step implementation details with actual code.

2. **Biome transition blending.** How to smoothly blend between biome types at boundaries (texture blending, vegetation gradients) is rarely covered in detail.

3. **Settlement/village generation.** L-system and grammar-based approaches are described in academic papers but practical game implementations with code are scarce.

4. **Multi-scale/hierarchical generation.** How to transition between world-map-level and local-map-level generation (what data flows between scales, how boundaries match) is poorly documented despite being essential for large worlds.

5. **Lock-and-key / progression-aware dungeon generation.** The theory is well described (Joris Dormans' work) but practical implementations with code are rare.

---

# K. Final Synthesis

## 1. Most Important PCG Techniques to Understand First

1. **Coherent noise (Perlin/Simplex) + fBm** — Foundation of terrain, biomes, and environmental variation.
2. **Cellular automata** — Simplest path to organic cave/dungeon generation.
3. **Voronoi partitioning** — Foundation for region-based world structure.
4. **BSP** — Most straightforward structured dungeon generation.
5. **Poisson disk sampling** — Essential for natural-looking object placement.
6. **Hydraulic erosion** — The single biggest quality improvement for terrain.
7. **Wave Function Collapse** — The most powerful constraint-based generation technique for modular content.
8. **Biome assignment via temperature/moisture** — Standard approach for world-scale ecological variation.

## 2. Most Important Comparisons to Understand

1. **Perlin vs Simplex vs Worley** — Understand what each noise type looks like and when to use it.
2. **fBm vs ridged multifractal vs domain warping** — Understand noise composition options beyond just layering.
3. **BSP vs graph-based dungeon generation** — Understand the tradeoff between spatial guarantees and structural control.
4. **Random placement vs Poisson disk** — Understand why even spacing matters visually.
5. **Noise-only terrain vs noise + erosion** — Understand the dramatic quality difference erosion makes.

## 3. Most Useful Sources to Study First

1. **Red Blob Games** — Start here. Interactive explanations of noise, Voronoi, pathfinding, polygon map generation. Unmatched pedagogical quality.
2. **Catlike Coding** — Go here for rigorous noise implementation. Full implementation details for value noise, Perlin, simplex, Voronoi noise.
3. **Boris the Brave** — Essential for WFC. The "WFC Explained" article is the definitive first-principles explanation.
4. **Sebastian Lague** — Best video content. Terrain generation series and hydraulic erosion are must-watch.
5. **RogueBasin** — Community reference for dungeon generation algorithms. BSP and cellular automata articles are standard.
6. **Inigo Quilez** — Mathematical depth for noise, SDFs, and domain warping. Terse but precise.

## 4. Concise "Best Current Knowledge Library"

| Problem | Best Default Approach | Key Technique(s) |
|---------|----------------------|-------------------|
| Terrain heightmap | fBm + ridged multifractal + erosion | Noise composition, particle-based erosion |
| Coastlines/islands | Noise threshold + falloff mask + domain warping | fBm, radial mask, domain warping |
| Mountains | Ridged multifractal + hydraulic erosion | Ridged noise, erosion simulation |
| Rivers | Drainage basin computation on eroded heightmap | Flow direction, flow accumulation |
| Caves (2D) | Cellular automata + flood fill cleanup | CA rules, connected components |
| Caves (3D) | 3D noise threshold + marching cubes | 3D Perlin/Simplex, marching cubes |
| Dungeons (structured) | BSP or graph-based room placement | BSP trees, region graphs |
| Dungeons (organic) | Drunkard's walk or cellular automata | Random walk, CA |
| Biomes | Temperature-moisture noise fields + Whittaker lookup | Layered noise, lookup table |
| Vegetation/props | Poisson disk sampling + biome/slope filtering | Bridson's algorithm, density fields |
| Tile-based content | WFC with adjacency rules | Constraint propagation |
| Trees/plants | L-systems (stochastic, parametric) | String rewriting, turtle graphics |
| Roads/paths | A* on cost map + spline smoothing | Pathfinding, Catmull-Rom splines |
| World structure | Voronoi regions + Delaunay graph | Voronoi, graph algorithms |
| Progression gating | Lock-and-key graph structure | Graph topology, constraint solving |
| Object placement | Poisson disk + density field modulation | Bridson's algorithm |
| Chunk streaming | Coordinate-based hashing + lazy generation | Hash-based PRNG, chunked data |

---

*This report synthesizes information from the sources cited throughout. Where specific algorithmic details are described, they reflect confirmed and widely-documented implementations. Where tradeoffs or recommendations are given, they reflect the consensus of the game development and PCG communities as expressed in the sources identified. Speculative claims are avoided; where uncertainty exists, it is noted.*
