# Research Prompt — Pass 4b: Constraint Propagation, WFC, and Grammar Systems

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should be able to implement
a basic version and understand the most important failure modes. For WFC specifically, the
standard is higher: the simple tiled model should be fully implementable from the writeup alone.

The reader already understands the local rule-based techniques from Pass 4a (autotiling,
Wang tiles, adjacency-rule systems, socket-based modular placement) — do not re-explain those.
Do explain how the techniques in this pass differ from local rule-based approaches and what
that difference enables.

---

## Scope — cover only these techniques, in this order

1. Constraint propagation (as the general concept underlying WFC and related systems)
2. Wave Function Collapse — simple tiled model and overlapping model
3. L-systems (deterministic, stochastic, parametric, context-sensitive)
4. Shape grammars and grammar-based generation

These are *propagation and grammar-based* techniques: they enforce constraints across a
non-local domain (WFC, constraint propagation) or generate content through recursive
rewriting rules (L-systems, shape grammars). Do NOT cover autotiling, Wang tiles,
adjacency-rule systems, or socket-based placement — those are in Pass 4a.

Do NOT cover noise functions, terrain erosion, dungeon layout, or biome systems. Those are
separate passes.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What does this technique produce that local rule-based systems
(Pass 4a) fundamentally cannot? What game contexts require this level of global structure
or generative expressiveness?

**Algorithm at implementation depth.** Inputs, outputs, and the steps a developer needs to
reproduce it. Use pseudocode or numbered steps wherever the sequence is non-obvious.
Distinguish the algorithm itself from specific library implementations (e.g., DeBroglie,
Tessera for WFC; specific L-system engines for grammars).

**Variants and tradeoffs.** Name the main variants, state what concretely changes between
them, and give the practical consequence. For WFC, cover the simple tiled model and
overlapping model as distinct variants with different inputs, entropy behavior, and use cases.

**Failure modes.** Name the specific failure (contradictions in WFC, exponential blowup in
grammars, global structure loss, non-terminating derivations, etc.), its proximate cause,
and the fix.

**Combinations.** How does this technique connect to others in this pass and to techniques
from other passes? How does WFC interact with dungeon layout (Pass 3), biome systems (Pass 5),
or noise-based seeding (Pass 1)?

---

## Depth floor

Constraint propagation as a standalone entry may be shorter — it serves primarily as the
conceptual foundation for WFC. WFC will naturally be the longest entry given its variant
space, failure modes, and production usage. L-systems and shape grammars vary significantly
in how well-documented they are at implementation level — flag gaps honestly. Do not compress
or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".
Distinguish the algorithm from specific implementations throughout.

---

## Priority sources

Maxim Gumin (original WFC repository and README) ·
Boris the Brave ("Wave Function Collapse Explained", "WFC Tips and Tricks", DeBroglie) ·
Brian Bucklew (GDC 2019 — WFC in Caves of Qud) ·
Oskar Stålberg (Bad North, Townscaper) ·
Paul Merrell (Model Synthesis — WFC precursor) ·
Prusinkiewicz and Lindenmayer ("The Algorithmic Beauty of Plants") ·
Red Blob Games (tile transitions — relevant as bridge from Pass 4a)

---

## WFC deep dive (write this as a dedicated section after the 4 technique writeups)

WFC warrants additional depth beyond the standard writeup. Cover all of the following:

- Constraint propagation from first principles, following Boris the Brave's framing —
  how arc consistency and propagation work before any WFC-specific logic is introduced
- What changes between the simple tiled model and the overlapping model: inputs, how
  adjacency rules are derived vs authored, entropy calculation, propagation behavior,
  and typical output character
- Contradiction handling and backtracking: how they work mechanically, when backtracking
  is needed, and its performance cost
- Adding non-local constraints: path connectivity, fixed tiles, boundary conditions —
  how each is implemented and at what cost
- Multi-cell tiles and their implementation challenges
- Practical integration challenges in games: global structure loss, biome-level filtering,
  seeding designer intent — what the known approaches are and where documentation is thin
- Known production uses — Bad North, Caves of Qud, Townscaper: what each used specifically,
  what problem it solved, and what the implementation looked like

---

## Closing sections (write these after the WFC deep dive)

**Comparison — autotiling vs WFC for tile-based content (1–2 paragraphs).**
This bridges Pass 4a and 4b. When is simple autotiling sufficient and when does WFC add
value that justifies its cost? Be concrete about the conditions — avoid "it depends" without
a following criterion.

**Comparison — grammar systems vs WFC (1–2 paragraphs).**
What each is best at, how inputs differ, and when to choose each. Note that L-systems and
shape grammars generate content through expansion while WFC generates through constraint
satisfaction — what does this mean for the kinds of content each can and cannot produce?

**Source landscape.**
Group the best references across both Pass 4a and 4b by type (tutorial, academic paper,
GDC talk, interactive tool, repository). One sentence per entry on what it is uniquely
valuable for that others in the same category do not cover.
