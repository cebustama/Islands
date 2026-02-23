# Rehydration Prompt — Islands.PCG — Phase F (F3) Hills + Topology + Islands.Noise Jobs

You are assisting with the design + implementation alignment of a Unity procedural toolkit (Islands-style).

## FOCUS
**Phase F — Map Pipeline by Layers (Core + vertical slice)**  
We are starting **F3 only** (next milestone). Ignore dungeon strategies except for baseline patterns (Lantern GPU packing style, determinism tests discipline).

## CURRENT STATE (DONE)
**F0–F2 are implemented and passing tests.**

- Runtime: `MapContext2D` (stable array registries), `MapInputs`, `MapTunables2D`, `MapIds2D`, `IMapStage2D`, `MapPipelineRunner2D`
- F2: `Stage_BaseTerrain2D` produces:
  - Field: `Height` (0..1)
  - Layers: `Land` (threshold) and `DeepWater` (border-connected flood fill)
- Operators: `MaskFloodFillOps2D` exists and is deterministic + OOB-safe.
- Lantern: `PCGMapVisualization` displays a selected `MapLayerId`.
- Tests: determinism tests + goldens exist for trivial pipeline and F2 pipeline.

## NON-NEGOTIABLE CONSTRAINTS
- Determinism: seed-driven; no GUID shuffles; no nondeterministic iteration sources (HashSet/Dictionary traversal) in core logic.
- Grid-first authoritative state: `MaskGrid2D` / `ScalarField2D`.
- Adapters-last: Tilemaps/Textures/Mesh are outputs only.
- Incremental + test-gated: every new stage/operator must ship with EditMode determinism tests + golden hash gates.
- OOB-safe neighbors: OOB counts as OFF; never read/write outside arrays.

---

# GOAL (THIS MILESTONE): Phase F3
Implement **Hills + Topology** and integrate **Islands.Noise using Jobs** (robustly) without changing runner contracts.

## F3.0 — Append topology layer IDs
**Modify:** `Runtime/Layout/Maps/MapIds2D.cs`

Append at end of `MapLayerId` (stable ordering) and update `COUNT`:
- `HillEdgeL1`, `HillInteriorL1`, `HillEdgeL2`, `HillInteriorL2`

## F3.1 — Add MaskTopologyOps2D + tests
**New:** `Runtime/Operators/MaskTopologyOps2D.cs`  
**New tests:** `Tests/EditMode/Operators/MaskTopologyOps2DTests.cs`

Minimal API:
- `ComputeEdgeAndInterior8(ref MaskGrid2D src, ref MaskGrid2D edge, ref MaskGrid2D interior)`

Rules:
- scan order y→x
- fixed 8-neighbor order (e.g., N, NE, E, SE, S, SW, W, NW)
- OOB neighbors count as OFF
- no HashSet/Dictionary

## F3.2 — Islands.Noise bridge operator (Jobs)
**New:** `Runtime/Operators/IslandsNoiseFieldOps2D.cs`

Responsibility: fill a `ScalarField2D` (or temp buffer) using `Noise.Job<T>.ScheduleParallel(...)`, then `Complete()` inside stage.

Rules:
- noise seed must be derived from `MapInputs.seed` + constant `stageSalt` (do NOT consume `ctx.Rng` to get a seed)
- apply quantization before threshold-to-mask to reduce FloatMode.Fast drift

## F3.3 — Stage_Hills2D (L1/L2 + topology)
**New:** `Runtime/Layout/Maps/Stages/Stage_Hills2D.cs`  
**New tests:** `Tests/EditMode/Maps/StageHills2DTests.cs`

Inputs:
- `Height` field
- `Land`, `DeepWater` layers

Outputs:
- `HillsL1`, `HillsL2`
- `HillEdgeL1`, `HillInteriorL1`, `HillEdgeL2`, `HillInteriorL2` (computed via `MaskTopologyOps2D`)

Invariants:
- `HillsL2 ⊆ HillsL1`
- hills never on water: gate by `Land`
- topology layers are subsets of their source hills layer

Tests:
- determinism: same inputs => same hashes
- goldens:
  - `HillsL1.SnapshotHash64()`
  - `HillsL2.SnapshotHash64()`
  - (recommended) `HillEdgeL1`, `HillEdgeL2` goldens

## F3.4 — Pipeline golden (F2+F3) + Lantern update
**New tests:** `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF3Tests.cs`
- stages: `[Stage_BaseTerrain2D, Stage_Hills2D]`
- lock goldens (per-layer preferred for debug clarity)

**Modify:** `Samples/PCGMapVisualization.cs`
- allow viewing hills/topology layers
- optional: expose minimal HillsConfig knobs (thresholds + noise settings), keep it lean

---

# WHAT I WANT FROM YOU (THE MODEL) NOW
1) Provide a detailed incremental implementation plan for F3.0–F3.4:
   - exact new/modified files + folders
   - exact APIs/method signatures
   - ordering of work + acceptance gates per step
2) Provide code for:
   - `MaskTopologyOps2D.cs` + tests
   - `IslandsNoiseFieldOps2D.cs`
   - `Stage_Hills2D.cs` + tests
   - `MapPipelineRunner2DGoldenF3Tests.cs`
3) Call out determinism hazards (FloatMode.Fast, threshold drift, seed derivation) and how you avoided them.

Keep changes minimal. Do not refactor unrelated systems. Runner contract stays the same (Jobs complete within stage).

