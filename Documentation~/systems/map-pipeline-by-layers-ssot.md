# Islands.PCG — Map Pipeline by Layers SSoT

Status: Active (implemented slice only)
Authority: Primary subsystem authority for implemented Map Pipeline by Layers behavior.
Scope: Implemented F0–Phase L + L→M runtime truth and active contracts for Map Pipeline by Layers.
Out of scope: Phase P+, **Phase W world-scale generation**, legacy tilemap map generation,
sample-only inspector convenience.

**Scope clarification (2026-08-20).** "Phase W" above means the *world-scale generation
mechanism* — world grid, `WorldTileContext`, world→local zoom — not every batch carrying a
`W` label. The W-aux calibration sub-batches edit implemented F-slice stages
(`Stage_Vegetation2D`, `Stage_BaseTerrain2D`); when they change a contract, that contract is
F-slice runtime truth and is governed here regardless of which batch changed it. Recorded
because the ambiguity was raised as an open governance question at W-aux.c block 3 and is
resolved as authority wording, not as a scope extension.

## Purpose
This document governs the implemented and test-gated truth of the Map Pipeline by Layers subsystem.

## Boundary
This SSoT covers only the currently implemented vertical slice:
- F0 Context + contracts
- F1 Map lantern skeleton
- F2 Base terrain (F2b reformed: ellipse + domain-warp silhouette; F2c: optional external shape input)
- F3 Hills + topology
- F4 Shore + ShallowWater
- F6 Traversal (Walkable + Stairs)
- Phase G Morphology (LandCore + CoastDist)
- Phase H Visualization (PCGViewMode enum; scalar field color-ramp view; per-layer preset colors)
- Phase H1 Composite Visualization (PCGMapCompositeVisualization; CPU Texture2D composite; CompositeLayerSlot)
- Phase H2 Data Export (MapDataExport; MapExporter2D)
- Phase H2b Tilemap Adapter (TilemapAdapter2D; TilemapLayerEntry; PCGMapTilemapSample;
  Islands.PCG.Adapters.Tilemap separate asmdef)
- Phase H2c–H7 Tilemap Visualization, Procedural Tiles, MapGenerationPreset, TilesetConfig,
  Multi-layer Tilemaps, Rule Tiles, Map Navigation Sample
- Phase J2 Height Redistribution
- Phase N2 Spline Remapping + Post-N2 Fixes
- Phase N4 Noise Settings Infrastructure + F2 Noise Upgrade
- Phase F3b Height-Coherent Hills
- Phase N5.a Base Shape Selector
- Phase N5.b Noise Settings Assets (NoiseSettingsAsset; TerrainNoiseSettings extensions;
  PropertyDrawer; configuration override pattern)
- Phase M Climate & Biome Classification (Stage_Biome2D; BiomeType; BiomeTable;
  Temperature, Moisture, Biome fields; 4×4 Whittaker lookup)
- F5 Vegetation
- Phase M2.b Contiguous Region Detection + Naming (Stage_Regions2D; RegionNameRegistry2D;
  RegionNameTableAsset; BiomeRegionId field)
- **Phase L Hydrology** (HeightFieldHydrologyOps2D; Stage_Hydrology2D; Rivers mask;
  Lakes mask; FlowAccumulation field)
- **L→M integration** (Stage_Biome2D river moisture enrichment via FlowAccumulation)

`MapLayerId.Paths` is registered but not yet written; its authoritative write belongs in Phase O.

## Subsystem intent
Map Pipeline by Layers is a general deterministic map-generation pipeline built on mask/field layers inside a `MapContext2D`, executed through deterministic `IMapStage2D` stages, with rendering/spawning deferred to adapters.

## Active contracts
### Registries
Current `MapLayerId` (COUNT = 15):
- Land = 0
- DeepWater = 1
- ShallowWater = 2
- HillsL1 = 3
- HillsL2 = 4
- Paths = 5 *(registered; not yet written — write deferred to Phase O)*
- Stairs = 6
- Vegetation = 7
- Walkable = 8
- LandEdge = 9
- LandInterior = 10
- LandCore = 11 *(Phase G)*
- MidWater = 12 *(Phase F4c)*
- Rivers = 13 *(Phase L — append-only)*
- Lakes = 14 *(Phase L — append-only)*

Current `MapFieldId` (COUNT = 7):
- Height = 0
- Moisture = 1 *(Phase M — written by Stage_Biome2D sub-stage M.2)*
- CoastDist = 2 *(Phase G)*
- Temperature = 3 *(Phase M — written by Stage_Biome2D sub-stage M.1)*
- Biome = 4 *(Phase M — written by Stage_Biome2D sub-stage M.3)*
- BiomeRegionId = 5 *(Phase M2.b — written by Stage_Regions2D; 0 = water/Unclassified sentinel; 1-based ints for land regions)*
- FlowAccumulation = 6 *(Phase L — raw upstream cell count; 0f for non-Land, ≥1f for Land)*

