# Phase L — Hydrology: Design Specification

Status: Planning only (not implementation authority)  
Authority: Design reference for Phase L implementation. Supplements the Phase L entry
in `PCG_Roadmap.md` with detailed stage contracts, algorithms, data structures,
and test plan.  
Depends on: Resolved design decisions from 2026-04-06 (roadmap Decisions 2 and 3).

---

## 1. Phase Intent

Phase L adds water features to the island — rivers that flow downhill from peaks to coast,
and lakes that fill inland depressions. These are the first terrain features driven by
topological simulation rather than noise thresholding. Rivers provide movement obstacles,
settlement placement constraints, and the `FlowAccumulation` field that Phase M consumes
for ecologically grounded moisture. Lakes provide gameplay-distinct freshwater bodies
separate from the coastal ShallowWater ring.

Phase L is designed to be **implementable independently of Phase M, Phase J, and Phase K**.
Its outputs are consumed by Phase M (moisture enrichment) and Phase N (POI constraints),
but no downstream phase is required for Phase L to produce valid output. Phase M is
explicitly designed with a fallback path for absent Phase L data.

---

## 2. Resolved Design Decisions (from Roadmap 2026-04-06)

These decisions are recorded in `PCG_Roadmap.md` and treated as settled here.

| # | Question | Resolution | Key rationale |
|---|----------|------------|---------------|
| DD-2 | River representation | Both `MapFieldId.FlowAccumulation` + `MapLayerId.Rivers` | Field is simulation output; mask is gameplay/rendering output |
| DD-3 | Lake modeling | Distinct `MapLayerId.Lakes` | Gameplay semantics differ from coastal ShallowWater |

---

## 3. New Registry Entries

### MapFieldId additions

| ID | Name | Written By | Type | Notes |
|----|------|-----------|------|-------|
| * | `FlowAccumulation` | `Stage_Hydrology2D` (sub-stage L.1) | float (raw upstream cell count) | New; append-only |

\* Exact ID depends on implementation order relative to Phase M. If Phase M is
implemented first (Temperature=3, Biome=4, COUNT→5), then FlowAccumulation = 5.
If Phase L is implemented first, FlowAccumulation = 3 and Phase M's IDs shift
accordingly. Phase M2's `NamedRegionId` shifts by +1 in either case. IDs are
confirmed at implementation time.

### MapLayerId additions

| ID | Name | Written By | Notes |
|----|------|-----------|-------|
| 12 | `Rivers` | `Stage_Hydrology2D` (sub-stage L.1) | New; append-only |
| 13 | `Lakes` | `Stage_Hydrology2D` (sub-stage L.2) | New; append-only |

After Phase L: `MapLayerId.COUNT` → 14 (was 12 after Phase G).

---

## 4. Stage Contract: `Stage_Hydrology2D`

### 4.1 Pipeline Position

After `Stage_Morphology2D` (Phase G), before `Stage_Biome2D` (Phase M).

Full pipeline order (with all planned phases):

```
BaseTerrain(F2) -> Hills(F3) -> Shore(F4) -> Traversal(F6)
  -> Morphology(G) -> Hydrology(L) -> Biome(M) -> Vegetation(M2.a) -> Regions(M2.b)
```

**Ordering rationale:** Phase L needs only `Height` (F2), `Land`/`DeepWater` (F2), and
`ShallowWater` (F4) — all available after F4. It is placed after Morphology rather than
after Shore because: (a) the pipeline's established pattern groups simulation stages after
geometric stages, and (b) Morphology's `CoastDist` field is not consumed by Phase L but
is consumed by Phase M, so placing L between G and M keeps the simulation chain contiguous
without interleaving. Phase M reads `FlowAccumulation` for moisture derivation, so L must
precede M.

### 4.2 Inputs (Read-Only)

| Data | Source | Usage |
|------|--------|-------|
| `Height` (MapFieldId 0) | Stage_BaseTerrain2D | Depression filling, D8 flow directions, flow accumulation |
| `Land` (MapLayerId 0) | Stage_BaseTerrain2D | Restricts river simulation to land cells; lake membership |
| `DeepWater` (MapLayerId 1) | Stage_BaseTerrain2D | Lake detection: excludes border-connected ocean |
| `ShallowWater` (MapLayerId 3) | Stage_Shore2D | Lake detection: excludes land-adjacent ring |

### 4.3 Outputs (Authoritative Write)

