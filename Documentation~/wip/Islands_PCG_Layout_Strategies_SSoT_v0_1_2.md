# Islands.PCG — Layout Strategies SSoT (v0.1.2)

Date: 2026-01-29  
Related spine doc: `Islands_PCG_Pipeline_SSoT_v0_1_10.md`  
Legacy reference: `PCG_DungeonPipeline_SSoT.md` (legacy behaviors to port)

---

## 0) Purpose and scope

This document is the **single source of truth for strategy-level behavior** in Islands.PCG: how each **layout strategy** works internally, what parameters mean, how information flows, and what test/visual gates define “done”.

**Non-goals:**
- Duplicating the engine spine (grid primitives, operators, adapters, GPU upload path). Those remain authoritative in the **Pipeline SSoT**.
- Documenting every low-level operator (e.g., raster line rules). Those live in the Pipeline SSoT “Operators / Primitives” sections.

**What *is* covered here:**
- Strategy intent and constraints
- Public API surface and config structs
- Parameter semantics + sanitization rules
- Algorithm flow (step-by-step)
- Safety rules (OOB-safe, scratch array requirements)
- Observability and outputs
- Determinism contract notes
- Test gates (Lantern + SnapshotHash64 golden)

---

## 1) How this doc relates to the Pipeline SSoT

**Pipeline SSoT = the spine**
- Defines shared contracts (grid-only, determinism, adapters-only outputs)
- Defines shared data structures (`MaskGrid2D`, hashing)
- Defines shared operators (DrawLine, StampDisc, RectFill, etc.)
- Defines phase acceptance rules (Lantern + golden hash gates)

**This doc = deep per-strategy internals**
- Explains *why* a parameter exists, what it changes, and how it’s used in the algorithm
- Provides the *flow of information* inside the strategy
- Records legacy parity notes and intentional deltas
- Lists test presets and what each gate proves

Recommended linking approach:
- Pipeline SSoT contains a **Strategies Index** with 1–2 line summaries and links to sections here.
- This doc links back to the Pipeline SSoT only for shared contracts and operator semantics.

---

## 2) Global contracts shared by all strategies

### 2.1 Determinism contract
All strategies must be deterministic under:
- the same **seed**
- the same **resolution / domain**
- the same **parameter set**

Rules:
- RNG = **`Unity.Mathematics.Random`** only.
- RNG is passed **by ref** into generation functions so state advances deterministically.
- **No** GUID-based shuffles, no `UnityEngine.Random`, no time-based seeds.

Shared utility:
- `LayoutSeedUtil.CreateRng(int seed)` clamps `seed >= 1` and returns deterministic RNG.

### 2.2 Data-oriented + grid-only contract
- Strategy writes to **`MaskGrid2D`** (and uses `NativeArray` for scratch/output).
- **No Tilemaps / HashSets** inside strategies.
- Tilemaps/Textures/Mesh are adapters only.

### 2.3 OOB-safety contract
- Strategies must not throw due to out-of-bounds writes during carving/stamping.
- Preferred patterns:
  - Clamp endpoints/targets to domain before raster ops.
  - Use raster ops that clip or clamp internally.
  - For neighborhood queries, treat OOB as OFF.

Shared utility:
- `MaskNeighborOps2D` provides OOB-safe neighbor queries. OOB neighbors count as OFF.

### 2.4 Snapshot hash contract (test gate)
- Every strategy must have:
  1) Lantern visualization mode
  2) EditMode test that computes **SnapshotHash64**
  3) Golden hash gate test(s)

Hashing:
- `MaskGrid2D.Hash.cs` defines stable hashing over the mask snapshot (see Pipeline SSoT for exact contract).

---

## 3) Strategy section template (used below)

Each strategy section uses:

1) **ID + Version**
2) **Intent**
3) **Public API**
4) **Parameters** (meaning + constraints + sanitization)
5) **Determinism notes**
6) **Algorithm flow**
7) **Safety rules**
8) **Outputs / observability**
9) **Test gates**
10) **Legacy parity notes**
11) **Known gotchas**
12) **Extension hooks**

---

# STRAT-D2 — Simple Random Walk (v0.1)

## Intent
Carve an organic, cave-like mask by doing a single biased random walk from a start cell.

Use when:
- you want very cheap “blob/cave” floors
- you want a baseline that is obviously deterministic + OOB-safe

Not for:
- structured rooms or explicit corridor networks (use D5/E1/E2/E3)

