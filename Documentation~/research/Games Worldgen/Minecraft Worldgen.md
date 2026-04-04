# How Minecraft Builds Its Infinite World

Minecraft's world generation pipeline — fundamentally rearchitected in the 1.18 Caves & Cliffs Part II update — transforms a single 64-bit integer seed into an infinite, deterministic three-dimensional landscape through a **12-stage chunk generation pipeline** driven by composable density functions and multi-octave Perlin noise. The modern system (Java Edition 1.18 through 1.21) replaces the old layer-based biome assignment and hardcoded terrain algorithms with a fully data-driven architecture: five 2D noise parameters (continentalness, erosion, weirdness, temperature, humidity) plus a derived depth parameter define a 6-dimensional climate space that simultaneously governs biome placement and, through shared spline functions, terrain shape. Every block position's fate — solid stone or empty air — is resolved by evaluating a single master density function (`final_density`) at noise-cell resolution, with aquifer logic, noise caves, and large ore veins all integrated into that same evaluation pass. This report reconstructs the complete pipeline from seed to finished chunk, identifies where evidence is confirmed versus inferred, and compares the approach to Dwarf Fortress's simulation-heavy worldgen.

---

## A. Executive summary of the geography pipeline

A Minecraft world begins with a **64-bit seed** that initializes a Xoroshiro128++ pseudorandom number generator. When a chunk is needed, the game advances it through 12 sequential status stages: allocation, structure registration, biome assignment, noise-based terrain filling, surface block replacement, carver caves, feature decoration (including scattered ores and vegetation), and finally lighting and entity spawning. The critical geographic work happens in three stages — **biomes**, **noise**, and **surface** — which together determine the physical landscape. Five large-scale 2D noise maps (continentalness, erosion, weirdness, temperature, humidity) are sampled at each horizontal position. Continentalness, erosion, and a folded derivative of weirdness called Peaks-and-Valleys (PV) are fed through cubic spline functions to produce terrain height offset and vertical stretch factor, which combine with 3D Perlin noise into the `final_density` function. Wherever this function exceeds zero, stone is placed; wherever it is zero or below, air or fluid fills the space. Aquifers independently determine fluid levels per underground cell. Noise caves — cheese, spaghetti, and noodle varieties — are carved directly into the density field. Surface rules then replace the top stone layers with biome-appropriate blocks (grass, sand, terracotta). Legacy carver caves and ravines cut through the surfaced terrain. Finally, features are placed in 11 ordered sub-steps: ores, trees, flowers, springs, and snow. The result is a fully deterministic, infinite landscape produced chunk-by-chunk on demand.

---

## B. Step-by-step chunk generation pipeline

### Stage 1 — Empty allocation

| | |
|---|---|
| **What is generated** | An empty proto-chunk data structure is created in memory |
| **Inputs** | Chunk coordinates (cx, cz) |
| **Outputs** | Empty chunk container with section arrays |
| **Ordering rationale** | Must exist before any data can be written |
| **Confidence** | Confirmed (Minecraft Wiki, chunk format documentation) |

### Stage 2 — Structure starts

| | |
|---|---|
| **What is generated** | Starting positions and piece layouts for structures that originate in this chunk |
| **Inputs** | World seed, chunk coordinates, structure placement salt values, preliminary noise sampling |
| **Outputs** | Structure start records with bounding boxes |
| **Ordering rationale** | Structures need to be registered early so subsequent stages (especially `beardifier` terrain adaptation and feature placement) know where structures exist. Comes before biomes because structure placement uses its own grid-based spacing system with structure-specific salts, not full biome data |
| **Confidence** | Confirmed (Minecraft Wiki) |

Note: Structure placement uses a deterministic grid with configurable spacing and separation parameters. Each structure type has a unique salt value combined with the world seed. Strongholds use concentric rings instead of a grid.

### Stage 3 — Structure references

| | |
|---|---|
| **What is generated** | Cross-references linking this chunk to structure starts in neighboring chunks |
| **Inputs** | Structure starts from surrounding chunks |
| **Outputs** | Reference entries pointing to which nearby chunks contain structure pieces that extend into this chunk |
| **Ordering rationale** | Must follow structure_starts; enables the features stage to place structure pieces that span multiple chunks |
| **Confidence** | Confirmed (Minecraft Wiki) |

### Stage 4 — Biomes

| | |
|---|---|
| **What is generated** | Biome assignments for every **4×4×4 block** sub-volume (biome resolution is quarter-block) |
| **Inputs** | World seed → six density functions via the noise router: `temperature`, `vegetation` (humidity), `continents` (continentalness), `erosion`, `depth`, `ridges` (weirdness). Each is evaluated from multi-octave Perlin noise |
| **Outputs** | A 3D biome palette stored per chunk section. Each position maps to a biome ID |
| **Ordering rationale** | Biomes must be assigned before noise terrain because the noise step references biome data for surface rule preparation and aquifer behavior. In practice, the biome noise parameters and terrain density function share underlying noise definitions — but the biome assignment itself is computed first |
| **Confidence** | Confirmed (Minecraft Wiki, Kniberg presentations, decompiled source) |

The Multi-Noise Biome Source treats each biome as a region in 6-dimensional parameter space. For any world position, the six parameter values are sampled and the game selects the biome whose defined interval is closest in squared Euclidean distance. This is effectively a Voronoi partition in 6D climate space.

### Stage 5 — Noise (terrain filling)

