# Technique Cross-Reference: Terrain Shaping + Biome/Region Systems

**Workstream:** Islands.PCG Research / Analysis
**Scope:** Extends the noise_cross_reference.md pattern to two technique families feeding Phase M (Climate & Biome) and Phase M2 (Biome-aware vegetation + named regions).
**Date:** 2026-04-06
**Source authority:** PCG technique reports (2a, 5a, 6b), six game worldgen reports, noise_cross_reference.md (pattern), technique_integration_matrix.md (update target), Phase_M_Design.md, Phase_M2_Design.md

---

## 1. Terrain Shaping — Game × Technique Status Grid

Techniques from Pass 2a: heightmap post-processing that shapes raw noise into terrain features.

Legend: ✓ = confirmed use | ~ = inferred / partial | ✗ = confirmed absent | ? = insufficient evidence | — = not applicable (architecture incompatible)

### 1.1 Grid: Terrain Shaping Techniques

| Technique | Minecraft | Dwarf Fortress | No Man's Sky | RimWorld | Cataclysm:DDA | Tangledeep |
|---|---|---|---|---|---|---|
| **Heightmap from noise** | ~ | ✓ | ~ | ✓ | ✗ | ✗ |
| **Island/continent mask** | ✗ | ~ | — | ~ | ✗ | — |
| **Mountain shaping** | ✓ | ✓ | ✓ | ~ | ✗ | — |
| **Cliffs/terraces** | ~ | ? | ~ | ✗ | ✗ | — |
| **Hydraulic erosion** | ✗ | ✓ | ✗ | ✗ | ✗ | — |
| **Thermal erosion** | ✗ | ✗ | ✗ | ✗ | ✗ | — |

### 1.2 Notes per cell

**Heightmap from noise:**
- **Minecraft (~):** Not a traditional 2D heightmap. Uses a 3D density function where the vertical component of the density equation (`offset - y/128`) produces an *implicit* heightmap. Terrain is defined volumetrically, not as a height field. Marked ~ because the continentalness/erosion/PV splines effectively encode a heightmap-like surface, but the representation is fundamentally different from a 2D scalar field.
- **Dwarf Fortress (✓):** Six scalar fields generated via midpoint displacement (diamond-square fractal) on a fixed-size grid. Elevation is an explicit 2D field. This is the most traditional heightmap approach in the corpus.
- **No Man's Sky (~):** 3D volumetric density field (layered fBm "Uber noise" + domain warping). Like Minecraft, terrain is a volumetric isosurface, not a 2D heightmap. Marked ~ for the same reason.
- **RimWorld (✓):** Perlin noise sampled on an icosphere surface to produce per-tile elevation. Operates at world-map scale (~2500 tiles), not at local-map pixel resolution. Local maps derive terrain from tile metadata, not from the elevation field directly.
- **Cataclysm:DDA (✗):** Overmap terrain is categorical (city, forest, field). No heightmap generation. Terrain types are land-use classifications, not elevation-derived.
- **Tangledeep (✗):** Dungeon floor generator. No overworld terrain or heightmap concept.

**Island/continent mask:**
- **Minecraft (✗):** Infinite world with no boundary. Continentalness noise creates ocean/land distribution without an explicit mask — the continentalness parameter is a *continuous field*, not a binary island mask applied to constrain generation.
- **Dwarf Fortress (~):** No explicit island mask. Continents and islands emerge from the elevation field after erosion. The world is finite and bounded, and ocean emerges at low elevation. However, world generation parameters control ocean percentage, which functions as an implicit constraint. Marked ~ because the effect is achieved through parameter tuning rather than a geometric mask.
- **No Man's Sky (—):** Planet-scale generation with spherical topology. No island/continent concept at the terrain generation level. Each planet is a continuous sphere.
- **RimWorld (~):** Coastlines emerge from elevation vs sea level threshold on the globe. No explicit mask, but the icosphere + elevation noise + sea level produces island/continent shapes. The world is spherical and finite. Similar to DF — the effect exists but the mechanism is elevation thresholding, not a geometric mask.
- **Cataclysm:DDA (✗):** No terrain elevation system. Overmap is an infinite grid of categorical terrain.
- **Tangledeep (—):** No overworld.

**Mountain shaping:**
- **Minecraft (✓):** Mountains emerge from the interaction of continentalness (high = far inland), erosion (low = mountainous), and Peaks-and-Valleys (high = peaks). The spline system maps these parameters to terrain offset, factor, and jaggedness. Additional 3D noise adds surface roughness at mountain peaks via the jaggedness parameter. This is confirmed and well-documented.
- **Dwarf Fortress (✓):** Mountains emerge from the elevation field (midpoint displacement produces natural mountain-range-like features). Mountain placement is a consequence of the fractal terrain generation process. Confirmed.
- **No Man's Sky (✓):** Per-planet noise parameters control terrain height range and feature scale. The Uber noise layer system with per-biome-type parameter blocks produces mountain-like features. Superformula shapes contribute to exotic mountain profiles. Confirmed from developer talks.
- **RimWorld (~):** Elevation noise produces height variation at world scale. Tiles with high elevation are classified as "mountainous." At local map scale, mountainous tiles get rock-filled terrain with mountain features. The shaping is coarse — no ridged noise or specific mountain shaping techniques are documented at the world-map level. Marked ~ because mountains exist but the shaping technique is basic elevation noise.
- **Cataclysm:DDA (✗):** No elevation-based terrain.
- **Tangledeep (—):** No overworld terrain.