## Public API
File: `SimpleRandomWalk2D.cs`

```csharp
public static int2 Walk(
    ref MaskGrid2D dst,
    ref Random rng,
    int2 start,
    int walkLength,
    int brushRadius,
    float skewX = 0f,
    float skewY = 0f,
    int maxRetries = 8);
```

Returns: final position (last carved cell).

## Parameters
- `dst`: allocated mask to write into.
- `rng`: seed-driven RNG (passed by ref).
- `start`: starting cell (caller should clamp; algorithm assumes it can be carved).
- `walkLength` (**>= 0**): number of steps attempted.
- `brushRadius` (**>= 0**):
  - `0`: carve single cells (`SetUnchecked`)
  - `>0`: carve a disc per step (`MaskRasterOps2D.StampDisc`)
- `skewX / skewY`: directional bias for movement:
  - `0`: unbiased
  - positive biases right/up, negative biases left/down
  - implemented via `Direction2D.PickSkewedCardinal`
- `maxRetries` (**>= 1**): bounce retries per step if a move would go OOB.

**Sanitization / guards (runtime):**
- throws if `walkLength < 0`, `brushRadius < 0`, `maxRetries < 1`.

## Determinism notes
- All stochasticity comes from `rng`.
- Direction picking uses deterministic float comparisons (`<=`) to match legacy semantics.

## Algorithm flow
1) Initialize `pos = start`.
2) Carve at `pos` (using brush radius).
3) For each step in `0..walkLength-1`:
   - Try up to `maxRetries`:
     - pick skewed cardinal dir
     - compute `next = pos + dir`
     - if `next` is in bounds: `pos = next`, carve, continue
   - If no in-bounds move found: **StopEarly** (end walk)

## Safety rules
- OOB writes are prevented by the retry loop; only in-bounds positions are carved.
- Carving uses `SetUnchecked` / `StampDisc` but only after bounds checks.

## Outputs / observability
- Writes floor (ON) cells into `dst`.
- Returns final `pos`.

## Test gates
- Lantern mode: visualize mask and confirm carving responds to parameters.
- SnapshotHash64 golden:
  - fixed `seed`, `resolution`, `start`, `walkLength`, `brushRadius`, `skewX/Y`, `maxRetries`.

## Legacy parity notes
- This is a deterministic grid-based carve replacing older tilemap implementations.

## Known gotchas
- Very small domains + high `walkLength` with strong skew can StopEarly quickly.
- `brushRadius > 0` will fill space quickly; tune `walkLength` accordingly.

## Extension hooks
- Replace brush from disc to custom stamp shapes later (without changing the walk logic).
- Add a “turn bias” memory (last direction influences next) if needed.

---

# STRAT-D3 — Iterated Random Walk (v0.1)

## Intent
Carve a more varied cave mask by performing **multiple** random walks (“iterations”), optionally restarting from existing floor cells.

This typically produces denser, more connected organic regions than D2.

## Public API
File: `IteratedRandomWalk2D.cs`

```csharp
public static int2 Carve(
    ref MaskGrid2D dst,
    ref Random rng,
    int2 start,
    int iterations,
    int walkLengthMin,
    int walkLengthMax,
    int brushRadius,
    float randomStartChance = 0f,
    float skewX = 0f,
    float skewY = 0f,
    int maxRetries = 8);
```

Returns: final position after the last iteration.

## Parameters
- `iterations` (**> 0**): number of walk iterations.
- `walkLengthMin/Max` (**>= 0**, inclusive max):
  - each iteration length is sampled in `[min..max]`
- `brushRadius`: same semantics as D2.
- `randomStartChance` (**0..1**):
  - chance each iteration restarts from a random existing floor cell (if any)
  - otherwise restarts from a deterministic baseline policy (see implementation)
- `skewX/skewY`, `maxRetries`: same semantics as D2.

**Sanitization / guards (runtime):**
- if `iterations <= 0` → deterministic no-op: returns `start`
- throws if any length is negative, or `brushRadius < 0`, `maxRetries < 1`
- `walkLengthMax` is clamped to be `>= walkLengthMin`

## Determinism notes
- RNG is advanced deterministically.
- “Restart from floor” selection must be deterministic; the implementation uses seeded sampling.

## Algorithm flow (conceptual)
For each iteration:
1) Decide starting position:
   - with probability `randomStartChance`, attempt to pick a previously carved floor cell
   - otherwise use the baseline start policy (deterministic)
