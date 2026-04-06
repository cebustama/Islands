# Current State

Status date: 2026-04-06

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–F6 + F4b + F4c + Phase G + Phase H + Phase H1 + Phase H2 + Phase H2b + Phase H2c + Phase H2d + Phase H3 + Phase H4 + Phase H5 + Phase H6 + Phase H7 + Phase J2 + Phase N2**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- Phase J2 — Height Redistribution implemented.
  `MapTunables2D.heightRedistributionExponent` (default 1.0, clamped [0.5, 4.0]).
  Applied as `pow(height01, exponent)` inside `Stage_BaseTerrain2D` after quantization,
  before Land threshold. Guarded with `!= 1.0f` for zero-cost default path.
  `BaseTerrainStage_Configurable` consolidated from three duplicate nested classes into
  a single shared class at `Runtime/PCG/Samples/Presets/`.
- Phase N2 — Spline Remapping implemented.
  New `ScalarSpline` readonly struct (`Runtime/PCG/Fields/ScalarSpline.cs`): piecewise-linear
  evaluation, immutable, `IsIdentity` fast-path, `FromAnimationCurve` bridge factory.
  `MapTunables2D.heightRemapSpline` field. `MapGenerationPreset.heightRemapCurve`
  (`AnimationCurve`) bridged via `ScalarSpline.FromAnimationCurve` in `ToTunables()`.
  Applied in `Stage_BaseTerrain2D` after pow() redistribution, before Land threshold.
  Coexists with J2 pow() (pow first, spline second). 22 new EditMode tests.
  Identity default preserves all existing golden hashes.
  `PCGMapTilemapVisualization` refactored: priority table removed, scalar field overlay
  added, custom Editor hides preset-controlled fields, Inspector matches preset layout.
  New `Islands.PCG.Editor` asmdef at `Editor/Inspectors/`.
- Prior resolution (Phase H6): Rule Tiles. `TilesetConfig.LayerEntry` extended with `ruleTile` field.
- Prior resolution (Phase F4b): Shore Depth Tunable. `Stage_Shore2D.ShallowWaterDepth01`.
- Prior resolution (Phase F4c): Mid-Water Layer. `MapLayerId.MidWater = 12`.
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
- N3 (Ridged Multifractal) design decision A/B/C still open — deferred to Phase K or Phase W.
- Unity version target for `TilemapCollider2D.usedByComposite` deprecation: upgrade to
  `compositeOperation` if targeting Unity 2022.2+ exclusively (currently suppressed with `#pragma warning disable CS0618`).

## Immediate next focus
Phase H arc is fully complete (H–H7 all done, including H6 Rule Tiles).
Phases F4b (Shore Depth) and F4c (Mid-Water Layer) done.
Phase J2 (Height Redistribution) done. Phase N2 (Spline Remapping) done.
Noise Composition Improvements N1 and N2 complete; N3 (Ridged Multifractal) deferred to
Phase K or Phase W (design decision A/B/C still open).

Next implementation phase to be selected. Candidates by roadmap order:
- Phase H8 — Mega-Tiles (2×2 large terrain sprites)
- Phase I — Burst / SIMD upgrades
- Phase J — Region Generation (static Voronoi)
- Phase L — Hydrology (Rivers, Lakes)
- Phase M — Climate + Biome
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
