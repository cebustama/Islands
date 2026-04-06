# RimWorld treats biome boundaries as hard edges — until recently

**Vanilla RimWorld (versions 1.0–1.5) performs zero biome blending at local map scale.** Every cell on a local map draws terrain, plants, and animals from a single `BiomeDef` — the one assigned to the world tile. The map generator never queries neighboring world tiles for biome data. This architectural assumption (`map.Biome` as a scalar, not a spatial grid) means settling on a biome border produces an identical map to settling in a biome's center. The Odyssey DLC (version 1.6, July 2025) introduced a partial fix: a "Mixed Biome" map feature that splits qualifying border tiles into two biome zones along a noisy line. The modding community solved this more thoroughly years earlier with the Biome Transitions mod, which introduced a per-cell `BiomeGrid` supporting up to seven biomes per map.

---

## Source code confirms a single-biome architecture

Decompiled C# source code from RimWorld's map generation pipeline reveals that **every biome-dependent terrain lookup references `map.Biome` — a single `BiomeDef` for the entire local map**. The critical class `GenStep_Terrain` generates terrain cell by cell using this flow:

1. Check for river terrain via `RiverMaker`
2. Check `BeachMaker.BeachTerrainAt(c, map.Biome)` — singular biome
3. Iterate `map.Biome.terrainPatchMakers[i].TerrainAt(c, map, fertility)` — singular biome
4. Look up `TerrainThreshold.TerrainAtValue(map.Biome.terrainsByFertility, fertility)` — singular biome
5. Fallback to gravel or rock based on elevation thresholds

The only reference to neighboring world tiles in `GenStep_Terrain` calculates river flow direction via `Find.WorldGrid.GetHeadingFromTo()`. `BeachMaker` uses `World.CoastDirectionAt(map.Tile)` to detect ocean adjacency, but this detects water versus land — it does not sample a neighbor's biome tables. `BiomeDef.CommonalityOfTerrain` and `BiomeDef.CommonalityOfPlant` are pure lookups against internal lists with **no positional parameter and no neighbor-biome parameter**.

`GenStep_ElevationFertility` generates Perlin noise grids for elevation and fertility as biome-independent math. `GenStep_RocksFromGrid` assigns rock types from world tile stone types without neighbor biome data. `WildPlantSpawner` uses `map.Biome` to determine species and density. The `MapGenerator` orchestrator passes a map with a single `map.Biome` to every generation step. In short, pre-Odyssey RimWorld has **zero transition zone logic, zero biome blending, and zero per-cell biome resolution**.

## Community documentation and player experience

The RimWorld Wiki states definitively: **"Each world tile has one particular biome."** The Modding Tutorials/Biomes page confirms that biome calculation during world generation evaluates each tile individually, assigns the highest-scoring biome via `BiomeWorker.GetScore()`, and that score is final. No wiki article documents any biome transition behavior at local map scale for the base game prior to 1.6.

Players settling on biome borders pre-Odyssey reported **no visual difference** compared to settling in a biome's center. The community recognized this as a limitation. Steam Workshop comments on the Biome Transitions mod called it "one of the very few mandatory mods that should basically be included in the base game." A community discussion thread captured the conceptual problem well: *"IRL biomes don't really have clear borders. The edge of a desert looks like slightly more sand and slightly less plants."* The Geological Landforms mod description framed vanilla map generation as limited to "simply potentially having either a cliff or a coast on one edge of your map."

Tynan Sylvester's Odyssey preview blog post presented mixed biomes as a **new feature**: "With Odyssey, biomes can blend together on a single map. For example, a map might have a temperate forest in the north and a desert in the south." The framing as novel confirms the feature did not exist prior to 1.6.

## Odyssey DLC introduced a binary biome split

The Odyssey DLC (RimWorld 1.6, released July 11, 2025) added **85+ "map features,"** one of which is "Mixed Biome." This feature activates with a **20% base probability** on world tiles bordering a different biome. When active, the local map is divided into two zones along a distorted (noisy) line, with each zone using terrain, plants, and wildlife from its respective biome.