2) Sample `L ~ UniformInt([walkLengthMin..walkLengthMax])`
3) Perform a D2-like walk of length `L` using skew + bounce retry
4) Accumulate carving into the same `dst` mask

## Safety rules
- Same OOB protection as D2 (retry loop).
- Carving only occurs at in-bounds positions.

## Outputs / observability
- Writes ON cells into `dst`.
- Returns final carved position.

## Test gates
- Lantern: verify iteration count and randomStartChance visually change connectivity.
- SnapshotHash64 golden:
  - fixed resolution + seed + config; hash must remain stable.

## Legacy parity notes
- Intended as the deterministic grid-based analog of iterated/cave walkers in legacy pipelines.

## Known gotchas
- If mask has very few cells ON and randomStartChance is high, “restart from floor” may frequently fall back to baseline start.
- High iterations * high brush radius can quickly fill most of the domain.

## Extension hooks
- Add weighted restarts: prefer frontier cells or dead-ends for more branching.
- Add “carve probability” per step to introduce speckling (still deterministic).

---

# STRAT-D5 — Rooms + Corridors Composition (v0.1)

## Intent
Generate a classic “rooms connected by corridors” dungeon:
1) Place axis-aligned rectangular rooms
2) Connect room centers in placement order using raster corridors (DrawLine)

This is a minimal, deterministic baseline for “structured dungeons”.

## Public API
File: `RoomsCorridorsComposer2D.cs`

Config:
```csharp
public struct RoomsCorridorsConfig
{
    public int roomCount;
    public int2 roomSizeMin;
    public int2 roomSizeMax;
    public int placementAttemptsPerRoom;
    public int roomPadding;
    public int corridorBrushRadius;
    public bool clearBeforeGenerate;
    public bool allowOverlap;
}
```

Generator:
```csharp
public static void Generate(
    ref MaskGrid2D mask,
    ref Unity.Mathematics.Random rng,
    in RoomsCorridorsConfig cfg,
    NativeArray<int2> outRoomCenters,
    out int placedRooms);
```

## Parameters
### Room placement
- `roomCount` (**>= 0**): rooms attempted.
- `roomSizeMin/Max`: inclusive room width/height sampling ranges.
- `placementAttemptsPerRoom` (**>= 1**): how many random samples to try for each room.
- `roomPadding` (**>= 0**): margin to keep rooms away from borders.

### Corridor carving
- `corridorBrushRadius` (**>= 0**): thickness of corridor raster lines.

### Mask policy
- `clearBeforeGenerate`: if true, clears mask first.
- `allowOverlap`:
  - true: stamp rooms even if overlaps existing ON cells
  - false: perform a cheap “AnySetInRect” scan; only stamp if area is empty

### Scratch / outputs
- `outRoomCenters` **must be allocated** and `Length >= cfg.roomCount`.
- Only indices `[0..placedRooms-1]` are valid outputs.

**Sanitization / guards (runtime):**
- throws if mask not allocated
- throws if outRoomCenters not allocated
- throws if outRoomCenters.Length < cfg.roomCount
- throws if roomCount < 0 or corridorBrushRadius < 0

## Determinism notes
- All sampling (room sizes, placements, corridor connections) uses the provided RNG by ref.

## Algorithm flow (exact steps)
1) Optional: `mask.Clear()` if `clearBeforeGenerate`
2) Place rooms:
   - for each room attempt:
     - sample width/height in inclusive range
     - sample top-left `(xMin,yMin)` within domain, respecting padding and fit
     - if `allowOverlap == false`, scan rect for any existing ON; if present, retry
     - if accepted: stamp room via rect fill; compute center and store in `outRoomCenters`
3) Connect rooms:
   - for `i = 1..placedRooms-1`:
     - draw corridor from `center[i-1]` to `center[i]` using `MaskRasterOps2D.DrawLine` with brush radius

## Safety rules
- Room stamping uses rect fill with domain-safe bounds.
- Corridors use raster ops; endpoints are in-bounds by construction.

## Outputs / observability
- `mask` contains rooms + corridors.
- `placedRooms` tells how many rooms actually landed.
- `outRoomCenters` provides room centers for external debugging/analytics.

## Test gates
- Lantern: verify room count and sizes respond to parameters; corridors connect in sequence.
- SnapshotHash64 golden:
  - fixed resolution + seed + config preset.

