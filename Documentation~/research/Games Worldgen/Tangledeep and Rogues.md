# How Tangledeep builds its dungeon floors

**Tangledeep generates each dungeon floor by selecting from roughly twelve distinct layout algorithms, parameterized through a master XML configuration file, and furnishing the result with region-themed enemies, objects, and hazards.** This makes it a **region-tuned, hybrid constructive generator** — a modern descendant of Rogue's room-and-corridor tradition that replaces a single universal algorithm with a pipeline of interchangeable layout generators selected per floor. Every floor in the game (except Item Dreams) is generated at character creation, using parameters from `mapgenerator.xml` that specify size, tileset, challenge rating, layout algorithm, spawn table, and stair connectivity. The result is a game that feels "finely tuned" despite being procedural — a characteristic it shares with Brogue and DCSS rather than Rogue or Angband.

Tangledeep occupies a specific niche in the roguelike dungeon-generation lineage. Where Rogue used one algorithm for all levels, NetHack mixed procedural and handcrafted levels, and Angband introduced vault templates, Tangledeep pushes further toward **data-driven multi-algorithm generation**, selecting different generators for different biomes while maintaining a unified XML-driven configuration layer. This report reconstructs the likely generation pipeline, maps its relationship to classic roguelike generators, and explicitly distinguishes confirmed evidence from inference and speculation throughout.

---

## A. Executive summary

Tangledeep is a turn-based roguelike dungeon crawler built in Unity/C# by Andrew Aversa (Impact Gameworks), with its full source code publicly available on GitHub. Its dungeon consists of **20 numbered main floors** plus boss arenas, side areas, Item Dreams, and DLC content, all organized into themed biome regions with two branching decision points.

The game's generation system is best classified as **region-tuned procedural generation with handcrafted exceptions**. Each floor is defined by an XML entry in `mapgenerator.xml` specifying a `LayoutType` parameter — described in the official modding documentation as "the procedural algorithm used to create the floor." The developer has confirmed approximately **12 different layout generators** producing styles ranging from "narrow rooms connected by hallways" to "maze-like," "wide-open," and "cave-like" layouts. Boss floors and certain special areas use a `SPECIAL` layout type with handcrafted room templates.

Compared to classic Rogue, which uses a single 3×3 grid room-and-corridor algorithm for all 26 levels, Tangledeep is dramatically more varied. It inherits the descending-floor structure, turn-based grid logic, and content-scaling-with-depth principles from the roguelike tradition, but modernizes them with multiple generation algorithms, XML data-driven configuration, visual theming via 16 tilesets, and a hybrid approach mixing procedural layouts with handcrafted content.

---

## B. Step-by-step floor-generation pipeline

The following pipeline is reconstructed from the modding documentation, developer statements, patch notes, and game behavior. Each stage is marked with its confidence level.

### Stage 1: Character creation triggers full dungeon generation
**Confidence: Confirmed** (modding documentation states: "In Tangledeep, every floor of the dungeon — which includes all side areas — is generated upon creating a character.")

All main dungeon floors, side areas, and hidden locations are generated at once when a new character is created. This pre-generation allows the rumor system (NPC Erin) to reference undiscovered areas and ensures seamless floor transitions. The only exception is **Item Dreams**, which are generated on demand during gameplay.

### Stage 2: Floor parameter loading from mapgenerator.xml
**Confidence: Confirmed** (XML structure documented in modding docs)

Each floor reads its configuration from the master `mapgenerator.xml` file. Parameters include:

| Parameter | Function | Example |
|-----------|----------|---------|
| `Level` | Unique floor ID | 226 |
| `LayoutType` | Algorithm selector | SPECIAL |
| `Size` | Floor dimensions | 28 |
| `Tileset` | Visual theme | BLUESTONEDARK |
| `ChallengeValue` | Difficulty (1.0–1.9) | 1.4 |
| `SpawnTable` | Monster spawn reference | (from spawntables.xml) |
| `StairsUpToLevel` | Return stair target | 110 |
| `StairsDownToLevel` | Forward stair target | (varies) |
| `SideArea` | Side area flag | (boolean) |
| `UnbreakableWalls` | Wall destructibility | 1 |
| `SpecialRoomTemplate` | Handcrafted layout name | (for SPECIAL type) |

### Stage 3: RNG state initialization
**Confidence: Strong inference** (seeds confirmed via Patch 1.07/1.08; deterministic replay required for Daily/Weekly challenges)

A world seed, either player-specified or randomly generated, initializes the RNG state. Since Daily and Weekly challenges produce identical dungeons for all players sharing the same seed, the seed must deterministically control layout generation, monster placement, and item distribution. The seed likely feeds a master RNG that is sequentially consumed as each floor is generated during character creation.

### Stage 4: Layout algorithm selection and spatial generation
**Confidence: Confirmed that ~12 generators exist; specific algorithm details are inferred**

Based on the `LayoutType` value, one of approximately 12 layout generators executes. The developer described four broad categories in a September 2017 Steam discussion:

