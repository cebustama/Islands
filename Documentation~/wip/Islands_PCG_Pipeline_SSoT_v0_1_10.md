# Islands.PCG — Contract-Focused Design Bible (v0.1.10)
**Status:** initial / minimal SSoT for the *new Islands.PCG pipeline*  
**Goal:** lock down *contracts* (data shapes, invariants, naming, layering) so we can scale from a reusable Fields/Grids toolkit → SDF primitives → dungeon strategies → adapters (Tilemap/Texture/Mesh) without drifting.

**Note (2026-01-29):** Roadmap and phase planning sections have been removed from this SSoT to keep it purely contract-focused. See the separate Phase E Planning Report (and the dedicated roadmap doc) for planning details.


**Update (v0.1.2):** Phase C4 complete — added **SDF compose raster ops** (`SdfComposeRasterOps`) and a lantern composite mode (`SdfCircleBoxCompositeMask`) to validate Union/Intersect/Subtract in **distance-space** before thresholding.

**Update (v0.1.3):** Phase C5 complete — added **fast mask boolean ops** directly on `MaskGrid2D` (`CopyFrom`, `Or`, `And`, `AndNot`) with **tail-bit determinism enforcement**, plus lantern modes (`MaskUnion`, `MaskIntersect`, `MaskSubtract`) and a deterministic smoke test (`MaskGrid2DBooleanOpsSmokeTest`).

**Update (v0.1.4):** Phase C6 complete — finalized the **lantern demo wiring** in `PCGMaskVisualization` so one scene can flip modes and visually verify: SDF primitives (`SdfCircleMask/SdfBoxMask/SdfCapsuleMask`), mask boolean ops (`MaskUnion/MaskIntersect/MaskSubtract`), and parity between **distance-composed** (`SdfCircleBoxCompositeMask` with `composeMode`) vs **mask-composed** results (at least union/intersect).

**Update (v0.1.5):** Phase D2 complete — added **SimpleRandomWalk2D** (single “drunk line” walk that carves directly into `MaskGrid2D` with optional `brushRadius` stamping via `MaskRasterOps2D.StampDisc`), plus a lantern mode (`SimpleRandomWalkMask`) in `PCGMaskVisualization` to validate the pure-grid carver deterministically (same seed ⇒ same path / same `CountOnes`).


**Update (v0.1.6):** Phase D3 complete — added **IteratedRandomWalk2D** (multi-walk, optional restart-on-floor, accumulating into `MaskGrid2D`), a new lantern mode (`IteratedRandomWalkMask`) in `PCGMaskVisualization`, and a **runtime snapshot hash** (`MaskGrid2D.SnapshotHash64`) plus `MaskGrid2D.TryGetRandomSetBit(...)` to support deterministic restart sampling and fast regression gates.

**Update (v0.1.8):** Phase D4.0 complete — locked the **Raster Shapes Contract** (endpoint-inclusive lines, disc-per-point brush semantics with forward-compatible pluggable brush surface, and clip/ignore out-of-bounds policy) to prevent corridor/room ports from drifting.
**Update (v0.1.9):** Phase D complete — D4 raster shapes (StampDisc/DrawLine + EditMode tests + lantern modes) and D5 rooms+corridors composition (RoomsCorridorsComposer2D runtime + lantern mode + determinism tests + golden hash gate).

**Update (v0.1.10):** Added the dedicated **PCGDungeonVisualization** lantern (strategy-focused visualizer) and the first Phase E port: **Corridor First** (`CorridorFirstDungeon2D`) plus supporting layout utilities (`LayoutSeedUtil`, `MaskNeighborOps2D`).


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



**Raster Shapes Contract (D4.0)** — All raster shape writers in `Islands.PCG` operate on `MaskGrid2D` deterministically and in a “safe operator” style.

- **Lines are endpoint-inclusive**: both `a` and `b` are always carved when in-bounds.
- **Brush meaning (current)**: `brushRadius` is a “disc stamp per line point” (each rasterized line point stamps a filled disc of radius `brushRadius`; `brushRadius == 0` stamps a single cell).
  - This is forward-compatible: the line *traversal* semantics stay fixed while the per-point “stamp” can later be swapped (disc/square/diamond/custom SDF-derived stamps).
- **Out-of-bounds behavior**: clip/ignore. Operations must never throw due to OOB coordinates; any writes outside the domain are skipped.

### D4.5 — Raster shapes completion gate (tests + lantern)
**D4 is considered complete when all of the following are true:**
1) `StampDisc` + `DrawLine` exist and are OOB-safe (clip/ignore writes, never-throw).
2) EditMode tests cover:
   - endpoint inclusion,
   - reversal invariance,
   - brush growth,
   - stable `SnapshotHash64` for identical inputs.
3) Lantern can display:
   - a disc,
   - a line (with brush radius),
   - and correctness is immediately visible (symmetry, continuity, thickness).

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

### 7.4 `RoomsCorridorsComposer2D` (Phase D5 “rooms + corridors”)
**Responsibility:** build a coherent “room + corridor” dungeon mask in *pure grids* by:
- stamping axis-aligned room rectangles into a `MaskGrid2D`, and
- connecting room centers with rasterized corridors.

