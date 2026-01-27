# Islands Engine — Final Technical Bible / SSoT (0.1.0-preview)
**Date:** 2026-01-25  
**Goal:** This document is the stable, consolidated Single Source of Truth for the **Islands Unity package**.  
It’s written to support the next big objective: **fusing / porting your dungeon PCG pipeline into Islands’ data-oriented (Burst/SIMD) style**, while also enabling “creative coding / generative art” workflows.

> This “Final” SSoT supersedes the incremental Pass docs (v1–v6). Those passes were cumulative snapshots; this file is the cleaned, stable baseline for continued work.

---

## 1) What Islands is (one paragraph)
**Islands** is a deterministic, Burst-friendly procedural toolkit built around a consistent SIMD pattern (**4-wide lanes**) and explicit domain transforms. It includes:
- a composable **Noise kernel framework** (hash → gradient/feature point → lattice/simplex/voronoi → fractal accumulation),
- a **procedural mesh generation** pipeline (generators + streams + jobs),
- a **surface displacement + flow** pipeline (noise-driven deformation and vector-field flow),
- and a **procedural instancing visualization** pipeline (Shapes → packed position/normal buffers → GPU instancing), plus samples that show CPU vs GPU graphing, hashing visualization, and fractal instanced rendering.

---

## 2) Islands “Style Contract” (non‑negotiables)

### 2.1 SIMD-first: work in groups of 4
Hot-path math uses `float4`, `int4`, `uint4`, `float3x4`, `float4x3`.  
Jobs and buffers are designed so the *native* unit is “4 samples at once”.

### 2.2 Burst + structs + no managed polymorphism in hot loops
Performance code is written as `struct` jobs and `struct` kernels. Selection is usually done via:
- **generic instantiations** compiled ahead of time (often forced by explicit compilation helpers), and/or
- **delegate lookup tables** (`ScheduleDelegate[]`) to avoid reflection.

### 2.3 Determinism is explicit and seed-driven
Randomness must come from the package’s hash primitives (`SmallXXHash`, `SmallXXHash4`) and explicit seeds.
No hidden `UnityEngine.Random` in core generation jobs.

### 2.4 Domain mapping is explicit
Spatial meaning is carried by transforms like `SpaceTRS` (`Matrix` and derivative matrix), not implicit in the noise kernels.

### 2.5 Data contracts are explicit
CPU packs 4-at-a-time; GPU often consumes *flat* buffers (`StructuredBuffer<float3>` / `float`), so upload steps must clearly define reinterpret/stride rules.

---

## 3) Module map (systems & responsibilities)

### A) **Core math & transforms**
- `SpaceTRS`: domain transform and derivative transform used to map “world / grid / tile space” into noise space.

### B) **Noise**
- Deterministic sampling of scalar fields (and their derivatives) in SIMD-friendly form.
- Components: hashing, gradients, lattice/simplex/voronoi schemes, fractal accumulation.

### C) **Meshes**
- Generate indexed meshes with Burst jobs.
- Components: `IMeshGenerator`, `IMeshStreams`, stream implementations, triangle encoding, generator catalog, mesh job scheduling.

### D) **Surfaces & Flow**
- `SurfaceJob<N>`: apply procedural displacement and compute consistent normals/tangents from derivatives.
- `FlowJob<N>`: use derivatives/fields to drive particle motion (vector field / flow map).

### E) **Visualization & Shapes**
- `Visualization`: instanced rendering pipeline (packed points + normals → GPU buffers → `DrawMeshInstancedProcedural`).
- `Shapes`: burst SIMD “domain samplers” (plane/sphere/torus/etc.) that fill packed position/normal buffers.

### F) **Samples (runtime “reference implementations”)**
- CPU & GPU graphing (FunctionLibrary, Graph, GPUGraph).
- Hash visualization (HashVisualization).
- Noise visualization (NoiseVisualization).
- Fractal instanced rendering (Fractal).
- Procedural meshes/surface demos (ProceduralMesh, ProceduralSurface).

---

# PART I — NOISE (fields)

## 4) Noise primitives (core contracts)

### 4.1 Hashing: `SmallXXHash` + `SmallXXHash4`
These are the canonical deterministic PRNG primitives.
- scalar and 4-wide versions
- “eat coordinates” pattern: `hash.Eat(x).Eat(y).Eat(z)` to produce stable pseudo-randomness per cell/point.

**Fusion rule:** any future PCG RNG should be a wrapper around these hashes.

