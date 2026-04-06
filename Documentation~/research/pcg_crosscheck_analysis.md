# PCG Technique Cross-Check & Islands Implementation Priority Analysis

Status: Analysis document  
Date: 2026-04-05  
Scope: Worldgen techniques only (terrain, biomes, climate, region structure, rivers, caves, multi-scale world organisation). Excludes dungeon interiors, narrative systems, UI.

---

## Deliverable 1 — Cross-Check Matrix

**Legend:** ✓ confirmed / ~ inferred / — absent or not applicable  
**Games:** MC = Minecraft, DF = Dwarf Fortress, NMS = No Man's Sky, RW = RimWorld, CDDA = Cataclysm:DDA, TD = Tangledeep

### A. Noise & Terrain Generation

| Technique | MC | DF | NMS | RW | CDDA | TD | Notes |
|---|---|---|---|---|---|---|---|
| Multi-octave coherent noise (fBm) | ✓ | ~ | ✓ | ✓ | ✓ | — | DF uses midpoint displacement (same fractal principle, different algorithm); TD is dungeon-only |
| Midpoint displacement / diamond-square | — | ✓ | — | — | — | — | DF's primary field-seeding method; described as 1980s-era by Adams himself |
| Ridged multifractal | ~ | — | ~ | ✓ | — | — | RW confirmed (elevation blend); MC achieves similar via PV splines; NMS likely uses for mountain layer |
| Domain warping | ~ | — | ✓ | — | — | — | NMS confirmed (turbulence + domain warp in Uber noise); MC density function composition is structurally analogous |
| Island / continent mask (distance falloff) | ✓ | ~ | — | — | — | — | MC continentalness noise is functionally an island mask; DF uses ocean edge parameters; NMS/RW/CDDA have no island concept |
| 3D density function / volumetric terrain | ✓ | — | ✓ | — | — | — | MC and NMS only; both use density → meshing (MC: block threshold; NMS: marching cubes) |
| Heightmap-based terrain | ~ | ✓ | ~ | ✓ | ~ | — | DF elevation 0–400; RW elevation float grid; MC and NMS derive heightmaps within 3D density |
| Erosion simulation (hydraulic/agent) | — | ✓ | — | — | — | — | DF only; "fake rivers" dig into terrain as erosion agents — a simplified but effective approach |
| Elevation smoothing / post-processing | ~ | ✓ | — | — | — | — | DF uses multiple smoothing passes; MC splines smooth transitions between terrain zones |
| Spline-based terrain shaping | ✓ | — | — | — | — | — | MC only; cubic splines map noise parameters to terrain offset/factor/jaggedness |
| Non-linear height redistribution | — | ✓ | — | — | — | — | DF's "non-linear parabola" reshapes mountain elevation profiles |

### B. Biomes & Climate

| Technique | MC | DF | NMS | RW | CDDA | TD | Notes |
|---|---|---|---|---|---|---|---|
| Multi-parameter biome classification | ✓ | ✓ | ✓ | ✓ | ~ | ~ | Universal across non-dungeon games; MC uses 6D; DF uses 5-variable; CDDA/TD use simpler variants |
| Temperature field (latitude + elevation) | ✓ | ✓ | ~ | ✓ | — | — | DF is richest (latitude + elevation + moisture + forest dampening); NMS has per-planet hazard type only |
| Rainfall / moisture field | ✓ | ✓ | — | ✓ | — | — | Three world-scale games; NMS and CDDA have no spatial moisture |
| Orographic precipitation / rain shadow | — | ✓ | — | — | — | — | DF only; refines rainfall based on mountain blocking and drainage |
| Vegetation as composite field | ~ | ✓ | ~ | ✓ | ✓ | — | DF derives vegetation from 4 fields; RW uses biome species × fertility; CDDA uses noise thresholds |
| Biome scoring / competition model | — | — | — | ✓ | — | — | RW's BiomeWorker.GetScore() is architecturally distinctive — each biome competes for each tile |
| Salinity field | — | ✓ | — | — | — | — | DF only; graduated salinity from ocean to coast influences biome classification |

