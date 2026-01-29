# Islands.PCG — Roadmap (Phase-Level Goals)

**Purpose:** This document tracks **high-level phases and outcomes** for the Islands.PCG toolkit.  
**Non-goal:** It does **not** restate implementation contracts or detailed task breakdowns (those live in the SSoT and phase planning reports).

**Status legend:** ✅ DONE · ▶️ NEXT / IN PROGRESS · ⏳ LATER

---

## Phase A — Fields/Grids core ✅ DONE
**Goal:** Establish deterministic, reusable data foundations.

**Outcomes**
- 2D domain + indexing conventions (`GridDomain2D`).
- Dense binary mask grid (`MaskGrid2D`) with safe bounds handling.
- Dense scalar field (`ScalarField2D`) for SDF/height/density workflows.
- Determinism utilities + snapshot hashing (regression-proofing).
- Core scalar→mask thresholding operator(s).

**Exit criteria**
- Core types compile + are unit-tested.
- Same seed/config produces stable `SnapshotHash64` for representative operations.

---

## Phase B — Lantern baseline ✅ DONE
**Goal:** Provide a fast “generate → visualize” loop for every layer of the stack.

**Outcomes**
- `PCGMaskVisualization` (Lantern) end-to-end path:
  allocate → generate → pack/upload → GPU instance display.
- Multiple source modes to validate individual layers quickly (field, mask, SDF-raster, etc.).

**Exit criteria**
- Any new operator/strategy can be validated visually in seconds.
- Lantern can be used as the primary smoke-test during iteration.

---

## Phase C — SDF + composition + mask ops ✅ DONE
**Goal:** Enable authored/procedural shapes via SDFs, then convert to grids.

**Outcomes**
- SDF primitives (`Sdf2D`) + composition utilities (`SdfComposeOps`).
- SDF rasterization to `ScalarField2D` + thresholding to `MaskGrid2D`.
- Mask boolean operators (union/intersect/subtract) with lantern validation.

**Exit criteria**
- Canonical SDF shapes render correctly (sign convention stable).
- Boolean ops and SDF pipelines have deterministic snapshot tests.

---

## Phase D — First grid-only dungeon strategies ✅ DONE
**Goal:** Prove the “grid-first, adapter-later” dungeon generation approach.

**Outcomes**
- Baseline layout primitives (e.g., `Direction2D`).
- Grid carvers:
  - Simple Random Walk
  - Iterated Random Walk
- Raster shapes (grid writers):
  - Line carving (Bresenham all octants, endpoint-inclusive)
  - Brush stamping (disc-per-point semantics; forward-compatible)
- Rooms + corridors composition (rect rooms + center connectors) with golden hash gates.

**Exit criteria**
- Each strategy has:
  - one public `Generate(...)` / `Carve(...)` entrypoint
  - a lantern mode
  - deterministic EditMode tests using `SnapshotHash64`
  - at least one “golden hash gate” where appropriate

---

## Phase E — Port remaining dungeon strategies ▶️ NEXT / IN PROGRESS
**Goal:** Replace remaining legacy dungeon strategies with pure grid implementations.

**Immediate targets**
- **Corridor First**
- **Room First (BSP)**
- **Room Grid** (layout-only minimal slice)

**Optional expansions (still Phase E scope if needed)**
- Cellular automata caves (grid-only).
- Room graph wiring utilities (MST / k-nearest connectivity) as reusable helpers.
- Noise/SDF-driven “blob rooms” + corridor carving (still emitting masks via the same raster contract).

**Exit criteria**
- Each ported strategy matches the Phase D acceptance pattern:
  lantern mode + deterministic snapshot tests (+ optional golden hash).

> Detailed implementation plan lives in **PhaseE_Planning_Report.md**.

---

## Phase F — Morphology + walls bitmasks ⏳ LATER
**Goal:** Add engine-grade post-process steps for game-ready dungeon masks.

**Outcomes**
- Morphology on `MaskGrid2D` (dilate/erode/open/close).
- Wall neighbor bitmask computation (`ComputeWallMask` or equivalent).
- LUT mapping from neighbor mask → wall output IDs (adapter-ready representation).

**Exit criteria**
- Morphology + wall bitmask results are deterministic and snapshot-tested.
- Outputs are suitable inputs for rendering/adapters without changing core logic.

---

## Phase G — Extract + adapters ⏳ LATER
**Goal:** Convert core outputs into practical downstream artifacts **without coupling core to Unity view systems**.

**Outcomes**
- Mask/field → `Texture2D` (debug + art workflows).
- Mask → Tilemap adapter (strictly adapter layer).
- Mask/field → Mesh (marching squares / contours).

**Exit criteria**
- Adapters are optional; core runtime can run headless.
- Adapters consume stable core outputs; no core dependency on rendering frameworks.

---

## Phase H — Burst/SIMD upgrades ⏳ LATER
**Goal:** Optimize hot loops while keeping behavior identical.

**Outcomes**
- Move performance-critical loops into Burst jobs where it actually matters.
- Improve memory layouts for word packing / SIMD-friendliness.
- Maintain snapshot parity (contracts unchanged).

**Exit criteria**
- Performance improvements demonstrated on representative workloads.
- All determinism + golden hash gates remain unchanged.

---

## Related documents
- **Contracts / design rules:** `Islands_PCG_Pipeline_SSoT_*_ContractsOnly_NoRoadmap.md`
- **Phase E detailed plan:** `PhaseE_Planning_Report.md`
