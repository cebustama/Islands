# Islands Engine — PCG Roadmap v3 (post Phase C)

Date: 2026-01-27

This roadmap updates **Roadmap v2** after completing **Phases A–C** of the new Islands-style PCG pipeline (Fields/Grids + SDF + mask ops), and defines the clean path into **Phase D (first dungeon strategy in pure grids)**.

---

## 0) Current status (what is DONE)

### Phase A — Grids foundation + one debug path ✅ DONE
Core “mask canvas” + visualization loop:
- `GridDomain2D`
- `MaskGrid2D` (bitset storage)
- deterministic writers (rect/checker)
- **Lantern**: `PCGMaskVisualization` (GPU-instanced debug visualization)
- Palette support (optional controller)

**Acceptance:** you can generate a mask deterministically and see it in one scene, with no Tilemaps involved.

### Phase B — Scalar fields + scalar → mask operators ✅ DONE
Scalar-to-mask pipeline:
- `ScalarField2D`
- `ScalarToMaskOps.Threshold(...)` with `ThresholdMode` (supports SDF fill via `LessEqual`)
- Lantern supports “thresholded scalar” (plus optional GPU preview when mode is `Greater/GreaterEqual`)

**Acceptance:** you can generate a scalar field, threshold it deterministically into a mask, and visualize it.

### Phase C — Primitives (SDF) + mask boolean ops ✅ DONE (C0–C6)
Geometry-first toolkit:
- SDF math: `Sdf2D` (circle/box/capsule/segment)
- Raster: `SdfToScalarOps` (SDF → `ScalarField2D`)
- Distance composition: `SdfComposeOps` + `SdfComposeRasterOps` (Union/Intersect/Subtract in distance-space)
- Mask-space boolean ops: `MaskGrid2D.CopyFrom/Or/And/AndNot` (word-wise, tail-bit deterministic)
- Proof: `MaskGrid2DBooleanOpsSmokeTest`
- **C6 demo wiring**: one lantern scene flips modes and visually validates primitives + boolean ops + parity (distance-composed vs mask-composed) for union/intersect.

**Acceptance:** Phase C “Done when” checklist is satisfiable in one scene.

---

## 1) Non‑negotiables (carry into Phase D+)

- **Determinism:** all randomness is seed-driven (`Unity.Mathematics.Random` or Islands hashes).
- **Data-oriented core:** algorithms operate on `MaskGrid2D` / `ScalarField2D` (Native memory), not Tilemaps/HashSets.
- **Adapters are outputs:** Tilemap/Texture/Mesh are *consumers*; core logic must not require them.
- **Incremental parity:** every ported strategy must be comparable to baseline outputs (snapshot tests / hashes).

---

## 2) Updated roadmap (phases going forward)

### Phase D — First dungeon strategy in pure grids (start porting) 🎯 NEXT
Goal: deliver a **single end-to-end dungeon generator** that produces a `MaskGrid2D` “floor” in a deterministic way, visualizable in the lantern, with at least one parity gate.

**D0 — Snapshot/hash harness (tiny but critical)**
- Add a small utility to compute a stable hash of a mask:
  - `MaskGrid2DHash.Compute(in MaskGrid2D mask) -> ulong` (or `Hash128`)
- Optional: dump a simple “snapshot” (resolution + list/hash) for regression.

**D1 — Iterated Random Walk operator on MaskGrid2D**
- Implement as a pure operator:
  - `RandomWalkOps.Iterated(ref MaskGrid2D dst, uint seed, int steps, int2 start, int brushRadius, BoundsPolicy policy)`
- Write directly to the bitset (via SetUnchecked / word ops as appropriate).

**D2 — Basic raster stamps (rooms/corridors) mixed in**
- Use your existing Phase C stamps:
  - Rooms: circle/box SDF → threshold → mask → union into floor mask.
  - Corridors: capsule SDF → threshold → mask → union.
- This keeps the “strategy” extensible without introducing Tilemaps.

**D3 — Lantern integration**
- Add 1–2 SourceModes for:
  - `RandomWalkFloor`
  - (optional) `RandomWalkPlusStamps`
- Parameters exposed in inspector (seed/steps/brush/start).

**Phase D acceptance**
- Same seed → same hash.
- No per-frame allocations (reuse grids/fields; allocate on resolution/mode change only).
- Visual output makes sense and is stable across play sessions.

---

### Phase E — Port the remaining dungeon strategies (parity functional)
Goal: replicate the tilemap-based pipeline’s strategies in pure grids, one by one.

- Corridor First (seeded shuffle; no `Guid.NewGuid`)
- Room First / BSP (operates on grids; produces mask + a lightweight room list)
- Room Grid (layout-only first, then connectors)
- Add “composition” steps using mask boolean ops (union/subtract) and stamps.

**Acceptance:** strategy outputs are comparable to baseline snapshots/hashes for key seeds.

---

### Phase F — Post-process + walls (core, not Tilemap-dependent)
Goal: replace tilemap-coupled post-processing with grid ops.

- Cellular Automata on `MaskGrid2D` (no Tilemap reads)
- Morphology ops: Dilate/Erode/Open/Close (useful for both dungeons + generative art)
- Walls:
  - neighbor mask per cell (byte/ushort)
  - LUT resolve wall type → tile id (strings removed)

---

### Phase G — Adapters / outputs (Tilemap, Texture, Mesh)
Goal: multiple output paths without changing the core.

- `TilemapAdapter` (floor + walls + layers)
- `TextureAdapter` (mask/scalar to Texture2D)
- `MeshAdapter` (marching squares / contour → extrude)
- Exporters (JSON/binary) for snapshots + tooling.

---

### Phase H — Fields toolkit expansion (biomes, overworld, creative coding)
Goal: expand beyond binary masks into richer content generation.

- Vector fields, multi-channel fields
- Noise sampling jobs (reuse Islands SIMD/Burst noise)
- Domain warp/turbulence, gradients, distance transforms
- Generative art “canvas” conveniences (palettes, blend modes, dithering)

---

## 3) Minimal file set per phase (for messages / handoffs)

### For Phase D (recommended “top 7”)
1) `MaskGrid2D.cs`
2) `GridDomain2D.cs`
3) `PCGMaskVisualization.cs`
4) `ScalarField2D.cs`
5) `ScalarToMaskOps.cs`
6) `Sdf2D.cs`
7) `SdfToScalarOps.cs`

(Plus: legacy Iterated Random Walk implementation from the old tilemap pipeline if you want fastest parity.)

---

## 4) Next action (cleanest immediate step)

Implement **Phase D0 + D1** first:
1) `MaskGrid2DHash` (or equivalent snapshot hash utility)
2) `RandomWalkOps.Iterated(...)` writing directly to `MaskGrid2D`
3) Add one lantern SourceMode to preview the walk result.

That gives you a deterministic, testable, visual vertical slice before porting anything else.
