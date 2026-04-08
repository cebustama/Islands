# Current State

Status date: 2026-04-08

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–F6 + F3b + F4b + F4c + Phase G + Phase H + Phase H1 + Phase H2 + Phase H2b + Phase H2c + Phase H2d + Phase H3 + Phase H4 + Phase H5 + Phase H6 + Phase H7 + Phase J2 + Phase N2 + Post-N2 Fixes + Phase N4 + Phase N5.a + Phase N5.b + Phase N5.c**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- Phase N5.c — Extended Noise Palette + Ridged Multifractal.
  Worley noise type (`TerrainNoiseType.Worley`) is now a parameterized family driven by
  `WorleyDistanceMetric` (Euclidean, SmoothEuclidean, Chebyshev) × `WorleyFunction`
  (F1, F2, F2MinusF1, CellAsIslands) = 12 generic instantiations dispatched via a flat
  switch in `MapNoiseBridge2D.FillWorleyNoise01`. No new `TerrainNoiseType` enum entries
  were added; the existing `Worley` entry is parameterized by the N5.b struct fields.
  `FractalMode` enum migrated from `Islands.PCG.Layout.Maps` to `Islands` namespace
  (declared in `Noise.cs`). `Noise.Settings` extended with `fractalMode`, `ridgedOffset`,
  `ridgedGain`. `Noise.GetFractalNoise<N>()` branches on `fractalMode`: Standard path is
  unchanged (zero golden break); Ridged path calls new private `GetRidgedFractalNoise<N>()`
  implementing the Musgrave ridged multifractal algorithm (offset–abs–square with
  inter-octave gain feedback). Applies to all `INoise` types (Perlin, Simplex, Value,
  all Worley variants).
  Assembly references updated: `Islands.PCG.Editor` and `Islands.PCG.Tests.EditMode`
  now reference `Islands.Runtime` directly for `FractalMode` resolution.
  New `MapNoiseBridge2DTests.cs` with 22 tests covering Worley dispatch, ridged behavior,
  determinism, range invariants, edge cases, and legacy path stability.
  Zero golden break at defaults. Zero RNG consumption. No new MapLayerId or MapFieldId.
  Known visual observation: Worley-family noise biases toward bright values due to the
  `n * 0.5 + 0.5` remap assuming [-1,1] centered output. Worley outputs non-negative
  distances, mapping to [0.5,1.0]. Tracked as potential post-N5.c follow-up
  (Worley-aware normalization in FillNoise01Core).
- Prior resolution: Phase N5.b — Noise Settings Assets.
  Added `NoiseSettingsAsset` ScriptableObject wrapping `TerrainNoiseSettings`, following
  the override-at-resolve pattern: when assigned to a preset or component noise slot, the
  asset's settings replace inline values; when null, inline values are used.
  `MapGenerationPreset` extended with `terrainNoiseAsset` and `warpNoiseAsset` slots.
  `ToTunables()` resolves asset → inline struct.
  All three visualization Inspectors updated with noise asset slots + embedded struct fields.
  Refactored 11 individual noise fields per consumer to 2 embedded `TerrainNoiseSettings`
  structs (serialization break for existing preset assets and scene-serialized components).
  `TerrainNoiseSettings` extended with 5 new fields: `WorleyDistanceMetric` enum
  (Euclidean, SmoothEuclidean, Chebyshev), `WorleyFunction` enum (F1, F2, F2MinusF1,
  CellAsIslands), `FractalMode` enum (Standard, Ridged), `ridgedOffset` (float, 1.0),
  `ridgedGain` (float, 2.0). All new fields carried but not functional until N5.c.
  `IEquatable<TerrainNoiseSettings>` added for dirty-tracking.
  Custom `TerrainNoiseSettingsDrawer` PropertyDrawer provides conditional visibility:
  Worley fields hidden when noiseType != Worley; ridged fields hidden when
  fractalMode == Standard.
- Prior resolution: Phase N5.a — Base Shape Selector.
  `IslandShapeMode` enum (Ellipse/Rectangle/NoShape/Custom) + `shapeMode` field on
  `MapTunables2D`. Ellipse default = bit-identical to pre-N5.a. All visualization
  Inspectors + Editor updated. Zero golden break at defaults. Zero RNG consumption.
- Prior resolution: Phase F3b — Height-Coherent Hills (Clean Break).
  Replaced topology-based Stage_Hills2D (independent noise on LandInterior) with
  height-threshold classification. HillsL1 and HillsL2 are now derived from
  Height field thresholds via `MapTunables2D.hillsThresholdL1` / `hillsThresholdL2`.
  Contract changes: HillsL1 ∩ HillsL2 == ∅ (disjoint, was overlapping);
  HillsL1/L2 ⊆ Land (was HillsL1 ⊆ LandInterior). LandEdge/LandInterior
  derivation unchanged (MaskTopologyOps2D.ExtractEdgeAndInterior4).
  All MapNoiseBridge2D and independent noise removed from Stage_Hills2D.
  Zero RNG consumption (continues N4 pattern).
  Thresholds exposed on MapTunables2D, MapGenerationPreset, all three visualization
  Inspectors (PCGMapVisualization, PCGMapCompositeVisualization,
  PCGMapTilemapVisualization), and the custom Editor (PCGMapTilemapVisualizationEditor).
  Full golden break for F3+ hashes. All F3–G pipeline and stage goldens re-locked.
  Latent Traversal test bug fixed: `Walkable ⊆ Land` assertion corrected to
  `Walkable ⊆ (Land ∪ ShallowWater)` to match the Post-N2 Issue 3 contract.
  Modified files: Stage_Hills2D, MapTunables2D, MapGenerationPreset,
  PCGMapVisualization, PCGMapCompositeVisualization, PCGMapTilemapVisualization,
  PCGMapTilemapVisualizationEditor, StageHills2DTests, MapGenerationPresetTests,
  StageTraversal2DTests, plus all F3–G pipeline golden test files.
