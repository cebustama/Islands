# Islands.PCG — Contract-Focused Design Bible (v0.1.8)
**Status:** initial / minimal SSoT for the *new Islands.PCG pipeline*  
**Goal:** lock down *contracts* (data shapes, invariants, naming, layering) so we can scale from a reusable Fields/Grids toolkit → SDF primitives → dungeon strategies → adapters (Tilemap/Texture/Mesh) without drifting.

**Update (v0.1.2):** Phase C4 complete — added **SDF compose raster ops** (`SdfComposeRasterOps`) and a lantern composite mode (`SdfCircleBoxCompositeMask`) to validate Union/Intersect/Subtract in **distance-space** before thresholding.

**Update (v0.1.3):** Phase C5 complete — added **fast mask boolean ops** directly on `MaskGrid2D` (`CopyFrom`, `Or`, `And`, `AndNot`) with **tail-bit determinism enforcement**, plus lantern modes (`MaskUnion`, `MaskIntersect`, `MaskSubtract`) and a deterministic smoke test (`MaskGrid2DBooleanOpsSmokeTest`).

**Update (v0.1.4):** Phase C6 complete — finalized the **lantern demo wiring** in `PCGMaskVisualization` so one scene can flip modes and visually verify: SDF primitives (`SdfCircleMask/SdfBoxMask/SdfCapsuleMask`), mask boolean ops (`MaskUnion/MaskIntersect/MaskSubtract`), and parity between **distance-composed** (`SdfCircleBoxCompositeMask` with `composeMode`) vs **mask-composed** results (at least union/intersect).

**Update (v0.1.5):** Phase D2 complete — added **SimpleRandomWalk2D** (single “drunk line” walk that carves directly into `MaskGrid2D` with optional `brushRadius` stamping via `MaskRasterOps2D.StampDisc`), plus a lantern mode (`SimpleRandomWalkMask`) in `PCGMaskVisualization` to validate the pure-grid carver deterministically (same seed ⇒ same path / same `CountOnes`).


**Update (v0.1.6):** Phase D3 complete — added **IteratedRandomWalk2D** (multi-walk, optional restart-on-floor, accumulating into `MaskGrid2D`), a new lantern mode (`IteratedRandomWalkMask`) in `PCGMaskVisualization`, and a **runtime snapshot hash** (`MaskGrid2D.SnapshotHash64`) plus `MaskGrid2D.TryGetRandomSetBit(...)` to support deterministic restart sampling and fast regression gates.

**Update (v0.1.8):** Phase D4.0 complete — locked the **Raster Shapes Contract** (endpoint-inclusive lines, disc-per-point brush semantics with forward-compatible pluggable brush surface, and clip/ignore out-of-bounds policy) to prevent corridor/room ports from drifting.


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


**Primitives vs Operators vs Layout (quick mental model):**
- **Primitives**: functions like `float SdCircle(p)` — take numbers, return numbers (no grids, no RNG).
- **Operators**: deterministic transforms on *data containers* — e.g., rasterize SDF → `ScalarField2D`, threshold → `MaskGrid2D`, stamp a disc into a mask.
- **Layout**: seed-driven orchestration — choose where/when to apply operators to produce an overall structure (walks, rooms, corridors). Layout is where “dungeon strategies” live.

- `Islands.PCG.Layout`  
  Seed-driven procedural *strategies* that orchestrate primitives + operators (e.g., random walks, room placement, corridor carving). **Layout writes into grids/fields, but does not talk to Tilemaps/Textures/Meshes.**


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
- Provides `SnapshotHash64(...)` for fast deterministic fingerprints (runtime + tests).
- Provides `TryGetRandomSetBit(...)` to sample a uniformly random “ON” cell (used by Iterated Random Walk restart).

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


### 6.3 `MaskRasterOps2D` (mask writers / “brush stamps”) — **NEW in v0.1.5**
**Responsibility:** small, deterministic raster “stamps” that write directly into a `MaskGrid2D`.

**Methods:**
- `StampDisc(ref MaskGrid2D dst, int cx, int cy, int radius, bool value)`
- `DrawLine(ref MaskGrid2D dst, int2 a, int2 b, int brushRadius = 0, bool value = true)`

**Contract:**
- Allocation-free; deterministic.
- Caller controls bounds semantics by providing `(cx,cy)`; the stamp itself clips to the domain.
- Intended usage: brush carving in Layout algorithms (random walks, tunnels, room painting) where `brushRadius > 0`.