## Legacy parity notes
- Matches common “rooms + corridors” pipeline behavior.
- Connection topology is intentionally simple (placement order). Future variants can use MST/graph wiring.

## Known gotchas
- With `allowOverlap=false` and small domains or large room sizes, placement may yield low `placedRooms`.
- `roomPadding` reduces available placement area; can exacerbate placement failures.

## Extension hooks
- Replace connection policy with MST over centers or Delaunay triangulation.
- Add corridor L-shapes, doors, or wall post-processing (Phase F).

---

# STRAT-E1 — Corridor First (v0.1)

## Intent
Generate a dungeon by carving corridors first, then stamping rooms:
- corridors create a navigable backbone
- rooms attach to corridor endpoints (and optionally dead ends)

This strategy is a strong foundation for later topology/morphology passes.

## Public API
File: `CorridorFirstDungeon2D.cs`

Config:
```csharp
public struct CorridorFirstConfig
{
    public int corridorCount;
    public int corridorLengthMin, corridorLengthMax;
    public int corridorBrushRadius;

    public int roomSpawnCount;     // >0 => take N endpoints after seeded shuffle
    public float roomSpawnChance;  // <=0 => per-endpoint chance
    public int2 roomSizeMin, roomSizeMax;

    public int borderPadding;

    public bool clearBeforeGenerate;
    public bool ensureRoomsAtDeadEnds;
}
```

Generator:
```csharp
public static void Generate(
    ref MaskGrid2D mask,
    ref Random rng,
    in CorridorFirstConfig cfg,
    NativeArray<int2> scratchCorridorEndpoints,
    NativeArray<int2> outRoomCenters,
    out int placedRooms);
```

## Parameters
### Corridor carving
- `corridorCount` (**>= 0**): number of corridor segments.
- `corridorLengthMin/Max` (**>= 1**, inclusive):
  - segment length sampled in `[min..max]`
- `corridorBrushRadius` (**>= 0**): thickness for `DrawLine`.

### Endpoint-based rooms
- `roomSpawnCount`:
  - if `> 0`: take exactly N unique endpoints after **seeded Fisher–Yates shuffle**
  - if `<= 0`: evaluate `roomSpawnChance` per endpoint
- `roomSpawnChance` (**0..1**): used only when `roomSpawnCount <= 0`
- `roomSizeMin/Max`: inclusive room width/height sampling ranges (clamped to >=1)

### Border policy
- `borderPadding` (**>= 0**): prevents corridors from targeting the outer border band.
  - corridor targets are clamped to `[border..(res-1-border)]`

### Mask policy
- `clearBeforeGenerate`: if true, clears mask first.
- `ensureRoomsAtDeadEnds`: if true, performs a dead-end scan pass and stamps rooms at dead ends.

### Scratch / outputs
- `scratchCorridorEndpoints` must be **allocated and non-empty**.
  - recommended capacity: `corridorCount + 1` (start + each segment end)
- `outRoomCenters` must be **allocated and non-empty**.
  - capacity controls best-effort recording of centers (stamping still occurs even if capacity is exceeded)
- `placedRooms`: number of centers actually written (bounded by `outRoomCenters.Length`)

**Sanitization / guards (runtime):**
- throws if scratchCorridorEndpoints is not created or empty
- throws if outRoomCenters is not created or empty
- config values are clamped to safe ranges (count >= 0, lengths >= 1, brush >= 0, sizes >= 1, border >= 0)

## Determinism notes
- Endpoint selection uses seeded **Fisher–Yates shuffle** (`ShufflePrefix`) — no GUID randomness.
- All sampling (dir, length, room size) comes from `rng`.

## Algorithm flow (exact steps)
1) Read domain width/height; early return if empty.
2) Sanitize config values deterministically.
3) Optional: `mask.Clear()` if `clearBeforeGenerate`.
4) Compute clamped interior bounds based on `borderPadding`.
5) Choose start position (center-ish), clamped to interior bounds.
6) Carve a seed cell at start (StampDisc radius 0).
7) For each corridor segment:
   - pick random cardinal dir from a fixed set
   - sample length in `[lenMin..lenMax]`
   - compute `target = current + dir*length`
   - clamp `target` to interior bounds
   - carve corridor using `MaskRasterOps2D.DrawLine(current,target,brushRadius)`
   - record `target` in scratch endpoints (if capacity)
   - set `current = target`
