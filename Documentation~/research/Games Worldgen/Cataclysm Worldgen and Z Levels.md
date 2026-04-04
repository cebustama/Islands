# Cataclysm: Dark Days Ahead world generation architecture

**Cataclysm: Dark Days Ahead generates its infinite post-apocalyptic world through a three-tier pipeline: a lazily generated 180×180-tile overmap defines abstract terrain layout, local mapgen concretizes each 24×24-tile area from JSON definitions when the player approaches, and 21 discrete z-levels (-10 to +10) stack vertically to form buildings, basements, and underground complexes.** This architecture produces a world that is simultaneously data-driven and procedural — the overmap's C++ algorithms place cities, rivers, roads, and specials using noise functions and pathfinding, while local tile content is defined almost entirely through moddable JSON templates and palettes. Z-levels were grafted onto what was originally a 2D engine, and this ancestry shows in constraints like per-level mapgen definitions, asymmetric z-offset support, and an evolving 3D field-of-vision system. Compared to Dwarf Fortress's always-on vertical simulation or Minecraft's continuous voxel volume, CDDA occupies a distinct architectural niche: a roguelike with discrete z-layer stacks, lazy generation, and a single 120×120×21-tile "reality bubble" outside which the world freezes.

---

## A. Executive summary

CDDA's world is spatially organized into four nested scales: **overmaps** (180×180 overmap terrain tiles), **overmap terrain tiles** or OMTs (24×24 player-visible tiles each), **submaps** (12×12 tiles, the persistence unit), and individual **map squares** (1×1 tiles). Vertically, **21 discrete z-levels** span from z = -10 (deep rock) to z = +10 (high altitude), stored as `OVERMAP_LAYERS` in the source.

The generation pipeline operates in two phases. First, when the game needs overmap data for a region, it generates the entire 180×180×21 overmap in a fixed C++ sequence — rivers, lakes, forests, cities, roads, specials — parameterized by JSON `region_settings`. Second, when the player's reality bubble reaches an unvisited OMT, local mapgen selects a weighted-random JSON definition matching that terrain type and builds the 24×24-tile area, applying palettes, nested chunks, map extras, and multi-z-level links. Once generated, all data persists permanently to disk.

Compared to **Dwarf Fortress**, CDDA uses a similar discrete z-layer model but generates lazily rather than simulating an entire embark site continuously. DF's z-range (~200 levels of geology) dwarfs CDDA's 21. Compared to **Minecraft**, CDDA uses discrete 2D layers rather than continuous 3D voxels, and its "reality bubble" is far more restrictive than Minecraft's independent chunk ticking. Both comparisons reveal CDDA as a hybrid: more vertically aware than most roguelikes, but fundamentally a 2D engine with layered extension.

---

## B. Spatial scale layers

CDDA organizes space across five distinct scales, each with specific responsibilities.

| Scale | Dimensions | Relationship | Stores | Does NOT store |
|-------|-----------|--------------|--------|----------------|
| **World** | Infinite grid of overmaps | Managed by `overmap_buffer` | Global unique special tracking, cross-overmap connections | Any tile-level data |
| **Overmap (om)** | 180×180 OMTs × 21 z-levels | One chunk of the world | Terrain type IDs, city locations/sizes, monster group zones, radio towers, placed specials | Actual terrain tiles, items, furniture |
| **Overmap terrain (omt)** | 24×24 map squares = 2×2 submaps | One abstract "tile" on the overmap | A single `oter_id` (e.g., "house_north", "road_straight") plus metadata (monster density, extras, flags) | The concrete layout of walls, items, monsters |
| **Submap (sm)** | 12×12 map squares | Persistence and load/save unit | Terrain IDs, furniture IDs, item stacks, vehicles, spawns, traps, radiation, fields | Overmap-level metadata |
| **Map square (ms)** | 1×1 tile | What the player sees | Individual terrain, furniture, items at that position | Nothing — this is the atomic unit |

**Z-levels** are consistent across all scales. The z-coordinate does not change when converting between map square, submap, OMT, or overmap coordinates. The **reality bubble** loads an 11×11 submap grid (~132×132 tiles) across **all 21 z-levels simultaneously**, totaling roughly **2,541 submaps** in active memory.

The C++ codebase enforces scale-correctness through strongly typed coordinates: `point_abs_omt` for absolute OMT positions, `tripoint_bub_ms` for reality-bubble-relative map squares, and conversion functions like `project_to<coords::omt>()`. This type system prevents accidental scale mismatches at compile time.

---

## C. Step-by-step world generation pipeline

The pipeline has two major phases — overmap generation and local mapgen — plus a runtime persistence layer. Each stage is numbered below with its scale, inputs, outputs, and timing.

### Phase 1: overmap generation (eager, on first access)

**Trigger**: The `overmap_buffer` receives a query for an overmap that doesn't exist on disk or in cache. The entire 180×180×21 overmap is generated at once.

**Stage 1 — Populate neighbor connections** [Confirmed]
Reads road, river, and rail entry points from the four adjacent overmaps (if they exist). Records boundary constraints that subsequent steps must honor. Scale: overmap edges. Inputs: neighbor overmaps. Outputs: boundary connection points.

**Stage 2 — Place rivers** [Confirmed]
Generates river paths using weighted random walks with entry/exit points at overmap boundaries. Uses neighbor boundary data for cross-overmap continuity. Rivers carve through the surface z-level.

**Stage 3 — Place lakes** [Confirmed]
Uses global noise functions (coherent across overmap boundaries) with threshold values from `region_settings` to create lake bodies. A minimum size threshold and shore terrain logic apply.

**Stage 4 — Place forests** [Confirmed]
Two noise functions with values 0–1 are evaluated across the entire overmap. Thresholds (`noise_threshold_forest` = 0.2, `noise_threshold_forest_thick` = 0.25) determine forest density. These noise functions are **globally coherent**, ensuring seamless forest edges across overmap boundaries.

