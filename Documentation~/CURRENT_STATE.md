# Current State

Status date: 2026-04-02

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–F6 + Phase G**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- Phase F2c — Arbitrary Shape Input is now implemented and test-gated.
  New `MapShapeInput` struct; `MapInputs` extended with optional 4th param (backward compatible).
  `Stage_BaseTerrain2D` branches on `HasShape`: when true, `GetUnchecked(x,y)` replaces ellipse+warp;
  all three RNG arrays still filled in the same order, preserving downstream determinism.
  No new MapLayerId/MapFieldId. F2b goldens unchanged. F2c goldens locked (center-circle, seed=12345).
- Prior resolution (Phase F2b): `Stage_BaseTerrain2D` reformed: circular radial falloff replaced with ellipse + domain-warp silhouette.
- Shape pipeline: ellipse aspect ratio → domain warp → smoothstep radial falloff → height perturbation.
- Two new `MapTunables2D` fields: `islandAspectRatio` (clamped [0.25, 4.0], default 1.0),
  `warpAmplitude01` (clamped [0, 1], default 0.0). Default values preserve pre-F2b circle geometry;
  goldens differ because warp arrays are always filled from ctx.Rng regardless of amplitude.
- No new `MapLayerId` or `MapFieldId` entries.
- All F2–Phase G golden hashes re-locked in one migration pass. Phase G goldens locked for first time.
- `PCGMapVisualization` patched with new Inspector tunables; `BaseTerrainStage_Configurable` updated
  to mirror Stage_BaseTerrain2D (WarpCellSize=16, BilinearSample helper).
- Prior resolution (Phase G): `MaskMorphologyOps2D` added; `Stage_Morphology2D` produces `LandCore` and
  `CoastDist`. `LandCore ⊆ LandInterior ⊆ Land`. `CoastDist` = BFS distance from `LandEdge` inward.
  `MapLayerId.LandCore = 11` (COUNT → 12), `MapFieldId.CoastDist = 2` (COUNT → 3). No RNG consumption.

## What the roadmap redesign pass resolved (2026-04-02)
- Phase F2b added to roadmap and immediately implemented: organic island shape reform (ellipse + domain warp).
- Phase F2c — Arbitrary Shape Input: implemented and test-gated. `MapShapeInput` companion struct;
  optional shape-input branch in `Stage_BaseTerrain2D`; F2b path and goldens unchanged.
- Archipelago support intent explicitly noted under Phase J (Voronoi regions) and Phase K (Plate Tectonics) as Level 3 of the island shape vision.

## What is not settled yet
- No unresolved migration batch remains for the reviewed snapshot corpus.
- Open design questions recorded in the roadmap: river representation (mask vs. flow-accumulation field), lake modeling (distinct layer vs. enclosed ShallowWater), biome output format.
- `MapFieldId.Moisture` write ownership confirmed: Phase M.
- `MapLayerId.Paths` write ownership confirmed: Phase O.
- `CoastDist` ScalarField2D visualization is not yet supported in the lantern (shows all-OFF); verified via golden tests only. Governed visualization of scalar fields is a Phase H concern.

## Immediate next focus
Phase H — Extract + Adapters.
See `planning/active/PCG_Roadmap.md` for the full sequence.

## Why Batch 7 closed the current hardening pass
Batch 2 established active PCG authority.
Batch 3 removed the main legacy map-generation ambiguity.
Batch 4 resolved layout strategies as staged support rather than separate subsystem authority.
Batch 5 resolved GraphLibrary as staged support / governed reference rather than subsystem authority.
Batch 6 hardened Noise / Meshes / Surfaces / Shaders and normalized their governed reference homes.
Batch 7 completed the remaining repo-wide normalization and traceability hardening for the reviewed evidence set.

## What Batch 7 resolved
- Repo-wide cross-links across the governed spine were normalized to the correct governed homes.
- Missing snapshot/source-file status headers were applied for the main authority-risk legacy files.
- `planning/archive/Islands_PCG_MapPipelineByLayers_Roadmap_v0.1.0_2026-01-29.md` is now landed as a governed archive destination instead of a merely declared future path.
