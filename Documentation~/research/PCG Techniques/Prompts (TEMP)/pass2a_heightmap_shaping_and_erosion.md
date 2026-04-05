# Research Prompt — Pass 2a: Heightmap Construction, Shaping, and Erosion

You are a technical research assistant specialising in procedural content generation for games.
This document will serve as a standalone implementation reference — the standard of completeness
is: a developer reading each section should be able to implement the technique without consulting
additional sources.

The reader already understands noise functions (Perlin, fBm, ridged multifractal, domain warping)
and scalar field composition — do not re-explain those. Do explain how terrain systems use them.

---

## Scope — cover only these techniques, in this order

1. Heightmap generation from noise
2. Island and continent masks
3. Mountain shaping
4. Cliffs and terraces
5. Hydraulic erosion (particle/droplet)
6. Thermal erosion

Do NOT cover rivers and drainage systems, cave generation, voxel terrain, marching cubes,
or any mesh extraction technique. Those are covered in Pass 2b.
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

**Failure modes.** Name the specific visual or numerical artifact, its proximate cause, and
the fix. If a failure mode has a recognized name, use it.

**Combinations.** What does this technique receive from upstream stages and what does it
produce for downstream stages? How does it connect to other techniques in this pass and to
the river, cave, and voxel techniques in Pass 2b?

---

## Depth floor

Hydraulic erosion has significantly more documented variant space than the shaping techniques —
it will naturally be longer. Do not compress it to match simpler entries. Cliffs and terraces
may be shorter if the available implementation-level documentation is thin — flag this honestly.

---

## Citation behavior

Cite the specific source immediately after each concrete claim about an algorithm, parameter,
or implementation decision. If a claim cannot be attributed to a consulted source, mark it
explicitly as "unconfirmed" before stating it. Flag gaps rather than filling them.

---

## Priority sources

Sebastian Lague (Hydraulic Erosion repo and video, Procedural Landmass series) ·
Hans Theobald Beyer (erosion paper) ·
Red Blob Games (terrain from noise, mapgen4) ·
Catlike Coding

Where a primary source and a tutorial conflict, prefer the primary source and note
the discrepancy.

---

## Closing sections (write these after all 6 technique writeups)

**Comparison — particle erosion vs grid-based shallow water vs simple approximation (1–2 paragraphs).**
Quality difference with a concrete visual example, implementation cost, and the conditions
under which each level of fidelity is appropriate. Note that full river simulation is covered
in Pass 2b — this comparison focuses on erosion fidelity tradeoffs, not drainage networks.

**Heightmap pipeline so far (numbered stage list + 1 short paragraph).**
The typical stage order for the techniques in this pass — from raw noise to an eroded
heightmap — what data flows between stages, and 3–5 common developer mistakes that occur
in this portion of the pipeline. The full end-to-end pipeline including rivers, caves, and
mesh extraction is covered at the close of Pass 2b.

**Source landscape.**
Group the best references for the techniques in this pass by type: primary paper, tutorial
series, repository, interactive tool. For each entry, one sentence on what it is uniquely
valuable for that others in the same category do not cover.
