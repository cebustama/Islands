# Phase W — Hierarchical World-to-Local Generation (Zoom-In): Design Specification

Status: Planning / exploratory only (not implementation authority)  
Authority: Design reference for Phase W implementation. Supplements the Phase W entry
in `PCG_Roadmap.md` with detailed architectural design.  
Depends on: Resolved design decisions from 2026-04-06 (roadmap Decision 4: Voronoi vs
world tiles; Decision 5: Temperature ownership in Phase M).

---

## 1. Phase Intent

Phase W enables selecting a tile on an overworld map and generating a full-resolution local
map parametrised by that tile's world-scale properties — the zoom-in pattern used by RimWorld
and Dwarf Fortress. This is the architectural prerequisite for any game that distinguishes
a world view from a playable local map.

**What this enables:** A world tile carries a compact property set. When selected, those
properties drive a local `MapContext2D` pipeline run, producing a meaningfully different map
for each tile (coastal vs. inland, forested vs. arid, flat vs. hilly) while remaining fully
deterministic.

---

## 2. Resolved Architectural Decisions

These decisions are recorded in `PCG_Roadmap.md` (design decisions section and Phase W entry)
and treated as settled here.

### 2.1 World Map Structure (Resolved)

The world map is a **rectangular grid** — a low-resolution `MapContext2D` (e.g. 64×64 or
128×128 tiles). The same pipeline stages run at world scale; each cell becomes a world tile.

Rationale:
- Preserves the grid-first invariant at all scales.
- Reuses all existing contracts (`MapContext2D`, `IMapStage2D`, `MapPipelineRunner2D`).
- Requires the pipeline to be parametrizable across resolutions. Preferred long-term.

### 2.2 Voronoi Cells ≠ World Tiles (Resolved — Decision 4)

Phase J's Voronoi partitioning operates *within* a local map to create biome regions — the
player walks through these regions and sees biome transitions, but the Voronoi structure
itself is invisible. Phase W's world map uses a rectangular grid. Phase K's Voronoi operates
at a coarser geological scale to define tectonic plates.

This means:
- Phase J does not need to be designed for Phase W compatibility.
- Phase W's world grid is rectangular, preserving the grid-first invariant at all scales.
- Voronoi is a *technique applied within maps* (Phase J: biome regions) and *across the
  world* (Phase K: geological plates), not the world-tile structure itself.
- The `MapShapeInput` hook (Phase F2c) remains the integration point between world and
  local scales — a world tile's coastal geometry is injected as a shape mask regardless
  of whether the world grid is rectangular or otherwise.

### 2.3 Three Independent Spatial Structures (Resolved)

| Structure | Scale | Technique | Purpose |
|-----------|-------|-----------|---------|
| World grid (Phase W) | World | Rectangular `MapContext2D` | World tiles the player selects |
| Local Voronoi (Phase J) | Within a local map | Voronoi partitioning | Biome region boundaries |
| Geological Voronoi (Phase K) | Across the world | Coarse Voronoi (5–8 cells) | Tectonic plates → elevation |

These are independent — each operates at its own scale with its own spatial structure.
Phase K's plate boundaries influence world-tile elevation envelopes but do not define
world-tile boundaries — those are always rectangular grid cells.

---

## 3. `WorldTileContext` — The Handoff Struct

Minimal property set a world tile must carry to parametrise local generation. Every property
must be derivable deterministically from world tile coordinates and the world seed so that
local maps regenerate identically at any time.

| Property | Type | Source | Consumed by |
|----------|------|--------|-------------|
| `LocalSeed` | uint | Derived from world coordinates + world seed | `MapInputs.seed` |
| `ElevationEnvelope` | float | World-scale height field | `MapTunables2D` (waterThreshold, islandRadius) |
| `ShorelineMask` | MaskGrid2D | World-scale coast geometry | `MapShapeInput` (Phase F2c hook) |
| `BiomeType` | BiomeType enum | Phase M biome classification | Stage enable/disable flags, `BiomeDef` lookup |
| `HillinessIntensity` | float | World-scale elevation variance | Hills stage tunables |
| `MoistureLevel` | float | Phase M / Phase L hydrology | `MapFieldId.Moisture` (Phase M) |
| `TemperatureLevel` | float | Phase M climate | `MapFieldId.Temperature` (Phase M) |

