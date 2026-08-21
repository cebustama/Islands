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
- `systems/map-pipeline-by-layers-ssot.md` (implemented slice: see `CURRENT_STATE.md` for the
  exact chain — the index owns authority order, not implementation state)

## Current staged support surfaces not promoted to subsystem authority
- GraphLibrary
- layout strategies as a separate authority surface
- Noise as a separate subsystem SSoT
- Meshes as a separate subsystem SSoT
- Surfaces as a separate subsystem SSoT
- shader layer as a separate subsystem SSoT

## Current active planning docs

Verified against the package directory listing, 2026-08-21.

Roadmap and phase design documents (`planning/active/`):
- `planning/active/PCG_Roadmap.md`
- `planning/active/Phase_L_Design.md`
- `planning/active/Phase_M_Design.md`
- `planning/active/Phase_M2_Design.md`
- `planning/active/Phase_Q_Design.md`
- `planning/active/Phase_T1_Design.md`
- `planning/active/Phase_V_Design.md`
- `planning/active/Phase_W_Design.md`
- `planning/active/crosscheck_roadmap.md`

Live doc-update queues (unapplied governed content):
- `Phase_T2_Pending_Doc_Updates.md` (partially applied 2026-08-21 — §1.4 and §3 blocked on
  `Phase_T2_Design.md` not existing). **The only live queue.** Not yet committed under
  `planning/active/`.

Exploration, not planning authority:
- `planning/exploration/Shape_Composition_Future_Exploration.md`

Note on the queues: the six W-aux.c…W-aux.f / X1.a queues were applied 2026-08-20;
`W_b_…`, `W-aux_h_…`, `Phase_Q_…` and `Phase_W_…` were disposed 2026-08-21 — Q applied in
full once open decision §9.1 was resolved, W closed with its §3 declared irrecoverable. All
are archived. `Phase_T2_…` alone stays live, because §1.4 and §3 carry content that exists
nowhere else and both wait on a design document that has not been written.

**Path correction, 2026-08-21.** Consumed queues live under `archive/pending doc updates/`,
**not** under `planning/archive/` as earlier revisions of this index stated. The archived
entries below were corrected against the directory listing.

**Registration gap, recorded 2026-08-21.** `W_b_…`, `Phase_T2_…` and `W-aux_h_…` all existed
and were live without ever appearing in this list, and none of the three is on disk in the
package. A pending queue is unapplied governed content: if it is neither committed nor
listed, it is invisible to the next session. New queues are to be registered here when
**written**, not when consumed, and committed under `planning/active/` at the same time.

## Current closed / archived planning docs

Verified against the package directory listing, 2026-08-21.

Roadmaps and reports (`planning/archive/`):
- `planning/archive/Islands_Governance_Migration_Roadmap.md`
- `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md`
- `planning/archive/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md`
- `planning/archive/Noise_Composition_Improvements_Roadmap.md`
- `planning/archive/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md`

Consumed doc-update queues (`archive/pending doc updates/`):
- `archive/pending doc updates/W-aux_c_Pending_Doc_Updates.md` (applied 2026-08-20)
- `archive/pending doc updates/W-aux_d_Vegetation_Quantile_Pending_Doc_Updates.md` (applied 2026-08-20)
- `archive/pending doc updates/W-aux_e_Threshold_Mapping_Audit_Pending_Doc_Updates.md` (applied 2026-08-20)
- `archive/pending doc updates/W-aux_f_Height_Ceiling_Desaturation_Pending_Doc_Updates.md` (applied 2026-08-20)
- `archive/pending doc updates/X1_Pending_Doc_Updates.md` (applied 2026-08-20)
- `archive/pending doc updates/Phase_Q_Pending_Doc_Updates.md` — **applied in full 2026-08-21**
  once open decision §9.1 was resolved; §3.1 landed in `Phase_Q_Design.md`
- `archive/pending doc updates/Phase_W_Pending_Doc_Updates.md` — **closed 2026-08-21**; §3
  (W.a golden) disposed as irrecoverable rather than applied, with a closure note in
  `changelog-ssot.md`
- `W_b_Pending_Doc_Updates.md` — applied in full 2026-08-21 (15 edits across `PCG_Roadmap.md`,
  `map-pipeline-by-layers-ssot.md`, `SSoT_CONTRACTS.md`, `CURRENT_STATE.md`,
  `changelog-ssot.md`). Not yet committed to the archive folder.
- `W-aux_h_Pending_Doc_Updates.md` — applied in full 2026-08-21 (11 edits across
  `PCG_Roadmap.md`, `CURRENT_STATE.md`, `changelog-ssot.md`), after the W.b queue per its own
  ordering dependency. Not yet committed to the archive folder.

**Listed here in earlier revisions but absent from the package tree** (2026-08-21):
`Phase_W_aux_Pending_Doc_Updates.md`, `Phase_W_aux_b_Pending_Doc_Updates.md` and
`W-aux_c_Blocks_2_3_Pending_Doc_Updates.md`. All three were applied and their disposition is
recorded in `Application_Ledger_2026-08-20.md`; the files themselves are not on disk.
Recorded as a gap, not silently dropped from this list.

Application ledgers (`archive/`):
- `archive/Application_Ledger_2026-08-20.md` — item-by-item disposition of eight queues
- `Application_Ledger_2026-08-21.md` — item-by-item disposition of the W.b, Phase T2 and
  W-aux.h queues plus the Q and W closures. Not yet committed.

## Current governed reference docs called out explicitly
- `reference/overview.md`
- `reference/noise.md`
- `reference/mesh.md`
- `reference/surfaces.md`
- `reference/shaders.md`
- `reference/graphs.md`
- `reference/legacy-map-generation-reference.md`
- `reference/pcg-layout-strategies-reference.md`
- `reference/tileset-import-guide.md`
- `reference/map-tilemap-scene-setup.md` *(added to this list 2026-08-21 — present on disk,
  previously unlisted)*
- `reference/scalar-heatmap-scene-setup.md` *(added to this list 2026-08-21 — present on disk,
  previously unlisted)*

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
