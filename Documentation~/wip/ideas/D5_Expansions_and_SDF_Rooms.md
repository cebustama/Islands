# D5 Expansions Plan — Rooms + Corridors Composer (General, Concrete)

This document elaborates the proposed expansions for `RoomsCorridorsComposer2D` in terms of **specific class/method changes (still general)**, and explains in detail how to use **SDF** for room construction using different shapes.

---

## 1) Expansion Rundown (Concrete Changes, Still General)

### A) Multiple Room Shapes (Rect / Circle / Capsule / Composite)
**Goal:** keep the composer stable, but allow room stamping to vary by shape.

**Changes to `RoomsCorridorsComposer2D`:**
- Extend config:
  - Add `RoomShapeMode roomShapeMode;` (e.g. `Rect`, `Circle`, `Capsule`, `Composite`)
  - Add minimal per-shape knobs:
    - Circle: `float roomRadiusMin`, `float roomRadiusMax` *(or derive from size range)*
    - Capsule: reuse `roomSizeMin/Max` as (length/width) or add `float capsuleRadiusMin/Max`
    - Composite: `SdfCombineMode compositeMode` + secondary primitive params
- Add a small “placed room descriptor”:
  - `struct RoomDesc { int2 center; int4 bounds; RoomShapeMode shape; /* optional */ }`
- Add/adjust helpers:
  - `TryPlaceRoom(...)` → samples candidate params + position and calls the right stamping path
  - `StampRoomRect(...)` (calls `RectFillGenerator.FillRect`)
  - `StampRoomCircleFast(...)` (calls `MaskRasterOps2D.StampDisc`)
  - `StampRoomSdf(...)` (SDF→Scalar→Threshold→Mask merge; see section 2)

**Interactions:**
- Composer decides *where* and *what room parameters*.
- Stamping tools write into `MaskGrid2D` (directly or via scratch masks).

---

### B) SDF Stamping Path for Rooms (Reusing Existing Toolbox)
**Goal:** allow rooms to be “any SDF blob” and participate in boolean composition.

**Add scratch buffers (allocate once and reuse):**
- `ScalarField2D roomSdfScratch;`
- `MaskGrid2D roomMaskScratch;`
- `EnsureScratchAllocated(in GridDomain2D domain)` method in the composer (or a new shared ops class)

**New helper methods (either private in composer or moved to a `RoomStampOps2D`):**
- `WriteRoomSdf(in RoomSdfParams p, ref ScalarField2D dstSdf)`
  - calls one of:
    - `SdfToScalarOps.WriteCircleSdf(...)`
    - `SdfToScalarOps.WriteBoxSdf(...)`
    - `SdfToScalarOps.WriteCapsuleSdf(...)`
    - `SdfComposeRasterOps.WriteCircleBoxCompositeSdf(...)` for recipes
- `ThresholdSdfToMask(in ScalarField2D sdf, ref MaskGrid2D dstMask, float threshold = 0f)`
  - calls `ScalarToMaskOps.Threshold(..., threshold, LessEqual)`
- `MergeMaskOr(ref MaskGrid2D main, in MaskGrid2D add)`
  - simplest: loop OR per cell (later: word-wise OR for speed)

**Why this is useful:**
- Complex room shapes become “cheap” (just different SDF raster ops).
- Collision checks become “mask intersection” (accurate) instead of bounding-rect scan.

---

### C) Better “No-Overlap” Placement (Real Collision Rules)
**Current:** `allowOverlap` toggles a rectangle scan.

**Upgrade options (minimal → stronger):**
1) **Rect-only**: inflate bounds by `separationPadding` and scan.
   - Add `int separationPadding;` to config
   - Add `IsRectAreaClear(...)` helper
2) **SDF rooms**: after building `roomMaskScratch`, detect overlap by intersection:
   - Add helper `AnyOverlap(in MaskGrid2D a, in MaskGrid2D b)` or `CountOverlapWords(...)`

**Interactions:**
- `TryPlaceRoom` stamps into scratch, checks overlap, and only merges into main on success.

---

### D) Corridor Routing Upgrades (Still Grid-Only)
**Current:** connect in placement order with `DrawLine`.

**Config additions:**
- `ConnectionMode { InOrder, NearestNeighborChain, MST }`
- Optional: `int corridorBrushRadius` already exists; keep as primary thickness control.

**New helpers:**
- `BuildConnections(centers, mode, ref rng, out edges)`:
  - `InOrder`: (i-1 → i)
  - `NearestNeighborChain`: greedily connect to nearest unconnected
  - `MST`: minimum spanning tree (stable tie-breaks for determinism)
- Corridor carving:
  - Default: `MaskRasterOps2D.DrawLine(...)`
  - Optional “SDF corridor”: `SdfToScalarOps.WriteCapsuleSdf(...)` + threshold + merge

---

### E) Room “Recipes” via SDF Composition (Union / Intersect / Subtract / Smooth)
**Goal:** huge variety from small building blocks.

**Config additions:**
- `bool useCompositeRooms;`
- `SdfCombineMode compositeMode;` (Union/Intersect/Subtract)
- Optional later: `float smoothK;` for smooth unions, etc.

