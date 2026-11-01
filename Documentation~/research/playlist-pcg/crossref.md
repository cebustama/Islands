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
