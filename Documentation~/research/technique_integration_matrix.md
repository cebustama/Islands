# Technique Integration Matrix

Status: Active reference  
Date: 2026-04-06  
Authority: Planning reference — not implementation truth.  
Home: `Documentation~/research/technique_integration_matrix.md`

Compact mapping of PCG technique families to Islands roadmap phases, implementation
status, and open decisions. Derived from the cross-check analysis (6 games × ~30
techniques) and the PCG technique reports (passes 1a–6b). Refer to the PCG Roadmap
and phase design documents for implementation-depth detail.

**Status key:** DONE = implemented and test-gated. PLAN = roadmapped phase. PARTIAL =
partially implemented or partially roadmapped. NONE = not roadmapped.
Design doc column references files under `planning/active/design/`.

---

## 1. Noise Primitives

| Technique | Islands Status | Consuming Phase | Notes |
|-----------|---------------|-----------------|-------|
| Coordinate hashing (SmallXXHash) | DONE | F0+ (all stages) | Core infrastructure. Fully SIMD-capable. |
| Perlin noise | DONE | F3, F5 (via MapNoiseBridge2D) | Primary noise type for hills and vegetation. |
| Simplex noise | DONE | Available, not consumed | Available for future stages if needed. |
| Worley / Voronoi noise | DONE | Available for Phase J, K | Full distance metric + value function family. |
| Value noise | DONE | Not consumed | Implemented for completeness; grid artifacts make it unsuitable for terrain. |

No open decisions. Noise primitives are implemented and stable.

---

## 2. Noise Composition

Detailed design in [`Noise_Composition_Improvements_Roadmap.md`](design/Noise_Composition_Improvements_Roadmap.md).

| Technique | Islands Status | Consuming Phase | Design Doc | Notes |
|-----------|---------------|-----------------|------------|-------|
| fBm (fractal octave summation) | DONE | F3, F5, future M/L/W | — | `Noise.GetFractalNoise<N>()`. Solid. |
| Power redistribution (N1) | PLAN | J2 | Noise Comp. Roadmap §3 | `pow(height, exp)`. Trivial. Implement any time. |
| Spline remapping (N2) | PLAN | M, W, general | Noise Comp. Roadmap §4 | Highest impact-per-effort of the three. Piecewise linear or cubic curve. |
| Ridged multifractal (N3) | PARTIAL | K, W | Noise Comp. Roadmap §5 | Turbulence wrapper exists; full Musgrave inter-octave feedback not implemented. **Unresolved:** implement as fractal mode in noise runtime (Option A) vs. standalone operator (Option C). Resolve when Phase K or W begins. |
| Domain warping (simple) | DONE | F2b | — | Two coarse noise grids for island shape distortion. |
| Domain warping (full Quilez) | NONE | J, K, W (potential) | — | fBm-into-fBm warping. Not urgent; simple warp covers current needs. |
| Turbulence (abs(noise)) | DONE | Available, not consumed | — | Used only as building block for N3. |
| Noise competition (per-type highest-wins) | NONE | M2 (potential) | — | RimWorld's stone-type pattern. Applicable for sub-biome variation. |

**Open decisions:** N3 implementation approach (Option A vs C) — deferred to Phase K/W.

---

## 3. Terrain & Elevation

| Technique | Islands Status | Consuming Phase | Notes |
|-----------|---------------|-----------------|-------|
| Heightmap-based terrain | DONE | F2 | `Height` scalar field. Foundational. |
| Island / continent mask | DONE | F2, F2b, F2c | Ellipse + domain warp + arbitrary shape input. |
| Height redistribution | PLAN | J2 | `pow(height01, exp)` post-processing. See Noise Composition Roadmap N1. |
| Elevation smoothing | NONE | — | Not planned. May be useful post-erosion; evaluate if needed. |
| Erosion simulation | NONE | Pre-Phase L (recommended) | **Only non-roadmapped technique from the top-12 priorities.** DF-style agent erosion. Would need a new `Stage_Erosion2D` between G and L. Significant pipeline reordering implications. |