- **Rooms-and-corridors**: "Narrow rooms connected by hallways" — used in Old Amber Station (floors 7–10, Branch B) and early Stonehewn Halls
- **Maze-like**: Used in Spiny Maze side area and Ancient Ruins (floors 12–13)
- **Wide-open**: Large open areas used in Fungal Caves (floors 7–10, Branch A) and late Ancient Ruins (floors 14–15)
- **Cave-like**: Organic cave shapes, likely using cellular automata or similar techniques

Patch notes reference additional named types: a **"Volcano" map type** (with specific monster spawn behavior) and **"cave rooms" map code** (which had a bug where rooms could spawn with no entrance). The `SPECIAL` type loads a handcrafted text-based room template instead of running a procedural algorithm.

### Stage 5: Terrain feature placement
**Confidence: Confirmed** (terrain types documented; developer described the terrain drawing system as a major development challenge)

Five terrain types are placed on the generated layout: **LAVA, WATER, ELECTRIC, MUD, and ISLANDSWATER** (the last being "Deadly Void" from Item Dreams). The developer described the terrain rendering as using "an intricate system of connection and literally thousands of sprites to account for all the different connection possibilities" — indicating an autotiling system that handles transitions between terrain types.

### Stage 6: Stair and exit placement
**Confidence: Confirmed** (stair connections explicitly defined in XML; bug fixes reference stairs encased in stone)

Stairs are placed connecting to the floor IDs specified in `StairsUpToLevel` and `StairsDownToLevel`. Patch 1.4 added "logic that should detect & correct 'bad' map generation where starting stairs are encased in stone" — confirming that stair placement occurs after layout generation and requires post-hoc validation.

### Stage 7: Side area and hidden location attachment
**Confidence: Strong inference** (wiki confirms hidden locations "do not always show up in the same place twice across multiple playthroughs"; modding docs show `MinSpawnFloor`/`MaxSpawnFloor` for side areas)

Hidden locations (side areas accessible via stairs) are randomly assigned to main dungeon floors within configured floor ranges. Approximately 40% spawn hidden and must be discovered via the rumor system. Side areas have their own floor definitions with independent `LayoutType`, `Size`, `Tileset`, and `SpawnTable` entries.

### Stage 8: Secret area and destructible wall insertion
**Confidence: Strong inference** (wiki explicitly describes secret areas behind destructible walls with merchants; `UnbreakableWalls` parameter controls wall destructibility)

Secret rooms containing exclusive merchants are embedded within floor layouts behind breakable walls. The Knight's Shovel item can destroy these walls unless the `UnbreakableWalls` flag is set. The placement of secrets likely occurs as a post-processing pass after primary layout generation, carving hidden pockets adjacent to the main traversable space.

### Stage 9: Monster spawning from spawn tables
**Confidence: Confirmed** (spawntables.xml documented; patch notes describe monster spawn bug fixes)

Each floor references a spawn table from `spawntables.xml` that defines which monsters can appear and their relative weights. Patch notes reveal a **"lax factor" for the monster spawn algorithm** that allows "less-than-ideal monster placement" on smaller maps to achieve "desired density" — indicating the system attempts to place monsters in valid floor positions and has fallback logic when ideal positions are scarce.

### Stage 10: Object and item placement
**Confidence: Confirmed** (mapobjects.xml documented; modding docs describe destructible objects and map objects)

Map objects — including treasure chests, barrels, crates, fountains, ice blocks, and Pandora's Boxes — are placed on the floor. Objects are defined in `mapobjects.xml` and can have `AutoSpawn` enabled for automatic placement. Pandora's Boxes appear from Floor 3 onward (one per floor, unless `NoSpawner` is flagged). Loot generation uses floor-level parameters including `BonusMagicChance`, `BonusLegendaryChance`, `MinItems`, `MaxItems`, and `MinMagicItems`.

### Stage 11: Validation and cleanup
**Confidence: Strong inference** (multiple bug fixes confirm validation issues were found and corrected)

A final pass validates floor playability. Evidence from patch notes includes:
- Fixing "cave rooms" where rooms could spawn with no entrance
- Fixing monsters spawning "enclosed in rock" in cave layouts
- Adding detection for stairs "encased in stone"
- Preventing crates and barrels from overlapping planks

These fixes demonstrate an ongoing validation layer that checks connectivity, entity placement legality, and object overlap.

---

## C. What defines a "floor" in Tangledeep

A Tangledeep floor is a discrete, self-contained 2D tile grid defined by the intersection of several systems:

**Layout geometry** is determined by the `LayoutType` algorithm and `Size` parameter. Floors range from compact (~28 tiles on a side for small side areas) to larger expanses for late-game main floors. The geometry is a grid of tiles where each cell holds terrain type, walkability, and visibility data. Walls use the selected tileset's sprites with autotiling for visual coherence.

**Floor-specific monster pools** are defined by the `SpawnTable` reference, which points to an entry in `spawntables.xml`. Different biomes have entirely different monster rosters. Cedar Caverns features "weak, relatively passive monsters," while late-game areas feature robotic enemies with fire attacks (Ancient Ruins) or spirits and bandits (Stonehewn Halls).