### C. Region Partitioning & World Structure

| Technique | MC | DF | NMS | RW | CDDA | TD | Notes |
|---|---|---|---|---|---|---|---|
| Voronoi / cell-based region partition | ✓ | ~ | ✓ | ~ | ~ | — | MC 6D Voronoi; NMS galaxy region grid; RW geodesic sphere tiles; DF contiguous-region detection |
| Contiguous region detection + naming | — | ✓ | — | ✓ | — | — | DF detects and names mountain ranges, forests, etc.; RW names geographic features |
| World rejection / parameter validation | — | ✓ | — | — | — | — | DF only; two rejection checkpoints during generation ensure parameter compliance |
| Geological layer assignment | — | ✓ | — | ✓ | — | — | DF assigns soil→sedimentary→metamorphic→intrusive per region; RW assigns stone types per tile |
| Sector-based special placement | — | — | — | — | ✓ | — | CDDA's 12×12 sector grid for weighted special placement is distinctive |
| Plate tectonics simulation | — | — | — | — | — | — | Not confirmed in any of the six games; DF's volcanism field is closest but is noise-seeded, not simulated |

### D. Water Systems

| Technique | MC | DF | NMS | RW | CDDA | TD | Notes |
|---|---|---|---|---|---|---|---|
| River generation (flow-downhill) | — | ✓ | — | ✓ | ✓ | — | DF carves channels with erosion agents; RW routes on tile graph; CDDA uses weighted random walks |
| Lake generation | — | ✓ | — | ~ | ✓ | — | DF grows lakes at river depressions; CDDA uses global noise thresholds |
| Ocean / sea-level classification | ✓ | ✓ | ✓ | ✓ | ✓ | — | Universal across non-dungeon games |
| Flow accumulation / drainage field | — | ✓ | — | ~ | — | — | DF confirmed (drainage 0–100 + river flow volumes); RW river sizing inferred |
| Aquifer / underground fluid system | ✓ | — | — | — | — | — | MC only; per-cell fluid level resolution within the density function |
| Flood fill for water bodies | ✓ | ✓ | — | — | — | — | MC: DeepWater as border-connected flood fill; DF: inland ocean removal |

### E. Caves & Underground

| Technique | MC | DF | NMS | RW | CDDA | TD | Notes |
|---|---|---|---|---|---|---|---|
| Noise-based 3D caves (threshold) | ✓ | — | ✓ | — | — | — | MC cheese/spaghetti/noodle; NMS CaveVoids density function |
| Perlin worm / agent carvers | ✓ | ~ | — | — | — | — | MC legacy carvers confirmed; DF erosion agents are structurally similar |
| Cellular automata caves | — | — | — | ~ | — | ~ | RW uses Perlin-guided flood-fill in rock; TD cave layout type likely CA-based |
| Stratified underground layers | — | ✓ | — | — | ✓ | — | DF 3-layer caverns + magma sea + underworld; CDDA z-level underground specials |
| Template-based underground structures | — | — | — | ✓ | ✓ | — | RW ancient dangers; CDDA labs, sewers, subway via JSON templates |

### F. Multi-Scale & Streaming

| Technique | MC | DF | NMS | RW | CDDA | TD | Notes |
|---|---|---|---|---|---|---|---|
| Chunk / tile-based streaming | ✓ | — | ✓ | ~ | ✓ | — | MC 16×16 chunks; NMS octree nodes; CDDA lazy overmap + reality bubble |
| Two-scale world-to-local generation | — | ✓ | ~ | ✓ | ✓ | — | DF embark; RW tile→250×250 map; CDDA overmap→24×24 local; NMS planet→surface is similar |
| LOD / multi-resolution generation | ~ | — | ✓ | — | — | — | NMS octree LOD confirmed; MC noise cell interpolation is minimal LOD |
| Hierarchical seed derivation | ✓ | ✓ | ✓ | ✓ | ~ | ✓ | Universal for determinism; all games derive sub-seeds from master seed |
| Data-driven pipeline configuration | ✓ | — | ✓ | ✓ | ✓ | ✓ | MC JSON data packs; NMS VoxelGeneratorSettings; RW XML defs; CDDA JSON; TD XML |
| Stateless regeneration from seed | — | — | ✓ | — | — | — | NMS only; no persistent world state; terrain is a pure function of coordinates |

