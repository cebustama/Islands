# Phase M2 — Biome-Aware Vegetation & Region Naming: Design Specification

Status: Planning only (not implementation authority)  
Authority: Design reference for Phase M2 implementation. Supplements the Phase M2 entry
in `PCG_Roadmap.md` with detailed stage contracts, data structures, and test plan.  
Depends on: Phase M design spec (`Phase_M_Design.md`), resolved design decisions from
2026-04-06 (roadmap), and F5 vegetation contracts from `map-pipeline-by-layers-ssot.md`.

---

## 1. Phase Intent

Phase M2 transforms Phase M's biome data from a passive classification into active
terrain variation. It has two independent sub-phases:

**M2.a** makes vegetation biome-aware — a tropical rainforest island becomes densely
forested while a subtropical desert island becomes barren, all from the same pipeline
with the same seed. This is the single largest visual payoff from Phase M's biome work.

**M2.b** identifies contiguous same-biome regions and assigns them unique IDs, producing
the raw material for named geographic features ("The Western Forest," "Coral Bay Marshes").
Dwarf Fortress does exactly this in its Stage 12 (region detection) and RimWorld uses
BiomeWorker scoring to assign named biome regions at world scale.

M2.a and M2.b are independently implementable. M2.a can ship without M2.b and vice
versa. Both require Phase M to be complete.

---

## 2. Sub-Phase M2.a — Biome-Aware Vegetation Refactor

### 2.1 Current State (F5 Contracts)

The current `Stage_Vegetation2D` (F5), as recorded in the map-pipeline-by-layers SSoT:

- **Reads:** `LandInterior` (read-only), `HillsL2` (read-only), `ShallowWater` (read-only)
- **Writes:** `Vegetation`
- **Eligibility:** `Vegetation ⊆ LandInterior`, `Vegetation ∩ HillsL2 == ∅`
- **Coverage logic:** SimplexPerlin01 via `MapNoiseBridge2D`, salt `0xB7C2F1A4u`, global
  threshold `0.40f`. A cell gets vegetation if `noise(x, y) >= 0.40`.
- **No biome awareness:** every eligible cell has equal probability of vegetation
  regardless of climate. A frozen tundra cell and a tropical rainforest cell use the
  same 40% coverage threshold.

### 2.2 Pipeline Ordering Decision (Resolved)

**Decision: Move vegetation after Biome.** The stage runs in its new position after
`Stage_Biome2D` (Phase M) instead of its current F5 position.

**Rationale — dependency analysis confirms safety:**

No stage between F5's current position and Phase M reads the `Vegetation` mask:

| Stage | Position | Reads `Vegetation`? |
|-------|----------|---------------------|
| `Stage_Traversal2D` (F6) | After F5 | No — reads `Land`, `HillsL1`, `HillsL2` only |
| `Stage_Morphology2D` (G) | After F6 | No — reads `Land` only |
| `Stage_Biome2D` (M) | After G | No — reads `Height`, `CoastDist`, `Land`, `LandEdge` |

Moving vegetation after Biome breaks no data dependencies. A single-pass approach
(one stage, one position) is cleaner than a two-pass model (noise pass at F5, biome
modulation pass after M) and avoids the complexity of coordinating two writes to the
same mask layer.

**New pipeline order:**

```
BaseTerrain(F2) -> Hills(F3) -> Shore(F4) -> Traversal(F6)
  -> Morphology(G) -> Biome(M) -> Vegetation(M2.a) [-> Regions(M2.b)]
```

Note: F6 (Traversal) now runs before Morphology instead of after Vegetation. This is
safe because F6 reads `Land`, `HillsL1`, `HillsL2` — none of which depend on Vegetation.
The `Walkable` mask does not include/exclude Vegetation cells; it excludes `HillsL2` only.

