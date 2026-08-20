# Current State

Status date: 2026-08-20 (Phase W in progress — W.a, W-aux.a, W-aux.b, W-aux.c, W-aux.d closed;
W-aux.e audit closed with no code change; W-aux.f and W-aux.g closed; authoring track X1.a closed)

## What is active now
- The Islands documentation migration was handled as Tier L and is now materially closed for the reviewed snapshot corpus.
- The old documentation tree is kept as a fixed snapshot under `Documentation-snapshot/`.
- The new governed documentation root is `Documentation~/`.
- The promoted subsystem authority surfaces remain the PCG core and the implemented Map Pipeline by Layers slice.

## What is implemented now (confirmed for documentation authority purposes)
- New PCG runtime direction: grid-first, deterministic, adapters-last.
- Map Pipeline by Layers implemented slice: **F0–N6 + Phase M + M-fix.a/c + M2.a + M2.b + Phase L + L→M + L-fix.a (revised) + Phase V (read-only inspection tooling: V.a + V.b) + Phase Q (Q + Q-fix.a, adapter-side) + Phase W.a + W-aux.a + W-aux.b + W-aux.c (blocks 1–3) + W-aux.d + W-aux.f + W-aux.g (all golden-captured
  where applicable; W-aux.e was a measurement audit with no code change)**, plus the authoring
  track **X1.a** (Editor-only, golden-neutral).
- Layout strategies are an implemented, test-gated support surface under PCG.
- GraphLibrary runtime is a real implemented surface, but it is **not** promoted subsystem authority.
- Noise runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Mesh runtime is real and coherent, but it is currently a governed reference / staged support surface, not a promoted subsystem SSoT.
- Surfaces runtime contains real jobs, but the surface layer remains mixed with sample orchestration and is currently governed reference / staged support, not a promoted subsystem SSoT.
- Shader assets and HLSL helpers are active support artifacts, but not a promoted subsystem SSoT.

## What current package development just resolved
- **W-aux.g — Hills window recalibration. RESOLVED 2026-08-20.**
  `HillsL2` now lands on its declared target consistently across seeds. The mechanism changed:
  `hillsL1` / `hillsL2` are **area fractions of `Land`** resolved per run by an order statistic
  (`HillsThresholdOps2D.ComputeAreaThresholds`), replacing the N5.e remap that expressed them as
  fractions of the interval `[waterThreshold01, 1.0]`. That interval stopped matching the field
  at W-aux.f — `Height` no longer reaches 1.0, and its maximum varies by seed — so the top of
  the range was dead space of seed-varying width and no fixed pair of thresholds could hold a
  window.

  Measured with `LogHeightHistogram`, `Default_MapPreset`, res 256, seeds 56 / 8 / 243:

  | | before (fixed thresholds) | after (area quantile) |
  |---|---|---|
  | band `>= thL2`, % of `Land` | 9.86 / 9.36 / 2.60 | **20.13 / 20.30 / 20.20** |
  | spread across seeds | 7.3 points (3.8×) | **0.17 points** |
  | hills band total (L1+L2) | 56.25 / 55.66 / 42.77 | 55.12 / 55.09 / 55.26 |
  | resolved `thL2` | 0.8834 (fixed) | 0.8455 / 0.8370 / 0.7706 |

  Targets are 55 % hills / 20 % peaks; defaults moved from `hillsL1` 0.30 / `hillsL2` 0.43
  (range fractions) to 0.55 / 0.20 (area fractions). The two sets of numbers are not comparable.

  Tie handling: the whole tie class at the cut enters the band, so the realized fraction is
  ≥ target and no spatially biased tie-break exists — the objection that made W-aux.e reject
  the quantile died with the `Height == 1.0` atom. Ten golden constants re-anchored (table in
  `changelog-ssot.md`, entry W-aux.g); `LandEdge` and `LandInterior` verified unchanged as a
  control. Full EditMode suite green, user-confirmed. Visual smoke test performed at the three
  reference seeds: silhouettes unchanged, peaks coherent with the `Height` overlay, no corner
  bias, peak extent comparable across seeds.

  Side effect worth recording: F3b′ removes the last coupling between Hills and
  `waterThreshold01`.

