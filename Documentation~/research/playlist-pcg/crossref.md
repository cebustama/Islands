# Playlist «PCG» Tier 1 — technique × video × Islands status

Cross-reference of [`cards.md`](cards.md). Status re-checked by the orchestrating agent with Grep over `Runtime/**/*.cs` on 2026-11-01 (`HEAD` 9230081 plus uncommitted NoiseLab work in the tree). "Missing" means no matching symbol was found, not a full audit. Research material, not authority.

## Gaps confirmed in code (nothing found in `Runtime/`)

| Technique | Library ID | Videos | Grep that found nothing |
| --- | --- | --- | --- |
| Dijkstra / distance-map analysis for gameplay (cull unreachable, farthest point, hot path, chokepoints) | NEW (builds on existing BFS) | Wolverson, Isaac, UnitOfTime | `AStar\|A\*\|Dijkstra`, `shortest\|spanning\|pathfind` |
| Cellular automata caves / rooms | D.12 | Wolverson, Galak-Z | `CellularAutomat\|CaveCA\|BirthLimit` |
| Delaunay + MST + loop edges (TinyKeep rooms) | NEW | Gun Game | `Delaunay`, `MinimumSpanning\|Kruskal\|Prim` |
| A* over rooms or landmarks | NEW | Betts, Wolverson | as above |
| Weighted / nested population tables | NEW | Qud, Betts | `PopulationTable\|WeightedTable\|WeightedPick` |
| Wave Function Collapse (finite, stitched, infinite) | D.16 | Watt, marian42 | `WaveFunction\|Wfc\|WFC` |
| Diffusion-limited aggregation | NEW | Wolverson | `DiffusionLimited\|DLA` |
| Hilbert / Z-order path layouts | NEW | Galak-Z | `Hilbert` |
| Marching squares + Douglas-Peucker contours | D.19 + NEW | Noita | `Marching`, `Douglas\|Peucker` |
| Herringbone Wang tiles | NEW | Noita | `Wang\|Herringbone` |

## Existing pieces the videos build on (found in code)

| Piece | File | Extension the videos suggest |
| --- | --- | --- |
| `ScalarSpline`, one `heightRemapSpline` | `Runtime/PCG/Fields/ScalarSpline.cs`, `Layout/Maps/MapTunables2D.cs` | Kniberg: several noises (continentalness, erosion, peaks-valleys) each with its own spline, chained into height |
| `BfsDistanceField` | used in `Layout/Maps/Stages/Stage_Morphology2D.cs`, `Operators/MaskMorphologyOps2D.cs` | Wolverson/Isaac: reuse as a Dijkstra map for validators and role assignment; Qud: seed plots from a distance map |
| `BiomeTable` (4×4 temperature × moisture, "upgradable to 6×6") | `Layout/Maps/BiomeTable.cs` | Kniberg: third quantised axis and variant flag; Betts: noise-threshold blending of two biome sets |
| `Stage_Vegetation2D` | `Layout/Maps/...` (referenced from `BiomeTable.cs`) | Synthetic Silviculture: optional succession layer (low priority) |
| Random walk (`SimpleRandomWalk2D`, `IteratedRandomWalk2D`) | `Layout/` | Nuclear Throne: several walkers with turn/spawn/die odds |
| BSP, room-first, corridor-first, room grid | `Layout/` | Isaac/UnitOfTime role post-process applies to their room graphs too |
| `GraphLibrary` (directed/undirected graphs, no algorithms) | `Runtime/Graphs/GraphLibrary/` | Host for MST / A* if added; it is not promoted authority |

## By destination

