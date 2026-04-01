# SSoT Index

Status: Active  
Purpose: Explicit index of current documentary authority inside Islands.

## Authority order
1. Subsystem files in `systems/`
2. `SSoT_CONTRACTS.md` for cross-cutting package contracts
3. `coverage-matrix.md` for ownership lookup
4. `CURRENT_STATE.md` for operational present tense only
5. `planning/active/` for future work only
6. `reference/` for governed support and reference surfaces
7. `planning/archive/`, `archive/`, `research/`, and snapshot material for historical or investigative support only

## Current promoted subsystem authorities
- `systems/pcg-core-ssot.md`
- `systems/map-pipeline-by-layers-ssot.md` (implemented slice currently F0–F3)

## Current staged support surfaces not promoted to subsystem authority
- GraphLibrary
- layout strategies as a separate authority surface
- Noise as a separate subsystem SSoT
- Meshes as a separate subsystem SSoT
- Surfaces as a separate subsystem SSoT
- shader layer as a separate subsystem SSoT

## Current active planning docs
- `planning/active/PCG_Roadmap.md`

## Current closed / archived planning docs
- `planning/archive/Islands_Governance_Migration_Roadmap.md`
- `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md`
- `planning/archive/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md`
- `planning/archive/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md`

## Current governed reference docs called out explicitly
- `reference/overview.md`
- `reference/noise.md`
- `reference/mesh.md`
- `reference/surfaces.md`
- `reference/shaders.md`
- `reference/graphs.md`
- `reference/legacy-map-generation-reference.md`
- `reference/pcg-layout-strategies-reference.md`

## Current governed historical-support docs called out explicitly
- `reference/GraphLibrary_Pipeline_Technical_Doc.md`
- `planning/archive/Islands_Governance_Migration_Roadmap.md`
- `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md`
- `planning/archive/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md`
- `planning/archive/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md`
- `archive/PCG_Pipeline_Technical_Snapshot.md`
- `archive/Islands_PCG_Pipeline_SSoT_v0_1_16.md`
- `archive/Islands_SSoT_Technical_Bible.md`
- `archive/snapshot-curation-register.md`

## Current GraphLibrary role
- **Implemented truth:** `Runtime/Graphs/GraphLibrary/*.cs`
- **Governed reference-facing home:** `reference/graphs.md`
- **Historical technical support retained in documentation:** `reference/GraphLibrary_Pipeline_Technical_Doc.md`
- **Promotion status:** not promoted to subsystem SSoT after Batch 5

## Current Batch 6 role resolution
- **Noise**
  - implemented truth: `Runtime/Noise/**`
  - governed home: `reference/noise.md`
  - promotion status: not promoted to subsystem SSoT after Batch 6
- **Meshes**
  - implemented truth: `Runtime/Meshes/**`
  - governed home: `reference/mesh.md`
  - promotion status: not promoted to subsystem SSoT after Batch 6
- **Surfaces**
  - implemented truth: `Runtime/Surfaces/*.cs`
  - sample orchestration: `Samples~/0.1.0-preview/ProceduralSurface.cs`
  - governed home: `reference/surfaces.md`
  - promotion status: not promoted to subsystem SSoT after Batch 6
- **Shaders**
  - implemented support artifacts: `Runtime/Shaders/**`
  - governed home: `reference/shaders.md`
  - promotion status: not promoted to subsystem SSoT after Batch 6

## Current governance spine docs
- `SSoT_CONTRACTS.md`
- `coverage-matrix.md`
- `CURRENT_STATE.md`
- `changelog-ssot.md`
- `migration-log.md`
- `supersession-map.md`

## Short local update loop
1. Identify the concept that changed.
2. Find its primary home in `coverage-matrix.md`.
3. Update the primary home first.
4. Update `CURRENT_STATE.md` if active status or next focus changed.
5. Update `changelog-ssot.md` if semantics or authority changed.
6. Update `supersession-map.md` if a document was replaced or absorbed.
7. Update `migration-log.md` if this was part of an ongoing salvage pass.