**Cliffs/terraces:**
- **Minecraft (~):** No explicit terrace/cliff function. However, the spline system can produce steep transitions between terrain zones (e.g., the steep continentalness transition at coastlines). The factor parameter controls vertical compression, which can produce cliff-like features where factor is low. Marked ~ because the effect can emerge but is not an explicit technique.
- **Dwarf Fortress (?):** The game reports do not document explicit cliff or terrace generation. Steep terrain exists in-game, but whether it results from an explicit step function vs. natural fractal steepness is unclear. Insufficient evidence.
- **No Man's Sky (~):** The density field parameterization can produce cliff-like and plateau features. Terrain archetypes introduced in Origins (3.0) include distinct morphologies. Whether any of these use explicit step/quantization functions vs. noise shaping is not confirmed from available sources. Marked ~ as inferred from visual output.
- **RimWorld (✗):** No cliff/terrace system at either world or local scale. Local maps have "mountain" tiles that are solid rock, but these are binary (passable/impassable), not shaped cliffs.
- **Cataclysm:DDA (✗):** No elevation-based terrain.
- **Tangledeep (—):** No overworld terrain.

**Hydraulic erosion:**
- **Minecraft (✗):** Confirmed absent. The "erosion" parameter is a cosmetic noise field that influences terrain flatness via splines. No iterative simulation, no water-flow modeling, no terrain modification after initial generation.
- **Dwarf Fortress (✓):** Confirmed. A greedy agent-based channel carver — not physically-based hydraulic erosion. "Fake river" agents spawn at mountain edges (elevation ≥ ~300 on a 0–400 scale), trace steepest-descent paths, and lower terrain when no downhill neighbor exists. `EROSION_CYCLE_COUNT` (default 50, community-recommended ~250) controls iteration count. No sediment tracking — erosion is purely subtractive. No flow accumulation during erosion; agents operate independently. After erosion, "real" rivers are placed along carved channels, loop-erased, and lakes are grown at accumulation points. Worlds failing minimum river counts are rejected. Per-step erosion magnitude, agent density per cycle, and exact smoothing algorithm remain undocumented (proprietary codebase). *[Enhanced: DwarfFortress_Worldgen_gap_erosion.md]*
- **No Man's Sky (✗):** Confirmed absent. No simulation whatsoever in terrain generation — all features are pure mathematical functions.
- **RimWorld (✗):** No erosion simulation. Rivers are placed by pathfinding algorithms on the world graph, not by erosion.
- **Cataclysm:DDA (✗):** No terrain elevation system.
- **Tangledeep (—):** No overworld terrain.

**Thermal erosion:**
- **Minecraft (✗):** Absent.
- **Dwarf Fortress (✗):** Confirmed absent. Tarn Adams stated "Ideally we'd use mineral types for that, but we don't yet." DF's erosion is a single unified process: greedy agent-based channel carving with no freeze-thaw cycle, no slope-dependent weathering, and no material-property-weighted erosion rate. The `PERIODICALLY_ERODE_EXTREMES` smoothing pass reduces extreme height differences but is cliff-smoothing, not thermal erosion. *[Gap resolved: DwarfFortress_Worldgen_gap_erosion.md]*
- **No Man's Sky (✗):** Absent.
- **RimWorld (✗):** Absent.
- **Cataclysm:DDA (✗):** Absent.
- **Tangledeep (—):** Not applicable.

### 1.3 Islands Status: Terrain Shaping

| Technique | Islands Status | Consuming Phase | Notes |
|---|---|---|---|
| Heightmap from noise | **DONE** | Core pipeline | fBm-based elevation field exists in the mask/field layer system. |
| Island/continent mask | **DONE** | Core pipeline | Distance-based island mask applied to constrain land shape. |
| Mountain shaping | **NONE** | Phase M / future | No ridged noise, regional mountain masks, or elevation redistribution. |
| Cliffs/terraces | **NONE** | Future | No step functions or quantization applied to the heightmap. |
| Hydraulic erosion | **NONE** | Phase W / future | Not planned for near-term phases based on available roadmap context. |
| Thermal erosion | **NONE** | Future | Not planned for near-term phases. |

### 1.4 Observations: Terrain Shaping

The cross-check reveals a clear divergence pattern. Of the six games, only Dwarf Fortress implements erosion simulation — making it the outlier, not the norm. Every game that produces terrain (Minecraft, DF, NMS, RimWorld) uses some form of heightmap-from-noise and mountain shaping, but the *implementation architectures* differ sharply: Minecraft and NMS use volumetric density functions (implicit heightmaps), while DF and RimWorld use explicit 2D scalar fields. Islands' 2D grid-based pipeline aligns with the DF/RimWorld approach.

Island/continent masks are relevant only to finite-world generators (DF, RimWorld, and Islands). Infinite-world generators (Minecraft, NMS) achieve land/ocean distribution through continuous noise parameters rather than geometric masks. Islands already has this technique implemented.

Mountain shaping is universal among terrain generators but the *mechanism* varies: Minecraft uses spline-driven parameter interaction, DF uses fractal field emergence, NMS uses parameterized noise layers. For Islands' 2D grid pipeline, the most directly relevant approaches are regional masks applied to ridged multifractal noise (technique report 2a) or elevation redistribution via power curves — both compatible with the mask/field architecture.

Cliffs and terraces appear nowhere as explicit techniques in the corpus, though Minecraft and NMS produce cliff-like features as emergent properties of their respective systems. This suggests the technique is optional for visual richness rather than architecturally necessary.

Erosion is confirmed in only one game (DF), and gap research revealed it is simpler than expected — a greedy agent-based channel carver, not physically-based hydraulic erosion. No sediment tracking, no material properties, no flow accumulation. The technique produces convincing drainage networks through brute-force iteration (50–250 cycles). For Islands' Phase W (water systems), the cross-check now suggests two viable paths: (a) DF-style greedy carving (simple, ~30–50 lines of core logic, produces drainage channels), or (b) river placement by pathfinding on the elevation field (RimWorld's approach, even simpler). Both are compatible with Islands' grid-first architecture.

