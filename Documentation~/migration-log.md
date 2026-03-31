# Migration Log

## 2026-03-23 — Initial governed scaffold + Batch 2 rescue landing

### Context
The migration began from a mixed legacy documentation tree with hidden authority concentrated in legacy `wip/` material.

### Decisions recorded
- Use `Documentation~/` as the governed docs root.
- Keep the full old docs tree as a frozen snapshot instead of rewriting it in place.
- Promote the new PCG direction now, but only where runtime evidence is already strong.
- Treat the implemented Map Pipeline by Layers slice as F0–F2 only.
- Keep F3+ in governed planning, not subsystem authority.

### Files created
- governance spine docs
- initial subsystem authorities for PCG
- active planning docs for PCG and the overall migration

### Remaining migration pressure
- legacy map-generation triage
- layout strategies verification
- subsystem hardening for noise / meshes / surfaces / graphs

## 2026-03-30 — Batch 3 legacy map-generation triage closed

### Context
Batch 3 existed to resolve whether the old tilemap-based island map-generation documents still described active package truth or only legacy / reference material.

### Evidence and decision
- The legacy `Map_Generation_SSoT` describes a distinct MonoBehaviour + Tilemaps + `GenerationData` / `GenerationStep` style system.
- That system is not part of the Islands package runtime.
- Therefore it does not require promotion, runtime verification inside Islands, or competing authority status.

### Result
- Legacy map generation is now classified as conceptual / architectural reference only.
- The early `MapPipelineByLayers` roadmap is treated as absorbed planning history.
- The active authority path remains the governed spine created in Batch 2.

### Next migration pressure
- layout strategies verification and staging
- GraphLibrary authority decision
- subsystem hardening for noise / meshes / surfaces / graphs

## 2026-03-30 — Batch 4 layout strategies verification and staging closed

### Context
Batch 4 existed to decide whether the layout-strategies family had earned its own subsystem SSoT or should remain staged support under PCG.

### Evidence and decision
- The layout strategies surface is implemented and test-gated.
- The strategy document contains deep per-strategy behavior, contracts, gates, and legacy parity notes.
- However, the promoted PCG system docs still provide the real spine for shared contracts and subsystem authority.
- Therefore the correct role is governed deep reference / staged subsystem support, not separate subsystem authority.

### Result
- Added `reference/pcg-layout-strategies-reference.md` as the governed destination for strategy internals.
- Reclassified the old layout-strategies SSoT file as historical-support source material for the new reference.
- Confirmed that the next unresolved authority boundary is GraphLibrary.

### Next migration pressure
- GraphLibrary authority decision
- subsystem hardening for noise / meshes / surfaces / graphs

## 2026-03-30 — Batch 5 GraphLibrary authority decision closed

### Context
Batch 5 existed to decide whether GraphLibrary should remain reference / support, remain historical technical support, or be promoted toward a subsystem authority surface.

### Inputs reviewed
- `Documentation~/reference/graphs.md`
- the then-runtime-local `Runtime/Graphs/GraphLibrary/GraphLibrary_Pipeline_Technical_Doc.md`
- `Runtime/Graphs/GraphLibrary/AbstractGraph.cs`
- `Runtime/Graphs/GraphLibrary/AbstractNode.cs`
- `Runtime/Graphs/GraphLibrary/DirectedGraph.cs`
- `Runtime/Graphs/GraphLibrary/UndirectedGraph.cs`
- `Runtime/Graphs/GraphLibrary/IGraph.cs`
- `Runtime/Graphs/GraphLibrary/PairValueImplementation.cs`
- `Runtime/Graphs/GraphLibrary/DirectedGraphExample.cs`

### Evidence and decision
- GraphLibrary has a real runtime boundary and a coherent enough code surface to count as implemented support infrastructure.
- However, it remains too mixed / local / thin for subsystem promotion.
- `Documentation~/reference/graphs.md` is useful as a governed reference-facing summary, but it is not subsystem authority.
- The old runtime-local `GraphLibrary_Pipeline_Technical_Doc.md` mixed active explanation with historical rescue / integration notes and should not act as live law.
- `DirectedGraphExample.cs` is stale and should not be treated as canonical usage truth.

### Result
- GraphLibrary remains staged support / governed reference.
- `Documentation~/reference/graphs.md` is the correct governed home for the reusable explanatory layer.
- Historical technical support is retained in governed documentation as `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md`.
- No `systems/graphs-ssot.md` is justified yet.

### Next migration pressure
- Noise / Mesh / Surfaces / Shaders hardening
- final normalization of remaining public / reference surfaces

## 2026-03-31 — Batch 6 Noise / Mesh / Surfaces / Shaders hardening closed

