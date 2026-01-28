# Islands PCG Toolkit — Progress Report (Phases A–B)

Date: 2026-01-27

This report covers **everything up to the end of Phase B (+ palette support)**.  
Phase C (SDF primitives + mask boolean ops + C6 lantern demo wiring) is documented separately in: **`Islands_PCG_PhaseC_Progress_Report.md`**.

---

# Islands PCG Toolkit — Steps 0–4 + Phase B + Phase C Progress Report (Fields/Grids + “Lantern” Visualization)

Date: 2026-01-26
Last updated: 2026-01-27 (Phase C C4 completed)

This report summarizes the completed “parallel and safe” foundation steps (0–4) for the Islands-style deterministic PCG toolkit: a reusable **Fields/Grids** core + a minimal **visual debug pipeline** (“linterna”) that avoids Tilemaps and keeps core logic data-oriented.

---

## Executive summary

We now have a working end-to-end loop:

**Generate (CPU, deterministic) → Store (pure grid data) → Upload (ComputeBuffer) → Visualize (GPU instanced procedural)**

This proves the core architectural constraint for the migration:

- **Core logic operates on grid data** (NativeArray/bitset), not Tilemaps/HashSets.
- **Adapters are outputs**, not required by the core.
- We can validate results via **snapshot-style visual tests**.

A checkerboard test confirms correct coordinate mapping, packing, and shader reading.

---

## Step-by-step accomplishments

### Step 0 — New runtime assembly (parallel + safe)
**Goal:** Create an isolated PCG runtime module to build the new system without touching legacy code.

**Outcome:**
- New assembly definition for the PCG runtime (e.g., `Islands.PCG.Runtime.asmdef`) with references to:
  - `Unity.Collections`, `Unity.Mathematics`, `Unity.Burst`
  - `Islands.Runtime` (to reuse infrastructure patterns such as `Visualization`)

**Why it matters:**
- Enables incremental migration with minimal risk: old pipeline can remain untouched while the new system stabilizes.

---

### Step 1 — Core domain: `GridDomain2D`
**Goal:** Define a deterministic, allocation-free description of a 2D discrete grid.

**Outcome:**
- `GridDomain2D` struct introduced to represent:
  - `Width`, `Height`, derived `Length`
  - Coordinate mapping `Index(x,y)` and bounds `InBounds(x,y)`

**Why it matters:**
- Shared foundation for any grid-based structure: masks, scalar fields, vector fields, distance fields, tags, etc.
- Centralizes indexing rules so everything downstream agrees about layout.

---

### Step 2 — Grids: `MaskGrid2D` (bitset 0/1)
**Goal:** Replace “set of occupied cells” (`HashSet<Vector2Int>`) with a compact, Burst-friendly storage.

**Outcome:**
- `MaskGrid2D` stores one bit per cell in a `NativeArray<ulong>`.
- Public ops include (typical set):
  - `Get`, `Set`
  - `Clear` (fast memset-style behavior)
  - `Fill` (optional)
  - `CountOnes` (debug/test helper)

**Why it matters:**
- Memory reduction: **1 bit per tile** vs hash overhead.
- Predictable iteration and cache friendliness.
- Enables hot-loops suitable for Jobs/Burst (room carving, flood fills, distance transforms, etc.).
- Provides an obvious bridge to future outputs (tilemap/texture/mesh) without changing core.

---

### Step 3 — Minimal generator: `RectFillGenerator`
**Goal:** Demonstrate “I can write to the grid” without implementing dungeon strategies yet.

**Outcome:**
- A trivial API like:

```csharp
RectFillGenerator.FillRect(ref mask, xmin, ymin, xmax, ymax, value, clampToDomain: true);
```

**Why it matters:**
- Validates the core write/read path and establishes the expected rectangle convention: `[min, max)` ranges.
- Gives us a controllable test input for visualization and unit tests.

---

### Step 4 — “Lantern” visualization: `PCGMaskVisualization`
**Goal:** Create one debug/visualization route that follows the existing Islands visualization pattern (like `NoiseVisualization`).

