# Research Prompt — Pass 5: Biomes, Placement, Scattering, and Region Partitioning

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each section should understand how to implement
the technique and connect it correctly to adjacent stages in a typical pipeline.

---

## Techniques to cover

Organize coverage into four groups. Within each group, treat each technique as a distinct entry.

**Region partitioning:**
Voronoi partitioning and Delaunay triangulation for map structure · Lloyd's relaxation ·
BSP as a spatial partitioning tool (not dungeon-specific) ·
Quadtrees and octrees for spatial organization ·
Region graphs and adjacency structures · Connected component analysis

**Biome and climate systems:**
Temperature-moisture model (Whittaker diagram) · Altitude-based biome assignment ·
Rainfall and rainshadow approximation · Wind-based moisture simulation (Red Blob Games mapgen4) ·
Biome masks and noise-based biome selection · Biome transition blending ·
Local vs global biome assignment

**Object placement and scattering:**
Density field-modulated placement · Slope-aware and altitude-aware filtering ·
Exclusion masks · Hierarchical scattering · Clustering approaches ·
Landmark and settlement placement
(Poisson disk sampling as an algorithm is covered in Pass 1 — cover only its application
to placement pipelines here.)

**Road and path generation:**
A* and Dijkstra on cost maps for road routing · Minimum spanning tree for network connectivity ·
Spline fitting for road smoothing · Road network generation strategies

Do NOT cover noise functions, terrain erosion, dungeon internals, or WFC. Those are separate passes.

---

## Per-technique content requirements

For each technique, write 2–4 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What does this technique produce and why is it used over simpler
alternatives? What does the output look like?

**Algorithm at implementation depth.** Inputs, outputs, key steps. Pseudocode or numbered
steps where the sequence is non-obvious.

**Failure modes.** Named artifact or problem, its proximate cause, and the fix.

**Pipeline connections.** What this technique receives from upstream stages and what it
produces for downstream. How does it fit into a biome or placement pipeline?

---

## Depth floor

Region partitioning techniques (Voronoi, Lloyd's, BSP) and climate simulation techniques
(wind-based moisture, rainshadow) tend to have more implementation detail than simpler
filtering operations (slope-aware filtering, exclusion masks). Let the available material
determine length — do not pad or compress to match.

Note where documentation is thin. Road network generation and settlement placement are
known to be poorly documented at implementation level — flag this honestly.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".

---

## Priority sources

Red Blob Games (polygon map generation, mapgen4 — wind, rainfall, river simulation) ·
Sebastian Lague (procedural planet generation) ·
Minecraft biome generation documentation and technical analysis ·
Amit Patel biome and moisture simulation articles

---

## Closing sections (write these after all technique writeups)

**Typical biome generation pipeline (numbered stage list + 1 paragraph).**
Stage order, what data flows between stages, and 3–5 common mistakes that produce
incorrect or visually unconvincing biome assignments.

**Typical vegetation and prop placement pipeline (numbered stage list + 1 paragraph).**
From biome map to placed objects — how density, slope, and exclusion interact in practice,
and 3–5 common mistakes.

**Road and settlement generation — state of documentation (1 paragraph).**
How roads connect settlements, cost-map-based routing in practice, and an honest assessment
of where implementation-level documentation is missing or underspecified in available sources.

**Source landscape.**
Group best references by type (interactive tool, tutorial, paper, dev blog).
One sentence per entry on what it is uniquely valuable for.
