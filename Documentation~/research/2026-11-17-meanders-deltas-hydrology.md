# Meanders, deltas and other water processes — sources and techniques (2026-11-17)

Research note, not authority. Origin: `/capturar` of three Instagram screenshots (@publicdomainrev) of Harold Fisk's 1944 meander maps. Claudio: «Me encantaría simular este proceso para Islands». Then: «profundiza en las fuentes y haz un análisis preliminar sobre las técnicas y algoritmos existentes que podríamos utilizar para un visualizer del fenómeno. ¿Esto serviría también para simular deltas? ¿Y otros fenómenos hídricos?»

Images: `referencias-visuales/nuevas/2026-11-17-fisk-meandros/`. Report text (extracted with `pdftotext -layout`, US government work, public domain): [`fisk-1944/fisk-1944-report.txt`](fisk-1944/fisk-1944-report.txt). Next step: web lab startup in `web/claudiobustamante-site/Documentation~/planning/active/Meanders_Lab_Startup.md`.

Legend: ✓ checked in the primary source (code or text read) · ~ checked in abstracts or secondary sources only · ? not checked.

## 1. The source: Fisk (1944)

- ✓ Harold N. Fisk, *Geological Investigation of the Alluvial Valley of the Lower Mississippi River*, Mississippi River Commission Publication 1 (War Department, Corps of Engineers), Vicksburg, 1 Dec 1944. Index and PDFs: https://biotech.law.lsu.edu/climate/mississippi/fisk/fisk.htm. The index notes that «some of the conclusions of the report are outdated».
- ✓ **Plate 22**, «Ancient courses within the Mississippi River meander belt», has **15 sheets** (Cape Girardeau to Donaldsonville). The screenshots are from it.
- ✓ **Data** (report p. 2): «approximately 16,000 borings, over 3,000 of which penetrated its entire depth», plus more than 300 project borings along four transvalley lines; near-complete aerial photo coverage (MRC and Department of Agriculture photos).
- ✓ **Stages:** 20 stages of ~100 years each (~2,000 years). Stages 17–20 are mapped historic courses of 1765, 1820, 1880 and 1940 (the text says 1940; the map legend reads 1944).
- ✓ **How he reconstructed them — the useful part:** «Reconstructions of stream courses were made by projecting the curvature of an isolated channel segment to its junction with another channel segment whose comparable age had been previously determined.» Curvature read from bar ridges and swales, ox-bow lakes and natural levees. Relative order from cross-cutting scars; years from «analogy to known migration since 1765». The report says the chronology «shows only relative values». **Fisk did by hand a curvature-based kinematic extrapolation**: nearly the mechanism of the model in §2. Good hook for an explainer.
- ✓ **Neck cutoffs** (p. 20): the loop is abandoned fast, the new channel migrates away, and the ox-bow lake receives only fine flood clays and «very slowly fills with blue mud» (clay plug). Rule for wetlands and soils.
- ~ **His chronology was revised:** Saucier (1994, *Geomorphology and Quaternary Geologic History of the Lower Mississippi Valley*, USACE WES) and a 1996 appraisal call Fisk's chronology largely invalid (wrong assumptions, no radiocarbon). Rittenour et al. later re-dated the channel belts with OSL. → Use Fisk as aesthetic and mechanism, not as data.
- ~ **Artistic precedent:** Robert Hodgin, *Meander* (2020, Houdini), procedural maps of rivers that never existed, directly inspired by Fisk; generates terrain, river, oxbows and an aged Corps-of-Engineers-style map. His rule: «slow rivers on flat plains meander, fast rivers on steep terrain do not». https://roberthodgin.com/project/meander

## 2. Meander techniques

