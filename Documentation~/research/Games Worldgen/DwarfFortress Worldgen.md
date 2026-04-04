# How Dwarf Fortress builds a world from scratch

**Dwarf Fortress generates world geography through a 14-stage simulation pipeline that transforms six fractal noise fields into a coherent, interacting physical landscape.** The system starts with midpoint displacement to seed elevation, rainfall, temperature, drainage, volcanism, and savagery onto a grid, then runs erosion agents, carves rivers, adjusts rainfall for orographic effects, classifies biomes from the interplay of all fields, and finally assigns geological layers and regional identities. What makes this pipeline distinctive is not the sophistication of any single algorithm — midpoint displacement is a 1980s technique — but the sheer depth of layered post-processing and the principle that biomes *emerge* from interacting physical variables rather than being placed directly. Tarn Adams, the sole developer, has described the full pipeline in interviews spanning 2008–2022, and those first-party descriptions form the backbone of this reconstruction.

---

## A. Executive summary of the generation pipeline

The geography pipeline proceeds in a strict sequence driven by data dependencies. First, a rectangular world grid is allocated and a pole configuration chosen. Six primary scalar fields — elevation, rainfall, temperature, drainage, volcanism, and savagery — are seeded on a coarse mesh and interpolated fractally via midpoint displacement. Temperature is then skewed by latitude. Peak locations are selected at maximum elevation, and the world undergoes its first rejection check against player-specified parameter ranges.

Vegetation is derived as the first composite field, and biome feasibility is tested. Mid-level elevations are smoothed to create plains, and volcanoes are placed at volcanism hotspots. The system then enters the erosion-and-river stage: temporary "fake rivers" flow downhill from mountain bases, carving channels into the elevation field. Real rivers are placed along these carved paths, and lakes grow at points along them. A second pass smooths elevations from mountains to sea. Rainfall is then refined with rain shadows and orographic precipitation. Temperatures are recalculated incorporating elevation, moisture, and forest dampening. Vegetation is set one final time. Salinity is assigned to ocean and coastal tiles. Contiguous biome areas are detected as named regions, and geological layers are assigned. A final parameter check runs, wildlife is distributed, and weather variables are initialized. Only then does history simulation begin.

**The entire geographic pipeline is deterministic from a seed** — the same seed and parameters on the same version produce an identical world.

---

## B. Step-by-step pipeline

### Stage 1: Memory allocation and pole selection
- **What is generated:** The world grid data structure is allocated. A pole configuration is chosen (or respected from player parameters): NORTH, SOUTH, NORTH_AND_SOUTH, NORTH_OR_SOUTH, or NONE.
- **Inputs:** World size parameter (pocket 17×17 through large 257×257, always (2^n)+1), pole setting.
- **Outputs:** Empty grid ready for field seeding; pole configuration that will drive the latitude temperature gradient.
- **Ordering rationale:** Must precede all other stages — the grid is the data structure everything writes into.
- **Confidence:** **Confirmed.** Tarn Adams: "It allocates the memory for the map. Then it chooses what sort of pole (e.g. north, south) it is going to have."

### Stage 2: Fractal field seeding and interpolation
- **What is generated:** Six primary scalar fields — **elevation** (0–400), **rainfall** (0–100), **temperature**, **drainage** (0–100), **volcanism** (0–100), and **savagery/wildness** (0–100) — are seeded at coarse mesh points and interpolated to fill the full grid using **midpoint displacement**.
- **Inputs:** PRNG world seed, mesh size settings, min/max values and X/Y variance parameters for each field, weighted mesh frequency arrays (6 range-weights per field), ocean edge parameters.
- **Outputs:** Six complete 2D scalar fields covering the world grid.
- **Ordering rationale:** All subsequent stages read from these fields. Everything downstream is either a transformation of, or derivation from, these six layers.
- **Confidence:** **Confirmed.** Tarn Adams: "The basic map field values (elevation, rainfall, temperature, drainage, volcanism, wildness) are seeded along a grid of variable size, respecting various settings (oceans, island sizes, other variances, etc.), and then filled in fractally." Algorithm confirmed as midpoint displacement: "The original implementation uses a mid-point displacement technique; that's how it's been for 7 years" (AiGameDev interview). A **non-linear parabola** is then applied to elevation "so the mountains are bent to look more realistic."

### Stage 3: Pole temperature adjustment and peak selection
- **What is generated:** The temperature field is modified by a latitude gradient based on the pole configuration chosen in Stage 1. Points at maximum elevation (400) are selected as mountain peaks.
- **Inputs:** Temperature field, pole configuration, elevation field.
- **Outputs:** Latitude-adjusted temperature field; designated peak locations.
- **Ordering rationale:** Temperature must incorporate latitude before any biome calculations. Peaks must be identified before the first rejection check evaluates mountain count.
- **Confidence:** **Confirmed.** Tarn Adams: "The poles vary the temperature, and it selects some points for the highest peaks."

### Stage 4: First rejection check
- **What is generated:** The system evaluates whether the world's elevation distribution fits the desired parameters. Altitudes may be adjusted. If the world is unfixable, it is rejected and regeneration begins from scratch.
- **Inputs:** All six fields, player parameter ranges (minimum mountains, elevation ranges, etc.).
- **Outputs:** Possibly adjusted elevation field; or a rejection signal triggering restart.
- **Ordering rationale:** Early rejection avoids wasting computation on worlds that can never satisfy constraints. Must occur after fields are generated but before expensive erosion/river stages.
- **Confidence:** **Confirmed.** Tarn Adams: "Here it does a first pass to see how it is doing, and attempts to adjust some altitudes to fit the map within the desired parameters if it missed. The world can be rejected at this point if it is unfixable, and it tries again."

### Stage 5: Initial vegetation derivation and biome rejection check
- **What is generated:** **Vegetation** is calculated as the first derived field, based on elevation, rainfall, temperature, and drainage. The system then tests whether the resulting biome distribution satisfies parameter ranges.
- **Inputs:** Elevation, rainfall, temperature, drainage fields.
- **Outputs:** Vegetation field; biome feasibility assessment; possible rejection.
- **Ordering rationale:** Vegetation and preliminary biome classification are needed to validate the world before committing to erosion. This is a cheap check that prevents expensive downstream computation on bad maps.
- **Confidence:** **Confirmed.** Tarn Adams: "The first derived field, vegetation, is then set based on elevation, rainfall, temperature, etc., and it tests for biome rejections if the map's biomes don't satisfy the ranges set in the parameters."