| Output | Type | Range / Contract |
|--------|------|-----------------|
| `FlowAccumulation` (MapFieldId) | float scalar field | Raw upstream cell count. 0f for non-Land cells. ≥1f for all Land cells (each cell counts itself). Maximum value = total Land cell count (all drainage converges to a single cell). |
| `Rivers` (MapLayerId 12) | binary mask | `Rivers ⊆ Land`. Derived by thresholding `FlowAccumulation`. |
| `Lakes` (MapLayerId 13) | binary mask | `Lakes ⊆ (NOT Land AND NOT DeepWater AND NOT ShallowWater)`. Inland water bodies. |

### 4.4 RNG / Noise Strategy

**No RNG consumption. No noise.** Phase L is fully deterministic from the Height field
and Land/water masks. All algorithms (Priority-Flood, D8, flow accumulation, CCA) are
deterministic by construction when scan order and tie-breaking rules are fixed.

This is the same pattern as `Stage_Morphology2D` (Phase G) and `Stage_Regions2D` (M2.b):
purely algorithmic stages that derive structure from existing data without noise.

---

## 5. Sub-Stage Detail

### 5.1 Sub-Stage L.1 — River Generation

River generation follows the standard hydrology pipeline from Pass 2b (Barnes 2014), adapted
for Islands' grid-first island geometry. The pipeline has four sequential steps, each
operating on intermediate data produced by the previous step. Only the final two outputs
(`FlowAccumulation` field and `Rivers` mask) are persisted.

#### 5.1.1 Step 1 — Depression Filling (Priority-Flood+ε)

**Purpose:** Eliminate local minima (depressions) in the Height field so that every Land
cell has a monotonically non-increasing path to the coast. Without this step, steepest-
descent flow tracing would dead-end at interior depressions, producing truncated rivers.

**Algorithm (Priority-Flood+ε, adapted for island geometry):**

The standard Priority-Flood floods inward from grid edges. For an island, the natural
drainage outlets are not the grid edges but the coastline — Land cells adjacent to water.
The algorithm is adapted accordingly:

```
Input:  Height field (read-only), Land mask
Output: filledHeight (float[], same dimensions, allocated as stage-local temp buffer)

// 1. Initialize: copy Height values; prepare visited array and min-heap
filledHeight[*] = Height[*]
visited[*] = false
minHeap = new PriorityQueue<(int x, int y), float>()

// 2. Seed coastal cells: Land cells with at least one non-Land 8-neighbor
//    (8-neighbor check matches D8 connectivity in step 2)
for y in 0..height:
  for x in 0..width:
    if Land[x,y]:
      for each of 8 neighbors (nx, ny):
        if NOT Land[nx,ny] OR out-of-bounds:
          minHeap.Enqueue((x, y), filledHeight[x,y])
          visited[x,y] = true
          break  // only enqueue once

// 3. Flood inward
while minHeap is not empty:
  (cx, cy), priority = minHeap.Dequeue()
  for each of 8 neighbors (nx, ny) that are Land AND NOT visited:
    visited[nx,ny] = true
    if filledHeight[nx,ny] <= filledHeight[cx,cy]:
      filledHeight[nx,ny] = filledHeight[cx,cy] + ε
    minHeap.Enqueue((nx, ny), filledHeight[nx,ny])
```

**Epsilon (ε) value:** `1e-5f`. This is within the range recommended by Pass 6b
(10⁻⁵ to 10⁻⁴ of total elevation range, which is [0,1] for the Height field). Small
enough to preserve erosion-carved detail, large enough for unambiguous flow direction
with 32-bit float precision.

**Neighbor visit order (determinism):** 8 neighbors visited in fixed clockwise order
starting from North: N, NE, E, SE, S, SW, W, NW. Combined with the min-heap's stable
ordering (equal priorities resolved by row-major cell index), this produces deterministic
output. The `PriorityQueue` implementation must break ties deterministically — if the
platform's built-in queue does not guarantee stable ordering, use cell index as a
secondary sort key: `(float priority, int cellIndex)` where `cellIndex = x + y * width`.

**8-connectivity rationale:** D8 flow directions use 8-connected neighbors (Moore
neighborhood). Depression filling must use the same connectivity to ensure consistency —
a filled path must be traversable by D8. This differs from the pipeline's usual
4-connectivity convention (used by topology, morphology, shores) but is standard and
necessary for hydrology. The technique reports are explicit: "D8 flow directions assign
each cell to its steepest downslope neighbour among the eight Moore neighbours."

**Performance note:** Priority-Flood is O(N log N) with a binary heap, where N is the
number of Land cells. For a 256×256 map (~40K land cells), this completes in <1ms.
The Barnes FIFO optimization (flat regions use a plain queue instead of the heap) can
be added as a performance refinement but is not required for correctness.

#### 5.1.2 Step 2 — D8 Flow Directions

