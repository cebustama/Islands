# Island Map Generation — Contract‑Focused SSoT (v0.1.2)
**Status:** step catalog complete (15/15 Step classes documented); some supporting types still not included in this capture
**Last updated:** 2026-01-29  
**Goal:** document the **current Island Map Generation pipeline** (MonoBehaviour + Tilemaps) as a **single source of truth**: components, responsibilities, interactions, and the data contracts they rely on.

**Reference style:** aligned to the structure and “contract-first” tone of `Islands_PCG_Pipeline_SSoT_v0_1_10.md`, but scoped to this *tilemap-based* island generator (not the new Islands.PCG grid pipeline).

---

## 0) Scope

This SSoT governs the **Island Map Generation system** composed of:
- `MapGenerator` (orchestrator)
- `GenerationData` (shared state + settings)
- `NoiseDataSO` + `NoseGenerationHelper` (noise/masks)
- `GenerationUtils` + `DirectionsHelper` (grid utilities)
- `MapRendering` (Tilemap painting adapter)
- `TilesCollidersGenerator` + `HillColliderHelper` (TilemapCollider2D inputs)

It **does not** fully document:
- The `GenerationStep` implementations are now fully documented (15/15 Step classes).
- `RandomWeightedTile` / `RandomWeightedTileAssets` (not provided yet)
- Any gameplay systems that consume placed prefabs beyond `PlacedObjects` bookkeeping

---

## 1) Non‑negotiables (system contracts)

1) **Single authoritative state**
   - All procedural steps must read/write through a single `GenerationData` instance per generation run.

2) **Explicit phases**
   - Generation is a strict phase sequence:
     1) Seed/offset configuration  
     2) `GenerationData.ResetData()`  
     3) Steps (`GenerationStep.Execute`)  
     4) Render (paint Tilemaps)  
     5) Colliders (paint collider Tilemaps)  
     6) `OnFinishedGenerating` event

3) **Tilemap is an adapter**
   - Steps do **not** paint Tilemaps directly.
   - Steps write to `GenerationData` containers; `MapRendering` is the sole painter.

4) **Bounds safety**
   - Any `(x,y)` access to `BaseMapTiles/TreeTiles/FixTiles` must satisfy:
     - `0 <= x < MapWidth`
     - `0 <= y < MapHeight`

---

## 2) Architecture overview

### 2.1 Component graph (who talks to whom)

```
                 ┌──────────────────────────┐
                 │        MapGenerator       │
                 │ (orchestrates the run)    │
                 └───────────┬──────────────┘
                             │ uses
                             ▼
┌────────────────────────────┴────────────────────────────┐
│                    GenerationData (SSoT)                  │
│  Settings + mutable arrays/sets produced by Steps         │
└───────────────┬───────────────────────┬──────────────────┘
                │                       │
        written by Steps         consumed by adapters
                │                       │
                ▼                       ▼
      ┌─────────────────┐       ┌──────────────────┐
      │ GenerationStep*  │       │   MapRendering   │
      │ Execute(data)    │       │ PaintTiles(data) │
      └─────────────────┘       └──────────────────┘
                                        │
                                        ▼
                               (Unity Tilemaps)

After rendering:
┌──────────────────────────────┐
│   TilesCollidersGenerator    │
│ AddColliders(data)           │
└───────────┬──────────────────┘
            │ depends on
            ▼
    ┌───────────────────────┐
    │   HillColliderHelper  │
    │ sprite → collider tile │
    └───────────────────────┘
```

\* `GenerationStep` base + all concrete Steps are documented in Section 10 (15/15).

### 2.2 Runtime flow (single run)

Pseudocode equivalent to `MapGenerator.GenerateMap()`:

1. **Seed setup**
   - If `RandomizeOffset`:
     - Pick `MapGenerationSeed = Random.Range(0..1_000_000)`
     - Apply seed into each `NoiseDataSO.Offset = DefaultOffset + (seed, seed)`

2. **Seed the random tile assets**
   - For each `RandomWeightedTile` asset: `SetRandomSeed(MapGenerationSeed)`

3. **Reset state**
   - `GenerationData.ResetData()` allocates arrays and clears sets/lists

4. **Execute Steps**
   - For each `GenerationStep` in `m_generationSteps`: `step.Execute(generationData)`

5. **Render**
   - `MapRendering.PaintTiles(generationData)`

6. **Colliders**
   - `TilesCollidersGenerator.AddColliders(generationData)`

7. **Signal**
   - `OnFinishedGenerating?.Invoke()`

---

## 3) Core data model: `GenerationData`

### 3.1 Settings (authoritative inputs)
- `MapWidth`, `MapHeight`
- `MapGenerationSeed` (int)
- `RandomizeOffset` (bool)

**Contract:** `ResetData()` must be called before any Step writes into the data containers.

### 3.2 Primary tile layers (authoritative outputs)
All arrays are sized `[MapWidth, MapHeight]`:

- `BaseMapTiles : TileType[,]`
  - The *primary terrain* layer (water/sea/ground/hills/sand/deep sea/etc.)

- `TreeTiles : TileType[,]`
  - Trees live on a separate Tilemap layer (multiple tree tile types supported).

- `FixTiles : TileType[,]`
  - “Fixups” used to override or patch rule-tile artifacts without disturbing base rule tiles.

**Invariants (recommended):**
- All three arrays must remain index-aligned and share the same dimensions.
- `TileType.None` means “no tile on that layer”.

### 3.3 Position sets (derived outputs / constraints)
- `GrassPositions : HashSet<Vector2Int>`
  - Separate grass layer positions (painted onto `m_grassTilemap`).

- `FixSeaEdgesPositions : HashSet<Vector2Int>`
  - Positions to force to deep sea water in fix tilemap (rule-tile shoreline correction at map bounds).