### Stage 6: Mid-level elevation smoothing and volcano placement
- **What is generated:** Mid-level elevations (between ocean and mountain thresholds) are smoothed to create broader plains. Volcanoes are placed at tiles where the volcanism field is at its maximum (100).
- **Inputs:** Elevation field, volcanism field, `VOLCANO_MIN` parameter.
- **Outputs:** Smoother elevation field with more gradual terrain transitions; volcano locations.
- **Ordering rationale:** Smoothing must happen before erosion so that erosion agents operate on a more realistic landscape. Volcanoes must be placed before erosion so they can influence the terrain.
- **Confidence:** **Confirmed.** Tarn Adams: "The mid-level elevations are smoothed at this point to make more plains areas, and volcanoes are placed respecting the hot spots in the volcanism field."

### Stage 7: Erosion and river generation
- **What is generated:** This is the most complex geographic stage, occurring in multiple sub-phases:
  1. Small inland oceans are "dried out" (removed or reclassified)
  2. Mountain base edges are identified (elevation ≥ ~300)
  3. **Temporary "fake rivers"** flow downhill from mountain bases, choosing the lowest-elevation neighbor at each step. Where no lower neighbor exists, the algorithm **digs into the terrain**, lowering that tile's elevation. This carves channels through the elevation field.
  4. Extreme elevation differences are smoothed to prevent universal canyons
  5. **Real permanent rivers** are placed along the carved channels
  6. **Lakes** are grown at several points along rivers
  7. River loops from erosion are fixed; flow amounts calculated for tributaries; rivers named
- **Inputs:** Elevation field (post-smoothing), mountain locations, `EROSION_CYCLE_COUNT`, `PERIODICALLY_ERODE_EXTREMES` toggle, river head min/max parameters.
- **Outputs:** Significantly modified elevation field with carved valleys; river network with flow data; lake locations.
- **Ordering rationale:** Erosion must follow initial smoothing (Stage 6) but precede rainfall refinement (Stage 9), because the final terrain shape determines rain shadow patterns. Rivers require a finalized mountain landscape to flow from.
- **Confidence:** **Confirmed.** Tarn Adams provided extensive detail: "Many fake rivers flow downward from these points, carving channels in the elevation field if they can't find a path to the sea. Extreme elevation differences are often smoothed here so that everything isn't canyons. Ideally we'd use mineral types for that, but we don't yet. Lakes are grown out at several points along the rivers."

### Stage 8: Final elevation smoothing and peak/volcano adjustments
- **What is generated:** Elevations are smoothed a second time "from the mountains down to the sea." Peaks and volcanoes perform local elevation adjustments.
- **Inputs:** Post-erosion elevation field, peak and volcano locations.
- **Outputs:** Finalized elevation field — the terrain is now in its permanent form.
- **Ordering rationale:** Elevation must be finalized before rainfall can be accurately adjusted for orographic effects.
- **Confidence:** **Confirmed.** Tarn Adams: "Elevations are smoothed again from the mountains down to the sea, and the peaks and volcanoes do some local adjustments."

### Stage 9: Rainfall refinement (orographic precipitation and rain shadows)
- **What is generated:** Rainfall values are adjusted based on terrain. Mountains force moist oceanic air upward, increasing rainfall on the windward side and creating rain shadows (dry zones) on the leeward side.
- **Inputs:** Finalized elevation field, rainfall field, drainage field (rain shadows only apply where drainage ≥ 50), `OROGRAPHIC_PRECIPITATION` toggle.
- **Outputs:** Refined rainfall field reflecting terrain-driven climate patterns.
- **Ordering rationale:** Depends on finalized elevation. Must precede final temperature and vegetation calculations.
- **Confidence:** **Confirmed.** Tarn Adams: "Now that the elevations are finalized, it makes adjustments to rainfall based on rain shadows and orographic precipitation." The drainage ≥ 50 constraint for rain shadow application is documented on the DF wiki as a confirmed behavior.

### Stage 10: Temperature recalculation and final vegetation
- **What is generated:** Temperatures are recalculated incorporating finalized elevation, refined rainfall, and "the dampening effects of forests." Vegetation is then set one final time using all updated values.
- **Inputs:** Finalized elevation, refined rainfall, current vegetation/forest coverage.
- **Outputs:** Final temperature field; final vegetation field.
- **Ordering rationale:** Temperature depends on finalized elevation and rainfall. Vegetation depends on final temperature. This is the last derivation step before biome classification.
- **Confidence:** **Confirmed.** Tarn Adams: "Temperatures are reset based on elevation and rainfall and the dampening effects of forests, and it uses the new values to set the vegetation level one final time."

### Stage 11: Salinity assignment
- **What is generated:** Salinity values are assigned. Ocean tiles receive salinity 100 automatically. Coastal tiles receive graduated salinity values.
- **Inputs:** Elevation field (to identify ocean), tile adjacency.
- **Outputs:** Salinity field (determines freshwater/brackish/saltwater lake and wetland variants).
- **Ordering rationale:** Must follow finalized elevation (to know which tiles are ocean). Must precede biome region detection (salinity differentiates biome subtypes).
- **Confidence:** **Confirmed.** Tarn Adams: "Salinity values are set for the ocean and tiles neighboring the ocean."

### Stage 12: Region detection, naming, and geological layer assignment
- **What is generated:** Contiguous areas of the same biome type are detected and grouped into named **regions**. Each region receives a procedurally generated name and identity. **Geological layers** (soil, sedimentary, metamorphic, igneous intrusive/extrusive) and underground structures (cavern layers, magma sea) are assigned.
- **Inputs:** All finalized fields (elevation, rainfall, drainage, temperature, salinity, volcanism, vegetation), biome classification rules, cavern parameters.
- **Outputs:** Named biome regions with boundaries; per-region geological column; underground structure.
- **Ordering rationale:** Can only occur after all fields are finalized. Adams noted geological assignment "should really be earlier" but currently happens here.
- **Confidence:** **Confirmed.** Tarn Adams: "Now that everything has settled down, we can detect the limits of the final biome regions and give them names and their own identity. We also add the geological layers and the underground layers here, though the geological stuff should really be earlier, as previously mentioned."

### Stage 13: Final verification
- **What is generated:** A final check against all player parameters. If the world has drifted too far from specifications during post-processing, it may be rejected.
- **Inputs:** All fields, all parameter ranges.
- **Outputs:** Acceptance or rejection of the world.
- **Confidence:** **Confirmed.** Tarn Adams: "There's a final verification process against the parameters here, to make sure it hasn't drifted too far afield from what the player wanted."

