# No Man's Sky: World Generation Pipeline
### A Technical Reconstruction — Pipeline Series Vol. 3
*Follows prior reports on Dwarf Fortress (14 stages) and Minecraft Java 1.21 (12 stages)*

---

## Preface & Research Transparency

This document synthesises information from the following source tiers, each cited throughout:

| Tier | Label | Sources |
|------|-------|---------|
| 1 | **[CONFIRMED]** | Hello Games official patch notes; Sean Murray GDC 2017 "Building Worlds Using Math(s)"; Innes McKendrick GDC 2017 "Continuous World Generation in No Man's Sky"; Innes McKendrick nucl.ai 2015 talk |
| 2 | **[INFERRED]** | NMS modding wiki (VoxelGeneratorSettings datamine); community reverse-engineering; Portal Repository coordinate analysis; NMS Fandom wiki technical pages |
| 3 | **[SPECULATIVE]** | Community blog reconstructions; forum analysis; implementation reasoning from observable behaviour |

The GDC Vault videos are paywalled, so their content is cited through secondary summaries (Gamedeveloper.com, procedural-generation.isaackarth.com, and direct attendee reports). Official patch notes from Hello Games are the primary confirmed source for update-specific changes.

---

## A. Executive Summary

No Man's Sky generates an 18-quintillion-planet universe (2⁶⁴ planets) entirely on-demand, with no planet data stored server-side — every world is re-derived from a deterministic seed each time a player approaches it. The pipeline operates at two temporal scales:

**Offline / structural generation** (no player involvement): A galaxy is divided into a 3D voxel grid of regions. Each region contains hundreds of star systems. Each system is assigned a type (by star colour class), planet count, and per-planet seeds — all derived hierarchically from positional coordinates. No terrain is generated until someone visits.

**On-demand / streaming generation** (triggered by player proximity): When a player warps to a system, a progressively detailed planetary model is assembled in real time. From space, planets render as low-poly spheres with atmosphere shaders. As the player descends, an octree-based voxel LOD system generates increasingly fine terrain geometry in concentric shells around the player. At surface level, full marching-cubes meshing, biome prop placement, resource deposit generation, and cave volume generation all stream in continuously as the player moves.

The terrain itself is produced by layering multiple **"Uber noise"** functions — coherent gradient noise (similar to Perlin/Simplex) combined with turbulence passes, domain warping, and featurespecific noise layers for caves, pillars, and resource deposits. These layers are parameterised by a **biome type** selected at planet-seed time from a weighted probability table that depends on star colour and galaxy type.

The result is a pipeline with no persistent world state: every rock, cave, ocean, and mountain is a pure function of its coordinate and the planet seed. This is the deepest architectural difference from Dwarf Fortress (fully simulated, stateful world history) and Minecraft (hybrid: pre-generated chunk data written to disk after first visit).

---

## B. Step-by-Step Pipeline

### Stage 1 — Galaxy Structure & Region Grid

**What is generated:** The galaxy is organised as a discrete 3D voxel grid of *regions*. Each region is a spatial cell that contains up to ~512–600 star systems. The galaxy has a defined shape (a disk with spiral arms, a denser core, and an empty zone of ~3,000 light-years at the galactic centre). There are 256 numbered galaxies in NMS; the player begins in Euclid (Galaxy 1).

**Inputs:** Fixed galaxy-type enum (Euclid is "normal"; other types include Lush, Empty, Harsh, Imperfect, Ancestral, Raging — each a named constant, not derived from a seed). Galaxy index number.

**Outputs:** Galaxy-type classification, which modifies biome probability tables for yellow-star systems in that galaxy. A conceptual 3D region grid spanning approximately 0x000–0xFFE on X and Z axes and 0x01–0xFF on Y (the height axis).

**Why here:** Galaxy type must be known before any planet biome probabilities can be computed. It is the outermost constraint in the generation hierarchy. **[CONFIRMED — wiki biome probability tables cross-referenced with datamined BIOMEFILENAMES.MBIN]**

**Source:** NMS Fandom wiki (Biome, Star System, Galactic Coordinates articles); datamined biome probability tables.

---

### Stage 2 — Region Seed & Star System Placement

**What is generated:** Within each region (identified by 3-digit hexadecimal X, Y, Z coordinates), individual star systems are placed. A region contains a Solar System Index (SSI) — a value that identifies which of the hundreds of possible positions within a region a given star system occupies. The SSI can range from 0x000 to 0x2FF for normal systems, with purple-star systems (added in Worlds Part II) using IDs 0x3E8–0x429.

**Inputs:** Region coordinates (YY-ZZZ-XXX in portal notation), Solar System Index.

**Outputs:** A unique positional address for each star system, which combined with the region coordinates forms the full 12-glyph portal address [P][SSS][YY][ZZZ][XXX].

**Order rationale:** System position must be determined before any system properties can be seeded, because the spatial coordinates are the primary seed input for the star system's generator.

**Note on determinism:** The 64-bit planet seed is almost certainly derived from this coordinate-plus-index address, not stored independently. Two players visiting the same portal address will encounter the same world because they are feeding the same input into the same deterministic function. **[INFERRED — community coordinate analysis, Steam forum reconstruction]**

---

### Stage 3 — Star Type Selection

**What is generated:** Each star system is assigned a spectral class and colour, which determines which resources its planets carry, which biome probability weights apply, and which drive upgrades are required to access the system.

| Star Class | Colour | Rarity | Stellar Resource | Biome Exotic Odds |
|---|---|---|---|---|
| F / G | Yellow | Most common | Copper / Activated Copper | Standard (×1 in normal galaxies) |
| M / K | Red / Orange | Uncommon | Cadmium / Activated Cadmium | Highest for exotic (×3) |
| E | Green | Rare | Emeril / Activated Emeril | Moderate (×1) |
| B / O | Blue | Rare | Indium / Activated Indium | High (×2) |
| X / Y | Purple | Added Worlds II; rare | Quartzite / Activated Quartzite | Variable; new terrain rules apply |

Binary and ternary configurations were added in Origins (3.0). Outlaw systems were added in Outlaws update; Dissonant systems in Interceptor.

**Inputs:** System seed (derived from coordinates). Galaxy type.

**Outputs:** Star colour class, which feeds into planet count, biome probability weights, and resource assignment.

**[CONFIRMED — NMS wiki Star System article, official patch notes for Origins and Worlds Part II]**

---

### Stage 4 — Planet Count & Orbital Assignment

**What is generated:** Each system receives 1–6 planetary bodies (planets and moons). Moons always follow their parent planet in the Planet Index ordering — a planet is assigned an index, and its moons receive immediately subsequent indices. In Worlds Part II, gas giants were added as a new planet class for purple systems; gas giants have their own moon sets.

The Planet Index (the first glyph in a portal address) encodes order of proximity from the space station entry point. This is not a physical orbital simulation — there are no Keplerian mechanics. The "orbital distance" is a narrative / gameplay ordering, not a physically computed value.

**Inputs:** System seed, star type, galaxy type.

**Outputs:** Planet count (1–6), per-planet index, designation as planet or moon, and the Planet Index value in the portal address.

**[CONFIRMED — portal address wiki; community portal address analysis]**

---

### Stage 5 — Planet Seed Assignment & Planet Type Classification

**What is generated:** Each planet receives its own seed, derived from the system seed and planet index. From this planet seed, the generator derives the planet's biome type (the dominant environmental category), size, atmospheric density, and hazard level.