### 1.5 Priority Analysis: Terrain Shaping

**Tier 1 — High impact, directly relevant to Phase M/M2/W:**
- **Mountain shaping** (regional masks + ridged noise or power redistribution). Used by 4/4 terrain-generating games. Directly enriches the elevation field that Phase M consumes for altitude-based biome assignment. Without it, Islands' heightmap is monotonous fBm, producing unconvincing biome distribution.

**Tier 2 — Medium impact, extends richness:**
- **Cliffs/terraces** (step functions on heightmap). Not explicitly used by any game in the corpus, but straightforward to implement on a 2D grid and adds visual variety. Low cost, moderate payoff. Could be deferred to a post-M2 polish pass.
- **Hydraulic erosion** (particle-based on 2D grid). Only DF uses it. High implementation complexity. Sebastian Lague's droplet method is well-documented and grid-compatible. Payoff is realistic-looking terrain and natural river channel emergence — valuable for Phase W but not required for Phase M.

**Excluded — Not applicable to Islands' 2D grid pipeline:**
- **Thermal erosion.** Confirmed absent in all six games including DF (gap research resolved). No game in the corpus implements slope-dependent weathering or angle-of-repose enforcement as a distinct process. Skip entirely.
- **3D density functions / volumetric terrain.** Minecraft and NMS approach. Architecturally incompatible with Islands' 2D grid-first pipeline. Not applicable.

---

## 2. Biome and Region Systems — Game × Technique Status Grid

Techniques from Pass 5a: how space is classified into biomes and organized into regions.

### 2.1 Grid: Biome and Region Techniques

| Technique | Minecraft | Dwarf Fortress | No Man's Sky | RimWorld | Cataclysm:DDA | Tangledeep |
|---|---|---|---|---|---|---|
| **Whittaker temp-moisture model** | ~ | ✓ | ✗ | ✓ | ✗ | ✗ |
| **Altitude-based biome assignment** | ~ | ✓ | ✗ | ✓ | ✗ | — |
| **Rainfall/rainshadow approx.** | ✗ | ✓ | ✗ | ~ | ✗ | — |
| **Biome masks (noise-based)** | ✓ | ~ | ✗ | ~ | ~ | ~ |
| **Biome transition blending** | ✓ | ~ | — | ~ | ~ | ✗ |
| **Local vs global assignment** | Local | Global | Global | Global | Local | Local |
| **Voronoi region partitioning** | ~ | ✗ | ✗ | ✗ | ✗ | ✗ |
| **Connected component analysis** | ✗ | ~ | ✗ | ✗ | ✗ | ✗ |

### 2.2 Notes per cell

**Whittaker temperature-moisture model:**
- **Minecraft (~):** Not a Whittaker diagram, but structurally analogous. Biomes are assigned by finding the nearest point in a multi-dimensional parameter space (temperature, humidity, continentalness, erosion, weirdness). Temperature and humidity are the closest analogs to the Whittaker axes, but the lookup is a fixed, designer-authored table in 5D+ space, not a 2D temperature × moisture diagram. Marked ~ because the principle is similar (climate parameters → biome) but the implementation is substantially more complex and less physically motivated.
- **Dwarf Fortress (✓):** The closest to a true Whittaker model in the corpus. Temperature (from latitude + elevation) and rainfall (from simulation with rainshadow) combine with drainage and vegetation fields to determine biome. As Tarn Adams described, handling fields separately and letting biomes emerge from their interplay produced more natural results than direct biome spawning. Confirmed.
- **No Man's Sky (✗):** No temperature-moisture model. Biome is a per-planet discrete type selected by weighted random draw from a probability table keyed to star color and galaxy type. There is no within-planet climate variation.
- **RimWorld (✓):** Temperature (latitude + elevation + seasonal curves) and rainfall (Perlin noise) are the two primary axes for biome lookup. The implementation is a conditional logic tree (temperature ranges × rainfall ranges → biome type), which is functionally equivalent to a Whittaker diagram lookup table. Confirmed from source code analysis.
- **Cataclysm:DDA (✗):** No temperature-moisture model. Terrain types are land-use categories defined in region_settings JSON.
- **Tangledeep (✗):** No climate system. Floor themes are assigned by dungeon depth/progression.

**Altitude-based biome assignment:**
- **Minecraft (~):** Altitude does not directly determine biome. However, continentalness (which correlates with distance from ocean and therefore indirectly with typical elevation) and the erosion parameter together influence which biomes appear. Mountain biomes appear where continentalness is high AND erosion is low AND PV is high. The relationship between altitude and biome is mediated through the spline system, not direct. Marked ~ because the effect exists but the mechanism is indirect.
- **Dwarf Fortress (✓):** Elevation directly affects temperature (lapse rate), which feeds into biome determination. High-elevation tiles are colder, producing mountain/tundra biomes. Additionally, extreme elevation directly classifies tiles as "mountains" regardless of other climate factors. Confirmed.
- **No Man's Sky (✗):** Single biome per planet. No altitude-based variation.
- **RimWorld (✓):** Tiles with very high elevation are classified as mountainous biomes regardless of temperature/rainfall. Temperature decreases with elevation, pushing high-altitude tiles toward colder biome types. Both effects are confirmed.
- **Cataclysm:DDA (✗):** No elevation system.
- **Tangledeep (—):** No overworld terrain.

