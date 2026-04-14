# Changelog — SSoT

## Phase N6 — Noise Preview Visualization
Date: 2026-04-09

### What changed
- **Scalar overlay system on `PCGMapTilemapVisualization` replaced with Texture2D +
  SpriteRenderer approach.** Two independent overlay slots replace the single-slot heatmap
  tilemap + per-cell tint system. Each overlay renders as a `Texture2D` displayed via a
  child `SpriteRenderer`, aligned pixel-to-cell with the base tilemap.
- **New `ScalarOverlaySource` enum** with 7 data sources:
  - Pipeline fields: `Height`, `CoastDist`, `Moisture` (read from `MapContext2D`).
  - Noise previews: `TerrainNoise`, `WarpNoiseX`, `WarpNoiseY`, `HillsNoise` (computed
    on-demand via `MapNoiseBridge2D.FillNoise01` with exact stage-matching salts).
  - Gap in numbering (3–9) reserves space for future Phase M pipeline fields.
- **New `ScalarOverlayRenderer` helper class** manages the child GameObject, SpriteRenderer,
  Texture2D, Sprite, and Color32 buffer for one overlay. `FilterMode.Point` for crisp cell
  edges. `HideFlags.DontSave | HideFlags.NotEditable` prevents scene serialization clutter.
  Alignment reads `tilemap.layoutGrid.cellSize` and scales the sprite transform.
- **Two overlay slots** in the Inspector, each with: enable toggle, `ScalarOverlaySource`
  dropdown, min/max range, color low/high ramp, and alpha slider. Auto-applies sensible
  min/max defaults when the source changes (CoastDist: −1 to 20; all others: 0 to 1).
- **Performance improvement:** one `tex.Apply()` per overlay per rebuild replaces 65,536
  `SetTile()` calls at 256×256 resolution. This is a bulk memory upload vs. N² individual
  Unity API calls.
- **Noise preview salts match governed stages exactly:**
  `TerrainNoise = 0xF2A10001u`, `WarpNoiseX = 0xF2A20002u`, `WarpNoiseY = 0xF2A30003u`,
  `HillsNoise = 0xF3D50001u`. Noise settings read from resolved `MapTunables2D`, guaranteeing
  the preview shows the exact same noise the pipeline consumed.
- **Legacy overlay paths fully removed:** `ApplyScalarOverlay`, `ApplyScalarHeatmapTilemap`,
  `ApplyScalarOverlayTint`, `ResetTintColors`, `SetHeatmapRendererAlpha`,
  `ApplyOverlayFieldDefaults`, `HeatmapPaletteSteps`, `_overlayWasApplied`.
- **Custom Editor updated** with two conditional overlay sections (fields expand on enable).

### Serialization break
Fields removed from `PCGMapTilemapVisualization`: `enableScalarOverlay`, `overlayField`
(MapFieldId), `overlayMin`, `overlayMax`, `overlayColorLow`, `overlayColorHigh`,
`scalarHeatmapTilemap`, `heatmapAlpha`. New overlay fields start disabled with defaults.
Sample-side only — no runtime contract impact.

### Golden impact
No golden impact. No pipeline stages modified. No `MapLayerId` or `MapFieldId` changes.
Pure adapter/visualization change.

### New files
- `ScalarOverlaySource.cs` — `Runtime/PCG/Adapters/Tilemap/` (enum, 58 lines).
- `ScalarOverlayRenderer.cs` — `Runtime/PCG/Adapters/Tilemap/` (helper class, 210 lines).

### Modified files
- `PCGMapTilemapVisualization.cs` — overlay system replaced (net +83 lines: 972 from 889).
- `PCGMapTilemapVisualizationEditor.cs` — overlay Inspector sections updated (269 from 249).

### No new MapLayerId or MapFieldId.


## Phase N5.e — Hills Threshold UX Remap
Date: 2026-04-08

### What changed
- **Hills threshold sliders reparameterized from raw Height [0,1] to relative fractions [0,1].**
  `hillsL1` and `hillsL2` on `MapTunables2D` now express relative position within the
  available land height range, rather than absolute height values.
  - `hillsL1` [0,1]: fraction of [waterThreshold, 1.0]. Effective threshold =
    `waterThreshold + hillsL1 × (1 − waterThreshold)`.
  - `hillsL2` [0,1]: fraction of [L1_effective, 1.0]. Effective threshold =
    `L1_effective + hillsL2 × (1 − L1_effective)`.
  - L2 ≥ L1 is guaranteed by construction (L2 starts above L1_effective); no explicit
    clamping needed.
- **Remap computed in `MapTunables2D` constructor.** `Stage_Hills2D` unchanged — it reads
  the effective raw thresholds exactly as before. The remap is a pure input-side transform.
- **New defaults:** `hillsL1 = 0.30`, `hillsL2 = 0.43`. At the default `waterThreshold = 0.50`,
  effective thresholds are ≈ 0.65 / 0.8005, closely matching the prior absolute defaults.
- **`MapGenerationPreset` updated** with renamed fields (`hillsL1`, `hillsL2`). `ToTunables()`
  passes the relative values to `MapTunables2D`, which computes the remap.
- **Serialization break:** `MapGenerationPreset` and all visualization components carry
  renamed fields (from `hillsThresholdL1`/`hillsThresholdL2` to `hillsL1`/`hillsL2`).
  No `FormerlySerializedAs` — this is a semantic change (relative vs. absolute), so silent
  migration would produce incorrect values. Existing preset assets must re-enter values.
- **`PCGMapVisualization` and `PCGMapCompositeVisualization`** received compile-fix field
  renames only (no new feature wiring), per the visualization maintenance policy. Lantern
  and composite remain frozen at N5.d feature set.
- **`PCGMapTilemapVisualization` and `PCGMapTilemapVisualizationEditor`** updated with
  renamed fields and relative-fraction semantics.

### Golden impact
Golden break for F3, F6, and StageHills2D tests (new default effective thresholds differ
from prior absolute defaults). Re-locked. F4, F5, G pipeline goldens unchanged.

### Test additions
- `StageHills2DTests.cs`: 5 new remap tests —
  `N5e_RelativeL1_MapsToExpectedEffective`,
  `N5e_RelativeL2_MapsToExpectedEffective`,
  `N5e_L2AlwaysAboveL1_ByConstruction`,
  `N5e_ZeroFractions_MapToWaterThreshold`,
  `N5e_OneFractions_MapToOne`.
- `MapGenerationPresetTests.cs`: 4 new/updated tests for relative-fraction defaults and
  forwarding. Old L2-below-L1 clamping test removed (impossible by construction).

### Modified files
- `MapTunables2D.cs` — remap logic in constructor; fields renamed to `hillsL1`/`hillsL2`.
- `MapGenerationPreset.cs` — field rename + `ToTunables()` updated.
- `PCGMapVisualization.cs` — compile-fix rename only.
- `PCGMapCompositeVisualization.cs` — compile-fix rename only.
- `PCGMapTilemapVisualization.cs` — field rename + relative-fraction semantics.
- `PCGMapTilemapVisualizationEditor.cs` — field rename.
- `StageHills2DTests.cs` — 5 new remap tests.
- `StageBaseTerrain2DTests.cs` — updated for new effective defaults.
- `MapGenerationPresetTests.cs` — 4 new/updated tests; L2-below-L1 test removed.
- `MapPipelineRunner2DGoldenF3Tests.cs` — golden re-locked.
- `MapPipelineRunner2DGoldenF6Tests.cs` — golden re-locked.

### No new MapLayerId or MapFieldId.

### Authority
- `MapTunables2D` is runtime implementation truth (remap logic lives here).
- `Stage_Hills2D.cs` is runtime implementation truth (unchanged by N5.e).
- Visualization consumers are sample-side only.

