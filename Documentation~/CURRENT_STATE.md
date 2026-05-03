# Current State

Status date: 2026-04-26 (Phase V.b complete — Phase V done)

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–N6 + Phase M + M-fix.a/c + M2.a + M2.b + Phase L + L→M + L-fix.a (revised) + Phase V (read-only inspection tooling: V.a + V.b) (all golden-captured where applicable)**.
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- **Phase V.a — Runtime Hover Tooltip + IMapContextSource interface.**
  Read-only inspection tooling. New `Islands.PCG.Inspection` asmdef hosts the
  `IMapContextSource` interface (5-member contract: `Context`, `Tilemap`, `FlipY`,
  `RegenerationVersion`, `TryWorldToCell`) and the `PCGHoverTooltip` MonoBehaviour
  (TMP-based, auto-canvas, [ExecuteAlways], pull-based refresh on
  `RegenerationVersion` change). All three viz classes
  (`PCGMapTilemapVisualization`, `PCGMapCompositeVisualization`, `PCGMapVisualization`)
  implement `IMapContextSource`; the two non-tilemap variants return Tilemap=null
  and TryWorldToCell=false so V.a no-ops gracefully against them. New sample-side
  free-cam (`MapCameraController2D`) added to `PCG Map Tilemap` scene as smoke-test
  rig — also consumes `IMapContextSource` for auto-framing on map bounds.
  No new `MapLayerId`, `MapFieldId`, stage, or pipeline change. No golden break.
  No determinism gate. No SSoT promotion (Phase V is planning-authority only).
  4 unit tests in `IMapContextSourceTryWorldToCellTests.cs` (round-trip flipY=true/false,
  out-of-bounds rejection, regen monotonicity). Smoke test §10 acceptance: green.
  3 asmdef edits added `Islands.PCG.Inspection` reference (Adapters.Tilemap, Samples,
  Tests.EditMode).
  See `planning/active/design/Phase_V_Design.md` for design contracts.
  V.a smoke surfaced and validated the L-fix.a routing patch below as concrete
  value-of-tooling evidence.
- **Phase V.b — Per-Cell Overlay System. DONE.**
  `PCGRuntimeOverlay` MonoBehaviour with two independent display modes:
  - **Color overlay:** per-cell discrete color from `BiomeColorPalette` SO (Biome
    field) or deterministic FNV-1a hash-color (BiomeRegionId). Uses an owned
    `ScalarOverlayRenderer` instance via `SetDataDirect` (promoted to public in
    `Adapters.Tilemap`).
  - **Text overlay:** per-cell numeric label for pipeline fields (Height, CoastDist,
    Moisture, Temperature, Biome, BiomeRegionId, FlowAccumulation). World-space
    Canvas + TextMeshProUGUI for URP 2D compatibility (TextMeshPro 3D MeshRenderer
    is invisible under URP 2D Renderer — see V-DD-5 implementation note in
    `Phase_V_Design.md`). View-aware: only emits glyphs for the camera-visible cell
    rect. Hard cap 64×64 visible cells (vertex budget guard). α decision: noise/
    derived preview sources log once per regen and render nothing.
  Files: `PCGRuntimeOverlay.cs` (Inspection), `BiomeColorPalette.cs` (Inspection),
  `BiomeColorPaletteTests.cs` (Tests.EditMode), `ScalarOverlayRenderer.cs`
  (Adapters.Tilemap, modified — `SetDataDirect` added).
  `BiomeColorPalette-Default.asset` created via Populate Defaults context menu.
  `PCGRuntimeOverlay` lives in `Islands.PCG.Inspection` namespace.
  `ScalarOverlayRenderer` moved from internal to public in `Adapters.Tilemap`.
  `Adapters.Tilemap.asmdef` gained `Unity.TextMeshPro` reference.
  No new `MapLayerId`, `MapFieldId`, stage, or pipeline change. No golden break.
  No determinism gate. No SSoT promotion.
  Smoke tests §11.2 (text overlay) and §11.3 (color overlay): all green.
- **L-fix.a (revised) — Multi-layer routing partitions for Rivers and Lakes.**
  `PCGMapTilemapVisualization.StampMultiLayer()` routes layers through three static
  arrays (`s_baseLayers`, `s_overlayLayers`, `s_colliderLayers`) when
  `enableMultiLayer = true`. Rivers and Lakes were absent from all three after Phase
  L shipped, so they were silently dropped in multi-layer mode regardless of
  `proceduralColorTable` or `TilesetConfig` configuration. A previously documented
  L-fix.a entry described an alternative routing (Rivers as overlay, Lakes as base)
  but was never committed. Surfaced again by V.a smoke testing 2026-04-15 — tooltip
  reported `Rivers` set on cells where no river tile rendered.
  **Implemented fix:** `MapLayerId.Lakes` and `MapLayerId.Rivers` appended to
  `s_baseLayers` in that order (Rivers wins on confluence); `MapLayerId.Lakes`
  added to `s_colliderLayers` (lakes block movement; rivers remain passable by
  design). No golden break. No new stages, fields, or layers.
  **Maintenance rule (re-affirmed):** any future phase that adds a new `MapLayerId`
  must classify it against all three partition arrays in
  `PCGMapTilemapVisualization` or the layer will be silently invisible and/or
  non-collidable in multi-layer mode. See Visualization Maintenance Policy below.
  1 file modified: `PCGMapTilemapVisualization.cs`.
