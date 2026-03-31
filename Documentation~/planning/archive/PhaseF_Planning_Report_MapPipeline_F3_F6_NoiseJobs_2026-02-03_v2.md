> **Status:** Historical support / absorbed planning source  
> **Absorbed into:** `Documentation~/planning/active/PCG_Roadmap.md`  
> **Authority note:** This file remains in the frozen snapshot for planning traceability only.

# Phase F Planning Report — Map Pipeline by Layers (F3–F6) + Islands.Noise (Jobs)

**Project:** Islands-style PCG Toolkit (Burst/SIMD-ready, deterministic)  
**Phase:** F (continuación después de F2)  
**Date:** 2026-02-03  
**Prepared for:** Implementación incremental (test-gated) del vertical slice de Map Pipeline por capas

---

## 0) Phase F Goal (resto de la fase después de F2)

## Plan delta (alignment with F3 incremental roadmap)
This revision only adds what was missing for full consistency:
- explicit topology layers (Edge/Interior for L1 and L2)
- `MaskTopologyOps2D` operator + tests
- `Stage_Hills2D` outputs include topology layers and use `Islands.Noise` Jobs (seed-derived, quantized) instead of `ctx.Rng.NextFloat()`
- explicit F3 pipeline golden + lantern update for hills config/layers

Completar el vertical slice del **Map Pipeline by Layers** agregando los stages F3–F6:

- **F3:** Hills + topology (**incluye milestone de integración con `Islands.Noise` usando Jobs**)
- **F4:** Shore + ShallowWater (mínimo)
- **F5:** Vegetation + fixups (y opcional Moisture)
- **F6:** Paths + Stairs + Placement (sin GameObjects; adapters-last)

Todo debe cumplir:

- **Determinismo:** seed-driven; sin iteración no estable en el core.
- **Grid-first:** authoritative state en `MaskGrid2D`/`ScalarField2D`.
- **OOB-safe:** vecinos OOB cuentan como OFF; sin lecturas/escrituras fuera de rango.
- **Test-gated:** EditMode tests + goldens (por-stage + pipeline).

---

## 1) Global Acceptance Criteria (aplica a cada stage F3–F6)

### Runtime
- Un stage por archivo con entrypoint:
  - `public sealed class Stage_X2D : IMapStage2D`
  - `public void Execute(ref MapContext2D ctx, in MapInputs inputs)`
- Stage usa `ctx.EnsureLayer(...)` / `ctx.EnsureField(...)` (clear discipline consistente).
- Sin `HashSet` traversal / sin `Dictionary` traversal como fuente de orden.
- Si hay RNG: SOLO `ctx.Rng` (y scan order fijo `y→x`).

### Noise (Islands.Noise)
Si un stage usa `Islands.Noise`:
- `Noise.Settings.seed` se deriva de `inputs.seed` + un *salt* constante del stage
  - **no** consumir `ctx.Rng` para “obtener una seed”, para evitar drift de streams.
- Antes de generar máscaras por threshold, aplicar **cuantización** (pasos fijos) para estabilidad.

### Lantern
- `PCGMapVisualization` debe poder mostrar cualquier `MapLayerId` generado.
- Cambiar seed o tunables debe reflejarse inmediatamente (dirty tracking).

### Tests
- EditMode determinism test: misma seed/tunables ⇒ mismo `SnapshotHash64()`.
- Golden gate por layer “principal” del stage (y por layer secundaria si aplica).
- Golden gate de pipeline para F3/F4/F5/F6 (en archivos nuevos, manteniendo goldens previos como drift alarms).

---

## 2) Phase F Modules & File Actions Overview (F3–F6)

### New (Runtime — Operators)
- `Runtime/Operators/IslandsNoiseFieldOps2D.cs`
  - Bridge: `Islands.Noise` (Jobs) → `ScalarField2D` (y opcional buffer auxiliar)
- (Si no existe aún) `Runtime/Operators/MaskNeighborOps2D.cs`
  - Vecinos OOB-safe (4 y 8)

### New (Runtime — Stages)
- `Runtime/Layout/Maps/Stages/Stage_Hills2D.cs`
- `Runtime/Layout/Maps/Stages/Stage_Shore2D.cs`
- `Runtime/Layout/Maps/Stages/Stage_Vegetation2D.cs`
- `Runtime/Layout/Maps/Stages/Stage_Paths2D.cs` (o `Stage_Navigation2D.cs`)
- (Opcional para claridad) `Runtime/Layout/Maps/Stages/Stage_Moisture2D.cs`

