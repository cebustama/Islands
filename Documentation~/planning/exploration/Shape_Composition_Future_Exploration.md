# Shape Composition — Future Exploration

**Status:** Parked. Not roadmapped. Not implementation authority.
**Relationship to Islands.PCG:** Experimental sibling subsystem, lives inside the package, reuses substrate, outputs `MaskGrid2D`. Not part of the governed map pipeline chain.
**Revisit trigger:** After current Islands.PCG roadmap reaches a natural breakpoint (post-Phase L hydrology at earliest).

---

## Vision

A deterministic, seeded, parametric shape composition system that outputs a `MaskGrid2D` (optionally via a `ScalarField2D` SDF working buffer). The original motivation, preserved from the pre-Islands iteration of the PCG project: define an archetype (a "cat") as a tree of primitive shapes (circles, rectangles, ovals, math curves) connected by named anchors with parameter ranges, press generate, get infinite variations of the same archetype under different seeds. First milestone archetype is literally a cat.

Output is grid-native and indistinguishable from any other mask in the Islands.PCG pipeline. That means the shape system gets the lantern, `MapDataExport`, `TilemapAdapter2D`, golden/snapshot tests, and the rest of the substrate for free.

---

## Namespace & location

- **Namespace:** `Islands.PCG.Shapes`
- **Folder:** `Runtime/Shapes/` (sibling to `Runtime/Stages/`)
- **Assembly:** initially share `Islands.PCG.Runtime`. Promote to separate assembly only if dependencies get tangled.
- **Authority:** experimental. Not governed by `systems/map-pipeline-by-layers-ssot.md`. Not part of the island pipeline chain. Sits beside existing stages, does not modify them.
- **Demo scene:** own scene under `Samples/ShapeComposition/`, lantern-visualized via `PCGMapVisualization`.

Graduation path (not committed): if the system matures, becomes its own subsystem SSoT at `systems/shape-composition-ssot.md` with its own contracts and test gates. Earned by working code, not prerequisite to writing it.

---

## Architecture summary

**Three-layer model:**

1. **Shape primitives.** SDF functions over continuous local coordinates. Each primitive exposes parameters (size, aspect, rotation) and named anchors (`top`, `bottom`, `center`, plus shape-specific). Minimum set: `Circle`, `Rectangle`, `Oval`. Expansion: `Capsule`, `Triangle`, `BezierBlob`, math curves.
2. **Composition graph.** Tree (or DAG) of shape instances with parent/anchor linkage and per-connection transforms (rotate/scale/offset/mirror). Each node carries parameter ranges rather than fixed values. Resolved deterministically under a seed to produce a `ResolvedComposition`.
3. **Archetype / template.** A `CompositionAsset` is a named archetype with parameter ranges. One template, infinite seeded instances. The "cat template" lives here.

**Internal representation: SDFs.** Each shape contributes a signed distance field to a working `ScalarField2D`. Composition uses `smin(existing, new, k)` for smooth blending — this is what makes organic compositions (cats, creatures, plants) look good instead of lumpy. Threshold at 0 at the end to produce the output `MaskGrid2D`.

**Rasterization model.** Per target grid cell, apply inverse parent-chain transform to get local-space position, evaluate shape SDF, combine with working buffer. Pixel-exact for rotated/scaled primitives. Same pattern works for hard masks and SDFs.

**Pipeline integration.** Single stage class `Stage_ShapeComposition2D : IMapStage2D`. Reads nothing, writes one mask (and optionally the intermediate SDF as a scalar field). Holds a reference to a `CompositionAsset` and the output layer ID. Composition tree is internal to the stage — from the runner's perspective it's one black-box stage.

---

## Components to build

| Component | Shape | Size |
|---|---|---|
| `IShapeSDF` primitive interface | Abstract: `EvaluateSDF(localX, localY) → float` | Small |
| `CircleSDF`, `RectangleSDF`, `OvalSDF` | Concrete primitives | Small each |
| `Anchor` resolution | Named points in local space with optional orientation | Small |
| `ShapeInstance` / `CompositionNode` | Tree node: primitive ref, parent, anchor links, local transform, blend strength `k` | Small |
| `CompositionAsset` | ScriptableObject: root + flat node list, parameter ranges | Trivial |
| `CompositionResolver` | Seeded range resolution → `ResolvedComposition` | Small |
| Transform chain math | 2D rotate/scale/offset, parent propagation | Small but fiddly |
| `CompositionRasterizer` | Walks resolved tree, evaluates each shape SDF, composites into working buffer via smin | Medium |
| `Stage_ShapeComposition2D` | `IMapStage2D` wrapper | Trivial |
| Demo scene + factory-built cat | `BuildCat()` code-built `CompositionAsset` | Small |

Rough budget for "generates a recognizable cat": one focused week of evenings, no editor tooling.

---

## Reuse from Islands.PCG

