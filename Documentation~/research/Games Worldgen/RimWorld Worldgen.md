# RimWorld's two-scale world generation pipeline

**RimWorld generates geography at two entirely separate scales — a spherical world map of hexagonal tiles and a flat local map of terrain cells — connected by a narrow data bridge that converts abstract tile metadata into concrete playable terrain.** The world map stores only scalar properties per tile (biome, hilliness, temperature, rainfall, river/road connections), while the local map synthesizes all spatial detail from scratch using seeded Perlin noise constrained by those properties. This architecture lets a single planet hold hundreds of thousands of tiles while only ever materializing the handful the player actually visits. Understanding this boundary — what crosses it, what doesn't, and how a six-field tile record becomes a 62,500-cell battlefield — is the central question of this report.

The reconstruction below draws on decompiled source code (josh-m/RW-Decompile, Chillu1/RimWorldDecompiled on GitHub), the RimWorld Wiki, modding framework documentation (BiomesKit, Geological Landforms, Map Designer), the Map Preview mod, Ludeon Studios blog posts, and community analysis. It targets RimWorld 1.4/1.5 (Anomaly era) as the primary reference, with notes on 1.6/Odyssey changes where they alter the architecture. Each claim is marked as **confirmed**, **strong inference**, or **speculative** where appropriate.

---

## A. Executive summary

RimWorld's procedural geography is a **two-tier discrete system**: a persistent spherical world map and ephemeral local maps instantiated on demand. The world map is a geodesic sphere of ~300,000 hexagonal (and 12 pentagonal) tiles, each storing a compact record of biome, hilliness, elevation, temperature, rainfall, swampiness, river links, road links, and stone types. No spatial detail exists at this level — a tile is a point with scalar attributes, not a miniature terrain map.

When a player settles on a tile, the game creates a flat rectangular grid (default **250×250 cells**, each ~1 meter) and runs a sequence of ~20 ordered generation steps (GenSteps). These steps read the tile's attributes, combine them with a deterministic seed derived from `Hash(worldSeed, tileID)`, and synthesize terrain through layered Perlin noise fields. The tile's hilliness controls mountain coverage, its biome controls terrain-type mappings and vegetation, its river/road links determine entry angles and widths, and its coast adjacency triggers beach generation. Everything else — cave layouts, mineral veins, ruin placement, individual cell terrain — is newly synthesized.

| Layer | Scope | Persistence | Data per unit | Spatial detail |
|-------|-------|-------------|---------------|----------------|
| **World map** | ~300k tiles on a sphere | Always in memory + save | ~8 scalar fields + link lists | None (point data only) |
| **Local map** | 250×250 cells (configurable) | Only while active | Full terrain, things, roofs per cell | Complete |

This separation matters for three reasons. **Performance**: generating and simulating only visited tiles keeps memory and CPU bounded. **Replayability**: the same world seed produces the same globe, but local maps are only realized when visited, so players discover terrain as they expand. **Narrative scale**: the world map supports strategic-level decisions (where to settle, caravan routing, faction geography) while local maps support tactical-level play (base layout, farming, defense).

---

## B. Step-by-step pipeline

### World generation stages

| # | Stage | Layer | Inputs | Outputs | Order | Confidence |
|---|-------|-------|--------|---------|-------|------------|
| W1 | **WorldGenStep_Components** | World | World seed, planet coverage | Initialized world components | First | Confirmed (code) |
| W2 | **WorldGenStep_Terrain** | World | Seed, coverage, overall temp/rainfall | Elevation, temperature, rainfall, hilliness, swampiness, biome for every tile | Early | Confirmed (code) |
| W3 | **WorldGenStep_Rivers** | World | Tile elevation/rainfall, adjacency graph | River links between tiles (RiverDef + neighbor ID) | After terrain | Confirmed (wiki + code refs) |
| W4 | **WorldGenStep_AncientRoads** | World | Tile graph, seed | Ancient asphalt road links via shallow civilization simulation | After rivers | Confirmed (wiki) |
| W5 | **WorldGenStep_Roads** | World | Faction bases, tile graph | Modern road links (path/dirt/stone) connecting settlements | After factions exist | Confirmed (wiki) |
| W6 | **WorldGenStep_Factions** | World | Valid land tiles, biome constraints | Faction settlement placements on tiles | After terrain | Confirmed (code) |
| W7 | **WorldGenStep_Features** | World | All tile data, geographic clusters | Named features (seas, mountain ranges, deserts) | Late | Confirmed (code ref) |
| W8 | **WorldGenStep_AncientSites** | World | Valid tiles | Ancient ruin world objects | Last | Strong inference |

Each step receives a sub-seed derived from `Gen.HashCombineInt(worldSeed, stepSeedPart)`. The steps are sorted by an explicit `order` field in their XML defs, then by `index` for ties.

### Local map generation stages

| # | Stage | Layer | Inputs | Outputs | Order | Confidence |
|---|-------|-------|--------|---------|-------|------------|
| L0 | **Map instantiation** | Transition | World tile, map size, MapGeneratorDef | Empty Map object, deterministic seed | Trigger | Confirmed (code) |
| L1 | **GenStep_ElevationFertility** | Local | Tile hilliness, seed | Elevation float grid, Fertility float grid | ~100 | Confirmed (code) |
| L2 | **GenStep_RocksFromGrid** | Local | Elevation grid, tile stone types | Rock walls, roof grid (thin/thick), stone-type regions | ~200 | Confirmed (code) |
| L3 | **GenStep_Caves** | Local | Rock wall positions, cave eligibility | Carved tunnels in rock, Caves float grid | ~210 | Confirmed (code) |
| L4 | **GenStep_CavesTerrain** | Local | Caves grid | Gravel/rough-stone floors inside tunnels | ~220 | Confirmed (code) |
| L5 | **GenStep_Terrain** | Local | Elevation, fertility, biome, river data, coast, caves | Concrete TerrainDef per cell | ~300 | Confirmed (code) |
| L6 | **GenStep_RockChunks** | Local | Rock positions | Scattered stone chunks on surface | ~320 | Confirmed |
| L7 | **GenStep_Roads** | Local | World road links, tile graph headings | Road surface across map | ~330 | Confirmed (code) |
| L8 | **GenStep_ScatterLumpsMineable** | Local | Rock wall cells | Steel, gold, plasteel, etc. ore lumps | ~340 | Confirmed |
| L9 | **GenStep_ScatterSteamGeysers** | Local | Valid open cells | 1–3 steam geysers | ~350 | Confirmed |
| L10 | **GenStep_ScatterRuinsSimple** | Local | Valid cells | Scattered ruined walls | ~360 | Confirmed |
| L11 | **GenStep_ScatterShrines** | Local | Mountain cells with thick roof | Ancient danger rooms with cryptosleep caskets | ~370 | Confirmed |
| L12 | **GenStep_Plants** | Local | Biome plant list, terrain fertility | Trees, bushes, grass, cave plants | ~400 | Confirmed |
| L13 | **GenStep_Animals** | Local | Biome animal list | Starting wildlife populations | ~410 | Confirmed |
| L14 | **GenStep_FindPlayerStartSpot** | Local | Walkable cells | Player landing zone | ~420 | Confirmed |
| L15 | **GenStep_Fog** | Local | All map data | Fog-of-war over unseen areas | ~430 | Strong inference |

