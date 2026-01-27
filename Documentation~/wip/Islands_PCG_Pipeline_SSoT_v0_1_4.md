# Islands.PCG — Contract-Focused Design Bible (v0.1.4)
**Status:** initial / minimal SSoT for the *new Islands.PCG pipeline*  
**Goal:** lock down *contracts* (data shapes, invariants, naming, layering) so we can scale from a reusable Fields/Grids toolkit → SDF primitives → dungeon strategies → adapters (Tilemap/Texture/Mesh) without drifting.

**Update (v0.1.2):** Phase C4 complete — added **SDF compose raster ops** (`SdfComposeRasterOps`) and a lantern composite mode (`SdfCircleBoxCompositeMask`) to validate Union/Intersect/Subtract in **distance-space** before thresholding.

**Update (v0.1.3):** Phase C5 complete — added **fast mask boolean ops** directly on `MaskGrid2D` (`CopyFrom`, `Or`, `And`, `AndNot`) with **tail-bit determinism enforcement**, plus lantern modes (`MaskUnion`, `MaskIntersect`, `MaskSubtract`) and a deterministic smoke test (`MaskGrid2DBooleanOpsSmokeTest`).

**Update (v0.1.4):** Phase C6 complete — finalized the **lantern demo wiring** in `PCGMaskVisualization` so one scene can flip modes and visually verify: SDF primitives (`SdfCircleMask/SdfBoxMask/SdfCapsuleMask`), mask boolean ops (`MaskUnion/MaskIntersect/MaskSubtract`), and parity between **distance-composed** (`SdfCircleBoxCompositeMask` with `composeMode`) vs **mask-composed** results (at least union/intersect).


---

## 0) Scope (what this document governs)
This SSoT covers the **new** data-oriented PCG pipeline (parallel-safe), built around:
- **Grid data** (bitmasks + scalar fields + later vector fields)
- **Deterministic operators** (threshold, boolean ops, SDF rasterization, etc.)
- **One debug/lantern path** to visualize results without requiring Tilemaps

It does **not** attempt to document the legacy tilemap pipeline (that’s in `PCG_DungeonPipeline_SSoT.md`).

---

## 1) Non‑negotiables (style contract)
1) **Determinism**
   - Same inputs + same seed ⇒ same outputs.
   - No `UnityEngine.Random` in core runtime code (seeded RNG only, later prefer Islands hash / `Unity.Mathematics.Random`).

2) **Data‑oriented core**
   - Core logic operates on **dense grids** (`NativeArray<float>`, bitsets, etc.), not Tilemaps/HashSets.
   - Core types are `struct` where it helps Burst + allocation‑free workflows.

3) **Adapters are outputs**
   - Tilemap/Texture/Mesh are *views* / *adapters*, never required by core generation logic.

4) **Incremental parity**
   - All new implementations must be comparable to baseline outputs via snapshots (bitsets/bytes) and simple deterministic tests.

---

## 2) Layering (who is allowed to depend on what)
**Rule of thumb:** “math → grid → ops → layout → adapters/samples”.

- `Islands.PCG.Core`  
  Pure indexing + domains (no allocations beyond what the structs already do).

- `Islands.PCG.Grids` / `Islands.PCG.Fields`  
  Data containers that own native memory.

- `Islands.PCG.Primitives`  
  Pure math primitives (SDF functions, composition). No grid knowledge.

- `Islands.PCG.Operators`  
  Deterministic transforms between grids/fields (threshold, SDF rasterization, boolean ops).

- `Islands.PCG.Layout` *(later)*  
  Higher-level procedural algorithms (room placement, corridor carving, region partitioning).

- `Islands.PCG.Adapters` *(later)*  
  Tilemap/Texture/Mesh exporters.

- `Islands.PCG.Samples`  
  “Lantern” visualizations and small demo scenes.

---

## 3) Core data types (contracts & invariants)

### 3.1 `GridDomain2D`
**Responsibility:** defines a discrete 2D grid domain (W×H) and indexing rules.  
**Contract:**
- `Index(x,y)` is **row-major**: `x + y * Width`.
- `InBounds(x,y)` defines validity.
- Domain is immutable (treat as value type).