**Stage 5 — Place swamps** [Confirmed]
Similar noise-threshold system with proximity-to-water variants. `noise_threshold_swamp_adjacent_water` = 0.3, `noise_threshold_swamp_isolated` = 0.6.

**Stage 6 — Place ravines** [Confirmed]
Ravine generation using parameters from `region_settings_ravine`.

**Stage 7 — Place cities** [Confirmed]
Cities are positioned with configurable spacing and size. Each city starts as a single intersection, with buildings placed within its radius. Regional settings control `city_size_factor` and weighted building lists.

**Stage 8 — Place forest trails** [Confirmed]
Trail networks through forested areas, using the `LINEAR` terrain flag for automatic variant selection.

**Stage 9 — Place roads** [Confirmed]
Cost-based pathfinding connects cities to each other and to overmap boundaries. Fields are cheapest, forests more expensive, swamps expensive, rivers trigger bridge placement. Uses the `overmap_connection` system.

**Stage 10 — Place specials** [Confirmed]
The overmap is divided into **144 sectors** (12×12 grid, each sector 15×15 OMTs). For each sector, the engine attempts to place one overmap special from a weighted batch, checking `city_distance`, `city_sizes`, `locations`, rotation validity, and occurrence limits. Specials include farms, bunkers, labs, anthills, refugee centers. Fixed specials have rigid layouts; mutable specials grow via a join-based jigsaw algorithm.

**Stage 11 — Place forest trailheads** [Confirmed]
Entry points connecting trails to roads.

**Stage 12 — Polish river** [Confirmed]
Cleans up river geometry and shore tiles.

**Stage 13 — Place mongroups** [Confirmed]
Distributes monster group zones across the overmap.

**Stage 14 — Place radios** [Confirmed]
Positions radio transmitter sources.

**Stage 15 — Generate sublevels** [Confirmed]
After surface generation, underground levels are generated iteratively:
```
z = -1
do {
    requires_sub = generate_sub(z)
} while (requires_sub && (--z >= -OVERMAP_DEPTH))
```
This creates underground lab extensions, sewer networks, subway tunnels, cave systems, and ant tunnels downward from their surface entries. Generation stops when no further underground extension is needed.

**Confidence**: Stages 1–15 are **confirmed** from official `OVERMAP.md` documentation, which directly references `overmap.cpp`.

### Phase 2: local mapgen (lazy, on reality bubble entry)

**Stage 16 — Select mapgen function** [Confirmed]
When the reality bubble reaches an ungenerated OMT, the engine looks up all mapgen definitions registered for that `oter_id` and selects one via weighted random choice.

**Stage 17 — Execute mapgen** [Confirmed]
The selected function fills a 24×24 tile area with terrain, furniture, items, monsters, and vehicles. For multi-story buildings, linked z-levels (roofs, basements, upper floors) are generated together with the ground floor.

**Stage 18 — Apply map extras** [Confirmed]
If the overmap terrain has an `extras` field, a random roll determines whether a map extra (helicopter crash, dead military squad, science equipment) overlays onto the generated map.

**Stage 19 — Persist to MAPBUFFER** [Confirmed]
Generated submaps enter the global `MAPBUFFER` and are saved to disk when they leave the reality bubble.

---

## D. Overmap architecture

### Structure and dimensions

A single overmap is a **180×180×21** grid of overmap terrain IDs (`oter_id` values). The constants `OMAPX = 180` and `OMAPY = 180` define the horizontal extent; `OVERMAP_LAYERS = 21` covers z = -10 through z = +10. In map-square terms, one overmap spans **4,320×4,320 tiles** horizontally — roughly **18.7 km²** if each tile represents approximately 1 meter.

Each overmap terrain slot stores an `oter_id` string identifying what that abstract location is: `"house_north"`, `"road_straight_ns"`, `"forest"`, `"lab_stairs"`, `"empty_rock"`. The overmap also stores city data (locations, sizes), monster group zones with their positions and populations, radio transmitter locations, and tracking data for placed specials.

### Default z-level terrain

The `default_oter` in `region_settings` assigns default terrain per z-level before any features are placed: **z = +10 to +1** default to `open_air`, **z = 0** defaults to `field`, **z = -1** defaults to `solid_earth`, **z = -2 to -3** default to `empty_rock`, and **z = -4 to -9** default to `deep_rock`. This means the underground is solid by default; only explicit features (labs, sewers, caves) carve into it.

### Multi-overmap world stitching

The infinite world is managed by `overmap_buffer`, which lazily generates and caches overmaps on demand. Cross-boundary continuity relies on three mechanisms. First, `populate_connections_out_from_neighbors()` reads edge connection points from already-generated adjacent overmaps, ensuring roads and rivers continue seamlessly. Second, **global noise functions** for forests and lakes produce coherent terrain across overmap boundaries without explicit stitching. Third, `overmap_global_state` tracks globally unique specials to prevent duplicates across overmaps.

### The overmap special system

Overmap specials are the primary mechanism for placing non-city points of interest. The overmap's 180×180 surface is divided into a **12×12 grid of sectors** (each 15×15 OMTs, set by `OMSPEC_FREQ = 15`), yielding **144 placement slots**. The placement algorithm iterates through sectors, attempts random positions and random specials from a pre-rolled batch, checks validity constraints (location type, city distance, occurrence limits), and places or retries.

**Fixed specials** define rigid multi-OMT layouts with explicit `[x, y, z]` offsets — a 2×2 campground, a 3×3 military base, or a vertical stack including a surface building at z = 0 plus basement at z = -1. **Mutable specials** use a join-based growth algorithm: starting from a root OMT, the engine grows the structure through phases, placing chunks that connect via directional "joins" (like jigsaw edges). The `microlab_mutable` and `anthill` exemplify this approach, producing organically shaped underground complexes.