DLC content (Royalty, Ideology, Biotech, Anomaly) injects additional GenSteps at specific order values — for example, anima tree placement (Royalty) and mechanoid structures (Biotech). The 1.6/Odyssey update adds a **Map Features** layer of GenSteps implementing 85+ terrain features (valleys, chasms, plateaus, deltas) on top of this pipeline.

---

## C. Pipeline dependency map

The dependency structure runs strictly top-to-bottom within each layer but crosses scales at exactly one point: **L0 reads the world tile record**.

```
WORLD GENERATION (runs once at game start)
═══════════════════════════════════════════
W1 Components
 └→ W2 Terrain (elevation → temperature → rainfall → hilliness → swampiness → biome)
     ├→ W3 Rivers (uses elevation gradient + rainfall)
     ├→ W6 Factions (needs valid land tiles)
     │    └→ W5 Roads (connects faction bases)
     ├→ W4 AncientRoads (independent topology)
     ├→ W7 Features (reads all tile data)
     └→ W8 AncientSites (needs valid tiles)

                    ║ SCALE BOUNDARY ║
                    ║  Tile record   ║
                    ║  passed down   ║

LOCAL MAP GENERATION (runs per tile, on demand)
═══════════════════════════════════════════
L0 Instantiation ← reads Tile{biome, hilliness, rivers, roads, coast, stones}
 └→ L1 ElevationFertility ← hilliness controls noise amplitude
     ├→ L2 RocksFromGrid ← elevation grid → rock walls; stones from tile
     │    └→ L3 Caves ← carves through placed rock
     │         └→ L4 CavesTerrain ← assigns floors in caves
     └→ L5 Terrain ← reads elevation, fertility, biome, river, coast, caves
          ├→ L6 RockChunks
          ├→ L7 Roads ← reads world road links for direction
          ├→ L8 MineableLumps ← needs rock walls to embed ores
          ├→ L9 SteamGeysers
          ├→ L10 Ruins, L11 Shrines ← need terrain/rock context
          ├→ L12 Plants ← needs terrain fertility
          ├→ L13 Animals ← needs biome species list
          └→ L14 PlayerStart, L15 Fog
```

The critical cross-scale dependencies are:

- **Hilliness → L1**: The single most important parameter crossing the boundary. It multiplies the Perlin elevation noise, controlling what fraction of cells become mountain.
- **Biome → L5, L12, L13**: Determines terrain-type mappings, plant species, animal species.
- **River links → L5**: Direction and size of river crossing the map.
- **Road links → L7**: Direction and type of road crossing the map.
- **Coast adjacency → L5**: Which map edge(s) become ocean/beach.
- **Stone types → L2**: Which 2–3 rock types fill the mountain cells.

---

## D. World map layer

### Globe structure and the tile grid

RimWorld's planet is a **geodesic polyhedron** — an icosahedron recursively subdivided into a mesh of hexagons and pentagons. The `WorldGrid` constructor calls `PlanetShapeGenerator.Generate(10, ...)` with a hardcoded `SubdivisionsCount = 10` and `PlanetRadius = 100f` (in Unity world units). This produces a sphere composed of **mostly hexagonal tiles with exactly 12 pentagonal tiles**, the latter located at the vertices of the original icosahedron. [Confirmed from decompiled WorldGrid.cs]

Total tile counts scale with the tessellation depth. Community measurements from the My Little Planet mod indicate approximately **300,000 total tiles** at full resolution. At the default **30% planet coverage**, only ~30% of these tiles become land; the rest are forced to sub-zero elevation (ocean) by a `ConvertToIsland` noise modifier keyed to the view angle. Each coverage step roughly triples the settlable tile count: ~6,700 settlable at the smallest setting, ~20,000 at one step up, ~60,000 at another, and ~182,000 at full coverage.

Tiles are stored as a **flat `List<Tile>`** indexed by integer tile ID. Adjacency uses a compressed sparse representation: two parallel arrays `tileIDToNeighbors_offsets` and `tileIDToNeighbors_values` encode each tile's neighbors without per-tile list allocations. To retrieve tile `t`'s neighbors, the engine iterates from `offsets[t]` to `offsets[t+1]` in the values array. Vertex positions use the same offset pattern. [Confirmed from WorldGrid.cs]

### Elevation, temperature, and rainfall