### Modify (Runtime — Config)
- `Runtime/Layout/Maps/MapTunables2D.cs` (mínimo)
  - agregar knobs para F3/F4/F5/F6 (thresholds y densidades) con clamp determinista

### Modify (Samples)
- `Samples/PCGMapVisualization.cs`
  - no necesita refactor; solo asegurar:
    - selector de layer muestra las nuevas capas
    - inspector expone tunables relevantes (si se decide exponerlos)

### New / Update (Tests)
- `Tests/EditMode/Maps/StageHills2DTests.cs`
- `Tests/EditMode/Maps/StageShore2DTests.cs`
- `Tests/EditMode/Maps/StageVegetation2DTests.cs`
- `Tests/EditMode/Maps/StagePaths2DTests.cs`
- `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF3Tests.cs`
- `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF4Tests.cs`
- `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF5Tests.cs`
- `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF6Tests.cs`

---

## 3) F3 — Hills + Topology + Islands.Noise (Jobs)

### Objective
Generate hills masks and their topology layers:
- `HillsL1`, `HillsL2`
- `HillEdgeL1`, `HillInteriorL1`, `HillEdgeL2`, `HillInteriorL2`

Also introduce a robust `Islands.Noise` integration using Jobs **inside the stage** (runner unchanged).

### F3.0 — Append topology layer IDs
**Modify file:** `Runtime/Layout/Maps/MapIds2D.cs`

- Append (end of enum; stable ordering):
  - `HillEdgeL1`, `HillInteriorL1`, `HillEdgeL2`, `HillInteriorL2`
- Update `COUNT`.

**Acceptance gate:** project compiles; no behavior change yet.

### F3.1 — Add MaskTopologyOps2D + tests
**New file:** `Runtime/Operators/MaskTopologyOps2D.cs`

**Responsibility:** deterministically compute `edge` and `interior` masks from a source mask.

**API (minimal):**
- `public static void ComputeEdgeAndInterior8(ref MaskGrid2D src, ref MaskGrid2D edge, ref MaskGrid2D interior)`

**Determinism notes:**
- Scan order fixed: y→x.
- Neighbor order fixed (8-neighbor): e.g. N, NE, E, SE, S, SW, W, NW.
- OOB neighbors count as OFF.
- No HashSet/Dictionary.

**Tests:** `Tests/EditMode/Operators/MaskTopologyOps2DTests.cs`
- small handcrafted patterns (single pixel, line, solid block, hollow ring)
- verify invariants: `edge ∪ interior == src`, `edge ∩ interior == ∅`

### F3.2 — Stage_Hills2D (L1/L2 + topology)
**New file:** `Runtime/Layout/Maps/Stages/Stage_Hills2D.cs`

**Inputs:** `Height` field, `Land`, `DeepWater` layers (from F2)

**Outputs:**
- `HillsL1`, `HillsL2`
- `HillEdgeL1`, `HillInteriorL1`, `HillEdgeL2`, `HillInteriorL2`

**Noise integration (Jobs):**
- Use bridge operator `IslandsNoiseFieldOps2D` to produce a bias field (or temp buffer).
- Seed derivation: `noiseSeed = f(inputs.seed, stageSalt)`; do **not** consume `ctx.Rng`.
- Quantize before thresholding to masks.

**Invariants:**
- `HillsL2 ⊆ HillsL1`
- hills never in water (gate by `Land`).
- topology layers subset of their source hills layer.

**Tests:** `Tests/EditMode/Maps/StageHills2DTests.cs`
- determinism test: run twice, same hashes.
- goldens:
  - `HillsL1.SnapshotHash64()`
  - `HillsL2.SnapshotHash64()`
  - (optional, recommended) `HillEdgeL1`, `HillEdgeL2` goldens.

### F3.3 — Pipeline golden (F2+F3)
**New file:** `Tests/EditMode/Maps/MapPipelineRunner2DGoldenF3Tests.cs`
- stages: `[ new Stage_BaseTerrain2D(), new Stage_Hills2D() ]`
- golden: combine hashes or assert per-layer goldens (prefer obvious per-layer asserts).

### F3.4 — Lantern update
**Modify:** `Samples/PCGMapVisualization.cs`
- allow selecting + viewing hills/topology layers
- expose minimal `HillsConfig` knobs (thresholds + noise settings) *only if helpful*; keep it lean.

