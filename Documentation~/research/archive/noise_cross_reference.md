# Noise Technique Cross-Reference: Games × Islands Implementation

## Methodology

Six games from the worldgen research corpus are cross-referenced against noise-related
techniques from the PCG technique reports (1a, 1b, 2a, 2b, 6b). Each technique is mapped
to its current implementation status inside Islands and to the roadmap phases that would
consume or extend it.

Games are marked: **✓** (confirmed use), **~** (inferred/likely), **✗** (confirmed absent
or not applicable), **?** (unknown/undocumented).

Islands status uses: **IMPL** (implemented in noise runtime), **USED** (consumed by the
map pipeline), **PLAN** (referenced in roadmap phases), **—** (not implemented, not planned).

---

## 1. Noise Primitives

### 1.1 Coordinate Hashing (stateless position → random value)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Xoroshiro128++ seeded per chunk; salt-based positional determinism |
| Dwarf Fortress | ✓ | PRNG seeded from world seed; sequential rather than coordinate-hashed |
| No Man's Sky | ✓ | Coordinate-as-seed architecture — the universe IS the hash space |
| RimWorld | ✓ | `Gen.HashCombineInt(worldSeed, tileID)` per tile; GenStep XOR salts |
| Cataclysm:DDA | ✓ | Seeded per overmap; sequential within overmap generation |
| Tangledeep | ✓ | Master RNG from world seed; sequential consumption per floor |

**Islands status: IMPL + USED**
`SmallXXHash` / `SmallXXHash4` are the canonical primitives. The map pipeline uses
`ctx.Rng` (consuming from a seeded sequence) plus `MapNoiseBridge2D` which derives
noise seeds from `f(inputs.seed, stageSalt)` for coordinate-based access.

**Observations:** Islands' coordinate hashing is the most architecturally modern of all
six games. Minecraft and NMS approach stateless hashing; DF, RimWorld, CDDA, and
Tangledeep all use sequential PRNG consumption (which Islands' map pipeline also does
for non-noise RNG via `ctx.Rng`, but noise stages use coordinate-based seeds).

---

### 1.2 Value Noise

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | Uses Perlin exclusively for terrain |
| Dwarf Fortress | ✗ | Uses midpoint displacement, not coherent noise |
| No Man's Sky | ? | Noise type unconfirmed; likely gradient-based |
| RimWorld | ✗ | Uses Perlin via LibNoise |
| Cataclysm:DDA | ? | Noise type in overmap gen not fully documented |
| Tangledeep | ✗ | No noise-based terrain; dungeon algorithms are structural |

**Islands status: IMPL**
Implemented via `IGradient` Value variant through the lattice system. Not currently
consumed by the map pipeline (Perlin/Simplex preferred).

**Observations:** No researched game uses value noise as a primary terrain generator.
Its grid-aligned artifacts make it unsuitable for terrain. Islands implements it for
completeness but the map pipeline correctly avoids it.

---

### 1.3 Perlin Noise (Gradient Noise)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | **Primary noise type.** Multi-octave Perlin for all 5 climate axes, density function, cave noise, carver worms. 50+ named noise definitions. No Simplex in Overworld. |
| Dwarf Fortress | ✗ | Uses midpoint displacement instead. Adams noted Perlin would be better but "it's actually OK the way it is." |
| No Man's Sky | ~ | Likely Perlin or similar gradient noise; exact type unconfirmed. Community assumes Perlin/fBm. |
| RimWorld | ✓ | **Primary noise type.** 6-octave Perlin for elevation, rainfall, fertility, hilliness, swampiness, cave carving. Uses LibNoise C# port. |
| Cataclysm:DDA | ~ | Overmap uses noise for forest distribution; type likely Perlin (C++ engine code). |
| Tangledeep | ✗ | No coherent noise in generation pipeline. |

**Islands status: IMPL + USED**
Implemented via `IGradient` Perlin variant through the lattice system. Consumed by the
map pipeline through `MapNoiseBridge2D` for hills bias fields (F3) and vegetation (F5).

**Observations:** Perlin is the workhorse of shipped game worldgen. Minecraft and RimWorld
both use it as their sole noise type — neither uses Simplex for terrain. This validates
Islands' current approach of having Perlin available and used, while also offering Simplex
as an alternative.