**New types:**
- `RoomsCorridorsConfig` (struct): minimal viable configuration for room placement + corridor carving.
- `RoomsCorridorsComposer2D.Generate(...)`: the deterministic generator.

**Primary API (minimal slice):**
- `void Generate(ref MaskGrid2D mask, ref Random rng, in RoomsCorridorsConfig cfg, NativeArray<int2> outRoomCenters, out int placedRooms)`

**Algorithm (exact flow, minimal slice):**
1) **Optional clear:** if `cfg.clearBeforeGenerate`, call `mask.Clear()`.
2) **Room placement loop:**
   - For each desired room index `i`:
     - sample a `(w,h)` size in `[roomSizeMin..roomSizeMax]`.
     - sample a center `(cx,cy)` within domain margins (`roomPadding`), so the room can fit.
     - convert to inclusive rect bounds `(xMin,xMax,yMin,yMax)`.
     - optionally reject if `allowOverlap == false` and the candidate area isn’t empty.
     - stamp the room using `RectFillGenerator.FillRect(..., clampToDomain:true)`.
     - store the accepted center in `outRoomCenters[i]` and increment `placedRooms`.
   - Each room gets up to `cfg.placementAttemptsPerRoom` attempts.
3) **Corridor connection (first slice):**
   - For `i = 1..placedRooms-1`, connect `outRoomCenters[i-1] → outRoomCenters[i]` with:
     - `MaskRasterOps2D.DrawLine(ref mask, a, b, brushRadius: cfg.corridorBrushRadius, value:true)`

**Key contracts:**
- Deterministic: all sampling uses `Unity.Mathematics.Random` (`rng`) only.
- Tilemap-free: operates solely on `MaskGrid2D`.
- OOB-safe: room stamps clamp; corridor carving clips/ignores out-of-domain writes.
- Designed for extension: better connection strategies (MST, graph wiring), non-rect rooms (SDF + threshold), and post-processing (Phase F morphology) can layer on top.



### 7.5 `CorridorFirstDungeon2D` (Phase E1 — Corridor First, grid-only)
**Responsibility:** generate a dungeon mask by carving **corridors first** (via `MaskRasterOps2D.DrawLine`), then stamping **rooms** at selected corridor endpoints and (optionally) at **dead-ends**.

**Primary API (allocation-free contract):**
- `void Generate(ref MaskGrid2D mask, ref Random rng, in CorridorFirstConfig cfg, NativeArray<int2> scratchCorridorEndpoints, NativeArray<int2> outRoomCenters, out int placedRooms)`

**Key contracts:**
- **Grid-only:** writes only to `MaskGrid2D` (no Tilemaps/HashSets).
- **Deterministic:** consumes only `Unity.Mathematics.Random` (`rng`) and does not use `UnityEngine.Random`.
- **No allocations inside:** caller provides scratch arrays (`scratchCorridorEndpoints`, `outRoomCenters`).
- **OOB-safe:** carving clamps endpoints to a configurable border padding; stamping uses `RectFillGenerator.FillRect(..., clampToDomain:true)`.

**Config surface (`CorridorFirstConfig`) highlights:**
- Corridor carving: `corridorCount`, `corridorLengthMin/Max`, `corridorBrushRadius`, `borderPadding`, `clearBeforeGenerate`.
- Room stamping:
  - Endpoint selection: either `roomSpawnCount > 0` (seeded shuffle, take N) **or** `roomSpawnChance` (per-endpoint chance).
  - Room sizing: `roomSizeMin`, `roomSizeMax`.
  - Optional dead-end pass: `ensureRoomsAtDeadEnds`.

**Algorithm (exact flow, minimal slice):**
1) (Optional) clear the mask (`clearBeforeGenerate`).
2) Choose a start cell (default center, clamped to `[padding..res-1-padding]`) and stamp at least one ON cell.
3) For `corridorCount` segments:
   - pick a cardinal direction and a length in `[lenMin..lenMax]`,
   - compute a clamped target cell,
   - carve `current → target` via `MaskRasterOps2D.DrawLine` (with brush radius),
   - record the target as an endpoint.
4) Deduplicate endpoints in-place (small N; O(N²) is acceptable and deterministic).
5) Stamp rooms at endpoints (rect fill), using either:
   - **count mode** (`roomSpawnCount`): seeded shuffle endpoints then take N, or
   - **chance mode** (`roomSpawnChance`): per-endpoint roll.
6) (Optional) dead-end scan:
   - for every ON cell, check 4-neighborhood dead-end predicate via `MaskNeighborOps2D.IsDeadEnd4`,
   - if not near an existing room center, stamp a room rect at that location.

**Supporting layout utilities:**
- `LayoutSeedUtil.CreateRng(int seed)` standardizes “int seed → `Unity.Mathematics.Random`”, clamped to `>= 1`.
- `MaskNeighborOps2D` provides OOB-safe 4-neighborhood queries, treating OOB neighbors as OFF.