### Inputs
`MapInputs`
- Seed is sanitized to >= 1
- Domain is explicit
- Tunables are deterministic
- `ShapeInput` (optional, default = `MapShapeInput.None`): when `HasShape = false` (default),
  `Stage_BaseTerrain2D` uses the F2b internal ellipse+warp silhouette; existing goldens unaffected.

`MapShapeInput` *(F2c)*
- `HasShape`: false = use internal ellipse+warp path; true = use external mask.
- `Mask` (MaskGrid2D): ON cells are land-eligible; OFF cells forced to water. Valid only when `HasShape = true`.
- Caller owns and disposes the mask; `MapInputs` holds by value.
- Dimensions must match the pipeline domain; `Stage_BaseTerrain2D` throws `ArgumentException` on mismatch.

### Tunables
`MapTunables2D`
- `shapeMode` — `IslandShapeMode` enum: Ellipse (default), Rectangle, NoShape, Custom.
- `islandRadius01`
- `waterThreshold01`
- `islandSmoothFrom01`
- `islandSmoothTo01`
- `islandAspectRatio`
- `warpAmplitude01`
- `terrainNoise` — `TerrainNoiseSettings` struct for height perturbation noise. *(N4)*
- `warpNoise` — `TerrainNoiseSettings` struct for domain warp noise. *(N4)*
- `heightQuantSteps` — height quantization steps (0 = none, 1024 = smooth). *(N4)*
- `hillsL1` — target fraction of **Land area** classified as hills-or-peaks; [0..1],
  default 0.55. Resolved per run to a Height threshold. *(F3b′; replaces the N5.e
  `hillsThresholdL1` range fraction)*
- `hillsL2` — target fraction of **Land area** classified as peaks; [0..1], default 0.20.
  Clamped to `hillsL1`. *(F3b′)*
- `hillsNoiseBlend` — per-cell noise modulation of the resolved hill thresholds;
  [0..1], default 0.0. *(N5.d)*
- `heightRedistributionExponent` — power-curve exponent; 1.0 = identity. *(J2)*
- `heightRemapSpline` — piecewise-linear height remap; default = identity. *(N2)*
- stage-specific tunables stay on the stage unless they clearly become map-wide contracts

### Configuration Assets (N5.b)

`NoiseSettingsAsset` (`Runtime/Layout/Maps/NoiseSettingsAsset.cs`)
- ScriptableObject wrapping a single `TerrainNoiseSettings` struct.
- Override-at-resolve pattern: when assigned to a visualization component noise slot,
  the asset's settings replace inline values. When null, inline values are used unchanged.

`TilesetConfig` — multi-layer stamping maintenance rule:
- `ToLayerEntries()` builds a keyed lookup by `MapLayerId`. Robust against assets with
  fewer entries than `MapLayerId.COUNT` — missing layers produce null tile entries
  (silently skipped at stamp time). A warning is logged when `layers.Length != COUNT`.
- Use the "Migrate to Phase L (add Rivers + Lakes)" context menu on existing assets to
  extend 13-entry arrays to 15 without losing tile assignments.
- `s_defaultPriorityOrder` in `TilesetConfig.cs` is the canonical stamp priority for
  new instances. **Must be updated when new `MapLayerId` entries are added.**

### Run context
`MapContext2D`
- owns layer/field memory
- stable index-based registries
- single run RNG
- deterministic allocation/clear rules
- throws on missing layer/field access (`GetLayer`, `GetField`)

### Runner
`MapPipelineRunner2D`
- stable array-order stage execution
- resets run state through `BeginRun`

### F2 base terrain contracts
`Stage_BaseTerrain2D` (F2b shape pipeline)
- reads tunables: `islandRadius01`, `waterThreshold01`, `islandSmoothFrom01/To01`,
  `islandAspectRatio`, `warpAmplitude01`, `terrainNoise.amplitude`
- writes `Height` (ScalarField2D), `Land` (MaskGrid2D), `DeepWater` (MaskGrid2D)
- `DeepWater` = border-connected NOT Land (deterministic flood fill). It is a
  **connectivity** classification, not a depth band: `ShallowWater` and `MidWater` (F4) are
  refinements layered on top of it and overlap it by design.
- `DeepWater ∩ Land == ∅`
- **(W-aux.f)** the height perturbation is normalized by `(1 + terrainAmp/2)`, the
  theoretical maximum of `mask01·(1 + (n − 0.5)·terrainAmp)`. `Height == 1.0` is therefore
  reachable only at `n == 1` (measure ≈ 0 for normalized fBm), never by clipping.
  `terrainAmp == 0` gives a normalization factor of exactly `1f`, so unperturbed presets are
  bit-identical. `math.saturate` is retained as a guard and is a no-op except for
  floating-point rounding.