---

## Deliverable 2 — Technique Frequency and Context Table

Ranked by frequency of appearance across the six games (confirmed + inferred).

| Rank | Technique | Appearances | Games | Typical Pipeline Stage | Primary Role |
|---|---|---|---|---|---|
| 1 | Hierarchical seed derivation | 6/6 | MC, DF, NMS, RW, CDDA, TD | F0 (initialization) | Determinism foundation |
| 2 | Multi-parameter biome classification | 6/6 | MC, DF, NMS, RW, CDDA (~), TD (~) | Late-mid (after terrain + climate fields) | Convert continuous fields → discrete categories |
| 3 | Ocean / sea-level classification | 5/6 | MC, DF, NMS, RW, CDDA | After base terrain | Separate land from water |
| 4 | Multi-octave coherent noise (fBm) | 5/6 | MC, DF (~), NMS, RW, CDDA | Early (terrain shape, field seeding) | Primary spatial signal source |
| 5 | Data-driven pipeline configuration | 5/6 | MC, NMS, RW, CDDA, TD | Meta / pipeline orchestration | Moddability, parameterisation |
| 6 | Heightmap-based terrain | 5/6 | MC (~), DF, NMS (~), RW, CDDA (~) | Early (base terrain) | Elevation as foundational field |
| 7 | Vegetation as composite field | 5/6 | MC (~), DF, NMS (~), RW, CDDA | After climate + terrain | Visual cover, gameplay gating |
| 8 | Voronoi / cell-based regions | 5/6 | MC, DF (~), NMS, RW (~), CDDA (~) | Mid (region structure) | Spatial partitioning for identity |
| 9 | Temperature field | 4/6 | MC, DF, NMS (~), RW | After terrain, before biomes | Climate axis for biome selection |
| 10 | Rainfall / moisture field | 3/6 | MC, DF, RW | After terrain, before biomes | Second climate axis for biome selection |
| 11 | River generation (flow-downhill) | 3/6 | DF, RW, CDDA | After elevation established | Hydrological network, landscape carving |
| 12 | Two-scale world-to-local | 3/6 (+NMS ~) | DF, RW, CDDA | Architectural pattern | Strategic vs. tactical detail separation |
| 13 | Chunk/tile streaming | 3/6 | MC, NMS, CDDA | Runtime | Infinite/large world support |
| 14 | Lake generation | 2/6 (+RW ~) | DF, CDDA | After rivers or via noise | Standing water features |
| 15 | Ridged multifractal | 2/6 (+MC ~, NMS ~) | RW, (MC, NMS) | Base terrain composition | Mountain ridgelines, dramatic terrain |
| 16 | Domain warping | 2/6 | MC (~), NMS | Base terrain / noise composition | Organic distortion of terrain features |
| 17 | Erosion simulation | 1/6 | DF | After initial terrain, before rivers | Natural valley/channel formation |
| 18 | Contiguous region detection + naming | 2/6 | DF, RW | After biome classification | Human-readable geography |
| 19 | World rejection / validation | 1/6 | DF | Mid-pipeline checkpoints | Quality assurance on generated output |
| 20 | Geological layer assignment | 2/6 | DF, RW | After biome/region identity | Underground material distribution |