**Open decisions:** Whether to roadmap erosion as a dedicated phase. Recommended before Phase L.

---

## 4. Climate & Biomes

| Technique | Islands Status | Consuming Phase | Design Doc | Notes |
|-----------|---------------|-----------------|------------|-------|
| Temperature field | PLAN | M.1 | Phase_M_Design.md | Latitude proxy + elevation lapse rate + CoastDist moderation + noise. |
| Moisture field | PLAN | M.2 | Phase_M_Design.md | CoastDist + FlowAccumulation (optional, from Phase L) + noise. |
| Whittaker biome classification | PLAN | M.3 | Phase_M_Design.md | Temperature × Moisture lookup → integer biome ID. `BiomeDef[]` table. |
| Biome-aware vegetation | PLAN | M2.a | Phase_M2_Design.md | Refactor `Stage_Vegetation2D` with per-biome density from `BiomeDef`. |
| Contiguous region detection | PLAN | M2.b | Phase_M2_Design.md | CCA on biome field → `MapFieldId.NamedRegionId`. |
| Biome scoring / competition | NONE | — | — | RimWorld-style BiomeWorker. Not needed if Whittaker lookup is sufficient. |
| Orographic precipitation | NONE | — | — | DF only. Would enrich moisture but adds significant complexity. Deferred. |

**Open decisions:** None — all design decisions for M and M2 are resolved.

---

## 5. Hydrology

| Technique | Islands Status | Consuming Phase | Design Doc | Notes |
|-----------|---------------|-----------------|------------|-------|
| Priority-Flood depression filling | PLAN | L | Phase_L_Design.md | Barnes 2014 algorithm. Prerequisite for flow routing. |
| D8 flow directions | PLAN | L | Phase_L_Design.md | Steepest-downslope neighbor. Intermediate data, not persisted. |
| Flow accumulation | PLAN | L | Phase_L_Design.md | `MapFieldId.FlowAccumulation` scalar field. Consumed by Phase M for moisture. |
| River mask extraction | PLAN | L | Phase_L_Design.md | `MapLayerId.Rivers` derived by thresholding FlowAccumulation. |
| Lake detection | PLAN | L | Phase_L_Design.md | `MapLayerId.Lakes` via connected-component analysis on non-land, non-DeepWater cells. |

**Open decisions:** None — river representation and lake modeling resolved (Roadmap Decisions 2, 3).

---

## 6. Region Partitioning

| Technique | Islands Status | Consuming Phase | Notes |
|-----------|---------------|-----------------|-------|
| Voronoi region partitioning (local) | PLAN | J | Within-map biome regions. Uses existing Noise.Voronoi support surface. New `MapFieldId.RegionId`. |
| Voronoi partition (geological / world) | PLAN | K | Coarser 5–8 cell partition for tectonic plates. Separate pass from Phase J. |
| Connected component analysis | DONE | G, M2.b | `MaskTopologyOps2D` infrastructure. Extended for scalar fields in M2.b (`ScalarFieldCcaOps2D`). |
| Plate tectonics simulation | PLAN (exploratory) | K | Voronoi cells + simple plate movement → collision zones → elevation. |

**Open decisions:** Phase K scope and subdivision — deferred until Phase J matures.

---

## 7. Placement & Traversal

| Technique | Islands Status | Consuming Phase | Notes |
|-----------|---------------|-----------------|-------|
| POI suitability scoring | PLAN | N | Suitability masks from terrain layers + biome output. Headless placement descriptors. |
| Path generation (A* / flood-corridor) | PLAN | O | `MapLayerId.Paths` (already registered). Rivers as obstacles, POIs as endpoints. |
| Poisson disk sampling | DONE (runtime) | N (potential) | Available in noise runtime. Not yet consumed by map pipeline. Natural fit for POI spacing. |

**Open decisions:** Path algorithm selection (A* vs flood-corridor vs skeleton) — resolve at Phase O implementation.

---

