# Current State

Status date: 2026-04-10 (M2.b resolved)

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–N6 + Phase M + M-fix.a/c + M2.a + M2.b (all golden-captured)**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- M2.b — Contiguous Region Detection + Naming.
  CCA over `Biome` field produces contiguous same-biome regions; specks merged into
  largest 4-adjacent neighbour (tie-break: lowest anchor row-major index).
  `MapFieldId.BiomeRegionId = 5` (COUNT → 6); 0 = water/Unclassified sentinel;
  1-based integers for classified land regions. Intra-map stable only — cross-seed
  stability is an explicit non-goal (R-7; see SSoT_CONTRACTS.md).
  New files: `Stage_Regions2D.cs`, `RegionNameRegistry2D.cs`, `RegionNameTableAsset.cs`.
  All four viz classes patched (`PCGMapVisualization`, `PCGMapCompositeVisualization`,
  `PCGMapTilemapVisualization`, `PCGMapTilemapVisualizationEditor`); `stagesM2b` lantern
  entry and `ScalarOverlaySource.BiomeRegionId = 5` added.
  Full-pipeline golden captured: `MapPipelineRunner2DGoldenM2bTests.cs`.
- Prior resolution: M2.a — Biome-Aware Vegetation Density.
  Per-biome vegetation density via biome-aware per-cell threshold computed in
  Stage_Vegetation2D from BiomeTable entries. Stage-local `moistureModulation`
  default set to 0 (biome threshold is now the primary driver; moisture is
  optional secondary modulation). Option A fallback when biome layer absent:
  `LegacyThreshold = 0.40f` constant preserves pre-M2.a behavior for
  biome-disabled pipelines. Pipeline reorder: vegetation now runs after biome
  classification (was: vegetation before biome). Three visualization classes
  (PCGMapTilemapVisualization, PCGMapCompositeVisualization, PCGMapVisualization)
  gained `stagesM2a` lantern entry. StageVegetation2DTests adopts dual-golden
  pattern (biome-on + biome-off paths). New `MapPipelineRunner2DGoldenM2Tests.cs`
  captures full-pipeline M2.a goldens. Side effect: M-fix.a/c goldens
  re-captured — 5 constants updated across `StageBiome2DTests.cs` and
  `MapPipelineRunner2DGoldenMTests.cs`.
- Prior resolution: M-fix.a + M-fix.c — Biome Tunables Inspector Wiring + Moisture Default Tuning.
  10 biome climate tunables promoted from hardcoded Stage_Biome2D defaults to
  Inspector-accessible serialized fields on PCGMapTilemapVisualization and
  MapGenerationPreset. Follows shallowWaterDepth01 pattern (stage-local feeding,
  not via MapTunables2D). Moisture defaults adjusted: coastalMoistureBonus 0.3→0.5,
  coastDecayRate 0.15→0.3, moistureNoiseAmplitude 0.5→0.3 (coast gradient now visible).
  Editor conditionally hides biome fields when enableBiomeStage is off.
  Golden break — all M hashes must be re-captured.
  4 files modified: Stage_Biome2D.cs, PCGMapTilemapVisualization.cs,
  MapGenerationPreset.cs, PCGMapTilemapVisualizationEditor.cs.
  No new stages, layers, or fields. Pure plumbing + default adjustment.
  Note: 10 tunables wired, not 11 — beachMinTemperature lives on BiomeTable
  as static readonly, not on Stage_Biome2D. Separate micro-fix if desired.
- Prior resolution: Phase M — Climate & Biome Classification.
- Prior resolution: Phase H8 — Mega-Tiles (2×2 Large Terrain Sprites).
- Prior resolution: Phase N6 — Noise Preview Visualization.
- Prior resolution: Phase N5.e — Hills Threshold UX Remap.
- Prior resolution: Phase N5.d — Hills Noise Modulation.
- Prior resolution: Phase N5.c — Extended Noise Palette + Ridged Multifractal.
- Prior resolution: Phase N5.b — Noise Settings Assets.
- Prior resolution: Phase N5.a — Base Shape Selector.
- Prior resolution: Phase F3b — Height-Coherent Hills (Clean Break).
- Prior resolution: Phase N4 — Noise Settings Infrastructure + F2 Noise Upgrade.
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
- `MapLayerId.Paths` write ownership confirmed: Phase O.
- Unity version target for `TilemapCollider2D.usedByComposite` deprecation: upgrade to
  `compositeOperation` if targeting Unity 2022.2+ exclusively (currently suppressed with `#pragma warning disable CS0618`).
- **Hills threshold UX:** ~~Roadmapped as Phase N5.e — relative parameterization.~~
  **Resolved by N5.e.** Hills threshold sliders reparameterized from raw Height [0,1] to
  relative fractions [0,1]. `hillsL1` = fraction of land height range; `hillsL2` = fraction
  of remaining range above L1. Remap computed in `MapTunables2D` constructor. Full slider
  ranges are now usable. See changelog for details.
- **Phase V** — scope defined: Runtime Inspection UI (V.a hover tooltip, V.b per-cell
  overlay system). Planning only; design doc `Phase_V_Design.md` to be written when
  phase activates after M2.b. See `PCG_Roadmap.md` Phase V section for full scope.

## Noted desired features (not yet roadmapped as phases)
- **Extended noise type palette (post-N4 observation):** ~~The noise runtime supports several
  additional types not yet exposed in TerrainNoiseType.~~ **Resolved by N5.c.** All
  Worley metric × function combinations (12 total) are accessible via the parameterized
  `Worley` enum entry + `WorleyDistanceMetric` / `WorleyFunction` struct fields.
  CellAsIslands + SmoothEuclidean is available for archipelago generation (Phase J).
  Ridged multifractal is implemented in the noise runtime for all noise types.

## Visualization Maintenance Policy
`PCGMapTilemapVisualization` is the primary testing surface. New tunables are wired into
it during each phase implementation.

`PCGMapVisualization` (GPU lantern) and `PCGMapCompositeVisualization` (Texture2D
composite) are frozen at their current state (N5.d-complete feature set). They received
compile-fix field renames in N5.e (hillsThresholdL1/L2 → hillsL1/L2) but no new feature
wiring. They are updated at milestone boundaries only (e.g., after H8, after Phase M) via
a single catch-up batch that wires all accumulated tunables. If a specific debugging need
requires one of these components before the next milestone, the specific field is wired on
demand.

This policy reduces per-phase touchpoints from 10 files to 7 and from 3 visualization
components to 1. The lantern and composite remain functional for all tunables up to and
including N5.d defaults.

## Immediate next focus
M2.b complete and golden-captured. Next: **Phase L** — Hydrology (Priority-Flood → D8
→ flow accumulation → river mask + lake CCA). Design complete in `Phase_L_Design.md`.

Confirmed next implementation sequence (toward Phase W):
1. **Phase L** — Hydrology.
2. **Phase P** — Pipeline Validation / World Rejection (recommended before W; can land any time after M).

Deferred / optional: H8b, T1, J, K, P (as before).

Long-term target: **Phase W**. Minimum path: M → W. Enriched: M → M2 → L → P → W.

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