**Raster Shapes Contract (D4.0)** — All raster shape writers in `Islands.PCG` operate on `MaskGrid2D` deterministically and in a “safe operator” style. **Lines are endpoint-inclusive**: both `A` and `B` are always carved when in-bounds. The `DrawLine` thickness is controlled by a **brush** concept: for now, `brushRadius` means **“disc stamp per line point”** (i.e., each rasterized line point stamps a filled disc of radius `brushRadius` into the mask; `brushRadius == 0` stamps a single cell). This definition is **forward-compatible**: the brush will be treated as a pluggable stamping primitive in the future (e.g., disc, square, diamond, custom SDF-derived stamps) while preserving the same `DrawLine` traversal semantics; only the per-point “stamp” implementation changes. **Out-of-bounds behavior is clip/ignore**: operations must never throw due to OOB coordinates;

### D4.5 — Raster shapes completion gate (tests + lantern)
**D4 is considered complete when:**
1) `StampDisc` + `DrawLine` exist and are OOB-safe (clip/ignore writes).
2) EditMode tests cover endpoint inclusion, reversal invariance, brush growth, and stable `SnapshotHash64`.
3) Lantern can display a disc and a line (with brush) and correctness is immediately visible.
 any writes that fall outside the domain are simply skipped (clipped to the domain), ensuring robustness for procedural iteration and consistent determinism.
**Why this is in Operators (not Layout):**
- It’s a reusable *grid write primitive* (a deterministic transform on a grid), not a dungeon strategy.

### 6.4 `SdfComposeRasterOps` (compose while rasterizing) — **NEW in v0.1.2**
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

## 7) Layout (procedural strategies on pure grids)

**Definition:** “Layout” is where we start building *actual dungeon strategies* by combining:
- **core data** (`MaskGrid2D`, `ScalarField2D`)
- **primitives** (math-only SDFs and composition)
- **operators** (threshold, rasterization, boolean ops, brush stamps)
- **seed-driven decisions** (`Unity.Mathematics.Random`)

**Hard rule:** Layout never depends on Tilemaps/Textures/Mesh. Those are adapters.

### 7.1 `Direction2D`
**Responsibility:** deterministic direction selection utilities for layout algorithms.

**Current surface (Phase D1):**
- `Cardinal` directions in a fixed order.
- `PickCardinal(ref Random rng)`
- `PickSkewedCardinal(ref Random rng, float skewX, float skewY)`

**Skew semantics (contract):**
- `skewX ∈ [-1, +1]` biases horizontal sign: `-1` = always left, `+1` = always right, `0` = unbiased.
- `skewY ∈ [-1, +1]` biases vertical sign: `-1` = always down, `+1` = always up, `0` = unbiased.
- Axis selection is 50/50 (horizontal vs vertical), then sign selection applies skew.

> Note: this matches the legacy intent while staying seed-driven (`Unity.Mathematics.Random`), and is tested in EditMode.

### 7.2 `SimpleRandomWalk2D` (Phase D2 “carver”)
**Responsibility:** perform a single random walk and **carve** into a destination `MaskGrid2D` (0/1 floor mask).

**Primary method:**
- `int2 Walk(ref MaskGrid2D dst, ref Random rng, int2 start, int walkLength, int brushRadius, float skewX=0, float skewY=0, int maxRetries=8)`

**Parameters (what they mean):**
- `dst`: the floor mask we’re writing into (must be allocated by the caller).
- `rng`: seed-driven RNG (passed by `ref` so it advances deterministically).
- `start`: initial cell (must be in-bounds).
- `walkLength`: number of steps to attempt (may stop early if boxed-in).
- `brushRadius`:
  - `0` → carve exactly one cell per visit (`dst.SetUnchecked(x,y,true)`).
  - `>0` → carve a disc stamp per visit (`MaskRasterOps2D.StampDisc(..., value:true)`).
- `skewX/skewY`: directional bias (see `Direction2D`).
- `maxRetries`: bounce retries when an attempted step would go OOB.