### Stage 14: Wildlife population and weather initialization
- **What is generated:** Initial wildlife populations are distributed across regions based on biome suitability. Weather variables are initialized.
- **Inputs:** Named biome regions, creature definitions.
- **Outputs:** Populated regions; weather state. **Geography generation is now complete.** History simulation begins after this point.
- **Confidence:** **Confirmed.** Tarn Adams: "Once that's done, it generates the initial wildlife populations in each region, and sets some weather variables."

---

## C. Pipeline dependency map

The dependency structure reveals why the pipeline must proceed in this specific order. Each arrow represents a data dependency where the downstream stage reads from the upstream stage's output.

```
PRNG Seed + Parameters
        │
        ▼
┌─────────────────────────────────────────────┐
│  Stage 2: Fractal Field Generation          │
│  (elevation, rainfall, temperature,         │
│   drainage, volcanism, savagery)            │
└─────────┬───────────────────────────────────┘
          │
          ▼
┌─────────────────────────────────────────────┐
│  Stage 3: Pole Temperature + Peak Selection │
│  (latitude gradient applied to temperature; │
│   peaks identified at elevation 400)        │
└─────────┬───────────────────────────────────┘
          │
          ▼
┌──────────────────────────┐
│  Stage 4: Rejection #1   │──── reject? → restart from Stage 2
└─────────┬────────────────┘
          │
          ▼
┌──────────────────────────────────────┐
│  Stage 5: Vegetation + Biome Check   │──── reject? → restart
└─────────┬────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 6: Elevation Smoothing + Volcanoes    │
│  (volcanism field → volcano placement)       │
└─────────┬────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 7: Erosion + Rivers + Lakes           │
│  (elevation → fake rivers → channel carving  │
│   → real rivers → lakes)                     │
└─────────┬────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 8: Final Elevation Smoothing          │
│  (elevation finalized permanently)           │
└─────────┬────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 9: Orographic Rainfall Refinement     │
│  (finalized elevation → rain shadows)        │
└─────────┬────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 10: Temperature Recalc + Vegetation   │
│  (elevation + rainfall + forests → temp;     │
│   all fields → final vegetation)             │
└─────────┬────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────┐
│  Stage 11: Salinity          │
└─────────┬────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 12: Regions + Geology + Underground   │
│  (all fields → biome classification →        │
│   region grouping → geological columns)      │
└─────────┬────────────────────────────────────┘
          │
          ▼
┌──────────────────────────┐
│  Stage 13: Rejection #3  │──── reject? → restart
└─────────┬────────────────┘
          │
          ▼
┌──────────────────────────────────────────────┐
│  Stage 14: Wildlife + Weather                │
│  (regions → fauna distribution)              │
└─────────┬────────────────────────────────────┘
          │
          ▼
     HISTORY SIMULATION BEGINS
```

**Critical dependency chains:**

- **Elevation → erosion → final elevation → rain shadows → temperature → biomes.** This is the longest dependency chain and explains why elevation is generated first and biome regions are detected near the end.
- **Drainage → rain shadow eligibility.** Rain shadows only apply where drainage ≥ 50, creating a coupling between two otherwise independent fractal fields.
- **Volcanism → volcano placement → local elevation adjustments.** The volcanism field drives volcano locations, which then modify the elevation field.
- **All fields → biome classification → regions → geology.** Geology depends on everything upstream, which is why Adams acknowledged it should be earlier but isn't.

---

## D. Geographic subsystems

### D1. Elevation and landmass formation

**Algorithm: midpoint displacement with post-processing.** The elevation map is the foundational geographic layer. Values are seeded on a coarse grid (mesh size is configurable) and interpolated fractally using midpoint displacement — a recursive subdivision algorithm where the midpoint of each grid segment is assigned a value interpolated from its neighbors plus a random displacement that decreases with each recursion level. World dimensions must be **(2^n)+1** (17, 33, 65, 129, or 257 tiles per side), which is a signature constraint of midpoint displacement algorithms.

After the fractal fill, a **non-linear parabola** is applied to elevation values to "bend the mountains to look more realistic." This likely compresses low elevations and stretches high elevations, producing flatter plains and more dramatic peaks — a common post-processing technique.

**Elevation ranges** span 0–400 in the world-scale field. Tiles at **0–99 become ocean**, **100–299 become land biomes**, and **300–400 become mountains**, with **400 reserved for peaks**. These thresholds are the primary mechanism for landmass formation: the fractal field creates a continuous height surface, and the sea-level cutoff at ~100 determines coastline geometry. There is no explicit continent-shaping algorithm — landmasses emerge from the interplay of fractal noise, seeding parameters, and ocean edge constraints.

**X and Y variance parameters** (maximum 3,200) control how rapidly elevation changes between adjacent tiles. Higher variance produces more rugged, fragmented terrain; lower variance creates smoother, more continental landscapes. **Weighted mesh frequencies** allow fine-tuning the probability distribution across six elevation bands, giving players control over how much of the world is ocean versus plains versus mountains.

### D2. Mountain ranges and the absence of tectonic simulation

**Dwarf Fortress does not simulate tectonic plates.** This is a notable absence compared to some procedural generators. Mountain ranges are not placed by modeling plate collision — they emerge purely from the fractal elevation field. Clusters of tiles with elevation ≥ 300 form contiguous mountain regions, and their linear or branching shape is an artifact of fractal noise characteristics rather than any explicit tectonic logic.

**Mountain peaks** are special features at the maximum elevation value of 400. The system "selects some points for the highest peaks" after the initial fractal generation. A `PEAK_NUMBER_MIN` parameter sets the minimum acceptable peak count, and worlds with too few peaks are rejected. After erosion, peaks undergo "local adjustments" — likely small elevation corrections to maintain their prominence after surrounding terrain has been carved.

The result is that **mountain ranges in DF tend to look somewhat realistic** despite the simple generation method, because the fractal noise naturally produces elongated ridges of high values, and erosion later carves realistic valleys between them.

### D3. Oceans, coastlines, and islands

Ocean formation is implicit: any tile with elevation below the sea-level threshold (~100) becomes ocean. Two key parameters control ocean extent at the world boundary:

- `PARTIAL_OCEAN_EDGE_MIN/MAX`: How many map edges have partial ocean coverage
- `COMPLETE_OCEAN_EDGE_MIN/MAX`: How many edges are fully ocean-covered

