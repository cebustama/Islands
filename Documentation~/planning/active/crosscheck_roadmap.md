# PCG Technique Cross-Check Roadmap

Status: Active tracker
Date: 2026-04-06
Home: `Documentation~/research/crosscheck_roadmap.md`

Tracks which technique families from the technique_integration_matrix.md have received
the deep game × technique cross-check treatment (same pattern as noise_cross_reference.md).

---

## Cross-check status

| # | Technique Family | Matrix Section | Status | Output File | Date |
|---|---|---|---|---|---|
| 1 | Noise primitives + composition | §1, §2 | **DONE** | `noise_cross_reference.md` | Pre-2026-04-06 |
| 2 | Terrain shaping | §3 | **DONE** | `terrain_biome_cross_reference.md` §1 | 2026-04-06 |
| 3 | Biome / region systems | §4, §6 (partial) | **DONE** | `terrain_biome_cross_reference.md` §2 | 2026-04-06 |
| 4 | Hydrology | §5 | **DONE** | `hydrology_cross_reference.md` | 2026-04-06 |
| 5 | Placement & traversal | §7 | Pending | — | — |
| 6 | Pipeline infrastructure | §8 | Not planned | — | — |

## Gap research tracker

| Gap | Status | Output File |
|---|---|---|
| Biome blending implementations | **RESOLVED** | `5a_gap_biome_blending_implementations.md` |
| Rainshadow on 2D grids | **RESOLVED** | `5a_gap_rainshadow_2d_grid.md` |
| DF erosion details | **RESOLVED** | `DwarfFortress_Worldgen_gap_erosion.md` |
| RimWorld biome boundaries | **RESOLVED** | `RimWorld_Worldgen_gap_biome_boundaries.md` |
| DF connected component analysis | Open (low priority) | — |
| NMS terrain archetypes | Open (low priority) | — |
| DF river classification criteria | Open (low priority) | — |
| DF lake growth algorithm | Open (low priority) | — |

## Batch updates applied to technique_integration_matrix.md

| Date | Source | Changes |
|---|---|---|
| Pre-2026-04-06 | `noise_cross_reference.md` | N1/N2/N3 roadmap items derived from noise cross-check. |
| 2026-04-06 | `terrain_biome_cross_reference.md` + 4 gap files | +5 new rows, ~7 row updates, 3 new exclusions. Rainshadow promoted to Tier 1. Coverage 44→47. |
| 2026-04-06 | `hydrology_cross_reference.md` | +3 new rows (agent-based carving, pathfinding placement, Strahler ordering), annotations on 5 existing rows. Coverage 47→50. |

## Next cross-check rationale

**Placement & Traversal is next** (if the analysis workstream continues). It feeds Phase N
(POI placement) and Phase O (paths), which are further out on the roadmap than the phases
served by the first four cross-checks.

**Pipeline Infrastructure** is architectural rather than technique-based and does not
benefit from game × technique cross-checking.

**Note:** The three cross-checks most relevant to the active pipeline (noise, terrain/biome,
hydrology) are now complete. Placement & Traversal is lower urgency.