---

### 1.4 Simplex / OpenSimplex Noise

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | Explicitly not used in Overworld. Legacy Simplex only in End island generator. |
| Dwarf Fortress | ✗ | No coherent noise functions used. |
| No Man's Sky | ? | Possible but unconfirmed. |
| RimWorld | ✗ | LibNoise provides Perlin; no Simplex usage found. |
| Cataclysm:DDA | ✗ | No evidence of Simplex usage. |
| Tangledeep | ✗ | No coherent noise. |

**Islands status: IMPL**
Implemented via the Simplex kernel family. Not currently consumed by the map pipeline
but available for future stages.

**Observations:** Surprisingly, no researched game confirms Simplex noise usage for
terrain generation. Perlin dominates in practice despite Simplex's theoretical advantages.
This suggests Islands should not prioritize Simplex-specific features over Perlin-compatible
composition techniques (fBm, domain warping, etc.), which are noise-type-agnostic.

---

### 1.5 Worley / Voronoi / Cellular Noise

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | 6D Voronoi partition for biome assignment (squared Euclidean distance in parameter space). Not used for terrain shape. |
| Dwarf Fortress | ✗ | No Voronoi noise. Regions detected by flood-fill, not Voronoi. |
| No Man's Sky | ✗ | No confirmed Voronoi noise usage. |
| RimWorld | ✗ | Biome assignment uses competitive scoring, not Voronoi distance. |
| Cataclysm:DDA | ✗ | No Voronoi noise. |
| Tangledeep | ✗ | No coherent noise. |

**Islands status: IMPL**
Full Voronoi family implemented: distance metrics (Worley, SmoothWorley, Chebyshev),
value functions (F1, F2, F2-F1, CellAsIslands). Not currently consumed by map pipeline.

**Observations:** Voronoi noise is rarely used directly for terrain among these games.
Minecraft's biome Voronoi operates in abstract parameter space, not spatial coordinates.
Islands' Voronoi implementation is well-positioned for Phase J (region partitioning)
and Phase K (plate tectonics), where spatial Voronoi cells are planned. The F2-F1 ridge
extraction is specifically relevant for mountain range generation in Phase K.

---

### 1.6 Midpoint Displacement / Diamond-Square

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | Uses Perlin fBm instead. |
| Dwarf Fortress | ✓ | **Primary terrain algorithm.** All 6 scalar fields seeded via midpoint displacement on (2^n)+1 grids. 1982-era technique, heavily post-processed. |
| No Man's Sky | ✗ | Uses layered fBm. |
| RimWorld | ✗ | Uses Perlin fBm. |
| Cataclysm:DDA | ✗ | No fractal terrain generation at this level. |
| Tangledeep | ✗ | No terrain generation. |

**Islands status: —**
Not implemented. Not planned.

**Observations:** Only DF uses this, and Adams himself acknowledged Perlin would be better.
The technique requires (2^n)+1 grid sizes, produces grid-alignment artifacts, and cannot
support on-demand chunk generation. Islands should not implement this — fBm via Perlin/Simplex
is strictly superior for Islands' grid-first, deterministic pipeline.

---

## 2. Noise Composition

### 2.1 Fractal Brownian Motion (fBm) / Octave Summation

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | All noise definitions use multi-octave Perlin. 6–9 octaves typical. `firstOctave` mechanism controls base scale. Custom amplitude arrays per octave. |
| Dwarf Fortress | ~ | Midpoint displacement is functionally similar to fBm (multi-scale fractal detail) but algorithmically distinct. |
| No Man's Sky | ✓ | "Uber noise" layers confirmed as fBm (frequency, amplitude, octave count, persistence, lacunarity-like parameter). Sean Murray GDC 2017 confirmed. |
| RimWorld | ✓ | 6-octave Perlin with persistence 0.4 for elevation; 6 octaves for rainfall, fertility. Standard fBm parameterization via LibNoise. |
| Cataclysm:DDA | ~ | Noise-based forest/terrain distribution likely uses multi-octave. |
| Tangledeep | ✗ | No noise composition. |