8) Deduplicate endpoints in-place (`DeduplicateInPlace`, O(N²) — fine for small N)
9) Stamp endpoint rooms:
   - if `roomSpawnCount > 0`: shuffle endpoints prefix deterministically, take first N
   - else: for each endpoint, stamp room with probability `roomSpawnChance`
   - room stamping uses `RectFillGenerator.FillRect(..., clampToDomain:true)`
10) Optional dead-end pass:
   - scan the whole grid for dead ends using `MaskNeighborOps2D.IsDeadEnd4`
   - if a dead-end is far from existing room centers, stamp a room there
   - record center if capacity

## Safety rules
- Corridor targets are clamped before raster ops.
- Room stamping clamps to domain.
- Dead-end checks are OOB-safe (OOB treated as OFF).

## Outputs / observability
- `mask` contains corridors + rooms.
- `placedRooms` counts rooms recorded in `outRoomCenters`.
- `scratchCorridorEndpoints` contains endpoints (deduped prefix used for room stamping).
- Deterministic helper functions:
  - `ShufflePrefix` (Fisher–Yates)
  - `DeduplicateInPlace`

## Test gates
- Lantern: verify corridor network, endpoint room placement, and dead-end pass.
- SnapshotHash64 golden:
  - fixed resolution + seed + config preset
  - recommended presets:
    - endpoint rooms only (dead-end off)
    - endpoint rooms + dead-end pass

## Legacy parity notes
- Designed to match the legacy corridor-first “shape” while enforcing determinism via seeded shuffle.
- Intentional delta: explicit border clamping + explicit dead-end policy (documented above).

## Known gotchas
- If `borderPadding` is too large relative to resolution, all targets clamp to a tiny interior region.
- If `roomSpawnCount` exceeds unique endpoints, it clamps by uniqueCount/capacity.
- Dead-end scan can stamp many rooms in dense corridor mazes if room separation radius is too small.

## Extension hooks
- Replace corridor direction selection with “don’t immediately backtrack” rules for cleaner networks.
- Replace endpoint selection with weighted endpoints (longer corridors, branching points).
- Layer Phase F morphology (walls/bitmasks) after mask generation.

---

# STRAT-E2 — Room First (BSP) (v0.1)

**Status:** implemented (Phase E2.2)  
Files:
- `BspPartition2D.cs` (pure layout utility)
- `RoomFirstBspDungeon2D.cs` (grid-only dungeon strategy)

## In simple terms (mental model)
1) Start with the whole map as one big rectangle.  
2) Split it into smaller rectangles (BSP “leaves”) by repeatedly cutting one rectangle into two.  
3) Inside each leaf, carve a room (in the current minimal slice, the room is simply the leaf shrunk inward by padding).  
4) Connect room centers with corridors to ensure the dungeon is navigable.  

Result: a dungeon that feels “structured” (rooms with corridors) without needing any tilemaps.

---

## Component: `BspPartition2D` (pure layout)

### Intent
Create a deterministic set of **leaf rectangles** by BSP-splitting a root rectangle. This stage is **layout-only**: it produces rectangles, it does not touch `MaskGrid2D`.

### Public API
File: `BspPartition2D.cs`

Rectangle type (half-open / [min, max) convention):
```csharp
public struct IntRect2D : IEquatable<IntRect2D>
{
    public int xMin, yMin, xMax, yMax;  // x in [xMin,xMax), y in [yMin,yMax)
    public int Width { get; }
    public int Height { get; }
    public bool IsValid { get; }
    public int2 Center { get; }
}
```

Partition config:
```csharp
public struct BspPartitionConfig
{
    public int splitIterations;
    public int2 minLeafSize;
}
```

Helpers:
```csharp
public static int MaxLeavesUpperBound(int splitIterations);
public static int PartitionLeaves(in IntRect2D root, ref Random rng, in BspPartitionConfig cfg, NativeArray<IntRect2D> outLeaves);
```

### Parameters (meaning)
- `splitIterations` (**>= 0**): how many split *attempts* to run. Worst-case leaves = `2^splitIterations`.
- `minLeafSize` (**>= 1, >= 1**): a split only succeeds if **both children** remain at least this size.

### Algorithm flow (simple but exact)
For `i in 0..splitIterations-1`:
1) Pick a random existing leaf index.
2) Try to split that leaf:
   - Prefer splitting along the **long axis** (using a ~1.25 aspect ratio threshold).
   - Otherwise, choose orientation randomly (still deterministic via RNG).
   - Pick a split coordinate so both children stay >= `minLeafSize`.
