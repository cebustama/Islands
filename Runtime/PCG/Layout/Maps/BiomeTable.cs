using Unity.Mathematics;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Per-biome metadata. Code-side array; may be promoted to ScriptableObject later.
    ///
    /// Phase M: initial definition.
    /// Phase M2 consumer: <see cref="vegetationDensity"/> drives per-biome vegetation.
    /// </summary>
    public struct BiomeDef
    {
        public BiomeType type;
        public string displayName;

        /// <summary>
        /// Base vegetation density for this biome. [0, 1].
        /// Consumed by Phase M2 to replace the global noise threshold in Stage_Vegetation2D.
        /// </summary>
        public float vegetationDensity;
    }

    /// <summary>
    /// Whittaker-style biome classification table.
    ///
    /// 4×4 grid indexed by quantized Temperature (rows) × Moisture (columns).
    /// Temperature bands: [0, 0.25), [0.25, 0.50), [0.50, 0.75), [0.75, 1.0].
    /// Moisture bands: same thresholds.
    ///
    /// Data-driven flat array — upgradable to 6×6 later without contract changes.
    ///
    /// Beach override is a post-table geographic feature override, not a climate outcome:
    /// warm LandEdge cells → Beach biome regardless of table result.
    ///
    /// Phase M: initial definition.
    /// </summary>
    public static class BiomeTable
    {
        public static readonly int TemperatureBands = 4;
        public static readonly int MoistureBands = 4;

        /// <summary>
        /// Flat Whittaker table: row-major [temp_band * MoistureBands + moist_band].
        ///
        /// | Temp \ Moisture | Dry (0)           | Moderate (1)    | Wet (2)                  | Saturated (3)        |
        /// |-----------------|-------------------|-----------------|--------------------------|----------------------|
        /// | Cold (0)        | Tundra            | Tundra          | Snow                     | Snow                 |
        /// | Cool (1)        | TemperateDesert   | Shrubland       | BorealForest             | BorealForest         |
        /// | Warm (2)        | Grassland         | TemperateForest | TemperateForest          | TemperateRainforest  |
        /// | Hot (3)         | SubtropicalDesert | Grassland       | TropicalSeasonalForest   | TropicalRainforest   |
        /// </summary>
        public static readonly BiomeType[] WhittakerTable = new BiomeType[]
        {
            // Row 0 (Cold): Dry → Saturated
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

        /// <summary>
        /// Minimum temperature for Beach override. LandEdge cells below this
        /// threshold do not receive Beach (they stay as their climate-assigned biome,
        /// typically Tundra or Snow at cold temperatures).
        /// </summary>
        public static readonly float BeachMinTemperature = 0.25f;

        /// <summary>
        /// Look up the Whittaker table biome for the given normalized temperature
        /// and moisture values. Both inputs are expected in [0, 1].
        ///
        /// Returns the table-assigned biome. Beach override is applied separately
        /// by the caller (geographic feature, not climate outcome).
        /// </summary>
        public static BiomeType Lookup(float temp01, float moist01)
        {
            int t = math.clamp((int)(temp01 * TemperatureBands), 0, TemperatureBands - 1);
            int m = math.clamp((int)(moist01 * MoistureBands), 0, MoistureBands - 1);
            return WhittakerTable[t * MoistureBands + m];
        }

        /// <summary>
        /// Per-biome metadata indexed by <see cref="BiomeType"/> ordinal.
        /// Array length == <see cref="BiomeType.COUNT"/>.
        /// </summary>
        public static readonly BiomeDef[] Definitions = new BiomeDef[]
        {
            new() { type = BiomeType.Unclassified,           displayName = "Unclassified",              vegetationDensity = 0.0f },
            new() { type = BiomeType.Snow,                   displayName = "Snow",                      vegetationDensity = 0.0f },
            new() { type = BiomeType.Tundra,                 displayName = "Tundra",                    vegetationDensity = 0.05f },
            new() { type = BiomeType.BorealForest,           displayName = "Boreal Forest",             vegetationDensity = 0.6f },
            new() { type = BiomeType.TemperateDesert,        displayName = "Temperate Desert",          vegetationDensity = 0.05f },
            new() { type = BiomeType.Shrubland,              displayName = "Shrubland",                 vegetationDensity = 0.25f },
            new() { type = BiomeType.TemperateForest,        displayName = "Temperate Forest",          vegetationDensity = 0.65f },
            new() { type = BiomeType.TemperateRainforest,    displayName = "Temperate Rainforest",      vegetationDensity = 0.85f },
            new() { type = BiomeType.SubtropicalDesert,      displayName = "Subtropical Desert",        vegetationDensity = 0.02f },
            new() { type = BiomeType.Grassland,              displayName = "Grassland",                 vegetationDensity = 0.15f },
            new() { type = BiomeType.TropicalSeasonalForest, displayName = "Tropical Seasonal Forest",  vegetationDensity = 0.7f },
            new() { type = BiomeType.TropicalRainforest,     displayName = "Tropical Rainforest",       vegetationDensity = 0.9f },
            new() { type = BiomeType.Beach,                  displayName = "Beach",                     vegetationDensity = 0.02f },
        };
    }
}