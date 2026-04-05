# Research Prompt — Pass 3a: Space-First Dungeon Generation Techniques

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should understand the algorithm
well enough to implement a basic version and diagnose the most common failure modes without
consulting additional sources.

---

## Scope — cover only these techniques, in this order

1. BSP dungeon generation
2. Room-and-corridor methods (random placement + pathfinding connection)
3. Random walk / drunkard's walk
4. Agent-based diggers
5. Cellular automata for cave-like spaces
6. Room templates and prefab placement
7. Connectivity verification (flood fill, graph analysis)
8. MST and Delaunay triangulation for room connectivity

These are all *space-first* techniques: they produce geometry directly, without a prior
structural or progression graph. Do NOT cover graph-driven dungeon generation, lock-and-key
structures, or mission/grammar-based generation — those operate at a different abstraction
layer and are covered in Pass 3b.

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

**Combinations.** How does this technique connect to others within this pass, to the
structure-first techniques in Pass 3b, and to placement or WFC techniques from other passes?

---

## Depth floor

MST and Delaunay triangulation are tools applied to room graphs rather than complete generation
methods — their entries may be shorter. Connectivity verification is similarly a support technique.
BSP and room-and-corridor are the most heavily documented techniques here and will naturally
be longer. Do not compress or pad to match length.

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
Bob Nystrom (dungeon generation articles) ·
Brian Bucklew (Caves of Qud GDC talk — CA and constructive methods) ·
Antonios Liapis (PCG book chapter on constructive dungeon generation)

---

## Closing sections (write these after all 8 technique writeups)

**Comparison — cellular automata caves vs random walk vs agent diggers (1–2 paragraphs).**
Organic quality, connectivity guarantees, and designer control over output. Be concrete about
what each produces and under what conditions each is the right choice.

**Space-first pipeline patterns (1 paragraph).**
The typical sequencing of techniques within a space-first dungeon generator — for example,
how room placement, MST connectivity, corridor carving, template injection, and connectivity
verification compose in practice. Note that structure-first techniques (Pass 3b) can wrap
or scaffold this pipeline to add progression awareness.

**Source landscape.**
Group best references by type (tutorial, GDC talk, repository, academic paper).
One sentence per entry on what it is uniquely valuable for that others in the same category
do not cover.
