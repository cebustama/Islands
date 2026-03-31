# Islands Documentation Governance Migration Roadmap (Closed Archive)

> **Status:** Historical support / closed migration roadmap  
> **Date archived:** 2026-03-31  
> **Superseded by:** normal governed operation through `Documentation~/CURRENT_STATE.md` + `Documentation~/SSoT_INDEX.md` + `Documentation~/coverage-matrix.md`  
> **Authority note:** This file is preserved for migration traceability only. The migration is considered materially closed for the reviewed snapshot corpus.

---

Date: 2026-03-31  
Status: Active migration roadmap after Batch 7  
Scope: Governance migration of the Islands package documentation  
Method basis: governance v0.4 stack + DSSW salvage workflow

## 1. Purpose
This roadmap consolidates:
- the original first-pass migration diagnosis,
- the concrete Batch 2 PCG authority rescue result,
- the Batch 3 legacy map-generation classification,
- the Batch 4 layout strategies staging decision,
- the Batch 5 GraphLibrary authority decision,
- the Batch 6 Noise / Mesh / Surfaces / Shaders hardening result,
- the creation of the new governed `Documentation~/` scaffold,
- and the remaining migration batches.

It replaces the need to mentally combine:
- the first-pass roadmap,
- the Batch 2 rescue roadmap,
- and the separate batch-by-batch decisions made afterward.

Those older roadmap files remain useful as source history and traceability, but this file is the active migration planning surface.

## 2. Core diagnosis that remains in force
The package does not primarily suffer from lack of documentation.  
It suffers from **authority ambiguity**.

This ambiguity still shows up as:
1. WIP acting as hidden law
2. legacy `SSoT` names implying authority they have not earned
3. roadmap / progress docs carrying implementation truth
4. reference docs competing with subsystem truth
5. runtime-local markdown acting like active authority
6. historical documents remaining unclear about whether they are reference, archive, or support

## 3. Tier and migration strategy

### Migration tier
Treat the package as **Tier L** for migration purposes.

### Required approach
- preservation-first
- salvage-driven
- authority-driven
- phase-based
- folder-by-folder
- package-aware
- parallel replacement instead of destructive cleanup

### Snapshot rule
The legacy documentation tree remains a **frozen migration input corpus**.  
It is expected that many files still live only in the snapshot until they receive an explicit role.  
This is normal and does not imply loss.

## 4. Governed root and spine
The governed docs root remains **`Documentation~/`**.  
Do **not** create a second `Docs/` root.

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
- `Documentation~/governance/`

## 5. What is already resolved

### Batch 1 — governance scaffold + public/reference normalization
**Status:** landed enough for the current migration stage; later curation still possible

What is done:
- governed spine created
- `Documentation~/planning/active/` created
- `Documentation~/systems/` created
- base governance docs created in seed form

What is still open:
- full repo-wide cross-reference repair
- explicit source-file header application for superseded / absorbed / historical-support docs
- broader archive / research curation

### Batch 2 — PCG core + active map pipeline rescue
**Status:** closed

Resolved decisions:
- the active implemented Islands PCG truth is the new grid-first pipeline
- PCG core has earned promoted subsystem authority
- Map Pipeline by Layers has earned promoted subsystem authority for the implemented slice only
- implemented map slice = **F0–F2 only**
- F3–F6 remain planning only
- progress reports and planning docs no longer define implementation truth

Governed outputs already expected / created:
- `Documentation~/systems/pcg-core-ssot.md`
- `Documentation~/systems/map-pipeline-by-layers-ssot.md`
- `Documentation~/planning/active/PCG_Roadmap.md`

### Batch 3 — legacy map-generation triage
**Status:** closed

Resolved decisions:
- `Map_Generation_SSoT_v0.1.2_2026-01-29.md` does **not** govern the active Islands runtime
- its code / system is external reference material, not package runtime truth
- role = legacy conceptual reference / historical support
- `Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` is historical roadmap source material to absorb into the active `PCG_Roadmap.md`

### Batch 4 — layout strategies verification and staging
**Status:** closed

Resolved decisions:
- layout strategies are real, implemented, and test-gated
- they should **not** be promoted yet as a separate subsystem SSoT
- they should remain **staged under PCG**
- the old `Islands_PCG_Layout_Strategies_SSoT_v0_1_2.md` should move out of WIP into a governed **reference / deep-support** role
- layout-strategy docs must stop implying primary authority by filename alone

### Batch 5 — GraphLibrary authority decision
**Status:** closed

Resolved decisions:
- GraphLibrary runtime boundary is real
- GraphLibrary is still too mixed / local / thin for subsystem promotion
- `Documentation~/reference/graphs.md` remains the correct governed reference-facing surface
- `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md` is the retained historical technical support / deep-support doc, not active law
- no `systems/graphs-ssot.md` is justified yet
- `DirectedGraphExample.cs` should not be treated as canonical usage truth

### Batch 6 — Noise / Mesh / Surfaces / Shaders hardening
**Status:** closed

Resolved decisions:
- Noise has a real runtime boundary, but it remains governed reference / staged support rather than a promoted subsystem SSoT.
- Meshes have a real runtime boundary, but they remain governed reference / staged support rather than a promoted subsystem SSoT.
- Surfaces have real runtime jobs, but remain mixed with sample orchestration and therefore remain governed reference / staged support rather than a promoted subsystem SSoT.
- Shaders are active support artifacts and governed reference material, not a promoted subsystem SSoT.
- `overview.md`, `noise.md`, `mesh.md`, `surfaces.md`, and `shaders.md` now have governed homes under `Documentation~/reference/`.
- `Samples~/0.1.0-preview/ProceduralSurface.cs` is treated as sample support / non-authoritative orchestration.