**Outcome:**
- `PCGMaskVisualization : Visualization` implements the 3 hooks:
  - `EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock)`
  - `DisableVisualization()`
  - `UpdateVisualization(NativeArray<float3x4> positions, int resolution, JobHandle handle)`

**Core idea:**
- Build a `MaskGrid2D` at the current `resolution`.
- Generate test content (rect or checker).
- Pack the 0/1 mask into `NativeArray<float4>` (4 instances per float4 pack).
- Upload to GPU as `_Noise` buffer via `ComputeBuffer.SetData`.
- Reuse the existing ShaderGraph/HLSL pipeline that reads:
  - `_Positions[unity_InstanceID]`
  - `_Normals[unity_InstanceID]`
  - `_Noise[unity_InstanceID]`

**Why it matters:**
- Proves the “no Tilemaps” pipeline with a real output path.
- Establishes a stable debugging tool that future dungeon strategies can target.

---


---

## Phase B — Scalar fields + scalar→mask operators (DONE)

Phase B extends the Fields/Grids foundation by adding a **dense scalar field** plus a deterministic **Scalar → Mask** conversion.
This unlocks the most common PCG pattern:

**ScalarField2D (continuous values) → Threshold (binary decision) → MaskGrid2D (0/1) → downstream carving/adapters**

### Deliverable B1 — `ScalarField2D` (dense float field)

**File:** `ScalarField2D.cs`  
**Namespace:** `Islands.PCG.Fields`

- Dense storage: `NativeArray<float>` (1 float per cell).
- Same domain ergonomics as the mask: bounds-checked `Get/Set`, plus `GetUnchecked/SetUnchecked` for hot loops.
- Deterministic `Clear(value)` fill.
- Owns native memory → must be `Dispose()`d.

### Deliverable B2 — `ScalarToMaskOps.Threshold` (Scalar → Mask)

**File:** `ScalarToMaskOps.cs`  
**Namespace:** `Islands.PCG.Operators`

- `Threshold(in ScalarField2D src, ref MaskGrid2D dst, float threshold, bool greaterEqual = true)`
- Guards against domain mismatch and unallocated storage.
- Deterministic nested loops with unchecked access inside known bounds.

### Deliverable B3 — Lantern update: show thresholded scalar output

**File:** `PCGMaskVisualization.cs` (modified)

Added a new debug source mode:

- `SourceMode.ThresholdedScalar`:  
  1) allocate a `ScalarField2D` at the current resolution  
  2) fill it deterministically (radial scalar pattern as a stand-in demo)  
  3) `ScalarToMaskOps.Threshold(...)` into the existing `MaskGrid2D`  
  4) reuse the *same* pack/upload shader path (`_Noise` buffer)

**Why this matters:** we now have an end-to-end proof that scalar fields can drive the exact same visualization pipeline as direct mask writers, which is the foundation for SDF primitives, noise-based terrain masks, cave density fields, and many dungeon carving operators.


## Key debugging fix (why it used to draw “one white block”)

We hit a classic failure mode: the scene drew as one solid block because the shader was not actually running in the **procedural instancing** variant.

Two fixes were required:

1) **Enable GPU Instancing** on the materials used by the visualization shader.
2) Ensure the ShaderGraph includes the required pragmas for instancing compilation, specifically:

```c
#pragma target 4.5
#pragma multi_compile_instancing
#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
```

After these changes, Frame Debugger confirmed:
- Instance count = `resolution * resolution` (e.g., 4096 for 64×64)
- `PROCEDURAL_INSTANCING_ON` keyword present
- Buffers `_Positions`, `_Normals`, `_Noise` bound to the draw call

---


## Phase B+ — Runtime palette & background color support (Shader parameters) (DONE)

This adds a **minimal “scene UI”** for color tuning and documents the shader-side requirement for exposing parameters.

### Deliverable B4 — `PCGMaskPaletteController` (scene palette driver)

**File:** `PCGMaskPaletteController.cs` (new)  
**Namespace:** `Islands.PCG.Samples`

Responsibilities:
- Sets **camera background** (forces `SolidColor` clear + `backgroundColor`).
- Sets **mask colors** for 0/1 by calling `PCGMaskVisualization.SetMaskColors(off,on)`.
- Works in-editor and at runtime (`[ExecuteAlways]`), so you can tweak colors without entering Play Mode.