Settings of 4 complete ocean edges produce island worlds. Settings of 0–1 produce continental maps. Coastlines are simply the contour where elevation crosses the sea-level threshold, meaning their complexity and realism depend entirely on the fractal field's resolution and variance parameters.

During the erosion stage, "small oceans are dried out" — isolated low-elevation depressions that would form unrealistic inland seas are removed. Ocean tiles are classified into **Arctic, Temperate, or Tropical** variants based on the temperature field at their location. Coastal tiles receive graduated salinity values that influence whether adjacent wetlands are freshwater, brackish, or saltwater.

### D4. Rainfall and climate

Rainfall is generated in two phases. The **initial phase** seeds a fractal rainfall field identically to elevation — midpoint displacement on a coarse grid, interpolated to the full world. The **refinement phase** occurs much later (Stage 9), after elevations are finalized, and implements **orographic precipitation**.

The orographic model works as follows: moist air from ocean areas blows over land. When terrain height increases, air is forced upward, causing precipitation on the windward side of mountains. If the mountain is tall enough, all moisture is exhausted, and the leeward side receives dramatically less rainfall — a **rain shadow**. This is toggleable via the `OROGRAPHIC_PRECIPITATION` parameter (on by default).

A confirmed quirk: **rain shadows only apply where drainage ≥ 50.** This creates an unintuitive coupling between two fractal fields and means that low-drainage areas (which would become swamps) are immune to rain shadow drying.

Rain shadow adjustments can push rainfall values **outside the configured min/max range**, meaning even a world with max rainfall set to 0 can produce wet biomes if orographic effects are strong enough.

Adams specifically cited rain shadows as a major quality improvement: "The world maps improved greatly when rain shadows were taken into consideration."

### D5. Temperature

Temperature is influenced by three factors in order of dominance:

1. **Latitude** (strongest effect): A vertical gradient always exists based on pole configuration. With NORTH_AND_SOUTH poles, both map edges are cold and the center is warm. The wiki confirms: "Temperature appears to always be a vertical gradient of some sort no matter how these parameters are set."
2. **Elevation**: Higher terrain is colder, a standard adiabatic lapse effect.
3. **Local fractal variation**: The temperature fractal field adds regional variation, but this is relatively minor compared to latitude and elevation effects.

Temperature is recalculated late in the pipeline (Stage 10) after elevations and rainfall are finalized. This recalculation also incorporates **forest dampening** — dense vegetation moderates temperature extremes, creating a feedback loop where forests (which require certain temperature ranges) themselves influence local temperature.

**Temperature thresholds for biome classification:**
- ≤ −5: Freezing biomes (tundra, glacier, arctic ocean)
- −4 to 9: Cold biomes (taiga replaces conifer forest)
- 10 to ~84: Temperate biomes
- ≥ 85: Tropical variants of all applicable biomes

### D6. Drainage

Drainage is the most conceptually unusual of the six fractal fields. It does **not** model water flow direction or watershed geometry. Instead, it represents a **static material property** — how readily the ground absorbs and channels water. The drainage field is generated identically to elevation (midpoint displacement, same mesh/variance system) and ranges from 0 to 100.

Its primary function is **biome differentiation**:
- **Low drainage + high rainfall** → standing water → swamps, marshes
- **High drainage + high rainfall** → water absorbed → forests
- **Low drainage + low rainfall** → grasslands, mudflats
- **High drainage + low rainfall** → badlands, rocky wasteland

Adams described this as a non-obvious design insight: "Oddly enough, drainage was another nonobvious consideration that helped smoothly delineate forests from swamps." Drainage also affects **soil depth** at embark scale (lower drainage → thicker soil layers) and gates rain shadow eligibility (≥ 50 required).

### D7. River generation

River generation is the most algorithmically complex geographic stage, operating through a distinctive two-phase process.

**Phase 1 — Erosion (fake rivers):** The system identifies mountain base edges (all tiles with elevation ≥ ~300). From these edges, many temporary river agents flow downhill. At each step, an agent moves to the **lowest-elevation neighbor**. If no neighbor is lower, the agent **digs into the current tile**, lowering its elevation. This continues until the agent reaches the ocean or gets permanently stuck. The camera is intentionally centered on a mountain during this phase so the player can watch mountains being worn down in real time. The `EROSION_CYCLE_COUNT` parameter controls how many iterations of this process run.

**Phase 2 — Real rivers:** After erosion creates viable channels to the ocean, permanent rivers are placed along these paths. River flow amounts are calculated (presumably by summing contributing upstream area), tributaries are identified, and rivers are given procedurally generated names. Lake locations are determined during this stage — lakes "grow out at several points along the rivers," likely at depressions along the carved channels.

Adams noted a limitation: "Ideally we'd use mineral types for that, but we don't yet" — meaning erosion doesn't currently consider rock hardness, treating all terrain identically.

### D8. Lakes and inland seas

Lakes form as a byproduct of river generation. When river channels create depressions without direct ocean outlets, or at specific points along river paths, lakes are "grown out." The growth mechanism is not precisely documented, but lakes are classified by **salinity** (freshwater, brackish, saltwater) and **temperature** (temperate or tropical).

During this stage, "small oceans are dried out" — inland depressions that are too large to be lakes but too small to be proper oceans are eliminated. Higher erosion cycle counts and higher elevation variability tend to produce more lakes.

### D9. Erosion modeling

DF's erosion model is **purely fluvial** — water-based. There is no thermal erosion, wind erosion, or tectonic uplift during the erosion phase. The algorithm is an **agent-based, greedy path-tracer** rather than a physics-based hydraulic simulation. Each erosion agent takes the locally optimal path downhill and modifies terrain only when stuck.

After the main erosion phase, two additional smoothing passes occur:
1. "Extreme elevation differences are often smoothed here so that everything isn't canyons"
2. "Elevations are smoothed again from the mountains down to the sea"

These smoothing passes are critical — without them, the greedy carving algorithm would produce unrealistically sharp terrain. The `PERIODICALLY_ERODE_EXTREMES` toggle controls whether extreme cliff smoothing occurs during erosion cycles.

### D10. Biome assignment

Biomes are classified from the interaction of four primary fields following a strict priority hierarchy:

**Step 1 — Elevation gates:**
- Elevation 0–99 → Ocean (Arctic / Temperate / Tropical based on temperature)
- Elevation 300–400 → Mountain
- Elevation 100–299 → Proceed to rainfall/drainage classification

