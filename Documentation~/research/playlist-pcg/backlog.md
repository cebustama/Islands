# Playlist «PCG» Tier 1 — ranked candidate backlog

Proposal from the orchestrating agent, 2026-11-01. **Nothing here is roadmapped**: Claudio decides what enters `PCG_Roadmap.md`. Effort is a rough guess (S ≈ one batch, M ≈ 2–3, L ≈ a phase). Sources in [`cards.md`](cards.md), status in [`crossref.md`](crossref.md).

| # | Candidate | Source videos | Effort | Value | Fits | Why this rank |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | **Distance-map validators**: cull unreachable regions, farthest point, hot path, chokepoints, role tags (start, boss = farthest leaf, dead ends = rewards) | Wolverson, Isaac, UnitOfTime, Galak-Z | S–M | high | operators on `MaskGrid2D`, reusing `BfsDistanceField` | Small, deterministic, golden-friendly; serves both dungeons and the island map (where is the far coast, which passes are chokepoints) |
| 2 | **Chained spline terrain shaping** (continentalness / erosion / peaks-valleys, each noise with its own spline) | Kniberg ×2 | M | high | `Stage_BaseTerrain2D`, `ScalarSpline` | Designer control over cliffs, plateaus and sea depth with code that already half exists; best NoiseLab v2 / Islands Lab demo («move the dots») |
| 3 | **Cellular automata operator** (birth/survival thresholds, iterations) | Wolverson, Galak-Z | S | medium-high | `Operators/` | D.12 is in the library but has no code; tiny and classic |
| 4 | **Weighted, nested population tables** (pick one / pick each / chance, fall-through faction → region → default) | Qud, Betts | S | medium-high | new data type, Unity-free | Foundation for any placement (props, vegetation variants, settlements); deterministic with the existing hash |
| 5 | **Room-graph dungeon family** (scatter + separate + Delaunay + MST + loop edges + L-hallways) | Gun Game (TinyKeep), UnitOfTime | M–L | medium | `Layout/`, `GraphLibrary` | The main missing dungeon family; needs Delaunay and MST, which Islands lacks |
| 6 | **Biome table: third axis and variants**, plus noise-threshold blending of two biome sets | Kniberg, Betts | M | medium | `BiomeTable`, Phase M | Table is documented as upgradable; blending is a small visual win |
| 7 | **Walker and growth variants**: multi-walker drunkard, DLA, Hilbert-window path | Nuclear Throne, Wolverson, Galak-Z | S each | medium-low | `Layout/` | Cheap extras on existing walkers; good teaching pages |
| 8 | **Settlement stage design** (Qud plots by distance seeding + largest inner rectangle; XCOM plot/parcel size buckets, facing, zones) | Qud, XCOM 2 | L | medium (later high) | after `Stage_Regions2D`; design doc first | Islands has no village layer; the game needs it, but it is a new phase |
| 9 | **Stitched WFC per sector** | Watt (+ marian42 as warning) | L | medium | future tile-kit work | Only worth it once there is a kit of pieces per people (Stålberg note); order-independent if stitched |
| 10 | Contour extraction (marching squares + Douglas-Peucker) | Noita | S–M | low | adapters | Useful for vector outlines of islands in the web; not core |
| — | Not recommended: floating origin, sand simulation, ocean waves, vegetation succession | SimonDev, Noita/Jackson, TMP ×2 | — | low | — | Solve problems Islands does not have, or runtime-only |

## Suggested order if Claudio wants a mini-track

1 → 3 → 2: validators and CA are small and testable; splines then feed NoiseLab v2 and an Islands Lab page. 4 can go in whenever placement work starts. 5, 8 and 9 are phase-sized and should wait for a design decision.

## Route elsewhere (not Islands)

- mundo-islas: RimWorld (elastic failure, storyteller), Tarn Adams (villain chains, information gating), Nystrom (type objects, energy turns), Nature of Order.
- videojuego-colonizacion: Kate Edwards' checklist; Qud's history → culture → architecture chain.

---

## Tier 2 update (2026-11-01)

Tier 2 adds no phase-sized candidates. Changes to the table above:

- **n.º 3 (CA operator) gains weight:** four independent sources now (Hofmann's tutorial is the clearest walkthrough). Seeded, double-buffered, fixed step count, largest-region keep via the existing flood fill. Do not copy the tutorial's unseeded `Random`.
- **n.º 1 (validators):** consume `Walkable` from `Stage_Traversal2D`, which already exists.
- **New, S:** **coast roughness metric** by box-counting, in `Inspection/`. Turns the exploratory «fractal coastline / fjord refinement» item in `CURRENT_STATE.md` into a measurable target (golden-testable number per seed). Rank it right after n.º 3.
- **New, S–M:** **corner-based (dual-grid) tile selection** in the tilemap adapter, alongside the existing RuleTile slot. Adapter-side, golden-neutral for the core.
- **New, S:** regional hilliness by multiplying two noises, as a `Stage_Hills2D` tunable (check first whether a mask noise already does this).
- **n.º 8 (settlement) gains design rules:** Pixel Architect's urbanism rules (shops face streets, plazas enclosed by buildings, no blank façades, no tall objects hiding the player). Opinion, not a tested algorithm.
- **Housekeeping, not code:** credit Sunny Valley Studio in the four dungeon layout files (relevant to the open-source tangent in `_comun/foco.md`, which already lists CatLikeCoding credits).

Route elsewhere:
- **Web (Labs):** logistic-map bifurcation page linked to the Mandelbrot scene (best fit for the emergence scene); CA playground with a rule slider (Rule 30/110 → Life); Euler vs RK4 light bending (kavan). Collatz coral for Flora Lab: low-medium.
- **Web Fauna Lab:** Blackthornprod's follow-the-leader chain (SmoothDamp per segment + sine wiggle) for tails and tentacles — complements `web/.../research/2026-10-31-animacion-procedural-explainer.md`.
- **NoiseLab (v3 candidates):** «CA lab» mode on the flooded mask; coast roughness number shown live.