**Purpose:** Assign each Land cell a flow direction pointing toward its steepest downslope
8-neighbor. This produces a directed acyclic graph where every Land cell has exactly one
downstream pointer, terminating at the coastline.

**Algorithm:**

```
Input:  filledHeight (from step 1), Land mask
Output: flowDir (int[], stage-local temp buffer; not persisted)

// D8 direction encoding: 0-7 for 8 neighbors, -1 for drainage sink (coast-adjacent)
dirOffsets = [(0,-1), (1,-1), (1,0), (1,1), (0,1), (-1,1), (-1,0), (-1,-1)]
             //  N       NE      E      SE      S       SW       W       NW
dirDistances = [1.0, √2, 1.0, √2, 1.0, √2, 1.0, √2]

for each Land cell (x, y):
  bestSlope = 0
  bestDir = -1
  for d in 0..7:
    (nx, ny) = (x + dirOffsets[d].x, y + dirOffsets[d].y)
    if out-of-bounds OR NOT Land[nx,ny]:
      // Non-land / OOB neighbor: treat as sea level (elevation 0)
      slope = filledHeight[x,y] / dirDistances[d]
    else:
      slope = (filledHeight[x,y] - filledHeight[nx,ny]) / dirDistances[d]
    if slope > bestSlope:
      bestSlope = slope
      bestDir = d

  flowDir[x,y] = bestDir
```

**Tie-breaking:** If multiple neighbors share the same maximum slope, the first one in
the fixed clockwise scan order wins. This is deterministic.

**Flat regions:** After Priority-Flood+ε, no two adjacent cells have identical filled
height (ε ensures a gradient). Therefore flat-region tie-breaking should not occur in
practice. If it does (float precision edge case), the clockwise scan tie-breaking
handles it.

**Non-Land neighbor drainage:** When a Land cell's steepest downslope is toward a
non-Land cell (ocean), `bestDir` points there. That cell is the drainage terminus.
The flow accumulation step (L.1.3) treats it as a sink — accumulated flow exits the
grid at that point.

**`flowDir` is not persisted.** Per the roadmap: "MapFieldId.FlowDirection is computed
during Phase L but not persisted as a registered field. It is an intermediate data
structure consumed only within Stage_Hydrology2D."

#### 5.1.3 Step 3 — Flow Accumulation

**Purpose:** Compute how many upstream cells drain through each Land cell. High values
indicate major drainage convergence — river locations. This is the primary simulation
output of Phase L.

**Algorithm:**

```
Input:  filledHeight (from step 1), flowDir (from step 2), Land mask
Output: flowAccum (float[], written to MapFieldId.FlowAccumulation)

// 1. Sort all Land cells by filledHeight, descending (highest first)
sortedCells = all (x, y) where Land[x,y], sorted by filledHeight descending
// Tie-break: row-major index (lower index first) for determinism

// 2. Initialize: each Land cell starts with 1 (counts itself)
flowAccum[x,y] = 1f for all Land cells
flowAccum[x,y] = 0f for all non-Land cells

// 3. Traverse highest-to-lowest, passing accumulated flow downstream
for each (x, y) in sortedCells:
  d = flowDir[x,y]
  if d >= 0:
    (nx, ny) = (x + dirOffsets[d].x, y + dirOffsets[d].y)
    if Land[nx,ny]:  // only accumulate into Land cells
      flowAccum[nx,ny] += flowAccum[x,y]
    // else: flow exits to ocean (sink)
```

**Value range:** `FlowAccumulation` stores raw upstream cell counts as floats. Minimum
value for any Land cell is 1f (counts itself). Maximum possible value equals the total
Land cell count (all drainage converges to a single cell). On a 64×64 map with ~2000
Land cells, peak values are typically 200–800. On a 256×256 map, peaks can reach 20K+.

**No normalization.** Raw counts are stored, consistent with `CoastDist` (which stores
raw integer BFS distances, not [0,1] values). Phase M normalizes via its own
`riverFlowNorm` tunable when computing moisture. Adapter-side consumers can normalize
against the observed maximum if needed.

**Resolution-scaling note (from Pass 6b):** Flow accumulation counts scale approximately
as the square of resolution. A river threshold that works at 64×64 will be too low at
256×256. The thresholding step (L.1.4) addresses this with a resolution-relative default.

#### 5.1.4 Step 4 — River Extraction (Thresholding)

**Purpose:** Derive the binary `Rivers` mask from `FlowAccumulation` by thresholding.
Cells with flow accumulation above the threshold are classified as rivers.

**Algorithm:**