**Key pattern:** The top 8 techniques appear in 5–6 of 6 games. These represent the essential worldgen toolkit. Techniques ranked 9–12 (temperature, moisture, rivers, two-scale) appear in 3–4 games and are the next tier of sophistication that distinguishes rich worldgen from basic. Techniques ranked 13+ are either architectural choices (streaming) or advanced simulation (erosion, plate tectonics) with lower adoption.

**Context observation (synthesis judgment):** Islands' current pipeline already implements the essentials at ranks 1, 3, 4, 5, 6 (seed derivation, sea classification, fBm noise, heightmap terrain, data-driven stages). The major gaps are at ranks 2, 7, 8, 9, 10, 11 — biome classification, vegetation-as-composite-field, Voronoi regions, temperature, moisture, and rivers. These are precisely the techniques the roadmap (Phases J, L, M) already targets.

---

## Deliverable 3 — Islands Implementation Priority List

### Priority 1: Moisture Field Generation
**Technique:** Rainfall/moisture scalar field from noise, optionally modulated by CoastDist and elevation.  
**Why high priority:** Moisture is the second axis of the Whittaker-style biome classification used by MC (humidity), DF (rainfall), and RW (rainfall). Without it, Phase M biome classification cannot produce ecologically coherent output. It is the single most blocking data dependency for the entire biome→POI→paths chain.  
**Already roadmapped?** Yes — Phase M is the confirmed owner of the first `MapFieldId.Moisture` write.  
**Depends on / extends:** `MapFieldId.Moisture` (registered, not written), `Stage_Morphology2D` CoastDist (for coastal moisture gradient), `MapNoiseBridge2D` (noise sampling).  
**First implementation step:** A new `Stage_Moisture2D` that writes a noise-based moisture field, using CoastDist as a multiplicative or additive modifier (coastal cells wetter). DF's vegetation derivation formula (moisture from rainfall + elevation + drainage) provides the reference model, but a simpler noise + coast-proximity blend is sufficient for a first pass.  
**Status: ALREADY ROADMAPPED (Phase M). Flag only — do not re-recommend.**

### Priority 2: Multi-Parameter Biome Classification
**Technique:** Classify each cell into a biome category using two or more scalar fields (Height, Moisture, CoastDist).  
**Why high priority:** Appears in all 6 games in some form. It is the key technique that converts continuous scalar fields into discrete, gameplay-meaningful regions. All downstream systems (vegetation density, POI suitability, tile appearance, path cost) depend on biome identity. Islands currently has no biome system — cells are classified only by mask layers (Land, Hills, Water).  
**Already roadmapped?** Yes — Phase M.  
**Open design decision:** Biome output format is explicitly unresolved (new layer IDs vs. scalar field vs. enum field). This must be decided before implementation. Recommendation (synthesis judgment): a new `MapFieldId.Biome` storing an integer biome ID per cell, with a `BiomeDef` lookup table, is the most flexible approach and matches MC/RW patterns.  
**Status: ALREADY ROADMAPPED (Phase M). Flag open design decision on output format.**

### Priority 3: Voronoi Region Partitioning
**Technique:** Partition the map into irregular regions using Voronoi cell decomposition.  
**Why high priority:** 5/6 games use some form of cell-based spatial partitioning. Voronoi regions serve as the foundation for biome distribution at macro scale, archipelago support (Phase J note), and the world-tile concept in Phase W. Without regions, biome classification operates cell-by-cell from noise alone, producing noisy boundaries instead of coherent regional identity.  
**Already roadmapped?** Yes — Phase J.  
**Depends on / extends:** Existing `Noise.Voronoi.cs` support surface; planned `MapVoronoiBridge2D`.  
**Open design decision (flagged in roadmap):** Whether Voronoi cells are the same concept as world tiles (Phase W) must be decided before Phase J is implemented. This is the single most important architectural fork in the roadmap.  
**Status: ALREADY ROADMAPPED (Phase J). Flag world-tile equivalence decision.**