| | |
|---|---|
| **What is generated** | Base terrain shape (stone vs. air), aquifer fluid placement, noise caves (cheese/spaghetti/noodle), large ore veins, and preliminary heightmaps |
| **Inputs** | Biome data, world seed, noise router density functions (all 15 fields), noise settings (cell sizes, world height bounds, sea level) |
| **Outputs** | A filled chunk with default block (stone), air, water, lava, and large ore vein blocks. `OCEAN_FLOOR_WG` and `WORLD_SURFACE_WG` heightmaps initialized |
| **Ordering rationale** | The core geographic step — all subsequent stages modify this base terrain. Requires biomes for surface rule preparation. Must precede surface rules, carvers, and features |
| **Confidence** | Confirmed (Minecraft Wiki, noise router documentation, Kniberg's technical presentations) |

This is the most computationally intensive stage. The world is divided into **noise cells of 4×8×4 blocks** (controlled by `size_horizontal=1` and `size_vertical=2` in vanilla overworld settings). The `final_density` function is evaluated at cell corners and trilinearly interpolated for intermediate block positions. **Positive density yields stone; non-positive density yields air** (or fluid via the aquifer system). Noise caves, aquifers, and large ore veins are all resolved during this same density evaluation pass.

### Stage 6 — Surface

| | |
|---|---|
| **What is generated** | Biome-appropriate surface blocks replacing the top layers of stone |
| **Inputs** | Filled terrain from noise step, biome assignments, surface rule definitions (from noise_settings JSON), hard-coded noises (`minecraft:surface`, `minecraft:surface_secondary`, `minecraft:clay_bands_offset`) |
| **Outputs** | Terrain with grass/dirt, sand/sandstone, terracotta bands, snow, podzol, mycelium, deepslate layer, and bedrock floor |
| **Ordering rationale** | Requires base terrain shape (from noise step) to know which blocks to replace. Must precede carvers so carver tunnels cut through the correct surface materials |
| **Confidence** | Confirmed (Minecraft Wiki, surface rule documentation) |

Surface rules operate as a top-to-bottom conditional sequence. The surface depth at each column is calculated as `floor(surface_noise(x, 0, z) × 2.75 + 3.0 ± 0.25)`, yielding typically **3–6 blocks** of surface material depth. Bedrock is placed at the world floor (Y = −64) with a randomized gradient, and deepslate replaces stone below approximately Y = 0 with a noise-driven transition zone between Y = 0 and Y = 8.

### Stage 7 — Carvers

| | |
|---|---|
| **What is generated** | Legacy carver caves and canyon ravines carved through existing terrain |
| **Inputs** | Surfaced terrain, carver configuration (probability, Y range, lava level, radius multipliers) |
| **Outputs** | Additional cave voids and ravine cuts in the terrain. `_WG` heightmaps are deleted after this stage |
| **Ordering rationale** | Must follow surface application so carvers cut through biome-specific blocks. Must precede features so decorations are not placed in soon-to-be-carved space |
| **Confidence** | Confirmed (Minecraft Wiki) |

Carver caves use **Perlin worm** algorithms — a starting point sends tunnels in random directions that branch and vary in radius. They generate from Y = −56 to Y = 180, with higher probability below Y = 47. Canyon carvers originate between Y = 10 and Y = 72. Each carver has a per-chunk probability of activation. These coexist with noise caves from Stage 5.

### Stage 8 — Features

| | |
|---|---|
| **What is generated** | All decorative features in 11 ordered sub-steps: raw modifications, lakes, geodes, dungeons, surface structures, scattered ores, springs, vegetation (trees/flowers/grass), and snow/ice |
| **Inputs** | Carved terrain, biome feature lists, structure piece data, placement modifiers, heightmaps |
| **Outputs** | Fully decorated terrain. Final heightmaps (`OCEAN_FLOOR`, `WORLD_SURFACE`, `MOTION_BLOCKING`, `MOTION_BLOCKING_NO_LEAVES`) are computed |
| **Ordering rationale** | Must follow all terrain-shaping stages. Feature sub-steps are strictly ordered to prevent conflicts (e.g., ores before vegetation, vegetation before snow) |
| **Confidence** | Confirmed (Minecraft Wiki, feature placement documentation) |

The 11 decoration sub-steps execute in this fixed order: **(1)** raw_generation, **(2)** lakes, **(3)** local_modifications (geodes, icebergs), **(4)** underground_structures (dungeons), **(5)** surface_structures, **(6)** strongholds, **(7)** underground_ores (scattered ore blobs, disk features), **(8)** underground_decoration, **(9)** fluid_springs, **(10)** vegetal_decoration (trees, flowers, cacti, kelp), **(11)** top_layer_modification (snow and ice). Features may write to a **3×3 chunk area** centered on the generating chunk.

### Stages 9–12 — Lighting, spawning, and finalization

| | |
|---|---|
| **What is generated** | Light propagation calculations (Stages 9–10), initial entity spawning (Stage 11), and conversion from proto-chunk to level chunk with deferred block updates executed (Stage 12) |
| **Inputs** | Fully decorated terrain, neighboring chunk light data |
| **Outputs** | A complete, playable chunk at status `minecraft:full` |
| **Confidence** | Confirmed (Minecraft Wiki) |

These stages are not geographically generative but are necessary for the chunk to become usable. The lighting engine requires all blocks to be finalized. The spawn stage places initial passive mobs. Stage 12 converts the proto-chunk into a level chunk and executes any deferred block updates accumulated during generation.

---

## C. Pipeline dependency map

```
SEED (64-bit integer)
  │
  ├──→ Xoroshiro128++ PRNG initialization
  │
  ├──→ Noise parameter generation (per horizontal position)
  │      ├── continentalness noise (9 octaves, base scale ~512 blocks)
  │      ├── erosion noise (multi-octave)
  │      ├── weirdness/ridges noise (multi-octave)
  │      ├── temperature noise (6 octaves, base scale ~1024 blocks)
  │      └── humidity/vegetation noise (multi-octave)
  │
  ├──→ BIOME ASSIGNMENT (6D parameter lookup)
  │      ├── temperature + humidity → biome variant within category
  │      ├── continentalness → ocean / coast / inland
  │      ├── erosion → flat / mountainous
  │      ├── PV (derived from weirdness) → peaks / valleys / rivers
  │      └── depth (derived from terrain height) → surface / cave biomes
  │
  ├──→ TERRAIN DENSITY (final_density evaluation)
  │      ├── Spline functions: (continentalness, erosion, PV) → offset, factor, jaggedness
  │      ├── Y-gradient + offset → depth function
  │      ├── 3D Perlin noise → overhangs, variation
  │      ├── sloped_cheese = depth × factor + 3D noise
  │      ├── range_choice splits surface terrain from underground
  │      ├── Noise caves: cheese, spaghetti, noodle → density subtraction
  │      ├── Aquifer system: barrier, floodedness, spread, lava → fluid placement
  │      ├── Large ore veins: vein_toggle, vein_ridged, vein_gap → ore/filler blocks
  │      └── Beardifier → structure terrain adaptation
  │
  ├──→ SURFACE RULES (conditional block replacement)
  │      ├── Biome-dependent: grass/dirt, sand/sandstone, terracotta, snow, podzol
  │      ├── Depth-dependent: deepslate transition, bedrock floor
  │      └── Noise-dependent: surface layer thickness, badlands bands
  │
  ├──→ CARVERS (subtractive cave/ravine carving)
  │      ├── Cave carvers (Perlin worms, Y = −56 to 180)
  │      └── Canyon carvers (Y = 10 to 72)
  │
  └──→ FEATURES (additive decoration, 11 sub-steps)
         ├── Scattered ores (triangular/trapezoidal Y distribution)
         ├── Fluid springs
         ├── Vegetation (trees, flowers, grass, coral, kelp)
         └── Snow/ice top layer
```

The critical dependency chain is: **seed → noise fields → biomes + density functions → terrain filling → surface rules → carvers → features → lighting → playable chunk**. Biome assignment and terrain density both derive from the same underlying noise fields but are computed in separate stages, with biomes resolving first.

---

## D. Geographic subsystems

### D.1 Seed handling and determinism

The world seed is a **64-bit signed integer** (Java `long`), providing over 18 quintillion possible values. When a player enters text, Java's `.hashCode()` method converts it to a 32-bit integer; blank seeds use the system clock. Since 1.18, the primary PRNG is **Xoroshiro128++** (Mojang's modern implementation), replacing the legacy `java.util.Random` which had only 48 bits of internal state. A `legacy_random_source` boolean in noise settings controls which PRNG is used — the Overworld uses the modern generator.