3) If a split succeeds, replace the leaf with child A and append child B.
4) If no splittable leaf can be found this iteration, stop early.

### Safety rules / capacity
`PartitionLeaves` does **not** guard against `outLeaves` overflow when appending.

**Hard requirement:**
- `outLeaves.Length >= MaxLeavesUpperBound(cfg.splitIterations)`  
  (i.e., big enough for the worst-case `2^splitIterations` leaves).

---

## Strategy: `RoomFirstBspDungeon2D` (grid-only)

### Intent
Turn BSP leaf rectangles into a dungeon:
1) BSP partitions the domain into leaf rects (layout)
2) Stamp a room in each leaf (carve)
3) Connect room centers with corridors (carve)

This is intentionally a **minimal stable slice**:
- rooms are currently the entire leaf (shrunk by padding), not a random smaller room within the leaf
- center connections are in **leaf order**, not yet “BSP sibling” wiring

### Public API
File: `RoomFirstBspDungeon2D.cs`

Config:
```csharp
public struct RoomFirstBspConfig
{
    public int splitIterations;
    public int2 minLeafSize;
    public int roomPadding;
    public int corridorBrushRadius;
    public bool connectWithManhattan;
    public bool clearBeforeGenerate;
}
```

Generator:
```csharp
public static void Generate(
    ref MaskGrid2D mask,
    ref Random rng,
    in RoomFirstBspConfig cfg,
    NativeArray<BspPartition2D.IntRect2D> scratchLeaves,
    NativeArray<int2> outRoomCenters,
    out int leafCount,
    out int placedRooms);
```

### Parameters
#### BSP layout
- `splitIterations`: forwarded to `BspPartition2D`.
- `minLeafSize`: forwarded to `BspPartition2D`.

#### Room stamping
- `roomPadding` (**>= 0**): shrinks each leaf inward before stamping (prevents rooms touching borders of their leaf).

#### Corridor carving
- `corridorBrushRadius` (**>= 0**): thickness of corridors.
- `connectWithManhattan`:
  - `true`: connect centers with an L-shape (two segments) using a deterministic random choice of “x-first” vs “y-first”
  - `false`: connect centers with a single `DrawLine`

#### Mask policy
- `clearBeforeGenerate`: if true, clears the mask first.

### Determinism notes
- BSP splitting and corridor elbow orientation use only `rng` (seed-driven).
- No GUID shuffles; no nondeterministic ordering.

### Algorithm flow (exact steps)
1) Validate mask domain; validate `scratchLeaves` and `outRoomCenters` are created and non-empty.
2) Sanitize config:
   - `splitIterations = max(0, splitIterations)`
   - `minLeafSize = max(1,1)`
   - `roomPadding = max(0, roomPadding)`
   - `corridorBrushRadius = max(0, corridorBrushRadius)`
3) If `clearBeforeGenerate`, clear mask.
4) Build a root rect covering the full domain using `[min,max)` convention.
5) Call `BspPartition2D.PartitionLeaves(root, ref rng, partCfg, scratchLeaves)` → returns `leafCount`.
6) For each leaf in `scratchLeaves[0..leafCount-1]` (bounded by `outRoomCenters.Length`):
   - `room = Shrink(leaf, roomPadding)`
   - if valid: stamp via `RectFillGenerator.FillRect(..., clampToDomain:true)`
   - write `room.Center` into `outRoomCenters`
7) `placedRooms = number of written centers`
8) If `placedRooms > 1`, connect centers in order:
   - if `connectWithManhattan`: pick elbow as `(cur.x, prev.y)` or `(prev.x, cur.y)` (deterministic RNG)
   - draw segment(s) with `MaskRasterOps2D.DrawLine(..., brushRadius)`

### Safety rules
- Room stamping clamps to domain (`clampToDomain:true`).
- Corridor carving uses raster ops; centers are in-bounds by construction.
- The only major safety requirement is scratch capacity (see below).

### Scratch / outputs (important)
- `scratchLeaves` is a **hard capacity requirement**:
  - must be `Length >= BspPartition2D.MaxLeavesUpperBound(splitIterations)`
- `outRoomCenters` is both an output and a capacity limiter:
  - generation stops stamping additional rooms when `outRoomCenters` fills
  - recommended `outRoomCenters.Length >= leafCount` for full-room coverage

### Outputs / observability
- `leafCount`: number of BSP leaves produced.
- `placedRooms`: number of rooms stamped / centers written (bounded by `outRoomCenters.Length` and validity after padding).
- `mask`: rooms + corridors carved into the grid.