**Rainfall/rainshadow approximation:**
- **Minecraft (✗):** The humidity parameter is an independent noise field with no physical relationship to terrain, wind, or rainfall. Confirmed absent.
- **Dwarf Fortress (✓):** The standout implementation. Wind direction is established, moisture is carried from ocean tiles, and rain shadows form on the lee side of mountain ranges. Rainfall interacts with drainage to determine wetland formation. This is the most physically motivated moisture simulation in the corpus. Confirmed.
- **No Man's Sky (✗):** No rainfall system.
- **RimWorld (~):** Rainfall is generated from Perlin noise on the globe surface. It is NOT physically simulated — there is no wind direction, no rainshadow, no moisture transport. However, rainfall does vary spatially and feeds into biome assignment. Marked ~ because rainfall exists as a field but lacks physical simulation.
- **Cataclysm:DDA (✗):** No rainfall system.
- **Tangledeep (—):** Not applicable.

**Biome masks (noise-based):**
- **Minecraft (✓):** The five 2D noise parameters (temperature, humidity, continentalness, erosion, weirdness) are all independent noise fields that effectively serve as biome selection masks. Each noise field constrains which biomes can appear in a given region. Confirmed.
- **Dwarf Fortress (~):** Biomes emerge from the interaction of multiple scalar fields (elevation, temperature, rainfall, drainage, vegetation, volcanism). These fields function as masks in the sense that each constrains biome assignment, but they are not designed as explicit "biome masks" — they are climate/geological parameters. Marked ~ because the effect is present but the framing is different.
- **No Man's Sky (✗):** Planet-wide single biome. No spatial biome masking.
- **RimWorld (~):** Temperature and rainfall noise fields effectively mask where each biome can appear. The fields are computed globally on the sphere and then used for per-tile biome lookup. Similar to DF — the fields function as masks but are framed as climate parameters. Marked ~.
- **Cataclysm:DDA (~):** Forest placement uses noise thresholds (forest_threshold in region_settings). Swamp terrain uses similar noise-based distribution. These function as biome masks, though the system is simpler than climate-driven biome assignment. Marked ~.
- **Tangledeep (~):** Floor themes may use noise or weighted random for variety within a dungeon progression zone. Evidence is thin — marked ~ as inferred.

**Biome transition blending:**
- **Minecraft (✓):** The multi-point parameter lookup naturally produces smooth transitions because nearby positions have similar parameter values. Biome boundaries are not hard lines — there is a gradual parameter gradient. At the block level, biomes are assigned per-position in parameter space, and adjacent positions may sample different biomes, producing natural transitions. Confirmed.
- **Dwarf Fortress (~):** Each world tile is assigned a single biome type, but because biome assignment emerges from continuous scalar fields, adjacent tiles tend to have similar climate values and thus similar (or compatible) biomes. Whether there is explicit blending at tile boundaries during embark-level local generation is not documented. Marked ~ — inferred from continuous field inputs.
- **No Man's Sky (—):** Planet-wide single biome. No transitions to blend within a planet.
- **RimWorld (~→✗/PARTIAL):** **Vanilla pre-1.6 (✗):** Confirmed zero biome blending. Every cell uses `map.Biome` — a single `BiomeDef` for the entire local map. `GenStep_Terrain` never queries neighboring world tiles for biome data. Settling on a biome border produces identical maps to settling in a biome's center. **Odyssey DLC 1.6 (PARTIAL):** A "Mixed Biome" map feature activates with 20% probability on qualifying border tiles, splitting the local map into two zones along a noise-distorted line. Binary partition, not a gradient — each half uses its biome's terrain tables independently. **Biome Transitions mod (2022):** Introduced a `BiomeGrid` MapComponent — per-cell biome assignment as a compact `ushort[]` grid supporting up to 7 biomes per map. Harmony patches redirect plant, terrain, and animal spawning to per-cell biome queries. Open source (CC BY-NC-SA 4.0). *[Gap resolved: RimWorld_Worldgen_gap_biome_boundaries.md]*
- **Cataclysm:DDA (~):** Forest noise thresholds create gradual density transitions. Forest_thick → forest → field transitions emerge from the noise gradient. This is a form of implicit blending via threshold gradation. Marked ~.
- **Tangledeep (✗):** Hard floor-theme boundaries between dungeon levels. No blending.

**Local vs global biome assignment:**
- **Minecraft (Local):** Biomes are assigned per-position by evaluating noise functions at that coordinate. No global map is needed. Any position can determine its biome independently. This is the purest local assignment in the corpus.
- **Dwarf Fortress (Global):** The entire world map is generated upfront. All six scalar fields are computed for the full map before biome assignment begins. Biome assignment depends on global interactions (rainshadow requires knowing mountain positions relative to wind direction).
- **No Man's Sky (Global):** Planet biome type is selected once during planet seed evaluation. The selection is global to the planet (one biome per planet), not computed locally.
- **RimWorld (Global):** All world tiles are generated together. Temperature, rainfall, and elevation are computed globally on the sphere before biome assignment. Biome assignment requires the global context (latitude-based temperature, elevation-adjusted temperature).
- **Cataclysm:DDA (Local):** Each overmap (180×180) is generated independently on demand. Forest/field distribution uses local noise evaluation. No global map context.
- **Tangledeep (Local):** Each floor is generated independently. Theme assignment is per-floor, not from a global map.

**Voronoi region partitioning:**
- **Minecraft (~):** The multi-point biome parameter lookup functions similarly to a Voronoi partition in parameter space — each biome "owns" a region in the 5D parameter space, and cells sample the nearest biome point. However, this is not spatial Voronoi — it operates in parameter space, not geographic space. Marked ~ because the algorithmic principle is Voronoi-like but the application domain differs from traditional spatial partitioning.
- **Dwarf Fortress (✗):** Uses a regular grid (midpoint displacement on fixed-size grid). No Voronoi spatial partitioning. Regions are contiguous areas of the same biome type on the grid.
- **No Man's Sky (✗):** No spatial partitioning. Planet-wide uniform biome.
- **RimWorld (✗):** Uses icosphere tiles. No Voronoi partitioning.
- **Cataclysm:DDA (✗):** Uses a regular grid (overmap tiles). No Voronoi partitioning.
- **Tangledeep (✗):** No overworld spatial partitioning.