### 3.2 `MaskGrid2D`
**Responsibility:** compact 0/1 occupancy grid (1 bit per cell).  
**Storage:** `NativeArray<ulong>` bitset.  
**Contract:**
- Owns native memory → **must `Dispose()`**.
- Must keep tail bits deterministic (bits beyond `Domain.Length` forced to 0).
- `Get/Set` are bounds-checked; `GetUnchecked/SetUnchecked` assume caller guarantees bounds.
- Intended for “floor/walls/solids/regions” masks and for snapshot testing (`CountOnes`).

### 3.3 `ScalarField2D`
**Responsibility:** dense scalar field (1 float per cell).  
**Storage:** `NativeArray<float>` values.  
**Contract:**
- Owns native memory → **must `Dispose()`**.
- Mutable struct: avoid copies; pass by `ref`/`in`.
- Used for heightmaps, density fields, **SDF distance fields**, etc.

---

## 4) Coordinate conventions (very important)
To avoid drift, we commit to **one** convention:

1) **Grid space is “cell coordinates”**
   - Cells are addressed by integer `(x,y)` in `[0..Width)×[0..Height)`.

2) **Sampling point is the cell center**
   - Continuous sample point `p` is `(x + 0.5, y + 0.5)` in grid units.

3) **SDF convention**
   - Signed distance is **negative inside**, `0` on boundary, positive outside.

4) **Normalized parameters in samples**
   - Samples may expose “center01” / “radius01” in `[0..1]` for inspector UX,
     but must convert to **grid units** before calling core ops.

---

## 5) Primitives (pure math)

### 5.1 `Sdf2D`
**Responsibility:** pure SDF math functions (circle/box/segment/capsule).  
**Contract:**
- Allocation-free and deterministic.
- Follows signed-distance convention (negative inside).

### 5.2 `SdfComposeOps`
**Responsibility:** composition operators over *distances* (`float dA`, `float dB`).  
**Contract:**
- Hard ops:
  - Union: `min(dA, dB)`
  - Intersect: `max(dA, dB)`
  - Subtract: `max(dA, -dB)`
- Smooth ops (optional) must be deterministic, no randomness.

---

## 6) Operators (grid/field transforms)

### 6.1 `SdfToScalarOps` (aka “RasterOps”)
**Responsibility:** *rasterize* an SDF primitive into a `ScalarField2D` by sampling every cell center.

**Methods (write into an existing field):**
- `WriteCircleSdf(ref ScalarField2D dst, float2 center, float radius)`
- `WriteBoxSdf(ref ScalarField2D dst, float2 center, float2 halfExtents)`
- `WriteCapsuleSdf(ref ScalarField2D dst, float2 a, float2 b, float radius)`

**Parameter convention:** inputs are in **grid units** (cell space). Sample points are cell centers: `(x + 0.5, y + 0.5)`.

**What “RasterOps” means here (simple):**
- “Rasterize” = loop over `(x,y)` cells → compute a float value per pixel/cell → write it into a field.
- It’s the bridge from “continuous math” (SDF) → “discrete grid data” (ScalarField2D).

**Contract:**
- Writes into an **existing** field (no allocation inside).
- Deterministic: the same primitive params produce the same field.
- Sampling uses the cell-center convention.

### 6.2 `ScalarToMaskOps`
**Responsibility:** convert `ScalarField2D` → `MaskGrid2D` by thresholding.  
**Contract:**
- Domains must match (same width/height).
- Threshold modes are explicit:
  - `Greater`, `GreaterEqual`, `Less`, `LessEqual`.
- SDF fill convention typically uses:
  - `threshold = 0`
  - `mode = LessEqual` (inside = distance <= 0).


### 6.3 `SdfComposeRasterOps` (compose while rasterizing) — **NEW in v0.1.2**
**Responsibility:** rasterize **two** SDF primitives into a *single* `ScalarField2D` by composing their distances per cell.

**Why this exists:**
- Mask boolean ops (0/1) are great later, but composing **distances first** is higher quality and unlocks smooth blends.
- This is the smallest “field-level modeling” step needed before we start porting dungeon strategies.

**Minimal API (contract):**
- `enum SdfCombineMode { Union, Intersect, Subtract }`
- `WriteCircleBoxCompositeSdf(ref ScalarField2D dst, float2 circleCenter, float circleRadius, float2 boxCenter, float2 boxHalfExtents, SdfCombineMode mode, bool invertDistance = false)`