**Object population** includes destructible containers (chests, barrels, crates), fountains for flask refills, Pandora's Boxes (from Floor 3+), and environmental objects specific to the biome. Conduit panels appear starting at Floor 16 (Assembler Facility).

**Secrets and breakable-wall spaces** contain exclusive merchants not found elsewhere. The wiki notes that "merchant locations vary per playthrough," indicating secrets are procedurally placed rather than fixed.

**Region and theme identity** is conveyed through the `Tileset` parameter (16 options including EARTH, LUSHGREEN, FUTURE, VOLCANO, NIGHTMARISH, SAND) combined with the layout algorithm. Cedar Caverns uses verdant grass with water pools; Ancient Ruins has a futuristic/sci-fi aesthetic with pillars; Stonehewn Halls features stony corridors with water and lava.

**Difficulty curve contribution** is encoded in `ChallengeValue` (ranging from 1.0 to 1.9 in 0.05 increments), which translates to a visible "Rank" and scales monster stats. The game displays contextual difficulty indicators (Easy/Tricky/Hard) relative to the player's current strength.

---

## D. Variation across dungeon regions and progression

### Biome-specific generation parameters

Different dungeon regions demonstrably use different layout generators and parameters. The evidence is strong:

| Region (Floors) | Layout Style | Evidence Source |
|-----------------|-------------|-----------------|
| Cedar Caverns (1–5) | Moderate rooms, water pools, grass | Wiki description |
| Fungal Caves (7–10, Branch A) | **Wide-open** generation | Wiki: "requires crowd control" |
| Old Amber Station (7–10, Branch B) | **Corridors and tight rooms** | Wiki: "corridors and small tight rooms connected together" |
| Ancient Ruins (12–15, Branch A) | **Maze → open transition** | Wiki: "first two floors are giant maze layouts; last two are more open" |
| Stonehewn Halls (12–15, Branch B) | **Expanding rooms** | Wiki: "tight rooms that gradually expand to giant expanses" |
| Spiny Maze (side area) | **Maze-like** | Wiki: "maze-like layout" |

This confirms that different biomes select different `LayoutType` values and likely modulate `Size` parameters across their floor range. Stonehewn Halls is particularly interesting: it explicitly transitions from tight to expansive across just four floors, suggesting either different `LayoutType` values per floor within the same biome or progressive `Size` parameter scaling.

### Boss floors are handcrafted exceptions