### Test gates (required)
- Lantern: verify:
  - increasing `splitIterations` increases leaf count (until constrained by minLeafSize)
  - padding shrinks rooms
  - Manhattan toggle changes corridor shape
- SnapshotHash64 golden:
  - at least one preset with `connectWithManhattan=false`
  - at least one preset with `connectWithManhattan=true`

### Legacy parity notes
- This is a deliberate “minimal slice” port:
  - rooms are not randomized within leaves yet
  - connections are in leaf order, not BSP-sibling connections
These are acceptable deltas for Phase E as long as determinism + gates hold.

### Known gotchas
- If `scratchLeaves` is too small, BSP partitioning can overflow the array.
- If `roomPadding` is too large, shrunk rooms become invalid (room skipped).
- Leaf-order corridors can look “snaky” compared to classic BSP sibling connections.

### Extension hooks (next improvements)
- Stamp a smaller random room **inside** each leaf (still deterministic).
- Wire corridors using BSP tree adjacency (connect sibling leaves) rather than leaf list order.
- Add door placement, pruning, and Phase F wall/morphology post-process.


---

# STRAT-E3 — Room Grid (layout-only minimal slice) (v0.1)

**Status:** implemented (Phase E3)  
File: `RoomGridDungeon2D.cs`

## Intent
Provide a **minimal**, deterministic room layout driven by a coarse grid + “path” selection:
- choose `roomCount` room centers on a coarse grid using a **seed-driven grid-walk**
- stamp a room at each center (`RectFillGenerator.FillRect`, clamped)
- connect rooms sequentially with corridors (`DrawLine` or Manhattan L)
- remain **grid-only**, **OOB-safe**, and **test-gated** (SnapshotHash64 + golden hash)

## Public API
File: `RoomGridDungeon2D.cs`

Config:
```csharp
public struct RoomGridConfig
{
    public int roomCount;
    public int cellSize;

    public int2 roomSizeMin, roomSizeMax;

    public int corridorBrushRadius;
    public int borderPadding;

    public bool connectWithManhattan;
    public bool clearBeforeGenerate;
}
```

Entry point:
```csharp
public static void Generate(
    ref MaskGrid2D mask,
    ref Random rng,
    in RoomGridConfig cfg,
    NativeArray<int> scratchPickedNodeIndices,
    NativeArray<int2> outRoomCenters,
    out int placedRooms);
```

## Parameters

### Coarse grid / placement
- `roomCount` (**>= 0**): desired number of rooms to place (actual may be lower if the coarse grid has fewer nodes or arrays truncate).
- `cellSize` (**>= 1**): spacing between coarse grid nodes (in mask cells).
- `borderPadding` (**>= 0**): shrinks the interior band used to build the coarse grid.
  - If padding collapses the interior, the strategy falls back to using the full domain.

### Room stamping
- `roomSizeMin/Max`: inclusive sampling ranges for room width/height (clamped to >= 1).
- Rooms are stamped as rectangles via:
  - `RectFillGenerator.FillRect(..., clampToDomain: true)`

### Corridor carving
- `connectWithManhattan`:
  - `true`: connect consecutive room centers with a Manhattan “L” (two `DrawLine` calls).
  - `false`: connect consecutive room centers with a direct `DrawLine`.
- `corridorBrushRadius` (**>= 0**): brush radius for corridor carving (0 = 1-cell wide).

### Mask policy
- `clearBeforeGenerate`: if true, clears the mask before generating.

### Scratch / outputs
- `scratchPickedNodeIndices`:
  - stores the indices of coarse-grid nodes selected (unique selection constraint).
  - hard requirement: must be created & non-empty; additionally `Length >= take` where `take` is the number of rooms actually attempted.
- `outRoomCenters`:
  - hard requirement: must be created & non-empty.
  - stores the placed room centers (`int2`) for `placedRooms` entries.
- `placedRooms`:
  - number of centers written (and rooms stamped).

## Determinism notes
- Uses only `Unity.Mathematics.Random` (seed-driven).
- Node selection is deterministic given the RNG stream:
  - choose a random start node
  - attempt to step to an unused neighbor first (path-like)
  - if blocked, fall back to sampling any unused node
- Fixed attempt counts are constants:
  - neighbor attempts: **12**
  - global fallback attempts: **64**
- Room sizes are sampled deterministically from RNG (inclusive min/max).