- **Vegetation overlap with Rivers and high mountain river-source visibility — by
  design, art-side concern.** Phase L added Rivers/Lakes after Stage_Vegetation was
  written; Stage_Vegetation does not exclude Rivers/Lakes from its eligibility set
  (M2a-1..4 contracts unchanged). Cells with both `Vegetation` and `Rivers` set are
  expected and represent fertile river valley ecology — vegetation tile sprites
  should use alpha-channel transparency around grass tufts so the underlying river
  tile shows through. Same pattern for high-elevation river sources where hill
  sprites occlude river origins on a separate overlay tilemap — handled via sprite
  alpha rather than a pipeline-stage or routing change. No `Stage_Vegetation`
  contract modification. No golden change.
- **Phase L — Hydrology (Priority-Flood → D8 → FlowAccumulation → Rivers + Lakes).** All golden-captured.
  New registry entries: `MapLayerId.Rivers = 13`, `MapLayerId.Lakes = 14` (COUNT → 15);
  `MapFieldId.FlowAccumulation = 6` (COUNT → 7).
  New operator: `HeightFieldHydrologyOps2D` — 4 static deterministic methods:
  `FillDepressions` (Priority-Flood+ε, SortedSet min-heap, coastal-cell seed),
  `ComputeFlowDirectionsD8` (steepest 8-neighbor, water-neighbors at h=0),
  `AccumulateFlow` (descending-height sort, downstream propagation),
  `ExtractRivers` (fractional-of-land-cells threshold, resolution-auto-scaling).
  New stage: `Stage_Hydrology2D` — runs L.1 (river gen) and L.2 (lake detection via
  three-way boolean exclusion; optional BFS size filter). Zero RNG consumption.
  Stage-local tunables: `epsilon = 1e-5f`, `riverThresholdFraction = 0.02f`,
  `minLakeArea = 0` (filter disabled). Pipeline position: after Morphology, before Biome.
  Invariants L-1..L-10 test-gated. Goldens captured: `StageHydrology2DTests.cs`,
  `MapPipelineRunner2DGoldenLTests.cs`.
  New files: `HeightFieldHydrologyOps2D.cs`, `Stage_Hydrology2D.cs`,
  `HeightFieldHydrologyOps2DTests.cs`, `StageHydrology2DTests.cs`,
  `MapPipelineRunner2DGoldenLTests.cs`.
  Adapter updates: `MapIds2D.cs`, `ScalarOverlaySource.cs` (FlowAccumulation=6),
  `PCGMapVisualization.cs` (Rivers/Lakes colors, enableHydrologyStage, stage arrays),
  `PCGMapTilemapVisualization.cs` (enableHydrologyStage, 5 tunables, stagesL/LM/LM2a/LM2b),
  `PCGMapTilemapVisualizationEditor.cs` (hydrology fields conditional),
  `MapGenerationPreset.cs` (biomeRiverMoistureBonus, biomeRiverFlowNorm),
  `TilesetConfig.cs` (Rivers+Lakes in priority order; ToLayerEntries() robust against
  13-entry legacy assets; migration context menu).
- **L→M integration — FlowAccumulation moisture enrichment in Stage_Biome2D.**
  `riverMoistureBonus = 0.4f` and `riverFlowNorm = 0f` (auto) promoted from commented
  stubs to active stage-local tunables. `hasFlowAccum = ctx.IsFieldCreated(FlowAccumulation)`
  guard makes enrichment strictly optional: when Phase L is absent, `riverFactor = 0`
  and output is bit-identical to the pre-L baseline. Existing M / M2a / M2b goldens
  NOT broken. New goldens captured: `MapPipelineRunner2DGoldenLMTests.cs`.
  1 file modified: `Stage_Biome2D.cs`.
- Prior resolution: M2.b — Contiguous Region Detection + Naming.
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
- Prior resolution: M-fix.a + M-fix.c — Biome Tunables Inspector Wiring + Moisture Default Tuning.
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
- `MapLayerId.Paths` write ownership confirmed: Phase O.
- Unity version target for `TilemapCollider2D.usedByComposite` deprecation: upgrade to
  `compositeOperation` if targeting Unity 2022.2+ exclusively (currently suppressed with `#pragma warning disable CS0618`).
