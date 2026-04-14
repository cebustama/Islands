namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Biome classification categories for the Whittaker-style lookup table.
    ///
    /// Phase M: 12 ecological biomes + Unclassified (water/off-map sentinel).
    /// Closely matches Patel's 13-type scheme from the canonical Red Blob Games
    /// polygon map generation reference.
    ///
    /// Stored as int-as-float in <see cref="MapFieldId.Biome"/>:
    ///   0f = Unclassified (water cells).
    ///   1f–12f = valid biome types.
    ///
    /// Values are stable and append-only. Do not reorder existing entries.
    /// </summary>
    public enum BiomeType : byte
    {
        /// <summary>Water, off-map, or non-land cells. Sentinel value.</summary>
        Unclassified = 0,

        // ---- Cold (low temperature) ----

        /// <summary>Permanent snow/ice cover at high elevation or extreme cold.</summary>
        Snow = 1,

        /// <summary>Treeless cold plains with sparse vegetation.</summary>
        Tundra = 2,

        /// <summary>Cold coniferous forest (taiga).</summary>
        BorealForest = 3,

        // ---- Temperate ----

        /// <summary>Cool arid region with sparse shrubs.</summary>
        TemperateDesert = 4,

        /// <summary>Woody shrubs and low bushes in moderate climate.</summary>
        Shrubland = 5,

        /// <summary>Deciduous broadleaf forest in moderate climate.</summary>
        TemperateForest = 6,

        /// <summary>Dense wet forest in moderate climate (e.g. Pacific Northwest).</summary>
        TemperateRainforest = 7,

        // ---- Hot (high temperature) ----

        /// <summary>Hot arid desert with minimal vegetation.</summary>
        SubtropicalDesert = 8,

        /// <summary>Warm open plains with grasses.</summary>
        Grassland = 9,

        /// <summary>Tropical forest with distinct wet/dry seasons.</summary>
        TropicalSeasonalForest = 10,

        /// <summary>Dense equatorial forest with year-round rainfall.</summary>
        TropicalRainforest = 11,

        // ---- Special (override-assigned, not from table lookup) ----

        /// <summary>Sandy coastal strip. Post-table override on warm LandEdge cells.</summary>
        Beach = 12,

        COUNT = 13
    }
}