**Golden hash impact:** All existing F0–G goldens are unaffected (stages F2–G produce
identical output regardless of F5's position). The F5 pipeline golden and the full
pipeline golden will change because vegetation output changes. New M2 goldens replace them.

### 2.3 Modified Stage Contract: `Stage_Vegetation2D`

The same stage class is modified in place. No new stage class.

#### 2.3.1 Inputs (Read-Only)

| Data | Source | Usage |
|------|--------|-------|
| `LandInterior` (MapLayerId 10) | Stage_Hills2D | Eligibility: vegetation only on land interior |
| `HillsL2` (MapLayerId 8) | Stage_Hills2D | Exclusion: no vegetation on hill peaks |
| `ShallowWater` (MapLayerId 3) | Stage_Shore2D | Exclusion (contract clarity) |
| `Biome` (MapFieldId 4) | Stage_Biome2D | **New.** Per-cell biome ID → BiomeDef lookup |
| `Moisture` (MapFieldId 1) | Stage_Biome2D | **New.** Optional moisture modulation (see 2.3.3) |

#### 2.3.2 Outputs (Authoritative Write)

| Layer | Contract |
|-------|----------|
| `Vegetation` (MapLayerId 6) | Same subset invariants as current F5. Coverage changes per biome. |

#### 2.3.3 Coverage Logic

**Per-cell threshold replaces global threshold:**

```
biomeId = (int)biomeField[x, y]
biomeDef = BiomeTable.Definitions[biomeId]
threshold = 1.0f - biomeDef.vegetationDensity

noiseValue = SimplexPerlin01(x, y, seed + salt)

if (eligible && noiseValue >= threshold):
    Vegetation[x, y] = ON
```

The noise function, salt (`0xB7C2F1A4u`), and spatial pattern are unchanged from current
F5. Only the threshold varies per cell based on biome.

**Per-biome expected coverage (from Phase M `BiomeDef.vegetationDensity`):**

| Biome | vegetationDensity | Effective threshold | Expected coverage |
|-------|-------------------|---------------------|-------------------|
| Snow | 0.00 | 1.00 | 0% (no vegetation) |
| SubtropicalDesert | 0.02 | 0.98 | ~2% |
| Beach | 0.02 | 0.98 | ~2% |
| Tundra | 0.05 | 0.95 | ~5% |
| Grassland | 0.15 | 0.85 | ~15% |
| Shrubland | 0.25 | 0.75 | ~25% |
| BorealForest | 0.60 | 0.40 | ~60% (same as current F5 global) |
| TemperateForest | 0.65 | 0.35 | ~65% |
| TropicalSeasonalForest | 0.70 | 0.30 | ~70% |
| TemperateRainforest | 0.85 | 0.15 | ~85% |
| TropicalRainforest | 0.90 | 0.10 | ~90% |
| Unclassified (water) | 0.00 | — | 0% (not eligible) |

**Optional moisture modulation (design-time decision):**

A secondary modulation can vary coverage within a biome based on local moisture:

```
moistureBonus = moistureModulation * (moisture[x, y] - 0.5f)
adjustedThreshold = clamp(threshold - moistureBonus, 0, 1)
```

Where `moistureModulation` is a global tunable (default 0.0f = disabled). When non-zero,
wetter cells within a biome get slightly more vegetation and drier cells slightly less.
This adds intra-biome spatial variation without changing the per-biome averages.

**Decision status: Moisture modulation is optional. Recommended to implement the parameter
but default it to 0.0f so it has no effect until tuned. Costs nothing to include.**

#### 2.3.4 RNG / Noise Strategy

Unchanged from current F5: coordinate hashing via `MapNoiseBridge2D` with salt
`0xB7C2F1A4u`. No `ctx.Rng` consumption. Deterministic by construction.

The salt is preserved from F5 so that the spatial noise pattern is identical. The only
difference is the threshold applied to that pattern. This means: on a single-biome
island where every Land cell has the same biome (e.g., TemperateForest at density 0.65),
the vegetation pattern would differ from old F5 (threshold 0.40 vs 0.35). This is
acceptable — the old golden is invalidated and a new M2 golden replaces it.

#### 2.3.5 Stage-Local Tunables

```csharp
float moistureModulation = 0.0f;  // [0, 0.5], 0 = disabled
int   noiseCellSize      = 8;     // inherited from F5 (unchanged)
```

The per-biome `vegetationDensity` values live in `BiomeDef` (Phase M), not as
stage-local tunables. This is intentional — biome properties should be authored in
one place and consumed by multiple stages.

### 2.4 Preserved Invariants

All existing F5 invariants are preserved:

| ID | Invariant | Status |
|----|-----------|--------|
| M2a-1 | `Vegetation ⊆ Land` | Preserved |
| M2a-2 | `Vegetation ⊆ LandInterior` (LandEdge ring excluded) | Preserved |
| M2a-3 | `Vegetation ∩ HillsL2 == ∅` (no vegetation on peaks) | Preserved |
| M2a-4 | `Vegetation ∩ ShallowWater == ∅` | Preserved |
| M2a-5 | Determinism: same seed + tunables => identical Vegetation | Preserved |
| M2a-6 | No-mutate: `LandInterior`, `HillsL2`, `ShallowWater`, `Biome`, `Moisture` unchanged | Preserved (Biome, Moisture added to no-mutate list) |

New invariants:

| ID | Invariant |
|----|-----------|
| M2a-7 | **Biome-zero suppression:** cells with `Biome == 0f` (water/Unclassified) never have Vegetation ON |
| M2a-8 | **Snow suppression:** cells with `Biome == Snow` never have Vegetation ON (vegetationDensity = 0.0) |
| M2a-9 | **Coverage monotonicity:** for two biomes A and B where `A.vegetationDensity > B.vegetationDensity`, the total Vegetation cell count in biome A cells >= that in biome B cells (statistical, not per-cell — tested over large maps) |

### 2.5 Test Plan (M2.a)

#### 2.5.1 Stage-Level Tests (`StageVegetation2DTests.cs` — updated)

| Test | Asserts |
|------|---------|
| Determinism | Two runs, same config → identical Vegetation hash |
| Vegetation golden (M2) | Hash locked for default tunables + default biomes, 64×64, seed=12345 |
| Subset invariants (M2a-1 through M2a-4) | All existing subset checks pass |
| No-mutate (M2a-6) | Input field/layer hashes unchanged (including Biome and Moisture) |
| Snow suppression (M2a-8) | Zero Vegetation cells in Snow biome regions |
| Desert sparsity | Vegetation count in SubtropicalDesert cells < 5% of eligible cells |
| Forest density | Vegetation count in TropicalRainforest cells > 80% of eligible cells |
| Without Phase M | Stage throws or skips gracefully if Biome field is absent (see 2.6) |
| Moisture modulation disabled | `moistureModulation = 0` → same result as no Moisture read |
| Moisture modulation enabled | `moistureModulation = 0.3` → wetter cells have more vegetation |

#### 2.5.2 Pipeline Golden (`MapPipelineRunner2DGoldenM2Tests.cs`)

Full F0–M2 golden. Invalidates the old F5 pipeline golden (new vegetation output).
Does NOT invalidate F0–G goldens (no upstream change).

### 2.6 Phase M Absence Behavior

**If Phase M has not run (Biome field does not exist):** `Stage_Vegetation2D` must handle
this gracefully. Two options:

- **Option A (recommended):** Fall back to the original global threshold (0.40f) for all
  cells. The stage detects the absence of `MapFieldId.Biome` via a try-get pattern or a
  field-existence check, and uses the legacy threshold. This preserves backward
  compatibility for pipelines that include M2.a's vegetation stage but not Phase M.
- **Option B:** Throw. The pipeline is misconfigured if M2.a runs without Phase M.

**Decision status: Option A recommended. Allows the same stage class to serve both
Phase M and pre-Phase M pipelines without configuration changes.**

---

## 3. Sub-Phase M2.b — Contiguous Region Detection and Naming

### 3.1 Intent

Convert the per-cell biome ID field into labeled contiguous regions. Each region is a
maximal connected set of cells sharing the same biome type. The output is a region ID
field and a metadata table. This enables downstream systems (Phase N POI placement,
Phase W world-tile descriptions, adapter-side name generation) to refer to geographic
features by region rather than by individual cells.

### 3.2 New Registry Entries

#### MapFieldId additions

| ID | Name | Written By | Type | Notes |
|----|------|-----------|------|-------|
| 5 | `NamedRegionId` | `Stage_Regions2D` (M2.b) | int-as-float | New; append-only. 0 = no region (water). 1–N = region IDs. |

After Phase M2.b: `MapFieldId.COUNT` → 6 (was 5 after Phase M: Height, Moisture,
CoastDist, Temperature, Biome).

#### MapLayerId

No new entries. COUNT stays at 12.

### 3.3 Stage Contract: `Stage_Regions2D`

#### 3.3.1 Pipeline Position

After `Stage_Vegetation2D` (M2.a). M2.b depends on Phase M but not on M2.a — however,
placing it after vegetation allows region metadata to include vegetation coverage
statistics per region if desired in the future.

```
BaseTerrain(F2) -> Hills(F3) -> Shore(F4) -> Traversal(F6)
  -> Morphology(G) -> Biome(M) -> Vegetation(M2.a) -> Regions(M2.b)
```

#### 3.3.2 Inputs (Read-Only)

| Data | Source | Usage |
|------|--------|-------|
| `Biome` (MapFieldId 4) | Stage_Biome2D | Cell biome IDs — defines region membership |
| `Land` (MapLayerId 0) | Stage_BaseTerrain2D | Water cell exclusion |

#### 3.3.3 Outputs (Authoritative Write)

| Output | Type | Contract |
|--------|------|----------|
| `NamedRegionId` (MapFieldId 5) | int-as-float scalar field | 0f for water cells. 1f–Nf for region IDs. Every Land cell belongs to exactly one region. |
| `RegionTable` | `RegionMetadata[]` (stage-local output, see 3.4) | Indexed by region ID. Metadata for each detected region. |

#### 3.3.4 Algorithm

**Connected-component analysis via flood fill on the Biome field.**

The algorithm processes Land cells only. Two cells are in the same region if and only if
they are 4-connected (cardinal neighbors) and have the same `Biome` value.

```
regionCounter = 0
for y in 0..height:
  for x in 0..width:
    if Land[x,y] AND regionField[x,y] == 0:
      regionCounter++
      biomeId = (int)biomeField[x,y]
      FloodFill4(x, y, biomeId, regionCounter, ...)
      RecordRegionMetadata(regionCounter, biomeId, ...)

// Optional: merge tiny regions (see 3.5)
```

**Connectivity type: 4-connected (cardinal only).** This is consistent with the rest of
the pipeline (`MaskTopologyOps2D` uses 4-neighborhood, `Stage_Shore2D` uses 4-adjacent).

**Scan order: row-major (y → x).** Region IDs are assigned in discovery order. This
produces deterministic IDs — the northwesternmost region always gets the lowest ID.
Matches the `MaskTopologyOps2D` convention: "stable row-major discovery order."

**Water cells:** Always receive `NamedRegionId = 0f`. Not flood-filled.

#### 3.3.5 RNG / Noise Strategy

None. `Stage_Regions2D` is fully deterministic without noise or randomness. No
`ctx.Rng` consumption. No `MapNoiseBridge2D` usage.

### 3.4 `RegionMetadata` Data Structure

```csharp
public struct RegionMetadata
{
    public int    regionId;       // 1-based; matches NamedRegionId field values
    public BiomeType biomeType;   // biome classification of this region
    public int    cellCount;      // number of cells in the region (area)
    public int    centroidX;      // integer centroid X (sum of x / cellCount)
    public int    centroidY;      // integer centroid Y (sum of y / cellCount)
    public int    minX, minY;     // bounding box min corner
    public int    maxX, maxY;     // bounding box max corner
}
```

**Storage:** `RegionMetadata[]` array, indexed by region ID (index 0 unused — region IDs
are 1-based). Owned by the stage and attached to `MapContext2D` via a new lightweight
metadata accessor pattern.

**Metadata accessor — design decision (to resolve at implementation time):**

The pipeline currently has no mechanism for stages to attach non-grid structured data to
`MapContext2D`. Region metadata is the first case of structured tabular output.

Options:
- **Option A (recommended):** Add a `Dictionary<string, object>` metadata bag to
  `MapContext2D`. Keyed by a stage-defined string (e.g., `"RegionTable"`). Downside:
  boxing, stringly-typed. Upside: zero-change to existing contracts; future metadata
  producers (Phase N POI tables) use the same pattern.
- **Option B:** Add a typed `RegionMetadata[]` property to `MapContext2D`. Clean but
  couples the context to a specific phase.
- **Option C:** Return the table as a separate output from the stage, outside
  `MapContext2D`. Requires runner changes.

**Decision status: Option A recommended. Simple, extensible, does not modify existing
contracts. Region table is read-only after the stage writes it. The metadata bag is
an additive extension to `MapContext2D` — no existing code is affected.**

### 3.5 Small Region Handling

Noise-generated biome boundaries produce many tiny 1–3 cell regions that clutter the
region map and produce meaningless named regions. Pass 5a explicitly warns: "noise
sensitivity: noisy generation produces many tiny 1–2 cell components that clutter the
label map."

**Strategy: post-CCA merge pass.**

After initial flood fill, regions below a minimum cell count are merged into the largest
4-adjacent neighbor region of any biome type. This relabels the small region's cells and
updates metadata.

```
minRegionArea = 8  // tunable; default 8 cells

for each region R where R.cellCount < minRegionArea:
    find the adjacent region with the most shared border cells
    relabel all of R's cells to that neighbor's regionId
    update neighbor's metadata (cellCount, centroid, bounding box)
    remove R from the region table
```

**Design notes:**

- The merge target is chosen by border length (most shared 4-adjacent cells), not by
  biome similarity. This prevents orphaned tiny regions surrounded by a different biome
  from remaining unmerged.
- Merging into a different-biome region means the merged cells' NamedRegionId points to a
  region whose biomeType differs from their Biome field value. This is intentional — the
  NamedRegionId is a geographic grouping, not a biome classifier. The Biome field remains
  the authoritative biome-per-cell truth.
- The merge pass is deterministic: regions are processed in ID order (row-major discovery
  order), and the tie-breaking rule for equal border lengths is lower region ID.
- `minRegionArea` is a stage-local tunable (default 8). Setting it to 0 disables merging.

### 3.6 Stage-Local Tunables

```csharp
int minRegionArea = 8;  // [0, 64]; 0 = no merging. Regions below this are merged.
```

### 3.7 Invariants (Test-Gated)

| ID | Invariant |
|----|-----------|
| M2b-1 | **Determinism:** Same seed + tunables => identical NamedRegionId field and RegionTable |
| M2b-2 | **Water sentinel:** `NamedRegionId[x,y] == 0f` for all non-Land cells |
| M2b-3 | **Land coverage:** `NamedRegionId[x,y] > 0f` for all Land cells |
| M2b-4 | **Region contiguity:** every region's cells form a single 4-connected component (post-merge) |
| M2b-5 | **No-mutate:** `Biome`, `Land`, `Vegetation`, all other fields/layers unchanged |
| M2b-6 | **Metadata consistency:** `RegionTable[id].cellCount` matches actual count of cells with that ID |
| M2b-7 | **Metadata coverage:** every region ID in the field has a corresponding RegionTable entry |
| M2b-8 | **Minimum area (post-merge):** if `minRegionArea > 0`, no region has `cellCount < minRegionArea` (unless the entire biome on the map is smaller) |
| M2b-9 | **Valid region ID range:** all Land values are in [1, regionCount] with no gaps |

### 3.8 Test Plan (M2.b)

#### 3.8.1 Stage-Level Tests (`StageRegions2DTests.cs`)

| Test | Asserts |
|------|---------|
| Determinism | Two runs, same config → identical NamedRegionId hash + RegionTable |
| NamedRegionId golden | Hash locked for default tunables, 64×64, seed=12345 |
| Water sentinel (M2b-2) | All non-Land cells have `NamedRegionId == 0f` |
| Land coverage (M2b-3) | All Land cells have `NamedRegionId > 0f` |
| Contiguity (M2b-4) | Per-region flood fill from any cell reaches all cells of that region |
| No-mutate (M2b-5) | Input field/layer hashes unchanged |
| Metadata consistency (M2b-6) | Per-region cell count matches metadata |
| Metadata coverage (M2b-7) | Every field value has a table entry |
| Small region merge | With `minRegionArea = 8`, no region has fewer than 8 cells |
| Merge disabled | With `minRegionArea = 0`, tiny regions are preserved |
| Single-biome island | All Land cells get regionId = 1 (one contiguous region) |
| Multi-biome island | Multiple regions detected, each internally contiguous |

#### 3.8.2 Pipeline Golden (`MapPipelineRunner2DGoldenM2Tests.cs`)

Full F0–M2 golden (includes M2.a vegetation + M2.b regions). Does NOT invalidate F0–G
goldens.

---

## 4. New Operator: `ScalarFieldCcaOps2D`

M2.b requires connected-component analysis on a scalar field (Biome IDs), not on a binary
mask. The existing `MaskFloodFillOps2D` operates on `MaskGrid2D` (binary); the existing
`MaskTopologyOps2D` computes edge/interior topology but not multi-label CCA.

A new operator bridges this gap.

```csharp
public static class ScalarFieldCcaOps2D
{
    /// <summary>
    /// Labels contiguous regions of cells sharing the same integer value in the source field.
    /// Only processes cells where the membership mask is ON.
    /// Uses 4-connectivity (cardinal neighbors). Row-major scan order for deterministic IDs.
    /// </summary>
    /// <param name="src">Source scalar field (integer-as-float values, e.g., Biome IDs)</param>
    /// <param name="membership">Mask restricting which cells are processed (e.g., Land)</param>
    /// <param name="dst">Output scalar field: 0 for non-member cells, 1–N for region IDs</param>
    /// <param name="regionCount">Total number of regions found</param>
    public static void LabelConnectedRegions4(
        ref ScalarField2D src,
        ref MaskGrid2D membership,
        ref ScalarField2D dst,
        out int regionCount)

    /// <summary>
    /// Computes metadata for each labeled region.
    /// </summary>
    public static RegionMetadata[] ComputeRegionMetadata(
        ref ScalarField2D regionField,
        ref ScalarField2D biomeField,
        int regionCount)

    /// <summary>
    /// Merges regions below minArea into their largest adjacent neighbor.
    /// Deterministic: processes regions in ID order, breaks ties by lower neighbor ID.
    /// </summary>
    public static void MergeSmallRegions(
        ref ScalarField2D regionField,
        ref RegionMetadata[] table,
        ref int regionCount,
        int minArea)
}
```

**Design notes:**

- `LabelConnectedRegions4` uses BFS flood fill (queue-based) from each unvisited member
  cell, matching the pattern established by `MaskFloodFillOps2D`. Two-pass union-find is
  an alternative but flood fill is simpler and consistent with codebase conventions.
- The `membership` mask parameter keeps the operator general — it can be used for any
  scalar-field CCA, not just biomes on Land.
- `MergeSmallRegions` modifies `regionField` in place and compacts the `table` array.
  After merging, region IDs may have gaps; a final relabel pass renumbers them to
  [1, regionCount] contiguously.

### 4.1 Operator Tests (`ScalarFieldCcaOps2DTests.cs`)

| Test | Asserts |
|------|---------|
| Single value, single component | All member cells get regionId = 1 |
| Two values, checkerboard | Many single-cell regions (4-connected: diagonals are separate) |
| Two blobs same value | Two regions if separated by different value |
| Non-member cells | Always 0 in output |
| Determinism | Same input → identical output |
| Metadata accuracy | cellCount, centroid, bounding box all correct |
| Merge: tiny regions absorbed | Regions below threshold merge into neighbors |
| Merge: isolated tiny region | Region surrounded by a single neighbor merges into it |
| Merge: relabel contiguous | After merge, IDs are contiguous 1–N |

---

## 5. Region Naming (Adapter-Side)

Region naming is explicitly **not** a pipeline concern. The pipeline produces region IDs
and metadata; adapters generate display names. This preserves the adapters-last boundary.

**Recommended approach for future adapter work (not in scope for M2.b):**

A `RegionNameGenerator` adapter that takes `RegionMetadata[]` and produces name strings
using a formula like:

```
[directional prefix] + [biome display name]

Examples:
  regionId=1, biomeType=TemperateForest, centroid=(20, 15)  → "The Northern Forest"
  regionId=2, biomeType=SubtropicalDesert, centroid=(45, 50) → "The Eastern Desert"
  regionId=3, biomeType=Grassland, centroid=(30, 40)         → "The Central Plains"
```

Directional prefix derived from centroid position relative to map center. Deduplication
by appending ordinal ("The Second Northern Forest") or using additional qualifiers
(area-based: "The Great Northern Forest" vs "The Lesser Northern Forest").

This is a content/presentation concern and can be arbitrarily sophisticated without
touching the pipeline.

---

## 6. Combined Invariants Summary

| ID | Invariant | Sub-phase |
|----|-----------|-----------|
| M2a-1 | `Vegetation ⊆ Land` | M2.a |
| M2a-2 | `Vegetation ⊆ LandInterior` | M2.a |
| M2a-3 | `Vegetation ∩ HillsL2 == ∅` | M2.a |
| M2a-4 | `Vegetation ∩ ShallowWater == ∅` | M2.a |
| M2a-5 | Vegetation determinism | M2.a |
| M2a-6 | No-mutate (inputs including Biome, Moisture) | M2.a |
| M2a-7 | Biome-zero suppression (water cells) | M2.a |
| M2a-8 | Snow suppression | M2.a |
| M2a-9 | Coverage monotonicity (statistical) | M2.a |
| M2b-1 | Region determinism | M2.b |
| M2b-2 | Water sentinel (NamedRegionId) | M2.b |
| M2b-3 | Land coverage (NamedRegionId) | M2.b |
| M2b-4 | Region contiguity (4-connected) | M2.b |
| M2b-5 | No-mutate (Biome, Land, Vegetation, etc.) | M2.b |
| M2b-6 | Metadata consistency (cell counts) | M2.b |
| M2b-7 | Metadata coverage (all IDs have entries) | M2.b |
| M2b-8 | Minimum area post-merge | M2.b |
| M2b-9 | Valid region ID range (contiguous 1–N) | M2.b |

---

## 7. Downstream Consumers

| Consumer | Reads | How |
|----------|-------|-----|
| Phase N (POI Placement) | `NamedRegionId`, `RegionTable` | Region-aware POI suitability (e.g., one dungeon per forest region) |
| Phase W (World tiles) | `NamedRegionId`, `RegionTable` | World-tile region names for UI/tooltip |
| TilesetConfig | `Biome` (unchanged) | Biome-conditional tile entries remain per-cell, not per-region |
| Composite viz | `NamedRegionId` | Region overlay visualization (unique color per region) |
| MapExporter2D | `NamedRegionId` | Automatic export (no contract changes needed) |
| RegionNameGenerator (adapter) | `RegionTable` | Display name generation (future adapter) |

---

## 8. Dependencies

| Dependency | Status | Required for | Required? |
|------------|--------|--------------|-----------|
| Phase M (Biome, Moisture, Temperature) | Planning (design complete) | Both M2.a and M2.b | Yes |
| F5 (Stage_Vegetation2D) | Done | M2.a (modified in place) | Yes |
| `BiomeTable.Definitions` (Phase M) | Planning (design complete) | M2.a (vegetationDensity lookup) | Yes |
| `MaskFloodFillOps2D` | Done | M2.b (pattern reference) | No (new operator) |
| `MaskTopologyOps2D` | Done | M2.b (convention reference) | No (new operator) |

---

## 9. Open Questions (Implementation Time)

| Question | Recommended | Notes |
|----------|-------------|-------|
| Moisture modulation default | 0.0f (disabled) | Include parameter, default off |
| Phase M absence behavior | Fallback to global 0.40f threshold | Option A from section 2.6 |
| RegionTable attachment to MapContext2D | Metadata bag (`Dictionary<string, object>`) | Option A from section 3.4 |
| CCA algorithm | Flood fill (BFS) | Consistent with existing `MaskFloodFillOps2D` pattern |
| Small region merge threshold | 8 cells default | Tunable; 0 disables |
| Region ID assignment order | Row-major discovery | Matches `MaskTopologyOps2D` convention |

---

## 10. Expected Files

**Runtime (M2.a):**
- `Stage_Vegetation2D.cs` (modified — reads Biome field, per-biome threshold)

**Runtime (M2.b):**
- `Stage_Regions2D.cs` (new)
- `RegionMetadata.cs` (new)
- `ScalarFieldCcaOps2D.cs` (new operator)
- `MapIds2D.cs` (updated — `MapFieldId.NamedRegionId = 5`, COUNT → 6)
- `MapContext2D.cs` (updated — metadata bag accessor, if Option A)

**Tests:**
- `StageVegetation2DTests.cs` (updated — biome-aware tests, new golden)
- `StageRegions2DTests.cs` (new)
- `ScalarFieldCcaOps2DTests.cs` (new)
- `MapPipelineRunner2DGoldenM2Tests.cs` (new — replaces old F5 pipeline golden)

**Sample:**
- `PCGMapVisualization` + `PCGMapCompositeVisualization` patched for NamedRegionId view
- Region overlay mode (unique color per region)

**No changes to:** `MapExporter2D.cs`, `TilemapAdapter2D.cs`, `BiomeTable.cs`, `BiomeType.cs`

---

## 11. What Phase M2 Does NOT Do

- **No biome transition blending.** Biome boundaries remain hard. Blend weights or
  transition-zone vegetation mixing are a later refinement if needed.
- **No vegetation type variation.** All vegetation is still a single `Vegetation` mask.
  Biome-specific vegetation types (trees vs. shrubs vs. cacti) require new MapLayerIds
  or a vegetation-type field — out of scope.
- **No region name generation.** The pipeline produces IDs and metadata only. Name
  strings are an adapter-side concern.
- **No region adjacency graph.** The metadata table records per-region statistics but not
  which regions are adjacent. A region adjacency graph (for political borders, trade
  routes, etc.) is a potential M2.c or Phase J2 concern.
- **No region-level constraint propagation.** Biome diversity rules ("no two adjacent
  regions of the same rare biome") require Phase J's Voronoi partitioning. M2.b's CCA
  regions are discovered post-hoc from Phase M's per-cell biome output; they are not
  constrained during generation.