- `HillLevel1Edge`, `HillLevel2Edge : HashSet<Vector2Int>`
  - Edge positions of each hill level (used for stairs avoidance and collider generation).

- `HillLevel1Interior`, `HillLevel2Interior : HashSet<Vector2Int>`
  - Interior positions (potential future use: special POIs, hill top objects).

- `HillStairPositions : HashSet<Vector2Int>`
  - Stairs occupy edge positions; those edge positions must **not** receive hill colliders.

- `PossiblePlacementPositions : HashSet<Vector2Int>`
  - Candidate walkable positions for prefabs / player / NPC placement.

### 3.4 Hill grouping (topology output)
- `MappedHillsLevel1`, `MappedHillsLevel2 : List<HashSet<Vector2Int>>`
  - Each entry represents a connected component of hill tiles at that level.

### 3.5 Prefab lifecycle bookkeeping
- `PlacedObjects : List<GameObject>`
  - `ResetData()` destroys and clears these objects.

**Contract:** Any Step that instantiates prefabs must register them into `PlacedObjects` so `ResetData()` can clean them up.

---

## 4) Tile vocabulary: `TileType`

`TileType` is the canonical semantic layer for generation and rendering.

**Contract:**
- Steps must communicate terrain intentions using `TileType` only.
- `MapRendering.GetTileFrom(TileType)` is the only mapping from `TileType` to `TileBase`.

Current values:

- Terrain: `Water`, `Sea`, `DeepSea`, `Ground`, `Sand`, `SandGrass`, `Grass`
- Hills: `HillLevel1`, `HillLevel2`
- Trees: `GreenTree`, `PalmTree`, `HillTree`
- Fix / special: `StairsUp`, `StairsDown`, `StairsLeft`, `StairsRight`
- `None` = no tile on a given layer

---

## 5) Orchestration: `MapGenerator`

### 5.1 Responsibility
- Owns the *one true* generation entrypoint: `GenerateMap()`.
- Runs all Steps in order.
- Triggers rendering and collider generation.
- Handles seed/offset setup for determinism / variability.

### 5.2 Inputs (serialized references)
- `m_generationData : GenerationData`
- `m_mapRendering : MapRendering`
- `m_mapColliderGenerator : TilesCollidersGenerator`
- `m_generationSteps : List<GenerationStep>` (**missing base class in provided files**)
- `m_weightedRandomTileAssets : List<RandomWeightedTile>` (**missing type**)
- `m_noiseDataToApplySeed : List<NoiseDataSO>`

### 5.3 Outputs/events
- `OnFinishedGenerating : UnityEvent` fired after render + colliders.

### 5.4 Determinism contract (current behavior)
- If `RandomizeOffset == false`:
  - Output depends on `MapGenerationSeed` (and any other parameters Steps might use).
- If `RandomizeOffset == true`:
  - Seed is randomized each run, and applied into each noise offset as:
    - `noise.Offset = noise.DefaultOffset + (seed, seed)`

**Note:** the Step implementations must avoid calling `UnityEngine.Random` directly (or must do so in a seed-controlled way), otherwise reproducibility will drift.

---

## 6) Rendering adapter: `MapRendering`

### 6.1 Responsibility
- Converts `GenerationData` outputs into **Tilemap state**.
- Clears and repaints:
  - `m_baseTilemap`
  - `m_treeTilemap`
  - `m_fixTilemap`
  - `m_grassTilemap`

### 6.2 Painting rules (contract)
- For each `(x,y)`:
  - Paint `BaseMapTiles[x,y]` into `m_baseTilemap`
  - If `TreeTiles[x,y] != None`: paint into `m_treeTilemap`
  - If `FixTiles[x,y] != None`: paint into `m_fixTilemap`

- Then paint sets:
  - `GrassPositions` → `m_grassTilemap` using `m_grassTile`
  - `FixSeaEdgesPositions` → `m_fixTilemap` using `m_deepSeaWaterTile`

### 6.3 Tile mapping (`GetTileFrom`)
- `GetTileFrom(TileType)` is the **only** supported mapping point.
- `TileType.None` maps to `null`.
- Unhandled `TileType` values throw `NotImplementedException`.

**Implication:** if Steps introduce new `TileType` values, `MapRendering` must be updated in lockstep.

### 6.4 Debug helper
- `GetBaseTilemapSpriteAt(worldPosition, generationData)`
  - Used to inspect which sprite got painted at a world point (useful when debugging RuleTile behavior).

---

## 7) Collider generation: `TilesCollidersGenerator` + `HillColliderHelper`

### 7.1 Goal
Generate colliders that:
- Block traversal over **Deep Sea**
- Block traversal over **Hill edges** (but allow stairs)

### 7.2 How colliders are produced (important)
This system relies on **TilemapCollider2D**:
- It does not directly create Collider2D components.
- Instead it **paints tiles** into “collider tilemaps” which are configured with TilemapCollider2D.

### 7.3 Inputs (required references)
- `m_colliderHillsTilemap`, `m_colliderDeepSeaTilemap`  
  (tilemaps that drive TilemapCollider2D)
- `m_baseTilemap`  
  (used to query sprite + transform matrix from RuleTile output)
- `m_mapRendering`  
  (used to retrieve the TileBase representing DeepSea for the collider tilemap)
- `m_hillColliderHelper`  
  (sprite → collider tile selection)

### 7.4 Deep Sea collider rule
If `BaseMapTiles[x,y] == DeepSea`:
- Paint the matching deep-sea TileBase into `m_colliderDeepSeaTilemap`.

### 7.5 Hill edge collider rule
If `BaseMapTiles[x,y]` is `HillLevel1` or `HillLevel2`:
- Let `pos = (x,y)`
- If `pos` is in `HillLevel1Edge` **or** `HillLevel2Edge`
- And `pos` is **not** in `HillStairPositions`
  - Then paint a hill-edge collider tile.