- **Islands package (generation):** distance-map validators, CA operator, chained splines, room-graph family, population tables, biome axis/blend, walker variants, DLA, Hilbert layout.
- **NoiseLab / web Islands Lab (teaching):** chained splines («move the dots»), Dijkstra maps, CA caves, noise-threshold biome blend, floating origin (why big worlds shimmer).
- **mundo-islas (gameplay, not the package):** RimWorld elastic failure and storyteller, Tarn Adams' villain agent chains with information gating, Nystrom's type objects and energy turn loop, Nature of Order (Alexander's properties), XCOM parcels for hand-built pieces.
- **videojuego-colonizacion (design):** Kate Edwards' consistency checklist, Qud's history → culture → architecture chain for villages.

## Determinism notes from the talks

- Stitched WFC (Watt): sectors solved in parallel with per-sector seeds, then a stitching pass. Compatible with seed-derived sector hashes (`SmallXXHash`).
- Infinite WFC (marian42): the result depends on where the player walked — **order-dependent**, so it clashes with "same inputs + seed ⇒ same output". Only usable for finite, whole-from-seed content.
- UnitOfTime compaction: always-valid layout (undo invalid moves), deterministic given the seed and iteration order.
- Qud: rebuilds a zone from save + seed for debugging, a pattern Islands already follows with goldens.

---

## Tier 2 additions (2026-11-01, from [`cards-tier2.md`](cards-tier2.md))

| Technique | Library ID | Videos | Islands status (checked) |
| --- | --- | --- | --- |
| Moore birth/survive CA mask operator | D.12 | Hofmann (Tilemap CA tutorial), Waite (Life), Wolverson, Galak-Z | missing — now four sources; only `MaskMorphologyOps2D` / `Stage_Morphology2D` exist |
| Box-counting fractal dimension of the coast mask | NEW (metric) | 3Blue1Brown, «Los fractales no suelen ser autosemejantes» | missing; natural home `Runtime/PCG/Inspection/` (`MapStatsExporter2D`). Reference values quoted in the video: Britain ≈ 1.21, Norway ≈ 1.52 |
| Corner-based (dual-grid) tile selection from a (W+1)×(H+1) sample | D.18 variant | CodingQuest | **partial, corrected:** the agent called it a gap, but `TilesetConfig.cs` has a Phase H6 `ruleTile` slot (Unity RuleTile neighbour matching). Corner/dual-grid selection itself is missing; see also `../2026-10-27-referencias-desde-marcadores.md` (dual grid) |
| Multiply two noises to vary hilliness by region | D.7 variant | Sam Hogan (Minecraft in 24 h) | not checked in detail; candidate tunable in `Stage_Hills2D` |
| Elementary CA (Rule 30, Rule 110) | NEW | Waite | missing; teaching only (NoiseLab «CA lab», web emergence scene) |
| Stochastic Allen-Cahn lattice (domain formation under noise) | NEW | Berglund | missing; exploratory |
| Escape-time iteration (Mandelbrot/Julia) | NEW | Numberphile, 3Blue1Brown | missing in Islands; already published on the web (`labs/scenes/mandelbrot`, Julia). Geologically artificial for terrain, as `CURRENT_STATE.md` already notes |
| Random-walk / corridor-first / room-first BSP dungeons | D.13, D.14 | Sunny Valley P2–P4 | **exists** (`Layout/SimpleRandomWalk2D.cs`, `IteratedRandomWalk2D.cs`, `CorridorFirstDungeon2D.cs`, `RoomFirstBspDungeon2D.cs`), very likely ported from this series and extended (seeded, `MaskGrid2D`, brush, skew). **The sources do not credit Sunny Valley Studio** |

Other findings: `Stage_Traversal2D` already writes a `Walkable` mask (Land or ShallowWater, minus HillsL2) — the natural input for the Tier 1 distance-map validators (backlog n.º 1). Logistic map and Rule 30 as random sources: not recommended (SmallXXHash is better and avoids float differences across platforms).

---

## Tier 3 additions (2026-11-01, from [`cards-tier3.md`](cards-tier3.md))

Context checked: rivers are Priority-Flood+ε → D8 → accumulation threshold, 1 cell wide, not post-processed (contract L-9); every depression is filled, so hydrology makes no natural lakes. Moisture = coastal decay + noise + river bonus; temperature = base − lapse·height − latitude + coast moderation + noise. Full pipeline order (`PCGMapVisualization`, stagesLM2b): BaseTerrain → Hills → Shore → Traversal → Morphology → Hydrology → Biome → Vegetation → Regions.

| Technique / rule | Library ID | Videos | Islands status (checked) |
| --- | --- | --- | --- |
| Prevailing wind by latitude band + orographic gain + rain shadow | NEW | Atlas Pro, TED-Ed (flying rivers), BE AMAZED (cap cloud) | **missing** — no wind term in `Stage_Biome2D`; moisture is radially symmetric around the coast. `latitudeEffect` exists (default 0) |
| Wind-advected moisture with forest recycling | NEW | TED-Ed | missing |
| Export Priority-Flood fill depth (`FillDepth`) → natural lakes, salt pans, oases | NEW | Budget Museum (Sahara), Algol (Messinian basin) | **partial** — `FillDepressions` in `Operators/HeightFieldHydrologyOps2D.cs` computes it and throws it away |
| River width by flow, sinuosity metric, meander displacement | NEW | Capture VR | missing (rivers 1 cell) |
| Floodplain mask (distance to river × low slope) + Wetland biome | NEW | It's Complicated | missing; `BiomeType` (12 types) has no wetland |
| Desert surface sub-types (erg, hamada, reg, wadi, oasis, salt pan, fog desert) | NEW | Budget Museum | missing; terrace quantisation exists but is not climate-driven |
| Plates from Voronoi: convergent uplift, divergent rifts, rifted-fragment islands | NEW | Algol, Fantasy Forge | missing; Voronoi F1/F2/`CellAsIslands` and `Ridged` noise exist (`Runtime/Noise`) |
| Fjord operator (cold + steep + coast) | NEW | Fantasy Forge, Atlas Pro | missing (also the exploratory item in `CURRENT_STATE.md`) |
| Reaction-diffusion (Gray-Scott / Gierer-Meinhardt) field, run per region | NEW | Be Smart | missing; candidate for `Runtime/Noise` and dryland vegetation |
| Moisture-driven vegetation | — | Fantasy Forge | **exists, inert**: `moistureModulation = 0.0f` (`Stage_Vegetation2D.cs:105`) |
| Continentality metric (interior dryness per landmass), shelf band, global ΔT | NEW | El Robot de Platón | missing; home `MapStatsExporter2D` / `MapTunables2D` |
| Island archetypes (atoll ring + lagoon, sea stack, sand crescent, nested caldera) | NEW | BE AMAZED | missing; `IslandShapeMode` has Ellipse/Rectangle/NoShape/Custom |
| Tilted strata / unconformity as a cliff material layer | NEW | Tom Scott | missing; visual only |
| Natural political borders (rivers and ridges as walls) | NEW | Fantasy Forge | missing; `Stage_Regions2D` partitions by biome only |

Routed elsewhere: Nakaya snowflake diagram and Turing patterns (web Labs explainers, emergence); Dejima, Wampís Nation, Runit Dome, Victorian drainage, cleared land around towns (videojuego-colonizacion, cuadernos).