**Biome classification** is a weighted random draw from a probability table. The weights depend on:
- Star colour class (red stars produce 3× more exotic planets than green, 1.5× more than blue)
- Galaxy type (Lush galaxies give yellow stars 4× higher lush-planet odds; Harsh galaxies mirror Normal for yellow stars but with more extreme weather)
- A global "Convert Dead To Weird" factor (0.5), introduced in the Visions update, which converts some Dead planets to Exotic — so the effective exotic frequency is higher than the base tables suggest

**Primary biome types** (as of Worlds Part II):
Lush, Barren, Scorched, Frozen, Toxic, Radioactive, Dead, Marsh/Swamp (subtypes of Lush added in Origins), Exotic (sub-typed: Glitch, Fractured, Cabled, Columned, etc.), Mega Exotic (Red/Green/Blue chromatic variants), Volcanic (Origins subtype of Scorched), Gas Giant (Worlds II, purple systems only), Waterworld (Worlds II, purple systems only).

Within each biome, a **biome subtype** (approximately 10 per biome for most categories) further specifies resource variants, flora density, and descriptor text.

**Terrain type** (water distribution pattern) is selected independently from biome:
Pangean (no water), Continental (ocean and landmasses), Riverland (ravine water, no ocean), Wetlands (shallow, widespread water), Swamp (landlocked pools), Archipelago (island chains, high ocean fraction).

**Terrain archetype** (topological shape category) is also selected independently. Ten archetypes are identified by the community (Caverns, Spires, Craters, Pillars, Flat Plains, Canyons, etc.). Post-Origins, newly generated planets always use the new terrain generation system; pre-Origins planets were never regenerated. **[CONFIRMED for biome types and update history; INFERRED for seed derivation details]**

---

### Stage 6 — Atmosphere Generation

**What is generated:** Each planet receives an atmospheric composition, sky colour, and weather type. The atmosphere is not a physically simulated layer — it is a set of scalar parameters that drive rendering shaders and gameplay hazard timers.

Key parameters generated at this stage:
- **Hazard type**: toxic, radioactive, heat, cold, radioactive, or none (Dead planets have no atmosphere hazard; Lush typically has none or minimal)
- **Hazard intensity**: mild / standard / extreme — drives the rate at which the player's hazard protection depletes
- **Sky colour**: derived from planet seed; colour diversity was greatly increased in Origins and Worlds Part I
- **Weather type**: Clear, Rainy, Blizzard, Firestorm, Toxic Storm, Radioactive Storm, etc. These are not simulated weather systems — they are state machines that toggle between clear and extreme states on a timer with some randomness
- **Atmospheric resource**: each biome has an atmosphere-specific resource harvestable by atmospheric processor

Post-Origins: weather became **localised** rather than planet-wide, and new weather events (tornadoes, meteor showers) were added. Post-Worlds Part I: cloud coverage, sky colour variation, night-time darkness, and storm visual intensity were dramatically expanded. Post-Worlds Part I: a full wind simulation system was introduced, creating consistent wind direction and speed per planet that affects foliage, particles, and waves. **[CONFIRMED — official patch notes Origins 3.0, Worlds Part I 5.0]**

---

### Stage 7 — Planet-Level Noise Setup (The Terrain Generator Parameterisation)

**What is generated:** This is where the terrain generation system is configured for a specific planet — the set of noise parameters that will drive all terrain generation on that world. This stage does not generate geometry; it selects and configures the mathematical functions that will be evaluated on demand later.

The configuration is structured around a file called `VoxelGeneratorSettings` (datamined from `METADATA\SIMULATION\SOLARSYSTEM`). It is an array of 10 `TkVoxelGeneratorSettingsElement` parent entries. Within these, three categories of noise configuration are used:

**1. NoiseLayers (7 × TkNoiseUberLayerData):**
Each entry corresponds to a biome-specific noise role:
- `Base` — general terrain shape; the dominant height/density function
- `Mountain` — sharp high-frequency detail; not always mountains per se, but steep features
- `UnderWater` — modifies density below the water table
- Additional layers for biome-specific surface treatments

Each `TkNoiseUberLayerData` layer contains settings for frequency, amplitude, octave count, persistence, and a lacunarity-like parameter that controls how fine-detail layers are weighted. This is confirmed fractal Brownian motion (fBm) — multiple octaves of gradient noise summed with geometric amplitude falloff. **[INFERRED — modding wiki datamine of VoxelGeneratorSettings]**

**2. GridLayers (9 × TkNoiseGridData):**
These add feature-scale variation on top of the base noise. Each has a child `TurbulenceNoiseLayer` (a single additional noise layer used for domain warping). The specific roles confirmed by modding community:
- Large terrain columns / pillars
- Floating islands (when HeightOffsets are set to push density above baseline; currently set to inactive in vanilla but modifiable)
- Resource deposit locations (Heridium, Iridium, Copper, Nickel, Gold, Emeril)

**3. Features (7 × TkNoiseFeatureData):**
Point-scale features added on top of the noise field — volcanic craters, spire bases, arch roots, etc. These appear to operate as additive signed-distance functions blended into the main density field.

**4. CavesUnderground (TkNoiseCaveData):**
A separate underground noise configuration controlling cave void generation (see Stage 11).

**Why here:** The noise parameterisation must be established before any voxel evaluation occurs, because every voxel sample calls these functions. Changing this configuration changes the entire surface. **[INFERRED from modding wiki datamine; CONFIRMED that noise layering is the fundamental approach — Sean Murray GDC 2017]**

---

### Stage 8 — Terrain Height Field & Topology Generation (Voxel Density Function)

**What is generated:** The actual terrain geometry. NMS uses a **volumetric signed density field** (not a heightmap). Every point in 3D space has a density value: positive density = solid rock; negative density = air. The surface of the terrain is the isosurface at density = 0. This volumetric approach is what enables overhangs, arches, caves, floating islands, and all non-heightmap terrain features.

**The sphere-cube projection problem:** Planets are spherical, but noise functions are naturally Cartesian. The confirmed solution (from Innes McKendrick's nucl.ai 2015 presentation) is:
- Voxel regions are conceptually organised as **octrees on cubes** — a cube-map structure in 6 face orientations
- These cubes are **projected onto a sphere** — the spherification step, so terrain generation happens in local spherical space
- However, the voxels themselves are **stored as flat arrays** within each octree node — efficient linear memory layout

This means the coordinate transformation from spherical surface to local Cartesian noise evaluation happens transparently at the function call level. The noise is evaluated in local planet-face space, not global Cartesian space, avoiding the polar distortion problem. **[CONFIRMED — nucl.ai 2015 talk summary, procedural-generation.isaackarth.com]**

**The density function structure (partial reconstruction):**
```
density(x, y, z) =
  sphereField(x, y, z, planet_radius)           // baseline planet sphere
  + fBm_Base(warp(x,y,z), octaves, freq)        // primary terrain shape
  + fBm_Mountain(x,y,z) * mountain_mask         // steep features
  + fBm_UnderWater(x,y,z) * (y < sea_level)     // underwater shaping
  + gridFeature(x,y,z)                           // pillar / column additive
  + featureSDFs(x,y,z)                           // arch / crater SDFs
  - caveVoids(x,y,z)                             // carved cave channels
```
The `sphereField` term is simply `planet_radius - distance_from_center`, which produces a solid sphere as the baseline. All terrain features are additive or subtractive modifications to this baseline density. **[SPECULATIVE SYNTHESIS — consistent with all confirmed information but the exact implementation is unverified]**

**Marching Cubes:** The density field is sampled on a 3D grid and converted to a surface mesh using the **marching cubes** algorithm. Each voxel cube is tested against the 256-case lookup table to determine which triangle configuration approximates the local isosurface. In Worlds Part I (2024), Hello Games rewrote terrain generation to use **dual marching cubes** — a variant that places vertices at the point of minimum quadric error rather than on grid edges, producing fewer vertices, smoother results, and better-captured sharp features. This change increased terrain generation speed and improved framerate. **[CONFIRMED — Worlds Part I patch notes]**

Importantly, this mesh generation runs on the **CPU**, not the GPU. Community analysis of NMS's performance characteristics confirmed this, noting that the game achieves very fast terrain transitions CPU-side. **[INFERRED — community analysis, cited by Nick's Voxel Blog]**