**Parameter convention:** inputs are in **grid units** (cell space). Sample points are cell centers: `(x + 0.5, y + 0.5)`.

**Composition rules (distance-space):**
- Union: `min(dA, dB)`
- Intersect: `max(dA, dB)`
- Subtract (A without B): `max(dA, -dB)`

**Contract:**
- Writes into an **existing** field (no allocation inside).
- Deterministic: same params ⇒ same field.
- If `invertDistance` is true, it negates the composed distance (swaps inside/outside semantics at threshold time).

---

## 7) Generators (tiny writers / sanity tests)
These are **not** “the framework”; they are minimal deterministic producers used to validate contracts:

- `RectFillGenerator` writes a filled rect into `MaskGrid2D`.
- `CheckerFillGenerator` writes checkerboard into `MaskGrid2D` (great for mapping validation).

---

## 8) Lantern / Debug visualization (current vertical slice)

### 8.1 `PCGMaskVisualization : Visualization`
**Responsibility:** a debug “lantern” that proves the end-to-end loop:

`Generate → Store in grid → Pack → Upload → GPU instance display`

**Contract:**
- Must be able to visualize:
  - direct masks (`RectMask`, `CheckerMask`)
  - scalar→threshold→mask (`ThresholdedScalar`)
  - SDF→scalar→threshold→mask (`SdfCircleMask`, `SdfBoxMask`, `SdfCapsuleMask`)
  - **Mask boolean ops (C5)** in mask-space: `MaskUnion`, `MaskIntersect`, `MaskSubtract` (built from two thresholded masks).
- **SDF compose** → scalar→threshold→mask (`SdfCircleBoxCompositeMask`)
- **C6 demo wiring:** inspector-exposed parameters for circle/box/capsule + `composeMode` must update outputs without per-frame allocations (reuse `EnsureMaskAllocated/EnsureScalarAllocated`).
- Must not require Tilemaps.
- Must keep data packing rules explicit (float4 packing for `_Noise`).

---

## 9) Roadmap (phases)
This roadmap starts with the reusable Fields/Grids toolkit, then ports dungeon strategies.

### Phase 0 — Baseline parity harness (legacy side)
- Seed all randomness (eliminate `Guid.NewGuid()` shuffles, random tile variants, etc.).
- Export snapshots (floor bitset + optional wall neighbor masks).
- Add tests: same seed ⇒ same snapshot; later compare baseline vs Islands.PCG.

### Phase 1 — Fields/Grids core (current)
- ✅ `GridDomain2D`, `MaskGrid2D`, `ScalarField2D`
- ✅ `ScalarToMaskOps.Threshold`
- ✅ Lantern path (`PCGMaskVisualization` + pack/upload)
- (next in Phase 1): `VectorGrid2D` + a few scalar ops (min/max/add/mul) if needed.

### Phase 2 — Primitives + boolean ops (dungeon building blocks)
- ✅ SDF primitives (`Sdf2D`) + composition (`SdfComposeOps`)
- ✅ Rasterization bridge (`SdfToScalarOps`)
- ✅ **SDF compose raster ops** (`SdfComposeRasterOps`) + lantern composite mode (`SdfCircleBoxCompositeMask`)
- ✅ Lantern SDF modes (`SdfCircleMask`, `SdfBoxMask`, `SdfCapsuleMask`) via `PCGMaskVisualization`
- ✅ **C6 lantern demo wiring**: one scene can flip modes to validate primitives + mask boolean ops + distance-vs-mask composition parity.
- ✅ **Mask boolean ops (C5)**: implemented as word-wise in-place ops on `MaskGrid2D` (`CopyFrom/Or/And/AndNot`) with tail-bit clearing.
  - Validation: `MaskGrid2DBooleanOpsSmokeTest` + lantern modes (`MaskUnion/MaskIntersect/MaskSubtract`).
- NEXT:
  - Optional: add a *functional* operator façade (e.g., `MaskBooleanOps.Union(in a, in b, ref dst)`) if you want non-mutating workflows.
  - (Optional later) generalized multi-shape SDF composition + SmoothUnion controls.