## 8. Pipeline Infrastructure

| Technique | Islands Status | Consuming Phase | Notes |
|-----------|---------------|-----------------|-------|
| Hierarchical seed derivation | DONE | All | `MapInputs.seed` → stage salts. Core invariant. |
| Deterministic pipeline execution | DONE | All | Same seed + inputs → same output. Snapshot-test gated. |
| World rejection / validation | PLAN | P | `IMapValidator2D` + retry loop. Recommended before Phase W. |
| Data-driven stage configuration | PARTIAL | H3 (partial) | `MapGenerationPreset` SO. Full stage-composition externalisation deferred post-M. |
| Burst / SIMD optimization | PLAN | I | Performance upgrade. No contract changes. |
| GPU composite visualization | PLAN | I2 | Shader-based multi-layer composite. After Phase I. |
| Two-scale world-to-local | PLAN | W | Rectangular world grid → `WorldTileContext` → local `MapContext2D`. |

**Open decisions:** World map resolution and exact coordinate system (Phase W). `WorldTileContext` delivery mechanism (Phase W).

---

## 9. Techniques Evaluated and Excluded

| Technique | Why excluded | Source |
|-----------|-------------|--------|
| Midpoint displacement / diamond-square | Requires (2^n)+1 grids; grid-alignment artifacts; cannot support on-demand chunks. fBm via Perlin/Simplex is strictly superior for Islands. | Cross-check: only DF uses this; Adams acknowledged Perlin would be better. |
| 3D density functions | Islands is 2D grid-first. Not applicable. | Cross-check: MC and NMS only; both are 3D voxel games. |
| Value noise for terrain | Grid-aligned artifacts make it unsuitable. Implemented for completeness only. | Cross-check: no game uses value noise as primary terrain generator. |
| Chunk-based streaming | Not currently applicable — Islands generates complete maps, not streamed chunks. | May revisit if Phase W requires incremental world generation. |
| LOD / multi-resolution generation | Not applicable to 2D grid pipeline at current scope. | NMS-specific (octree LOD for 3D planet surfaces). |

---

## 10. Coverage Summary

| Category | Total Techniques | DONE | PLAN | PARTIAL | NONE |
|----------|-----------------|------|------|---------|------|
| Noise Primitives | 5 | 5 | — | — | — |
| Noise Composition | 8 | 3 | 2 | 1 | 2 |
| Terrain & Elevation | 5 | 2 | 1 | — | 2 |
| Climate & Biomes | 7 | — | 5 | — | 2 |
| Hydrology | 5 | — | 5 | — | — |
| Region Partitioning | 4 | 1 | 2 | — | 1 |
| Placement & Traversal | 3 | 1 | 2 | — | — |
| Pipeline Infrastructure | 7 | 2 | 4 | 1 | — |
| **Total** | **44** | **14** | **21** | **2** | **7** |

Of the 7 NONE items: 2 are deferred-but-possible (full domain warping, noise competition),
2 are excluded by design (elevation smoothing, orographic precipitation), 1 is exploratory
(biome scoring), and **1 is the only top-12 priority not yet roadmapped (erosion simulation)**.

---

## Cross-References

| Document | Role |
|----------|------|
| `planning/active/PCG_Roadmap.md` | Phase sequencing and status. Implementation authority for what is built. |
| `planning/active/design/Phase_M_Design.md` | Climate + Biome stage contracts and test plan. |
| `planning/active/design/Phase_M2_Design.md` | Biome-aware vegetation + region detection contracts. |
| `planning/active/design/Phase_L_Design.md` | Hydrology stage contracts and algorithms. |
| `planning/active/design/Phase_W_Design.md` | World-to-local architectural design. |
| `planning/active/design/Noise_Composition_Improvements_Roadmap.md` | N1/N2/N3 noise technique designs. |
| `research/pcg_crosscheck_analysis.md` | Source analysis: 6 games × 30 techniques, priority list. |
| `research/pcg_technique_library.md` | Technique reference: algorithmic detail for all PCG families. |