**Algorithm (exact flow):**
1) Validate inputs (`walkLength >= 0`, `brushRadius >= 0`, `start` in-bounds).
2) `pos = start`; **carve** at `pos`.
3) For `i in 0..walkLength-1`:
   - Try up to `maxRetries` times:
     - pick `dir = Direction2D.PickSkewedCardinal(ref rng, skewX, skewY)`
     - `next = pos + dir`
     - if `next` in-bounds → `pos = next`; carve at `pos`; continue to next step
   - If no valid direction found after retries: **StopEarly** (break).
4) Return the final `pos`.

**Objective:** this is the smallest “drunk line” mask you can build, and it’s the building block for `IteratedRandomWalk2D` (the full strategy in D3).


### 7.3 `IteratedRandomWalk2D` (Phase D3 full strategy)
**Responsibility:** replicate the legacy dungeon concept: perform **multiple** random walks, optionally restarting from an existing floor cell, accumulating floor into a single `MaskGrid2D`.

**Primary method:**
- `int2 Carve(ref MaskGrid2D dst, ref Random rng, int2 start, int iterations, int walkLengthMin, int walkLengthMax, int brushRadius, float randomStartChance, float skewX=0, float skewY=0, int maxRetries=8)`

**Parameters (what they mean):**
- `iterations`: number of individual walks to run (accumulating into `dst`).
- `walkLengthMin/walkLengthMax`: per-walk length range (inclusive max sampling).
- `randomStartChance`: for `i>0`, probability to restart from a uniformly random ON cell in `dst` (if any exist).
- `start`: for `i==0`, the initial start cell (no restart roll on the first iteration).
- `brushRadius/skewX/skewY/maxRetries`: forwarded to `SimpleRandomWalk2D.Walk(...)`.

**Parity rule (non-negotiable):**
When `iterations=1` AND `walkLengthMin==walkLengthMax` AND `randomStartChance==0`, the implementation must avoid consuming extra RNG so the direction sequence matches `SimpleRandomWalk2D` for the same seed/config.

**Algorithm (exact flow):**
1) Validate inputs and clamp where appropriate (min/max, non-negative, chance in [0..1]).
2) For each iteration `i`:
   - Choose `walkLength`:
     - if `min==max`: fixed length (no RNG consumed)
     - else: `rng.NextInt(min, max+1)`
   - Choose `iterStart`:
     - if `i==0`: `start`
     - else if `randomStartChance>0` and `dst` has any ON cell and `rng.NextFloat() < randomStartChance`:
       - `dst.TryGetRandomSetBit(ref rng, out iterStart)`
     - else:
       - previous walk end
   - Run `SimpleRandomWalk2D.Walk(...)` accumulating into `dst`.
3) Return final end position.

### 7.4 How D2/D3 plug into the pipeline (Lantern path)
In the lantern (`PCGMaskVisualization`), D2 runs in `SourceMode.SimpleRandomWalkMask` and D3 runs in `SourceMode.IteratedRandomWalkMask`:

- ensure/allocate `MaskGrid2D` at the chosen resolution
- clear mask (or respect `clearBeforeDraw`)
- create `Unity.Mathematics.Random rng` from `walkSeed`
- call `SimpleRandomWalk2D.Walk(...)`
- upload `MaskGrid2D` to GPU via the existing pack/upload path
- log `CountOnes()` for sanity

This is the “core PCG → debug view” adapter. The algorithm itself stays Tilemap-free.

### 7.5 Determinism gates (what we test)
Minimum acceptance for Layout algorithms:
- Same `seed + config + domain` ⇒ same output mask.
- Simple monotonic sanity checks (e.g., longer `walkLength` should not reduce `CountOnes` with the same seed/start).

EditMode tests can validate determinism cheaply before any PlayMode/visual checks.

## 8) Generators (tiny writers / sanity tests)
These are **not** “the framework”; they are minimal deterministic producers used to validate contracts:

- `RectFillGenerator` writes a filled rect into `MaskGrid2D`.
- `CheckerFillGenerator` writes checkerboard into `MaskGrid2D` (great for mapping validation).

---

## 9) Lantern / Debug visualization (current vertical slice)

### 9.1 `PCGMaskVisualization : Visualization`
**Responsibility:** a debug “lantern” that proves the end-to-end loop:

`Generate → Store in grid → Pack → Upload → GPU instance display`

**Contract:**
- Must be able to visualize:
  - direct masks (`RectMask`, `CheckerMask`)
  - scalar→threshold→mask (`ThresholdedScalar`)
  - SDF→scalar→threshold→mask (`SdfCircleMask`, `SdfBoxMask`, `SdfCapsuleMask`)
  - **Mask boolean ops (C5)** in mask-space: `MaskUnion`, `MaskIntersect`, `MaskSubtract` (built from two thresholded masks).
