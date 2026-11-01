# Playlist «PCG» Tier 3 — natural processes (group C), 14 cards

Read on 2026-11-01 through the YouTube transcript panel in Claude in Chrome (`get_page_text`, full transcripts, not sampled). 12 of 14 had a usable transcript; *History of the Earth* has only on-screen event captions and *Looking at Interesting Old Maps* has none (card from title/description). Transcript text was not saved. Islands status checked with Grep over `Runtime/` (spot-checked by the orchestrator: no wind term in `Stage_Biome2D`; `moistureModulation = 0.0f` in `Stage_Vegetation2D.cs:105`). Research material, not authority. Cross-reference and backlog changes: end of [`crossref.md`](crossref.md) and [`backlog.md`](backlog.md).

## Part A cards (rivers, climate, deserts, islands)

Transcripts: all 7 videos had one (read through the YouTube panel). Auto-captions: 1, 4, 5. Human captions: 2, 6. Spanish captions: 3 (human translation of the TED-Ed English) and 7 (original Spanish).

Islands context checked first (2026-11-01):
- `Stage_Hydrology2D` does Priority-Flood+ε (fills every depression), then D8 flow directions, flow accumulation, and rivers = cells where `FlowAccumulation ≥ totalLandCells × riverThresholdFraction`. Rivers are 1 cell wide, follow the steepest descent and are never post-processed (contract L-9). Lakes are only non-Land water that is neither DeepWater nor ShallowWater. Hydrology makes no lakes, because every depression gets filled. There are no deltas, meanders, width or floodplains.
- `Stage_Biome2D`: temperature = base − lapse·height − latitude + coast moderation + noise. Moisture = coastal bonus decaying inland + noise + river bonus from FlowAccumulation. There is **no wind, no orographic effect and no rain shadow**. `BiomeType` (12 types) already has `TemperateRainforest`, `TropicalRainforest`, `SubtropicalDesert` and `TemperateDesert`, but no wetland, salt flat or oasis.
- Grep over `Runtime` (non-test) for `meander|floodplain|wetland|marsh|swamp|orograph|rain shadow|prevailing|oasis|dune|wadi|atoll|lagoon|reef|sandbar|fjord|delta (hydrology)`: **0 hits**.

---

### The River Erosion Process — Capture VR (3:09)
- **URL:** https://youtu.be/IxlpDWItLPg
- **Source note:** auto-captions (good; a lab flume narrated live)
- **Core idea:** a sand-table river run. The water widens its channel, eats away and lowers the banks, drops sediment in dead zones, and in three minutes changes from a narrow meander to a wider, straighter course that even jumps to another part of the channel.
- **Natural rules (with minute marks):**
  - An obstacle in the channel (a toy truck) cuts the cross-section. The water speeds up around it and attacks a new spot on the bank: shifting the flow moves the erosion (0:37–1:05).
  - Erosion first widens the channel and lowers the banks, fastest at the bends (1:12–1:36).
  - In a wide reach the water slows, sediment builds bars, and the bars turn the flow (2:01–2:29).
  - Net result: the course migrates, a meander can straighten, and the channel can move sideways (avulsion, 2:35–3:01).
- **Generation rules for Islands:**
  - A post-pass in `Stage_Hydrology2D` (as a new optional sub-stage, because L-9 forbids touching the thresholded mask itself). It would widen rivers by flow (e.g. width ∝ √FlowAccumulation, the usual hydraulic-geometry rule, not from the video) and widen more at sharp turns of the D8 path.
  - Optional meander pass: displace the river polyline sideways with low-frequency noise, scaled by how flat the ground is (low slope → more sinuous), with a seeded RNG for determinism.
  - Inspection metric: sinuosity = path length ÷ straight distance, per river, in `MapStatsExporter2D`.
- **Islands status:** width missing (rivers are 1 cell: the threshold in `Operators/HeightFieldHydrologyOps2D.cs`). Meanders and sinuosity missing.
- **Where it could land:** a sub-stage L.3 «channel shaping» in Hydrology, plus a sinuosity stat. Islands Lab web page «Why rivers bend».
- **Value for Islands:** medium. It's a qualitative demo with no numbers, but it names the missing pieces (width, migration, deposition bars).

### Why rivers shouldn't look like this — It's Complicated (The Guardian) (5:51)
- **URL:** https://youtu.be/zkmJRJaPBXE
- **Source note:** full transcript read (human captions)
- **Core idea:** the single, tidy, straight-banked river is a human artefact (drainage, levees, dredging). Natural lowland rivers are multi-channel and marshy, with wide floodplains that store water. Re-wilding them cuts both floods and droughts.
- **Natural rules (with minute marks):**
  - Natural rivers have many channels and marshes and are not clearly bounded. The video's references are the Paraná, the Mackenzie and the Allier (1:56–2:14).
  - Floodplains, marshes and ponds hold rain and release it slowly. Remove them and floods peak higher and dry spells go deeper (2:23–2:52).
  - Britain, 1840–1890: 12 million acres drained. Levees and straightening are what make the «postcard» river (0:58–1:27).
  - Restoration on the Meuse: levees moved hundreds of metres back and meanders re-cut. Per the video, it takes the extreme flood from 1-in-100 years to 1-in-1,250 years (3:37–4:25). Pickering used 167 leaky log dams (4:56).
- **Generation rules for Islands:**
  - Rule «natural lowland river = braided/marsh ribbon». In `Stage_Hydrology2D` or `Stage_Biome2D`, a floodplain mask: cells within N cells of a river, with low slope and height below river height + δ. Rivers become several channels in it, or carry a wetland tag.
  - Tunables: `floodplainWidth` and `floodplainMaxRise`, both scaled by flow.
  - A «Wetland/Marsh» biome would override the 4×4 `BiomeTable` when floodplain && moisture high.
  - Validator: on a flat coastal plain the marsh fraction > 0. Steep mountain reaches stay single-channel.
  - A «tamed vs. wild» toggle would render the same river straightened (for post-colonisation states).