### 3.1 Seed Derivation

`LocalSeed` must be derived deterministically from world coordinates and the world seed.
The standard pattern: `localSeed = Hash(worldSeed, tileX, tileY)` where Hash is a
coordinate-based hash that produces well-distributed seeds. This ensures:
- The same world tile always generates the same local map.
- Adjacent tiles produce uncorrelated local maps (no visible seed-proximity artifacts).
- Regenerating a tile at any time (after unloading and reloading) produces bit-identical output.

### 3.2 Property Derivation

All properties are read from the world-scale `MapContext2D` pipeline output at the
corresponding world-tile cell coordinates:
- `ElevationEnvelope` ← `Height` field at world scale
- `BiomeType` ← `Biome` field at world scale (int-as-float, cast to BiomeType enum)
- `MoistureLevel` ← `Moisture` field at world scale
- `TemperatureLevel` ← `Temperature` field at world scale
- `HillinessIntensity` ← derived from world-scale Height field variance in a local neighborhood
- `ShorelineMask` ← derived from world-scale coast geometry (which cells are Land at world scale
  determines the shape mask for the local map)

---

## 4. Architectural Hook (Already in Place)

`MapShapeInput` (Phase F2c) accepts an external mask that overrides the internal island
silhouette. A coastal world tile can inject a shore mask derived from world-scale coast
geometry directly into `Stage_BaseTerrain2D` without restructuring the pipeline.

This is the primary integration point between world scale and local generation.

**How it works:** A world tile at the coast has some cells that are Land and some that are
water at world scale. When zooming in, the local-scale `MapShapeInput` mask is derived from
the world-scale Land/water pattern in the tile's neighborhood, ensuring the local map's
coastline aligns with the world map's coast geometry.

---

## 5. World Map Generation

The world map is itself a low-resolution `MapContext2D` (e.g. 64×64 or 128×128 tiles).
The same stages run at world scale; each cell becomes a world tile.

This is architecturally clean: the pipeline is resolution-agnostic by design. A 64×64
world map uses the same `Stage_BaseTerrain2D`, `Stage_Hills2D`, `Stage_Biome2D` etc.
as a 256×256 local map. The tunables differ (larger island radius, different noise
frequencies) but the contracts are identical.

**What differs at world scale:**
- `islandRadius01` and `waterThreshold01` are tuned for continent-scale geometry.
- Noise frequencies are lower (world-scale features, not local terrain detail).
- The output fields (Height, Temperature, Moisture, Biome) become the source for
  `WorldTileContext` properties rather than direct rendering inputs.
- Phase K (Plate Tectonics) may inject geological structure at this scale only.

---

## 6. Local Map Generation from World Tile

When a world tile is selected for zoom-in, the process is:

1. **Read world tile properties:** Extract `WorldTileContext` from the world-scale
   pipeline output at the tile's coordinates.
2. **Derive local seed:** `localSeed = Hash(worldSeed, tileX, tileY)`.
3. **Build shape mask:** Derive a `MapShapeInput` from the world-scale coast geometry
   in the tile's neighborhood. Interior tiles get a full-land mask; coastal tiles get
   a mask matching the world-scale coastline; ocean tiles are not zoomable.
4. **Configure tunables:** Map `WorldTileContext` properties to `MapTunables2D` values
   and stage-local tunables (e.g., `HillinessIntensity` → hills noise amplitude).
5. **Run local pipeline:** Execute the standard `MapPipelineRunner2D` with the configured
   `MapInputs` (including shape mask and derived seed). The output is a full-resolution
   local `MapContext2D`.

