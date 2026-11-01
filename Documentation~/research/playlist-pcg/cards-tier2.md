# Playlist «PCG» — Tier 2 technique cards

33 videos from groups B (tutorials) and D (math, noise, emergence) of [`../2026-11-01-playlist-pcg.md`](../2026-11-01-playlist-pcg.md). Research material, not authority.

**How they were made:** first written on 2026-11-01 from transcripts **sampled** in slices (the Chrome tool truncated script output at about 1 KB). **Re-read the same day with full transcripts** (YouTube transcript panel read with `get_page_text`) and rewritten card by card; each card ends with a «Changes vs. sampled card» line. 26 of 33 videos had a usable transcript; the rest (music-only clips, a garbled Korean track) are carded from title and description and say so. Islands status re-checked with Grep over `Runtime/`; the orchestrator spot-checked the main corrections (`MaskNeighborOps2D` only counts 4 cardinal neighbours; no dilate or majority filter in `Runtime/PCG`; `MaskTopologyOps2D.LabelConnectedComponents4` exists; the archive snapshot names match the Sunny Valley series).

Parts: 1 noise and tile tutorials · 2 Sunny Valley dungeon series, animation and art · 3 math and emergence.

---

## Part 1 cards (beginner/intermediate Unity tutorials)

Full transcripts read for all 9 videos (re-read 2026-11-01). All are auto-captions; CodingQuest's are tagged Dutch auto-generated and badly garbled (the gist is clear). Islands checks re-run with Grep/Glob on `D:\Projects\Islands\Runtime`: `MaskNeighborOps2D.cs` only has `CountCardinalOn` (4-neighbour) and `IsDeadEnd4` (no 8-neighbour/Moore count); `MaskFloodFillOps2D.cs` only has `FloodFillBorderConnected_NotSolid`; `MaskTopologyOps2D.LabelConnectedComponents4` exists (usable to keep the largest region); no cellular automata, WFC or corner/dual-grid autotiler.

### PERLIN NOISE in Unity – Procedural Generation Tutorial — Brackeys (10:34)
- **URL:** https://youtu.be/bG0uEXV6aHQ
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality.
- **Core idea:** Absolute-beginner texture demo: a 256x256 `Texture2D` on an 8x8 quad, filled pixel by pixel with Unity's `Mathf.PerlinNoise(x, y)` as grey. Opens with white noise vs "pseudo-random" smooth noise (0:18-0:48). The teaching is the classic pitfalls: integer inputs, scale, offset, `Apply`, lit material.
- **Techniques:**
  - Perlin noise as a 2D scalar field — D.4 — `sample = Mathf.PerlinNoise(xCoord, yCoord)`, `new Color(sample, sample, sample)` (5:15-6:02).
  - Normalised sampling coordinates — D.4 (usage pattern) — `xCoord = (float)x / width * scale`; the reason given: Perlin "repeats at whole numbers", so integer pixel coordinates give a flat image (6:06-7:20). Remember the float cast.
  - `texture.Apply()` after `SetPixel` — engine pitfall (7:22-7:40). Unlit Texture material because the default lit material in a light-less scene looks grey (7:55-8:10).
  - Scale as zoom — `scale` default 20; bigger scale crams more noise in, which reads as zooming out (8:14-8:45). Moved from `Start` to `Update` to tweak live (8:49).
  - Offset as pan and as "seed" — D.1/D.4 (usage pattern) — `+ offsetX/offsetY` (default 100) after scaling; `Random.Range(0, hugeNumber)` for both offsets in `Start` gives a different texture each run (9:05-10:05). Not a real seed.