Boss floors (Dirtbeak's Den at 6F, Bandit HQ at 11F, Assembler Facility at 16F, Central Processing at 20F, The Core at 21F) use the `SPECIAL` layout type with text-based room templates. Central Processing features a distinctive **star-shaped layout** with four corridors leading to four sub-section stairways — a design that would be impractical to produce reliably through pure procedural generation.

### Progression trends

Later floors become **larger and more complex** but not uniformly so. The branching structure means players encounter either the claustrophobic Old Amber Station or the open Fungal Caves at the same depth, creating dramatically different tactical experiences. The overall trend moves from simple nature-themed areas (Cedar Caverns) toward increasingly technological environments (Ancient Ruins, Assembler Facility, Central Processing), with corresponding shifts in hazard types from water and mud to lava, conduit panels, and robotic enemies.

### Side areas and mini-dungeons

Over **20 hidden locations** can appear as side areas on main dungeon floors. These include combat encounters (Abandoned Facility, Stalker Nest, Elemental Lair), specialty shops (Casino, Pet Shoppe, Desert Oasis), and multi-floor sub-dungeons (Flooded Temple has two floors; Casino has two floors). Each hidden location has its own floor definition in `mapgenerator.xml` with independent layout, tileset, and spawn parameters. Side area floor ranges are controlled by `MinSpawnFloor`/`MaxSpawnFloor`, so specific hidden locations only appear within appropriate depth ranges.

**Item Dreams** are the only floors generated during gameplay rather than at character creation. They are mini-dungeons accessed via the Dreamcaster using Orbs, featuring visual themes (deserts, tundras, space) not found in the main dungeon, Dream Crystals with aura effects, and Deadly Void hazards. Item Nightmares use a distinctive **spiral-shaped cave layout**.

**Wanderer's Journeys** (Legend of Shara DLC) add 10–50 floor procedural adventures with unique gimmick rules, most of which reset the player to level 1. These represent the most extensive use of the procedural generation system, as they must produce tens of coherent floors with appropriate difficulty scaling.

---

## E. Seeds, determinism, and replay structure

World seeds were introduced in **Patch 1.07** and expanded in **Patch 1.08**. Players can input a specific numeric seed or use a random one. Seeds determine dungeon layout, monster positions, item drops, and loot — the full procedural generation state. Community members share seeds for specific finds (e.g., "seed 7175 has a jewelry vendor with floramancer gear set on floor 2 of Riverstone Waterway").

**Daily Challenges** use a shared seed that changes every 24 hours; **Weekly Challenges** use a seed that persists for seven days. All players competing on the same challenge seed face identical dungeon layouts, creating a competitive speedrun-like experience with leaderboard support.

Because all floors (except Item Dreams) are generated at character creation, the seed must deterministically produce the entire dungeon in a single pass. This is consistent with the confirmed pre-generation architecture. What likely varies from the seed is player-driven runtime state: which monsters the player has killed, which items have been picked up, and how spawning timers progress (since cleared floors have "diminished maximum monster spawns but continue spawning indefinitely").

The save/load system — described by the developer as one of the "two biggest obstacles during development" — must serialize the complete generated dungeon state. This is consistent with the pre-generation model: the full dungeon is generated once, serialized to the save file, and then progressively modified by player actions.

---

## F. Data-driven and hybrid content

Tangledeep's generation system is **heavily data-driven**, using XML configuration files to separate generation parameters from engine code. This architecture enables the modding system and allows rapid designer iteration.

### XML configuration files

| File | Contents |
|------|----------|
| `mapgenerator.xml` | Master floor definitions: layout type, size, tileset, challenge value, stair connections, spawn tables |
| `spawntables.xml` | Monster spawn tables with weighted entries per floor/region |
| `mapobjects.xml` | Destructible object definitions (chests, barrels, fountains, etc.) |
| `shops.xml` | Shop inventories and merchant configurations |
| `monsters.xml` | Monster stat definitions |

### Room templates for handcrafted floors

`SPECIAL` layout floors use text-based room templates that define the entire floor as a grid of tiles. These templates can include pre-placed monsters (MONSTER/RANDOMMONSTER with optional ChampMods), terrain features (LAVA, WATER, ELECTRIC, MUD), and destructible objects. This is how boss arenas, Central Processing's star-shaped layout, and certain side areas achieve their distinctive designs.

### The hybrid philosophy

The game's Steam store page describes it as featuring "finely-tuned, procedurally-generated **and handcrafted** gameplay." This hybrid approach works on multiple levels:

- **Algorithm selection**: ~12 procedural generators chosen per floor, versus `SPECIAL` handcrafted layouts for boss and key story floors
- **Parameter tuning**: Each floor's XML entry fine-tunes the procedural generation (size, density, difficulty) even within the same algorithm
- **Content tables**: Spawn tables, loot tables, and shop tables are externally configurable, allowing designers to precisely control what appears on each floor
- **Object library**: A rich library of map objects in `mapobjects.xml` provides the furnishing vocabulary for both procedural and handcrafted floors

This is architecturally similar to how DCSS uses different generators for different branches while sharing a common entity system, and how Angband uses `dungeon_profile.txt` to configure generation parameters per level type.

---

## G. Tangledeep as a roguelike descendant

### Structural comparison across the lineage

| Feature | Rogue (1980) | Brogue (2009) | NetHack (1987) | Angband (1990) | Tangledeep (2018) |
|---------|------|--------|---------|---------|------------|
| **Layout algorithm** | Single 3×3 grid | Room accretion + CA | Room-corridor + maze + scripts | Block-grid + vaults + profiles | ~12 selectable generators |
| **Grid size** | 80×24 (fixed) | ~80×30 (fixed) | 80×21 (fixed) | Up to 256×256 | Variable per floor |
| **Room types** | Rectangles only | Cross rooms, CA blobs, circles | Rectangles + special rooms | 10+ types including vaults | Multiple styles per algorithm |
| **Connectivity** | Spanning tree | Inherent tree + loops | All-pairs attempt | Cyclic all-pairs | Algorithm-dependent |
| **Special/fixed levels** | None | Machines by depth | ~30+ scripted levels | Vault templates | SPECIAL layout + templates |
| **Content scaling** | Monster table by depth | Depth-indexed auto-generators | Difficulty + special rooms | Depth-based + OOD | ChallengeValue + spawn tables |
| **Level persistence** | Yes | Yes | Yes | No | Yes |
| **Data-driven config** | None | Internal parameters | Lua scripts (3.7+) | dungeon_profile.txt + vault.txt | XML (mapgenerator.xml + more) |
| **Floor feel** | Grid-geometric | Organic-naturalistic | Eclectic-narrative | Massive-explorative | Themed-varied |

### What Tangledeep inherits from Rogue

Tangledeep preserves the foundational roguelike structure: a **descending sequence of procedurally generated grid-based floors**, explored in turn-based fashion, with permadeath consequences. The "rooms connected by hallways" layout type is a direct descendant of Rogue's room-and-corridor generation. The concept of stairs connecting floors vertically, monsters becoming harder with depth, and items growing in power — all originate in Rogue's 1980 design.

### What it inherits from the broader tradition

From **NetHack**, Tangledeep takes the concept of mixing procedural and handcrafted levels. NetHack's special levels (Medusa's Island, the Castle, Sokoban) have a direct analog in Tangledeep's `SPECIAL` layout boss floors. The idea that certain floors deserve designer attention while the majority can be procedural is a NetHack innovation that Tangledeep fully embraces.

From **Angband**, Tangledeep adopts the principle of data-driven configuration. Angband's `vault.txt` template system and later `dungeon_profile.txt` parameterization are conceptual ancestors of Tangledeep's `mapgenerator.xml`. The idea that different floor types should use different room geometries and generation parameters — realized in Angband 4.x with its classic/modified/moria/labyrinth/cavern profiles — is taken further in Tangledeep's 12-generator system.