Each special can declare `connections` — road, subway, or sewer links that the connection system auto-generates to link the special to the nearest infrastructure.

---

## E. Overmap → local mapgen handoff

### What data crosses the boundary

The overmap provides the local mapgen system with a surprisingly thin interface. The primary datum is the **`oter_id`** — a string like `"house_north"` that determines which pool of mapgen functions to draw from. Beyond this, the overmap passes:

- **Rotation state**: The `_north`/`_east`/`_south`/`_west` suffix on the `oter_id` tells mapgen which direction the structure faces (typically toward the nearest road). The mapgen output is rotated accordingly after generation.
- **Monster density** (`mondensity`): A numeric modifier that scales creature spawns during mapgen.
- **Map extras reference** (`extras`): Points to a group of possible post-generation overlays in `region_settings`.
- **Spawn data** (`spawns`): Monster group, population range, and probability.
- **Flags**: `SIDEWALK` triggers sidewalk generation, `LINEAR` selects connectivity variants.

For **overmap specials**, additional context crosses the boundary:
- **Mapgen parameters** scoped to `"overmap_special"` ensure consistent random choices across all OMTs in the special (e.g., the same roof material everywhere).
- **Predecessor terrain**: The overmap records what terrain existed before a special was placed, enabling `"predecessor_mapgen"` to generate the underlying terrain first as a base layer.
- **Join information** (mutable specials): Available to `place_nested` for conditional content.

### How abstract terrain becomes concrete

The concretization process is a **weighted random selection**. Multiple JSON mapgen definitions can register for the same `om_terrain` ID. When the engine needs to generate `"house"`, it might choose from 30+ different house layouts, each with a `weight` value (default 1000). A house with weight 500 appears one-third as often as one with weight 1000.

This means the overmap says "this tile is a house facing north" but does NOT determine which specific floor plan, furniture arrangement, or item distribution appears. That is decided at generation time by the weighted random pool. This design enables mods to add new building variants simply by creating JSON mapgen files with matching `om_terrain` values — they automatically join the selection pool.

### Determinism

**Overmap generation is seed-determined** — the same world seed produces the same city layouts, road networks, and special placements. **Local mapgen is NOT reproducible** from seed alone. The mapgen system uses the game's runtime RNG for variant selection, furniture placement, item spawning, and nested chunk choices. There is no evidence of position-derived deterministic seeding for local mapgen. Two runs with the same world seed will produce identical overmaps but may generate different interior layouts for the same house.

---

## F. Z-level architecture

This is the most architecturally complex subsystem in CDDA, shaped by the game's evolution from a purely 2D engine.

### Discrete stacked layers

CDDA represents verticality as **21 discrete 2D layers** stacked at integer z-coordinates. Each layer is a complete horizontal plane of tiles. The z-coordinate is an integer in the range **[-10, +10]** inclusive, defined by the constants `OVERMAP_DEPTH = 10`, `OVERMAP_HEIGHT = 10`, and `OVERMAP_LAYERS = 21` (in `game_constants.h`). Every `tripoint` in the engine carries an explicit z-component, and all coordinate types — from map squares to overmap terrains — include z.

At the overmap scale, this means each overmap stores **180 × 180 × 21 = 680,400** terrain ID slots. At the reality bubble scale, the game loads **11 × 11 × 21 = 2,541 submaps** simultaneously.

### What occupies each z-level

| Z-range | Default terrain | Typical contents |
|---------|----------------|------------------|
| +10 to +2 | `open_air` | Upper floors of tall structures, wind turbines, radio towers |
| +1 | `open_air` | Rooftops of single-story buildings, second floors of houses |
| 0 | `field` | Ground level: all surface terrain, roads, buildings, forests |
| -1 | `solid_earth` | Basements, sewers, subway tunnels, first underground lab floors |
| -2 to -3 | `empty_rock` | Deeper sewers, subway infrastructure, upper lab levels |
| -4 to -9 | `deep_rock` | Deep underground labs, cave systems, mine shafts |
| -10 | `deep_rock` | Maximum depth; game enforces a hard floor here |

Buildings extend upward from z = 0, with rooftops typically at z = +1 for single-story structures. Underground labs can extend to z = -10, the hard limit. A bug report documented a lab attempting to generate at z = -11, which triggered a debug error — confirming the **hard boundary** enforcement.

### Structural linking across z-levels during generation

Z-levels are **not independently generated**. They are linked through two mechanisms operating at different scales:

**At overmap scale**: Overmap specials define multi-z-level structures explicitly. A special's `overmaps` array includes entries at different z-offsets — for instance, a bookstore at `[0,0,0]` with a secret lair at `[0,0,-1]`. When the special is placed, all constituent OMTs across all z-levels are assigned their terrain types simultaneously. The `generate_sub()` loop also creates underground extensions (lab stairs, sewer connections) iteratively downward.

**At local mapgen scale**: When the player approaches a ground-floor OMT that has linked z-levels (roof, basement, upper floors), **all linked levels are generated together**. The documentation explicitly states: "If you use an existing overmap_terrain and it has a roof or other z-level linked to its file, the other levels will be generated with the ground floor." The function `get_existing_omt_stack_arguments()` in `overmapbuffer.h` retrieves data for an entire vertical column of OMTs.

However, each z-level still requires its own separate JSON mapgen definition. A single `"rows"` block cannot span multiple z-levels. Instead, buildings define separate `mapgen` entries for each floor (e.g., `"house_01"` at z = 0, `"house_01_roof"` at z = +1, `"house_01_basement"` at z = -1), and the overmap special ties them into a vertical stack.

### Vertical continuity mechanisms

Five mechanisms connect z-levels during gameplay:

**Stairs** (`t_stairs_up`, `t_stairs_down`) are the primary vertical traversal method. The `game::vertical_move()` function handles transitions. Stairs must be placed at corresponding positions across two z-levels to function — a design constraint enforced by mapgen authors, not the engine.

