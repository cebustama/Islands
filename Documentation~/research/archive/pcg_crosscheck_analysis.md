# PCG Technique Cross-Check & Islands Implementation Priority Analysis

Status: Analysis document (reference)  
Date: 2026-04-05 (updated 2026-04-06)  
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
**Status: ROADMAPPED (Phase M).**

### Priority 2: Multi-Parameter Biome Classification
**Technique:** Classify each cell into a biome category using two or more scalar fields (Height, Moisture, CoastDist).  
**Why high priority:** Appears in all 6 games in some form. It is the key technique that converts continuous scalar fields into discrete, gameplay-meaningful regions. All downstream systems (vegetation density, POI suitability, tile appearance, path cost) depend on biome identity.  
**Status: ROADMAPPED (Phase M). Output format resolved — see PCG_Roadmap.md § Decision 1.**

### Priority 3: Voronoi Region Partitioning
**Technique:** Partition the map into irregular regions using Voronoi cell decomposition.  
**Why high priority:** 5/6 games use some form of cell-based spatial partitioning for biome distribution at macro scale.  
**Status: ROADMAPPED (Phase J). Voronoi-vs-world-tiles resolved — see PCG_Roadmap.md § Decision 4.**

### Priority 4: River Generation
**Technique:** Flow-downhill river placement from high elevation toward coast, producing river paths as a new mask layer.  
**Why high priority:** 3/6 games implement rivers (DF, RW, CDDA). Rivers are the primary hydrological feature that breaks up terrain monotony, creates strategic crossing points, and feeds into moisture/fertility systems.  
**Status: ROADMAPPED (Phase L). River representation and lake modeling resolved — see PCG_Roadmap.md § Decisions 2 and 3.**

### Priority 5: Two-Scale World-to-Local Generation
**Technique:** Generate a coarse world map where each cell carries compact metadata, then generate a full-resolution local map parameterised by that cell's properties.  
**Why high priority:** 3/6 games use this pattern (DF, RW, CDDA). Islands' `MapShapeInput` (Phase F2c) already provides the integration hook.  
**Status: ROADMAPPED (Phase W). World grid structure resolved — see PCG_Roadmap.md § Decision 4.**

### Priority 6: Temperature Field
**Technique:** A scalar field representing temperature, varying by latitude equivalent (Y-axis position), elevation, and optionally distance from water.  
**Why high priority:** 3/6 games use spatial temperature (MC, DF, RW). Third axis of rich biome classification.  
**Status: RESOLVED. Folded into Phase M scope — see PCG_Roadmap.md § Decision 5.**

### Priority 7: Contiguous Region Detection and Naming
**Technique:** After biome classification, detect contiguous regions of the same biome type, assign each a unique ID, and optionally generate a name.  
**Why high priority:** 2/6 games use this (DF, RW). Transforms a noisy biome grid into coherent named regions.  
**Status: ROADMAPPED as Phase M2.b. Design document complete (Phase_M2_Design.md).**

### Priority 8: Vegetation as Biome-Composite Field
**Technique:** Replace the current noise-only vegetation mask with a biome-aware composite that varies density and type by biome, moisture, and elevation.  
**Why high priority:** 5/6 games derive vegetation from multiple inputs rather than a single noise threshold.  
**Status: ROADMAPPED as Phase M2.a. Design document complete (Phase_M2_Design.md).**

### Priority 9: Non-Linear Height Redistribution
**Technique:** Apply a power curve to the height field to reshape elevation distribution.  
**Why high priority:** DF uses a non-linear parabola. Single-line change with high visual impact.  
**Status: ROADMAPPED as Phase J2. Also covered as N1 in Noise Composition Improvements Roadmap.**

### Priority 10: World Rejection / Parameter Validation
**Technique:** Validate generated output against parameter ranges and reject + regenerate if failed.  
**Why high priority:** Only DF implements this, but critical for design-compliant output as pipeline grows.  
**Status: ROADMAPPED as Phase P.**

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
**Why high priority:** 5/6 games use data-driven configuration. Islands already has `MapGenerationPreset` SO (Phase H3) for tunables, but stage selection and ordering are still code-controlled.  
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
| 6 | Temperature field | **Phase M** (resolved — folded into Phase M scope) |
| 7 | Contiguous region detection | **Phase M2.b** (roadmapped, design doc complete) |
| 8 | Biome-aware vegetation | **Phase M2.a** (roadmapped, design doc complete) |
| 9 | Height redistribution | **Phase J2** (roadmapped) + N1 in Noise Composition Roadmap |
| 10 | World rejection / validation | **Phase P** (roadmapped) |
| 11 | Erosion simulation | **Not roadmapped** — recommend pre-Phase L |
| 12 | Data-driven stage config | **Partially addressed** (H3) — defer until post-Phase M |

**Key finding:** 10 of 12 priorities are now explicitly roadmapped. The only non-roadmapped technique is erosion simulation (priority 11), which would benefit from a dedicated phase or sub-phase before Phase L. Data-driven stage configuration (priority 12) is partially addressed and deferred.

---

## Design Decisions — Resolution Status

> **All five design decisions below were resolved on 2026-04-06** and folded into
> `PCG_Roadmap.md` § "Resolved design decisions." The recommendations from this
> analysis were accepted as stated. This section is retained for traceability.

1. **Biome output format** → **Resolved (Decision 1).** `MapFieldId.Biome` integer scalar field with `BiomeDef[]` lookup table.

2. **River representation** → **Resolved (Decision 2).** Both `MapFieldId.FlowAccumulation` (scalar) and `MapLayerId.Rivers` (mask, derived by thresholding).

3. **Lake modeling** → **Resolved (Decision 3).** Distinct `MapLayerId.Lakes` mask layer.

4. **Voronoi cells = world tiles?** → **Resolved (Decision 4).** No — separate concepts at separate scales. Phase J operates within local maps; Phase W uses a rectangular world grid.

5. **Temperature field ownership** → **Resolved (Decision 5).** Part of Phase M as a climate sub-stage writing both Temperature and Moisture before biome classification.