**Step 2 — Rainfall × drainage classification** (approximate thresholds for elevation 100–299):

| Rainfall | Drainage 0–32 | Drainage 33–65 | Drainage 66–100 |
|----------|---------------|-----------------|------------------|
| 0–9 | Sand Desert | Rocky Wasteland | Badlands |
| 10–32 | Grassland | Shrubland | Shrubland |
| 33–65 | Marsh / Grassland | Grassland / Savanna | Savanna / Shrubland |
| 66–100 | Swamp | Forest (mixed) | Forest (broadleaf/conifer) |

**Step 3 — Temperature modifiers** create climate variants:
- Frozen (≤ −5): Tundra or Glacier
- Cold (−4 to 9): Taiga
- Temperate (10–84): Standard biome names
- Tropical (≥ 85): Tropical variants, including Mangrove Swamp where drainage ≤ 9

**Step 4 — Salinity** differentiates freshwater/brackish/saltwater wetlands and lakes.

Adams articulated the core design philosophy: "When creating terrain, it is tempting to spawn particular biomes or allow a fractal to directly define the biomes. However, Dwarf Fortress achieved much better results by handling fields separately: temperature, rainfall, elevation, drainage, etc. The interplay of those fields determined the final biome, resulting in a more natural, internally consistent solution."

### D11. Geological layers, stone, and minerals

Geological layer assignment occurs **late** in the pipeline (Stage 12), after all surface geography is finalized. Adams acknowledged this is suboptimal: "the geological stuff should really be earlier."

**Four primary layer types** are modeled on real stratigraphic principles:

1. **Soil** (topmost, 0–10+ z-levels deep): Varies by biome — clay, sand, loam, peat. Lower drainage correlates with thicker soil.
2. **Sedimentary** (80% chance of being the first stone layer below soil): Sandstone, limestone, chalk, shale. Richest source of **iron ores** (hematite, limonite, magnetite), **coal** (lignite, bituminous), and **flux stone**.
3. **Metamorphic** (can appear at any level except topmost or below igneous intrusive): Quartzite, marble, schist, slate.
4. **Igneous intrusive** (always the deepest layer, just above the magma sea): Granite, gabbro, diorite. Contains the most valuable gems.
5. **Igneous extrusive** (replaces sedimentary near high-volcanism areas): Basalt, obsidian, rhyolite.

The **volcanism field** controls the prevalence of igneous layers. Higher volcanism values increase igneous extrusive occurrence.

**Mineral veins** occur in four shapes: large clusters, veins (1–4 tiles wide, ~50 tiles long), small clusters (1–9 tiles), and single gems. Specific minerals are tied to specific layer types. The `MINERAL_SCARCITY` parameter (100 = everywhere, 50,000 = very rare) globally scales mineral frequency. Some mineral placement appears to be **determined at dig-time** rather than world generation, using the world seed deterministically.

Geological layers are assigned **per biome** — different biomes within a single embark can have entirely different geological columns, which creates interesting gameplay dynamics at biome boundaries.

### D12. Volcano placement

Volcanoes require a volcanism field value of **exactly 100** at their tile. They are placed during Stage 6, "respecting the hot spots in the volcanism field." In the current implementation, volcanoes are **one-embark-tile features**: hollow pillars of obsidian filled with magma, extending from the surface to the magma sea below. They do not erupt or produce dynamic geological effects.

A volcano tile adjacent to a mountain tile is treated as having elevation 400 (mountain). The `VOLCANO_MIN` parameter sets the minimum acceptable count; worlds with fewer volcanoes are rejected. After erosion and the second elevation smoothing, "peaks and volcanoes do some local adjustments" — likely minor elevation corrections to maintain their geological prominence.

### D13. Region and local map scale relationships

**Three spatial scales** exist in Dwarf Fortress:

| Scale | Tile dimensions | Real-world equivalent |
|-------|----------------|----------------------|
| World map | 17×17 to 257×257 region tiles | ~20–480 km across |
| Region tile (embark screen) | Each region tile = 16×16 local tiles | ~3 km per region tile |
| Local tile (fortress play) | Each local tile = 48×48 in-game tiles | ~96×96 meters |

**Generated at world scale:** All six fractal fields, biome classification, river networks, lake locations, volcano placements, region boundaries, geological column assignments, civilization sites, history.

**Generated at embark scale:** 3D z-level topography (the single world elevation value is expanded into a detailed 3D heightmap), specific stone layer composition and mineral vein placement, soil depth, cavern geometries, aquifer placement, detailed river channels, and specific flora/fauna placement.

---

## E. Algorithmic reconstruction

### E1. Master pipeline pseudocode — **mostly confirmed**

