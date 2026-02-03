# Islands.PCG — Roadmap: “Map Pipeline by Layers” Integration (v0.1.0)
**Date:** 2026-01-29  
**Purpose:** plan how to integrate a **layer-based Map pipeline** (generalized from the legacy Island tilemap pipeline) into **Islands.PCG**, while staying inside the **Islands.PCG contracts** (grid-first, deterministic, adapters-last).

---

## 0) Starting point (where we are)
- Phase A–D DONE (grids/fields, lantern, SDF ops, first grid-only dungeon strategies).
- Phase E NEXT / IN PROGRESS (port remaining dungeon strategies: Corridor First, Room First (BSP), Room Grid).
- Legacy “Map/Island” generator exists as a Step chain over `TileType[,]` + sets, with known determinism pitfalls (e.g. HashSet selection order).

---

## 1) Goal statement (integration target)
Introduce a **general Map pipeline** in Islands.PCG that:
1) uses a **MapContext** holding multiple **layers** (masks/fields)  
2) runs deterministic **MapStages** that write only to grids/fields  
3) uses **Adapters** (optional) to output Tilemap/Texture/Mesh/spawns  
4) preserves Islands.PCG style rules:
   - no Tilemaps/GameObjects in core logic  
   - deterministic snapshot gates (`SnapshotHash64`) per stage/layer  
   - “math → grid → ops → layout → adapters”

---

## 2) Strategy: add a new workstream without blocking Phase E
Keep Phase E focused on dungeon ports, but start a parallel “Map Layers” stream that reuses the same core toolbox:
- `MaskGrid2D`, `ScalarField2D`, SDF→Scalar→Mask ops, `MaskRasterOps2D`, lantern upload path.

When Phase E finishes, Map Layers can “promote” to a first-class pipeline slice.

---

## 3) Proposed new phase in the roadmap
### Phase I — Map Pipeline by Layers (Core + Island vertical slice)
**Rationale:** existing roadmap has Phase F/G/H as later post-process + adapters + perf.  
Map Layers is a layout-level system (between “ops” and “adapters”), so it deserves its own phase.

**Exit criteria (Phase I)**
- A Map pipeline can generate at least a multi-layer output (e.g., Land/Water/Hills/Paths) headlessly.
- A dedicated lantern (`PCGMapVisualization`) can visualize each layer.
- Each stage has snapshot tests + at least one golden hash gate for a full pipeline preset.

---

# Phase I — Detailed plan (steps I0–I6)

## I0 — Define the layer stack + context (no algorithms yet)
**Objective:** create the “SSoT containers” that will replace legacy `GenerationData` in Islands.PCG style.

**Deliverables**
- `MapContext2D` that owns:
  - named `MaskGrid2D` layers (initial MVP):
    - `Land`, `DeepWater`, `ShallowWater`, `HillsL1`, `HillsL2`, `Paths`, `Stairs`, `Vegetation`, `Walkable`
  - optional `ScalarField2D` fields:
    - `Height`, `Moisture` (MVP can start with just `Height`)
  - metadata: `seed`, `configId`, counts (ones per layer)

- `IMapStage2D` contract:
  - `void Execute(ref MapContext2D ctx, in MapInputs inputs);`

**Acceptance**
- Context alloc/dispose is correct (no leaks) and deterministic.
- “Empty pipeline” produces stable hashes for cleared layers.

---

## I1 — Add a dedicated Map lantern (“PCGMapVisualization”)
**Objective:** keep the existing “generate → pack → upload” loop, but support multiple layers like a debugger.

**Deliverables**
- `PCGMapVisualization` (Samples):
  - dropdown: `LayerViewMode` (Land/Water/Hills/Paths/etc.)
  - same upload path as existing lanterns.

**Acceptance**
- Any layer can be visualized within seconds.
- A single preset + seed reliably reproduces the same visual result.

---

## I2 — MVP terrain: Land + DeepWater (parity with legacy base island step)
**Objective:** reproduce the *shape* of the legacy base island step, but in grids.