### 7.6 RuleTile transform parity (critical)
Because RuleTiles may **rotate/mirror** sprites, colliders must match visuals.

For hill colliders:
- `SetHillEdgeTileWithCorrectRotationMirroring(x,y)`:
  1) Read sprite from `m_baseTilemap.GetSprite(tilePos)`
  2) Read matrix from `m_baseTilemap.GetTransformMatrix(tilePos)`
  3) Convert sprite → collider TileBase via `HillColliderHelper.GetTileForSprite(sprite)`
  4) Paint collider tile into `m_colliderHillsTilemap`
  5) Copy transform matrix into `m_colliderHillsTilemap.SetTransformMatrix(tilePos, matrix)`

### 7.7 `HillColliderHelper` contract
- Maintains a list of `SpriteTileBasePair` mappings.
- Returns `m_defaultTile` if no explicit sprite mapping is found.

**Implication:** collider correctness depends on keeping the sprite list aligned with the RuleTile sprite set.

---

## 8) Noise + masks: `NoiseDataSO` + `NoseGenerationHelper`

### 8.1 Responsibility
- Provide a reusable, inspector-friendly configuration (`NoiseDataSO`)
- Generate:
  - multi-octave Perlin noise maps (`GeneratePerlinNoiseMap`)
  - circular masks (`GenerateCircularMask`) used for “island falloff”

### 8.2 `NoiseDataSO` contract
- `Scale`, `Octaves`, `Persistence`, `Lacunarity`, `Offset`, `DefaultOffset`

**Seed interaction:** `MapGenerator` may overwrite `Offset` each generation run (when `RandomizeOffset`).

### 8.3 Noise generation contract
`GeneratePerlinNoiseMap(width, height, noiseData)`:
- Produces `float[width,height]` in normalized range `[0..1]`.

`GenerateCircularMask(width, height, circleRadiusModifier01)`:
- Produces `float[width,height]` where values increase with distance from center.
- Typical usage: combine with noise so edges fall into water/sea.

**Naming note:** the helper class is currently named `NoseGenerationHelper` (typo). If renamed, update references and docs in lockstep.

---

## 9) Utility toolbox: `GenerationUtils` + `DirectionsHelper`

### 9.1 `DirectionsHelper`
Defines canonical direction sets:
- `DirectionOffsets4` (Von Neumann)
- `DirectionOffsets8` (Moore)

**Contract:** Steps should reuse these lists to avoid drift in neighbor semantics.

### 9.2 `GenerationUtils` responsibilities
Provides reusable algorithms over `TileType[,]` maps:

- **Edge extraction**
  - `GetEdgeTiles(typesToSearchFor, typesAdjacentTo, directions, mapTiles)`
  - Finds tiles of `typesToSearchFor` that are adjacent to any `typesAdjacentTo` tile.

- **Adjacency queries**
  - `IsAdjacentToTiles(x, y, tileTypes, directions, mapTiles)`

- **Edge expansion (thickening)**
  - `ExpandEdgeTiles(initialEdge, width, validTileTypes, mapTiles)`
  - Grows an edge outward by N layers using 8-direction adjacency.

- **Inverse edge detection**
  - `IsEdgeToTiles(x, y, nonEdgeTileTypes, directions, mapTiles)`
  - Treats a tile as an edge if it neighbors *any* tile not in `nonEdgeTileTypes`.

- **A* pathfinding**
  - `FindPathUsingAstar(start, destination, grid, obstacleTileTypes)`
  - Uses 4-direction movement, uniform cost.

**Contract (recommended):**
- These utilities must remain **pure** with respect to `GenerationData` (i.e., operate on passed-in arrays/sets and return results).
- Steps may build their own higher-level logic using th## 10) Generation Steps (complete catalog)

### 10.1 Step base contract (implemented)

#### `GenerationStep` (abstract, MonoBehaviour)
**Responsibility:** a single, stateful (but reset-per-run) pipeline stage that mutates `GenerationData`.  
**API:** `public abstract void Execute(GenerationData generationData);`

**Serialized fields**
- `m_description` (TextArea): human-readable step note for Inspector/debugging.

**Contract**
- **Reads/writes only `GenerationData`** (and its referenced ScriptableObjects), plus Unity services needed for math/noise.
- **Does not paint Tilemaps** (rendering is `MapRendering.PaintTiles`).
- **Does not create colliders** (colliders are `TilesCollidersGenerator.AddColliders`).
- Should be **deterministic** for a given `GenerationData.MapGenerationSeed` + NoiseData offsets + any weighted-random systems.

> NOTE: Some steps include `OnDrawGizmos*` debug visualizations. These must not affect generation state.

**Determinism pitfall:** any “random selection” from `HashSet<T>` (or iteration-order dependent logic) can break reproducibility even with a fixed seed. Prefer stable ordering (sorted Lists) for selections that must be deterministic.

### 10.2 Step catalog (all available steps)

#### 10.2.1 `BaseIslandMapGenerationStep`
**Responsibility:** generate the **base landmass mask** and the **base terrain** (Water/Ground), then tag ocean water as DeepSea.  
**Reads**
- `generationData.MapWidth`, `generationData.MapHeight`
- `m_noiseData` (terrain noise) when `m_applyBaseTerrain`
- `m_islandNoiseData` (additional island mask noise) when `m_applyAdditionalIslandMask`
**Writes**
- `generationData.BaseMapTiles[x,y]` → `Water` or `Ground`, then converts edge-connected `Water` → `DeepSea`
- Returns a float mask (`BaseIslandMask`) internally, used to shape the base terrain in `BaseIslandGeneration`