### 4.2 The noise atom: `Sample4`
`Sample4` carries:
- value `v` (`float4`)
- derivatives `dx, dy, dz` (`float4` each)
It supports sample-space operations (ex: `Smoothstep`) that update both value and derivatives consistently.

**PCG meaning:**
- **Scalar field** = `v`
- **Gradient field** = `(dx, dy, dz)`
- **Flow field** can be derived from gradients (downhill) or curl-like constructions.

### 4.3 Gradients: `IGradient` and implementations
`IGradient` maps a hash + offsets → `Sample4`. Examples:
- Value, Perlin, Simplex, Turbulence wrapper, Smoothstep wrapper.
`BaseGradients` provides fast direction sampling without trig.

### 4.4 Lattice noise (`Lattice*`)
Classic grid interpolation, generalized through:
- lattice policy (`Normal` vs `Tiling`) + smoothing derivatives
- gradient policy (`IGradient`)
`LatticeTiling` is the “official” path for periodic domains.

### 4.5 Simplex noise (`Simplex*`)
Skew/unskew coordinate transforms and simplex kernels.
Often gives smoother natural results than lattice Perlin.

### 4.6 Voronoi (`Voronoi*` + Distance + Function)
Split into:
- distance metric (`IVoronoiDistance`: Worley, SmoothWorley, Chebyshev)
- value function (`IVoronoiFunction`: F1, F2, F2-F1, CellAsIslands, etc.)
This stack is extremely useful for “cells/regions/rooms” style procedural layouts.

---

# PART II — MESHES (geometry)

## 5) Mesh generation architecture

### 5.1 `IMeshGenerator` (what to build)
Defines:
- `VertexCount`, `IndexCount`, `JobLength`, `Bounds`, `Resolution`
- `Execute<S>(int i, S streams)` to emit vertices and triangles through streams

**Rule:** generators must be deterministic from parameters.

### 5.2 `IMeshStreams` (how to write)
Defines:
- `Setup(MeshData, Bounds, vertexCount, indexCount)`
- `SetVertex(int index, Vertex v)`
- `SetTriangle(int index, int3 tri)`

Streams own the vertex layout and index encoding decisions.

### 5.3 Triangle encoding: `TriangleUInt16`
Canonical index triangle struct:
- sequential `ushort a,b,c`
- conversion from `int3`
Current streams write index buffers via `ushort` and reinterpret into `TriangleUInt16`.

**Rule:** current pipeline is **UInt16-only** → keep vertex counts ≤ 65535 unless you add a UInt32 stream family.

---

## 6) Vertex layout + stream families (critical contract)

### 6.1 Authoring vertex: `Meshes.Vertex`
Generators write this payload:
- `position (float3)`
- `normal (float3)`
- `tangent (float4)`
- `texCoord0 (float2)`

### 6.2 Stream implementations (known)
- **SingleStream**: interleaved full layout (position/normal/tangent/UV0) in one stream.
- **MultiStream**: split attributes into 4 separate streams.
- **PositionStream**: positions only (debug/implicit cases).

### 6.3 SurfaceJob hard assumption (IMPORTANT)
`SurfaceJob<N>` reads vertex data as `SingleStream.Stream0` and processes **4 vertices per iteration** by reinterpreting into `Vertex4`.

**Consequences:**
1) Mesh vertex count should be divisible by 4 (or be padded).
2) If you want CPU displacement/normals, generate meshes using **SingleStream** (or implement a SurfaceJob variant for other layouts).

---

## 7) Generator catalog (what’s in the package)
Islands includes multiple generator families; the important axis is:

### 7.1 Duplicated-vertex grids (per-cell independent)
- `SquareGrid` (duplicated per quad)  
Best for per-cell deformation, marching squares outputs, or “pixel canvas to mesh” conversions.

### 7.2 Shared-vertex grids (continuous surfaces)
- `SharedSquareGrid`, `SharedTriangleGrid`, `SharedCubeSphere`  
Best for smooth displacement and fewer cracks; also lower vertex counts.

### 7.3 Polyhedra & sphere variants
- Cube / CubeSphere (cube mapping to sphere)
- Octahedron / Octasphere / GeoOctasphere
- Tetrahedron (+ sphere variant in Shapes)
- UVSphere
- GeoIcosphere, Dodecahedron, FlatHexagonGrid

**Practical guidance for PCG:**
- For heightfields/terrain: `SharedSquareGrid` + `SurfaceJob<Noise>` is the default.
- For debug/visualization: `SquareGrid` (duplicated) is great because each quad can be treated independently.
- For worlds/planets: `SharedCubeSphere` / `GeoIcosphere` / `Octasphere` families depending on desired distribution.

