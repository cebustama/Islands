# Research Prompt — Pass 4: Rule-Based, Tile-Based, and Constraint-Based Generation

You are a technical research assistant specialising in procedural content generation for games.

The output standard: a developer reading each technique section should be able to implement a
basic version and understand the most important failure modes. For WFC specifically, the standard
is higher: the simple tiled model should be fully implementable from the writeup alone.

---

## Techniques to cover

Wave Function Collapse (WFC) — overlapping and simple tiled models ·
Constraint propagation (as a general concept underlying WFC and related systems) ·
Autotiling (bitmask-based tile selection) ·
Wang tiles ·
L-systems (deterministic, stochastic, parametric, context-sensitive) ·
Shape grammars and grammar-based generation ·
Socket-based / connector-based modular placement ·
Adjacency-rule systems

Do NOT cover noise functions, terrain erosion, or dungeon layout algorithms. Those are separate passes.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What specific problem does this technique solve? What can it
produce that simpler alternatives cannot?

**Algorithm at implementation depth.** Inputs, outputs, and the steps a developer needs to
reproduce it. Use pseudocode or numbered steps wherever the sequence is non-obvious.
Distinguish the algorithm from specific library implementations.

**Variants and tradeoffs.** Name the main variants, state what concretely changes between them,
and give the practical consequence.

**Failure modes.** Name the specific failure (contradictions, exponential blowup, global
structure loss, etc.), its proximate cause, and the fix.

**Combinations.** How does this technique connect to others in this pass and in adjacent passes?

---

## Depth floor

WFC will naturally be the longest entry given its complexity and variant space. Wang tiles and
autotiling may be shorter. Do not compress or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim.
If a claim cannot be attributed to a consulted source, mark it explicitly as "unconfirmed".
Distinguish the algorithm itself from specific library implementations (e.g., DeBroglie, Tessera).

---

## Priority sources

Maxim Gumin (original WFC repository and README) ·
Boris the Brave ("Wave Function Collapse Explained", "WFC Tips and Tricks", DeBroglie library) ·
Brian Bucklew (GDC 2019 — WFC in Caves of Qud) ·
Oskar Stålberg (Bad North, Townscaper) ·
Prusinkiewicz and Lindenmayer ("The Algorithmic Beauty of Plants") ·
Paul Merrell (Model Synthesis — WFC precursor) ·
Red Blob Games (tile transitions)

---

## WFC deep dive (write this as a dedicated section after the technique writeups)

WFC warrants additional depth beyond the standard writeup. Cover:

- Constraint propagation from first principles (following Boris the Brave's framing)
- What changes between the overlapping model and the simple tiled model — inputs, entropy
  calculation, propagation behavior
- Contradiction handling and backtracking: how they work, when backtracking is needed,
  and its cost
- Adding non-local constraints: path connectivity, fixed tiles, boundary conditions —
  how each is implemented
- Multi-cell tiles and their implementation challenges
- Practical integration challenges: global structure loss, biome-level filtering,
  seeding designer intent into a WFC output
- Known production uses — Bad North, Caves of Qud, Townscaper: what each used specifically
  and what problems each solved with it

---

## Closing sections (write these after the WFC deep dive)

**Comparison — grammar systems vs WFC (1–2 paragraphs).**
What each is best at, how inputs differ, and when to choose each. Be concrete.

**Comparison — autotiling vs WFC for tile-based content (1–2 paragraphs).**
When simple autotiling is sufficient and when WFC adds value that justifies its cost.

**Source landscape.**
Group best references by type (tutorial, paper, GDC talk, repository, interactive tool).
One sentence per entry on what it is uniquely valuable for.