**Key parameters (serialized)**
- `m_applyBaseTerrain`: enable/disable base terrain noise.
- `m_applyIslandMask`: enable/disable island shaping (if false, `BaseIslandMask` returns null).
- `m_applyAdditionalIslandMask`: adds perlin detail to the circular island silhouette.
- Terrain thresholds: `m_waterThreshold`, `m_hillsThreshold` (note: hills threshold here clamps base noise; hills are painted by later steps).
- Island thresholds: `m_islandMaskThreshold`, `m_islandAdditionalMaskThreshold`, `m_islandSmoothStepFrom`, `m_islandSmoothStepTo`.
- Noise: `m_noiseData`, `m_islandNoiseData`.

**Algorithm sketch**
1) Build island mask:
   - Start with `GenerateCircularMask(width,height)` (0 center → 1 edge).
   - Apply `SmoothStep(from,to, circularMask)` to control “roundness/size”.
   - Optionally mix with an additional perlin noise mask (`m_islandNoiseData`) and threshold it.
   - Result is a **binary-ish** mask where **1 = land interior, 0 = ocean edge**.
2) Generate base terrain:
   - If mask is present: multiply base terrain noise by mask (land shrinks to island).
   - Clamp noise to `[0, m_hillsThreshold]`, then classify:
     - `< m_waterThreshold` → `Water`
     - else → `Ground`
3) Ocean tagging:
   - Flood-fill (4-neighbor) `Water` connected to `(0,0)` and convert those tiles to `DeepSea`.
   - Inland lakes remain `Water`.

**Preconditions**
- `generationData.BaseMapTiles` is allocated (typically via `generationData.ResetData()`).

**Postconditions**
- `BaseMapTiles` contains `{Ground, Water, DeepSea}` only.

**Ordering**
- Must run **first** (or at least before any step that expects `BaseMapTiles` to exist).

---

#### 10.2.2 `HillsGenerationStep`
**Responsibility:** paint `HillLevel1` and `HillLevel2` overlays using perlin noise, biased toward the island center.  
**Reads**
- `generationData.MapWidth`, `generationData.MapHeight`
- `m_hillsNoiseData`
**Writes**
- `generationData.BaseMapTiles[x,y]` → may overwrite existing `Ground/Water` with `HillLevel1` / `HillLevel2`

**Key parameters**
- `m_applyHills`
- `m_hillsLevel1AmountModifier`, `m_hillsLevel2AmountModifier` (higher = more hills)
- `m_hillsCircularMaskModifier` (bias hills toward center)
- `m_hillsNoiseData`

**Algorithm sketch**
1) Generate hills noise (`GeneratePerlinNoiseMap`) and a circular mask (`GenerateCircularMask(width,height, modifier)`).
2) For each tile: `tempHill = hillsNoise[x,y] * (1 - circularMask[x,y])`
3) Threshold using “one minus” modifiers:
   - if `tempHill > (1 - level1Modifier)` → set `HillLevel1`
   - if `tempHill > (1 - level2Modifier)` → set `HillLevel2` (overrides level 1)

**Preconditions**
- Base terrain already generated (otherwise hills will be painted onto default/empty tiles).

**Postconditions**
- `BaseMapTiles` may now include hill types.

**Ordering**
- Typically runs after `BaseIslandMapGenerationStep`, before hill analysis steps.

---

#### 10.2.3 `HillEdgeInteriorDetectionStep`
**Responsibility:** classify hill tiles into **edge** and **interior** sets for both hill levels.  
**Reads**
- `generationData.BaseMapTiles`
- `GenerationUtils.IsEdgeToTiles(...)` with `DirectionsHelper.DirectionOffsets8`
**Writes**
- `generationData.HillLevel1Edge`, `generationData.HillLevel2Edge`
- `generationData.HillLevel1Interior`, `generationData.HillLevel2Interior`

**Classification rules (as implemented)**
- Level 1 tile (`HillLevel1`) is **edge** if it borders (8-neighbor) any tile **not** in `{HillLevel1, HillLevel2}`.
- Level 2 tile (`HillLevel2`) is **edge** if it borders any tile **not** in `{HillLevel2}` (so borders with level 1 count as “edge” for level 2).
- After initial classification:
  - `HillLevel1Interior` removes (`ExceptWith`) `HillLevel2Interior` and `HillLevel2Edge` so level-2 areas don’t remain in level-1 interior.

**Preconditions**
- Hills have already been painted in `BaseMapTiles`.

**Postconditions**
- Hill edge/interior sets are populated and consistent across hill levels.

**Ordering**
- Must run before any step that needs hill topology (stairs, colliders, placement rules, hill grouping).

---

#### 10.2.4 `MappingSeparateHillsStep`
**Responsibility:** group hill interior tiles into **connected components** (“separate hills”).  
**Reads**
- `generationData.HillLevel1Interior`, `generationData.HillLevel2Interior`
- `DirectionsHelper.DirectionOffsets8` (8-connected grouping)
**Writes**
- `generationData.MappedHillsLevel1`, `generationData.MappedHillsLevel2`

**Algorithm sketch**
- BFS flood-fill over the supplied interior set; each connected component becomes a `HashSet<Vector2Int>`.
- Produces `List<HashSet<Vector2Int>>` per hill level.

**Debug hooks**
- Stores references in `m_hillsLevel1 / m_hillsLevel2` and draws each component with a distinct Gizmos color in `OnDrawGizmosSelected()` (Play Mode).

**Preconditions**
- `HillEdgeInteriorDetectionStep` has populated the interior sets.

**Postconditions**
- Per-level hill component lists are available for later logic (e.g., pick a “main hill”, spawn POIs, etc.).

**Ordering**
- After hill edge/interior detection; before any system that wants “per-hill” semantics.

---