---

# PART III — SURFACES & FLOW (field → deformation → motion)

## 8) Surface displacement: `SurfaceJob<N>`
`SurfaceJob<N>`:
- samples noise in domain space (`SpaceTRS`)
- displaces vertices (plane: y-height; sphere: radial)
- recomputes normals/tangents from derivatives

**Plane vs Sphere switch**
This switch must remain consistent across:
- CPU displacement job logic
- shaders/material flags (e.g., `_IsPlane`) used in displacement graphs

## 9) Flow fields: `FlowJob<N>`
Used to drive particle motion by sampling derivatives / fields consistently with the same domain transforms.

---

# PART IV — VISUALIZATION & SHAPES (fast canvas & instancing)

## 10) Visualization base class (GPU instancing pipeline)
`Visualization` generates and renders **resolution² instances** without GameObjects.

### 10.1 Data produced
- CPU packed arrays:
  - `NativeArray<float3x4> positions`
  - `NativeArray<float3x4> normals`
- GPU buffers (flat):
  - `_Positions` / `_Normals` as `StructuredBuffer<float3>`
- `_Config` encodes: `(resolution, instanceScale/resolution, displacement)` (as currently used by the sample pipeline)

### 10.2 Extension points
Derived visualizations implement:
- `EnableVisualization(dataLength, propertyBlock)` (allocate extra buffers)
- `DisableVisualization()` (release)
- `UpdateVisualization(positions, resolution, dependency)` (schedule extra jobs)

---

## 11) Shapes (sampling domains for instancing and fields)
`Shapes` provides SIMD point sampling for common domains:
- Plane, UVSphere, Torus
- Tetrahedron/Octahedron variants (and sphere projections)

### 11.1 The key reusable primitive: packed UV enumeration
The mapping `IndexTo4UV(i, resolution, invResolution)` is essentially Islands’ **“packed pixel enumerator”**:
- It converts packed index `i` into 4 uv samples centered in their cells.
- This is directly reusable for your “0/1 pixel canvas” PCG toolkit.

### 11.2 Shape job architecture
`Shapes.Job<S>` (Burst) fills packed `positions[i]`, `normals[i]` from `S : IShape` and applies TRS transforms.

---

# PART V — GPU / SHADERS (handshake)

## 12) HLSL contracts (what CPU provides)

### 12.1 Procedural instancing (point surfaces)
HLSL expects buffers:
- `_Positions` and `_Normals` (`StructuredBuffer<float3>`)
- optional `_Noise` (`StructuredBuffer<float>`)
- `_Config` values (meaning defined by the visualization)
and uses `UNITY_PROCEDURAL_INSTANCING_ENABLED` to set instance transforms based on instance ID.

Relevant includes:
- `NoiseGPU.hlsl`
- `PointGPU.hlsl`
- `HashGPU.hlsl`

### 12.2 Procedural mesh ripple helpers
- `ProceduralMesh.hlsl` contains helper functions used by shader graphs for ripples/deformation on meshes.

### 12.3 Shader Graph assets
The package includes Shader Graph assets (procedural mesh, displacement, cube map). Treat these as **rendering layer**, driven by the CPU-side contracts above.

---

# PART VI — SAMPLES (reference implementations you can copy/paste)

## 13) Graphing (CPU vs GPU)
### 13.1 CPU Graph (`Graph`)
- Spawns `resolution²` point prefabs and animates them by calling FunctionLibrary functions.
- Supports transitions between functions using `Morph`. fileciteturn28file2

### 13.2 GPU Graph (`GPUGraph`)
- Uses a compute shader to fill a positions buffer (`ComputeBuffer`) and draws instances procedurally.
- Uses the same transition logic but entirely on GPU. fileciteturn28file1

### 13.3 FunctionLibrary (the function set)
Defines parametric surfaces (Wave, MultiWave, Ripple, Sphere, Torus) and morphing utilities. fileciteturn28file0

**Why this matters for fusion:**  
This is your existing “creative coding canvas” pattern: **grid sampling + function composition + GPU instancing**.

---

## 14) Fractal (GPU instanced matrices)
`Fractal` builds a hierarchy of parts, updates them via Burst jobs, uploads matrices to GPU buffers per level, and draws each level instanced. fileciteturn28file3

**Reusable idea:** “many instances, one buffer, one draw call” — same pattern that your PCG “sprite/voxel canvas” can reuse.

---