### Priority 4: River Generation
**Technique:** Flow-downhill river placement from high elevation toward coast, producing river paths as a new mask layer.  
**Why high priority:** 3/6 games implement rivers (DF, RW, CDDA). Rivers are the primary hydrological feature that breaks up terrain monotony, creates strategic crossing points, and feeds into moisture/fertility systems. Phase N (POI placement) benefits from rivers for settlement suitability (villages near water). Phase O (Paths) needs rivers as obstacles that paths must bridge or avoid.  
**Already roadmapped?** Yes — Phase L.  
**Open design decisions (flagged in CURRENT_STATE.md):**  
  - River representation: mask vs. flow-accumulation field. **Recommendation (synthesis judgment):** Both. A `MapFieldId.FlowAccumulation` scalar field drives the simulation; a `MapLayerId.Rivers` mask layer is derived from it by thresholding. This matches the DF approach (flow volumes + visible rivers) and preserves mask/field architecture.  
  - Lake modeling: distinct `Lakes` layer vs. enclosed `ShallowWater`. **Recommendation (synthesis judgment):** Distinct `MapLayerId.Lakes` layer, since lakes have different gameplay semantics (fishing, settlement proximity) from shallow coastal water.  
**Depends on / extends:** `Height` field (F2), `Land` mask, `ShallowWater` (F4), `MaskFloodFillOps2D` (for depression detection).  
**First implementation step:** Implement Priority-Flood depression filling on the Height field (Barnes 2014 algorithm from Pass 2b), then D8 flow directions from each cell to steepest downslope neighbor. This is the foundational data structure from which rivers, flow accumulation, and lakes all derive.  
**Status: ALREADY ROADMAPPED (Phase L). Flag both open design decisions with recommendations.**

### Priority 5: Two-Scale World-to-Local Generation
**Technique:** Generate a coarse world map where each cell carries compact metadata, then generate a full-resolution local map parameterised by that cell's properties.  
**Why high priority:** 3/6 games use this pattern (DF, RW, CDDA), and it is the architectural prerequisite for any game with both strategic (world) and tactical (local) play scales. Islands' `MapShapeInput` (Phase F2c) already provides the integration hook — a world tile can inject a shore mask into local generation.  
**Already roadmapped?** Yes — Phase W.  
**Open design decisions (flagged in roadmap):** Option A (pipeline at world scale) vs. Option B (noise-direct); world map resolution; whether world tiles = Voronoi cells.  
**Status: ALREADY ROADMAPPED (Phase W). Flag as dependent on Phase J/M decisions.**

### Priority 6: Temperature Field
**Technique:** A scalar field representing temperature, varying by latitude equivalent (Y-axis position), elevation, and optionally distance from water.  
**Why high priority:** 3/6 games use spatial temperature (MC, DF, RW). Temperature is the third axis of rich biome classification — without it, biomes can distinguish wet/dry and high/low but not hot/cold. DF's model is the gold standard: latitude gradient + elevation attenuation + moisture dampening + forest cooling.  
**Already roadmapped?** Not explicitly as a separate phase. Phase M mentions biome classification using Height and Moisture but does not explicitly list Temperature as an input field.  
**Recommendation:** Add `MapFieldId.Temperature` to the registry and assign its write ownership to Phase M alongside Moisture. A temperature field derived from `(Y-position latitude proxy) + (Height-based lapse rate) + (CoastDist-based maritime moderation)` requires no new infrastructure — it is a scalar field written by a new stage using existing inputs.  
**Depends on / extends:** `MapFieldId` registry (new entry needed), Height field, CoastDist field.  
**First implementation step:** Register `MapFieldId.Temperature` as a new append-only ID. Implement the write in Phase M's moisture/climate stage as a second output field.  
**Status: NOT EXPLICITLY ROADMAPPED. Recommend adding to Phase M scope.**