- **Random walk layouts (Phase D)**: `SimpleRandomWalkMask` (D2) and `IteratedRandomWalkMask` (D3), driven by seed + walk params and rendered via the same pack/upload path.
- **Raster shape debug (Phase D4)**: `RasterDiscMask` and `RasterLineMask` for validating disc symmetry, diagonal continuity, and brush thickness.
- **SDF compose** → scalar→threshold→mask (`SdfCircleBoxCompositeMask`)
- **C6 demo wiring:** inspector-exposed parameters for circle/box/capsule + `composeMode` must update outputs without per-frame allocations (reuse `EnsureMaskAllocated/EnsureScalarAllocated`).
- Must not require Tilemaps.
- Must keep data packing rules explicit (float4 packing for `_Noise`).

---

## 10) Roadmap (phases)
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
- ✅ Random Walk layouts: D2 (single walk) + D3 (iterated strategy) complete; next is room/corridor raster shapes (D4).
- ✅ **D4.0 Raster Shapes Contract locked** (inclusive endpoints, disc-per-point brush with future pluggable brushes, OOB clip/ignore).
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

## 11) Proposed module / package structure (namespaces + assemblies)
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

## 12) Minimal “first file set” (the smallest stable surface)
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
12) `Direction2D`
13) `MaskRasterOps2D`
14) `SimpleRandomWalk2D`

**Proof / test (recommended):**
15) `MaskGrid2DBooleanOpsSmokeTest`
16) `Direction2DTests`
17) `SimpleRandomWalk2DTests` (Phase D2)
18) `DrawLineTests` (Phase D4)

---

## 13) Immediate next step deliverables (very small, high ROI)
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

**Deliverable E — SimpleRandomWalk2D (Phase D2) — DONE**
- Implemented: `Direction2D` (D1), `MaskRasterOps2D.StampDisc`, `SimpleRandomWalk2D.Walk(...)`
- Implemented: lantern wiring mode `SourceMode.SimpleRandomWalkMask` in `PCGMaskVisualization`.
- Acceptance:
  - visible continuous-ish trail starting at `start` (lantern)
  - `MaskGrid2D.CountOnes()` increases as `walkLength` grows (sanity)
  - same seed ⇒ same trail (determinism)

**Deliverable F — IteratedRandomWalk2D (Phase D3) — DONE**
- Implemented: `IteratedRandomWalk2D.Carve(ref MaskGrid2D dst, ref Random rng, ...)` (multi-walk + restart-on-floor + accumulate)
- Adds:
  - multiple walks (`iterations`)
  - restart policy (`randomStartChance` sampling uniformly among existing ON cells via `MaskGrid2D.TryGetRandomSetBit`)
  - per-iteration `walkLength` randomization (`walkLengthMin..walkLengthMax`)
  - runtime fingerprinting (`MaskGrid2D.SnapshotHash64`) to support fast regression gates
- Acceptance:
  - `iterations=1` + fixed length + `randomStartChance=0` matches D2 behavior (parity)
  - increasing `iterations` increases density/spread
  - same seed + config ⇒ identical output (prefer `SnapshotHash64` gate; `CountOnes` as sanity)

## Appendix — “How the current vertical slice works” (one sentence)
- *Primitives* compute distances (`Sdf2D`) → *RasterOps* write those distances into a scalar field (`SdfToScalarOps`) → *Threshold* turns distances into a filled mask (`ScalarToMaskOps`) → *Lantern* packs and uploads for GPU visualization (`PCGMaskVisualization`). (Current SDF shapes: circle/box/capsule.)


---

## 14) Phase D4 deliverable summary (new)

**Deliverable G — Raster shapes (Phase D4) — DONE**
- Implemented `MaskRasterOps2D.DrawLine(...)` using Bresenham (all octants), endpoint-inclusive.
- Brush semantics: `brushRadius` = disc stamp per line point (via `StampDisc`), forward-compatible.
- EditMode tests: endpoint inclusion, reversal invariance (hash), axis count sanity, brush growth.
- Lantern modes: `RasterDiscMask` + `RasterLineMask` in `PCGMaskVisualization` for rapid visual QA.