**Determinism is guaranteed** by combining the world seed with chunk coordinates and operation-specific salt values. Each chunk's generation is reproducible regardless of the order in which chunks are loaded. Structure placement uses `world_seed + structure_salt`, where each structure type has a unique salt. The same seed, game version, and world type always produce identical terrain.

A subtle caveat documented on the Minecraft Wiki notes that if chunk-loading entities approach from different directions, minor generation variations can occur — but these affect entity spawning, not terrain geometry. The Minecraft@Home research group confirmed that pre-1.18 seeds had significantly reduced effective entropy because `java.util.Random` used only 48 of 64 bits internally, making seed-cracking feasible through distributed computation.

**Status: Confirmed** (Minecraft Wiki, Minecraft@Home research, decompiled source analysis)

### D.2 Noise functions

Minecraft's fundamental noise algorithm is Ken Perlin's **Improved Perlin Noise** (2002), a 3D gradient noise function using a quintic fade curve `f(t) = 6t⁵ − 15t⁴ + 10t³` and a shuffled permutation table of 256 integers doubled to 512 entries. The raw output range is approximately **±1.04**. Each sub-noise in Minecraft's implementation averages two 3D Perlin samples (a "double Perlin" approach that reduces directional artifacts).

Noise is layered into multi-octave configurations following fractal Brownian motion. Each noise definition specifies a `firstOctave` (negative integer controlling base scale) and an `amplitudes` array. For a noise with `firstOctave = -9` and 9 amplitude entries, the first octave operates at **2⁹ = 512 block scale**; each subsequent octave doubles in frequency. The amplitude formula for sub-noise at index `i` is:

```
raw_amplitude_i ≈ 1.04 × amplitudes[i] × 2^(N − i − 1) / (2^N − 1)
```

The final noise range is `±10 × sum(raw_amplitudes) / (3 × (1 + 1/m))`, where `m` is the count of non-zero amplitude entries.

The vanilla game defines over **50 named noise files**, including noises for continentalness (9 octaves, base scale ~512 blocks), temperature (6 octaves, base scale ~1024 blocks), erosion, ridge/weirdness, all three cave types, four aquifer parameters, three ore vein parameters, surface layer thickness, and several biome-specific surface noises (badlands bands, iceberg pillars). Large Biomes variants (`continentalness_large`, `erosion_large`, `temperature_large`, `vegetation_large`) double the spatial scale of the corresponding climate noises.

**No Simplex noise** is used in the modern Overworld pipeline (Simplex was used in some legacy features and remains in the End island generator). Domain warping is achieved through `shifted_noise` density functions that apply positional offsets before sampling.

**Status: Confirmed** (Minecraft Wiki noise documentation, MC Assets vanilla data files, decompiled source)

### D.3 The density function and terrain shaping system

The density function system, introduced in 1.18, is a **fully data-driven, JSON-configurable expression tree** that computes a scalar value for every block position. Over 25 density function types exist, organized into categories:

- **Noise sampling**: `noise`, `shifted_noise`, `old_blended_noise`, `end_islands`
- **Arithmetic**: `add`, `mul`, `min`, `max`, `abs`, `half_negative`, `quarter_negative`, `squeeze`, `constant`, `clamp`
- **Coordinates**: `y_clamped_gradient` (linearly maps Y to a value range)
- **Splines**: `spline` (cubic spline lookup using another density function as input — the key mechanism for converting smooth noise into dramatic terrain features)
- **Control flow**: `range_choice` (conditional branching based on input value range)
- **Caching/interpolation**: `interpolated`, `flat_cache`, `cache_2d`, `cache_once`, `cache_all_in_cell`
- **Blending**: `blend_density`, `blend_alpha`, `blend_offset` (for old-world chunk transitions)
- **Special**: `beardifier` (automatically adds terrain smoothing around structures), `weird_scaled_sampler`

The **`final_density`** function is the master terrain arbiter. The vanilla Overworld implementation is split by a `range_choice` on the `sloped_cheese` value at threshold **1.5625**: values above this threshold use surface terrain shaping; values at or below use underground cave logic.

The **surface part** combines three spline-derived parameters computed from continentalness, erosion, and PV:

- **`offset`**: Terrain height relative to sea level. Low continentalness → negative offset (ocean floors). High continentalness + low erosion → high offset (mountains). Computed via nested cubic splines.
- **`factor`**: Vertical squeeze/stretch factor. Higher values compress 3D noise variation, producing flatter terrain. Lower values allow more vertical noise, creating overhangs and floating features.
- **`jaggedness`**: Additional surface roughness. Zero in most terrain; positive where continentalness is high, erosion is low, and PV is high (mountain peaks). Larger with negative weirdness.

The fundamental terrain equation (simplified):

```
depth(x, y, z) = offset(C, E, PV) − y/128
sloped_cheese = depth × factor + 3D_perlin_noise × (factor > 0 ? 4 : 1) + jaggedness_noise × jaggedness
```

The **underground part** takes the minimum of spaghetti caves, cheese caves, and cave entrance density values, boosted near the surface to suppress cave openings. From Y = −40 to Y = −64, density is gradually fixed to **0.1171875** to prevent caves from exposing bedrock.