- **Phase V** — V.a implemented (read-only inspection tooling — IMapContextSource
  interface + PCGHoverTooltip + 3 viz-class impls + free-cam smoke rig). V.b
  (per-cell text + discrete color overlay system) remains design-complete, planning
  only. See `planning/active/design/Phase_V_Design.md` §6 for V.b contracts.
- **Phase Q** — scope defined: Biome-Conditional Tile Selection. Pure adapter-side
  consumer of `MapFieldId.Biome`. Closes the documented but unimplemented gap between
  Phase M (biomes produced) and `TilesetConfig` (biomes ignored). Planning only; design
  doc to be written when phase activates. See `PCG_Roadmap.md` Phase Q section.
- **Phase Q2** — scope defined: Composite-Condition Tile Selection. Pure adapter-side
  consumer of multi-`MapLayerId` boolean composition (e.g. waterfalls = `Rivers ∧
  HillsL2`, bridges = `Rivers ∧ Paths`, fords, cliffs, wetlands). Mirrors Phase H8
  `MegaTileRule` SO pattern with a new `CompositeTileRule` ScriptableObject system.
  Sibling of Phase Q (independent of, can ship in either order; share architecture
  flavor). Surfaced during V.a smoke testing 2026-04-15 from a discussion of
  river-on-mountain visibility. Planning only; design doc to be written when phase
  activates. See `PCG_Roadmap.md` Phase Q2 section.
- **TilesetConfig .asset migration:** existing `TilesetConfig-8bit` and
  `TilesetConfig-DragonWarrior` assets have 13-entry `layers` arrays. Use the
  "Migrate to Phase L (add Rivers + Lakes)" context menu on each asset to extend
  to 15 entries. Until migrated, `ToLayerEntries()` returns a valid 15-entry result
  (via keyed lookup) but logs a warning and Rivers/Lakes tiles remain unassigned.
- **`beachMinTemperature`** lives on BiomeTable as static readonly, not on Stage_Biome2D —
  not yet Inspector-tunable.
- **D8 grid artifacts:** diagonal-preference in flow direction can produce visible
  horizontal/vertical river artifacts on flat terrain. Rho8 (stochastic D8) would
  mitigate this but requires RNG, violating the no-RNG invariant. Deferred to a
  potential Phase L2 refinement.

## Noted desired features (not yet roadmapped as phases)
- **Extended noise type palette (post-N4 observation):** Resolved by N5.c. All
  Worley metric × function combinations (12 total) are accessible via the parameterized
  `Worley` enum entry + `WorleyDistanceMetric` / `WorleyFunction` struct fields.
  CellAsIslands + SmoothEuclidean is available for archipelago generation (Phase J).
  Ridged multifractal is implemented in the noise runtime for all noise types.
- **Phase L2 extensions (identified but not roadmapped):** Strahler ordering,
  streams/brooks secondary threshold, river inlet/outlet modeling for lakes,
  lake depth field. None required for Phase W.
- **Local-zoom detail features:** Candidate features derived from existing
  pipeline outputs whose visible effect is bounded to zoom scale. PCG-side
  contribution is intentionally minimal; dynamic / runtime aspects live outside
  the pipeline.
  - *Intertidal band (mareas)* — static PCG mask in `[waterThreshold ±
    tidalAmplitude]` derived from Height. Dynamic sea-level oscillation
    (time-of-day tides) is runtime/adapter concern, out of PCG scope. Open
    decision: PCG-side mask emission (new `MapLayerId.TidalBand`) vs. pure
    runtime derivation from existing Height + waterThreshold.
  - *Potholes / marmitas fluviales* — use case for Phase Q2
    (`CompositeTileRule`): `Rivers ∧ HighFlow ∧ drop-proximity`. Prerequisite
    candidate: slope/gradient field (possible Phase L2 addition, not required).
- **Fractal coastline / fjord refinement (exploratory):** Mandelbrot/Julia
  iteration or other fractal techniques (IFS, midpoint displacement, ridged
  multifractal) as candidate generators for high-detail coastlines and
  fjord-like inlets. Overlaps conceptually with three existing items: Phase N3
  (ridged multifractal, PARTIAL — the standard PCG analogue for fjord ridges);
  erosion simulation (tier 3 NONE in `technique_integration_matrix.md` — matches
  real glacial fjord formation); Phase F2c (arbitrary shape input — candidate
  integration point). Open decisions: fractal family (Mandelbrot boundaries are
  mathematically distinctive but geologically artificial; ridged multifractal +
  erosion is geologically grounded); pipeline placement (F2b shape-mask level
  vs. F4-adjacent coastline post-refinement); scope (global silhouette
  candidate for `IslandShapeMode` extension vs. local-zoom detail only).
