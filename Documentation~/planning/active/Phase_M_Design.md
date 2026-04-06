# Phase M — Climate & Biome Classification: Design Specification

Status: Planning only (not implementation authority)  
Authority: Design reference for Phase M implementation. Supplements the Phase M entry
in `PCG_Roadmap.md` with detailed stage contracts, data structures, and test plan.  
Depends on: Resolved design decisions from 2026-04-06 (roadmap).

---

## 1. Phase Intent

Phase M transforms a monochrome island into a multi-region landscape with distinct
ecological character. It computes two climate fields (Temperature, Moisture), classifies
every land cell into a biome category via a Whittaker-style lookup table, and writes all
three as scalar fields. This is the single most impactful phase for world variety — every
downstream system (vegetation, POI placement, tile selection, world-tile handoff) gains a
principled, ecologically grounded basis for variation.

Phase M is designed to work **with or without** Phase J (Voronoi regions) and **with or
without** Phase L (Hydrology). When those phases are absent, Phase M produces functional
but less ecologically rich output. This is a deliberate design choice — Phase M should be
implementable immediately after Phase G, with optional enrichment from later phases.

---

## 2. Resolved Design Decisions (from Roadmap 2026-04-06)

These decisions are recorded in `PCG_Roadmap.md` and treated as settled here. This section
summarizes them for self-contained readability; the roadmap is authoritative.

| # | Question | Resolution | Key rationale |
|---|----------|------------|---------------|
| DD-1 | Biome output format | `MapFieldId.Biome` integer scalar field | Compact; no MapLayerId explosion; matches Minecraft/DF/RimWorld pattern |
| DD-2 | River representation | Both `MapFieldId.FlowAccumulation` + `MapLayerId.Rivers` | Field is simulation output; mask is gameplay/rendering output |
| DD-3 | Lake modeling | Distinct `MapLayerId.Lakes` | Gameplay semantics differ from coastal ShallowWater |
| DD-4 | Voronoi vs world tiles | Separate concepts at separate scales | Grid-first at world scale; Voronoi within local maps only |
| DD-5 | Temperature ownership | Phase M writes `MapFieldId.Temperature` | Natural co-location with Moisture and Biome computation |

---

## 3. New Registry Entries

### MapFieldId additions

| ID | Name | Written By | Type | Notes |
|----|------|-----------|------|-------|
| 3 | `Temperature` | `Stage_Biome2D` (sub-stage M.1) | float [0,1] | New; append-only |
| 1 | `Moisture` | `Stage_Biome2D` (sub-stage M.2) | float [0,1] | Already registered; first authoritative write |
| 4 | `Biome` | `Stage_Biome2D` (sub-stage M.3) | int-as-float | New; append-only |

After Phase M: `MapFieldId.COUNT` -> 5 (was 3: Height, Moisture, CoastDist).

### MapLayerId

No new entries. COUNT stays at 12.

---

## 4. Stage Contract: `Stage_Biome2D`

### 4.1 Pipeline Position

After `Stage_Morphology2D` (Phase G), before any Phase M2 biome-aware vegetation rework.

Full stage order:

```
BaseTerrain(F2) -> Hills(F3) -> Shore(F4) -> Vegetation(F5) -> Traversal(F6)
  -> Morphology(G) -> Biome(M)
```

**Vegetation ordering note:** F5 runs before Biome intentionally. The current F5
noise-threshold vegetation is a reasonable first pass that does not require biome data.
Phase M2 will refactor F5 to read biome and moisture fields, but Phase M itself does
not modify F5's behavior. See S8 for the M2 interaction model.

### 4.2 Inputs (Read-Only)