**Connected component analysis:**
- **Minecraft (✗):** Not used. Biomes are assigned per-position without regard to connectivity.
- **Dwarf Fortress (~):** Regions of the same biome type are implicitly contiguous areas. The game must identify connected river systems, ocean bodies, and landmasses for history simulation (civilizations need connected territory). Whether this uses explicit flood-fill/CCA or is handled differently is not documented in available reports. Marked ~ as inferred from requirements.
- **No Man's Sky (✗):** Not applicable (planet-wide single biome).
- **RimWorld (✗):** Not documented.
- **Cataclysm:DDA (✗):** Not documented.
- **Tangledeep (✗):** Connectivity verification exists within dungeon floors (flood fill for ensuring reachability) but this is dungeon-level, not region/biome-level.

### 2.3 Islands Status: Biome and Region Systems

| Technique | Islands Status | Consuming Phase | Notes |
|---|---|---|---|
| Whittaker temp-moisture model | **PLAN** | Phase M | Core of Phase M design — temperature + moisture fields → biome lookup. |
| Altitude-based biome assignment | **PLAN** | Phase M | Elevation field feeds temperature (lapse rate) and direct mountain biome override. |
| Rainfall/rainshadow approx. | **PLAN** | Phase M | Gap research confirmed: wind-sorted sweep, ~30 lines, <1ms. Recommended for Phase M. |
| Biome masks (noise-based) | **PARTIAL** | Phase M | Mask/field architecture supports this natively. Temperature + moisture fields ARE biome masks. |
| Biome transition blending | **NONE** | Phase M2 | Needed for Phase M2 vegetation placement near biome boundaries. |
| Local vs global assignment | **PLAN: Global** | Phase M | Islands generates a complete finite map — global assignment is the natural fit. |
| Voronoi region partitioning | **NONE** | Phase M2 | Candidate for named region generation. Decision pending. |
| Connected component analysis | **NONE** | Phase M2 | Needed for identifying contiguous biome regions for naming. |

### 2.4 Observations: Biome and Region Systems

**The temperature-moisture convergence is strong.** Three of the four games with geographic biomes (DF, RimWorld, and Minecraft-approximately) use some form of temperature + moisture → biome lookup. This validates Phase M's planned approach. The fourth (NMS) uses a fundamentally different model (planet-wide single biome) that is not relevant to Islands.

**Altitude-based biome assignment is universal** among games with elevation + biomes. Both DF and RimWorld use elevation to modify temperature (lapse rate) and to override biome assignment at extreme elevations. Phase M should incorporate both effects: (1) temperature field = base temperature − (elevation × lapse_rate), and (2) mountain biome override at high elevation regardless of moisture.

**Rainshadow is rare but now confirmed feasible at island scale.** Only DF simulates rainshadow, and gap research reveals DF's algorithm is a directional moisture sweep (wind-sorted, upwind-to-downwind) — simpler than expected. Amit Patel's mapgen4 uses an equivalent approach that adapts directly to regular grids. The minimum viable algorithm is ~30 lines of code, costs <1ms on a 256×256 grid, and produces visible wet/dry asymmetry on any mountain ridge spanning ≥10 tiles. The payoff is conditional: the biome system must be moisture-sensitive enough to express the gradient (8+ biomes with moisture-sensitive subtypes), and the island's mountains must be large enough to create meaningful shadow (20–40 tile spine). *[Gap resolved: 5a_gap_rainshadow_2d_grid.md]*

**Biome transition blending is now well-characterized from gap research.** Vanilla RimWorld confirmed zero blending pre-1.6; Odyssey DLC added a binary 2-biome split (not gradient blending). Minecraft achieves perceived smoothness through parameter-space continuity + cosmetic color blur, not feature-level blending. Five distinct algorithmic approaches have been documented from open-source implementations: (1) scattered-point normalized convolution producing sparse weight vectors per cell (KdotJPG), (2) query-time interpolation on a hard biome grid (AnotherCraft), (3) edge-factor scalar with stochastic placement sampling (TerraformGenerator), (4) noise-perturbed boundaries (Red Blob Games / AutoBiomes / Noita), and (5) Gaussian blur on a dense grid. The edge-factor approach (1 float per cell, ~100 lines) is the recommended starting point for Islands. *[Gaps resolved: 5a_gap_biome_blending_implementations.md, RimWorld_Worldgen_gap_biome_boundaries.md]*

**Local vs global assignment splits along world-size lines.** Infinite/streaming worlds (Minecraft, Cataclysm) use local assignment. Finite upfront worlds (DF, RimWorld) use global assignment. Islands generates a complete finite map, so global assignment is the natural fit — and enables rainshadow and other global-context techniques.

**Voronoi region partitioning is absent from all six games.** This is notable. Red Blob Games' mapgen2/mapgen4 uses Voronoi extensively, but no game in the research corpus does. DF, RimWorld, and Cataclysm all use regular grids. Minecraft uses per-position noise evaluation. The technique is well-documented in the PCG literature but the game evidence suggests that regular grids serve the same purpose for grid-based games. For Phase M2's named regions, connected component analysis on the biome map (identifying contiguous same-biome areas) may be simpler and more appropriate than Voronoi partitioning.

**Connected component analysis is implicit but undocumented.** DF almost certainly uses it for identifying landmasses and river systems, but the game reports don't document the mechanism. For Islands' Phase M2, CCA on the biome map is a well-understood technique (flood fill per biome type) that directly produces named regions.

### 2.5 Priority Analysis: Biome and Region Systems

