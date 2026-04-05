# Research Prompt — Pass 5b: Object Placement, Scattering, and Road Generation

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should understand how to
implement it and connect it correctly to adjacent stages in a typical pipeline.

The reader already understands: noise functions and scalar field composition (Pass 1),
heightmap-based terrain (Pass 2), and the region partitioning and biome systems from
Pass 5a (Voronoi partitioning, Lloyd's relaxation, BSP as spatial tool, quadtrees/octrees,
region graphs, biome assignment, climate simulation, biome transition blending) — do not
re-explain those. Do explain how the techniques here consume biome maps, terrain data,
and region structures produced by earlier passes.

Poisson disk sampling as an algorithm is covered in Pass 1a — cover only its application
to placement pipelines here, not the algorithm itself.

---

## Scope — cover only these techniques, in this order

**Object placement and scattering:**
1. Density field-modulated placement
2. Slope-aware and altitude-aware filtering
3. Exclusion masks
4. Hierarchical scattering (large objects first, then fill)
5. Clustering approaches
6. Landmark and settlement placement

**Road and path generation:**
7. A* and Dijkstra on cost maps for road routing
8. Minimum spanning tree for road network connectivity
9. Spline fitting for road smoothing
10. Road network generation strategies

These techniques answer the question: what goes in the space that has been organized and
classified? They consume the outputs of Pass 5a (biome maps, region graphs, terrain data)
and produce the populated world.

Do NOT cover region partitioning, biome assignment, or climate simulation — those are in
Pass 5a. Do NOT cover noise functions, terrain erosion, dungeon layout, or WFC.

---

## Per-technique content requirements

For each technique, write 2–4 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What does this technique produce and why is it used over simpler
alternatives?

**Algorithm at implementation depth.** Inputs, outputs, key steps. Pseudocode or numbered
steps where the sequence is non-obvious.

**Failure modes.** Named artifact or problem, its proximate cause, and the fix.

**Pipeline connections.** What upstream data does this technique consume (biome map, terrain
heightmap, exclusion masks, region graph)? What does it produce for downstream stages or
the final world representation?

---

## Depth floor

Landmark and settlement placement and road network generation strategies are the least
well-documented techniques in this pass at implementation level — flag gaps honestly rather
than filling them. A*, MST, and spline fitting are well-documented and may be shorter.
Hierarchical scattering and density field-modulated placement sit in between. Do not
compress or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".

---

## Priority sources

Red Blob Games (polygon map generation, road routing on cost maps) ·
Amit Patel (pathfinding articles — A* and Dijkstra on grids and graphs) ·
Sebastian Lague (procedural planet generation — placement and scattering) ·
Bridson SIGGRAPH 2007 (for Poisson disk application context — algorithm is in Pass 1a)

---

## Closing sections (write these after all 10 technique writeups)

**Typical vegetation and prop placement pipeline (numbered stage list + 1 paragraph).**
From biome map and terrain data to placed objects — the full stage order, what data flows
between stages, and 3–5 common mistakes that occur when density, slope, and exclusion
interact incorrectly.

**Road and settlement generation — state of documentation (1 paragraph).**
How roads connect settlements in practice, what cost-map-based routing looks like
concretely, and an honest assessment of where implementation-level documentation is
missing or underspecified across the available sources. This is one of the least
well-documented areas in practical PCG — honest gap-flagging is especially valuable here.

**Pass 5 full pipeline (1 paragraph).**
How Pass 5a and 5b connect end-to-end: the stage order from raw terrain through region
partitioning, biome assignment, and into placement and road generation. What data structure
is handed off at each boundary and which passes downstream (Pass 6) consume the final outputs.

**Source landscape.**
Group best references across both Pass 5a and 5b by type (interactive tool, tutorial,
academic paper, dev blog). One sentence per entry on what it is uniquely valuable for
that others in the same category do not cover.
