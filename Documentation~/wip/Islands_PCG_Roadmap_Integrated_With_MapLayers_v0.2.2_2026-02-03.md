# Islands.PCG — Roadmap Integrado (incluye “Map Pipeline by Layers”) v0.2.2
**Fecha:** 2026-02-03  
**Contexto:** Estamos por cerrar **Phase E** (port de estrategias de dungeons). Todo lo posterior se reestructura para incorporar un **pipeline general de Map por capas**, alineado a los contratos del SSoT (grid-first, determinismo, adapters-last).

---

## Resumen de cambios (post-Phase E)
- Insertamos una fase nueva inmediatamente después de E:
  - **Phase F — Map Pipeline by Layers (Core + vertical slice)** ✅ *nuevo*
- Re-etiquetamos lo que venía después:
  - **Phase F (antes)** “Morphology + walls bitmasks” → **Phase G**
  - **Phase G (antes)** “Extract + adapters” → **Phase H**
  - **Phase H (antes)** “Burst/SIMD upgrades” → **Phase I**

> Motivo: el “Map pipeline” es **layout-level**, y necesita existir **antes** de morphology/adapters/perf para poder beneficiarse de esas fases.

---

## Phase A — Fields/Grids core ✅ DONE
(igual que roadmap actual)

## Phase B — Mask raster + debug surfaces ✅ DONE
(igual que roadmap actual)

## Phase C — SDF pipeline + boolean ops ✅ DONE
(igual que roadmap actual)

## Phase D — First grid-only dungeon strategies ✅ DONE
(igual que roadmap actual)

---

## Phase E — Port remaining dungeon strategies ▶️ NEXT / IN PROGRESS
**Goal:** portar a grid-only:
- Corridor First
- Room First (BSP)
- Room Grid (layout-only slice)

**Exit criteria**
- Lantern mode por estrategia + snapshot tests (+ golden hash opcional).
- Estrategias viven 100% sobre `MaskGrid2D` (sin Tilemaps).

---

# Phase F — Map Pipeline by Layers (Core + vertical slice) ▶️ IN PROGRESS (F0–F2 DONE; F3 NEXT)
**Goal:** introducir un pipeline **general de mapas** basado en **capas (layers)** sobre grids/fields, con stages deterministas y visualización tipo lantern.

### Outcomes (alto nivel)
- **Modelo de datos**: `MapContext2D` (SSoT único) con:
  - layers `MaskGrid2D` (MVP): `Land`, `DeepWater`, `ShallowWater`, `HillsL1`, `HillsL2`, `Paths`, `Stairs`, `Vegetation`, `Walkable`
  - fields `ScalarField2D` (MVP): `Height` (y opcional `Moisture`)
  - metadata: seed/config, contadores, outputs de placement (sin GameObjects)
- **Contrato de stages**: `IMapStage2D.Execute(ref MapContext2D ctx, in MapInputs inputs)`
- **Map lantern**: `PCGMapVisualization` para ver capa por capa.
- **Vertical slice “Island-like”** (pero generalizable): stages que reproduzcan el flujo conceptual del generador legacy, sin Tilemaps.

### Sub-steps recomendados (F0–F6)
- **F0 — Context + contracts** ✅ DONE (2026-02-02)
  - creado: `MapIds2D`, `MapTunables2D`, `MapInputs`, `MapContext2D`, `IMapStage2D`, `MapPipelineRunner2D`
  - agregado: EditMode determinism + golden tests (trivial pipeline gate)
- **F1 — Map lantern** ✅ DONE (2026-02-02)
  - `PCGMapVisualization` (mask layers) con selector de `MapLayerId` + upload `_Noise` (mismo contrato que el dungeon lantern)
- **F2 — Base terrain (Land + DeepWater)** ✅ DONE (2026-02-03)
  - `Stage_BaseTerrain2D`: Height (0..1) + threshold → `Land` + flood fill → `DeepWater`
  - `MaskFloodFillOps2D`: flood fill estable y OOB-safe
  - Lantern micro-step: tunables editables en `PCGMapVisualization` (islandRadius01, waterThreshold01, smoothFrom/To)
  - Gates: goldens de `Land`/`DeepWater` + golden de pipeline F2 (manteniendo el trivial golden como drift alarm)