- **Islands status:** floodplain, wetland and multi-channel all missing (no hits; `BiomeType.cs` has no wetland).
- **Where it could land:** Hydrology sub-stage + a new `BiomeType.Wetland` (a contract change: the biome histogram goldens would change).
- **Value for Islands:** medium-high. It gives a clear visual rule that is cheap to make (distance-to-river × slope mask), and it fixes the «single canal» look that D8 rivers have.
- **Other projects:** videojuego-colonizacion: the colonisers drain and straighten the island's marsh rivers (Victorian drainage as the model), with visible consequences (floods, drought). It's a mechanic and a visual «before/after».

### The largest river on Earth is actually in the sky — TED-Ed (Iseult Gillespie) (5:36)
- **URL:** https://youtu.be/tm6VSkm_ko8
- **Source note:** full transcript read (Spanish subtitles, human translation of the English original)
- **Core idea:** the Amazon's trees transpire so much water that humid air currents («flying rivers») form above the canopy. The trade winds carry them west, the Andes turn them south, and they rain over much of South America. Clearing the forest dries them up. The second half is about the Wampís Nation defending its territory.
- **Natural rules (with minute marks):**
  - Per the video, a mature Amazon tree transpires 200–1,000 L/day (0:38). The flying rivers carry about 20 billion tonnes of water a day, more than the Amazon River discharges (0:53–1:04).
  - The trade winds blow east to west at the equator, and the moisture travels with them (1:05–1:12).
  - A mountain barrier turns the flow aside (the Andes → south). Where it meets cold air masses, it rains (1:14–1:28).
  - Deforestation → less moisture recycled → drought and heat downwind. Per the video, parts of the continent could turn into desert within decades (1:33–1:48).
- **Generation rules for Islands:**
  - A wind-advection moisture pass for `Stage_Biome2D`. Moisture enters from the sea on the upwind side and is carried step by step along the prevailing wind. Over forest cells it is partly recharged (recycling factor), and over high ground it is lost. Mountains above a height deflect or block it.
  - Tunables: `windDirection` (or by latitude band: trades E→W, westerlies W→E, as in the next card), `recycleFactor`, `orographicLoss`.
  - This needs two passes (biome → vegetation → moisture again) or a fixed small number of iterations, to stay deterministic.
- **Islands status:** missing. Moisture has no wind term (`Stages/Stage_Biome2D.cs`, M.2 = coast distance + noise + river bonus).
- **Where it could land:** M.2b «advected moisture» in `Stage_Biome2D`, also used by the orographic rule (next card).
- **Value for Islands:** medium. The feedback loop is a nice idea, but on single-island maps it only matters for large islands or world maps (Phase W).
- **Other projects:** videojuego-colonizacion: the island's forest «makes» its own rain. Felling it dries the leeward side, a systemic consequence of colonisation. cuaderno-civilizaciones / cuaderno-injusticia: the Wampís Nation (autonomous territorial government since 2015; drove out illegal gold mining 2016–2018, per the video).

### Why We Find Rainforests in Unexpected Places — Atlas Pro (24:29)
- **URL:** https://youtu.be/oSOqJ5bRHx0
- **Source note:** auto-captions (good)
- **Core idea:** rainforest is arbitrary as a category, but the big three line up at ≥ 1,400 mm/yr. Outside the tropics that much rain only happens through the **orographic effect**: moist air from a large body of water hits mountains right on the coast. That gives a narrow belt on the windward side, with dry land behind the mountains.
- **Natural rules (with minute marks):**
  - Thresholds: Amazon about 2,000 mm (peaks 3,600). Congo 1,400–1,800. SE Asia > 1,400 (peaks 4,000). Working rule: rainforest above 1,400 mm/yr (1:56–3:48).
  - The tropics get steady warm, humid inflow. Mid-latitudes get mixed air masses and less predictable weather (4:43–5:12).
  - **Temperate rainforests only exist where mountains sit right next to a large body of water.** Moist air is forced up, cools and rains out (6:32–7:09). The result is a narrow corridor between sea and range, e.g. the Pacific NW at about 2,400 mm (5:39–6:41).
  - Prevailing winds: trades blow E→W in the tropics, westerlies W→E at mid-latitudes. So west coasts get wet and the lee/east side stays dry, as in Patagonia (7:21–7:34).
  - Further south, rain passes 4,000 mm (Magellanic forests). Glaciers and altitude break up the forest, and fjords hold forest between the peaks (8:09–8:30).
  - New Zealand's Southern Alps: west side over 2,000 mm. Lower ranges (Great Dividing Range) catch much less, so only patches (10:30–11:22).
  - Enclosed seas work too if mountains back them: Alborz/Caspian 1,500–2,000 mm with desert all around (Hyrcanian); Pontic/Black Sea (Colchis) (12:26–14:11).
  - A warm current (the Gulf Stream) adds rain to the coasts it reaches (15:03–16:00). Cold + wet = ice sheet rather than forest (SE Greenland at 1,800 mm, 18:00–18:27).
  - Snow counts as precipitation but isn't «rain»: precipitation type depends on temperature (21:38–22:17).
  - Human clearing removes forest where the climate supports it. Iceland went from about 40 % forest to grass and moss after settlement and sheep (17:07–17:43).