From **Brogue**, Tangledeep inherits the aspiration toward floors that feel designed rather than random. Brogue's use of cellular automata for organic cave shapes, its careful loop insertion for tactical routing, and its environmental storytelling through terrain features all find echoes in Tangledeep's varied layout styles and multi-terrain floors.

### What Tangledeep modernizes

Tangledeep's primary innovation is **biome-indexed algorithm selection**. Rather than using one algorithm everywhere (Rogue) or switching between a small number of modes (Angband's 5 profiles), Tangledeep maps each biome region to specific layout generators that produce thematically appropriate spaces. Fungal Caves demand wide-open areas for crowd control; Old Amber Station uses tight corridors for choke-point tactics; Ancient Ruins transition from maze to open across four floors. This creates a **direct coupling between generation algorithm and gameplay identity** that is more intentional than in most predecessors.

The **XML-driven full-dungeon pre-generation** is also distinctive. By generating all floors at character creation, Tangledeep can reason about the full dungeon structure (enabling the rumor system) while ensuring deterministic seed-based replay. This contrasts with Angband's on-demand non-persistent generation and NetHack's level-by-level persistent generation.

---

## H. Historical lineage of roguelike dungeon generation

The evolution from Rogue to Tangledeep traces a clear arc from simplicity to complexity, from single-algorithm to multi-algorithm, and from code-driven to data-driven generation.

**Rogue (1980)** established the paradigm: divide a screen into a grid, place random rooms in grid cells, connect them with corridors, populate with monsters and items. One algorithm, one room shape (rectangle), one difficulty axis (depth). The 3×3 grid constraint was elegant — it eliminated overlap checking entirely — but produced visually uniform levels.

**Hack/NetHack (1982–1987)** introduced **heterogeneity**. By adding special levels with fixed layouts, special room types (shops, temples, zoos), and maze-generation algorithms, NetHack broke the assumption that all floors should use the same generator. The special level description language (later Lua scripting) was the first significant data-driven dungeon content system in the roguelike tradition.

**Angband (1990)** scaled generation to **multi-screen levels** and introduced the **vault template system** — hand-designed room layouts inserted into procedural dungeons. This was the first clear articulation of the "hybrid" principle: procedural structure with handcrafted content pockets. Angband also pioneered configurable room types (pits, nests, inner rooms) that varied the procedural generation vocabulary.

**ADOM (1994)** added **overworld geography** and region-specific generation, linking multiple dungeons with different themes. This was an early form of the biome-indexed generation that Tangledeep would later refine.

**Dungeon Crawl Stone Soup (1997–present)** formalized **branch-specific generators**, using different algorithms for different dungeon branches. The Lair uses organic cave generation while the Vaults use geometric layouts. This direct coupling of algorithm to region identity is Tangledeep's most important ancestor.

**Brogue (2009)** demonstrated that procedural generation could produce levels that feel **designer-crafted**. Brian Walker's room accretion algorithm, cellular automata caves, loop insertion based on pathing distance, and "machine" puzzle systems showed that careful algorithm design and parameterization could replace handcrafting for most levels. Brogue proved that the gap between "procedural" and "designed" was a matter of algorithm sophistication, not an inherent limitation.

**Tangledeep (2018)** synthesizes these innovations into a **data-driven multi-algorithm pipeline**. Its ~12 layout generators, XML-parameterized floors, biome-indexed algorithm selection, handcrafted boss rooms, and seed-based determinism represent the modern state of the art for floor-by-floor roguelike generation. It is not the most technically novel system (Caves of Qud's Wave Function Collapse and Cogmind's constrained cellular automata push further), but it is a thorough, well-tuned embodiment of the lineage's accumulated wisdom.

---

## I. Technique family identification

| Technique Family | Likely Used? | Confidence | Evidence |
|-----------------|-------------|------------|----------|
| **Rectangular room placement** | Yes | High | Developer describes "narrow rooms connected by hallways"; Old Amber Station has "corridors and small tight rooms" |
| **Corridor connection graphs** | Yes | High | Rooms must be connected; patch notes reveal connectivity bugs (stairs encased in stone, rooms with no entrance) |
| **Cellular automata / organic caves** | Likely | Medium | Developer describes "cave-like" generators; "cave rooms" map code referenced in patches; Fungal Caves and Cedar Caverns have organic feel |
| **Maze generation** | Yes | High | Developer describes "maze-like" generators; Ancient Ruins and Spiny Maze explicitly described as maze layouts |
| **Secret room insertion** | Yes | High | Wiki confirms secret areas behind destructible walls with exclusive merchants |
| **Special-room templates** | Yes | Confirmed | `SPECIAL` LayoutType with `SpecialRoomTemplate` parameter; text-based room templates documented |
| **Region-specific parameter tuning** | Yes | Confirmed | Each floor has independent Size, Tileset, ChallengeValue, LayoutType, SpawnTable in XML |
| **Floor furnishing passes** | Yes | Confirmed | Monster spawning, object placement, Pandora's Box insertion are separate from layout generation |
| **Validation / cleanup passes** | Yes | Confirmed | Patch notes describe fixes for enclosed stairs, inaccessible rooms, overlapping objects |
| **Weighted spawn distribution** | Yes | Confirmed | spawntables.xml with per-floor monster tables; loot parameters (BonusMagicChance, etc.) |
| **BSP (Binary Space Partitioning)** | Possible | Low | No direct evidence; the room-and-corridor styles could use BSP or simpler grid-based placement |
| **Wave Function Collapse** | Unlikely | Low | No evidence; game predates widespread WFC adoption in roguelikes |
| **Drunkard's walk** | Possible | Low | Could underlie some cave-like generators; no direct evidence |