**Open air tiles** (`t_open_air`) represent empty sky outside building footprints at elevated z-levels. They are visually transparent and impassable, creating the illusion of open space above ground.

**The `TFLAG_NO_FLOOR` flag** marks tiles that lack a floor, enabling falling and line-of-sight penetration to the z-level below. The engine checks this flag for gravity, visibility, and weather propagation.

**Ramps** allow diagonal vertical transitions (moving up/down while also moving horizontally). Construction code validates ramp placement by checking adjacent tiles on the target z-level.

**Collapse mechanics** destroy floors dynamically, applying crush damage (distributed as head 25%, torso 45%, legs 10% each, arms 5% each) and forcing entities downward.

### The asymmetric z-offset limitation

A critical technical constraint affects mapgen: **z-level offsets in JSON mapgen work correctly for positive (upward) offsets but NOT for negative (downward) offsets** when it comes to mapgen flags like `ERASE_ALL_BEFORE_PLACING_TERRAIN`. The documentation attributes this to it being "costly and messy to determine a level generation order dynamically." This means a ground-floor mapgen can easily place roof content at z = +1, but placing basement content at z = -1 requires the basement to have its own independent mapgen definition triggered by a separate overmap terrain entry.

### 3D field of vision

Cross-z-level vision has been an evolving experimental feature. The `map::get_inter_level_visibility()` function returns a `std::bitset<OVERMAP_LAYERS>` indicating which z-levels are visible from the player's current level. It builds a "floor cache" by scanning submaps for `TFLAG_NO_FLOOR` and `TFLAG_SUN_ROOF_ABOVE` flags.

The 3D FoV implementation extends Bresenham-based line-of-sight into three dimensions, casting rays through open-air and no-floor tiles. The lighting code in `lightmap.cpp` iterates downward from the player's z-level to `origin.z() - fov_3d_z_range`, checking each tile for opacity. Performance was historically a concern — early implementations were "fast enough to be playable but slow enough to be problematic" — though ongoing optimization has improved this. Known bugs remain: monsters tracking players across z-levels in unintended ways, and vision caching issues requiring explicit look-around commands at different levels.

### Technical debt from 2D origins

The original Cataclysm (by Whales, open-sourced 2010) was fundamentally 2D. CDDA's fork gradually added z-level support as an experimental feature. In versions 0.E and earlier, z-levels could be disabled entirely. Several architectural artifacts reveal this 2D heritage:

- The `map::grid[]` array uses flat 1D indexing with `OVERMAP_LAYERS` divisions, rather than native 3D addressing
- Code comments like "Note: this is only OK because 3D vision isn't a thing yet. 3D vision is a thing! Is this still OK?" reveal assumptions baked into the codebase
- Zone management bugs across z-levels (zones becoming unloaded when the player changes z-level)
- NPC pathfinding failing across z-levels
- Sound propagation not accounting for floor material attenuation between z-levels
- The asymmetric z-offset limitation in mapgen flags

The "roof project" was a major initiative to enforce z-level consistency: **all buildings now require JSON roofs**, and all new buildings must be multi-tile across z-levels. This policy eliminated the visual artifact of seeing into buildings from above and established z-level-aware generation as mandatory rather than optional.

---

## G. Data-driven generation

### The JSON-C++ division

CDDA's worldgen uses a clear two-layer architecture. The **C++ engine** provides procedural algorithms: noise generation, cost-based pathfinding for roads, sector-based special placement, weighted random selection. **JSON data** parameterizes every aspect of these algorithms: what terrain types exist, what buildings can appear in cities, how forests distribute, what mapgen layouts define each building type.

The official documentation notes that "most of the existing C++ buildings have been moved to JSON and currently JSON mapping is the preferred method." Legacy C++ mapgen functions persist for some terrain types but are deprecated.

### JSON type relationships

The data model forms a directed dependency graph:

**`overmap_terrain`** → defines abstract tile types with display properties, flags, and monster data. Serves as the key that links overmap placement to local mapgen.

**`overmap_special`** → groups multiple `overmap_terrain` entries into composite structures. References specific rotated variants (e.g., `"building_north"`) and defines placement constraints.

**`overmap_location`** → named collections of valid `overmap_terrain` IDs used by specials and connections for placement validation (e.g., `"forest"` location accepts `"forest"`, `"forest_thick"`, etc.).

**`overmap_connection`** → defines how linear features (roads, sewers, subways) connect points, with terrain costs and override rules.

**`region_settings`** → controls all overmap-generation parameters: noise thresholds for forests, city spacing and building lists, map extra probabilities, default terrain per z-level, weather generation, and regional terrain/furniture resolution.

**`mapgen`** → concrete 24×24 tile definitions keyed to `overmap_terrain` IDs. Contains `"rows"` (ASCII layout), symbol-to-content mappings, placement commands, palette references, and nested chunk spawns.

**`palette`** → reusable symbol definition sets shared across mapgen entries. Can contain terrain, furniture, item, monster, and vehicle mappings. Support hierarchical inclusion and randomized selection via distributions.

### Regional variation through region_settings

The `region_settings` system enables biome-like variation without changing mapgen definitions. Pseudo-terrain types like `t_region_groundcover` are resolved to actual terrain via weighted lists: in a default region, `t_region_groundcover` might resolve to grass (80%), dead grass (13%), or dirt (7%). A desert mod can replace this with sand, cracked earth, and gravel. Forest noise thresholds can shift based on distance from origin via `forest_threshold_increase`, enabling gradual biome transitions.

The `feature_flag_settings` subsystem allows regions to blacklist or whitelist overmap features by flag, enabling mods like "No Labs" or "Wilderness Only" without modifying the special definitions themselves.

---

## H. Persistence, runtime generation, and simulation boundary

### The reality bubble