- **(W-aux.f)** `waterThreshold01` is calibrated against the normalized field. When
  `terrainNoise.amplitude` or `heightRedistributionExponent` change, the threshold that
  preserves a given coastline moves by `(1 + amplitude/2)^(−exponent)`. This is *not* applied
  automatically: a silently self-adjusting threshold would hide recalibration from the
  goldens. The full list of places that threshold is declared is in `SSoT_CONTRACTS.md`.
- **(W-aux.f) formula-copy coupling.** This composition exists in three places:
  `Stage_BaseTerrain2D` (governed), `BaseTerrainStage_Configurable` (governed mirror, used by
  the Tilemap adapter) and `PCGMapTilemapVisualization.LogHeightHistogram` (ungoverned
  temporary probe, retained by decision). A change here must update all three; only the first
  two are covered by any gate, and no test asserts mirror parity.
- the `NoShape` path (`h01 = n`) does not reach the normalized expression and is unaffected.

`Stage_BaseTerrain2D` (F2c shape-input path — opt-in via `MapInputs.ShapeInput.HasShape = true`)
- `Land ⊆ shape mask`: no Land cell exists outside the provided shape.

`Stage_BaseTerrain2D` (N5.a shape mode selection)
- **Ellipse** (default): F2b path unchanged. Bit-identical to pre-N5.a output.
- **Rectangle**: Chebyshev-normalized edge distance to axis-aligned rectangle.
- **NoShape**: `h01 = n`. Water threshold alone carves coastlines.
- **Custom**: Falls back to Ellipse when no external shape is provided.

### F3 / F3b / F3b′ — Hills + Topology (Stage_Hills2D)

**Writes:** `HillsL1`, `HillsL2`, `LandEdge`, `LandInterior`

**Subset invariants:**
- `HillsL2 ⊆ Land`, `HillsL1 ⊆ Land`, `HillsL1 ∩ HillsL2 == ∅`
- `LandEdge ∪ LandInterior == Land`, `LandEdge ∩ LandInterior == ∅`

#### Threshold resolution contract (F3b′, W-aux.g)

`hillsL1` / `hillsL2` are **area fractions of `Land`**, not height values and not fractions of
a height range. They are resolved to Height-space thresholds **per run**, inside the stage, by
`HillsThresholdOps2D.ComputeAreaThresholds`:

- **Population.** `Land` cells only; water is excluded from the order statistic.
- **Selection.** Land heights sorted ascending; `k = round(frac · n)` selects the k-th value
  from the top as the threshold. Exact order statistic — no histogram binning error.
- **Tie rule.** The threshold *is* the value of the k-th cell and classification is `Height >=
  threshold`, so the whole tie class at the cut enters the band. The realized fraction is
  therefore **≥ target**, exceeded by at most the size of that tie class (tie classes exist
  because `Height` is quantized upstream; `pow` and the spline are monotone and preserve them).
  **No spatial tie-break exists and none may be introduced** — that bias is what W-aux.e
  rejected when the quantile was first considered.
- **Ordering.** `hillsL2` is clamped to `hillsL1` at construction (peaks ⊆ hills budget), which
  gives `thL2 >= thL1` by construction.
- **Empty band.** Fraction 0, or an empty `Land` population, yields `+inf`. `Height >= +inf`
  is false for every cell, and `+inf` survives the N5.d blend offset arithmetic unchanged.
- **Determinism.** Same `Height` + same `Land` + same fractions ⇒ same sorted array ⇒ same
  thresholds. The operator consumes no RNG and calls no Unity API.

**Single-implementation rule.** The resolved thresholds are per-run data, not tunables, so they
cannot be read off `MapTunables2D` by anything downstream. Every consumer that needs them —
including the adapter-side `LogHeightHistogram` probe — calls the same operator. A second,
mirrored implementation would drift silently, because diagnostics surfaces are not covered by
golden hashes.

**Order of operations.** Threshold resolution happens first, once per run; the N5.d
`hillsNoiseBlend` offset is applied per cell *after*, shifting both thresholds by the same
amount and preserving the L1–L2 gap. Consequently the exported `HillsL2` layer is not the raw
threshold band when `hillsNoiseBlend > 0`.

**Declared target population.** Any declared hills window is measured on the **raw threshold
band (pre-blend)**. That is what the mechanism controls; the exported layer additionally
depends on the noise settings. `hillsNoiseBlend` is an expressive boundary modifier, not a
calibration lever.

**Decoupling note.** F3b′ removes the last dependency of Hills on `waterThreshold01` (the N5.e
remap used it as the base of the range). Hills is no longer a `waterThreshold01` calibration
consumer; see `SSoT_CONTRACTS.md` §Calibration entry points.

### F4 shore contracts
`Stage_Shore2D`
- writes `ShallowWater`, `MidWater` (F4c, only when `MidWaterDepth01 > 0`)
- `ShallowWater ⊆ NOT Land`, `MidWater ⊆ NOT Land`, `MidWater ∩ ShallowWater == ∅`

