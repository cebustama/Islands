# Research Prompt — Pass 3: Dungeon Generation and Interior Layout Techniques

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should understand the algorithm
well enough to implement a basic version and diagnose the most common failure modes without
consulting additional sources.

---

## Techniques to cover

BSP dungeon generation · Room-and-corridor methods (random placement + pathfinding connection) ·
Graph-driven dungeon generation · Random walk / drunkard's walk · Agent-based diggers ·
Cellular automata for cave-like spaces · Lock-and-key graph structures ·
Mission-based and grammar-based layout generation · Room templates and prefab placement ·
Connectivity verification (flood fill, graph analysis) ·
MST and Delaunay triangulation for room connectivity

Do NOT cover noise functions, terrain erosion, biome systems, or WFC. Those are separate passes.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What does this technique produce that simpler methods do not?
What game contexts is it suited or ill-suited for?

**Algorithm at implementation depth.** Inputs, outputs, and the steps a developer needs to
reproduce it. Use pseudocode or numbered steps wherever the sequence is non-obvious.

**Variants and tradeoffs.** Name the main variants, state what concretely changes between them,
and give the practical consequence — not just that they differ.

**Failure modes.** Name the specific failure (disconnected rooms, overlapping rooms, monotonous
layouts, degenerate partitions, etc.), its proximate cause, and the fix.

**Combinations.** How does this technique connect to others — within this pass, and to terrain,
WFC, or placement techniques from other passes?

---

## Depth floor

MST and Delaunay triangulation are tools applied to room graphs rather than complete generation
methods — their entries may be shorter. Lock-and-key structures and graph-driven generation will
naturally be longer given their complexity. Do not compress or pad to match length.

Note which techniques are well-documented at implementation level and which are not —
honest gaps are more useful than filled-in guesses.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".

---

## Priority sources

RogueBasin (BSP dungeon — JICE article, Dungeon-Building Algorithm — Mike Anderson/Tyrant) ·
Herbert Wolverson (Hands-On Rust roguelike tutorial) ·
Joris Dormans (lock-and-key generation, mission/space duality) ·
Brian Bucklew (Caves of Qud GDC talk) ·
Bob Nystrom (dungeon generation articles) ·
Antonios Liapis (PCG book chapter on constructive dungeon generation)

---

## Lock-and-key deep dive (write this as a dedicated section after the technique writeups)

Lock-and-key and mission graph generation warrants additional depth because it operates at a
different level of abstraction from spatial techniques — it structures progression, not just space.

Cover: how mission graphs encode progression constraints, how spatial layout is derived from a
mission graph (the mission/space duality in Dormans' framework), known implementations and their
specific approaches, and where current documentation is thin or implementation details are missing.

---

## Closing sections (write these after the lock-and-key deep dive)

**Comparison — BSP vs room-and-corridor vs graph-driven (1–2 paragraphs).**
What each produces, what control it gives over layout structure and progression gating,
and the conditions under which each is appropriate. Be concrete.

**Comparison — cellular automata caves vs random walk vs agent diggers (1–2 paragraphs).**
Organic quality, connectivity guarantees, and designer control over output.

**Source landscape.**
Group best references by type (tutorial, academic paper, GDC talk, repository).
One sentence per entry on what it is uniquely valuable for that others in the same category
do not cover.