## 6. Current promotion / staging state

### Promoted subsystem authority now
- PCG core pipeline
- Map Pipeline by Layers (implemented slice only: F0–F2)

### Staged support, not promoted yet
- layout strategies as separate SSoT
- GraphLibrary
- Noise
- Meshes
- Surfaces

### Governed reference / support only
- overview of support surfaces
- shaders as a standalone surface

### Historical / reference-only surfaces
- legacy tilemap map generation
- old roadmap / progress docs once absorbed
- process artifacts such as old rehydration prompts
- GraphLibrary technical snapshot support in documentation and stale example support
- public snapshot docs once normalized into governed reference homes

## 7. Status of the technical PCG roadmap
The migration roadmap is distinct from the technical PCG roadmap.

### Current synchronized understanding
- Phase A: done
- Phase B: done
- Phase C: done
- Phase D: done
- Phase E: implemented / test-gated support surface
  - E1 Corridor First implemented
  - E2 Room First BSP implemented
  - E3 Room Grid implemented and locked
  - E4 seed-set regression complete
- Phase F:
  - F0 done
  - F1 done
  - F2 done
  - F3 next
- Later phases G / H / I remain later work

## 8. What Batch 7 resolved
Batch 7 closed the repo-wide hardening pass that was still pending after the earlier authority decisions.

Resolved outcomes:

1. **Cross-reference repair landed**
   - the main governed spine now points to the correct governed homes
   - stale unqualified references were normalized where they mattered for navigation and traceability

2. **Supersession / absorption headers were applied for the main authority-risk source files**
   Particularly for:
   - `Documentation-snapshot/Documentation~/wip/Islands_PCG_Pipeline_SSoT_v0_1_16.md`
   - `Documentation-snapshot/Documentation~/wip/Islands_PCG_Layout_Strategies_SSoT_v0_1_2.md`
   - `Documentation-snapshot/Documentation~/wip/Map_Generation_SSoT_v0.1.2_2026-01-29.md`
   - `Documentation-snapshot/Documentation~/wip/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md`
   - `Documentation-snapshot/Documentation~/wip/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md`
   - `Documentation-snapshot/Documentation~/wip/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md`
   - `Documentation-snapshot/Documentation~/subsystems/PCG_Pipeline_Technical_Snapshot.md`
   - `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md`
   - the snapshot-era `overview.md`, `noise.md`, `mesh.md`, `surfaces.md`, `shaders.md`

3. **Governed archive landing completed for the old Map Layers roadmap**
   - `Documentation~/planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` now exists as a governed archive file rather than a merely declared destination

4. **GraphLibrary normalization tightened**
   - GraphLibrary historical technical support is now retained in `Documentation~/reference/GraphLibrary_Pipeline_Technical_Doc.md`
   - the runtime-local markdown copy should no longer remain part of the package documentation surface

5. **Folder scaffold gaps were closed**
   - `planning/archive/`
   - `archive/`
   - `research/`
   - `governance/`

## 9. Batch status table


| Batch | Purpose | Status | Result |
|---|---|---|---|
| Batch 1 | governance scaffold + public/reference normalization | partially landed | new governed spine exists; normalization still incomplete |
| Batch 2 | PCG core + active map-pipeline rescue | closed | promoted PCG core + Map Pipeline F0–F2 authority |
| Batch 3 | legacy map-generation triage | closed | legacy map-generation classified as historical/reference-only |
| Batch 4 | layout strategies verification and staging | closed | implemented + gated, but staged under PCG rather than promoted |
| Batch 5 | GraphLibrary authority decision | closed | GraphLibrary kept as staged support / governed reference, not promoted subsystem authority |
| Batch 6 | Noise / Mesh / Surfaces / Shaders hardening | closed | governed reference homes normalized; no new subsystem promotions |
| Batch 7 | cross-reference repair + source-file header application + curation | closed | normalized repo-level traceability and landed the main archive/header pass |

## 10. Immediate next work after Batch 7

### Goal
Perform only bounded cleanup now that the current migration stage has its main authority and traceability risks under control.

### Likely areas
- broader archive / research curation
- residual non-core markdown normalization
- deleting any runtime-local markdown that no longer belongs in the documentation surface
- keeping the governed spine stable as the active navigation path

### Important constraint
Do not reopen promotion decisions already closed in Batches 2–7 unless new runtime evidence directly forces a correction.

## 11. Execution order from here
1. Apply the Batch 7 normalized tree as the active documentation surface
2. Remove the obsolete runtime-local GraphLibrary markdown copy
3. Perform a narrower cleanup pass for remaining archive / research surfaces
4. Only after that should the old structure be considered meaningfully replaceable as active guidance

## 12. Done criteria for the current migration stage
This migration stage is successful when:
- the governed spine exists and is the active navigation path
- active PCG truth no longer depends on WIP docs
- legacy map generation no longer appears to be active package law
- layout strategies are correctly staged
- GraphLibrary has an explicit authority role
- Noise / Meshes / Surfaces / Shaders have governed homes and no false subsystem promotions
- supersession / absorption is explicit for authority-risk docs
- and the frozen snapshot remains preserved for traceability without competing as live authority