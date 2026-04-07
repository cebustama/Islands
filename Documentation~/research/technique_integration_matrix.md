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
| Heightmap-based terrain | DONE | F2 | `Height` scalar field. Foundational. MC and NMS use volumetric density (implicit heightmap); DF and RW use explicit 2D fields. Islands aligns with DF/RW approach. |
| Island / continent mask | DONE | F2, F2b, F2c | Ellipse + domain warp + arbitrary shape input. Relevant only to finite-world generators (DF, RW, Islands). |
| Mountain shaping | NONE | Pre-Phase M | **Tier 1.** Regional mask + ridged noise or power redistribution. Used by 4/4 terrain generators in cross-check. Must run before Phase M — monotonous fBm produces unconvincing altitude-based biome distribution. |
| Height redistribution | PLAN | J2 | `pow(height01, exp)` post-processing. See Noise Composition Roadmap N1. |
| Cliffs / terraces | NONE | Post-M2 polish | Tier 2. Step functions or quantization on heightmap. Not explicitly used by any game in corpus; emergent in MC/NMS. Low cost, moderate payoff. |
| Elevation smoothing | NONE | — | Not planned. May be useful post-erosion; evaluate if needed. |
| Erosion simulation | NONE | Pre-Phase L (recommended) | **Only non-roadmapped technique from the top-12 priorities.** Gap research confirmed DF uses greedy agent-based channel carving (not physically-based): spawn at mountain edges, steepest descent, lower terrain when stuck, 50–250 cycles. Purely subtractive, no sediment. Would need `Stage_Erosion2D` between G and L. |

**Open decisions:** Whether to roadmap erosion as a dedicated phase (recommended before Phase L). Mountain shaping should be roadmapped before Phase M (terrain enrichment prerequisite for meaningful biome distribution).

---

## 4. Climate & Biomes

| Technique | Islands Status | Consuming Phase | Design Doc | Notes |
|-----------|---------------|-----------------|------------|-------|
| Temperature field | PLAN | M.1 | Phase_M_Design.md | Latitude proxy + elevation lapse rate + CoastDist moderation + noise. Cross-check confirms: altitude feeds temperature via lapse rate in DF and RW, not biome directly. Mountain biome override at extreme elevation. |
| Moisture field | PLAN | M.2 | Phase_M_Design.md | CoastDist + FlowAccumulation (optional, from Phase L) + noise + rainshadow modulation (see below). |
| Rainfall / rainshadow approximation | PLAN | M.2 | Phase_M_Design.md | **Tier 1** (promoted from Tier 2). Wind-sorted moisture sweep, ~30 lines, <1ms on 256×256. Modulates base noise moisture with terrain-driven wet/dry asymmetry. Payoff conditional on biome granularity (8+ biomes) and mountain size (≥20-tile spine). See `5a_gap_rainshadow_2d_grid.md`. |
| Whittaker biome classification | PLAN | M.3 | Phase_M_Design.md | Temperature × Moisture lookup → integer biome ID. `BiomeDef[]` table. Confirmed in DF and RW; MC uses analogous but more complex 5D lookup. Cross-check strongly validates this approach. |
| Biome transition blending | NONE | M2 | Phase_M2_Design.md | **Tier 1.** Recommended: noise-perturbed boundaries (0 cost, ~20 lines) + edge-factor scalar per cell (1 float, ~100 lines). Upgrade path: scattered-point convolution (KdotJPG). Data contract: hard biome enum + edge-factor float = 6 bytes/cell. See `5a_gap_biome_blending_implementations.md`. |
| Biome-aware vegetation | PLAN | M2.a | Phase_M2_Design.md | Refactor `Stage_Vegetation2D` with per-biome density from `BiomeDef`. Consumes edge-factor for boundary transitions. |
| Contiguous region detection | PLAN | M2.b | Phase_M2_Design.md | CCA on biome field → `MapFieldId.NamedRegionId`. Simpler and better-supported than Voronoi for named regions (no game in corpus uses spatial Voronoi for biome regions). |
| Biome scoring / competition | NONE | — | — | RimWorld-style BiomeWorker. Not needed if Whittaker lookup is sufficient. |