#### 10.2.5 `ShoreLineGenerationStep`
**Responsibility:** compute a **shoreline band** and apply **land/shore transition** tiles required for RuleTiles (currently: `SandGrass` intersection).  
**Reads**
- `generationData.BaseMapTiles`
- `generationData.MapWidth`, `generationData.MapHeight`
- `m_shoreWidth`
- `m_tilesToSearchFor` (default: `Ground`, `HillLevel1`, `Water`)
- `DirectionsHelper.DirectionOffsets8` (neighbor scan)

**Writes**
- `generationData.BaseMapTiles[x,y]` for the computed **intersection** tiles → `SandGrass`
- Debug: stores `m_gizmoShoreline` for Gizmos (no gameplay impact)

**Key parameters (serialized)**
- `m_createShoreline`: enable/disable the step.
- `m_shoreWidth`: expansion width used by `ExpandShoreline`.
- `m_tilesToSearchFor`: which base tile types are considered “land-ish” when computing the shoreline bands.

**Algorithm sketch**
1) Compute initial edge tiles (`GetEdgeTiles`) using 8-neighborhood over `BaseMapTiles`.
2) Expand edge tiles (`ExpandEdgeTiles`) to produce:
   - a near-edge **shore** set (width = 1)
   - an expanded **intersection** set (width = `m_shoreWidth`)
3) Apply `SandGrass` to the intersection set to help RuleTile transitions.

**Preconditions**
- `BaseMapTiles` is allocated and already contains at least a landmass + surrounding water (`BaseIslandMapGenerationStep`).

**Postconditions**
- A band of tiles near the coast may be overwritten to `SandGrass`.

**Ordering**
- After base landmass generation.
- Before `ShallowSeaGenerationStep` (which searches for `Sand`/`SandGrass`).
- If hills are part of your “coastline silhouette”, run after `HillsGenerationStep` (it includes `HillLevel1` in its search list).

**Implementation note**
- The class-level summary mentions “sand tiles”, but the current implementation **only writes `SandGrass`**. If you want an explicit `Sand` band, add a second pass that assigns `Sand` to the shoreline set.

---

#### 10.2.6 `ShallowSeaGenerationStep`
**Responsibility:** create a **shallow sea band** by converting a configurable width of `DeepSea` tiles adjacent to the shore into `Sea`.  
**Reads**
- `generationData.BaseMapTiles`
- `generationData.MapWidth`, `generationData.MapHeight`
- `m_shallowSeaWidth`
- `m_tilesToSearchFor` (default: `Sand`, `SandGrass`)
- `DirectionsHelper.DirectionOffsets4` (neighbor scan)

**Writes**
- `generationData.BaseMapTiles[x,y]` for the expanded band → `Sea`

**Key parameters (serialized)**
- `m_generateShallowSea`: enable/disable the step.
- `m_shallowSeaWidth`: how far to expand into `DeepSea`.
- `m_tilesToSearchFor`: which tiles constitute “shore” for the expansion seed.

**Algorithm sketch**
1) Find `DeepSea` edge tiles adjacent to any `m_tilesToSearchFor` tile (4-neighborhood).
2) Expand the edge tiles outward into `DeepSea` up to `m_shallowSeaWidth`.
3) Set the resulting positions to `Sea`.

**Preconditions**
- `BaseMapTiles` already contains `DeepSea` (tagged ocean) and shore markers (`SandGrass` and/or `Sand`).

**Postconditions**
- A band of `DeepSea` becomes `Sea`.

**Ordering**
- After `ShoreLineGenerationStep`.
- Before any steps that assume the final coastal water classification (rendering/colliders).

**Validation hooks**
- All `Sea` tiles should previously have been `DeepSea` (no sea “inside” the island).
- `Sea` tiles should form a contiguous ring around the coastline in typical maps.

---

#### 10.2.7 `EdgeFlatteningGenerationStep`
**Responsibility:** “flatten”/smooth a chosen tile type by replacing it when it has enough neighbors of specified types.  
**Reads**
- `generationData.BaseMapTiles`
- `generationData.MapWidth`, `generationData.MapHeight`
- `DirectionsHelper.DirectionOffsets8` (8-neighbor sampling)
**Writes**
- `generationData.BaseMapTiles[pos] = m_tileToSet` for positions that match the rule

**Key parameters**
- `m_applyStep`
- `m_neighborsLimit` (replace when neighbor count ≥ limit)
- `m_tileToFlatten`
- `m_neighborTilesToDetect` (TileTypes counted as “neighbors”)
- `m_tileToSet` (replacement tile)

**Algorithm sketch**
1) Scan all tiles; when tile == `m_tileToFlatten`, count 8-neighbors that are in `m_neighborTilesToDetect`.
2) If count ≥ `m_neighborsLimit`, mark position.
3) After scan, apply replacements.

**Preconditions**
- `BaseMapTiles` already meaningful (base terrain/hills/etc).

**Postconditions**
- Reduced jaggies / “islands of tile type” depending on configuration.

**Ordering**
- Can be applied multiple times at different pipeline points (e.g., after hills, after sand, etc.).

---

#### 10.2.8 `GrassGenerationStep`
**Responsibility:** generate grass overlay positions using perlin noise, excluding hill edges.  
**Reads**
- `generationData.BaseMapTiles`
- `generationData.HillLevel1Edge`, `generationData.HillLevel2Edge`
- `m_grassNoiseData`
**Writes**
- `generationData.GrassPositions` (adds positions)

**Key parameters**
- `m_applyGrass`
- `m_grassThreshold` (“higher threshold = less grass”)
- `m_grassNoiseData`

**Placement rules (as implemented)**
- Candidate tile types: `Ground` or `HillLevel1` only.
- Noise test: `grassNoise[x,y] > (1 - m_grassThreshold)`
- Exclusion: positions in `HillLevel1Edge` or `HillLevel2Edge` are rejected.