- **Generation rules for Islands:**
  - **Orographic moisture/rain shadow** in `Stage_Biome2D`. March each cell upwind along `windDirection`. Moisture gain ∝ positive height change on the windward slope near the coast (`orographicGain`), and moisture is depleted after crossing a ridge (`rainShadowStrength`).
  - `windDirection` comes from latitude band when `latitudeEffect > 0` (trades / westerlies), otherwise from a tunable.
  - Validator «windward > leeward»: for each ridge, mean moisture on the windward side ≥ leeward. Rainforest biomes only where mountains lie within K cells of the coast on the windward side.
  - `BiomeTable` check: `TemperateRainforest` needs high moisture *and* moderate temperature, and should mostly land on windward coastal slopes (an inspection metric: % of TemperateRainforest cells with windward exposure).
  - Snow vs. rain: where temperature is below a threshold and moisture high → Snow/ice rather than forest (already roughly true via `BiomeTable`).
  - Optional «warm current» tunable: + moisture/temperature along one coast.
- **Islands status:** orographic, rain shadow and wind direction missing. Latitude exists (`latitudeEffect`, `Stages/Stage_Biome2D.cs`, default 0). Lapse rate exists. Coast moderation exists. Rainforest biome types exist (`BiomeType.cs`).
- **Where it could land:** `Stage_Biome2D` M.2 (the same advection pass as the TED-Ed card). New NoiseLab-style didactic scene, or a web Islands Lab page «Why one side of the island is green».
- **Value for Islands:** **high.** This is the biggest missing realism rule in the biome stage: today moisture is radially symmetric around the coast, so every island is wet all round. One wind direction plus ridges would give asymmetric, believable islands.
- **Other projects:** videojuego-colonizacion: a wet windward half and a dry leeward half of the island is a strong setting for two peoples or two ways of life. cuaderno-civilizaciones: the Valdivian and Magellanic forests, plus the Nothofagus trees as relics of the Antarctic connection (Chilean context). Web Flora Lab: Nothofagus as a future plant after the araucaria and the palma chilena.

### The Sahara: MORE than Dunes — The Budget Museum (12:38)
- **URL:** https://youtu.be/uYlQZUFMoYo
- **Source note:** auto-captions (fair; place names garbled)
- **Core idea:** per the video, only about 20 % of the Sahara is sand dunes. The rest is a set of ecoregions (One Earth framework): coastal fog desert, xeric steppe, rocky plateaus, gravel plains, dry riverbeds, oases, salt depressions below sea level and cooler, wetter volcanic massifs.
- **Natural rules (with minute marks):**
  - About 9 million km², bounded by ocean, sea, the Atlas and the Sahel. Per the video, only 20 % is dunes (0:26–1:00).
  - Atlantic coastal desert: little rain, but sea mist. A thin belt with mangroves, mudflats and salt marsh on a rich sea (2:49–3:37).
  - Xeric steppe and woodland (Mediterranean influence, winter rain). Vegetation follows elevation differences. Small depressions (dayas) collect soil and shrubs (4:31–5:18).
  - Further south it gets drier. True desert surfaces: ergs (dunes up to hundreds of metres), hamadas (bare rocky plateaus), regs (gravel plains), wadis (dry riverbeds) (6:05–6:40).
  - Plants grow where groundwater can be reached: along wadis and depressions, and at oases where aquifers break the surface (7:03–7:19).
  - Halophytic zones: depressions below sea level, the beds of dried-up lakes. Very salty, occasionally flooded (7:21–8:02).
  - Volcanic mountains in the south are «islands» of cooler, wetter climate, with permanent pools and even endemic fish (10:07–10:52). Rock art and relict lakes record a greener Sahara, and the wet/dry swing is cyclical (11:07–11:41).