### F5 vegetation contracts (M2.a; M2a-3 reformulated W-aux.c block 3; M2a-9 reformulated W-aux.d)
`Stage_Vegetation2D`
- writes `Vegetation`
- `Vegetation ⊆ Land`, `Vegetation ⊆ LandInterior`
- M2a-3 (reformulated): `Vegetation ∩ HillsL2` is restricted to cells whose biome
  declares `BiomeDef.vegetatesOnPeaks == true`. The legacy fallback path (Biome field
  absent) preserves the original global exclusion `Vegetation ∩ HillsL2 == ∅`.
  Per-biome policy values live in `BiomeTable.Definitions`; they are calibration,
  not contract — the contract is that the flag is consulted per cell.
- M2a-9 (reformulated, W-aux.d — global quantile cut). Per-cell acceptance on the
  biome path is a quantile of the vegetation noise field, not an absolute threshold.
  Let `E` be the eligible population (`LandInterior` ∧ valid biome sentinel ∧
  `vegetationDensity > 0` ∧ not blocked by the peak policy) and let
  `bucket(c) = clamp(floor(noise01(c) * QuantSteps), 0, QuantSteps-1)` with
  `QuantSteps = 1024`. For density `d`, `cut(d)` is the largest bucket `k` such that
  `|{ c ∈ E : bucket(c) >= k }| >= ceil(d * |E|)`.
  - (a) **Exactness.** `c ∈ E` is vegetated ⟺ `bucket(c) >= cut(density(c))`.
  - (b) **Nesting.** `d1 > d2 ⟹ cut(d1) <= cut(d2)`, so accepted sets are nested over
    `E`. This is a deterministic set-inclusion property, replacing the former
    statistical "denser biomes cover more" formulation, which held by accident while
    the mapping was broken.
  - (c) **Floor.** Realized coverage over `E` is `>= d`, never below. Whole buckets are
    accepted, so the excess is bounded by the **population share of the cut bucket**,
    not by `1/QuantSteps`. Measured at seeds 56/8/243, res 256, 2026-08-19: realized
    coverage per nominal density is 5.00–5.04, 15.05–15.17, 25.24–25.30, 60.16–60.38,
    65.08–65.29, 85.16–85.22. Max excess +0.38 pp.
  - (d) **NOT guaranteed: per-biome coverage equal to `d`.** Biomes are cut against the
    global distribution, so biome placement that correlates with the noise field moves
    per-biome coverage away from nominal in either direction. Demonstrated: Tundra and
    TemperateDesert share `d = 0.05`, and their realized coverage differs by 3–5× with
    the ordering flipping by seed — seed 56 gives Desert 11.22% vs Tundra 2.18%, seed
    243 gives Desert 3.16% vs Tundra 10.48%, on populations of thousands of cells.
    Do not read a per-biome percentage as a calibration error without checking the
    biome's population size first: biomes with a handful of cells (Grassland: 12, 15
    and 0 cells at the three seeds tested) report 0.00% for ordinary small-sample
    reasons, not because of this clause.
  - (e) **COUPLING — contract surface, not an implementation detail.** `cut(d)` is a
    function of `E`. Any change to the eligibility policy — including flipping
    `vegetatesOnPeaks` on a single biome, or changing one biome's density to or from
    zero — shifts the threshold of **every** biome. A single-biome edit is expected to
    produce a whole-map diff. This is by design.
  - Legacy path (Biome field absent): absolute threshold `0.40`, no quantile, no
    coupling. Preserved as the fallback witness; it is a separate early-out in the
    stage, not a branch inside the main loop, so the two semantics cannot drift.
- the measured distribution that motivates the above: over `E` the field occupies
  `[0.2861, 0.7568]` / `[0.2832, 0.7676]` / `[0.2939, 0.7451]` at seeds 56 / 8 / 243
  (res 256), means 0.5056 / 0.5250 / 0.5159, with ~71% of mass inside a 0.20-wide
  window. Any absolute threshold above ~0.77 accepts zero cells at every seed tested,
  regardless of biome or terrain. The shape is stable across seeds; this was not a
  seed-56 peculiarity.
- aggregate stability after the change (res 256): `Vegetation` is 6.30% / 6.22% / 6.88%
  of the map at seeds 56 / 8 / 243. The quantile fixes the aggregate by construction;
  a large swing here would indicate the histogram is being built over the wrong
  population.
- the noise field itself is unchanged by W-aux.d: same salt `0xB7C2F1A4`, frequency 4,
  3 octaves, lacunarity 2, persistence 0.5, `quantSteps` 1024. Sample consumption is
  one `FillSimplexPerlin01` call per `Execute`, as before.
- cost profile: two passes over the domain (histogram, then apply) plus
  `O(BiomeType.COUNT * QuantSteps)` for the cuts. Previously one pass.
