# Coverage Matrix

Status: Active  
Purpose: Map each important concept to its primary documentary owner.

| Concept | Primary home | Role | Status |
|---|---|---|---|
| PCG core contracts | `systems/pcg-core-ssot.md` | subsystem authority | Active |
| Map Pipeline by Layers implemented slice (F0–Phase G + F2c) | `systems/map-pipeline-by-layers-ssot.md` | subsystem authority | Active |
| F2c — Arbitrary Shape Input contracts and test gates | `systems/map-pipeline-by-layers-ssot.md` | subsystem authority | Active |
| Future PCG sequencing (Phase H+) | `planning/active/PCG_Roadmap.md` | planning only | Active |
| Governance migration record | `planning/archive/Islands_Governance_Migration_Roadmap.md` | closed migration roadmap / planning history | Historical support |
| Cross-cutting documentation / technical rules | `SSoT_CONTRACTS.md` | package contracts | Active |
| Operational migration state | `CURRENT_STATE.md` | operational state | Active |
| Document replacement traceability | `supersession-map.md` | governance support | Active |
| Migration salvage decisions | `migration-log.md` | governance support | Active |
| Overview of support surfaces and authority boundaries | `reference/overview.md` | governed reference / navigation support | Active |
| Legacy external tilemap map-generation system used as conceptual input | `reference/legacy-map-generation-reference.md` | governed reference / historical support | Active |
| Legacy external tilemap map-generation source capture | `Documentation-snapshot/Documentation~/wip/Map_Generation_SSoT_v0.1.2_2026-01-29.md` | historical-support source in frozen corpus | Historical support |
| Early Map Layers transition roadmap (`v0.1.0`) | `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` | planning history / absorbed source | Historical support |
| Integrated PCG roadmap with Map Layers (`v0.2.4`) | `planning/archive/Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.4_2026-02-03.md` | planning history / superseded source | Historical support |
| Phase F planning report (`F3–F6`) | `planning/archive/PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03_v2.md` | planning history / absorbed source | Historical support |
| Historical PCG technical snapshot | `archive/PCG_Pipeline_Technical_Snapshot.md` | historical technical support | Historical support |
| Historical PCG split-source design bible (`v0.1.16`) | `archive/Islands_PCG_Pipeline_SSoT_v0_1_16.md` | historical split-source support | Historical support |
| Historical package technical bible | `archive/Islands_SSoT_Technical_Bible.md` | historical technical support | Historical support |
| Snapshot coverage / curation ledger | `archive/snapshot-curation-register.md` | archive governance support | Active |
| Layout strategy deep behavior / strategy internals | `reference/pcg-layout-strategies-reference.md` | governed deep reference / staged subsystem support | Active |
| Layout strategies as separate authority surface | no separate SSoT at present | deferred future reconsideration only if subsystem boundary hardens | Resolved for now |
| GraphLibrary runtime contracts and behavior | `Runtime/Graphs/GraphLibrary/*.cs` | implemented truth / runtime support surface | Active |
| GraphLibrary reference-facing overview | `reference/graphs.md` | governed reference / staged support | Active |
| GraphLibrary technical deep reference | `reference/GraphLibrary_Pipeline_Technical_Doc.md` | historical technical support / deep-support | Historical support |
| GraphLibrary example usage file | `Runtime/Graphs/GraphLibrary/DirectedGraphExample.cs` | historical support only / stale example | Non-authoritative |
| GraphLibrary promotion | no separate `systems/graphs-ssot.md` at present | staged support / reference after Batch 5 | Resolved for now |
| Noise runtime contracts and usage boundary | `reference/noise.md` | governed reference / staged support | Active |
| Noise implementation truth | `Runtime/Noise/**` | implemented truth / runtime support surface | Active |
| Noise subsystem promotion | no separate `systems/noise-ssot.md` at present | staged support / governed reference after Batch 6 | Resolved for now |
| Mesh runtime contracts and usage boundary | `reference/mesh.md` | governed reference / staged support | Active |
| Mesh implementation truth | `Runtime/Meshes/**` | implemented truth / runtime support surface | Active |
| Mesh subsystem promotion | no separate `systems/meshes-ssot.md` at present | staged support / governed reference after Batch 6 | Resolved for now |
| Surface jobs and sample orchestration boundary | `reference/surfaces.md` | governed reference / staged support | Active |
| Surface implementation truth | `Runtime/Surfaces/*.cs` | implemented truth / runtime support surface | Active |
| Sample surface orchestration | `Samples~/0.1.0-preview/ProceduralSurface.cs` | sample support / non-authoritative orchestration | Active support / non-authority |
| Surface subsystem promotion | no separate `systems/surfaces-ssot.md` at present | staged support / governed reference after Batch 6 | Resolved for now |
| Shader graphs and HLSL support surface | `reference/shaders.md` | governed reference / support | Active |
| Shader implementation assets | `Runtime/Shaders/**` | support artifacts / non-subsystem implementation surface | Active |
| Shader promotion | no separate shader subsystem SSoT at present | reference/support only after Batch 6 | Resolved for now |
| `TilemapLayerGroup` — multi-layer group descriptor (H5) | `Runtime/PCG/Adapters/Tilemap/TilemapLayerGroup.cs` | adapter-side implementation truth | Active |
| `TilemapAdapter2D.ApplyLayered` — multi-tilemap stamp (H5) | `Runtime/PCG/Adapters/Tilemap/TilemapAdapter2D.cs` | adapter-side implementation truth | Active |
| `TilemapAdapter2D.SetupCollider` — physics collider auto-setup (H5) | `Runtime/PCG/Adapters/Tilemap/TilemapAdapter2D.cs` | adapter-side implementation truth | Active |
| H5 test coverage (`ApplyLayered` null guards + independence, tests 8–11) | `Runtime/PCG/Tests/EditMode/PCG/Maps/TilemapAdapter2DTests.cs` | EditMode test gates | Active |