```
FUNCTION generate_world(seed, params):
    rng = initialize_prng(seed)
    grid = allocate_grid(params.world_size)    // (2^n)+1 dimensions
    pole = select_pole(params.pole_config, rng)

    // STAGE 2: Fractal field generation
    FOR field IN [elevation, rainfall, temperature, drainage, volcanism, savagery]:
        mesh = create_coarse_mesh(params[field].mesh_size)
        seed_mesh_values(mesh, params[field].min, params[field].max,
                         params[field].weights, rng)
        grid[field] = midpoint_displacement_fill(mesh, 
                         params[field].x_variance, params[field].y_variance, rng)
    
    // Post-process elevation
    grid.elevation = apply_nonlinear_parabola(grid.elevation)
    
    // STAGE 3: Latitude temperature + peaks
    apply_latitude_gradient(grid.temperature, pole)
    peaks = select_peak_locations(grid.elevation, threshold=400)
    
    // STAGE 4: First rejection check
    IF NOT check_elevation_params(grid.elevation, params):
        attempt_altitude_adjustment(grid.elevation, params)
        IF still_invalid: REJECT → restart
    
    // STAGE 5: Vegetation + biome check
    grid.vegetation = derive_vegetation(grid.elevation, grid.rainfall,
                                         grid.temperature, grid.drainage)
    biomes = classify_biomes(grid)
    IF NOT check_biome_params(biomes, params): REJECT → restart
    
    // STAGE 6: Smoothing + volcanoes
    smooth_midlevel_elevations(grid.elevation, ocean_threshold=100,
                                mountain_threshold=300)
    volcanoes = place_volcanoes(grid.volcanism, threshold=100,
                                min_count=params.volcano_min)
    
    // STAGE 7: Erosion + rivers + lakes
    dry_out_small_oceans(grid.elevation)
    mountain_edges = find_mountain_bases(grid.elevation, threshold=300)
    
    FOR cycle IN range(params.erosion_cycle_count):
        FOR start IN mountain_edges:
            run_fake_river(grid.elevation, start)
            // At each step: move to lowest neighbor;
            // if no lower neighbor, dig current tile
        IF params.erode_extremes:
            smooth_extreme_cliffs(grid.elevation)
    
    rivers = run_real_rivers(grid.elevation, mountain_edges)
    lakes = grow_lakes_along_rivers(rivers, grid.elevation)
    fix_river_loops(rivers)
    calculate_flow_amounts(rivers)
    name_rivers(rivers, rng)
    
    // STAGE 8: Final elevation smoothing
    smooth_elevations_mountain_to_sea(grid.elevation)
    adjust_peaks_locally(peaks, grid.elevation)
    adjust_volcanoes_locally(volcanoes, grid.elevation)
    
    // STAGE 9: Orographic rainfall
    IF params.orographic_precipitation:
        apply_rain_shadows(grid.rainfall, grid.elevation, grid.drainage,
                           drainage_threshold=50)
    
    // STAGE 10: Temperature recalc + final vegetation
    grid.temperature = recalculate_temperature(grid.elevation, grid.rainfall,
                                                grid.vegetation, pole)
    grid.vegetation = derive_vegetation(grid.elevation, grid.rainfall,
                                         grid.temperature, grid.drainage)
    
    // STAGE 11: Salinity
    grid.salinity = assign_salinity(grid.elevation, ocean_threshold=100)
    
    // STAGE 12: Regions + geology
    regions = detect_biome_regions(grid)
    name_regions(regions, rng)
    FOR region IN regions:
        region.geology = assign_geological_layers(region.biome,
                            grid.volcanism, params.mineral_scarcity)
        region.underground = generate_caverns(params.cavern_layers)
    
    // STAGE 13: Final verification
    IF NOT final_parameter_check(grid, regions, params): REJECT → restart
    
    // STAGE 14: Wildlife + weather
    populate_wildlife(regions, rng)
    initialize_weather(grid, rng)
    
    RETURN world(grid, regions, rivers, lakes, volcanoes, peaks)
```

### E2. Midpoint displacement subroutine — **partial reconstruction**

```
// Confidence: PARTIAL RECONSTRUCTION
// The general algorithm is confirmed; specific displacement scaling
// and boundary handling are inferred from standard implementations.

FUNCTION midpoint_displacement_fill(mesh, x_var, y_var, rng):
    grid_size = world_size    // must be (2^n)+1
    step = grid_size - 1
    
    // Initialize corners/mesh points from seeded values
    // (already done during mesh seeding)
    
    WHILE step > 1:
        half = step / 2
        displacement_scale = scale_from_variance(x_var, y_var, step)
        
        // Midpoint step: interpolate center of each square
        FOR each square of size (step × step):
            center = average_of_corners(square)
            center += rng.random(-displacement_scale, +displacement_scale)
            grid[center_x][center_y] = center
        
        // Edge midpoints: average of adjacent endpoints
        FOR each edge midpoint:
            value = average_of_neighbors
            value += rng.random(-displacement_scale, +displacement_scale)
            grid[edge_x][edge_y] = value
        
        step = half
    
    RETURN clamp(grid, min_value, max_value)
```

### E3. Fake river erosion subroutine — **mostly confirmed**

```
// Confidence: MOSTLY CONFIRMED
// Core logic directly described by Tarn Adams.
// Specific "dig depth" and termination conditions are inferred.

FUNCTION run_fake_river(elevation_grid, start_pos):
    pos = start_pos
    max_steps = some_limit    // prevents infinite loops
    
    FOR step IN range(max_steps):
        neighbors = get_adjacent_tiles(pos)
        lowest = min(neighbors, key=elevation)
        
        IF elevation[lowest] < elevation[pos]:
            // Flow downhill
            pos = lowest
        ELSE:
            // Stuck — dig into current position
            elevation[pos] -= dig_amount
            // Continue flowing from new lower position
        
        IF elevation[pos] < ocean_threshold:
            // Reached the sea — done
            RETURN
    
    // Got stuck after max_steps — river terminates
    RETURN
```

### E4. Biome classification subroutine — **mostly confirmed**

```
// Confidence: MOSTLY CONFIRMED
// Threshold values are community-researched and may have
// minor inaccuracies across game versions.

FUNCTION classify_biome(elevation, rainfall, drainage, temperature, salinity):
    IF elevation < 100:
        IF temperature <= -5: RETURN ARCTIC_OCEAN
        IF temperature >= 85: RETURN TROPICAL_OCEAN
        RETURN TEMPERATE_OCEAN
    
    IF elevation >= 300:
        RETURN MOUNTAIN
    
    IF temperature <= -5:
        IF drainage >= 75: RETURN GLACIER
        RETURN TUNDRA
    
    // Main biome classification for elevation 100–299
    IF rainfall < 10:
        IF drainage < 33: RETURN SAND_DESERT
        IF drainage < 66: RETURN ROCKY_WASTELAND
        RETURN BADLANDS
    
    IF rainfall < 33:
        IF drainage < 33: RETURN GRASSLAND
        RETURN SHRUBLAND
    
    IF rainfall < 66:
        IF drainage < 33: RETURN MARSH(salinity)
        IF drainage < 66: RETURN GRASSLAND_OR_SAVANNA
        RETURN SAVANNA_OR_SHRUBLAND
    
    // rainfall >= 66
    IF drainage < 33: RETURN SWAMP(salinity, temperature)
    IF drainage < 66: RETURN FOREST_MIXED(temperature)
    RETURN FOREST(temperature)    // broadleaf or conifer
    
    // Temperature then selects tropical/temperate/taiga variant
```

### E5. Rain shadow subroutine — **speculative synthesis**

```
// Confidence: SPECULATIVE SYNTHESIS
// Adams described the effect conceptually but not the implementation.
// The algorithm below is a plausible reconstruction based on the
// described behavior and standard orographic precipitation models.

FUNCTION apply_rain_shadows(rainfall, elevation, drainage, drain_threshold):
    // Determine dominant wind directions (likely from ocean toward land,
    // or possibly from a fixed prevailing direction — UNKNOWN)
    
    FOR each wind_direction:
        FOR each row/column along wind_direction:
            moisture = initial_moisture_from_ocean_proximity
            
            FOR each tile along wind path:
                IF drainage[tile] < drain_threshold:
                    CONTINUE    // rain shadows don't apply here
                
                elev_change = elevation[tile] - elevation[previous_tile]
                
                IF elev_change > 0:
                    // Terrain rising — force precipitation
                    precip = moisture * orographic_factor * elev_change
                    rainfall[tile] += precip
                    moisture -= precip
                    moisture = max(0, moisture)
                ELSE:
                    // Terrain falling or flat — rain shadow
                    // Moisture stays depleted
                    rainfall[tile] = max(rainfall[tile] - shadow_factor, 0)
```