Key constraints of the Odyssey implementation:

- **Exactly two biomes** per map — if a tile borders multiple biomes, only one secondary biome is selected
- Both biomes must be from an **approved whitelist** of 14 surface biomes (arid shrubland, boreal forest, cold bog, desert, extreme desert, glacial plain, grassland, ice sheet, temperate forest, temperate swamp, tropical rainforest, tropical swamp, tundra) — sea ice, scarlands, glowforest, and lava field are excluded
- The split is **roughly half-and-half along a noise-distorted line**, not a gradient or interpolation
- A new `MapGenUtility` class handles per-cell terrain assignment using biome-specific modifiers (`gravelTerrain`, `coastalBeachTerrain`, `lakeBeachTerrain`, `riverbankTerrain`)
- The feature is configured via `TileMutatorDefs` with biome whitelists and blacklists
- Weather and temperature remain determined by world tile properties, not per-cell

This is a **binary partition, not a blending gradient**. Each half of the map uses its biome's terrain tables independently. There is no interpolation of terrain commonalities or fertility-based mixing between the two biomes.

## Modding community solved this more completely in 2022

The most significant mod addressing biome boundaries is **Biome Transitions** by m00nl1ght (first published May 29, 2022, Steam Workshop ID 2814391846), built as an add-on to the same author's **Geological Landforms** framework (Steam Workshop ID 2773943594). This mod's approach is architecturally instructive.

**The `BiomeGrid` MapComponent** is the core innovation. It replaces vanilla's scalar `map.Biome` with a per-cell biome assignment grid:

- `Entry[] _grid` — an array indexed by cell position (`x * mapSize + z`), mapping each cell to a biome `Entry`
- Each `Entry` contains a `BiomeDef BiomeBase`, optional `BiomeVariantLayer` overlays, and a resolved effective `Biome` property
- Serialized as a compact `ushort[]` (supporting up to 65,535 distinct biome combinations per map)
- `BiomeAt(IntVec3 c)` provides per-cell biome queries to all patched systems

**Spatial mapping logic**: During map generation, the mod examines the settled world tile's neighbors to determine which biomes are adjacent and in which directions. A spatial function maps these directions to local map coordinates, placing each neighbor's biome on the corresponding edge or corner. Noise functions (from the TerrainGraph library) distort boundaries for natural-looking transitions. The intrusion distance is configurable via a node-based landform editor.

**Harmony patches** redirect vanilla systems to consult `BiomeGrid` instead of `map.Biome`:

- `WildPlantSpawner.WildPlantSpawnerTickInternal` — plants spawn according to the biome at their cell position
- Terrain generation GenSteps — `terrainsByFertility` and `terrainPatchMakers` lookups use the cell's local biome
- Animal spawning — animals from secondary biomes can spawn in their biome zones
- `MapGenUtility` terrain modifiers — biome-specific terrain fields resolve per-cell

**Capabilities versus Odyssey**: The mod supports **up to 7 biomes per map** (versus Odyssey's 2), distributes them spatially based on actual world tile neighbor directions (versus Odyssey's binary split), and works with any modded biomes automatically. When both the mod and Odyssey DLC are active, the mod disables Odyssey's Mixed Biome feature and uses its own implementation. An experimental "unidirectional transitions" option prevents the symmetric issue where both tiles at a border show transitions.

The source code is available on GitHub under CC BY-NC-SA 4.0 at `github.com/m00nl1ght-dev/GeologicalLandforms`, with the BiomeGrid implementation at `Sources/GeologicalLandforms/BiomeGrid.cs` (368 lines) and the Biome Transitions add-on under `AddOn/BiomeTransitions/`.

## Other modding approaches to biome boundaries

Several alternative strategies exist in the modding ecosystem:

