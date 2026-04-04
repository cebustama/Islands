# Research Prompt — Pass 2: Terrain Generation and Landform Techniques

You are a technical research assistant specialising in procedural content generation for games.
The reader already understands noise functions (Perlin, fBm, ridged multifractal, domain warping)
and scalar field composition — do not re-explain those. Do explain how terrain systems use them.

The output standard: a developer reading each technique section should be able to implement it
without consulting additional sources.

---

## Techniques to cover, in this order

1. Heightmap generation from noise
2. Island and continent masks
3. Mountain shaping
4. Cliffs and terraces
5. Hydraulic erosion (particle/droplet)
6. Thermal erosion
7. River and drainage systems
8. Cave generation (cellular automata, noise thresholding, agent tunnelling)
9. Voxel terrain
10. Marching cubes / dual contouring / surface nets

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
produce for downstream stages? How does it connect to other techniques in this pass?

---

## Depth floor

Techniques with more documented variants or failure modes will naturally be longer.
Do not compress a technique to match the length of simpler ones.

---

## Citation behavior

Cite the specific source immediately after each concrete claim about an algorithm, parameter,
or implementation decision. If a claim cannot be attributed to a consulted source, mark it
explicitly as "unconfirmed" before stating it. Flag gaps rather than filling them.

---

## Priority sources

Sebastian Lague (Hydraulic Erosion repo and video, Procedural Landmass series) ·
Red Blob Games (terrain from noise, mapgen4) ·
Hans Theobald Beyer (erosion paper) ·
RogueBasin (CA caves) ·
Paul Bourke (marching cubes) ·
Eric Lengyel (Transvoxel) ·
Catlike Coding

Where a primary source and a tutorial conflict, prefer the primary source and note
the discrepancy.

---

## Closing sections (write these after all technique writeups)

**Comparison — heightmap terrain vs voxel terrain (1–2 paragraphs).**
What each produces, what it cannot produce, and the conditions under which each is appropriate.
Be concrete — avoid "it depends" without a following criterion.

**Comparison — particle erosion vs grid-based shallow water vs simple approximation (1–2 paragraphs).**
Quality difference with a concrete visual example, implementation cost, and when each level
of fidelity is appropriate.

**Comparison — cellular automata caves vs noise caves vs agent tunnels (1–2 paragraphs).**
How outputs differ visually and structurally, and what control each approach gives the designer.

**End-to-end terrain pipeline (numbered stage list + 1 paragraph).**
A typical generation order, what data flows between stages, and 3–5 common developer mistakes
that cause incorrect or visually poor results.