**Preconditions**
- Base terrain exists.
- Hill edge sets exist (otherwise “edge exclusion” is ineffective).

**Postconditions**
- `GrassPositions` contains only walkable-ish tiles (ground/level1 hill) excluding hill edges.

**Ordering**
- After hill edge detection; before any “fix vegetation on edges” step.

---

#### 10.2.9 `TreeGenerationStep`
**Responsibility:** populate the **tree overlay** (`TreeTiles`) using noise, placing different tree types on different terrain bands (ground/hills vs sand).  
**Reads**
- `generationData.BaseMapTiles`
- `generationData.HillLevel1Edge`, `generationData.HillLevel2Edge`
- `generationData.HillLevel1Interior` (to decide `HillTree` vs `GreenTree`)
- `generationData.MapWidth`, `generationData.MapHeight`
- Noise: `m_treeNoiseData`, `m_palmTreeData`

**Writes**
- `generationData.TreeTiles[x,y]` → `GreenTree`, `HillTree`, `PalmTree` (or `None`)

**Key parameters (serialized)**
- `m_applyTrees`: enable/disable the step.
- `m_treeAmount`: density threshold for ground/hill trees.
- `m_palmTreeAmount`: density threshold for palm trees.
- `m_treeNoiseData`, `m_palmTreeData`: noise configs.

**Algorithm sketch**
1) Generate two noise maps: one for regular trees, one for palms.
2) For each tile:
   - If `treeMap` passes threshold AND base is `Ground` or `HillLevel1` AND not a hill-edge tile:
     - If position is in `HillLevel1Interior` → `HillTree`
     - Else → `GreenTree`
   - If `palmTreeMap` passes threshold AND base is `Sand` or `SandGrass` → `PalmTree`

**Preconditions**
- `BaseMapTiles` and `TreeTiles` are allocated.
- Hill edge/interior sets exist if you want edge avoidance + hill tree classification.

**Postconditions**
- `TreeTiles` contains only overlay tiles; it does not mutate `BaseMapTiles`.

**Ordering**
- After hill edge/interior detection.
- After shoreline generation (so `Sand`/`SandGrass` exists for palms).
- Before `MapObjectPlacementStep` (so tree exclusions are correct).
- If you use `PathFromHillsGenerationStep` to clear trees along paths, run this step **before** that path step.

**Validation hooks**
- No trees on hill edge positions.
- Palm trees only on sand bands.

---

#### 10.2.10 `PathFromHillsGenerationStep`
**Responsibility:** carve **access paths** from hill regions toward the map edges and place **stairs** where paths cross hill edges (also clears trees along those paths).  
**Reads**
- `generationData.MappedHillsLevel1`, `generationData.MappedHillsLevel2`
- `generationData.HillLevel1Edge`, `generationData.HillLevel2Edge`
- `generationData.BaseMapTiles`, `generationData.TreeTiles`, `generationData.FixTiles`
- `generationData.MapWidth`, `generationData.MapHeight`
- `generationData.MapGenerationSeed`

**Writes**
- `generationData.BaseMapTiles[x,y]` along paths → substitutes (currently: `Ground` and/or `HillLevel1`)
- `generationData.FixTiles[x,y]` at stair locations → `StairsUp/Down/Left/Right`
- `generationData.HillStairPositions` (adds stair positions)
- `generationData.TreeTiles[x,y] = None` for every position on the path
- Debug: keeps `m_hillsLevel1Paths` / `m_hillsLevel2Paths` for Gizmos

**Hard-coded configuration (current code)**
- Level 1 paths: obstacles = `{ Water, HillLevel2 }`, substitutes = `{ Ground }`
- Level 2 paths: obstacles = `{ Water }`, substitutes = `{ Ground, HillLevel1 }`

**Algorithm sketch**
1) Build a list of the 4 map corners (destinations).
2) Seed `System.Random` with `MapGenerationSeed`.
3) For each mapped hill region:
   - Pick a random destination corner.
   - Pick a start tile from the hill region.
   - Compute an A* path from start→destination avoiding `obstacleTileTypes`.
   - Walk the path:
     - If the position lies on a hill edge (one of the provided edge sets), place a stair tile in `FixTiles` and record the position in `HillStairPositions`.
     - Otherwise, rewrite `BaseMapTiles` using the current substitute tile (intended to “cut” a ramp).
     - Clear `TreeTiles` on every path tile.

**Preconditions**
- Hill regions are mapped (`MappingSeparateHillsStep`) and edge sets exist (`HillEdgeInteriorDetectionStep`).
- `FixTiles` is allocated (typically in `GenerationData.InitializeOrReset` or similar).

**Postconditions**
- Stairs exist on hill edges and paths are cleared of trees.
- `FixTiles` contains the authoritative stair tile for rendering.

**Ordering**
- After hill mapping + edge detection.
- After `TreeGenerationStep` if you want trees to be cleared on the carved paths.
- Before `MapObjectPlacementStep` (so placement set respects the carved/cleared terrain) and before collider generation (stairs affect hill colliders).

**Determinism notes (important)**
- The current start-tile selection iterates a `HashSet<Vector2Int>` hill region and takes the “first” element. `HashSet` iteration order is **not guaranteed stable**, so this can break reproducibility even with the same seed.
  - Contract recommendation: choose a deterministic start (e.g., `min(x,y)`), or convert to a sorted list and then select via seeded RNG.

**Navigation notes (important)**
- The obstacle lists do not currently include `Sea` / `DeepSea`. If those tiles are non-walkable in your game, add them to `obstacleTileTypes` (or handle them inside the A* neighbor filter) to prevent paths “escaping” into the ocean.

**Validation hooks**
- If hills exist, `HillStairPositions.Count > 0`.
- Every `HillStairPositions` entry should also be contained in `HillLevel1Edge` and/or `HillLevel2Edge`.
- No `TreeTiles` remain on any path tile.