```
Input:  flowAccum (from step 3), Land mask
Output: Rivers mask (MapLayerId.Rivers)

totalLandCells = Land.CountOnes()
threshold = totalLandCells * riverThresholdFraction

for each cell (x, y):
  Rivers[x,y] = Land[x,y] AND (flowAccum[x,y] >= threshold)
```

**`riverThresholdFraction` (tunable, default 0.02):** Fraction of total Land cells. A
cell becomes a river if at least this fraction of all Land cells drain through it. The
fractional threshold auto-scales with resolution, addressing the resolution-sensitivity
issue documented in Pass 6b: "the threshold must scale with resolution, approximately
as the square of the resolution ratio." Since total Land cells scale as resolution²,
using a fraction of Land cells provides correct scaling automatically.

**Default 0.02 (2%):** On a 64×64 map with ~2000 Land cells, threshold ≈ 40 upstream
cells. On a 256×256 map with ~40K Land cells, threshold ≈ 800. These produce visually
reasonable river density — a few major drainage paths per island with tributaries above
the threshold.

**River width is adapter-side.** The `Rivers` mask is single-pixel (one cell wide).
Width variation for rendering (streams vs. rivers) can be derived adapter-side by
reading `FlowAccumulation` values at `Rivers` cells and applying secondary thresholds
(e.g., `flowAccum > 4× threshold` = wide river tile). This is not in scope for Phase L.

---

### 5.2 Sub-Stage L.2 — Lake Detection

**Purpose:** Identify inland water bodies — NOT-Land cells enclosed within the island
that are not part of the border-connected ocean (`DeepWater`) or the coastal ring
(`ShallowWater`). These cells are the result of noise-generated height depressions that
dip below the water threshold inside the island perimeter.

**Algorithm:**

```
Input:  Land mask, DeepWater mask, ShallowWater mask
Output: Lakes mask (MapLayerId.Lakes)

// Lake candidate cells: water cells that are neither ocean nor coastal ring
for each cell (x, y):
  Lakes[x,y] = NOT Land[x,y] AND NOT DeepWater[x,y] AND NOT ShallowWater[x,y]
```

**Explanation of the three-way exclusion:**
- `NOT Land`: must be a water cell.
- `NOT DeepWater`: must not be connected to the map border (DeepWater = border-connected
  NOT-Land, computed in F2 via flood fill).
- `NOT ShallowWater`: must not be in the 1-cell land-adjacent ring (ShallowWater = NOT-Land
  cells 4-adjacent to at least one Land cell, computed in F4).

The ShallowWater exclusion means that the shoreline ring of an inland depression is
classified as ShallowWater (not Lakes). Only cells deeper than 1 cell from the lake
shore are Lakes. This produces a natural visual layering: Land → ShallowWater → Lakes,
mirroring the coastal Land → ShallowWater → DeepWater layering. A 1-cell-wide depression
is entirely ShallowWater with no Lakes cells — appropriately, since a single-cell puddle
is not a meaningful lake.

**No CCA required for the base mask.** The Lakes output is a simple boolean mask. Unlike
M2.b (which needs per-region IDs and metadata), Phase L only needs to identify which
cells are lake cells. CCA is not needed unless we add minimum-size filtering.

**Optional: minimum lake size filtering.**

Small 1–2 cell lake fragments can appear from noise artifacts. An optional post-pass
removes lakes below a minimum area:

```
if minLakeArea > 0:
  Run CCA (4-connected flood fill) on Lakes mask
  For each component with cellCount < minLakeArea:
    Clear those cells from Lakes mask
```

This uses existing `MaskFloodFillOps2D` infrastructure. The filter is controlled by
a stage-local tunable (`minLakeArea`, default 0 = no filtering). Unlike M2.b's region
merge (which relabels cells into neighbors), lake filtering simply removes the cells
— a tiny isolated water pocket becomes unclassified (neither Lake nor DeepWater nor
ShallowWater nor Land). This is acceptable because the cell is already non-Land and
has no gameplay function at that size.

**Lake occurrence depends on terrain.** Whether lakes appear at all is determined by
the noise-generated Height field. With default island settings (radial falloff +
moderate noise amplitude), inland depressions below the water threshold are possible
but not guaranteed. Larger maps and higher noise amplitudes produce more lakes. This
is emergent and intentional — lakes are a natural terrain feature, not a forced one.

---

## 6. New Operator: `HeightFieldHydrologyOps2D`

Phase L's algorithms (Priority-Flood, D8, flow accumulation) are domain-specific to
hydrology but cleanly separable from the stage orchestration. A dedicated operator
houses them, following the established pattern of `MaskFloodFillOps2D`,
`MaskTopologyOps2D`, `MaskMorphologyOps2D`, and `ScalarFieldCcaOps2D`.

