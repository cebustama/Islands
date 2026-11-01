# Playlist «PCG» — Tier 1 technique cards

23 videos from group A of [`../2026-11-01-playlist-pcg.md`](../2026-11-01-playlist-pcg.md), analysed on 2026-11-01. Research material, not authority.

**How they were made:** transcripts fetched with `youtube-transcript-api` (auto or author captions, not saved in the repo); five Sonnet subagents wrote one card each from the transcript, checking Islands status with quick greps in `Runtime/`. The infinite-city video has no subtitles: its card comes from the author's blog post. The orchestrating agent re-checked the key status claims in code (see `crossref.md`). Cards are own summaries; minute marks refer to the video. Speaker names marked as inferred are not stated in the transcript.

Parts: 1 Qud, Edwards · 2 RimWorld, Nature of Order, Betts · 3 Minecraft ×2, XCOM 2, floating origin · 4 Galak-Z, Wolverson, Noita ×2, DF villains · 5 Nystrom, Gun Game, Isaac, Nuclear Throne, UnitOfTime, stitched WFC, Two Minute Papers ×2 · 6 infinite WFC city.


---

## Part 1

### End-to-End Procedural Generation in Caves of Qud — Jason Grinblat & Brian Bucklew, Freehold Games (GDC) (1:04:21)
- **URL:** https://youtu.be/jV-DZqdKlnE
- **Core idea:** A walkthrough of how Qud chains small, well-known generators into one large pipeline that builds a village. The pipeline goes from abstract data (history as key/value strings) to concrete game objects in five steps, and each step takes the previous step's output plus pre-baked design data. The talk is about the "plumbing" between modules, not the modules themselves.
- **Techniques:**
  - Abstract-to-concrete multi-stage pipeline ("abstraction mountain") — NEW — five steps: generate history, resolve neighbors (both at world-gen), generate culture, generate architecture, fabricate game objects (both at village entry). Early stages stay abstract; later stages consume them; if no abstract input exists, a default or a random roll is used, and player apophenia covers the gaps (5:00-8:00, 47:00-49:00).
  - Two-phase timing — NEW (architecture pattern) — bake world-level data at new-game, fabricate each village only when the player enters its map tile (10:00-13:00). This is on-demand per-zone generation.
  - Village history as a bag of key/value strings — NEW — name, base faction, palette, government, sacred/profane things, signature items and dishes. Produced by a small state machine over historic-event templates. Events are generated "backwards": pick how the event ends, then invent the rationalization (20:00-22:00). Details are in a separate talk; this one is vague on the rules.
  - Reification / "resolve neighbors" — NEW — abstract tags like "worships a legendary creature" are linked to a real nearby object once the village has a location on the (mostly static) world map. A faction is also created per village and injected into the static faction set (23:00-27:00). Location choice uses region tags (a goat cult goes to mountains).
  - Population tables (weighted tables with nesting) — NEW — XML tables with relative weights, a "pick one" or "pick each" style, nested sub-tables, per-entry counts/chances ("1-2 savages, 50% seller") and consumer-specific hints such as "inside corner" or "within radius 2 of the door" (30:00-35:00). Design-time data, ignored by consumers that do not understand the hints.
  - Specialization fall-through — NEW — a decision like building style rolls on a faction table first, then a region table, then a default table (38:00-41:00). Only important factions/regions get their own table.
  - Replacement grammar over a hand-authored graph of cultural relationships — NEW — the same graph serves text generation (dish names, proverbs, quest text) and "which culture traits sit near which" (18:00, 36:00-38:00, 47:00-51:00).
  - Dijkstra-map region seeding for building placement — Voronoi partitioning (D.15) related, NEW variant — seed a Dijkstra (distance) map, seed more points in unfilled areas, keep adding seeds at local maxima until the largest region is under a size limit set by building style; then take the largest inner rectangle of each region as a building plot; drop WFC or square/round buildings into those rectangles (41:00-43:00).
  - Inside/outside analysis by flood from seeds — NEW small variant of flood fill — drop Dijkstra seeds inside each building, close narrow gaps until spaces are constrained, giving inside vs. outside labels for any building style (43:00-44:00).
  - WFC building templates (D.16) — mentioned only as a plug-in for building interiors (a prior talk covers it).
  - Blueprint/component objects + factory with late mutation — NEW (not a generation technique) — XML blueprints made of parts (render, physics, light source); the factory builds the object, then the generator mutates it just before placement (tattoo on a villager, desecrated statue, history text on a monument) (44:00-47:00).
- **Parameters / numbers:** Example weights 20 vs. 40 (monuments twice as likely as doors); "radius 2" placement hint; torch light radius 6; 50% / 10% spawn chances in an encounter table; three design-time inputs (factions, region map, relationship graph); a code base over 10 years old. No noise parameters (the talk has almost no math).
- **Determinism, seeds, chunks/seams:** The world seed drives every random call, so the same seed gives "mostly" the same world (16:00). A save file can be loaded and one zone rebuilt with a command to replay a village generation in a debugger (58:00-59:00). The whole world state is saved as per-screen chunks with .NET binary serialization, not just the seed (1:00:00-1:02:00). Debug visuals were ASCII dumps plus a noise-map visualizer. Seams between chunks: not discussed.
- **Islands status:**
  - Abstract-to-concrete multi-stage pipeline: **partial** — `Runtime/PCG/Layout/Maps/MapPipelineRunner2D.cs` and `IMapStage2D.cs` with stages in `Layout/Maps/Stages/` (BaseTerrain, Biome, Hydrology, Regions...) give a staged pipeline over mask/field layers; missing: abstract non-grid data (history, culture, tags) flowing between stages and an on-demand "fabricate on entry" phase.
  - Region seeding by distance map: **partial** — `Runtime/PCG/Operators/MaskMorphologyOps2D.cs` has `BfsDistanceField` (BFS distance from seeds); missing: iterative seeding at local maxima with a size cap, and largest-inner-rectangle extraction (no hits in Runtime).
  - Inside/outside by flood: **partial** — `Operators/MaskFloodFillOps2D.cs` and `MaskTopologyOps2D.LabelConnectedComponents4` exist, plus morphology ops; the "close narrow spaces" recipe is not packaged.
  - Population tables with weights/nesting/hints: **missing** — no weighted-table code in Runtime; closest are `Layout/Maps/BiomeTable.cs` (biome lookup) and `RegionNameTableAsset.cs` / `Stage_Regions2D.cs` (hash(seed, biome, anchor) into a name pool).
  - History state machine, resolve-neighbors linking, culture graph: **missing** (`Runtime/Graphs/GraphLibrary/AbstractGraph.cs` is a possible base for a relationship graph).
  - WFC (D.16): not checked here (covered by the technique library).
  - Blueprint/parts objects: **n/a** (gameplay data, not generation).
  - Already covered elsewhere: `2026-10-27-referencias-desde-marcadores.md` lists the Qud WFC talk and Grinblat's *Procedural History*, but not this talk; this card ADDS the pipeline plumbing, population tables and distance-map plot placement.
- **Where it could land in Islands:** (1) a small `WeightedTable` with "pick one / pick each", nesting and chances, plus a seed-hash roll helper, in `Runtime/PCG` (core, no Unity, no `UnityEngine.Random`). (2) A later settlement stage after `Stage_Regions2D`: seed spreading with BFS distance maps, then largest-rect extraction, as a mask op. (3) mundo-islas: the history -> resolve neighbors -> culture -> fabricate flow as a design for villages, sharing a graph with the text generator. (4) A web Islands Lab part on distance-map plot placement is conservative and doable.
- **Value for Islands:** high — the staged abstract-to-concrete design, population tables and the plot-placement recipe map directly onto Claudio's layered, deterministic goals and onto the colonization game's village needs. Nothing for noise.