| Data | Source | Usage |
|------|--------|-------|
| `Height` (MapFieldId 0) | Stage_BaseTerrain2D | Temperature: elevation-based lapse rate |
| `CoastDist` (MapFieldId 2) | Stage_Morphology2D | Temperature: maritime moderation; Moisture: coastal wetness |
| `Land` (MapLayerId 0) | Stage_BaseTerrain2D | Water cells get sentinel biome value |
| `LandEdge` (MapLayerId 9) | Stage_Hills2D | Beach biome override |
| `FlowAccumulation` (MapFieldId, Phase L) | Stage_Rivers2D | Moisture: river proximity. **Optional** — absent if Phase L not implemented |
| `RegionId` (MapFieldId, Phase J) | Stage_Regions2D | Biome: macro-region coherence. **Optional** — absent if Phase J not implemented |

### 4.3 Outputs (Authoritative Write)

| Field | Range | Contract |
|-------|-------|----------|
| `Temperature` | [0, 1] normalized; 0 = coldest, 1 = hottest | Written for **all** cells (water included, for visual continuity and Phase W readability). |
| `Moisture` | [0, 1] normalized; 0 = driest, 1 = wettest | Written for **all** cells. |
| `Biome` | 0f = Unclassified (water). 1f-Nf = biome enum values | Non-Land cells receive 0f. All Land cells receive a valid biome ID > 0. |

### 4.4 RNG / Noise Strategy

**Preferred approach: Coordinate hashing via `MapNoiseBridge2D`.**

The roadmap notes: "No RNG consumption required if noise uses coordinate hashing via
MapNoiseBridge2D." This is the preferred path because:

