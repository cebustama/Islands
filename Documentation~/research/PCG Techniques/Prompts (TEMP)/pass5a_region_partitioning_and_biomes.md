# Research Prompt — Pass 5a: Region Partitioning and Biome/Climate Systems

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should understand how to
implement it and connect it correctly to adjacent stages in a typical pipeline.

The reader already understands noise functions, scalar field composition (Pass 1), and
heightmap-based terrain (Pass 2) — do not re-explain those. Do explain how the techniques
here build on or consume terrain and scalar field outputs.

---

## Scope — cover only these techniques, in this order

**Region partitioning:**
1. Voronoi partitioning and Delaunay triangulation for map structure
2. Lloyd's relaxation
3. BSP as a spatial partitioning tool (distinct from dungeon-specific BSP in Pass 3a)
4. Quadtrees and octrees for spatial organization
5. Region graphs and adjacency structures
6. Connected component analysis

**Biome and climate systems:**
7. Temperature-moisture model (Whittaker diagram approach)
8. Altitude-based biome assignment
9. Rainfall and rainshadow approximation
10. Wind-based moisture simulation (Red Blob Games mapgen4 approach)
11. Biome masks and noise-based biome selection
12. Biome transition blending
13. Local vs global biome assignment

These techniques answer the question: how is space organized and classified? They produce
the spatial skeleton and climate data that placement techniques (Pass 5b) consume.

Do NOT cover object placement, scattering, roads, or settlement generation — those are
in Pass 5b. Do NOT cover noise functions, terrain erosion, dungeon layout, or WFC.

---

## Per-technique content requirements

For each technique, write 2–4 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What does this technique produce and why is it used over simpler
alternatives?

**Algorithm at implementation depth.** Inputs, outputs, key steps. Pseudocode or numbered
steps where the sequence is non-obvious.

**Failure modes.** Named artifact or problem, its proximate cause, and the fix.

**Pipeline connections.** What this technique receives from upstream stages and what it
produces for downstream. How does it connect to other techniques in this pass and to the
placement and road techniques in Pass 5b?

---

## Depth floor

Voronoi/Delaunay and wind-based moisture simulation are the most algorithmically complex
entries and will naturally be longer. Altitude-based biome assignment, biome masks, and
local vs global assignment are simpler and may be shorter. Do not compress or pad to
match length. Flag thin documentation honestly rather than filling gaps.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".

---

## Priority sources

Red Blob Games (polygon map generation, mapgen4 — wind, rainfall, river simulation,
Voronoi regions, biome assignment) ·
Sebastian Lague (procedural planet generation) ·
Amit Patel biome and moisture simulation articles ·
Minecraft biome generation documentation and technical analysis

---

## Closing sections (write these after all 13 technique writeups)

**Typical biome generation pipeline (numbered stage list + 1 paragraph).**
The typical stage order from terrain heightmap through to final biome assignment — what
data flows between stages and what each stage receives and produces. Include 3–5 common
mistakes that cause incorrect or visually unconvincing biome assignments. Note which
outputs feed directly into the placement pipeline in Pass 5b.

**Source landscape.**
Group best references for the techniques in this pass by type (interactive tool, tutorial,
academic paper, dev blog). One sentence per entry on what it is uniquely valuable for that
others in the same category do not cover.