**Islands status: IMPL + USED**
`Noise.GetFractalNoise<N>()` with `Noise.Settings` (seed, frequency, octaves, lacunarity,
persistence). Consumed by map pipeline via `MapNoiseBridge2D` in F3 (hills) and F5 (vegetation).

**Observations:** fBm is universal. Every game that uses noise uses fBm or its functional
equivalent. Islands' implementation is solid. The main gap is that the map pipeline currently
uses relatively simple fBm (single pass for hills, single pass for vegetation), while
Minecraft uses 50+ named noise definitions and NMS uses 7 Uber noise layers. As the map
pipeline grows (Phase M temperature/moisture, Phase L hydrology), more fBm instances with
distinct parameterizations will be needed.

**Relevance to planned phases:**
- Phase M: Temperature noise perturbation, Moisture noise perturbation (both use fBm)
- Phase J: Region boundary noise (fBm for organic Voronoi cell edges)
- Phase W: Local map elevation reinflation (fBm at local resolution, RimWorld pattern)

---

### 2.2 Ridged Multifractal

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | Uses spline remapping for dramatic terrain instead. "Erosion" and "weirdness" noise parameters are standard Perlin, not ridged. |
| Dwarf Fortress | ✗ | No ridged noise. Mountain peaks are fractal elevation maxima. |
| No Man's Sky | ~ | "Mountain" Uber noise layer likely uses ridged variant; unconfirmed. |
| RimWorld | ✓ | **RidgedMultifractal** blended with Perlin for world elevation. Creates sharp mountain ridges complementing smoother Perlin continental shapes. |
| Cataclysm:DDA | ✗ | No evidence. |
| Tangledeep | ✗ | N/A. |

**Islands status: IMPL (partial)**
The noise runtime supports the `Turbulence` gradient wrapper which provides `|noise|`
behavior. Full Musgrave-style ridged multifractal with inter-octave feedback (signal-
dependent amplitude weighting) would need to be implemented as a fractal accumulation
variant or a custom composition in the map pipeline.

**Status clarification:** The noise runtime's fractal accumulation (`GetFractalNoise`)
implements standard fBm octave summation. The `abs(noise)` → inversion → feedback loop
that defines ridged multifractal is NOT implemented as a built-in fractal mode.
To use ridged multifractal in the map pipeline, it would need to be either:
(a) added as a new fractal accumulation mode in the noise runtime, or
(b) composed manually in the map stage code using per-octave noise evaluation.

**Observations:** RimWorld is the only confirmed user, and it uses ridged multifractal
specifically for mountain shaping. This is directly relevant to Islands: the current
`Height` field is produced by the base terrain stage with simple noise; a ridged
multifractal variant could dramatically improve mountain terrain quality.

**Relevance to planned phases:**
- Phase K (Plate Tectonics): Ridged multifractal is the canonical technique for
  mountain ranges at collision zones.
- Phase W (World-to-Local): Local map elevation reinflation could blend Perlin
  (lowlands) with ridged multifractal (mountains), exactly as RimWorld does.

---

### 2.3 Turbulence (|noise| per octave)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | No turbulence function used. |
| Dwarf Fortress | ✗ | No turbulence. |
| No Man's Sky | ✓ | `TurbulenceNoiseLayer` confirmed in datamined VoxelGeneratorSettings. Used for domain warping of GridLayers. |
| RimWorld | ✗ | No turbulence. |
| Cataclysm:DDA | ✗ | No evidence. |
| Tangledeep | ✗ | N/A. |

**Islands status: IMPL**
`Turbulence` gradient wrapper implemented in the noise runtime. Wraps any `IGradient`
with `abs()` on the value. Not currently consumed by the map pipeline.

**Observations:** Only NMS uses turbulence, and specifically as a domain warping tool
(not for direct terrain shape). The technique's C0-only continuity and non-zero mean
make it unsuitable for heightfields. Islands' implementation is available if needed for
texture-like effects or domain warping, but it is not a priority for the map pipeline.

---