### Priority 7: Contiguous Region Detection and Naming
**Technique:** After biome classification, detect contiguous regions of the same biome type, assign each a unique ID, and optionally generate a name.  
**Why high priority:** 2/6 games use this (DF, RW). It transforms a noisy biome grid into coherent named regions that the player can reference ("the Western Forest," "the Coastal Marsh"). This is valuable for world-map UI, quest generation, and player orientation. Islands' existing `MaskTopologyOps2D` already has flood-fill and connected-component infrastructure.  
**Already roadmapped?** Not explicitly. Phase M mentions biome classification but not region detection or naming.  
**Depends on / extends:** Phase M biome output, `MaskTopologyOps2D` connected components, `MaskFloodFillOps2D`.  
**First implementation step:** After Phase M produces a biome ID field, run connected-component labeling (already available via `MaskTopologyOps2D` patterns) on each biome type to produce a region ID field. Naming is a downstream adapter concern.  
**Status: NOT EXPLICITLY ROADMAPPED. Recommend adding as Phase M sub-phase or Phase M2.**

### Priority 8: Vegetation as Biome-Composite Field
**Technique:** Replace the current noise-only vegetation mask with a biome-aware composite that varies density and type by biome, moisture, and elevation.  
**Why high priority:** 5/6 games derive vegetation from multiple inputs rather than a single noise threshold. Islands' current `Stage_Vegetation2D` uses only a noise threshold on `LandInterior`; it has no moisture or biome awareness. Once Phase M produces biome and moisture data, vegetation should be re-derived as a richer composite — dense in wet forest biomes, sparse in arid biomes, absent in tundra.  
**Already roadmapped?** Partially — Phase M notes that it "enables biome-aware downstream work: vegetation density." The actual re-derivation of vegetation is not a named phase.  
**Depends on / extends:** Phase M biome output, `MapFieldId.Moisture`, `Stage_Vegetation2D` (refactor).  
**First implementation step:** After Phase M, modify `Stage_Vegetation2D` to accept biome ID and moisture as additional inputs. Replace the single noise threshold with per-biome coverage parameters.  
**Status: PARTIALLY ROADMAPPED (noted as Phase M downstream). Recommend explicit sub-phase.**

### Priority 9: Non-Linear Height Redistribution
**Technique:** Apply a power curve or piecewise transfer function to the height field to reshape elevation distribution — concentrating detail at low elevations (more plains) while allowing dramatic peaks.  
**Why high priority:** DF uses a non-linear parabola to make mountains more realistic. The standard noise-to-heightmap pipeline (which Islands currently uses) produces roughly Gaussian elevation distribution, leading to too much mid-range terrain and not enough dramatic peaks or broad plains. A simple `pow(height, exponent)` redistribution is a single-line change with high visual impact.  
**Already roadmapped?** No.  
**Conflicts with core invariants?** None — it is a deterministic scalar field transformation that fits cleanly as a post-processing step after `Stage_BaseTerrain2D`.  
**Depends on / extends:** `Height` field (F2). Could be a new micro-stage or a configuration option on the existing base terrain stage.  
**First implementation step:** Add an optional `heightRedistributionExponent` tunable to `MapTunables2D` (default 1.0 = no change). Apply `pow(height01, exponent)` after the height field is normalised in `Stage_BaseTerrain2D`. Exponents > 1.0 flatten lowlands and steepen peaks; < 1.0 does the reverse.  
**Status: NOT ROADMAPPED. Low-cost, high-impact addition. Recommend for any point after current H-series.**