**Tier 1 — High impact, directly relevant to Phase M/M2:**
- **Whittaker temperature-moisture model.** Used by 3/4 biome-capable games. Core of Phase M. Well-documented in technique report 5a. The evidence strongly supports temperature + moisture as the primary biome assignment axes.
- **Altitude-based biome assignment.** Used by 3/4 biome-capable games. Feeds directly into the temperature field (lapse rate) and provides mountain biome override. Essential for Phase M.
- **Biome masks (noise-based).** Already partially supported by Islands' mask/field architecture. Temperature and moisture fields ARE biome masks. Phase M naturally produces these.
- **Rainfall/rainshadow approximation.** Promoted from Tier 2. Gap research confirmed trivial implementation cost (~30 lines, <1ms) and direct grid compatibility. Transforms moisture from random noise into geographic consequence of terrain. Recommended for Phase M alongside base moisture noise.
- **Connected component analysis.** Required for Phase M2 named regions. Simple flood-fill algorithm. Every contiguous same-biome area becomes a nameable region.
- **Biome transition blending.** Required for Phase M2 vegetation placement. Gap research identified five concrete approaches; edge-factor scalar recommended as starting point.

**Tier 2 — Medium impact, extends richness:**
- **Voronoi region partitioning.** Not used by any game in the corpus for biome regions. Potentially useful for non-biome region structure (named geographic areas, political regions) but CCA on the biome map is simpler for the primary Phase M2 use case.
- **Local vs global assignment.** Global is the obvious choice for Islands. Not an implementation item — it's an architectural decision already settled by Islands' finite-map design.

**Excluded — Not applicable:**
- **NMS-style planet-wide single biome.** Irrelevant to Islands' requirements.
- **Minecraft-style 5D+ parameter space lookup.** Overengineered for Islands' needs. The 2D Whittaker model is sufficient for a finite 2D grid world.

---

## 3. Summary Matrix

Compact technique × game × Islands grid combining both families.

### 3.1 Terrain Shaping Summary

| Technique | MC | DF | NMS | RW | CDDA | Tang | Islands | Priority |
|---|---|---|---|---|---|---|---|---|
| Heightmap from noise | ~ | ✓ | ~ | ✓ | ✗ | ✗ | DONE | — |
| Island/continent mask | ✗ | ~ | — | ~ | ✗ | — | DONE | — |
| Mountain shaping | ✓ | ✓ | ✓ | ~ | ✗ | — | NONE | **Tier 1** |
| Cliffs/terraces | ~ | ? | ~ | ✗ | ✗ | — | NONE | Tier 2 |
| Hydraulic erosion | ✗ | ✓ | ✗ | ✗ | ✗ | — | NONE | Tier 2 |
| Thermal erosion | ✗ | ✗ | ✗ | ✗ | ✗ | — | NONE | Excluded |

### 3.2 Biome and Region Systems Summary

| Technique | MC | DF | NMS | RW | CDDA | Tang | Islands | Priority |
|---|---|---|---|---|---|---|---|---|
| Whittaker temp-moisture | ~ | ✓ | ✗ | ✓ | ✗ | ✗ | PLAN | **Tier 1** |
| Altitude-based biome | ~ | ✓ | ✗ | ✓ | ✗ | — | PLAN | **Tier 1** |
| Rainfall/rainshadow | ✗ | ✓ | ✗ | ~ | ✗ | — | PLAN | **Tier 1** |
| Biome masks (noise) | ✓ | ~ | ✗ | ~ | ~ | ~ | PARTIAL | **Tier 1** |
| Biome transition blend | ✓ | ~ | — | ~ | ~ | ✗ | NONE | **Tier 1** |
| Local vs global assign | L | G | G | G | L | L | PLAN:G | — |
| Voronoi region partition | ~ | ✗ | ✗ | ✗ | ✗ | ✗ | NONE | Tier 2 |
| Connected component analysis | ✗ | ~ | ✗ | ✗ | ✗ | ✗ | NONE | **Tier 1** |

---

## 4. Recommended Updates to technique_integration_matrix.md

The following rows should be added, changed, or annotated:

### Rows to add:
1. **Mountain shaping** — Status: NONE. Priority: Tier 1. Consuming phase: pre-Phase M (heightmap enrichment). Note: "Used by 4/4 terrain generators. Regional mask + ridged noise recommended. Directly feeds altitude-based biome assignment."
2. **Cliffs/terraces** — Status: NONE. Priority: Tier 2. Consuming phase: post-M2 polish. Note: "Not explicitly used by any game in corpus. Low-cost heightmap post-processing."
3. **Hydraulic erosion** — Status: NONE. Priority: Tier 2. Consuming phase: Phase W. Note: "Only DF uses it. High complexity. Consider after river placement is designed."
4. **Biome transition blending** — Status: NONE. Priority: Tier 1. Consuming phase: Phase M2. Note: "Recommended approach: noise-perturbed boundaries (zero cost) + edge-factor scalar per cell (1 float, ~100 lines). Gives placement systems a 0–1 boundary distance signal. Upgrade path: scattered-point convolution (KdotJPG) for smooth parameter interpolation if needed. See 5a_gap_biome_blending_implementations.md."
5. **Connected component analysis** — Status: NONE. Priority: Tier 1. Consuming phase: Phase M2. Note: "Required for named regions. Simple flood-fill. Not explicitly documented in any game but functionally necessary."
6. **Rainfall/rainshadow approximation** — Status: PLAN. Priority: **Tier 1** (promoted after gap research). Consuming phase: Phase M. Note: "Wind-sorted moisture sweep, ~30 lines, <1ms on 256×256. Modulates base moisture noise with terrain-driven rainshadow. Recommended for Phase M alongside Whittaker model."

