# Playlist «PCG» — Tier 2 technique cards

33 videos from groups B (tutorials) and D (math, noise, emergence) of [`../2026-11-01-playlist-pcg.md`](../2026-11-01-playlist-pcg.md), analysed on 2026-11-01. Research material, not authority.

**How they were made — read this before trusting a card:** the transcript library was IP-blocked by YouTube after Tier 1, so three Sonnet subagents read YouTube's transcript panel in Chrome. The Chrome tool truncates script output at about 1 KB, so **most transcripts were sampled in slices (start, middle, keyword probes), not read in full**; cards say so where details are missing. Some videos have no speech or no captions (cards from title and description, marked). Some captions were only available in Spanish or Arabic. Islands status was checked with quick greps; the orchestrating agent corrected one claim (see `crossref.md`, Tier 2).

Parts: 1 noise and tile tutorials · 2 Sunny Valley dungeon series and art pipeline · 3 math and emergence.


---

## Part 1

## Part 1 cards (beginner/intermediate Unity tutorials)

Status checks done with Grep/Glob on `D:\Projects\Islands\Runtime`: no cellular-automata cave code, no WFC, no neighbor-based autotiler (grep for cellular/wfc/autotile/wang only hits "quantile" code). Present: `Runtime/Noise/Noise/Noise.Lattice.cs`, `Noise.Simplex.cs`, `Noise.Gradient.cs`, `Noise.Voronoi.Function.cs`; `Runtime/PCG/Layout/MaskNeighborOps2D.cs`; `Runtime/PCG/Operators/{MaskMorphologyOps2D,ScalarToMaskOps,MaskFloodFillOps2D}.cs`; map stages `Layout/Maps/Stages/Stage_{BaseTerrain,Hills,Shore,Biome,Hydrology,Vegetation,Traversal}2D.cs`; tilemap adapter `Adapters/Tilemap/{TilemapAdapter2D,TilesetConfig,BiomeTileOverride,MegaTile*}.cs`; quantile cuts (Stage_Vegetation2D, W-aux.d; NoiseLab area-quantile water, CURRENT_STATE lines ~31, 66, 153). All 9 videos had transcripts (auto-captions; CodingQuest's are badly garbled, gist only).

### PERLIN NOISE in Unity – Procedural Generation Tutorial — Brackeys (10:34)
- **URL:** https://youtu.be/bG0uEXV6aHQ
- **Core idea:** Absolute-beginner texture demo: loop over a 256x256 `Texture2D`, call Unity's `Mathf.PerlinNoise(x, y)` per pixel and write it as grey. Main teaching points are the classic pitfalls (integer inputs, scale, offset, `Apply`).
- **Techniques:**
  - Perlin noise as a 2D scalar field — D.4 — one `Mathf.PerlinNoise` call per pixel, output 0..1 as grey (5:15).
  - Normalised sampling coordinates — D.4 (usage pattern) — divide pixel index by width/height so input spans 0..1, then multiply by a `scale` (default 20) to zoom (6:30, 8:14). Reason given: Perlin is zero/flat at whole numbers, so integer pixel coordinates give junk (6:08).
  - Offset as pan and as "seed" — D.1/D.4 (usage pattern) — add `offsetX/offsetY` (default 100) to the scaled coords; randomising the offset gives a different map each run (9:05-10:05). Not a real seed, just a random sample window.
- **Parameters / numbers:** 256x256 (65,536 px; he says "over 655,000" garbled), scale 20, offsets 100, random offset range "zero to a huge number".
- **Determinism, seeds, chunks/seams:** Only "random offset instead of seed"; nothing on determinism across runs or chunks.
- **Islands status:** Perlin noise — exists (`Runtime/Noise/Noise/Noise.Lattice.cs`, seeded via SmallXXHash, deterministic, Burst). Scale/offset/domain — exists (TerrainNoiseSettings / MapNoiseBridge2D). Texture preview — exists (NoiseLab sample, per CURRENT_STATE).
- **Where it could land in Islands:** Nowhere new. Possibly a one-line "common mistakes" callout (integer coordinates, forgot Apply) in the web Islands Lab Perlin page, which already exists.
- **Value for Islands:** low — adds nothing technical; Islands already has a stronger, deterministic Perlin and a didactic scene.

### MESH COLOR in Unity – Terrain Generation — Brackeys (13:15)
- **URL:** https://youtu.be/lNyZ9K71Vhc
- **Core idea:** Continues a mesh-terrain series: (a) UVs for a flat grid mesh, (b) per-vertex colour from a height gradient, shown with a vertex-colour Shader Graph. Mentions that stacking noise layers (octaves) makes terrain better, without teaching it.
- **Techniques:**
  - Grid UVs — NEW (rendering, not generation) — UV = (x/xSize, z/zSize) per vertex (3:06-4:30).
  - Height-to-colour ramp via vertex colours — NEW (presentation) — track min/max vertex height while building, normalise with `Mathf.InverseLerp(min, max, y)`, feed to `Gradient.Evaluate`, store in `mesh.colors`; unlit-style PBR Shader Graph with a Vertex Color node into Albedo (5:45-11:25). Painted bands: water, grass, rock, snow (11:44).
  - Multi-octave noise — D.7 — only name-dropped (1:12).
- **Parameters / numbers:** Heights unnormalised (about 3-20) so normalisation is required before the gradient; Gradient keys by hand.
- **Determinism, seeds, chunks/seams:** Not discussed. Min/max normalisation is per mesh, so it would give different colours per chunk (seam hazard), unstated.
- **Islands status:** Height-ramp colouring — partial: `Noise.Gradient.cs` exists in the noise lib, `ScalarOverlayRenderer.cs` / `PCGMapTilemapVisualization.cs` colour scalar fields on tilemaps, and the web Islands Lab has a palette library. No 3D mesh/vertex-colour path (Islands is grid-first, adapters last). fBm — exists (D.7).
- **Where it could land in Islands:** Only if a future 3D mesh adapter is built: a vertex-colour height ramp using a fixed global height range instead of per-mesh min/max. Otherwise nothing.
- **Value for Islands:** low — rendering tutorial for 3D meshes; the one transferable point (normalise by a global range, not per-mesh min/max) is already how Islands treats fields.

### Procedural world generation with Perlin Noise — CodingQuest (14:29)
- **URL:** https://youtu.be/eiM_AStmeGE
- **Core idea:** Python/PyGame 2D civilisation-style tile world: sum scaled Perlin layers into a height map, split the height range into terrain types by weights, and draw each tile choosing a sprite from the terrain type at its four corners so coastlines and beaches transition smoothly. (Captions are garbled; details below are the clear parts.)
- **Techniques:**
  - Multi-octave sum of separate Perlin functions — D.7 — four Perlin generators with octaves 3, 6, 12, 24, each scaled down progressively before summing (3:40-5:09). Seeded by a random seed parameter (9:34).
  - Height range split by weights — NEW-ish variant of D.10/D.20 — terrain types ordered low to high; a weight list divides the (min to max) height range proportionally into bands: more weight on ocean raises sea level, giving islands (11:01-13:00). The range is measured from the actual lowest to highest value, so water fraction is approximately (not exactly) the weight share.
  - Corner-based tile choice — D.18 (Wang/corner tileset variant) — sample the noise map at (W+1) x (H+1) points; each tile reads the terrain type at its 4 corners and picks the tile sprite that has those corner types (7:40-10:00, 13:34). Removes hard stair-step edges between types.
- **Parameters / numbers:** Octaves 3/6/12/24 (his library's "octaves" meaning), weight list per terrain type, noise map is size+1 in X and Y.
- **Determinism, seeds, chunks/seams:** Seeded noise; no chunking.
- **Islands status:** Multi-octave sum — exists (fBm, `Runtime/Noise`). Weighted band split — partial/exists: Islands already does better with exact quantile cuts (`Stage_Vegetation2D` quantile, NoiseLab percentage water; documented in CURRENT_STATE). Corner-based autotiling — missing: `TilemapAdapter2D` + `TilesetConfig` + `BiomeTileOverride` + `MegaTile*` map a cell to a tile per cell (and stamp multi-cell pieces), no corner-sampled or bitmask neighbor selection.
- **Where it could land in Islands:** (1) Tilemap adapter: an optional "corner/dual-grid tile selection" pass that reads the biome/terrain id of the 4 cells around each vertex and indexes a 16-entry tileset (needs W+1 x H+1 sampling or dual grid). The project map says the 2026-10-27 bookmark research covers WFC and dual grid (not re-verified here). (2) Web Islands Lab: a short note comparing weight bands vs quantile cuts.
- **Value for Islands:** medium — corner-sampled transitions fill a real gap in the tilemap adapter (D.18 missing); the rest Islands already exceeds.

### Procedural Generation In Unity 2D With Tile & Perlin noise — ChronoABI (24:02)
- **URL:** https://youtu.be/AqXC-QmhRXQ
- **Core idea:** 2D side-view terrain: replace GameObject-per-tile spawning with three `Tilemap`s (dirt, grass, stone) and compute a column height from 1D Perlin noise. Explains why Tilemap scales better and shows a seed as an X offset.
- **Techniques:**
  - 1D heightline from Perlin — D.10 (side-view variant) — `height = Mathf.PerlinNoise(x, seed)` style call (the speaker writes `PerlinNoise(x, seed)` with time as an anecdote first), times a `height` value, divided by a `smoothness` value, rounded to int (11:00-16:00). Per column: fill stone below a computed depth, dirt layers, one grass tile at the top.
  - Tilemap instead of GameObjects — n/a (engine usage) — `Tilemap.SetTile(Vector3Int, TileBase)`; claims 300x100 = 30,000 tiles runs fine (9:20-10:10). Separate tilemap per tile kind (dirt/grass/stone) is a layering choice.
  - Seed as Perlin's second argument — D.1 (usage pattern) — `seed = Random.Range(-1e6, 1e6)` fed into the y argument; each value gives a different but repeatable terrain (20:40-22:50).
  - Save as prefab — n/a — editor-only utility.
- **Parameters / numbers:** width 80 (demo) up to 300x100 stress test, height 30-70, smoothness 20-60, seed range +-1,000,000. "Height > smoothness" gives jagged, "smoothness > height" gives smooth terrain.
- **Determinism, seeds, chunks/seams:** Seed is a float offset (not a PRNG seed); no chunking.
- **Islands status:** Heightline from noise — exists in spirit (heightmap terrain `Stage_BaseTerrain2D`, top-down); side-view platformer terrain — missing but not in Islands' scope (grid-first top-down maps). Tilemap output — exists (`TilemapAdapter2D`, per-layer groups via `TilemapLayerGroup`/`TilemapLayerEntry`).
- **Where it could land in Islands:** Nowhere. A 1D side-view mode could be a web Islands Lab toy, but not useful for the world generator.
- **Value for Islands:** low — Islands already has the Tilemap adapter and deterministic noise; this video is weaker on both.

### Tile Map auto generation in Unity – TUTORIAL — Daniel Hofmann (32:38)
- **URL:** https://youtu.be/xNqqfABXTNQ
- **Core idea:** Cellular-automata cave/island generator on a Unity `Tilemap` using the birth/death-limit rule, with two tilemaps (terrain top, water/lava bottom) and a save-to-prefab helper. Click to run another iteration so the structure visibly smooths.
- **Techniques:**
  - Birth/death-limit cellular automaton — D.12 — init: each cell alive with probability `initialChance` (25% in the demo; `Random.Range(1,101) < chance`, 3:50-10:30). Step: count the 8 neighbors (Moore); out-of-map cells count as alive, which draws a solid border (14:20-16:20). Alive cell with neighbors < death limit dies; dead cell with neighbors > birth limit is born (16:30-18:00). Double buffered (old map -> new map). Repeat N times (slider 1-10) or one per click (4:40, 20:50).
  - Two-layer output — n/a — top tilemap for filled cells, bottom tilemap fully filled for water/lava underneath (19:00-20:30).
  - Editable/saved result as prefab — n/a — `PrefabUtility` to save the generated grid (27:00-30:00); also swap tile assets (animated tiles) after the fact.
- **Parameters / numbers:** initialChance 25%, birthLimit 3, deathLimit 2, repetitions 1 per click, map 120x70, sprite 24 px, PPU 24. Sliders: limits 1-8, repetitions 1-10.
- **Determinism, seeds, chunks/seams:** Uses `UnityEngine.Random` with no seed control, so not reproducible; no chunking. Edge handled by treating outside as alive.
- **Islands status:** CA caves — missing in code (no cellular-automata generator in `Runtime/PCG`; `MaskNeighborOps2D.cs` and `MaskMorphologyOps2D.cs` already provide the neighbor-count building blocks; technique D.12 in the library). Tilemap output and layers — exists (`TilemapAdapter2D`, `TilemapLayerGroup`).
- **Where it could land in Islands:** New layout generator next to `SimpleRandomWalk2D`/`IteratedRandomWalk2D` in `Runtime/PCG/Layout/` (e.g. `CellularCaves2D`), seeded through `LayoutSeedUtil`, using `MaskNeighborOps2D` for the count, a ping-pong double buffer, a border policy parameter (alive/dead/wrap), and optional post-pass with `MaskFloodFillOps2D` to keep the largest connected region. Also a web Islands Lab page (the grid is tiny, teaches well). Note Hofmann's version is the beginner form (single-pass 4/5-style rule); the classic Lague/Johnson form (neighbors >4 -> wall) is equivalent in outcome.
- **Value for Islands:** medium — it is the clearest walkthrough of D.12, which Islands lacks; Islands should implement it deterministically, not copy the unseeded RNG.

### How to store data in tiles (Unity Tilemap + Scriptable Objects) — Shack Man (9:33)
- **URL:** https://youtu.be/XIqtZnqutGg
- **Core idea:** Gameplay data per tile TYPE (walk speed, poison) lives in a `ScriptableObject` wrapping a list of `TileBase`; a manager builds a `Dictionary<TileBase, TileData>` at start so any script can query the data under a world position. Explicitly not for per-cell mutable state.
- **Techniques:**
  - Tile type to data lookup — n/a (gameplay data) — `TileData : ScriptableObject { TileBase[] tiles; float walkingSpeed; float poison; }`; `tilemap.GetTile(WorldToCell(pos))` then dictionary lookup; return 1 if no tile (4:04-7:55). Several sprites can share one data asset.
  - Per-cell mutable state — mentioned only: he says use another approach (a future video) (0:46).
- **Parameters / numbers:** Walking speed 1 (grass) and 2 (stone) as multipliers.
- **Determinism, seeds, chunks/seams:** Not relevant.
- **Islands status:** Lookup from tile to gameplay data — partial: `TilesetConfig` / `BiomeTileOverride` map ids to tiles (generation to display direction); Islands keeps semantics in the data layers (`MapLayerId` masks / `MapFieldId` fields), which are the better source of truth than looking up the tile.
- **Where it could land in Islands:** mundo-islas (gameplay) only: query biome/mask layers directly for movement cost rather than reading data back from tile assets. The video is a useful counter-example (display-asset-as-data).
- **Value for Islands:** low — Islands separates data layers from rendering by design; nothing to adopt.

### Wave function collapse Unity Tutorial 01 Introduction — Sunny Valley Studio (5:56)
- **URL:** https://youtu.be/ws4r3wLPNSE
- **Core idea:** Series intro only: WFC generates a larger tilemap resembling a small example input; one example gives many outputs. Promises a series: input reader, pattern manager, solver, output creator, in object-oriented, reusable code with interfaces so the core works on any grid. No algorithm is explained here.
- **Techniques:**
  - Wave Function Collapse (overlapping/pattern model, by description) — D.16 — only named and demoed on Stålberg's tool (1:11-3:00); steps promised for later videos (3:55-4:35: "reading input, pattern manager, solver, output creator").
  - Adapter-style architecture — n/a — core solver on generic grid values, with input and output interfaces for tilemaps (3:20-3:48). Same philosophy as Islands' core-vs-adapter split.
- **Parameters / numbers:** None. Uses Kenney assets.
- **Determinism, seeds, chunks/seams:** Not discussed.
- **Islands status:** WFC — missing in code (no WFC class under `Runtime/PCG`); technique D.16 in the library. Core/adapter split — exists (the Islands design principle).
- **Where it could land in Islands:** Nothing from this video. If WFC is ever added, the useful reference is the later videos in this series, not this intro.
- **Value for Islands:** low — pure introduction, no content on the algorithm. Flag: adds nothing.

### Procedural 2D Map with Caves P1 Basic Perlin Noise – Unity 2020 — Sunny Valley Studio (14:16)
- **URL:** https://youtu.be/MTNt32_NQlc
- **Core idea:** Part 1 of 2. Introduces `Mathf.PerlinNoise` on a 1D side-view heightline with tilemaps, visualising raw values as text meshes first, then filling tiles from y=0 up to the noise height. Part 2 (not in this batch) adds multi-octave and caves; the caves promised in the title do not appear here.
- **Techniques:**
  - 1D heightline from Perlin — D.10 (side-view) — `height = Mathf.Floor(PerlinNoise(x*freq, 1) * amplitude)`, fill a column of dirt tiles up to that height (6:17-12:20).
  - Amplitude and frequency as the two terrain knobs — D.4/D.7 (usage) — amplitude scales 0..1 to 0..N tiles; frequency must not be 1 (integer sampling gives the same value); 0.01 broad hills, 0.05-0.09 more and narrower hills (7:00, 12:43-13:50).
  - Value debug view (text per sample) — n/a — useful teaching device only.
- **Parameters / numbers:** map length 50, amplitude 1 -> 10 -> 50, frequency 0.01 / 0.05 / 0.08 / 0.09 / 0.1.
- **Determinism, seeds, chunks/seams:** No seed; same output each run.
- **Islands status:** Perlin, frequency and amplitude — exists (`Noise.Lattice.cs`, `TerrainNoiseSettings.cs`). Side-view columns — out of scope. Tilemap writing — exists.
- **Where it could land in Islands:** Nothing.
- **Value for Islands:** low — duplicates Brackeys' basics; the cave content sits in part 2.

### I Made Minecraft in 24 Hours — Sam Hogan (13:28)
- **URL:** https://youtu.be/Nj8gt_92c-M
- **Core idea:** A speed-run dev log of a Minecraft-like in Unity with a noise library (3D noise from a downloaded library). The generation-relevant parts are quick, but they describe a layered recipe for terrain, caves, strata, trees, water, with a clear determinism trick for trees.
- **Techniques:**
  - Chunked voxel meshes with face culling — NEW (3D voxel meshing) — loop blocks in a chunk; emit a quad only where a solid block touches air (1:50-3:00). Replaces 40,000 cubes (200x200) that performed badly.
  - Infinite terrain by chunk streaming — NEW-ish (streaming) — dictionary of loaded chunks; load around the player when entering a new chunk, unload distant ones (3:25-3:55).
  - Surface = sum of several 2D noise layers at different scales; product of two layers to make some regions hilly and others flat — D.7 plus a modulation trick (5:48-6:03). The product (hilliness mask times detail) is a cheap regional-variation technique.
  - 3D noise caves with a noise mask — NEW (3D carve) — generate land, subtract cells where 3D noise passes a threshold, restrict caves to regions using another noise layer as a mask (6:10-6:50).
  - Stone/dirt strata — NEW-ish — a second heightmap lower than the surface; blocks under it become stone, noise lets stone reach the surface sometimes (6:50-7:15).
  - Chunk-seeded RNG for trees — D.1 — seed a PRNG with the chunk coordinates so a chunk reloaded later regenerates the identical trees (7:19-8:15).
  - Water as a separate mesh built from the terrain (so it's not inside land or over cave mouths) — NEW (rendering) (8:28-9:20).
  - Edit voxel data then regenerate the mesh — n/a (gameplay).
- **Parameters / numbers:** 20x20 test grid, 200x200 breakdown (about 40,000 cubes); no noise numbers given.
- **Determinism, seeds, chunks/seams:** The one explicit point: per-chunk RNG seeded by chunk coordinates (so chunk load order never changes results). He says chunks have "seamless transitions" after a hiccup (3:12), meaning noise is sampled at world coordinates (not stated explicitly). No details.
- **Islands status:** Multi-octave and layered noise — exists (fBm/ridged in `Runtime/Noise`, `MapNoiseBridge2D`). Product/mask of two noise layers — partial (the map pipeline composes fields; no explicit "hilliness mask" recipe checked). Coordinate-seeded RNG — exists (`LayoutSeedUtil.cs`, SmallXXHash). Chunk streaming — missing (infinite maps are not in Islands; the web "Sea of points" scene did a chunked version, per the project notes). 3D voxel meshes, 3D cave carving, water mesh — missing and out of the 2D grid-first scope. Existing research in `research\Games Worldgen\` on Minecraft covers the real algorithm much better; this card adds only the compact beginner recipe.
- **Where it could land in Islands:** Mostly nowhere. The "multiply two layers to vary hilliness regionally" idea could be a tunable in `Stage_Hills2D` / a web Lab explainer; the coordinate-seeded RNG per chunk is already the Islands principle and worth quoting when explaining chunk determinism.
- **Value for Islands:** low — entertaining overview; the Minecraft research already in the repo and Islands' own pipeline are far deeper.

#### Part summary
- Most valuable for Islands: Daniel Hofmann's cellular-automata Tilemap tutorial (D.12) — clearest walkthrough of a technique Islands does not have; implement as a seeded `CellularCaves2D` layout using `MaskNeighborOps2D` plus flood-fill for the largest cave, not the unseeded `Random` version.
- Second: CodingQuest's corner-sampled tile selection (sample (W+1)x(H+1), pick tile by 4 corner types, D.18) — a real gap in `TilemapAdapter2D`/`TilesetConfig`; its weighted-band height split is already superseded by Islands' quantile cuts.
- Flag: the Brackeys Perlin and mesh-colour videos, ChronoABI, Shack Man's tile-data video, the WFC intro (5:56, no algorithm content) and Sunny Valley's Perlin part 1 add nothing beyond what Islands already has; the Minecraft-in-24-hours video is covered in more depth by `research\Games Worldgen\`.
- NEW techniques (outside D.1-D.20): chunked voxel face culling, 3D-noise cave carving with a noise mask, stone/dirt strata from a second heightmap, separate water mesh (all 3D, out of scope for the grid-first library); height-range split by weights (variant of D.10/D.20).

---

## Part 2

## Part 2 cards

Note: the Sunny Valley dungeon series (P2-P4) has no credit in the Islands sources (grep for "Sunny Valley" in `D:\Projects\Islands` only hits two research tables, "in research"). But the structure matches the series closely: iterated walk from a random already-carved cell, corridor-first with rooms at corridor endpoints and at dead ends, and BSP with per-leaf padding. So these are very likely ports; Islands replaced HashSet with a MaskGrid2D plus `DeduplicateInPlace`, and Tilemaps with a grid-only core.

### Theory - P2 - Unity Procedural Generation of a 2D Dungeon — Sunny Valley Studio (4:11)
- **URL:** https://youtu.be/jlNqp_QAibo
- **Core idea:** A dungeon is a set of rooms that must all be reachable, so rooms need connecting corridors. Two strategies, corridor-first and room-first, built from two algorithms: random walk and binary space partitioning (BSP). Pure theory, no code.
- **Techniques:**
  - Corridor-first — NEW (not in D.1-D.20; closest is D.14) — carve connected corridors first (connectivity is guaranteed by construction), then pick random points along them and grow a room there, e.g. with a random walk (0:30-1:00).
  - Room-first — D.13 — make the rooms first, then connect them with corridors. The video only promises "a very simple method" and leaves the details to later episodes (1:00).
  - Random walk, simple and iterated — D.14 — one agent steps in random directions for N steps and gives a dense island-like blob around the start. Iterating: each new agent starts from a random already-visited cell, so each new path attaches to the old ones and the area gets much bigger (1:30-2:30).
  - BSP — D.13 — split a big rectangle vertically or horizontally, recursively, until pieces are too small to split but can still hold a room of minimum width and height. Then shrink each leaf by an offset (about 1 tile per side) so adjacent rooms are separated by wall (2:30-3:30).
  - Walls around floor and corridors — D.18 (autotiling) — postponed to later episodes (3:40).
- **Parameters / numbers:** minimum room width/height; step count per walk; number of iterations; offset about 1 tile from each side. No other numbers.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:**
  - Simple random walk: exists, `Runtime/PCG/Layout/SimpleRandomWalk2D.cs`.
  - Iterated random walk: exists, `IteratedRandomWalk2D.cs`.
  - Corridor-first: exists, `CorridorFirstDungeon2D.cs` (corridors with `DrawLine`, rooms at endpoints and optionally dead ends).
  - BSP room-first: exists, `RoomFirstBspDungeon2D.cs` + `Layout/Bsp/BspPartition2D.cs`.
  - Walls/autotiling: partial. Not checked in depth here (D.18 belongs to another card).
- **Where it could land in Islands:** nowhere new. It is the conceptual source of the existing layout strategies. Possible use as intro text on the Islands Lab web page for dungeon layouts (strategy comparison corridor-first vs room-first).
- **Value for Islands:** low. Already implemented; only useful as the origin reference for the docs.

### Project setup - P3 — Sunny Valley Studio (5:43)
- **URL:** https://youtu.be/9W_ZoPRQ8OQ
- **Core idea:** Prepares the Unity 2020 2D project used by the series: 2D template, a provided tile/sprite pack, named floor and several wall tiles, and quality settings. Nothing about the algorithms.
- **Techniques:**
  - Tilemap visualization with named wall tiles (one per wall type, assigned to the visualizer script) — n/a for the generator, adapter-level idea.
  - Turn off anti-aliasing in Quality settings to remove seam artifacts between tiles when zoomed (about 3:00).
- **Parameters / numbers:** Unity 2020.1.14 used (not important per the speaker); 2D preset.
- **Determinism, seeds, chunks/seams:** not discussed. The anti-aliasing tile seams are a rendering matter only.
- **Islands status:** n/a (setup). The architecture it hints at (generator separate from a tilemap visualizer) already exists in Islands as headless grid core plus adapters.
- **Where it could land in Islands:** a note in the Tilemap adapter docs: disable MSAA / anti-aliasing and use pixel-perfect settings to avoid seams. Nothing in core.
- **Value for Islands:** low. Only the seam/anti-aliasing tip.

### Random Walk Algorithm - P4 — Sunny Valley Studio (9:19)
- **URL:** https://youtu.be/F_Zc1nvtB0o
- **Core idea:** Implements the random-walk primitive in a static class of "procedural generation algorithms". The walk returns a set of positions and not a tilemap, so the algorithm is separate from drawing.
- **Techniques:**
  - Simple random walk — D.14 — start at a position, pick a random cardinal direction, step; repeat for N steps and collect every visited cell.
  - Deduplication by HashSet — D.14 — the walk can revisit cells, so positions go into a HashSet (value-equality of Vector2Int) to avoid duplicates; set operations (union, intersect, except) are mentioned as handy (about 2:30-3:30).
  - Direction list helper (up/right/down/left, random pick) — D.14. Implied by the implementation; the transcript sampled here did not show details.
  - Reuse by iterated walks is left for the next video (P5, not in this batch).
- **Parameters / numbers:** start position, walk length; direction set is the 4 cardinal directions. No other numbers.
- **Determinism, seeds, chunks/seams:** uses Unity's random (not seeded in the part sampled). Not deterministic by design here.
- **Islands status:** exists and improved. `SimpleRandomWalk2D.Walk` writes into a `MaskGrid2D`, uses seeded `Unity.Mathematics.Random` passed by ref, has brush radius, skew and retries (extras beyond the video); `Direction2D.cs` is the direction helper.
- **Where it could land in Islands:** nothing missing.
- **Value for Islands:** low. Islands is already a superset of this.

### Procedural Animation: Tail, Wings, Hair, Tentacles! — Blackthornprod (14:14)
- **URL:** https://youtu.be/9hTnlp9_wX8
- **Core idea:** Wiggly appendages (tail, tentacles, wings, hair) made with Unity's LineRenderer: each point of the line smoothly follows the previous one, and a sine-driven "wiggler" object adds a wave.
- **Techniques:**
  - Follow-the-leader chain — NEW (animation, not generation) — an array of segment positions; point 0 sticks to the head and each next point moves toward the previous one with `SmoothDamp` (needs a velocity array and a smooth-time parameter: smaller means a stiffer follow).
  - Sine wiggle — NEW — a parent "wiggler" empty object oscillates with `Mathf.Sin(time * wiggleSpeed) * amplitude`; the chain targets it (wiggle speed about 10 was used).
  - Stylized look from LineRenderer width curve, sprite material (unlit transparent) and texture; the same rig is reused for wings and hair by changing points and textures.
- **Parameters / numbers:** segment count ("length"), smooth speed/time, wiggle speed (about 10), wiggle amplitude. Exact numbers beyond that not captured.
- **Determinism, seeds, chunks/seams:** not discussed; time-driven and runtime-only.
- **Islands status:** n/a for the generator.
- **Where it could land in Islands:** Fauna Lab (web): tails/tentacles/fins for animals such as the pudú tail or a generated creature, as a 2D chain with sine target (same math in p5.js). Game art: easy secondary motion for creatures in mundo-islas and the colonization game (sea creatures, serpent). Not for core.
- **Value for Islands:** low (generator) / medium for Fauna Lab. A cheap, understandable technique for secondary animation.

### Creating a Scene for my 3D Pixel Art Game — t3ssel8r (5:01)
- **URL:** https://youtu.be/ERA7-I5nPAU
- **Core idea:** Time-lapse of building a whole scene in a custom pixel-art rendering engine: model in Blender with primitives, import into Unity, paint terrain, and the engine places leaf/grass sprites in painted zones. A scene took under 2 hours.
- **Techniques:**
  - Primitive-based modeling: trees as cylinders with merged spheres; rocks as subdivided cubes; stairs by array modifier; duplicate one tree around with varied rotation and scale (0:30-1:30).
  - Procedural displacement + remesh in Blender to make rocks look like rock and reduce poly count, then the engine bakes the displacement into static geometry on import (about 2:00).
  - Unity terrain: paint height map; paint where leaf and grass sprites should be drawn (a painted mask of vegetation) (about 2:00-3:00).
  - Scatter of grass/leaf sprites from a painted mask — D.2-adjacent (scatter) — the video does not say how points are distributed.
- **Parameters / numbers:** about 2 h per scene. No other numbers.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** the "painted mask drives vegetation" idea matches Islands mask layers (`MapLayerId`) and vegetation adapters (partial; not checked in depth). Rendering engine: n/a.
- **Where it could land in Islands:** game art reference for the pixel-art look of mundo-islas (3D low-poly rendered as pixel art). A possible later adapter: Islands masks (forest, grass) to sprite-scatter on a 3D terrain. Hillside / Palmar web scenes could borrow "a few primitives, duplicate with rotation and scale" for tree variety.
- **Value for Islands:** low. Art direction reference only.

### Pixel Perfect Lights and Particles in Unity! — Unity (8:52)
- **URL:** https://youtu.be/2qeNu2QApAM
- **Core idea:** **Transcript unusable: the auto-captions were garbled (wrong language).** Card from title and description only. Shows how to use URP's 2D Renderer and 2D Lights with the pixel-perfect camera so lights and particles keep the pixel grid (Unity 2019.4).
- **Techniques:**
  - URP 2D Renderer + 2D Lights (point/freeform/sprite lights) — n/a — normal-mapped 2D lighting.
  - Pixel Perfect Camera component so lights/particles render at the sprite pixel resolution — n/a. Details not verified from the video.
- **Parameters / numbers:** not available (no transcript).
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a.
- **Where it could land in Islands:** adapter/visualization only: if the Tilemap adapter is used for a pixel-art demo, use the Pixel Perfect Camera plus 2D lights for torches in dungeons. Web labs: the pixel-art sprite preview could mimic this with a low-res render target scaled with nearest neighbour.
- **Value for Islands:** low. Visualization tip only; unverified details.

### Making 3D pixel art using picoCAD — Brandon James Greer (8:21)
- **URL:** https://youtu.be/dMX0SI5Av-8
- **Core idea:** picoCAD (a low-poly modeller on the PICO-8 console) lets you model a wireframe, apply small PNG textures to faces, and renders with a distinct chunky look. The video builds simple objects (a sphere approximated from an 8-sided cylinder, extrusions) and exports them to use as pixel art.
- **Techniques:**
  - Low-poly shapes from primitives: cylinder with 8 sides plus face extrude to make a rounded form (about 2:30).
  - Face-by-face texturing with tiny PNGs (hand-painted pixel texels), so the shading look comes from texture, not lighting.
  - Reference-based eyeballing of proportions from 2D artwork (about 5:00).
- **Parameters / numbers:** 8 faces around for a "sphere"; tiny textures. No other numbers.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a.
- **Where it could land in Islands:** game art pipeline for the colonization game (chunky 3D pixel look from simple models). Flora Lab: a possible 3D variant of the araucaria (low-poly trunk + texture-painted crown).
- **Value for Islands:** low. Art reference only.

### Architectural Designer Creates a Pixel Art Town — Pixel Architect (17:43)
- **URL:** https://youtu.be/KMJbhee4xTk
- **Core idea:** An architect designs the town of his pixel-art game (Chef RPG) with real urbanism principles: a market district with taller shops on the ground floor, a fish market with Asian-market inspiration, and attention to which spaces people gather in. Hand-made, not procedural.
- **Techniques:**
  - Zoning by district: taller buildings (2-3 storeys) with shops on the ground floor along the market street; wooden construction for fish stalls (about 8:00 onward).
  - Urban design rules: streets lined with shops and activities are lively; blank building faces or oversized plazas are dead; public spaces need containment/edges (about 5:00-6:00).
  - Fix for occlusion in oblique/pixel views: fading buildings to show the player is not great; he suggests designing so no "dead zone" hides behind tall objects (about 3:00).
- **Parameters / numbers:** buildings 2-3 storeys in the market; no other numeric rules captured.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a as an algorithm. Related existing item: village/settlement placement is not covered by this video. (Not checked in code.)
- **Where it could land in Islands:** these rules read as ready-made constraints for a future procedural settlement generator in mundo-islas or the colonization game: frontage rule (shops along streets), plaza size limit relative to surrounding buildings, avoid blank facades, no occluding tall objects in front of the player path. The Palmar / "Cómo nace una aldea" web article could cite them. Mark as ideas, not verified evidence.
- **Value for Islands:** medium. The only card here with design rules usable as scoring/constraint terms for a settlement layer (the video is a design opinion, not a tested algorithm).

### Make Low Poly Trees & Save the Planet — Grant Abbitt (16:29)
- **URL:** https://youtu.be/p-9pgZI3inI
- **Core idea:** Blender 2.8 tutorial: a low-poly tree built by hand (wobbly trunk, branches, icosphere foliage) as a contribution to the #teamtrees campaign.
- **Techniques:**
  - Trunk from a scaled/extruded shape with a "squiggle" and widened base via edge loops, to avoid a flat look (about 3:00).
  - Foliage as low-subdivision icospheres placed on branch tips (snap 3D cursor to a branch, add icosphere, adjust subdivisions) (about 8:00).
  - Variants by repeating the process; style kept deliberately angular.
- **Parameters / numbers:** icosphere subdivisions chosen per look (no fixed value given in the sampled text).
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a for generation. Related: Flora Lab generates trees procedurally (araucaria `jubaea.js`-style generators), more advanced than this manual method.
- **Where it could land in Islands:** a possible 3D variant of Flora Lab trees: trunk spline plus icosphere blobs on branch tips (the same "blob crown" idea as the Grumos sprite). Game art only.
- **Value for Islands:** low.

### 3D AI Model Generation is Getting GOOD - UE5.4 — Sorceress Gamedev (12:25)
- **URL:** https://youtu.be/RWl_FNP6mq8
- **Core idea:** Pipeline: generate concept art with AI (Leonardo AI real-time canvas, with sliders to keep a consistent style), turn the image into a textured 3D model with an image-to-3D AI service, import the FBX into Unreal Engine 5.4 and rig it with a procedural walk. Works for stylised characters, not for high-resolution realistic humans.
- **Techniques:**
  - Image-to-3D with a polygon target (10,000 by default) and a PBR 4K texture download (about 5:00-8:00).
  - Consistent style via Leonardo's real-time generation and style tweaking (about 2:00).
  - Procedural walk on the generated mesh in UE (not explained in the part sampled).
- **Parameters / numbers:** 10,000 polygons default; 4K PBR textures.
- **Determinism, seeds, chunks/seams:** not discussed; AI generation is not reproducible by seed in this workflow.
- **Islands status:** n/a. It also conflicts with Islands' invariants (determinism, no network) and with the project note that the LLM only designs and tests.
- **Where it could land in Islands:** game art prototyping only (placeholder creatures for the colonization game bestiary), outside the package.
- **Value for Islands:** low.

### Refactoring - When, How, and Why (Zelda in Unity) — Mister Taft Creates (20:50)
- **URL:** https://youtu.be/ZS7K2_NNZVw
- **Core idea:** Refactor a game's player movement script by splitting it by responsibility: anything that is not movement (health, inventory) gets its own component. Aim: general advice for structuring code, not only this project.
- **Techniques / architecture lessons:**
  - Single-responsibility components: ask for each field "is this movement?"; if not, move it out (player inventory to its own component) (about 3:00-6:00).
  - Optional capabilities as separate components (knockback component can be left off an object that should not be knocked back; a damage-dealing component per target type) (late in the video).
  - Refactor in small, planned steps (movement now, health next, then upgrade engine version).
- **Parameters / numbers:** none.
- **Determinism, seeds, chunks/seams:** not applicable.
- **Islands status:** architecture lesson only; Islands already separates concerns (grids, operators, layout stages, adapters last). Nothing to add.
- **Where it could land in Islands:** nowhere. A reminder to keep new pipeline stages as small single-purpose operators and optional adapters.
- **Value for Islands:** low.

#### Part summary
- Transcripts read for 9 of 11 videos (some only sampled in 2-4 slices because the Chrome tool truncates output); **no usable transcript:** `2qeNu2QApAM` (Pixel Perfect Lights; captions were garbled, wrong language, so card is from title/description). P4 (random walk) and P2 were read, but P3 (project setup) and the art-pipeline videos only by sampling.
- The Sunny Valley dungeon series (P2-P4) is almost certainly the origin of `SimpleRandomWalk2D`, `IteratedRandomWalk2D`, `CorridorFirstDungeon2D` and `RoomFirstBspDungeon2D` (same structure; no credit in the source). Islands already supersedes it: seeded Unity.Mathematics.Random, MaskGrid2D, brush radius, skew. Nothing missing from this set.
- Most useful idea: the Pixel Architect urbanism rules (frontage shops, no blank facades, plaza containment, no occluding tall objects) as constraint terms for a future settlement generator (mundo-islas, game, Palmar/aldea article). Medium value, but they are design opinion, not tested.
- Blackthornprod's follow-the-leader chain with SmoothDamp + sine wiggler is a cheap secondary-animation idea for Fauna Lab (tails, tentacles). No NEW generation techniques; "corridor-first" is the only item outside D.1-D.20 (closest D.14), and it is already implemented.
- The rest (picoCAD, t3ssel8r, URP 2D lights, low-poly trees, AI 3D models, refactoring) are n/a for the generator; game-art references only.

---

## Part 3

## Part 3 — Math / emergence videos

Method note: transcripts read from YouTube's panel via Claude in Chrome. Languages: Mandelbrot/Numberphile EN; 3B1B Holomorphic ES (auto-translated); 3B1B fractals ES; Veritasium logistic map AR (only Arabic track was reachable; numbers cross-checked, mechanism stated from it); Collatz EN; Hilbert hotel ES; Segerman EN; kavan EN auto. **No transcript:** ecSl5TzJZpo, IK7nBOLYzdE and P2uhhAXd7PI (music only, cards from the author's full description, which gives the rule tables), C2vgICfQawE, Udrt2otKmyA (description only).

Status baseline checked: `Runtime/PCG/Operators/MaskMorphologyOps2D.cs` and `Stage_Morphology2D.cs` exist (dilate/erode style ops); no birth/survive CA step, no fractal iteration, no box-counting and no ODE integrator found in `Runtime/PCG` or `Runtime/Noise` (grep). CURRENT_STATE.md l.920 lists "Fractal coastline / fjord refinement" as exploratory with open decisions. Web labs already have `scenes/mandelbrot.astro` and `scenes/julia.astro`.

### What's so special about the Mandelbrot Set? — Numberphile, Ben Sparks (16:53)
- **URL:** https://youtu.be/FFftmWSzgmk
- **Core idea:** Iteration as stable/unstable behaviour. Squaring on the real line splits at |x|=1; in the complex plane z->z^2+c gives Julia sets (c fixed, z0 varies) and, starting at 0 and varying c, the Mandelbrot set. Colouring by how fast it escapes gives the familiar images.
- **Techniques:**
  - Escape-time iteration z_{n+1}=z_n^2+c — NEW (not in D.1–D.20) — (14:00) proof: once |z|>2 it never returns; in practice iterate a cap (~200) and colour black if still inside radius 2, else colour by the iteration count at which it left.
  - Julia vs Mandelbrot as the same map in dynamic-plane vs parameter-plane (8:00, 15:15): Mandelbrot is a "map of Julia sets"; the Julia set at a point looks like the Mandelbrot neighbourhood there (seahorse valley, 15:40).
- **Parameters / numbers:** escape radius 2; cap ~200 iterations ("check 200 times", 14:20); smooth colour bands = iteration count; Mandelbrot 1979-80 on IBM printer; technicians "cleaned" the dust artefacts (10:30).
- **Determinism, seeds, chunks/seams:** deterministic; no seed. Not discussed beyond that.
- **Islands status:** missing (no escape-time code in Runtime). Web: exists (mandelbrot.astro, julia.astro).
- **Where it could land:** web only (already published). For Islands: `Runtime/Noise` sample mode "escape-time field" is cheap (iteration count as a scalar field, Burst-friendly, per-cell independent, chunk-safe) but it is a visual gimmick, not terrain. Conservative: NoiseLab optional demo of an iteration-count field as a mask source (threshold = a Mandelbrot-shaped island). Realistic (b) low priority; (c) already done.
- **Value for Islands:** low. Value for the web emergence/fractal series: medium as narrative source (printer-dust anecdote, the stability map).

### Beyond the Mandelbrot set, an intro to holomorphic dynamics — 3Blue1Brown (27:36)
- **URL:** https://youtu.be/LqbZpur38nw
- **Core idea:** Iterating complex functions: fixed points, attracting/repelling by |f'|, cycles, Fatou set (predictable) vs Julia set (chaotic boundary). Parameter space vs seed space. Mandelbrot shapes reappear in unrelated parameter spaces (Newton on cubics).
- **Techniques:**
  - Escape-time and parameter-space plots — NEW — (3:50–7:30) Mandelbrot fixes z0=0 and sweeps c; Newton/Julia fix the function and sweep z0. Colour black if bounded, gradient by divergence speed.
  - Fixed-point stability test |f'(z*)|<1 attracts, >1 repels, =0 superattracting (9:50–12:10).
  - Periodic cycles via f^n(z)=z; count grows 2^n for z^2+c (13:00–14:40).
  - Critical-point trick (Fatou): an attracting cycle must capture a critical point, so testing the single seed 0 (or the roots' mean for Newton cubics) is enough to find attracting behaviour (17:30–19:00, 18:30).
  - Julia set as chaotic boundary: small discs expand to cover the plane (Montel), explaining why boundaries are rough at any zoom (21:00–25:00).
  - Newton fractal as rational-map iteration (2:05).
- **Parameters / numbers:** escape radius 2 (5:00); Newton map x - p/p'; example cubic z^3-2z+2 with an attracting 2-cycle 0<->1 (15:35); parameter slice with fixed roots -1 and +1 and free third root lambda (19:20).
- **Determinism, seeds, chunks/seams:** deterministic; not discussed.
- **Islands status:** missing. Noise library has no complex-iteration fields.
- **Where it could land:** web (c): a page "Newton fractal and its parameter space" fits the fractal series; interactive sampling is a shader/p5 job. Islands: none directly. The one transferable idea is the "chaotic boundary vs stable interior" lens for the coastline/fjord question: Julia boundaries are geologically artificial (as CURRENT_STATE already notes), so it supports choosing ridged multifractal + erosion instead.
- **Value for Islands:** low. Value for web: medium.

### Los fractales no suelen ser autosemejantes — 3Blue1Brown (21:36)
- **URL:** https://youtu.be/gB9n2gHsHN4
- **Core idea:** Fractal dimension defined by how "mass" scales under rescaling; works for non-self-similar shapes via box counting. A fractal is a shape with non-integer dimension that stays that way across scales.
- **Techniques:**
  - Self-similarity dimension D = log(copies)/log(1/scale): Sierpinski log2(3)=1.585, Koch log3(4)=1.262, right-angle Koch log4(8)=1.5 (6:30–9:10).
  - Box-counting dimension — NEW — (10:30–15:00): cover with a grid, count cells touched at scale s, fit N(s)=k*s^D, take log-log regression; slope = D. Warns the local slope can change with zoom (15:40–17:30) and that applied fractals need a constant slope over a range of scales (17:50).
  - Measured values: coastline Britain ~1.21 (13:20, stays at 1000x zoom, 18:05), Norway ~1.52 (19:03), calm sea surface a bit over 2, stormy sea ~2.3 (19:12).
- **Parameters / numbers:** as above; disc cell count converges slowly to the 4x ratio (11:25).
- **Determinism, seeds, chunks/seams:** the measurement is deterministic given a fixed grid; grid alignment sensitivity not discussed.
- **Islands status:** missing (no box-counting metric). `Runtime/PCG` has mask grids and `MapLayerId` masks on which the box-count is trivial to compute.
- **Where it could land:** (a) A **roughness metric for coastlines**: compute the box-counting dimension of the Land/Coast mask at a few scales (2x2, 4x4, ... blocks), report slope. This turns the open "fractal coastline" decision into measurable targets (e.g. aim for ~1.2 like Britain, ~1.5 fjordy like Norway) and gives a determinism-safe test: tune fBm octaves/gain or ridged parameters so D lands in a range, assert in EditMode tests. That is the most realistic and useful idea in the whole batch. (c) Web Islands Lab page: "How rough is your coastline?" with live slider on octaves/lacunarity and a log-log plot; reuses existing noise pages. (b) NoiseLab sample: a read-out of D for the thresholded noise mask next to the histogram.
- **Value for Islands:** high (cheap, testable, directly informs the coastline backlog item and the web).

### This equation will change how you see the world (the logistic map) — Veritasium (18:39)
- **URL:** https://youtu.be/ovJcsL7vyrk
- **Core idea:** x_{n+1} = r x_n (1-x_n) with x in [0,1]. Fixed point for r in (1,3), period doubling cascade, chaos from r ~3.57, windows of order inside chaos, universal Feigenbaum ratio 4.669. Bifurcation diagram is a slice of the Mandelbrot set along the real axis.
- **Techniques:**
  - Logistic map iteration — NEW — (0:29–2:35) worked example r=2.6, x0=0.4 gives 0.624 then settles on 0.615. Initial condition irrelevant after a transient (2:56).
  - Bifurcation diagram (3:40–7:00): extinction r<1; stable fixed point 1<r<3; splits at r=3 into period 2, then 4, 8, 16, 32...; chaos from r=3.57; stable period-3 window at r=3.83 (then 6, 12, 24...).
  - Used historically as a pseudo-random generator (5:20): deterministic yet unpredictable.
  - Mandelbrot link: set z=0 start, c real; the bifurcation diagram appears as the side view of the Mandelbrot set; main cardioid = fixed point, main bulb = period 2, the "needle" = chaotic part, period-3 window = the small medallion (8:30–9:40).
  - Feigenbaum constant 4.669 from ratios of successive bifurcation widths; universal for any single-hump map such as sin (14:30–15:50).
- **Parameters / numbers:** r thresholds 1, 3, ~3.57, 3.83; delta=4.669; May's 1976 Nature paper (16:05). Experiments quoted: Libchaber mercury convection, flicker response of eyes and salamanders, rabbit heart fibrillation, dripping tap (10:45–14:10).
- **Determinism, seeds, chunks/seams:** perfectly deterministic but extremely sensitive to x0 in the chaotic regime; floating-point differences across platforms break reproducibility after ~tens of iterations in chaos. Not discussed in the video, but it matters for Islands' same-seed => same-output invariant (Burst vs managed math, FMA).
- **Islands status:** missing. Islands already has SmallXXHash for seeded randomness (better suited).
- **Where it could land:** (a) As a *parameter source* it is a poor fit: SmallXXHash is already a high-quality integer PRNG and the logistic map adds platform-sensitivity. A defensible use is **visual/didactic only**: a "chaos vs noise" comparison. (c) Web page for the emergence scene: bifurcation diagram interactive with r slider and cobweb plot, linked to the Mandelbrot scene already published ("the same diagram seen from the side") — this strongly matches Claudio's emergence interest (order from chaos, his 2023 song idea in perfil.md). Realistic and high value for the web. (b) NoiseLab: not recommended.
- **Value for Islands:** low (use hash). Value for web: high.

### The Simplest Math Problem No One Can Solve – Collatz — Veritasium (22:09)
- **URL:** https://youtu.be/094y1Z2wpJg
- **Core idea:** Rule: odd -> 3n+1, even -> n/2. Conjecture: all positive integers reach the 4-2-1 loop. Trajectories ("hailstones") behave like random walks with downward drift; unproven.
- **Techniques:**
  - Collatz map as a deterministic chaotic-looking integer sequence — NEW — (0:20–1:40); example 7 -> 22 -> 11 -> 34 ... -> 1; 27 climbs to 9,232 and needs 111 steps (2:51–3:18); 9,663 peaks at ~27 million (14:05).
  - Statistical argument (7:07–8:40): an odd step is effectively x3/2 but halving follows with probability; geometric mean per odd-to-odd step is 3/4, so sequences shrink on average. 341 -> 1024 -> ten halvings (8:45).
  - Log of the trajectory looks like geometric Brownian motion with downward trend (4:30–5:05).
  - Benford's law of leading digits in trajectories: ~30% start with 1, ~17.5% with 2, ~12% with 3, under 5% with 9 (5:10–6:15).
  - Directed-graph / tree visualisation (9:00) and the "coral": rotate by a fixed angle left for odd, right for even steps, produces organic branching shapes (9:34–10:00).
  - Verification facts: tested to 2^68 (10:34); any other cycle must exceed 186 billion elements (11:03); Terras 1976, Tao 2019 "almost all" results (11:56–13:20); Conway FRACTRAN, undecidability (18:55).
- **Parameters / numbers:** as listed; negative integers have three loops (e.g. around -5, -17) (14:48).
- **Determinism, seeds, chunks/seams:** pure integer arithmetic, bit-exact on any platform (a plus over logistic map). Overflow beyond 2^63 for large seeds is a practical gotcha not mentioned.
- **Islands status:** missing.
- **Where it could land:** (a) The Collatz "coral" (turtle-graphics with odd/even turn angles) is a small **L-system-like branching generator** (relates to D.17) seeded by an integer — plausible for flora/vegetation silhouettes in Flora Lab (`/labs/flora/`), where Claudio is building tree generators. It gives "organic" branches from an integer. Honest limitation: all trajectories merge into one tree; shapes vary only with the two angles, so variety is limited and the result is more art than botany. Also a hash-like integer scrambler is better done with SmallXXHash. (c) Web: medium; fits "emergence from simple rule" collection.
- **Value for Islands:** low. Web/Flora Lab: low-medium (curiosity).

### La paradoja del Hotel Infinito — TED-Ed, Jeff Dekofsky (6:00)
- **URL:** https://youtu.be/Uj3_KqkI9Zo
- **Core idea:** Hilbert's hotel: countable infinities can be re-packed by mapping rooms; n -> n+1, n -> 2n, and primes powers for infinite buses of buses.
- **Techniques:**
  - Index remapping / injective pairing of N x N into N via prime powers p_k^seat — n/a for generation — (2:54–3:55): bus k seat s goes to room p_k^s; no collisions, some rooms empty (room 6).
- **Parameters / numbers:** 2^7=128, 3^7=2187 examples.
- **Determinism, seeds, chunks/seams:** not discussed.
- **Islands status:** n/a as a technique. Conceptually adjacent to cantor/Morton interleaving, which is relevant to how a 2D chunk coordinate is mapped to a single integer key for hashing (an injective pairing). Not checked in code in this batch; only worth a note if chunk keys are ever combined carelessly (e.g. x+y), then collisions appear.
- **Where it could land:** nothing for Islands. A pedagogy note for a possible hash/chunk-key explanation on the web hash page.
- **Value for Islands:** low.

### Conway's game of life goes wrong. — Gamer Steve (0:32)
- **URL:** https://youtu.be/ecSl5TzJZpo
- **no transcript: from title/description only.** Description: "I don't think it's supposed to do that." A 32-second clip of a Game of Life simulation behaving unexpectedly (likely an implementation bug, e.g. in-place update or boundary error). I cannot state what exactly goes wrong.
- **Core idea (inferred):** common implementation bug in cellular automata.
- **Techniques:** Conway B3/S23 — D.12 (CA family) — the lesson, if this is the usual bug, is double-buffering: compute the next generation from a frozen copy, never update in place.
- **Islands status:** CA step missing in `Runtime/PCG` (only morphology ops found); D.12 not implemented as a stage.
- **Where it could land:** nothing beyond the D.12 notes below.
- **Value for Islands:** low.

### Cellular Automata: Rule 30 + Conway's Game of Life — Elliot Waite (3:26)
- **URL:** https://youtu.be/IK7nBOLYzdE
- **no transcript: music only; card from the author's description (rule table given there).**
- **Core idea:** A 1D automaton (Rule 30) scrolls upwards and feeds the bottom row of a 2D Game of Life, so 1D chaos continuously injects patterns into 2D.
- **Techniques:**
  - Elementary CA, Rule 30 — NEW (D.12 covers only 2D cave CA) — each cell reads (left, centre, right) from the previous row, alive=1: 111->0, 110->0, 101->0, 100->1, 011->1, 010->1, 001->1, 000->0 (the description's table). Start: a single alive cell in the middle. Rule 30 gives a pseudo-random centre column (historically used as an RNG).
  - Coupling a 1D CA as the source row of a 2D CA (Life B3/S23) — NEW.
  - Visual: dead cells fade through colours (cell "age since death").
- **Parameters / numbers:** 3-cell neighbourhood; 8-entry rule table = 8-bit rule number 30 (binary 00011110).
- **Determinism, seeds, chunks/seams:** deterministic from the initial row; but the row-by-row dependency means a chunk needs all previous rows (cone of dependence) so infinite worlds need a seam strategy.
- **Islands status:** missing (no CA step in Runtime/PCG; `MaskMorphologyOps2D` is the closest operator).
- **Where it could land:** (a) CA as a **mask operator** in `Runtime/PCG/Operators`: `MaskCaStepOps2D.Step(src, dst, birthMask, surviveMask)` (Moore, 9-bit masks) with double buffering, fixed iteration count, row-major order => bit-exact and Burst-friendly. Used for cave smoothing (D.12), coast cleanup, forest patches. Rule 30/elementary rows: only as a 1D seed generator or "stripe/edge noise"; low value. Rule 30 centre column as RNG is inferior to SmallXXHash. (b) NoiseLab sample "CA lab" with rule number slider (0–255) and B/S masks for Life-like rules (Cave rule B678/S345678 typical but not stated in the video; mark unverified) is realistic and fits the existing visual-sandbox purpose. (c) Web page on emergence: strongest fit; this is the clearest "simple rule -> complexity" demo for the pending emergence scene.
- **Value for Islands:** medium (the generic B/S CA operator is useful; the elementary-CA part is for the web).

### Cellular Automata: Rule 110 + Conway's Game of Life — Elliot Waite (4:01)
- **URL:** https://youtu.be/P2uhhAXd7PI
- **no transcript: music only; card from the author's description.**
- **Core idea:** Same setup as Rule 30 video, with Rule 110.
- **Techniques:** Rule 110 — NEW — table from description: 111->0, 110->1, 101->1, 100->0, 011->1, 010->1, 001->1, 000->0 (binary 01101110 = 110). Known Turing-complete (general fact, not in the description).
- **Parameters / numbers:** single alive starting cell; asymmetrical growth leftwards, creating gliders-like structures.
- **Determinism, seeds, chunks/seams:** as above.
- **Islands status:** missing.
- **Where it could land:** same as Rule 30: one shared elementary-CA function parameterised by rule byte (`(rule >> (4l+2c+r)) & 1`) serves both; trivial to implement on web and in a NoiseLab "CA lab".
- **Value for Islands:** low-medium (merge with Rule 30 card; one function).

### epic conway's game of life — Rational Animations (6:33)
- **URL:** https://youtu.be/C2vgICfQawE
- **no transcript: from title/description only.** Description says this is an old video, animated, and links the program used to run the Game of Life with the shown patterns preloaded (link not opened). Content (patterns: still lifes, oscillators, gliders, guns) is expected but I cannot verify what the video shows.
- **Techniques:** Game of Life B3/S23 — D.12 — missing as stage.
- **Where it could land:** web emergence scene reference (pattern gallery). Not Islands.
- **Value for Islands:** low.

### Lattice of particles in a double-well potential, subjected to noise — Nils Berglund (0:40)
- **URL:** https://youtu.be/Udrt2otKmyA
- **no transcript: from title/description only.** Description: discretized Allen-Cahn equation, a 32x32 lattice of harmonically coupled particles in a double-well potential, driven by noise.
- **Core idea:** Stochastic Allen-Cahn: each cell sits in one of two wells (+1 / -1), neighbours pull toward each other, noise kicks cells across the barrier; domains coarsen and interfaces move. A continuous cousin of Ising / majority-vote CA.
- **Techniques:** Stochastic Allen-Cahn lattice — NEW — update, per cell: dx = -(x^3 - x)*dt + k*(sum of neighbours - 4x)*dt + sigma*sqrt(dt)*noise (the standard form; the video description gives no formula, treat as general knowledge). Cell values in [-1,1] threshold at 0 -> binary mask with organic domain boundaries.
- **Determinism, seeds, chunks/seams:** deterministic if noise comes from a counter-based hash (SmallXXHash of cell, step, seed) rather than a stateful RNG; seam handling needs halo of N steps.
- **Islands status:** missing. Closest: iterated smoothing/majority filters in `MaskMorphologyOps2D` (partial analogue).
- **Where it could land:** (a) A **"phase-separation" mask operator** that produces blobby land/water or biome domains with tunable coarsening time — a physically grounded alternative to cave CA. Only a few dozen lines, headless, hashed noise. Uncertain gain over thresholded fBm; mark as exploratory. (b) NoiseLab mode "Allen-Cahn": slider for steps and noise, shows coarsening live; realistic. (c) Web emergence scene: very fitting (order from noise, coarsening, symmetry breaking).
- **Value for Islands:** medium-low (interesting, exploratory); web value high.

### Where do these circles come from? — Henry Segerman (4:09)
- **URL:** https://youtu.be/A2IAyXc0LuE
- **Core idea:** A sphere voxelised on a cubic lattice shows circular interference patterns when faces are coloured by facing direction (as on a Lego globe). Circles appear in coordinate directions, midway between axes, and along diagonals, as disruptions of the default tiling of face colours where the sphere curves away from a tangent plane. Moire appears as cubes get smaller than pixels.
- **Techniques:**
  - Voxel sphere centred on a cell centre, shaded per face direction (red/green/blue) — n/a for generation, relevant to rendering — (1:33). Implemented as a Shadertoy shader (0:36).
  - Contour-like "disruption" patterns of discretisation (2:18–3:00).
- **Parameters / numbers:** cube side inversely proportional to time (animation) (0:30); open question about ripple rates left unanswered (3:05).
- **Determinism, seeds, chunks/seams:** not discussed; Moire/aliasing from grid vs pixel grid (3:32).
- **Islands status:** n/a. Relevant caveat for Islands: rasterising circular features (SDF discs, island shapes) on a grid produces these aliasing structures; Islands is grid-first so the artefacts are inherent to circle masks, and visible mainly when stairstep faces are shaded by direction (e.g. autotiling or 3D voxel adapters).
- **Where it could land:** web (c) tiny explainer: "why does a voxel sphere show circles" — nice to pair with Islands shape masks. Not a technique.
- **Value for Islands:** low.

### Simulating Black Holes in C++ — kavan (12:28)
- **URL:** https://youtu.be/8-B6ryuBkCM
- **Core idea:** Ray-march light rays around a Schwarzschild black hole by integrating geodesic equations in polar coordinates; 2D first (CPU), then 3D on the GPU with a compute shader. Rays that fall under the Schwarzschild radius turn red; a disc object around the hole shows lensing.
- **Techniques:**
  - Schwarzschild radius r_s as event horizon, mass M (0:59).
  - Geodesic equations in the plane, second derivatives of r and phi (5:00–6:25): phi'' = -(2/r) r' phi'; r'' = -(c^2 r_s)/(2 r^2) + r phi'^2 (speaker's reading, simplified; the video's own rendering of the second term was garbled). Step: accelerations update velocities, velocities update position, then convert polar -> Cartesian.
  - Euler method fails (steps too sharp, too straight path) (7:36); fix: **Runge-Kutta 4 (RK4)**, four evaluations averaged by the RK4 formula per step (7:52) — NEW integrator pattern.
  - 3D scale: 800x600 = 480,000 rays per frame, 10–20k steps each, so CPU unusable; move to GPU compute shader (8:50–10:20); reduced to 400x300 for speed (11:13).
  - Camera with orbit controls shooting rays by FOV (9:45); simple disc object intersection (11:30).
- **Parameters / numbers:** c = 299,792,458 m/s; 20,000 RK4 steps per ray; 800x600 -> 400x300.
- **Determinism, seeds, chunks/seams:** not discussed; fixed-step integrators are deterministic on one machine, GPU vs CPU float differences would diverge.
- **What it adds over the hydrogen-orbital card** (`2026-10-31-hydrogen-cloud-sampling.md`, same author): hydrogen was about *sampling a density* (stateless, per-vertex, quantile); this one is *integrating an ODE per sample* (stateful, many steps, RK4). Different technique family. Shared lessons: author builds in C++/OpenGL, compute shader for parallel independent samples. For the mobile NoiseLab, black-hole ray marching with 10-20k steps per ray is not mobile-realistic.
- **Islands status:** missing: no ODE integrator. Not a world-gen technique.
- **Where it could land:** RK4 itself is a useful small tool *if* Islands ever does physical simulation (water flow, erosion D.11 with particles, wind/weather noted in CURRENT_STATE "Atmospheric / weather"), but Euler/semi-implicit is usually enough there. Web (c): a "bend the straight lines" 2D light-bending demo with RK4 vs Euler toggle is cheap, visual and fits Claudio's physics pages (warp bubble). Not a priority for Islands.
- **Value for Islands:** low. Web: medium (RK4 vs Euler comparison is a good small explainer).

#### Part summary
- **Highest value:** box-counting fractal dimension from the 3B1B video: a measurable roughness metric for coastlines (Britain ~1.21, Norway ~1.52) that turns the "fractal coastline / fjord refinement" backlog item into tunable, testable targets on the Land/Coast mask (EditMode test with range assertions). NEW technique, cheap, deterministic.
- **Generic CA mask operator** (Moore B/S masks, double-buffered, fixed steps) is the missing D.12 piece (only `MaskMorphologyOps2D` exists). Elementary Rule 30/110 are NEW but belong mostly to a NoiseLab "CA lab" and the web emergence scene; stochastic Allen-Cahn (Berglund) is an exploratory domain-forming operator (NEW).
- **Web, not Islands:** logistic-map bifurcation diagram linked to the already published Mandelbrot scene (best fit for the emergence page), CA playground with rule slider, Euler vs RK4 light bending (kavan). Logistic map and Rule 30 are poor Islands RNGs compared with SmallXXHash.
- **NEW techniques:** escape-time iteration, box-counting dimension, elementary CA (Rule 30/110), Allen-Cahn lattice, RK4 integrator, Collatz coral. Mandelbrot/Julia boundaries stay low value for terrain (artificial); the holomorphic video supports that reasoning.
- **Marked without transcript:** ecSl5TzJZpo, IK7nBOLYzdE and P2uhhAXd7PI (rule tables from author description), C2vgICfQawE, Udrt2otKmyA.