- **W-aux.f — Height ceiling saturation. RESOLVED 2026-08-20.**
  The `Height == 1.0` plateau reported by the W-aux.e audit is gone. `Stage_BaseTerrain2D`
  now normalizes the perturbed height by `(1 + terrainAmp/2)` — the theoretical maximum of
  `mask01·(1 + (n − 0.5)·terrainAmp)` — instead of relying on `math.saturate` to absorb the
  overflow. `Height == 1.0` is therefore reachable only at `n == 1` (measure ≈ 0 for
  normalized fBm), never by clipping. `terrainAmp == 0` gives a normalization factor of
  exactly `1f`, so unperturbed presets are bit-identical.

  Measured with the `LogHeightHistogram` probe, `Default_MapPreset`, res 256,
  seeds 56 / 8 / 243:

  | | before | after |
  |---|---|---|
  | cells at `Height == 1.0` | 2601 / 2602 / 748 | **0 / 0 / 0** |
  | cells at `Height > 1.0` | 0 | 0 |
  | max `Height` | 1.0 | 0.95957 / 0.96334 / 0.93824 |
  | min `Height` (sea floor) | 0.09440 | 0.08242 |
  | min `Height` on land | — | 0.41232 |
  | shaping-chain mirror mismatch | 0 | 0 |

  The former atom fanned out into the top band as predicted: in seed 56 the cells above the
  theoretical destination (0.8731 post-pow) total ≈ 2580 against the 2601 of the original
  atom (bin-interpolated, not cell-exact).

  `saturate` is retained as a guard; it is a no-op except for floating-point rounding.
  `BaseTerrainStage_Configurable` was patched in the same step (2 sites, one per shape
  branch), as was the adapter-side probe's shaping-chain re-derivation.

  **`waterThreshold01` recalibration.** The normalization rescales the land field by a
  constant, so every calibrated water threshold moved by `(1 + amplitude/2)^(−exponent)` to
  hold its coastline in place:

  | Entry point | before | after | factor inputs |
  |---|---|---|---|
  | `MapTunables2D.Default` | 0.50 | **0.42553192** | amp 0.35, exp 1.0 |
  | `MapGenerationPreset` (class field default) | 0.50 | **0.42553192** | mirrors the above |
  | `PCGMapVisualization` (serialized default) | 0.50 | **0.42553192** | mirrors the above |
  | `PCGMapCompositeVisualization` (serialized default) | 0.50 | **0.42553192** | mirrors the above |
  | `PCGMapTilemapVisualization` (serialized default) | 0.50 | **0.42553192** | mirrors the above |
  | `Default_MapPreset.asset` | 0.472 | **0.412119** | amp 0.22, exp 1.3 |

  Serialized component instances in existing scenes keep their own saved values; only newly
  added components pick up the new default. The threshold is **not** self-adjusting: a
  silently compensating threshold would hide recalibration from the goldens.

  Verified consequence: **Land topology is bit-identical.** `Land`, `DeepWater`, `LandCore`,
  `LandEdge`, `LandInterior` and `Lakes` goldens passed unchanged in every fixture —
  ellipse, rectangle and shape-input — across the full EditMode suite. 29 golden constants
  that depend on the *value* of Height were re-anchored; see `changelog-ssot.md`.
- **W-aux.e — Threshold-mapping audit. CLOSED 2026-08-19, no code change.**
  Asked whether `Stage_Hills2D` (`Height >= hillsThresholdL1/L2`) and
  `Stage_BaseTerrain2D` (`Land = Height >= waterThreshold01`) carried the same defect
  W-aux.d fixed in vegetation. Verdicts: `waterThreshold01` is a **feature** — it is a
  coordinate, never presented as a fraction, and `pow` is a documented reshaper whose
  purpose is to move mass; `Stage_Hills2D` is **exonerated** — it implements its contract
  faithfully; the defect was upstream, in the height composition formula, and is now fixed
  (W-aux.f block above). The audit changed no core code, no tunable, no contract and no
  golden — that is its result, not an omission.
  Its instrument re-derived the BaseTerrain shaping chain and reported **0 mismatches,
  maxAbsDiff 0** against the exported field at all three seeds, which is why its attribution
  counts as measurement rather than as a reading of code.
  Two side findings retained as separate debt: the `Noise.GetFractalNoise` normalization
  (`amplitudeSum` accumulated after `amplitude *= persistence`) is wrong but is **not** a
  remedy for anything the audit found; and `Default_MapPreset`'s `heightRemapCurve` is
  functionally identity while `ScalarSpline.IsIdentity` conservatively reports `false`,
  costing one `Evaluate` pass per cell.
- **W-aux.d — Vegetation quantile mapping. DONE 2026-08-19.**
  `Stage_Vegetation2D` accepts cells by a **global quantile cut over the eligible
  population**, replacing the absolute threshold `1 − vegetationDensity`. The noise field is
  untouched (same salt `0xB7C2F1A4`, frequency 4, 3 octaves, lacunarity 2, persistence 0.5,
  `quantSteps` 1024); only the cut point moves, so spatial character is preserved. Cost:
  two passes over the domain plus `O(BiomeType.COUNT · QuantSteps)`, previously one pass.
  Biome `vegetationDensity` values now mean what their names say — a fraction of the
  eligible population — with one caveat that is contract surface, not detail: biomes are cut
  against the **global** distribution, so per-biome coverage is not guaranteed to equal `d`,
  and any change to the eligibility policy (including flipping `vegetatesOnPeaks` on a
  single biome) shifts the threshold of every biome. See M2a-9 in
  `map-pipeline-by-layers-ssot.md`.
  Densities have **not** been recalibrated since the mapping changed. Measured aggregate:
  `Vegetation` is 6.30 / 6.22 / 6.88 % of the map at res 256, seeds 56 / 8 / 243.
  Gate: `M2a_QuantileCut_IsExact_Nested_AndAboveNominal` replaces
  `M2a_CoverageMonotonicity_DenseBiomesExceedSparseBiomes`, which passed throughout the
  period the mapping was broken and was removed rather than relaxed.