CDDA's simulation boundary is the **reality bubble**: a **132×132-tile horizontal area** (11×11 submaps, approximately 5.5×5.5 OMTs) extending across **all 21 z-levels**. The FAQ describes this as a "5×5 square of overmap terrain tiles" — the player's current OMT plus two additional OMTs in every direction. Only entities within this volume are actively simulated: monsters move, fires spread, vehicles operate, weather affects terrain.

Since version 0.F, z-levels are always enabled, meaning the reality bubble is a full 3D volume of approximately **132 × 132 × 21 ≈ 366,000 tiles**. In earlier versions, disabling z-levels reduced this to a single layer.

### What freezes outside the bubble

Outside the reality bubble, almost everything halts. Fires burn indefinitely without progressing or extinguishing. Vehicles don't consume fuel. Monsters don't move or fight. NPC actions pause. This is a known limitation with active feature requests for "multiple reality bubbles" or "mini-bubbles" that would allow limited simulation outside the main bubble.

A few systems transcend the bubble. **Wandering zombie hordes** (when enabled) are tracked at the overmap level and move between overmap tiles abstractly. When re-entering the bubble, several **catch-up systems** calculate elapsed-time effects: food rot, plant growth, animal reproduction, and some NPC activities.

### Generation timing summary

| What | When generated | Persistence |
|------|---------------|-------------|
| Overmap terrain layout (180×180×21) | First access to that overmap region | Saved as `.omap.gz` files |
| Underground extensions (labs, sewers) | During overmap generation, iterative downward | Part of overmap save |
| Local map tiles (24×24 per OMT) | First reality bubble contact | Saved as `.map` files per submap |
| Linked z-levels (roof, basement) | Together with ground floor on first contact | Saved per-submap per z-level |
| Map extras | Applied once after base mapgen | Part of submap save |
| Monster spawns | At mapgen time or via overmap mongroups | Persisted in submap data |

### Save file structure

Save data is stored in `save/<WorldName>/`. Overmap data lives in files named `o.<x>.<y>` (or `overmap_<x>_<y>.omap.gz`). Local map data is organized hierarchically: directories named `<omx>.<omy>.<z>` contain individual submap files named `<smx>.<smy>.<z>.map` in JSON format. Each `.map` file encodes the 12×12 tile contents: terrain arrays, furniture, item lists with positions, vehicles, traps, radiation values, and fields.

---

## I. Comparing Cataclysm's z-level architecture to other games'

### Cataclysm vs. Dwarf Fortress

Dwarf Fortress and CDDA both use **discrete z-level stacks** — integer-indexed horizontal planes stacked vertically — but their implementations differ profoundly in scale, simulation philosophy, and generation approach.

**Z-level scale and geology.** DF's z-range spans roughly **150–200 levels** per embark, determined by geological simulation: sedimentary layers, igneous intrusions, aquifers, three procedurally placed cavern layers, a magma sea, and an adamantine spire at the bottom. CDDA's **21 levels** are comparatively sparse, with a default of solid earth/rock below z = 0 and open air above. DF's geology is a first-class generation system; CDDA has no geological model — underground features are placed as discrete specials (labs, sewers) carved into default rock.

**World-scale abstraction.** DF generates an entire world with continents, biomes, civilizations, and millennia of history before play begins. The player selects an embark site, and the local area (~192×192 tiles, 100+ z-levels) is realized from world-level parameters. CDDA's overmap is the closest analogue to DF's world map, but it contains no history, no civilizations, and no geological model. CDDA's overmap is a terrain-placement layer; DF's world is a historical simulation layer. Where DF's embark is a one-time realization of a pre-simulated world region, CDDA's local mapgen is a lazy concretization of an abstract terrain label.

**Simulation scope.** DF simulates **every z-level within the embark simultaneously and continuously**. Water flows, magma moves, creatures path-find in 3D, temperature propagates, and cave-ins cascade across z-levels in real time. CDDA simulates only the reality bubble — a ~132×132×21 volume — and everything outside freezes. DF's embark is roughly 192×192×200 tiles, all active; CDDA's active volume is 132×132×21. DF's approach is computationally expensive (the legendary "FPS death") but physically coherent. CDDA sacrifices physical coherence for an infinite world.

**Underground continuity.** DF guarantees geological continuity: rock types transition logically between layers, water tables connect to surface rivers, and three cavern layers span the entire embark at consistent depths. CDDA's underground is structurally discontinuous — a lab at z = -4 has no geological relationship to a sewer at z = -1 three tiles away. Underground features are independently placed specials, not emergent from a unified geological model.

**Generation timing.** DF generates the embark site upfront and simulates continuously. CDDA generates lazily at two timescales (overmap on first access, local on reality bubble contact) and simulates only the active bubble. This gives CDDA an infinite world but means underground features are never geologically coherent.

### Cataclysm vs. Minecraft

Minecraft and CDDA share lazy chunk-based generation but differ fundamentally in spatial representation and underground modeling.

**Spatial representation.** Minecraft uses a **continuous 3D voxel volume** with sub-chunk sectioning (16×16×16 sections within 16×16×384 chunks as of 1.18+, y = -64 to +320). Every block is individually addressable in a uniform 3D grid. CDDA uses **21 discrete 2D planes** — each z-level is an independent 2D layer with no continuous vertical connectivity. In Minecraft, you can place a block at any y-coordinate; in CDDA, you can only interact with 21 specific altitudes. Minecraft's representation is fundamentally 3D; CDDA's is 2D with a thin vertical stack.

**Chunk generation and lazy loading.** Both games generate terrain lazily as the player approaches. Minecraft generates 16×16 chunks; CDDA generates 24×24 OMTs. Both persist generated chunks to disk. However, Minecraft generates the **full vertical extent** of a chunk simultaneously (all blocks from y = -64 to y = +320), while CDDA generates z-levels in a linked but conceptually separable manner. Minecraft's cave carving, ore distribution, and structure placement operate on the full 3D volume during chunk generation.