- **Generation rules for Islands:**
  - Split the desert biomes by surface. Inside `SubtropicalDesert`/`TemperateDesert` cells, a sub-classifier (in `Stage_Biome2D` or a new desert pass):
    - erg = flat-ish low ground with noise-dune ridges
    - hamada = high, flat plateau
    - reg = flat, mid-height, gravel
    - wadi = river cells (FlowAccumulation over the threshold) in an arid biome, drawn as a dry bed, not water
    - oasis = local depression or wadi cell with high flow and low height → vegetation + water
    - salt pan = closed depression (a pit that Priority-Flood would fill)
  - Validator: dunes ≤ ~30 % of desert cells (the video's 20 % as a target, not a law).
  - Hydrology hook: return the filled depressions as data (the depth Priority-Flood added per cell), not throw them away. Depth > 0 in arid climate → salt flat/ephemeral lake. In wet climate → lake (this would also give Islands real endorheic lakes).
  - Mountains in deserts: lapse rate already cools them. Add an orographic moisture bonus (previous card) so desert massifs green up.
  - Coastal fog: in an arid biome, coast distance ≤ N → «fog desert» variant with more vegetation.
- **Islands status:** missing: dunes/erg, hamada, wadi, oasis, salt flats (no hits). Depression filling exists but its depth isn't exported (`Operators/HeightFieldHydrologyOps2D.cs`, `FillDepressions`). Terrace quantisation exists (`MapTunables2D.cs`, «dramatic terraced appearance»), which is close to plateaus but not driven by climate.
- **Where it could land:** a desert surface sub-classifier after `Stage_Biome2D`, plus a new `FillDepth` field from Hydrology (it feeds lakes, salt pans and oases). Tiles via `BiomeTileOverride` per sub-type.
- **Value for Islands:** medium-high. The idea «arid ≠ sand» plus «depression depth → lake/salt pan/oasis» gives several rules at once. The FillDepth export would also fix the missing natural lakes.
- **Other projects:** cuaderno-civilizaciones: the Atacama is the Chilean parallel (fog desert/camanchaca, salt flats, oases like San Pedro, and Tulor, already in the notebook).

### Strangest Islands That Exist — BE AMAZED (26:07)
- **URL:** https://youtu.be/Wzel5A2-D_E
- **Source note:** full transcript read (human captions)
- **Core idea:** a listicle of 20 odd islands. Few of them are about natural process. The useful ones work as **island archetypes**: sea stack, atoll, sandbar on a shelf edge, nested island-in-lake-in-volcano, an island under its own cloud, weathered volcanic soils.
- **Natural rules (with minute marks):**
  - Ball's Pyramid: a sea stack 562 m high, the eroded remnant of a shield volcano about 7 Ma old. Tall, tiny, steep (0:36–1:07).
  - Litla Dímun: moist air rising over a 414 m peak cools and condenses into a cap cloud. Steep shores leave it uninhabited (12:52–13:41). This is orographic condensation again.
  - Vulcan Point (Taal): an island in a crater lake, on a volcano island in a lake, on Luzon. Recursive land/water nesting (13:49–14:21). The 2020 eruption dried the crater lake (14:39–14:56).
  - Sable Island: a 42 km × 1.6 km crescent of sand on a glacier-carved shelf ridge. Grass, wild horses, uninhabited (15:15–16:00).
  - Maldives atolls: rings of reef around sunken extinct volcanoes. The sequence is fringing reef → barrier reef → atoll as the volcano sinks; coral sand builds islets (20:58–21:50).
  - Chamarel (Mauritius): basalt weathered into clay (iron/aluminium oxides) gives bands of coloured soil (11:45–12:21).
  - Rotating circular island in the Paraná delta (per the video, an unproven ice-disc-like current hypothesis, 6:51–8:21).
  - Runit Dome (Enewetak): a concrete cap over US nuclear-test waste, cracking (18:46–20:28).
- **Generation rules for Islands:**
  - Archetype presets for `Stage_BaseTerrain2D` / shape input:
    - «sea stack» = small radius, very high peak, steep `islandSmoothFrom01`
    - «atoll» = ring mask (annulus) with an inner lagoon of ShallowWater, low height, Beach biome
    - «sand crescent» = thin curved mask, low height, grass only
    - «nested caldera» = a land ring around a lake, with an islet inside
  - Validator for the atoll: lagoon is water, enclosed by land ± passes, and not DeepWater-connected unless a pass is wanted.
  - Lake-islet rule: land components inside a Lakes component must be allowed and must survive `MinLakeArea`.
  - Optional soil-colour field for volcanic weathering (tile tint by age/height). Low priority.
- **Islands status:** archetypes missing. `IslandShapeMode.cs` has Ellipse, Rectangle, NoShape and Custom (a ring/atoll mask would go through Custom/`MapShapeInput`). Atoll, lagoon and reef missing (no hits). Lakes are only the enclosed non-land water from the threshold (`Stages/Stage_Hydrology2D.cs` L.2), so an inner lagoon would come out as Lakes, not as lagoon/ShallowWater.
- **Where it could land:** `MapGenerationPreset` assets per archetype, and an annulus shape builder in `ShapeMaskBuilder2D.cs`. Web Islands Lab «a gallery of island types».
- **Value for Islands:** low-medium. It's entertainment, not process, but it adds atoll and nested-lake as good test shapes and stress cases for Shore and Hydrology.
- **Other projects:** cuaderno-injusticia: Runit Dome and US nuclear testing in the Marshall Islands (a colonial «tomb» left to the local government). Okunoshima (wartime poison gas, erased from maps). videojuego-colonizacion: atoll + lagoon as a home for the sea people (the girl diver).

### Islas artificiales: la fantasía de inventar un país — Un Mundo Inmenso (10:02)
- **URL:** https://youtu.be/lVNipUB0ClU
- **Source note:** full transcript read (Spanish captions)
- **Core idea:** a tour of artificial islands (Palm Jumeirah, Hulhumalé, Japan's airports, Dejima, Fort Boyard, Barro Colorado, Puerto Madero, Rose Island) and what they mean legally. Almost nothing about natural process.
- **Natural rules (with minute marks):**
  - Two methods: land reclaimed from the sea, or platforms (0:52–1:04).
  - Palm Jumeirah: built from rock and sand, no steel or concrete in the base. 120 million m³ dredged from the sea floor, because per the video desert sand «liquefies» in water. Outer breakwater for waves and wind (1:20–2:04).
  - Barro Colorado: hilltops that became an island when the Panama Canal flooded Gatún Lake. Now a biodiversity reference site (5:53–6:14).
  - Law: artificial islands don't count for territorial sea/EEZ claims (7:39–7:55).
- **Generation rules for Islands:**
  - «Hilltops become islands when the water rises» is exactly how the `waterThreshold01` cut already makes islands. It suggests a sea-level slider or animation as a didactic (NoiseLab-style) demo.
  - The breakwater idea: an outer ring of land shelters calm ShallowWater (same shape family as the atoll).
  - Nothing else for generation.
- **Islands status:** the water threshold exists (`MapTunables2D.cs` / `Stage_BaseTerrain2D.cs`). No sea-level animation (not checked further; low value).
- **Where it could land:** at most a «raise the sea» toggle in a NoiseLab-style sample.
- **Value for Islands:** low. It's a human-geography video. The only generator idea (flooded hilltops) already exists.
- **Other projects:** videojuego-colonizacion: **Dejima** (1634–1853), an artificial islet in Nagasaki Bay where Dutch traders were confined and could not cross the bridge into «sacred» Japan. It's a precise historical model for a people that lets outsiders trade only in a controlled enclave, a possible counter-history for the game. Rose Island and Sealand (micro-states on platforms) are possible tangents.

---

## Part B cards (maps, Earth history, rocks, patterns)

Transcripts: full transcript for Tom Scott, Veritasium, Be Smart, The Fantasy Forge; auto-captions (Spanish, good) for El Robot de Platón; event-annotation captions only (no narration) for History of the Earth; none for Geography Geek (captions unavailable). Video 7 found: `HawxK2FWm3M`, channel confirmed as The Fantasy Forge.

Islands checks below reuse part A where relevant (rivers = Priority-Flood + D8, 1 cell wide; moisture = coastal distance + river bonus; latitude, lapse rate and coast moderation exist; no fjord, wind, orographic, delta). New greps over `D:\Projects\Islands\Runtime` for this part: no hits for `tecton`, `plate` (only "plateau"), `reaction-diffusion`/`turing`/`gray-scott`/`activator`, `strata`/`geolog`/`rock`, `glacier`/`fjord`, `volcan`. Noise has `Ridged` multifractal (`Runtime/Noise/Noise/Noise.cs`) and Voronoi `F1`, `F2`, `F2MinusF1`, `CellAsIslands` (`Runtime/Noise/Noise/Noise.Voronoi.Function.cs`).

### Looking at Interesting Old Maps for 10 Minutes — Geography Geek (10:24)
- **URL:** https://youtu.be/TitHhF0e79I
- **Source note:** no transcript: from title/description only (captions unavailable; no chapters; description says only "random old maps from the 16th to the 19th century").
- **Core idea:** A tour of curious 16th–19th-century maps. Content not recoverable without watching.
- **Natural rules:** none extractable.
- **Generation rules for Islands:** at most a style reference for a rendered "old map" view (parchment palette, hachured mountains, sea monsters, cartouches). That belongs to a render adapter or the web, not to the generator.
- **Islands status:** n/a.
- **Where it could land:** a possible "antique map" palette in the web palette library or a Tilemap/visualization skin.
- **Value for Islands:** low. No rules, only style, and the content itself was not readable.
- **Other projects:** videojuego-colonizacion: the "two maps of the same island" idea (outsider's chart vs. native map) noted on 2026-10-27 could use period cartography as reference. Watching it by eye would be needed.

### History of the Earth — Algol (11:35)
- **URL:** https://youtu.be/Q1OreyX0-fw
- **Source note:** captions are only on-screen event notes (about 26 cards, Ma dates), no narration. The video itself is an animated plate reconstruction (based on Algol's "Interactive Continental Drift") with temperature, atmosphere and day-length readouts.
- **Core idea:** 4.5 Gyr of Earth history as a moving map. Continents assemble and break apart, and oceans and atmosphere change. The intro card warns that reconstructions before ~1000 Ma are inaccurate and the climate numbers are estimates.
- **Natural rules (with minute marks):**
  - Continents are not fixed. Land repeatedly gathers into supercontinents and rifts apart (the whole animation). Rule: a coastline is a snapshot of plates in motion, so islands and continents share histories (rifted fragments match across a sea).
  - ~0:26 (4410 Ma) surface water appears early; ~0:54 magnetic field active at 10–50 % of today's strength.
  - 4:24–4:32 (2500–2450 Ma) Great Oxygenation; banded iron formations "rust" the oceans (per the video, possibly planet-wide). 6:53 (1600 Ma) sulfur-rich "purple" oceans. Global colour and chemistry change over time: palette as a function of epoch.
  - 5:43 (2023 Ma) Vredefort, the largest confirmed impact crater (300 km). Impacts are a legitimate, rare terrain feature.
  - 6:20 (1800 Ma) "Boring Billion": long tectonic and climatic stability is possible.
  - 9:40 (600 Ma) ozone layer forms; first charcoal, so fire needs enough oxygen.
  - 11:12 (50 Ma) PETM: magmatism causes a warm peak. 11:19 (5.96 Ma) Messinian crisis: closing Gibraltar dries the Mediterranean into a desert basin more than 50 °C hot. At 5.33 Ma the Zanclean flood refills it in months to years. Rule: a sea whose only strait is closed becomes a deep evaporite basin below sea level.
- **Generation rules for Islands:**
  - **Plate-seeded macro layout (missing).** Use Voronoi cells (existing `Noise.Voronoi`, `CellAsIslands`) as plates, give each a motion vector, and classify each boundary as convergent, divergent or transform from the relative velocity. Convergent boundaries raise ridge height (feeding `Stage_BaseTerrain2D`/`Stage_Hills2D`, perhaps with `Ridged` noise along the boundary). Divergent boundaries lower height (rift valleys, new straits). This is the same idea Fantasy Forge draws by hand (below).
  - **Rifted-fragment islands:** offset a piece of the land mask along a plate vector so that the coasts on both sides of a strait roughly match. Validator: cross-strait coastline similarity.
  - **Closed-basin rule (`Stage_Hydrology2D`):** a water body below sea level with no ocean connection becomes a lake, salt flat or dry basin depending on moisture (part A already found no endorheic or salt-flat handling).
  - **Impact crater** as a rare stamp operator (ring rim + bowl) in base terrain. Optional, low priority.
- **Islands status:** plates/tectonics missing (no `tecton`/`plate` hits). Voronoi primitives exist (`Noise.Voronoi.Function.cs`). Ridged noise exists (`Noise.cs`). DeepWater = ocean connected to the border exists (`Stage_Hydrology2D.cs`, F2). Closed-basin evaporites missing.
- **Where it could land:** a new optional `Stage_Tectonics2D` before `Stage_BaseTerrain2D`, writing a "uplift" field that base terrain and hills read. NoiseLab could show Voronoi plates first. The web Islands Lab catalogue already lists techniques, and "plates from Voronoi" fits there.
- **Value for Islands:** medium. Strongest argument for a plate stage; the video gives no numbers usable as parameters.
- **Other projects:** cuaderno-civilizaciones (deep-time context for the future animated map); videojuego-colonizacion (the island's geologic origin myth vs. its real geology).

### ¿Cómo es que HAY Planetas MÁS HABITABLES que la Tierra? — El Robot de Platón (14:27)
- **URL:** https://youtu.be/nUdip3qGM7w
- **Source note:** auto-captions (Spanish, good quality).
- **Core idea:** Following René Heller and John Armstrong's "superhabitable worlds" (published in *Astrobiology*, per the video), some planets could suit life better than Earth. Factors include tidal heating, more surface area, smaller continents, shallow seas, plate tectonics, a slightly warmer climate, orange-dwarf stars and a gas giant nearby.
- **Natural rules (with minute marks):**
  - 0:35 Atacama: decades without rain, acidic soil, high UV, yet bacteria live there. Even "dead" biomes have some life.
  - 5:40–6:30 tidal heating (Europa's tides ~1000× the Moon's from Earth, per the video) can make bodies habitable outside the habitable zone, or uninhabitable inside it.
  - 6:51 ~1.5 Earth masses keep interior heat, molten core and magnetic field longer.
  - **7:09 The same land area split into smaller continents is more habitable. Very large continents have vast, inhospitable desert interiors (continentality).**
  - **7:23 Shallow seas hold more biodiversity than the deep ocean.**
  - 7:34 Dry "Dune" planets may be more common habitable worlds, because water vapour is a greenhouse gas (Venus).
  - 8:00 Plate tectonics recycles materials and supports life.
  - 8:21 About +5 °C over Earth means larger tropics and more biodiversity. Fewer temperature swings mean fewer glaciations and extinctions.
  - 9:27–10:05 Orange (K) dwarfs are favoured: superhabitable worlds would be slightly older and 2–3 Earth masses. 10:13 candidates: 24 objects, only KOI 5715.01 met all three criteria (per the video).
- **Generation rules for Islands:**
  - **Continentality → aridity.** Moisture already falls with coastal distance (part A), so a big landmass already gets a dry core. Add a validator or metric: "interior dryness vs. landmass size". Report the land fraction farther than N cells from the coast and its mean moisture per connected landmass, and check that a large continent gets a dry centre and an archipelago does not.
  - **Shallow shelf as its own richness band.** `Stage_Shore2D`/`Stage_Hydrology2D` separate shallow from deep water. Expose a "shelf" class (shallow water within N cells of land) for resource/fauna density (reefs, kelp, fishing). This also connects to the sea people of videojuego-colonizacion (divers).
  - **Global climate offset tunable:** a single +ΔT applied before the `BiomeTable` lookup widens the tropical band. Cheap "planet preset" knob.
  - **Archipelago preset:** same land fraction but more, smaller landmasses means more coast, more moisture and more biome variety. Inspection metric: number of landmasses and coast/land cell ratio.
- **Islands status:** coastal-distance moisture exists (part A). Latitude temperature exists (`Stage_Biome2D.cs`, `BiomeType.cs`). A per-landmass interior-aridity metric is missing (`MapStatsExporter2D.cs` exists as the place for it). A shelf/shallow class beyond the shore band: partial (DeepWater vs. shallow is split in `Stage_Hydrology2D.cs`; no resource meaning). A global temperature offset is not checked/likely missing.
- **Where it could land:** `MapStatsExporter2D` (landmass count, coast ratio, interior aridity); a `temperatureOffset` tunable in `MapTunables2D`; shelf mask in `Stage_Shore2D`.
- **Value for Islands:** medium. Gives two cheap, testable checks (continentality and shelf) plus a climate knob. The rest is astrobiology.
- **Other projects:** videojuego-colonizacion (the shallow-sea people and their richer reef economy); web personal (a "planet preset" slider in the Islands Lab).

### Literally just three minutes where I talk about some rocks — Tom Scott (3:15)
- **URL:** https://youtu.be/EfdwRRpiYGQ
- **Source note:** full transcript read.
- **Core idea:** Siccar Point (Scotland), Hutton's unconformity of 1788: near-vertical grey greywacke under almost horizontal red sandstone, proof of deep geological time.
- **Natural rules (with minute marks):**
  - 0:59 Greywacke laid down on the ocean floor ~435 Ma, then folded, tilted and uplifted "millimetre by millimetre" until nearly vertical (1:08).
  - 1:15 Red sandstone deposited on top ~370 Ma; more uplift, tilting and erosion followed.
  - 1:36–1:45 The result: two rock layers of different age, type and dip in contact = an **unconformity**. 1:48 They are not rare if you know what to look for.
  - General rule: visible rock = deposition → deformation → erosion → new deposition, repeated. Coastal cliffs expose the stack.
- **Generation rules for Islands:**
  - **Strata as a cheap secondary field:** a "rock layer index" from height plus a low-frequency tilt (plane + noise). It colours cliffs and exposed slopes with banded strata that are tilted, not flat. An unconformity = two strata fields with different tilt joined at an erosion surface (a height threshold). Mostly a visual/material layer for cliffs in `Stage_Shore2D` (cliff cells) or a render adapter.
  - Could also drive resources (stone and ore by layer) for gameplay.
- **Islands status:** missing (no `strata`/`geolog`/`rock` hits).
- **Where it could land:** a visualization/material layer, or a NoiseLab demo ("tilted strata with unconformity"). Not a priority stage.
- **Value for Islands:** low. A nice visual and material idea, no impact on layout.
- **Other projects:** web personal (a tiny explainer scene: deposit, tilt, erode, deposit, with a slider for geological time; fits the Labs format).

### The Snowflake Myth — Veritasium (18:48)
- **URL:** https://youtu.be/ao2Jfm35XeE
- **Source note:** full transcript read. Title on the page is **"The Snowflake Myth"** (not "Mystery").
- **Core idea:** Ken Libbrecht grows "designer" snowflakes in the lab. Snowflake shape follows two variables, temperature and supersaturation (the Nakaya diagram). Six-fold symmetry comes from identical conditions on all arms, not from communication between them. Libbrecht proposes a new explanation for the plate↔column alternation.
- **Natural rules (with minute marks):**
  - 7:21–8:07 Facets form because molecules stick readily to rough surfaces and bounce off smooth ones. Rough areas fill in and flat facets grow slowly, so any shape becomes faceted.
  - 8:20 Hexagonal prism: if the basal facets grow faster you get a column, if the prism facets grow faster you get a plate.
  - **8:38–8:55 Corners stick out further into humid air, so they grow faster, then stick out more (positive feedback). This makes six branches, and side branches form at the branch corners. ~100,000 droplets and 30–45 min per flake.**
  - **9:40 Nakaya diagram: ~−2 °C plates; ~−5 °C columns/needles; ~−15 °C plates (branches with high supersaturation); below −20 °C columns and plates.**
  - 10:19 A cloud rising through a cold front gives capped columns (columns at −6…−10, then plates as it cools). The shape records the path through conditions.
  - **10:45 Symmetry: all six arms see the same conditions at each moment. Different flakes take different paths, so no two are alike.**
  - 12:41 Twin flakes grown too close compete for moisture and stunt each other.
  - 13:28–16:06 Nucleation barriers per facet depend on temperature. Libbrecht's hypothesis: narrow facets get dips (basal ~−4 °C, prism ~−15 °C), which explains the alternation. Lab results agree so far (per the video).
- **Generation rules for Islands:**
  - **"History-driven growth" as a generator pattern:** a shape = integration of a shared condition timeline applied to every symmetric branch. This is a recipe for deterministic, symmetric-but-unique procedural shapes (snowflakes, frost, crystals, ice decals, magic sigils). Seed → condition curve (T(t), S(t)) → the Nakaya lookup picks plate/column/branch per step.
  - **Tip-growth positive feedback + competition** is the same mechanism as DLA/river-network growth and Space Colonization trees. It could serve as a check on how Islands branches things (rivers are flow-based, which is correct for rivers; this is for frost/coral/lightning decorations).
  - Competition for a shared resource suppresses neighbours. This matches Poisson/blue-noise spacing rules already used for vegetation on the web (points-2d).
  - Not a terrain rule. Islands has no snow or ice morphology.
- **Islands status:** missing (no crystal/DLA growth). Not needed for layout.
- **Where it could land:** web Labs (a snowflake explainer with the Nakaya diagram as a 2-slider control is a natural fit for the explainer recipe) or a decorative generator for the game's magic or ice. Islands: not a stage.
- **Value for Islands:** low for terrain. High as a web explainer, and the "shared timeline → symmetric uniqueness" idea is a reusable determinism pattern.
- **Other projects:** web personal (Labs explainer, very strong fit); videojuego-colonizacion (the serpent's scale or magic symbols grown by a timeline). Claudio's emergence interest (order from simple rules) fits directly.

### The Mathematical Code Hidden In Nature — Be Smart (14:05)
- **URL:** https://youtu.be/JLkCaBwRrVo
- **Source note:** full transcript read.
- **Core idea:** Turing's 1952 "The Chemical Basis of Morphogenesis": two morphogens diffuse at different rates and react, which produces spots, stripes and labyrinths. Gierer and Meinhardt rediscovered it in the 1970s. Real morphogens are now found (mouse palate ridges, feather and hair spacing, shark denticles, digits).
- **Natural rules (with minute marks):**
  - 4:00–4:16 Two chemicals that diffuse at **different rates** are the key. 4:56 Diffusion alone or reaction alone does not make patterns.
  - **5:36 Activator makes more of itself and makes inhibitor; inhibitor suppresses the activator.**
  - **5:52–6:38 Forest-fire analogy: fires (activator) and firefighters (inhibitor) spread. Spots appear only if the inhibitor diffuses faster than the activator.**
  - **6:46–7:20 The same equations give spots, stripes or mixtures by small changes in rates (self-production, the two diffusion speeds).**
  - **7:20–7:43 Domain shape matters: on irregular surfaces different regions show different patterns (spots on the body, stripes on the tail).**
  - 9:24–9:35 Digits: three interacting activator/inhibitor signals give an on/off striped "binary" pattern.
- **Generation rules for Islands:**
  - **Reaction-diffusion (Gray-Scott or Gierer-Meinhardt) as a noise-like field generator** on the grid: deterministic given seed, steps and parameters. Uses: (a) **vegetation patterning in arid biomes** (real "tiger bush" banded vegetation and spotted vegetation in drylands follow this mechanism; a natural extension, not in the video) driven by moisture: low moisture gives spots or bands, higher gives labyrinth or full cover; (b) labyrinthine textures for reefs/brain coral in the shelf band; (c) a mask for `Stage_Vegetation2D` instead of fBm in specific biomes.
  - **Domain-shape effect:** run R-D inside the actual biome region (`Stage_Regions2D` components) so that narrow regions get stripes and wide ones get spots. A free source of variety.
  - Parameters: feed/kill (Gray-Scott) or diffusion ratio Dv/Du > 1. Map biome moisture to a point in parameter space.
  - Determinism: fixed iteration count and fixed-point or deterministic float order (Burst). Cost: hundreds to thousands of iterations; fine at 256² offline, maybe a compute shader later.
- **Islands status:** missing (no `reaction`/`turing`/`gray-scott`/`activator` hits; the one hit in `StageVegetation2DTests.cs` is an unrelated comment). Vegetation today = global quantile of fBm, biome-aware, with `moistureModulation` = 0 by default (`Stage_Vegetation2D.cs`).
- **Where it could land:** `Runtime/Noise` as a new field generator (or a PCG operator `ReactionDiffusionOps2D`); a NoiseLab v2 panel (spots ↔ stripes slider); `Stage_Vegetation2D` per-biome pattern mode for arid/savanna biomes. Web Islands Lab: a Turing-patterns part pairs well with the noise series.
- **Value for Islands:** medium-high. A genuinely new, cheap-to-prototype generator that gives realistic dryland vegetation structure and directly expresses emergence.
- **Other projects:** web personal (Labs explainer: the fire/firefighter analogy is a ready-made layman framing); perfil (emergence as favourite phenomenon; Turing biography connects with the Matilda-style "ignored science" theme only loosely, skip).

### Creating a Realistic Fantasy Map | Fantasy World-Building — The Fantasy Forge (25:59)
- **URL:** https://youtu.be/HawxK2FWm3M
- **Source note:** full transcript read (English US captions). Found via search, channel confirmed as The Fantasy Forge.
- **Core idea:** A hand-made workflow for a D&D map: coastline outline → tectonic plate lines → mountains along plate collisions → rivers from mountains to sea → forests by water and away from cities → climate bands by equator → deserts and tundra/taiga → fjords in the cold north → landmarks → borders following natural features. Everything should have a reason.
- **Natural rules (with minute marks):**
  - 1:13 Random dice/noodles outlines are fine for quick maps, but a planned layout feels real.
  - **3:14–3:39 Mountains form where plates push into each other and buckle upward.**
  - **4:37–6:17 Draw plate boundaries first, then decide whether each pair converges or diverges. Islands can be pieces rifted off by a moving plate (5:42). Mountain ranges go along convergent boundaries. The result reads as realistic ranges with valleys between them (6:41).**
  - 7:39–7:51 Extending a plate line predicts where mountains appear on new islands.
  - **8:12–8:32 Rivers start in mountains, flow downhill, end in a lake or the ocean. They never cross a range.** 9:24 A rift or torn area can itself host a river.
  - 10:28–10:47 Rivers make natural country borders.
  - **11:24–11:43 Wild forests form near water (rivers, coasts). Areas around capitals and big cities are cleared for farming. Wilderness remains between cities.**
  - 12:31 A valley between ranges gathers seeds and life: a good place for a large forest.
  - 14:58–15:21 The equator line sets warm/cold bands. 16:46–16:59 Deserts are placed partly by latitude (the creator places arid land toward the south pole, a fantasy choice). 17:12–17:24 A tundra/taiga line in the north.
  - **19:12–19:27 Cold north: glaciers carve fjords, so the coastline gets a "Norwegian" look.**
  - **21:32–21:42 Borders follow coastline, mountain ranges, walls and rivers.** 6:53 Countries split by ranges develop differently from open-plain ones.
- **Generation rules for Islands:**
  - **Plates → ridges:** same as the Algol card. A `Stage_Tectonics2D` from Voronoi plates with convergence-driven uplift. This video gives the authoring order: plates come **before** height noise.
  - **Rivers never cross ridges:** already true with Priority-Flood + D8 (part A). Validator idea: no river cell's flow path climbs above its source's ridge (sanity check, already implied by D8).
  - **Forest = water proximity minus settlement pressure:** turn on `moistureModulation` (exists, 0 by default) so river/coast moisture raises vegetation, and add a later settlement/clearing mask (cells within R of settlements lose vegetation). Settlements are not in Islands yet, so this lands in mundo-islas or a future stage.
  - **Valley forests:** low cells enclosed by hills (`Stage_Hills2D`, `Stage_Morphology2D`) get a vegetation bonus.
  - **Fjords as a high-latitude coast operator:** for coastal cells whose temperature is below T and whose slope is steep, carve narrow inlets along downhill/valley directions. Missing (part A); this video confirms the rule (cold + mountains + coast).
  - **Natural borders:** `Stage_Regions2D` partitions by biome. A political-region pass could use rivers and ridges as cost barriers (watershed/flood fill with river and ridge edges as walls). Inspection metric: share of border length on rivers or ridges.
- **Islands status:** tectonics missing; ridged noise exists (`Noise.cs`); rivers downhill exist (part A); moisture → vegetation exists but inert (`Stage_Vegetation2D.cs`, `moistureModulation = 0.0f`); fjords missing; biome regions exist (`Stage_Regions2D.cs`, 4-way CCA), political borders missing; latitude bands exist (part A).
- **Where it could land:** `Stage_Tectonics2D` (new) → `Stage_BaseTerrain2D`; turn `moistureModulation` on in a preset and measure; a fjord operator in `Stage_Shore2D`; political regions downstream in mundo-islas.
- **Value for Islands:** medium. Lay advice, but it states the authoring order (plates → mountains → rivers → forests → climate → borders) that Islands mostly follows, minus the plate step. Cheapest win: enable moisture-driven vegetation.
- **Other projects:** videojuego-colonizacion (borders on rivers and ridges, cleared land around the outsiders' towns vs. untouched forest; sacred forest vs. timber); mundo-islas (settlement clearing mask, countries).