**Implementation plan**
- Build a scalar “radial falloff” (0 center → 1 edge) OR use SDF circle distance.
- Combine with perlin height noise and threshold to `Land`.
- Compute `DeepWater` as the water component connected to borders (flood-fill).

**New operators likely needed**
- `PerlinToScalarOps2D.Fill(...)` (perlin → ScalarField2D)
- `FloodFillOps2D.ConnectedToBorder(...)` (stable BFS/queue)
- minimal `ScalarFieldOps2D` helpers (mul/add/smoothstep/clamp)

**Acceptance**
- Determinism: `Land` and `DeepWater` hashes stable for seed/config.

---

## I3 — Hills + hill topology layers (edges/interiors/components)
**Objective:** migrate hill logic (hills + edge/interior + grouping) to masks.

**Deliverables**
- `HillsStage2D`:
  - generate `HillsL1`, `HillsL2` using thresholds on `Height`
- `HillTopologyStage2D`:
  - `HillEdgesL1/L2` and `HillInteriorL1/L2`
  - optional connected components (stable ordering by min cell index)

**Acceptance**
- Hash tests for each layer.
- Invariants:
  - `HillsL2 ⊆ HillsL1` (if intended)
  - `Edges ∪ Interior == Hills` and `Edges ∩ Interior == ∅`

---

## I4 — Shore + ShallowWater bands (ties into Phase F later)
**Objective:** replicate shoreline + shallow sea rings.

**Two-step approach**
1) Minimal banding now: `ExpandEdgeMaskOps2D` (N-layer growth) or ring logic
2) Upgrade later using morphology (Phase F): Dilate/Erode/Open/Close

**Acceptance**
- Bands appear around coast, deterministic hashes.

---

## I5 — Vegetation layers (Grass/Trees) + fixups as pure masks
**Objective:** mirror vegetation steps + fixups, but as layered masks.

**Deliverables**
- `VegetationStage2D`: `GrassMask`, `TreeMask` from noise thresholds (with exclusions)
- `FixupsStage2D`: clear vegetation where policy requires; edge fixes become masks

**Acceptance**
- Policy invariants hold (e.g., no trees in deep water).
- Hash tests for vegetation layers.

---

## I6 — Paths/Stairs/Walkable + placement metadata (no GameObjects in core)
**Objective:** replace path/placement/player steps with pure outputs.

**Deliverables**
- `PathStage2D`: A* or BFS over `Walkable` → `Paths`
- `StairsStage2D`: detect hill crossings along paths → `Stairs`
- `PlacementStage2D`: `CandidateSpawn` list (NativeList<int2>) or `SpawnMask`
- `PickSpawnStage2D`: pick one deterministically (stable ordering + seeded rng)

**Acceptance**
- Connectivity smoke tests (if required by rules).
- Determinism: path hash + picked spawn stable.

---

# 4) Where this connects to existing roadmap phases

## Phase F — Morphology + walls bitmasks (upgrade quality)
- Replace band hacks with morphology.
- Optionally compute “coastline walls/edges” using neighbor bitmasks (same concept as dungeon walls).

## Phase G — Extract + adapters (practical outputs)
- Map layers → `Texture2D` (fast preview / authoring)
- Map layers → Tilemap adapter (editor convenience)
- Map layers → Mesh (contours / marching squares)

## Phase H — Burst/SIMD upgrades (only after parity locks)
- Optimize hot loops only after snapshot hashes + golden presets are stable.

---

# 5) MVP vs “Full parity”
MVP is **not** “pixel-identical” to the legacy tilemap generator.
MVP is:
- layers exist
- deterministic + testable
- visually plausible via lantern
- adapters can consume outputs later

If you later want “legacy parity”, add a parity harness:
- compare coarse metrics (land %, hill %, coast length, component counts)
- compare hashes after normalizing representation (not tile sprites)

---

## 6) Suggested first concrete integration slice (minimal pain)
1) I0 Context + I1 Map lantern  
2) I2 Base Land + DeepWater  
3) I3 Hills + Topology  
4) I6 Spawn picking (mask-only)  
5) Then bring in I4/I5 quality layers
