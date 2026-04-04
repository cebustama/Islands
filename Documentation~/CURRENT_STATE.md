# Current State

Status date: 2026-04-04

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–F6 + Phase G + Phase H + Phase H1 + Phase H2 + Phase H2b + Phase H2c + Phase H2d + Phase H3 + Phase H4**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- Phase H4 — Animated Tiles implemented and smoke-test verified.
  - `TilesetConfig.LayerEntry` (`Runtime/PCG/Adapters/Tilemap/TilesetConfig.cs`) extended with
    optional `animatedTile` (`TileBase`) field. `ToLayerEntries()` priority updated:
    `enabled + animatedTile → animatedTile`; `enabled + tile → tile`; both-null or disabled → null.
    Fully backward compatible; existing `.asset` files unaffected (new field defaults to null).
  - `PCGMapTilemapVisualization.ComputeTilesetConfigHash()` extended to include `animatedTile`
    InstanceID per entry in the FNV-1a loop. Inspector edits to animated tile slots now trigger
    real-time rebuild on the same frame, matching static tile and enabled-toggle behavior.
  - No asmdef changes: `animatedTile` is typed as `TileBase` (engine base type); `AnimatedTile`
    from 2D Tilemap Extras is assigned to the slot in the Inspector at edit time.
  - 3 new EditMode tests green (`TilesetConfigTests` total: 17):
    `DefaultLayers_AllAnimatedTilesNull`,
    `ToLayerEntries_AnimatedTileWinsOverStaticTile`,
    `ToLayerEntries_AnimatedTileNull_FallsBackToStaticTile`.
    All pre-existing tests unchanged.
  - Pure adapter/sample-side. No new MapLayerId, MapFieldId, or runtime stage contracts.
    Adapters-last invariant preserved.
- Prior resolution (Phase H3): `MapGenerationPreset` SO + `TilesetConfig` SO.
  Four components patched with override-at-resolve preset slot. Two new asmdefs:
  Islands.PCG.Samples.Shared + Islands.PCG.Samples. 12 EditMode tests green.
  H3 post-merge fixes: null-means-skip in ToLayerEntries(); ComputeTilesetConfigHash() added;
  explicit LayerEntry.layerId field; visual priority order in BuildDefaultLayers().
- Prior resolution (Phase H2d): `ProceduralTileEntry` + `ProceduralTileFactory` (white-sprite cache,
  `BuildPriorityTable`, `ClearCache`). `PCGMapTilemapVisualization` patched: `useProceduralTiles`
  toggle, `ProceduralTileEntry[] proceduralColorTable`, `proceduralFallbackColor`, FNV-1a dirty hash,
  three `[ContextMenu]` palette presets (Classic/Prototyping/Twilight). 13 EditMode tests green.
  Smoke tests passed. Adapters-last preserved.
- Prior resolution (Phase H2c): `PCGMapTilemapVisualization` `[ExecuteAlways]` MonoBehaviour.
  Dirty tracking on all tunables; `MapContext2D` kept `Persistent`; FNV-1a priority table hash;
  console hash log (seed + tilesStamped). `Islands.PCG.Adapters.Tilemap.asmdef` gains
  `Unity.Mathematics`. No new runtime contracts.
- Prior resolution (Phase H2b): Tilemap Adapter implemented and test-gated (10 tests green).
  `TilemapAdapter2D` static class, `TilemapLayerEntry` serializable struct, `PCGMapTilemapSample`
  MonoBehaviour. Separate `Islands.PCG.Adapters.Tilemap.asmdef`.
- Prior resolution (Phase H2): `MapDataExport` and `MapExporter2D` implemented and test-gated
  (14 tests). Managed snapshot of `MapContext2D`; adapters-last; deterministic.
- Prior resolution (Phase H1): `PCGMapCompositeVisualization` (sample-side). CPU Texture2D
  composite of all active layers via fixed priority table. `CompositeLayerSlot` struct.
  Optional scalar overlay (Height / CoastDist). No new MapLayerId/MapFieldId.
- Prior resolution (Phase H): `PCGMapVisualization` patched with `PCGViewMode` enum; scalar
  field view mode (Height/CoastDist); per-layer preset ON colors. No new MapLayerId/MapFieldId.
- Prior resolution (Phase F2c): Arbitrary Shape Input implemented and test-gated.
  `MapShapeInput` struct; `MapInputs` extended (backward compatible). `Stage_BaseTerrain2D`
  branches on `HasShape`. F2b goldens unchanged. F2c goldens locked.
- Prior resolution (Phase F2b): `Stage_BaseTerrain2D` reformed: ellipse + domain-warp silhouette.
  Two new `MapTunables2D` fields: `islandAspectRatio`, `warpAmplitude01`.
  All F2–G golden hashes re-locked. Phase G goldens locked for first time.
- Prior resolution (Phase G): `MaskMorphologyOps2D`; `Stage_Morphology2D` produces `LandCore`
  and `CoastDist`. `MapLayerId.LandCore = 11` (COUNT → 12), `MapFieldId.CoastDist = 2` (COUNT → 3).

## What the roadmap redesign pass resolved (2026-04-02)
- Phase F2b added to roadmap and immediately implemented: organic island shape reform (ellipse + domain warp).
- Phase F2c — Arbitrary Shape Input: implemented and test-gated. `MapShapeInput` companion struct;
  optional shape-input branch in `Stage_BaseTerrain2D`; F2b path and goldens unchanged.
- Archipelago support intent explicitly noted under Phase J (Voronoi regions) and Phase K (Plate Tectonics) as Level 3 of the island shape vision.
- Phase H2 — Data Export / Map Adapters added to roadmap after Phase H.
  Phase H2 is the natural completion of the "Adapters" half of Phase H's original intent;
  inserted before Phase I so game integration can begin without waiting for the full pipeline.

## What is not settled yet
- No unresolved migration batch remains for the reviewed snapshot corpus.
- Open design questions recorded in the roadmap: river representation (mask vs. flow-accumulation field), lake modeling (distinct layer vs. enclosed ShallowWater), biome output format.
- `MapFieldId.Moisture` write ownership confirmed: Phase M.
- `MapLayerId.Paths` write ownership confirmed: Phase O.
- Scalar field normalization range (scalarMin/scalarMax) is inspector-settable but not auto-ranged; CoastDist range varies by map size and CoastDistMax tunable.

## Immediate next focus
Phase H5 — Multi-layer Tilemap & Collider Integration.
Adapter-side extension separating pipeline layers across multiple stacked Unity Tilemaps on the
same Grid, enabling transparency overlays and dedicated physics layers:
- Base layer (opaque): DeepWater / ShallowWater / Land / LandCore / LandEdge
- Overlay layer (transparency): Vegetation / HillsL1 / HillsL2 / Stairs
- Collider layer (invisible, physics only): non-walkable cells (HillsL2 + water)
- `TilemapAdapter2D` new `ApplyLayered()` overload accepting a layer-group-to-Tilemap descriptor.
- Auto-setup `TilemapCollider2D` + `CompositeCollider2D` on the collider Tilemap.
- Pure adapter/sample-side. No new runtime contracts or MapLayerId/MapFieldId entries.
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