### 2.4 Domain Warping

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ~ | `shifted_noise` density functions apply positional offsets before sampling — a simple form of domain warping. Not full fBm-into-fBm warping. |
| Dwarf Fortress | ✗ | No domain warping. |
| No Man's Sky | ✓ | Confirmed. Each GridLayer has a child TurbulenceNoiseLayer used for domain warping. "Uber noise" layers warped by additional passes. |
| RimWorld | ✗ | No domain warping. |
| Cataclysm:DDA | ✗ | No domain warping. |
| Tangledeep | ✗ | N/A. |

**Islands status: IMPL (partial) + USED (simple form)**
The noise runtime does not have a built-in domain warping composition mode, but the
architecture supports it (evaluate noise at warped coordinates). The map pipeline uses
a simple form in Phase F2b: two coarse noise grids (WarpCellSize=16) distort the island
shape mask via `warpAmplitude01`.

**Observations:** NMS is the only game with confirmed heavy domain warping. The technique
is primarily valuable for breaking noise regularity and producing organic coastlines.
Islands already uses a simple warp for island shape (F2b). Full Quilez-style single/double
warping could be implemented as a map pipeline composition rather than a noise runtime
primitive — the noise runtime provides the building blocks (multiple fBm evaluations at
offset coordinates), and the stage code would compose them.

**Relevance to planned phases:**
- Phase J (Voronoi Regions): Domain warping Voronoi cell boundaries produces organic
  biome borders instead of geometric edges.
- Phase K (Plate Tectonics): Warped continental shapes are more naturalistic than
  raw Voronoi cells.
- Phase W (World-to-Local): Local terrain can use domain warping for organic variation
  within a tile's elevation envelope.

---

### 2.5 Spline Remapping

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | **Critically important.** Cubic spline density functions convert smooth noise into dramatic terrain features. The `spline` type is the key mechanism for terrain variety — it maps continentalness/erosion/PV to offset/factor/jaggedness. |
| Dwarf Fortress | ~ | Non-linear parabola applied to elevation "to bend mountains to look more realistic." A simple form of curve remapping. |
| No Man's Sky | ? | Likely used internally but not confirmed from datamine. |
| RimWorld | ✗ | No spline remapping. Direct noise → threshold. |
| Cataclysm:DDA | ✗ | No spline remapping. |
| Tangledeep | ✗ | N/A. |

**Islands status: —**
Not implemented in the noise runtime or map pipeline.

**Observations:** This is arguably the highest-impact unimplemented noise composition
technique for Islands. Minecraft's entire terrain variety comes from spline remapping —
the same noise values produce flat plains OR dramatic peaks depending on how the spline
curves them. Without spline remapping, noise fields can only produce linear or power-
redistributed height profiles, which limits terrain variety.

**Relevance to planned phases:**
- Phase M (Biome Classification): Spline curves could remap Temperature/Moisture
  noise for more naturalistic climate gradients.
- Phase W (World-to-Local): Spline remapping is how RimWorld-style "the same noise
  means different things in different contexts" works — a world tile's hilliness
  parameter could select a spline curve that reshapes local elevation noise.
- General pipeline: Any scalar field can benefit from spline remapping. Adding a
  `SplineRemap` operator to the map pipeline would be broadly useful.

---

### 2.6 Power Redistribution (pow(elevation, exponent))

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | Uses spline remapping instead. |
| Dwarf Fortress | ✓ | Non-linear parabola on elevation. |
| No Man's Sky | ? | Likely internal but unconfirmed. |
| RimWorld | ✗ | No power redistribution; hilliness controls noise amplitude directly. |
| Cataclysm:DDA | ✗ | N/A. |
| Tangledeep | ✗ | N/A. |

**Islands status: —**
Not explicitly implemented. Could be trivially applied as `pow(normalizedHeight, exponent)`
in any stage.

**Observations:** Simple and cheap. Less flexible than spline remapping but much easier
to implement. Good for a quick terrain quality improvement: exponents > 1 flatten lowlands
and sharpen peaks, producing more naturalistic height distributions from raw fBm.

---

