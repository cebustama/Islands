# Islands Engine — PCG Roadmap v6 (post Phase D3)

Date: 2026-01-28

This roadmap supersedes **Roadmap v3** and aligns with the **Pipeline SSoT v0.1.8** and the latest **Phase D progress**.
It keeps the same principle: **Fields/Grids toolkit first → port dungeon strategies → add post-process + adapters**.

---

## 0) Current status (what is DONE)

### Phase A — Grids foundation + one debug path ✅ DONE
- `GridDomain2D`
- `MaskGrid2D` (bitset storage, deterministic tail bits)
- deterministic writers (rect/checker)
- Lantern: `PCGMaskVisualization` (GPU-instanced debug visualization)

**Acceptance:** generate a mask deterministically and see it in one scene, with no Tilemaps involved.

### Phase B — Scalar fields + scalar → mask operators ✅ DONE
- `ScalarField2D`
- `ScalarToMaskOps.Threshold(...)` with `ThresholdMode` (supports SDF fill via `LessEqual`)
- Lantern mode: thresholded scalar (optional GPU preview for `>` / `>=`)

**Acceptance:** scalar → mask is deterministic and visualizable.

### Phase C — Primitives (SDF) + mask boolean ops ✅ DONE (C0–C6)
- SDF math: `Sdf2D` (circle/box/capsule/segment)
- Raster: `SdfToScalarOps` (SDF → `ScalarField2D`)
- Distance composition: `SdfComposeOps` + `SdfComposeRasterOps` (Union/Intersect/Subtract in distance-space)
- Mask boolean ops: `MaskGrid2D.CopyFrom/Or/And/AndNot` (word-wise, deterministic)
- Proof: `MaskGrid2DBooleanOpsSmokeTest`
- C6: one lantern scene flips modes and visually validates primitives + boolean ops + parity.

**Acceptance:** Phase C “Done when” checklist is satisfiable in one scene.

### Phase D — First dungeon strategy in pure grids ✅ DONE (D0–D4 DONE)
We are porting the legacy dungeon concepts into **pure grid** strategies.

✅ **D0 — RandomWalk behavioral contract locked**
- Cardinal (4-dir)
- OOB policy: Bounce (maxRetries) + StopEarly
- Random restart on existing floor with `randomStartChance` (uniform sampling)
- Determinism statement is explicit

✅ **D1 — Direction2D (deterministic direction picking)**
- `PickSkewedCardinal(ref Random rng, skewX, skewY)` (axis 50/50, sign biased)

✅ **D2 — SimpleRandomWalk2D (single-walk “carver”)**
- `SimpleRandomWalk2D.Walk(...)` writes directly into `MaskGrid2D`
- Optional brush stamping via `MaskRasterOps2D.StampDisc(...)`
- Lantern mode: `SourceMode.SimpleRandomWalkMask`
- Sanity metric: `MaskGrid2D.CountOnes()`

**Acceptance (D2):** visible “drunk line” + `CountOnes()` sanity + same seed ⇒ same output.

✅ **D3 — IteratedRandomWalk2D (full strategy)**

✅ **D4 — Raster shapes (disc + line) (grid-only)**
- `MaskRasterOps2D.DrawLine(...)` (Bresenham, endpoint-inclusive, optional disc-per-point brush)
- EditMode tests: endpoint inclusion, reversal invariance (hash), axis count sanity, brush growth
- Lantern modes: `RasterDiscMask`, `RasterLineMask`

- `IteratedRandomWalk2D.Carve(...)` runs multiple walks, accumulating floor into `MaskGrid2D`
- Optional restart from existing floor via `randomStartChance` (uniform among ON cells)
- Support in `MaskGrid2D`: `TryGetRandomSetBit(...)`
- Runtime regression gate: `MaskGrid2D.SnapshotHash64(...)`
- Lantern mode: `SourceMode.IteratedRandomWalkMask`
- EditMode tests: determinism + D2 parity sanity

**Acceptance (D3):** `iterations=1` (fixed length, chance=0) matches D2; increasing iterations increases density/spread; same seed+config ⇒ identical output (prefer hash gate).


---

## 1) Non‑negotiables (carry into all future phases)
- **Determinism:** all randomness is seed-driven (`Unity.Mathematics.Random` / Islands hashes). No `UnityEngine.Random` in core.
- **Data-oriented core:** algorithms operate on `MaskGrid2D` / `ScalarField2D` (native memory), not Tilemaps/HashSets.
- **Adapters are outputs:** Tilemap/Texture/Mesh are consumers; core must not require them.
- **Incremental parity:** every ported strategy must be comparable to baseline outputs (snapshot tests / hashes).

---

## 2) Phase D (continued) — Basic raster shapes (grid-only) ✅ DONE

### D4 — “Basic raster shapes” needed by the port (grid-only)
**Goal:** cover the legacy ShapeAlgorithms overlap used by dungeon strategies **without** going through scalar fields: line / disc / rect.

**You already have**
- Rect fill (`RectFillGenerator.FillRect(...)`)
- Disc stamping (`MaskRasterOps2D.StampDisc(...)`) (already used by Random Walk brush)