### Rows to update:
7. **Whittaker temperature-moisture model** — If already present, update to: Status: PLAN. Priority: Tier 1. Note: "Confirmed in DF and RimWorld. Minecraft uses analogous but more complex 5D lookup. Core of Phase M."
8. **Altitude-based biome assignment** — If already present, update to: Status: PLAN. Priority: Tier 1. Note: "Confirmed in DF and RimWorld. Two effects: lapse rate on temperature + mountain override."
9. **Voronoi region partitioning** — If already present, annotate: "Not used by any game in corpus for biome regions. Consider CCA on biome map instead for Phase M2 named regions."
10. **Island/continent mask** — Update to: Status: DONE. Note: "Relevant only to finite-world generators. DF and RimWorld achieve similar effect via elevation thresholding."

### Rows to annotate (no status change):
11. **Heightmap from noise** — Annotate: "MC and NMS use volumetric density (implicit heightmap). DF and RW use explicit 2D fields. Islands aligns with DF/RW approach."

---

## 5. Identified Gaps

### 5.1 Gaps in game reports

| Gap | Affected technique | Status | Resolution |
|---|---|---|---|
| DF thermal erosion | Thermal erosion | **RESOLVED** | Confirmed absent. DF uses a single unified erosion process (greedy agent carving). No thermal erosion, no material properties. See `DwarfFortress_Worldgen_gap_erosion.md`. |
| DF connected component analysis | CCA | Open (low priority) | DF must identify connected landmasses and river basins, but the mechanism is undocumented. CCA is algorithmically trivial (flood fill) — the gap is "does DF use it," not "how does CCA work." |
| RimWorld biome transition at local scale | Biome transition blending | **RESOLVED** | Vanilla pre-1.6: confirmed zero blending (`map.Biome` is a scalar). Odyssey 1.6: binary 2-biome split on ~20% of border tiles. Biome Transitions mod (2022): per-cell `BiomeGrid` with up to 7 biomes. See `RimWorld_Worldgen_gap_biome_boundaries.md`. |
| NMS terrain archetypes | Cliffs/terraces, mountain shaping | Open (low priority) | Origins 3.0 introduced 10 terrain archetypes. Whether any use explicit step/quantization is undocumented. Cliffs/terraces are Tier 2 for Islands. |
| Tangledeep biome-like systems | Biome masks | **Skipped** | Irrelevant to overworld biome needs. |
| Cataclysm region_settings detail | Biome masks, transition blending | **Skipped** | Architectural mismatch with climate-based biomes. |

### 5.2 Gaps in technique reports

| Gap | Affected technique | Status | Resolution |
|---|---|---|---|
| Biome blending implementation details | Biome transition blending | **RESOLVED** | Five distinct approaches documented from open-source implementations. Edge-factor scalar recommended for Islands. See `5a_gap_biome_blending_implementations.md`. |
| Rainshadow on finite 2D grids | Rainfall/rainshadow | **RESOLVED** | Wind-sorted moisture sweep confirmed feasible at island scale. ~30 lines, <1ms on 256×256. Conditional payoff depends on biome granularity and mountain size. See `5a_gap_rainshadow_2d_grid.md`. |

---

## 6. Phase M Design Implications

These are concrete findings from the cross-check that should feed into Phase M and Phase M2 design decisions. They are analysis, not implementation proposals.

### 6.1 Findings for Phase M (Climate & Biome)

**Finding 1: Temperature + moisture is the validated biome assignment model.** All three games with geographic biomes (DF, RimWorld, Minecraft-approximately) derive biomes from climate parameters on at least two axes. Altitude-only assignment is insufficient — no game in the corpus uses elevation alone for biome selection. Phase M's planned Whittaker-style approach is well-supported.

**Finding 2: Altitude feeds temperature, not biome directly.** Both DF and RimWorld implement altitude→temperature via a lapse rate, then feed the modified temperature into the biome lookup. Only at extreme elevations does altitude override biome assignment directly (mountain biome). Phase M should use: `effective_temperature = base_temperature - (elevation × lapse_rate)`, with a separate mountain override threshold.

**Finding 3: Rainshadow is feasible and recommended for Phase M.** Gap research confirmed that a wind-sorted moisture sweep (mapgen4 pattern) adapts directly to regular grids at trivial cost (~30 lines, <1ms on 256×256). DF's algorithm is equivalent in principle — directional sweep with moisture depletion by elevation. Starting Phase M with a combined approach is recommended: generate a base moisture noise field, then modulate it with a single-pass rainshadow sweep using the elevation field and a prevailing wind direction parameter. This transforms rainfall from random noise into a geographic consequence of terrain. The payoff is conditional on two factors: (a) the biome system should distinguish 8+ biomes with moisture-sensitive subtypes, and (b) the island generator should produce mountain spines of ≥20 tiles for the effect to be visible. If either condition is unmet, the rainshadow pass still runs at negligible cost and can be tuned later. *[Updated from gap research: 5a_gap_rainshadow_2d_grid.md]*

**Finding 4: Global assignment enables richer climate.** Islands' finite map + global generation makes rainshadow, latitude-based temperature gradients, and coast-distance effects all feasible — unlike streaming generators (Minecraft, CDDA) which must evaluate locally. Phase M should exploit this advantage.

**Finding 5: Mountain shaping should precede Phase M.** The elevation field directly feeds altitude-based biome assignment. Without mountain shaping (regional masks, ridged noise, or power redistribution), the fBm heightmap produces uniformly distributed elevation, which makes altitude-based biome assignment produce uniformly distributed biome rings rather than geographically interesting biome distribution. Mountain shaping is a terrain-enrichment step that should execute before Phase M's climate fields are computed.

### 6.2 Findings for Phase M2 (Biome-aware vegetation + named regions)