- Prior resolution: Phase N4 — Noise Settings Infrastructure + F2 Noise Upgrade.
  Replaced manual value noise in Stage_BaseTerrain2D with proper noise runtime via
  MapNoiseBridge2D.FillNoise01. Full golden break from pre-N4 output.
- Prior resolution: Post-N2 Fixes (Issues 1–3).
- Phase J2 — Height Redistribution implemented.
- Phase N2 — Spline Remapping implemented.
- Prior resolution (Phase H6): Rule Tiles.
- Prior resolution (Phase F4b): Shore Depth Tunable.
- Prior resolution (Phase F4c): Mid-Water Layer.
- Prior resolution (Phase H7): Map Navigation Sample.
- Prior resolution (Phase H5): Multi-layer Tilemap & Collider Integration.
- Prior resolution (Phase H4): Animated Tiles in TilesetConfig.
- Prior resolution (Phase H3): MapGenerationPreset + TilesetConfig SOs.
- Prior resolution (Phase H2d): Procedural Tiles.
- Prior resolution (Phase H2c): PCGMapTilemapVisualization.
- Prior resolution (Phase H2b): TilemapAdapter2D.
- Prior resolution (Phase H2): MapDataExport + MapExporter2D.
- Prior resolution (Phase H1): PCGMapCompositeVisualization.
- Prior resolution (Phase H): PCGMapVisualization scalar field view.
- Prior resolution (Phase F2c): MapShapeInput; MapInputs extended.
- Prior resolution (Phase F2b): Stage_BaseTerrain2D ellipse + domain-warp silhouette.
- Prior resolution (Phase G): MaskMorphologyOps2D; Stage_Morphology2D → LandCore, CoastDist.

## What the roadmap redesign pass resolved (2026-04-02)
- Phase F2b added to roadmap and immediately implemented: organic island shape reform (ellipse + domain warp).
- Phase F2c — Arbitrary Shape Input: implemented and test-gated.
- Archipelago support intent explicitly noted under Phase J and Phase K.
- Phase H2 — Data Export / Map Adapters added to roadmap after Phase H.

## What is not settled yet
- No unresolved migration batch remains for the reviewed snapshot corpus.
- Open design questions recorded in the roadmap: river representation, lake modeling, biome output format — now all resolved as design decisions in the roadmap.
- `MapFieldId.Moisture` write ownership confirmed: Phase M.
- `MapLayerId.Paths` write ownership confirmed: Phase O.
- Unity version target for `TilemapCollider2D.usedByComposite` deprecation: upgrade to
  `compositeOperation` if targeting Unity 2022.2+ exclusively (currently suppressed with `#pragma warning disable CS0618`).
- **Hills noise modulation:** Roadmapped as Phase N5.d. A `hillsNoiseBlend` parameter
  (0–1) will mix per-cell noise offsets into the height thresholds for additional variation.
  No contract changes. See N5 section in roadmap.
- **Worley normalization bias:** Voronoi-family noise biases toward [0.5,1.0] after the
  standard `n * 0.5 + 0.5` remap because Worley outputs non-negative distances rather
  than [-1,1] centered values. A Worley-aware normalization mode in FillNoise01Core
  would improve visual contrast. Small follow-up, no contract impact.

## Noted desired features (not yet roadmapped as phases)
- **Extended noise type palette (post-N4 observation):** ~~The noise runtime supports several
  additional types not yet exposed in TerrainNoiseType.~~ **Resolved by N5.c.** All
  Worley metric × function combinations (12 total) are accessible via the parameterized
  `Worley` enum entry + `WorleyDistanceMetric` / `WorleyFunction` struct fields.
  CellAsIslands + SmoothEuclidean is available for archipelago generation (Phase J).
  Ridged multifractal is implemented in the noise runtime for all noise types.

## Immediate next focus
Phase N5.c (Extended Noise Palette + Ridged Multifractal) done and smoke-tested.

Next implementation sequence (confirmed):
1. **Phase N5.d** — Hills Noise Modulation.
   Adds optional per-cell noise offset to the height-threshold hills classification (F3b).
   `hillsNoiseBlend` (0–1), default 0.0 = pure height-threshold (golden-safe).
2. **Phase H8** — Mega-Tiles (2x2 large terrain sprites).
   Adapter-side. Depends on art assets with 2x2 sprite variants.

See `planning/active/PCG_Roadmap.md`.

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