### Phase 3 — Layout (port dungeon strategies, one by one)
Each strategy becomes a composition of:
- primitive fields/masks
- operators
- deterministic seeded decisions
Examples:
- Room-first (rooms as SDF blobs + deterministic corridors)
- Corridor-first
- BSP / partitioning
- Voronoi/Poisson seeded layouts

### Phase 4 — Extract + adapters
- Mask/field → Texture2D (debug + art)
- Mask → Tilemap (adapter only)
- Mask/field → Mesh (marching squares)
- Optional: neighbor wall bitmasks + LUT

### Phase 5 — Burst/SIMD upgrades
- Convert hot loops into Burst jobs.
- Add 4-wide packed enumeration (align with Islands’ SIMD patterns) where it pays off.
- Determinism test harness becomes mandatory gating.

---

## 10) Proposed module / package structure (namespaces + assemblies)
**Runtime assemblies**
- `Islands.PCG.Runtime`
  - References: `Unity.Collections`, `Unity.Mathematics`, `Unity.Burst`, `Islands.Runtime` (Visualization base)

**Editor assemblies**
- `Islands.PCG.Editor` (adapters tooling, editors, test UI)

**Tests**
- `Islands.PCG.Tests` (PlayMode + EditMode as needed)

**Namespaces**
- `Islands.PCG.Core`
- `Islands.PCG.Grids`
- `Islands.PCG.Fields`
- `Islands.PCG.Primitives`
- `Islands.PCG.Operators`
- `Islands.PCG.Layout`
- `Islands.PCG.Adapters`
- `Islands.PCG.Samples`

---

## 11) Minimal “first file set” (the smallest stable surface)
This is the minimum set that defines the contract and gives us a working lantern:

1) `GridDomain2D`
2) `MaskGrid2D`
3) `ScalarField2D`
4) `ScalarToMaskOps`
5) `Sdf2D`
6) `SdfComposeOps`
7) `SdfToScalarOps`
8) `SdfComposeRasterOps`
9) `RectFillGenerator`
10) `CheckerFillGenerator`
11) `PCGMaskVisualization`

**Proof / test (recommended):**
12) `MaskGrid2DBooleanOpsSmokeTest`

---

## 12) Immediate next step deliverables (very small, high ROI)
**Deliverable A — Mask boolean ops (bitset-fast) — DONE (C5)**
- Implemented directly on `MaskGrid2D` as in-place, word-wise methods:
  - `CopyFrom(in other)`
  - `Or(in other)` (union)
  - `And(in other)` (intersection)
  - `AndNot(in other)` (subtract: A without B)
- Contract: same domain required; deterministic; no allocations inside; tail-bit clearing enforced after ops.
- Proof: `MaskGrid2DBooleanOpsSmokeTest` + lantern modes (`MaskUnion/MaskIntersect/MaskSubtract`).

**Deliverable B — Compose multiple SDFs directly into a scalar field**
- New: `SdfRasterComposeOps2D.cs` (optional)
- Goal: sample 2+ primitives + compose (Union/Subtract/SmoothUnion) per cell into one scalar field.
- This is the cleanest bridge toward “room blobs + corridors” without committing to a dungeon strategy yet.

**Deliverable C — Lantern mode for composed SDFs (**DONE as C4**)**
- Implemented: `SdfComposeRasterOps.cs` in `Islands.PCG.Operators`
- Implemented: `PCGMaskVisualization` mode `SourceMode.SdfCircleBoxCompositeMask`
- Validates Union/Intersect/Subtract by composing **distances** per cell, then thresholding `<= 0`.

**Deliverable D — Lantern final demo wiring (**DONE as C6**)
- Implemented: `PCGMaskVisualization` supports SDF primitive mask modes + mask boolean modes + distance-composed composite mode.
- Acceptance: in a single scene, flip modes and visually verify primitives, boolean ops, and parity between `SdfCircleBoxCompositeMask` (Union/Intersect) and `MaskUnion/MaskIntersect`.

---

## Appendix — “How the current vertical slice works” (one sentence)
- *Primitives* compute distances (`Sdf2D`) → *RasterOps* write those distances into a scalar field (`SdfToScalarOps`) → *Threshold* turns distances into a filled mask (`ScalarToMaskOps`) → *Lantern* packs and uploads for GPU visualization (`PCGMaskVisualization`). (Current SDF shapes: circle/box/capsule.)
