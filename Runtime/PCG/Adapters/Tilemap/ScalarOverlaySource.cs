namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Data sources available for scalar overlay visualization on the tilemap.
    ///
    /// Pipeline field sources (Height, CoastDist, Moisture, Temperature, Biome) are
    /// read directly from <see cref="Islands.PCG.Core.MapContext2D"/> after the pipeline runs.
    ///
    /// Noise preview sources (TerrainNoise, WarpNoiseX/Y, HillsNoise) are computed
    /// on-demand via <see cref="Islands.PCG.Layout.Maps.MapNoiseBridge2D.FillNoise01"/>
    /// using the exact same salts and <see cref="Islands.PCG.Layout.Maps.TerrainNoiseSettings"/>
    /// that the corresponding pipeline stages consume. This guarantees the preview matches
    /// the noise the stage used — not an approximation.
    ///
    /// Phase N6. Adapter-side only — not a pipeline contract. No new MapFieldId entries.
    /// Phase M: Temperature (3) and Biome (4) added to the reserved pipeline-field range.
    /// </summary>
    public enum ScalarOverlaySource : byte
    {
        // =============================================================
        // Pipeline fields — read from MapContext2D
        // =============================================================

        /// <summary>Post-processed height field [0,1]. Includes shape mask,
        /// redistribution, spline remap, and quantization.</summary>
        Height = 0,

        /// <summary>Coast distance field. BFS distance from LandEdge through Land;
        /// negative for water. Typical range: −1 to ~maxDist.</summary>
        CoastDist = 1,

        /// <summary>Moisture field [0,1]. Phase M — shows blank (zeros) if Phase M
        /// is not in the active stage set.</summary>
        Moisture = 2,

        /// <summary>Temperature field [0,1]. Phase M — shows blank (zeros) if Phase M
        /// is not in the active stage set.</summary>
        Temperature = 3,

        /// <summary>Biome field (int-as-float). Phase M — shows blank (zeros) if Phase M
        /// is not in the active stage set. Values are BiomeType ordinals.
        /// Best visualized with a discrete palette; the linear ramp gives a rough
        /// gradient view (low biome IDs = cold, high = tropical/beach).</summary>
        Biome = 4,

        /// <summary>Biome region ID field (int-as-float). Phase M2.b — shows blank
        /// (zeros) if Stage_Regions2D is not in the active stage set.
        /// Values are 1-based region IDs; 0 = water.
        /// Linear ramp reveals distinct region blobs; adjust Max to match
        /// the expected region count for the map size.</summary>
        BiomeRegionId = 5,

        // =============================================================
        // Noise previews — on-demand via MapNoiseBridge2D
        // Gap in numbering reserves 6–9 for future pipeline fields
        // without colliding.
        // =============================================================

        /// <summary>Raw terrain height noise [0,1] before shape mask, redistribution,
        /// or quantization. Salt: 0xF2A10001 (matches Stage_BaseTerrain2D).</summary>
        TerrainNoise = 10,

        /// <summary>Raw warp noise X channel [0,1]. Salt: 0xF2A20002.
        /// Shows the noise pattern, not the actual pixel displacement
        /// (displacement = warpAmplitude01 × minDim × noise).</summary>
        WarpNoiseX = 11,

        /// <summary>Raw warp noise Y channel [0,1]. Salt: 0xF2A30003.</summary>
        WarpNoiseY = 12,

        /// <summary>Hills noise modulation field [0,1]. Salt: 0xF3D50001.
        /// When hillsNoiseBlend = 0 this noise exists but does not affect
        /// hill classification; the preview still shows the underlying pattern.</summary>
        HillsNoise = 13,

        // =============================================================
        // Derived previews — on-demand recomputation
        // =============================================================

        /// <summary>Island shape silhouette [0,1] — the pure mask before noise
        /// perturbation, redistribution, quantization, or spline remap.
        /// Recomputed on-demand from shape mode, warp noise, and island tunables.
        /// Shows uniform 1.0 when shapeMode = NoShape.
        /// When an F2c external shape is active the built-in shape is shown as fallback.
        /// Adapter-side only; not a pipeline field.</summary>
        ShapeMask = 14,
    }
}