---

## F. Worked example

Consider a small **33×33 pocket world** with NORTH_AND_SOUTH poles and default parameters.

**Stage 2 — Field generation.** The PRNG seeds a 5×5 coarse mesh for each field. Midpoint displacement fills the 33×33 grid. Elevation results in a central ridge running east-west (values 250–400) with lowlands (50–150) to the north and south. The north and south edges have tiles below 100 (ocean). Rainfall is generally moderate (40–70) across most of the map. Drainage varies irregularly.

**Stage 3 — Temperature and peaks.** With NORTH_AND_SOUTH poles, temperature is coldest at both the north and south map edges (~−10) and warmest at the center (~75). Three tiles at elevation 400 along the central ridge are designated as peaks.

**Stage 4 — Rejection check.** The elevation distribution has sufficient ocean (north and south strips), sufficient mountains (central ridge), and enough plains. World passes.

**Stage 5 — Vegetation and biome check.** Preliminary biomes include arctic ocean along the northern edge, tundra/taiga in the cold-but-above-sea-level northern lowlands, temperate forests and grasslands in the mid-latitudes approaching the mountains, mountain biomes along the ridge, and mirror patterns to the south. Biome counts satisfy minimums. World passes.

**Stage 6 — Smoothing and volcanoes.** Elevation tiles in the 120–250 range are gently smoothed, creating broader plains between the ocean strips and the central ridge. One tile in the central mountains has volcanism = 100 and becomes a volcano.

**Stage 7 — Erosion and rivers.** The system identifies ~8 mountain base tiles. Fake rivers flow north and south from these points, carving V-shaped valleys into the ridge's flanks. One particularly deep channel on the north side drops elevation from 280 to 130 over 6 tiles, creating a valley. Real rivers are placed: two major rivers flow north to the northern ocean, one flows south. A small lake forms in a depression along the northern river where erosion created a local minimum.

**Stage 8 — Final smoothing.** Elevation gradients from the ridge to the oceans are smoothed. The peak at elevation 400 and the volcano retain their height through local adjustments.

**Stage 9 — Rain shadows.** The central mountain ridge blocks moisture from the northern ocean. Southern slopes of the ridge, which face away from the northern ocean, experience reduced rainfall (dropping from ~55 to ~25 in a band 3–4 tiles south of the ridge). A small rain shadow desert appears south of the highest peaks. Conversely, the northern slopes receive enhanced rainfall (~65–80).

**Stage 10 — Temperature and vegetation recalculation.** The forested northern mid-latitudes, with high rainfall and moderate drainage, develop moderate vegetation that slightly dampens temperature extremes. Southern mid-latitudes are drier due to the rain shadow, resulting in grassland and shrubland with less dampening.

**Stage 11 — Salinity.** Northern and southern ocean tiles receive salinity 100. Coastal tiles get salinity ~66–33 depending on distance from ocean.

**Stage 12 — Regions and geology.** The system detects ~15 named regions: an Arctic Ocean ("The Frozen Sea"), a taiga belt ("The Cold Pines"), temperate broadleaf forests ("The Verdant Thicket"), the mountain range ("The Granite Spires"), a temperate grassland/shrubland south of the mountains ("The Dry Steppe"), and so on. The mountain region is assigned an igneous intrusive base layer (granite) with metamorphic (quartzite) above it. The northern forests get thick soil over sedimentary limestone. The volcano tile gets an igneous extrusive column (basalt, obsidian).

**Stage 13 — Verification.** All parameters satisfied. World accepted.

**Stage 14 — Wildlife.** Arctic ocean receives polar bears, walruses. Taiga receives wolves, elk. Forests receive deer, foxes. Grasslands receive horses, elephants. Mountains receive mountain goats. The world is ready for history simulation.

---

## G. Comparative analysis

### Midpoint displacement vs. modern noise functions

DF uses **midpoint displacement**, a fractal terrain algorithm published by Fournier, Fussell, and Carpenter in 1982. Most modern procedural generators — Minecraft, No Man's Sky, and tools like libnoise and FastNoise — use **Perlin or Simplex noise** with fractal Brownian motion (fBm). Noise-based methods offer smoother output, finer control over frequency content, and freedom from grid-alignment artifacts that midpoint displacement can produce. However, Adams noted the technique is "actually OK the way it is" because the initial heightmap is so heavily post-processed that the base algorithm matters less than the simulation layers applied afterward.

### No tectonic simulation

Tools like WorldEngine and Tectonics.js generate mountain ranges by simulating plate collisions, producing geologically plausible continent boundaries and orogeny. **DF skips this entirely** — mountains are just fractal peaks that happen to cluster. This is simpler and faster but means DF mountain ranges lack the characteristic linear patterns of subduction zones or rift valleys. The tradeoff is acceptable because DF's erosion and climate post-processing add enough coherence to make the landscape feel realistic at gameplay scale.

### Erosion: agent-based path-tracing vs. hydraulic simulation

Academic terrain generation increasingly uses **hydraulic erosion simulation**, where virtual water droplets are tracked across the surface, picking up and depositing sediment based on velocity, slope, and carrying capacity. This produces highly realistic drainage networks and valley profiles. DF's erosion is a **coarser, agent-based approach**: rivers simply flow downhill and dig when stuck. This is computationally cheaper and produces plausible valleys, but it cannot model sediment transport, alluvial fans, or differential erosion by rock type (Adams explicitly noted this limitation).

### Climate model: distinctive among games

DF's **orographic precipitation model** is unusual in game worldgen. Most games either ignore climate entirely, derive biomes from noise-based moisture maps, or use simple latitude bands. DF's implementation of rain shadows — where mountains block moisture and create leeward deserts — produces geographically recognizable patterns (analogous to the Atacama, Gobi, or Great Basin deserts). This single feature dramatically increases map coherence and was cited by Adams as one of the biggest quality improvements.

### Biome classification: multi-field emergence vs. direct placement