### Priority 10: World Rejection / Parameter Validation
**Technique:** After key generation stages, validate the output against desired parameter ranges (minimum land %, elevation distribution, biome coverage) and reject + regenerate if failed.  
**Why high priority:** Only DF implements this, but it is critical for any game that needs generated worlds to meet design requirements. Islands currently has no mechanism to reject a bad seed — the pipeline always produces output regardless of quality. As the pipeline gains more stages (biomes, rivers, regions), the probability of producing a degenerate map increases.  
**Already roadmapped?** No.  
**Conflicts with core invariants?** Potential conflict with determinism: rejection changes the seed, so "same seed = same output" still holds per seed, but the user-facing seed may differ from the internal seed if rejection occurs. This must be resolved by design (e.g., rejection increments the seed and retries, documented behavior).  
**Depends on / extends:** `MapPipelineRunner2D` (needs a validate-or-reject loop), `MapContext2D` (needs validation queries like land-area %).  
**First implementation step:** Add a `IMapValidator2D` interface with a `bool Validate(MapContext2D)` method. Implement a basic validator checking minimum land percentage. Wrap `MapPipelineRunner2D` with a retry loop (max N attempts, incrementing seed).  
**Status: NOT ROADMAPPED. Recommend adding as a pipeline infrastructure improvement before Phase J.**

### Priority 11: Erosion Simulation (Simplified Agent-Based)
**Technique:** Simplified erosion using downhill-flowing agents that carve channels into the height field, creating natural valleys and drainage paths.  
**Why high priority:** Only DF implements erosion, but it is the single technique most responsible for DF's natural-looking terrain. Without erosion, noise-based heightmaps look artificially smooth. DF's approach (fake rivers that dig when stuck) is simple enough to implement on a 2D grid and directly feeds into river generation (Phase L).  
**Already roadmapped?** No.  
**Conflicts with core invariants?** None if implemented as a deterministic stage consuming Height + Land and writing a modified Height field. Agent iteration order must be deterministic (e.g., row-major spawning, fixed-order neighbor selection).  
**Depends on / extends:** `Height` field (F2), `Land` mask. Best sequenced between base terrain (F2) and hydrology (Phase L) — the eroded heightmap makes river routing more natural.  
**First implementation step:** Implement DF-style erosion agents: spawn at high-elevation land cells in row-major order, walk downhill choosing the steepest neighbor, lower the current cell's height by a fixed amount when stuck. Run N cycles (tunable). This is a new `Stage_Erosion2D` between F2 and F3 (or between G and L).  
**Status: NOT ROADMAPPED. Recommend as pre-Phase L enhancement. Significant pipeline reordering implications — must be planned carefully.**

### Priority 12: Data-Driven Stage Configuration (Pipeline Parameterisation)
**Technique:** Allow pipeline stages, their order, and their parameters to be configured externally (JSON, ScriptableObject) rather than hard-coded in C#.  
**Why high priority:** 5/6 games use data-driven configuration (MC JSON data packs, NMS VoxelGeneratorSettings, RW XML defs, CDDA JSON region_settings, TD XML). Islands already has `MapGenerationPreset` SO (Phase H3) for tunables, but stage selection and ordering are still code-controlled. As the pipeline grows, external configuration becomes increasingly valuable for designers and modders.  
**Already roadmapped?** Partially — `MapGenerationPreset` (Phase H3) externalises tunables but not stage composition.  
**Conflicts with core invariants?** None if stage ordering remains deterministic (array-order execution is already the contract).  
**First implementation step:** Extend `MapGenerationPreset` with an optional `IMapStage2D[]` override list, allowing designers to enable/disable stages or reorder them via SO configuration. The existing stage toggle system (enableHillsStage, enableVegetationStage, etc.) is a partial version of this.  
**Status: PARTIALLY ADDRESSED (Phase H3 tunables). Full stage-composition externalisation NOT ROADMAPPED. Lower priority — recommend deferring until pipeline stabilises after Phase M.**

---

## Summary: Roadmap Alignment