---

## J. Algorithmic reconstruction

The following pseudo-code reconstructs the likely Tangledeep floor generation pipeline. Confidence levels are marked per block.

### Block 1: Dungeon initialization (at character creation)
**Confidence: Mostly confirmed**

```
function GenerateFullDungeon(seed):
    InitializeRNG(seed)
    floorDefinitions = LoadXML("mapgenerator.xml")
    
    for each floorDef in floorDefinitions:
        floor = new Floor(floorDef.Level)
        floor.grid = new TileGrid(floorDef.Size, floorDef.Size)
        floor.tileset = floorDef.Tileset
        floor.challengeValue = floorDef.ChallengeValue
        floor.spawnTable = LoadSpawnTable(floorDef.SpawnTable)
        
        GenerateFloor(floor, floorDef)
        RegisterStairConnections(floor, floorDef)
        world.AddFloor(floor)
    
    AssignHiddenLocationsToMainFloors(world)
```

### Block 2: Spatial layout generation
**Confidence: Partial reconstruction**

```
function GenerateFloor(floor, floorDef):
    switch floorDef.LayoutType:
        case ROOMS_HALLWAYS:
            GenerateRoomAndCorridor(floor)
        case MAZE:
            GenerateMaze(floor)
        case WIDE_OPEN:
            GenerateOpenLayout(floor)
        case CAVE:
            GenerateCaveLayout(floor)      // likely cellular automata
        case VOLCANO:
            GenerateVolcanoLayout(floor)
        case SPECIAL:
            LoadRoomTemplate(floor, floorDef.SpecialRoomTemplate)
        // ... approximately 6-7 more generator types
    
    ApplyTerrainFeatures(floor, floorDef.Tileset)
```

### Block 3: Pathing and connectivity
**Confidence: Partial reconstruction**

```
function EnsureConnectivity(floor):
    regions = FloodFillFindRegions(floor)
    if regions.count > 1:
        // Connect disconnected regions
        for each pair of adjacent regions:
            DrillCorridor(region1, region2)
    
    // Validate stairs are reachable
    if not IsReachable(floor.stairsUp, floor.stairsDown):
        CarvePathBetween(floor.stairsUp, floor.stairsDown)
```

### Block 4: Special/secret/branch content
**Confidence: Partial reconstruction**

```
function PlaceSecretAreas(floor, floorDef):
    if not floorDef.UnbreakableWalls:
        numSecrets = DetermineSecretCount(floor)
        for i in range(numSecrets):
            wall = FindSuitableWallForSecret(floor)
            secretRoom = CarveSecretRoom(floor, wall)
            if RNG.chance(merchantChance):
                PlaceMerchant(secretRoom)
            else:
                PlaceTreasure(secretRoom)
            MarkWallAsDestructible(wall)
```

### Block 5: Object and enemy furnishing
**Confidence: Mostly confirmed**

```
function FurnishFloor(floor, floorDef):
    // Monster spawning with lax factor
    targetDensity = CalculateMonsterDensity(floor)
    attempts = 0
    while spawnedMonsters < targetDensity and attempts < maxAttempts:
        monster = floorDef.spawnTable.WeightedSelect()
        position = FindValidSpawnPosition(floor)
        if position != null:
            SpawnMonster(floor, monster, position)
        else if attempts > laxThreshold:
            position = FindLessIdealPosition(floor)  // "lax factor"
            SpawnMonster(floor, monster, position)
        attempts++
    
    // Object placement
    PlaceMapObjects(floor)  // from mapobjects.xml, AutoSpawn entries
    if not floorDef.NoSpawner and floor.depth >= 3:
        PlacePandorasBox(floor)
    PlaceFountains(floor)
    PlaceLootChests(floor, floorDef.lootParams)
```

### Block 6: Validation and finalization
**Confidence: Strong inference**

```
function ValidateFloor(floor):
    // Check stairs accessibility
    if IsEncasedInStone(floor.stairsUp) or IsEncasedInStone(floor.stairsDown):
        ClearAroundStairs(floor)  // Patch 1.4 fix
    
    // Check all rooms have entrances
    for each room in floor.rooms:
        if not HasEntrance(room):
            CarveEntrance(room)  // "cave rooms" bug fix
    
    // Check no entities in walls
    for each entity in floor.entities:
        if IsInsideWall(entity.position):
            RelocateEntity(entity)  // "monsters enclosed in rock" fix
    
    // Check object overlap
    RemoveOverlappingObjects(floor)  // "crates overlapping planks" fix
    
    ApplyAutotiling(floor)  // Thousands of connection sprites
```

---