---

### Stage 9 — Biome Placement & Surface Material Selection

**What is generated:** Surface material (textures, colours, palette), flora prop types and density rules, and the mapping from terrain features to surface appearance. There are no "biome regions" within a single planet in the traditional sense — each NMS planet is entirely one biome type. The biome was selected at Stage 5.

What varies *within* a planet:
- **Altitude-based material blending**: rock colour and texture blends from lowland to mountain. The `Mountain` noise layer partially controls where "rocky" material appears vs. soil/grass.
- **Slope-based blending**: steep slopes typically show bare rock regardless of biome. Shallow slopes show the biome's ground cover.
- **Colour palette**: derived from the planet seed. Grass, soil, rock, and sky each receive procedurally chosen hues. Origins and subsequent updates expanded the palette range dramatically.
- **Flora prop rules**: each biome has tagged prop templates (trees, grasses, rocks, crystals) with rules for preferred altitude bands, slope limits, and density ranges. These are applied as the player streams in at ground level (Stage 13).

The texturing system is notable for working at multiple scales simultaneously — it must look correct from orbit (a large colour palette texture projected onto the sphere mesh) and from 1-metre walking distance (high-resolution tiling with detail normal maps). This multi-scale texturing challenge was specifically called out in Hello Games' technical talks. **[CONFIRMED — McKendrick GDC 2017 summary mentions texturing as a major challenge; INFERRED for implementation specifics]**

---

### Stage 10 — Water Body & Ocean Generation

**What is generated:** The water table is a scalar height — a simple global sea-level value per planet. Any terrain below this level is considered underwater. The terrain type (Continental, Wetlands, etc.) determines how high this sea-level sits relative to the median terrain height.

Pre-Worlds Part I: water was rendered as a flat plane with a normal-mapped surface shader. Waves were simulated by the shader (fake).

**Worlds Part I (2024) overhaul [CONFIRMED]:** Water rendering was completely replaced with a **mesh-based system**:
- True wave geometry is generated (not just shader-faked)
- Waves respond dynamically to weather and wind conditions
- Wave height and foam intensity vary with water depth (shallow areas = spiky breaking waves; deep ocean = long rolling swells)
- Ships leave real splashes and trails
- Water colour varies per planet
- Ships can now land on water (previously impossible due to the flat-plane collision model)

**Worlds Part II (2025) further enhancements [CONFIRMED]:**
- Significantly deeper oceans possible (previously ~200 units depth; waterworlds in purple systems can now be "kilometres deep")
- Underwater rendering reworked with light scattering, underwater crepuscular rays, caustics
- Rain generates visible surface ripples
- Water can become "perfectly still" in rare calm conditions
- Clouds, nebulae, and terrain now reflected in water surface

The water level is determined before terrain is meshed, but the interaction between terrain shape and water is a rendering concern, not a generative feedback: the terrain generator does not know about the sea level; it simply generates the density field, and the renderer clips or blends the ocean mesh against it. **[INFERRED — consistent with voxel architecture; water as a separate system]**

---

### Stage 11 — Cave & Underground Volume Generation

**What is generated:** Underground void volumes (caves, tunnels, caverns). In NMS, caves are a subtracted component of the terrain density field — not a separate "cave carving" pass, but additional terms in the main density function that push density negative in tunnel-like shapes.

The `CavesUnderground` configuration block in `VoxelGeneratorSettings` contains `TkNoiseCaveData` entries. The turbulence noise layer in each `TkNoiseGridData` entry also influences cave column shapes and underground resource pillars.