- **reporting note.** `MapStatsExporter2D.vegetationByBiome.pctOfBiome` divides by all
  cells of the biome, including cells blocked by the peak policy. It is therefore not
  directly comparable to `d`, nor to a per-biome figure computed over the eligible
  population. Compare like with like.
- ordering requirement: must run **after** `Stage_Biome2D` (biome field available)

### F6 traversal contracts
`Stage_Traversal2D`
- writes `Walkable`, `Stairs`
- `Walkable = Land AND NOT HillsL2`
- `Stairs ⊆ HillsL1`, `Stairs ∩ HillsL2 == ∅`, `Stairs ⊆ Walkable`

### Phase G morphology contracts
`Stage_Morphology2D`
- writes `LandCore` (MapLayerId 11), `CoastDist` (MapFieldId 2)
- `LandCore ⊆ Land`, `LandCore ⊆ LandInterior`
- `CoastDist == 0f` at `LandEdge`; `CoastDist > 0f` at reachable LandInterior; `-1f` elsewhere

### Phase H2 adapter contracts
`MapDataExport`
- Managed snapshot of a completed `MapContext2D`. Lifetime independent of the source context.
- `HasLayer(id)` / `HasField(id)`: true iff the layer/field was created in the source context.
- `GetLayer(id)` → `bool[]`: throws `InvalidOperationException` if absent.
- Instantiated only via `MapExporter2D.Export`; constructor is `internal`.

`MapExporter2D`
- Static adapter; read-only. Does not write to or modify the context.
- Exports all layers/fields present in the context; absent ones produce null slots.
- **Extensible:** new `MapLayerId`/`MapFieldId` entries are automatically exported
  without contract changes — no update to `MapExporter2D` needed when adding layers.
- Throws `ArgumentNullException` if context is null.

### Phase H2b tilemap adapter contracts
`TilemapLayerEntry`
- `[Serializable]` struct. Maps one `MapLayerId` to one `TileBase` asset.
- Entries with a null `Tile` field are silently skipped during Apply.

`TilemapAdapter2D`
- Static adapter; read-only consumer of `MapDataExport`. Never writes to pipeline state.
- Priority: entries evaluated low→high (array order); last matching layer per cell wins.
- Absent layers (not in export): silently skipped; no exception.

### Phase H5 — Multi-layer stamping contracts

`PCGMapTilemapVisualization.StampMultiLayer()` routes `activeTable` entries through
two static filter arrays. A layer not present in either array is **silently never stamped**
when `enableMultiLayer = true`, regardless of its presence in the procedural color table
or TilesetConfig. This is the authoritative specification of which layers go where.

```
s_baseLayers   (base tilemap — water + land surfaces, low→high priority):
  DeepWater, Lakes, MidWater, ShallowWater, Land, LandCore, LandEdge

s_overlayLayers (overlay tilemap — gameplay features, low→high priority):
  Vegetation, HillsL1, HillsL2, Stairs, Rivers

s_colliderLayers (collider tilemap):
  DeepWater, MidWater, HillsL2
```

**Maintenance rule (L-fix.a, 2026-04-15):** Every future phase that adds a new
`MapLayerId` **must** add it to `s_baseLayers` or `s_overlayLayers` in
`PCGMapTilemapVisualization.cs`, or that layer will be silently invisible in
multi-layer mode. `MapExporter2D` exports all layers automatically — the gap is
exclusively in this filtering step. Assignment convention:
- **`s_baseLayers`** — water bodies and land surface types (e.g., Lakes, future
  lava/ice fields, specialized terrain surfaces).
- **`s_overlayLayers`** — discrete gameplay features painted on top of terrain
  (e.g., Rivers, future roads, faction markers).

### Phase M — Climate & Biome Classification
`Stage_Biome2D` — single `IMapStage2D` with three sub-stages.

**Pipeline position:** After Stage_Morphology2D (G) and Stage_Hydrology2D (L, when present).

**Reads (read-only):**
- `Height` (MapFieldId 0), `CoastDist` (MapFieldId 2), `LandEdge` (MapLayerId 9), `Land` (MapLayerId 0)
- `FlowAccumulation` (MapFieldId 6) — **optional, Phase L**. Detected via `ctx.IsFieldCreated`.
  When absent, `riverFactor = 0` and output is bit-identical to pre-L baseline.

**Writes:** `Temperature` (3), `Moisture` (1), `Biome` (4)