```csharp
public static class HeightFieldHydrologyOps2D
{
    /// <summary>
    /// Priority-Flood+ε depression filling for island geometry.
    /// Floods inward from coastal Land cells (8-adjacent to non-Land), raising
    /// depressions to ensure monotonic drainage toward the coast.
    /// </summary>
    /// <param name="height">Source height field (read-only)</param>
    /// <param name="land">Land mask — only Land cells are processed</param>
    /// <param name="filledHeight">Output: depression-free height values for Land cells.
    /// Non-Land cells receive 0f. Allocated by caller.</param>
    /// <param name="epsilon">Gradient increment for raised cells (default 1e-5f)</param>
    public static void FillDepressions(
        ref ScalarField2D height,
        ref MaskGrid2D land,
        ref ScalarField2D filledHeight,
        float epsilon = 1e-5f)

    /// <summary>
    /// D8 flow direction assignment. Each Land cell receives a direction index (0–7)
    /// pointing to its steepest downslope 8-neighbor. Non-Land cells receive -1.
    /// </summary>
    /// <param name="filledHeight">Depression-free height from FillDepressions</param>
    /// <param name="land">Land mask</param>
    /// <param name="flowDir">Output: per-cell direction index. Allocated by caller.</param>
    public static void ComputeFlowDirectionsD8(
        ref ScalarField2D filledHeight,
        ref MaskGrid2D land,
        int[] flowDir)

    /// <summary>
    /// Flow accumulation: traverses Land cells from highest to lowest filled height,
    /// passing each cell's accumulated count to its downstream D8 neighbor.
    /// </summary>
    /// <param name="filledHeight">Depression-free height (for sort order)</param>
    /// <param name="land">Land mask</param>
    /// <param name="flowDir">D8 directions from ComputeFlowDirectionsD8</param>
    /// <param name="flowAccum">Output scalar field: upstream cell counts.
    /// 0f for non-Land, ≥1f for all Land cells. Allocated by caller.</param>
    public static void AccumulateFlow(
        ref ScalarField2D filledHeight,
        ref MaskGrid2D land,
        int[] flowDir,
        ref ScalarField2D flowAccum)

    /// <summary>
    /// Thresholds flow accumulation to produce a binary river mask.
    /// </summary>
    /// <param name="flowAccum">Flow accumulation field</param>
    /// <param name="land">Land mask</param>
    /// <param name="threshold">Minimum flow count for river classification</param>
    /// <param name="rivers">Output mask: ON where flowAccum >= threshold AND Land</param>
    public static void ExtractRivers(
        ref ScalarField2D flowAccum,
        ref MaskGrid2D land,
        float threshold,
        ref MaskGrid2D rivers)
}
```

**Design notes:**

- All methods are pure, deterministic, and static. No RNG, no noise, no allocations
  beyond caller-provided output buffers.
- Scan and neighbor visit orders are fixed (documented per-method) for determinism.
- The operator does NOT include lake detection — that is a simple mask-boolean operation
  that does not warrant operator infrastructure.
- `FillDepressions` uses an internal min-heap. The heap implementation must be deterministic
  (stable ordering for equal priorities). If C#'s `PriorityQueue<TElement, TPriority>` does
  not guarantee stable dequeue order for equal priorities, use a composite priority
  `(float height, int cellIndex)` to enforce row-major tie-breaking.
- `AccumulateFlow` requires a descending sort of all Land cells by filled height. Sorting
  must be stable (equal heights resolved by row-major cell index). `Array.Sort` with a
  custom comparer using cell index as tiebreaker satisfies this.

### 6.1 Operator Tests (`HeightFieldHydrologyOps2DTests.cs`)

| Test | Asserts |
|------|---------|
| Fill flat island | All Land cells retain original height (no depressions to fill) |
| Fill single depression | Depression cell raised to outlet height + ε |
| Fill nested depressions | All depressions filled; every Land cell can drain to coast |
| Fill preserves peaks | Peak cells never lowered by depression filling |
| D8 simple slope | Uniform slope → all cells point downhill |
| D8 diagonal preference | Steepest diagonal wins over shallower cardinal |
| D8 coastal drainage | Coastal cells point toward non-Land neighbor |
| D8 after fill | No Land cell has flowDir == -1 with no valid downslope (post-fill guarantee) |
| Flow accum: single line | Linear drainage → downstream cell has count = N |
| Flow accum: fork | Two tributaries merge → downstream cell = sum + 1 |
| Flow accum: all cells ≥ 1 | Every Land cell has flowAccum ≥ 1f |
| Flow accum: non-Land = 0 | Every non-Land cell has flowAccum = 0f |
| Flow accum: conservation | Sum of all land flowAccum values going to non-Land = total Land cell count |
| River threshold | Threshold at 50% → only the highest-accumulation cells are Rivers |
| Determinism (all ops) | Two runs, same input → identical outputs |

