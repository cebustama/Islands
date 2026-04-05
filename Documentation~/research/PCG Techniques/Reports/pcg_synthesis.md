# PCG Research Synthesis — Rehydration Context for Pass 6b

This document condenses the key findings from passes 1a through 6a into the specific information pass 6b requires: technique names, their inputs/outputs, data structures produced and consumed, pipeline orderings, failure modes at technique boundaries, and documentation quality assessments.

---

## PASS 1a — Randomness Primitives and Noise

### Techniques Covered
1. **Coordinate hashing (SquirrelNoise5)** — Stateless uint32 from (position, seed). Replaces sequential PRNGs. Multi-dim via prime folding. Output: uint32 or float [0,1]/[-1,1].
2. **Weighted random selection (Walker's alias method)** — O(1) sampling from discrete distributions after O(n) preprocessing. Output: index into outcome table.
3. **Shuffle bags** — Fisher-Yates shuffle for period-controlled distributions (Tetris Guideline). Output: ordered sequence.
4. **Poisson disk sampling (Bridson's algorithm)** — O(N) blue-noise point distribution with minimum distance r. Uses background grid of cell size r/√2, k=30 candidates. Output: array of 2D/3D positions. Key artifact: Nebraska Problem (quasi-regular row alignment in 3D views).
5. **Stratified sampling / blue noise textures** — Jittered grid (contradictory jitter tension at 0.6 vs 0.9). Void-and-cluster pre-computed tileable textures (CC0 from Christoph Peters). Output: point set or 2D scalar array.
6. **Value noise** — Hash lattice points → interpolate scalars. Worst grid artifacts of any coherent noise. Output: scalar float.
7. **Perlin noise (improved)** — Gradient vectors at lattice points, dot with displacement, trilinear interpolate via quintic fade (6t⁵−15t⁴+10t³ for C2). 2^N gradient evaluations. Output: scalar ~[-1,1].
8. **Simplex / OpenSimplex2** — N+1 vertices per simplex, radial kernel summation (not interpolation). O(N²) scaling. OpenSimplex2F ≈ simplex speed; OpenSimplex2S smoother for ridged. Patent expired Jan 2022. Output: scalar ~[-1,1].
9. **Worley noise (cellular/Voronoi)** — Distance to nth-closest feature point. F1 = radial cells, F2−F1 = edge ridges. 3×3 neighbor search. Outputs: float distances F1..Fn + integer cell ID.

### Key Data Structures Produced
- **Scalar value** (float per query point) — from all noise types
- **Point set** (array of positions) — from Poisson disk, stratified sampling
- **Cell ID** (integer) — from Worley noise, for per-cell variation

### Comparisons Settled
- OpenSimplex2 is the default for smooth fields (no patent, low bias, competitive speed)
- Worley for cellular/boundary patterns (fundamentally different from gradient noise)
- Poisson disk strictly better than uniform random or jittered grid for object placement

---

## PASS 1b — Noise Composition and Field Operations

### Techniques Covered
1. **Fractal Brownian motion (fBm)** — Octave summation: `Σ noise(λⁱ·p) · Gⁱ`. H=1/G=0.5 standard for terrain (-9 dB/octave). Variants: standard, heterogeneous terrain (altitude-weighted), derivative-damped (slope attenuation via `1/(1+dot(d,d))`). Failure: lattice alignment at λ=2.0 (fix: detune to 2.01 or rotate ~37° between octaves).
2. **Ridged multifractal** — `offset − |noise|` squared, with inter-octave feedback (weight = clamp(signal·gain, 0,1)). Produces heterogeneous jagged ridges. Musgrave params: H=1.0, offset=1.0, gain=2.0. Output: scalar field (heightmap).
3. **Turbulence** — fBm with `|noise|` per octave. Always non-negative. C0-only (cusp discontinuities). Best for domain warping periodic functions (marble). Mean ≈ 0.2 (not zero).
4. **Domain warping** — `f(p + h(p))` where h is vector displacement from independent fBm calls. Single warp: subtle organic. Double warp: dramatically swirling. Canonical: `fbm(p + 4.0·q)` with offset vectors (5.2, 1.3). Cost: ~5 fBm evals for double warp.
5. **Scalar field composition** — Operators: additive, multiplicative masking, lerp, power redistribution (`pow(e,exp)`), smoothstep, min/max, spline remap. Biome pattern: 2+ independent noise axes → Whittaker lookup. Minecraft uses 5D biome selection. Failure: seam artifacts at biome borders, dynamic range collapse from cascaded multiplication, self-similarity fatigue from correlated seeds.
6. **Signed distance fields (SDFs)** — Exact geometric distance per point. Primitives: circle, box, segment. Boolean ops: union=min, subtraction=max(−a,b), intersection=max. Smooth ops: polynomial smin with blend radius k. Domain ops: rounding, onion/shell, repetition, displacement (`sdf + fbm`). Interior distances corrupted after Boolean ops.

### Key Data Structures Produced
- **Scalar field** — float[] array at chunk resolution (64×64 to 256×256), or evaluated on-demand
- **Gradient field** — scalar + 2D/3D derivative vector per sample (from derivative-returning noise)
- **Distance field** — signed float per point (zero = surface, negative = inside)
- **Weighted distribution table** — (category, weight) pairs per sample for biome blending

---

## PASS 2a — Heightmap Construction, Shaping, and Erosion

### Techniques Covered
1. **Heightmap from noise** — Layered fBm (4-6 octaves, persistence 0.5, lacunarity 2.0). Composition: RidgedMulti for mountains + Billow for lowlands + control noise selector (libnoise pattern). Global normalization: divide by `(1−G^N)/(1−G)` with fudge factor 0.9. Power redistribution: `pow(e·1.2, exponent)` with exp 2.5-3.0.
2. **Island/continent masks** — Distance functions (square bump, Euclidean, Chebyshev+sigmoid). Combination: subtraction, multiplication, lerp, additive. Noise perturbation for organic coastlines (amplitude 0.1-0.3, frequency 3-8).
3. **Mountain shaping** — Ridged multifractal with octave-weighted feedback. Regional mask (cubed noise at freq 0.002). Voronoi ridge blending (F2−F1). Failure: pillow mountains (fix: pow or ridge noise), uniform mountains (fix: cubed-noise mask).
4. **Cliffs and terraces** — Height quantization: `round(h·n)/n`. Smooth terracing with smoothstep on fractional part. Inverse thermal erosion for cliff-plateau terrain (erodes when d ≤ T instead of d > T). Houdini HeightField Terrace as reference parameterization.
5. **Hydraulic erosion (particle)** — Droplets with position, direction, speed, water, sediment. Gradient via bilinear interpolation. Capacity: `max(−Δh·speed·water·capacityFactor, minCapacity)`. Erosion within radius via weighted brush. Deposition via bilinear only. 70K-200K droplets on 512². Lague defaults: inertia 0.05, capacity 4, erodeSpeed 0.3, radius 3.
6. **Thermal erosion** — Material transfer when slope > talus angle T. T = 4/N. Erosion coeff c = 0.5. 50-200 iterations. Complements hydraulic: produces V-shaped valleys. Failure: uniform slopes, oscillation when c > 0.5.

### Pipeline Order
1. Base noise heightmap → 2. Normalize + redistribute → 3. Island mask → 4. Mountain shaping → 5. Terracing (optional) → 6. Hydraulic erosion → 7. Thermal erosion → 8. Post-erosion detail recovery

### Key Data Structure
- **Heightmap** — single-channel float array, optionally with auxiliary maps (erosion amount, sediment depth, slope, moisture)

---

## PASS 2b — Rivers, Caves, Voxels, and Mesh Extraction

### Techniques Covered
1. **River/drainage systems** — Priority-Flood depression filling (Barnes 2014, +ε for gradient). D8 flow directions (steepest downslope, diagonal/√2). Flow accumulation highest-to-lowest. Threshold for river segments. Mapgen4 alternative: upstream growth from coast on Delaunay mesh with Strahler ordering + wind-based rainfall. Andrino's rivers-first paradigm.
2. **Cave generation** — Three methods:
   - *Cellular automata*: 40-45% fill, 4-5 rule (B5678/S45678), 15 iterations. No connectivity guarantee. 2D primarily.
   - *Noise thresholding*: Two intersected 3D noise fields for tunnels (`abs(noise1)<w AND abs(noise2)<w`). Chunk-independent. Depth attenuation essential.
   - *Agent tunnelling / Perlin worms*: Noise-guided movement, variable radius sphere carving. Sequential state prevents chunk independence.
3. **Voxel terrain** — 3D scalar field, density = surfaceHeight(x,z) − y + noise3D. Chunked flat array (16³ or 32³). Cave carving: `final = terrain · cave_mask`. LOD via nested grids/clipmaps. 1-voxel overlap at chunk boundaries.
4. **Mesh extraction** — Three algorithms:
   - *Marching cubes*: Vertices on edges, 256-case lookup (15 unique), up to 5 tri/cell. Bevels sharp features. Transvoxel for LOD stitching (512 configs, 73 classes).
   - *Dual contouring*: One vertex inside cell via QEF (Hermite data needed). Preserves sharp features. Requires gradient. Adaptive octree natural.
   - *Surface nets*: Dual topology, centroid of crossings (no gradient needed). ~Half face count of MC. ~20M tri/sec. Cannot preserve sharp features.

### End-to-End Terrain Pipeline (12 stages)
Seed → base heightmap → island mask → mountain shaping → erosion → depression fill → flow direction → flow accumulation → river carving + moisture → biome classification → voxel density (if 3D) → mesh extraction → texture/material

### Key Data Structures
- **Flow-direction grid** — per-cell direction enum (D8)
- **Flow-accumulation grid** — per-cell upstream count
- **Voxel grid** — chunked 3D float array (density or SDF)
- **Triangle mesh** — vertex positions, normals, indices

### Comparisons Settled
- Heightmap for outdoor terrain without overhangs; voxels when caves/destruction are core
- CA caves for 2D roguelike organic shapes; noise thresholding for 3D seamless streaming; agent tunnelling for designer-controlled corridors
- MC for ecosystem compatibility; DC for sharp features; surface nets for best simplicity/quality balance on organic terrain

---

## PASS 3a — Space-First Dungeon Generation

### Techniques Covered
1. **BSP dungeon** — Recursive rectangular subdivision. Three phases: build tree → place rooms in leaves → connect siblings bottom-up. Split range 0.45-0.55 (homogeneous) vs 0.1-0.9 (heterogeneous). Aspect-ratio rejection for slivers. Guaranteed connectivity via tree traversal.
2. **Room-and-corridor** — Two families: Anderson's incremental growth (grow from existing dungeon, ~40/300 attempts succeed) and Nystrom's rooms-then-maze-then-merge (region IDs, spanning-tree connector merging, dead-end removal, extra-connector chance for loops).
3. **Random walk / drunkard's walk** — Agent walks randomly on solid grid, carves floor. Presets: open_area (center spawn, 400 lifetime, 50%), winding_passages (random spawn, 100 lifetime, 40%). Single-origin = guaranteed connected.
4. **Agent-based diggers** — Blind stochastic (escalating turn/room probabilities) and look-ahead (collision checking). Cogmind: self-mutating parameters (width, turn prob, room prob). Most designer control of organic methods.
5. **Cellular automata caves** — 40-45% fill, 4-5 rule, 15 iterations. Enhanced two-phase: R1≥5 OR R2≤2 for 4 iterations, then R1≥5 for 3. No connectivity guarantee — flood fill + cull/connect required.
6. **Room templates / prefabs** — Spelunky: 4×4 grid, solution-path random walk, type-based template selection, wildcard resolution, monster placement. Connection-point alignment for non-grid systems (Edgar).
7. **Connectivity verification** — Flood fill (BFS/DFS from spawn), Union-Find for incremental tracking. Delete unreachable or drill corridors.
8. **MST + Delaunay for room connectivity** — Delaunay triangulation → MST extraction → add back 8-15% non-MST edges for loops. Corridors via L-shaped or A* routing.

### Pipeline Pattern
Room generation → connectivity planning (MST/Delaunay) → corridor carving → organic modification (CA erosion) → template injection → connectivity verification

### Key Data Structures
- **Tile grid** — 2D int array (wall=1, floor=0, door=2, etc.)
- **Room list** — array of (position, dimensions, type)
- **Connectivity graph** — adjacency list of room IDs with edge types

---

## PASS 3b — Structure-First Dungeon Generation

### Techniques Covered
1. **Graph-driven generation** — Abstract topology graph (rooms=nodes, connections=edges) → spatial realization. Edgar: configuration-space SA with chain decomposition, backtracking. Unexplored: 5×5 grid construction. Force-directed layout (Fruchterman-Reingold). Input graph must be planar for Edgar.
2. **Lock-and-key structures** — Directed graph with lock conditions on edges. Key invariant: key reachable before lock. Metazelda: key-levels (rooms reachable with n keys), spanning tree growth, graphify phase for cycles. Types: unique keys (simplest), persistent (reusable), fungible consumable (UNSOLVED generation problem), stateful (no guidance exists).
3. **Mission/grammar-based generation** — Dormans' graph grammars: context-sensitive subgraph rewriting. Seed: linear chain Start→T₁→...→Goal. Six lock-key rewrite rules. Recipe-controlled rule application. Unexplored: ~5,000 rules, 24 major cycle types, 3 resolution stages (5×5→10×10→50×50). PhantomGrammar: Normal/LSystem/Cellular execution modes.

### Mission/Space Duality (Dormans)
- **Mission** = logical task ordering (what the player must do)
- **Space** = geometric room layout (where the player goes)
- Same mission → many spaces; same space → many missions
- Grammar produces mission → space-first techniques realize geometry

### Documentation Assessment
- BSP, room-corridor, CA, random walk: **excellently documented**
- Mission graphs, grammar-based, lock-and-key: **poorly documented at implementation level**
- Critical gaps: spatial fitness function undefined, force-directed parameters missing, fungible key generation unsolved, grammar authoring methodology absent, no pseudocode in Dormans' papers

---

## PASS 4a — Local Rule-Based Tile and Module Placement

### Techniques Covered
1. **Autotiling** — Bitmask from neighbor states → lookup table. 4-bit (16 tiles) or 8-bit blob (47 tiles after corner elimination). Quarter-tile variant: 20 sub-tiles. Dual-grid: only 5 tiles. Fully deterministic, no failure possible with complete tilesets.
2. **Wang tiles** — Edge-colored squares, no rotation. Scanline placement with stochastic selection. 2 colors → 16 tiles complete set. O(1) per cell, zero failure probability. Edge-colored vs corner-colored (fixes corner problem).
3. **Adjacency-rule systems** — Which tiles can neighbor which. Three specification methods: sample-based extraction, label/socket matching, content-based inference. Greedy placement possible but dead-ends likely without propagation.
4. **Socket-based modular placement** — Typed connectors per face. Symmetric vs asymmetric sockets. Sub-face painting (Tessera: 3×3 per face). Content-based inference (Townscaper, Bad North). 400 modules: 2,400 socket annotations vs 960,000 explicit rules.

### Spectrum
Autotiling (deterministic) → Wang tiles (stochastic, no-fail) → Adjacency rules (greedy, can fail) → Sockets (rich typing, fragile without propagation) → WFC (propagation + backtracking)

---

## PASS 4b — Propagation and Grammar Techniques

### Techniques Covered
1. **Constraint propagation (AC-3/AC-4)** — Domain reduction via arc consistency. AC-4: support count array, O(e·k²). Incomplete alone — needs search (guessing + backtracking). Contradiction = empty domain.
2. **Wave Function Collapse** — CSP solver: observe (min-entropy cell → weighted random collapse) + propagate (AC-4 support count cascade). Two models:
   - *Simple tiled*: authored tiles + explicit adjacency rules. Designer control.
   - *Overlapping*: NxN patterns extracted from example image. Captures N-range correlations.
   - Contradiction handling: full restart (Gumin), backtracking with changelog (DeBroglie), modifying in blocks (Merrell).
   - Non-local constraints: fixed tiles, boundary conditions, path connectivity (DeBroglie: Connected, Loop, Acyclic), count constraints.
   - Production uses: Caves of Qud (overlapping, multi-pass), Bad North (simple tiled 3D, 300+ tiles, no backtrack), Townscaper (irregular grid + marching cubes + priority WFC, fails silently).
3. **L-systems** — Parallel rewriting from axiom. DOL (deterministic context-free), stochastic, parametric, context-sensitive. Turtle graphics interpretation. Exponential string growth O(k^n). Best for vegetation, branching structures. Barrett critique: L-system formalism often reducible to simpler procedural code.
4. **Shape grammars / CGA Shape** — Recursive geometric rule application. CGA: symbolic matching + split operation for building generation. Müller et al. SIGGRAPH 2006. Dormans: graph grammars for missions + shape grammars for spaces.

### Key Insight
WFC = local consistency everywhere simultaneously. Grammars = hierarchical intentional structure. Best approach: grammars for macro structure, WFC for local detail fill (Caves of Qud pattern).

---

## PASS 5a — Region Partitioning and Biome Systems

### Techniques Covered (Partitioning)
1. **Voronoi / Delaunay** — Poisson-disc seeds → Delaunator → dual mesh. Dual-graph architecture: polygon graph (regions) + corner graph (geometry). Circumcenter vs centroid (centroid avoids outside-triangle issues). Boundary: clip or ghost points.
2. **Lloyd's relaxation** — Seeds → cell centroids → re-Voronoi. 2 iterations sufficient. Modern: skip by using Poisson disc seeds directly.
3. **BSP for regions** — Recursive rectangular subdivision. Axis alternation to avoid slivers. Hierarchical nesting for theming.
4. **Quadtrees / octrees** — Adaptive spatial resolution. Sparse voxel octrees for memory. T-junction cracks at LOD boundaries.
5. **Region adjacency graphs** — Voronoi-Delaunay dual gives adjacency free. Patel's Center/Corner/Edge triple structure. Consumed by biome assignment, river generation, moisture simulation.
6. **Connected component analysis** — Flood fill or two-pass union-find. Ocean vs lakes via border flood fill. Filter tiny components.

### Techniques Covered (Biomes)
7. **Temperature-moisture (Whittaker)** — Two scalar axes → biome lookup table. Patel: 4 elevation × 6 moisture → 13 biomes. Gallant: 6×6 matrix. Azgaar: 100×10 matrix.
8. **Altitude-based** — Single-axis elevation → biome bands via lapse rate (~6.5°C/km). Simple but lacks moisture diversity.
9. **Rainshadow approximation** — Ray from wind direction, accumulate altitude → moisture reduction on leeward side.
10. **Wind-based moisture (mapgen4)** — Topological sort by wind direction → single-pass: transport → evaporation → orographic cap → precipitation. Rainshadow emergent. Four params: wind_angle, raininess, evaporation, rain_shadow.
11. **Biome masks / noise selection** — Layered noise thresholds. Minecraft 1.18+: 6D multi-noise (temperature, humidity, continentalness, erosion, weirdness, depth). Biome noise frequency should be 4-8× slower than terrain noise.
12. **Biome transition blending** — Grid-based (Minecraft, visible grid artifacts), Gaussian blur, scattered blend (normalized sparse convolution — best quality, from KdotJPG).
13. **Local vs global assignment** — Global: coordinate-only, chunk-streamable (Minecraft). Local: neighbor-aware, requires pre-computation (Dwarf Fortress hybrid).

### Biome Pipeline
Partition terrain → assign elevation → classify land/water → compute temperature → compute moisture → assign biomes (Whittaker lookup) → blend transitions

### Key Data Structures
- **Region map** — per-cell region ID or Voronoi polygon index
- **Adjacency map** — graph of region IDs with shared-border metadata
- **Biome map** — per-cell biome enum + per-cell blend weights at transitions

---

## PASS 5b — Object Placement, Scattering, and Road Generation

### Techniques Covered (Placement)
1. **Density fields** — `final = biome_density × noise × slope_mask × exclusion_mask`. Two consumers: radius modulation (Poisson disk min_dist varies with density) or rejection probability (fixed-radius + random reject).
2. **Slope/altitude filtering** — Central finite differences for normals (factor 2s for world units). Smoothstep ramps instead of hard cutoffs to avoid visible contour lines.
3. **Exclusion masks** — Water, roads, buildings, cliffs, placed objects. Composed by multiplication (AND). Buffer padding via morphological dilation. Edge feathering via smoothstep.
4. **Hierarchical scattering** — Multi-pass large→small. Each pass's placed objects generate exclusion zones for next. Spatial hash grid or mask rasterization for footprint tracking.
5. **Clustering** — Deformation kernels (Lane & Prusinkiewicz): inhibitory/promotional/mixed kernels, controllable Hopkins index 0.4-2.4+. Alternative: noise-threshold clustering (UE5, Horizon: Zero Dawn).
6. **Settlement placement** — Multi-criteria terrain scoring: flatness, water access, separation, accessibility. Cepero pipeline: flat-area sampling → separation → anisotropic expansion → political aggregation → roads. Patel mapgen2-towns: Dijkstra-based resource evaluation.

### Techniques Covered (Roads)
7. **A*/Dijkstra on cost maps** — Slope (exponential), biome (forest 2-3×, marsh 5-10×), water (10-50× or ∞), existing roads (0.1-0.5× discount). Galin: connectivity mask k=5 (80 candidates, 11.3° angular resolution).
8. **MST for road network** — Delaunay → MST → add back 10-15% edges for loops.
9. **Spline fitting** — Catmull-Rom (centripetal parameterization to prevent self-intersection). Ramer-Douglas-Peucker simplification first. Terrain modification: excavation/embankment in road corridor.
10. **Road network strategies** — Spanning-tree (simplest, well-documented), hierarchical (Galin 2011), L-system (Parish & Müller 2001), tensor-field (Chen 2008, Devans implementation).

### Placement Pipeline
Density field → slope/altitude masks → exclusion masks → hierarchical Poisson disk → clustering → surface alignment → placed objects

### Documentation Gaps
- No single source describes complete settlement-to-road pipeline
- No published cost-function weight sets
- Landmark placement has zero algorithmic treatment
- Hub-and-spoke, organic branching roads: blog-post level only

---

## PASS 6a — Multi-Scale Generation and Graph-Based World Structure

### Techniques Covered (Multi-Scale)
1. **Chunk-based generation** — Fixed-size spatial cells (Minecraft: 16×16×384). Multi-stage pipeline: empty→structure_starts→structure_references→biomes→noise→surface→carvers→features→light→full. Coordinate-seeded randomness (ChunkRandom 4 methods). Cross-chunk: structure_starts/references + 3×3 write area.
2. **Hierarchical world-to-local** — Nested scales: continent → climate → biome → terrain → props. Info flows down as constraints, up as statistics. Mapgen2: coast-first. Mapgen4: elevation-first. No Man's Sky: 6 scales from universe seed.
3. **Coordinate-based determinism** — Hash-based (stateless, order-independent) vs sequential PRNG (fragile). Critical: discarding and regenerating chunk must be bit-identical. Minecraft seeding bugs at seed 107038380838084.
4. **Lazy generation** — Defer chunk production until needed. Minecraft ticket system (load levels 22-44). Background thread pool. Movement threshold to avoid redundant recalc.
5. **LOD-aware generation** — Vertex skipping at LOD level n (every 2^n-th vertex). CDLOD: per-vertex morphing eliminates popping and T-junctions. Geometry clipmaps. LOD-dependent octave counts (danger: aggregate height shift).
6. **Boundary matching** — Four sub-cases:
   - Height seams: global normalization (not per-chunk)
   - Normal seams: extended grid technique (+1 row/col each direction)
   - LOD cracks: skirts, degenerate triangles, precomputed index buffers, CDLOD morphing, clipmap stitching
   - Content discontinuities: erosion (global pre-pass), WFC (Marian42: offset tiling), biome blending (KdotJPG: sparse convolution)

### Techniques Covered (Graph-Based)
7. **Overworld graphs** — Regions=nodes, connections=edges. Graph-first (BambooleanLogic: cycle+branch components, connectivity parameter) vs mesh-first (mapgen2: Poisson→Delaunay→Voronoi→assign properties). MST+reconnection for connectivity control.
8. **Critical path generation** — Dormans: linear seed graph + rewrite rules (reorganize, insert lock-key, move lock forward, move key backward). Unexplored: cyclic backbone with 24 major cycle types. Spelunky: 4×4 grid random walk. ASP: constraint-satisfaction with A* verification.
9. **Progression gating** — Lock-and-key model generalized. Metazelda key-levels. Dead Cells: hand-designed macro gating + procedural micro rooms. Failure: soft-lock from suboptimal consumable key use.
10. **Region connectivity** — Delaunay→MST→selective reconnection (10-15%). Flood fill validation from start. Directed analysis for one-way connections. Gated reachability: simulate traversal with inventory tracking.

### Multi-Scale Pipeline Pattern
1. Overworld graph (once at creation) → 2. Region parameter resolution (on entry) → 3. Lazy chunk generation (on demand, coordinate-seeded) → 4. Local generators from earlier passes → 5. LOD mesh generation → 6. Boundary matching

### Common Architectural Mistakes
- Leaking global state into chunk generation (erosion, rivers)
- Ignoring overworld graph during local generation
- Insufficient boundary matching (height OK but normals, LOD, WFC, biome blending broken)

---

## CROSS-PASS DATA STRUCTURE CATALOG

| Data Structure | Storage Format | Produced By | Consumed By |
|---|---|---|---|
| **Scalar field** | 2D float[] (64²-256² per chunk) or on-demand | All noise (1a), fBm/ridged/turbulence/warp (1b), heightmap (2a) | Terrain mesh, biome classification, density fields, erosion |
| **Gradient field** | vec3 or vec4 per sample | Derivative-returning noise (1a), derivative fBm (1b) | Analytic normals, slope texturing, erosion direction |
| **Distance field (SDF)** | 2D/3D float[] grid or procedural | SDF primitives + Boolean ops (1b) | Mesh extraction (MC), collision, proximity effects, masks |
| **Point set** | Array of float positions | Poisson disk (1a), stratified (1a) | Object placement (5b), Voronoi seeds (5a) |
| **Mask** | 2D float[0,1] or binary | Smoothstep thresholding, SDF, slope, exclusion | Scalar field composition, placement filtering |
| **Tile grid** | 2D int array | BSP/CA/walk/agent dungeon (3a), WFC (4b) | Autotiling (4a), connectivity verification, rendering |
| **Voxel grid** | Chunked 3D float[] (16³-32³) | Heightmap→density conversion, 3D noise, cave carving (2b) | MC/DC/surface nets mesh extraction (2b) |
| **Graph (general)** | Adjacency list or matrix | Delaunay (5a), MST (3a/5b), mission grammar (3b), overworld (6a) | Room connectivity, road routing, progression gating |
| **Region map** | Per-cell region ID | Voronoi (5a), BSP (5a), flood fill CCA (5a) | Biome assignment, adjacency graph, placement |
| **Adjacency map** | Graph of region IDs + border metadata | Delaunay dual of Voronoi (5a), BSP tree siblings | Biome constraints, moisture simulation, road routing |
| **Biome map** | Per-cell enum + blend weights | Whittaker lookup (5a), multi-noise (5a) | Density fields (5b), material selection, asset palettes |
| **Spline** | Array of control points + type | Road routing (5b), river paths (2b) | Road mesh generation, terrain modification, exclusion masks |
| **Chunk coordinate system** | Hash map: (chunkX,chunkZ) → chunk data | Chunk-based generation (6a) | Lazy loading, LOD management, boundary matching |

---

## DOCUMENTATION QUALITY ASSESSMENT

### Well-Documented (implementable from published sources)
- Noise primitives (Perlin, simplex, Worley, fBm) — Quilez, Gustavson, Book of Shaders
- Poisson disk sampling — Bridson's 1-page paper
- Hydraulic erosion (particle) — Lague's complete C#/compute shader implementation
- BSP dungeons — RogueBasin JICE article, multiple complete implementations
- Room-and-corridor — Nystrom's "Rooms and Mazes" with interactive demos
- Cellular automata caves — RogueBasin, Jeremy Kun, Wolverson's Rust tutorial
- Autotiling (bitmask) — Boris the Brave's complete classification system
- WFC (simple tiled) — Boris the Brave's walkthrough, gridbugs.org Rust implementation
- A* pathfinding on cost maps — Red Blob Games interactive tutorials
- Voronoi/Delaunay partitioning — Patel's polygon map generator

### Adequately Documented (implementable with effort)
- Domain warping — Quilez's canonical examples
- Ridged multifractal — Musgrave's musgrave.c source
- Thermal erosion — Olsen's reference pseudocode
- WFC (overlapping) — Gumin's README + academic analysis
- L-systems — Prusinkiewicz & Lindenmayer's ABOP
- Marching cubes — Bourke's lookup tables + Lewiner's MC33
- River generation on irregular meshes — mapgen4 source + Andrino
- Biome blending — KdotJPG's scattered blend

### Poorly Documented (significant gaps at implementation level)
- **Mission graph generation** — Dormans' papers have no pseudocode; spatial fitness function undefined; force-directed parameters missing
- **Grammar authoring** — Unexplored's ~5,000 rules proprietary; no methodology for designing production grammars
- **Fungible key generation** — Explicitly unsolved; no algorithm guarantees solvability with consumable interchangeable keys
- **Graph-to-map transformation** — Dormans flags as underspecified in his own paper
- **Settlement placement scoring** — Conceptual frameworks only; no tested weight sets
- **Landmark placement** — Zero algorithmic treatment in literature
- **Road network integration** — No single source covers complete pipeline from settlements through final rendering
- **Hierarchical road networks** — Galin 2011 only
- **Dual contouring QEF** — Implementation details scattered; manifold issues poorly addressed
- **3D cellular automata** — No standard 3D rule parameters published
- **Chunk-boundary WFC** — Only Marian42's offset-tiling approach documented
- **Erosion across chunk boundaries** — No satisfactory general solution published

---

## TECHNIQUE-BY-GAME-PROBLEM MAPPING

| Game Problem | Primary Techniques | Pipeline Order |
|---|---|---|
| Continents/coastlines | fBm + island mask + domain warping | noise → mask → warp → threshold at sea level |
| Mountains | Ridged multifractal + cubed regional mask | regional mask → ridged noise → power redistribution |
| Rivers | Priority-Flood + D8 flow + accumulation threshold (or mapgen4 upstream growth) | depression fill → flow direction → accumulation → carve |
| Caves | CA (2D roguelike), dual-noise intersection (3D voxel), Perlin worms (3D corridors) | noise/CA → connectivity verification → mesh extraction |
| Biomes | Temperature-moisture Whittaker lookup + scattered blend | elevation → temperature → moisture → lookup → blend |
| Dungeons | BSP/room-corridor (simple), graph-grammar + lock-key (structured) | mission graph → spatial realization → connectivity verify |
| Settlements | Multi-criteria terrain scoring + Voronoi expansion | terrain scoring → separation → expansion → political |
| Roads | Delaunay→MST→A* on cost map→Catmull-Rom spline | settlement nodes → MST → per-edge routing → spline → terrain mod |
| Vegetation | Hierarchical Poisson disk + density fields + slope/exclusion masks | density field → masks → hierarchical scatter → clustering |
| World maps | Voronoi dual mesh + overworld graph + biome pipeline | seeds → Voronoi → elevation → climate → biomes → rivers |
| Local area maps | Chunk-based noise + LOD + boundary matching | coordinate-seeded noise → mesh → LOD → stitch |
| Progression spaces | Mission grammar + lock-key + critical path | seed graph → rewrite rules → spatial realization → verify |