Most games either place biomes directly (Voronoi cells with assigned types) or use a **Whittaker biome diagram** mapping temperature and precipitation to biome type. DF goes further by incorporating **six interacting variables** (elevation, rainfall, drainage, temperature, volcanism, salinity). The drainage field in particular is distinctive — it creates smooth, physically motivated transitions between forests and swamps that other approaches typically handle with discrete boundaries or additional noise layers.

### The rejection system: uncommon but effective

DF's **generate-and-test** approach — creating worlds, checking them against parameters, and discarding failures — is unusual. Most generators either constrain generation to always produce valid output (e.g., Minecraft) or accept whatever emerges. DF's approach allows simpler generation algorithms because the rejection system handles quality control post-hoc. The cost is occasional long generation times when parameters are restrictive, with up to 85+ rejections before finding an acceptable world.

### What is truly distinctive about DF

**The individual algorithms are not particularly advanced.** As the AiGameDev analysis concluded: "Each of the algorithms or models in the game, taken on their own, might not be very complex. However, it's the combination of these systems that really makes the difference." DF's distinctiveness lies in three areas: the **number of interacting layers** (more than almost any other game), the **simulation-based post-processing pipeline** that transforms simple fractal data into coherent geography, and the **integration between geography and history** where centuries of simulated civilization interact with the physical landscape.

---

## H. Open questions and poorly documented areas

**1. Rain shadow wind direction.** Adams described the orographic effect conceptually but never specified how wind direction is determined. Is it always from the nearest ocean? A fixed prevailing direction? Computed from some atmospheric model? This is the most significant algorithmic gap in the documented pipeline.

**2. Non-linear parabola details.** Adams mentioned applying "a non-linear parabola so the mountains are bent to look more realistic" but never specified the function. Is it a power curve (elevation^k)? A piecewise function? The exact transfer function is unknown.

**3. Lake growth algorithm.** Lakes are "grown out at several points along the rivers," but the growth mechanism — whether it's a flood-fill to a water-level threshold, a fixed-area expansion, or something else — is not documented.

**4. Exact biome thresholds.** The biome classification thresholds (rainfall 33/66, drainage 33/66, temperature −5/9/85) are community-researched through experimentation and are approximate. They may differ slightly across game versions, and edge cases are not fully mapped.

**5. Geological layer assignment algorithm.** How exactly are stone types chosen for each layer within a biome? Is there a weighted random selection from a biome-specific pool? How are cross-biome layer transitions handled? This is poorly documented.

**6. Interaction between mesh weighting and midpoint displacement.** The weighted mesh system (6-value frequency arrays per field) interacts with midpoint displacement seeding, but the exact mechanism — whether weights control initial mesh value probability or displacement magnitude — is not fully explained.

**7. "Small oceans dried out" mechanism.** During erosion, small inland oceans are removed, but the size threshold and the method (raising elevation? reclassifying tiles?) is undocumented.

**8. Exact erosion dig amount.** When a fake river agent is stuck and digs, how much is the elevation reduced per step? Is it a fixed amount, proportional to surrounding elevation, or variable?

**9. Vegetation derivation formula.** Vegetation is "derived from elevation, rainfall, temperature, drainage," but the exact formula or lookup table is not documented.

**10. Post-erosion river path selection.** After erosion carves channels, how exactly are "real" rivers placed? Do they trace the carved channels exactly, or is there an additional pathfinding step?

**11. Future myth generator integration.** Adams described plans for a creation myth generator where "the cosmic egg can leave fragments that form continents and landforms on the map generator." As of 2026, it is unclear how far this integration has progressed and whether it has altered the geography pipeline.

---

## Best current reconstruction: the complete pipeline as a numbered list

1. **Allocate world grid** at (2^n)+1 dimensions (17 to 257 per side)
2. **Select pole configuration** (NORTH, SOUTH, BOTH, EITHER, or NONE)
3. **Seed six primary fields** on a coarse mesh: elevation, rainfall, temperature, drainage, volcanism, savagery
4. **Fill all fields fractally** via midpoint displacement, respecting variance and weighted mesh parameters
5. **Apply non-linear parabola** to the elevation field to reshape mountain profiles
6. **Apply latitude temperature gradient** based on pole configuration
7. **Select mountain peak locations** at maximum elevation (400)
8. **First rejection check**: verify elevation distribution; attempt to adjust; reject and restart if unfixable
9. **Derive vegetation** from elevation, rainfall, temperature, and drainage
10. **Check biome feasibility**: classify preliminary biomes; reject if minimum biome counts not met
11. **Smooth mid-level elevations** (between ocean and mountain thresholds) to create plains
12. **Place volcanoes** at tiles where volcanism = 100
13. **Dry out small inland oceans** (remove or reclassify isolated low-elevation depressions)
14. **Identify mountain base edges** as starting points for erosion
15. **Run erosion cycles**: temporary river agents flow downhill from mountain bases, digging into terrain when stuck, carving channels toward the ocean
16. **Smooth extreme elevation differences** to prevent universal canyons
17. **Place permanent rivers** along carved channels
18. **Grow lakes** at depressions along river paths
19. **Fix river loops**, calculate flow volumes, identify tributaries, name rivers
20. **Second elevation smoothing** from mountains to sea
21. **Apply local peak and volcano elevation adjustments**
22. **Refine rainfall** with orographic precipitation and rain shadows (where drainage ≥ 50)
23. **Recalculate temperature** incorporating final elevation, rainfall, and forest dampening
24. **Set final vegetation** from all updated fields
25. **Assign salinity** to ocean tiles (100) and coastal tiles (graduated)
26. **Classify biomes** from final elevation × rainfall × drainage × temperature × salinity
27. **Detect contiguous biome regions** and assign names
28. **Generate geological layers** per region (soil → sedimentary/igneous extrusive → metamorphic → igneous intrusive)
29. **Generate underground structure** (cavern layers, magma sea, underworld)
30. **Final verification** against all player parameters; reject and restart if failed
31. **Distribute wildlife** across regions based on biome suitability
32. **Initialize weather variables**
33. **Geography complete** → history simulation begins

*Steps 1–7 and 9–32 are confirmed by Tarn Adams's own descriptions. Steps 8 and 30 (rejection points) are confirmed at a general level, with exact rejection criteria partially documented. Specific sub-step ordering within the erosion phase (steps 13–21) is a partial reconstruction based on the sequence Adams described. Biome thresholds in step 26 are community-researched approximations. The rain shadow wind model in step 22 remains undocumented.*