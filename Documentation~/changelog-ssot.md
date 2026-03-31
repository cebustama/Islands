# Changelog — SSoT

## 2026-03-23
- Created the governed `Documentation~/` spine for Islands in parallel to the frozen legacy snapshot.
- Promoted initial subsystem authority seeds for:
  - `systems/pcg-core-ssot.md`
  - `systems/map-pipeline-by-layers-ssot.md`
- Created governed planning docs:
  - `planning/active/PCG_Roadmap.md`
  - `planning/active/Islands_Governance_Migration_Roadmap.md`
- Recorded the core Batch 2 authority decision:
  - active PCG truth = new grid-first pipeline
  - implemented Map Pipeline by Layers truth = F0–F2
  - F3–F6 remain planning only
  - legacy tilemap map-generation material is not active authority by default

## 2026-03-30
- Closed Batch 3 (legacy map-generation triage).
- Classified the old tilemap-based `Map_Generation_SSoT_v0.1.2_2026-01-29.md` as legacy conceptual reference / historical support, not active Islands runtime authority.
- Added `reference/legacy-map-generation-reference.md` as the governed destination for that role.
- Classified `Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` as absorbed planning history.
- Planned `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` as the governed historical destination for that planning source.
- Advanced the governance roadmap so Batch 4 became the immediate next migration batch.
- Closed Batch 4 (layout strategies verification and staging).
- Classified layout strategies as an implemented, test-gated support surface under PCG, not a separate subsystem SSoT.
- Added `reference/pcg-layout-strategies-reference.md` as the governed destination for deep per-strategy behavior.
- Reclassified `Islands_PCG_Layout_Strategies_SSoT_v0_1_2.md` from pending to historical-support source material for the new governed reference path.
- Advanced the governance roadmap so Batch 5 became the immediate next migration batch.
- Closed Batch 5 (GraphLibrary authority decision).
- Classified GraphLibrary as a real runtime support surface but **not** a promoted subsystem authority.
- Normalized `reference/graphs.md` as the governed reference-facing GraphLibrary surface.
- Reclassified GraphLibrary technical deep support into governed documentation rather than live runtime-local law.
- Recorded that no `systems/graphs-ssot.md` is justified yet.
- Marked `Runtime/Graphs/GraphLibrary/DirectedGraphExample.cs` as stale historical support, not canonical usage truth.
- Advanced the governance roadmap so Batch 6 became the immediate next migration batch.

## 2026-03-31
- Closed Batch 6 (Noise / Mesh / Surfaces / Shaders hardening).
- Recorded that Noise has a real runtime boundary but should remain governed reference / staged support rather than a promoted subsystem SSoT.
- Recorded that Meshes have a real runtime boundary but should remain governed reference / staged support rather than a promoted subsystem SSoT.
- Recorded that Surfaces have real runtime jobs but remain mixed with sample orchestration, so they should remain governed reference / staged support rather than a promoted subsystem SSoT.
- Recorded that Shaders are active support artifacts and governed reference material, not a promoted subsystem SSoT.
- Added normalized governed reference homes:
  - `reference/overview.md`
  - `reference/noise.md`
  - `reference/mesh.md`
  - `reference/surfaces.md`
  - `reference/shaders.md`
- Expanded the Batch 6 absorption path for `Documentation-snapshot/Documentation~/wip/Islands_PCG_Pipeline_SSoT_v0_1_16.md` so reusable deltas can land in governed reference docs as well as subsystem docs.
- Advanced the governance roadmap so Batch 7 became the immediate next migration batch.
- Closed Batch 7 (repo-wide normalization and traceability hardening for the reviewed evidence set).
- Landed `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` as a real governed archive destination.
- Applied explicit status headers to the main snapshot and legacy authority-risk files reviewed in Batch 7.
- Normalized cross-links across the governed spine to the correct governed homes.
- Materialized the declared governed folder scaffold for `planning/archive/`, `archive/`, `research/`, and `governance/`.
- Normalized GraphLibrary historical technical support to `reference/GraphLibrary_Pipeline_Technical_Doc.md` as the retained documentation copy.

- Closed final archive / research curation for the reviewed snapshot corpus.
- Archived the governance migration roadmap to `planning/archive/Islands_Governance_Migration_Roadmap.md` and removed it from `planning/active/`.
- Added governed archive copies for the main historical planning and technical-support documents that still matter for traceability.
- Added `archive/snapshot-curation-register.md` as the explicit record of snapshot coverage and disposition.
- Kept `research/` intentionally sparse because no reviewed snapshot file qualified as active exploratory research.
- Confirmed the next normal work surface is `planning/active/PCG_Roadmap.md` (with `F3` as the next planned implementation slice).