---

## 7. Intermediate Data (Not Persisted)

The following data structures are computed within `Stage_Hydrology2D` but not written to
`MapContext2D` as registered fields or layers:

| Data | Type | Lifetime | Notes |
|------|------|----------|-------|
| `filledHeight` | `float[]` (or temp `ScalarField2D`) | Stage-local, freed after stage completes | Depression-free height values. Required by D8 and flow accumulation. |
| `flowDir` | `int[]` | Stage-local, freed after stage completes | D8 direction per cell (0–7 or -1). Required by flow accumulation. |

Per the roadmap: "MapFieldId.FlowDirection is computed during Phase L but not persisted
as a registered field. It is an intermediate data structure consumed only within
Stage_Hydrology2D. If a downstream consumer later requires it, it can be promoted to a
registered field at that time."

**Memory note:** On a 256×256 map, `filledHeight` and `flowDir` together require ~512 KB
of temporary memory. This is allocated at stage entry and freed at stage exit. The
`Allocator.Temp` pattern used by `MaskMorphologyOps2D` (ping-pong buffer) is the
established precedent for stage-local temporaries.

---

## 8. Stage-Local Tunables

```csharp
float epsilon                = 1e-5f;  // [1e-6, 1e-3]; Priority-Flood gradient increment
float riverThresholdFraction = 0.02f;  // [0.005, 0.10]; fraction of Land cells for river
int   minLakeArea            = 0;      // [0, 32]; 0 = no lake filtering; minimum lake size
```

**`epsilon`:** Gradient increment for depression filling. Smaller values preserve terrain
detail but risk float-precision ties. Larger values create visible artificial ramps.
Default 1e-5f is well within the recommended range (Pass 6b: 10⁻⁵ to 10⁻⁴ of the [0,1]
height range).

**`riverThresholdFraction`:** Controls river density. Lower values produce more rivers
(including minor tributaries); higher values produce only major drainage channels.
Auto-scales with resolution because it operates on fraction of Land cells, not raw counts.

**`minLakeArea`:** Minimum connected lake body size in cells. Smaller fragments are
removed. Default 0 disables filtering — all inland water is preserved. Setting to 3–4
removes noise-artifact puddles while preserving intentional lakes.

---

## 9. Invariants (Test-Gated)

| ID | Invariant |
|----|-----------|
| L-1 | **Determinism:** Same seed + tunables ⇒ identical FlowAccumulation, Rivers, Lakes |
| L-2 | **Rivers ⊆ Land:** no river cell exists outside Land |
| L-3 | **Lakes ⊆ NOT-Land:** no lake cell exists on Land |
| L-4 | **Lakes ∩ DeepWater == ∅:** lakes are not border-connected ocean |
| L-5 | **Lakes ∩ ShallowWater == ∅:** lakes are not coastal ring cells |
| L-6 | **Rivers ∩ Lakes == ∅:** rivers and lakes occupy disjoint cell sets (rivers on Land, lakes off Land) |
| L-7 | **FlowAccumulation range:** 0f for non-Land cells, ≥1f for all Land cells |
| L-8 | **FlowAccumulation conservation:** the sum of flow exiting Land to non-Land neighbors equals total Land cell count (all water drains to ocean) |
| L-9 | **Rivers from threshold:** `Rivers[x,y] == true` iff `Land[x,y] AND flowAccum[x,y] >= threshold` |
| L-10 | **No-mutate:** `Height`, `Land`, `DeepWater`, `ShallowWater`, `LandEdge`, `LandInterior`, `HillsL1`, `HillsL2`, `Vegetation`, `Walkable`, `Stairs`, `LandCore`, `CoastDist` all unchanged |
| L-11 | **Depression-free (internal):** after Priority-Flood, every Land cell has at least one 8-neighbor with lower or equal filled height (verified internally, not on persisted output) |
| L-12 | **D8 validity (internal):** after Priority-Flood, every Land cell has a valid flow direction (0–7) pointing toward a strictly lower 8-neighbor (verified internally) |

---

## 10. Test Plan

### 10.1 Operator-Level Tests (`HeightFieldHydrologyOps2DTests.cs`)

See section 6.1 above.

### 10.2 Stage-Level Tests (`StageHydrology2DTests.cs`)

