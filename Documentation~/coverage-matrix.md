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
| Phase Q — biome-conditional tile selection (adapter-side) | `planning/active/Phase_Q_Design.md` | design authority for implemented adapter slice | Active |
| Tileset import and biome tile authoring workflow | `reference/tileset-import-guide.md` | governed reference / implementation-time support | Active |
| Phase W-aux documentation-application record (W-aux.a + W-aux.b) | `planning/archive/Phase_W_aux_Pending_Doc_Updates.md`, `planning/archive/Phase_W_aux_b_Pending_Doc_Updates.md` | planning history / applied-record support | Historical support |
| ~~Blocked documentation items (Phase Q §3.1; Phase W §3)~~ **both disposed 2026-08-21** | `archive/pending doc updates/Phase_Q_Pending_Doc_Updates.md` (applied in full), `archive/pending doc updates/Phase_W_Pending_Doc_Updates.md` (§3 closed as irrecoverable) | consumed input — not authority | Historical support |
| Authoring tools (preset diagnostics, preset diff) | `planning/active/PCG_Roadmap.md` (Phase X1) + `CURRENT_STATE.md` | planning + implemented truth | Active |
| Preset calibration values for `Default_MapPreset` | `changelog-ssot.md` (per-batch measured deltas) + `CURRENT_STATE.md` (current values) | governance support / implemented truth | Active |
| W-aux.c…W-aux.f + X1.a documentation-application record | `archive/pending doc updates/` (five queues) + `archive/Application_Ledger_2026-08-20.md` | consumed input, applied 2026-08-20 — not authority | Historical support |
| W.b + W-aux.h + Phase T2 documentation-application record | `W_b_Pending_Doc_Updates.md`, `W-aux_h_Pending_Doc_Updates.md`, `Phase_T2_Pending_Doc_Updates.md` + `Application_Ledger_2026-08-21.md` | consumed input, applied 2026-08-21 — not authority | Historical support, except the live T2 queue |
| Phase T2 planning branch (3D relief adapters) | `planning/active/PCG_Roadmap.md` (Phase T2) + `Phase_T2_Pending_Doc_Updates.md` | planning only — asserts no implemented truth | Active, 2 items blocked on `Phase_T2_Design.md` |
| Hydrology instrumentation (`hydroprobe`, R7) | `CURRENT_STATE.md` §Temporary measurement probes + §Preset diagnostics | implemented truth, adapter/Editor-side | Active, probe has a written retirement criterion |

## Test coverage and known gaps

The table above maps concepts to their documentary owner. This table maps **implemented
surfaces to their test coverage**, and exists to record gaps deliberately rather than let
them read as oversights.

| Surface | Coverage | Note |
|---|---|---|
| `MapStatsExporter2D` (W-aux.a + W-aux.c blocks 1 and 3) | **none — intentional** | Diagnostic-only surface, no runtime consumer, no invariant depends on its output. Not covered by golden or determinism gates. A wrong number here misleads calibration but cannot change generation output. If it ever feeds an automated acceptance rubric (Phase P), this row must be revisited. |
| `MapGenerationPresetJsonImporter` (W-aux.c block 2) | `MapGenerationPresetJsonRoundTripTests`, 5 tests | Literal `ToJson()` round-trip (comparison excludes `asset`, `stageTogglesNote`, `derived`), injected unknown field, absent field preserving the previous value, `noise.*.source = "asset:<n>"` handling, malformed value isolation. |
| `MapGenerationPresetWizard` (the `EditorWindow`) | **None** | Only the importer and the diagnostics logic are gated. UI behaviour — overwrite confirmation, `Undo.RecordObject`, asset creation, panel rendering, scrolling, console log — is verified by manual inspection only (screenshots, 2026-08-19). |
| `MapGenerationPresetDiagnostics` (X1.a) | `MapGenerationPresetDiagnosticsTests`, 10 tests | One preset per rule, a clean preset firing nothing, three diff gates. Green 2026-08-19. The rules' *measured backing runs* are historical after W-aux.d and W-aux.f; no test asserts that a rule's cited run still describes the pipeline. |
| M2a-3 per-biome peak policy (W-aux.c block 3) | `StageVegetation2DTests.AssertSubsetInvariants` | Gated in two forms via the `globalHillsL2Exclusion` parameter: strict emptiness on the legacy path, per-biome `vegetatesOnPeaks` on the biome path. |
| M2a-9 quantile cut (W-aux.d) | `M2a_QuantileCut_IsExact_Nested_AndAboveNominal` | Reimplements the cut independently of the stage and compares cell by cell, then checks nesting and the coverage floor. Replaces `M2a_CoverageMonotonicity_DenseBiomesExceedSparseBiomes`, which passed throughout the period the mapping was broken and was removed rather than relaxed. |
| `Vegetation` runtime coverage | **Partial** | `Vegetation` is not among the ten runtime golden hashes logged by `PCGMapTilemapVisualization`; its only gate is the 64×64 EditMode golden. A vegetation policy change driven by preset values would trip no 256×256 gate. |
| River / lake / relief tuning advice | **No corrida-backed evidence** | Only vegetation density and height composition have backing runs. Relevant if the X1.b advisor line proceeds — any guidance on the others would be `inferred` at best. |
| Sea-floor relief (W-aux.b) | Indirect | The identity default (`seaFloorLevel01 = seaFloorAmplitude01 = 0`) is covered by existing goldens. Non-identity behaviour is verified only by the W-aux.b differential (manual). No test asserts `Land` / `DeepWater` invariance with the feature **on**. |
| `Stage_BaseTerrain2D` ↔ `BaseTerrainStage_Configurable` parity | **None** | Two implementations of the same base-terrain mathematics; nothing asserts equivalence. `PCGMapTilemapVisualization` instantiates the mirror, so every visual, tooltip, exporter statistic and console hash measures the mirror while the golden tests measure the governed stage. See `CURRENT_STATE.md`, W-aux.b structural block. |
| Q-BUG-2 (collider group excluded from the biome-aware path) | **None — smoke only** | `StampMultiLayerBiomeAware` is private with no test seam. Verified by the Phase Q smoke protocol. See `planning/active/Phase_Q_Design.md` §12. |

**Proposed tests (not written).** Recorded so the gap has a concrete remedy attached:

1. `SeaFloorLandInvarianceTests` — same seed and domain, run with sea floor off and on;
   assert `Land` and `DeepWater` snapshot hashes identical and `Height` hashes different.
2. `BaseTerrainMirrorParityTests` — run both stages over identical `MapContext2D` and
   `MapInputs`; assert `Height`, `Land` and `DeepWater` are bit-identical. Would have
   caught the W-aux.b half-patch failure mode directly. **Still unwritten as of 2026-08-20**,
   and W-aux.f is the second batch that had to patch both implementations by hand — plus a
   third, ungoverned copy of the same formula now lives in the `LogHeightHistogram` probe.