### Context
Batch 6 existed to decide whether Noise, Meshes, Surfaces, and Shaders had earned promotion into new subsystem SSoTs or whether they should remain governed reference / support surfaces.

### Inputs reviewed
- `Documentation-snapshot/Documentation~/overview.md`
- `Documentation-snapshot/Documentation~/noise.md`
- `Documentation-snapshot/Documentation~/mesh.md`
- `Documentation-snapshot/Documentation~/surfaces.md`
- `Documentation-snapshot/Documentation~/shaders.md`
- `Documentation-snapshot/Documentation~/subsystems/PCG_Pipeline_Technical_Snapshot.md`
- `Documentation-snapshot/Documentation~/wip/Islands_PCG_Pipeline_SSoT_v0_1_16.md`
- `Documentation-snapshot/Documentation~/wip/archive/Islands_SSoT_Technical_Bible.md`
- `Runtime/Noise/**`
- `Runtime/Meshes/**`
- `Runtime/Surfaces/*.cs`
- `Runtime/Shaders/**`
- `Samples~/0.1.0-preview/ProceduralSurface.cs`
- the existing governed spine under `Documentation~/`

### Evidence and decision
- Noise has a real runtime boundary and coherent contracts, but it is still better treated as governed reference / staged support than as a promoted subsystem SSoT.
- Meshes have a real runtime boundary and clear enough contracts to deserve a governed home, but not a separate promoted subsystem SSoT.
- Surfaces have a real job-level runtime surface, but their broader narrative remained mixed with sample orchestration.
- Shaders are support artifacts and governed reference material, not the center of a promoted subsystem authority surface.

### Result
- Added normalized governed reference homes for overview, noise, mesh, surfaces, and shaders.
- Kept subsystem promotion limited to PCG core and Map Pipeline by Layers.
- Preserved the snapshot-era public/module docs as historical-support source material instead of treating them as live law.

### Next migration pressure
- repo-wide cross-reference repair
- source-file header application
- archive / research curation

## 2026-03-31 — Batch 7 normalization and traceability hardening closed

### Context
Batch 7 existed to finish repo-wide normalization after the major authority decisions were already closed.

### Inputs reviewed
- governed spine docs under `Documentation~/`
- governed reference docs under `Documentation~/reference/`
- the frozen snapshot corpus under `Documentation-snapshot/Documentation~/`
- `Map_Generation_SSoT_v0.1.2_2026-01-29.md`
- `Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md`

### Evidence and decision
- The remaining risk was traceability and stale navigation, not missing subsystem promotions.
- Several spine docs still referenced legacy homes imprecisely or mentioned destinations that had not been landed yet.
- The missing legacy map-generation and early Map Layers roadmap sources were enough to finish the archive/header pass without reopening any earlier promotion decision.
- GraphLibrary historical support belonged in the documentation tree, not in a runtime-local markdown file.

### Result
- Normalized the main governed cross-links and navigation wording across the spine.
- Applied explicit status headers to the main snapshot and legacy authority-risk files.
- Landed `Documentation~/planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` as an explicit governed archive destination.
- Materialized the previously declared governed folders `planning/archive/`, `archive/`, `research/`, and `governance/`.
- Moved GraphLibrary technical historical support to `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md` as the only documentation copy intended to remain.

### Remaining migration pressure
- broader archive / research curation outside the reviewed set
- any residual non-core markdown that still overclaims authority by naming or location


## 2026-03-31 — Final archive / research curation and migration closeout

### Context
After Batch 7, the remaining work was no longer authority triage but bounded curation: deciding what historical material should be promoted into governed archive form, what should remain only in the frozen snapshot, and whether research required any landed documents.

### Evidence and decision
- The reviewed snapshot corpus was small enough to classify file-by-file.
- Several historical planning and technical-support documents were still useful enough to warrant governed archive copies.
- No reviewed snapshot document qualified as active research material; the correct outcome for `research/` was to keep it intentionally sparse.
- The migration roadmap itself no longer belonged in `planning/active/` once the curation pass closed.

### Result
- Archived the closed migration roadmap to `planning/archive/Islands_Governance_Migration_Roadmap.md`.
- Added governed archive copies for:
  - `planning/archive/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md`
  - `planning/archive/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md`
  - `archive/PCG_Pipeline_Technical_Snapshot.md`
  - `archive/Islands_PCG_Pipeline_SSoT_v0_1_16.md`
  - `archive/Islands_SSoT_Technical_Bible.md`
- Added `archive/snapshot-curation-register.md` as the explicit ledger of reviewed snapshot coverage and disposition.
- Closed the migration as a materially finished governance pass for the reviewed corpus.

### Next normal work
- Resume package development via `planning/active/PCG_Roadmap.md`.
- Treat future documentation work as ordinary maintenance tied to implementation changes, not as an open migration batch series.