## Algorithm flow (exact steps)
1) Sanitize config (clamp counts, sizes, padding).
2) Clear mask if `clearBeforeGenerate`.
3) Compute interior bounds from `borderPadding` (best-effort; fall back if collapsed).
4) Define coarse-grid origin at `interiorMin + cellSize/2`, clamped.
5) Compute coarse-grid dimensions `nx, ny`, nodeCount = `nx * ny`.
6) If `nodeCount <= 0`: stamp one room at domain center, write 1 center, return.
7) Compute `take = min(roomCount, nodeCount, outRoomCenters.Length)`.
8) Pick `startIndex = rng.NextInt(0, nodeCount)`. Stamp the first room at that node center.
9) For each subsequent room `i = 1..take-1`:
   - Try up to 12 times to move to a random cardinal neighbor node that is in-bounds and unused.
   - If none found, try up to 64 times to sample a random unused node anywhere.
   - If still none found, stop early.
   - Stamp a room at the node center.
   - Carve corridor to previous center:
     - Manhattan L if enabled, else direct line.
   - Record center to `outRoomCenters`.
10) Output `placedRooms`.

## Safety rules
- All room stamping uses `FillRect(... clampToDomain: true)` (OOB-safe).
- All corridor carving uses `MaskRasterOps2D.DrawLine` (OOB-safe).
- Interior computations are clamped; padding collapse is handled via a fallback.
- Strategy may place fewer rooms than requested if:
  - coarse grid is small
  - scratch/output arrays are smaller than `roomCount`
  - selection saturates (rare unless `take` ≈ nodeCount)

## Outputs / observability
- Primary output: updated `MaskGrid2D`.
- Observability:
  - `placedRooms` returned
  - `outRoomCenters[0..placedRooms)` contains chosen centers (useful for Lantern logs/debug)

## Test gates
- EditMode determinism:
  - same seed/config/resolution ⇒ same `SnapshotHash64()`
- Golden hash gate:
  - lock expected hash after Lantern visual verification
- (Optional sanity gate) `mask.CountOnes() > 0` for the golden config.

## Legacy parity notes
- This is a **minimal slice** intended to provide a deterministic “grid-ish” room layout.
- It does not attempt to reproduce any legacy Tilemap-driven behavior exactly; the contract is grid-only + deterministic.

## Known gotchas
- Large `cellSize` can reduce nodeCount, placing fewer rooms than expected.
- Very large `borderPadding` can collapse the interior band; the strategy falls back to full-domain placement.
- If `outRoomCenters.Length < roomCount`, placement is truncated deterministically to the available capacity.

## Extension hooks
- Neighbor policy: extend to 8-neighborhood or weighted directions.
- Connection policy: MST / nearest-neighbor graph instead of sequential.
- Add a “snake path” option to traverse the coarse grid deterministically without neighbor sampling.

---

## Appendix A — Scratch array capacity rules (summary)

- **D2/D3:** no scratch arrays required.
- **D5 Rooms+Corridors:**
  - `outRoomCenters.Length >= cfg.roomCount` (hard requirement; throws otherwise)
- **E1 CorridorFirst:**
  - `scratchCorridorEndpoints` must be created & non-empty (throws)
    - recommended `Length >= corridorCount + 1`
  - `outRoomCenters` must be created & non-empty (throws)
    - capacity controls how many centers get recorded
- **E2 Room First (BSP):**
  - `scratchLeaves.Length >= BspPartition2D.MaxLeavesUpperBound(cfg.splitIterations)` (hard requirement; otherwise overflow)
  - recommended `outRoomCenters.Length >= leafCount` to avoid truncating room stamping
- **E3 Room Grid:**
  - `scratchPickedNodeIndices` must be created & non-empty (throws)
    - required `Length >= take`, where `take = min(cfg.roomCount, nodeCount, outRoomCenters.Length)`
    - recommended `Length >= cfg.roomCount` for typical use
  - `outRoomCenters` must be created & non-empty (throws)
    - capacity truncates how many centers are recorded/connected


---

## Appendix B — Where to store this doc

If Islands is a Unity Package (UPM), prefer:

- `Packages/com.yourname.islands/Documentation~/Islands_PCG_Layout_Strategies_SSoT.md`

Otherwise:

- `Docs/PCG/Islands_PCG_Layout_Strategies_SSoT.md`

The Pipeline SSoT should link to this doc as the “deep strategy reference”.