**Sub-stage M.2 — Moisture (with Phase L integration):**
```
coastFactor  = coastalMoistureBonus / (1 + max(coastDist, 0) * coastDecayRate)
riverFactor  = 0                                          // when FlowAccumulation absent
             = riverMoistureBonus * clamp(fa / norm, 0, 1)  // when FlowAccumulation present
moisture     = clamp(noiseAmplitude * noise + coastFactor + riverFactor, 0, 1)
```
`norm = riverFlowNorm` when > 0; otherwise `max(1, landCount * 0.02)` (auto, matching
Phase L's default `riverThresholdFraction = 0.02`).

**Stage-local tunables (12, Inspector-accessible):**
baseTemperature, lapseRate, latitudeEffect, coastModerationStrength, tempNoiseAmplitude,
tempNoiseCellSize, coastalMoistureBonus, coastDecayRate, moistureNoiseAmplitude,
moistureNoiseCellSize, **riverMoistureBonus** (0.4f), **riverFlowNorm** (0f = auto).

**Invariants:**
- M-1..M-8 (unchanged from pre-L).
- M-8 (extended): When FlowAccumulation absent, moisture is bit-identical to the
  pre-Phase-L baseline. Existing M / M2a / M2b goldens remain valid.

### Phase L — Hydrology
`HeightFieldHydrologyOps2D` — static operator class, 4 methods.

**Algorithm pipeline (all deterministic, zero RNG):**

1. `FillDepressions(height, land, filledHeight, w, h, epsilon=1e-5f)` — Priority-Flood+ε.
   Seeds a min-heap with all coastal Land cells (8-adjacent to non-Land or OOB).
   Floods inward; raises depressions by ε increments. Uses `SortedSet<(float,int)>` keyed
   on (filledHeight, rowMajorIndex) for deterministic tie-breaking.

2. `ComputeFlowDirectionsD8(filledHeight, land, flowDir, w, h)` — D8 steepest-descent.
   Each Land cell points to its steepest 8-neighbor. Non-Land neighbors treated as h=0
   (sea level), so coastal cells drain directly to water. Tie-break: first in fixed
   clockwise order (N, NE, E, SE, S, SW, W, NW).

3. `AccumulateFlow(filledHeight, land, flowDir, flowAccum, w, h)` — upstream count propagation.
   Land cells sorted descending by filledHeight (row-major tiebreak). Each cell passes its
   accumulated count to its D8 downstream neighbor. Initializes each Land cell to 1f.
   Non-Land cells initialized to 0f.

4. `ExtractRivers(flowAccum, land, threshold, rivers, w, h)` — binary threshold.
   `rivers[x,y] = Land[x,y] AND flowAccum[x,y] >= threshold`.

`Stage_Hydrology2D` — orchestrates L.1 + L.2.

**Pipeline position:** After Stage_Morphology2D (G), before Stage_Biome2D (M).
Full order: `BaseTerrain → Hills → Shore → [Veg] → Traversal → Morphology → Hydrology → Biome → Veg(M2a) → Regions(M2b)`

**Reads (read-only):**
- `Height` (MapFieldId 0), `Land` (MapLayerId 0), `DeepWater` (MapLayerId 1),
  `ShallowWater` (MapLayerId 2)

**Writes (authoritative):**
- `FlowAccumulation` (MapFieldId 6) — raw upstream cell count; 0f for non-Land, ≥1f for Land
- `Rivers` (MapLayerId 13) — `Rivers ⊆ Land`
- `Lakes` (MapLayerId 14) — `NOT Land AND NOT DeepWater AND NOT ShallowWater`

**Stage-local tunables:**
- `epsilon = 1e-5f` — Priority-Flood gradient increment
- `riverThresholdFraction = 0.02f` — fraction of Land cells; auto-scales with resolution
- `minLakeArea = 0` — minimum lake component size; 0 = no filtering

**Invariants (L-1 through L-10, all test-gated):**
- L-1: Determinism — same seed + tunables → identical FlowAccumulation, Rivers, Lakes
- L-2: `Rivers ⊆ Land`
- L-3: `Lakes ⊆ NOT Land`
- L-4: `Lakes ∩ DeepWater == ∅`
- L-5: `Lakes ∩ ShallowWater == ∅`
- L-6: `Rivers ∩ Lakes == ∅` (by construction: Rivers on Land, Lakes off Land)
- L-7: FlowAccumulation = 0f for non-Land; ≥1f for all Land cells
- L-8: max(FlowAccumulation) ≤ totalLandCells
- L-9: `Rivers[x,y] == true iff Land[x,y] AND flowAccum[x,y] >= threshold`
- L-10: No-mutate — Height, Land, DeepWater, ShallowWater, CoastDist unchanged

**RNG / Noise:** Zero ctx.Rng consumption. All algorithms deterministic by construction.

**Temp buffers:** `filledHeight` (float[]) and `flowDir` (int[]) are stage-local managed
arrays freed at stage exit (~512 KB combined on 256×256).

**FlowAccumulation → Phase M contract:**
Phase M reads FlowAccumulation via `ctx.IsFieldCreated(MapFieldId.FlowAccumulation)`.
Values are raw counts (not normalized). Phase M normalizes via its own `riverFlowNorm`
tunable. No additional interface required — the scalar field IS the interface.

**Resolution-relative thresholding:** `riverThresholdFraction` operates on fraction of
total Land cells (not absolute count). This auto-scales: at 64×64, threshold ≈ 40 cells
(2% of ~2000); at 256×256, threshold ≈ 800 cells (2% of ~40000). The 2% default is an
educated estimate — validate via visual smoke test at each resolution.

**Known limitation:** D8 on a regular grid can produce visible horizontal/vertical
artifacts in river paths. Rho8 (stochastic D8) would reduce this but requires RNG,
violating the no-RNG invariant. Deferred to Phase L2 if visually problematic.

**Visualization (Inspector):**
- `enableHydrologyStage` toggle (default: false — no golden break)
- `hydroEpsilon`, `hydroRiverThresholdFraction`, `hydroMinLakeArea` tunables
- Stage arrays: `stagesL`, `stagesLM`, `stagesLM2a`, `stagesLM2b`
- `ScalarOverlaySource.FlowAccumulation = 6` (scalar overlay, recommended max=500 at 64×64)
- Rivers in `s_overlayLayers`; Lakes in `s_baseLayers` (see Phase H5 contract above)

**Golden coverage:**
- `HeightFieldHydrologyOps2DTests.cs` — operator micro-tests (14 tests)
- `StageHydrology2DTests.cs` — stage invariants L-1..L-10 + goldens captured
- `MapPipelineRunner2DGoldenLTests.cs` — F0→G→L pipeline golden captured
- `MapPipelineRunner2DGoldenLMTests.cs` — F0→G→L→M pipeline golden captured

### Phase M2.b — Contiguous Region Detection + Naming
`Stage_Regions2D`
- writes `BiomeRegionId` (MapFieldId 5) — 0 = sentinel, 1-based ints for land regions
- Contracts R-1 through R-8 (see SSoT_CONTRACTS.md)
- R-7: BiomeRegionId values are intra-map stable only — must not be persisted or compared across seeds.

## Implemented surface
### F0
- `MapIds2D`, `MapInputs`, `MapTunables2D`, `MapContext2D`, `IMapStage2D`, `MapPipelineRunner2D`

### F2
- `Stage_BaseTerrain2D` (F2b, F2c, N5.a), `MapShapeInput`, `MaskFloodFillOps2D`
- Outputs: `Height`, `Land`, `DeepWater`

### F3
- `MaskTopologyOps2D`, `MapNoiseBridge2D`, `HillsThresholdOps2D` *(F3b′)*, `Stage_Hills2D`
- Outputs: `LandEdge`, `LandInterior`, `HillsL1`, `HillsL2`

### F4
- `Stage_Shore2D`
- Outputs: `ShallowWater`, `MidWater` (F4c)

### F5
- `Stage_Vegetation2D`
- Outputs: `Vegetation`

### F6
- `Stage_Traversal2D`
- Outputs: `Walkable`, `Stairs`

### Phase G
- `MaskMorphologyOps2D`, `Stage_Morphology2D`
- Outputs: `LandCore` (MapLayerId 11), `CoastDist` (MapFieldId 2)

### Phase L
- `HeightFieldHydrologyOps2D`, `Stage_Hydrology2D`
- Outputs: `Rivers` (MapLayerId 13), `Lakes` (MapLayerId 14), `FlowAccumulation` (MapFieldId 6)
- enableHydrologyStage toggle; stagesL / stagesLM / stagesLM2a / stagesLM2b arrays

### Phase M
- `Stage_Biome2D` (sub-stages M.1 Temperature, M.2 Moisture+L-enrichment, M.3 Biome)
- `BiomeType`, `BiomeTable`
- Outputs: `Temperature` (MapFieldId 3), `Moisture` (MapFieldId 1), `Biome` (MapFieldId 4)

### Phase M2.b
- `Stage_Regions2D`, `RegionNameRegistry2D`, `RegionNameTableAsset`
- Outputs: `BiomeRegionId` (MapFieldId 5)

## Determinism rules
- stable seed sanitation
- stable registry ordering
- no uninitialized layer/field memory
- stage execution order is array order
- row-major scans
- deterministic flood fill queue/neighbor ordering
- deterministic topology neighbor semantics (4-neighborhood)
- stable connected-components discovery order when labeling is used
- snapshot-hash gates for masks (MaskGrid2D.SnapshotHash64)
- FNV-1a float-bit hash gates for scalar fields (HashScalarField in test helpers)
- export determinism: same context state ⇒ identical MapDataExport output
- tilemap adapter determinism: same export + same priority table ⇒ identical Tilemap output
- Phase L: SortedSet<(float,int)> min-heap with (height, rowMajorIndex) composite key; descending-height sort with row-major tiebreak in AccumulateFlow

## Test-gated behavior
*(F0–M2.b coverage unchanged except the M2a golden noted below — see prior revisions)*

- F5 vegetation goldens (StageVegetation2DTests.cs, 64×64, seed 12345):
  legacy path `0x6CDCB0E869BB070F` (**re-anchored W-aux.f**, was `0xE7876A1519EC45D3`,
  which had held since M2.a — the legacy path was touched by neither W-aux.c block 3 nor
  W-aux.d, and that golden holding green through both is the evidence of it; W-aux.f moved it
  because the *value* of `Height` changed under every path);
  biome path `0x6D433B1023A09BB6` (**re-anchored W-aux.f**, was `0x6AB1192251196B17`
  (W-aux.d), before that `0x5B1DB3468075FFDC` (W-aux.c block 3), before that
  `0x41BB2F99C2BE043D`). Each re-anchor is the intended consequence of a named change, not a
  regression.
- M2a-3 is asserted in two forms by `AssertSubsetInvariants(ref ctx, globalHillsL2Exclusion)`:
  strict emptiness on the legacy path, per-biome `vegetatesOnPeaks` on the biome path.
- M2a-9 is asserted by `M2a_QuantileCut_IsExact_Nested_AndAboveNominal`, which replaces
  `M2a_CoverageMonotonicity_DenseBiomesExceedSparseBiomes`. The old test compared two
  biome pairs with a ≥8-cell tolerance and passed throughout the period the mapping was
  broken; it is removed rather than relaxed. The new gate recomputes the noise field
  from `Stage_Vegetation2D`'s public constants, rebuilds the eligible histogram and the
  cut independently of the stage, and compares **cell by cell**, then checks nesting
  (b) and the coverage floor (c). Deducing the cut from the lowest vegetated bucket
  would be unsound — a biome with no cell sitting exactly at the cut reports a cut above
  the real one — which is why the gate reimplements the rule instead of observing it.
- **W-aux.f re-anchor (2026-08-20).** 29 golden constants that depend on the *value* of
  `Height` were re-anchored across F3, F5, F6, L, L+M, M, M2, M2b and the stage-level
  fixtures; the old→new pairs are recorded in `changelog-ssot.md`. Land-topology goldens
  (`Land`, `DeepWater`, `LandCore`, `LandEdge`, `LandInterior`, `Lakes`) passed **unchanged**
  in every fixture, which is the evidence that the recalibrated `waterThreshold01` held the
  coastline in place.

- Phase L operator micro-tests (HeightFieldHydrologyOps2DTests.cs)
- Phase L stage invariants L-1..L-10 (StageHydrology2DTests.cs)
- Phase L stage goldens: FlowAccumulation, Rivers, Lakes hashes locked
- Phase L pipeline golden: F0→G→L hash locked (MapPipelineRunner2DGoldenLTests.cs)
- Phase L+M pipeline golden: F0→G→L→M hash locked (MapPipelineRunner2DGoldenLMTests.cs)
- Phase L+M invariants: LM_Determinism, LM_NoMutate_FlowAccumulation,
  LM_RiverEnrichment_MoistureHigherNearRivers, LM_RiverFlowNorm_Auto_MatchesExplicit

## Known limitations
- Scalar field normalization range (scalarMin/scalarMax) is inspector-settable but not auto-ranged
- D8 flow direction can produce axis-aligned river artifacts on flat/uniform terrain (Rho8 deferred)
- FlowAccumulation threshold default (2%) is empirically calibrated, not game-validated — requires smoke testing at 64×64, 128×128, 256×256
- `beachMinTemperature` lives on BiomeTable (static readonly 0.25f), not yet Inspector-tunable
- Vegetation density→coverage was non-linear and effectively binary below ~0.4 density
  until W-aux.d; **resolved** by the M2a-9 quantile cut above. The residue is M2a-9(d):
  per-biome coverage is still not guaranteed to equal `d`, because biomes are cut against the
  global distribution. `BiomeTable` densities have not been recalibrated since the mapping
  changed, so current values mean something different from what they meant when they were
  chosen.
- The exported `HillsL2` layer still varies across seeds even though the raw threshold band
  does not: 11.19 / 21.71 / 28.34 % of `Land` at seeds 56 / 8 / 243 against a band held at
  ~20.2 % (`Default_MapPreset` @256, `hillsNoiseBlend = 0.35`). The blend re-introduces a
  dependency on the local shape of the height histogram around the cut. Declared targets are
  measured pre-blend by contract, so this is a known expressive limitation, not a contract
  violation. Resolving it would mean resolving the quantile *after* the blend — a different
  contract, not scheduled.
- `Vegetation` is not among the ten runtime golden hashes logged by
  `PCGMapTilemapVisualization`; its only gate is the 64×64 EditMode golden
- `MapLayerId.Paths` registered but not yet written; ownership deferred to Phase O
- TilesetConfig .asset files with 13 entries require migration context menu; pending for TilesetConfig-8bit and TilesetConfig-DragonWarrior

## Not governed here
- Phase P+ roadmap work (Pipeline Validation and beyond)
- Phase W world-scale generation
- `Paths` layer write (deferred to Phase O)
- Legacy tilemap generation documents