### A Geographer's Guide to Building Game Worlds — Kate Edwards, GDC (1:05:23)
- **URL:** https://youtu.be/EVGonAUUQ8c
- **Core idea:** A design/culturalization talk, not an algorithm talk. A geographer who ran geopolitical strategy at Microsoft argues for "realization over realism": give the world just the detail the narrative and experience need, keep it logically consistent, and avoid cultural mistakes. It offers a "recipe" and a layered view of worlds borrowed from GIS.
- **Techniques:**
  - Layered world model (GIS-style layers) — NEW (conceptual, close to Islands' `MapLayerId`/`MapFieldId` design) — split a world into layers (geomorphology, climate, demographics, history, religion, language...) and pick only the ones the game needs (39:00-43:00). Layers are listed, not detailed.
  - Recipe for realizing a world — NEW (design checklist) — suggestion of familiarity + hint of complex systems + dash of cultural evidence + big chunk of logical consistency + pinch of topology (9:00-12:00).
  - Logical consistency — NEW (design rule) — rules must hold across space, time and content; no contradictions or conflicts between content elements (e.g., a cold-climate culture in a hot desert needs an in-world reason) (14:00-18:00).
  - Topology as connectedness (not distance) — NEW (concept) — what links to what: people to land, culture to environment, artifact to lore. Hints like an NPC line or an artifact tying two places make the universe feel larger (18:00-21:00). Closer to a relationship graph than to grid topology.
  - Cultural evidence and "backfilling" — NEW (process) — place the minimum content that signals a culture, with a purpose for each object, and ask "why is this here?" (Jade Empire prayer wheels, wooden-cross grave markers in a fantasy world, a moai that pins a game to Easter Island) (12:00-17:00, 36:00-37:00).
  - Real vs. fictional context checklists — n/a — real-world risks: augmentation, accuracy, interpretation, appropriateness; fictional-world risks: generalization, emulation, allegory, artifacts (27:00-37:00). The rest is examples (Age of Empires patch for Korea, Halo allegory, Hearts of Iron in China).
  - Discoverability — n/a — judge whether players can ever see a content element (24:00-26:00).
- **Parameters / numbers:** None for generation. Anecdotal only (Jade Empire's cultural mix about 80% Chinese, 5% Japanese, 5% Korean). No sizes, ranges or counts.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:**
  - Layered model: **partial** — layers already exist as masks/fields (`Runtime/PCG/Layout/Maps/MapIds2D.cs`, stages in `Layout/Maps/Stages/`); the human-layer side (history, religion, language) is **missing**.
  - Logical-consistency / topology as relationship graph: **missing** as code; `Runtime/Graphs/GraphLibrary/AbstractGraph.cs` is a potential base.
  - The rest: **n/a** (design guidance, not generation).
  - Already covered elsewhere: Edwards appears as a connection in `2026-11-capturas.md` and the playlist note (`2026-11-01-playlist-pcg.md`, "in research"); this card ADDS the layer-selection and consistency checklist.
- **Where it could land in Islands:** not an Islands feature. Best used in mundo-islas / videojuego-colonizacion as a review checklist for generated cultures and artifacts (e.g., a consistency check on generated villages: each object has a stated reason; environment-culture pairs validated against tags). A web article could use the layer idea to explain Islands' layers to non-experts. Keep any code part in the gameplay layer, not core.
- **Value for Islands:** low for the generator code, medium for the world-building and colonization game (a design checklist matching Claudio's interest in culture, colonialism and avoiding stereotypes). The speaker says herself it is only an overview.

#### Part summary
- Most valuable: the Qud abstract-to-concrete pipeline (history -> resolve neighbors -> culture -> architecture -> fabricate) as an architecture for a village/settlement layer after `Stage_Regions2D`, with defaults and randomness filling gaps.
- Population tables (weighted, nested, "pick one/each", chances, consumer-specific hints, faction -> region -> default fall-through) are a small, deterministic, Unity-free addition that Islands lacks (**NEW**).
- Plot placement by distance-map seeding with a size cap and largest-inner-rectangle extraction reuses the existing `BfsDistanceField` and flood-fill ops (**NEW** variant of D.15); inside/outside labelling by flood is nearly supported.
- Qud rebuilds a single zone from a save plus the seed for debugging, a pattern worth copying (per-zone rebuild by seed).
- Edwards' talk adds no algorithms; its value is a layer-selection and logical-consistency checklist for mundo-islas and the colonization game.

---

## Part 2

## Part 2 — Design / narrative talks (RimWorld, Alexander patterns, Betts)

Note: the first two talks are about game and narrative design, not grid generation. Their natural home is **mundo-islas** (gameplay, storyteller, NPC layer), not the Islands.PCG package. The Betts talk is the only one with directly reusable PCG mechanics.

### RimWorld: Contrarian, Ridiculous, and Impossible Game Design Methods — Tynan Sylvester, GDC (1:00:52)
- **URL:** https://youtu.be/VdqhHKjepiE
- **Core idea:** RimWorld is framed as a "story generator", not a game. Design choices follow from that frame: do not reward skill proportionally, make failure elastic (the story continues), keep visuals abstract so the player's pattern-matching fills in emotion and intent. The second half is a production method (idea reservoir, bubble-sort prioritisation) and is not about generation.
- **Techniques:**
  - Story generator with disproportionate pushback — NEW (design principle) — the system may hurt a skilled player on purpose, because a good story needs unexpected pushback (7:00-10:00). The AI storyteller is only named (59:00); the talk gives no algorithm for incident selection.
  - Elastic failure — NEW — failure is a gradient, not game-over (11:00-13:00). Raiders leave once satisfied (a kidnapping or some stolen items) instead of massacring everyone. Low colony wealth lowers colonist mood expectations, so recovery is possible. The player can abandon the base in a caravan. Each stage of failure creates new story material.
  - Stakes on things the player cares about — NEW — the threat targets pets, loved ones and characters, not a "lives" counter (9:00-11:00).
  - Apophenia budget: abstraction plus long-lived relevance — NEW — (a) show no faces or body language so the player imagines them (19:00-20:00); (b) incidents last long (disease, toxic fallout for months, manhunter packs waiting outside for a week, sieges) and colonists keep memories (botched surgery, deaths, marriages), so many circumstances overlap and the player builds a narrative (21:00-22:00). The numeric memory model is admitted to be thin; the richness is in the player's reading.
  - "Is it relevant to a character's emotional journey?" as a feature filter — NEW — a decision razor that cuts economy chains, weapon customisation and fluid dynamics (23:00-25:00).
  - Cheat the sim where it costs nothing — NEW (production) — shipped without stockpiles (items vanish into a magic place), without physical traders (a comms console teleports goods), with crop blight that kills plants at random instead of spreading (39:00-41:00).
  - Idea reservoir plus bubble sort — n/a — one to-do doc per developer, one huge ideas doc (about 60 pages), pairwise "which is better" comparison repeated over time, nothing locked (30:00-37:00). Process, not generation.
  - Q&A: leave story recording and interpretation to the player; no achievements, because they would displace the story frame (51:00, 59:00).
- **Parameters / numbers:** ideas doc about 60 pages and 25,000 words; to-do list kept to 1-2 weeks of work; 3-person team. No incident-points or timing numbers are given.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** every technique here is **missing** in Islands.PCG, and all are gameplay-layer, so they would not belong in the package. `Documentation~/research/Games Worldgen/RimWorld Worldgen.md` covers RimWorld's world generation (biomes, factions); a quick grep of Documentation~ for storyteller/incident/pacing found no card for the storyteller, so this adds the design-philosophy side. `Documentation~/research/2026-11-capturas.md` and `2026-11-01-playlist-pcg.md` mention related names; I did not read them.
- **Where it could land in Islands:** **mundo-islas**, not the package. (1) A narrator layer with elastic-failure rules: raids that retreat once they have taken something, and recovery mood bonuses. This fits the planned storyteller and the Buffer Souls NPC memory (long-lived overlapping memories such as "botched my surgery"). (2) The "emotional journey" razor as a scope filter for the game's systems. (3) Abstract visuals that leave room for interpretation suit the colonisation game. The package would only need to expose neutral data (settlements, resources), nothing narrative.
- **Value for Islands:** low for the package; medium-high for mundo-islas and the colonisation game design (storyteller and failure design).

### The Nature of Order in Game Narrative — GDC Narrative Summit (1:02:44)
- **URL:** https://youtu.be/E-qnXNUSUMA
- **Core idea:** The speaker applies the 15 fundamental properties from Christopher Alexander's *The Nature of Order* as a design checklist for games and narrative. Each property is illustrated with a game example; there are no algorithms. Speaker attribution: he mentions "my 23rd GDC" and his "lenses" book, so it is almost certainly Jesse Schell, but the transcript never names him.
- **Techniques (all NEW as design heuristics, none are algorithms):**
  - Levels of scale (19:00-22:00) — structure should be good at big, medium and small scales at once: plot / characters / dialogue; Pac-Man dots / ghosts / board.
  - Strong centers (22:00-26:00) — find the centres of your game; PUBG's shrinking circle is a hidden centre; choose-your-own-adventure is said to fail for lack of one.
  - Boundaries (26:00-28:00) — thresholds in the hero's journey; two systems with conflicting boundaries create drama (Bioshock little sisters, Papers Please).
  - Alternating repetition (28:00-31:00) — tension then release as a wave, not a ramp (Halo islands / river; cutscene / level "string of pearls").
  - Positive space (31:00-34:00) — in narrative, dialogue vs silence (Oxenfree lets you stay silent or interrupt).
  - Good shape (34:00-37:00) — shape in time: Telltale's Walking Dead gives dialogue choices at a steady rhythm and shows a "will remember that" notice only when a choice matters.
  - Local symmetries (36:00-38:00) — no global symmetry, many small symmetric units glued together (Alhambra, original Zelda map). He admits he does not know the narrative analogue.
  - Deep interlock (38:00-41:00) — player story and designer story must fit (Planetfall's Floyd, Brothers: A Tale of Two Sons).
  - Contrast (41:00-43:00), gradients (43:00-44:00; One Hour One Life ageing), roughness (44:00-48:00; imperfection, dirt), echoes (48:00-52:00; Cuphead's "jumping into danger" in story and mechanic), the void (52:00-55:00; empty space marks where important things happen, e.g. boss rooms), simplicity (55:00-59:00; one object serves many purposes, Pac-Man dots), not-separateness (59:00-61:00).
  - Process principle (11:00-14:00) — Alexander's "timeless way": build, observe how people use it, then correct. Example: leave grass unpaved, then pave where people walked (desire paths).
- **Parameters / numbers:** 15 properties; 253 patterns in *A Pattern Language*. No generator numbers.
- **Determinism, seeds, chunks/seams:** not discussed. A closing remark (62:00) suggests AI could use these 15 patterns; unsupported.
- **Islands status:** all **n/a** as generation techniques. Loose overlaps (my interpretation, not claims of the talk): levels of scale resembles the fBm octave design in `Runtime/Noise`; local symmetries resembles template composition in the layout generators (`Runtime/PCG/Layout`).
- **Where it could land in Islands:** mostly a **review checklist** for generated output, not code. Conservative suggestions: levels of scale (a map should read at continent, region and tile scale), the void / strong centre (leave a deliberate empty space or hub in layouts), desire paths (derive roads from where agents walk, in mundo-islas), alternating repetition for pacing in the narrator. None is in the talk as an algorithm.
- **Value for Islands:** low-medium. Useful vocabulary for judging "does this generated place feel alive" and for narrative pacing; nothing to implement directly.

### FDG2020 Keynote: Tom Betts on Procedural Content Generation as Art, Design and Play — Tom Betts (20:06)
- **URL:** https://youtu.be/-kWhjZ5LhIw
- **Core idea:** PCG as an expressive artistic tool with embedded aesthetics and intent, not only a problem-solver. Betts shows his games (You Are Hunted, In Ruins, Into the Pit) and a concrete dungeon pipeline in which the player edits generator parameters inside the fiction (mixing ritual ingredients), within ranges that always stay playable.
- **Techniques:**
  - Rooms by packing or subdivision, then cost-weighted A* between key rooms — partly D.13 (BSP dungeon), plus NEW (A* over rooms with random traversal cost) (13:00-14:00) — fill the area with rectangles, mark some as destinations, give each room a semi-random traversal cost, run A* between destinations; rooms off the path are closed or left open depending on parameters; redundant exits are pruned.
  - Abstract room templates plus weighted prop tables — NEW (templates are close to D.16 ideas, but it is not WFC) (14:00-15:00) — templates say where walls could be but not their type; each tile picks a prop from a weighted probability array (a "foliage" tile may become any of 10 trees).
  - Bitmasked walls with weighted variants — D.18 autotiling (15:00) — a prop set per wall-neighbour combination, each drawing from a weighted variant list.
  - Credit-pool encounter budget — NEW (15:00-16:00) — enemies are in tiered sets with a cost, and each room "pays" from a credit pool; loot uses tables.
  - Biome mixing with thresholded simplex noise — D.5 + D.20 variant; NEW as "biome-set blending inside a room" (16:00-17:00) — a thresholded simplex cloud over the dungeon decides per tile which biome set (stone castle, petrified forest, corrupted docks) supplies tiles, lighting, AI, loot and sound; mixing happens inside a single room, and the generative soundtrack layers audio from the active sets.
  - Player-exposed generation parameters within safe ranges — NEW (design pattern) (17:00-19:00) — ingredients change biome mix, difficulty, monster population, rarity, elite count and rng ranges (e.g. more chests and more traps together). The range grows as the player progresses.
  - Earlier projects: Voronoi patchwork "quilt" islands (You Are Hunted, 3:00-4:00); pathfinding to place roads and key locations that feel intentional (The Light Keeps Us Safe, 6:00-7:00); PCG as a design-time tool to scout generated landscapes (7:00).
- **Parameters / numbers:** few: 10 tree variants per foliage tile (example); no numeric ranges for the ritual parameters. The talk is explicitly not deeply technical.
- **Determinism, seeds, chunks/seams:** not discussed. He says parameters are limited to ranges that still give playable output, and that the early You Are Hunted generator had "lots of edge cases".
- **Islands status:**
  - Room packing / subdivision: **exists** (`Runtime/PCG/Layout/Bsp`, `RoomFirstBspDungeon2D.cs`, `RoomGridDungeon2D.cs`, `RoomsCorridorsComposer2D.cs`).
  - Cost-weighted A* between destination rooms: **missing** (grep for astar/pathfind in `Runtime/PCG` finds nothing; `Runtime/Graphs/GraphLibrary/AbstractGraph.cs` matched a looser search and may hold graph code, not checked).
  - Bitmask autotiling: **partial / unverified** (`Layout/MaskNeighborOps2D.cs` handles neighbours; no bitmask-to-prop table found by grep).
  - Weighted prop tables: **missing** in the core (the Tilemap adapter in `Runtime/PCG/Adapters/Tilemap` was not checked for weighted choice).
  - Credit-pool budget: **missing**.
  - Biome mixing by thresholded noise: **partial** (`Layout/Maps/BiomeTable.cs` and the climate / biome stages exist; blending two whole biome sets per tile not confirmed).
  - Player-facing parameter ranges: **partial** (`Layout/Maps/MapTunables2D.cs` and `Samples/Presets/MapGenerationPreset.cs` hold tunables and presets; enforcement of "safe expressive range" not checked).
- **Where it could land in Islands:**
  - Pipeline stage: a weighted prop / variant table plus credit-pool placement as a post-biome "scatter" stage (props, resources, encounters), kept deterministic by hashing cell and seed with SmallXXHash.
  - Layout: optional A* between destination rooms with per-room random cost in `RoomsCorridorsComposer2D`, so corridors skip some rooms and look intentional.
  - NoiseLab / web Islands Lab: a short page on "threshold a noise field to choose between two tile sets" is a small, clear demo that fits the existing noise lessons.
  - mundo-islas: the in-fiction "ritual" edit of generator inputs within a safe range could become a gameplay mechanic.
- **Value for Islands:** medium. Nothing algorithmically new, but the credit-pool budget, cost-weighted A* between landmarks and the expressive-range idea are concrete, cheap additions.

#### Part summary
- None of these talks adds a heavy algorithm. The two design talks (Sylvester, the Alexander-patterns talk) belong to **mundo-islas / the colonisation game**, not the package.
- Most useful for mundo-islas: Sylvester's elastic failure (raiders leave once satisfied, recovery mood buffs, flee option), long-lived overlapping memories as raw material for Buffer Souls and the narrator, and the "emotional journey" razor as a scope filter.
- Most useful for the package, from Betts: a credit-pool encounter budget, weighted prop tables per tile or bitmask case, cost-weighted A* between destination rooms, and thresholded-noise blending of two biome sets (probably a small extension of the existing biome stages and a good NoiseLab demo).
- NEW techniques (outside D.1-D.20): credit-pool budgeting, weighted prop tables, A* with per-room random costs, in-fiction parameter editing within safe ranges; plus design heuristics (elastic failure, apophenia budget, Alexander's 15 properties).
- Speaker attribution for the Alexander talk is inferred (Jesse Schell); the transcript does not name him.

---

## Part 3

### Reinventing Minecraft world generation — Henrik Kniberg, Jfokus (49:41)
- **URL:** https://youtu.be/ob3VwY4JyzE
- **Core idea:** A tour of how Minecraft 1.18 builds terrain: smooth noise is not used directly as height but passed through designer-editable spline points ("terrain shaping") driven by three named noises, then a 3D density field with a vertical squash bias adds overhangs, and biomes are picked from lookup tables over five noises. Compared with the existing `Minecraft Worldgen.md`, it adds the *why* and the designer-facing mental model (named noises, soft coupling, table-based biomes), plus Q&A design trade-offs.
- **Techniques:**
  - Noise-to-height spline mapping (terrain shaping) — NEW (related to D.10) — name a noise ("continentalness", -1..1), place control points (noise value -> height) and fit a curve. Flat segments make plateaus, a steep segment (0.3 to 0.4 jumping 100 -> 150) makes cliffs; low values drop below sea level and so create oceans (19:00–22:00).
  - Chained / nested splines — NEW — each point of the continentalness spline can lead to another spline on erosion, then on peaks-and-valleys (22:00–23:00). Rule of thumb: high erosion = flat regardless; low erosion + high continentalness + high PV = steep ridges. The talk is vague on the exact wiring.
  - Three terrain-shaping noises (continentalness, erosion, peaks-and-valleys), each with its own octave/amplitude config — NEW as a combination (21:00).
  - Octaves/fBm — D.7 — each octave doubles frequency, halves amplitude; shown with sine curves first (15:00–16:00).
  - 3D density + height bias — NEW (D.19 covers only surface extraction) — density = 3D Perlin in [-1,1]; positive = stone, <= 0 = air; a bias lowers density with height around a centre (y=100). Two parameters: squashing factor (high = flat, low = fractured/overhangs) and height offset (moves terrain up/down). Both are spline-driven by the three noises (26:00–29:00).
  - Cheese caves — NEW (thresholded 3D noise; adjacent to D.12) — noise above a threshold = air gives disconnected big chambers (30:00).
  - Spaghetti/noodle caves via noise ridging — NEW (adjacent to D.8) — air where |noise| < constant, i.e. the zero-crossing shell of a field; a thinner constant gives "noodle" caves (31:00–32:00).
  - Multi-noise biome table — D.20 (extended) — five noises (continentalness, erosion, PV, temperature, humidity); each is quantised into index ranges (erosion 0 = -1..-0.78, erosion 6 = 0.55..1). Tables chain: PV high -> (continentalness, erosion) picks a category (peak / middle) -> temperature/humidity picks the biome; a weirdness noise flips variants (jungle vs bamboo jungle) (34:00–37:00, 40:00).
  - Neighbour-aware table authoring — NEW (design practice) — configure so incompatible biomes (desert next to snowy peaks) never touch; hard because the space is 5-D (37:00).
  - Biome as a thin layer over terrain, only soft-coupled — NEW (design principle) — the same terrain repainted as desert/meadow/grove/swamp; mountain biomes are matched to mountain shaping by configuration, not by a hard dependency (32:00–36:00).
  - 2D surface biomes + injected 3D cave biomes — NEW — fully 3D biomes caused oddities (grass tint changing 10 blocks up because of a "jungle in the sky"), so surface biomes are full-height 2D columns and only caves are 3D volumes (40:00–42:00).
  - Ore placement — NEW — per chunk roll how many blobs, try random positions, skip if the spot is air/water (44:00).
- **Parameters / numbers:** world 60M x 60M x 384; 18 quintillion seeds; chunk 16x16 columns; demo base y=100, noise amplitude 20 (80–120); octaves x2 frequency, x0.5 amplitude; erosion index ranges above. No real octave counts are given.
- **Determinism, seeds, chunks/seams:** Seams are only implicit: continuity comes from noise being a pure function of world position, so any chunk order matches. Generated chunks are stored permanently, not regenerated from deltas (42:00). Chunks are full-height columns, which he calls wasteful (46:00). Performance was the reason ML alternatives were not used (39:00).
- **Islands status:**
  - Spline height remap — **partial** — `Runtime/PCG/Fields/ScalarSpline.cs` is a piecewise-linear 1D remap used in `Runtime/PCG/Layout/Maps/Stages/Stage_BaseTerrain2D.cs` (`heightRemapSpline`, N2). Missing: splines keyed by more than one named field, and smooth (cubic) interpolation.
  - Continentalness/erosion/PV fields — **missing** (grep finds "erosion" only in morphology ops, unrelated).
  - fBm / ridged — **exists** (`Runtime/Noise`, D.7/D.8).
  - 3D density with squash bias — **missing** in PCG (`Lattice3D` and `Simplex3D` exist in `Runtime/Noise/Noise/Noise.Lattice.cs` and `Noise.Simplex.cs`, but no PCG stage consumes a 3D field; Islands is grid-2D).
  - Cheese/spaghetti caves — **missing** (2D CA caves D.12 are separate).
  - Biome table — **partial** — `Runtime/PCG/Layout/Maps/BiomeTable.cs` is a 4x4 temperature x moisture grid plus a beach override; no erosion/continentalness axis, no weirdness variant, no neighbour constraints.
  - Ore/feature dice-roll placement — **n/a** to the 2D pipeline (a vegetation stage exists: `Stage_Vegetation2D.cs`).
- **Where it could land in Islands:** (1) A multi-input "terrain shaping" stage in `Layout/Maps/Stages/` after base terrain: a `ScalarSpline` per axis, fed by a continentalness-like field (close to the island shape mask / distance to coast) and an erosion field. (2) Extend `BiomeTable` with an optional third quantised axis and a variant flag, keeping the 4x4 default so goldens stay valid. (3) NoiseLab / web Islands Lab page: a "move the dots, see the terrain" spline editor over a 1D noise slice, the clearest explainer in the talk. (4) Ridged-shell caves only if Islands gets a 3D layer (underground in mundo-islas), not now.
- **Value for Islands:** medium-high — spline shaping is cheap (ScalarSpline exists) and gives designer control over cliffs, plateaus and ocean depth; the 3D parts are low until a 3D layer exists.

### Minecraft terrain generation in a nutshell — Henrik Kniberg (25:49)
- **URL:** https://youtu.be/CSa5O6knuwI
- **Core idea:** The short scripted version of the Jfokus talk: build terrain up from `getBlock(x,y,z)` through random, flat, sine, Perlin, octaves, spline-shaped noise, 3D density, caves and biomes. Almost everything repeats the long talk; the extra value is the clean progression and a few explicit numbers.
- **Techniques:**
  - Terrain as a pure function `getBlock(x,y,z)` — NEW (framing) — chunks fit "no matter which order we generate them" because the function depends only on position (4:00).
  - Staged generation: shape (stone/air) -> water fill -> surface layer -> features/structures (4:00) — NEW as a sequence (Islands already layers similarly).
  - Sine -> Perlin -> octaves (7:00–12:00) — D.4, D.7 — surface = base + noise x amplitude; octave = x2 frequency, x0.5 amplitude.
  - Spline points for noise-to-height (13:00–16:00) — NEW — two points = flat; move points to get cliffs. Example: continentalness -1 -> 50, 0.3 -> 100, 0.4 -> 150, then flat at 150.
  - Three noises (continentalness, erosion, PV), inspectable with F3 (16:00).
  - 3D density + squash (17:00–19:00) — NEW — density > 0 = stone; bias rises below a centre y and falls above it.
  - Cheese vs spaghetti caves (19:00–20:00) — NEW — "white = air" vs "the border between white and black = air".
  - Biome tables (21:00–24:00) — D.20 extended — range-indexed tables; warm biomes kept apart from cold ones.
- **Parameters / numbers:** flat demo y=100, random +0..19; sea level y=102; sine x10; Perlin x20 giving 80–120; spline example above; erosion index 0 = -1..-0.78; "up to 10 potential neighbours" per cell of the 5-D table (24:00, as spoken).
- **Determinism, seeds, chunks/seams:** Chunk 16 x 16 x 384; continuity comes from the noise function, not from stitching. He states interpolation and caching are omitted from the samples (6:00); the 4x8x4 cell interpolation is in the existing `Minecraft Worldgen.md`, not here.
- **Islands status:** same as the long talk. Spline remap **partial** (`Runtime/PCG/Fields/ScalarSpline.cs`, used in `Stage_BaseTerrain2D.cs`); fBm/ridged **exists**; 3D density/caves **missing** in PCG (3D primitives exist in `Runtime/Noise/Noise/Noise.Lattice.cs` and `Noise.Simplex.cs`); `BiomeTable.cs` **partial** (4x4 only).
- **Where it could land in Islands:** best used as the script for a web Islands Lab part, "terrain in 8 steps" (random -> flat -> sine -> noise -> octaves -> splines) in the explainer-recipe style, reusing the NoiseLab histogram/percentile idea to show how a spline reshapes the height distribution. No new engine work beyond the long talk.
- **Value for Islands:** medium — the most reusable of the two as teaching material; as engineering content it is a subset of the long talk.

### Plot and Parcel: Procedural Level Design in XCOM 2 — Brian Hess, GDC (49:36)
- **URL:** https://youtu.be/5jrq5rDI4dk
- **Core idea:** XCOM 2 builds tactical maps by assembling hand-authored pieces: a *plot* (a map with road/terrain layout and slots) is filled with *parcels* (buildings in size buckets) and *plot-covered parcels* (PCPs: small reusable road/terrain pieces with varied cover). Variety comes from many interchangeable pieces plus metadata rules (facing, driveways, zones, mission tags), not from generated geometry. Mostly a production postmortem of what rules were needed so assembled levels still play well.
- **Techniques:**
  - Plot / parcel slot assembly — NEW — a master map has holes (slots); each slot is filled by a streamed-in parcel map of matching size bucket (9:00). Closest existing: D.13 BSP / room placement, but pieces here are authored.
  - Size buckets on a shared grid base — NEW — small / medium / large parcels (first 12x12, 12x24, 24x24; shipped 16x16, 16x32, 32x32). Pieces must share a base unit (tried base 3, 14, 16, finally 8) or road widths (6, 10, 16) will not tile (16:00–17:00, 22:00–23:00).
  - Parcel subdivision — NEW — a large slot filled by two mediums, or a medium plus two smalls; turned off because it looked like a hodgepodge (12:00, 17:00).
  - Plot-covered parcels (PCPs) — NEW — fill the non-building area with small modular road/terrain tiles whose cover layout varies; reuse is fine because they are small and generic. A small-town street set had 24 pieces (courts, corners, straights of two lengths, intersections) (14:00–15:00). The biggest win in the talk.
  - Facing and driveway constraints — NEW (socket/adjacency matching) — each parcel marks which side faces a road and may only rotate into a slot whose road side matches; driveways must connect to a road in the plot. Over-constraining makes a parcel almost never appear, so use facing OR driveway, not both (18:00–20:00).
  - Zoning — NEW — tag parcels and slots with a zone (neighbourhood vs downtown) so related buildings cluster; costs variety, so more parcels are needed per zone (20:00–21:00).
  - Biome asset/map swap at load — NEW — maps are authored once as temperate; materials/meshes (later also whole maps) are swapped for arid/tundra/alien, with a "biome-only deco" layer for things with no equivalent (25:00–27:00, 40:00–41:00).
  - Multi-parcel buildings — NEW — block buildings assembled from 16 pieces (endcaps, corners, straights, T-junctions) spanning several parcels (37:00–38:00).
  - Random room-order dungeon — NEW — 15 unique 16x16 rooms cover three 7-room runs; order randomised, accepting illogical layouts for gameplay (39:00–40:00).
  - Procedural lead-up + fixed narrative parcel — NEW (design pattern) — a hand-built 32x32 parcel as the objective inside a procedural map (31:00–32:00).
  - Gameplay-driven placement rules — NEW (design knowledge) — cover density, the "one-wide rule" (two empty tiles between object clusters), a sea of low cover with sparse high cover (3:00–4:00).
  - Quick-launch authoring tool — n/a — loads a plot and lets you swap any parcel from a menu (28:00–29:00).
- **Parameters / numbers:** parcel sizes 16x16 / 16x32 / 32x32; a plot went from 8 parcels + 40 PCPs to 4 parcels + 20 PCPs after rules were relaxed (24:00); wilderness shipped ~40 parcels + ~130 PCPs; the game shipped ~80 plots, >200 parcels, 450 PCPs (33:00); a new plot type (slums) needed about half the pieces; 15 rooms for the dungeon type.
- **Determinism, seeds, chunks/seams:** Seeds not discussed. Seams are handled by authoring: shared base unit, matching road widths, and facing/driveway markup. Cinematics suffered because the map changes each time and no procedural-to-static export existed (45:00).
- **Islands status:**
  - Slot-and-piece assembly — **missing** as a layout system. Closest: `Runtime/PCG/Adapters/Tilemap/MegaTileStamper.cs` / `MegaTileRule.cs` (adapter-level stamping of multi-tile pieces, not layout-level parcels) and the `Runtime/PCG/Layout/` composers (rooms+corridors).
  - Facing / socket matching — **missing** (no per-piece edge metadata; WFC D.16 is the generic version).
  - Zones — **partial** — biome and region ids exist (`Stage_Biome2D.cs`, `RegionNameRegistry2D.cs`) but do not constrain placement of authored pieces.
  - Biome swap by data — **partial** — `BiomeColorPalette-Default.asset` and the tilemap adapter map biome id to visuals, so reskinning is data-driven; no per-piece alternates.
- **Where it could land in Islands:** (1) Layout pipeline: a settlement/village stage on top of the biome and river masks that places authored parcels (size buckets on a base-8 or base-16 grid) into slots along roads, with "facing" = adjacent road mask, using flood-fill/topology operators to find road-adjacent edges. (2) mundo-islas: the "procedural lead-up + hand-built narrative parcel" pattern for story beats in the colonisation game. (3) A web Islands Lab part on "authored pieces vs generated geometry" as a counterpoint to the noise parts. Keep conservative: it needs authored prefabs to be useful.
- **Value for Islands:** medium — not a noise technique, but the constraint set (size buckets on a shared base, facing, zones, reuse for small generic pieces) is the most practical reference for a village stage, which Islands does not have yet.

### 3D World Generation #8: Floating Origins for Bigger Worlds — SimonDev (12:06)
- **URL:** https://youtu.be/qYdcynW94vM
- **Core idea:** Scaling a three.js procedural planet to Earth size breaks because 32-bit floats lose precision far from the origin, so GPU vertices jitter. The fix is a floating origin: generate each terrain chunk's vertices relative to the camera position at build time, render with a camera at the origin, and shift each chunk by (build origin minus current camera position).
- **Techniques:**
  - Floating origin (camera-relative rendering) — NEW — (a) pass an `origin` (camera position) to the mesh worker; (b) vertex = local-to-world matrix applied, then minus origin; (c) draw with a view matrix whose translation is zeroed; (d) per chunk, set position = build origin minus current camera position each frame (7:00–10:00).
  - Float precision limits — n/a — 32-bit float has 23 mantissa bits (~6–7 decimal digits); at x=100,000, adding 0.001 is lost (4:00–5:00). CPU doubles do not fix it because the GPU uses 32-bit (6:00).
  - Fixed-point numbers — NEW — mentioned and rejected as too invasive (6:00).
  - Centralising terrain constants (radius, height) into one file — n/a — housekeeping (2:00).
- **Parameters / numbers:** planet radius started at ~4,000 m, then raised "by a bunch of zeros"; failure visible around 100,000 units; known leftovers: texture swimming (textures follow the camera), chunk seams (needs skirts), far clipping plane at planet scale (10:00–11:00).
- **Determinism, seeds, chunks/seams:** Seeds not discussed. Chunk seams are explicitly unresolved. Mesh generation runs in worker threads.
- **Islands status:** **n/a / missing** — Islands is grid-first and finite (integer cell coordinates), so there is no planetary-scale float problem and no camera-relative mesh path (no floating-origin matches in `Runtime`). Relevant only for a streaming, very large 3D world.
- **Where it could land in Islands:** Only as a future note for a large-world/planet mode in mundo-islas. One possibly relevant takeaway for `Runtime/Noise`: sample noise at integer lattice coordinates plus a local offset instead of huge float world positions; the video does not say this, so treat it as inference.
- **Value for Islands:** low — clear and correct, but it solves a problem Islands does not have; useful at most as a teaching page ("why big worlds shimmer").

#### Part summary
- Most valuable: Kniberg's spline-driven terrain shaping (noise -> height via editable control points, chained by continentalness/erosion/PV) is a cheap extension of the existing `ScalarSpline` + `Stage_BaseTerrain2D`, and a very good Islands Lab explainer ("move the dots, see cliffs and oceans").
- Second: extend `BiomeTable` (currently 4x4 temperature x moisture) with a third quantised axis and a variant flag, plus the design rules of neighbour-compatible authoring and soft coupling between biome and terrain.
- NEW techniques: chained noise-to-height splines, 3D density with squash factor and height offset, cheese/spaghetti/noodle caves via thresholding and ridging, 2D surface biomes plus injected 3D cave biomes, plot/parcel/PCP assembly with facing, driveway and zone metadata, load-time biome asset swapping, floating origin.
- XCOM's parcel system is the only gameplay-layout reference in the batch: useful later for a village/settlement stage and for hand-built parcels inside procedural maps in mundo-islas; floating origin is low value for a finite grid-first library.

---

## Part 4

## Part 4 cards

### Galak-Z: Forever: Building Space-Dungeons Organically — Zach Aman, 17-bit (GDC, 24:49)
- **URL:** https://youtu.be/38xLmlomvyE
- **Core idea:** Cellular-automata caves look great but cannot be populated or controlled at level scale, so the team used CA only for fixed-size rooms and built the level layout from a space-filling curve (Hilbert) over a room grid. Hand-authored room templates (painted layers of spawns, waypoints, props) are seeded by the CA and skinned per "dungeon zone".
- **Techniques:**
  - Cellular automata caves — D.12 — random seed fill (~50% alive), apply a stable-equilibrium rule set, about 10 iterations to settle; more dead cells in the seed gives less open space (5:00-6:00). Used only per room, at a fixed grid size.
  - BSP + minimum spanning tree rooms — D.13 — tried first and abandoned: corridors too long/angled, rooms awkward (7:00-8:00).
  - Z-order (Morton) curve — NEW — interleave bits of x,y into one index; invertible and locality-preserving, but has diagonal jumps so it was rejected (8:30-10:00).
  - Hilbert-curve dungeon path — NEW — orthogonal, invertible 1D<->2D map (d <-> x,y), ~20 lines of bit ops (11:00). A fixed Hilbert path is recognizable, so: take the next power-of-two Hilbert curve (4x the area), pick a random offset, use the dungeon grid as a window; walk the curve, keep cells inside the window, skip while outside, and end the path if it never re-enters a neighbouring cell (12:00).
  - Dead-end offshoots — NEW — cells excluded from the path but touching it become candidate dead ends; treasure and objectives go there. Too few dead ends: discard and regenerate (13:00). Reject-and-retry is cheap, so brute force is accepted.
  - Bit-string zigzag layout for "space hulks" — NEW — a 2-row dungeon: read the bits of a random number left to right, each bit stays on the row or switches row (19:00).
  - Room template editor with layered painting — NEW (authoring pattern) — low-res template with layers (geometry fixes, patrol waypoints, spawn points, decor, obstacles, lava, foreground); the CA seeds the base shape and designers touch it up (15:00-16:30).
  - Dungeon zones — NEW — 1-4 zones per dungeon, often spanning several rooms; each controls lighting, background/foreground, enemy and loot tables and run length. One template gives many looks (17:00-18:00).
  - Organic skin: flood-fill the CA grid, triangulate to meshes + collision, place prefab rock objects along the edges (18:00).
  - Hybrid dungeon types by mixing JSON data (cave + hulk = pirate base) (20:00).
- **Parameters / numbers:** CA seed ~50/50, ~10 iterations; 1-4 zones per dungeon; space hulks never more than 2 rooms high; rooms a fixed size (value not stated clearly). Hilbert window is a power of two, 4x the dungeon area in the example (4x4 rooms in an 8x8 curve).
- **Determinism, seeds, chunks/seams:** Not discussed as such. Generation is a fast brute-force retry loop against criteria (enough dead ends). Room edges must be open at the cardinal exits so neighbours connect. Advice: one coordinate system everywhere (his editor was y-down, game y-up), data in JSON, keep each layer to ~5-6 concepts.
- **Islands status:**
  - CA caves (D.12): **missing** — no cellular-automata code in `Runtime/PCG` (grep for cellular/automata is empty); a library card only.
  - BSP: **exists** — `Runtime/PCG/Layout/Bsp/`, `RoomFirstBspDungeon2D.cs`. MST: **missing** (CorridorFirst uses walks, no MST found).
  - Hilbert/Z-order path layout: **missing** (NEW).
  - Dead-end detection on a path: **missing**; `Layout/MaskNeighborOps2D.cs` has neighbour ops that could support it.
  - Flood fill / components: **exists** — `Operators/MaskFloodFillOps2D.cs`, `MaskTopologyOps2D.LabelConnectedComponents4`.
  - Room templates with painted layers, zones: **missing** (`RoomGridDungeon2D.cs` exists but has no template/zone concept).
- **Where it could land in Islands:** A new `Layout/` generator (a Hilbert-window layout) that outputs a room-cell path as a MaskGrid2D plus a dead-end mask, feeding `RoomGridDungeon2D`; a CA operator in `Operators/` (seed density, birth/survival, N steps, seeded with SmallXXHash) usable for cave rooms and coast roughening. Zones map to a region/biome label layer. For mundo-islas, dead ends as reward slots in dungeons or ruins. Conservative: dungeons are secondary to the island map pipeline.
- **Value for Islands:** medium — the Hilbert-window layout and dead-end trick are a cheap, novel, deterministic layout generator; CA rooms fill a known gap (D.12 has no code).

### Herbert Wolverson – Procedural Map Generation Techniques — Bracket Productions (Roguelike Celebration, 27:29)
- **URL:** https://youtu.be/TlLIOgWYVpI
- **Core idea:** A tour of simple map generators (rooms, BSP, CA, drunkard's walk, DLA, Voronoi, Perlin) followed by a validation toolkit built on Dijkstra maps: cull unreachable areas, place start/exit, compute a "hot path", and guarantee keys are solvable. Slogan: guide the randomness, then check it with algorithms. Already noted briefly in `research/2026-10-27-referencias-desde-marcadores.md` (item 1412: DLA and Dijkstra hot path); this card adds the details below.
- **Techniques:**
  - Random room placement + dog-leg corridors — D.13-adjacent — reject overlaps, stop at a criterion, join with L corridors (3:30).
  - BSP rooms — D.13 — halve randomly vertical/horizontal until room-sized, add a 1-tile gutter, zero rejections (4:30-5:30).
  - Cellular automata — D.12 — 50% random walls; per interior tile count 8 neighbours: 0 -> wall, 1-4 -> floor, 5+ -> wall; write to a copy each pass; same seed, same result (5:30-6:30).
  - Drunkard's walk — D.14 — several walkers with a max distance each, spawned inside the already-open area, until ~1/3 of the map is open; always contiguous, looks water-carved (6:30-7:45).
  - Diffusion-limited aggregation — NEW — carve a seed; shoot a particle from a random point in a random direction; on hitting open space, carve the last solid cell passed. Variants: central attractor (aim at the centre: open middle, ragged edges) and mirror symmetry (8:00-9:30). Also used to "erode" an existing map (16:00).
  - Voronoi diagrams — D.15 — brute-force nearest seed; the metric changes the look: Euclidean smooth, Manhattan sharp/man-made, Chebyshev between. Uses: cell walls, spawn groups per cell, hostile factions in far cells, city roads from cell edges (9:30-12:00).
  - Perlin/Simplex overworld — D.4/D.5 — octaves, gain, lacunarity, frequency; height bands (below 0 water, 0-0.75 land, above mountain); blend a second, bumpier noise as you zoom (12:00-15:00). In Q&A: mix several noise maps per region to avoid the "samey Perlin" look and run an erosion pass (25:00).
  - Combining techniques + prefabs — NEW (as pattern) — BSP half + CA half stitched, a prefab vault at the junction; DLA to blast an existing map; prefab placement by finding rooms bigger than the prefab, or a random-location fit test (15:00-17:00).
  - Dijkstra map — NEW as a named op — multi-source BFS distance from start points, sentinel for unreachable (17:00-18:30). Uses: delete or hide unreachable tiles; choose start/exit on valid tiles; put the exit or a bonus at the least accessible point.
  - Hot path — NEW — path start to exit, a second Dijkstra map from the path, tiles under ~10 are "near the main route": prune the rest, mark breadcrumb rooms, keep scary/bonus rooms off-path, order story beats by room index (20:00-23:00).
  - Key/lock solvability — NEW — put the locked door at a chokepoint found by flood fill so the key must lie in the reachable-before set (23:00).
- **Parameters / numbers:** CA thresholds above; drunkard's walk open target 1/3; hot path threshold ~10 tiles; BSP gutter 1 tile; altitude bands -1..0 water, 0..0.75 land. Gain/lacunarity values are left to experimentation.
- **Determinism, seeds, chunks/seams:** States CA is deterministic for a given seed, and Perlin with a fixed seed gives the same map and is zoom-consistent. Otherwise not discussed.
- **Islands status:**
  - Room/BSP: **exists** — `Layout/RoomFirstBspDungeon2D.cs`, `Layout/Bsp/`.
  - CA: **missing** (no cellular-automata code).
  - Drunkard's walk: **exists** — `Layout/SimpleRandomWalk2D.cs`, `IteratedRandomWalk2D.cs`.
  - DLA: **missing**.
  - Voronoi noise: **exists** — `Runtime/Noise/Noise/Noise.Voronoi*.cs` (metrics and functions). Voronoi partitioning of a grid (D.15): **partial** — noise-based cells in `MapNoiseBridge2D.cs`, no explicit seed-site partition operator found.
  - Perlin/simplex/fBm/ridged: **exists** — `Runtime/Noise`.
  - Dijkstra map: **partial** — `Operators/MaskMorphologyOps2D.BfsDistanceField` (uniform-cost BFS distance field; no weighted costs). Connected components: **exists** (`MaskTopologyOps2D.LabelConnectedComponents4`, `Stage_Regions2D`).
  - Hot path / solvability validators: **missing**.
  - Prefabs: **missing**.
- **Where it could land in Islands:** `Operators/`: a weighted Dijkstra map over MaskGrid2D (extends BfsDistanceField), then a small validators layer (unreachable cull, farthest point, hot-path mask, chokepoint finder); CA and DLA as new operators beside the morphology ops. A hot-path mask suits mundo-islas for placing landmarks, quest items and off-path danger. Web Islands Lab: a CA/DLA/drunkard comparison page in the style of the points-2d part. Prefab stamping is a later feature.
- **Value for Islands:** high — the Dijkstra/hot-path validation recipes reuse the existing grid and masks directly, and CA/DLA fill small, concrete gaps.

### Exploring the Tech and Design of Noita — Petri Purho, Nolla Games (GDC, 31:00)
- **URL:** https://youtu.be/prXuyMCgbTc
- **Core idea:** Noita simulates every pixel with a falling-sand cellular automaton, adds rigid bodies by extracting them from the grid and putting them back, and scales to a large world with dirty rects, checkerboard multithreading and chunk streaming. The design half is about taming good vs game-breaking emergence, and why making it a roguelite and communicating the systems helped.
- **Techniques:**
  - Falling-sand rules — NEW (simulation, not generation) — sand: try down, else down-left/down-right; water: same plus sideways; gases are the inverse (2:00-3:30).
  - Pixel-to-particle ejection — NEW — a pixel is lifted into a separate velocity+gravity particle system and reinserted on contact, so liquids splash (4:00-5:00).
  - Rigid bodies from pixels — NEW — marching squares outline (D.19), Douglas-Peucker simplification, triangulation, Box2D; per-pixel UV in its triangle gives the new position. Frame order: remove body pixels, step sand sim, step Box2D, write body pixels back, displace blocking pixels into the particle system (5:30-8:00).
  - Static terrain uses only marching squares + Douglas-Peucker for hollow collision bodies, only where needed (8:00).
  - Chunked dirty rects — NEW — world split into 64x64 chunks, each keeps a dirty rectangle to update; skips stable regions (10:00).
  - Checkerboard multithreading — NEW — 4 passes; each pass updates every fourth chunk in parallel, so a pixel may move up to 32 px without touching another thread's area; wait between passes; single shared buffer (10:30-12:30).
  - World streaming — NEW — 512x512 areas, about 12 live; the furthest is written to disk and re-read on revisit (12:30-13:30).
  - Level generation (Q&A) — NEW — Sean Barrett's herringbone Wang tile set, chosen over Spelunky's square 4x4 template grid because no visible global seam grid appears (28:00-29:30). Few details given.
- **Parameters / numbers:** prototype 256x256; chunks 64x64; stream areas 512x512, ~12 live; 4 update passes; 32 px safe travel.
- **Determinism, seeds, chunks/seams:** Seams only discussed for the Wang-tile choice; determinism of the simulation is not discussed (multithreaded single-buffer sim). The checkerboard scheme is the chunk-boundary solution for simulation.
- **Islands status:**
  - Marching squares: **missing** in `Runtime/PCG` (grep empty; D.19 card only). `Runtime/Meshes` not checked for iso-surface code.
  - Douglas-Peucker: **missing**.
  - Herringbone Wang tiles / tile grammar: **missing** (D.16 WFC is not implemented either).
  - Falling-sand CA, dirty rects, checkerboard threading, streaming: **n/a** for generation; relevant only if a runtime simulation is wanted.
- **Where it could land in Islands:** Mostly mundo-islas/gameplay, not the generator: emergent-systems design lessons (icons that communicate state, let the player blame themselves not the game). Marching squares + Douglas-Peucker is a useful contour extractor for coastlines and mask outlines (`Operators/`, adapters for colliders). Herringbone Wang tiles is a research item for seam-free tile layouts; study separately first.
- **Value for Islands:** low-to-medium — little generation content; value is design guidance and the contour extraction chain.

### Recreating Noita's Sand Simulation in C and OpenGL — John Jackson (10:03)
- **URL:** https://youtu.be/VLZjd_Y1gJ8
- **Core idea:** A from-scratch falling-sand CA in C: a 2D array of particle structs, one update function per material, and velocity-based movement. Useful mainly as a reference for how a material-based cellular automaton is structured.
- **Techniques:**
  - Particle grid with per-cell struct — NEW (simulation) — id, lifetime, colour, updated-this-frame flag; iterate rows then columns and dispatch the update by id (1:30-2:30).
  - Material rules as small variations — NEW — sand (down, down-left, down-right); water adds left/right spread; fire consumes flammables and has a short life; smoke is inverted sand; gunpowder, salt, lava, steam, oil, acid as modified sand/water with flammability, dissolve, density, corrosion chances (5:00-7:30).
  - Velocity + gravity with path stepping — NEW — compute the desired target, walk each cell along the path, stop at the last free cell; fixes jagged water (3:30-5:00).
  - Image import to particles — NEW — map each pixel to the nearest palette material by Euclidean colour distance (7:45-8:45).
- **Parameters / numbers:** none specific; per-material probabilities are not given.
- **Determinism, seeds, chunks/seams:** Not discussed (the "updated" flag avoids moving a particle twice in a frame).
- **Islands status:** **n/a** — runtime simulation, not generation. A generic CA operator is **missing** (see Galak-Z and Wolverson cards). Palette matching from images: **missing**, unrelated to core PCG.
- **Where it could land in Islands:** Only as a reference if a CA operator is added (neighbour rules, double buffer vs in-place). Possibly a web Islands Lab toy (sand/water on the island map) or mundo-islas dynamic effects (fire spread, flooding); low priority.
- **Value for Islands:** low — fun and simple, adds little beyond the Noita talk.

### Tarn Adams – Villains in Dwarf Fortress — Tarn Adams, Bay 12 Games (Roguelike Celebration, 33:01)
- **URL:** https://youtu.be/4-7TtPX5uhg
- **Core idea:** A plan to make Dwarf Fortress adventure mode less "flat" by adding villains who run schemes through chains of agents (necromancer -> apprentice -> spies -> advisor -> ruler), built from DF's existing entities (relationships, agents, armies, missions) instead of new systems. The player investigates and follows clues up the chain; each node acts on local rules so the network can break without breaking the game. The existing Islands DF note (`research/Games Worldgen/DwarfFortress Worldgen.md`) covers only geography/worldgen, so this is entirely new relative to it.
- **Techniques:**
  - Agent chains with goals and tools — NEW — each villain has inherent goals (necromancer: bodies) and history goals (revenge on the town that expelled him); each node knows only its goals, tools and link upward (10:00-14:00).
  - Relationship flags + MICE — NEW — relationships (friend, lover, divorce, grudge) carry small flags; MICE (money, ideology, coercion, ego) used as recruitment levers on picked people, as simple rolled numbers (12:00).
  - Goal propagation by messages — NEW — a goal (war creates bodies; revenge: kill these people) is passed down the line; the keeper of the seals knows assassination (13:00-14:00).
  - Local network repair — NEW — each node periodically checks its own motivation; if its contact dies it goes up the chain or finds another agent; compromise is tracked as a count of stored "agreements", which also feeds villain monologues and blackmail (14:30-17:30).
  - Information gating — NEW — the player starts with zero knowledge; conversation reveals pieces; the links between them must be investigated (18:00-19:30). Escape hatches always exist (a bandit just confesses), so a failed clue system still leaves a complete arc (20:00, 22:00).
  - Reuse of squads: an "army controller" lets companions be sent on missions, which generalises to player-as-villain or later diplomacy (31:00).
- **Parameters / numbers:** none; qualitative design. He estimates this at months, versus years for the creation-myth/magic work.
- **Determinism, seeds, chunks/seams:** Not discussed. Villain state comes from worldgen history; legends mode can later show the full story.
- **Islands status:** **missing** and outside the current PCG scope (no agent/plot layer in `Runtime/PCG`). Not covered in `DwarfFortress Worldgen.md` (pipeline and geography only).
- **Where it could land in Islands:** mundo-islas, not the PCG package: a "villain scheme" layer on top of Buffer Souls / NPC state, using the local-check-and-repair rule so schemes survive NPC death, and information gating for a mystery quest. Pairs with the colonization game (colonizer factions acting through intermediaries such as a cacique) and the storyteller-narrator idea. Keep design-only until the world and NPC layers exist; PCG should at most provide named sites (hideouts, towns) as hooks.
- **Value for Islands:** medium — high as design material for mundo-islas, none for the generator itself.

#### Part summary
- Highest value: Wolverson's Dijkstra-map validation set (unreachable cull, farthest point, hot path, chokepoint for locks): extend the existing `BfsDistanceField` and add validator operators; plus the missing CA (D.12 has no code) and DLA (NEW) operators.
- Galak-Z adds NEW layout techniques: Hilbert-curve path through a random-offset window, dead-end offshoots as reward slots, bit-string zigzag layout, room templates and dungeon zones; all cheap and deterministic with retry.
- NEW techniques in this batch: DLA, Dijkstra hot path / key solvability, Hilbert and Z-order path layouts, dead-end candidates, dungeon zones, herringbone Wang tiles (Noita), Douglas-Peucker contour simplification, villain agent chains (DF).
- Noita and the Jackson recreation are mostly runtime simulation (n/a for generation); only marching squares + Douglas-Peucker and the emergent-systems communication lessons transfer.
- Tarn Adams' villain talk is entirely new relative to the existing DF worldgen note and belongs in mundo-islas (NPC scheme layer with local repair, information gating), not in the PCG package.

---

## Part 5

### Bob Nystrom – Is There More to Game Architecture than ECS? — Roguelike Celebration (23:06)
- **URL:** https://youtu.be/JxI3Eu5DPwE
- **Core idea:** Nystrom argues classic domain-split ECS (a performance fix for cache misses in AAA games) does not suit turn-based tile roguelikes. He uses three patterns instead: capability components on items, type objects ("breeds") for content breadth, and the command pattern ("Action") for behaviour. Not a generation talk.
- **Techniques:**
  - Type object pattern (breed/kind class, optionally with a parent breed) — NEW (architecture, not in D.1–D.20) — one shared class instance per kind of monster/tile/item; entities point to it (13:00). Content is then defined in data files.
  - Capability components instead of inheritance — NEW — an item is a bag of capabilities (melee, defense, use); mix and match, data-driven (11:00).
  - Command/Action pattern with energy-based turn scheduling — NEW — AI and player both return an Action object; the loop runs it (16:00–19:00).
  - Verb-to-noun refactoring heuristic (20:00).
- **Parameters / numbers:** about 317 monsters in a big roguelike vs. 16 in a typical modern game (his claim, 9:00); nothing generation-specific.
- **Determinism, seeds, chunks/seams:** Not discussed. He shows his own level generator running at the end (21:00) but explains nothing about it.
- **Islands status:** All n/a as generation techniques. Islands already splits data from logic (mask/field layers, stages as `IMapStage2D`), which is a different axis.
- **Where it could land in Islands:** Only mundo-islas (gameplay layer): breed/type objects for biome-dependent creatures and tile kinds; Actions plus energy for the roguelike turn loop; Action-as-noun gives undo and shared AI/player code. Nothing for Runtime/Noise or the pipeline.
- **Value for Islands:** low for PCG; medium for mundo-islas architecture.

### Dungeon Generation in Gun Game — Firebelley Games (8:18)
- **URL:** https://youtu.be/sr6xgWhl08A
- **Core idea:** The developer drops a circle-brush walker (poor segmentation, hard to place enemies/start/exit, fragile "points of interest" heuristic) for the TinyKeep algorithm: scatter rooms, separate them, keep the biggest as main rooms, connect via a Delaunay-MST graph with a few loops, then add hallways. The room graph is what makes gameplay placement easy.
- **Techniques:**
  - TinyKeep room pipeline — NEW — (1) place random-size rectangles within a radius of the origin; (2) separation steering until no overlaps; (3) main rooms = top 30 by area; (4) Delaunay triangulation on main-room centers (Delaunator library); (5) minimum spanning tree; (6) add a random number of non-MST Delaunay edges back for loops; (7) hallways as straight or L-shaped (90 degree) lines; (8) any non-main room hit by a hallway line is kept as hallway filler; (9) paint tiles along the lines so hallways are at least a few tiles wide (3:00–6:00).
  - Delaunay + MST connectivity — D.15 (the Delaunay dual) and the "MST for connectivity" branch of the library tree — used in step 4–6 above.
  - Random walker with circular brush — D.14 — for softer edges (0:00).
  - Leaf-room detection on the graph for optional encounters; pre-made rooms as forced main rooms; polygonal rooms need polygon collision in separation (3:00, 6:30).
  - Centroid + farthest-tile points of interest with a 15 degree minimum angle — abandoned by the author (1:00).
- **Parameters / numbers:** top 30 rooms by area as main; 15 degrees min between POI rays; circle brush; loop edges = "random number" (unspecified).
- **Determinism, seeds, chunks/seams:** Not discussed.
- **Islands status:** Random walk: exists (`Runtime/PCG/Layout/SimpleRandomWalk2D.cs`, `IteratedRandomWalk2D.cs`). Rooms+corridors: partial (`RoomFirstBspDungeon2D.cs`, `RoomGridDungeon2D.cs`, `RoomsCorridorsComposer2D.cs` with `allowOverlap`; `CorridorFirstDungeon2D.cs` has `ensureRoomsAtDeadEnds`). Separation steering, Delaunay, MST, loop re-adding: missing (grep of Runtime found no Delaunay/spanning code).
- **Where it could land in Islands:** New layout strategy in `Runtime/PCG/Layout/` ("scatter-separate-graph dungeon") with a headless graph output (rooms, edges, leaf flags), then reuse the existing corridor carving and the rooms+corridors composer. Separation must be deterministic (fixed iteration order, fixed-step math). An MST utility could also serve road/river network ideas. Web Islands Lab candidate: BSP vs. TinyKeep.
- **Value for Islands:** medium-high: fills the main missing dungeon-layout family, and its graph output enables pacing/placement.

### Binding of Isaac: Room Generation Explained! — Himsl Games (7:04)
- **URL:** https://youtu.be/1-HIA6-LBJc
- **Core idea:** The speaker describes the original Flash-era Isaac: grow a grid of blank rooms by random door checks until the target count is reached (restart if it fails), then assign room roles to dead ends. His account of Rebirth is a guess.
- **Techniques:**
  - Random growth of a room graph on a grid with restart-on-failure — NEW — start room; for each open door a random check places a neighbor; repeat to the desired count; restart the whole thing if it gets stuck (0:00).
  - Dead-end role assignment — NEW — rooms with a single neighbor are end rooms; boss = farthest end room from start; shop/item rooms also on end rooms; secret rooms added afterwards, possibly linking an item room to another (1:00).
  - Layout-aware room placement (Rebirth, speculated) — NEW — choose from a pool of premade rooms with a door on the needed side, avoiding repeats (2:00–3:00).
  - Difficulty-tiered room pool — NEW — about 120 rooms rated easy/medium/hard; same layout reused with a different difficulty mix per floor; a per-level difficulty budget is suggested but not implemented (4:00).
- **Parameters / numbers:** about 120 rooms; a small grid makes late levels slow to solve; infinite-loop risk from restarts.
- **Determinism, seeds, chunks/seams:** Not discussed; the restart loop implies retry with the next RNG state, which fits a seeded scheme.
- **Islands status:** Grid room placement: partial (`RoomGridDungeon2D.cs`). Dead-end detection: partial (`CorridorFirstDungeon2D.cs` `ensureRoomsAtDeadEnds`). Farthest-room role assignment, room roles, premade room pools, difficulty budget, secret rooms: missing.
- **Where it could land in Islands:** A post-process "roles" stage over a room graph (BFS distance from start, leaf flags -> boss/shop/secret) in `Runtime/PCG/Layout/`, and later a mundo-islas difficulty budget for filling rooms. Pairs with the TinyKeep graph above.
- **Value for Islands:** medium: simple, generalizable role-assignment recipe; the rest is thin.

### Procedural Generation of Nuclear Throne — Nebula Coding (10:15)
- **URL:** https://youtu.be/I74I_MhZIK8
- **Core idea:** A Unity tutorial for Vlambeer's multi-walker drunkard's walk: several walkers spawn, die and turn by per-iteration chances, carving floors until a fill percentage is reached; then walls are added and lone walls optionally removed.
- **Techniques:**
  - Multi-walker drunkard's walk — D.14 (extension) — list of walkers (position + direction); each iteration: floor under each walker; with a chance remove one walker (never the last; at most one per iteration); per-walker chance to change direction; per-walker chance to spawn a new walker at its position (capped); move; clamp inside the grid with a 1-cell border; stop at a floor-percentage threshold with an iteration cap (3:00–5:30).
  - Wall pass — D.18-adjacent — empty cells cardinally adjacent to floors become walls (7:00).
  - Lone-wall cleanup — morphology-style — a wall with no non-floor 8-neighbors becomes floor (7:30–9:00).
- **Parameters / numbers:** direction uniform over 4 cardinals; turn/spawn/destroy chances, walker cap and fill % are script variables (values not stated).
- **Determinism, seeds, chunks/seams:** Not discussed (plain random).
- **Islands status:** Single walker: exists (`SimpleRandomWalk2D.cs`, `IteratedRandomWalk2D.cs`). Multi-walker with spawn/destroy and fill-percentage stop: missing (checked by file names and the iterated-walk header only). Wall/morphology cleanup: exists (`Runtime/PCG/Operators/MaskMorphologyOps2D.cs`, `Runtime/PCG/Layout/MaskNeighborOps2D.cs`).
- **Where it could land in Islands:** A strategy in `Runtime/PCG/Layout/` as a thin variant of `IteratedRandomWalk2D` (walker pool; rolls drawn from one seeded stream in fixed walker order for determinism); fill-ratio stop is a cheap addition. Useful as a cave/bay-shaped mask source.
- **Value for Islands:** low-medium: small, easy addition on top of existing walkers.

### I rewrote my dungeon generator! — UnitOfTime (4:27)
- **URL:** https://youtu.be/NpS5v_Tg4Bw
- **Core idea:** Old generator: random walk on a coarse grid, one room per cell. Problems: rooms must be the same size, and the boss can land next to spawn. After failing with force-directed planar graph drawing, he keeps the random-walk graph and adds a pacing post-process, then variable room sizes and a compaction pass.
- **Techniques:**
  - Handcrafted flow graph (Enter the Gungeon approach) — NEW — a predefined abstract graph decides connectivity and room types; rooms picked from a pool (0:30).
  - Force-directed planar drawing (spring edges, repelling nodes, then gravity compression) — NEW, tried and abandoned: weights hard to tune, slow with reruns (1:00–2:30).
  - Random-walk room graph + pacing post-process — D.14 + NEW — root = spawn; boss = a far leaf; this step is where any pacing rule goes (3:00).
  - Variable room sizes then compaction — NEW — pick a rectangle per room type, scale the graph up until a minimum gap holds, then move each room toward spawn and undo that room's move if it collides or loses axis alignment with its parent; every step keeps the layout valid, so no repair (3:00–4:00).
- **Parameters / numbers:** none concrete (minimum gap and force weights unspecified).
- **Determinism, seeds, chunks/seams:** Not discussed; the "always valid, undo one step" invariant is deterministic-friendly.
- **Islands status:** Grid-based room walk: partial (`RoomGridDungeon2D.cs` and the random-walk files). Pacing roles, flow-graph templates, variable-size compaction: missing.
- **Where it could land in Islands:** `Runtime/PCG/Layout/`: a "graph-first dungeon" input (authored or walked graph) plus the shared role-assignment step and a compaction operator. The talk is brief on implementation, so treat it as design inspiration.
- **Value for Islands:** medium: the invariant-preserving compaction and the pacing hook are the useful parts.

### A new way to generate worlds (stitched WFC) — Watt (10:51)
- **URL:** https://youtu.be/dFYMOzoSDNE
- **Core idea:** To avoid noise-looking islands and awkward biome borders, the author makes one small hand-drawn sample per biome (and per border type), runs WFC per "sector", then a "stitcher" pass runs WFC with already-placed neighbors as pre-filled constraints to fill the gaps. Sectors are placed using Perlin noise and run in parallel.
- **Techniques:**
  - Wave Function Collapse — D.16 — explained with adjacency rules: all cells start with every option; collapse the lowest-entropy cell; propagate to neighbors (0:30–1:30); rules can be learned from one input image; can start from a partially filled output (1:30–2:00).
  - Sector WFC + stitched borders — NEW (chunked use of D.16) — sectors (about 10x10) each get a biome sample; the stitcher runs on the gaps with a dedicated border sample (e.g. desert-to-forest) (2:00–2:45, 6:00).
  - Burst + Jobs port — NEW (engineering) — naive 100x100 took 3.05 s; first Burst version was slower (4.3 s) because of per-sector temporary allocations; reusing buffers gave 250 ms (5:00); a 1000x1000 map in 4 s in a build, sectors in parallel (4:00–5:30).
  - Perlin noise to place sectors/biomes (6:00); hand-built dungeons from authored rooms (entry, boss, random pool of middle rooms) (7:00–8:00).
- **Parameters / numbers:** sectors about 10x10; 1000x1000 map; 5 min naive vs. 4 s Burst build; WFC is weak at large scale (parallel paths, disjointed rooms) (1:45).
- **Determinism, seeds, chunks/seams:** Seams are the whole point (stitcher with border samples); he admits some stitching is visible. Seeding and determinism across parallel jobs are not discussed.
- **Islands status:** WFC: missing (no WFC code in `Runtime/`; only library card D.16). Biome map: exists (`Runtime/PCG/Layout/Maps/Stages/Stage_Biome2D.cs`, `BiomeTable.cs`). Authored-room pool dungeon: missing. It is marked "[in research]" in `Documentation~/research/2026-11-01-playlist-pcg.md`, but I found no dedicated card.
- **Where it could land in Islands:** A WFC operator in `Runtime/PCG/Operators/` working on discrete mask/id layers, per chunk with border-conditioned runs (fits grid-first, chunk-friendly design). Determinism would need a per-sector seed hashed from (world seed, sector coords) via `SmallXXHash` and a fixed propagation order (my inference; not in the video). Also vegetation or village layout on top of `Stage_Biome2D`. Lab page candidate: "WFC with seams".
- **Value for Islands:** high: the seam-handling idea, the Burst allocation lesson and the sector-seed design match Islands' invariants.

### Simulating A Virtual World…For A Thousand Years! — Two Minute Papers (5:21)
- **URL:** https://youtu.be/8YOpFsZsR9w
- **Paper:** "Synthetic Silviculture: Multi-scale Modeling of Plant Ecosystems", Makowski, Hädrich, Scheffczyk, Pirk, Michels, Pałubicki, ACM TOG (SIGGRAPH) 2019, 38(4), art. 131. Identified by web search; the video only says "from 2019" and shows no title.
- **Core idea:** Plants are simulated individually with competition for light (and, per the paper, water and soil) so vegetation depends on precipitation, temperature and terrain; the video shows ecological succession over centuries.
- **Techniques:**
  - Ecological succession by resource competition — NEW — shrubs, then pines, then shade-tolerant spruces take over as the canopy changes (1:00–2:00).
  - Climate-driven vegetation type (precipitation x temperature -> tundra, desert, rainforest) — D.20 (0:30).
  - Multi-scale plant modeling for scalability — NEW — details not given in the video (3:00).
- **Parameters / numbers:** 400 years/few hundred plants; 100,000 plants; 500,000 plants over more than 1,000 years (about 1,400); other objects are grown around (2:00–3:00).
- **Determinism, seeds, chunks/seams:** Not discussed. Offline simulation, not real-time or chunked.
- **Islands status:** Climate-biome table: exists (`Stage_Biome2D.cs`, `BiomeTable.cs`). Vegetation mask: exists (`Stage_Vegetation2D.cs`, quantile-mapped, biome-aware). Succession / individual plant competition: missing.
- **Where it could land in Islands:** Idea only: a cheap deterministic age-class succession pass after `Stage_Vegetation2D` (N steps of shading, producing species/age layers), as a web Islands Lab or Flora Lab page. The paper itself is too heavy and the video lacks its details.
- **Value for Islands:** low-medium: confirms the climate -> vegetation layering; succession is an optional layer.

### This New Method Can Simulate a Vast Ocean! — Two Minute Papers (5:58)
- **URL:** https://youtu.be/ia-VBSF4KXA
- **Paper:** "Ships, Splashes, and Waves on a Vast Ocean", Libo Huang, Ziyin Qu, Xun Tan, Xinxin Zhang, Dominik L. Michels, Chenfanfu Jiang, ACM TOG / SIGGRAPH Asia 2021. Identified by web search; the video names Libo Huang and SIGGRAPH Asia.
- **Core idea:** Couple a FLIP particle simulation (turbulence, splashes near moving objects) inside a box with a boundary element method (BEM) that simulates only the water surface for large ocean waves outside it, with a transition zone between. Trade-off: crisp detail is lost in the BEM region.
- **Techniques:**
  - FLIP + BEM hybrid fluid solver — NEW (not a worldgen technique) — local volumetric domain plus surface-only far field (0:30–2:30).
- **Parameters / numbers:** none concrete.
- **Determinism, seeds, chunks/seams:** The transition zone is the hard part; seams shown for visualization only (3:00). Determinism not discussed.
- **Islands status:** n/a (offline fluid simulation, not procedural world generation).
- **Where it could land in Islands:** Nothing direct. A weak analogy to the "local-zoom detail" idea in `CURRENT_STATE.md` ("Noted desired features"): detailed near, cheap far.
- **Value for Islands:** low.

#### Part summary
- Most valuable: Watt's stitched WFC: per-sector WFC with border samples and a stitching pass over pre-placed neighbors, parallel jobs, and the Burst lesson (reuse buffers). It maps to chunked, seeded generation with `SmallXXHash`-derived sector seeds. WFC (D.16) is currently missing in Islands.
- NEW dungeon family: TinyKeep (scatter, separate, top-N rooms, Delaunay, MST + loop edges, L-hallways); no Delaunay/MST/separation code found in `Runtime/`. Pair it with a role-assignment post-process (BFS-farthest leaf = boss, dead ends = shop/secret) from the Isaac and UnitOfTime videos; the same stage works on any room graph.
- Multi-walker drunkard's walk (Nuclear Throne) is a small extension of the existing `IteratedRandomWalk2D`; low effort. UnitOfTime's always-valid compaction with per-room undo and flow-graph templates are the other NEW ideas.
- Nystrom (type objects, capability components, Action/energy loop) is relevant only to mundo-islas gameplay architecture. The two Two Minute Papers videos are low value: Synthetic Silviculture (Makowski et al., 2019) suggests an optional vegetation-succession layer; Ships, Splashes, and Waves on a Vast Ocean (Huang et al., 2021) is n/a.

---

## Part 6

### Infinite procedurally generated city in Unity — marian519 / Marian Kleineberg (5:55)
- **URL:** https://youtu.be/-W7zt8181Zo · blog https://marian42.de/article/wfc/ (2019-01-06) · code https://github.com/marian42/wavefunctioncollapse (MIT) · build https://marian42.itch.io/wfc
- **Source note:** the video has no subtitles; this card is built from the video description and the author's blog post, not from the video itself.
- **Core idea:** an infinite 3D city from ~100 hand-modelled blocks with WFC / Model Synthesis (Merrell), collapsed lazily around the player. The interesting part is not WFC itself but what breaks when the world has no edges.
- **Techniques:**
  - WFC on a 3D slot grid — D.16 — lowest-entropy slot first, entropy weighted by module probability (not just option count); adjacency and weights hand-authored, not learned from an example.
  - Connector-based adjacency — NEW (vs. D.16 as written) — each face gets a connector number; horizontal connectors flipped / not flipped / symmetric, vertical ones a rotation index 0–3 or invariant. Neighbour lists and rotated variants are generated automatically from "module prototypes" edited in the Unity inspector. Plus explicit exclusion rules for pairs that match but look bad.
  - Infinite world without chunks — NEW — chunking failed (propagation reaches several slots beyond a chunk, so isolated chunks became unsolvable). Final: a lazy dictionary position→slot; only a small area is collapsed near the player, but propagation is allowed to run outside it.
  - Boundary constraints via a wrapped column — NEW — "air on top, solid at bottom" would cascade forever in an infinite map; instead a separate 1×n×1 column (wrapped) holds the pruned module sets, and each new slot is initialised from its column position.
  - Backtracking with history — NEW — keeps collapse order and propagation history; on contradiction, undoes part of it. Late detection can force large undos and rarely regenerates the slot the player is in.
- **Parameters / numbers:** ~100 modules; 6 connectors per module; no performance numbers given.
- **Determinism, seeds, chunks/seams:** the result depends on generation order (where the player walked), so it is **not** order-independent — the same seed explored differently gives a different city. This is the key clash with Islands' invariant.
- **Islands status:** WFC — missing (no WFC/WaveFunction in `Runtime/`); connector adjacency — missing; backtracking — missing.
- **Where it could land in Islands:** not in the deterministic map pipeline as-is. Candidate only for bounded, finite local content (a settlement or building interior inside one tile, generated whole from a seed), where order-dependence disappears. The connector/prototype authoring idea fits a future kit-of-pieces per people (videojuego-colonizacion, Stålberg note).
- **Value for Islands:** medium — mainly as a documented warning: the author himself calls infinite WFC unsuitable for commercial games because of late contradictions; compare with stitched WFC (Watt) in part 5.