- **Parameters / numbers:** 256x256 (he says "over 655,000" pixels: wrong, it's 65,536), quad scale 8x8, scale 20, offsets 100.
- **Determinism, seeds, chunks/seams:** Only "random offset instead of seed" with unseeded `UnityEngine.Random`; nothing on reproducibility or chunks.
- **Islands status:** Perlin noise — exists (`Runtime/Noise/Noise/Noise.Lattice.cs`, seeded via SmallXXHash, deterministic, Burst). Scale/offset/domain — exists (`TerrainNoiseSettings.cs` / MapNoiseBridge2D). Texture preview — exists (NoiseLab sample).
- **Where it could land in Islands:** Nowhere new. At most a "common mistakes" callout (integer coordinates, missing `Apply`) on the web Islands Lab Perlin page.
- **Value for Islands:** low — adds nothing technical; Islands already has a stronger, deterministic Perlin and a didactic scene.
- **Changes vs. sampled card:** Added white-vs-smooth-noise intro, lit-material pitfall, Start→Update for live tuning, quad scale; "flat at whole numbers" restated as the speaker's "repeats at whole numbers"; flagged the 655,000 figure as the speaker's miscount (not a caption error).

### MESH COLOR in Unity – Terrain Generation — Brackeys (13:15)
- **URL:** https://youtu.be/lNyZ9K71Vhc
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality.
- **Core idea:** Continues a mesh-terrain series: (a) UVs for a flat grid mesh so a texture shows, (b) per-vertex colour from a height gradient, shown with a vertex-colour Shader Graph in the Lightweight Render Pipeline. Octaves are shown already applied (three layers) but not taught.
- **Techniques:**
  - Grid UVs — NEW (rendering, not generation) — `uvs[i] = new Vector2((float)x/xSize, (float)z/zSize)`, same loop as vertices, `mesh.uv = uvs` (2:13-4:46). Without UVs the texture shows only as an averaged tint.
  - Height-to-colour ramp via vertex colours — NEW (presentation) — track `minTerrainHeight`/`maxTerrainHeight` while creating vertices, `height = Mathf.InverseLerp(min, max, vertices[i].y)`, `colors[i] = gradient.Evaluate(height)`, `mesh.colors = colors` (5:12-9:21).
  - Vertex-colour shader — LWRP package + pipeline asset in Graphics settings, Shader Graph (PBR graph) with a Vertex Color node into Albedo (9:30-11:30).
  - Live gradient tuning — terrain is rebuilt every frame, so editing the gradient updates it live; bands painted: light-blue water, green grass, dark-grey rock, snow (11:36-12:08). Saved with Copy Component / Paste Component Values (12:20).
  - Multi-octave noise — D.7 — only mentioned: "two more layers of noise... it has a name, octaves" (1:00-1:20).
- **Parameters / numbers:** Heights roughly 3-20 before normalisation; gradient keys by hand.
- **Determinism, seeds, chunks/seams:** Not discussed. Min/max normalisation is per mesh, so different chunks would get different colours for the same height (seam hazard, unstated). The min/max fields start at the float default 0, so the minimum never rises above 0 (minor unstated bug).
- **Islands status:** Height-ramp colouring — partial: `Noise.Gradient.cs` in the noise lib, `ScalarOverlayRenderer.cs` / `PCGMapTilemapVisualization.cs` colour scalar fields on tilemaps, palette library in the web Islands Lab. No 3D mesh/vertex-colour path (grid-first, adapters last). fBm — exists.
- **Where it could land in Islands:** Only if a 3D mesh adapter is built: a vertex-colour ramp over a fixed global height range, not per-mesh min/max.
- **Value for Islands:** low — rendering tutorial for 3D meshes; the one transferable point (normalise by a global range) is already how Islands treats fields.
- **Changes vs. sampled card:** Shader Graph is a PBR graph in LWRP (setup steps added); added live per-frame rebuild, copy/paste of component values, the min-starts-at-0 detail; no change of substance to the assessment.

### Procedural world generation with Perlin Noise — CodingQuest (14:29)
- **URL:** https://youtu.be/eiM_AStmeGE
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions tagged Dutch, badly garbled (gist reliable, exact wording not).
- **Core idea:** Python/PyGame 2D Civilization-style tile world: sum scaled Perlin layers into a height map, split the actual height range into terrain types by weights, and draw each tile by the terrain types at its four corners so coasts and beach-grass borders transition smoothly. Code and tileset on his GitHub.
- **Techniques:**
  - Background — Perlin (1983) made after Tron (1982) to get more natural textures; white noise vs gradient noise; a 1D profile read as mountains, valley, lake (0:01-3:20). Deep vs shallow water tiles from height bands (3:13).
  - Multi-octave sum of separate Perlin functions — D.7 — four Perlin generators with octaves 3, 6, 12, 24 (in his `perlin_noise` module "octaves" means frequency), each higher one scaled down more before adding (3:40-5:09, code 8:54-10:30). Seeded by a random `seed` parameter (9:34). Sampled at float coordinates = index / number of samples (10:16-10:30).
  - Height range split by weights — variant of D.10/D.20 — terrain types ordered low to high; total range measured from the world's actual lowest to highest value, divided over the types in proportion to a weight list (10:41-12:20). More ocean weight raises sea level and gives islands; low ocean weight and high grass/mountain/snow give continents (12:35-13:03). Sweeping the ranges made his intro animation (6:43). Water share is only approximately the weight share (range-based, not area-based).
  - Corner-based tile choice — D.18 (corner/Wang tileset) — the naive one-tile-per-terrain-type map shows hard stair-step coasts (7:10-7:40); instead sample (W+1) x (H+1) values, each tile reads the terrain types at its 4 corners and picks the tile with those corners (7:40-8:48, code 13:34-13:51). Adding a terrain type needs extra tiles drawn in the tileset (11:24).
- **Parameters / numbers:** Octaves 3/6/12/24, weight list per terrain type, noise map size+1 in X and Y.
- **Determinism, seeds, chunks/seams:** Seeded noise; no chunking.
- **Islands status:** Multi-octave sum — exists (fBm, `Runtime/Noise`). Band split — exists in a better form: exact quantile cuts (`Stage_Vegetation2D` global quantile cut; NoiseLab percentage water). Corner-based autotiling — missing: `TilemapAdapter2D` + `TilesetConfig` (H6 `ruleTile` slot only) + `BiomeTileOverride` + `MegaTile*` choose per cell, no corner or bitmask selection.
- **Where it could land in Islands:** (1) Tilemap adapter: optional corner/dual-grid tile selection reading the terrain/biome id of the 4 cells around each vertex into a 16-entry tileset per pair of types. (2) Web Islands Lab: short note comparing weight bands (range share) with quantile cuts (area share).
- **Value for Islands:** medium — corner-sampled transitions fill a real gap in the tilemap adapter; the rest Islands already exceeds.
- **Changes vs. sampled card:** Added history intro, deep/shallow water bands, naive-vs-corner comparison, "new type needs new tiles" cost, continents vs islands from weights, float sampling detail; caption language corrected to Dutch-tagged auto-captions.

### Procedural Generation In Unity 2D With Tile & Perlin noise — ChronoABI (24:02)
- **URL:** https://youtu.be/AqXC-QmhRXQ
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, fair quality.
- **Core idea:** Part 2 of a side-view terrain series: replace GameObject-per-tile spawning with three `Tilemap`s (dirt, grass, stone) and replace the random column height of part 1 with 1D Perlin noise. Shows height vs smoothness and a seed as Perlin's second argument.
- **Techniques:**
  - Tilemap instead of GameObjects — engine usage — `tilemap.SetTile(new Vector3Int(x, y, 0), tile)` per layer; 300 x 100 = 30,000 tiles runs fine (6:04-10:25). The per-column stone/dirt split (random stone depth) is kept from part 1 (1:01-1:27).
  - 1D heightline from Perlin — D.10 (side-view) — demo first with `Mathf.PerlinNoise(Time.time, 0)` moving an object (11:20-12:40), then `height = Mathf.RoundToInt(heightValue * Mathf.PerlinNoise(x / smoothness, seed))` per column (13:00-16:20). Height scales the output; smoothness divides the input (lower frequency), so "height > smoothness" is jagged and "smoothness > height" is smooth (16:42-17:05).
  - Regenerate/clear keys — A regenerates, D calls `ClearAllTiles()` on the three tilemaps (17:46-19:00).
  - Seed as Perlin's second argument — D.1 (usage pattern) — with seed fixed at 0 every run gives the same terrain; seed 1 or 1000 gives a different one; `seed = Random.Range(-1000000, 1000000)` before each generation (20:20-22:15).
- **Parameters / numbers:** width 80 (demo) / 300x100 (stress test), height and smoothness sliders 0-100 (demo 50/20, 50/50), seed range ±1,000,000.
- **Determinism, seeds, chunks/seams:** The seed is a float offset (not a PRNG seed) drawn from unseeded `UnityEngine.Random`; reproducible only if you keep the value. Smoothness slider allows 0 (division by zero, unstated). No chunking.
- **Islands status:** Heightline from noise — exists in spirit (top-down heightmap, `Stage_BaseTerrain2D`); side-view platformer terrain — out of scope. Tilemap output — exists (`TilemapAdapter2D`, per-layer groups via `TilemapLayerGroup`/`TilemapLayerEntry`).
- **Where it could land in Islands:** Nowhere. A 1D side-view mode could be a web Islands Lab toy only.
- **Value for Islands:** low — Islands already has the Tilemap adapter and deterministic noise; this video is weaker on both.
- **Changes vs. sampled card:** Corrected the formula: smoothness divides the input coordinate (frequency), not the output; added the `Time.time` demo, A/D regenerate-clear keys, slider ranges and divide-by-zero caveat, the seed-0 sameness demo; the "SaveAsPrefab" item from the sampled card is not in this video (removed).

### Tile Map auto generation in Unity – TUTORIAL — Daniel Hofmann (32:38)
- **URL:** https://youtu.be/xNqqfABXTNQ
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality.
- **Core idea:** Cellular-automata island/cave generator on a Unity `Tilemap` (Unity 2017.3 beta), framed as derived from Conway's Game of Life: random fill, then birth/death-limit steps; two tilemaps (terrain on top, water/lava fully filled underneath); left click steps the simulation, right click resets, middle click saves the grid as a prefab. Result stays editable with the tile palette.
- **Techniques:**
  - Random init — D.12 — `terrainMap[x,y] = Random.Range(1,101) < initChance ? 1 : 0` (9:40-10:35). Note `< 25` over 1..100 gives 24%, not 25%.
  - Birth/death-limit step — D.12 — for each cell, count the 8 neighbours by iterating a `BoundsInt(-1,-1,0, 3,3,1)` and skipping (0,0) (13:20-14:50); out-of-map neighbours count as alive, which draws a solid border ("comment it out if you don't like the border", 15:00-16:20, 26:20). Alive cell with `neighbours < deathLimit` dies, else survives; dead cell with `neighbours > birthLimit` is born, else stays dead (16:49-18:00). Double buffered (`oldMap` -> `newMap`).
  - Stepping semantics — `doSim(nu)` runs `nu` steps on the existing array; a new random array is made only if the array is null (after a right-click `clearMap(true)`) (6:32-11:20, 20:35-21:35). So each left click continues smoothing the same map.
  - Placement — top tile where value 1, bottom tile everywhere; cell at `(-x + width/2, -y + height/2)` to centre the map (this also mirrors both axes, unstated) (18:20-20:20).
  - Save as prefab — editor-only: find the Grid by name, `PrefabUtility` to `Assets/<name><counter>.prefab`, `EditorUtility.DisplayDialog` on success/failure; he warns it can overwrite (26:30-30:10).
  - Swap tile assets after the fact — animated water tile and scripted tiles give a game look (31:10-32:10).
- **Parameters / numbers:** initChance 25 (slider 0-100), birthLimit 3, deathLimit 2 (sliders 1-8), repetitions 1 per click (slider 1-10), map 120x70, orthographic size 45, 24x24 px sprite, PPU 24, point filter, no compression, top map order in layer 1. With these values: alive survives with ≥2 neighbours, dead is born with ≥4: a growth rule suited to a sparse 25% start.
- **Determinism, seeds, chunks/seams:** `UnityEngine.Random` with no seed, so not reproducible; no chunking. Edge handled by treating outside as alive.
- **Islands status:** CA generator — missing (nothing under `Runtime/PCG`). Building blocks — partial: `MaskNeighborOps2D.cs` counts only the 4 cardinal neighbours (`CountCardinalOn`), so an 8-neighbour count would be new; `MaskTopologyOps2D.LabelConnectedComponents4` exists for keeping the largest region; `MaskFloodFillOps2D.FloodFillBorderConnected_NotSolid` exists for border-connected fills. Tilemap output and layers — exists (`TilemapAdapter2D`, `TilemapLayerGroup`).
- **Where it could land in Islands:** New layout generator next to `SimpleRandomWalk2D`/`IteratedRandomWalk2D` in `Runtime/PCG/Layout/` (e.g. `CellularCaves2D`): seeded through `LayoutSeedUtil`, a new `CountMooreOn` (8-neighbour) in `MaskNeighborOps2D`, ping-pong buffers, explicit border policy (alive/dead/wrap), birth/death limits and step count as tunables, then `LabelConnectedComponents4` to keep the largest region. Also as a mask-smoothing operator on existing masks (shore, regions), and a web Islands Lab page. Hofmann's 25% / B>3 / D<2 is a growth rule; the more common Lague form starts near 45-50% with "wall if >4 neighbours" and gives different shapes, so expose both as parameter presets.
- **Value for Islands:** medium — clearest walkthrough of D.12, which Islands lacks; implement deterministically, not with the unseeded RNG.
- **Changes vs. sampled card:** Corrected Islands status (no 8-neighbour count in `MaskNeighborOps2D`; flood fill does not keep the largest region, `LabelConnectedComponents4` does); corrected "equivalent to the Lague form" (it is a different growth rule); added the 24% off-by-one, `BoundsInt` neighbour loop, continue-vs-reset stepping, axis mirroring, prefab overwrite warning, import settings, Unity 2017.3.

### How to store data in tiles (Unity Tilemap + Scriptable Objects) — Shack Man (9:33)
- **URL:** https://youtu.be/XIqtZnqutGg
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality.
- **Core idea:** Gameplay data per tile TYPE (walking speed, poison) lives in a `ScriptableObject` that wraps a list of `TileBase`; a `MapManager` builds a `Dictionary<TileBase, TileData>` in `Awake` so any script can ask for the data under a world position. Explicitly global per type, not per-cell mutable state.
- **Techniques:**
  - Tile type to data lookup — gameplay data — `TileData : ScriptableObject { List<TileBase> tiles; float walkingSpeed; float poison; }` with `CreateAssetMenu`; `TileBase` so custom scripted tiles work (2:00-3:00). Dictionary built automatically to avoid mix-ups (5:10-5:45). `GetTileWalkingSpeed(Vector2 worldPos)`: `WorldToCell`, `GetTile`, lookup, return 1 if no tile (6:24-6:52). Two sprite variants share one data asset.
  - Consumer decoupling — bugs move at `baseSpeed * walkingSpeed` and only pass their position; they don't know a tilemap exists (7:20-7:57). Runs smoothly with every bug querying every frame (9:06).
  - Per-cell mutable state (stickiness wearing off) — he says use structs, in a future video (0:44-0:59).
- **Parameters / numbers:** Walking speed 1 (grass), 2 (stone). Tiles twice the cell size: fix grid cell size or double PPU (3:27).
- **Determinism, seeds, chunks/seams:** Not relevant.
- **Islands status:** Tile-to-gameplay-data lookup — partial/by design different: `TilesetConfig` / `BiomeTileOverride` map ids to tiles (data → display); semantics live in `MapLayerId` masks / `MapFieldId` fields, a better source of truth than reading tiles back.
- **Where it could land in Islands:** mundo-islas (gameplay) only: query biome/mask layers directly for movement cost; the "consumer passes a position, a service answers" decoupling is the right shape. The video is a counter-example of display-asset-as-data.
- **Value for Islands:** low — Islands separates data layers from rendering by design; nothing to adopt.
- **Changes vs. sampled card:** List not array; added the `Awake` auto-build, consumer decoupling, per-frame query cost, "structs" for per-cell state, cell-size fix; no change of substance.

### Wave function collapse Unity Tutorial 01 Introduction — Sunny Valley Studio (5:56)
- **URL:** https://youtu.be/ws4r3wLPNSE
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality.
- **Core idea:** Series intro only: WFC generates a larger tilemap that resembles one small input; one example gives many outputs. Promises an object-oriented, reusable implementation: input reader, pattern manager, solver, output creator. No algorithm is explained here.
- **Techniques:**
  - Wave Function Collapse (overlapping model, by description: "reuses pixel values in different spots") — D.16 — only demoed on Oskar Stålberg's web tool and texture-synthesis examples (1:08-2:45); the course plan is a theory part then code from input reader to output creator (3:55-4:35). "The algorithm can be tweaked by applying more rules" (3:11).
  - Adapter-style architecture — core works on generic values for any grid; input/output interfaces for tilemaps can be swapped (3:20-3:53). Same philosophy as Islands' core-vs-adapter split.
- **Parameters / numbers:** None. Kenney assets; GitHub repo with a starter project and each step; resources: Stålberg's talk and two articles (4:41-5:20). The playlist has 23 lessons.
- **Determinism, seeds, chunks/seams:** Not discussed.
- **Islands status:** WFC — missing in code (no WFC class under `Runtime/PCG`). Core/adapter split — exists (design principle).
- **Where it could land in Islands:** Nothing from this video. If WFC is ever added, the later lessons of this 23-lesson course (and its GitHub steps) are the reference.
- **Value for Islands:** low — pure introduction, no algorithm content.
- **Changes vs. sampled card:** Added course length (23 lessons), resources list, GitHub per-step repo, "more rules" remark; no change of substance.

### Procedural 2D Map with Caves P1 Basic Perlin Noise – Unity 2020 — Sunny Valley Studio (14:16)
- **URL:** https://youtu.be/MTNt32_NQlc
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality.
- **Core idea:** Part 1 of 2. Sets up a side-view tilemap world (ground, background and a "Perlin 2D" tilemap; `WorldRenderer` and `WorldGenerator` scripts; Kenney voxel pack) and introduces `Mathf.PerlinNoise` on a 1D heightline: raw values shown as text meshes, then dirt columns up to the noise height. The intro shows the finished map with caves and overhangs as darker areas from 2D Perlin (0:13-0:28), but caves and multi-octave noise are in part 2.
- **Techniques:**
  - 1D heightline from Perlin — D.10 (side-view) — `GetNoiseValue(x, y) = Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude`, called with y = 1; floor to int; fill `y = 0..height` with dirt (7:58-12:20).
  - Amplitude and frequency — D.4 (usage) — amplitude scales 0..1 to 0..N tiles; frequency must not leave integer inputs (Unity's Perlin returns the same value at integers) (6:26-7:20). Frequency 0.01 gives one big hill; 0.05 bigger, sparser hills; 0.08-0.09 more hills; 0.1 many hills (12:43-13:50). He judges the single-octave result "very repetitive" (13:55).
  - Value debug view — a TextMesh per sample with the value to 3 decimals; neighbouring raw values read 0.46-0.48 (10:40), i.e. Unity's Perlin hovers near 0.5 and rarely reaches 0 or 1 at low frequency.
- **Parameters / numbers:** map length 50, amplitude 1 -> 10 -> 50, frequency 0.01 / 0.05 / 0.08 / 0.09 / 0.1.
- **Determinism, seeds, chunks/seams:** No seed or offset: same output each run.
- **Islands status:** Perlin, frequency, amplitude — exists (`Noise.Lattice.cs`, `TerrainNoiseSettings.cs`). The "raw values cluster near 0.5" observation matches the noise-range issue Islands already fixed (`PerlinOnSimplex` gradient, normalised fBm, 2026-10-30/31) and the NoiseLab histogram. 2D noise threshold as a cave mask — not in this part. Side-view columns — out of scope.
- **Where it could land in Islands:** Nothing; the 0.46-0.48 readout is a nice concrete example for the NoiseLab / web Lab explanation of noise range.
- **Value for Islands:** low — duplicates Brackeys' basics; the cave content sits in part 2.
- **Changes vs. sampled card:** Added the scene setup (three tilemaps, renderer/generator split), the intro preview of 2D-Perlin caves, the exact `GetNoiseValue` form, the 0.46-0.48 raw readout linked to Islands' noise-range fix, the frequency-to-shape mapping as stated.

### I Made Minecraft in 24 Hours — Sam Hogan (13:28)
- **URL:** https://youtu.be/Nj8gt_92c-M
- **Source note:** full transcript read (re-read 2026-11-01); auto-captions, good quality (long music-only stretches).
- **Core idea:** A 24-hour, no-internet speed-run dev log of a Minecraft-like in Unity with one downloaded noise library. The generation parts are brief but give a layered recipe (surface, caves, strata, trees, water) and one clear determinism trick for trees.
- **Techniques:**
  - Why voxels, not a heightmap of cubes — 20x20 cubes on 2D Perlin first; at 200x200 (~40,000 cubes) performance is bad, a cube more than one unit above its neighbour leaves visible gaps, and caves/mining are impossible (0:33-1:47).
  - Chunked voxel meshes with face culling — NEW (3D voxel meshing) — loop blocks in a chunk; emit a quad only where land touches air (1:50-2:55). Mesh colliders per chunk; single texture atlas per chunk mesh (4:20).
  - Infinite terrain by chunk streaming — NEW-ish — the terrain script generates voxel data and spawns chunks; dictionary of loaded chunks; load around the player when entering a new chunk, unload distant ones (3:00-3:55). "Seamless transitions" after a fix (3:12-3:21), implying noise sampled at world coordinates (not stated).
  - Surface = several 2D noise layers at different scales; multiplying two layers makes some regions hilly and others flat — D.7 plus modulation (5:48-6:03).
  - 3D noise for caves and overhangs, masked — NEW (3D carve) — generate the land, cut out cells where 3D noise passes a threshold; another noise layer as a mask so caves aren't everywhere (5:37-6:47).
  - Stone/dirt strata — a second, lower heightmap turns dirt into stone; enough variation that stone sometimes reaches the surface (6:48-7:14).
  - Chunk-seeded RNG for trees — D.1 — a PRNG seeded with the chunk coordinates, so a chunk unloaded and reloaded rebuilds the exact same trees (7:22-8:14).
  - Water as a separate simplified "water chunk" mesh shaped around the terrain, so it is not inside land or over cave entrances, and can use another material and be walked through (8:28-9:07).
  - Edit voxel data then regenerate the chunk mesh; raycast to pick the block (4:51-5:18). Inventory, skybox, post-processing — n/a.
- **Parameters / numbers:** 20x20 test grid, 200x200 (~40,000 cubes); no noise values given.
- **Determinism, seeds, chunks/seams:** Per-chunk RNG seeded by chunk coordinates (load order never changes results); world-coordinate sampling implied by seamless chunks. Nothing on a global world seed.
- **Islands status:** Multi-octave and layered noise — exists (fBm/ridged in `Runtime/Noise`, MapNoiseBridge2D). Product of two layers as a hilliness mask — partial (fields are composed; no explicit recipe checked). Coordinate-seeded RNG — exists (`LayoutSeedUtil.cs`, SmallXXHash). Chunk streaming — missing (not in Islands' scope; the web "Mar de puntos" scene did chunks). 3D voxel meshes, 3D cave carving, water mesh — missing, out of the 2D grid-first scope. `research\Games Worldgen\` covers real Minecraft worldgen in more depth.
- **Where it could land in Islands:** Mostly nowhere. "Multiply two layers to vary hilliness regionally" could be a tunable in `Stage_Hills2D` or a web Lab explainer; the chunk-seeded RNG is worth quoting when explaining chunk determinism.
- **Value for Islands:** low — entertaining overview; the repo's Minecraft research and Islands' own pipeline go far deeper.
- **Changes vs. sampled card:** Added why the cube heightmap fails (gaps for steps >1, caves impossible), 3D noise also gives overhangs, texture atlas, mesh colliders, water chunk details; no change of substance.

---

## Part 2 cards (dungeon series, animation, art, design)

Full transcripts read for 10 of 11 videos (re-read 2026-11-01). `2qeNu2QApAM` (Unity, pixel-perfect lights) has only a garbled Korean auto-caption track: card from title/description. t3ssel8r has human-written English captions; the rest are English auto-captions, all clean enough.

Note on lineage (new evidence, replaces the old "very likely, uncredited" note): the archived `Documentation~/archive/PCG_Pipeline_Technical_Snapshot.md` describes the **legacy** "PCG example project" that Islands salvaged. It uses the exact names of the Sunny Valley series: a static `ProceduralGenerationAlgorithms` class with `SimpleRandomWalk` / `IteratedSimpleRandomWalk` / `RandomWalkCorridor` / `BinarySpacePartitioning`, a `Direction2D` helper, `HashSet<Vector2Int>` floor sets, a `TilemapVisualizer`, and `SimpleRandomWalkDungeonGenerator : AbstractDungeonGenerator`, `CorridorFirstDungeonGenerator`, `RoomFirstDungeonGenerator`. P4 confirms `ProceduralGenerationAlgorithms`, `SimpleRandomWalk`, `Direction2D` and `HashSet<Vector2Int>` on camera. So the chain is **Sunny Valley series → legacy PCG project (extended with skew, cellular automata, wall bitmasks) → Islands Runtime rewrite**. The Runtime code is a rewrite, not a copy (details in the P4 card). Still no credit anywhere in Runtime.

### Theory - P2 - Unity Procedural Generation of a 2D Dungeon — Sunny Valley Studio (4:11)
- **URL:** https://youtu.be/jlNqp_QAibo
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean; YouTube chapters: Intro, Concept, Room First, Random Walk, Binary Space Partitioning, Conditions.
- **Core idea:** A dungeon is one room or a set of rooms, and every area must be traversable, so besides rooms you need corridors. Two methods, corridors-first and rooms-first, built from two algorithms: random walk and binary space partitioning (BSP). Theory only, no code.
- **Techniques:**
  - Corridors-first — NEW (not in D.1-D.20; closest is D.14) — make a series of corridors first, so you know they are connected, then pick random points along their path and grow a room there, "for example" with a random walk (0:42-1:04).
  - Rooms-first — D.13 — make the playable areas first, then connect them. He calls connecting them "in a meaningful way" the hard part and promises only "a very simple method" (1:05-1:31).
  - Random walk — D.14 — an agent at a start position picks a random direction, takes one step, and repeats for a set number of steps. Repeating walks from the same start gives a dense "island shape" near the start (1:32-2:06).
  - Iterated random walk — D.14 — run several iterations, each new agent starting from an already-populated position. This keeps new paths connected to old ones and covers "a much bigger area" than always restarting at the same start (2:06-2:32).
  - BSP — D.13 — split a big space vertically or horizontally, again and again, until pieces are too small to split but can still fit a room of the set minimum width and height. He leaves the exact split conditions for the implementation episode (2:34-3:10).
  - Offset — D.13 — shrink each area by an offset ("like one tile from each side") so the rooms are separated by walls; otherwise you get one big room (3:11-3:30).
  - Walls around floor and corridors — D.18 — postponed to the end of the series (3:39-3:53).
- **Parameters / numbers:** minimum room width and height; steps per walk; number of iterations; offset of about 1 tile per side. Target engine: Unity 2020.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:**
  - Simple random walk: exists, `Runtime/PCG/Layout/SimpleRandomWalk2D.cs`.
  - Iterated random walk: exists, `IteratedRandomWalk2D.cs` (restart from a random ON cell via `TryGetRandomSetBit`, with a probability instead of always).
  - Corridors-first: exists, `CorridorFirstDungeon2D.cs`.
  - BSP rooms-first with padding: exists, `RoomFirstBspDungeon2D.cs` + `Layout/Bsp/BspPartition2D.cs`.
  - Walls: no wall classifier in Runtime. The legacy project had one (neighbour bitmask catalogue, per the archive snapshot); not ported. Phase H6 has a `ruleTile` slot in `TilesetConfig.cs`.
- **Where it could land in Islands:** nothing new in core. Origin reference for the layout docs, and intro text for a web page comparing corridors-first and rooms-first.
- **Value for Islands:** low — already implemented; useful only as the documented origin of four layout strategies.
- **Changes vs. sampled card:** minute marks corrected to the chapters (corridors-first 0:42, rooms-first 1:05, random walk 1:32, iterated 2:06, BSP 2:34, offset 3:11, walls 3:39). Added: a single walk repeated from the same start gives the "island shape"; the split conditions are deferred. Islands status adds that the iterated restart is probabilistic and that the legacy wall classifier was not ported.

### Project setup - P3 — Sunny Valley Studio (5:43)
- **URL:** https://youtu.be/9W_ZoPRQ8OQ
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean.
- **Core idea:** Sets up the Unity 2D project for the series: 2D preset, a starter package from the GitHub repo (tileset, tile palette, named tiles), checks for the 2D Sprite and 2D Tilemap Editor packages, and turns off anti-aliasing. Nothing about the algorithms.
- **Techniques:**
  - Starter assets: a third-party dungeon tileset (credited to a pixel-art creator in the description), already sliced in the Sprite Editor (0:45-2:00).
  - Wall tiles by side: the tileset has different tiles for top, bottom, left and right walls plus several corners for different situations (he calls the projection "axiometric"). Getting them right is "a bit of work", left for the end of the series. The floor tile and each wall tile are named so they can be assigned to the right field of the wall visualizer script (2:49-4:07). D.18-adjacent (wall selection by neighbourhood comes later).
  - Anti-aliasing off in Project Settings > Quality: AA pulls pixels from neighbouring tiles of the tilemap and causes seam artifacts when zoomed (4:10-5:08).
- **Parameters / numbers:** Unity 2020.1.14 ("shouldn't really matter"); 2D preset; URP optional.
- **Determinism, seeds, chunks/seams:** not discussed. Seams here are a rendering artifact only.
- **Islands status:** n/a (setup). The split it implies (algorithm returns positions, a separate visualizer paints tiles) exists in Islands as headless grid core plus `Runtime/PCG/Adapters/Tilemap/`. The many-wall-tile selection is not in Runtime (see P2).
- **Where it could land in Islands:** a one-line tip in the Tilemap adapter docs or the host project setup: disable anti-aliasing (and use point filtering / pixel-perfect camera) to avoid tile seams.
- **Value for Islands:** low — only the anti-aliasing seam tip.
- **Changes vs. sampled card:** corrected the anti-aliasing minute (about 4:10-5:08, not about 3:00) and added the reason (AA samples neighbouring tiles). Added: starter package from GitHub, required 2D packages, per-side wall tiles and corners named for the visualizer.

### Random Walk Algorithm - P4 — Sunny Valley Studio (9:19)
- **URL:** https://youtu.be/F_Zc1nvtB0o
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean; chapters: Intro, Create Script, Visual Studio, Random Walk, Hashset, Hashset Methods, Implementation.
- **Core idea:** Implements the simple random walk in a static class `ProceduralGenerationAlgorithms` (not a MonoBehaviour), returning a `HashSet<Vector2Int>` path, plus a small static `Direction2D` class in the same file. Using the walk to make a floor is left to P5.
- **Techniques:**
  - Static algorithm library — `public static class ProceduralGenerationAlgorithms`, so any class can call the algorithms (0:33-1:18).
  - Simple random walk — D.14 — `SimpleRandomWalk(Vector2Int startPosition, int walkLength)`: add start to the path; `previousPosition = start`; loop `walkLength` times: `newPosition = previousPosition + Direction2D.GetRandomCardinalDirection()`, add to path, `previousPosition = newPosition`; return path (1:20-8:35).
  - Dedup by HashSet — D.14 — `Vector2Int` overrides `Equals` and `GetHashCode`, so the set removes cells the walk steps on twice (2:00-2:51). Mentions `UnionWith`, `IntersectWith`, `ExceptWith`, `SymmetricExceptWith` as useful later (2:53-3:24).
  - Direction helper — `Direction2D.cardinalDirectionsList` = up (0,1), right (1,0), down (0,-1), left (-1,0), clockwise; `GetRandomCardinalDirection()` returns `list[Random.Range(0, count)]` (5:00-8:00).
- **Parameters / numbers:** `startPosition`, `walkLength`; 4 cardinal directions. No bounds, no grid: the walk is unbounded in an infinite integer plane.
- **Determinism, seeds, chunks/seams:** uses `UnityEngine.Random.Range`, no seed. Not deterministic as written; no bounds handling.
- **Islands status (port check, compared line by line with `SimpleRandomWalk2D.cs`, `IteratedRandomWalk2D.cs`, `Direction2D.cs`):**
  - Shared: the concept and loop shape (start, step `walkLength` times by a random cardinal, mark each cell), the parameter name `walkLength`, the helper class name `Direction2D`, and the idea of deduplication (Islands gets it free by writing bits into a `MaskGrid2D`).
  - Different: method `Walk(ref MaskGrid2D dst, ref Random rng, int2 start, int walkLength, int brushRadius, float skewX, float skewY, int maxRetries)` vs `SimpleRandomWalk(startPosition, walkLength)`; `Direction2D.Cardinal` order is +X, -X, +Y, -Y (not up/right/down/left clockwise); seeded `Unity.Mathematics.Random`; in-bounds bounce with retries and stop-early; disc brush; skew via `PickSkewedCardinal`, whose comments call it "legacy skewX/skewY semantics". None of these comments mention the tutorial; skew is not in P4.
  - Verdict: P4 alone **does not prove** a direct port of the Runtime code: it is a clean rewrite. The lineage is confirmed indirectly through the legacy project in `Documentation~/archive/PCG_Pipeline_Technical_Snapshot.md`, which uses the series' class and method names verbatim (see the note at the top). The "legacy" in Runtime comments points to that project, which had already added skew.
- **Where it could land in Islands:** nothing missing. A short "origins" line in the layout docs crediting the Sunny Valley series via the legacy project would make the lineage traceable.
- **Value for Islands:** low — Islands is a superset; only provenance value.
- **Changes vs. sampled card:** added the exact code structure, names and direction order from the full transcript and a real port comparison. Corrected "very likely ported from this series" to: Runtime is a rewrite; lineage proven through the archived legacy snapshot, not by the Runtime code itself. Confirmed the RNG is `UnityEngine.Random.Range` (the old card hedged "in the part sampled").

### Procedural Animation: Tail, Wings, Hair, Tentacles! — Blackthornprod (14:14)
- **URL:** https://youtu.be/9hTnlp9_wX8
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean. Sponsored segment 0:00-0:08 and 13:01-14:00.
- **Core idea:** Keyframe-free wiggly appendages with a LineRenderer whose points each chase the previous one. The technique and script are credited to indie dev Owen Senior (1:05). A head sprite rotates toward and moves to the cursor; tentacles, tails, wings and hair are the same rig with different sprites.
- **Techniques:**
  - Head: rotate toward target (rotation speed about 20) and `MoveTowards` the cursor at `moveSpeed * deltaTime` (1:11-2:43).
  - Tentacle v1, follow-the-leader with lag — NEW (animation) — `segmentPoses[length]`; point 0 = a `targetDir` transform; for i ≥ 1, `SmoothDamp(current, segmentPoses[i-1] + targetDir.right * targetDist, ref velocity[i], smoothSpeed + i / trailSpeed)`. The `targetDist` offset keeps points from bunching when still. Smaller `smoothSpeed` = faster catch-up (4:00-7:00). This version stretches when the head speeds up or stops.
  - Sine wiggle — NEW — `wiggleDir` parent of `targetDir`, rotated by a sine of time: `wiggleSpeed` 10, `wiggleMagnitude` 20 (7:21-8:07).
  - Tentacle v2, fixed segment length — NEW — replace SmoothDamp with: target = previous point + normalized(current − previous) × `targetDist`, then smooth toward it. Keeps form and curvature at any speed; best for snakes, worms, dragons. `smoothSpeed` 0.2 lags, 0.001 snaps (9:04-10:40). This is the classic distance-constraint (FABRIK-like single pass) chain.
  - Attachments: `tailEnd` transform at the last point; `bodyParts[]` each set to `segmentPoses[i]`, with a body-rotation script so each part faces the previous one (10:40-12:04).
  - `ResetPos()` in Start to spread the points so they do not start stacked at (0,0) (12:24-12:42).
  - Look: LineRenderer width curve (can alternate thick/thin), colour gradient with a transparent tip, end-cap vertices 1, unlit transparent material with a sprite that bends with the line; wings from a feather sprite (3:00-3:40, 8:08-8:52, 12:17).
- **Parameters / numbers:** length 30, `targetDist` 0.2, `smoothSpeed` 0.02-0.05 (v1), `trailSpeed` 350, `wiggleSpeed` 10, `wiggleMagnitude` 20, v2 `smoothSpeed` 0.2 vs 0.001, head rotation speed about 20.
- **Determinism, seeds, chunks/seams:** not discussed; frame-time driven (SmoothDamp, Time), runtime only.
- **Islands status:** n/a for the generator.
- **Where it could land in Islands:** Fauna Lab (web, p5.js): v2 chain for the pudú tail or ears, fish, snakes and the primordial serpent of the colonization game; the sine-rotated target gives swimming motion. mundo-islas: cheap secondary motion for creatures. Not core.
- **Value for Islands:** low — no generation content; medium only for Fauna Lab animation (the v2 fixed-length chain is the useful bit).
- **Changes vs. sampled card:** added the second script (fixed-length chain, 9:04), the exact SmoothDamp formula with `targetDist` and `i / trailSpeed`, all numbers (30, 0.2, 0.02-0.05, 350, 10, 20), body-part attachments, `ResetPos`, and credit to Owen Senior. Corrected "smaller smooth time means a stiffer follow" to "smaller = faster catch-up" and the wiggle: it rotates a parent transform (magnitude 20), not an amplitude in units.

### Creating a Scene for my 3D Pixel Art Game — t3ssel8r (5:01)
- **URL:** https://youtu.be/ERA7-I5nPAU
- **Source note:** full transcript read (re-read 2026-11-01). Human-written English captions (sparse narration over a time-lapse; recorded 2020-11-14).
- **Core idea:** Start-to-finish time-lapse of one scene in his custom pixel-art rendering engine: everything modelled from simple primitives in Blender, detailed with procedural displacement, imported to Unity (where displacement is baked), then terrain height painted and grass/leaf sprites configured. Under 2 hours in total.
- **Techniques:**
  - Primitive kit: stairs from a stretched cube with the Array modifier; pillars by simple extrusions; trees as cylinders with merged spheres on top; rocks as subsurfed cubes; damage on stone tiles with a Boolean cut; a bigger tree from extruded cylinders; bushes with the same sphere method (0:11-1:28).
  - One tree, duplicated around the scene with varied rotation and scale (0:44-0:51).
  - Procedural detail: displacement + remesh on rocks (lower poly count); brick-like procedural detail on stone blocks; random displacement on leaves with edited normals for better lighting; woody displacement on trunks, then decimation (1:43-2:34).
  - Import: the engine bakes all procedural displacement into static geometry automatically (2:34-2:47).
  - Unity terrain: paint the height map, a dirt path and light hills (2:47-3:47); "a few final configurations" tell the engine where to draw leaf and grass sprites (3:10-3:38). How sprites are distributed is not shown.
  - Iteration: big-tree leaves looked "too spherical", fixed back in Blender (3:47-4:06).
- **Parameters / numbers:** just under 2 h per scene. No others.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a for the rendering. Related: Islands has a `Vegetation` mask layer (`MapIds2D.cs`, id 7) produced by `Stage_Vegetation2D.cs`; there is no sprite-scatter adapter on 3D terrain.
- **Where it could land in Islands:** art-direction reference for mundo-islas (low-poly 3D rendered as pixel art). Flora Lab / Hillside / Palmar: "one model, duplicated with random rotation and scale" plus a few blob-crown variants for cheap variety, and the note that crowns must not look too spherical. A later adapter could turn the Islands `Vegetation` mask into sprite scatter.
- **Value for Islands:** low — art reference only.
- **Changes vs. sampled card:** corrected "paint where leaf and grass sprites should be drawn (a painted mask)": the narration only says configurations tell the engine where to draw them (3:10); no painted vegetation mask is shown. Added the full primitive kit (Boolean damage, edited leaf normals, decimation), the "too spherical" fix and minute marks; Islands status now cites `Stage_Vegetation2D.cs`.

### Pixel Perfect Lights and Particles in Unity! — Unity (8:52)
- **URL:** https://youtu.be/2qeNu2QApAM
- **Source note:** no transcript: from title/description only. The only caption track is Korean auto-generated and is garbled noise (re-checked 2026-11-01); no English track offered.
- **Core idea:** Per the description, shows how to use the Universal Render Pipeline 2D Renderer and 2D Lights to make pixel-perfect lights and particles. Nothing beyond that sentence could be verified.
- **Techniques:**
  - URP 2D Renderer + 2D Lights — n/a — from the description.
  - Keeping lights and particles on the pixel grid (pixel-perfect camera is implied by the title, not verified).
- **Parameters / numbers:** not available.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a.
- **Where it could land in Islands:** Tilemap adapter demos only: pixel-perfect camera plus 2D lights for torches in dungeon visualizations. Web labs: render to a low-res target and upscale with nearest neighbour.
- **Value for Islands:** low — visualization tip only; content unverified.
- **Changes vs. sampled card:** none of substance. The caption language is identified as Korean auto-generated; the "Unity 2019.4" detail in the old card could not be verified from the description and was dropped.

### Making 3D pixel art using picoCAD — Brandon James Greer (8:21)
- **URL:** https://youtu.be/dMX0SI5Av-8
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean; chapters: Introduction, Creating the Pixel Art Texture, Modelling the Mesh, Importing and Mapping the Texture, Finishing Touches and Export, Creating a Sphere, Outroduction.
- **Core idea:** picoCAD (low-poly modeller by Johan Peitz, built on the PICO-8 fantasy console, name-your-price) lets you build a wireframe, map a small PNG onto its faces, and renders by re-interpreting the object in pixels at every rotation angle. Two builds: a vending machine (cube) and a Pokéball (sphere from a cylinder).
- **Techniques:**
  - Texture first, in the PICO-8 palette (picoCAD converts anyway, but designing in-palette avoids surprises); a 10×10 grid to plan faces; laid out like papercraft (0:42-1:57).
  - Odd vs even face sizes do not matter: the texture is stretched over the selected UV area and re-sampled for the current zoom and angle (1:12-1:33, 3:27-3:50).
  - Mesh: preset cube, reshape by dragging vertices, eyeball proportions from the 2D art (1:59-2:39).
  - UV mapping: drag the PNG into the window, move each face's UVs onto the art; rotating/mirroring a UV lets one decorative piece serve several faces and saves texture space (3:01-3:58, 7:31-7:49).
  - Shading: the light-bulb shader applies dithering with darker shades of the PICO-8 palette for depth (4:26-4:40). Alpha colour for transparency; export a turntable animation at one of three rotation speeds; projects save as text files (4:15-4:58).
  - Sphere: 8-sided cylinder, extrude the top face twice, then push the points into a ball; keep UV areas the same size around the equator so the stripe width stays constant; hand-shade back faces and add a specular highlight on the texture (5:01-7:20).
- **Parameters / numbers:** max texture 128×128 px; PICO-8 16-colour palette; 8 sides for the cylinder; 3 export speeds.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a.
- **Where it could land in Islands:** web labs (Flora/Fauna sprite export): palette-limited shading by **dithering toward darker palette shades** is a cheap, deterministic way to shade the Grumos/Rayos sprites; constant-UV-width trick for banded objects. Game art for the colonization game: chunky low-poly look. A low-poly araucaria variant (8-sided trunk + textured crown).
- **Value for Islands:** low — art reference; the dithered palette shading is the one transferable idea for the web sprite tools.
- **Changes vs. sampled card:** added the 128×128 limit and PICO-8 palette, per-angle re-pixelation, UV rotation/mirroring to save texture space, the dither shader (4:26), export options, the extrude-twice sphere recipe and constant UV width. Corrected the "about 5:00" reference eyeballing (it is at 2:32, on the vending machine).

### Architectural Designer Creates a Pixel Art Town — Pixel Architect (17:43)
- **URL:** https://youtu.be/KMJbhee4xTk
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean; chapters: Intro, Urban Design, Constraints, Architecture, Market, Center, Fish Market, InGame Walkthrough. Narration ends at 14:45 (music after).
- **Core idea:** Noah, an architecture graduate making the top-down pixel-art game Chef RPG, designs a seaside market town on a hill with urban-design rules: sketch, primary circulation and massing first, then buildings by district, then landscape, then Unity colliders and NPCs. Hand-made, not procedural.
- **Urbanism rules (precise, with minutes):**
  1. Start from a sketch; then fix primary circulation routes and the massing and function of buildings before any detail (1:20-1:50).
  2. One central route, wider than the rest so the player reads it, must pass through the town square (1:50-1:58).
  3. Primary routes connect primary nodes of activity: here the town centre and the entrances to other maps (1:58-2:09).
  4. Secondary routes add permeability and mobility (2:09-2:13).
  5. No dead end on a public road unless it ends at a point of interest (2:14-2:28). He keeps one deliberate dead end on the docks as a spot for future story events (9:43-9:48).
  6. Open buildings to the pedestrian realm: streets lined with shops and activities are popular; streets along a big blank building face, or uncontained plazas that are too big, are not (2:28-2:46).
  7. Public space needs a "permeable membrane": activities on both sides, people passing back and forth. Line all pathways with shops and game activities to maximise liveliness (2:46-3:05).
  8. Top-down "dead zone": anything tall hides the character behind it. Fading buildings is rejected (looks bad, still wastes the space behind). Three fixes: lift the ground so the tall thing becomes a cliff; put lower objects such as trees in between (only partial occlusion); embed buildings into the slope. Hence the town is on a hill rising toward the top of the map, as in Pokémon and Final Fantasy (3:06-4:40).
  9. Typology: 2-3 storey buildings with shops on the ground floor and homes above are ideal for pedestrian streets; in a rural town nothing needs to be taller (5:58-6:14).
  10. Open market stalls instead of one room per shop: fewer rooms, no in-and-out and load times (6:25-6:52).
  11. Every drawn door must be enterable, so draw few doors (8:51-9:03).
  12. Function over spectacle: two fish shops are enough; ten would drag out gameplay (10:01-10:09).
  13. Material follows structure: wooden buildings on the wooden docks, since stilts could not hold stone or metal (10:21-10:34).
  14. Edges: no straight hardscape-to-grass edge; nature taking back the town; wild, varied surrounding landscape gives the built form its contrast (11:40-13:07).
- **Other techniques:** style mix (European + Asian, Ghibli-like, cyberpunk replacing steampunk, corporate tech "encroaching" on tradition, e.g. a flood barrier and a parasitic machine on the fish market) (4:57-5:51, 9:05-9:31, 10:43-10:54); a sidewalk to mediate buildings and square, worn ground texture (7:37-7:57); fall version by hue-shifting colour ranges per layer (13:08-13:20); Unity polygon colliders and random NPCs with basic AI (14:04-14:37).
- **Parameters / numbers:** 2-3 storeys in the market; 2 fish shops; game set about 40 years in the future.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a as an algorithm. Runtime has no settlement, road or building stage (Stages: BaseTerrain, Shore, Hills, Morphology, Hydrology, Biome, Vegetation, Regions, Traversal). `Stage_Traversal2D` writes a `Walkable` mask, the nearest existing hook.
- **Where it could land in Islands:** constraint/scoring terms for a future settlement generator (mundo-islas, colonization game): a widest main route through the square; main routes join activity nodes and map exits; dead ends only at points of interest; frontage (every path cell next to a building with an active ground floor); plaza size bounded by its built edge; height increases away from the camera (place tall things uphill, or cliffs instead of walls); one door = one interior. The colonization theme fits rule 14's "encroachment" (outsider tech latched onto local buildings). The "Cómo nace una aldea" article could cite these. Opinion of one designer, not tested rules.
- **Value for Islands:** medium — the only card with design rules usable as constraints for a settlement layer; design opinion, not a tested algorithm.
- **Changes vs. sampled card:** rules extracted precisely with minutes (14 rules). Corrected minutes: urban rules are at 1:50-3:05 (not 5:00-6:00), the occlusion section at 3:06-4:40 (not about 3:00), the market at 5:54 (not 8:00). Added the three dead-zone fixes and "town on a hill", the main-route and dead-end rules, open stalls, enterable doors, materials on stilts, edges, the fall hue-shift and Unity colliders/NPCs. The old "plaza containment" rule is now stated as he says it (uncontained plazas that are too big are dead).

### Make Low Poly Trees & Save the Planet — Grant Abbitt (16:29)
- **URL:** https://youtu.be/p-9pgZI3inI
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean.
- **Core idea:** Blender 2.8 tutorial for #TeamTrees: **three** low-poly trees, each with a different fast method, then flat materials and ambient occlusion.
- **Techniques:**
  - Tree 1, skin-modifier skeleton (1:00-3:40): merge a mesh to one vertex, extrude a branching skeleton of vertices, add the Skin modifier, scale the skin radius per vertex (Ctrl+A: thick base, thin tips), subdivide edges for extra joints, apply. Foliage: snap the 3D cursor to a branch tip, add a low-subdivision icosphere, duplicate, scale and rotate them, leave some branch visible, check from the sides so the crown is not flat.
  - Tree 2, stacked cones (conifer) (3:45-8:45): 6-sided cylinder trunk with a tapered top; foliage tiers as duplicated 6-sided frustums, each a little smaller and rotated, growing taller near the top; loop cuts with one edge pulled up or down for irregular tiers; knife-tool notches (an inset bottom face makes them possible); n-gons are fine for stylised low poly.
  - Tree 3, whorled branches (pine-like) (9:00-13:15): 6-sided trunk with loop cuts scaled, rotated and moved for a "squiggle", base scaled up; a branch from a plane with two loop cuts shaped like a leaf; origin at the trunk end; **linked duplicates (Alt+D) rotated around Z** to make a ring, then copied up the trunk in rings, each ring scaled down and rotated; editing one instance edits all, while per-object rotation and scale still vary.
  - Shading (13:27-16:05): desaturated, rough (non-shiny) greens; one brown slightly toward red; 2-3 slightly different greens across crown pieces "as if light hits them from different directions"; ambient occlusion on in the render settings.
- **Parameters / numbers:** cylinders at 6 vertices (default 32); icosphere subdivisions chosen by style (low); materials with roughness turned up.
- **Determinism, seeds, chunks/seams:** not discussed (manual modelling).
- **Islands status:** n/a for generation. Related, outside Runtime: the web Flora Lab (araucaria, palma) already generates trees procedurally.
- **Where it could land in Islands:** Flora Lab (web): tree 3's whorl structure (rings of identical branches, shrinking and rotated per ring) is the araucaria's own architecture; tree 2 is a cheap conifer variant; "2-3 near-equal greens per crown as fake lighting" fits the Grumos sprite. Game art for mundo-islas. Not core.
- **Value for Islands:** low — manual art; the whorl and per-blob green-shift ideas are mildly useful for Flora Lab.
- **Changes vs. sampled card:** corrected "a low-poly tree built by hand": the video builds three trees with three methods. Added the skin-modifier skeleton, stacked-cone conifer with loop-cut irregularity and notches, whorled branches with linked duplicates, 6-sided cylinders, and the shading rules (desaturated greens, per-blob green shifts, AO). Minute marks re-anchored.

### 3D AI Model Generation is Getting GOOD - UE5.4 — Sorceress Gamedev (12:25)
- **URL:** https://youtu.be/RWl_FNP6mq8
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean. Contains affiliate links (said at 8:13-8:25).
- **Core idea:** Pipeline: concept art in Leonardo AI's real-time generator (style sliders), image-to-3D in **Rodin by Hyper Human**, FBX into Unreal Engine 5.4 with a hand-made PBR material. Good for stylised creatures with solid shapes, not for realistic high-res humans.
- **Techniques:**
  - Consistent style: tweak Leonardo's sliders (kids illustration, anime, folk art), then **write down the seed and slider values** and reuse them to make variants in one art style (0:46-2:42).
  - Pick concept art with simple, solid geometry: thin hairs and fine parts fail (3:14-3:42).
  - Rodin: preview first; auto-detected flags (sharp edges, symmetric geometry) can be overridden; "hyper" mode for thin surfaces such as antennae; polygon count 10,000 by default; a text hint ("with four mechanical legs") fixed a mis-read leg (3:45-6:00).
  - Texture generated from the image (PBR detail slider; results vary run to run, so hard to compare) (6:00-7:26).
  - Export FBX, PBR shaded; 1K textures free, 4K paid and "noticeably better" (7:30-8:33).
  - UE import: materials did not come through, so he builds one by hand (diffuse, metallic, normal, roughness) (8:34-9:46).
  - Rigging and procedural walk are only mentioned at the end (static mesh to skeletal mesh with UE's built-in rigging); not shown (11:33-12:17).
- **Parameters / numbers:** 10,000 polygons default; 1K free / 4K paid textures; about 1 minute per generation.
- **Determinism, seeds, chunks/seams:** the seed is used only to keep a style consistent in Leonardo; image-to-3D and texture generation are not reproducible (he says each run differs).
- **Islands status:** n/a. Conflicts with Islands invariants (deterministic, no network in the package) and with the rule that LLMs only design and test.
- **Where it could land in Islands:** outside the package only: placeholder creatures for the colonization game bestiary. The "record seed + sliders for a consistent style" habit is a general lesson for any generator.
- **Value for Islands:** low — external AI art pipeline, conflicts with core invariants.
- **Changes vs. sampled card:** named the image-to-3D service (Rodin by Hyper Human) and added the seed-and-sliders consistency trick, hyper mode, the prompt fix, free 1K vs paid 4K, the manual UE material and affiliate links. Corrected "rig it with a procedural walk": the walk is only mentioned, not shown or explained.

### Refactoring - When, How, and Why (Zelda in Unity) — Mister Taft Creates (20:50)
- **URL:** https://youtu.be/ZS7K2_NNZVw
- **Source note:** full transcript read (re-read 2026-11-01). English auto-captions, clean.
- **Core idea:** Before adding an ability system, he explains the five SOLID principles and audits the bloated `PlayerMovement` script, tagging everything that is not movement with `TODO` comments. No code is moved in this video; the health and knockback refactors come in the next episodes, then the Unity 2019 upgrade.
- **Techniques / architecture lessons:**
  - SOLID, with Unity examples (1:30-6:00): single responsibility (Rigidbody2D manages only physics); open/closed (use `[SerializeField] private` instead of public fields; getters instead of setters); Liskov (enemy subclasses usable wherever `Enemy` is); interface segregation (inventory, pause menu and start menu kept apart); dependency inversion (depend on abstractions, hard-code as little as possible).
  - Motivation (6:15-9:00): health, knockback, inventory, magic, i-frames and arrows all live in `PlayerMovement`, so a shared health system for player and enemies is impossible. Goal: build things like a fireball by adding components (movement, hit, optional knockback, damage per target type so it hurts enemies but not pots).
  - Method (9:00-18:20): go field by field and method by method asking "is this movement?"; mark each non-movement item with a categorised tag (`TODO health`, `TODO inventory`, `TODO magic`, `TODO i-frame`, `TODO ability`, `TODO knockback`) and find them later with Ctrl+F; plan the refactor before doing it.
- **Parameters / numbers:** none.
- **Determinism, seeds, chunks/seams:** not applicable.
- **Islands status:** architecture lesson only. Islands already separates grids, operators, stages (`IMapStage2D`) and adapters; nothing to add.
- **Where it could land in Islands:** nowhere new. A reminder to keep stages single-purpose and to plan refactors with categorised TODO tags before moving code.
- **Value for Islands:** low — generic architecture advice already followed.
- **Changes vs. sampled card:** added the full SOLID walkthrough (the old card had only single responsibility), the categorised-TODO method and the fact that no code is moved in this episode. Corrected "player inventory moved to its own component" and "refactor in small steps (movement now, health next)": in this video he only tags; health and knockback are refactored in later videos.

---

## Part 3 cards (math and emergence)

Full transcripts read for 8 of 13: FFftmWSzgmk, LqbZpur38nw (ES auto-translated), gB9n2gHsHN4 (ES human), ovJcsL7vyrk (EN auto, switched from the default Arabic track), 094y1Z2wpJg, Uj3_KqkI9Zo (ES human), A2IAyXc0LuE, 8-B6ryuBkCM (EN auto); IK7nBOLYzdE has a caption track but it is only "[Musique]" (music only). No transcript: ecSl5TzJZpo, IK7nBOLYzdE, P2uhhAXd7PI, C2vgICfQawE, Udrt2otKmyA (cards from the full descriptions, read on 2026-11-01).

Status baseline (re-checked by Grep over `D:\Projects\Islands\Runtime`): `Runtime/PCG/Operators/MaskMorphologyOps2D.cs` (only `Erode4Once`, `Erode4` and `BfsDistanceField`; no dilate, no majority filter) and `Stage_Morphology2D.cs` exist; no birth/survive CA step, no escape-time / complex iteration, no box-counting metric and no ODE integrator in `Runtime/PCG` or `Runtime/Noise`. Web labs already have `scenes/mandelbrot.astro` and `scenes/julia.astro`.

### What's so special about the Mandelbrot Set? — Numberphile, Ben Sparks (16:53)
- **URL:** https://youtu.be/FFftmWSzgmk
- **Source note:** full transcript read (re-read 2026-11-01); English human captions, good.
- **Core idea:** Iteration seen as stable vs unstable behaviour. Squaring on the real line splits at |x|=1 (and at -1); in the complex plane plain squaring is stable inside the unit circle. Adding a constant, z->z^2+c, gives Julia sets (c fixed, start point varies) and, always starting at 0 and varying c, the Mandelbrot set. Colour by how fast a point escapes.
- **Techniques:**
  - Real-line stability demo (0:15–2:00): squaring sends |x|>1 to infinity, |x|<1 to 0; "sharp line" at 1 and -1. A 1D warm-up that an explainer can show before going 2D.
  - Complex squaring as a "chasing" dot with a circular flavour (2:36–3:50): the unit circle is the stability boundary of z->z^2.
  - Julia sets (4:05–7:55): fix c, vary z0; the boundary of stability is no longer a circle; sets can be connected or "dust" (disconnected). Gaston Julia did it by hand, early 20th century.
  - Mandelbrot set as the parameter plane (8:00–12:40): start always at z0=0, colour each c by whether its orbit stays bounded. Orbits inside the main body settle on 1 point, in the left bulb on 2 "piles", in the top bulb on 3 (12:20–12:35).
  - Escape-time colouring — NEW (not in D.1–D.20) — (13:15–14:50): once |z|>2 it never returns (proved); iterate a cap (~200 checks), colour black if still within radius 2, otherwise colour by the iteration at which it crossed (50 vs 60 iterations = different shades). Colour changes very fast near the boundary = sensitivity to initial conditions, "the hallmark of chaos" (14:53–15:10).
  - Mandelbrot as a "map of Julia sets" (15:15–16:15): the Julia set for c in seahorse valley looks like the Mandelbrot neighbourhood at c. "A geography of iterative stability" (16:12). Programmable "in ten seconds on a spreadsheet" (16:33).
- **Parameters / numbers:** escape radius 2; cap ~200 iterations (14:20); Mandelbrot 1979-80 at IBM, printer output; technicians "cleaned" the dust they took for printer artefacts (10:25–10:53, story from James Gleick's *Chaos*). Demo built in GeoGebra (6:53, 11:13).
- **Determinism, seeds, chunks/seams:** deterministic, per-pixel independent (chunk-safe). Not discussed in the video.
- **Islands status:** missing (no escape-time code in `Runtime`). Web: exists (`mandelbrot.astro`, `julia.astro`).
- **Where it could land:** web only (already published). For Islands: an iteration-count field in `Runtime/Noise` would be Burst-friendly and chunk-safe, but it is a visual gimmick, not terrain. At most a NoiseLab optional "escape-time field" as a mask source (threshold = Mandelbrot-shaped island). Low priority.
- **Value for web:** medium, as narrative for the existing Mandelbrot/Julia pages. What is missing there: (1) a 1D strip showing x->x^2 with arrows toward 0 or infinity and the sharp switch at ±1; (2) the "printer dust" anecdote with a toggle that renders the set as black/white dots only, like the 1980 print; (3) bulb labels (1, 2, 3 "piles") shown by drawing the orbit of the c under the cursor.
- **Value for Islands:** low — escape-time fields are artificial shapes, not terrain; the web already covers it.
- **Changes vs. sampled card:** added the 1D real-line warm-up, the bulb-to-period link (1/2/3 piles), the "50 vs 60 iterations" colouring logic, GeoGebra and Gleick sources, and concrete web additions. Value unchanged.

### Beyond the Mandelbrot set, an intro to holomorphic dynamics — 3Blue1Brown (27:36)
- **URL:** https://youtu.be/LqbZpur38nw
- **Source note:** full transcript read (re-read 2026-11-01); Spanish auto-translated captions (readable; a few garbled lines, e.g. a stray SRT index at 5:31).
- **Core idea:** Holomorphic dynamics = iterating complex-differentiable functions (polynomials, rational maps). Fixed points and cycles, attracting vs repelling by |f'|, Fatou set (predictable) vs Julia set (chaotic boundary). Parameter space vs seed space. The Mandelbrot shape reappears in a different parameter space (Newton's method on cubics), so it is universal, not specific to z^2+c.
- **Techniques:**
  - Newton fractal recap (2:05–2:57): iterate x - p(x)/p'(x) from every pixel, colour by the root reached; rough fractal borders between colours.
  - Rational maps (3:02–3:44): Newton's map for z^3-1 is a polynomial over a polynomial; Fatou and Julia studied iterating any rational map right after WWI, without computers.
  - Escape-time plot of z^2+c (4:09–5:21): start z0=0; if |z| reaches 2 it diverges; black if bounded, gradient by divergence speed. Main cardioid = converges to a fixed point; big left disc = 2-cycle; top/bottom discs = 3-cycles (5:52–6:11).
  - Parameter space vs seed space (6:26–7:35): Mandelbrot sweeps c with fixed z0; "Julia" pictures fix c and sweep z0; all are 2D slices of a 4D (c, z0) space. Terminology: the Julia set is properly the boundary, not the filled black interior (7:37–8:03).
  - Fixed points f(z)=z (8:39–9:40): always exist for rational maps (fundamental theorem of algebra); Newton's map has them at the roots by design.
  - Stability via the derivative (9:52–12:15): near a point, f acts like multiplication by f'(z) (scale + rotate; f'=2i at z=i means a 90° rotation plus x2). |f'|<1 attracting, >1 repelling; Newton roots have f'=0 = superattracting. Exercise: the attracting-fixed-point condition for z^2+c traces exactly the main cardioid (12:18–12:40).
  - Cycles (12:45–14:43): solve f^n(z)=z; for z^2+c the degree doubles each composition (4, 8, 16...), so about 2^n period-n points; nearly all lie on the boundary, probability zero to land on them in practice (14:54–15:04).
  - Attracting cycle in Newton (15:31–16:19): z^3-2z+2 has an attracting 0<->1 cycle; check with |(f∘f)'(0)|<1. Seeds near it never find a root (a practical failure of Newton's method).
  - Critical-point trick (17:18–19:15): Fatou's theorem: any attracting cycle captures at least one critical point (f'=0). For Newton on cubics the critical point is the mean of the three roots, so testing that single seed tells whether the polynomial has an attracting cycle. Same reason z0=0 suffices for the Mandelbrot set (18:26–18:35).
  - Mandelbrot in Newton's parameter space (19:19–21:06): fix roots at -1 and +1, move the third root λ; colour pixel λ black if the centroid seed falls into a cycle; zooming finds a Mandelbrot-shaped black region. Black is rare, so Newton usually works.
  - Rough boundaries (21:11–22:16): with 3+ limiting behaviours, any small circle on a boundary contains all colours, so the boundary can never be smooth.
  - Fatou vs Julia sets (22:25–24:59): Fatou = eventually stable, small discs shrink; Julia = small discs expand and, by a corollary of Montel's theorem, eventually cover the whole plane except at most two points ("everything goes everywhere", his nickname). Demo: a few thousand points within 1e-6 of a Julia point spray everywhere (24:25–25:00). Some maps have Julia set = whole plane (26:04–26:16).
  - Takeaway (26:18–27:05): chaos and fractal are logically linked, not just analogous: quantifying chaos at points explains the rough boundary.
- **Parameters / numbers:** escape radius 2 (5:01); z^3-2z+2 cycle 0<->1 (15:35); roots fixed at ±1 with free λ (19:22); cluster within one millionth (24:31). Recommends the interactive article at acko.net (5:34) and credits Ben Sparks's Numberphile illustration (5:24).
- **Determinism, seeds, chunks/seams:** deterministic, per-pixel independent; not discussed.
- **Islands status:** missing; no complex-iteration fields in `Runtime/Noise`.
- **Where it could land:** web: a page "Newton fractal and its parameter space" (or an extension of the Julia page) for the fractal series. Islands: none directly. Transferable lens for the coastline/fjord question: Julia boundaries are rough because of a theorem about chaos, not because of any physical process, so they make geologically artificial coasts; this supports ridged multifractal + erosion over fractal-set shapes.
- **Value for web:** medium. A concrete page: left panel = Newton fractal for a cubic with three draggable roots (pixel colour = root reached, black = never converged); right panel = parameter plane of λ with roots fixed at ±1, each pixel coloured by the fate of the centroid seed; a zoom button that jumps to the hidden Mandelbrot copy. Second widget, "everything goes everywhere": click a boundary point, release 2,000 particles within 1e-6, watch them spread over all basins.
- **Value for Islands:** low — no terrain technique; only an argument for physically based coast roughness.
- **Changes vs. sampled card:** added the derivative-as-scale-and-rotation reading, cardioid = attracting fixed points, the probability-zero remark on cycles, the rough-boundary argument (3+ colours), Julia set = whole plane note and the acko.net reference; web page made concrete. Value unchanged.

### Los fractales no suelen ser autosemejantes — 3Blue1Brown (19:54)
- **URL:** https://youtu.be/gB9n2gHsHN4
- **Source note:** full transcript read (re-read 2026-11-01); human Spanish translation (Teo López Puccio and Grant Sanderson), good. Real length is 19:54, not 21:36 as in the sampled card.
- **Core idea:** Fractal dimension = the exponent with which "mass" scales when a shape is rescaled. Self-similar shapes give it by a logarithm; any shape (coastlines included) gives it by box counting and a log-log fit. A fractal is a shape whose dimension is not an integer and stays roughly constant across many scales. Self-similar fractals are toy models, not the typical case (Mandelbrot's point was to model natural roughness, a "rebellion against calculus", 1:12).
- **Techniques:**
  - Self-similarity dimension (3:08–9:20): halve a line → 2 copies, a square → 4, a cube → 8, so mass scales as (1/2)^D with D=1,2,3. Sierpinski: 3 copies at 1/2 → D=log2(3)≈1.585 (infinite length, zero area). Koch: 4 copies at 1/3 → log3(4)≈1.262. Right-angle Koch: 8 copies at 1/4 → log4(8)=1.5 exactly.
  - Box-counting dimension — NEW — (10:12–14:52), the key procedure:
    1. Lay a square grid over the shape and count the cells the shape touches (10:29).
    2. Rescale the shape by a factor s (equivalently, use a finer grid) and count again (10:46). For a disc the count grows ~s^2, for Sierpinski ×3 per doubling, i.e. 2^1.585 (12:02–12:50).
    3. Model: N(s) = k·s^D (13:42–13:55). Take logs: log N = log k + D·log s, a straight line (14:08–14:30).
    4. Try many scale factors, plot (log s, log N), run a linear regression; the slope is the measured dimension (14:33–14:52).
    - Caveat 1: convergence is slow; on a coarse grid the disc's ratio is far from 4 (11:04–11:33).
    - Caveat 2: the local slope can change with zoom (15:38–16:49): a 3D coiled tube looks D=1 from far, D=2 at mid zoom (a tube surface), D=1 again when the coils are resolved. In 3D you count cubes instead of squares (15:58).
    - Caveat 3: pure maths takes the limit of the slope at infinite zoom (17:00–17:21); in applied work you measure over a wide but finite range of scales and call the shape fractal only if the slope stays roughly constant across them (17:29–18:00).
  - Definition (15:10–15:35): fractals are shapes with non-integer dimension; the stricter definition is in the video description (not shown in the transcript).
- **Parameters / numbers (verified against the transcript):** Britain's coastline ≈ **1.21** (2:09, 13:22) and still ≈1.21 after a ×1000 zoom (18:00–18:07). Norway ≈ **1.52**, "much rougher than Britain" (19:03–19:11). Calm ocean surface slightly above 2, stormy ≈ 2.3 (19:12–19:21). Closing claim: fractal dimension separates natural objects from man-made ones (19:21–19:27). The video gives no source for the coastline numbers; for reference (general knowledge, not from the video), Richardson's data as used by Mandelbrot (1967) gave ≈1.25 for Britain's west coast, so ~1.2–1.25 is the usual range and 1.21 is a commonly quoted whole-coast figure.
- **Determinism, seeds, chunks/seams:** the measurement is deterministic for a fixed grid origin and fixed scale list; grid alignment sensitivity is not discussed (in practice average over a few grid offsets or fix the origin at (0,0) for tests).
- **Islands status:** missing: no box-counting or dimension metric in `Runtime` (Grep for box-count / dimension / coastline found only vertex-attribute "dimension" in `Runtime/Meshes/Streams/`). The inputs exist: `Runtime/PCG/Grids/MaskGrid2D.cs`, `Stage_Shore2D.cs` and `Stage_BaseTerrain2D.cs` produce land/coast masks, and `Runtime/Noise/Noise/Noise.cs` has `FractalMode` Standard (fBm) and Ridged (Musgrave) with octave/lacunarity/gain settings to tune.
- **Where it could land:** (a) A **coast roughness metric**: extract the coastline cells of the Land mask (land cells with a water 4-neighbour), count occupied blocks at block sizes 1, 2, 4, 8, 16, 32 cells (equivalent to rescaling), fit log N vs log(1/size) by least squares, report the slope D and the fit residual. Use as an inspection metric in the map visualizer and as an EditMode test asserting a range for fixed seeds (e.g. a "Britain-like" preset in 1.15–1.30, a "fjord" preset nearer 1.5), which turns the open "fractal coastline / fjord refinement" item into a measurable target. Honest limit: a 256² map spans only ~5 octaves of scale, so the fit is short and noisy; report D only over block sizes 2–32 and check that the slope is roughly constant (the video's caveat 3), otherwise flag "not fractal at this resolution". (b) NoiseLab: D read-out of the thresholded mask next to the existing histogram. (c) Web Islands Lab page, below.
- **Value for web:** high. A concrete explainer page "How rough is your coastline?": (1) a noise island with sliders for octaves, gain and lacunarity, coastline drawn on top; (2) a grid overlay whose cell size is stepped (32 → 16 → 8 → 4 → 2 px) with touched cells lit and counted live; (3) a log-log plot that adds a point per step and draws the regression line, slope shown as D; (4) reference marks on the plot axis at 1.0 (smooth circle), 1.21 (Britain), 1.26 (Koch), 1.52 (Norway); (5) a "Koch snowflake" preset to check the tool returns ≈1.26; (6) the 3D-tube caveat as a short note on why slope can change with zoom.
- **Value for Islands:** high — cheap, deterministic, testable metric that directly informs the coastline backlog item and the web.
- **Changes vs. sampled card:** corrected the duration (19:54) and minute marks (Britain 2:09/13:22/18:05; Norway 19:03 confirmed); spelled out the box-counting procedure step by step with its three caveats (slow convergence, slope changes with zoom, finite range of scales in applied work); added the "natural vs man-made" claim, the no-source note on coast figures, the resolution limit for Islands maps and the concrete web page. Value unchanged.

### This equation will change how you see the world (the logistic map) — Veritasium (18:38)
- **URL:** https://youtu.be/ovJcsL7vyrk
- **Source note:** full transcript read (re-read 2026-11-01); the panel defaulted to Arabic, switched to English auto-generated (good, minor errors: "Lib Taber" = Libchaber, "bifurcation z'"). Read both; numbers agree.
- **Core idea:** x_{n+1} = r·x_n·(1-x_n), x as a fraction of the maximum population: the simplest equation with a negative feedback loop (an inverted parabola, 1:21–1:36). The long-run value depends on r, not on x0: extinction, a fixed point, period doubling, chaos, windows of order inside chaos. The bifurcation diagram is fractal, is the real-axis side view of the Mandelbrot set, and its doubling rate gives the universal Feigenbaum constant 4.669.
- **Techniques:**
  - Logistic map iteration — NEW — worked example (1:38–2:52): r=2.6, x0=0.4 → 0.624 → 0.61, 0.619, 0.613, 0.617, 0.615, 0.616, 0.615 = equilibrium 0.615. Changing x0 only changes the first few years (2:52–3:11), so you can discard a transient.
  - Bifurcation diagram (3:35–6:18): x-axis r, y-axis the values visited after many generations. r<1 extinction; 1<r<3 one stable value rising with r; r>3 splits into a 2-cycle, then 4, 8, 16, 32, 64, doublings arriving faster and faster; r≈3.57 chaos; windows of order, e.g. r≈3.83 a stable 3-cycle that doubles to 6, 12, 24 before chaos returns. Periods of every length exist (6:10–6:16).
  - Pseudo-random numbers (5:19–5:43): the map was one of the first ways to get random numbers on computers; deterministic, so only pseudo-random.
  - Fractal self-similarity of the diagram on zoom (6:18–6:37).
  - Mandelbrot link (6:37–10:17): iterate z^2+c thousands of times and plot the visited values on a third (z) axis; seen from the side along the real axis, it is the bifurcation diagram. Main cardioid = one value, main bulb = 2, next = 4, 8, 16...; chaos lies on the "needle"; the small "medallion" (mini-Mandelbrot) = period-3 window; other bulbs off the cardioid show 3-, 4-, 5-cycles as "ghostly images" on the z axis.
  - Real systems with the period-doubling route (10:30–14:19): Libchaber's mercury convection cell (two counter-rotating rolls; temperature spikes go period 2, 4, 8 as the gradient rises); human and salamander eyes under flickering light respond to every other flash; rabbit hearts on the way to fibrillation (cycles 2, 4, then aperiodic), with chaos-timed shocks restoring a regular beat; the dripping tap goes period 2 then chaotic as flow rises ("try it at home").
  - Feigenbaum constant (14:19–16:04): ratio of successive bifurcation widths → 4.669; the same for any single-hump map, e.g. x_{n+1}=sin x; "universality".
- **Parameters / numbers:** thresholds r=1, 3, ≈3.57, ≈3.83; δ≈4.669; Robert May, *Nature* 1976, with his plea to teach this equation (16:04–16:31). Gleick's *Chaos* cited again (17:07). Description links the code for the interactives (not opened).
- **Determinism, seeds, chunks/seams:** deterministic but, in the chaotic regime, extremely sensitive to x0. Not discussed in the video, but it matters for Islands: float differences between Burst and managed code or across platforms (FMA, rounding) diverge after a few dozen chaotic iterations, which breaks same-seed → same-output.
- **Islands status:** missing, and not needed: seeded randomness is SmallXXHash in `Runtime/Noise` (integer, platform-exact).
- **Where it could land:** (a) not as an Islands RNG or parameter source (worse than SmallXXHash and float-fragile). (b) NoiseLab: not recommended. (c) Web emergence/fractal series, below.
- **Value for web:** high. A concrete page "Order, chaos, order": (1) a cobweb plot (parabola + diagonal) with sliders for r and x0, showing x0 being forgotten (the 0.615 example as the default); (2) the bifurcation diagram drawn progressively as r sweeps 1→4, with markers at 3, 3.57, 3.83 and a zoom box to show self-similarity; (3) a button "see it from the side" that links to the published Mandelbrot scene and highlights the real-axis needle, cardioid and period-3 medallion; (4) a toggle to swap the parabola for sin, showing the same cascade (universality) and the measured ratio converging to 4.669; (5) one sidebar of the real-world cases (dripping tap, Libchaber, hearts). This fits Claudio's emergence interest (order out of chaos).
- **Value for Islands:** low — no terrain use; hashing is better and deterministic.
- **Changes vs. sampled card:** source is now the English track (was Arabic only); added the full worked sequence, periods-of-every-length claim, the "ghostly images" 3/4/5 bulbs, the description's code link and a concrete web page. Value unchanged.

### The Simplest Math Problem No One Can Solve – Collatz — Veritasium (22:08)
- **URL:** https://youtu.be/094y1Z2wpJg
- **Source note:** full transcript read (re-read 2026-11-01); English human captions, good.
- **Core idea:** Rule: odd → 3n+1, even → n/2. Conjecture: every positive integer ends in the 4-2-1 loop. Trajectories ("hailstone numbers") look like geometric Brownian motion with a downward drift; checked to 2^68, "almost all" results proven (Tao 2019), no proof; possibly undecidable.
- **Techniques:**
  - Collatz map — NEW — worked example from 7 (0:22–1:33): 7, 22, 11, 34, 17, 52, 26, 13, 40, 20, 10, 5, 16, 8, 4, 2, 1. Total stopping time: 26 takes 10 steps and peaks at 40; 27 climbs to 9,232 and takes 111 steps (2:30–3:16). Neighbouring seeds behave very differently.
  - Log trajectory = geometric Brownian motion (4:23–5:10): take the log, remove the linear trend, the residual looks like coin flips; downward trend (vs the stock market's upward one). Work of Kontorovich and Sinai.
  - Benford's law of leading digits (5:10–6:57): over the first billion sequences ~30% start with 1, ~17.5% with 2, ~12% with 3, under 5% with 9. Works when numbers span several orders of magnitude; used for fraud and election checks (with care).
  - Heuristic drift (7:07–9:02): 3n+1 is always even, so an odd number grows at most ×3/2 per step; halving chains of length k occur with probability 1/2^k; geometric mean from one odd number to the next = ×3/4 < 1, so sequences shrink on average. 341 → 1024 → ten halvings to 1.
  - Directed graph (9:02–9:34): each number points to its successor; it "looks like a tree or a series of little streams that flow into each other", all draining into the "river" 4-2-1.
  - "Coral" rendering (9:34–9:58): walk each path, rotating anticlockwise at odd numbers and clockwise at even ones; tuning the two angles gives organic coral or seaweed shapes.
  - Ways it could fail (9:58–10:30): a divergent seed or a separate loop.
  - Evidence and partial results: brute force to 2^68 ≈ 2.95×10^20 (10:30–10:58); any other loop must have at least 186 billion numbers (11:01–11:11); Terras 1976 "almost all" sequences dip below their seed; bounds x^0.869 (1979) and x^0.7925 (1994); Tao 2019: almost all go below any f(x)→∞, e.g. log log log x (11:53–13:22). Seeds up to 10,000 plotted by peak; 9,663 reaches ~27 million (14:01–14:26).
  - Negative integers (14:44–15:03): three independent loops, starting at low values such as -17 and -5.
  - Why "almost all" is not "all" (15:27–16:09): perfect squares are "almost none" of the integers yet infinite. Pólya conjecture, disproved by Haselgrove in 1958 with a counterexample ~1.845×10^361 (16:23–16:54).
  - Turing-machine view (16:54–17:52): a seed is an input tape; you can construct a number that rises ×3/2 any finite number of times in a row, but not control what follows. Conway's FRACTRAN (1987) generalisation is Turing-complete, so undecidability is possible (18:55–19:31).
- **Parameters / numbers:** as listed. Interviewees: Jeffrey Lagarias (the world authority; warned students off it), Alex Kontorovich (spent 20 years "on team 3x+1", now doubts it). Erdős: "Mathematics is not yet ripe enough for such questions."
- **Determinism, seeds, chunks/seams:** pure integer arithmetic, bit-exact on any platform (a plus over the logistic map). Practical gotcha not in the video: values overflow 64-bit integers for large seeds (peaks grow far above the seed), so use `ulong` with an overflow check or big integers.
- **Islands status:** missing, and not needed as a generator.
- **Where it could land:** (a) The "coral" (turtle graphics with two turn angles along merging paths) is a small L-system-like branching generator seeded by integers, usable as a curiosity in Flora Lab (`/labs/flora/`). Honest limits: all paths merge into one tree; variety comes only from the two angles and the seed range; it is art, not botany. (b) The "streams flowing into a river" image is a nice analogy for drainage networks (many sources merging into one outlet), but it is not a hydrology technique; Islands rivers should come from downhill flow, not from this. (c) Web emergence collection.
- **Value for web:** medium. A concrete page: (1) type a seed, watch the hailstone path as a height chart (26 vs 27 side by side), with step count and peak; (2) log-scale toggle with the trend line removed to show the coin-flip wiggle; (3) live Benford histogram filling as more seeds run; (4) the coral: seeds 1..N drawn as turtle paths with two sliders for the odd/even angles and a palette from the site library. Ties to Flora Lab only as an aside.
- **Value for Islands:** low — no terrain technique; integer-exact but no use beyond curiosity.
- **Changes vs. sampled card:** added the full 7-sequence, 26 vs 27 stopping times, the Kontorovich/Sinai attribution, the 1979/1994 bounds, the squares and Pólya arguments, the Turing-machine view and the "streams into a river" graph image (with a caution); corrected the negative-number loops (the video names starting values -17 and -5 for three loops, not "around -5, -17"). Value unchanged.

### La paradoja del Hotel Infinito — TED-Ed, Jeff Dekofsky (5:59)
- **URL:** https://youtu.be/Uj3_KqkI9Zo
- **Source note:** full transcript read (re-read 2026-11-01); human Spanish translation (Jenny Lam-Chowdhury, reviewed by Sebastián Betti), good.
- **Core idea:** Hilbert's thought experiment (1920s): a full hotel with countably many rooms can still take one guest, 40 guests, an infinite bus, and an infinite line of infinite buses, by remapping room numbers. It works only for countable infinity (Cantor's aleph-null), not for the reals.
- **Techniques:**
  - Shift n → n+1 for one guest (0:31–0:59); n → n+40 for a bus of 40 (1:03–1:16).
  - n → 2n frees all odd rooms for an infinite bus (1:16–2:02).
  - Prime-power assignment for infinitely many infinite buses (2:20–4:15): current guests go to 2^room (room 7 → 128), bus k seat s goes to p_k^s with the next primes 3, 5, 7, 11, 13, 17... (seat 7 of bus 1 → 3^7 = 2187). Unique by unique prime factorisation (Euclid: infinitely many primes, ~300 BC). Rooms that are not prime powers stay empty, e.g. room 6.
  - Countable vs uncountable (4:19–5:14): naturals = aleph-null; a "hotel of the reals" (rooms 1/2, √2, π) could not be enumerated.
- **Parameters / numbers:** 2^7 = 128, 3^7 = 2187, room 6 empty.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a as a technique. The idea it illustrates, an injective map from pairs to single integers, is what a hash needs when combining coordinates. Checked: `Runtime/Noise/SmallXXHash.cs` combines coordinates by chaining `Eat()` calls (x, then y, then z, as in `Runtime/Noise/Noise/Noise.Lattice.cs` l.105–151), not by adding them, so there is no "x+y collision" problem there. Chunk keys elsewhere were not audited.
- **Where it could land:** nothing for Islands. At most a side note on the web hash page (Islands Lab part 1): "why you can't just add x and y" (collisions along diagonals) vs pairing/chaining, with Hilbert's prime-power trick as the playful version (wasteful: most integers unused, values explode, so never used in practice; Cantor pairing or hash chaining instead).
- **Value for Islands:** low — a pedagogy curiosity; the code already combines coordinates safely.
- **Changes vs. sampled card:** added the 40-guest step, Cantor/aleph-null and the reals hotel, and checked the code: SmallXXHash chains coordinates with `Eat()` (the sampled card said "not checked"). Value unchanged.

### Conway's game of life goes wrong. — Gamer Steve (0:31)
- **URL:** https://youtu.be/ecSl5TzJZpo
- **Source note:** no transcript: from title/description only (re-checked 2026-11-01). Unlisted 31-second clip, no speech; the whole description is "I don't think it's supposed to do that". Screenshots of the clip could not be captured (renderer froze), so what goes wrong is not verified.
- **Core idea (inferred):** a Game of Life run that behaves unexpectedly; most likely a bug or an unusual initial pattern. Cannot be stated from the page.
- **Techniques:** Conway B3/S23 — D.12 (CA family). The usual lesson for "Life goes wrong" (general knowledge, not from the video): double-buffer, i.e. compute generation n+1 from a frozen copy of n, never in place; and decide the border rule (dead border vs wrap-around) explicitly.
- **Islands status:** no CA step in `Runtime/PCG` (`MaskMorphologyOps2D.cs` has only `Erode4Once`, `Erode4`, `BfsDistanceField`).
- **Where it could land:** nothing beyond the CA operator note in the Elliot Waite cards.
- **Value for web:** none on its own; a one-line "bugs look like this" aside on an emergence page at most.
- **Value for Islands:** low — no verifiable content.
- **Changes vs. sampled card:** confirmed no speech and the one-line description; corrected the duration (0:31); noted that the video itself could not be inspected. Value unchanged.

### Cellular Automata: Rule 30 + Conway's Game of Life — Elliot Waite (3:26)
- **URL:** https://youtu.be/IK7nBOLYzdE
- **Source note:** no transcript: music only (the only caption track is French auto-generated "[Musique]"); card from the author's full description, read 2026-11-01. The rule table below restates it and was checked.
- **Core idea:** A 1D automaton (Rule 30) grows at the bottom of the screen; its rows are shifted up until they become the bottom row of a 2D Game of Life in the top half, so 1D chaos keeps injecting patterns into 2D Life.
- **Techniques:**
  - Elementary CA, Rule 30 — NEW (D.12 covers only 2D cave CA). Start: one alive cell in the middle of the bottom row. Each new cell reads the three cells of the previous row (up-left, up, up-right). With alive = 1: 111→0, 110→0, 101→0, 100→1, 011→1, 010→1, 001→1, 000→0. In words (author): alive if exactly one of the three is alive, or if only the right two are alive. Wolfram's numbering: outputs read from 111 down to 000 = 00011110 = 30. The rows are shifted up each step (a scrolling space-time diagram).
  - Coupling (description): the Rule 30 rows, once they reach the top half, are copied into the bottom row of the Life grid, which runs B3/S23 on the Moore neighbourhood (3 neighbours → alive; 2 neighbours and alive → stays alive; otherwise dead).
  - Rendering: white = alive; dead cells fade through several colours by time since death, then to black (an "age since death" buffer).
  - Code: GitHub repo by the author (link truncated on the page: `github.com/elliotwaite/rule-3…`); links MathWorld for all 256 elementary rules.
- **Parameters / numbers:** 3-cell neighbourhood, 8-entry table = 8-bit rule number; Life uses 8 neighbours.
- **Determinism, seeds, chunks/seams:** fully deterministic from the start row. For tiling, an elementary-CA row depends on a cone of previous rows (one cell wider per step on each side), so a chunk needs a halo of N cells for N steps; same for Life.
- **Islands status:** missing (no CA step in `Runtime/PCG`; closest is `MaskMorphologyOps2D.Erode4`).
- **Where it could land:** (a) a generic Life-like CA mask operator in `Runtime/PCG/Operators` (birth/survive masks on the Moore neighbourhood, double-buffered, fixed iteration count, row-major order: bit-exact and Burst-friendly) for cave smoothing (D.12), coast cleanup and forest patches; the elementary 1D part has little terrain use (stripe/edge noise at most), and Rule 30 as an RNG is inferior to SmallXXHash. (b) NoiseLab "CA lab" sample: rule byte slider 0–255 and B/S masks. (c) web, below.
- **Value for web:** high. The clearest "simple rule → complexity" demo for the pending emergence scene. A concrete page: (1) a rule editor with the 8 neighbourhood pictograms as toggles and the rule number updating live (and a number box that updates the toggles), drawing the space-time triangle from one cell; (2) presets 30, 90 (Sierpinski), 110, 184; (3) the coupling demo: the 1D row feeding a Life grid above, with the age-colour fade using the site palettes; (4) a "random start row" toggle to show that Rule 30 stays chaotic and Rule 110 forms gliders.
- **Value for Islands:** medium — the generic B/S CA operator is a useful missing piece; the elementary-CA part is for the web.
- **Changes vs. sampled card:** confirmed the table against the full description (same as before); added the author's verbal rule, the Wolfram numbering order, the row-shift mechanics, the code and MathWorld links, the light-cone halo note and a concrete web page. Value unchanged.

### Cellular Automata: Rule 110 + Conway's Game of Life — Elliot Waite (4:01)
- **URL:** https://youtu.be/P2uhhAXd7PI
- **Source note:** no transcript: music only (no caption track); card from the author's full description, read 2026-11-01.
- **Core idea:** Same set-up as the Rule 30 video, with Rule 110 as the 1D source feeding Life.
- **Techniques:**
  - Rule 110 — NEW — alive = 1: 111→0, 110→1, 101→1, 100→0, 011→1, 010→1, 001→1, 000→0 = 01101110 = 110 (checked against the description). Author's reading: a cell is alive if the cell above or the cell up-right is alive, unless all three are alive. Hence the pattern grows as a triangle down and to the left (the cone spreads only leftwards; the page frame shows a right-angled triangle), with "bubbles" wherever all three cells above were alive.
  - Same coupling into Life B3/S23 and the same age-fade rendering as the Rule 30 video.
- **Parameters / numbers:** single alive starting cell in the middle of the bottom row. Rule 110 is known to be Turing-complete (general fact, not in the description).
- **Determinism, seeds, chunks/seams:** as for Rule 30; the growth is one-sided for this rule, but a generic implementation should still use a symmetric halo.
- **Islands status:** missing.
- **Where it could land:** same function as Rule 30: one elementary-CA step parameterised by the rule byte, `next = (rule >> (4*l + 2*c + r)) & 1`, serves all 256 rules; web and NoiseLab "CA lab" only.
- **Value for web:** medium, as a preset of the Rule 30 page (gliders and "bubbles"; Turing-complete aside).
- **Value for Islands:** low-medium — merge with the Rule 30 card; one function, no terrain use of its own.
- **Changes vs. sampled card:** added the author's verbal rule and why the triangle grows only to the left, the "bubbles" explanation and the halo remark; table confirmed. Value unchanged.

### epic conway's game of life — Rational Animations (6:33)
- **URL:** https://youtu.be/C2vgICfQawE
- **Source note:** no transcript: from title/description only (re-checked 2026-11-01). No caption track. The opening frame shows on-screen text ("The Conway's Game of Life is a famous cellular automata"), so the video explains with text cards, but later frames could not be captured (renderer froze). Description: an old video of the channel; the patterns were run in **Golly** (golly.sourceforge.net), "already containing the patterns you see".
- **Core idea (from title and description):** a tour of Game of Life patterns run in Golly; the specific patterns are not verified.
- **Techniques:** Game of Life B3/S23 — D.12 — missing as a stage. Golly is the reference open-source Life simulator (HashLife for huge patterns; general knowledge), and its bundled pattern library is a ready source of presets.
- **Where it could land:** web emergence scene: Golly's pattern collection (RLE files) as preset sources for a Life page (glider, glider gun, oscillators). Not Islands.
- **Value for web:** low-medium — a useful pointer to Golly as the pattern source, not content in itself.
- **Value for Islands:** low — a Life pattern tour, no terrain content.
- **Changes vs. sampled card:** identified the linked program as Golly; noted the on-screen text cards. Value unchanged.

### Lattice of particles in a double-well potential, subjected to noise — Nils Berglund (0:40)
- **URL:** https://youtu.be/Udrt2otKmyA
- **Source note:** no transcript: from title/description only (re-checked 2026-11-01). The description gives **no equations**, only "Discretized Allen-Cahn equation: a lattice of 32x32 harmonically coupled particles in a double-well potential, subjected to noise", plus two links (an article on images.math.cnrs.fr and Berglund's research page at idpoisson.fr), not opened. A frame shows a 3D plot of the 32×32 particle heights (red near the upper well, blue near the lower) with a colour map of the same field projected below: two large domains separated by a moving interface.
- **Core idea:** stochastic Allen-Cahn on a lattice: each site sits near one of two wells (+1 / -1), neighbours pull toward each other through springs, noise kicks sites across the barrier; domains form, coarsen and their interfaces move. A continuous cousin of Ising / majority-vote CA.
- **Techniques:** Stochastic Allen-Cahn lattice — NEW. The standard discretised form (general knowledge, NOT from the description): per site and time step dt, x += [ -(x^3 - x) + k·(sum of 4 neighbours - 4x) ]·dt + σ·sqrt(dt)·ξ, with ξ a standard normal sample. Threshold at 0 → binary mask with organic domain boundaries; k sets interface width, σ the noise, the number of steps the coarsening scale.
- **Determinism, seeds, chunks/seams:** deterministic only if ξ comes from a counter-based hash (SmallXXHash of seed, cell, step), not a stateful RNG; a chunk needs a halo of N cells for N steps.
- **Islands status:** missing. No smoothing or majority filter either (`MaskMorphologyOps2D.cs` has only erosion and a BFS distance field).
- **Where it could land:** (a) an exploratory "phase-separation" mask operator for blobby land/water or biome domains with tunable coarsening; a few dozen lines, headless, hashed noise; gain over thresholded fBm uncertain. (b) NoiseLab mode "Allen-Cahn" with sliders for steps, k and σ, showing coarsening live. (c) web, below.
- **Value for web:** high. A concrete page "Order from noise": a 64×64 grid starting from pure noise, play/pause, sliders for coupling k and noise σ; a 3D height view like the video's next to a flat two-colour map; a counter of domain-wall length falling over time (coarsening); a "too much noise" setting that never orders (symmetry breaking needs low σ).
- **Value for Islands:** low-medium — interesting, exploratory; web value high.
- **Changes vs. sampled card:** confirmed the description has no equations (the formula stays marked as general knowledge), described the visible frame (3D heights + projected map), noted the two reference links and that Islands has no majority filter. Value label normalised from "medium-low" to "low-medium" (same meaning).

### Where do these circles come from? — Henry Segerman (4:08)
- **URL:** https://youtu.be/A2IAyXc0LuE
- **Source note:** full transcript read (re-read 2026-11-01); English human captions, good.
- **Core idea:** Voxel spheres (every sphere approximation on a cubic lattice centred on a cube centre) show circles when faces are coloured by facing direction, as on a Lego globe. The circles are contour-like disruptions of the default face-colour pattern where the sphere curves away from a tangent plane. Moiré appears when cubes shrink below pixel size.
- **Techniques:**
  - Animation of all voxel-sphere approximations (0:14–0:36): zooming out so the sphere stays the same size; cube side inversely proportional to time. Real-time Shadertoy shader (shadertoy.com/view/7ds3zB; 0:36–0:53).
  - Origin of the question (0:53–1:23): Ravi Vakil saw circles on the Legoland California globe; circles along the coordinate axes (expected, "contours of a round hill"), midway between axes, and in other directions.
  - Face colouring by direction (1:26–1:47): red/green/blue per face normal; on the real globe the light does the same shading.
  - Mechanism (1:47–2:42): near an axis the default pattern is one colour (green) because those faces would form a tangent plane; where cubes must step away from that plane, red and blue faces appear and draw the circle. Midway between axes the default is green/blue stripes; disruptions of the stripes draw circles again.
  - Long diagonal (2:42–3:02): default is a tiling of all three colours; disruptions are harder to see but present; more complex directions become visible as cubes shrink.
  - Open question (3:05–3:19): why the circles "ripple" at different rates per direction (slowest on the axes).
  - Moiré (3:21–3:45): grid of cubes vs grid of pixels; he expects sub-pixel sampling (supersampling) to remove it.
- **Parameters / numbers:** none beyond side ∝ 1/time. Credits CodeParade for the Legoland photos (description).
- **Determinism, seeds, chunks/seams:** n/a.
- **Islands status:** n/a as a technique. Relevant caveat: rasterising circular shapes (SDF discs and radial island masks, `Runtime/PCG/Primitives/SdfComposeOps.cs`, `Runtime/PCG/Layout/Maps/IslandShapeMode.cs`) on a grid produces these stair-step patterns; they become visible when stair faces are shaded by direction (autotiling by edge direction, 3D voxel adapters) and as moiré when cells are smaller than screen pixels.
- **Where it could land:** web (c) tiny explainer pairing with Islands shape masks. Practical note for a future voxel/autotile adapter: expect contour-like circles on radial shapes; noise-perturbed shapes hide them.
- **Value for web:** medium-low. A concrete page: a voxel sphere (or in 2D a pixel disc with edges coloured by direction) with a slider for cell size, faces coloured RGB by normal; toggles "light shading instead of RGB" (the Lego look) and "supersampling" to make the moiré go away.
- **Value for Islands:** low — rendering curiosity; one caveat for future adapters.
- **Changes vs. sampled card:** added the Shadertoy link, the Legoland/Ravi Vakil origin, the stripes-between-axes and three-colour-diagonal mechanism, the sub-pixel-sampling fix for moiré, and the Islands files where circular masks are made. Value unchanged.

### Simulating Black Holes in C++ — kavan (12:27)
- **URL:** https://youtu.be/8-B6ryuBkCM
- **Source note:** full transcript read (re-read 2026-11-01); English auto-generated captions, fair (physics terms garbled: "short sealed radius" = Schwarzschild, "crystal symbols" = Christoffel symbols, "Runga Cuda" = Runge-Kutta, "geodessic.com" = a `.comp` compute shader).
- **Core idea:** A ray tracer whose rays follow null geodesics of the Schwarzschild metric instead of straight lines. 2D first on the CPU (rays with fading trails bending around the hole), then 3D: 480,000 rays × 10–20k steps is too slow on the CPU, so the integrator moves to a GPU compute shader; a disc around the hole shows the lensing halo.
- **Techniques:**
  - Set-up (0:51–2:16): OpenGL in C++; structs for the black hole (position, mass, event horizon = Schwarzschild radius r_s) and rays (position, stepped at c = 299,792,458 m/s); circle drawn from sin/cos; ray trails stored as a vector of past positions, brightest at the tip and fading out.
  - Polar coordinates centred on the hole (2:24–3:04): r = hypot(x, y), φ = atan2(y, x); if r < r_s stop stepping (the ray is captured).
  - Physics explanation (3:06–4:46): geodesic = straightest path in curved space (plane routes on a flat map); light follows a null geodesic; Einstein field equations; Schwarzschild's 1915 solution for a single static spherical mass (non-spinning hole).
  - Geodesic equations (4:46–6:47): from the geodesic equation with Christoffel symbols, with an affine parameter as the step: φ'' = -(2/r)·r'·φ' and r'' = -(c^2·r_s)/(2r^2) + r·φ'^2 (as stated in the video, signs reconstructed from garbled captions; a simplified form, not the full Schwarzschild null-geodesic system, which also carries (1 - r_s/r) factors). Step: accelerations update dr and dφ, which update r and φ, converted back to Cartesian for the trail (6:47–7:04).
  - Results (7:04–7:26): visible bending, rays spiralling in, one ray completing three orbits before escaping.
  - Euler fails (7:27–7:52): a ray takes one giant step straight into the hole; paths too straight ("driving fast on a curved road in pitch dark").
  - **RK4** fix (7:52–8:28) — NEW integrator pattern: evaluate the geodesic function four times per step on intermediate states and combine them with the RK4 weights; smooth curves, no rigid steps.
  - Two black holes (8:28–8:38): a quick 2D test with two polar frames.
  - 3D (8:38–11:20): add θ; 800×600 = 480,000 rays per frame, four evaluations per RK4 step, 10–20k steps for the hole to appear: the CPU frame rate collapses (a toggle that disables bending shows the curving is the cost). Orbit camera "like in Blender" shooting rays by FOV; pixels written into a texture on a fullscreen quad. A GPU compute shader runs a 20,000-step loop per ray: red and break if captured, otherwise RK4 and continue. Resolution lowered to 400×300.
  - Objects (11:20–11:41): a disc around the hole with a ray-object intersection test → the halo image.
- **Parameters / numbers:** c = 299,792,458 m/s; ~20,000 RK4 steps per ray; 800×600 → 400×300. Code: github.com/kavan010/black_hole (description).
- **Determinism, seeds, chunks/seams:** not discussed; fixed-step RK4 is deterministic on one device, but CPU vs GPU float differences diverge for rays near the photon sphere (a chaotic region).
- **What it adds over the hydrogen-orbital card** (`2026-10-31-hydrogen-cloud-sampling.md`, same author): hydrogen was sampling a density (stateless, per vertex); this one integrates an ODE per sample (stateful, many steps, RK4). Shared lessons: C++/OpenGL, compute shader for parallel independent samples. 10–20k steps per ray is not realistic for the mobile NoiseLab.
- **Islands status:** missing: no ODE integrator in `Runtime` (Grep for RK4 / Runge / integrat found none). Not a world-gen technique.
- **Where it could land:** RK4 is a small useful tool only if Islands ever simulates particles (droplet erosion D.11, wind/weather in CURRENT_STATE "Atmospheric / weather"); Euler or semi-implicit Euler is usually enough there. Web, below.
- **Value for web:** medium. A concrete 2D page "Bend the straight lines": a black hole in the centre, a fan of rays from the left with fading trails; an Euler / RK4 toggle and a step-size slider that reproduce the "ray jumps into the hole" failure; a slider for the hole's mass (r_s) and a click to launch a single ray, with an orbit counter; fits next to the Warp Bubble page.
- **Value for Islands:** low — physics rendering, not terrain; RK4 only as a future utility.
- **Changes vs. sampled card:** added the trails, the capture rule, the geodesic/null-geodesic explanation, the caveat that the stated equations are simplified, the two-black-hole test, the bending-toggle cost diagnosis, the texture-on-quad and 20k-step compute loop details and the GitHub repo. Value unchanged.

#### Part summary
- **Highest value:** box-counting fractal dimension (3B1B): a measurable roughness metric for the coast mask. Numbers verified in the transcript: Britain ≈1.21 (2:09, 13:22, still ≈1.21 at ×1000 zoom, 18:05) and Norway ≈1.52 (19:03); the video cites no source. Procedure: count touched cells at several scales, fit log-log, slope = D; call it fractal only if the slope is roughly constant over the measured scales. On Islands-size maps the scale range is short (block sizes 2–32), so treat D first as an inspection metric with a fit residual, then as a range assertion in EditMode tests.
- **Generic CA mask operator** (Moore B/S masks, double-buffered, fixed steps) is the missing D.12 piece; `MaskMorphologyOps2D.cs` has only erosion and a BFS distance field (no dilate, no majority filter). Elementary rules 30/110 (tables confirmed against the author's descriptions) belong to a NoiseLab "CA lab" and the web emergence page; stochastic Allen-Cahn is an exploratory domain-forming operator.
- **Web, not Islands:** logistic-map page linked to the published Mandelbrot scene (cobweb, bifurcation, "see it from the side", sin-map universality); elementary-CA rule editor feeding Life; Allen-Cahn "order from noise"; Newton parameter space with the hidden Mandelbrot; Euler vs RK4 light bending; box-counting "How rough is your coastline?"; Collatz coral as an aside. Logistic map and Rule 30 are poor Islands RNGs next to SmallXXHash.
- **Corrections from the re-read:** the 3B1B fractal video is 19:54; the Veritasium logistic map is now read in English; Collatz negative integers have three loops starting at values such as -17 and -5; SmallXXHash chains coordinates with `Eat()` (no additive key collisions); the Allen-Cahn description has no equations (formula stays general knowledge); Golly is the program behind Rational Animations' video; the baseline "dilate/erode" claim was wrong (erosion only).
- **No transcript:** ecSl5TzJZpo, IK7nBOLYzdE (music-only caption track), P2uhhAXd7PI, C2vgICfQawE, Udrt2otKmyA.