The local map is fully deterministic: the same world tile always produces the same local
map because every input is derived deterministically from the world seed and tile coordinates.

---

## 7. Phase Compatibility Requirements

### 7.1 Phase M (Biome Classification)

World-scale biome assignment is the natural source of `BiomeType`, `MoistureLevel`, and
`TemperatureLevel` in `WorldTileContext`. Phase M's output format (integer biome ID field)
is legible at world tile granularity — simply read the scalar field value at the tile's
world coordinates.

Phase M's design doc specifies that all three outputs (Temperature, Moisture, Biome) are
scalar fields written for all cells including water. This ensures world tiles at any
position can read climate data, even for ocean-adjacent tiles that need to know the
temperature/moisture context of nearby land.

### 7.2 Phase K (Plate Tectonics)

Plate-derived elevation can become the `ElevationEnvelope` property in `WorldTileContext`.
The Phase F2c shape-input mechanism already supports this — plate collision zones can
drive both terrain silhouettes and hill layers.

### 7.3 Phase L (Hydrology)

`MoistureLevel` in `WorldTileContext` can be enriched by Phase L's `FlowAccumulation`
field. If Phase L is not implemented, moisture comes from Phase M's noise+coast model only.

---

## 8. Boundary Matching (Deferred — Phase W2)

Boundary matching between adjacent local maps (edges align correctly) is explicitly deferred.

- RimWorld does not solve this.
- Dwarf Fortress solves it via shared regional constraint data.
- The rectangular grid structure makes boundary matching tractable in a later sub-phase.

**Approach for Phase W2 (not designed yet):** The most promising approach is to share
edge-constraint data between adjacent tiles. When generating a local map, the generator
reads the edge cells of already-generated neighbors and constrains its own edge cells to
match. This requires a generation order (e.g., spiral from center) and a cache of edge data.

Implement zoom-in generation first without edge consistency; treat matching as Phase W2.

---

## 9. Open Design Questions

| Question | Options | Notes |
|----------|---------|-------|
| World map resolution | 64×64, 128×128, or configurable | Affects world-tile granularity and total world size |
| `WorldTileContext` delivery | Struct passed to `MapInputs` vs. new `IMapWorldContext` interface | Struct is simpler; interface is more extensible |
| Phase W2 scope and timing | After Phase W, before or after Phase N | Not in scope for Phase W |
| Ocean tile behavior | Not zoomable vs. generates ocean-only local map | Affects whether ocean tiles are selectable |
| Shape mask derivation | Single-tile footprint vs. 3×3 neighborhood sampling | Neighborhood gives smoother coastlines |

---

## 10. Dependencies

| Dependency | Status | Required? | Role |
|------------|--------|-----------|------|
| Phase F2c (MapShapeInput) | **Done** | Yes | Primary integration hook |
| Phase M (Biome classification) | Planning | Yes | BiomeType, MoistureLevel, TemperatureLevel |
| Phase L (Hydrology) | Planning | Optional | Enriched MoistureLevel |
| Phase K (Plate Tectonics) | Planning | Optional | ElevationEnvelope at geological scale |

## 11. Downstream

- **Phase N (POI Placement):** World tiles carry POI suitability hints; local generation
  produces the map into which POIs are placed.
- **Phase O (Paths):** World-scale path network connects world tiles; local maps expose
  path entry points at tile edges.

---

## 12. What Phase W Does NOT Do

- **No boundary matching.** Adjacent local maps may have discontinuous edges. Phase W2.
- **No chunk streaming.** World tiles are generated on-demand, not streamed. Lazy generation
  and caching are implementation details, not architectural requirements.
- **No Voronoi world structure.** The world grid is rectangular. Voronoi is used within
  local maps (Phase J) and for geological plates (Phase K), not for world tiles.
- **No world-scale simulation.** Climate, weather, seasons are not simulated at world scale.
  World-tile properties are static once the world map is generated.