| Test | Asserts |
|------|---------|
| Determinism | Two runs, same config → identical FlowAccumulation hash, Rivers hash, Lakes hash |
| FlowAccumulation golden | Hash locked for default tunables, 64×64, seed=12345 |
| Rivers golden | Hash locked for same config |
| Lakes golden | Hash locked for same config |
| Rivers ⊆ Land (L-2) | All Rivers cells are Land |
| Lakes ⊆ NOT-Land (L-3) | All Lakes cells are NOT-Land |
| Lakes ∩ DeepWater == ∅ (L-4) | No Lakes cell is DeepWater |
| Lakes ∩ ShallowWater == ∅ (L-5) | No Lakes cell is ShallowWater |
| Rivers ∩ Lakes == ∅ (L-6) | Disjoint |
| FlowAccumulation range (L-7) | 0f for non-Land, ≥1f for Land |
| Flow conservation (L-8) | Sum of outgoing flow = total Land cells |
| No-mutate (L-10) | All input field/layer hashes unchanged |
| Rivers reach coast | Every River connected component is 8-adjacent to a non-Land cell (rivers drain to ocean, not dead-ends) |
| No rivers on tiny island | A 4×4 island with ~4 Land cells: Rivers should be empty (no cell exceeds 2% of 4 = threshold < 1, so threshold rounds to 1, all cells qualify — or threshold fraction produces zero rivers depending on rounding). Test documents the small-map edge case. |
| Lake filtering disabled | `minLakeArea = 0` → all inland water fragments preserved |
| Lake filtering enabled | `minLakeArea = 4` → no lake component has fewer than 4 cells |
| No lakes on default island | With default tunables (moderate noise), verify lake count is plausible (may be 0 — not a failure) |

### 10.3 Pipeline Golden (`MapPipelineRunner2DGoldenLTests.cs`)

Full F0–G–L golden. Does NOT invalidate existing F0–G goldens (no upstream stage changes).

**Note on pipeline golden scope:** If Phase L is implemented after Phase M, the pipeline
golden would be F0–G–L–M (since M consumes FlowAccumulation). The test file name and
scope are adjusted at implementation time based on actual phase ordering.

---

## 11. Downstream Consumers

| Consumer | Reads | How |
|----------|-------|-----|
| Phase M (Biome, sub-stage M.2) | `FlowAccumulation` | Moisture enrichment: `riverFactor = riverMoistureBonus * clamp(flowAccum / riverFlowNorm, 0, 1)`. Optional — Phase M has a fallback for absent FlowAccumulation. |
| Phase N (POI Placement) | `Rivers`, `Lakes` | Obstacle constraints (paths must bridge rivers), suitability scoring (lakeside settlements, river crossings) |
| Phase O (Paths) | `Rivers` | River cells increase path cost; optional bridge placement at crossings |
| Phase W (World tiles) | `FlowAccumulation` | World-tile `MoistureLevel` enrichment (via Phase M) |
| TilesetConfig (Phase H3) | `Rivers`, `Lakes` | Water tile entries for rivers and lakes |
| Composite viz (Phase H1) | `Rivers`, `Lakes` | Blue/teal color entries in compositing priority table |
| MapExporter2D (Phase H2) | `FlowAccumulation`, `Rivers`, `Lakes` | Automatic export (no contract changes) |
| Adapter-side river width | `FlowAccumulation` at `Rivers` cells | Multiple threshold levels for stream vs. river tile variants (not in Phase L scope) |

---

## 12. Dependencies

| Dependency | Status | Required? | Role |
|------------|--------|-----------|------|
| F2 (Stage_BaseTerrain2D) | **Done** | Yes | Height field, Land mask, DeepWater mask |
| F4 (Stage_Shore2D) | **Done** | Yes | ShallowWater mask (lake detection exclusion) |
| MaskFloodFillOps2D | **Done** | Optional | Lake size filtering (if `minLakeArea > 0`) |
| Phase G (Stage_Morphology2D) | **Done** | No | Not consumed by Phase L, but Phase L is sequenced after G in the pipeline |
| Phase M (Stage_Biome2D) | Planning | No | Phase M consumes Phase L output, not the reverse |

**No circular dependencies.** Phase L reads only from F2 and F4 outputs. Phase M reads
from Phase L output but Phase L does not read from Phase M.

---

## 13. Interaction with Phase M's Moisture Formula

Phase M's design spec (section 5.2) defines the river moisture factor as:

```
riverFactor = 0.0
if (FlowAccumulation field exists):
    riverFactor = riverMoistureBonus * clamp(flowAccum[x,y] / riverFlowNorm, 0, 1)
```

