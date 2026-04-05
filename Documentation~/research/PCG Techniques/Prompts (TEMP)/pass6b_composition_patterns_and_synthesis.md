# Research Prompt — Pass 6b: Composition Patterns, Synthesis, and Final Reference

You are a technical research assistant specialising in procedural content generation for games.

This is the final integrative pass. It requires no new technique research — it synthesizes
and connects the material from all earlier passes. The companion document
"PCG Research Synthesis — Rehydration Context for Pass 6b" contains the condensed findings
from passes 1a through 6a. Use it as your primary reference. Reference all techniques by name
without re-explaining them. The value of this pass is in showing how techniques compose,
what data flows between them, how to sequence learning, and where the documentation landscape
is weak.

---

## Part 1 — Composition patterns

For each combination below, write 2–3 paragraphs covering:
- Why the combination works (what each component contributes that the other cannot)
- The stage order and the intermediate data structure produced at each handoff
- 2–3 common mistakes that occur **specifically at the boundary** between stages — not general
  mistakes already covered in individual technique passes

### Combinations (in order):

**1. Noise + masks + biome assignment**
Pass 1b scalar fields (fBm, ridged, domain-warped) → smoothstep/power thresholding to produce masks → Pass 5a Whittaker biome classification via temperature-moisture lookup.
Key data handoffs: scalar field → mask (float[0,1]) → biome enum + blend weights.
Boundary concern: correlated seeds between elevation and moisture noise; biome noise frequency vs terrain noise frequency; dynamic range collapse from cascaded mask multiplication.

**2. Heightmap + erosion + rivers**
Pass 2a noise-to-heightmap pipeline (fBm + island mask + mountain shaping) → hydraulic erosion (Lague particle method, 70K-200K droplets) + thermal erosion → Pass 2b Priority-Flood depression filling + D8 flow directions + flow accumulation thresholding.
Key data handoffs: float heightmap → eroded heightmap + sediment/slope maps → flow-direction grid + flow-accumulation grid + river polylines.
Boundary concern: erosion before vs after mountain shaping; depression-fill altering erosion-carved features; river threshold sensitivity to heightmap resolution.

**3. Voronoi regions + graph connectivity**
Pass 5a Voronoi/Delaunay dual mesh (Poisson disc seeds → Delaunator → polygons with adjacency) → Pass 6a overworld graph construction (nodes=regions, edges from Delaunay, MST extraction + 10-15% reconnection for loops).
Key data handoffs: point set → Delaunay triangulation + Voronoi polygons → region adjacency graph → overworld graph with biome/progression labels.
Boundary concern: Voronoi cell irregularity affecting graph edge weights; non-planar graph from reconnection conflicting with 2D realization; graph connectivity vs spatial proximity mismatch.

**4. Cellular automata + cleanup pass**
Pass 3a CA caves (40-45% fill, 4-5 rule, 15 iterations) or Pass 2b CA cave generation → flood fill connectivity verification → corridor drilling or region culling.
Key data handoffs: tile grid (binary wall/floor) → connected component labels + component sizes → repaired tile grid with guaranteed connectivity.
Boundary concern: corridor drilling destroying CA's organic character; flood fill seed placement affecting which component survives; resolution-scaling (Kun's 4× upscale) interaction with connectivity pass.

**5. WFC + designer-authored constraints**
Pass 4b WFC (simple tiled model with AC-4 propagation) → fixed tiles for entrance/exit, boundary conditions for grid edges, path connectivity constraints (DeBroglie), critical path seeding from Pass 6a mission graph.
Key data handoffs: tile set + adjacency rules + weight table → pre-collapsed cells + boundary-banned tiles → WFC domain arrays → solved tile grid.
Boundary concern: fixed tiles creating propagation cascades that over-constrain; path constraints requiring backtracking (performance-heavy); mission graph room types lacking matching tile subsets in the tileset.