### 7.6 How D2/D3/D5 plug into the pipeline (Lantern path)
In the lantern (`PCGMaskVisualization`):

- D2 runs in `SourceMode.SimpleRandomWalkMask`.
- D3 runs in `SourceMode.IteratedRandomWalkMask`.
- D5 runs in `SourceMode.RoomsCorridorsMask`.

Each mode follows the same trusted loop:
1) ensure/allocate `MaskGrid2D` at the c
### 9.2 `PCGDungeonVisualization : Visualization`
**Responsibility:** a **strategy-focused** lantern for grid-based dungeon generation. Unlike `PCGMaskVisualization` (which covers a broad set of pipeline demos: SDF, boolean ops, raster shapes), this component is optimized for iterating on **Layout strategies**.

**Contract:**
- Generates into a persistent `MaskGrid2D` (no Tilemaps/HashSets).
- Uploads packed mask values into the GPU instancing `_Noise` buffer (float4 packing).
- Supports strategy switching + parameter dirty tracking to avoid unnecessary regeneration.
- Exposes a simple 0/1 palette (`_MaskOffColor`, `_MaskOnColor`) for readability.
- Suitable as a single “workbench” scene while porting/validating Phase D/E strategies.

**Currently supported strategies (as of v0.1.10):**
- Simple Random Walk (D2)
- Iterated Random Walk (D3)
- Rooms + Corridors (D5)
- Corridor First (E1)

**Notes / invariants:**
- Strategy RNG creation should use `LayoutSeedUtil.CreateRng(seed)` (seed clamped to `>= 1`) for consistency.
- When a strategy needs scratch memory, it must be provided by the caller (the visualization uses `NativeArray<int2>` with `Allocator.Temp` for per-update scratch, keeping core strategy allocation-free).


hosen resolution
2) clear mask (or respect the mode’s `clearBeforeDraw` / `clearBeforeGenerate`)
3) create `Unity.Mathematics.Random rng` from the mode’s seed
4) call the runtime generator (`Walk`, `Carve`, or `Generate`)
5) upload `MaskGrid2D` to GPU via the existing pack/upload path
6) (optional) log `CountOnes()` / `SnapshotHash64()` for quick sanity + gating

This is the “core PCG → debug view” adapter. The algorithms themselves stay Tilemap-free.

### 7.7 Determinism gates (what we test)
Minimum acceptance for Layout algorithms:
- Same `seed + config + domain` ⇒ same output `MaskGrid2D` snapshot hash (`SnapshotHash64`).
- Reversal invariance where applicable (e.g., line A→B equals B→A).
- Simple monotonic sanity checks where appropriate (e.g., increasing brush radius should increase filled area for the same endpoints/seed).

**Regression gates we rely on:**
- “Same inputs ⇒ same `SnapshotHash64`” tests for each algorithm (cheap determinism proof).
- Optional **golden hash** tests (lock one fixed config + seed to a constant hash) to detect accidental behavior changes immediately.
  - If you intentionally change the algorithm, you update the expected hash as part of that change.

EditMode tests validate determinism cheaply before any PlayMode/visual checks.

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
  - SDF compose → scalar→threshold→mask (composite SDF demo)
  - mask boolean ops in mask-space (`MaskUnion`, `MaskIntersect`, `MaskSubtract`)
- **Layout modes (Phase D):**
  - `SimpleRandomWalkMask` (D2)
  - `IteratedRandomWalkMask` (D3)
  - `RoomsCorridorsMask` (D5)
- **Raster shape debug (D4):**
  - `RasterDiscMask`
  - `RasterLineMask` (with `lineBrushRadius`)
- Must reuse the same allocation + pack/upload path across modes (no Tilemaps).
- Must keep packing rules explicit and deterministic.


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
This is the minimum set that defines the contract and gives us a working lantern + Phase D vertical slice.

**Core types**
- `GridDomain2D`
- `MaskGrid2D` (+ hashing partial)
- `ScalarField2D`

**Primitives + operators**
- `ScalarToMaskOps`
- `Sdf2D`
- `SdfComposeOps`
- `SdfToScalarOps`
- `SdfComposeRasterOps`
- `MaskRasterOps2D` (StampDisc + DrawLine)
- `RectFillGenerator`
- `CheckerFillGenerator`

**Layout (Phase D vertical slice)**
- `Direction2D`
- `SimpleRandomWalk2D`
- `IteratedRandomWalk2D`
- `RoomsCorridorsComposer2D`

**Lantern**
- `PCGMaskVisualization`

**Proof / tests (recommended)**
- `Direction2DTests`
- `SimpleRandomWalk2DTests`
- `IteratedRandomWalk2DTests`
- `DrawLineTests`
- `RoomsCorridorsComposer2DTests` (includes golden hash gate)
- `GoldenSnapshotHashTests` (shared “lock it in” pattern)
- `MaskGrid2DBooleanOpsSmokeTest`

**Optional (recommended while porting Phase E strategies)**
- `CorridorFirstDungeon2D`
- `LayoutSeedUtil`
- `MaskNeighborOps2D`
- `PCGDungeonVisualization` (strategy-focused workbench)

---