- **Atmospheric / weather features (exploratory):** Umbrella for four distinct
  sub-problems commonly conflated as "PCG clouds":
  - *Weather sim as static PCG output* — multi-pass simulation on Height +
    CoastDist + (new) Wind emitting averaged `CloudCover` / `Rainfall` fields
    after N iterations. Reference pattern: Nick McDonald 2018
    (nickmcd.me/2018/07/10/procedural-weather-patterns). Candidate extension
    of Phase M.2 (Moisture) or new stage post-M. Requires new `MapFieldId.Wind`
    contract decision. Strongest PCG fit.
  - *Dynamic weather sim (runtime)* — same simulation but as runtime loop.
    Consumes PCG outputs; emits none. Out of PCG scope.
  - *Cloud shadow mask* — parallel to intertidal band question: static PCG
    mask vs. runtime-dynamic flip. Gameplay signal for stealth / lighting /
    local moisture.
  - *Cloud sprite generation* — pure adapter/visual concern (fBm on sprite
    texture). Out of PCG scope.
  Open decisions: F-family placement (extend F4 Climate & Biomes vs. new F9
  Atmospheric Fields); Wind field contract (vector vs. magnitude+angle);
  whether wind requires Phase W (world Y-axis for latitude-driven circulation)
  or can operate locally. Comparators: (1) Dwarf Fortress — worldgen rainfall as static field, weather as separate runtime layer with E-W wind by latitude and three cloud layers; includes orographic rainshadow pass at worldgen. (2) RimWorld — minimal baseline: temperature and rainfall are independent Perlin×latitude fields (no wind field, no rainshadow at worldgen); local terrain gen ignores climate scalars entirely; runtime weather consumes them as event-sampling probability. Both validate the static-worldgen → runtime-probability split this umbrella proposes; RW establishes the lower-complexity bound, DF the upper.

## Visualization Maintenance Policy
`PCGMapTilemapVisualization` is the primary testing surface. New tunables are wired into
it during each phase implementation.

`PCGMapVisualization` (GPU lantern) and `PCGMapCompositeVisualization` (Texture2D
composite) are frozen at their current state. They are updated at milestone boundaries
only via a single catch-up batch. If a specific debugging need requires one of these
components before the next milestone, the specific field is wired on demand.

**Multi-layer stamping maintenance rule (established by L-fix.a, re-affirmed
2026-04-15 after V.a smoke):** `PCGMapTilemapVisualization` uses three hardcoded
static arrays to route layers in multi-layer mode: `s_baseLayers` (base tilemap,
terrain surfaces), `s_overlayLayers` (overlay tilemap, decoration features), and
`s_colliderLayers` (gameplay collision surface — layers that block movement).
Any new `MapLayerId` added by a future phase MUST be classified against all three
arrays or the layer will be silently invisible and/or non-collidable when
`enableMultiLayer = true`. **Phase V.a's hover tooltip is the canonical debugging
surface for this drift** — it iterates ALL `MapLayerId`s directly (V-DD-10),
deliberately ignoring the partition arrays, so a routing omission appears as
"tooltip says layer X is set, tilemap renders nothing." Canonical grouping after
L-fix.a (revised):
- `s_baseLayers`: terrain surfaces drawn on the base tilemap, in render order
  (later entries paint over earlier) — DeepWater, MidWater, ShallowWater, Land,
  LandCore, LandEdge, Lakes, Rivers
- `s_overlayLayers`: decoration features painted on a separate overlay tilemap
  above the base — Vegetation, HillsL1, HillsL2, Stairs
- `s_colliderLayers`: layers that block movement on the collider tilemap —
  DeepWater, MidWater, HillsL2, Lakes

## Immediate next focus
Phase V complete (V.a + V.b), all smoke-validated. V.a value-proven (surfaced
and validated the L-fix.a routing partition omission). V.b resolves the M2.a
"biome IDs as continuous gradient is wrong" problem with discrete color overlay
and adds per-cell text inspection for all pipeline fields.

**Next batch:** Resume toward **Phase W** (world-to-local architecture). The
previously documented Phase P → Phase W sequencing is paused (not cancelled).
Adapter-track phases (T1, Q, Q2) remain independent and can be picked up at any
time. Optional V.c quality-of-life items (e.g., `textMinCellScreenSize` readability
threshold for text overlay) are logged but not blocking.

Long-term target remains **Phase W**. The previously documented Phase P → Phase W
sequencing is paused (not cancelled) pending Phase V completion.

Deferred / optional: H8b, T1, J, K, P, Q, Q2, W.

Minimum path to W: M → W. Enriched path: M → M2 → L → V → P → W.
Adapter-side enrichment (independent of W path): Q (biome-conditional tiles), Q2 (composite-condition tiles).

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