| Priority | Technique | Roadmap Status |
|---|---|---|
| 1 | Moisture field | **Phase M** (roadmapped) |
| 2 | Biome classification | **Phase M** (roadmapped) |
| 3 | Voronoi regions | **Phase J** (roadmapped) |
| 4 | River generation | **Phase L** (roadmapped) |
| 5 | Two-scale world-to-local | **Phase W** (roadmapped) |
| 6 | Temperature field | **Not explicitly roadmapped** — recommend adding to Phase M |
| 7 | Contiguous region detection | **Not roadmapped** — recommend as Phase M sub-phase |
| 8 | Biome-aware vegetation | **Partially noted** — recommend explicit sub-phase post-M |
| 9 | Height redistribution | **Not roadmapped** — low-cost, recommend any time post-H series |
| 10 | World rejection / validation | **Not roadmapped** — recommend before Phase J |
| 11 | Erosion simulation | **Not roadmapped** — recommend pre-Phase L |
| 12 | Data-driven stage config | **Partially addressed** (H3) — defer until post-Phase M |

**Key finding:** The top 5 priorities are already roadmapped. The roadmap's sequencing (J → L → M) is well-aligned with the cross-check evidence. The 6 non-roadmapped techniques (priorities 6–11) fill gaps that the cross-check reveals as common practice across the researched games.

---

## Open Design Decisions That Block Top-5 Recommendations

These are surfaced from both the existing roadmap documentation and this analysis:

1. **Biome output format** (blocks Priority 2 / Phase M): New MapLayerId entries per biome? A `MapFieldId.Biome` integer field? An enum field? This decision cascades into downstream phases (N, O, W, adapter tileset configuration). **Recommendation:** Integer biome ID field with a BiomeDef lookup table.

2. **River representation** (blocks Priority 4 / Phase L): Mask layer vs. flow-accumulation scalar field. **Recommendation:** Both — flow-accumulation field as the simulation output, river mask derived by thresholding.

3. **Lake modeling** (blocks Priority 4 / Phase L): Distinct Lakes layer vs. enclosed ShallowWater. **Recommendation:** Distinct `MapLayerId.Lakes` layer.

4. **Voronoi cells = world tiles?** (blocks Priority 3 / Phase J and Priority 5 / Phase W): Whether Voronoi region cells from Phase J are the same concept as world tiles in Phase W. **Recommendation:** Yes — design Phase J cells as world-tile-compatible from the start. This is cheap to decide now and expensive to change later.

5. **Temperature field ownership** (blocks Priority 6): Should Temperature be part of Phase M, or a separate preceding phase? **Recommendation:** Part of Phase M, as a climate sub-stage that writes both Moisture and Temperature before biome classification runs.

---

## Proposed Next Task

The most useful follow-on work would be one of:

**Option A — Phase M Design Specification:** Produce a detailed design document for Phase M (Biome Classification) that resolves the open design decisions listed above, specifies the biome definition format, defines the Moisture + Temperature stage contracts, and specifies the biome scoring/classification algorithm. This is the highest-value planning work because Phase M is the linchpin that unlocks the entire downstream chain (vegetation rework, POI, paths, world-to-local).

**Option B — Phase J/W Architectural Decision Record:** Produce a short decision document resolving whether Voronoi cells = world tiles, since this decision must be made before Phase J implementation begins and the roadmap explicitly flags it as pending.

**Option C — Non-Roadmapped Technique Integration Plan:** Produce implementation sketches for priorities 9–11 (height redistribution, world rejection, erosion), which are low-dependency additions that could be implemented in parallel with the current H-series adapter work.

---

## Rehydration Prompt

If continuity matters for a follow-on session, use the following prompt:

> You are continuing the Islands PCG analysis. The previous session produced a cross-check matrix covering 6 games × ~30 worldgen techniques, a frequency table, and a 12-item priority list. Key findings: the top 5 priorities are already roadmapped (Phases J, L, M, W); 6 non-roadmapped techniques were identified (temperature field, contiguous region detection, biome-aware vegetation, height redistribution, world rejection, erosion); 5 open design decisions were surfaced with recommendations. The full analysis is in project knowledge. The proposed next task was [Option A/B/C — specify which]. Proceed with that task, grounding all claims in the project knowledge documents (CURRENT_STATE.md, pipeline SSoT, PCG_Roadmap.md, game worldgen reports, technique reports 1a–6b).
