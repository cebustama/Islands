# Research Prompt — Pass 2b: Rivers, Caves, and Volumetric Terrain

You are a technical research assistant specialising in procedural content generation for games.
This document will serve as a standalone implementation reference — the standard of completeness
is: a developer reading each section should be able to implement the technique without consulting
additional sources.

The reader already understands noise functions (Perlin, fBm, ridged multifractal, domain warping),
scalar field composition, and the heightmap construction and erosion techniques from Pass 2a
(heightmap generation, island masks, mountain shaping, cliffs and terraces, hydraulic erosion,
thermal erosion) — do not re-explain those. Do explain how the techniques in this pass build on
or consume a heightmap produced by those stages.

---

## Scope — cover only these techniques, in this order

1. River and drainage systems
2. Cave generation (cellular automata, noise thresholding, agent tunnelling)
3. Voxel terrain
4. Marching cubes / dual contouring / surface nets

Do NOT cover heightmap generation, island masks, mountain shaping, cliffs, terraces,
hydraulic erosion, or thermal erosion. Those are covered in Pass 2a.
Do NOT cover biome systems, dungeon generation, WFC, or object placement.

---

## Per-technique content requirements

For each technique, write 3–5 paragraphs covering all of the following areas.
Organize as flowing prose — do not use these as subsection headers.

**What it does and why it's used.** What specific problem does this technique solve that
simpler approaches do not? What does the output look like and how does it fit into a
terrain pipeline?

**Algorithm at implementation depth.** Inputs, outputs, and the key steps a developer needs
to reproduce it. Use pseudocode or numbered steps wherever the sequence of operations is
non-obvious. Prefer concrete parameter ranges over vague descriptions.

**Variants and tradeoffs.** Name the main implementation variants and state what concretely
changes between them and what the practical consequence is — not just that they differ.
For cave generation in particular, cover cellular automata, noise thresholding, and agent
tunnelling as distinct variants rather than separate techniques.

**Failure modes.** Name the specific visual or numerical artifact, its proximate cause, and
the fix. If a failure mode has a recognized name, use it.

**Combinations.** What does this technique receive from upstream stages (including the
heightmap and erosion outputs from Pass 2a) and what does it produce for downstream systems
such as biome assignment, placement, and rendering?

---

## Depth floor

Marching cubes / dual contouring / surface nets warrants a longer entry given the algorithmic
differences between variants and the practical tradeoffs at mesh extraction. River and drainage
systems may be shorter if the available implementation-level documentation is thin — flag this
honestly rather than filling gaps. Do not compress or pad to match length.

---

## Citation behavior

Cite the specific source immediately after each concrete claim about an algorithm, parameter,
or implementation decision. If a claim cannot be attributed to a consulted source, mark it
explicitly as "unconfirmed" before stating it. Flag gaps rather than filling them.

---

## Priority sources

Sebastian Lague (Procedural Landmass series, hydraulic erosion video) ·
Red Blob Games (mapgen4 — river and drainage simulation) ·
RogueBasin (cellular automata caves) ·
Paul Bourke (marching cubes) ·
Eric Lengyel (Transvoxel / dual contouring) ·
Catlike Coding

Where a primary source and a tutorial conflict, prefer the primary source and note
the discrepancy.

---

## Closing sections (write these after all 4 technique writeups)

**Comparison — heightmap terrain vs voxel terrain (1–2 paragraphs).**
What each representation can and cannot produce, and the concrete conditions under which
each is appropriate. Be specific — avoid "it depends" without a following criterion.

**Comparison — cellular automata caves vs noise caves vs agent tunnels (1–2 paragraphs).**
How outputs differ visually and structurally, what control each approach gives the designer,
and the conditions under which each is appropriate.

**Comparison — marching cubes vs dual contouring vs surface nets (1–2 paragraphs).**
What changes algorithmically between each, the quality and performance tradeoffs,
and when each is the right choice.

**End-to-end terrain pipeline (numbered stage list + 1 paragraph).**
The full typical generation order from raw noise through to a renderable terrain with
rivers and caves — covering both Pass 2a and Pass 2b stages together. Include what data
flows between stages and 3–5 common developer mistakes that occur across the full pipeline.

**Source landscape.**
Group the best references for the techniques in this pass by type: primary paper, tutorial
series, repository, interactive tool. For each entry, one sentence on what it is uniquely
valuable for that others in the same category do not cover.