**Underground generation.** Minecraft's underground emerges from **noise-based 3D carving**: cave systems are carved through solid stone using layered noise functions (cheese caves, spaghetti caves, noodle caves in 1.18+), producing continuous, organically shaped 3D volumes. CDDA's underground is **template-placed**: discrete labs, sewers, and caves are inserted as overmap specials with JSON-defined layouts. Minecraft's caves are emergent geometry; CDDA's underground features are authored structures.

**Simulation boundary.** Minecraft's simulation extends beyond the player's immediate vicinity through independent chunk ticking: loaded chunks simulate redstone, crop growth, mob spawning, and water flow independently. "Lazy chunks" (loaded but not ticked) still process some events. CDDA's reality bubble is absolute — nothing simulates outside it except abstract horde movement.

**Performance implications.** Minecraft's continuous 3D volume requires significantly more memory per horizontal area (384 blocks tall vs. CDDA's 21 levels) but benefits from highly optimized chunk meshing and rendering. CDDA's discrete layers are memory-efficient per level but pay overhead in cross-level interaction logic.

### Analytical distinctions between vertical architectures

These three games exemplify three fundamentally different approaches to representing verticality in procedural worlds:

**Many dungeon floors** (traditional roguelikes like Nethack, Angband): Separate, disconnected floor maps with no spatial relationship between levels. Each floor is generated independently. Going downstairs teleports you to a new map. CDDA does NOT use this model — its z-levels are spatially linked.

**Discrete z-level stacks** (CDDA, Dwarf Fortress, Caves of Qud): Connected vertical layers where each z-level occupies a real position in a shared coordinate space. Entities on z = 3 exist directly above entities on z = 2. Features like stairs, open air, and falling connect levels. DF and CDDA both use this model, but DF simulates all levels continuously while CDDA simulates only the reality bubble.