Cave generation creates:
- **Horizontal tunnel networks**: created by noise functions that produce density inversions (hollow tubes) at certain depths
- **Large cavern chambers**: the Caverns terrain archetype specifically produces planet-wide enormous caves — the noise parameters for this archetype set extreme amplitudes for cave-cutting functions
- **Underground overhangs and sea-floor caves**: underwater terrain shape (Stage 8's `UnderWater` noise layer) creates underwater caves and overhangs visible from within the ocean

In practice, cave generation is the subtraction of a second noise field from the main field. The second field uses different frequency parameters that produce the characteristic curved-tube topology of cave passages. Because this is just more density field evaluation, it streams in at the same time as surface terrain — there's no separate cave generation pass. **[INFERRED — modding wiki datamine of TkNoiseCaveData; consistent with density field architecture]**

---

### Stage 12 — Resource & Mineral Distribution

**What is generated:** The locations and types of mineable resource deposits, scattered minerals, and harvestable plants. Resources are of several types:

1. **Stellar resources** (Copper, Cadmium, Emeril, Indium, Quartzite): determined by star colour (Stage 3). Found as surface and underground rock formations. Distribution shaped by the GridLayer turbulence noise — confirmed entries in `VoxelGeneratorSettings` include `Resources_Heridium`, `Resources_Iridium`, `Resources_Copper`, `Resources_Nickel`, `Resources_Gold`, `Resources_Emeril`. These correspond to density-field additive features that create exposed resource nodules. **[INFERRED — modding wiki datamine of VoxelGeneratorSettings GridLayer entries]**

2. **Biome agricultural resources** (Star Bulb, Frost Crystal, Cactus Flesh, etc.): placed as props (surface decorations) driven by the biome prop placement system. These are not terrain features but object instances.

3. **Atmospheric resources**: not placed in the world; harvested via atmosphere processors at any surface location on the planet.

4. **Storm crystals**: rare glowing minerals that appear only during extreme weather. These are a conditional prop spawn, not a permanent terrain feature.

5. **Underwater mineral seams**: added in Worlds Part II — large resource deposits can appear underwater on all planets (not just purple systems).

Resource placement must occur after terrain generation because resources are placed on or near the terrain surface. The turbulence noise layers in the density field create the physical shape of the resource protrusions; the resource type is assigned based on which `TkNoiseGridData` entry is active for a given planet's star type. **[INFERRED]**

---

### Stage 13 — Points of Interest Placement (Brief — Geographic Context Only)

**What is generated:** Structures, anomalies, settlements, and portals are placed using a separate spatial scattering system (not the terrain density field). Points of interest are:
- Guaranteed: every planet has exactly one portal
- Probabilistic: trading posts, abandoned buildings, crashed ships, monoliths, ruins — their presence and density depends on biome, sentinel activity level, and system inhabitedness
- Geographic constraint: POIs are placed on the terrain surface (not underground) and avoid extreme slopes

POI placement is a separate layer that reads the generated terrain surface to find valid spawn locations. It is not part of the noise-based terrain pipeline. Settlements (added in the Frontiers update) function similarly — they seed a location on the terrain and build around it. **[CONFIRMED — wiki, observable game behaviour; INFERRED for placement algorithm details]**

---

### Stage 14 — Streaming & LOD Pipeline (Player Approach)

**What is generated / loaded:** As the player moves from hyperspace → system space → planetary orbit → atmosphere → surface, the engine progressively commits more computational resources to terrain detail.

**Phase 1 — System Space / Far Orbit:**
The planet renders as a low-polygon sphere (a smooth displaced sphere, not the voxel surface) with:
- Atmosphere scattering shader (giving the glowing halo)
- A projected colour texture representing the biome palette
- Cloud coverage texture (post-Worlds Part I: full volumetric clouds visible from space, matching actual surface weather state)
- No terrain geometry whatsoever at this distance

**Phase 2 — Close Orbit / Atmospheric Approach:**
The planet's octree LOD system activates. Outer octree nodes at the lowest detail level begin generating the large-scale shape of the terrain — broad continental outlines, mountain ranges at coarse resolution. The voxel density function is evaluated at low sample frequency. The resulting mesh is very coarse (~tens of metres per voxel equivalent).

**Phase 3 — Atmosphere Entry:**
The game transitions from the spherical planet model to the ground-relative coordinate system. The origin is effectively "locked" to the player to maintain floating-point precision (a standard technique for planetary-scale rendering). Atmosphere entry effects (heat shield glow, turbulence effects — redesigned in Worlds Part II) play here. Mid-detail LOD nodes generate.

**Phase 4 — Low Altitude / Landed:**
The innermost LOD shells are now active. Full-resolution voxel density is evaluated in a radius around the player. The marching cubes mesh is generated at ~1-metre-equivalent resolution near the player, transitioning to coarser resolutions at distance. Flora props stream in: grasses, trees, rocks, and mineral deposits are placed procedurally within render distance. Underground caves are generated simultaneously with surface terrain — they are part of the same density function.

The octree structure means that:
- A region covering 1 km at LOD 0 is subdivided into 8 sub-regions at LOD 1, 64 at LOD 2, 512 at LOD 3, etc.
- Only nodes near the isosurface (where terrain actually exists) are fully evaluated
- Empty space and deeply solid rock are culled early without meshing

**Key confirmed architectural point:** Terrain generation runs **on the CPU**, not the GPU. The GPU handles rendering. Community analysis found that the per-octree-node design allows very fast generation by keeping each node to a fixed-size 3D array regardless of LOD level, avoiding the exponential cost blowup that naive implementations suffer. **[CONFIRMED — McKendrick GDC 2017 "continuous world generation, from voxel-based world generation through polygonization and texturing"; INFERRED for LOD hierarchy details]**

---

## C. Pipeline Dependency Map

```
Galaxy Type (constant per galaxy)
    │
    ▼
Region Grid (XYZ voxel address)
    │
    ▼
Star System Seed (derived from region coords + SSI)
    │
    ├──▶ Star Type / Colour Class
    │         │
    │         ├──▶ Stellar Resource Type (Copper / Cadmium / Emeril / Indium / Quartzite)
    │         └──▶ Biome Probability Weight Modifier
    │
    ├──▶ Planet Count (1–6)
    │
    └──▶ Per-Planet Seed (system seed × planet index)
              │
              ├──▶ Biome Type Selection (weighted random, uses star type + galaxy type weights)
              │         │
              │         ├──▶ Biome Subtype
              │         ├──▶ Hazard Type & Intensity
              │         ├──▶ Flora/Fauna Template Sets
              │         └──▶ Agricultural & Atmospheric Resource Types
              │
              ├──▶ Terrain Type (Pangean / Continental / Wetlands / etc.)
              │
              ├──▶ Terrain Archetype (Caverns / Spires / Craters / etc.)
              │
              ├──▶ VoxelGeneratorSettings Parameter Block
              │         │
              │         ├──▶ NoiseLayers (Base, Mountain, UnderWater, ×7)
              │         ├──▶ GridLayers (Pillars, Islands, Resources ×9)
              │         ├──▶ Feature SDFs (×7)
              │         └──▶ CavesUnderground (TkNoiseCaveData)
              │
              ├──▶ Atmosphere Parameters (sky colour, weather type, cloud density)
              │
              ├──▶ Water Table Height (→ determines ocean/wetland extent)
              │
              └──▶ Colour Palette (ground, sky, water, flora tints)
                        │
                        ▼
              [PLAYER APPROACHES]
                        │
                        ▼
              Octree LOD Node Activation (distance-driven)
                        │
                        ▼
              Voxel Density Function Evaluation
              (sphereField + fBm_Base + fBm_Mountain + GridFeatures + CaveVoids)
                        │
                        ▼
              Dual Marching Cubes Meshing (Worlds Part I+)
                        │
                        ├──▶ Surface Material Assignment (altitude + slope blending)
                        ├──▶ Resource Deposit Geometry (from GridLayer turbulence)
                        ├──▶ Cave Void Geometry (from CavesUnderground)
                        └──▶ Water Mesh Generation (dynamic waves, Worlds Part I+)
                                    │
                                    ▼
                        Flora/Prop Streaming (biome-tagged templates placed on surface)
                                    │
                                    ▼
                        POI Placement Validation (terrain surface scan for valid locations)
```

---

## D. Geographic Subsystems (Deep Dives)

### D.1 — Seed Handling & Determinism

The entire universe is deterministic from coordinates alone. The key insight is that the **portal address IS the coordinate IS the seed**. There is no separate planet seed table — the seed is computed on the fly from the address.

Portal address structure: **[P][SSS][YY][ZZZ][XXX]**
- **P** (1 glyph, 0–F) = Planet Index within the system
- **SSS** (3 glyphs, 000–2FF normal / 3E8–429 purple) = Solar System Index within the region
- **YY** (2 glyphs, 01–FF) = Y coordinate (height in galaxy), origin at galactic centre
- **ZZZ** (3 glyphs, 001–FFF) = Z coordinate (galactic width)
- **XXX** (3 glyphs, 001–FFF) = X coordinate (galactic length)

The coordinate origin differs between the Galactic Coordinates system (corner-based, Alpha Minoris = 0000:0000:0000) and the Portal Coordinates system (galactic-centre = 0,0,0). Tools like the NMS Portal Decoder convert between the two.

The implication of coordinate-as-seed: the same 12-glyph address always produces the same planet, on any platform, in any game mode, on any playthrough — within the same galaxy number. Addresses from Euclid (Galaxy 1) do not work in Hilbert Dimension (Galaxy 2) because the galaxy number is an implicit additional seed modifier. **[CONFIRMED — community portal coordinate research, NMS Portal Decoder tool]**

Planet seeds are described by the community as 64-bit integers (2⁶⁴ = 18,446,744,073,709,551,616 — matching the advertised planet count). **[INFERRED — consistent with advertised number; not officially specified]**

---

### D.2 — Noise Functions

**Confirmed:** NMS terrain uses layered noise (Sean Murray GDC 2017 explicitly describes this; Innes McKendrick's talks use the term "Uber Noise" for the layered configuration system). The base noise type is coherent gradient noise — consistent with Perlin or Simplex noise as described in community analyses.

**The "Uber Noise" system** (confirmed naming from developer materials): a composable noise system where multiple noise generators can be stacked, each with its own frequency, amplitude, octave count, persistence, and domain-warp parameters. The `TkNoiseUberLayerData` structure confirmed in the modding datamine is the serialised form of this system. Each layer is an independently parameterised fBm (fractal Brownian motion) generator.

**Domain warping:** The `TurbulenceNoiseLayer` found in each GridLayer entry is the mechanism for domain warping — using one noise function to perturb the input coordinates of another. This is the technique described by Inigo Quilez as "domain warping" and produces the characteristic swirling, eroded appearance of natural terrain features. In NMS, this is used specifically for pillar/column features and floating island shapes. **[INFERRED — modding wiki datamine; SPECULATIVE that "turbulence" = domain warping per standard noise architecture]**

**The Superformula:** Multiple sources describe NMS using a "superformula" for terrain shapes. The superformula (Gielis, 2003) is a generalisation of the circle equation that can produce a wide variety of 2D and 3D shapes by varying six parameters:
```
r(θ) = (|cos(mθ/4)/a|^n2 + |sin(mθ/4)/b|^n3)^(-1/n1)
```
This appears to be used in NMS for generating the overall profile/envelope of terrain features (mountain shapes, rock formations, exotic biome structures) rather than the fine noise detail. It provides the "alien" organic silhouette variety without requiring artist-modelled assets. **[INFERRED — confirmed that NMS uses the superformula per procedural generation community sources; exact integration with the noise pipeline is unclear]**

---

### D.3 — Planet Type Classification & Atmosphere Generation

See Stages 5 and 6 above for the detailed breakdown. Key architectural points:

- Biome and terrain archetype are **independent selections** from the same planet seed. A Frozen biome planet can have any of the 10 terrain archetypes.
- The biome determines *what*, the terrain archetype determines *the shape of what*
- Atmosphere is purely parametric (no fluid simulation), but became much richer visually in Worlds Part I (volumetric clouds, dynamic weather localisation, wind simulation)
- Star colour directly influences biome probability — this is the strongest geographic consequence of the star system tier

---

### D.4 — Terrain Height & Topology

The fundamental choice of a **volumetric density field** rather than a heightmap is what distinguishes NMS terrain architecture from most open-world games. A heightmap (H(x,z) = y) cannot represent overhangs, arches, caves, or floating islands — all of which are prominent in NMS. The density field (ρ(x,y,z) = value) supports arbitrary 3D topology.

The cost is computational: evaluating a 3D density function is O(n³) in resolution, versus O(n²) for a heightmap. NMS manages this through the octree LOD system — high resolution is only evaluated in the small shell around the player.

**Terrain scale evolution:**
- Launch (2016): terrain height range relatively modest
- Origins (3.0, 2020): mountains now 4× taller than anything previously possible; terrain archetype system introduced
- Worlds Part I (5.0, 2024): dual marching cubes, better efficiency allows denser terrain at same performance
- Worlds Part II (5.50, 2025): further terrain algorithm evolution; deeper valley/plain/mountain variation; anti-repetition improvements (reducing tiling artifacts visible across a single planet); significantly deeper ocean generation in purple systems

**The pre/post-Origins break:** Hello Games explicitly stated that they did not want to regenerate existing planets' terrain in Origins. Their solution was to birth *new* planets in existing systems (1–2 new bodies per system, always the farthest from the space station). Systems that already had 6 bodies before Origins received no new planets. New planets always use the new terrain generation. Old planets retain the old terrain generation forever. This versioned coexistence of two terrain generation systems within the same universe is a remarkable constraint. **[CONFIRMED — Origins patch notes, Sean Murray PC Gamer interview]**

---

### D.5 — Biome Placement & Surface Material

Each planet is one biome — there are no biome transitions within a planet. What varies within a planet is:
- Altitude-driven material blending (soil → rock at high elevation)
- Slope-driven exposure of raw rock
- Prop density variation (flora thinner near mountains, denser in valleys)
- Weather state (localised post-Origins)

This is a major difference from Minecraft (five independent climate axes producing continuous biome gradients) and Dwarf Fortress (continuous temperature/rainfall fields producing emergent biome classification). NMS's biome is a discrete type rather than a position in a multi-dimensional parameter space.

However, the Exotic and Mega Exotic biomes function more like "meta-biomes" — within them, a wide range of visual sub-types (Wire Cell, Hexagonal Bush, fractured rock, etc.) can occur, giving the impression of extreme variety despite a single biome label.

---

### D.6 — Cave & Underground Volume Generation

NMS caves are a natural consequence of the volumetric density field. No separate cave-carving pass exists — caves are simply regions where the density function produces negative values at underground depths.

The 10 terrain archetypes include several specifically cave-focused:
- **Caverns**: vast flat plains with enormous hangar-sized cave networks underneath; a hauler-class ship can fly through them
- **Flat + Caves**: smooth surface terrain concealing massive underground volumes
- **Alpine + Caves**: mountainous surface with cave networks in the flatter areas beneath

The CavesUnderground noise configuration uses different frequency and amplitude parameters from the surface noise — typically lower frequency (longer-wavelength voids) to create tunnel-like channels rather than randomly-distributed voids. The two noise fields are combined in the density function by subtraction: cave noise that is sufficiently negative wins over the positive terrain density. **[INFERRED — modding wiki; consistent with density field architecture]**

---

### D.7 — Water Body & Ocean Generation

See Stage 10 for the complete evolution. Architecturally:

- Water level is a per-planet scalar (a single height value), not a 3D water simulation
- The terrain density field is generated without knowledge of the water level; the engine then floods all terrain below the threshold
- Pre-Worlds Part I: water was a flat render plane — computationally trivial but visually flat
- Post-Worlds Part I: water is a separate mesh-based wave simulation with dynamic geometry, making water a first-class physical object rather than a visual trick
- Post-Worlds Part II: ocean depth now varies dramatically in purple-system planets; underwater exploration is a genuine vertical dimension with pressure hazards

---

### D.8 — Resource & Mineral Distribution

Resources are distributed through two mechanisms:
1. **Density-field additive features** (GridLayer turbulence): creates the 3D geometry of exposed resource veins and deposits. The GridLayer entries in VoxelGeneratorSettings have explicit entries named for specific resources (Copper, Nickel, Gold, Emeril, etc.). The turbulence noise places these additively on the terrain surface.
2. **Prop placement** (biome template system): agricultural plants, crystals, and miscellaneous harvestables are placed as surface objects using the biome's prop rules.

The stellar resource for a system is fixed by star colour. But within a planet, the spatial distribution of that resource is determined by which GridLayer turbulence configurations are active — producing the characteristic vein-like outcrops visible in the terrain. **[INFERRED — modding wiki datamine of GridLayer naming]**

---

### D.9 — Streaming & LOD Pipeline

See Stage 14 for the complete breakdown. Key points for implementers:

- The octree depth is the primary LOD mechanism — each octree node is a fixed-size 3D array (confirmed by community voxel engine analysis)
- Only octree nodes near the density isosurface are meshed; nodes entirely inside solid rock or entirely in air are culled
- The LOD transition is driven by apparent screen-size of a node, not raw distance — so a small detail close to the player triggers LOD refinement at the same time as a large feature farther away
- Flora, props, and POIs stream in as separate passes after terrain meshing, not concurrently with it
- The absence of loading screens between space and surface is achieved by this continuous LOD streaming — there is no discrete "level load" event. The transition feels smooth because the LOD system is always running.

---

### D.10 — Major Update History & What Changed

| Update | Version | Key Generation Changes |
|--------|---------|----------------------|
| Atlas Rises | 1.3 | Portals activated; galaxy coordinate system standardised for player use |
| Next | 1.5 | Mega Exotic biome added; warp drive class system restructured to star colour |
| Visions | 1.75 | Exotic biome subtypes expanded; "Convert Dead to Weird" factor added; colour palette expanded |
| Origins | 3.0 | **Major**: New planets birthed in existing systems; mountains 4× taller; 10 terrain archetypes; localised weather; volcanoes; binary/ternary stars; biome subtypes (marsh/swamp, volcanic); Creature generation reset |
| Frontiers | 3.6 | Settlements added (geographic POI system) |
| Sentinel | 3.8 | Dissonant planets added (new planet subtype with unique fauna) |
| Worlds Part I | 5.0 | **Major**: Dual marching cubes terrain generation; complete water system overhaul (mesh-based waves); volumetric cloud system rewritten; GPU-based flora rendering; wind simulation system; planet variety increase (new biomes within existing categories) |
| Worlds Part II | 5.5 | **Major**: Purple star systems added; new terrain algorithm with deeper variation and anti-repetition; gas giants and waterworlds added; terrain improvements applied only to purple systems (existing planets untouched); significantly deeper oceans in purple systems; underwater rendering reworked |

---

## E. Algorithmic Reconstruction

### E.1 — Galaxy and System Seeding
**Status: Partial reconstruction**

```python
def get_planet_seed(galaxy_id, region_x, region_y, region_z, system_index, planet_index):
    # Combine positional coordinates deterministically
    # Exact hash function unknown; likely some variant of PCG or xxHash
    system_seed = hash(galaxy_id, region_x, region_y, region_z, system_index)
    star_type = weighted_select(STAR_TYPE_TABLE, system_seed, seed_field="star_type")
    
    for i in range(planet_count(system_seed)):
        planet_seed = hash(system_seed, i)
        yield planet_seed

def planet_count(system_seed):
    # 1–6 planets, weighted toward 2–4
    return weighted_int(1, 6, system_seed, seed_field="planet_count")
```

### E.2 — Planet Type Classification
**Status: Mostly confirmed (biome table structure); speculative (exact random function)**

```python
def classify_planet(planet_seed, star_type, galaxy_type):
    # Galaxy type modifies weights for yellow-star systems only
    weights = BASE_BIOME_WEIGHTS[star_type]
    if star_type == YELLOW:
        weights = modify_for_galaxy(weights, galaxy_type)
    
    # Convert Dead to Weird/Exotic with 0.5 probability
    biome = weighted_select(weights, planet_seed, seed_field="biome_type")
    if biome == DEAD and random(planet_seed, "dead_to_exotic") < 0.5:
        biome = EXOTIC
    
    terrain_type = weighted_select(TERRAIN_TYPE_WEIGHTS, planet_seed, seed_field="terrain_type")
    terrain_archetype = weighted_select(ARCHETYPE_WEIGHTS, planet_seed, seed_field="archetype")
    
    return biome, terrain_type, terrain_archetype
```

### E.3 — Voxel Density Function
**Status: Speculative synthesis (consistent with all confirmed information)**

```python
def density(x, y, z, planet_params):
    # Distance from planet centre (in planet-local spherical space)
    dist = length(x, y, z)
    sphere = planet_params.radius - dist
    
    # Convert to surface-relative coordinates for noise evaluation
    # (spherified cube face projection handles this transparently)
    nx, ny, nz = sphere_to_noise_space(x, y, z, planet_params.face)
    
    # Base terrain shape (fBm, ~4–6 octaves)
    base = fbm(nx, ny, nz,
               freq=planet_params.base.freq,
               octaves=planet_params.base.octaves,
               persistence=planet_params.base.persistence)
    
    # Mountain/steep features (domain-warped fBm)
    warp_x, warp_y, warp_z = fbm3(nx, ny, nz, planet_params.turbulence)
    mountain = fbm(nx + warp_x, ny + warp_y, nz + warp_z,
                   freq=planet_params.mountain.freq,
                   octaves=planet_params.mountain.octaves,
                   persistence=planet_params.mountain.persistence)
    
    # Underwater shaping (active below sea level)
    underwater = fbm(nx, ny, nz, **planet_params.underwater) if y < planet_params.sea_level else 0
    
    # Resource deposit features (per GridLayer entry)
    resource_bump = sum(
        resource_feature(nx, ny, nz, res) 
        for res in planet_params.resource_layers
        if res.active
    )
    
    # Cave subtraction (void regions underground)
    cave = cave_density(nx, ny, nz, planet_params.caves)
    
    return sphere + base + mountain + underwater + resource_bump - cave

def cave_density(nx, ny, nz, cave_params):
    # Cave noise produces positive values where voids should exist
    # Subtracted from the main field
    return max(0, fbm(nx, ny, nz, **cave_params) - cave_params.threshold)
```

### E.4 — LOD Octree Evaluation
**Status: Partial reconstruction**

```python
def update_terrain(player_pos, planet_params):
    root_node = get_planet_octree_root(planet_params)
    nodes_to_mesh = []
    
    def traverse(node):
        apparent_size = node.radius / distance(player_pos, node.center)
        if apparent_size < LOD_THRESHOLD:
            # Node is far enough — render at this LOD level
            if node.intersects_surface(planet_params):
                nodes_to_mesh.append(node)
        else:
            # Node is close — subdivide into 8 children
            for child in node.get_or_create_children():
                traverse(child)
    
    traverse(root_node)
    
    for node in prioritised(nodes_to_mesh, player_pos):
        # Sample density at fixed grid resolution per node
        density_grid = [density(x, y, z, planet_params)
                        for x, y, z in node.sample_points()]
        # Dual marching cubes (post Worlds Part I)
        mesh = dual_marching_cubes(density_grid, node.resolution)
        submit_to_render(node, mesh)
```

---

## F. Worked Example — One Planet, Start to Finish

**Scenario:** A player in the Euclid galaxy warps to the system at portal address `1044:0081:0D6D:0038`, then lands on the first planet (Planet Index = 1).

**Stage 1–2: Address parsing**
- Region: YY=0081, ZZZ=0D6D, XXX=1044 (moderate galactic height, mid-galaxy position)
- System Index: 0x0038 = decimal 56 → the 56th system in this region
- Galaxy: Euclid (ID 1) → Normal galaxy type
- System seed: hash(1, 0x1044, 0x0081, 0x0D6D, 56) → some 64-bit value S

**Stage 3: Star type**
- S yields: Yellow star (most probable outcome in Euclid for any given system seed)
- Stellar resource: Copper (and potentially Activated Copper)

**Stage 4: Planet count**
- S yields: 4 planets in this system (index 1=planet, index 2=moon of #1, index 3=planet, index 4=planet)

**Stage 5: Planet type**
- Planet seed P = hash(S, 1) (first planet)
- Star: Yellow; Galaxy: Normal → lush biome has 2× elevated weight
- Draw: Lush biome selected
- Terrain type draw: Continental (large landmasses, oceans)
- Terrain archetype draw: Alpine (tall, steep mountainous terrain with some flat sections)
- Biome subtype draw: "Verdant" (rich grass, moderate trees)

**Stage 6: Atmosphere**
- Hazard: None (lush biome is typically benign)
- Weather type: Rainy (periodic rain, occasional storms)
- Sky colour: Blue-tinted atmosphere with pale yellow sunlight
- Cloud coverage: Medium-high, variable

**Stage 7: Noise parameterisation**
- VoxelGeneratorSettings block: pre-defined parameter set for "Lush/Continental/Alpine" combination
- Base noise: fBm, 5 octaves, moderate frequency → rolling hills as base terrain
- Mountain noise: high amplitude, domain-warped, domain-warping turbulence active → produces the Alpine spires
- UnderWater noise: gentle, lower amplitude → smooth seafloor
- Cave configuration: moderate void amplitude → standard-sized cave networks present
- Resource deposits: Copper (GridLayer entry active for yellow-star copper), plus biome agricultural resources

**Stage 8–9: At player approach**
- Outer LOD: planet renders as a blue-green sphere with cloud texture and atmospheric glow
- Mid LOD: continental outlines visible — blue oceans, green landmasses, white mountain caps
- Inner LOD: individual mountain peaks visible from orbit
- Surface: dual marching cubes mesh generates at ~1m resolution around player; surface shows green grass, scattered trees, rocky outcrops with orange copper veins visible

**Stage 10: Water**
- Sea level set to Continental threshold — approximately 40% of terrain below water
- Post-Worlds Part I: wave mesh generates; during the rain weather state, waves are moderate
- Coastal areas: medium swells; deeper water: long rolling waves

**Stage 11: Caves**
- Cave noise produces tunnels at 10–50 metres depth
- Alpine terrain archetype: caves are present but not the dominant feature (Caverns archetype would produce much larger ones)
- Player discovers a cave entrance in a hillside — the cave network connects to multiple surface exits

**Stage 12: Resources**
- Surface copper veins visible as orange rocky protrusions (GridLayer resource feature placed by turbulence noise)
- Star Bulb plants (biome agricultural resource) scattered in meadow areas at moderate density
- Atmosphere harvester (if built) would yield Nitrogen (lush biome atmospheric resource)

**Stage 13: POIs**
- Portal: placed on a hillside plateau at a valid slope angle
- Trading Post: placed near a flat coastal area
- Monolith: placed on a hilltop with sightlines in multiple directions

**Ground-level experience:** The player walks on green grass, sees mountains through blue fog, hears rain, approaches copper deposits on rocky outcrops, finds a cave entrance, and can dive into oceans with moderate wave action and clear shallow water fading to deep blue.

---

## G. Comparison Section

### G.1 — Seed Architecture

| Dimension | Dwarf Fortress | Minecraft (Java 1.21) | No Man's Sky |
|---|---|---|---|
| Primary seed | Single integer world seed | Single 64-bit world seed | Spatial coordinates (12-glyph portal address) |
| Seed scope | Entire world from one seed | Entire world from one seed | Per-system, per-planet seeds derived from coordinates |
| Multi-world | New game = new seed | New world = new seed | Same universe, 256 galaxies — seeds determined by galactic address |
| Determinism mechanism | Seed → reproducible simulation | Seed → reproducible noise | Coordinates → reproducible density field evaluation |
| "Where is the seed?" | Chosen at world creation | Chosen at world creation | Implicit in the location itself — the universe IS the seed space |

NMS's coordinate-as-seed approach is the most distinctive. There is no "world seed" — the universe is an infinite, pre-committed address space where any location has always had the same content.

---

### G.2 — Noise vs. Fractal vs. Simulation

| System | Primary terrain method | Secondary systems |
|---|---|---|
| Dwarf Fortress | Fractal midpoint displacement (fault-line model) | Six interacting scalar fields simulated forward in time; agent-based erosion |
| Minecraft | 3D density function (fBm noise in all three climate/terrain dimensions) | Five 2D climate noise maps; aquifer placement via noise |
| No Man's Sky | 3D volumetric density field (layered fBm "Uber noise" + domain warping) | Superformula for feature shapes; per-planet parameter block from biome selection |

All three use noise as a foundation. The key differences:
- **Dwarf Fortress** runs simulation *on top of* the initial noise: erosion agents, river carving, soil leaching — the final world is a product of simulated time, not pure function evaluation
- **Minecraft** is pure function evaluation, but uses five independent noise axes to produce a rich implicit multi-dimensional parameter space for biomes
- **NMS** is also pure function evaluation, but with a single combined 3D density field — the "parameter space" is the biome type enum selection, not a continuous multi-dimensional field

NMS has *no simulation whatsoever* in its terrain generation. Every feature is either a mathematical function or a biome-parameterised version of that function.

---

### G.3 — Climate & Biome Classification

| System | Biome classification method | Key inputs |
|---|---|---|
| Dwarf Fortress | Emergent: temperature + rainfall simulation → lookup table | Latitude, altitude, simulated rainfall shadow |
| Minecraft | Implicit: position in 5D noise space (temperature, humidity, continentalness, erosion, weirdness) | 5 independent noise values |
| No Man's Sky | Explicit: discrete weighted-random draw from a probability table | Star colour, galaxy type, planet seed |

Dwarf Fortress and Minecraft both produce *geographically continuous* biome transitions — adjacent regions share climate values and transition smoothly. NMS has **discrete, planet-wide biomes** — an entire planet is one biome type, full stop. There are no gradients between lush and frozen zones on the same world.

This is the single sharpest difference between NMS and the other two. NMS's biome system is a type selection; the others' biome systems are emergent consequences of a continuous parameter field.

---

### G.4 — Cave Generation

| System | Cave method |
|---|---|
| Dwarf Fortress | Pre-simulated: three distinct cave layers generated during world history; agent-based underground rivers |
| Minecraft | Noise caves baked into the 3D density function (cheese voids, spaghetti tunnels, noodle passages as distinct noise configurations) |
| No Man's Sky | Subtracted density field terms; CavesUnderground noise configuration per-planet |

NMS and Minecraft share the fundamental approach: caves are negative-density regions in a 3D field. The difference is that Minecraft explicitly names and differentiates three cave morphologies (cheese/spaghetti/noodle) with distinct noise signatures, while NMS treats cave generation as a single parameterised noise block per planet, producing naturally varied but less architecturally differentiated results. Dwarf Fortress's caves are the most geologically coherent — they are the product of simulated geological and hydrological processes, not noise.

---

### G.5 — Scale Handling

| System | Scale challenge | Solution |
|---|---|---|
| Dwarf Fortress | Finite (256×256 to 16×16 embark tiles); no scale problem | Fixed grid; no LOD needed |
| Minecraft | Effectively infinite horizontally, finite vertically (−64 to 320 blocks) | 16×16 chunk system; chunks loaded/unloaded; rendered LOD with distance fog |
| No Man's Sky | Planetary scale: ~600km diameter planets; seamless space-to-surface | Octree LOD with spherified-cube voxel structure; floating-point origin rebasing |

NMS has the most extreme scale challenge. The jump from viewing a planet from orbit (apparent size: small sphere) to walking on its surface (terrain features at metre resolution) spans roughly 6 orders of magnitude in detail. The octree LOD handles this gracefully — the same mathematical function is evaluated at different resolutions depending on player distance.

The **planetary curvature problem** is solved by the spherified-cube approach: terrain generation happens in a local coordinate frame per planet face, so the generator never needs to handle global spherical coordinates directly. The player always stands on a "flat" local tangent plane that is, in reality, part of a sphere. The game's small physical planet size (~600km diameter in NMS terms, versus 12,700km for real Earth) means the curvature is visible from medium altitude but not noticeable while walking.

---

### G.6 — On-Demand vs. Pre-Simulated

| System | Generation timing | Data persistence |
|---|---|---|
| Dwarf Fortress | Pre-simulated: full world history computed before play begins | World saved to disk; fully stateful |
| Minecraft | On-demand per chunk: generated when first entered; cached to disk forever after | Chunks written to disk; permanent state |
| No Man's Sky | On-demand per player session: no data cached; regenerated from seed each visit | No world state stored; only player progress stored |

NMS is the only system of the three with **no persistent world geometry**. Every visit to every planet is a fresh evaluation of the same function. This is architecturally radical: it means the game scales to 18 quintillion planets at zero storage cost, but it also means the world has no history — erosion cannot happen, landslides cannot occur, and player modifications (terrain manipulation) are not stored in the planet. They are stored in the player's base data, which is a separate system overlaid on the generated world.

---

### G.7 — What Is Genuinely Distinctive About NMS

Three things are architecturally novel:

1. **The universe as address space**: The universe is not a world — it is a function. Every planet's properties are deterministically derived from its coordinates with no seed table, no stored data, no prior computation required. This is philosophically different from "a procedurally generated world" — it is closer to "a mathematical object that you explore."

2. **Seamless scale**: The LOD pipeline from orbit to walking-speed surface movement with no loading screen is a genuine technical achievement for a small team. The combination of the octree structure, the spherified-cube coordinate system, and CPU-based marching cubes meshing achieves this without a GPU compute pipeline.

3. **Biome and star type as geographic constraints**: The idea that what star a planet orbits determines what minerals it has, what colour its sky is, and which biomes are probable creates a geography that is *cosmologically coherent* rather than just visually random. A blue-star system has a distinctive character across all its planets — they share a mineral palette and biome tendency — creating the experience of a coherent stellar neighbourhood.

---

## H. Open Questions

The following aspects of NMS's generation pipeline remain genuinely unclear or poorly documented as of 2025:

1. **The exact noise type**: Whether NMS uses Perlin noise, Simplex noise, or a proprietary variant is not officially confirmed. Community sources assume Perlin/fBm but this is not authoritative.

2. **The hash/RNG chain**: How exactly the system seed is derived from region coordinates, and how the planet seed is derived from the system seed, is not publicly documented. The exact hash function (PCG, xxHash, xorshift, etc.) is unknown.

3. **Floating-island mechanics**: The modding wiki confirms that the TurbulenceNoiseLayer for floating islands is currently set to `Active: False` in vanilla. It is unclear whether floating islands (occasionally seen in-game post-Worlds Part I on lush planets) use a different mechanism, or whether some biome configurations override this default.

4. **What Worlds Part II's terrain algorithm actually changed**: The patch notes confirm that the terrain algorithm was "evolved and refined" and that "specific improvements have been made to reduce repeating patterns." The technical mechanism — whether this was additional octave layering, a new domain-warp pass, a modified noise function, or something else — is not described.

5. **How planetary curvature integrates with the noise coordinate system**: The spherified-cube approach is confirmed at a high level, but the exact transformation applied to noise input coordinates (how the game handles the face boundaries of the cube faces on the sphere projection) is not documented. This is particularly relevant for large terrain features that span cube-face boundaries.

6. **Cave-surface coupling**: It is unclear whether the cave density function and the surface terrain density function share any parameters or coordinate space. If they are fully independent, caves near the surface might produce "thin ceiling" artefacts; the mechanism preventing this (if any) is not described.

7. **The exact biome-to-VoxelGeneratorSettings mapping**: The VoxelGeneratorSettings file contains 10 parent entries, but which entry corresponds to which biome (and how the archetype further modifies or selects among them) is not fully reverse-engineered.

8. **Worlds Part I dual marching cubes: seam handling**: When the LOD octree has adjacent nodes at different LOD levels (a common situation), dual marching cubes must handle the seam between coarse and fine meshes without visible cracks. The technique NMS uses is not documented.

9. **The superformula's exact role**: Multiple sources describe NMS as using the superformula. Whether it is applied to terrain feature profiles, flora shapes, exotic biome rock formations, or some other element — and how it integrates with the noise pipeline — is not confirmed.

10. **Purple system terrain: how new and old planets coexist**: Worlds Part II applied new terrain generation rules only to purple systems. Since purple systems are new additions (not regenerated from existing yellow/red/green/blue systems), this is not a persistence problem — but the exact parameter differences between the "old" and "new" terrain algorithm are not publicly documented beyond "more diverse shapes, deeper oceans, anti-repetition improvements."

---

## Final Summary: Best Current Reconstruction of the Full Pipeline

1. **Galaxy type** assigned (constant per galaxy, modifies biome probability tables for yellow-star systems)
2. **Region grid** establishes 3D voxel address space; each region holds ~512 systems
3. **System seed** derived deterministically from region XYZ coordinates + Solar System Index
4. **Star type** selected (Yellow/Red/Green/Blue/Purple), determining stellar resource, biome probability weights, and drive requirement
5. **Planet count** (1–6) and per-planet indices assigned from system seed
6. **Per-planet seed** derived from system seed + planet index; biome type, terrain type, and terrain archetype selected from weighted tables using star type and galaxy type as modifiers
7. **VoxelGeneratorSettings parameter block** selected/parameterised for this planet's biome+archetype combination — configures all subsequent noise evaluation
8. **Atmosphere parameters** generated: sky colour, hazard type and intensity, weather type, cloud density
9. **Colour palette** assigned: ground, sky, flora, water tints derived from planet seed
10. **Water table height** set from terrain type selection (Continental, Pangean, etc.)
11. **[Player approaches]** Octree LOD activates; planet renders as atmospheric sphere at distance
12. **Outer LOD nodes** evaluated: voxel density function sampled at low resolution; coarse terrain mesh generated
13. **Progressive LOD refinement** as player descends through atmosphere
14. **Full-resolution voxel density function** evaluated in player-proximate region: sphereField + fBm_Base + fBm_Mountain + GridFeatures + CaveVoids − CaveDensity
15. **Dual marching cubes** (post Worlds Part I) converts density field to surface mesh at full resolution
16. **Surface material** assigned via altitude and slope blending; resource deposit geometry extruded from GridLayer turbulence features
17. **Water mesh** generated at sea level with dynamic wave simulation (post Worlds Part I)
18. **Flora, props, and minerals** placed as surface object instances using biome-tagged template rules
19. **POIs** (portal, structures, anomalies) validated against terrain surface and placed at qualifying slope/altitude positions
20. **Player walks on the surface** — all geometry is a pure, stateless function of coordinates + planet seed; nothing is stored

---

*Report compiled April 2025. Sources: Hello Games official patch notes (Origins 3.0, Worlds Part I 5.0, Worlds Part II 5.50); Innes McKendrick GDC 2017 "Continuous World Generation in No Man's Sky" (via Gamedeveloper.com and procedural-generation.isaackarth.com summaries); Innes McKendrick nucl.ai 2015 (via procedural-generation.isaackarth.com); Sean Murray GDC 2017 "Building Worlds Using Math(s)" (via Gamedeveloper.com); NMS Modding Wiki "Terrain Generation" article (VoxelGeneratorSettings datamine); NMS Fandom Wiki (Biome, Star System, Portal Address, Galactic Coordinates, Terrain Archetype articles); NMS Miraheze Wiki (Biome article); Steam Community forum discussions; Portal Repository community data.*