Phase L's `FlowAccumulation` stores raw upstream cell counts (not normalized). Phase M
normalizes via its own `riverFlowNorm` tunable. The recommended value for `riverFlowNorm`
is the river threshold: `riverFlowNorm = totalLandCells * riverThresholdFraction`. This
means cells AT the river threshold have `riverFactor = riverMoistureBonus` (maximum
moisture contribution), and cells with lower accumulation have proportionally less.

**Contract between L and M:** Phase M detects `FlowAccumulation` via a field-existence
check (try-get pattern or `HasField`). If the field exists, its values are raw counts
≥1f on Land, 0f elsewhere. Phase M is responsible for its own normalization. No
additional interface or handoff struct is needed — the scalar field IS the interface.

---

## 14. Open Questions (Implementation Time)

| Question | Recommended | Notes |
|----------|-------------|-------|
| PriorityQueue tie-breaking | Composite key `(float height, int cellIndex)` | Guarantees deterministic dequeue order for equal heights |
| Sort stability for flow accumulation | `Array.Sort` with cell-index tiebreaker | Ensures deterministic traversal order |
| Temp buffer allocation | `Allocator.Temp` (matching Phase G pattern) | `filledHeight` and `flowDir` freed at stage exit |
| MapFieldId assignment | Assign at implementation time based on current COUNT | Phase M IDs may need updating if L is implemented first |
| Lake filtering default | 0 (disabled) | Conservative; enable after visual inspection |
| Rho8 vs D8 | D8 (deterministic) | Rho8 reduces grid artifacts but requires RNG, breaking the no-RNG invariant. Defer to refinement phase if grid artifacts are visible. |
| River connectivity to ocean | Verify in test, don't enforce | Rivers should reach ocean by construction (Priority-Flood guarantees drainage). Test validates this rather than enforcing it as a runtime constraint. |

---

## 15. Expected Files

**Runtime:**
- `Stage_Hydrology2D.cs` (new)
- `HeightFieldHydrologyOps2D.cs` (new operator)
- `MapIds2D.cs` (updated — `MapLayerId.Rivers = 12`, `MapLayerId.Lakes = 13`,
  `MapFieldId.FlowAccumulation`, COUNT updates)

**Tests:**
- `HeightFieldHydrologyOps2DTests.cs` (new — operator micro-tests)
- `StageHydrology2DTests.cs` (new — stage invariant and golden tests)
- `MapPipelineRunner2DGoldenLTests.cs` (new — pipeline golden)

**Sample:**
- `PCGMapVisualization` patched for Rivers/Lakes layer view and FlowAccumulation scalar
  field view
- `PCGMapCompositeVisualization` patched with Rivers (blue) and Lakes (teal/dark blue)
  color entries in compositing priority table

**No changes to:** `MapContext2D.cs`, `MapExporter2D.cs`, `TilemapAdapter2D.cs`,
`MaskFloodFillOps2D.cs`, `Stage_BaseTerrain2D.cs`, `Stage_Shore2D.cs`

---

## 16. What Phase L Does NOT Do

- **No erosion simulation.** Phase L does not modify the Height field. Hydraulic/thermal
  erosion (Pass 2a techniques) would carve channels into the terrain; Phase L only detects
  drainage patterns on the existing terrain. Erosion is a separate potential phase (not
  currently on the roadmap) that would run BEFORE Phase L.
- **No river width.** The Rivers mask is one cell wide. Width variation is adapter-side
  via FlowAccumulation values.
- **No river names.** River naming is an adapter-side concern (same pattern as M2.b region
  naming). A `RiverSegment` metadata table (river ID, source cell, mouth cell, total
  flow, Strahler order) is a potential Phase L2 extension.
- **No river carving into terrain.** Rivers do not lower the Height field along their
  path. Visual valley formation requires erosion simulation (not in scope).
- **No lake depth.** Lakes are a binary mask. Depth variation within a lake (for fishing,
  boat navigation, etc.) would require a separate lake depth field — out of scope.
- **No rainshadow.** Moisture from rainfall is deferred. Phase M's moisture is derived
  from coastal proximity + flow accumulation + noise. A wind-direction-aware rainfall
  model (as in mapgen4) is a potential Phase M refinement, not a Phase L concern.
- **No lake-river interaction.** Rivers flow on Land; lakes exist on NOT-Land. A river
  that reaches an inland depression terminates at the lake shore. River flow through or
  around lakes (inlet/outlet modeling) is not modeled — the Priority-Flood filling
  implicitly handles drainage through lake areas by raising their filled height.
- **No Strahler ordering.** River hierarchy (stream order 1, 2, 3...) is not computed.
  FlowAccumulation serves as a continuous proxy for river importance. Strahler ordering
  is a potential Phase L2 extension if discrete river hierarchy is needed.
