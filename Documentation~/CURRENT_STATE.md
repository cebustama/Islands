# Current State

Status date: 2026-04-06

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–F6 + F4b + F4c + Phase G + Phase H + Phase H1 + Phase H2 + Phase H2b + Phase H2c + Phase H2d + Phase H3 + Phase H4 + Phase H5 + Phase H6 + Phase H7**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- Phase H6 — Rule Tiles implemented.
  `TilesetConfig.LayerEntry` extended with `ruleTile` field. Priority: ruleTile > animatedTile > tile.
  `ComputeTilesetConfigHash()` extended. RuleTile assets support Animation output per-rule.
  Pure adapter/sample-side.
- Phase F4b — Shore Depth Tunable implemented.
  `Stage_Shore2D` gains `ShallowWaterDepth01` field. Height-based ShallowWater classification.
  Adjacency ring always included. New Inspector slider + MapGenerationPreset field.
- Phase F4c — Mid-Water Layer implemented.
  New `MapLayerId.MidWater = 12` (COUNT 12 → 13). `Stage_Shore2D` gains `MidWaterDepth01`
  field; writes MidWater when > 0. 3-band water: Shallow → Mid → Deep. `MidWater ∩ ShallowWater == ∅`.
  MidWater added to TilesetConfig priority, base layers, collider layers, procedural palettes.
  Default 0 = no MidWater layer, no golden hash changes.
- Prior resolution (Phase H7): Map Navigation Sample implemented and smoke-tested.
  - **New `MapPlayerController2D`** (`Runtime/PCG/Samples/PCG Map Tilemap/MapPlayerController2D.cs`):
    `Rigidbody2D`-based 4-directional movement (Dynamic, GravityScale=0, FreezeRotation Z).
    `Update` reads `Input.GetAxisRaw`; `FixedUpdate` sets `rb.linearVelocity` directly.
    Inspector: `moveSpeed` (default 5f), optional `SpriteRenderer` for horizontal flip,
    optional `frontSprite`/`backSprite` for North/South facing swap. `CircleCollider2D`
    (radius 0.3) for smooth wall sliding.
  - **New `CameraFollow2D`** (`Runtime/PCG/Samples/PCG Map Tilemap/CameraFollow2D.cs`):
    `LateUpdate` + `Vector3.SmoothDamp` follow. No Cinemachine dependency.
  - **Scene setup**: physics layers (`Player` ↔ `MapCollider`), Collider Tilemap on
    `MapCollider` layer (renderer disabled), Overlay Tilemap for vegetation/hills.
    Documented in `reference/map-tilemap-scene-setup.md`.
  - **Smoke test passed** (7/7): walk on land, blocked by water, blocked by mountain,
    diagonal wall slide, camera follow, seed change, facing flip.
  - Pure sample-side. No new MapLayerId, MapFieldId, or runtime stage contracts.
    Adapters-last invariant preserved.
- Prior resolution (Phase H5): Multi-layer Tilemap & Collider Integration.
  `TilemapLayerGroup`, `TilemapAdapter2D.ApplyLayered`, `SetupCollider`.
  Layer partition: Base, Overlay, Collider. 4 new EditMode tests (total 11).
- Prior resolution (Phase H4): `TilesetConfig.LayerEntry` extended with `animatedTile` field.
  `ToLayerEntries()` priority updated. `ComputeTilesetConfigHash()` extended. 3 new tests green
  (`TilesetConfigTests` total: 17). Pure adapter/sample-side.
- Prior resolution (Phase H3): `MapGenerationPreset` SO + `TilesetConfig` SO.
  Four components patched with override-at-resolve preset slot. Two new asmdefs:
  Islands.PCG.Samples.Shared + Islands.PCG.Samples. 12 EditMode tests green.
- Prior resolution (Phase H2d): `ProceduralTileEntry` + `ProceduralTileFactory`. 13 EditMode tests green.
- Prior resolution (Phase H2c): `PCGMapTilemapVisualization` `[ExecuteAlways]` MonoBehaviour.
- Prior resolution (Phase H2b): `TilemapAdapter2D` + `TilemapLayerEntry` + `PCGMapTilemapSample`. 10 tests.
- Prior resolution (Phase H2): `MapDataExport` + `MapExporter2D`. 14 tests.
- Prior resolution (Phase H1): `PCGMapCompositeVisualization` (sample-side).
- Prior resolution (Phase H): `PCGMapVisualization` patched with `PCGViewMode` enum + scalar field view.
- Prior resolution (Phase F2c): `MapShapeInput` struct; `MapInputs` extended. F2c goldens locked.
- Prior resolution (Phase F2b): `Stage_BaseTerrain2D` reformed: ellipse + domain-warp silhouette.
  Two new `MapTunables2D` fields. All F2–G golden hashes re-locked.
- Prior resolution (Phase G): `MaskMorphologyOps2D`; `Stage_Morphology2D` → `LandCore`, `CoastDist`.

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
- Scalar field normalization range (scalarMin/scalarMax) is inspector-settable but not auto-ranged.
- Unity version target for `TilemapCollider2D.usedByComposite` deprecation: upgrade to
  `compositeOperation` if targeting Unity 2022.2+ exclusively (currently suppressed with `#pragma warning disable CS0618`).

## Immediate next focus
Phase H arc is fully complete (H–H7 all done, including H6 Rule Tiles).
Phases F4b (Shore Depth) and F4c (Mid-Water Layer) done.
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