**Visual acceptance:**
- `HillsL1/L2` render as coherent regions on Land.
- `HillEdge*` outlines hills boundaries.
- `HillInterior*` fills the rest.


## 4) F4 — Shore + ShallowWater (mínimo)

### Objective
Generar `ShallowWater` como un “anillo” determinista alrededor de `Land` pero solo en `DeepWater` (para excluir lagos interiores).

**New file:** `Runtime/Layout/Maps/Stages/Stage_Shore2D.cs`

**Algorithm (mínimo)**
- Para cada celda water (`Land` OFF):
  - si `DeepWater` ON y tiene algún vecino 4 o 8 con `Land` ON ⇒ `ShallowWater` ON
  - si no, OFF

**Invariantes**
- `ShallowWater ∩ Land == ∅`
- `ShallowWater ⊆ DeepWater` (si decides forzarlo)

**Tests**
- golden de `ShallowWater`
- pipeline golden F4 (BaseTerrain + Hills + Shore)

---

## 5) F5 — Vegetation + fixups (y opcional Moisture)

### Objective
Generar `Vegetation` (mask) con reglas simples:
- Solo en `Land`
- Nunca en `DeepWater` ni `ShallowWater`
- (Opcional) dependiente de `Moisture` (Noise + distancia a agua) y/o excluye `HillEdges`

**New files**
- `Stage_Vegetation2D.cs`
- (Opcional) `Stage_Moisture2D.cs` (si decides persistir Moisture en `MapFieldId`)

**Determinismo**
- Si hay sampling probabilístico:
  - scan order fijo y→x
  - `ctx.Rng` usado de forma consistente (una llamada por celda candidata)
  - evitar “consumo condicional” difícil de razonar (o explicitarlo)

**Tests**
- golden de `Vegetation`
- invariantes: `Vegetation ∩ (DeepWater ∪ ShallowWater) == ∅`
- pipeline golden F5

---

## 6) F6 — Paths / Stairs / Placement (sin GameObjects)

### Objective
Crear navegación mínima y outputs de placement headless:
- `Walkable` (mask)
- `Paths` (mask)
- `Stairs` (mask; cruce de edges o transición hills)
- (Opcional) `SpawnMask` o lista de spawns en metadata

**New file:** `Stage_Paths2D.cs`

**Algorithm (mínimo)**
- `Walkable = Land - (DeepWater ∪ ShallowWater)` y opcionalmente excluir `HillsL2`/`HillEdges`
- Paths:
  - BFS/A* determinista con tie-break fijo (orden de vecinos constante)
  - seleccionar start/goal determinista (por ejemplo, extremos opuestos del bounding box de Land)
- Stairs:
  - marcar puntos donde path cruza un edge (si hay `HillEdges`) o transición `HillsL1`→no-hill

**Tests**
- goldens de `Walkable`/`Paths`/`Stairs` (al menos `Paths`)
- pipeline golden F6

---

## 7) “Milestone” nuevo incluido: Islands.Noise (Jobs) robusto

Este milestone se considera “completo” cuando:
- Existe `IslandsNoiseFieldOps2D` y un stage real (F3) lo usa.
- El stage produce máscaras con goldens estables (gracias a cuantización).
- El runner no tuvo que cambiar (Jobs completan dentro del stage).
- Lantern muestra `HillsL1/HillsL2` correctamente.

---

## 8) Minimal file set sugerido (≤10) para rehidratar F3–F6

1) `Islands_PCG_Roadmap_Integrated_With_MapLayers_v0.2.3_2026-02-03.md`  
2) `PhaseF_Planning_Report_MapPipeline_F3_F6_NoiseJobs_2026-02-03.md`  
3) `Runtime/Layout/Maps/MapContext2D.cs`  
4) `Runtime/Layout/Maps/MapPipelineRunner2D.cs`  
5) `Runtime/Layout/Maps/MapTunables2D.cs`  
6) `Runtime/Layout/Maps/Stages/Stage_BaseTerrain2D.cs`  
7) `Runtime/Operators/MaskFloodFillOps2D.cs`  
8) `Runtime/Grids/MaskGrid2D.cs` + `MaskGrid2D.Hash.cs`  
9) `Samples/PCGMapVisualization.cs`  
10) `noise.md` (o docs equivalentes) + el archivo `Noise.cs` base