| Technique | Gives | Cost | Verdict |
| --- | --- | --- | --- |
| A. Fixed curve (sine-generated / Kinoshita) | A pretty bend, no history | Minimal | Only for initial shapes |
| **B. Migrating centerline** (Howard & Knutson 1984) | Migration, cutoffs, oxbows, fossil courses | Low (hundreds of points) | **The one** |
| C. 2D morphodynamics (shallow water + sediment) | Real physics | Very high | Not for a lab |

**A.** ✓ Langbein & Leopold (1966), USGS Professional Paper 422-H, «River meanders — theory of minimum variance»: direction angle as a sine of channel distance; meander length / mean bend radius ≈ 4.7. ~ The «Kinoshita curve» (higher-order sine-generated curve) was named by Parker et al. (1983); Kinoshita's 1961 original not found.

**Theory behind B.** ✓ Ikeda, Parker & Sawai (1981), *J. Fluid Mech.* 112, 363–377, «Bend theory of river meanders. Part 1. Linear development» (bend instability). ✓ Parker, Sawai & Ikeda (1982), *JFM* 115, 303–314, Part 2 (nonlinear: skewing and fattening of bends). ~ Howard & Knutson (1984), *Water Resources Research* 20(11), 1659–1667, «Sufficient conditions for river meandering: a simulation approach».

