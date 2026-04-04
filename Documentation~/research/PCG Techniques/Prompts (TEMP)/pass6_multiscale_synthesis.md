# Research Prompt — Pass 6: Multi-Scale Generation, Composition Patterns, and PCG Synthesis

You are a technical research assistant specialising in procedural content generation for games.
This is the integrative pass — reference techniques from earlier passes by name without
re-explaining them.

The output standard: this document should serve as the connective layer between the five earlier
passes, showing how techniques compose into pipelines, what data flows between stages, and how
a developer should sequence their learning. It should also provide full treatment of multi-scale
techniques not covered in earlier passes.

---

## Part 1 — Multi-scale and streaming techniques

These techniques are not covered in earlier passes and require full treatment here.

For each, write 3–5 paragraphs covering: what problem it solves, the algorithm at implementation
depth (pseudocode or numbered steps where non-obvious), key variants and tradeoffs, failure
modes with causes and fixes, and how it connects to terrain and biome systems from earlier passes.

Techniques to cover:
- Chunk-based generation (Minecraft-style coordinate-seeded generation)
- Hierarchical world-to-local generation (overworld → regional → local)
- Coordinate-based deterministic regeneration
- Lazy generation and on-demand computation
- LOD-aware procedural generation
- Boundary matching between chunks or regions

---

## Part 2 — Graph-based world structure

Also requires full treatment. Same format as Part 1.

Techniques to cover:
- Overworld graphs (nodes = regions, edges = connections)
- Critical path generation and optional branching
- Progression gating in generated spaces
- Region connectivity and world traversability

---

## Citation behavior

Cite the specific source immediately after each concrete claim about an algorithm, parameter,
or pipeline behavior. Mark claims that cannot be attributed as "unconfirmed". This pass
synthesizes across many domains — source attribution is especially important here.

---

## Priority sources

Sebastian Lague (chunked terrain, LOD) ·
Red Blob Games (mapgen2 and mapgen4 pipeline architecture) ·
Minecraft technical analysis (chunk generation, biome computation) ·
Joris Dormans (mission/space generation graphs) ·
GDC talks on world generation architecture

---

## Part 3 — Composition patterns (write after Parts 1 and 2)

For each combination below, write 2–3 paragraphs covering: why the combination works,
the stage order, the intermediate data structure produced at each stage, and 2–3 common
mistakes that occur at the handoff between stages.

Combinations to cover:
- Noise + masks + biome assignment
- Heightmap + erosion + rivers
- Voronoi regions + graph connectivity
- Cellular automata + cleanup pass
- WFC + designer-authored constraints
- World graph + local terrain realization
- Poisson disk sampling + slope and biome filtering
- Random walk + room graph scaffolding

---

## Closing sections (write these after Part 3)

**Data structure catalog.**
For each data structure that appears across PCG pipelines — scalar field, mask, tile grid,
voxel grid, graph, point set, spline, SDF, region map, adjacency map, chunk coordinates,
biome map — write one paragraph: what it contains, typical storage format in a game context,
which techniques produce it, and which techniques consume it.

**Techniques by game problem.**
For each of the following game problems, name the relevant techniques (referencing earlier
passes), describe a typical pipeline in one or two sentences, and name the key tradeoff
a developer will face:

continents and coastlines · mountains · rivers · caves · biomes · dungeons ·
villages and settlements · roads · vegetation · world maps · local area maps ·
progression spaces

**Learning roadmap.**
A phased learning sequence for PCG techniques from all six passes, with rationale for the
ordering. 3–5 phases. For each phase: which techniques to learn, and why this group before
the next one.

**Open questions and documentation gaps.**
Which techniques are commonly oversimplified in tutorials? Which are poorly documented at
implementation level? Which are well-covered? Be specific — name the technique and the gap.

**Final synthesis — best default approach lookup table.**
A compact table: game problem → recommended default technique(s) → when to deviate.
Cover the same game problems listed in "Techniques by game problem" above.
