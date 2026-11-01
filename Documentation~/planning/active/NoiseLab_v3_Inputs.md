# NoiseLab v3 — inputs from the «PCG» playlist analysis

- **Status:** input for planning, not a plan. Written 2026-11-01 from the playlist analysis while NoiseLab v2 was being finished in a parallel session. When v3 starts, read this together with the v2 close-out and `../archive/NoiseLab_v2_Startup.md` (which already defers "real biomes with temperature and moisture" to v3). Claudio decides scope; nothing here is roadmapped.
- **Sources:** [`../../research/playlist-pcg/`](../../research/playlist-pcg/) (`cards.md`, `cards-tier2.md`, `cards-tier3.md`, `crossref.md`, `backlog.md`). Private wrapped: https://claude.ai/artifact/2ENwKc2kpHJmcVvpCNfexa.
- **Why NoiseLab:** several backlog items are best understood visually before they become pipeline stages. A NoiseLab mode is a cheap prototype, a teaching scene and a check that the idea is worth a stage. The rule from v1 still holds: CPU, deterministic, same seed ⇒ same image.

## Candidate modes, ranked

| # | Mode | What the user sees and touches | Feeds which Islands item | Effort | Source cards |
| --- | --- | --- | --- | --- | --- |
| 1 | **Biomes with wind and rain shadow** | Temperature and moisture fields over the flooded map, then a wind direction (or latitude band: trades E→W, westerlies W→E). Windward slopes get wetter, leeward ones dry. Toggle wind off to see today's symmetric moisture. | Backlog Tier 3: wind + orographic moisture in `Stage_Biome2D` (largest realism gap). v2 already deferred biomes to v3 | M | Atlas Pro, TED-Ed (Tier 3) |
| 2 | **Chained splines («move the dots»)** | Two or three noises (continentalness, erosion, peaks-valleys), each with a draggable spline, combined into height. Shows why a single monotone spline is invisible under percentile water and bands, and chaining is not. | Backlog n.º 2 (`ScalarSpline`, `Stage_BaseTerrain2D`) | M | Kniberg ×2 (Tier 1) |
| 3 | **CA lab on the flooded mask** | Run birth/survive steps on the land mask; rule selector (Hofmann's growth rule vs. the classic smoothing rule, Life as a curiosity); step counter; keep-largest-region toggle. | Backlog n.º 3. Note: needs a Moore (8) count, `MaskNeighborOps2D` is 4-only; keep-largest via `MaskTopologyOps2D.LabelConnectedComponents4` | S | Hofmann, Waite, Wolverson, Galak-Z |
| 4 | **Coast roughness, live** | Box-counting dimension of the current coastline, with the log–log plot and the scale range used. Reference marks: Britain ≈ 1.21, Norway ≈ 1.52. | Backlog: coast roughness metric in `Inspection/` | S | 3Blue1Brown (Tier 2, verified) |
| 5 | **Reaction-diffusion panel** | Gray-Scott with a spots ↔ stripes ↔ labyrinth slider; optional run inside the land mask only. | Backlog Tier 3: reaction-diffusion field in `Runtime/Noise` (dryland vegetation) | M | Be Smart (Tier 3) |
| 6 | **Sea-level and lakes** | A sea-level slider (hilltops become islands) plus the Priority-Flood fill depth shown as lakes in wet climate and salt pans in dry. | Backlog Tier 3: export `FillDepth` | S | Budget Museum, Un Mundo Inmenso, Algol |
| 7 | **Distance map overlay** | Tap a point: BFS distance across the walkable land, farthest point, chokepoints. | Backlog n.º 1 (validators) | S | Wolverson, Isaac |
| 8 | **Voronoi plates** | Plates from Voronoi cells with motion arrows; convergent boundaries raise ridges, divergent ones open rifts, before any height noise. | Backlog Tier 3: `Stage_Tectonics2D` (phase-sized; prototype first) | M–L | Algol, Fantasy Forge |

Suggested v3 core if scope must stay small: **1 + 4 + 3** (biomes are already promised for v3; roughness and CA are small and testable). 2 and 6 are the next layer. 5, 7 and 8 can wait for v4 or the web.

## Constraints and notes

- Keep every mode deterministic and seed-driven. CA and reaction-diffusion use a fixed step count and double buffering; never `UnityEngine.Random`.
- Box counting on a 256² map is only meaningful for box sizes of roughly 2–32 cells; show the fitted range, or measure on a larger render.
- Wind/orographic in NoiseLab is a prototype of the Biome2D rule, not the rule itself. If it works, port it to `Stage_Biome2D` as a governed change (biome goldens will move).
- Quick win outside NoiseLab: `moistureModulation` in `Stage_Vegetation2D.cs:105` is 0 by default; turning it on in a preset gives forests near water.
- Web counterparts (Islands Lab / Labs): «Why one side of the island is green», «How rough is your coastline?», Turing patterns. The same prototype can feed both.
