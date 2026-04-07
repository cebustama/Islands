using System;
using UnityEngine;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Noise algorithm selector for terrain generation.
    /// Maps to compile-time <see cref="Noise.INoise"/> generic instantiations
    /// in the noise runtime. Dispatched by <see cref="MapNoiseBridge2D.FillNoise01"/>.
    ///
    /// Phase N4.
    /// </summary>
    public enum TerrainNoiseType : byte
    {
        /// <summary>Simplex2D with Perlin gradients. Smooth, low artifact. Default.</summary>
        Perlin = 0,
        /// <summary>Simplex2D with Simplex gradients. Slightly different character.</summary>
        Simplex = 1,
        /// <summary>Lattice2D value noise. Blobby grid artifacts — mainly for comparison.</summary>
        Value = 2,
        /// <summary>Voronoi2D F1 (Worley). Cell-based — experimental for terrain.</summary>
        Worley = 3,
    }

    /// <summary>
    /// Serializable noise configuration for terrain height perturbation and domain warp.
    /// Wraps <see cref="Noise.Settings"/> fields plus <see cref="amplitude"/> (stage-side
    /// scaling) and <see cref="noiseType"/> (algorithm selection).
    ///
    /// Two instances are used by <see cref="MapTunables2D"/>: one for height noise,
    /// one for warp noise. The bridge method <see cref="MapNoiseBridge2D.FillNoise01"/>
    /// reads all fields except <see cref="amplitude"/>, which is consumed by the stage.
    ///
    /// Phase N4.
    /// </summary>
    [Serializable]
    public struct TerrainNoiseSettings
    {
        /// <summary>Noise algorithm to use.</summary>
        public TerrainNoiseType noiseType;

        /// <summary>
        /// Number of noise cells across the [0,1] normalized domain. Resolution-independent.
        /// Higher = finer features. 8 ≈ old noiseCellSize=8 at 64×64.
        /// </summary>
        [Range(1, 32)] public int frequency;

        /// <summary>Number of fBm octaves. More octaves = more fine detail layered on.</summary>
        [Range(1, 6)] public int octaves;

        /// <summary>Frequency multiplier per octave. 2 = standard doubling.</summary>
        [Range(2, 4)] public int lacunarity;

        /// <summary>Amplitude decay per octave. 0.5 = standard halving.</summary>
        [Range(0f, 1f)] public float persistence;

        /// <summary>
        /// Amplitude multiplier applied by the consuming stage (not the noise runtime).
        /// Controls how much this noise field contributes to the final height or warp value.
        /// </summary>
        [Range(0f, 1f)] public float amplitude;

        /// <summary>Default settings for terrain height perturbation noise.</summary>
        public static TerrainNoiseSettings DefaultTerrain => new TerrainNoiseSettings
        {
            noiseType = TerrainNoiseType.Perlin,
            frequency = 8,
            octaves = 4,
            lacunarity = 2,
            persistence = 0.5f,
            amplitude = 0.35f,
        };

        /// <summary>
        /// Default settings for domain warp noise. Lower frequency (coarser features)
        /// to produce broad organic shape distortion rather than fine detail.
        /// Amplitude is 1.0 because actual displacement is scaled by
        /// <see cref="MapTunables2D.warpAmplitude01"/> in the stage.
        /// </summary>
        public static TerrainNoiseSettings DefaultWarp => new TerrainNoiseSettings
        {
            noiseType = TerrainNoiseType.Perlin,
            frequency = 4,
            octaves = 1,
            lacunarity = 2,
            persistence = 0.5f,
            amplitude = 1.0f,
        };
    }
}