# Snapshot Curation Register

Status: Active archive governance support  
Purpose: Record the disposition of every file in the reviewed snapshot corpus after migration closeout.

## Summary
- Reviewed snapshot file count: 13
- Reviewed snapshot files left unanalyzed: 0
- Snapshot files with a governed successor or retained archive copy: 13/13
- Research promotions from the reviewed snapshot corpus: 0

## Disposition ledger

| Snapshot file | Analyzed | Governed home / retained destination | Notes |
|---|---|---|---|
| `Documentation-snapshot/Documentation~/overview.md` | Yes | `reference/overview.md` | Superseded by governed reference doc; no separate archive copy needed. |
| `Documentation-snapshot/Documentation~/noise.md` | Yes | `reference/noise.md` | Superseded by governed reference doc; no separate archive copy needed. |
| `Documentation-snapshot/Documentation~/mesh.md` | Yes | `reference/mesh.md` | Superseded by governed reference doc; no separate archive copy needed. |
| `Documentation-snapshot/Documentation~/surfaces.md` | Yes | `reference/surfaces.md` | Superseded by governed reference doc; no separate archive copy needed. |
| `Documentation-snapshot/Documentation~/shaders.md` | Yes | `reference/shaders.md` | Superseded by governed reference doc; no separate archive copy needed. |
| `Documentation-snapshot/Documentation~/subsystems/PCG_Pipeline_Technical_Snapshot.md` | Yes | `archive/PCG_Pipeline_Technical_Snapshot.md` | Retained as governed historical technical support. |
| `Documentation-snapshot/Documentation~/wip/Islands_PCG_Layout_Strategies_SSoT_v0_1_2.md` | Yes | `reference/pcg-layout-strategies-reference.md` | De-authorized and replaced by governed deep reference. |
| `Documentation-snapshot/Documentation~/wip/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` | Yes | `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` + `planning/active/PCG_Roadmap.md` | Absorbed into active roadmap and retained as planning history. |
| `Documentation-snapshot/Documentation~/wip/Islands_PCG_Pipeline_SSoT_v0_1_16.md` | Yes | `systems/pcg-core-ssot.md`, `systems/map-pipeline-by-layers-ssot.md`, `reference/overview.md`, `reference/noise.md`, `reference/mesh.md`, `reference/shaders.md`, and `archive/Islands_PCG_Pipeline_SSoT_v0_1_16.md` | Split source; no longer a single active authority surface. |
| `Documentation-snapshot/Documentation~/wip/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md` | Yes | `planning/archive/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md` + `planning/active/PCG_Roadmap.md` | Superseded planning source retained for traceability. |
| `Documentation-snapshot/Documentation~/wip/Map_Generation_SSoT_v0.1.2_2026-01-29.md` | Yes | `reference/legacy-map-generation-reference.md` | Classified as legacy conceptual reference; frozen source remains only in snapshot. |
| `Documentation-snapshot/Documentation~/wip/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md` | Yes | `planning/archive/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md` + `planning/active/PCG_Roadmap.md` | Absorbed planning source retained in governed planning archive. |
| `Documentation-snapshot/Documentation~/wip/archive/Islands_SSoT_Technical_Bible.md` | Yes | `archive/Islands_SSoT_Technical_Bible.md` | Retained as long-range historical technical support. |

## Files not promoted as active governed docs
These files were reviewed and intentionally **not** promoted as active authority surfaces:
- `Documentation-snapshot/Documentation~/subsystems/PCG_Pipeline_Technical_Snapshot.md`
- `Documentation-snapshot/Documentation~/wip/Islands_PCG_Pipeline_SSoT_v0_1_16.md`
- `Documentation-snapshot/Documentation~/wip/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md`
- `Documentation-snapshot/Documentation~/wip/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md`
- `Documentation-snapshot/Documentation~/wip/archive/Islands_SSoT_Technical_Bible.md`

They still remain valuable, but their correct role is historical support, planning history, or split-source traceability rather than live law.

## Snapshot handling rule after closeout
- Treat `Documentation-snapshot/` as frozen source history.
- Do not continue adding migration annotations there unless a future salvage pass explicitly requires it.
- Prefer updating the governed spine, `supersession-map.md`, and this register instead of editing the frozen snapshot corpus again.
