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

- **n.º 3 (CA operator) gains weight:** four independent sources now (Hofmann's tutorial is the clearest walkthrough). Seeded, double-buffered, fixed step count; needs a new Moore (8-neighbour) count (`MaskNeighborOps2D` is 4-only) and keep-largest via `MaskTopologyOps2D.LabelConnectedComponents4`. Expose the rule (birth/survive sets), since Hofmann's growth rule and Lague's smoothing rule give different caves. Do not copy the tutorial's unseeded `Random`.
- **n.º 1 (validators):** consume `Walkable` from `Stage_Traversal2D`, which already exists.
- **New, S:** **coast roughness metric** by box-counting, in `Inspection/`. Turns the exploratory «fractal coastline / fjord refinement» item in `CURRENT_STATE.md` into a measurable target (golden-testable number per seed). Rank it right after n.º 3. Full re-read: report the slope only over the scale range where it is stable; on 256² maps that is roughly 2–32 cells, so measure on a larger render or several map sizes.
- **New, S–M:** **corner-based (dual-grid) tile selection** in the tilemap adapter, alongside the existing RuleTile slot. Adapter-side, golden-neutral for the core.
- **New, S:** regional hilliness by multiplying two noises, as a `Stage_Hills2D` tunable (check first whether a mask noise already does this).
- **n.º 8 (settlement) gains design rules:** Pixel Architect's 14 urbanism rules (full re-read, 1:50–3:05 and later): widest main route through the square linking activity nodes and map exits; no dead ends except at a point of interest; shops along streets; no blank façades or oversized plazas; tall objects must not hide the player (raise the ground, lower objects in between, set buildings into the slope); 2–3 storeys; organic edges. Opinion, not a tested algorithm, but usable as scoring terms.
- **Housekeeping, not code:** credit Sunny Valley Studio in the four dungeon layout files (lineage confirmed via the archive snapshot of the legacy project) (relevant to the open-source tangent in `_comun/foco.md`, which already lists CatLikeCoding credits).

Route elsewhere:
- **Web (Labs):** logistic-map bifurcation page linked to the Mandelbrot scene (best fit for the emergence scene); CA playground with a rule slider (Rule 30/110 → Life); Euler vs RK4 light bending (kavan). Collatz coral for Flora Lab: low-medium.
- **Web Fauna Lab:** Blackthornprod's follow-the-leader chain (SmoothDamp per segment + sine wiggle) for tails and tentacles — complements `web/.../research/2026-10-31-animacion-procedural-explainer.md`.
- **NoiseLab (v3 candidates):** «CA lab» mode on the flooded mask; coast roughness number shown live.

---

## Tier 3 update (2026-11-01)

Group C adds the first **climate and hydrology realism** candidates. Ranked additions:

- **New, quick win (XS):** turn on `moistureModulation` in a preset and measure (forests near water). The code exists; only a tunable and a golden check.
- **New, S:** **export `FillDepth`** from Priority-Flood (lakes in wet climate, salt pans/ephemeral lakes in arid, oases in wadis). Fixes the missing natural lakes; contract change in Hydrology outputs.
- **New, M, high value:** **prevailing wind + orographic moisture + rain shadow** in `Stage_Biome2D` (wind from latitude band or a tunable; windward gain, leeward loss). Biggest realism gap: today every island is wet all round. Validator: windward mean moisture ≥ leeward per ridge. Good NoiseLab / Islands Lab demo («why one side of the island is green»).
- **New, S–M:** floodplain mask + `BiomeType.Wetland`; river width by flow + sinuosity metric (Hydrology sub-stage, since L-9 forbids touching the river mask).
- **New, S:** continentality and coast-ratio metrics in `MapStatsExporter2D` (pairs with coast roughness from Tier 2).
- **New, M:** desert surface sub-classifier (needs `FillDepth` first).
- **New, M:** reaction-diffusion field in `Runtime/Noise` (dryland spotted/banded vegetation, run per region). Also NoiseLab v3 / web emergence material.
- **New, L (design first):** `Stage_Tectonics2D` from Voronoi plates (uplift field before base terrain; fjords and rift islands follow). Phase-sized.
- Low: island archetype presets (atoll, sea stack) as test shapes; strata material layer; natural borders (mundo-islas).

Suggested order now: moistureModulation → validators (n.º 1) → CA (n.º 3) → FillDepth → wind/orographic → chained splines (n.º 2). Nothing here is roadmapped; Claudio decides.

Route elsewhere: web Labs (snowflake / Nakaya explainer; Turing patterns with the fire-and-firefighters framing; strata time slider); videojuego-colonizacion (drained marsh rivers, forest that makes its own rain, windward/leeward peoples, atoll for the sea people, Dejima as a trade enclave); cuadernos (Wampís Nation, Runit Dome, Atacama as the Chilean desert parallel).

## Tier 2 re-read (2026-11-01)

Tier 2 was re-read with full transcripts (26 of 33 usable). No value changed and no candidate moved. The corrections above (CA needs a Moore count and a configurable rule; coast roughness over a limited scale range; 14 Pixel Architect rules; Sunny Valley lineage via the legacy project) are folded into the bullets. New small notes: picoCAD's dithered shading toward darker palette shades and Grant Abbitt's third tree method (rings of linked branches, the araucaria's structure) for the web Flora Lab and sprite tools.

## Captures (outside the playlist)

Pending ideas delivered with `/capturar`. Same rule: nothing here is roadmapped.

- **2026-11-02 · Wave-exposure field (eikonal, fast sweeping), M.** From `shoreMap.js` in Ultra Water: travel time from the coast with c = √(g·depth), then wave direction, shoaling and exposure per coast cell. Feeds beaches / coves / cliffs and settlement siting; a continuous-metric sibling of n.º 1 (`BfsDistanceField`). Not the "ocean waves" rejected above: it is a static generation field, not runtime simulation. Open question: the same sweep for a wind field that bends with terrain (generalises NoiseLab v3's single-direction wind; needs anisotropic speed). Card: [`../2026-11-capturas.md`](../2026-11-capturas.md).