### 2.7 Scalar Field Composition (multi-field blending)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Entire density function system is scalar field composition: add, mul, min, max, abs, clamp, range_choice, blend_density. 25+ operator types. Fully data-driven JSON expression trees. |
| Dwarf Fortress | ✓ | Six scalar fields interact: drainage gates rain shadows, volcanism drives volcano placement, elevation + rainfall + temperature → biome. Emergent composition. |
| No Man's Sky | ✓ | Layered density fields: Base + Mountain + UnderWater + GridLayers + Features + Caves. Additive composition with per-layer parameterization. |
| RimWorld | ✓ | Elevation = blend(Perlin, RidgedMulti, control). Temperature = latitude + noise + elevation penalty. Biome = competitive scoring across climate fields. |
| Cataclysm:DDA | ~ | Region settings combine noise thresholds for forest, city placement. |
| Tangledeep | ✗ | N/A. |

**Islands status: USED (basic)**
The map pipeline composes fields through stage sequencing: Height field → threshold →
Land mask; Height + Land → Hills threshold → HillsL1/L2; CoastDist field consumed by
multiple downstream stages. Composition is currently procedural (C# stage code) rather
than declarative.

**Observations:** Every serious worldgen game composes multiple scalar fields. The
sophistication range is enormous: from RimWorld's simple additive composition to
Minecraft's 25-operator expression tree DAG. Islands' current approach (procedural
stage code) is adequate for the current pipeline complexity but may benefit from a
more declarative composition system as Phase M/L/J add more fields.

**Relevance to planned phases:**
- Phase M: Temperature = f(Height, latitude, CoastDist, noise) — multi-field composition.
- Phase M: Moisture = f(CoastDist, FlowAccumulation, noise) — multi-field composition.
- Phase M: Biome = WhittakerLookup(Temperature, Moisture, Height) — the canonical
  multi-axis classification pattern.

---

## 3. Noise Application Patterns

### 3.1 Multi-Axis Biome Classification (Whittaker-style)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | 6D parameter space (temperature, humidity, continentalness, erosion, weirdness, depth) → Voronoi partition → biome ID. Most sophisticated implementation. |
| Dwarf Fortress | ✓ | Emergent: temperature + rainfall + drainage + elevation → biome type. Not a lookup table — biomes emerge from continuous field interactions. |
| No Man's Sky | ✗ | Planet-wide discrete biome type selected from probability table at planet seed time. No continuous biome field. |
| RimWorld | ✓ | BiomeWorker competitive scoring: each biome evaluates f(temperature, rainfall, elevation) and highest score wins. Functionally a Whittaker-style lookup. |
| Cataclysm:DDA | ✗ | No biome classification from climate fields. Overmap terrain types are placed by C++ algorithms. |
| Tangledeep | ✗ | Biomes are XML-configured per floor, not derived from noise. |

**Islands status: PLAN (Phase M)**
Phase M design specifies Whittaker-style Temperature × Moisture lookup table with Height
for altitude-band overrides. Output: `MapFieldId.Biome` integer scalar field with
`BiomeDef[]` lookup table. This matches the standard approach from Minecraft (simplified),
DF, and RimWorld.

---

### 3.2 Island / Continent Masks

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Continentalness noise controls land vs. ocean transition. Low continentalness = ocean biomes. |
| Dwarf Fortress | ✓ | Ocean edge parameters (PARTIAL_OCEAN_EDGE, COMPLETE_OCEAN_EDGE) + sea-level threshold on fractal elevation. |
| No Man's Sky | ✗ | Entire planets are terrain; no island masking needed. |
| RimWorld | ✓ | `ConvertToIsland` noise modifier when coverage < 100%. Also implicit via elevation ≤ 0 = ocean. |
| Cataclysm:DDA | ✗ | No island/continent shaping. |
| Tangledeep | ✗ | N/A. |

**Islands status: USED**
Stage_BaseTerrain2D produces Land mask via ellipse + noise warp (F2b) or arbitrary shape
input (F2c). This is the core island generation mechanism. Well-implemented.

---

### 3.3 Cave Generation via Noise Thresholding

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Three distinct cave morphologies: cheese (large voids from 3D noise), spaghetti (intersected noise tunnel pairs), noodle (thin narrow passages). Each is a distinct noise configuration baked into the density function. |
| Dwarf Fortress | ✗ | Caves placed during history simulation, not noise-derived. |
| No Man's Sky | ✓ | CavesUnderground noise configuration per-planet. Subtracted density field terms create cave voids. |
| RimWorld | ✓ | Perlin-guided tunnel carving through flood-filled rock groups ≥ 300 cells. |
| Cataclysm:DDA | ✗ | No noise-based caves. Underground structures are JSON-defined. |
| Tangledeep | ~ | CA-like cave generators (inferred). Not noise-based. |

**Islands status: —**
Not implemented. Not currently planned in the roadmap.

**Observations:** Cave generation via noise thresholding requires 3D noise or 2D noise
composition (intersected fields). Islands' 2D grid-first pipeline would need to use the
2D intersection approach: `abs(noise1) < width AND abs(noise2) < width` for tunnel-like
caves. This could be a future phase but would need design work to integrate with the
existing 2D mask/field architecture.

---

### 3.4 Noise as Height × Hilliness Amplitude Control

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Spline-derived `factor` parameter controls vertical compression per position. |
| Dwarf Fortress | ✓ | X/Y variance parameters control elevation change rate. |
| No Man's Sky | ✓ | Per-biome noise amplitude parameterization. |
| RimWorld | ✓ | **Hilliness enum directly multiplies Perlin elevation amplitude.** MapGenTuning factors: ~0.0035 (Flat) to ~0.1 (Impassable). The single most important parameter crossing world-to-local boundary. |
| Cataclysm:DDA | ✗ | N/A. |
| Tangledeep | ✗ | N/A. |

**Islands status: USED (indirectly)**
The map pipeline's Height field is produced with fixed noise parameters. Hills are
derived by thresholding, not by amplitude modulation. Phase W (World-to-Local) would
adopt the RimWorld pattern: world tile hilliness → local noise amplitude multiplier.

**Relevance to planned phases:**
- Phase W: `WorldTileContext.HillinessIntensity` is explicitly designed as a noise
  amplitude multiplier for local map generation, directly matching RimWorld's pattern.

---

### 3.5 Noise for Climate Fields (Temperature, Moisture/Rainfall)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Independent 6-octave Perlin for temperature (base scale ~1024 blocks) and humidity/vegetation. Abstract parameters — no physical relationship to geography. |
| Dwarf Fortress | ✓ | Fractal rainfall field (midpoint displacement) + orographic rain shadow refinement. Temperature = latitude + elevation penalty. Physical simulation. |
| No Man's Sky | ✗ | Climate determined by star color / planet seed, not noise fields. |
| RimWorld | ✓ | Temperature = latitude curve + Perlin offset (freq 0.018, ±4°C) + elevation penalty. Rainfall = Perlin (freq 0.015) × latitude curve. |
| Cataclysm:DDA | ~ | `region_settings` has seasonal temperature/rainfall; generation details sparse. |
| Tangledeep | ✗ | No climate model. |

**Islands status: PLAN (Phase M)**
Phase M.1: Temperature = base_temp - latitude_factor - (elevation × lapse_rate) +
coast_moderation + noise_perturbation. Phase M.2: Moisture = f(CoastDist,
FlowAccumulation, noise). Both consume existing fBm through `MapNoiseBridge2D`.

**Observations:** RimWorld's approach is the closest match to Islands' Phase M design.
Both use noise as a perturbation on top of physically-derived base values (latitude,
elevation, coast distance). This is a middle ground between Minecraft's abstract noise
parameters and DF's full physical simulation.

---

### 3.6 3D Density Functions (Volumetric Terrain)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | Core terrain mechanism. `final_density` expression tree evaluated at 4×8×4 noise cells. Positive = stone, non-positive = air. Enables overhangs, floating islands, caves in a single unified field. |
| Dwarf Fortress | ✗ | 2D heightfield + z-level stacking. No 3D density. |
| No Man's Sky | ✓ | Volumetric signed density field on spherified-cube. Marching cubes meshing at octree LOD nodes. |
| RimWorld | ✗ | 2D cell grid. No volumetric terrain. |
| Cataclysm:DDA | ✗ | Discrete z-layers, not volumetric. |
| Tangledeep | ✗ | 2D grid. |

**Islands status: —**
Not applicable. Islands is grid-first, 2D. The noise runtime supports 3D evaluation
but the map pipeline operates on 2D grids. No 3D density function is planned.

---

### 3.7 Per-Stone-Type Noise Competition

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✗ | Stone type determined by Y-level bands (stone/deepslate/tuff). |
| Dwarf Fortress | ✓ | Geological layers assigned per-region based on biome. Layer simulation, not noise. |
| No Man's Sky | ✗ | Mineral deposits via noise-placed resource points, not competition. |
| RimWorld | ✓ | **Each stone type gets its own Perlin noise layer; highest value wins per cell.** Produces large contiguous regions with organic flowing boundaries between stone types. |
| Cataclysm:DDA | ✗ | No stone type system. |
| Tangledeep | ✗ | N/A. |

**Islands status: —**
Not implemented. Not currently planned.

**Observations:** RimWorld's per-type noise competition is elegant and directly
applicable to Islands for any "which variant wins at this cell" problem: sub-biome
variation, soil types, ore distribution, terrain material selection. The pattern is:
evaluate N independent noise fields, assign the type whose noise is highest at each cell.

---

## 4. Noise Infrastructure & Architecture

### 4.1 Data-Driven Noise Configuration (JSON/XML-parameterized)

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | **Fully data-driven.** 50+ noise definitions in JSON. Density function DAG in JSON. Biome parameter ranges in JSON. Modders can reconfigure the entire terrain pipeline without code. |
| Dwarf Fortress | ✓ | World generation parameters (variance, mesh frequency weights, min/max per field) exposed in `d_init.txt` and advanced world generation UI. |
| No Man's Sky | ~ | VoxelGeneratorSettings datamined but not officially moddable. Parameterized internally. |
| RimWorld | ~ | Noise parameters hardcoded in C# (frequency, octaves, persistence). BiomeWorker scores are code-defined. Not data-driven. |
| Cataclysm:DDA | ✓ | `region_settings` JSON controls noise thresholds for forest, city spacing, map extras. |
| Tangledeep | ✓ | `mapgenerator.xml` controls floor parameters. But no noise configuration — it's structural, not noise-based. |

**Islands status: USED (partial)**
Noise parameters are currently set in stage code (C#). `MapTunables2D` exposes some
parameters (island aspect ratio, warp amplitude) via Inspector. `MapGenerationPreset`
ScriptableObject (Phase H3) provides preset-level configuration. Full data-driven noise
configuration (Minecraft-style JSON expression trees) is not implemented.

**Observations:** Islands' current approach (hardcoded noise parameters in stage code,
some Inspector tunables) matches RimWorld's level. For a toolkit/engine, moving toward
Minecraft's data-driven model would dramatically increase flexibility. This is not on the
roadmap but worth considering as the pipeline matures.

---

### 4.2 Deterministic Chunk/Tile Seed Derivation

| Game | Status | Notes |
|------|--------|-------|
| Minecraft | ✓ | `worldSeed → Xoroshiro128++ → chunk-local salt` per noise definition. |
| Dwarf Fortress | ✓ | Single seed → sequential PRNG for entire world (no chunk system). |
| No Man's Sky | ✓ | Coordinates → hierarchical seed derivation (galaxy → system → planet). |
| RimWorld | ✓ | `Gen.HashCombineInt(worldSeed, tileID) XOR GenStepSeedPart` per stage. |
| Cataclysm:DDA | ✓ | Per-overmap seed derivation. |
| Tangledeep | ✓ | Master RNG from world seed. |

**Islands status: USED**
`MapInputs` carries seed; stages derive noise seeds via `f(inputs.seed, stageSalt)`.
Determinism is a core invariant: same seed + inputs → identical output, snapshot-test gated.

---

## 5. Summary Matrix: Technique × Game × Islands

```
Technique              MC   DF   NMS  RW   CDDA TDP  | Islands Status
─────────────────────  ───  ───  ───  ───  ───  ───  | ──────────────
Coord Hashing          ✓    ✓    ✓    ✓    ✓    ✓    | IMPL+USED
Value Noise            ✗    ✗    ?    ✗    ?    ✗    | IMPL
Perlin Noise           ✓    ✗    ~    ✓    ~    ✗    | IMPL+USED
Simplex Noise          ✗    ✗    ?    ✗    ✗    ✗    | IMPL
Worley/Voronoi Noise   ✓¹   ✗    ✗    ✗    ✗    ✗    | IMPL
Midpoint Displacement  ✗    ✓    ✗    ✗    ✗    ✗    | —
fBm                    ✓    ~    ✓    ✓    ~    ✗    | IMPL+USED
Ridged Multifractal    ✗    ✗    ~    ✓    ✗    ✗    | IMPL(partial)
Turbulence             ✗    ✗    ✓    ✗    ✗    ✗    | IMPL
Domain Warping         ~    ✗    ✓    ✗    ✗    ✗    | IMPL(partial)+USED(simple)
Spline Remapping       ✓    ~    ?    ✗    ✗    ✗    | —
Power Redistribution   ✗    ✓    ?    ✗    ✗    ✗    | —
Scalar Composition     ✓    ✓    ✓    ✓    ~    ✗    | USED(basic)
Whittaker Biome        ✓    ✓    ✗    ✓    ✗    ✗    | PLAN(Phase M)
Island Masks           ✓    ✓    ✗    ✓    ✗    ✗    | USED
Cave Noise Threshold   ✓    ✗    ✓    ✓    ✗    ~    | —
Hilliness×Amplitude    ✓    ✓    ✓    ✓    ✗    ✗    | PLAN(Phase W)
Climate Noise Fields   ✓    ✓    ✗    ✓    ~    ✗    | PLAN(Phase M)
3D Density Functions   ✓    ✗    ✓    ✗    ✗    ✗    | — (2D pipeline)
Stone Noise Compete    ✗    ✓    ✗    ✓    ✗    ✗    | —
Data-Driven Config     ✓    ✓    ~    ✗    ✓    ✓²   | USED(partial)
Deterministic Seeding  ✓    ✓    ✓    ✓    ✓    ✓    | USED

¹ Minecraft uses Voronoi in 6D parameter space for biome, not spatial terrain
² Tangledeep's XML is structural, not noise-related
```

---

## 6. Priority Analysis: What's High-Value and Missing

### Tier 1 — High impact, directly relevant to planned phases

| Technique | Impact | Target Phase | Rationale |
|-----------|--------|-------------|-----------|
| **Spline Remapping** | Very High | M, W, general | Minecraft's terrain variety comes from this. Converts flat noise into dramatic landscapes. Simple to implement (evaluate piecewise cubic curve). No game achieves terrain variety without either spline remapping or simulation. |
| **Ridged Multifractal (full)** | High | K, W | Only noise technique that produces mountain ridges directly. RimWorld uses it for world elevation. Islands has partial support (Turbulence wrapper) but not the full Musgrave inter-octave feedback. |
| **Power Redistribution** | Medium | immediate | Trivial to implement (`pow(h, exp)`). Quick win for height field quality. DF uses it. |

### Tier 2 — Medium impact, extends pipeline richness

| Technique | Impact | Target Phase | Rationale |
|-----------|--------|-------------|-----------|
| **Domain Warping (full Quilez-style)** | Medium-High | J, K, W | NMS's organic terrain depends on it. Islands has simple warp (F2b) but not full fBm-into-fBm warping. Extends existing infrastructure. |
| **Noise Competition (per-type highest-wins)** | Medium | M2, future | RimWorld's stone-type pattern. Elegant for any "which variant here?" problem. Relevant to sub-biome variation. |
| **Multi-noise climate fields** | Medium | M | Already designed. Implementation is straightforward fBm + composition using existing MapNoiseBridge2D. |

### Tier 3 — Lower priority or naturally deferred

| Technique | Impact | Target Phase | Rationale |
|-----------|--------|-------------|-----------|
| **Data-driven noise config** | Long-term High | post-W | Important for toolkit/modding. Not critical until the pipeline is feature-complete. |
| **Heterogeneous/derivative-damped fBm** | Low-Medium | K, W | Advanced fBm variants. The noise runtime's Sample4 already carries derivatives, making derivative-damped fBm feasible. But standard fBm + spline remapping covers most needs. |
| **Cave noise thresholding** | Low (2D) | unplanned | Would need design work for 2D grid integration. Niche for Islands' current scope. |
| **3D density functions** | N/A | — | Islands is 2D grid-first. Not applicable. |
