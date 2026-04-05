# Research Prompt — Pass 6a: Multi-Scale Generation and Graph-Based World Structure

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should be able to implement
a basic version and understand the most important failure modes without consulting additional
sources.

The reader already understands all techniques from earlier passes — noise functions and scalar
fields (1a/1b), heightmap construction, erosion, rivers, caves, and voxel terrain (2a/2b),
space-first and structure-first dungeon generation (3a/3b), local rule-based tile placement
and WFC/grammar systems (4a/4b), and region partitioning, biome systems, placement, and roads
(5a/5b). Reference these by name without re-explaining them. Do explain how the techniques in
this pass build on, wrap, or orchestrate those earlier systems.

---

## Scope — cover only these techniques, in this order

**Multi-scale and streaming-friendly generation:**
1. Chunk-based generation (Minecraft-style coordinate-seeded generation)
2. Hierarchical world-to-local generation (overworld → regional → local)
3. Coordinate-based deterministic regeneration
4. Lazy generation and on-demand computation
5. LOD-aware procedural generation
6. Boundary matching between chunks or regions

**Graph-based world structure:**
7. Overworld graphs (nodes = regions, edges = connections)
8. Critical path generation and optional branching
9. Progression gating in generated spaces
10. Region connectivity and world traversability

These techniques are not covered in earlier passes and require full treatment here.
Do NOT cover composition patterns, data structure catalogs, learning roadmaps, or synthesis
tables — those are the subject of Pass 6b.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What problem does this technique solve that earlier, non-streaming
or non-hierarchical approaches do not? What breaks without it at scale?

**Algorithm at implementation depth.** Inputs, outputs, and the key steps a developer needs
to reproduce it. Use pseudocode or numbered steps wherever the sequence of operations is
non-obvious.

**Variants and tradeoffs.** Name the main variants, state what concretely changes between
them, and give the practical consequence — not just that they differ.

**Failure modes.** Name the specific failure (chunk boundary seams, determinism violations
across reload, LOD popping, disconnected overworld graphs, etc.), its proximate cause,
and the fix.

**Connections to earlier passes.** Which techniques from passes 1–5 does this technique
wrap, orchestrate, or impose constraints on? For example: how does chunk-based generation
affect where fBm can be evaluated? How does boundary matching interact with erosion outputs?
How does a critical path graph constrain dungeon layout or biome assignment?

---

## Depth floor

Chunk-based generation and boundary matching are among the most practically important
techniques here and will naturally be longer given their failure mode space. Coordinate-based
deterministic regeneration may be shorter — it overlaps significantly with the coordinate
hashing material from Pass 1a. Flag this and cross-reference rather than repeating.
Do not compress or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".

---

## Priority sources

Sebastian Lague (chunked terrain, LOD) ·
Red Blob Games (mapgen2 and mapgen4 pipeline architecture) ·
Minecraft technical analysis (chunk generation, coordinate-seeded biome computation) ·
Joris Dormans (mission/space generation graphs — overworld graph structure) ·
GDC talks on world generation architecture

---

## Closing section (write this after all 10 technique writeups)

**Multi-scale pipeline patterns (1 paragraph).**
How the techniques in this pass compose in a typical streaming world: which techniques
from this pass wrap which techniques from earlier passes, what the typical stage order
is when moving from world-level structure down to local detail, and where the most
common architectural mistakes occur. Frame this as preparation for the full composition
patterns covered in Pass 6b.

**Source landscape.**
Group best references for the techniques in this pass by type (GDC talk, technical
analysis, tutorial series, dev blog). One sentence per entry on what it is uniquely
valuable for that others in the same category do not cover.