## 15) Hash visualization (deterministic RNG debug)
`HashVisualization : Visualization`:
- builds hash values for each packed position (using `SmallXXHash4`)
- uploads hashes to GPU for display/debug. fileciteturn28file4

**Reusable idea:** debug tools that make determinism visible.

---

## 16) Procedural mesh & surface demos
- `ProceduralMesh`: registry-driven mesh generator browser (MeshJob scheduling, apply, optional optimization, gizmos).
- `ProceduralSurface`: the full reference pipeline (MeshJob → SurfaceJob → optional FlowJob, plus material flags).

---

# PART VII — Fusion target: Islands.PCG (how we port your dungeon pipeline)

## 17) The target architecture (proposed)

### 17.1 `Islands.PCG.Fields`
**Goal:** represent scalar/vector fields as deterministic, Burst-friendly, SIMD-packed grids.

Recommended core types:
- `ScalarGrid2D` (values) and optionally `ScalarGrid2D4` (packed float4 lanes)
- `VectorGrid2D` (vectors) / `MaskGrid2D` (bitmask occupancy)
- Operators: threshold, blur, dilation/erosion, union/intersection, distance transforms (later), flood-fill, connected components.

**Hook into Islands:** write these grids by sampling Noise (`Sample4`) and/or Shapes enumerators.

### 17.2 `Islands.PCG.Primitives`
Shape-drawing on grids:
- rectangle, circle, ellipse
- signed distance functions (SDF) as the unifying abstraction
- composition ops: add/subtract/smooth-min (for caves/organic rooms)

**Hook into Islands:** reuse `Shapes.IndexTo4UV` (packed pixel enumeration) for SIMD filling.

### 17.3 `Islands.PCG.Layout`
Higher-level dungeon logic:
- room placement (grid or Poisson)
- corridor carving (A* / drunken walk / L-system / directed tunneling)
- partitioning (Voronoi as room seeds; excellent overlap with Islands Voronoi module)

### 17.4 `Islands.PCG.Extract`
Convert fields into geometry:
- 2D: marching squares → mesh
- 3D (future): marching cubes / dual contour
**Hook into Islands:** emit geometry via `IMeshStreams` (prefer `SingleStream` if SurfaceJob will run afterwards).

---

## 18) How existing Islands modules improve dungeon PCG (concrete synergies)

### 18.1 Noise → better cave + region control
Use simplex/lattice FBM for:
- cavern masks (thresholded noise)
- region weighting for room density
- “wet/dry” gradients for biome-style variation

### 18.2 Voronoi → room/cell partitioning
Use Voronoi distance/function combos for:
- cell layout
- region borders (F2-F1 ridges)
- “islands plateaus” style room blobs

### 18.3 Derivatives → slope/flow-based decoration
Gradients from `Sample4` can drive:
- flow maps for rivers/lava
- wall drip directions
- erosion-inspired decoration fields

### 18.4 Visualization → instant debug of fields
Before generating meshes, render packed points:
- color by occupancy / distance / region id
- visualize vector fields as normals/axes
This accelerates iteration massively.

---

## 19) Immediate “Fusion” next steps (minimal, incremental)
1) **Create `Islands.PCG.Fields`** with a packed grid (float4 lanes) + basic ops (threshold, union, flood-fill).  
2) Implement `NoiseScalarField2DJob` that samples `Noise` into a grid using `SpaceTRS`.  
3) Implement `RoomSDF` primitives (rect/circle/ellipse) and boolean composition into `MaskGrid2D`.  
4) Prototype one dungeon algorithm in Islands-style:
   - “room-first” carved via SDF + corridors via deterministic tunneling.  
5) Add a Visualization-based debug view for the dungeon mask.  
6) Add marching squares extraction → `IMeshStreams` → optional `SurfaceJob` for stylized displacement.

---

## 20) What’s left to import into this SSoT (optional)
If you want this SSoT to become “complete package documentation”, the remaining candidates are:
- any **Tests** (determinism regression harness)
- any additional **Runtime registries** / explicit Burst compilation helpers not yet described
- any additional shader include files used by the samples

But for the **PCG fusion** goal, you’re now in a good place: the engine contracts are mapped and stable.

---

## Appendix — Source files added in the “samples” batch
- `FunctionLibrary.cs`, `Graph.cs`, `GPUGraph.cs`
- `FunctionLibrary.compute`
- `PointGPU.hlsl`
- `Fractal.cs`, `FractalGPU.hlsl`
- `HashVisualization.cs`, `HashGPU.hlsl`