- **X1.a — `Islands.PCG.Editor.MapGenerationPresetDiagnostics` (authoring track). DONE 2026-08-19.**
  Pure Editor-side preset analysis: `preset → List<PresetFinding>`. No pipeline execution,
  no asset mutation, no UI dependency. Golden-neutral: nothing in this surface can alter
  generation output.

  Six rules, all computed from preset fields plus static tables:

  | Rule | Fires when | Backing |
  |---|---|---|
  | `R1.DegenerateFalloff` | `islandSmoothFrom01 >= islandSmoothTo01` | inferred |
  | `R2.InvisibleMidWaterBand` | `midWaterDepth01 > 0` and `<= shallowWaterDepth01` | inferred |
  | `R3.InertHillsNoise` | hills asset assigned, `hillsNoiseBlend == 0`, hills stage on | inferred |
  | `R4.ClampSaturationPlateau` | `islandSmoothFrom01 < 0.05` with terrain amplitude `> 0` | measured |
  | `R5.HotBandUnreachable` | land temperature ceiling `< 0.75` (Hot band lower bound) | measured |
  | `R6.ZeroCoverageDensities` | vegetation stage on, biomes with `0 < density < 0.4` | measured |

  `PresetFinding` carries `RuleId`, severity, `Backing` (`Measured` / `Inferred`) and, for
  measured findings, the named run. The type makes an unbacked measured claim
  unrepresentable rather than merely discouraged.

  **Preset diff.** `DiffJson(a, b)` compares `ToJson()` output line-wise, excluding the same
  non-reimportable surface the round-trip test excludes (`asset`, `stageTogglesNote`, the
  `derived` block). Known limitation: duplicate identical lines are compared as a set, so a
  moved duplicate is not reported.

  Gate: `MapGenerationPresetDiagnosticsTests` — one preset per rule, a clean preset that
  fires nothing, three diff gates. Green 2026-08-19.

  Known gap: **the wizard UI itself has no test.** The importer and the diagnostics logic
  are tested; window behavior (import lifecycle, Undo, panel rendering, scrolling, console
  log) is verified by manual inspection only.

  **Rule backing is now historical (recorded 2026-08-20).** `R4`'s subject — the
  `Height == 1.0` atom — was removed by W-aux.f, and `R6`'s density→coverage curve was
  measured under the pre-W-aux.d mapping. The rules still compute as specified; their named
  runs no longer describe the current pipeline. Re-basing them is not scheduled.
- **W-aux.c — Calibration instrumentation, preset wizard, per-biome vegetation policy. DONE 2026-08-19.**
  Three blocks:
  - **Block 1 — instrumentation.** `MapStatsExporter2D` gained `landHeightHistogram`,
    `biomeByBand` and `vegetationEligibility`, plus a second `ToJson` overload taking the
    run's effective `waterThreshold01`. The export contract carries data, not tunables, so
    the threshold is passed in rather than added to `MapDataExport`; the single-argument
    overload is preserved and reports the new sections as `"present": false`. Button-driven,
    outside the rebuild path, golden-neutral. `Default_MapPreset` was recalibrated in the
    same block (15 values; see `changelog-ssot.md`).
  - **Block 2 — preset authoring.** `MapGenerationPresetWizard` (EditorWindow) and
    `MapGenerationPresetJsonImporter` in `Islands.PCG.Editor` (`Editor/Windows/`): import
    (new asset / overwrite loaded), export, four-category diagnostic panel, and a W.b gap
    warning in the UI. `Islands.PCG.Tests.EditMode.asmdef` gained a reference to
    `Islands.PCG.Editor`. Gate: `MapGenerationPresetJsonRoundTripTests`, five tests.
  - **Block 3 — per-biome vegetation policy.** `BiomeDef.vegetatesOnPeaks` exists and
    `Stage_Vegetation2D` consults it per cell instead of applying a global `HillsL2`
    exclusion; contract M2a-3 reformulated accordingly, with the legacy fallback keeping the
    global exclusion. `true` for Tundra, BorealForest, Shrubland, TemperateForest,
    TemperateRainforest, Grassland, TropicalSeasonalForest, TropicalRainforest; `false` for
    Unclassified, Snow, TemperateDesert, SubtropicalDesert, Beach. Calibration, not
    contract — reversible without touching stage code.
    `MapStatsExporter2D.vegetationEligibility` reports `biomeEligible`,
    `peaksUnlockedByBiomePolicy`, `peaksBlockedByBiomePolicy`, `eligible`, `vegetated`.
  `hillsL2_fraction` was raised 0.62 → **0.65** in block 3. The declared 15–30 % `HillsL2`
  window was **not** met (30.07 % of Land at seed 56) and the window itself was later shown
  to be unreachable pre-W-aux.f; it is open again now that the height field is healthy — see
  `PCG_Roadmap.md`.
  Correction to a previously carried claim: the baseline stated that no prior assembly
  reference work was needed for the wizard. The test assembly did need the new
  `Islands.PCG.Editor` reference; the claim about `Islands.PCG.Editor.asmdef` itself held.
- **W-aux.b — Sea-floor relief (submarine terrain). DONE 2026-08-18.**
  Two tunables on `MapTunables2D`: `seaFloorLevel01` (mean floor elevation as a fraction
  of `waterThreshold01`) and `seaFloorAmplitude01` (relief amplitude as a fraction of
  `waterThreshold01`). Both default to `0.0` = flat ocean, bit-identical to pre-W-aux.b
  output. Per cell, applied in `Stage_BaseTerrain2D` **after** quantization, J2
  redistribution and the N2 spline, and **only** where `h01 < waterThreshold01`:

  ```
  floor01 = (seaFloorLevel01 + (terrainNoise01 − 0.5) · seaFloorAmplitude01) · waterThreshold01
  h01     = max(h01, clamp(floor01, 0, waterThreshold01 − 1e-4))
  ```

  **Contract — lower bound, never assignment.** Existing falloff gradients survive
  wherever they are higher, so the coastal shelf produced by `islandSmoothFrom01` and
  the abyssal floor compose instead of competing. The hard clamp to
  `waterThreshold01 − 1e-4` guarantees `Land ⊆ (h01 ≥ waterThreshold01)` under **every**
  tunable value, not merely at the default — therefore `Land`, `DeepWater`, `CoastDist`,
  morphology, hills, regions, vegetation and the whole biome histogram are invariant
  under this feature.
  **RNG parity.** The step reuses the terrain noise array already sampled by the stage;
  zero additional draws, consumption parity unchanged.
  **Hydrology is unaffected by construction:** `HeightFieldHydrologyOps2D` treats
  non-Land cells as height 0 (initialisation and D8 neighbour handling), so submarine
  elevation is invisible to fill, flow direction and accumulation.
  **Known interaction:** the floor is applied after `heightQuantSteps`, so it is not
  terraced. With low quantization values land shows terraces while the sea floor stays
  smooth. Deliberate — moving the step before quantization would expose it to
  land-tuned redistribution and spline reshaping.
  No golden was broken (identity default). `ShallowWater` and `MidWater` now denote
  depth bands rather than pure adjacency once the feature is enabled.
