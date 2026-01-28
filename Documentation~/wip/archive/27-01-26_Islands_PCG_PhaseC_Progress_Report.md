# Islands PCG Toolkit — Phase C Progress Report (C0–C6)

Date: 2026-01-27

## Phase C goal
Unlock a **geometry-first** toolkit on top of Fields/Grids:

- **SDF primitives** (circle/box/capsule; extendable to ellipse/segment later)
- **SDF → ScalarField2D raster ops**
- **Scalar → Mask** via SDF-friendly threshold (`distance <= 0`)
- **Distance-space composition** (Union/Intersect/Subtract) *before* thresholding
- **Mask boolean ops** (Union/Intersect/Subtract) in **bitset space** (fast, word-wise)
- **C6 lantern demo wiring**: one scene can flip modes and visually verify correctness

---

## Status
✅ **C0–C6 complete** (Phase C is code-complete and ready for acceptance checks).

### C0 — Phase C scaffolding + namespaces
- Added/organized Phase C folders/namespaces:
  - `Islands.PCG.Primitives` (pure math)
  - `Islands.PCG.Operators` (writes/transforms on grids/fields)

### C1 — SDF-friendly thresholding
- Updated `ScalarToMaskOps` to support `ThresholdMode`:
  - `Greater`, `GreaterEqual`, `Less`, `LessEqual`
- Enables canonical SDF fill:
  - `threshold = 0`
  - `mode = LessEqual` (inside = distance <= 0)

**Files:**
- `ScalarToMaskOps.cs`

### C2 — Pure SDF math primitives + composition ops
- Implemented distance functions in `Sdf2D`:
  - `Circle`, `Box`, `Segment`, `Capsule`
- Implemented composition ops in `SdfComposeOps`:
  - `Union(min)`, `Intersect(max)`, `Subtract(max(dA, -dB))`
  - (Optional) smooth variants available

**Files:**
- `Sdf2D.cs`
- `SdfComposeOps.cs`

### C3 — Rasterize SDF → ScalarField2D
- Implemented raster ops writing into an existing scalar field:
  - `WriteCircleSdf`, `WriteBoxSdf`, `WriteCapsuleSdf`
- Uses the **cell-center sampling convention** `(x+0.5, y+0.5)` in grid units.

**Files:**
- `SdfToScalarOps.cs`

### C4 — Raster-compose two SDFs in distance-space
- Implemented `SdfComposeRasterOps` to compose per cell while rasterizing:
  - Union / Intersect / Subtract in **distance-space**
- Added lantern mode to visualize composed distance → thresholded mask.

**Files:**
- `SdfComposeRasterOps.cs`
- `PCGMaskVisualization.cs` (added `SdfCircleBoxCompositeMask`)

### C5 — Fast mask boolean ops (word-wise) on MaskGrid2D
- Implemented in-place bitset ops on `MaskGrid2D` (backed by `NativeArray<ulong>`):
  - `CopyFrom(in other)`
  - `Or(in other)` (union)
  - `And(in other)` (intersection)
  - `AndNot(in other)` (subtract: `this &= ~other`)
- Ensures **tail-bit determinism** after ops (bits beyond `Domain.Length` cleared).
- Added a deterministic smoke test.

**Files:**
- `MaskGrid2D.cs`
- `MaskGrid2DBooleanOpsSmokeTest.cs`
- `PCGMaskVisualization.cs` (added `MaskUnion/MaskIntersect/MaskSubtract`)

### C6 — Final Phase C demo wiring in PCGMaskVisualization
- Finalized lantern modes + inspector parameters so a single scene can flip modes and visually verify:
  - SDF primitive masks:
    - `SdfCircleMask`, `SdfBoxMask`, `SdfCapsuleMask`
  - Mask boolean ops:
    - `MaskUnion`, `MaskIntersect`, `MaskSubtract`
  - Parity checks (visual):
    - distance-composed (`SdfCircleBoxCompositeMask` + `composeMode`) vs mask-composed (`MaskUnion/MaskIntersect`) at least for union/intersect.
- Uses allocation reuse (`EnsureMaskAllocated`, `EnsureScalarAllocated`, etc.) to avoid per-frame allocations.

**Files:**
- `PCGMaskVisualization.cs`

---

## Acceptance checklist (how to “prove” Phase C is done)

### 1) Visual verification (Lantern scene)
In the lantern scene, flip `SourceMode` and confirm:

**Primitives**
- `SdfCircleMask`: circle looks correct as you change center/radius.
- `SdfBoxMask`: box respects center + half-extents.
- `SdfCapsuleMask`: capsule respects endpoints A/B and radius.

**Mask boolean ops**
- `MaskUnion`: union of circle + box.
- `MaskIntersect`: overlap region only.
- `MaskSubtract`: circle with box “bite” removed (or vice-versa depending on implementation).

**Parity (distance-composed vs mask-composed)**
- Compare:
  - `SdfCircleBoxCompositeMask` with `composeMode=Union` vs `MaskUnion`
  - `composeMode=Intersect` vs `MaskIntersect`
- They should match visually for crisp thresholding (`threshold=0`, `LessEqual`).

### 2) Deterministic smoke test (C5)
- Add `MaskGrid2DBooleanOpsSmokeTest` to an empty GameObject and press Play.
- Expect log: `"[MaskGrid2D] Boolean ops smoke test passed."`

### 3) Tail-bit determinism edge-case
- Try a resolution not divisible by 64 (e.g., 63 or 65) and ensure no “garbage bits” appear on the last row/column.

---

## What Phase C enables right now (examples)

1) **Room stamps**
- Use SDF box/circle → threshold to create “room masks” deterministically.

2) **Corridor carving**
- Use SDF capsule → threshold to carve corridors as thick line segments.

3) **Boolean composition workflows**
- Compose in distance-space for higher quality (and later smooth blends).
- Or compose in mask-space for fast “grid boolean” carving.

4) **Code-art / generative masks**
- Build silhouette-style masks and export later as textures/meshes (Phase D+).

---

## Next step after Phase C: Phase D (first dungeon strategy in pure grids)

Recommended D0 vertical slice:
- **Iterated Random Walk** operating directly on `MaskGrid2D`, seeded with `Unity.Mathematics.Random`.
- Optional: stamp occasional rooms (SDF box/circle) and union them into the floor mask.
- Validate via lantern + (recommended) add a simple snapshot/hash export for parity gating later.