**Finding 6: Biome blending is now a solved design question with a clear recommendation.** Gap research documented five distinct approaches. For Islands' 2D grid with hard biome-per-cell assignment and downstream vegetation placement, the recommended architecture is: (a) **noise-perturbed biome boundaries** at assignment time (zero-cost, ~20 lines of domain warping before biome evaluation, produces organic boundary shapes), plus (b) **edge-factor scalar** per cell (1 float, ~100 lines of boundary detection, gives downstream placement systems a 0–1 distance-from-boundary signal for stochastic sampling). This two-layer approach maintains the hard-enum biome grid while producing natural-looking vegetation transitions. If higher quality is later needed, upgrading to scattered-point convolution (KdotJPG pattern, ~500 lines, sparse weight vectors) provides smooth parameter interpolation. The RimWorld `BiomeGrid` pattern (per-cell biome assignment grid) confirms that the approach works in practice but requires all downstream systems to query per-cell rather than per-map biome — a design decision Islands should make from the start, not retrofit. *[Updated from gap research: 5a_gap_biome_blending_implementations.md, RimWorld_Worldgen_gap_biome_boundaries.md]*

**Finding 7: Named regions via CCA is simpler than Voronoi.** No game in the corpus uses Voronoi for biome regions. Connected component analysis on the biome map (flood fill per biome type) naturally identifies contiguous biome areas. Each connected component becomes a nameable region. This is simpler, more predictable, and directly compatible with Islands' grid architecture. Voronoi could be reserved for non-biome region structure (political regions, quest zones) if needed later.

**Finding 8: The biome blending data contract is now resolved in favor of hard-enum + edge-factor.** The gap research confirms that a hard biome map (one enum per cell) with a supplementary edge-factor float (distance from nearest biome boundary, normalized 0–1) gives downstream consumers everything they need without requiring weighted-combination logic. Vegetation placement checks the edge factor: deep in biome interior (factor > 0.8) → sample from primary biome table at full density; near boundary (factor < 0.3) → stochastically sample from adjacent biome's table with distance-weighted probability. This avoids the 6b-identified problem of forcing all downstream systems to handle weighted biome vectors. The RimWorld Biome Transitions mod validates that per-cell biome queries work, but also demonstrates the retrofitting cost — Islands should store per-cell biome assignment (enum) + edge factor (float) from the start, even if early phases only write a uniform biome. Total memory overhead: 6 bytes per cell (2-byte biome enum + 4-byte float). *[Updated from gap research]*

---

## 7. Appendix: Source Authority Notes

This analysis draws on the following source documents (referenced by filename in the Islands PCG Research project):

**PCG technique reports:**
- `2a_Heightmap_construction__shaping__and_erosion.md` — Terrain shaping technique definitions, erosion algorithms, pipeline ordering
- `5a_Region_partitioning_and_biome_systems.md` — Biome/region technique definitions, Whittaker model, blending approaches
- `pass_6b_composition_patterns_and_synthesis.md` — Composition patterns, data structure catalog, technique-by-game-problem lookup

**Game worldgen reports:**
- `Minecraft_Worldgen.md` — 3D density function, 5D climate noise, spline-driven terrain, biome parameter lookup
- `DwarfFortress_Worldgen.md` — Midpoint displacement, 6 scalar fields, agent-based erosion, rainshadow simulation, emergent biomes
- `NMS_World_Generation_Pipeline.md` — Uber noise layers, per-planet biome selection, volumetric density, no simulation
- `RimWorld_Worldgen.md` — Icosphere tiles, Perlin elevation/rainfall, latitude-based temperature, biome lookup table, world→local transition
- `Cataclysm_Worldgen_and_Z_Levels.md` — Overmap categorical terrain, region_settings JSON, forest noise thresholds, z-level stack
- `Tangledeep_and_Rogues.md` — Dungeon floor generation, no overworld, floor themes by progression

**Cross-reference and planning docs:**
- `noise_cross_reference.md` — Pattern followed for this analysis (noise technique family)
- `technique_integration_matrix.md` — Update target for findings
- `Phase_M_Design.md` — Climate & Biome phase design
- `Phase_M2_Design.md` — Biome-aware vegetation + named regions phase design

**Gap research files (produced after initial cross-reference to resolve identified gaps):**
- `5a_gap_biome_blending_implementations.md` — Five concrete blending approaches from open-source implementations. Resolves blending design question for Phase M2.
- `5a_gap_rainshadow_2d_grid.md` — Six rainshadow approaches for 2D grids. Confirms feasibility at island scale. Promotes rainshadow to Tier 1 for Phase M.
- `DwarfFortress_Worldgen_gap_erosion.md` — DF erosion algorithm detail from Tarn Adams interviews and community analysis. Confirms single unified process (no thermal), greedy agent-based carving, purely subtractive.
- `RimWorld_Worldgen_gap_biome_boundaries.md` — RimWorld local-map biome boundary treatment. Confirms zero blending pre-1.6, binary split in Odyssey 1.6, per-cell BiomeGrid in Biome Transitions mod.

**Confidence notes (updated after gap research):**
- Minecraft evidence is the strongest (Wiki documentation, JSON data files, Kniberg presentations).
- Dwarf Fortress evidence is now strong for erosion (Tarn Adams interviews, community parameter testing) but the per-step erosion magnitude and agent density remain black-boxed (proprietary codebase). CCA mechanism still undocumented.
- RimWorld evidence is now strong for biome boundaries (decompiled source confirms zero blending pre-1.6; Odyssey 1.6 and Biome Transitions mod implementations documented in detail).
- NMS evidence is moderate (developer talks + community datamining, but exact noise implementations remain partially speculative).
- Cataclysm evidence is strong for architecture but the game's terrain system is categorically different from heightmap-based generation, limiting cross-reference utility.
- Tangledeep evidence is thin and largely irrelevant to terrain/biome techniques (dungeon roguelike, no overworld).