- **W-aux.b — Duplicated base terrain implementation (structural fact).**
  `BaseTerrainStage_Configurable` (`Runtime/PCG/Samples/Presets/`, asmdef
  `Islands.PCG.Samples.Shared`) is a full second implementation of the base terrain
  height mathematics, mirroring `Stage_BaseTerrain2D` and carrying the same stage salts.
  `PCGMapTilemapVisualization` instantiates **this mirror**, not the governed stage, so
  every visual inspection, hover tooltip, exporter statistic and console golden produced
  through the Tilemap adapter measures the mirror.
  The governed EditMode golden tests exercise `Stage_BaseTerrain2D`. **No test asserts
  that the two produce identical output.** A change applied to one and not the other
  passes all tests while silently changing — or failing to change — every visual and
  exporter result. W-aux.b had to patch both.
  Evidence: `PCGMapTilemapVisualization.cs` L333 / L1053 (field declaration and
  instantiation); `BaseTerrainStage_Configurable.cs` class doc ("Keep this class in sync
  with `Stage_BaseTerrain2D`"), identical stage salts, mirrored hot loop.
- **W-aux.a — `Islands.PCG.Inspection.MapStatsExporter2D` (inspection surface). DONE 2026-08-17.**
  Static read-only function over `MapDataExport` returning a JSON statistics snapshot:
  dimensions and seed; min/max/mean per `MapFieldId`; count and percentage per all
  15 `MapLayerId`; full 13-entry `BiomeType` histogram with zero-count biomes listed
  explicitly; region count; vegetation broken down by biome. Diagnostic surface only —
  not authority, not a golden. Region count is derived from distinct positive values of
  `BiomeRegionId` (0 = water sentinel, `Stage_Regions2D` invariant R-2), deliberately
  not from `Stage_Regions2D.LastBuiltRegistry`, so the function stays decoupled from the
  stage and does not depend on region-id contiguity.
  Invoked from the **"Log Map Stats (JSON to Console)"** button in
  `PCGMapTilemapVisualizationEditor` (Diagnostics section). Reads the live context
  without forcing a rebuild; warns instead of throwing when no context exists.
  O(cells) per field and layer, hence button-driven and never wired into the
  per-rebuild path.
  **Extended by W-aux.c block 1 and block 3** — see the W-aux.c resolution block above.
  Not test-covered; the gap is declared in `coverage-matrix.md`.
- **Phase W.a — World-scale pipeline run. DONE 2026-08-09.**
  A 64×64 world-scale run driven from a `MapGenerationPreset`, visually smoke-validated.
  Confirms the resolution-agnostic claim in `Phase_W_Design.md` §5: the same stages run
  unchanged at world scale. World identity is `shapeMode = Ellipse` (see
  `Phase_W_Design.md` §5). No core change; no new layer, field or stage.
  The W.a console golden (seed 56, 64×64) was captured in-session but **is not yet
  registered in a governed surface** — see `Phase_W_Pending_Doc_Updates.md` §3.
- **Phase Q — Biome-Conditional Tile Selection (adapter-side). DONE 2026-08-09.**
  Closes the documented gap between Phase M (produces `MapFieldId.Biome`) and the tilemap
  adapter (previously ignored it entirely).
  - `BiomeTileOverride` ScriptableObject: additive per-biome tile overrides layered over a
    base `TilesetConfig`. Flat `TileBase[]` lookup of size
    `BiomeType.COUNT × MapLayerId.COUNT`, O(1) per cell, rebuilt lazily and on `OnValidate`.
  - `TilemapAdapter2D.ApplyBiomeAware` / `ApplyLayeredBiomeAware`: biome-aware stamping
    overloads. A null override delegates to `Apply` / `ApplyLayered`, so pre-Q behavior is
    byte-identical.
  - `PCGMapTilemapVisualization`: `biomeTileOverride` Inspector field,
    `StampMultiLayerBiomeAware`, and override-content hashing for dirty tracking.
  - Zero new `MapLayerId`, zero new `MapFieldId`, zero new stages, zero changes to
    `MapPipelineRunner2D`, zero golden impact. Purely adapter-side.
  - Hard biome boundaries by design. Transition blending remains Tier 1, not built.
  - `BiomeRegionId` (M2.b) is not consumed by Q — separate axis.
  - Mega-tiles (H8) run as a post-pass after stamping and therefore win unconditionally
    over biome-varied tiles. Documented as a non-goal with defined behavior.

  **Batch Q-fix.a** — four defects found on review of the previously unregistered
  implementation, all adapter-side, all fixed and regression-tested:

  | Id | Defect | Fix |
  |---|---|---|
  | Q-BUG-1 | Override was ignored when the base layer entry had a null tile — layer masks were only cached when a base tile existed | Cache layer masks unconditionally; guard `if (resolved != null)` in the per-cell scan preserves `Apply()` parity |
  | Q-BUG-2 | The collider group was routed through the biome-aware path, so an override on `HillsL2` or `Lakes` replaced the collider sentinel tile with biome art | Collider group stamped separately via `ApplyLayered`; never biome-aware |
  | Q-BUG-3 | Biome field read with `(int)` truncation while `PCGHoverTooltip` (V.a) and `PCGRuntimeOverlay` (V.b) use `Mathf.RoundToInt` | Adapter now uses `Mathf.RoundToInt` |
  | Q-BUG-4 | The Phase Q hash block sat after the `tilesetConfig == null` early return, so override edits could miss dirty tracking | Hash block moved above the early return |

  Test coverage: `BiomeTileOverrideTests` 13 → 16 tests. Q-BUG-2 has no unit coverage —
  `StampMultiLayerBiomeAware` is private with no test seam — and is verified by the smoke
  protocol only. Logged as test debt.

  **Utility Q-aux.a** — `BiomeTileOverridePlaceholderGenerator`, editor-only. Generates
  flat-color placeholder `Tile` assets per (biome, layer) slot so a biome-conditional setup
  can be smoke-tested before any real art exists. Colors mirror the `BiomeColorPalette`
  defaults (V.b), making "painted tile color == overlay color" a direct cross-check.
  Zero runtime code.
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
  See `planning/active/Phase_V_Design.md` for design contracts.
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

## Temporary measurement probes (adapter-side, retained by decision 2026-08-19)

Two non-governed probes live on `PCGMapTilemapVisualization`, each with an Inspector button
on `PCGMapTilemapVisualizationEditor`:

- `LogVegetationNoiseHistogram()` (W-aux.d) — vegetation noise distribution across three
  populations plus a quantile-cut preview per biome.
- `LogHeightHistogram()` (W-aux.e, updated in W-aux.f) — `Height` distribution across three
  populations (all / `Land` / below the water threshold), saturation attribution by shaping
  step, the cuts that would realize a target `HillsL2` fraction, and a pow/spline sweep of
  `Land` drift at fixed `waterThreshold01`. Its self-check reports a mismatch count against
  the exported field and declares itself invalid rather than reporting untrustworthy
  numbers — that counter is what caught the desynchronization during W-aux.f.

Both mirror private constants or arithmetic from the stages they measure and will drift
silently if those stages change; each carries a `TEMPORARY` header saying so. Retained
deliberately — the intent is to feed this data into graphed visualizations later — rather
than deleted after their originating batch. They read the last-built context: no rebuild,
no RNG, no core dependency.

**Formula-copy coupling (recorded 2026-08-20).** `LogHeightHistogram` holds a **third** copy
of the height composition formula, after `Stage_BaseTerrain2D` and
`BaseTerrainStage_Configurable`. Only the first two are governed. The coupling is registered
in `map-pipeline-by-layers-ssot.md` §F2 so a future change to the formula names all three
places. Promoting the probe to a governed diagnostic surface, or scheduling its removal,
remains undecided.

## Open observations

Findings that surfaced during the W-aux.c…W-aux.f run with no owning batch, recorded here so
they survive their originating sessions. **All are UNVERIFIED as stated** and none is fixed;
each names where it would have to be checked.

- **`Stage_Vegetation2D.moistureModulation` may be unreachable.** Public field, default
  `0.0f`, gates the moisture modulation. Whether anything in the pipeline construction path
  assigns it was never checked. If nothing does, it is the same pathology as the five
  component-scoped fields and `Stage_Regions2D.SpeckThreshold`: a tunable that exists in code
  but cannot be reached from a preset. **Belongs to the W.b field inventory.**
- **Vegetation noise frequency as a spatial-character lever.** `Stage_Vegetation2D` uses
  `NoiseFrequency = 4` / 3 octaves. Its original motivation — the broken density semantics —
  died with W-aux.d, which proved the cause was value mapping, not grain. What remains is a
  live aesthetic question: continuous lobes versus scatter. Raising the frequency breaks the
  vegetation goldens. Not scheduled.
- **`Showcase (MapGenerationPreset)` does not appear in the package tree.** The W-aux.b
  calibration reference in `changelog-ssot.md` is stated against `Showcase @256`, so those
  numbers are not reproducible from the repository alone until the asset is committed under a
  confirmed name and path. Carried since 2026-08-18, re-confirmed since.
- **The exported `HillsL2` layer is still seed-dispersed even though the threshold band is
  not.** 11.19 / 21.71 / 28.34 % of `Land` at seeds 56 / 8 / 243 against a band held at
  ~20.2 % (`Default_MapPreset` @256, `hillsNoiseBlend = 0.35`), and the sign of the deviation
  flips between seeds. Inferred cause, not verified: the ±0.0525 blend offset flips a number
  of cells that depends on the local density of the height histogram on each side of the cut,
  and that density is asymmetric per seed. Declared targets are measured pre-blend by
  contract (`map-pipeline-by-layers-ssot.md` §F3b′), so this is a live expressiveness
  question, not a defect. Resolving it would mean resolving the quantile after the blend.
- **`Default_MapPreset.waterThreshold01` may carry the wrong calibration value.** The asset
  reads **0.425532**, which is the compensated value for the *code default* parameters
  (amp 0.35, exp 1.0). The asset's own parameters are amp 0.22 / exp 1.3, whose compensation
  factor is 0.873132. The value recorded for this asset at W-aux.f was 0.412119, and
  `0.412119 / 0.873132 = 0.47201` — consistent with a pre-W-aux.f asset threshold of 0.472.
  Inference (arithmetic checked 2026-08-20, asset history not checked): the field was
  overwritten with the code default instead of its own compensated value, putting the
  preset's coastline higher than calibrated. Not a Hills concern — W-aux.g decoupled Hills
  from `waterThreshold01`. Needs a decision: correct the asset, or accept 0.425532 and record
  it as the intended value.
- **Perceptual effect of the W-aux.f fix on `Temperature` and biomes was never inspected.**
  Land is now ~12.7 % lower on formerly-clipped cells; with `lapseRate = 0.62` that warms the
  core. The hashes changed and the goldens were re-anchored, but nobody looked at the result.
- **No visual smoke test was run on the new `Height` field.** W-aux.f was validated by
  histogram statistics and hashes only. **Partially addressed 2026-08-20:** the W-aux.g smoke
  test included a `Height` overlay cross-check at the three reference seeds and found it
  coherent, but it was run to validate hill placement, not the `Height` field as such.
- **`Height` goldens for `Default_MapPreset` @256 were not recaptured after W-aux.f.** The
  W-aux.e captures (`196AF8D87C1D19EC` / `58707DC13667740C` / `8067C0763C948648`, seeds
  56 / 8 / 243) are superseded with no replacement. These are calibration references, not
  gates — no test consumes them.
- **Optional test-hygiene edits from W-aux.f may or may not have been applied:**
  `StageHills2DTests.N5d_BlendPositive_DiffersFromBlend0` and the explanatory comment on
  `StageBaseTerrain2DTests.NoShapeTunables()`. Never confirmed.

## Measured calibration baselines and climate reachability

Reference points for preset calibration, produced by `MapStatsExporter2D` (W-aux.a) and
the W-aux.b differential. **Not goldens.** They constrain how presets are tuned; they do
not gate anything and no test depends on them.

### Measured biome baselines (seed 56)

| Run | Non-zero biomes | Whittaker cells reached | Land |
|---|---|---|---|
| preset `Default`, 64×64 | 7 | 6/16 | 32.25% |
| preset `Showcase`, 64×64 | 10 | 9/16 | 28.44% |
| preset `Showcase`, 128×128 | 10 | 11/16 | 28.45% |
| preset `Default`, 256×256 | 9 (incl. Snow 20, Tundra 6923) | — | — |
| preset `Showcase`, 256×256 | 10 | — | — |

`Showcase` @128 is the best measured calibration at that resolution: same ten biomes as
@64 but with five of them substantial rather than ≤9 cells.

Across all five measured runs the only biome never reached is **`SubtropicalDesert`**.
Snow and Tundra, unreachable at 64×64 and 128×128, are reached at 256×256 — which
reinforces the resolution-dependence finding below rather than contradicting the Cold
ceiling analysis, which was derived for 64×64 with `latitudeEffect` and lapse rate held
at preset values.

### `Height == 0` on water is a preset property, not a pipeline invariant

There is no code path that forces water to zero height. The outer ocean is flat because
`radial01Sq = math.saturate(distSq * invRadiusSq)` saturates at 1
(`Stage_BaseTerrain2D` ~L220), so with `islandSmoothTo01 = 1` every cell beyond
`islandRadius01` gets `mask01 = 0` exactly and therefore `h01 = 0`. **Inside the falloff
disk**, lowering `islandSmoothFrom01` toward 0 produces an annulus with
`mask01 ∈ (0, waterThreshold01)` — water carrying a real height gradient.

Measured consequence: the `Showcase` preset (`islandSmoothFrom01 = 0`) already produced
partial submarine relief before W-aux.b, with zero core changes. `ShallowWater` moved
from 4.79% to 15.97% because the height band woke up. The arithmetic closes exactly:
`Default` 196 shallow + 2579 mid = 2775 = total water; `Showcase` 654 + 2277 = 2931 =
total water.

### `Lakes` — why it measured 0, and how it was reached

`Stage_Shore2D` classifies `ShallowWater` = adjacency ring ∪
(`h ≥ waterThreshold01 − shallowWaterDepth01`). At the values that produced the empty
runs that threshold is `0.472 − 0.451 = 0.021`, so every water cell above 0.021 is
`ShallowWater`. Since `Lakes = NOT Land ∧ NOT DeepWater ∧ NOT ShallowWater`,
`ShallowWater` absorbed inland water entirely and `Lakes` was necessarily empty.
Confirmed by hover inspection: interior pond cells read `Height = 0.431` and `0.471`
(both below `waterThreshold01 = 0.472`) and were labelled `ShallowWater`.

`DeepWater` is *border-connected* NOT-Land via stable flood fill
(`Stage_BaseTerrain2D` L19), not simply NOT-Land, so enclosed water is measurable:
`Showcase` @128 has 11723 water cells vs 11665 `DeepWater` → **58 enclosed cells**
available to become lakes. `Default` has 0 enclosed cells.

**Interpretation note (X1.a, 2026-08-19).** Because `DeepWater` is border-connectivity and
not a depth classification, `ShallowWater` and `MidWater` are refinements layered *on top of*
it — `Stage_Shore2D` states `ShallowWater ∩ DeepWater` is intentionally non-empty and never
mutates `DeepWater`. Their counts therefore overlap by design and their sum exceeding total
water is expected, not a bug. Any "% of deep ocean" read from this layer is wrong; the
correct phrasing is **"% of sea (border-connected water)"**. Applies to `MapStatsExporter2D`
output and to any calibration reasoning built on it. This corrects interpretation only; no
generation output changes.

`Lakes == 0` was therefore a calibration outcome, not a defect — and it is reachable
without any core change to `Stage_Hydrology2D`. It requires (a) enclosed water, not
border-connected, and (b) a `shallowWaterDepth01` band narrow enough that the enclosed
cells are not swallowed by `ShallowWater`. The 4-connected adjacency ring is always
`ShallowWater` regardless, so ponds smaller than roughly 3×3 can never contain a lake
cell. **Measured: `Lakes = 3`**, `Showcase` @256 seed 56, `shallowWaterDepth01 = 0.10`,
sea-floor relief enabled — the first non-empty `Lakes` layer recorded in the workstream.
Layer hash `8CEDA6FE410EAD3A` (previously `B339AEBFA674B70A` for the empty layer).

Separately: an inland lake is always `Biome = Unclassified` by contract M-2
(`Biome = 0` for all non-Land cells). Lake identity lives in the `Lakes` layer, never in
the `Biome` field.

### Climate output is not scale-invariant

Verified in source (`Stage_Biome2D` L219 and L291):

```
coastMod    = coastModerationStrength / (1 + max(cd, 0))
coastFactor = coastalMoistureBonus    / (1 + max(cd, 0) * coastDecayRate)
```

`cd` is `CoastDist`, a BFS distance measured **in cells**. Doubling resolution doubles
the island's width in cells, so interior cells become arbitrarily far from the coast and
both climate fields shift. Measured: `Showcase` 64 → 128 raised `CoastDist.max` from 12
to 23, dropping `coastFactor` at the interior from 0.109 to 0.063 and `Moisture.min`
from 0.213 to 0.135. The Dry moisture column became reachable, taking
**TemperateDesert from 1 to 291 cells and Grassland from 2 to 256**.

`coastModerationStrength`, `coastDecayRate` and `coastalMoistureBonus` operate on
`CoastDist` in cell units, so the same preset produces different biome distributions at
different resolutions — drier, more climatically varied interiors at higher resolution.
This has direct Phase W consequences: a 64×64 world map and a higher-resolution local map
do not agree on climate for the same tunables. Whether the coast terms should be
normalized by domain size is an open design question, not a bug to patch silently.

### Temperature reachability ceiling (derived at 64×64)

Verified arithmetic against `Stage_Biome2D` L220:
`T = baseTemperature − lapseRate·h − latitudeEffect·latNorm + coastMod ± noise`, with
`latNorm = |y/h − 0.5|·2` (0 at domain centre, 1 at top/bottom edges). Two independent
obstacles to reaching the Cold row (T < 0.25) on land at 64×64:

1. **`latitudeEffect` cannot cool a centred island.** With a centred island of
   `islandRadius01 ≈ 0.33–0.42` the
   island sits where `latNorm ≲ 0.33`, so the term contributes at most −0.165 on land.
   Latitude cools the border ocean, not the island. Raising it from 0 to 0.5 moved
   `Temperature.min` only to 0.333.
2. **`lapseRate` cannot serve Cold and Hot simultaneously under `[Range(0,1)]`.**
   Requiring `base − lapse·1 < 0.25` and `base − lapse·waterThreshold01 ≥ 0.75` yields
   `lapse ≥ 0.95` and `base ≥ 1.20`. But `MapGenerationPreset` L107/L111 clamp both to
   `[0, 1]`. At `base = 1.0, lapse = 1.0` the lowest land cell reads
   `T = 1 − 0.472 + 0.1 = 0.63` → Warm. Hot and Cold are mutually exclusive.

Reaching all four temperature bands on a single centred island **at this resolution**
requires widening the **land's** height span, not the climate tunables: lower
`waterThreshold01` (e.g. 0.22) so `lapseRate` has range to work with, compensating with a
smaller `islandRadius01`. This invalidates the current calibration of
`shallowWaterDepth01` / `midWaterDepth01`, which are tuned against 0.472.
`SubtropicalDesert` (Hot ∧ Dry) is a separate, non-structural problem: Hot lives on low
land, low land hugs the coast, and the coast is wet — break the correlation by lowering
`coastalMoistureBonus`, raising `coastDecayRate`, and raising `moistureNoiseAmplitude`.

**Re-measured 2026-08-19 (`Default_MapPreset` @256, seed 56).** The Hot-row ceiling on land
is confirmed, with new numbers. Land temperature ceiling before coastal moderation and noise
was `baseTemperature − lapseRate · waterThreshold01 = 0.82 − 0.62 · 0.472 = 0.527`; with the
maximum coastal term (+0.1) and noise (+0.05) it reached ~0.68, still below the Hot threshold
0.75. The measured `Temperature.max = 0.873757` belongs to **water** (runtime tooltip: a
border-connected water cell, `h = 0.190`, `T = 0.800`), not to land. `SubtropicalDesert`,
`TropicalSeasonalForest` and `TropicalRainforest` therefore sat at 0 cells by arithmetic, not
by defect. Standing decision unchanged: 10 non-zero biomes with real submarine relief is
preferred over 13 with a flat ocean.
**Caveat (2026-08-20):** this arithmetic uses `waterThreshold01 = 0.472`, which W-aux.f
recalibrated to 0.412119 for this preset. The ceiling was **not** re-derived against the new
value; treat the conclusion as directionally intact and the number as stale.

**Scope note.** This ceiling was derived for 64×64. At 256×256 the Cold row is reached
without any of the above (Snow 20, Tundra 6923 on `Default`), because the resolution
effect above widens `CoastDist` and therefore the climate span. The analysis constrains
low-resolution calibration, not the pipeline in general.

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
- **Phase V — settled.** V.a and V.b are both implemented and smoke-validated; see the
  resolution blocks above. Optional V.c quality-of-life items (e.g.
  `textMinCellScreenSize` readability threshold for the text overlay) are logged but not
  blocking. See `planning/active/Phase_V_Design.md` for contracts.
- **Phase Q — settled.** Implemented and closed 2026-08-09 (Q + Q-fix.a + Q-aux.a); see
  the resolution block above and `planning/active/Phase_Q_Design.md`. Remaining Q-track
  work is authoring real tile art and, optionally, Q2. One open item remains inside the
  design doc: Q-BUG-2 has no unit test coverage because `StampMultiLayerBiomeAware` is
  private with no test seam (`Phase_Q_Design.md` §12).
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
- **One knob, three layers (W-aux.b).** `shallowWaterDepth01` governs simultaneously what
  is wadeable (`Walkable`), what can be a lake (`Lakes`) and where the shallow band ends.
  Under real submarine depth this coupling is more visible, not less. Separating it is a
  Shore / Traversal contract change, deliberately out of W-aux.b scope.
- **Sea-floor vs. quantization (W-aux.b).** The relief step runs after `heightQuantSteps`,
  so the sea floor is never terraced. If terraced seabeds are ever wanted, the step needs
  its own quantization rather than being moved earlier in the pipeline — moving it would
  expose it to land-tuned redistribution and spline reshaping.
- **Hash consolidation.** The FNV-1a scheme is duplicated between the golden test files
  and `PCGMapTilemapVisualization`. The partial defining `MaskGrid2D.SnapshotHash64` sits
  at `Runtime/PCG/Grids/MaskGrid2D.Hash.cs` and has not been read. Still open.
- **`Stage_BaseTerrain2D` ↔ `BaseTerrainStage_Configurable` parity is unasserted.** Two
  implementations of the same mathematics; no test compares them. See the W-aux.b
  structural block above and `coverage-matrix.md`.
- **`HillsL2` window is an open calibration decision, and it is now reachable.**
  `hillsL2_fraction = 0.65` yields 30.07 / 33.67 / 25.42 % of `Land` at seeds 56 / 8 / 243
  against a declared 15–30 % window. Before W-aux.f no threshold could express a fraction
  below the saturation atom, so the window was unsatisfiable; with the atom gone it is a
  normal calibration question again. Two levers exist that did not before —
  `heightRedistributionExponent`, and quantile mapping in Hills (previously rejected because
  it could not split the 1.0 tie). Not scheduled here; see `PCG_Roadmap.md`.
- **Biome coverage is thin at the reference preset.** `biomesNonZero` is 10 / 8 / 8 of 13 at
  seeds 56 / 8 / 243, with SubtropicalDesert, TropicalSeasonalForest and TropicalRainforest at
  zero in all three, and Grassland and TemperateRainforest in the single or double digits of
  cells. Surfaced by W-aux.d verification; belongs to `Stage_Biome2D` and its
  temperature/moisture bands, not to vegetation. Not scheduled.
- **Two documentation items remain blocked and are not resolvable by a documentation pass.**
  `Phase_Q_Design.md` §3.1 (starter asset naming and path) is blocked by the open decision
  recorded in `Phase_Q_Pending_Doc_Updates.md` §9.1; the correction is marked in place inside
  the design document so it lands in one pass. The W.a world-scale golden
  (`Phase_W_Pending_Doc_Updates.md` §3) is blocked on missing data — the hash values exist in
  no governed file, test constant or captured log, and re-running the capture is code work,
  not documentation work. A reserved slot is recorded in `changelog-ssot.md`.
- **Domain offset / continuous zoom (open design question, W-aux §10).**
  `Stage_BaseTerrain2D` L128 hardcodes `center = new float2(w * 0.5f, h * 0.5f)`; there is
  no offset tunable, so the island cannot be shifted within the domain, and there is no
  way to sample a *window* of the same continuous field at a different offset or scale.
  This matters because Phase W's zoom mechanism is different in kind: F2c hands the local
  map a binary `MapShapeInput` mask derived from the world's 3×3 neighbourhood, rather
  than re-sampling the noise field with an offset. A domain-offset tunable would serve
  both island recentring and continuous zoom, and would sit alongside — or compete with —
  W's mask mechanism. Not a task; recorded so the decision is made deliberately rather
  than discovered mid-implementation.

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

**Q-fix.a rule.** Any `MapLayerId` present in `s_colliderLayers` must not receive biome
art through the biome-aware stamping path. The collider group is stamped separately via
`TilemapAdapter2D.ApplyLayered` and must stay that way. `HillsL2` and `Lakes` are both in
`s_colliderLayers` and both are plausible biome-override targets, so this is a live hazard
rather than a theoretical one.

## Immediate next focus
**Phase W is the active milestone.** W.a (world-scale run), W-aux.a (measurement),
W-aux.b (submarine relief), W-aux.c (instrumentation, preset wizard, per-biome vegetation),
W-aux.d (vegetation quantile mapping), W-aux.e (threshold audit) and W-aux.f (height ceiling
de-saturation) are closed. The authoring track opened with X1.a and is parallel by
construction — it neither blocks nor is blocked by W.b. Phase Q (adapter-side
biome-conditional tile selection) is implemented and smoke-validated as of 2026-08-09,
including Q-fix.a and Q-aux.a; remaining Q-track work is authoring real tile art and,
optionally, Q2.

**Next batch:** **Hills window recalibration** against the healthy `Height` field opened by
W-aux.f. The declared 15–30 % `HillsL2` window was unsatisfiable while the saturation atom
existed and is a normal calibration question again; the mechanism (recalibrated absolute
threshold vs. quantile cut) is undecided.

**Then:** **W.b — parameter surface consolidation.** Scope is defined in `PCG_Roadmap.md`
§Phase W and is not restated here. In one line: it promotes tunables that exist in code but
cannot be reached from a preset. It is **not** a step of the world→local zoom sequence —
that arc continues at W.c.

The previously documented Phase P → Phase W sequencing is paused, not cancelled: no rubric
yet exists for what distinguishes a good world from a bad one, and that rubric is to be
distilled from observing W-generated worlds. Adapter-track phases T1 and Q2 remain
independent and can be picked up at any time. X1.b remains blocked on measured runs beyond
vegetation and height.

Deferred / optional: H8b, T1, J, K, P, Q2, X1.b.

Minimum path to W: M → W. Enriched path: M → M2 → L → V → P → W.
Adapter-side enrichment (independent of W path): Q (biome-conditional tiles — DONE), Q2 (composite-condition tiles).

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