## K. Worked example: generating Cedar Caverns 3F

**Step 1: Context determination.** The player created a character with seed 7175. The system is generating all dungeon floors sequentially. Floor 3 of Cedar Caverns is being generated.

**Step 2: Parameter loading.** The system reads Floor 3's entry from `mapgenerator.xml`: LayoutType is a rooms-and-corridors variant (inferred from Cedar Caverns' visual appearance), Size is moderate (perhaps 30–35), Tileset is LUSHGREEN or EARTH, ChallengeValue is approximately 1.1, and SpawnTable references early-game monsters (moss jellies, forest spirits, basic plant creatures).

**Step 3: Layout generation.** The rooms-and-corridors generator creates 6–10 rectangular rooms of varying sizes within the grid. Rooms are connected by corridors, creating an explorable network. Several rooms are larger, suggesting gathering areas; others are narrow, creating tactical choke points. The generator places ground tiles for rooms and corridors, wall tiles for boundaries.

**Step 4: Terrain features.** Two or three bodies of water are placed within or adjacent to rooms, consistent with Cedar Caverns' described "large and small" water pools. Patches of flowers and grass overlay the ground tiles. Occasional dirt paths connect areas.

**Step 5: Stair placement.** Up-stairs connecting to Cedar Caverns 2F are placed in one room; down-stairs connecting to Cedar Caverns 4F are placed in a distant room, encouraging full floor exploration.

**Step 6: Hidden location.** The system checks whether any hidden locations have been assigned to Floor 3 during the dungeon initialization pass. If so, an additional stairway to the side area is placed in a room.

**Step 7: Secret areas.** One or two walls adjacent to rooms are marked as destructible. Behind them, small secret rooms are carved containing a treasure chest or an exclusive merchant.

**Step 8: Monster population.** Using the Cedar Caverns spawn table, 8–12 monsters are placed in valid floor positions across the map. The spawn table favors "weak, relatively passive" creatures appropriate for early floors. The lax factor is unlikely to activate since early floors tend to have ample room.

**Step 9: Object placement.** A Pandora's Box is placed (Floor 3 is the first floor eligible). Fountains for flask refills are placed in 1–2 rooms. Several crates and barrels containing minor items are scattered across the floor.

**Step 10: Validation.** The system verifies stairs are accessible, all rooms have entrances, no monsters are inside walls, and no objects overlap. Autotiling renders the thousands of connection sprites for walls, water edges, and terrain transitions.

**What makes this floor "Tangledeep-like":** The SNES-era visual aesthetic with lush green tileset and autotiled water creates a warm, inviting atmosphere distinct from the grey-stone aesthetic of traditional roguelikes. The floor feels curated rather than random because the layout generator, terrain features, monster pool, and visual theme are all coordinated through the XML configuration. The presence of a Pandora's Box creates a meaningful risk/reward decision (opening it increases global monster difficulty). The potential hidden location and secret areas add discovery-driven exploration beyond simply finding the exit stairs.

---

## L. Comparative analysis of floor-generation styles

### Rogue: the elegant constraint

Rogue's 3×3 grid algorithm is a masterpiece of constraint-driven design. By dividing the screen into nine cells and placing at most one room per cell, it eliminates overlap checking entirely. The spanning-tree connectivity algorithm guarantees reachability with minimal corridors, while optional extra connections add tactical variety. The weakness is uniformity: every level has the same grid feel, the same rectangular rooms, and the same overall density. Rogue optimized for reliability and speed on 1980 hardware, accepting visual monotony as the cost.

### Brogue: designed randomness

Brogue's room accretion algorithm represents the most sophisticated single-algorithm approach. By generating rooms in a "hyperspace" buffer and sliding them until they fit against the existing dungeon, it produces organic clustering without overlap. The four room types (cross rooms, cellular automata blobs, circles, multi-circles) create visual variety within a unified system. The **loop insertion pass** — only opening walls between rooms that are far apart in the room tree — is a brilliant heuristic for creating tactical routing without making everything trivially accessible. Lakes generated via cellular automata with flood-fill connectivity validation demonstrate how post-processing can add visual richness without breaking playability. Brogue proves that one well-designed algorithm can outperform multiple simpler ones.

### NetHack: the heterogeneous tradition

NetHack's generation system is fundamentally eclectic. Regular levels use room-and-corridor placement with 13+ special room types (shops, temples, zoos, beehives, barracks). Maze levels use recursive backtracking. Special levels use a scripting language (originally a custom DSL compiled by `lev_comp`, now Lua in 3.7+) for fixed or semi-fixed layouts. This heterogeneity creates strong pacing — the player never knows whether the next level will be a standard dungeon floor, a maze, or the carefully designed Castle with its guaranteed Wand of Wishing. NetHack's innovation is that **some floors deserve to be handcrafted**, a principle Tangledeep directly inherits.

### Angband: scale and templates