**Status: Confirmed** (Minecraft Wiki density function and noise router documentation, vanilla JSON data files, Kniberg's JFokus 2022 presentation, community analysis)

### D.4 Biome placement

#### Pre-1.18 system (layer-based)

Before 1.18, biomes were assigned through a **multi-layer stacking system** — a cascade of 40+ simple 2D transformations applied iteratively. An initial "island layer" generated binary land/ocean at 1:4096 scale using a quadratic congruential generator (~10% land). Successive zoom layers doubled resolution while adding noise. Climate layers assigned temperature zones (warm, temperate, cold, freezing), with 1-in-13 landmasses marked "special" for rarer biomes. A separate layer stack generated rivers at noise-region boundaries. A third stack assigned ocean temperature variants.

The critical limitation was tight coupling: **biomes directly controlled terrain shape** via per-biome "depth" and "scale" parameters, meaning identical biomes always produced identical terrain profiles. The system produced approximately 31% ocean, 13% dry biomes, 22% medium, 23% cold, 6% frozen, and 4% rare biomes.

#### Post-1.18 system (multi-noise)

The modern system uses a **6-dimensional parameter space**:

| Parameter | Source | Levels/Range | Terrain effect |
|---|---|---|---|
| Temperature | 2D Perlin noise | 5 levels (−1.0 to 1.0) | None — biome selection only |
| Humidity | 2D Perlin noise | 5 levels (−1.0 to 1.0) | None — biome selection only |
| Continentalness | 2D Perlin noise | Continuous (−1.2 to 1.0) | Indirect — shared spline references |
| Erosion | 2D Perlin noise | 7 levels (−1.0 to 1.0) | Indirect — shared spline references |
| Weirdness | 2D Perlin noise | Continuous | Indirect — via PV transform |
| Depth | Derived from terrain height | ~0 at surface, +1/128 per block down | Determines surface vs. cave biomes |

Each biome defines min/max intervals for all six parameters. The game assigns whichever biome's interval center is closest in squared Euclidean distance to the sampled values. This creates a **Voronoi-like partition** in 6D space with naturally smooth transitions.

Inland biome selection follows a hierarchical lookup: continentalness × erosion × PV selects a *category* (ocean, coast, beach, middle, plateau, shattered), then temperature × humidity × weirdness sign selects the specific biome within that category. The result is a **5×5 temperature-humidity grid** for each category, with two variants per weirdness polarity (positive vs. negative).

**Cave biomes** (Dripstone Caves, Lush Caves, Deep Dark) override surface biomes at sufficient depth. Deep Dark is hardcoded at erosion < −0.225 and depth > 0.9.

**Status: Confirmed** (Minecraft Wiki, Fabric API VanillaBiomeParameters javadoc, Kniberg's blog post, vanilla data analysis)

### D.5 Aquifer system and water table generation

The aquifer system determines whether empty space (density ≤ 0) is filled with air, water, or lava. Without it, all sub-sea-level cavities would flood uniformly.

**Global fluid picker** (fallback): Air above sea level (Y = 63); water from sea level to Y = −54; lava below Y = −54.

**Floodedness determination** proceeds through a priority chain:

1. **Disabled** (use global picker) if the global picker returns lava
2. **Disabled** if ≥12 blocks above the preliminary surface
3. **Disabled** if ≤12 blocks below surface AND a nearby position has surface below sea level
4. **Empty** (always air) in Deep Dark regions (erosion < −0.225, depth > 0.9)
5. Otherwise, the `fluid_level_floodedness` density function determines:
   - **Flooded** (water to sea level) if value > 0.8
   - **Empty** (air) if value ≤ 0.4
   - **Randomized** (independent local water level) otherwise

For randomized aquifers, the world is partitioned into **16×40×16 block cells**, each assigned a uniform fluid level computed from `fluid_level_spread` noise. Fluid type (water vs. lava) is determined in **64×40×64 cells**: if the fluid level is above Y = −10, always water; otherwise, the `lava` density function determines the fluid — lava when |lava noise| > 0.3, water otherwise. This is why lava aquifers concentrate below Y = −10.

The aquifer barrier noise (`barrier`) controls separation between adjacent fluid bodies, preventing different water levels from flooding into each other.

**Status: Confirmed** (jacobsjo's reverse-engineered aquifer analysis, Minecraft Wiki, noise router documentation)

### D.6 Cave and underground volume generation

Two distinct systems generate underground voids:

#### Noise caves (1.18+, part of density function evaluation)

These are integrated into the `final_density` calculation during the noise stage, making them part of the base terrain rather than post-generation modification.

**Cheese caves** form when the 3D `sloped_cheese` noise produces large positive regions underground — the "white" (high-value) regions of the noise image become air, creating massive open caverns with stone pillars. Near the surface, density is artificially increased to suppress cheese cave openings (suppressed when `sloped_cheese > 2.34375`, gradually allowed as values drop to 1.5625). Cheese caves are the largest cave type, producing cathedral-scale chambers.

**Spaghetti caves** form at the *boundaries* between high and low noise regions — where the noise crosses a threshold. This edge-detection approach produces long, winding tunnels. Both 2D and 3D spaghetti variants exist, controlled by dedicated density functions (`spaghetti_2d`, `spaghetti_3d_1`, `spaghetti_3d_2`) with separate thickness and modulator noises.

**Noodle caves** use the same boundary principle with tighter parameters, producing tunnels **1–5 blocks wide** — the most claustrophobic cave type.

**Noise pillars** generate within cheese caves as solid stone columns where the density function locally exceeds zero, resembling natural speleothems.

#### Carver caves (legacy, still active)

Carver caves use **Perlin worm** algorithms — subtractive tunneling that runs *after* surface rules. Cave carvers operate from Y = −56 to Y = 180 (higher probability below Y = 47), with configurable probability per chunk, horizontal radius multiplier, and floor level shape. Canyon carvers (ravines) originate between Y = 10 and Y = 72, creating long, narrow, deep vertical cuts. Carver caves can cut through ocean floor sand/gravel, re-enabling underwater ravines that were briefly absent.

**Status: Confirmed** (Minecraft Wiki cave documentation, Kniberg's "cheese/spaghetti/noodle" terminology from developer presentation, density function JSON analysis)

### D.7 Surface decoration and feature placement

Surface rules execute as a dimension-wide conditional sequence (not per-biome). At the time they run, only air, stone, water, lava, and large ore vein blocks exist. The rule system supports conditions for biome, Y-level, water depth, noise thresholds, steep slopes, temperature, and proximity to the preliminary surface.

Key surface treatments:

- **Temperate biomes**: Grass block (top) → 3–4 dirt blocks → stone
- **Deserts**: Sand → sandstone → stone
- **Badlands**: Banded terracotta strata via `clay_bands_offset` noise, orange terracotta at altitude
- **Ocean floors**: Sand (warm) or gravel (cold/normal) with coral in warm variants
- **Swamps**: Grass with noise-based water replacement at Y = 60–62
- **Deepslate transition**: Noise-gradient crossover between Y = 0 and Y = 8
- **Bedrock floor**: Randomized gradient at Y = −64 to −60

Feature placement follows 11 strict sub-steps within the features stage. Each feature uses placement modifiers to determine position: count per chunk, square random offset, height range (uniform/triangular/trapezoidal), heightmap snapping, biome filtering, rarity filtering, and block predicate checks. Features may read a 3×3 chunk neighborhood for biome lists, preventing decoration gaps at biome boundaries.

**Status: Confirmed** (Minecraft Wiki, surface rule documentation, TheForsakenFurby's surface rules guide)

### D.8 Ore vein generation

Two distinct ore systems operate at different pipeline stages:

#### Large ore veins (noise stage)

Generated simultaneously with terrain via the noise router's `vein_toggle`, `vein_ridged`, and `vein_gap` density functions. These produce **serpentine, virtually unlimited** ore bodies — single veins can contain **2,000+ ore blocks**. Two types exist:

- **Copper veins**: Y = 0 to Y = 50, thickest around Y = 20–30, embedded in **granite** filler
- **Iron veins**: Y = −60 to Y = −8, thickest around Y = −40 to −28, embedded in **tuff** filler

Within a vein volume, approximately 70% of blocks remain unchanged stone/deepslate. Of the modified 30%, a portion becomes ore (10–30%) and the remainder becomes filler. **2% of ore blocks** are raw metal blocks (block of raw copper/iron) instead of regular ore.

The `vein_toggle` density function determines vein type (≤ 0 = iron, > 0 = copper). `vein_ridged` controls vein membership (< 0 = part of vein). `vein_gap` plus a random threshold determines which vein blocks become actual ore versus filler stone.

#### Scattered ore features (features stage)

Placed during the underground_ores sub-step, *after* carvers. Each ore type has a configured Y-range, distribution shape, blob size, and frequency:

| Ore | Peak Y | Range | Distribution | Air exposure reduction |
|---|---|---|---|---|
| Diamond | −59 | −64 to 16 | Triangular | Yes — significant |
| Iron | 15 and 232 | −64 to 320 | Two triangular peaks | Slight |
| Gold | −16 | −64 to 32 | Triangular | No |
| Coal | 96 | 0 to 320 | Triangular | Yes |
| Copper | 48 | −16 to 112 | Triangular | No |
| Lapis Lazuli | 0 | −64 to 64 | Triangular + buried | No |
| Emerald | 236 | −16 to 320 | Mountain biomes only | No |

The 1.18 overhaul replaced uniform-distribution ore spawning with **triangular distributions** (most common at peak Y, rarer at range edges) and added air-exposure reduction for diamonds, making deep mining more rewarding than cave exploration.

**Status: Confirmed** (Minecraft Wiki ore and ore vein documentation, noise router field documentation)

### D.9 Climate parameters in detail

The five 2D noise parameters form the heart of the geographic system:

**Continentalness** is the dominant geographic organizer. Its ranges map directly to terrain zones:

| Continentalness range | Geographic zone |
|---|---|
| −1.2 to −1.05 | Mushroom Fields (rare, far ocean) |
| −1.05 to −0.455 | Deep Ocean (~Y = 30 seafloor) |
| −0.455 to −0.19 | Ocean (~Y = 45 seafloor) |
| −0.19 to −0.11 | Coast (beaches, stony shores) |
| −0.11 to 0.03 | Near-inland |
| 0.03 to 0.3 | Mid-inland |
| 0.3 to 1.0 | Far-inland (mountains possible) |

**Erosion** controls terrain roughness: low erosion (−1.0 to −0.78) produces mountainous terrain with meadows, snowy slopes, and peaks; high erosion (0.55 to 1.0) produces flat plains and swamps.

**Weirdness** serves dual purpose. Its raw value selects between normal and variant biomes (negative → normal, positive → variant like Bamboo Jungle or Ice Spikes). Its *folded* derivative — **Peaks and Valleys (PV)** — is computed as:

**PV = 1 − |3|W| − 2|**

where W is the weirdness value. This folding creates:

| PV range | Terrain character |
|---|---|
| −1.0 to −0.85 | Valleys (rivers) |
| −0.85 to −0.6 | Low slice |
| −0.6 to 0.2 | Mid slice |
| 0.2 to 0.7 | High slice |
| 0.7 to 1.0 | Peaks |

Because valleys occur where weirdness crosses near zero, **rivers naturally separate normal and variant biomes** — Jungle on one bank, Bamboo Jungle on the other.

**Temperature** (5 levels) and **humidity** (5 levels) affect only biome selection, not terrain shape. Their noise operates at very large scales (temperature: `firstOctave = -10`, approximately 1024-block base wavelength) to ensure broad climatic regions.

**Status: Confirmed** (Minecraft Wiki, Kniberg's presentation, vanilla biome parameter data)

### D.10 Ocean and coastline formation

Oceans and coastlines emerge directly from the **continentalness noise** and its spline-driven effect on terrain height. Where continentalness is low, the `offset` spline produces negative values, dropping terrain below sea level (Y = 63). Water fills the space via the global aquifer system.

The spline transition between ocean and coast (near continentalness ≈ −0.19) is deliberately steep, producing realistic shorelines. Erosion modulates this transition: high-erosion coasts are gradual (sandy beaches), while low-erosion coasts produce dramatic cliffs and stony shores.

Ocean temperature variants (frozen, cold, normal, lukewarm, warm) map directly from the temperature parameter's 5 levels. Deep ocean variants exist at continentalness < −0.455. Mushroom Fields occupy extremely low continentalness (< −1.05), making them rare and far from any continent.

Beach biomes are assigned at coast-level continentalness with low PV values. Snowy Beach at temperature level 0, regular Beach at levels 1–3, and Desert directly at level 4 (no separate beach). With positive weirdness, beaches may not generate at all, creating coastal cliff formations instead.

**Status: Confirmed** (Minecraft Wiki biome documentation, continentalness range data, spline analysis)

### D.11 Heightmap generation

The game maintains **six heightmap types** — four for gameplay and two temporary ones for generation:

| Heightmap | Tracks | Lifetime |
|---|---|---|
| `WORLD_SURFACE_WG` | Highest non-air block | Generation only (deleted after carvers) |
| `OCEAN_FLOOR_WG` | Highest motion-blocking block | Generation only (deleted after carvers) |
| `WORLD_SURFACE` | Highest non-air block | Permanent — lightning, sky exposure |
| `OCEAN_FLOOR` | Highest motion-blocking block | Permanent — structure/feature placement |
| `MOTION_BLOCKING` | Highest motion-blocking or fluid block | Permanent — rain/snow rendering |
| `MOTION_BLOCKING_NO_LEAVES` | Same, excluding leaves | Permanent — mob spawning |

Heightmaps are stored as **256 values** (16×16 grid) per chunk, each encoded as **9 bits** (range 0–384), packed into 37 longs. During the noise stage, the `initial_density_without_jaggedness` function estimates preliminary surface height by scanning top-down for the first Y where the value exceeds **25/64** (0.390625), used by aquifers and surface rules. Final heightmaps are computed after feature placement by scanning each column for the relevant block categories.

**Status: Confirmed** (Minecraft Wiki heightmap documentation, chunk format specification)

---

## E. Algorithmic reconstruction

### E.1 Master generation algorithm

```
ALGORITHM: GenerateChunk(seed, cx, cz)
// Status: MOSTLY CONFIRMED (from wiki pipeline documentation and source analysis)

1. ALLOCATE empty proto-chunk at (cx, cz)

2. STRUCTURE STARTS:
   For each structure type S:
     salt = S.placement_salt
     If grid_check(seed, cx, cz, S.spacing, S.separation, salt):
       Compute all piece positions for S starting at (cx, cz)
       Store StructureStart record

3. STRUCTURE REFERENCES:
   For each neighboring chunk that has a StructureStart:
     Store reference pointing to that chunk's structure

4. BIOMES:
   For each 4×4×4 sub-volume in chunk:
     (bx, by, bz) = sub-volume center
     T = sample_octave_noise(temperature_noise, bx, bz)
     H = sample_octave_noise(vegetation_noise, bx, bz)
     C = sample_octave_noise(continentalness_noise, bx, bz)
     E = sample_octave_noise(erosion_noise, bx, bz)
     W = sample_octave_noise(ridges_noise, bx, bz)
     D = compute_depth(by, preliminary_surface)
     biome = nearest_biome_in_6D(T, H, C, E, W, D)
     Store biome at sub-volume

5. NOISE (terrain fill):
   For each noise cell (4×8×4 blocks):
     Evaluate final_density at 8 cell corners:
       // Surface branch (sloped_cheese > 1.5625):
       C, E, W = sample climate noises at (x, z)
       PV = 1 - |3|W| - 2|
       offset = spline(C, E, PV)
       factor = spline(C, E, PV)
       jaggedness = spline(C, E, PV, W)
       depth = offset - y/128
       sloped_cheese = depth * factor + jagged_noise * jaggedness + 3D_noise * 4
       
       // Underground branch (sloped_cheese ≤ 1.5625):
       cheese = cheese_cave_density(x, y, z)
       spaghetti = spaghetti_cave_density(x, y, z)
       noodle = noodle_cave_density(x, y, z)
       cave_density = min(cheese, spaghetti, cave_entrance)
       cave_density = max(cave_density, surface_proximity_boost)
       cave_density = min(cave_density, noodle)
       
       final = squeeze(0.64 * interpolated(blend(range_choice(...)))) + beardifier
     
     Trilinearly interpolate within cell
     For each block in cell:
       If final_density > 0:
         Place default_block (stone)
         Check vein_toggle/vein_ridged/vein_gap for large ore veins
       Else:
         Query aquifer system:
           Determine floodedness state
           If fluid: place water or lava at computed fluid level
           Else: place air
   
   Initialize OCEAN_FLOOR_WG, WORLD_SURFACE_WG heightmaps

6. SURFACE:
   For each column (x, z):
     surface_depth = floor(surface_noise(x,0,z) * 2.75 + 3.0 ± 0.25)
     Find preliminary surface Y
     Evaluate surface rule sequence top-to-bottom:
       Check biome, Y-level, water depth, noise thresholds, slope
       Replace stone with matching surface block (grass, sand, etc.)
     Place bedrock (Y = -64, gradient to -60)
     Place deepslate (below Y ≈ 0, gradient to Y = 8)

7. CARVERS:
   For each configured carver:
     If random(chunk_seed) < carver.probability:
       Generate Perlin worm starting point
       Tunnel through terrain, replacing solid blocks with air/cave_air
       Canyon carvers: similar but with vertical extent
   Delete _WG heightmaps

8. FEATURES (11 sub-steps):
   For step in [raw_generation, lakes, local_modifications,
                underground_structures, surface_structures, strongholds,
                underground_ores, underground_decoration, fluid_springs,
                vegetal_decoration, top_layer_modification]:
     Place structure pieces matching this step
     For each feature in biome's feature list at this step:
       Apply placement modifiers to find position(s)
       Generate feature at position(s)
   
   Compute final heightmaps (OCEAN_FLOOR, WORLD_SURFACE, 
                              MOTION_BLOCKING, MOTION_BLOCKING_NO_LEAVES)

9-12. LIGHTING, SPAWNING, FINALIZATION:
   Initialize and propagate light levels
   Spawn initial passive mobs
   Convert proto-chunk → level chunk
   Execute deferred block updates
```

**Status: MOSTLY CONFIRMED** — The stage order, density function architecture, biome parameter system, surface rule mechanics, and feature sub-steps are all documented in official wiki sources derived from decompiled source code. The exact internal sequencing within the noise fill loop (aquifer queries, vein checks) is reconstructed from noise router field documentation and community analysis.

### E.2 Noise sampling algorithm

```
ALGORITHM: SampleOctaveNoise(noise_def, x, y, z)
// Status: CONFIRMED (from Minecraft Wiki noise documentation)

total = 0
For i = 0 to len(noise_def.amplitudes) - 1:
  If noise_def.amplitudes[i] == 0: continue
  
  freq = 2^(-noise_def.firstOctave + i)
  amp = 1.04 * noise_def.amplitudes[i] * 2^(N-i-1) / (2^N - 1)
  
  // Double Perlin: average of two noise samples
  sample1 = improved_perlin_3d(perm_table_1, x*freq, y*freq, z*freq)
  sample2 = improved_perlin_3d(perm_table_2, x*freq, y*freq, z*freq)
  total += ((sample1 + sample2) / 2) * amp

m = count_nonzero(noise_def.amplitudes)
return total * 10 / (3 * (1 + 1/m))  // Normalization
```

**Status: CONFIRMED** (Minecraft Wiki amplitude/frequency formula documentation)

### E.3 Biome assignment algorithm

```
ALGORITHM: AssignBiome(T, H, C, E, W, D)
// Status: CONFIRMED (from multi-noise biome source documentation)

PV = 1 - abs(3 * abs(W) - 2)

min_distance = INFINITY
selected_biome = null

For each biome B in registry:
  dist = 0
  dist += squared_range_distance(T, B.temperature_range)
  dist += squared_range_distance(H, B.humidity_range)
  dist += squared_range_distance(C, B.continentalness_range)
  dist += squared_range_distance(E, B.erosion_range)
  dist += squared_range_distance(PV, B.weirdness_range)  // Uses PV, not raw W
  dist += squared_range_distance(D, B.depth_range)
  dist += B.offset^2  // Penalty term; 0 for most biomes
  
  If dist < min_distance:
    min_distance = dist
    selected_biome = B

return selected_biome

FUNCTION squared_range_distance(value, [min, max]):
  If value < min: return (min - value)^2
  If value > max: return (value - max)^2
  return 0
```

**Status: CONFIRMED** (Minecraft Wiki, Fabric MultiNoiseUtil.NoiseHypercube documentation)

---

## F. Worked example: one chunk, seed to surface

Consider generating chunk (4, 7) — block coordinates (64, ?, 112) — with an arbitrary seed `12345`.

**Stage 1 — Allocation**: Proto-chunk created at (4, 7) with empty section arrays covering Y = −64 to 319.

**Stage 2–3 — Structures**: Grid checks for each structure type using `hash(12345, 4, 7, salt)`. Suppose no structures start here. Structure references checked for nearby chunk starts.

**Stage 4 — Biomes**: For the block position (64, 64, 112), noise sampling yields (hypothetical values):

| Parameter | Sampled value | Interpretation |
|---|---|---|
| Temperature | 0.35 | Warm (level 3) |
| Humidity | −0.05 | Moderate (level 2) |
| Continentalness | 0.45 | Far-inland |
| Erosion | −0.50 | Low erosion (level 1) — mountainous |
| Weirdness | −0.30 | Negative → normal biome variant |
| Depth | 0.0 | At surface |

PV = 1 − |3 × 0.30 − 2| = 1 − |−1.1| = 1 − 1.1 = −0.1 → **Mid slice**.

With far-inland continentalness, low erosion, and mid-slice PV, the category lookup yields **plateau**. Temperature = warm, humidity = moderate, weirdness < 0 → **Forest** biome.

**Stage 5 — Noise fill**: The spline functions process the climate values:

- `offset(C=0.45, E=−0.50, PV=−0.1)` → perhaps **+0.35** (above sea level, moderately elevated)
- `factor(C=0.45, E=−0.50, PV=−0.1)` → perhaps **4.0** (moderate compression)
- `jaggedness(C=0.45, E=−0.50, PV=−0.1, W=−0.30)` → perhaps **0.2** (slight roughness)

For a column at (64, 112), iterating through Y values:

- At Y = 100: `depth = 0.35 − 100/128 = −0.43`. Negative depth + factor → sloped_cheese strongly negative → **air**.
- At Y = 80: `depth = 0.35 − 80/128 = −0.275`. Still negative but closer to surface → depends on 3D noise. If 3D noise adds enough → marginally positive → **stone** (possible overhang).
- At Y = 72: `depth = 0.35 − 72/128 = −0.2125`. With factor = 4 and positive 3D noise contribution → density crosses zero → **surface approximately here** (~Y = 72, well above sea level = 63).
- At Y = 40: `depth = 0.35 − 40/128 = +0.0375`. Positive → underground. If `sloped_cheese ≤ 1.5625`, cave logic activates. Suppose cheese cave noise produces a large negative value at this position → **cheese cave void** → aquifer check → floodedness = empty → **air pocket**.
- At Y = −20: Deep underground. Vein check: `vein_toggle = −0.15` (< 0 → iron vein zone). `vein_ridged = −0.4` (< 0 → part of vein). `vein_gap = −0.25` (> −0.3 → ore block if random check passes). Result: **iron ore** in tuff matrix.
- At Y = −62: Near bedrock. Density fixed at 0.1171875 → always **stone** (protecting bedrock layer).

**Stage 6 — Surface**: At the surface (Y ≈ 72), surface noise yields `surface_depth = 4`. Rules evaluate: biome = Forest → replace Y = 72 with grass block, Y = 71–68 with dirt, below = stone. At Y = −64 to −60: bedrock placed with randomized gradient. Below Y = 0: deepslate replaces stone.

**Stage 7 — Carvers**: Random check: suppose carver probability 0.15 and roll = 0.08 → carver activates. A Perlin worm starting at Y = 35 tunnels through, replacing stone with cave_air in a roughly 4-block-radius tube.

**Stage 8 — Features**: Underground_ores sub-step: coal ore blob placed at Y = 65 (4 blocks, triangular distribution peaks at 96). Vegetal_decoration: an oak tree placed at surface Y = 73 (1 block above grass). Top_layer: no snow (temperature too warm).

**Result**: A forested plateau chunk at approximately Y = 72, with a cheese cave at Y = 40, a carver tunnel at Y = 35, an iron ore vein in tuff near Y = −20, and an oak tree on the surface. The terrain is modestly elevated above sea level, consistent with far-inland, low-erosion parameters.

---

## G. Comparison with Dwarf Fortress

### Noise-based versus fractal field approaches

Minecraft and Dwarf Fortress use fundamentally different noise strategies. Minecraft employs **multi-octave Perlin noise** sampled per-position in an infinite, on-demand world — a continuous function evaluated at any coordinate without pre-computation. Dwarf Fortress uses **midpoint displacement** (diamond-square fractal) to generate a fixed-size 2D heightmap upfront, filling in detail iteratively from coarse grid to fine resolution across six scalar fields. Minecraft's approach scales to infinite worlds but lacks global coherence; DF's approach produces a complete, globally consistent map but at fixed boundaries.

### Climate simulation depth

This is the starkest divergence. Minecraft's temperature and humidity are **abstract, uncoupled noise parameters** used solely for biome lookup — temperature does not interact with elevation, humidity has no physical relationship to rainfall, and there is no wind or atmospheric model. Dwarf Fortress simulates **physically motivated climate**: temperature decreases with latitude and elevation, **rain shadows** form on the lee side of mountain ranges, and drainage interacts with rainfall to determine wetland formation. DF's climate emerges from field interactions; Minecraft's is painted on by independent noise.

### Erosion modeling

Dwarf Fortress performs **iterative agent-based erosion**: rivers flow downhill from mountain edges, actively **carving channels in the elevation field** when they cannot find paths to the sea, with configurable erosion cycle counts. Lakes grow at accumulation points. Worlds that fail to meet minimum river counts are rejected and regenerated. Minecraft's "erosion" parameter is a **cosmetic noise field** that influences terrain flatness through spline functions — no iterative simulation, no water-flow modeling, no terrain modification after initial generation.

### Biome classification methods

Both games derive biomes from multiple independent parameters rather than direct biome noise, but the implementation differs significantly. Minecraft uses a **fixed lookup table** mapping discretized parameter ranges to specific biome IDs in a 6D Voronoi partition. Dwarf Fortress determines biomes **emergently** from the interplay of continuous fields: high rainfall + low drainage = swamp; high elevation = mountain; low rainfall + high drainage = badlands. As Tarn Adams noted, handling fields separately and letting biomes emerge from their interplay produced "a more natural, internally consistent solution" than direct biome spawning. Minecraft's approach is more designable (each biome can be precisely placed in parameter space) but less emergent.

### Cave generation philosophy

Minecraft generates caves **geometrically** through 3D noise functions (cheese, spaghetti, noodle) and Perlin worm carvers — purely mathematical shapes with no ecological or geological simulation. Cave biomes are assigned by the same noise-parameter system as surface biomes. Dwarf Fortress places caves during **history generation** after the physical world is complete, creating stratified underground layers with their own ecosystems, forgotten beasts, and civilizations. DF's caves exist as spaces with narrative context; Minecraft's exist as noise-derived voids with painted-on biome decoration.

### What distinguishes Minecraft's approach

Three aspects stand out. First, Minecraft's **3D density function system** is genuinely novel in the procedural generation landscape — rather than a 2D heightmap with post-hoc cave carving, the entire terrain volume is defined by a single mathematical expression that simultaneously produces surface topography, overhangs, floating islands, and cave networks. Second, the **composable, data-driven density function architecture** allows the entire terrain pipeline to be reconfigured through JSON data packs without code changes — a level of moddability unusual in procedural generation systems. Third, Minecraft's **infinite, on-demand generation** with per-chunk determinism is an engineering achievement that DF's upfront, fixed-size approach does not attempt. The tradeoff is that Minecraft cannot perform the global consistency checks, iterative simulation, or world-rejection that make DF's geography physically coherent.

---

## H. Open questions and uncertainties

**1. Exact spline point values.** The cubic spline functions mapping continentalness, erosion, and PV to terrain offset, factor, and jaggedness contain dozens of control points. While the spline *mechanism* is fully documented, the **precise numerical values of all spline points** are embedded in vanilla data files that span over 200,000 lines of JSON. Complete tabulation and analysis of these values remains incomplete in public documentation. The splines are the most opaque part of the terrain system — they determine *exactly* where cliffs form, how steep coastlines are, and at what parameter values peaks emerge.

**2. Aquifer barrier behavior at cell boundaries.** The aquifer system partitions the world into cells of different sizes (16×40×16 for fluid levels, 64×40×64 for fluid type selection). The exact interpolation and edge-handling behavior between adjacent cells with different fluid levels is only partially documented. jacobsjo's reverse-engineered analysis is the most detailed public source, but some boundary conditions remain unclear.

**3. Beardifier implementation details.** The `beardifier` density function, which smooths terrain around structures, is automatically injected by the game engine and cannot be configured through data packs. Its exact distance falloff function, interaction radius, and behavior when multiple structures overlap is not fully documented publicly.

**4. Post-1.21 changes.** Java Edition versions after 1.21 (the current target) may introduce changes to the generation pipeline. The Pale Garden biome (1.21.4) added a new surface biome, but it is unclear whether the underlying density function system or noise router architecture has been modified. Minecraft@Home's "Panorama-Pale-Garden" project suggests ongoing community interest in tracking these changes.

**5. Nether and End generation internals.** While this report focuses on the Overworld, the Nether uses a simplified noise router (3D Perlin between lava floors and ceiling, aquifers disabled) and the End uses a custom `end_islands` density function. The exact parameterization of Nether cave generation and End island spacing deserves separate documentation.

**6. Performance optimizations and generation parallelism.** Vanilla Minecraft uses a single primary thread for chunk generation (with some worker thread delegation in `populateNoise`). The exact boundaries of parallelizable work, cache invalidation behavior, and the conditions under which proto-chunks "freeze" at intermediate stages are implementation details not fully captured by public documentation.

**7. Legacy random source edge cases.** The `legacy_random_source` flag in noise settings toggles between the old 48-bit Java Random and the new 64-bit Xoroshiro128++. The exact behavioral differences this creates — particularly whether it affects anything beyond noise permutation table initialization — are not comprehensively documented.

**8. Interaction between noise caves and aquifer preliminary surface.** The preliminary surface height used by aquifers is computed from `initial_density_without_jaggedness`, which deliberately underestimates the actual surface. How accurately this underestimate tracks the real surface across extreme terrain (amplified worlds, shattered biomes) and whether it causes aquifer misbehavior in edge cases is poorly documented.

---

## Best current reconstruction: the complete pipeline as a numbered list

1. **Seed initialization**: 64-bit integer seed initializes Xoroshiro128++ PRNG; chunk-local salts ensure positional determinism.

2. **Noise field precomputation**: Five 2D multi-octave Perlin noise maps — continentalness (9 octaves, ~512-block base scale), erosion, weirdness/ridges, temperature (6 octaves, ~1024-block base scale), and humidity/vegetation — are sampled at each horizontal position. A sixth parameter, depth, is derived from terrain height rather than noise.

3. **Biome assignment**: The six parameter values at each 4×4×4 sub-volume are matched against all registered biomes via squared Euclidean distance in 6D parameter space. The closest biome is assigned. Ocean biomes dominate at low continentalness; mountains at high continentalness + low erosion + high PV; rivers at PV valleys (weirdness ≈ 0).

4. **Spline-based terrain parameter derivation**: Continentalness, erosion, and PV (= 1 − |3|W| − 2|) are fed through interconnected cubic splines to produce three terrain-shaping parameters: offset (base height), factor (vertical compression), and jaggedness (surface roughness).

5. **Density function evaluation**: For each noise cell (4×8×4 blocks), `final_density` is evaluated at cell corners. The function combines a Y-gradient with spline-derived offset and factor, adds 3D Perlin noise for overhangs and variation, and applies a `range_choice` to split surface terrain from underground cave logic. Results are trilinearly interpolated within cells. Positive density → stone; non-positive → air or fluid.

6. **Noise cave generation**: Simultaneously with terrain filling, cheese caves (large chambers from high 3D noise), spaghetti caves (boundary-crossing tunnels), and noodle caves (narrow variants) are generated as negative-density regions within the `final_density` expression.

7. **Aquifer resolution**: Empty spaces below or near sea level are evaluated by the aquifer system. Floodedness state (disabled/flooded/empty/randomized) is determined from preliminary surface height, erosion, depth, and the `fluid_level_floodedness` noise. Randomized aquifers assign local water levels per 16×40×16 cell. Fluid type (water vs. lava) is determined per 64×40×64 cell, with lava predominating below Y = −10.

8. **Large ore vein generation**: During the same noise pass, `vein_toggle`, `vein_ridged`, and `vein_gap` density functions identify serpentine ore vein volumes. Copper veins (Y = 0 to 50, in granite) and iron veins (Y = −60 to −8, in tuff) are placed directly into the terrain.

9. **Surface rule application**: A dimension-wide conditional rule sequence replaces the top 3–6 blocks of stone with biome-appropriate materials. Bedrock is placed at the world floor with a randomized gradient. Deepslate replaces stone below Y ≈ 0. Badlands receive banded terracotta via dedicated noise. Surface rules check biome, Y-level, water proximity, noise thresholds, and slope steepness.

10. **Carver cave and ravine generation**: Legacy Perlin worm cave carvers (Y = −56 to 180, configurable probability) and canyon carvers (Y = 10 to 72) cut subtractive tunnels through the surfaced terrain, coexisting with noise caves from Step 6.

11. **Feature decoration in 11 ordered sub-steps**: Raw generation → lakes → local modifications (geodes) → underground structures (dungeons) → surface structures → strongholds → underground ores (scattered ore blobs with triangular Y-distributions, air-exposure reduction for diamonds) → underground decoration → fluid springs → vegetal decoration (trees, flowers, grass, kelp, coral) → top-layer modification (snow, ice).

12. **Heightmap finalization**: Four permanent heightmaps (WORLD_SURFACE, OCEAN_FLOOR, MOTION_BLOCKING, MOTION_BLOCKING_NO_LEAVES) are computed by scanning each column for relevant block categories.

13. **Lighting, spawning, and chunk promotion**: Light levels are propagated, initial passive mobs spawn, the proto-chunk converts to a level chunk, and deferred block updates execute. The chunk is now playable.

This reconstruction reflects the state of Java Edition 1.18 through 1.21. The density function system, multi-noise biome source, aquifer mechanics, and feature pipeline have remained architecturally stable across these versions. The fundamental innovation of 1.18 — replacing layer-based biomes and hardcoded terrain with a composable, data-driven density function architecture — represents the most significant rearchitecting of Minecraft's world generation in the game's history, and establishes the framework likely to persist through future updates.