**Add minimally (to stamp corridors and connect rooms):**
- `MaskRasterOps2D.DrawLine(ref MaskGrid2D mask, int2 a, int2 b, int brushRadius=0, bool value=true)`
  - brushRadius 0: carve a 1-cell-wide Bresenham line
  - brushRadius > 0: for each line point, stamp a disc (corridor thickness)

**Acceptance (D4):**
- Line looks consistent (no gaps) in Lantern at multiple slopes.
- With `brushRadius>0`, produces an even corridor thickness.
- Deterministic: same seed+params ⇒ identical `SnapshotHash64`.

**Status (D4):** ✅ Complete
- Implemented `MaskRasterOps2D.DrawLine(...)` (Bresenham, endpoint-inclusive, optional disc brush).
- Added EditMode tests (`DrawLineTests`) for endpoint inclusion, reversal invariance (hash), axis count sanity, brush growth.
- Added Lantern modes in `PCGMaskVisualization`: `RasterDiscMask`, `RasterLineMask`.

### D5 — Rooms + corridors composition (first “room+corridor” dungeon slice) (tests + optional parity snapshots)
**Goal:** cheap regression protection before porting more strategies.

Minimum:
- EditMode tests for raster shapes:
  - `DrawLine` determinism (hash compare)
  - `DrawLine` symmetry sanity (A→B equals B→A, if using the same algorithm)
  - `DrawLine` thickness sanity (brushRadius increases ones)

Recommended:
- Curated “seed set” snapshots for Phase D strategies using `SnapshotHash64` to detect regressions fast.
 (tests + optional snapshot hash)
**Goal:** cheap regression protection before we port more strategies.

Minimum:
- EditMode: `IteratedRandomWalk2DTests`
  - same seed ⇒ identical grid (cell-by-cell) and same `CountOnes`
  - different seed ⇒ different output (non-equality sanity)
Optional but recommended:
- `MaskGrid2DHash.Compute(in MaskGrid2D mask) -> ulong`
  - use in tests + logs (faster than cell-by-cell for long-term regression)

### Phase D acceptance (end-to-end)
- One scene can generate an iterated-walk floor mask deterministically in Lantern (no Tilemaps).
- No per-frame allocations (reuse grids; allocate only on resolution/mode change).
- A deterministic test gate exists (EditMode).

---

## 3) Phase E — Port remaining dungeon strategies (parity functional)
**Goal:** replicate the tilemap-based pipeline’s strategies in pure grids, one by one, keeping parity gates.

- E1 Corridor First (seeded shuffle; remove `Guid.NewGuid()` patterns)
- E2 Room First (place rooms on grid; corridors via capsule stamps or simple diggers)
- E3 BSP / partition strategies (produce mask + lightweight room list)
- E4 Room Grid layouts (then connectors)
- E5 Composition steps using:
  - mask boolean ops (union/subtract)
  - stamps (disc/box)
  - SDF blobs (circle/box/capsule) → scalar → threshold → mask

**Acceptance:** outputs comparable via snapshots/hashes for a curated seed set.

---

## 4) Phase F — Post-process + walls (core, not Tilemap-dependent)
**Goal:** replace tilemap-coupled post-processing with grid ops.

- F1 Cellular Automata on `MaskGrid2D`
- F2 Morphology: Dilate/Erode/Open/Close (useful for dungeons + generative art)
- F3 Walls:
  - neighbor mask per cell (byte/ushort)
  - LUT resolve wall type → tile id (adapter maps ids to actual tiles)

---

## 5) Phase G — Adapters / outputs (Tilemap, Texture, Mesh)
**Goal:** multiple output paths without changing core logic.

- G1 Tilemap adapter (floor + walls + layers)
- G2 Texture adapter (mask/scalar to Texture2D)
- G3 Mesh adapter (marching squares / contour → extrude)
- G4 Snapshot/export tooling (JSON/binary) for regression + debugging

---

## 6) Phase H — Fields toolkit expansion (biomes, overworld, creative coding)
**Goal:** expand beyond binary masks into richer content generation.

- H1 Vector fields, multi-channel fields
- H2 Noise sampling jobs (reuse Islands SIMD/Burst noise)
- H3 Domain warp/turbulence, gradients, distance transforms
- H4 Generative art “canvas” conveniences (palettes, blend modes, dithering)

---

## 7) Minimal file set for the next handoff (Phase D5)
**Core:**
1) `GridDomain2D.cs`
2) `MaskGrid2D.cs`
3) `MaskRasterOps2D.cs`
4) `Direction2D.cs`
5) `SimpleRandomWalk2D.cs`

**Visualization / debug:**
6) `PCGMaskVisualization.cs`

**Optional (recommended) for D4 gates:**
7) `IteratedRandomWalk2DTests.cs` (keep as regression gate)
8) `MaskRasterOps2DTests.cs` (DrawLine + StampDisc)
9) `MaskGrid2DHash.cs` (if you choose the hash route)

---

## 8) Next action (cleanest immediate step)
Implement **D4** in this order:
1) Add `MaskRasterOps2D.DrawLine(...)` (Bresenham + optional brush stamping)
2) Wire `DrawLine` into Lantern (either a dedicated mode or a tiny “line stamp debug” mode)
3) Add EditMode tests for determinism + gap-free corridor behavior
4) (Optional) Add a small “corridor presets” set in the inspector for quick manual QA