Angband operates at a larger scale than its peers, with multi-screen levels on a grid up to 256×256. Its block-based room placement (11×11 blocks, 33×11 room footprints) produces spacious levels with diverse room types. The **vault template system** — hand-designed room layouts in text files — allows designers to create memorable encounters within procedural dungeons. Monster pits and nests create concentrated themed encounters. Modern Angband 4.x adds five generation profiles (classic, modified, moria, labyrinth, cavern) selected per level, making it the closest classic roguelike ancestor to Tangledeep's multi-generator approach. The key difference is that Angband's levels are **non-persistent** (regenerated each visit), while Tangledeep pre-generates and persists all floors.

### Tangledeep: the data-driven synthesis

Tangledeep's contribution to the design space is the **complete externalization of floor identity into data**. Where Angband hard-codes five profiles and NetHack embeds special-level logic in compiled scripts, Tangledeep defines each floor's algorithm, parameters, visual theme, monster roster, and connectivity in XML. This makes the generation system simultaneously more transparent (designers can modify floor behavior by editing XML) and more granular (every individual floor can be independently tuned). The ~12 layout generators provide the spatial vocabulary; the XML configuration layer provides the compositional grammar; and the furnishing passes (monsters, items, objects) provide the content. The result is a floor-generation system that is less technically ambitious than Brogue's single-algorithm elegance but more practically flexible than any of its predecessors.

---

## M. Open questions and unresolved gaps

**Exact layout algorithm implementations.** The ~12 LayoutType enum values are not publicly documented. We know "rooms-and-corridors," "maze-like," "wide-open," "cave-like," "Volcano," and "SPECIAL," but the remaining 6–7 types are unconfirmed. The source code exists on GitHub (github.com/aaversa/tangledeep) but individual files could not be accessed during this research.

**Specific algorithm families per generator.** Whether the cave-like generator uses cellular automata, a drunkard's walk, or another approach is inferred but not confirmed. Whether the rooms-and-corridors generator uses BSP, grid-based placement, or random placement with retry logic is unknown.

**Room template usage in procedural floors.** It is unclear whether any procedural (non-SPECIAL) layout types incorporate pre-designed room templates or sub-layouts, or whether templates are used exclusively for SPECIAL floors. Angband and NetHack both insert templates into procedural levels; Tangledeep may do the same.

**How seeds interact with the multi-floor pipeline.** Whether each floor consumes a deterministic portion of the RNG sequence, or whether the seed generates per-floor sub-seeds, is unconfirmed. The pre-generation architecture suggests sequential consumption, but runtime generation of Item Dreams may use a separate RNG stream.

**The full mapgenerator.xml structure.** The modding documentation provides an example XML entry for Casino 2F, but the complete file — with entries for all ~100+ floors — has not been publicly analyzed. The reference XML was previously hosted at impactgameworks.com/ReferenceXML/ but is no longer publicly browsable.

**How deterministic the full dungeon really is.** Seeds clearly determine layout and initial population, but whether monster respawning, traveling merchant rotations, and Item Dream generation are also seed-determined is uncertain. The interaction between the world seed and runtime RNG (e.g., combat outcomes, drop rolls) likely introduces divergence from a purely deterministic run.

**Generation order for furnishing passes.** Whether monsters are placed before or after objects, whether secret rooms are carved before or after terrain features, and the exact ordering of the ~5 post-layout passes is inferred from patch notes (which reveal specific bugs in specific passes) but not directly confirmed.

**Influence of Dungeonmans.** The developer was taught Unity basics by Jim Shepard, creator of Dungeonmans (another roguelike). Whether Tangledeep's generation architecture was directly influenced by or adapted from Dungeonmans code is plausible but unconfirmed.

---

## Best current reconstruction

### The Tangledeep floor-generation pipeline

1. **At character creation**, initialize master RNG from world seed
2. **For each floor** defined in `mapgenerator.xml`:
   - Read parameters: LayoutType, Size, Tileset, ChallengeValue, SpawnTable, stair targets
   - **Select and execute** one of ~12 layout generators based on LayoutType (or load handcrafted template for SPECIAL)
   - **Apply terrain** features (water, lava, mud, electric) per biome theme
   - **Place stairs** connecting to specified floor IDs
   - **Carve secret rooms** behind destructible walls (unless UnbreakableWalls)
   - **Spawn monsters** from weighted spawn table with lax-factor fallback
   - **Place objects**: Pandora's Box, fountains, chests, crates, map objects
   - **Validate**: connectivity, stair accessibility, entity placement legality, object overlap
   - **Apply autotiling**: render thousands of connection sprites for visual coherence
3. **Assign hidden locations** to main floors within configured floor ranges
4. **Serialize** the complete dungeon state to the save file

### Tangledeep's place in the roguelike generation lineage

Tangledeep sits at the convergence of four major innovations from the roguelike tradition: **Rogue's room-and-corridor spatial grammar**, **NetHack's hybrid procedural-handcrafted floor design**, **Angband's template and profile system**, and **Brogue's aspiration toward designed-feeling procedural output**. Its distinctive contribution is the **full XML externalization of floor identity** combined with **biome-indexed algorithm selection from a library of ~12 generators**. This makes it a representative modern endpoint of the roguelike dungeon-generation lineage — not the most technically novel (Caves of Qud and Cogmind push further), but one of the most systematically data-driven and designer-accessible implementations of the tradition's accumulated principles.