---

#### 10.2.11 `FixHillEdgePlacementStep`
**Responsibility:** remove grass and trees from hill edge tiles to keep edges clean (stairs/colliders/readability).  
**Reads**
- `generationData.HillLevel1Edge`, `generationData.HillLevel2Edge`
**Writes**
- `generationData.GrassPositions.Remove(pos)` for hill edge positions
- `generationData.TreeTiles[pos] = TileType.None` for hill edge positions

**Key parameters**
- `m_applyFix`

**Preconditions**
- Hill edge sets exist.
- Grass/trees have already been generated (otherwise the step is mostly a no-op).

**Postconditions**
- No grass or tree tiles remain on hill edges.

**Ordering**
- After vegetation generation; before collider generation and rendering.

---

#### 10.2.12 `MapObjectPlacementStep`
**Responsibility:** compute a conservative set of positions where **prefabs/actors could be placed**.  
**Reads**
- `generationData.BaseMapTiles`
- `generationData.TreeTiles` (used as an exclusion mask)
- `generationData.HillLevel1Interior`, `generationData.HillLevel2Interior`
**Writes**
- `generationData.PossiblePlacementPositions`

**Key parameters**
- `m_tileTypesOpenForPlacement` (base tile types that are allowed)

**Algorithm sketch**
1) Scan all tiles:
   - If `BaseMapTiles[x,y]` is in `m_tileTypesOpenForPlacement`, add position.
   - Track any positions where `TreeTiles[x,y] != None`.
2) Union in both hill interior sets (explicitly allows hill interiors even if not in the open list).
3) Remove (`ExceptWith`) all tree positions.

**Preconditions**
- `BaseMapTiles` and `TreeTiles` are allocated.
- Hill interior sets exist (if you want hill interiors included).

**Postconditions**
- `PossiblePlacementPositions` excludes trees, but does **not** currently exclude grass, stairs, or other future blockers.

**Ordering**
- After tree generation (so exclusions are accurate); after hill interior detection (if including hill interiors).

---

#### 10.2.13 `PlayerPlacementStep`
**Responsibility:** place the **player prefab** on a deterministic pseudo-random valid tile.  
**Reads**
- `generationData.PossiblePlacementPositions`
- `generationData.MapGenerationSeed`
- `m_playerPrefab`

**Writes**
- Instantiates `m_playerPrefab` in the scene.
- Adds the instance to `generationData.PlacedObjects`.

**Key parameters (serialized)**
- `m_applyStep`: enable/disable the step.
- `m_playerPrefab`: prefab to instantiate.

**Algorithm sketch**
1) Seed `System.Random` with `MapGenerationSeed`.
2) Choose `randomIndex` in `[0, PossiblePlacementPositions.Count)`.
3) Iterate the `HashSet` until `index == randomIndex`, then instantiate at that tile center.

**Preconditions**
- `PossiblePlacementPositions` has been computed (`MapObjectPlacementStep`).
- `m_playerPrefab` is assigned.

**Postconditions**
- Exactly one player instance is placed (when enabled) and tracked in `PlacedObjects`.

**Ordering**
- After `MapObjectPlacementStep` (and after any subsequent step that may change placement blockers).
- Before any “cleanup/reset” logic that destroys `PlacedObjects`.

**Determinism note**
- Like the hill path step, this uses a `HashSet` enumeration. Even with the same seed, different `HashSet` iteration orders can change which tile corresponds to `randomIndex`.
  - Contract recommendation: choose from a **sorted List** (e.g., `PossiblePlacementPositions.OrderBy(p => (p.y, p.x))`) for stable results.

---

#### 10.2.14 `FixMapEdgesStep`
**Responsibility:** mark boundary tiles so rendering can force them to `DeepSea` (prevents RuleTile shoreline artifacts).  
**Reads**
- `generationData.MapWidth`, `generationData.MapHeight`
**Writes**
- `generationData.FixSeaEdgesPositions` (adds all boundary positions)

**Key parameters**
- `m_applyStep`

**Implementation note**
- The second boundary loop currently uses `generationData.MapHeight - 1` where `MapWidth - 1` is expected for the x-max edge.
  - If your map is square this is harmless; if rectangular, it will mark tiles beyond the intended width.

**Preconditions**
- None (can run anytime after `ResetData`).

**Postconditions**
- `FixSeaEdgesPositions` contains a ring of edge coordinates.

**Ordering**
- Typically near the end (as a “final fixup”), before `MapRendering.PaintTiles(...)`.

---

### 10.3 Recommended ordering for the current step set

This pipeline is Inspector-driven (`MapGenerator.m_generationSteps`). The list below is the **recommended default order** that satisfies current read/write dependencies and minimizes “re-do” work:

1) `BaseIslandMapGenerationStep`  
2) `HillsGenerationStep`  
3) `HillEdgeInteriorDetectionStep`  
4) `MappingSeparateHillsStep`  
5) `ShoreLineGenerationStep`  
6) `ShallowSeaGenerationStep`  
7) `EdgeFlatteningGenerationStep` *(optional / placement depends on what you flatten; see note below)*  
8) `GrassGenerationStep`  
9) `TreeGenerationStep`  
10) `PathFromHillsGenerationStep`  
11) `FixHillEdgePlacementStep`  
12) `MapObjectPlacementStep`  
13) `PlayerPlacementStep`  
14) `FixMapEdgesStep`