- **F3 — Hills + topology** ▶️ NEXT
  - `Stage_Hills2D` (nombre tentativo): genera `HillsL1`/`HillsL2` desde `Height` + bias/ruido
  - Topology mínima determinista: `HillEdges`/`HillInterior` (8-neighbor, OOB-safe)
  - Invariantes + tests: `HillsL2 ⊆ HillsL1`, exclusión con agua, goldens por layer
- **F4 — Shore + ShallowWater (versión mínima)**
  - banding simple ahora; luego se “mejora” en Phase G con morphology
- **F5 — Vegetation + fixups**
  - masks de trees/grass + reglas de exclusión (no en water, no en hill edges, etc.)
- **F6 — Paths/Stairs/Placement (sin GameObjects)**
  - paths (A* o BFS) sobre `Walkable`
  - stairs como máscara de “cruce de edge”
  - placement como `SpawnMask` o `NativeList<int2>` + selección determinista de spawn

### Exit criteria (Phase F)
- Pipeline headless: genera todas las capas MVP sin Unity view systems.
- `PCGMapVisualization` puede mostrar cualquier capa.
- Cada stage tiene snapshot tests; el pipeline completo tiene al menos 1 golden hash gate.
- Invariantes mínimas validadas (ejemplos):
  - `HillsL2 ⊆ HillsL1` (si aplica)
  - `Vegetation ∩ DeepWater == ∅`
  - `Stairs ⊆ Paths` (o regla equivalente)
  - selección de spawn determinista por seed

---

# Phase G — Morphology + neighborhood masks (dungeons + maps) ⏳ LATER (antes “Phase F”)
**Goal:** post-proceso engine-grade reutilizable para **cualquier** pipeline que emita masks (dungeons o maps).

### Outcomes
- Morphology sobre `MaskGrid2D` (dilate/erode/open/close).
- Neighbor bitmasks (ej. walls/coastline/edges) + LUT mapping (adapter-ready).
- “Quality upgrades” para Map:
  - shore/shallow rings con morphology en vez de band hacks
  - suavizado de ruido / limpieza de artefactos

### Exit criteria
- Determinismo + snapshot tests.
- Las salidas son “adapter-ready” (sin depender de render frameworks).

---

# Phase H — Extract + adapters (dungeons + maps) ⏳ LATER (antes “Phase G”)
**Goal:** convertir outputs del core en artefactos utilizables **sin acoplar core a Unity view systems**.

### Outcomes
- Mask/field → `Texture2D` (debug + workflows art).
- Mask → Tilemap adapter (capa adapter estricta).
- Mask/field → Mesh (marching squares / contours).
- (Opcional) “Spawn adapter” (instanciación) que consuma metadata del pipeline, sin contaminar stages.

### Exit criteria
- Adapters opcionales; core corre headless.
- Adapters consumen outputs estables; core no depende de Tilemaps/Renderers.

---

# Phase I — Burst/SIMD upgrades ⏳ LATER (antes “Phase H”)
**Goal:** optimizar hot loops sin cambiar comportamiento.

### Outcomes
- Burst jobs donde importe (y solo donde importe).
- Layout/memoria para SIMD/word packing.
- Mantener snapshot/golden parity.

### Exit criteria
- Mejoras de performance demostrables en workloads representativos.
- Gates de determinismo intactos.

---

## Notas de integración con el sistema legacy (Map Generation SSoT)
- El legacy pipeline sirve como “spec conceptual” (qué capas existen y qué reglas deben cumplirse),
  pero el objetivo en Islands.PCG es **masks/fields + adapters**, evitando:
  - iteración no estable (HashSet ordering)
  - uso de `UnityEngine.Random` sin RNG explícito
  - GameObjects dentro del core

---

## Próximos pasos concretos (cuando cierres Phase E)
1) ✅ **F0 + F1** completados (Context + Map lantern).
2) ▶️ Hacer **F2** (Land + DeepWater) y bloquear determinismo con hash.
3) Agregar **F3** (Hills + topology).
4) Recién después, sumar F4–F6.

