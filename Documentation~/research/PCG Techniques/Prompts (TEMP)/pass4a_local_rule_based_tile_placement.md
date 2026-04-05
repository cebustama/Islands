# Research Prompt — Pass 4a: Local Rule-Based Tile and Module Placement

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should be able to implement
a basic version and understand the most important failure modes without consulting additional
sources.

---

## Scope — cover only these techniques, in this order

1. Autotiling (bitmask-based tile selection)
2. Wang tiles
3. Adjacency-rule systems
4. Socket-based / connector-based modular placement

These are all *local rule-based* techniques: they determine tile or module selection based on
compatibility rules evaluated at or near the placement site, with no backtracking and no global
propagation state. Do NOT cover WFC, constraint propagation, L-systems, or shape grammars —
those require global propagation or rewriting and are covered in Pass 4b.

Do NOT cover noise functions, terrain erosion, dungeon layout, or biome systems. Those are
separate passes.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What specific problem does this technique solve? What can it
produce that manual tile placement or simpler alternatives cannot?

**Algorithm at implementation depth.** Inputs, outputs, and the steps a developer needs to
reproduce it. Use pseudocode or numbered steps wherever the sequence is non-obvious.
Distinguish the algorithm from specific tooling or editor implementations.

**Variants and tradeoffs.** Name the main variants, state what concretely changes between
them, and give the practical consequence — not just that they differ.

**Failure modes.** Name the specific failure (missing tile combinations, visible seams,
incompatible socket mismatches, bitmask lookup errors, etc.), its proximate cause, and the fix.

**Combinations.** How does this technique connect to others in this pass? How does it relate
to WFC (Pass 4b) as either a simpler alternative or a component within a hybrid system?
How does it connect to dungeon layout (Pass 3) or biome/placement techniques (Pass 5)?

---

## Depth floor

Socket-based modular placement has significantly more variant space than autotiling and will
naturally be longer. Wang tiles and adjacency-rule systems may be shorter if the available
implementation-level documentation is thin — flag this honestly rather than filling gaps.
Do not compress or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".
Distinguish the algorithm itself from specific editor or engine implementations.

---

## Priority sources

Red Blob Games (tile transition articles, autotiling) ·
Boris the Brave (autotiling, adjacency rules, DeBroglie library — relevant sections) ·
Oskar Stålberg (Townscaper, Bad North — modular placement) ·
Paul Merrell (Model Synthesis — relevant as precursor context for adjacency systems)

---

## Closing section (write this after all 4 technique writeups)

**Local rule-based systems in context (1 paragraph).**
How the techniques in this pass relate to each other on a spectrum from simplest to most
expressive, and where each sits relative to the propagation-based systems in Pass 4b.
Frame this as preparation for the autotiling vs WFC comparison that appears in Pass 4b.

**Source landscape.**
Group best references for the techniques in this pass by type (tutorial, interactive tool,
repository, academic paper). One sentence per entry on what it is uniquely valuable for
that others in the same category do not cover.