All three base fields are generated inside `WorldGenStep_Terrain`, which also computes hilliness, swampiness, and biome. The generation uses the **LibNoise** library (a C# port) for noise primitives.

**Elevation** is built from two blended noise sources: a 6-octave **Perlin noise** field (frequency `0.035 × freqMult`, lacunarity 2.0, persistence 0.4) and a 6-octave **RidgedMultifractal** field (frequency `0.012 × freqMult`), blended by a third 5-octave Perlin control field. This creates broad continental shapes from the Perlin component and sharp mountain ridges from the fractal component. A `ScaleBias` maps the result to the elevation range. Tiles with `elevation ≤ 0` are flagged `WaterCovered = true` and become ocean. `freqMult` scales all frequencies based on planet coverage to maintain feature density regardless of globe size. [Confirmed from WorldGenStep_Terrain.cs]

**Temperature** combines a latitude curve (hot at the equator, freezing at the poles) with a Perlin noise offset at frequency `0.018 × freqMult`, amplitude ±4°C. Higher elevation further depresses temperature. The result is stored per tile as average annual temperature; seasonal variation is calculated dynamically from latitude during gameplay. Serialized as `ushort` with 0.1°C precision. [Confirmed from code + wiki]

**Rainfall** uses a separate 6-octave Perlin (frequency `0.015 × freqMult`) normalized to [0, 1], then multiplied by a `SimpleCurve` mapping absolute latitude to tropical convergence: equatorial regions receive more rain, polar regions less. Stored as integer mm/year. [Confirmed from code]

### Biome assignment through competitive scoring

Biome selection uses a **highest-score-wins** system. Every `BiomeDef` with `generatesNaturally = true` has a `BiomeWorker` subclass whose `GetScore(Tile, tileID)` method returns a float. The biome with the highest positive score wins the tile. This design is both elegant and mod-friendly: mods add new biomes simply by defining a `BiomeWorker` that outscores vanilla entries under the desired climate conditions.

For example, the **TemperateForest** worker returns `-100` for water tiles, `0` if temperature < −10°C or rainfall < 600 mm, and otherwise `15 + (temp − 7) + (rainfall − 600) / 180`. Warmer, wetter tiles score higher, so tropical biomes can outcompete temperate ones at higher values. Desert biomes score high at low rainfall; IceSheet scores high at extreme cold. [Confirmed from decompiled BiomeWorker classes]

The **12 base-game biomes** are: Temperate Forest, Temperate Swamp, Tropical Rainforest, Tropical Swamp, Arid Shrubland, Desert, Extreme Desert, Boreal Forest, Cold Bog (added 1.5), Tundra, Ice Sheet, and Sea Ice. Ocean and Lake are non-playable biomes. The Odyssey DLC adds 5 additional surface biomes (Glowforest, Scarlands, Lava Fields, Meadow, Permafrost).

### Rivers, roads, and the tile graph

**Rivers** are generated in `WorldGenStep_Rivers` after terrain. They are stored as `List<Tile.RiverLink>` on each tile, where each `RiverLink` holds a `RiverDef` (Creek, River, Large River, or Huge River) and a neighbor tile ID. Rivers flow **toward oceans**, are more likely in high-rainfall areas, and can merge — a creek joining a river becomes a large river. The exact algorithm is not fully decompiled in public sources but is strongly inferred to be a downhill flow-accumulation process seeded at high-elevation, high-rainfall tiles. [Strong inference from wiki + code references]

**Roads** are generated in two passes. `WorldGenStep_AncientRoads` runs a shallow "ancient civilization simulation" that places Ancient Asphalt Roads and Highways. `WorldGenStep_Roads` then generates modern roads (Path, Dirt Road, Stone Road) connecting faction settlements to the road network. Road data mirrors rivers: `List<Tile.RoadLink>` per tile. World pathfinding uses **A\* on the tile adjacency graph** with road-discounted movement costs and a spherical-distance heuristic. [Confirmed from WorldPathFinder.cs + wiki]

**Coastlines** are detected dynamically: a land tile adjacent to a water tile is coastal. `World.CoastDirectionAt(tileID)` returns a `Rot4` direction indicating which side faces ocean, used during local map generation to place beaches.

### Hilliness classification from noise and elevation

Hilliness is an enum — `Flat`, `SmallHills`, `LargeHills`, `Mountainous`, `Impassable` — assigned by combining two additional noise fields (`noiseHillsPatchesMicro` and `noiseHillsPatchesMacro`) with elevation. The micro noise must exceed 0.46 and the macro noise must exceed −0.3 for a tile to receive hills or mountains; beyond that, higher elevation pushes the classification toward Mountainous and Impassable. **Swampiness** is only assigned to Flat or SmallHills tiles, modulated by a separate Perlin noise field, minimum rainfall threshold, and inverse elevation. [Confirmed from WorldGenStep_Terrain.cs]

### Complete tile data record

Every generated tile stores the following fields:

| Field | Type | Description |
|-------|------|-------------|
| `biome` | `BiomeDef` | Biome assigned by competitive scoring |
| `elevation` | `float` | Meters above sea level (≤0 = water) |
| `hilliness` | `Hilliness` | 5-level enum |
| `temperature` | `float` | Average annual °C |
| `rainfall` | `float` | mm/year |
| `swampiness` | `float` | 0–1 (Flat/SmallHills tiles only) |
| `Rivers` | `List<RiverLink>` | River connections to neighbors |
| `Roads` | `List<RoadLink>` | Road connections to neighbors |
| `feature` | `WorldFeature` | Named geographic feature (sea, range, etc.) |
| `WaterCovered` | `bool` | Derived from elevation ≤ 0 |

Additionally, `World.NaturalRockTypesIn(tileID)` deterministically assigns **2–3 stone types** per tile (from granite, limestone, marble, sandstone, slate; vacstone added in Odyssey). These are generated from seeded randomness keyed to the tile ID. The first listed type is the most abundant locally.

---

## E. Local map layer

### Map instantiation and the seed contract

Local map generation triggers when a player settles a tile, enters a quest site, or encounters an event. `MapGenerator.GenerateMap()` creates a new `Map` object and computes a deterministic seed:

```csharp
int seed = Gen.HashCombineInt(Find.World.info.Seed, parent.Tile);
```

This single integer, combined with each GenStep's unique `SeedPart` constant, ensures the **same world seed + same tile ID + same map size + same game version + same mod list = identical local map**. The Map Preview mod exploits this guarantee to render previews without actually generating a full map. Map size is player-selected: **200×200** (Small) through **400×400** (Ludeonicrous+), default **250×250** (Medium). Maps are always square. [Confirmed from MapGenerator.cs + Map Preview mod]

### Elevation and fertility grids shape the terrain skeleton

**GenStep_ElevationFertility** is the foundational local generation step. It produces two `MapGenFloatGrid` arrays — **Elevation** and **Fertility** — that all subsequent steps read. Both use 6-octave Perlin noise at frequency **0.021**, lacunarity 2.0, persistence 0.5, normalized to [0, 1] via `ScaleBias(0.5, 0.5)`. [Confirmed from GenStep_ElevationFertility.cs]

The elevation grid is then **multiplied by a hilliness factor** from `MapGenTuning`:

| Hilliness | Approx. factor | Effect |
|-----------|---------------|--------|
| Flat | ~0.0035 | Noise nearly zeroed; no rock outcroppings |
| SmallHills | ~0.012 | Scattered small rock formations |
| LargeHills | ~0.028 | Substantial hills and rock masses |
| Mountainous | ~0.042 | Large contiguous mountain ranges |
| Impassable | ~0.1+ | Most of the map is rock |

These factors are approximate values from pre-1.6 decompilations; they control what fraction of cells exceed the rock threshold. [Strong inference; exact values may differ in current versions]

For **Mountainous and Impassable** tiles, an additional `DistFromAxis` modifier pushes elevation high along one randomly chosen edge (spanning **42%** of map width via `EdgeMountainSpan = 0.42f`). This creates the characteristic "mountain wall on one side" layout. The edge is chosen to avoid conflicting with any river's entry direction. [Confirmed from code]

The **fertility grid** is computed identically but with an independent seed. It determines where rich soil, standard soil, and gravel appear.

### Rock walls, roofs, and stone-type distribution

**GenStep_RocksFromGrid** reads the elevation grid and places rock walls:

- Elevation **> 0.70** → natural rock wall spawned
- Elevation **> 0.728** (0.70 × 1.04) → thin rock roof (can collapse, removable)
- Elevation **> 0.798** (0.70 × 1.14) → overhead mountain roof (permanent, enables infestations)

Rock groups with fewer than **20 roofed cells** have their roofs stripped to prevent tiny isolated pockets. [Confirmed from GenStep_RocksFromGrid.cs]

**Stone type** at each cell is determined by a noise competition: each of the tile's 2–3 stone types gets its own Perlin noise layer. At each cell, the type with the highest noise value wins. This creates large contiguous regions of each stone type with organic, flowing boundaries. The first stone type in the tile's list tends to be most abundant. [Confirmed from RockNoises/RockDefAt code]

### Cave generation carves through mountain mass

**GenStep_Caves** only runs on tiles where `Find.World.HasCaves(tileID)` returns true (mountainous tiles have ~50% chance, large hills ~25%). It flood-fills contiguous rock groups, requiring at least **300 cells** in a group to host caves. Tunnels are carved using Perlin-guided paths (open tunnels and closed/collapsed tunnels), writing to a shared `Caves` float grid. **GenStep_CavesTerrain** then assigns gravel or rough stone floors inside carved areas. [Confirmed from code]

### Terrain resolution: the priority waterfall

**GenStep_Terrain** is the core resolver that converts the abstract float grids into concrete `TerrainDef` cells. For each cell, it evaluates terrain sources in strict priority order:

1. **Under solid rock or cave** → rock-type-specific rough floor
2. **Deep ocean** (from `BeachMaker`) → `WaterOceanDeep`
3. **River** (from `RiverMaker`) → `WaterMovingShallow` / `WaterMovingChestDeep`
4. **Beach** (from `BeachMaker`) → sand/gravel beach terrain
5. **Biome terrain patch makers** → Perlin-driven marsh, mud, marshy soil patches
6. **Gravel band** (elevation 0.55–0.61) → Gravel
7. **Rock floor** (elevation ≥ 0.61) → rough stone floor
8. **Fertility-based terrain** → biome's `terrainsByFertility` thresholds map fertility values to Soil, Rich Soil, Sand, etc.

This waterfall ensures rivers always override beach, beach overrides marsh patches, and elevation-derived rock always overrides fertility-based soil. The biome controls steps 5 and 8: swamp biomes define extensive marsh `terrainPatchMakers`, desert biomes map all fertility values to sand, boreal forests produce standard soil/rich soil patterns. [Confirmed from GenStep_Terrain.TerrainFrom() decompilation]

### Water features cross the scale boundary

**Rivers** are realized by `RiverMaker`, constructed inside `GenStep_Terrain`. The generation reads `Tile.Rivers` to find the largest river connection, then uses `WorldGrid.GetHeadingFromTo(thisTile, neighborTile)` to compute the **entry angle**. The river center is placed randomly within the middle 40% of the map. A `RiverMaker` object renders the river path across the map at the computed angle. River width and depth ratio come from the `RiverDef` (Creek is narrowest; Huge River is widest). For **coastal tiles**, the river angle is overridden to align with the coast direction ±30°. [Confirmed from GenStep_Terrain.GenerateRiver()]

**Coastal terrain** is managed by `BeachMaker`, initialized from `World.CoastDirectionAt(tileID)`. It creates a gradient from deep ocean → shallow ocean → sand beach → land, applied to the edge(s) facing ocean tiles. In 1.6, coasts and mountains are **no longer constrained to the four cardinal directions**, producing more natural, angled coastlines.

### Flora, fauna, and scattered content

**GenStep_Plants** distributes vegetation using the biome's `wildPlants` list (species + commonality weights). Each cell with positive terrain fertility can receive a plant; density scales with fertility and biome type (tropical rainforest >> desert). Cave-specific plants (Agarilux, Bryolux, Glowstool) spawn in appropriate underground cells. **GenStep_Animals** spawns starting wildlife from the biome's animal species list.

**GenStep_ScatterShrines** places **ancient dangers** — sealed rooms inside mountains containing cryptosleep caskets, mechanoids, and loot. These require overhead mountain roof to spawn and typically appear once per mountainous map. **GenStep_ScatterRuinsSimple** scatters partial wall structures on the surface.

**Mineral deposits** (GenStep_ScatterLumpsMineable) are embedded as ore lumps inside rock walls. Steel is most common; plasteel, gold, silver, jade, uranium, and compacted components are rarer. More mountainous tiles have more rock → more room for minerals. Flat tiles have nearly none.

### What is inherited versus newly generated

| Inherited from world tile | Newly synthesized at local time |
|---------------------------|-------------------------------|
| Biome (controls terrain mappings, plants, animals) | Exact cell-by-cell terrain layout |
| Hilliness (controls elevation noise amplitude) | Mountain positions and shapes |
| River links (direction, size) | River path curvature and exact cells |
| Road links (direction, type) | Road surface path across map |
| Coast adjacency (direction) | Beach gradient and shoreline shape |
| Stone types (2–3 types) | Per-cell stone type via noise competition |
| Temperature, rainfall | Weather patterns, growing seasons (runtime) |
| Swampiness | Marsh patch frequency via terrainPatchMakers |

---

## F. The world-to-local transition is a constrained reinterpretation

### What exact data crosses the boundary

The local map generator reads a compact set of tile properties through `map.TileInfo` (returns the `Tile` object) and `map.Biome` (shortcut to the tile's `BiomeDef`). The complete list of world-level data consumed during local generation:

- **`Hilliness`** → multiplier on elevation Perlin noise (GenStep_ElevationFertility)
- **`BiomeDef`** → terrain-by-fertility table, terrain patch makers, beach terrain type, plant/animal species, cave eligibility (GenStep_Terrain, GenStep_Plants, GenStep_Animals)
- **`Rivers` list** → river presence, direction (via `GetHeadingFromTo`), size (`RiverDef`) (GenStep_Terrain)
- **`Roads` list** → road presence, direction, type (GenStep_Roads)
- **Coast direction** (`World.CoastDirectionAt`) → beach edge (BeachMaker)
- **Stone types** (`World.NaturalRockTypesIn`) → rock type noise competition (GenStep_RocksFromGrid)
- **`HasCaves`** → whether to run cave carving (GenStep_Caves)
- **Seed** → `Gen.HashCombineInt(worldSeed, tileID)` → deterministic RNG for all noise fields

Temperature and rainfall are **not directly consumed** during terrain layout generation. They influenced biome selection at the world level and affect runtime weather/growing seasons, but the local terrain step reads only the biome, not the climate scalars.

### Abstracted at world level, concretized locally

The world tile is a **point with properties, not a miniature map**. It has no spatial structure — no notion of "where mountains are within this tile" or "which corner the river enters." The local map is therefore **not a geometric zoom-in** but a **constrained procedural reinterpretation**: the tile's hilliness sets the *amount* of mountain but not its *position*; the river link sets the *direction* and *size* but not the *curvature*; the biome sets the *palette* but not the *pattern*.

This means two players who settle the same tile with the same seed see identical terrain (determinism), but the terrain bears no geometric relationship to the tile's position on the globe beyond matching its climate and geographic constraints. A tile in the center of a mountain range has the same generation logic as one on the range's edge — only the hilliness enum and river/road links differ.

### Deterministic functions versus fresh synthesis

**Deterministic from tile + seed:**
- Elevation noise (Perlin seeded from `Hash(worldSeed, tileID) XOR GenStepSeedPart`)
- Fertility noise (same mechanism, different SeedPart)
- Rock type boundaries (per-type Perlin noise, seeded)
- Cave layout (seeded flood-fill + Perlin carving)
- River center position and angle (seeded random within middle 40% of map, angle from world graph)

**Newly synthesized (not stored anywhere at world level):**
- All cell-level terrain assignments
- Mountain shapes and positions
- Cave tunnel paths
- Mineral deposit locations
- Ruin and ancient danger positions
- Plant and animal positions
- Steam geyser locations

### How this design benefits the game

**Performance**: The world map consumes minimal memory (~300k tiles × ~50 bytes ≈ 15 MB). Each local map is expensive (~250k cells with terrain, things, roofs, pathfinding grids), but only 1–5 are active simultaneously. Generating a map takes seconds, not minutes.

**Memory**: Inactive tiles are never materialized. A 30% coverage world with 100,000+ land tiles generates local maps only for the 1–3 the player has actually visited.

**Replayability**: Because local terrain is synthesized from noise, no two tiles produce the same map. Players cannot predict exact terrain until they commit to settling.

**Simulation scope**: The world map supports strategic systems (caravanning, trade, faction diplomacy, quest locations) at negligible computational cost, while local maps support the full colony simulation (pathfinding, temperature, construction, combat) at a scale bounded by map size.

### Information lost at the boundary

The world tile record discards all sub-tile geography. There is no elevation profile within the tile, no river path, no mountain position. The local map must regenerate all spatial structure. This means:

- A "mountainous" tile could have mountains on the north edge or the south edge — the world map doesn't know
- A river's curvature is locally random — the world map only knows which two neighbors it connects
- Terrain patches (marsh, rich soil) have no world-level representation
- Cave systems are entirely local — the world only knows whether caves are *possible*

---

## G. Algorithmic reconstruction

### World generation (runs once)

```
FUNCTION GenerateWorld(seedString, coverage, overallTemp, overallRainfall):
    worldSeed = Hash(seedString)
    grid = CreateGeodesicSphere(subdivisions=10)          // ~300k hex tiles
    freqMult = FrequencyMultiplier(coverage)
    
    // W2: Terrain generation
    FOR EACH tile in grid:
        pos = tile.sphericalPosition
        
        // Elevation: blend Perlin + RidgedMultifractal
        elevNoise = Blend(
            Perlin(freq=0.035*freqMult, octaves=6, persist=0.4, seed=worldSeed),
            RidgedMulti(freq=0.012*freqMult, octaves=6, seed=worldSeed+1),
            control=Perlin(freq=0.12*freqMult, octaves=5, seed=worldSeed+2)
        )
        IF coverage < 1.0:
            elevNoise = ConvertToIsland(elevNoise, viewAngle)
        tile.elevation = ScaleBias(elevNoise.GetValue(pos))
        
        // Temperature: latitude + noise + elevation penalty
        tile.temperature = LatitudeCurve(pos.latitude)
                         + 4.0 * Perlin(freq=0.018*freqMult).GetValue(pos)
                         - ElevationPenalty(tile.elevation)
                         + overallTemp.offset
        
        // Rainfall: Perlin × latitude curve
        tile.rainfall = Perlin(freq=0.015*freqMult).GetValue(pos)
                      * LatitudeRainfallCurve(pos.latitude)
                      * overallRainfall.multiplier
        
        // Hilliness: noise patches + elevation thresholds
        micro = HillsMicroNoise.GetValue(pos)
        macro = HillsMacroNoise.GetValue(pos)
        IF micro > 0.46 AND macro > -0.3:
            tile.hilliness = ClassifyByElevation(tile.elevation)  // SmallHills..Impassable
        ELSE:
            tile.hilliness = Flat
        
        // Swampiness (flat/small hills only)
        IF tile.hilliness <= SmallHills:
            tile.swampiness = SwampNoise.GetValue(pos) * RainfallFactor * InvElevFactor
        
        // Biome: highest-score-wins
        tile.biome = ArgMax(biomeWorker.GetScore(tile) for biomeWorker in allBiomeWorkers)
    
    // W3: Rivers (flow-to-ocean on tile graph)        [PARTIAL RECONSTRUCTION]
    GenerateRivers(grid, worldSeed)
    
    // W4-W5: Roads
    GenerateAncientRoads(grid, worldSeed)               // civilization sim
    
    // W6: Factions
    PlaceFactionSettlements(grid)
    GenerateModernRoads(grid, factionBases)              // connect settlements
    
    // W7-W8: Features and ancient sites
    NameGeographicFeatures(grid)
    PlaceAncientSites(grid)
    
    // Assign stone types per tile
    FOR EACH landTile in grid:
        landTile.stoneTypes = PickStoneTypes(worldSeed, landTile.id, count=2..3)
    
    RETURN grid
```
**Confidence: W2 mostly confirmed from decompiled code. W3 partial reconstruction. W4–W8 confirmed existence and order; internal algorithms partially inferred.**

### Local map generation (runs per tile on demand)

```
FUNCTION GenerateLocalMap(worldSeed, tileID, mapSize):
    tile = WorldGrid[tileID]
    seed = HashCombine(worldSeed, tileID)
    map = CreateEmptyMap(mapSize)
    
    // L1: Elevation + Fertility grids
    PushSeed(seed XOR 826504671)  // ElevationFertility SeedPart
    FOR EACH cell in map:
        baseElev = Perlin(freq=0.021, octaves=6, persist=0.5).GetValue(cell)
        baseElev = (baseElev + 1) / 2  // normalize to [0,1]
        map.Elevation[cell] = baseElev * HillinessFactor(tile.hilliness)
        
        IF tile.hilliness >= Mountainous:
            map.Elevation[cell] += DistFromAxis(cell, randomEdge, span=0.42)
        
        map.Fertility[cell] = Perlin(freq=0.021, octaves=6, seed=seed2).GetValue(cell)
        map.Fertility[cell] = (map.Fertility[cell] + 1) / 2
    PopSeed()
    
    // L2: Rock walls + roofs
    PushSeed(seed XOR 1182952823)  // RocksFromGrid SeedPart
    FOR EACH cell in map:
        IF map.Elevation[cell] > 0.70:
            PlaceRockWall(cell, RockTypeAt(cell, tile.stoneTypes))
            IF map.Elevation[cell] > 0.798:
                SetRoof(cell, OverheadMountain)
            ELIF map.Elevation[cell] > 0.728:
                SetRoof(cell, ThinRock)
    RemoveSmallRoofGroups(minSize=20)
    PopSeed()
    
    // L3: Caves (conditional)
    IF World.HasCaves(tileID):
        FOR EACH rockGroup with cells >= 300:
            CarvePerlinTunnels(rockGroup, seed)
            WriteCavesGrid(carvedCells)
    
    // L4: Cave terrain
    FOR EACH cell WHERE Caves[cell] > 0:
        SetTerrain(cell, Gravel or RoughStoneFloor)
    
    // L5: Terrain resolution
    IF tile.Rivers exists:
        riverAngle = GetHeadingFromTo(tileID, largestRiverNeighbor)
        riverCenter = RandomInMiddle40Percent(map)
        riverMaker = RiverMaker(riverCenter, riverAngle, riverDef)
    IF IsCoastal(tileID):
        beachMaker = BeachMaker(coastDirection)
    
    FOR EACH cell in map:
        terrain = ResolveTerrainPriority(cell):
            1. IF hasSolidRock(cell): rockFloor
            2. IF beachMaker.IsDeepOcean(cell): WaterOceanDeep
            3. IF riverMaker.IsRiver(cell): riverTerrain
            4. IF beachMaker.IsBeach(cell): beachTerrain
            5. IF biome.patchMakers match(cell): patchTerrain (marsh, mud)
            6. IF 0.55 < elevation < 0.61: Gravel
            7. IF elevation >= 0.61: roughRockFloor
            8. ELSE: biome.terrainsByFertility.AtValue(Fertility[cell])
        SetTerrain(cell, terrain)
    
    // L6-L15: Scatter passes and population
    ScatterRockChunks(map)
    DrawRoads(map, tile.Roads, worldGrid)               // Bezier curves edge-to-edge
    ScatterMineralLumps(map, insideRockOnly=true)        // steel, gold, plasteel...
    ScatterSteamGeysers(map, count=1..3)
    ScatterRuins(map)
    PlaceAncientDangers(map, requireOverheadMountain=true)
    DistributePlants(map, tile.biome.wildPlants)
    SpawnAnimals(map, tile.biome.wildAnimals)
    FindPlayerStartSpot(map)
    ApplyFogOfWar(map)
    
    RETURN map
```
**Confidence: L1–L5 mostly confirmed from decompiled code. L6–L15 confirmed in structure; exact parameters partially inferred. Seed XOR values confirmed from code.**

---

## H. Worked example

### Starting conditions

- **World seed**: `"colonize"` → integer hash → `worldSeed = 1938462571` (illustrative)
- **Planet coverage**: 30%
- **Overall temperature**: Normal
- **Overall rainfall**: Normal

### World generation produces a tile

The engine generates ~300,000 tiles on a geodesic sphere. Consider tile **#47,832**, located at approximately 35°N latitude (northern temperate zone).

**WorldGenStep_Terrain** computes:
- **Elevation**: Perlin + RidgedMultifractal blend → **142m** above sea level (land, moderate elevation)
- **Temperature**: latitude curve gives ~18°C base − elevation penalty of ~1°C + noise offset of +1.2°C → **~18.2°C average annual**
- **Rainfall**: Perlin × latitude curve → **~830 mm/year**
- **Hilliness**: micro noise = 0.51 (> 0.46 ✓), macro noise = 0.15 (> −0.3 ✓), moderate elevation → **LargeHills**
- **Swampiness**: 0.0 (LargeHills, but just barely; swampiness is 0 because hilliness > SmallHills)

**Biome scoring**: TemperateForest scores `15 + (18.2 − 7) + (830 − 600)/180 = 15 + 11.2 + 1.28 = 27.5`. BorealForest requires colder temps and scores lower. Desert requires low rainfall. TropicalRainforest requires higher temp. **TemperateForest wins.**

**WorldGenStep_Rivers**: A river flows through this tile, connecting tile #47,831 (upstream, higher elevation) to tile #47,833 (downstream, toward coast). River type: **River** (medium size).

**Stone types**: Seeded selection assigns **granite** (primary), **limestone** (secondary).

**Tile #47,832 record**:
```
biome: TemperateForest | hilliness: LargeHills | elevation: 142m
temperature: 18.2°C | rainfall: 830mm | swampiness: 0.0
rivers: [→#47833, RiverDef.River] | roads: [→#47840, DirtRoad]
stones: [Granite, Limestone] | coast: none | caves: eligible (25% → yes)
```

### Local map generation on settlement

Player clicks "Settle" with map size 250×250. The engine computes:

```
localSeed = HashCombine(1938462571, 47832) = 762091443 (illustrative)
```

**GenStep_ElevationFertility**: 6-octave Perlin noise at frequency 0.021, multiplied by `ElevationFactorLargeHills ≈ 0.028`. This produces an elevation grid where most cells sit in the 0.0–0.5 range, with scattered peaks reaching 0.7+. No `DistFromAxis` edge-mountain effect (LargeHills, not Mountainous).

**GenStep_RocksFromGrid**: Cells with elevation > 0.70 become **granite or limestone walls** (whichever has higher noise value at that cell). Roughly **15–25%** of the map becomes rock. Cells > 0.798 get overhead mountain roofs; cells 0.728–0.798 get thin rock roofs.

**GenStep_Caves**: The tile is cave-eligible. The largest rock group exceeds 300 cells. Perlin-guided tunnels carve winding passages through the rock, creating 2–3 cave rooms and connecting corridors.

**GenStep_Terrain**:
- River: `GetHeadingFromTo(47832, 47833)` = ~135° (southeast). River center placed at roughly (110, 140). RiverMaker draws a band of `WaterMovingShallow` and `WaterMovingChestDeep` cells diagonally across the map.
- No coast: BeachMaker does nothing.
- Biome patches: TemperateForest has minimal marsh patches (swampiness = 0).
- Gravel band at elevation 0.55–0.61 forms a transition ring around rock formations.
- Rock floors at elevation ≥ 0.61.
- Remaining cells: fertility grid maps to **Soil** (common), **Rich Soil** (patches where fertility is highest), and occasional **Gravel**.

**GenStep_Roads**: `GetHeadingFromTo(47832, 47840)` = ~45° (northeast). A dirt road path is drawn as a Bezier curve from the southwest edge to the northeast edge.

**Final map**: A temperate forest with scattered **granite and limestone hills** covering ~20% of terrain, a **medium river** running southeast, a **dirt road** crossing northeast, **2 cave rooms** inside the largest hill mass with an **ancient danger** sealed within overhead mountain, patches of **rich soil** in the river's vicinity, **oak and birch trees** at moderate density, a few **deer and squirrels**, **3 steel lumps**, **1 gold lump**, and **2 steam geysers**. The player's landing zone is automatically placed in a walkable clearing near the map center.

---

## I. Comparison with Dwarf Fortress, Minecraft, and No Man's Sky

### RimWorld occupies a unique middle ground in multi-scale architecture

Each of these four games solves the "world is too big to simulate everywhere" problem differently. RimWorld's answer — a **persistent abstract globe** connected to **ephemeral concrete maps** by a narrow metadata bridge — is the most pragmatically bounded of the four.

| Dimension | RimWorld | Dwarf Fortress | Minecraft | No Man's Sky |
|-----------|----------|----------------|-----------|--------------|
| Scale tiers | 2 (globe + local) | 3 (world + region + z-level local) | 1 (continuous chunks) | 4 (galaxy → system → planet → surface) |
| World map | Hex sphere, ~300k tiles | Grid, up to 257×257 regions | None | Mathematical star field |
| Local trigger | Player settles/visits | Player embarks | Player proximity | Player approaches |
| Local persistence | While active + save | Permanent fort site | All chunks saved on disk | Seeds only + player edits |
| World simulation depth | Factions + events (runtime) | True geology, hydrology, centuries of history | None | Shallow (climate rules) |
| Precomputation | World map precomputed; local on demand | Entire world + history precomputed; local on demand | Nothing precomputed | Nothing precomputed |
| Climate model | Latitude + global sliders | Fractal noise + erosion + rain shadows | 6D noise parameter space | Star distance + noise |
| Terrain dimensionality | 2D cell grid | Full 3D z-levels | 3D voxels via density functions | 3D voxels + polygonal decoration |

**Dwarf Fortress** represents the simulation-maximalist pole. Its world generation runs true hydrological erosion: "Many fake rivers flow downward from [mountain edges], carving channels in the elevation field if they can't find a path to the sea." After erosion reshapes terrain, rain shadows and temperatures are recalculated. Then a multi-century history simulation generates civilizations, wars, and ruins. The local embark map inherits geological strata, aquifers, and underground cavern layers from the world's geological model. DF's world-to-local relationship is **physically grounded** — local geology derives from global geological simulation — while RimWorld's is **parametrically constrained** (a hilliness enum and a biome tag).

**Minecraft** has no world map at all. There is only one continuous scale of 16×16×384 block chunks, generated on demand as the player moves. Biomes are determined by a **6-dimensional noise parameter space** (temperature, humidity, continentalness, erosion, weirdness, depth) evaluated per block. The streaming model enables infinite exploration but provides no strategic overview layer. RimWorld's two-tier system is architecturally the inverse: a complete world overview exists before any local terrain is generated, enabling strategic decisions about *where* to settle before committing.

**No Man's Sky** represents the mathematical-function pole. Its 18+ quintillion planets are generated from pure mathematics evaluated at coordinates — nothing is stored. The transition from space to surface is **seamless** (no loading screens, no discrete boundary). But per-location simulation depth is shallow compared to both RimWorld and DF. RimWorld stores local maps as full simulation states; NMS regenerates terrain from seeds and stores only player-made modifications as deltas.

### How geographic determinism differs across scales

In RimWorld, the world map is **fully deterministic** from the seed (same seed → same biomes, rivers, elevation). Local maps are **conditionally deterministic**: same seed + same tile + same map size + same version = same map. But the relationship between world and local is a **lossy one-way function** — the world tile compresses all sub-tile geography into a handful of scalars, and the local map reinflates it using noise. You cannot predict the local terrain from the world map.

DF's relationship is tighter: the world stores geological and hydrological data that the local embark directly inherits. You *can* predict embark characteristics from the world map because the world map is geologically detailed.

Minecraft's is tightest of all within its single scale: the same noise functions that determine biome at the world level also determine terrain shape at the block level. There is no information loss across scales because there is only one scale.

NMS is similar to RimWorld's approach but with seamless blending: planetary parameters (atmospheric density, temperature, biome type) are point-data that get reinflated by noise at the surface level. The key difference is that NMS performs this reinflation continuously as the player moves, while RimWorld does it once per tile settlement.

---

## J. Open questions and areas of uncertainty

**What are the exact `MapGenTuning` elevation factor values in current versions?** The approximate values (~0.0035 for Flat through ~0.1 for Impassable) are from older decompilations. These constants directly control how hilliness translates to mountain coverage and are critical to precise reconstruction. They may have been adjusted in 1.4, 1.5, or 1.6.

**How exactly does `WorldGenStep_Rivers` route rivers?** The flow-to-ocean downhill algorithm is strongly inferred but the specific implementation (gradient descent on the tile graph, accumulation thresholds for river size upgrades, merge logic) has not been publicly decompiled in detail.

**How does the "ancient civilization simulation" for road generation work?** The wiki mentions it but no decompiled code has been publicly analyzed. It likely generates a simplified settlement pattern and connects nodes, but the settlement placement logic, road routing priorities, and history depth are unknown.

**How does 1.6's Map Features system interact with the GenStep pipeline?** Map Features inject additional GenSteps, but whether they modify the elevation/fertility grids (pre-terrain) or only add post-terrain features, and how they interact with the existing hilliness-based generation, requires current-version decompilation.

**How does `World.NaturalRockTypesIn` select stone types?** The method is deterministic and seeded by tile ID, but the selection algorithm (random from all stone types? biome-weighted? region-correlated?) is not confirmed from public decompilation. Players report no apparent biome correlation.

**Are there hidden tile-level properties beyond the documented fields?** The `Tile` class may store additional data (pollution levels from Biotech, anomaly markers from Anomaly DLC, tile mutator references from 1.6) that affect local generation in ways not yet publicly documented.

**How faithfully do local rivers represent world-level river topology?** The local river angle is derived from the world graph heading, but the center position is randomized. If two adjacent tiles both have the same river, do their local rivers visually connect at the map edge? Evidence suggests approximate alignment but not pixel-perfect continuity, since each map is generated independently.

**How does swampiness modulate local terrain?** Swampiness is stored per tile (0–1, Flat/SmallHills only) and presumably controls the amplitude of marsh terrain patch makers, but the exact multiplication or threshold mechanism in `GenStep_Terrain` has not been confirmed in detail.

---

## Best current reconstruction of the full pipeline

1. **Player provides**: seed string, planet coverage (30/50/100%), overall temperature, overall rainfall, faction settings.
2. **Globe construction**: `PlanetShapeGenerator.Generate(10)` creates ~300,000 hexagonal/pentagonal tiles on a geodesic sphere with compressed adjacency arrays.
3. **Per-tile terrain fields**: `WorldGenStep_Terrain` computes elevation (Perlin + RidgedMultifractal blend), temperature (latitude + noise + elevation), rainfall (Perlin × latitude curve), hilliness (micro/macro noise + elevation thresholds), and swampiness (noise, Flat/SmallHills only).
4. **Biome assignment**: Each `BiomeWorker.GetScore(tile)` is evaluated; highest positive score wins. 12+ biomes compete based on temperature, rainfall, and elevation.
5. **River generation**: `WorldGenStep_Rivers` creates river networks flowing downhill to oceans, stored as `RiverLink` pairs between adjacent tiles.
6. **Road generation**: Ancient asphalt roads via civilization simulation; modern roads connecting faction bases.
7. **Faction placement**: `WorldGenStep_Factions` places settlements on valid land tiles respecting biome and spacing constraints.
8. **Feature naming**: Geographic clusters (seas, ranges, deserts) receive names.
9. **Stone type assignment**: Each land tile receives 2–3 deterministic stone types.
10. **World map complete**. Player explores the globe and selects a tile to settle.
11. **Local seed derivation**: `seed = Gen.HashCombineInt(worldSeed, tileID)`. Each GenStep further XORs with its unique `SeedPart`.
12. **Elevation grid**: 6-octave Perlin noise at frequency 0.021, multiplied by hilliness factor. Mountainous tiles get an edge-mountain wall (42% span). Stored in `MapGenerator.Elevation`.
13. **Fertility grid**: Independent 6-octave Perlin at same frequency. Stored in `MapGenerator.Fertility`.
14. **Rock walls**: Cells with elevation > 0.70 become stone walls (type from per-stone Perlin competition). Thin rock roof at > 0.728; overhead mountain at > 0.798. Groups < 20 roofed cells stripped.
15. **Caves**: If tile is cave-eligible, flood-fill rock groups ≥ 300 cells; carve Perlin-guided tunnels.
16. **Terrain resolution**: Priority waterfall per cell — rock floor → deep ocean → river → beach → biome patches → gravel band (0.55–0.61) → rock floor (≥ 0.61) → fertility-mapped terrain (soil/rich soil/sand).
17. **Road drawing**: Bezier curves from edge to edge matching world-level road link directions.
18. **Scatter passes**: Mineral lumps in rock, steam geysers on surface, ruined walls, ancient dangers inside mountains.
19. **Ecology**: Plants distributed by biome species list × terrain fertility; animals spawned from biome animal list.
20. **Player start spot**: Located in a walkable clearing near map center.
21. **Fog of war**: Applied over unseen areas.
22. **Map live**. Colony simulation begins on the fully generated local map.