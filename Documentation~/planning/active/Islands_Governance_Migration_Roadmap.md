# Islands Documentation Governance Migration Roadmap

Date: 2026-03-23
Status: Active migration roadmap after Batch 2 landing
Scope: Governance migration of the Islands package documentation
Method basis: governance v0.4 stack + DSSW salvage workflow

## 1. Purpose
This roadmap consolidates:
- the original first-pass migration diagnosis,
- the concrete Batch 2 PCG authority rescue result,
- the creation of the new governed `Documentation~/` scaffold,
- and the immediate next migration batches.

It replaces the need to mentally combine a high-level first-pass roadmap with a separate Batch 2 rescue roadmap.
The older roadmap files remain valuable as source history, but this file is the new active planning surface for the migration itself.

## 2. Core diagnosis that remains in force
The central problem is still not lack of documentation.
It is authority ambiguity.

This continues to show up as:
1. WIP acting as hidden law
2. legacy `SSoT` names implying authority they have not earned
3. roadmap/progress docs carrying implementation truth
4. reference docs competing with active subsystem truth
5. unclear boundaries between the new PCG runtime and legacy map-generation material

## 3. Tier and strategy
### Migration tier
Treat the package as **Tier L** for migration purposes.

### Required approach
- preservation-first
- salvage-driven
- authority-driven
- phase-based
- folder-by-folder
- package-aware
- parallel replacement instead of in-place cleanup

### Snapshot rule
The old documentation tree should remain preserved as a fixed snapshot while the new governed tree is adopted.
Do not start by deleting or mass-renaming the legacy material.

## 4. Target governed spine
The governed docs root remains `Documentation~/`.
Do not create a second `Docs/` root.

### Spine
- `Documentation~/README.md`
- `Documentation~/SSoT_INDEX.md`
- `Documentation~/SSoT_CONTRACTS.md`
- `Documentation~/coverage-matrix.md`
- `Documentation~/changelog-ssot.md`
- `Documentation~/CURRENT_STATE.md`
- `Documentation~/migration-log.md`
- `Documentation~/supersession-map.md`

### Governed folders
- `Documentation~/systems/`
- `Documentation~/reference/`
- `Documentation~/planning/active/`
- `Documentation~/planning/archive/`
- `Documentation~/research/`
- `Documentation~/archive/`
- `Documentation~/governance/` (local process reminder)

## 5. What Batch 2 already resolved
Batch 2 is no longer just a plan. It is now a migration landing point.

### Authority decisions already made
- The active implemented truth of Islands PCG is the new grid-first pipeline.
- The implemented and test-backed Map Pipeline by Layers slice is F0–F2.
- F3–F6 remain planning only.
- Legacy tilemap map-generation material is not active authority for the new PCG runtime by default.

### Immediate documents already created in seed form
- `systems/pcg-core-ssot.md`
- `systems/map-pipeline-by-layers-ssot.md`
- `planning/active/PCG_Roadmap.md`
- core governance spine docs

## 6. Subsystem promotion state
### Promoted now
- PCG core pipeline
- Map Pipeline by Layers (implemented slice only)

### Staged but not promoted yet
- layout strategies as a separate SSoT
- GraphLibrary
- shader layer as standalone SSoT
- legacy tilemap map generation

### Planned hardening later
- Noise
- Meshes
- Surfaces
- reference-facing docs for Shaders/Graphs

## 7. Batch status table
| Batch | Purpose | Status | Result |
|---|---|---|---|
| Batch 1 | governance scaffold + public/reference normalization | partially landed in seed form | new docs spine created |
| Batch 2 | PCG core + active map-pipeline rescue | landed conceptually and in seed docs | promoted PCG core + F0–F2 authority |
| Batch 3 | legacy map-generation triage | next | classify old map-generation authority correctly |
| Batch 4 | layout strategies verification and staging | queued | decide whether separate SSoT is warranted |
| Batch 5 | GraphLibrary authority decision | queued | keep as reference or promote later |
| Batch 6 | Noise / Mesh / Surfaces / Shaders hardening | queued | promote what is mature and normalize reference docs |

## 8. Immediate next batch — Batch 3
### Goal
Determine whether the legacy tilemap map-generation documentation is:
- reference-only,
- historical-only,
- partially still active,
- or still needed as a compatibility bridge.

### Why this is next
After Batch 2, the biggest remaining authority ambiguity in PCG is not the new pipeline itself.
It is the legacy map-generation material that still carries SSoT-like naming and may still be referenced as if it described current package truth.

### Required source set for Batch 3
#### Highest-priority docs
- `Documentation~/wip/Map Layers/Map_Generation_SSoT_v0.1.2_2026-01-29.md`
- `Documentation~/wip/Map Layers/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md`
- any old overview or map-generation explainer that still describes islands/dungeons/world maps as if it were current runtime truth

#### Runtime verification files (only if still relevant)
- any runtime file still tied to the legacy tilemap generation path
- any adapter or sample that still consumes legacy map-generation outputs
- any tests that still verify the old generator path

#### Supporting files
- any document that informally acts as “the real explanation” of the old map system
- any snapshot or archive file that explains what the legacy system actually did

### Expected outputs of Batch 3
- a clean classification for legacy map generation
- either a `reference/legacy-tilemap-map-generation.md` document or an explicit archive-only decision
- updates to `coverage-matrix.md`, `CURRENT_STATE.md`, `supersession-map.md`, and `migration-log.md`
- removal of any remaining implication that the old map-generation SSoT is active package law

## 9. Batch 4 preview — Layout strategies
Batch 4 should evaluate whether layout strategies deserve separate subsystem authority or should remain staged under PCG support.

Likely inputs:
- `Documentation~/wip/Islands_PCG_Layout_Strategies_SSoT_v0_1_2.md`
- representative runtime strategy files
- relevant tests

## 10. Batch 5 preview — GraphLibrary
Batch 5 should determine whether GraphLibrary remains reference/support or has earned promotion.

## 11. Batch 6 preview — Noise / Mesh / Surfaces / Shaders
Batch 6 should harden the public-facing reference docs and promote only the subsystem authorities that are cleanly justified by runtime evidence.

## 12. Execution order from here
1. Place this new governed `Documentation~/` tree next to the frozen legacy snapshot.
2. Review and adjust file names if needed, but keep roles intact.
3. Use Batch 3 to resolve legacy map-generation authority.
4. Then use Batch 4 to resolve layout strategies.
5. Only after the high-risk PCG ambiguity is resolved should the migration broaden into the rest of the package.

## 13. Done criteria for the current stage
This stage is successful when:
- the new governed spine exists,
- active PCG truth no longer depends on WIP docs to be understood,
- the migration has an explicit next batch,
- and the legacy snapshot can remain frozen without blocking the new governed path.