**6. World graph + local terrain realization**
Pass 6a overworld graph (region nodes with biome/progression labels) → Pass 2a/2b terrain generation per region (fBm with region-specific parameters) + Pass 3a/3b dungeon layout for dungeon-tagged nodes.
Key data handoffs: graph node (biome ID, elevation range, feature set, dungeon flag) → per-region heightmap or dungeon tile grid → stitched world with consistent boundaries.
Boundary concern: terrain parameter discontinuity at region boundaries despite biome blending; dungeon entrances misaligned with terrain surface; overworld graph edge traversability not matching realized terrain passability.

**7. Poisson disk sampling + slope and biome filtering**
Pass 1a Poisson disk (Bridson's algorithm, minimum distance r) → Pass 5b density field modulation + slope filtering (smoothstep ramp on gradient magnitude) + biome-based exclusion masks.
Key data handoffs: point set (positions) → density-modulated candidates → slope/altitude-filtered candidates → exclusion-masked final placements.
Boundary concern: density-boundary clumping when radius modulation produces different spacings across a biome edge; slope mask resolution mismatch with placement grid; exclusion mask not updated between hierarchical passes.

**8. Random walk + room graph scaffolding**
Pass 3a random walk or agent diggers (floor carving with lifetime/direction parameters) → Pass 3b graph-driven realization (connectivity graph extracted from carved geometry, or graph providing targets for walk).
Key data handoffs: tile grid with carved floors → extracted room regions + adjacency graph → graph with room types and lock-key annotations OR graph providing target positions → agent walks constrained to connect graph nodes.
Boundary concern: extracted graph topology being too simple (tree-like) for lock-key systems requiring cycles; room boundaries ambiguous in organic walk output; graph-imposed connectivity conflicting with walk's organic character.

---

## Part 2 — Data structure catalog

For each data structure below, write one paragraph covering:
- What it contains
- Typical storage format in a game context (array of floats, adjacency list, spatial grid, etc.)
- Which techniques from earlier passes **produce** it
- Which techniques **consume** it
- Any important conversion or compatibility notes between passes

Use the cross-pass data structure table from the synthesis document as a starting point,
but add depth on conversion issues and compatibility concerns that the table cannot capture.

**Data structures to cover:**
scalar field · mask · tile grid · voxel grid · graph (general) · point set ·
spline · signed distance field · region map · adjacency map ·
chunk coordinate system · biome map

---

## Part 3 — Techniques by game problem

For each game problem below, write 2–4 sentences covering:
- Which techniques from earlier passes are relevant
- A typical pipeline in the correct stage order
- The key tradeoff a developer will face

Be concrete — avoid "it depends" without a following criterion. Use the
technique-by-game-problem table from the synthesis document as a scaffold,
but add the tradeoff analysis that the table lacks.

**Game problems:**
continents and coastlines · mountains · rivers · caves · biomes ·
dungeons · villages and settlements · roads · vegetation · world maps ·
local area maps · progression spaces

---

## Part 4 — Learning roadmap

A phased learning sequence for the full PCG technique library. Write 4–5 phases.
For each phase:
- Name the techniques to learn in this phase
- Explain why this group before the next (what dependencies or mental models does it establish)
- Flag any techniques commonly learned too early or skipped incorrectly

Suggested phase structure based on dependency analysis from the synthesis:

- **Phase 1**: Coordinate hashing, Perlin/simplex/OpenSimplex2, fBm, value noise. (Foundation: everything downstream samples noise)
- **Phase 2**: Heightmaps, power redistribution, island masks, domain warping, Worley noise, Poisson disk. (Spatial composition: combining primitives into terrain and point distributions)
- **Phase 3**: Erosion (particle), cellular automata, BSP dungeons, room-and-corridor, connectivity verification, autotiling. (Simulation and spatial generation: consuming fields to produce playable geometry)
- **Phase 4**: WFC, Voronoi/Delaunay, biome systems (Whittaker), scalar field composition, river generation, object placement pipeline. (Constraint-based and region-based systems: techniques requiring multiple inputs)
- **Phase 5**: Mission graphs, lock-and-key, graph grammars, chunk-based streaming, LOD, boundary matching, road networks. (Architecture and structure: orchestrating all previous techniques into a coherent world)

Adjust, merge, or split phases as your analysis warrants. The above is a starting point, not a constraint.

---

## Part 5 — Open questions and documentation gaps

Write one cohesive section (not a list) covering:
- Which techniques are commonly oversimplified in tutorials and what specifically is missing
- Which techniques are poorly documented at implementation level and where the gap is
- Which techniques are well-covered and can be learned reliably from available sources

Use the documentation quality assessment from the synthesis document as ground truth.
Key gaps identified across passes:
- Dormans' mission/space pipeline: no pseudocode, spatial fitness undefined, force-directed params missing
- Fungible key generation: explicitly unsolved
- Grammar authoring methodology: Unexplored's ~5,000 rules proprietary, no systematic approach
- Settlement/landmark placement: conceptual only, no tested scoring functions
- Road network integration: no single source covers complete pipeline
- Chunk-boundary solutions for erosion, WFC, biome blending: scattered and incomplete
- Dual contouring QEF: scattered implementation details
- 3D CA caves: no standard parameters

Be specific — name the technique and the gap rather than speaking generally.

---

## Part 6 — Final synthesis

**Best default approach lookup table.**
A compact table with three columns: game problem → recommended default technique(s) →
when to deviate from the default. Cover the same twelve game problems from Part 3.
The "when to deviate" column should be concrete — a criterion, not a hedge.

**Most important techniques to learn first (1 short paragraph).**
Across the entire library, which 4–6 techniques give the most leverage as foundations?
The synthesis identifies: coordinate hashing, fBm, Poisson disk sampling, cellular automata,
Voronoi/Delaunay partitioning, and WFC (simple tiled). Evaluate whether this set is correct
and explain why those specifically — what do they unlock downstream that other techniques cannot?

**Most important comparisons to understand (1 short paragraph).**
Which technique comparisons matter most for a developer building their first PCG system?
Candidates from the synthesis:
- Heightmap vs voxel terrain (the overhang threshold)
- CA caves vs noise caves vs agent tunnels (connectivity, chunk-independence, control)
- Space-first vs structure-first dungeons (explorable spaces vs designed experiences)
- WFC vs autotiling (when local consistency requires propagation)
- fBm vs ridged multifractal (homogeneous vs heterogeneous terrain)
- Global vs local biome assignment (streaming vs designer control)

Evaluate which of these are the most consequential and explain what a wrong choice costs.

---

## Citation behavior

Cite sources for any factual claims about specific techniques, documented implementations,
or pipeline architectures. For synthesis and judgment (learning roadmap, gap analysis,
recommendations), citation is not required but should be noted where a specific source
informed the judgment.

---

## Priority sources

All sources cited in passes 1a through 6a are relevant. For architectural and pipeline
content specifically:
- Red Blob Games (mapgen2 and mapgen4 full pipeline architecture)
- Joris Dormans (mission/space pipeline, graph grammars)
- Boris the Brave (WFC, autotiling taxonomy, constraint-based generators, Unexplored reverse-engineering)
- Minecraft Wiki (complete chunk pipeline, multi-noise biome system, seeding architecture)
- Sebastian Lague (chunked terrain, erosion, cave generation)
- Inigo Quilez (fBm, domain warping, SDF primitives)
- Musgrave (ridged multifractal, erosion foundations)
- Galin et al. (road generation, cost functions, terrain modification)

---

## Format note

Parts 1–6 should be written in order. Parts 1 and 2 are the longest. Parts 3–6 should
be concise — the lookup table in Part 6 and the roadmap phases in Part 4 are the primary
deliverables in those sections, not extended prose.

---

## Success criterion

A developer who reads this document after reading the individual pass reports should:
1. Know how to sequence any pair of techniques into a working pipeline
2. Know which data structure to produce at each handoff point
3. Know the 2–3 mistakes most likely to occur at each technique boundary
4. Have a concrete learning order for the full technique library
5. Know which techniques have reliable documentation and which will require original engineering
