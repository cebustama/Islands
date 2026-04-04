Research Prompt — Pass 1: Noise, Scalar Fields, and Randomness Foundations
You are a technical research assistant specialising in procedural content generation for games. This is a foundational reference — cover only the techniques listed. Do not cover terrain pipelines, biome systems, dungeon generation, WFC, or object placement; those are separate passes.
Cover these techniques only:
Seeded PRNG and coordinate hashing · Weighted random selection and shuffle bags · Poisson disk sampling · Stratified and blue noise · Value noise · Perlin noise (classic and improved) · Simplex and OpenSimplex noise · Worley / Voronoi noise · Fractal Brownian motion (fBm) · Ridged multifractal noise · Turbulence · Domain warping · Layered masks and scalar field composition · Signed distance fields (where relevant to PCG)
For each technique, write 3–5 paragraphs covering:

What problem it solves and why it exists
The core algorithm at an implementation level — inputs, outputs, key steps
Key variants and how they differ (e.g. classic vs. improved Perlin, Bridson vs. naive Poisson disk)
Common failure modes, visual artifacts, and how to avoid them
What it pairs well with and how downstream systems consume its output

After the technique writeups, include two short comparison sections (1–2 paragraphs each):

Perlin vs Simplex vs OpenSimplex vs Worley — algorithmic differences, visual character, performance, when to choose each
Random placement vs Poisson disk sampling — quality differences, implementation cost, when each is appropriate

Close with a brief data structures section describing what data structures these techniques produce (scalar fields, point sets, masks, distance fields, etc.) and how downstream passes consume them. Then a short source landscape grouping the best references by type (tutorial, paper, talk, repo).
Sources to prioritise: Catlike Coding (Jasper Flick, noise series), Inigo Quilez (noise and SDF articles), Red Blob Games, Sebastian Lague, Squirrel Eiserloh (GDC noise-based RNG), Ken Perlin's 1985 and 2002 papers, Robert Bridson's Poisson disk paper, Stefan Gustavson's "Simplex noise demystified". Cite sources inline throughout.
Style: Technical and concrete. Use pseudocode or numbered steps where it aids clarity. Distinguish confirmed algorithmic facts from tutorial simplifications. Flag genuine gaps rather than filling them with guesses. Depth over breadth.