**Open decisions:** None — all design decisions for M and M2 are resolved. Rainshadow and biome blending approaches selected from gap research.

---

## 5. Hydrology

| Technique | Islands Status | Consuming Phase | Design Doc | Notes |
|-----------|---------------|-----------------|------------|-------|
| Priority-Flood depression filling | PLAN | L | Phase_L_Design.md | Barnes 2014 algorithm. Prerequisite for flow routing. Cross-check: no game uses formal Priority-Flood. DF achieves depression-free drainage via erosion agent terrain modification (different mechanism, same functional goal). 0/6 games use the GIS/academic standard; technique is well-validated in hydrology literature. |
| D8 flow directions | PLAN | L | Phase_L_Design.md | Steepest-downslope neighbor. Intermediate data, not persisted. Cross-check: DF's erosion agents use equivalent per-cell steepest-descent lookup as an agent-local operation, not a grid-global computation. 0/6 games compute a standalone flow-direction grid. |
| Flow accumulation | PLAN | L | Phase_L_Design.md | `MapFieldId.FlowAccumulation` scalar field. Consumed by Phase M for moisture. Cross-check: 0/6 games compute flow accumulation as an explicit scalar field. Islands' primary novel contribution in hydrology — no game precedent, strong technique-report and GIS support. Dual-purpose: river identification + moisture enrichment. |
| River mask extraction | PLAN | L | Phase_L_Design.md | `MapLayerId.Rivers` derived by thresholding FlowAccumulation. Cross-check: 0/6 games derive rivers by thresholding flow accumulation. MC uses noise-biome classification, DF uses erosion-channel tracing, RW uses graph pathfinding. Islands' fractional threshold auto-scales with resolution (Phase L design §5.1.4). |
| Lake detection | PLAN | L | Phase_L_Design.md | `MapLayerId.Lakes` via boolean mask (NOT-Land ∩ NOT-DeepWater ∩ NOT-ShallowWater). Cross-check: only DF generates lakes from drainage simulation (growth at accumulation points). MC has noise-based underground aquifers. Islands uses simpler noise-depression approach, not drainage-derived basins. |
| Agent-based river carving | NONE | Future (pre-L erosion) | — | DF-only technique. Tightly coupled to DF's erosion simulation: spawn at mountain edges, steepest descent, lower terrain when stuck. Excluded for Phase L — requires terrain modification, violating Phase L's no-Height-mutate invariant. If erosion is ever added, it would be a separate pre-L phase. |
| Pathfinding-based river placement | NONE | Phase W (potential) | — | RimWorld's approach: rivers as world-graph edges placed by pathfinding from mountains to coast. Designed for world-tile-scale connectivity. Excluded for within-island drainage (standard hydrology pipeline is more principled). Potentially relevant for Phase W world-tile river inheritance. |
| Strahler stream ordering | NONE | L2 (potential) | — | Discrete river hierarchy (stream order 1, 2, 3...). 0/6 games use it; technique reports recommend it (mapgen4). FlowAccumulation serves as continuous proxy. Deferred — useful if gameplay needs river hierarchy classes. |

**Open decisions:** None — river representation and lake modeling resolved (Roadmap Decisions 2, 3). Hydrology cross-check complete; no gaps requiring resolution.

---

## 6. Region Partitioning