## Phase N5.d — Hills Noise Modulation
Date: 2026-04-08

### What changed
- **`hillsNoiseBlend` tunable on `MapTunables2D`.** Float [0,1], default 0.0. Controls
  per-cell noise modulation of hill classification thresholds. 0.0 = pure height-threshold
  (F3b behavior, golden-safe). > 0 = thresholds shift by `blend * (noise - 0.5) * 0.30`,
  producing organic hill boundaries that loosely follow height but with irregular edges.
  Both thresholds (L1 and L2) shift by the same offset, preserving the L1–L2 gap and all
  invariants (HillsL1 ∩ HillsL2 == ∅, both ⊆ Land).
- **`hillsNoise` field on `MapTunables2D`.** `TerrainNoiseSettings` struct configuring the
  noise algorithm for hill threshold modulation. Default `DefaultHills` (Perlin, freq 6,
  oct 2). The `amplitude` field is ignored — modulation depth is controlled by
  `hillsNoiseBlend`. Default-struct check: frequency=0 maps to `DefaultHills`.
- **`TerrainNoiseSettings.DefaultHills` static property added.** Perlin, frequency 6,
  octaves 2, lacunarity 2, persistence 0.5, amplitude 1.0, Standard fractal mode.
  Medium-scale organic variation suitable for hill boundary modulation.
- **`Stage_Hills2D` modified.** When `hillsNoiseBlend > 0`, fills a noise array via
  `MapNoiseBridge2D.FillNoise01` (salt `0xF3D50001u`, `Allocator.Temp`) and offsets both
  effective thresholds per cell. When `blend == 0`, no allocation, no noise fill — identical
  code path to pre-N5.d. Zero ctx.Rng consumption. Doc comment updated to note optional
  coordinate-hashed noise.
- **`ModulationRange = 0.30f` constant** in `Stage_Hills2D`. At blend=1.0, thresholds shift
  by ±0.15 in height-space. Fixed for now; can be exposed as a tunable later if needed.
- **`MapGenerationPreset` extended** with `hillsNoiseBlend` (float), `hillsNoiseSettings`
  (TerrainNoiseSettings, DefaultHills), and `hillsNoiseAsset` (NoiseSettingsAsset, nullable).
  `ToTunables()` resolves asset → inline struct following the N5.b override-at-resolve pattern.
  Hills (F3b) header updated to include blend slider. New Hills Noise (N5.d) header added
  for noise struct.
- **All three visualization Inspectors updated** (`PCGMapVisualization`,
  `PCGMapCompositeVisualization`, `PCGMapTilemapVisualization`): `hillsNoiseBlend` field,
  `hillsNoiseAsset` + `hillsNoiseSettings` fields, `ResolveHillsNoise()` helper,
  dirty-tracking cache + comparison, tunables construction wiring.
- **`PCGMapTilemapVisualizationEditor` updated** with `hillsNoiseBlend`, `hillsNoiseAsset`,
  `hillsNoiseSettings` serialized properties and Inspector drawing.

### Golden impact
No golden break at defaults. `hillsNoiseBlend = 0.0` (default) produces bit-identical
output to pre-N5.d. All existing F3b–G pipeline golden tests pass unchanged.

### Test additions
- `StageHills2DTests.cs`: 5 new tests — `N5d_Blend0_MatchesPreN5dGoldens`,
  `N5d_BlendPositive_IsDeterministic`, `N5d_BlendPositive_DiffersFromBlend0`,
  `N5d_BlendPositive_Invariants_Hold`, `N5d_BlendPositive_SeedVariation`.
- `MapGenerationPresetTests.cs`: 8 new tests — `DefaultValues_HillsNoiseBlend_IsZero`,
  `DefaultValues_HillsNoiseSettings_MatchDefaultHills`, `DefaultValues_HillsNoiseAsset_IsNull`,
  `ToTunables_HillsNoiseBlend_IsForwardedCorrectly`,
  `ToTunables_HillsNoiseSettings_AreForwardedCorrectly`,
  `ToTunables_DefaultPreset_HillsNoiseMatchesDefault`,
  `ToTunables_WithHillsNoiseAsset_ReadsFromAsset`,
  `ToTunables_NullHillsNoiseAsset_ReadsInlineSettings`.

### No new MapLayerId or MapFieldId.

### Authority
- `Stage_Hills2D.cs` is runtime implementation truth.
- `MapTunables2D` is runtime implementation truth.
- `TerrainNoiseSettings` is runtime implementation truth.
- Visualization consumers are sample-side only.

## Phase N5.c — Extended Noise Palette + Ridged Multifractal
Date: 2026-04-08

### What changed
- **Worley noise parameterized dispatch.** The `TerrainNoiseType.Worley` enum entry is now
  a parameterized family. `WorleyDistanceMetric` (Euclidean, SmoothEuclidean, Chebyshev) ×
  `WorleyFunction` (F1, F2, F2MinusF1, CellAsIslands) = 12 generic `Voronoi2D<>` instantiations
  dispatched via `MapNoiseBridge2D.FillWorleyNoise01` (flat switch on `metric * 4 + function`).
  No new `TerrainNoiseType` enum entries were added. Default (Euclidean + F1) produces
  bit-identical output to the pre-N5.c hardcoded Worley case.
- **`FractalMode` enum migrated** from `Islands.PCG.Layout.Maps` (TerrainNoiseSettings.cs) to
  the `Islands` namespace (Noise.cs). This is a noise-runtime concept consumed by
  `Noise.Settings`. All consumers updated with `using Islands;`.
- **`Noise.Settings` extended** with 3 new fields:
  - `FractalMode fractalMode` (default Standard = 0, zero-init safe).
  - `float ridgedOffset` (default 1.0, Musgrave canonical).
  - `float ridgedGain` (default 2.0, Musgrave canonical).
  `Settings.Default` updated with explicit defaults.
- **`Noise.GetFractalNoise<N>()` modified.** Early-return branch when `fractalMode == Ridged`
  delegates to new private `GetRidgedFractalNoise<N>()`. Standard path is unchanged —
  no code difference for `FractalMode.Standard`.
- **`GetRidgedFractalNoise<N>()` implemented** (~35 lines). Musgrave ridged multifractal:
  `signal = (offset - |noise|)^2` with inter-octave feedback `weight = clamp(signal * gain, 0, 1)`.
  Returns `Sample4` with `.v` only (derivatives zero — abs() breaks continuity).
  Applies to all `INoise` types (Perlin, Simplex, Value, all Worley variants).
- **`MapNoiseBridge2D.FillNoise01`** populates the 3 new `Noise.Settings` fields from
  `TerrainNoiseSettings`. Worley case delegates to `FillWorleyNoise01`. `FillNoise01Core`
  is unchanged (ridged branching happens inside `Noise.GetFractalNoise`).
  `FillSimplexPerlin01` (F3 legacy path) is unchanged.
- **Assembly references updated:** `Islands.PCG.Editor.asmdef` and
  `Islands.PCG.Tests.EditMode.asmdef` now reference `Islands.Runtime` directly for
  `FractalMode` resolution after namespace migration.
- **`TerrainNoiseSettings.cs`** updated: `FractalMode` enum declaration removed (migrated).
  Doc comments updated from "not functional until N5.c" to "functional as of N5.c".
  `Worley` enum entry doc updated to describe parameterized family.
  All field declarations, defaults, `IEquatable`, `GetHashCode` unchanged.
- **`TerrainNoiseSettingsDrawer.cs`** updated: added `using Islands;` for `FractalMode`.
  Functional code unchanged.