**Implementation:**
- Use `SdfComposeOps` to combine two or more primitive distances.
- If you already have `WriteCircleBoxCompositeSdf(...)`, you can start with that:
  - `Intersect(circle, box)` → “rounded box”
  - `Union(circle, box)` → “blob room”
  - `Subtract(box, circle)` → “courtyard/hole” room
- Then threshold and merge like any SDF room.

---

### F) Post-Process Hook (Future Phase) — Morphology + Walls
This is conceptually downstream of D5:
- Morphology: `Dilate/Erode/Open/Close` on `MaskGrid2D`
- Walls: `ComputeWallMask` (neighbor bitmask) + LUT to tile IDs

**Why it’s separate from D5:**
- D5 is “compose a coherent layout mask.”
- Morphology/Walls are “engine-grade post-process” *after* layout exists.

---

## 2) Detailed: How to Use SDF for Room Construction (Different Shapes)

### 2.1 Core SDF → Room Mask Pipeline (Every Time)
1) **Pick room parameters in grid units**
   - Center: `float2 center` (often derived from integer cell coords)
   - Size: radii / half-extents / endpoints
2) **Rasterize the signed distance into a `ScalarField2D`**
   - Sample at cell centers: `p = (x + 0.5f, y + 0.5f)`
   - Store **signed distance**: negative = inside, positive = outside
3) **Convert the SDF to a filled mask by thresholding**
   - Use threshold `0`: inside if `distance <= 0`
   - `ScalarToMaskOps.Threshold(sdf, mask, threshold: 0f, mode: LessEqual)`
4) **Merge into main dungeon mask**
   - Add rooms: OR into `mainMask`
   - Carve holes/courtyards: ANDNOT or subtract merge (later)

This pipeline is deterministic as long as:
- You use `Unity.Mathematics.Random` for parameters
- Raster ops use consistent sampling (cell centers)
- Threshold rules are fixed (e.g., `<= 0`)

---

### 2.2 Shape Examples

#### A) Circle room
- Raster: `SdfToScalarOps.WriteCircleSdf(ref sdf, center, radius)`
- Threshold: `distance <= 0` → filled disc

**Notes:**
- You can keep a “fast path” via `MaskRasterOps2D.StampDisc(...)` for small/simple rooms.
- SDF circle becomes useful when you start composing shapes.

---

#### B) Box room (rect via SDF)
- Raster: `SdfToScalarOps.WriteBoxSdf(ref sdf, center, halfExtents)`
- Threshold: `<= 0`

**Why do this if you already have `FillRect`?**
- Because box rooms can now participate in unions/subtractions and distance-based operations.

---

#### C) Capsule room (pill-shaped)
- Raster: `SdfToScalarOps.WriteCapsuleSdf(ref sdf, a, b, radius)`
- Threshold: `<= 0`

**Uses:**
- Organic rooms
- “Chambers” that blend nicely with corridors
- Optional corridor model (capsule corridor = SDF corridor)

---

### 2.3 Composition = Room Variety (No New Primitives)
Once rooms are SDF-based, you can combine them:

- **Union**: grow / blob out by combining shapes
- **Intersect**: constrain a blob into a box (rounded box)
- **Subtract**: carve holes/courtyards

Example “recipes”:
- Rounded rectangle: `Intersect(circle, box)`
- Blob: `Union(circle, box)`
- Courtyard: `Subtract(box, circle)`

Then threshold and merge as usual.

---

### 2.4 “Padding” and Thickness via Threshold (SDF Trick)
You can inflate/erode shapes without new ops:

- Expand outward by `t`: fill `distance <= +t`
- Shrink inward by `t`: fill `distance <= -t`

This can later replace or complement:
- room padding rules
- corridor thickness rules
- wall band generation (e.g., walls where `0 < d <= wallThickness`)

---

### 2.5 Adding New SDF Shapes Cleanly (Ellipse, Rotated Box)
If you want ellipses/rotations, add **one SDF primitive** and **one raster op**:

**Ellipse**
- Add: `Sdf2D.Ellipse(float2 p, float2 center, float2 radii)`
- Add: `SdfToScalarOps.WriteEllipseSdf(ref ScalarField2D dst, float2 center, float2 radii)`

**Rotated box**
- Add: `Sdf2D.RotatedBox(float2 p, float2 center, float2 halfExtents, float angleRad)`
- Add: `SdfToScalarOps.WriteRotatedBoxSdf(...)`

Composer integration remains identical: raster → threshold → merge.

---

## 3) Suggested Next Expansion Order (High Leverage)
If you want a clean “next step” beyond the minimal D5 slice:

1) **Add SDF room stamping + scratch buffers** (unlocks variety and accurate overlap tests)
2) **Add no-overlap via mask intersection** (more robust layouts)
3) **Upgrade corridor connection graph** (NearestNeighbor/MST)
4) **Add composite recipes** (big variety, little surface change)
5) **Add morphology/walls later** (post-process phase)

---

*End.*