**Hard dependency rules**
- Anything that reads `BaseMapTiles` must run after `BaseIslandMapGenerationStep`.
- `HillEdgeInteriorDetectionStep` must run after hills are painted.
- `MappingSeparateHillsStep` depends on interior sets (`HillLevel*Interior`).
- `ShallowSeaGenerationStep` depends on shoreline markers (`Sand`/`SandGrass`).
- `TreeGenerationStep` depends on hill edge sets (avoid edges) and shoreline tiles (palms).
- `PathFromHillsGenerationStep` depends on mapped hills + edge sets; run after trees if it should clear them.
- `MapObjectPlacementStep` depends on `TreeTiles` (to exclude trees) and hill interiors (to include them).
- `PlayerPlacementStep` depends on `PossiblePlacementPositions`.

**EdgeFlattening placement note**
- If you use it to remove tiny “water holes” inside land (flatten `Water` → `Ground`), run it **early** (right after base generation) so shoreline/sea/vegetation don’t form around artifacts.
- If you use it to smooth coastal water classification, run it **after** shoreline + shallow sea.

**Post-process ordering note**
- Collider generation must happen after all steps that change walkability/edges (`PathFromHillsGenerationStep`, `FixMapEdgesStep`).

---


## 11) Validation checklist (current system)

### 11.1 Global invariants (every run)
1) **Array sizes match**
   - `BaseMapTiles`, `TreeTiles`, `FixTiles` are non-null and sized `[MapWidth, MapHeight]`.

2) **Set/list reset correctness**
   - `GrassPositions`, `FixSeaEdgesPositions`, hill sets, `HillStairPositions`, and `PossiblePlacementPositions` are cleared before regeneration (or rebuilt from scratch).
   - Multiple calls to `GenerateMap()` do not leak prefabs; `PlacedObjects` are destroyed and cleared.

3) **Rendering parity**
   - `MapRendering.PaintTiles()` clears and repaints all tilemaps.
   - No out-of-range exceptions while painting.

4) **Collider parity**
   - `TilesCollidersGenerator.AddColliders()` completes without exceptions.
   - Deep sea colliders exist wherever `BaseMapTiles == DeepSea`.
   - Hill edge colliders exist on `HillLevel*Edge` **except** where `HillStairPositions` exist.

### 11.2 Step-level sanity checks (tightened)
- **After `BaseIslandMapGenerationStep`**
  - At least one `Ground` tile exists.
  - `DeepSea` exists (unless intentionally disabled), and forms the outer ocean.

- **After `ShoreLineGenerationStep`**
  - `SandGrass` tiles exist near the coast (typical maps).
  - No `SandGrass` is written into clearly-ocean tiles unless that is intentional for your RuleTile setup.

- **After `ShallowSeaGenerationStep`**
  - `Sea` tiles exist and are only found where there used to be `DeepSea`.

- **After `HillsGenerationStep`**
  - Hill tiles exist (unless hill threshold makes them empty).
  - (Optional) hills should not appear in the ocean; if they do, clamp hills to land tiles.

- **After `HillEdgeInteriorDetectionStep`**
  - `HillLevel1Edge ∩ HillLevel1Interior == ∅` and similarly for level 2.
  - Every hill interior tile is adjacent to only hill tiles (no water).

- **After `MappingSeparateHillsStep`**
  - Each `MappedHillsLevel*` entry is connected (4-neighborhood).
  - Total union of mapped sets equals the corresponding interior set.

- **After `GrassGenerationStep`**
  - Every grass position satisfies:
    - Base tile is `Ground` or `HillLevel1`
    - Not on hill edge sets

- **After `TreeGenerationStep`**
  - No trees on hill edge positions.
  - Palm trees only on `Sand` / `SandGrass`.

- **After `PathFromHillsGenerationStep`**
  - If any mapped hills exist, `HillStairPositions.Count > 0`.
  - All stair tiles in `FixTiles` are in `HillStairPositions`.
  - All `HillStairPositions` are on hill edges.
  - No trees remain on carved path tiles.

- **After `FixHillEdgePlacementStep`**
  - No grass positions on hill edge tiles.
  - No trees on hill edge tiles.

- **After `MapObjectPlacementStep`**
  - `PossiblePlacementPositions` contains no tile where `TreeTiles != None`.
  - (Optional) If you add more blockers later (grass, stairs, props), extend this exclusion contract.

- **After `PlayerPlacementStep`**
  - Exactly one player instance exists (when enabled) and is tracked in `PlacedObjects`.

- **After `FixMapEdgesStep`**
  - `FixSeaEdgesPositions` contains all boundary positions.
  - **Implementation warning:** the current code uses `generationData.MapHeight - 1` as the max X boundary; this likely should be `MapWidth - 1`.

### 11.3 Determinism gate (recommended)
- For a fixed `MapGenerationSeed` and fixed `NoiseDataSO` offsets:
  - `BaseMapTiles` should be identical between runs.
  - Avoid iteration-order-dependent selections from `HashSet<T>` (notably in `PathFromHillsGenerationStep` and `PlayerPlacementStep`); use sorted Lists for stable behavior.

---


## 12) Known gaps / next updates

- Add the **full Steps section** once Step files are attached (most important missing piece).
- Document the missing types:
  - `GenerationStep`
  - `RandomWeightedTile`
  - Any “tile catalogs” / RuleTile assets assumptions
- Clarify intended semantics for:
  - `Persistence` and `Lacunarity` usage in multi-octave noise (current defaults are unconventional; may be intentional).
- Add explicit invariants for:
  - what counts as “walkable” in `PossiblePlacementPositions`
  - allowed overlaps between layers (e.g., grass over sand grass, trees over hill, etc.)

---

## Appendix A — File index (v0.1.0 inputs)

- `MapGenerator.cs`
- `GenerationData.cs`
- `MapRendering.cs`
- `TilesCollidersGenerator.cs`
- `HillColliderHelper.cs`
- `NoiseDataSO.cs`
- `NoseGenerationHelper.cs`
- `GenerationUtils.cs`
- `DirectionsHelper.cs`