| Mod | Approach | Mechanism |
|-----|----------|-----------|
| **Biome Transitions** + Geological Landforms | Per-cell spatial biome grid | `BiomeGrid` MapComponent with Harmony patches; up to 7 biomes per map |
| **Odyssey DLC** Mixed Biome | Binary biome partition | `TileMutatorDef` feature; 2 biomes split along noisy line |
| **More Vanilla Biomes** / **RimUniverse** | Transitional biome types | New `BiomeDef`s (Grasslands, Woodland, Alpine Meadow) that naturally occur at world-level boundaries |
| **Yayo's Nature** | Temporal biome change | Whole-map biome replacement every 60 days with gradual 12-day transformation |
| **Terra Project** | Additional terrain types | New terrain definitions for smoother visual transitions between biome-characteristic terrains |
| **ReGrowth** series | Biome enrichment | Additional plants, textures, and atmospheric effects per biome; does NOT implement multi-biome maps |

The **transitional biome type** approach (More Vanilla Biomes, RimUniverse) is worth noting as a design alternative. Rather than blending two biomes on a single map, these mods insert new world-level biome types at the boundaries between major biomes. RimUniverse uses a Whittaker biome diagram for placement, adding Woodland between Temperate Forest and Grassland, Permafrost between Tundra and Ice Sheet, etc. This avoids the per-cell complexity entirely but only smooths transitions at world-map resolution.

**ReGrowth** is often mentioned in the context of biome boundaries but does **not** implement multi-biome local maps. It adds visually rich new biome types to the world map and is compatible with (but does not replace) Biome Transitions.

## Summary of findings

| Question | Answer |
|----------|--------|
| Does vanilla RimWorld (pre-1.6) have biome transition at local map scale? | **No.** Zero neighbor biome sampling. Pure single-biome generation. |
| Does Odyssey DLC (1.6) have biome transition? | **Partial.** Binary 2-biome split on ~20% of qualifying border tiles. Not a gradient. |
| What mechanism produces the Odyssey transition? | `TileMutatorDef` "Mixed Biome" feature; `MapGenUtility` per-cell biome-aware terrain assignment; noisy line divides map in half. |
| Has the community identified the absence of transitions as a limitation? | **Yes.** The Biome Transitions mod (2022) with 2,500+ Steam ratings was created specifically to address this. Community called it "mandatory." |
| Best mod implementation for biome blending? | **Biome Transitions** by m00nl1ght: `BiomeGrid` per-cell biome array, Harmony patches to plant/terrain/animal systems, up to 7 biomes per map, directionally accurate placement. Open source. |
| Fundamental architectural limitation in vanilla? | Biome is a **map-level scalar** (`map.Biome`) rather than a **per-cell spatial property**. Every vanilla system assumes one biome per map. |
| What architectural change is required for multi-biome? | A `MapComponent` storing per-cell biome assignments (compact `ushort[]` grid), plus Harmony patches to redirect `map.Biome` lookups in terrain generation, plant spawning, and animal spawning to per-cell queries. |

## Conclusion: implications for Islands.PCG

RimWorld's experience demonstrates a clear evolution from hard biome boundaries to partial blending. The vanilla architecture's core assumption — biome as a scalar property of the entire local map — is the root cause of hard edges. The **`BiomeGrid` pattern** (per-cell biome assignment as a compact indexed array, populated by a spatial function that maps world-tile neighbor directions to local coordinates with noise-distorted boundaries) is the proven solution, independently validated by both the modding community (2022) and Ludeon Studios' official implementation (2025).

For Islands.PCG, three design insights emerge. First, **biome should be a per-cell property from the start** rather than retrofitted — this avoids the patching complexity that RimWorld mods require. Second, **noise-distorted Voronoi-like partitioning** (where neighbor tile directions determine zone placement and Perlin noise distorts boundaries) produces natural-looking transitions without requiring true biome interpolation. Third, **weather and temperature can remain tile-level properties** without breaking immersion, as the Biome Transitions mod demonstrates — players accept this practical compromise. The Odyssey DLC's simpler 2-biome split confirms that even partial solutions significantly improve perceived naturalness over hard edges.