Fully reused, unchanged:
- `MaskGrid2D`, `ScalarField2D`, `GridDomain2D`
- `MapContext2D`, `MapPipelineRunner2D`, `IMapStage2D`
- `MapNoiseBridge2D` + `SmallXXHash` for parameter jitter, noisy edges, anchor perturbation
- `NoiseSettingsAsset` override-at-resolve pattern → direct template for `CompositionAsset` parameter resolution
- `MapDataExport`, `TilemapAdapter2D`, `PCGMapVisualization` lantern
- Determinism / golden / snapshot test discipline

New code is genuinely additive. Zero modification of existing stages, contracts, or SSoT.

---

## Milestone staircase

Each step ends at something runnable and visually inspectable.

1. **Primitive SDF → mask.** `CircleSDF` + rasterizer function. Scene renders one circle through the lantern. ~½ day.
2. **Wrapped as a stage.** `Stage_StampCircle2D` with Inspector params, executed via `MapPipelineRunner2D`. ~½ day.
3. **Rectangle + Oval primitives.** Same pattern. ~½ day.
4. **SDF working buffer.** Stage allocates `ScalarField2D`, rasterizes into it, thresholds to mask. Single shape, SDF path. ~½ day.
5. **Two shapes with smin.** First blobby composition. Visual "ooh that's cool" milestone. ~½ day.
6. **Transform chaining.** Rotation/scale/offset per shape, parent chain. First rotated compositions. ~1 day with debugging.
7. **Anchors.** Named points, anchor-to-anchor attachment with parent chain resolution. ~1 day.
8. **`CompositionAsset` + factory-built cat.** First recognizable cat. First platypus too. ~1½ days.

**Milestone 0 (tiny, cheap, high value):** create the folder and namespace, write this doc's intent into a README, do nothing else. Fifteen minutes. Reserves the space without committing.

---

## Explicitly out of scope for first version

- Editor tooling (custom inspector, tree view, node graph editor). Code-built compositions only.
- Grammars / recursion / L-systems. Flat trees only.
- Symmetry constraints (mirror, radial). Hand-built if needed.
- SVG export or vector output. Grid only.
- Integration with the island pipeline's POI / path / biome stages. Standalone.
- Multi-archetype libraries. One cat template is the target.
- Runtime composition editing. Author-time only.

---

## Open questions to resolve before starting

1. **Hard mask vs SDF primitives.** SDFs assumed above because they enable smooth blending, but a hard-mask-only first pass is simpler. Decide at milestone 4.
2. **Anchor orientation.** Are anchors just points, or do they carry a tangent/normal for "attach ear *outward* from head"? Skippable until milestone 7.
3. **Blend strength `k` tuning.** Per-node, per-template, or global? Default per-node with sensible fallback.
4. **Parameter range type.** Simple `Range<float> = (min, max)` first. Upgrade to curve/distribution-based if needed.
5. **First archetype.** Cat is the vision. Plants/flowers are more visually forgiving and might be a better calibration target before attempting the cat. Decide at milestone 8.
6. **Coordinate system.** Shape local space Y-up or Y-down? Must match `MaskGrid2D` convention to avoid transform bugs.

---

## Risks

- **Scope creep via integration ideas.** "Shapes should be placed by POI stages" / "compositions should be biome-aware" / "shapes should interact with hydrology". All reasonable later, all fatal to milestone 8. Defer ruthlessly.
- **Transform math bugs.** Parent chain × connection × local compounding is historically where these systems break first. Build an axes-visualization debug overlay before milestone 6.
- **Uncanny valley.** First cat will look wrong in ways that feel unfixable. Expected. Build ten before judging.
- **Authoring friction.** Code-built compositions get painful past ~10 nodes. If cat requires 20+ nodes, authoring UX moves up in priority.
- **Determinism surface.** Seed propagation through parameter ranges must be stable across resolver runs. Reuse the coordinate-hashing discipline from F2/M/M2.a.

---

## Relationship to current roadmap

Does **not** compete with or block anything on `PCG_Roadmap.md`. Does not interact with Phase L (hydrology), Phase N (POIs), Phase O (paths), or Phase W (world tiles). Shares substrate only, not semantic scope.

Natural revisit point: after a current-roadmap phase closes cleanly and before the next starts. Earliest sensible slot is post-Phase L. Could equally wait until post-Phase N if the island pipeline keeps generating higher-priority work.

Not a candidate for roadmap promotion until:
- Milestone 8 (recognizable cat) is hit
- The code has survived one round of "actually use it for something"
- A decision has been made about whether it stays experimental or earns subsystem authority

---

## Rehydration prompt

> Revisit the Shape Composition exploration captured in `Shape_Composition_Future_Exploration.md`. The vision is a parametric shape composition system that outputs `MaskGrid2D` via SDF primitives, composed through a tree of anchor-linked shape instances, deterministic under a seed, reusing the Islands.PCG substrate unchanged. First archetype target is a cat. Not roadmapped; not governed authority. Goal for this session: [decide milestone 0 kickoff / review architecture / draft SDF primitive functions / other].