**Full 3D voxel space** (Minecraft, Terraria's continuous vertical axis): A uniform volumetric representation where every point in 3D space is individually addressable. No concept of "levels" — vertical space is continuous. Cave systems, overhangs, and floating islands emerge naturally from the representation. This is the most expressive but most memory-intensive approach.

---

## J. Does Cataclysm resemble Dwarf Fortress?

The answer is **structurally yes, philosophically no**. Both games use discrete z-level stacks with integer-indexed horizontal planes, stairs for vertical traversal, and support for buildings that span multiple z-levels. Both treat the z-axis as a first-class coordinate. At the data structure level, they are recognizably similar — a `tripoint(x, y, z)` in CDDA and a tile address `(x, y, z)` in DF serve the same role.

But the resemblance dissolves at the simulation level. DF's z-levels participate in a unified physical simulation: fluid dynamics, temperature, structural integrity, and pathfinding all operate in 3D. A river on the surface connects to an aquifer underground which connects to a cavern lake — all physically simulated. CDDA's z-levels are primarily a **spatial addressing system** grafted onto a 2D simulation engine. The reality bubble simulates only the local volume; underground features are disconnected authored templates; and physics (water flow, structural collapse) operates with significant simplifications.

DF's world generation includes geological modeling that determines what exists at each z-level through physical simulation of rock layers, erosion, and mineral deposition. CDDA assigns default rock types to underground z-levels and then carves authored structures into them. There is no geological model, no mineral distribution logic, and no physical relationship between adjacent underground tiles unless explicitly authored.

The comparison is best understood as: **CDDA adopted DF's spatial model (the z-level stack) but not its simulation philosophy (physics-driven 3D coherence)**. CDDA's z-levels serve primarily to enable multi-story buildings, basements, and underground exploration — quality-of-life features for a survival roguelike. DF's z-levels are foundational to its identity as a physics simulator.

---

## K. Technique-family identification

CDDA's world generation employs a identifiable set of procedural generation techniques, each applied at specific scales.

**Coherent noise fields** [Confirmed]: Used for forest, lake, and swamp placement across the overmap. Global noise functions ensure cross-overmap continuity. Controlled by threshold parameters in `region_settings`.

**Cost-based pathfinding** [Confirmed]: Used for road generation. The `overmap_connection` system applies weighted A* or similar pathfinding, with terrain-type-dependent costs (field < forest < swamp < river, where river forces bridge placement).

**Sector-based random placement with constraint satisfaction** [Confirmed]: The overmap special system divides the overmap into 144 sectors and attempts to place one special per sector, checking distance, size, location, and occurrence constraints. This is a form of Poisson-like distribution with hard constraints.

**Weighted random template selection** [Confirmed]: Local mapgen selects from multiple JSON-defined building layouts using weight values. This is the simplest form of grammar-based generation — a single-level weighted choice.

**ASCII template instantiation with palette mapping** [Confirmed]: The core local mapgen format uses 24-character strings as row templates, with single characters mapped to terrain, furniture, items, and monsters via palette definitions. This is a tile-stamping approach.

**Nested composition / hierarchical template overlay** [Confirmed]: `place_nested` inserts sub-templates into parent mapgen, with conditional placement based on overmap neighbors and joins. This creates combinatorial variety from a library of reusable chunks.

**Join-based jigsaw growth** [Confirmed]: Mutable overmap specials use directional "joins" to grow structures organically from a root tile, with placement phases and maximum extent limits. This technique is closely related to Wave Function Collapse without the full constraint propagation — it's more of a greedy stochastic growth algorithm.

**Predecessor layering** [Confirmed]: The `predecessor_mapgen` and `fallback_predecessor_mapgen` systems generate a base terrain (e.g., forest) before overlaying a structure, ensuring buildings don't float in void.

---

## L. Algorithmic reconstruction

The following pseudocode reconstructs CDDA's generation pipeline. Each block is marked with confidence level.

### Overmap generation [Confirmed]
```
function generate_overmap(seed, neighbors[4]):
    rng = seed_rng(seed, overmap_position)
    grid = new oter_id[180][180][21]
    
    // Initialize defaults from region_settings
    for z in -10..+10:
        for x in 0..179:
            for y in 0..179:
                grid[x][y][z] = default_oter[z]  // open_air, field, solid_earth, etc.
    
    // Stage 1: Read boundary constraints from neighbors
    boundary_connections = read_edge_connections(neighbors)
    
    // Stage 2-5: Natural features via noise
    noise1 = global_perlin_noise(seed, overmap_position)
    noise2 = global_perlin_noise(seed + offset, overmap_position)
    for each tile in grid[z=0]:
        if noise1[tile] > LAKE_THRESHOLD: mark_lake(tile)
        if noise1[tile] > FOREST_THICK_THRESHOLD: grid[tile] = forest_thick
        elif noise1[tile] > FOREST_THRESHOLD: grid[tile] = forest
        if near_water AND noise2[tile] > SWAMP_WATER_THRESHOLD: grid[tile] = swamp
    
    place_rivers(boundary_connections.rivers)  // Random walk with boundary constraints
    
    // Stage 7: Cities
    cities = place_cities(region_settings.city_spacing, region_settings.city_size)
    for each city:
        fill_city_buildings(city, region_settings.city.houses, region_settings.city.shops)
    
    // Stage 9: Roads via pathfinding
    for each pair of (cities + boundary_connections.roads):
        path = weighted_astar(start, end, cost_function)  // field=1, forest=3, swamp=5, river=bridge
        for tile in path: grid[tile] = road_variant(neighbors)
    
    // Stage 10: Specials via sector placement
    sectors = divide_grid(180, OMSPEC_FREQ=15)  // 12×12 = 144 sectors
    batch = build_special_batch(enabled_specials, occurrence_rolls)
    for each sector in sectors:
        for attempt in 1..MAX_ATTEMPTS:
            pos = random_point_in_sector(sector)
            special = random_choice(batch)
            rotation = random_rotation()
            if valid_placement(special, pos, rotation, grid):
                stamp_special(special, pos, rotation, grid)
                update_batch(batch, special)
                break
    
    // Stage 15: Underground extension
    z = -1
    repeat:
        needs_more = generate_sub(z, grid)  // Extend labs, sewers downward
        z -= 1
    until not needs_more or z < -10
    
    return grid
```

### Local mapgen [Confirmed]
```
function generate_local_map(oter_id, position):
    candidates = mapgen_registry[oter_id]  // All JSON definitions for this terrain
    weights = [c.weight for c in candidates]
    selected = weighted_random_choice(candidates, weights)
    
    tiles = new tile[24][24]
    
    // Fill base terrain
    if selected.fill_ter: fill_all(tiles, selected.fill_ter)
    
    // Apply palettes
    symbol_map = {}
    for palette in selected.palettes:
        symbol_map.merge(palette.definitions)  // Last palette wins for exclusive types
    symbol_map.merge(selected.local_definitions)  // Local overrides palettes
    
    // Stamp rows
    for y in 0..23:
        for x in 0..23:
            char = selected.rows[y][x]
            tiles[x][y].terrain = symbol_map.terrain[char]
            tiles[x][y].furniture = symbol_map.furniture[char]
            tiles[x][y].items = roll_items(symbol_map.items[char])
    
    // Execute placement commands
    for cmd in selected.place_commands:
        execute_placement(cmd, tiles)  // place_monsters, place_items, place_vehicles, etc.
    
    // Place nested chunks
    for nest in selected.place_nested:
        if check_conditions(nest.neighbors, nest.joins, overmap_context):
            chunk = weighted_random_choice(nest.chunks)
            overlay_chunk(chunk, nest.x, nest.y, nest.z_offset, tiles)
    
    // Apply map extras (if triggered)
    if oter.extras and random_roll(extras.chance):
        extra = weighted_random_choice(oter.extras.group)
        apply_extra(extra, tiles)
    
    // Generate linked z-levels [Confirmed]
    for linked_z in get_linked_z_levels(oter_id):
        generate_local_map(linked_z.oter_id, position.with_z(linked_z.z))
    
    persist_to_mapbuffer(tiles, position)
```

### Reality bubble shift [Inferred]
```
function on_player_move(new_position):
    if new_position crosses submap boundary:
        map.shift(direction)
        for each newly_exposed_submap in leading_edge:
            if MAPBUFFER.contains(newly_exposed_submap.position):
                map.grid[index] = MAPBUFFER.load(position)
            else:
                oter = overmap_buffer.get_terrain(position)
                generate_local_map(oter, position)  // Generates all linked z-levels
        for each dropped_submap in trailing_edge:
            MAPBUFFER.save(dropped_submap)
```

---

## M. Worked example

Consider a player exploring eastward toward a two-story house at overmap coordinates (92, 45, 0) on overmap (0, 0).

**Step 1 — Overmap exists.** Overmap (0, 0) was generated when the world was created. During city generation (Stage 7), tile (92, 45, 0) was assigned `oter_id = "house_two_story_north"`. The overmap special or city building system also assigned (92, 45, +1) as `"house_two_story_upper_north"` and (92, 45, -1) as `"house_two_story_basement_north"`.

**Step 2 — Reality bubble approaches.** The player is at (90, 45, 0). The reality bubble extends ~2.5 OMTs in each direction, so tiles up to (92, 45) are within range.

**Step 3 — Submap check.** The engine queries `MAPBUFFER` for the submaps at absolute position (92, 45, 0). They don't exist — this OMT has never been visited.

**Step 4 — Local mapgen triggered.** The engine looks up `"house_two_story_north"` in the mapgen registry and finds 8 candidate definitions with weights ranging from 250 to 1000. It selects one with weight 1000 (probability ~1000/5250 ≈ 19%).

**Step 5 — Ground floor generation (z = 0).** The selected mapgen loads `standard_domestic_palette`, resolving symbols like `|` → `t_wall_w`, `+` → `t_door_c`, `h` → `t_floor` + `f_chair`. The 24-row ASCII layout is stamped, placement commands scatter items (`place_items: "livingroom"` with 70% chance per item slot), and `place_nested` inserts a random kitchen variant from 4 options.

**Step 6 — Linked z-levels generated.** Because `"house_two_story_north"` is linked to upper and basement levels, the engine also generates:
- **z = +1** (`"house_two_story_upper_north"`): Second-floor bedrooms, stairs connecting to z = 0, `t_open_air` outside the building footprint.
- **z = +2** (`"house_two_story_roof_north"`): `t_flat_roof` over the footprint, `t_open_air` everywhere else, `t_gutter_downspout` at one corner.
- **z = -1** (`"house_two_story_basement_north"`): Concrete walls, `t_stairs_up` matching the stairs at z = 0, storage items, possibly a water heater and `f_utility_shelf`.

**Step 7 — Map extras roll.** The `"house_two_story_north"` terrain has extras pointing to `"build"` map extras group. A roll of 3% fails — no extra is applied.

**Step 8 — Persistence.** The 4 submaps per z-level × 4 z-levels = 16 submaps are stored in `MAPBUFFER`. When the player moves away, they'll be saved to files like `maps/0.0.0/184.90.0.map` (submap coordinates for z = 0) and `maps/0.0.1/184.90.1.map` (z = +1).

**Step 9 — Player enters.** The player moves to (92, 45, 0) and sees a furnished two-story house. Looking up through a stairwell with 3D FoV enabled, they can see the upstairs hallway. The z = +1 level is already loaded in the reality bubble, so interaction is immediate.

---

## N. Open questions

**Is local mapgen seeded per-position?** No documentation confirms whether local mapgen uses a position-derived deterministic seed or the global runtime RNG. If the latter, identical world seeds may produce different building interiors. This matters for reproducibility and bug reporting. **Confidence: Speculative** that it uses runtime RNG, based on the absence of any documented position-seeding mechanism.

**How does `generate_sub()` decide to extend underground?** The iterative downward generation loop `do { requires_sub = generate_sub(z) } while(...)` stops when `requires_sub` returns false, but the exact heuristic — does it check for downward stairs? For specific overmap terrains? — is unclear from documentation alone. **Confidence: Inferred** that it checks for terrain types that imply further depth (lab stairs, mine shafts).

**What is the exact active chunk loading order for multi-z-level structures?** When a ground-floor OMT triggers generation of its roof and basement, are all linked z-levels generated in the same frame? Is there a defined generation order (ground first, then up, then down)? **Confidence: Inferred** that ground is first, positive z-offsets next (since mapgen flags work for positive offsets), negative last.

**How does the mutable special growth algorithm handle z-level joins?** Mutable specials like `microlab_mutable` grow through join connections, but documentation focuses on horizontal joins. Can joins connect vertically? If so, how does the growth algorithm handle 3D expansion? **Confidence: Speculative** — the join system likely supports z-offsets in chunk definitions, but the growth algorithm may be primarily horizontal with vertical extent defined per-chunk.

**What happens when an underground lab tries to generate below z = -10?** Bug report #48679 confirmed this causes a debug error. Has this been fixed by clamping, or does it still produce errors in edge cases? **Confidence: Unknown** — the bug was reported; fix status unclear.

**How will multiple reality bubbles change the architecture?** Feature requests for mini-bubbles (for NPC activities, fire progression) would require significant changes to the simulation boundary model. Would this require per-submap simulation state? Per-OMT tick queues? **Confidence: Speculative** — no implementation exists yet.

---

## O. Final synthesis

CDDA's world generation architecture is a pragmatic, layered system that achieves remarkable scope — an infinite, explorable, vertically structured post-apocalyptic world — through a clean separation between abstract placement and concrete realization. The overmap serves as a planning layer, assigning terrain types through a deterministic C++ pipeline parameterized by JSON. Local mapgen serves as a realization layer, selecting and instantiating specific layouts from a weighted template pool. Z-levels serve as a vertical extension layer, grafted onto a 2D foundation with enough structural integrity to support multi-story buildings, underground labs, and cross-level visibility.

The system's greatest strength is its **data-driven extensibility**. A modder can add a new building to the game by writing JSON alone — no C++ required. Palettes, nested mapgen, and the weighted selection pool create combinatorial variety from authored components. The overmap special system supports everything from rigid 2×2 surface structures to organically growing underground complexes through the join-based mutable special mechanism.

The system's most significant limitation is the **reality bubble**. By simulating only a 132×132×21 volume around the player, CDDA sacrifices physical coherence for infinite extent. The world outside the bubble is frozen, creating well-known gameplay artifacts (eternal fires, idle NPCs, non-progressing events). This design choice makes CDDA's infinite world possible but fundamentally different from DF's always-on simulation.

The z-level system, while functional and increasingly robust, carries the weight of its 2D origins. Each z-level requires a separate mapgen definition. Negative z-offsets don't support mapgen flags. 3D field of vision remains an area of active development. Underground features are authored templates rather than geologically emergent structures. These are not bugs but architectural constraints — the natural consequence of extending a 2D tile engine into the third dimension incrementally rather than designing for 3D from the start.

What CDDA has built is nonetheless distinctive in the roguelike space: a **data-driven, lazily generated, vertically aware, infinitely extensible world** that supports both authored detail (hand-designed building layouts) and procedural variety (weighted random selection, nested composition, noise-based terrain distribution). It occupies a unique point in the design space between DF's simulation depth and Minecraft's volumetric freedom — trading both for something neither offers: a modder-friendly, JSON-defined, survival-focused world engine that can generate an entire city block complete with furnished multi-story buildings, connected sewers, and rooftop access without a single line of compiled code.