Key behavior:
- `OnValidate()` calls `Apply()` so edits in the Inspector propagate immediately.
- Optional `applyEveryFrame` (see note at end of report) re-applies continuously for slider-driven UI.

### Deliverable B5 — Shader + visualization palette wiring (Mask 0/1 → Colors)

**Files:**
- `PCGMaskVisualization.cs` (modified)
- ShaderGraph: `PCG Mask URP GPU` (modified)
- HLSL: `NoiseGPU.hlsl` / `PCGMaskGPU.hlsl` (modified, depending on your setup)

What changed:
- **`PCGMaskVisualization` now supports palette parameters**:
  - Defines shader property IDs for `_MaskOffColor` and `_MaskOnColor`.
  - Caches the `MaterialPropertyBlock` used by the instanced draw call.
  - Exposes `SetMaskColors(Color off, Color on)` which updates the cached MPB.

- **Shader must expose matching properties**:
  - Add two **Color** properties with References:
    - `_MaskOffColor` (value 0)
    - `_MaskOnColor`  (value 1)
  - Replace the “grayscale/noise” output with a palette mix, conceptually:
    - `finalColor = lerp(_MaskOffColor, _MaskOnColor, maskValue01)`
  - Here `maskValue01` is the same **0/1 value** you already upload in `_Noise[unity_InstanceID]`.

**Why this matters:**
- Establishes a repeatable pattern for *any* shader parameter:
  - add ShaderGraph property → set it through MPB → update live at runtime.
- Makes debugging much easier (high contrast palettes, dark UI background, etc.).

### Expected on-screen result

When running the demo:
- Changing **Background Color** updates the camera clear color immediately.
- Changing **Mask Off / Mask On** colors recolors the grid cells:
  - mask=0 cells use `_MaskOffColor`
  - mask=1 cells use `_MaskOnColor`

This is independent from the mask source mode (Rect, Checker, ThresholdedScalar): it’s purely a visualization palette.

---

## Validation results

### Rect test
- Confirmed that writing a rectangle to the mask produces a corresponding visual “block” in the grid.

### Checkerboard test (strongest proof)
- A checker pattern is extremely sensitive to indexing mistakes.
- The displayed checkerboard pattern confirms:
  - correct mapping `x + y*width`
  - correct packing (4 instances per float4)
  - correct per-instance buffer reads (`unity_InstanceID`)
  - correct distribution of instances from the shape job (plane)


### ThresholdedScalar test (Scalar → Threshold → Mask → GPU)

- With `SourceMode = ThresholdedScalar`, the lantern renders a **filled disc** (from a deterministic radial scalar pattern).
- Changing `threshold` shrinks/grows the disc as expected.
- This confirms:
  - scalar allocation and indexing are correct
  - the threshold operator maps scalar values to bits correctly
  - the pack/upload GPU path is unchanged (only the mask source changed)


---

## What to keep vs what to clean up now

**Keep**
- `GridDomain2D`, `MaskGrid2D`, `ScalarField2D`
- `RectFillGenerator`, `CheckerFillGenerator`
- `ScalarToMaskOps.Threshold`
- `PCGMaskVisualization` as the canonical early “linterna” tool (now supports direct masks *and* scalar→threshold masks)

**Optional cleanup**
- Turn off verbose logs in `PCGMaskVisualization` by default.
- Keep an internal `enableLogs` toggle for future debugging.

---

---

## What’s next (after Phase B)

We now have a stable end-to-end loop:

**Generate (CPU, deterministic) → Store (pure grid data) → Upload (ComputeBuffer) → Visualize (GPU instanced procedural)**

The immediate continuation is **Phase C** (now documented separately), followed by **Phase D** (first dungeon strategy in pure grids).

**Phase D preview (once Phase C is accepted as complete):**
- Port **one** dungeon strategy into a pure-`MaskGrid2D` operator (recommended: Iterated Random Walk).
- Validate via the existing lantern, and add a minimal snapshot/hash check for parity gating.
