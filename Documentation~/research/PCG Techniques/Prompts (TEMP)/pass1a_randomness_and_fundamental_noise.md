# Research Prompt — Pass 1a: Randomness Primitives and Fundamental Noise Types

You are a technical research assistant specialising in procedural content generation for games.
This document will serve as a standalone implementation reference — the standard of completeness
is: a developer reading each section should be able to implement the technique without consulting
additional sources.

---

## Scope — cover only these techniques, in this order

1. Seeded PRNG and coordinate hashing
2. Weighted random selection and shuffle bags
3. Poisson disk sampling
4. Stratified and blue noise
5. Value noise
6. Perlin noise (classic and improved)
7. Simplex and OpenSimplex noise
8. Worley / Voronoi noise

Do NOT cover fBm, ridged multifractal, turbulence, domain warping, masks, SDFs,
terrain pipelines, biome systems, dungeon generation, WFC, or object placement.
Those are covered in Pass 1b and later passes.

---

## Per-technique content requirements

For each technique, the writeup must cover all five of the following areas.
Organize them as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What limitation or gap does this technique address? Why does it
exist as a distinct primitive rather than being subsumed by adjacent techniques?

**Algorithm at implementation depth.** Cover inputs, outputs, and the key steps a developer
would need to reproduce the technique. Include pseudocode or numbered steps wherever the
sequence of operations is non-obvious. For algorithms with a canonical primary source
(Perlin 1985/2002, Bridson 2007, Gustavson 2005, Eiserloh GDC 2017), verify the description
against that source before writing.

**Variants and how they differ.** Identify the main implementation variants (e.g., classic vs.
improved Perlin, Bridson vs. naive dart-throwing, OpenSimplex vs. OpenSimplex2). For each
variant, state concretely what changes algorithmically and what the practical consequence is —
not just that they differ.

**Failure modes and artifacts.** Name specific visual or numerical artifacts that arise from
incorrect or naive implementation. For each artifact, give the proximate cause and the fix.
If a failure mode has a documented name or origin (e.g., "Nebraska Problem", modulo bias),
use it.

**Downstream integration.** What data structure does the technique output (scalar field,
point set, distance field, etc.)? How do adjacent techniques in this pass consume that output?
What does the pairing look like in practice?

---

## Depth floor

Each technique writeup should be long enough to cover all five areas without padding, and
no shorter than that. Techniques with more documented variants or failure modes will naturally
be longer. Do not compress a technique to match the length of simpler ones.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim — not at the end
of paragraphs or sections. If a claim cannot be attributed to a consulted source, mark it
explicitly as "unconfirmed" rather than presenting it as established fact. Do not omit a gap:
flag it.

---

## Priority sources — consult these first for each technique

Squirrel Eiserloh, GDC 2017 "Noise-Based RNG" · Ken Perlin 1985 and 2002 papers ·
Robert Bridson SIGGRAPH 2007 · Stefan Gustavson "Simplex noise demystified" (2005) ·
Steven Worley SIGGRAPH 1996 · Catlike Coding noise series (Jasper Flick) ·
Inigo Quilez (iquilezles.org — noise and SDF articles) ·
Red Blob Games · The Book of Shaders (chapters 10–13) · OpenSimplex2 repository (KdotJPG)

Where a primary source and a tutorial conflict, prefer the primary source and note
the discrepancy.

---

## Closing sections (write these after all 8 technique writeups)

**Comparison — Perlin vs Simplex vs OpenSimplex vs Worley (2 paragraphs).**
Cover: what changes algorithmically, visual character, performance differences,
and when to choose each. Be concrete — avoid "it depends" without a criterion.

**Comparison — Random placement vs Poisson disk sampling (1–2 paragraphs).**
Cover: quality difference with a specific visual example, implementation cost,
and the conditions under which each is appropriate.

**Source landscape.**
Group the best references by type: primary paper, tutorial series, GDC talk, interactive tool,
repository. For each entry, one sentence on what it's uniquely valuable for that others
in the same category do not cover.
