# Research Prompt — Pass 3b: Structure-First and Progression-Aware Dungeon Generation

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should understand the algorithm
well enough to implement a basic version and diagnose the most common failure modes without
consulting additional sources.

The reader already understands the space-first dungeon techniques from Pass 3a (BSP,
room-and-corridor, random walk, agent diggers, CA caves, room templates, connectivity
verification, MST/Delaunay) — do not re-explain those. These techniques are the spatial
realization layer that structure-first approaches sit above.

---

## Scope — cover only these techniques, in this order

1. Graph-driven dungeon generation
2. Lock-and-key graph structures
3. Mission-based and grammar-based layout generation

These are *structure-first* techniques: they define a progression graph, mission structure,
or grammar before any spatial layout is produced, then derive space from that structure.
This is the abstraction layer Joris Dormans calls the mission/space duality.

Do NOT cover BSP, room-and-corridor, random walk, agent diggers, CA caves, room templates,
or connectivity verification — those are in Pass 3a.
Do NOT cover noise functions, terrain erosion, biome systems, or WFC. Those are separate passes.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What does this technique produce that space-first methods
(Pass 3a) fundamentally cannot? What game contexts require this level of structural control?

**Algorithm at implementation depth.** Inputs, outputs, and the steps a developer needs to
reproduce it. Use pseudocode or numbered steps wherever the sequence is non-obvious.
For graph-driven and mission-based techniques especially, be explicit about the boundary
between the structural pass (graph construction) and the spatial pass (layout realization).

**Variants and tradeoffs.** Name the main variants, state what concretely changes between them,
and give the practical consequence — not just that they differ.

**Failure modes.** Name the specific failure (unsatisfiable constraints, spatial conflicts from
graph assumptions, over-constrained grammars, etc.), its proximate cause, and the fix.

**Combinations.** How does this technique use space-first techniques from Pass 3a as its
spatial realization layer? How does it connect to placement, WFC, or biome techniques from
other passes?

---

## Depth floor

These three techniques are substantially more complex than the space-first techniques in Pass 3a —
their entries will naturally be longer. Lock-and-key and mission/grammar-based generation are
among the least well-documented at implementation level in the PCG literature; flag gaps honestly
rather than filling them. Do not compress to match the length of simpler techniques in other passes.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".

---

## Priority sources

Joris Dormans (lock-and-key generation, mission/space duality — GDC talk and academic papers) ·
Brian Bucklew (Caves of Qud GDC talk — graph-driven and constructive methods) ·
Antonios Liapis (PCG book chapter on constructive dungeon generation) ·
Herbert Wolverson (Hands-On Rust roguelike tutorial) ·
Bob Nystrom (dungeon generation articles)

---

## Lock-and-key and mission graph deep dive (write this as a dedicated section after the technique writeups)

Lock-and-key and mission graph generation warrants additional depth beyond the standard writeup
because it operates at a categorically different level of abstraction from spatial techniques —
it structures progression, not just space.

Cover all of the following:
- How mission graphs encode progression constraints (locks, keys, optional branches, critical path)
- How spatial layout is derived from a mission graph — the specific steps of the mission/space
  duality in Dormans' framework
- Known implementations and their specific approaches (what each concretely does, not just that
  they exist)
- Where current documentation is thin, ambiguous, or missing at implementation level — this is
  one of the least well-documented areas in practical PCG and honest gap-flagging is especially
  valuable here

---

## Closing sections (write these after the deep dive)

**Comparison — BSP vs room-and-corridor vs graph-driven (1–2 paragraphs).**
What each produces, what control it gives over layout structure and progression gating, and
the conditions under which each is appropriate. This comparison bridges Pass 3a and 3b —
it should be concrete about what the structure-first approach adds that space-first methods
cannot provide, and at what cost.

**State of documentation across Pass 3 (1 paragraph).**
An honest assessment of which dungeon generation techniques are well-documented at
implementation level (BSP, room-and-corridor, CA caves) and which are not (mission graphs,
grammar-based generation, graph-driven realization). Identify the specific gaps that would
most benefit a developer trying to implement the less-documented techniques.

**Source landscape.**
Group best references across both Pass 3a and 3b by type (tutorial, GDC talk, academic paper,
repository). One sentence per entry on what it is uniquely valuable for that others in the
same category do not cover.