- It avoids RNG consumption order fragility (the pattern that requires "always consume N
  arrays regardless of tunables" to keep downstream stages stable).
- `MapNoiseBridge2D` already handles seed derivation from `inputs.seed + stageSalt`.
- Temperature and moisture noise are both low-frequency fields — coordinate hashing at
  coarse cell sizes is inexpensive and deterministic by construction.

**Stage salt:** A unique stage salt (e.g., `0xB10ME`) is used to derive noise seeds,
keeping Phase M's noise fields uncorrelated with existing terrain noise (F2/F3).

**If `ctx.Rng` consumption is needed** (e.g., for a noise pattern not supported by the
bridge): consumption order must be fixed and documented, and both arrays must always be
fully consumed regardless of tunable values. This is the F2b pattern.

**Decision status: Preferred approach identified (coordinate hashing). Final choice
confirmed at implementation time based on `MapNoiseBridge2D` capability review.**

---

## 5. Sub-Stage Detail

### 5.1 Sub-Stage M.1 — Temperature Field

**Formula (per cell):**

```
latitudeFactor = latitudeEffect * abs(y / domainHeight - 0.5) * 2.0

coastModeration = coastModerationStrength * (1.0 / (1.0 + max(coastDist[x,y], 0)))

temp_raw = baseTemperature
         - lapseRate * height[x,y]
         - latitudeFactor
         + coastModeration
         + tempNoiseAmplitude * (noise(x, y, tempSeed) - 0.5)

temperature[x,y] = clamp(temp_raw, 0, 1)
```

**Design notes:**

- `baseTemperature` (default ~0.7): the sea-level equatorial temperature. High default
  ensures a single tropical island generates warm biomes by default, matching the
  "islands" theme. The roadmap formula confirms: `base_temp - latitude_factor -
  (elevation x lapse_rate) + coast_moderation + noise_perturbation`.
- `lapseRate` (default ~0.5): height-to-temperature reduction. With Height in [0,1] and
  a 0.5 lapse rate, the highest peaks lose half the base temperature. This is the primary
  axis of temperature variation on a single island. Models the real-world environmental
  lapse rate of ~6.5 deg C per 1,000m elevation gain.
- `latitudeEffect` (default 0.0): Y-axis to temperature gradient. Zero for single-island
  generation (no latitude concept). Non-zero for world-scale Phase W maps where Y
  represents latitude. Top-of-map is cold, bottom is warm (configurable via sign).
- `coastModeration`: coastal cells have more moderate temperatures. The `1/(1+coastDist)`
  falloff ensures the effect is strongest at the coast and negligible inland.
- Noise perturbation: low-frequency coordinate-hashed noise via `MapNoiseBridge2D`.

**Stage-local tunables (M.1):**

```
float baseTemperature         = 0.7f;   // [0, 1]
float lapseRate               = 0.5f;   // [0, 1]
float latitudeEffect          = 0.0f;   // [0, 1], 0 = no latitude
float coastModerationStrength = 0.1f;   // [0, 0.5]
float tempNoiseAmplitude      = 0.05f;  // [0, 0.3]
int   tempNoiseCellSize       = 16;     // coarse; 2x terrain noise freq
```

### 5.2 Sub-Stage M.2 — Moisture Field

**Formula (per cell):**

```
coastFactor = coastalMoistureBonus / (1.0 + max(coastDist[x,y], 0) * coastDecayRate)

riverFactor = 0.0
if (FlowAccumulation field exists):
    riverFactor = riverMoistureBonus * clamp(flowAccum[x,y] / riverFlowNorm, 0, 1)

moisture_raw = moistureNoiseAmplitude * noise(x, y, moistureSeed)
             + coastFactor
             + riverFactor

moisture[x,y] = clamp(moisture_raw, 0, 1)
```

**Design notes:**

- Moisture noise frequency must be 4-8x lower than terrain noise frequency to avoid
  biome fragmentation (technique report 5a, "five common mistakes," point 1). With
  terrain at NoiseCellSize=8, moisture should use CellSize >= 32.
- `riverFactor`: optional enrichment from Phase L's `FlowAccumulation` field. The roadmap
  explicitly states: "If Phase L is not yet implemented, moisture falls back to CoastDist +
  noise only."
- **Rainshadow:** deferred to Phase L or a later M refinement.

**Stage-local tunables (M.2):**

```
float coastalMoistureBonus    = 0.3f;   // [0, 1]
float coastDecayRate          = 0.15f;  // [0, 1], higher = faster decay
float moistureNoiseAmplitude  = 0.5f;   // [0, 1]
int   moistureNoiseCellSize   = 32;     // 4x terrain freq minimum
float riverMoistureBonus      = 0.4f;   // [0, 1], applied only if Phase L present
float riverFlowNorm           = 50.0f;  // normalization divisor for flow accumulation
```

### 5.3 Sub-Stage M.3 — Biome Classification

#### 5.3.1 BiomeType Enum

```csharp
public enum BiomeType : byte
{
    Unclassified = 0,   // water, off-map, non-land

    // Cold (low temperature)
    Snow                   = 1,
    Tundra                 = 2,
    BorealForest           = 3,   // taiga

    // Temperate
    TemperateDesert        = 4,
    Shrubland              = 5,
    TemperateForest        = 6,   // deciduous
    TemperateRainforest    = 7,

    // Hot (high temperature)
    SubtropicalDesert      = 8,
    Grassland              = 9,
    TropicalSeasonalForest = 10,
    TropicalRainforest     = 11,

    // Special (override-assigned, not from table lookup)
    Beach                  = 12,

    COUNT                  = 13
}
```

12 biomes + Unclassified. Closely matches Patel's 13-type scheme from the canonical
Red Blob Games polygon map generation reference.

#### 5.3.2 Whittaker Table: 4x4 (Temperature x Moisture)

| Temp \ Moisture | Dry (0) | Moderate (1) | Wet (2) | Saturated (3) |
|-----------------|---------|--------------|---------|----------------|
| **Cold (0)** | Tundra | Tundra | Snow | Snow |
| **Cool (1)** | TemperateDesert | Shrubland | BorealForest | BorealForest |
| **Warm (2)** | Grassland | TemperateForest | TemperateForest | TemperateRainforest |
| **Hot (3)** | SubtropicalDesert | Grassland | TropicalSeasonalForest | TropicalRainforest |

Temperature bands: [0, 0.25), [0.25, 0.50), [0.50, 0.75), [0.75, 1.0].
Moisture bands: same thresholds. Data-driven array — upgradable to 6x6 later without
contract changes.

**Missing triangle constraint:** Cold+Saturated = Snow (ice), not an impossible biome.
The table entries *are* the constraint — no runtime triangle clipping needed.

**Beach override:** LandEdge cells with `Temperature >= 0.25` are overridden to Beach.
Post-table override (geographic feature, not climate outcome).

#### 5.3.3 BiomeDef Data Structure

Roadmap: "initially a code-side array; may be promoted to ScriptableObject later."

```csharp
public struct BiomeDef
{
    public BiomeType type;
    public string displayName;
    public float vegetationDensity;    // [0, 1] — consumed by Phase M2
}

public static class BiomeTable
{
    public static readonly int TemperatureBands = 4;
    public static readonly int MoistureBands = 4;

    public static readonly BiomeType[] WhittakerTable = new BiomeType[]
    {
        // Row 0 (Cold): Dry -> Saturated
        BiomeType.Tundra, BiomeType.Tundra, BiomeType.Snow, BiomeType.Snow,
        // Row 1 (Cool):
        BiomeType.TemperateDesert, BiomeType.Shrubland,
        BiomeType.BorealForest, BiomeType.BorealForest,
        // Row 2 (Warm):
        BiomeType.Grassland, BiomeType.TemperateForest,
        BiomeType.TemperateForest, BiomeType.TemperateRainforest,
        // Row 3 (Hot):
        BiomeType.SubtropicalDesert, BiomeType.Grassland,
        BiomeType.TropicalSeasonalForest, BiomeType.TropicalRainforest,
    };

    public static readonly float BeachMinTemperature = 0.25f;

    public static BiomeType Lookup(float temp01, float moist01)
    {
        int t = Mathf.Clamp((int)(temp01 * TemperatureBands), 0, TemperatureBands - 1);
        int m = Mathf.Clamp((int)(moist01 * MoistureBands), 0, MoistureBands - 1);
        return WhittakerTable[t * MoistureBands + m];
    }

    public static readonly BiomeDef[] Definitions = new BiomeDef[]
    {
        new() { type = BiomeType.Unclassified,           displayName = "Unclassified",             vegetationDensity = 0.0f },
        new() { type = BiomeType.Snow,                   displayName = "Snow",                     vegetationDensity = 0.0f },
        new() { type = BiomeType.Tundra,                 displayName = "Tundra",                   vegetationDensity = 0.05f },
        new() { type = BiomeType.BorealForest,           displayName = "Boreal Forest",            vegetationDensity = 0.6f },
        new() { type = BiomeType.TemperateDesert,        displayName = "Temperate Desert",         vegetationDensity = 0.05f },
        new() { type = BiomeType.Shrubland,              displayName = "Shrubland",                vegetationDensity = 0.25f },
        new() { type = BiomeType.TemperateForest,        displayName = "Temperate Forest",         vegetationDensity = 0.65f },
        new() { type = BiomeType.TemperateRainforest,    displayName = "Temperate Rainforest",     vegetationDensity = 0.85f },
        new() { type = BiomeType.SubtropicalDesert,      displayName = "Subtropical Desert",       vegetationDensity = 0.02f },
        new() { type = BiomeType.Grassland,              displayName = "Grassland",                vegetationDensity = 0.15f },
        new() { type = BiomeType.TropicalSeasonalForest, displayName = "Tropical Seasonal Forest", vegetationDensity = 0.7f },
        new() { type = BiomeType.TropicalRainforest,     displayName = "Tropical Rainforest",      vegetationDensity = 0.9f },
        new() { type = BiomeType.Beach,                  displayName = "Beach",                    vegetationDensity = 0.02f },
    };
}
```

---

## 6. Invariants (Test-Gated)

| ID | Invariant |
|----|-----------|
| M-1 | **Determinism:** Same seed + tunables => identical Temperature, Moisture, Biome fields |
| M-2 | **Water sentinel:** `Biome[x,y] == 0f` for all non-Land cells |
| M-3 | **Land coverage:** `Biome[x,y] > 0f` for all Land cells |
| M-4 | **Temperature range:** all values in [0, 1] |
| M-5 | **Moisture range:** all values in [0, 1] |
| M-6 | **Beach consistency:** warm LandEdge cells have `Biome == Beach` |
| M-7 | **No-mutate:** Height, CoastDist, Land, LandEdge unchanged after execution |
| M-8 | **Valid biome range:** all Land biome values are valid `BiomeType` enum values |

---

## 7. Test Plan

### 7.1 Stage-Level Tests (`StageBiome2DTests.cs`)

| Test | Asserts |
|------|---------|
| Determinism | Two runs, same config -> identical field hashes |
| Temperature golden | Hash locked for default tunables, 64x64, seed=12345 |
| Moisture golden | Hash locked for same config |
| Biome golden | Hash locked for same config |
| Water sentinel (M-2) | All non-Land cells have `Biome == 0f` |
| Land coverage (M-3) | All Land cells have `Biome > 0f` |
| Temperature range (M-4) | All values in [0, 1] |
| Moisture range (M-5) | All values in [0, 1] |
| Beach override (M-6) | All warm LandEdge cells have `Biome == Beach` |
| No-mutate (M-7) | Input field/layer hashes unchanged |
| Valid biome range (M-8) | All Land biome values are valid enum values |
| Without Phase L | Runs without FlowAccumulation; moisture = coast+noise only |
| Without Phase J | Runs without RegionId; per-cell classification only |

### 7.2 Pipeline Golden (`MapPipelineRunner2DGoldenMTests.cs`)

Full F0-M golden. Does NOT invalidate existing F0-G goldens.

---

## 8. Phase M2 Interaction

**M2.a — Vegetation refactor:** replaces F5's global noise threshold with per-biome
`BiomeDef.vegetationDensity`. Pipeline ordering decision (move F5 after M vs. two-pass
vegetation) deferred to Phase M2 design.

**M2.b — Region naming:** CCA on same-biome cells -> `MapFieldId.NamedRegionId` + metadata
table. Deferred to Phase M2 design.

---

## 9. Downstream Consumers

| Consumer | Reads | How |
|----------|-------|-----|
| Phase M2 | `Biome`, `Moisture` | Per-biome vegetation density |
| Phase N | `Biome` | POI suitability scoring |
| Phase W | `Biome`, `Moisture`, `Temperature` | WorldTileContext properties |
| TilesetConfig | `Biome` | Biome-conditional tile entries |
| Composite viz | `Biome` | Per-biome color overlay |
| MapExporter2D | All three fields | Automatic export (no contract changes) |

---

## 10. Open Questions (Implementation Time)

| Question | Recommended |
|----------|-------------|
| Noise strategy | Coordinate hashing via `MapNoiseBridge2D` |
| BiomeDef promotion | Code-side first, SO later (per roadmap) |
| Field hash method | FNV-1a over float bits (matches CoastDist) |
| Coast moderation | Additive (simpler to tune) |
| Stage structure | Single `Stage_Biome2D` with three internal methods |

---

## 11. Expected Files

**Runtime:** `Stage_Biome2D.cs`, `BiomeType.cs`, `BiomeTable.cs`, `MapIds2D.cs` (updated)  
**Tests:** `StageBiome2DTests.cs`, `MapPipelineRunner2DGoldenMTests.cs`  
**Sample:** `PCGMapVisualization` + `PCGMapCompositeVisualization` patched for biome view  
**No changes to:** `MapContext2D.cs`, `MapExporter2D.cs`, `TilemapAdapter2D.cs`