**B in code — meanderpy** (Zoltán Sylvester, Apache-2.0, https://github.com/zsylvester/meanderpy; ~ Sylvester, Durkin & Covault 2019, *Geology* 47, 263–266, «High curvatures drive river meandering»: linear curvature–migration, 55 % of migration variance in seven rivers). ✓ Read in the source:

1. Resample the centerline at fixed spacing Δs (spline, no smoothing).
2. Curvature `(dx·ddy − dy·ddx)/(dx²+dy²)^1.5` (`np.gradient`), made dimensionless by multiplying by width W.
3. Nominal rate `R0 = kl · W·curv`.
4. Upstream convolution: `R1[i] = ω·R0[i] + γ·Σ(R0[i..0] · G)/ΣG`, `G = exp(−α·s)` (s = distance back along the channel), `α = 2·k·Cf/D`; ω = −1, γ = 2.5, k = 1. Then `R1 *= sinuosity^(−2/3)`.
5. Move each node along the normal: `x += R1·dy/ds·dt`, `y −= R1·dx/ds·dt`, capped at 0.5·Δs per step (CFL-like).
6. Neck cutoff: full pairwise distance matrix; ignore a band of `(crdist + 20·Δs)/Δs` nodes around the diagonal; cut where distance < `crdist`. The cut loop is stored as a `Cutoff` (coordinates, W, D) with its time in `cutoff_times`.
7. Save a centerline every `saved_ts` steps (`cl_times`). **Painting each saved centerline by age is Fisk's map.**
- README example: W = 200 m, D = 6 m, Δs = 50 m, `crdist` = 2W, Cf = 0.011, `kl` = 60 m/yr, dt = 0.1 yr, `saved_ts` = 20. Caveat: the README's positional call no longer matches `migrate()`'s signature; pass keywords.
- The pairwise matrix is O(n²); fine for a few thousand nodes, use a spatial hash beyond that.

**B for graphics — Paris, Guérin, Collon & Galin (2023)**, «Authoring and Simulating Meandering Rivers», *ACM TOG* 42(6), SIGGRAPH Asia, doi 10.1145/3618350. ~ Paper (not read: HAL and ACM blocked): vector-based, interactive, terrain and user constraints, cutoffs and avulsions, compared with real rivers by sinuosity and wavelength. ✓ Code (C++, MIT, no dependencies, https://github.com/aparis69/Meandering-rivers) — **its README says it is not the code that produced the paper's scenes**:
- Same equation and constants as meanderpy (cites Sylvester 2019), plus a terrain-slope falloff (no migration on steep ground = Hodgin's rule), the normal blended with user directions, and depth from width (Konsoer et al. 2013).
- Cutoff: a node within one channel width of a node ≥ 5 nodes further on.
- Missing: oxbows are not stored, no carving, no deposits; avulsion is manual and random (±35° random path, truncated at the first intersection).
- Use: compact C++ reference to port to C#, not a complete model.

## 3. Deltas — yes, with another algorithm

B moves **one** channel. A delta is many channels that split, deposit and jump.
- **Rule-based avulsion:** the channel aggrades until it sits above the floodplain; past a threshold it jumps to the steepest path. Repeated, it gives switching lobes, alluvial fans and simple deltas. Cheap, and it is the «abrupt event» of Paris 2023. ~ Cellular fan models report that shorter avulsion periods give rounder fans, and that attraction to abandoned channels gives the abrupt fan edge (ESurf 2022, https://esurf.copernicus.org/articles/10/555/2022/). The Mississippi itself is the bridge case: successive delta lobes and the threatened capture by the Atchafalaya (McPhee, *The Control of Nature*, 1989).
- **DeltaRCM** ~ (Liang, Voller & Paola 2015, *Earth Surface Dynamics* 3, 67–86; Python port pyDeltaRCM, MIT, ✓ licence, Moodie et al. 2021, JOSS): water and sediment parcels random-walk the grid weighted by depth and direction and deposit where flow slows. Channels, bifurcations and lobe switching **emerge**. Grid-based like Islands → the right choice for the delta.

## 4. Other water processes

| Process | Technique | Fit with Islands |
| --- | --- | --- |
| Valleys cut by rivers; relief that ages | Stream power law solved with **FastScape** ~ (Braun & Willett 2013, *Geomorphology* 180–181, 170–179; implicit, O(n)) | **Best fit**: needs exactly the D8 receivers and flow accumulation Phase L already computes. Same as proposal 4 of [`2026-11-13-pcg-games-proposals.md`](2026-11-13-pcg-games-proposals.md) («erosion with an age») |
| Braided rivers | ~ Murray & Paola (1994), *Nature* 371, 54–57, cellular model | Simple grid; validation mixed (poor match to a lab river in 2005, good pattern statistics in 1998) |
| Floods, lakes filling, ephemeral channels | Virtual pipes ~ (Mei, Decaudin & Hu 2007, PG '07) + erosion/deposition (Šťava et al. 2008) | Burst, like the v6 wind |
| Nicer river networks than D8 | ~ Graph grown inland from the coast (Génevaux et al. 2013, *ACM TOG* 32(4)); Peytavie et al. 2019 «Procedural Riverscapes» | Future alternative to Phase L's raster |
| Wetlands and floodplain | Derived: oxbows + fossil courses + low ground beside the channel (Fisk p. 20, clay plugs) | Feeds Q2 `Wetland` (`../planning/active/PCG_Roadmap.md`) and the card *Why rivers shouldn't look like this* (`playlist-pcg/cards-tier3.md`) |

Already in NoiseLab v6: Stommel currents and pressure-projection wind; the eikonal shore-wave capture (2026-11-02, `2026-11-capturas.md`).

## 5. Visualizer proposal (draft)

1. **Web first, JS** (Islands Lab), because the centerline is small, «Fisk mode» looks good fast and the explainer recipe exists. Modes: Fisk (migration, age slider, colour by age, «cut the neck», oxbows layer), valley (river from a D8 path on a NoiseLab map; meanders only where slope is low), delta (DeltaRCM-lite at the mouth).
2. **Then NoiseLab C# / Burst**: rasterize the centerline to `Rivers` / `Lakes` / wetland; FastScape before Phase L.
3. **Scale problem:** on a 256² island a meander is invisible. It needs a large plain, the cell zoom (mundo-islas) or a lab of its own. Decide before any pipeline work.

## Open

- The Paris 2023 paper itself (equations, metrics) — try the HAL PDF from a browser.
- DeltaRCM's routing and deposition rules — read pyDeltaRCM before the delta mode.
- Kinoshita (1961) original.