### New files
- `Runtime/PCG/Tests/EditMode/Maps/MapNoiseBridge2DTests.cs` — 22 tests covering
  Worley parameterized dispatch (parity, differentiation, all 12 combos via `[Values]`),
  ridged multifractal (differs from standard, determinism, valid range, applies to all
  types), Standard mode isolation (ridged/Worley fields don't affect non-relevant paths),
  ridged edge cases (offset=0, gain=0, single octave), seed variation, and
  `FillSimplexPerlin01` legacy path stability.

### Golden impact
No golden break at defaults. `FractalMode.Standard` (zero-init) + `Euclidean` + `F1`
(zero-init) produce bit-identical output to pre-N5.c. `FillSimplexPerlin01` constructs
`Noise.Settings` without the new fields (zero-init = Standard), unchanged codepath.
All existing F3–F6/G pipeline golden tests pass.

### Serialization note
`Noise.Settings` layout changed (3 new fields). Existing scene-serialized `Noise.Settings`
instances in sample/visualization code (`NoiseVisualization`, `ProceduralSurface`) will
reset new fields to zero on deserialization. Since `FractalMode.Standard = 0` is the
default and the Standard path ignores `ridgedOffset`/`ridgedGain`, this is harmless.
Samples only need re-entering values if switching to Ridged mode.

### Known observation
Worley-family noise biases toward bright values (0.5–1.0 range) in the scalar heatmap
visualization. Cause: the `n * 0.5 + 0.5` remap in `FillNoise01Core` assumes [-1,1]
centered output, but Voronoi distances are non-negative [0,1]. Not a bug — a normalization
mismatch tracked as a potential post-N5.c follow-up (Worley-aware remap mode).

### Test additions
- `Worley_EuclideanF1_MatchesPreN5cWorleyCase`: determinism + golden parity
- `Worley_SmoothEuclideanF1_DiffersFromEuclideanF1`: metric dispatch confirmed
- `Worley_ChebyshevF1_DiffersFromEuclideanF1`: metric dispatch confirmed
- `Worley_EuclideanF2_DiffersFromEuclideanF1`: function dispatch confirmed
- `Worley_F2MinusF1_DiffersFromF1`: function dispatch confirmed
- `Worley_CellAsIslands_DiffersFromF1`: function dispatch confirmed
- `Worley_AllCombinations_ProduceValidRange` (×12): range [0,1] + non-flat for all combos
- `Ridged_Perlin_DiffersFromStandard`: ridged ≠ standard
- `Ridged_Perlin_IsDeterministic`: same seed → identical
- `Ridged_Perlin_ProducesValidRange`: [0,1] + non-flat
- `Ridged_Simplex_DiffersFromStandard`: ridged applies cross-type
- `Ridged_Worley_DiffersFromStandard`: ridged applies to Voronoi
- `Ridged_WorleyCellAsIslands_ProducesValidRange`: exotic combo validation
- `Standard_RidgedFieldValues_DoNotAffectOutput`: isolation guarantee
- `Standard_WorleyFieldValues_DoNotAffectNonWorleyOutput`: isolation guarantee
- `Ridged_OffsetZero_ProducesValidRange`: degenerate edge case
- `Ridged_GainZero_ProducesValidRange`: no-feedback edge case
- `Ridged_SingleOctave_ProducesValidRange`: no-loop edge case
- `Ridged_DifferentSeeds_ProduceDifferentOutput`: seed variation
- `FillSimplexPerlin01_UnchangedByN5c`: legacy path stability


## Phase N5.b — Noise Settings Assets
Date: 2026-04-08

### What changed
- New `NoiseSettingsAsset` ScriptableObject at `Runtime/Layout/Maps/NoiseSettingsAsset.cs`.
  `[CreateAssetMenu]` (Islands/PCG/Noise Settings). Wraps a single `TerrainNoiseSettings`
  struct. Reusable across multiple presets and visualization components.
- `MapGenerationPreset` extended with two optional `NoiseSettingsAsset` slots:
  `terrainNoiseAsset` and `warpNoiseAsset`. Override-at-resolve pattern: when assigned,
  `ToTunables()` reads from the asset; when null, reads from inline struct (backward
  compatible).
- **Serialization break:** `MapGenerationPreset` and all three visualization components
  refactored from 11 individual noise fields (`terrainNoiseType`, `terrainFrequency`, ...)
  to 2 embedded `TerrainNoiseSettings` structs (`terrainNoiseSettings`, `warpNoiseSettings`).
  Serialized field names changed — existing preset assets and scene-serialized components
  must re-enter noise values.
- `TerrainNoiseSettings` extended with 5 new fields (Phase N5.b, carried but not
  functional in noise runtime until N5.c):
  - `WorleyDistanceMetric` enum (Euclidean, SmoothEuclidean, Chebyshev) — default Euclidean.
  - `WorleyFunction` enum (F1, F2, F2MinusF1, CellAsIslands) — default F1.
  - `FractalMode` enum (Standard, Ridged) — default Standard.
  - `ridgedOffset` (float, default 1.0) — ridged multifractal offset parameter.
  - `ridgedGain` (float, default 2.0) — ridged multifractal gain parameter.
- `IEquatable<TerrainNoiseSettings>` implemented on the struct for dirty-tracking.
  Replaces 11+ individual `Mathf.Approximately` / enum comparisons per consumer with
  a single `Equals()` call.
- New `TerrainNoiseSettingsDrawer` custom PropertyDrawer at `Editor/TerrainNoiseSettingsDrawer.cs`.
  Conditional field visibility: Worley fields hidden when noiseType != Worley; ridged fields
  hidden when fractalMode == Standard. Applies automatically wherever the struct is serialized.
- All three visualization components (PCGMapVisualization, PCGMapCompositeVisualization,
  PCGMapTilemapVisualization) updated: NoiseSettingsAsset slots, embedded noise structs,
  simplified dirty-tracking. Resolution chain: preset → asset → inline struct.
- `PCGMapTilemapVisualizationEditor` updated: draws noise asset slots with info boxes
  when asset overrides are active; uses PropertyDrawer for struct fields.

### Golden impact
No golden break at defaults. All new enum/float fields have identity defaults that produce
bit-identical output to pre-N5.b. New fields are carried in the struct but ignored by
`MapNoiseBridge2D.FillNoise01` until N5.c adds the corresponding case branches.

### Test additions
- `DefaultValues_TerrainNoise_N5bFieldsHaveIdentityDefaults`: verifies new field defaults.
- `DefaultValues_WarpNoise_N5bFieldsHaveIdentityDefaults`: same for warp.
- `DefaultValues_NoiseAssets_AreNull`: verifies null-by-default asset slots.
- `ToTunables_WithTerrainNoiseAsset_ReadsFromAsset`: asset override resolution.
- `ToTunables_NullTerrainNoiseAsset_ReadsInlineSettings`: inline fallback.
- `ToTunables_WithWarpNoiseAsset_ReadsFromAsset`: warp asset override.
- `TerrainNoiseSettings_Equals_DefaultsAreEqual`: IEquatable identity.
- `TerrainNoiseSettings_Equals_DifferentFieldsAreNotEqual`: base field inequality.
- `TerrainNoiseSettings_Equals_DifferentN5bFieldsAreNotEqual`: new field inequality.

### Modified files
- `TerrainNoiseSettings.cs` — extended struct + 3 new enums + IEquatable.
- `NoiseSettingsAsset.cs` — **new** ScriptableObject.
- `MapGenerationPreset.cs` — asset slots + struct embed + ToTunables update.
- `PCGMapVisualization.cs` — struct embed + asset slots + dirty tracking.
- `PCGMapCompositeVisualization.cs` — same.
- `PCGMapTilemapVisualization.cs` — same.
- `PCGMapTilemapVisualizationEditor.cs` — asset slot drawing + PropertyDrawer.
- `TerrainNoiseSettingsDrawer.cs` — **new** Editor-only PropertyDrawer.
- `MapGenerationPresetTests.cs` — extended test coverage.

### No new MapLayerId or MapFieldId.


## Phase N5.a — Base Shape Selector
Date: 2026-04-08

### What changed
- New `IslandShapeMode` enum at `Runtime/PCG/Layout/Maps/IslandShapeMode.cs`:
  Ellipse (default), Rectangle, NoShape, Custom. Future: PolarCoords.
- `MapTunables2D` extended with `shapeMode` field (default Ellipse).
  Constructor parameter is last with default, so all existing call sites are
  backward compatible with no code changes.
- `Stage_BaseTerrain2D` extended with shape mode switch in the hot loop:
  - Ellipse: unchanged F2b radial smoothstep + domain warp (bit-identical).
  - Rectangle: Chebyshev-normalized distance (max(|v.x|/halfX, |v.y|/halfY)),
    same smoothstep(fromSq, toSq, distSq) semantics as Ellipse. Domain warp
    displaces before edge evaluation. Half-extents from radius * aspect.
  - NoShape: h01 = noise sample directly. No mask, no perturbation formula.
    Water threshold alone carves coastlines.
  - Custom: falls back to Ellipse when no MapShapeInput is provided.
  - F2c `MapShapeInput.HasShape` takes unconditional priority over shapeMode.
- `BaseTerrainStage_Configurable` updated with identical shape mode switch
  (lantern twin kept in sync).
- `MapGenerationPreset` extended with `shapeMode` field + `ToTunables()` updated.
- All three visualization Inspectors (PCGMapVisualization, PCGMapCompositeVisualization,
  PCGMapTilemapVisualization) updated with shapeMode field, dirty tracking, tunables wiring.
- `PCGMapTilemapVisualizationEditor` updated to draw shapeMode when no preset assigned.
- Console log lines updated to include `shape=` in debug output.

### Golden impact
No golden break at defaults. `IslandShapeMode.Ellipse` (default) produces bit-identical
output to pre-N5.a. Rectangle and NoShape modes produce new distinct output (expected).
New Rectangle and NoShape goldens locked.

### Test additions
- `N5a_Ellipse_Default_MatchesPreN5aGoldens`: proves backward compatibility.
- Rectangle: determinism, differs-from-Ellipse, invariants, golden lock, seed variation.
- NoShape: determinism, differs-from-Ellipse, produces-land-and-water, invariants,
  golden lock, seed variation.
- `N5a_Custom_WithoutShapeInput_MatchesEllipse`: Custom fallback verified.
- `N5a_ShapeInput_TakesPriorityOverShapeMode`: F2c priority contract verified.
- `MapGenerationPresetTests`: shapeMode default, forwarding for all four enum values.

### No new MapLayerId or MapFieldId.

## Phase F3b — Height-Coherent Hills (Clean Break)
Date: 2026-04-08


## Phase F3b — Height-Coherent Hills (Clean Break)
Date: 2026-04-08

### What changed
- `Stage_Hills2D` rewritten: topology-based hill placement (independent noise on
  LandInterior + MaskTopologyOps2D neighbor counting) replaced with height-threshold
  classification from the Height scalar field.
- New contract: `HillsL2 = Land AND Height >= thresholdL2`;
  `HillsL1 = Land AND Height >= thresholdL1 AND NOT HillsL2`.
- HillsL1 and HillsL2 are now disjoint (`HillsL1 ∩ HillsL2 == ∅`);
  previously `HillsL2 ⊆ HillsL1` (overlap).
- Hills domain broadened from `LandInterior` to `Land` (coastal cliffs can be hills).
- LandEdge / LandInterior derivation unchanged (MaskTopologyOps2D.ExtractEdgeAndInterior4).
- All MapNoiseBridge2D usage and independent noise removed from Stage_Hills2D.
  Zero RNG consumption (continues N4 pattern — entire pipeline uses coordinate hashing).
- `MapTunables2D` extended with `hillsThresholdL1` (default 0.65) and
  `hillsThresholdL2` (default 0.80). Constructor-validated (L2 clamped >= L1).
- `MapGenerationPreset` extended with matching fields + `ToTunables()` updated.
- All three visualization Inspectors updated (PCGMapVisualization,
  PCGMapCompositeVisualization, PCGMapTilemapVisualization) with Hills (F3b) header,
  dirty tracking, tunables wiring.
- `PCGMapTilemapVisualizationEditor` updated to draw the new fields when no preset assigned.

### Golden impact
Full golden break for F3+ hashes (F3, F4, F5, F6, Phase G). All pipeline and stage
golden hashes re-locked. F0–F2 goldens unaffected.

### Test corrections
- `StageTraversal2DTests.Stage_Traversal2D_Invariants_Hold`: corrected `Walkable ⊆ Land`
  to `Walkable ⊆ (Land ∪ ShallowWater)`. This was a latent test bug since Post-N2 Issue 3
  (ShallowWater walkability), surfaced by the F3b golden break.

### Authority
- `Stage_Hills2D.cs` is runtime implementation truth.
- `MapTunables2D` is runtime implementation truth.
- Visualization consumers are sample-side only.

## 2026-04-04 (Phase H4 — Animated Tiles)
- Phase H4 — Animated Tiles implemented and smoke-test verified.
- `TilesetConfig.LayerEntry` struct (`Runtime/PCG/Adapters/Tilemap/TilesetConfig.cs`) extended:
  - New field `animatedTile` (`TileBase`, default null). Typed as `TileBase` so `AnimatedTile`
    from 2D Tilemap Extras is accepted at the Inspector slot without a compile-time asmdef
    reference to the Extras package.
  - `ToLayerEntries()` tile resolution updated to three-way priority:
      enabled + animatedTile assigned → emit animatedTile
      enabled + tile assigned        → emit tile
      enabled + both null            → emit null (skipped)
      disabled                       → emit null (skipped)
  - Backward compatible: existing `.asset` files and all call sites unchanged; `animatedTile`
    defaults to null on deserialization.
  - `BuildDefaultLayers()` explicitly initializes `animatedTile = null` alongside existing fields.
  - Class summary updated with Phase H4 note.
- `PCGMapTilemapVisualization.ComputeTilesetConfigHash()` extended:
  - `animatedTile.GetInstanceID()` (defaulting to 0 when null) added as an extra FNV-1a round
    per layer entry, immediately after the static tile round.
  - Inspector edits to animated tile slots now trigger real-time rebuild on the next frame,
    matching the existing behavior for tile, enabled, layerId, and fallback fields.
  - Per-frame cost remains O(MapLayerId.COUNT) = 12 iterations — negligible.
  - Method comment updated; class summary updated with Phase H4 note.
- No asmdef changes required. No new MapLayerId, MapFieldId, or runtime stage contracts.
  Adapters-last invariant preserved.
- 3 new EditMode tests green (`TilesetConfigTests.cs`, total: 17):
  - `DefaultLayers_AllAnimatedTilesNull` — default state guard.
  - `ToLayerEntries_AnimatedTileWinsOverStaticTile` — animated tile precedence contract.
  - `ToLayerEntries_AnimatedTileNull_FallsBackToStaticTile` — backward-compatible fallback.
  Tests use `ScriptableObject.CreateInstance<Tile>()` (engine base type, no Extras dependency).
  All pre-existing 14 tests unchanged.
- `PCG_Roadmap.md` updated: Phase H4 marked Done; Phase H5 marked Next.
- `CURRENT_STATE.md` updated: H4 recorded as resolved; immediate next focus set to Phase H5.

## 2026-04-04 (Phase H3 — Post-merge fixes)
- TilesetConfig.ToLayerEntries() bug fix:
  Unassigned/disabled entries now emit null tile instead of fallbackTile, matching
  the TilemapAdapter2D null-means-skip contract. fallbackTile is the per-cell adapter
  fallback only, not a per-entry tile. Root cause: high-priority subset layers
  (LandCore, LandEdge, etc.) were overwriting their parent layer tile (Land) with the
  fallback, making all land appear as water when fallbackTile was assigned.
- TilesetConfig real-time dirty detection:
  Added ComputeTilesetConfigHash() to PCGMapTilemapVisualization — FNV-1a over
  (layerId int + tile InstanceID + enabled bool) × COUNT + fallback InstanceID.
  Detects tile swaps, enabled-toggles, layerId changes, and fallback edits made
  directly to the SO asset while assigned. Now matches MapGenerationPreset's
  real-time field-edit behavior.
- TilesetConfig.LayerEntry explicit layerId field:
  Added public MapLayerId layerId to LayerEntry. LayerId is now stored explicitly
  per entry, decoupled from array position. Reordering entries in the Inspector
  changes stamp priority without scrambling the LayerId-to-tile mapping.
  BuildDefaultLayers() now uses visual priority order (low→high):
    DeepWater → ShallowWater → Land → LandInterior → LandCore → Vegetation
    → HillsL1 → HillsL2 → Stairs → LandEdge → Walkable → Paths
  Hills appear after Vegetation so mountain tiles correctly overwrite forest tiles.
  (Previously HillsL1=3 < Vegetation=7 by MapLayerId integer, causing Vegetation
  to overwrite Hills — visually wrong for overlapping cells.)
- TilesetConfigTests updated: new tests for visual priority order, explicit layerId
  contract, and fallbackTile-not-propagated-to-entries regression gate.
- ComputeTilesetConfigHash() updated to include layerId in the FNV-1a hash.
- Migration note: existing TilesetConfig .asset files need their layerId field
  re-assigned per entry after the script update (Unity serializes new fields as
  default=0=Land). Recommend deleting and recreating the asset to get correct defaults.

## 2026-04-04 (Phase H3)
- Phase H3 — Sample Infrastructure (Presets & Configuration) implemented and smoke-test verified.
- New `MapGenerationPreset` ScriptableObject (`Runtime/PCG/Samples/Presets/MapGenerationPreset.cs`):
  - Fields: seed (uint), resolution (int), stage toggles (Hills/Shore/Veg/Traversal/Morphology),
    F2 tunables (islandRadius01, waterThreshold01, islandSmoothFrom01, islandSmoothTo01,
    islandAspectRatio, warpAmplitude01), noise settings (noiseCellSize, noiseAmplitude, quantSteps),
    clearBeforeRun. `[CreateAssetMenu]` under Islands/PCG/Map Generation Preset.
  - `ToTunables()` produces a `MapTunables2D` from the preset's shape fields; MapTunables2D clamps/orders values.
  - Default field values match the component defaults (noiseAmplitude=0.18f, quantSteps=1024,
    noiseCellSize=8, seed=1u, resolution=64, all stage toggles=true, islandAspectRatio=1.0,
    warpAmplitude01=0.0).
- New `TilesetConfig` ScriptableObject (`Runtime/PCG/Adapters/Tilemap/TilesetConfig.cs`):
  - `LayerEntry` serializable struct: label (string) + TileBase tile + bool enabled.
  - `layers LayerEntry[]` array initialized to MapLayerId.COUNT entries with default labels.
  - `fallbackTile TileBase`: used for disabled entries or entries with no tile assigned.
  - `ToLayerEntries()`: converts to `TilemapLayerEntry[]` for TilemapAdapter2D.Apply.
    Returns null if layers.Length != MapLayerId.COUNT and logs a warning; caller falls back
    to its inline array. Tile resolution per entry: enabled+tile→tile; enabled+no tile→fallback;
    disabled→fallback.
  - `[CreateAssetMenu]` under Islands/PCG/Tileset Config.
- Architecture improvement (required for thin shared asmdef approach):
  - New `Islands.PCG.Samples.Shared.asmdef` at `Runtime/PCG/Samples/Presets/`:
    references Islands.PCG.Runtime only; contains MapGenerationPreset.cs.
  - New `Islands.PCG.Samples.asmdef` at `Runtime/PCG/Samples/`:
    references Islands.Runtime + Islands.PCG.Runtime + Islands.PCG.Samples.Shared + math/collections/burst.
    Covers PCGMapVisualization, PCGMapCompositeVisualization, PCGDungeonVisualization,
    PCGMaskVisualization, PCGMaskPaletteController, MaskGrid2DBooleanOpsSmokeTest.
    Islands.PCG.Runtime is now clean of sample-side code; no scene references broken.
  - `Islands.PCG.Adapters.Tilemap.asmdef` updated: adds Islands.PCG.Samples.Shared reference.
  - `Islands.PCG.Tests.EditMode.asmdef` updated: adds Islands.PCG.Samples.Shared +
    Islands.PCG.Samples references.
- Four visualization/sample components patched (override-at-resolve pattern):
  - `PCGMapVisualization.cs`: preset slot added; effective values resolved at top of
    UpdateVisualization(); CacheParams() caches effective values; ParamsChanged() compares
    effective values + preset reference. Resolution excluded (controlled by base Visualization class).
  - `PCGMapCompositeVisualization.cs`: same pattern; resolution IS overridable from preset.
  - `PCGMapTilemapVisualization.cs`: preset slot + tilesetConfig slot; tile resolution priority:
    Procedural > TilesetConfig.ToLayerEntries() > inline priorityTable.
    _lastTilesetConfig tracked by reference; SO field edits do not auto-refresh (documented in tooltip).
    Three `[ContextMenu]` palette presets (Classic/Prototyping/Twilight) preserved from H2d.
  - `PCGMapTilemapSample.cs`: preset slot + tilesetConfig slot; effective values resolved at
    top of Generate(); TilesetConfig.ToLayerEntries() ?? priorityTable fallback pattern.
- Recommended asset storage conventions established:
  - MapGenerationPreset .asset files → Runtime/PCG/Samples/Presets/
  - TilesetConfig .asset files       → Runtime/PCG/Samples/PCG Map Tilemap/Tilesets/
- 12 new EditMode tests green: 8 in MapGenerationPresetTests (defaults, ToTunables, clamping,
  auto-ordering), 4 in TilesetConfigTests (default length/labels/enabled, ToLayerEntries shape,
  LayerId mapping, disabled entry, mismatch guard).
  All pre-existing tests unchanged.
- No new MapLayerId, MapFieldId, or runtime stage contracts. Adapters-last invariant preserved.
- Smoke tests passed: preset swap triggers map regeneration with correct console hash;
  TilesetConfig swap updates tilemap tiles; null slots correctly fall back to inline fields.
- `PCG_Roadmap.md` updated: Phase H3 marked Done; Phase H4 marked Next.
- `CURRENT_STATE.md` updated: H3 recorded as resolved; immediate next focus set to Phase H4.

## 2026-04-04 (Phase H2d)
- Phase H2d — Procedural Tile Generation implemented and smoke-test verified.
- New `ProceduralTileEntry` `[Serializable]` struct (`Runtime/PCG/Adapters/Tilemap/ProceduralTileEntry.cs`):
  - Maps one `MapLayerId` to a solid `UnityEngine.Color`. Priority is positional (low→high),
    matching `TilemapLayerEntry` semantics.
- New `ProceduralTileFactory` static class (`Runtime/PCG/Adapters/Tilemap/ProceduralTileFactory.cs`):
  - `GetOrCreate(Color)`: returns a cached runtime `Tile` (ScriptableObject). Backing sprite is a
    shared white 1×1 `Texture2D` (`FilterMode.Point`); `Tile.color` carries the tint. Cache key
    is `Color32` (avoids float-equality edge cases).
  - `BuildPriorityTable(ProceduralTileEntry[])`: converts a color table into a
    `TilemapLayerEntry[]` for `TilemapAdapter2D.Apply`. Null/empty input → empty array (never null).
  - `ClearCache()`: releases all cached tiles and the shared sprite via `DestroyImmediate`.
    Safe to call on an empty cache. Domain reload wipes the static cache automatically.
- `PCGMapTilemapVisualization` patched (`Runtime/PCG/Adapters/Tilemap/PCGMapTilemapVisualization.cs`):
  - New `[Header("Procedural Tiles")]` Inspector section: `useProceduralTiles` bool toggle,
    `ProceduralTileEntry[] proceduralColorTable`, `Color proceduralFallbackColor` (default dark grey).
  - When `useProceduralTiles` is true, the pre-authored Priority Table and Fallback Tile are
    bypassed; `ProceduralTileFactory.BuildPriorityTable` + `GetOrCreate` resolve the active table.
  - Dirty tracking extended: FNV-1a hash (`ComputeProceduralHash`) over LayerId int + RGBA bytes;
    fallback Color compared directly. Three new `last*` cache fields.
  - Console log gains `proceduralTiles=` field.
  - Three `[ContextMenu]` palette presets (one-click color table population from the Inspector):
    - **Procedural Palette / Classic (Natural)**: earthy navy, steel blue, grass green, tan, brown,
      forest green, sandy yellow, olive, deep olive.
    - **Procedural Palette / Prototyping (Debug)**: high-contrast saturated hues (blue, cyan,
      green, yellow, orange, magenta, white, red, dark red) — maximally distinct per layer.
    - **Procedural Palette / Twilight (Moody)**: deep purples, teals, and warm gold accents.
  - Private helpers `Entry(id, hex)` and `Hex(hex)` (via `ColorUtility.TryParseHtmlString`).
  - `TilemapAdapter2D` unchanged. Adapters-last invariant preserved.
- 13 EditMode tests in `ProceduralTileFactoryTests`
  (`Tests/EditMode/PCG/Adapters/Tilemap/ProceduralTileFactoryTests.cs`):
  - Cache identity (same color → same instance; different colors → different instances;
    Color32-equal values share slot).
  - Tile properties (non-null sprite, correct Color32 channel fidelity, Tile type).
  - BuildPriorityTable (null/empty guards, LayerId order, all tiles non-null,
    same-color entries share instance).
  - ClearCache (fresh instance after clear, multiple calls safe, empty cache safe).
- No new `MapLayerId`, `MapFieldId`, or runtime stage contracts. No asmdef changes.
- Smoke tests passed: baseline sanity, mode toggle (art ↔ procedural, no errors), LandCore
  priority cross-check (dark olive interior only, not overwriting land), seed variation,
  all three palette presets verified in the Game Window.
- `PCG_Roadmap.md` updated: Phase H2d marked Done; Phase H3 marked Next.
- `CURRENT_STATE.md` updated: H2d recorded as resolved; immediate next focus set to Phase H3.

## 2026-04-03 (Phase H2c)
- Phase H2c — Live Tilemap Visualization implemented and smoke-test verified.
- New `PCGMapTilemapVisualization` component (`Runtime/PCG/Adapters/Tilemap/PCGMapTilemapVisualization.cs`):
  - `[ExecuteAlways]` MonoBehaviour; regenerates in Editor without entering Play mode.
  - Full Inspector: Tilemap target, seed, resolution, stage toggles (Hills/Shore/Veg/Traversal/Morphology),
    all `MapTunables2D` fields (islandRadius01, waterThreshold01, islandSmoothFrom01, islandSmoothTo01,
    islandAspectRatio, warpAmplitude01, noiseCellSize, noiseAmplitude, quantSteps),
    flipY, clearBeforeRun, `TilemapLayerEntry[]` priorityTable, fallbackTile.
  - Dirty tracking on all fields: per-float `Mathf.Approximately`, per-int/bool `!=`,
    TileBase object identity (`!=`), FNV-1a hash over priority table entries (LayerId int + tile InstanceID).
  - `MapContext2D` allocated `Persistent`, kept alive between runs, reallocated only on resolution change.
    Disposed in `OnDisable`.
  - Per-rebuild: `MapPipelineRunner2D.Run` → `MapExporter2D.Export` → `TilemapAdapter2D.Apply`.
  - Console log on each rebuild: `seed`, `tilesStamped/total`, stage flags, flipY.
  - `BaseTerrainStage_Configurable` private nested class (sync note: must stay bit-identical to
    `PCGMapVisualization` and `PCGMapCompositeVisualization` copies).
  - Coexists with `PCGMapTilemapSample`; same asmdef (`Islands.PCG.Adapters.Tilemap`).
- `Islands.PCG.Adapters.Tilemap.asmdef` updated: `"Unity.Mathematics"` added to references array.
  Required by `BaseTerrainStage_Configurable` (`math`, `float2`, `NativeArray` math ops).
- No new `MapLayerId`, `MapFieldId`, or runtime stage contracts. Adapters-last invariant preserved.
- Smoke tests passed: baseline sanity, stage isolation (HillsL1), morphology cross-check (LandCore
  priority ordering confirmed), seed variation, flipY mirror.
- LandCore priority note confirmed: must be placed above Land and below Vegetation in the priority
  table. Placing it last causes it to overwrite all other land features.
- `PCG_Roadmap.md` updated: Phase H2c marked Done; Phase H2d marked Next.
- `CURRENT_STATE.md` updated: H2c recorded as resolved; immediate next focus set to Phase H2d.

## 2026-04-03 (Phase H2)
- Phase H2 — Data Export / Map Adapters implemented and test-gated.
- New `MapDataExport` sealed class (`Runtime/PCG/Layout/Maps/MapDataExport.cs`):
  - Managed snapshot of a completed `MapContext2D`. Produced by `MapExporter2D.Export`.
  - Holds `bool[]` per created layer and `float[]` per created field (row-major, index = x + y * Width).
  - Absent slots (layer/field not created in the run) stored as null; `HasLayer`/`HasField` guards.
  - Access: `HasLayer`, `GetLayer`, `GetCell`; `HasField`, `GetField`, `GetValue`.
  - `GetCell` and `GetValue` throw `ArgumentOutOfRangeException` on OOB coordinates.
  - `GetLayer`/`GetField` throw `InvalidOperationException` on absent slot.
  - Lifetime: independent of source context; snapshot survives `ctx.Dispose()`.
  - `internal` constructor; instantiation only via `MapExporter2D`.
- New `MapExporter2D` static class (`Runtime/PCG/Layout/Maps/MapExporter2D.cs`):
  - Adapters-last: read-only adapter; never writes to the context.
  - Enumerates all `MapLayerId` and `MapFieldId` values; exports all present ones.
  - Layer copy: row-major scan via `MaskGrid2D.GetUnchecked(x, y)`.
  - Field copy: flat `NativeArray<float>` index scan via `ScalarField2D.Values[j]`.
  - Returns a `MapDataExport` with width, height, seed, and all populated slots.
  - Deterministic: same context state ⇒ identical export output.
  - Extensible: later phases that add new layers/fields are automatically exported with no adapter changes.
- Key decisions recorded:
  - Output type: managed class (`bool[][]` / `float[][]`), not struct, not ScriptableObject.
    Rationale: headless, Unity-free, directly testable; class avoids copy-by-value of large arrays.
  - Scope: all-present export (not a declared subset). Extensible by construction.
  - Tilemap adapter: deferred to Phase H2b (separate slice; requires Unity.Tilemaps dependency).
  - Static adapter: context is all state needed; no streaming or instance required at this stage.
- New `MapExporter2DTests` (14 tests, `Runtime/PCG/Tests/EditMode/Maps/MapExporter2DTests.cs`):
  - Empty-context export: all layers/fields absent, correct width/height/seed/length.
  - Layer round-trip fidelity: diagonal pattern, all-ones, all-zeros.
  - Unrelated layer absent after single-layer export.
  - Field round-trip fidelity: gradient pattern, absent field.
  - Determinism: two exports from same context state produce identical arrays.
  - Snapshot independence: post-export context mutation does not affect snapshot values.
  - Guard paths: null context, absent layer GetLayer, absent field GetField, OOB GetCell, OOB GetValue.
- No new `MapLayerId`, `MapFieldId`, or runtime stage contracts. No PCGMapVisualization patch.
  Adapters-last invariant preserved: `MapExporter2D` is a pure read adapter.
- Smoke test: no visual smoke test required (no new MaskGrid2D layer written).
  Optional console check: `Debug.Log` of exported layer/field counts and a single cell spot-check.
  Expected full-pipeline output: `layers=10, fields=2` (Paths and Moisture not yet written).
- `PCG_Roadmap.md` updated: Phase H2 marked Done; Phase H2b added as Next.
- `CURRENT_STATE.md` updated: Phase H2 recorded as resolved; immediate next focus set to Phase H2b.
- `map-pipeline-by-layers-ssot.md` updated: boundary advanced to Phase H2; adapter contracts and surface entry added.

## 2026-04-02 (Phase H1)
- Phase H1 — Composite Map Visualization implemented (sample-side, smoke-test verified).
- New `PCGMapCompositeVisualization.cs` (`Runtime/PCG/Samples/PCG Map/`).
- Composites all active pipeline layers into a single `Texture2D` via CPU `SetPixels32`.
  One cell at a time; per-cell color determined by a fixed priority table (low → high,
  later entries overwrite earlier):
    DeepWater → ShallowWater → Land → LandCore → Vegetation → HillsL1 → HillsL2
    → Stairs → LandEdge
  LandCore is positioned above Land but below all terrain features, so it tints the
  deep interior (teal) while Vegetation, Hills, Stairs, and LandEdge render on top.
- Layer configuration uses a `[Serializable] CompositeLayerSlot` struct (label + color +
  enabled) replacing two parallel arrays, so the Inspector shows each layer by name.
- Optional multiplicative scalar-field tint overlay (Height or CoastDist); off by default.
- Composite pixel hash (FNV-1a over Color32 array) logged to Console on each dirty rebuild;
  use this as an informal visual golden baseline — no formal test gate required for
  sample-side code.
- Does NOT replace `PCGMapVisualization` (single-layer diagnostic lantern); both coexist.
- No new `MapLayerId`, `MapFieldId`, or runtime stage contracts.
- `BaseTerrainStage_Configurable` duplicated from `PCGMapVisualization` (sample-side;
  sync note in both files; factoring out as `internal` deferred until a third consumer exists).
- `PCG_Roadmap.md` updated: Phase H1 marked Done; Phase H2 marked Next.
- `CURRENT_STATE.md` updated: Phase H1 recorded as resolved; immediate next focus confirmed H2.
- `map-pipeline-by-layers-ssot.md` updated: boundary note, implemented surface, known limitations.

## 2026-04-02 (Phase H)
- Phase H — Extract + Adapters (visualization half) implemented.
- `PCGMapVisualization.cs` patched with `PCGViewMode` enum and scalar field visualization:
  - New `PCGViewMode` enum: `MaskLayer` (existing binary ON/OFF behavior), `ScalarField` (new).
  - `viewMode` Inspector field selects the active mode.
  - `viewField` (MapFieldId) + `scalarMin` / `scalarMax` Inspector fields govern scalar view.
  - `PackFromFieldAndUpload` packs normalized field values into the existing GPU float buffer.
    No shader changes required: existing lerp between maskOffColor/maskOnColor provides the ramp.
  - Normalization: `saturate((v - scalarMin) / (scalarMax - scalarMin))`.
    CoastDist recommended defaults: scalarMin=−1, scalarMax=20.
    Height recommended defaults: scalarMin=0, scalarMax=1.
  - `Moisture` (Phase M) and any unwritten field: shows all-low ramp (PackZerosAndUpload).
  - Palette header renamed: "MaskLayer: OFF/ON — ScalarField: Low/High ramp".
  - Layer preset colors: `useLayerPresetColors` toggle + `layerPresetOnColors Color[12]` array.
    When enabled, maskOnColor is overridden per active viewLayer at display time (MPB only;
    Inspector field unchanged). Color array edits do not trigger dirty detection; toggle the
    field or change seed to refresh.
  - Console log line updated to include `mode=` and `field=`.
- `MapContext2D.cs` extended with additive `GetField(MapFieldId)` public method (mirrors the
  existing `GetLayer` pattern). No contracts changed. No new MapLayerId/MapFieldId entries.
- Phase H2 — Data Export / Map Adapters added to roadmap as the next planned slice (before Phase I).
  Phase H2 completes the "Adapters" half of the original Phase H intent.
- `PCG_Roadmap.md` updated: Phase H marked done; Phase H2 added as next; sequence updated.
- `CURRENT_STATE.md` updated: Phase H recorded as resolved; CoastDist/Height visualization
  limitations removed; immediate next focus set to Phase H2.
- `map-pipeline-by-layers-ssot.md` updated: boundary advanced to Phase H2+; Phase H implemented
  surface entry added; known limitations updated.

## 2026-04-02 (Phase F2c)
- Phase F2c — Arbitrary Shape Input implemented and test-gated.
- New `MapShapeInput` companion struct (`Runtime/PCG/Layout/Maps/MapShapeInput.cs`):
  `HasShape` flag + `MaskGrid2D Mask`. Default (`None`) preserves F2b ellipse+warp path.
  Caller owns and disposes the mask; `MapInputs` holds by value.
- `MapInputs` extended with optional 4th constructor parameter `MapShapeInput shapeInput = default`.
  All existing call sites unchanged (backward compatible).
- `Stage_BaseTerrain2D` extended with F2c shape-input branch:
  - When `HasShape = true`: `mask01 = shape.GetUnchecked(x, y) ? 1f : 0f` replaces ellipse+warp.
  - All three RNG arrays (island noise, warpX, warpY) are always filled in the same order
    regardless of path, so downstream stages see identical RNG state with or without shape input.
  - Dimension guard: throws `ArgumentException` if shape mask dimensions differ from domain.
- No new `MapLayerId` or `MapFieldId` entries.
- No `PCGMapVisualization` patch required: no new stage, no new layer; lantern always runs F2b
  path (no shape injection); shape-path visual testing deferred to future editor tooling.
- F2b goldens unchanged (no-shape path is bit-for-bit identical).
- New F2c shape-path goldens locked: Land=`0xD986402B40273547`, DeepWater=`0xD5F1514F5471CC2F`
  (64×64, seed=12345, center-circle radius=20).
- New test coverage: `Stage_BaseTerrain2D_WithShapeInput_IsDeterministic`,
  `Stage_BaseTerrain2D_WithShapeInput_LandSubsetOfShape`,
  `Stage_BaseTerrain2D_WithShapeInput_GoldenHashes_Locked`,
  `MapPipelineRunner2D_GoldenHash_F2cShapePath_IsLocked`.
- `map-pipeline-by-layers-ssot.md` updated: boundary advanced to Phase H; F2c contracts added.
- `PCG_Roadmap.md` updated: F2c marked Done; Phase H marked Next.
- `CURRENT_STATE.md` updated: F2c recorded as resolved; immediate next focus set to Phase H.

## 2026-04-02 (Phase F2b)
- Phase F2b — Island Shape Reform implemented and test-gated.
- `Stage_BaseTerrain2D` reformed: circular radial falloff replaced with ellipse + domain-warp silhouette.
- Shape pipeline: ellipse (x-axis scaled by 1/islandAspectRatio) → domain warp (two WarpCellSize=16 noise
  arrays displacing the sampling point) → smoothstep radial falloff → height perturbation noise.
- New `MapTunables2D` fields: `islandAspectRatio` (clamped [0.25, 4.0], default 1.0),
  `warpAmplitude01` (clamped [0, 1], default 0.0).
- Both warp noise arrays always consumed from ctx.Rng regardless of warpAmplitude01 value,
  keeping total RNG consumption count tunable-independent for all downstream stages.
  RNG consumption order: island noise → warpX → warpY (stable regardless of tunables).
- aspect=1.0 + warp=0.0 => geometrically identical circle to pre-F2b; goldens differ.
- No new `MapLayerId` or `MapFieldId` entries introduced.
- All F2–Phase G golden hashes re-locked in one migration pass. Phase G goldens locked for first time.
- `PCGMapVisualization` patched: new Inspector header "F2 Tunables (Island Shape — Ellipse + Warp)"
  with `islandAspectRatio` and `warpAmplitude01` fields; dirty tracking and MapTunables2D construction
  updated; `BaseTerrainStage_Configurable` updated to mirror Stage_BaseTerrain2D exactly (including
  WarpCellSize=16 constant and BilinearSample helper).
- `map-pipeline-by-layers-ssot.md` updated: boundary advanced to F2c; tunables list extended
  with new F2b fields; F2b shape pipeline contracts added under Stage_BaseTerrain2D.
- `PCG_Roadmap.md` updated: F2b marked Done; F2c marked Next; status snapshot updated.
- `CURRENT_STATE.md` updated: F2b noted as resolved; immediate next focus set to F2c.

## 2026-04-02 (Phase G)
- Expanded the implemented Map Pipeline by Layers slice from F0–F6 to F0–F6 + Phase G.
- Recorded Phase G — Morphology as implemented and test-gated rather than planning-only.
- Added `MaskMorphologyOps2D` as the deterministic morphological operator surface:
  - `Erode4Once`: single-pass 4-neighborhood erosion (cell is ON iff all 4 cardinal neighbors are ON in src)
  - `Erode4(radius)`: multi-pass erosion via ping-pong with one Allocator.Temp buffer
  - `BfsDistanceField`: multi-source BFS distance field; seeds enqueued row-major; unreached cells receive -1f sentinel
- Added `Stage_Morphology2D` as the implemented morphology stage for the active slice.
- Authoritative outputs: `LandCore` mask, `CoastDist` scalar field.
- New append-only IDs: `MapLayerId.LandCore = 11` (COUNT → 12), `MapFieldId.CoastDist = 2` (COUNT → 3).
- Stage tunables are stage-local: `ErodeRadius` (default 3), `CoastDistMax` (default 0 = auto: min(w,h)/2).
- Contracts:
  - `LandCore ⊆ Land`; `LandCore ⊆ LandInterior` (guaranteed when ErodeRadius >= 1).
  - `CoastDist == 0f` at all LandEdge cells.
  - `CoastDist > 0f` at all LandInterior cells reachable within CoastDistMax steps.
  - `CoastDist == -1f` at all non-Land cells and cells beyond CoastDistMax.
- `Stage_Morphology2D` does not consume `ctx.Rng` (no noise, no randomness).
- Added Phase G stage-level golden gate (`StageMorphology2DTests`: LandCore mask hash + CoastDist field hash).
- Added Phase G pipeline golden gate (`MapPipelineRunner2DGoldenGTests`).
- Field hash uses FNV-1a over float bits (math.asuint), matching the spirit of MaskGrid2D.SnapshotHash64.
- Patched `PCGMapVisualization` with `enableMorphologyStage` toggle and `stagesG` array.
- Updated `PCG_Roadmap.md`:
  - Phase G marked done; entry expanded with implementation details.
  - Phase F2b added (planning only): island shape reform — domain warping, parameterized silhouettes, golden migration.
  - Phase F2c added (planning only): arbitrary shape input — image/mask/Voronoi cell as base terrain outline.
  - Phase J and K enriched with explicit archipelago support intent (Level 3 island shape vision).
  - Status snapshot updated: F6 done, Phase G done, F2b next (later), F2c later.
- Updated `CURRENT_STATE.md`: implemented slice reads F0–F6 + Phase G; next focus is Phase F2b.
- Updated `map-pipeline-by-layers-ssot.md`:
  - Scope and boundary extended to Phase G.
  - Phase G contracts added under `Stage_Morphology2D` and `MaskMorphologyOps2D`.
  - `LandCore` and `CoastDist` added to active registry contracts.
  - Phase G added to implemented surface (operators + stage + outputs + lantern).
  - Test-gated behavior list extended with morphology stage gates and Phase G pipeline golden.
  - Determinism rules extended with scalar field hash gate note.
  - Known limitations updated: CoastDist lantern limitation noted.
  - "Not governed here" updated from Phase G+ to Phase F2b+.
- No new subsystem SSoTs created. No authority decisions changed.
- Advanced the active roadmap so Phase F2b becomes the next planned implementation slice.

## 2026-04-01 (F6)
- Expanded the implemented Map Pipeline by Layers slice from F0–F5 to F0–F6.
- Recorded F6 — Traversal as implemented and test-gated rather than planning-only.
- Added `Stage_Traversal2D` as the implemented traversal stage for the active slice.
- Authoritative outputs: `Walkable` mask, `Stairs` mask.
- Contracts:
  - `Walkable` = `Land AND NOT HillsL2`; shore-edge land (LandEdge) is included; only hill peaks excluded.
  - `Stairs` = `HillsL1 AND NOT HillsL2` cells 4-adjacent to at least one `HillsL2` cell.
  - `Stairs ⊆ Walkable`; `Walkable ∩ HillsL2 == ∅`; `Stairs ∩ HillsL2 == ∅`.
  - `Stairs` may be empty on flat maps; not a defect.
- `Stage_Traversal2D` does not consume `ctx.Rng` (no noise, no randomness).
- Decided and recorded: `MapLayerId.Paths` write deferred to Phase O; F6 does not produce it.
  Paths depends on Phase N (POI placement) as a design prerequisite — paths connect places, and
  the places are not known until Phase N.
- Added F6 stage-level golden gate (`StageTraversal2DTests`) and F6 pipeline golden gate
  (`MapPipelineRunner2DGoldenF6Tests`).
- Patched `PCGMapVisualization` with `enableTraversalStage` toggle and `stagesF6` array.
- Updated `PCG_Roadmap.md`:
  - F6 entry rewritten: Walkable + Stairs only; Paths removed from scope.
  - Added Phase O — Traversal Network / Paths after Phase N.
  - Phase N enriched with RPG-style POI suitability examples (coastal village, forest dungeon,
    cave entrance at Stairs cells, open-plains camp).
  - Phase O records `MapLayerId.Paths = 5` as pre-registered; write ownership assigned here.
  - Status snapshot updated: Phase O added as "later (planning only)".
- Updated `CURRENT_STATE.md`: implemented slice reads F0–F6; immediate next focus is Phase G.
- Updated `map-pipeline-by-layers-ssot.md`:
  - Scope and boundary extended to F6.
  - F6 contracts added under `Stage_Traversal2D`.
  - F6 added to implemented surface (stage + outputs + lantern).
  - Test-gated behavior list extended with traversal stage gates.
  - `Paths` deferral note added (Phase O).
  - "Not governed here" updated.
- No new subsystem SSoTs created. No authority decisions changed.
