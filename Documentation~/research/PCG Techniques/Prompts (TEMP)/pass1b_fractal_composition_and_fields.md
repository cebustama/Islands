# Research Prompt — Pass 1b: Fractal Composition and Scalar Field Operations

You are a technical research assistant specialising in procedural content generation for games.
This document will serve as a standalone implementation reference — the standard of completeness
is: a developer reading each section should be able to implement the technique without consulting
additional sources.

The reader already understands the techniques from Pass 1a (seeded PRNGs, coordinate hashing,
Poisson disk sampling, value noise, Perlin noise, simplex noise, Worley noise) — do not
re-explain those. Do explain how the techniques in this pass build on or consume them.

---

## Scope — cover only these techniques, in this order

1. Fractal Brownian motion (fBm)
2. Ridged multifractal noise
3. Turbulence
4. Domain warping
5. Layered masks and scalar field composition
6. Signed distance fields (PCG-relevant aspects only)

Do NOT cover seeded PRNGs, coordinate hashing, sampling methods, value noise, Perlin,
simplex, or Worley noise. Those are covered in Pass 1a.
Do NOT cover terrain pipelines, biome systems, dungeon generation, WFC, or object placement.

---

## Per-technique content requirements

For each technique, the writeup must cover all five of the following areas.
Organize them as flowing prose — do not use these as subsection headers.

**Problem and motivation.** What limitation or gap does this technique address? Why does it
exist as a distinct primitive rather than being subsumed by adjacent techniques?

**Algorithm at implementation depth.** Cover inputs, outputs, and the key steps a developer
would need to reproduce the technique. Include pseudocode or numbered steps wherever the
sequence of operations is non-obvious. For algorithms with a canonical primary source
(Quilez fBm and domain warping articles, Musgrave multifractal formulations, Quilez SDF
primitives), verify the description against that source before writing.

**Variants and how they differ.** Identify the main implementation variants (e.g., standard
fBm vs. heterogeneous fBm, single-warp vs. double-warp domain warping, smooth union vs.
hard union for SDFs). For each variant, state concretely what changes algorithmically and
what the practical consequence is — not just that they differ.

**Failure modes and artifacts.** Name specific visual or numerical artifacts that arise from
incorrect or naive implementation. For each artifact, give the proximate cause and the fix.
If a failure mode has a documented name or origin, use it.

**Downstream integration.** What data structure does the technique output (scalar field,
mask, distance field, etc.)? How do adjacent techniques in this pass and in later passes
(terrain, biomes, placement) consume that output? What does the pairing look like in practice?

---

## Depth floor

fBm, domain warping, and scalar field composition will naturally be longer given their
variant space and compositional role. SDFs may be shorter if coverage is limited to
PCG-relevant primitives and operations (masking, blending, shape generation) rather than
full raymarching applications. Do not compress or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete algorithmic claim — not at the end
of paragraphs or sections. If a claim cannot be attributed to a consulted source, mark it
explicitly as "unconfirmed" rather than presenting it as established fact. Do not omit a gap:
flag it.

---

## Priority sources — consult these first for each technique

Inigo Quilez (iquilezles.org — fBm, domain warping, SDF primitives, smooth minimum articles) ·
F. Kenton Musgrave (ridged multifractal and heterogeneous terrain — Texturing and Modeling,
3rd ed., Chapter 16) · The Book of Shaders (chapters 12–13) ·
Catlike Coding noise series (Jasper Flick) · Red Blob Games (terrain from noise) ·
Ken Perlin 1985 (original turbulence description)

Where a primary source and a tutorial conflict, prefer the primary source and note
the discrepancy.

---

## Closing sections (write these after all 6 technique writeups)

**Data structures produced by Passes 1a and 1b combined (1 paragraph per type).**
For each data structure these techniques output — scalar field, point set, gradient field,
distance field, weighted distribution table — describe: what it contains, typical storage
format in a game context, and which downstream passes (terrain, biomes, dungeons, placement)
consume it and how. Reference Pass 1a techniques by name where they are the producers.

**Source landscape.**
Group the best references for the techniques in this pass by type: primary paper, tutorial
series, GDC talk, interactive tool, repository. For each entry, one sentence on what it
is uniquely valuable for that others in the same category do not cover.