| Technique | Islands Status | Consuming Phase | Notes |
|-----------|---------------|-----------------|-------|
| Voronoi region partitioning (local) | PLAN | J | Within-map biome regions. Uses existing Noise.Voronoi support surface. New `MapFieldId.RegionId`. Cross-check note: no game in corpus uses spatial Voronoi for biome regions; CCA on biome map is simpler for named regions. Voronoi may still suit non-biome partitioning (geological, political). |
| Voronoi partition (geological / world) | PLAN | K | Coarser 5–8 cell partition for tectonic plates. Separate pass from Phase J. |
| Connected component analysis | DONE | G, M2.b | `MaskTopologyOps2D` infrastructure. Extended for scalar fields in M2.b (`ScalarFieldCcaOps2D`). **Tier 1 for Phase M2**: flood-fill per biome type → each connected component becomes a nameable region. |
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
| Thermal erosion | Confirmed absent in all 6 games including DF. DF's erosion is a single unified greedy-carving process with no thermal component, no material properties, no slope-dependent weathering. | Cross-check gap research: `DwarfFortress_Worldgen_gap_erosion.md`. |
| MC-style multi-dimensional parameter space biome lookup | 5D+ Voronoi partition in parameter space is overengineered for Islands' needs. 2D Whittaker model (temperature × moisture) is sufficient and validated by DF/RW. | Cross-check: MC-specific; no other game uses this approach. |
| Planet-wide single biome (NMS model) | Irrelevant to Islands' within-map biome requirements. NMS assigns one biome per planet with no spatial variation. | Cross-check: NMS-specific architectural choice. |

---

## 10. Coverage Summary

| Category | Total Techniques | DONE | PLAN | PARTIAL | NONE |
|----------|-----------------|------|------|---------|------|
| Noise Primitives | 5 | 5 | — | — | — |
| Noise Composition | 8 | 3 | 2 | 1 | 2 |
| Terrain & Elevation | 7 | 2 | 1 | — | 4 |
| Climate & Biomes | 8 | — | 6 | — | 2 |
| Hydrology | 8 | — | 5 | — | 3 |
| Region Partitioning | 4 | 1 | 2 | — | 1 |
| Placement & Traversal | 3 | 1 | 2 | — | — |
| Pipeline Infrastructure | 7 | 2 | 4 | 1 | — |
| **Total** | **50** | **14** | **22** | **2** | **12** |

Of the 12 NONE items: 2 are Tier 1 and should be roadmapped before Phase M/M2
(**mountain shaping**, **biome transition blending**), 1 is the only top-12 priority
not yet roadmapped (**erosion simulation**), 3 are hydrology techniques excluded for
Phase L (agent-based carving, pathfinding placement, Strahler ordering — the first two
excluded by architecture, Strahler deferred to L2), 2 are Tier 2 deferred items
(cliffs/terraces, elevation smoothing), 1 is not needed if Whittaker suffices (biome
scoring), and 2 are deferred-but-possible noise techniques (full domain warping, noise
competition).

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
| `research/terrain_biome_cross_reference.md` | **Terrain shaping + biome/region cross-check** (6 games × 14 techniques, gap-resolved). |
| `research/hydrology_cross_reference.md` | **Hydrology cross-check** (6 games × 9 techniques). Source for hydrology batch update. |
| `research/5a_gap_biome_blending_implementations.md` | Five concrete blending approaches from open-source implementations. |
| `research/5a_gap_rainshadow_2d_grid.md` | Six rainshadow approaches for 2D grids; feasibility assessment. |
| `research/DwarfFortress_Worldgen_gap_erosion.md` | DF erosion algorithm detail from Tarn Adams interviews. |
| `research/RimWorld_Worldgen_gap_biome_boundaries.md` | RimWorld local-map biome boundary treatment. |

---

## Changelog

- **2026-04-06:** Batch update from `terrain_biome_cross_reference.md` (post-gap-research). Added 5 rows (mountain shaping, cliffs/terraces, rainshadow, biome blending, thermal erosion exclusion + 2 more exclusions). Updated 7 existing rows with cross-check evidence and gap research findings. Promoted rainshadow from excluded to PLAN/Tier 1. Coverage: 44 → 47 techniques.
- **2026-04-06:** Batch update from `hydrology_cross_reference.md`. Added 3 rows (agent-based carving, pathfinding placement, Strahler ordering). Annotated 5 existing hydrology rows with cross-check findings (0/6 games use standard hydrology pipeline; flow accumulation is Islands' novel contribution). Coverage: 47 → 50 techniques.
