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
        /// <summary>
        /// Voronoi2D (Worley) noise family. Cell-based noise parameterized by
        /// <see cref="TerrainNoiseSettings.worleyDistanceMetric"/> and
        /// <see cref="TerrainNoiseSettings.worleyFunction"/>.
        /// Default (Euclidean + F1) matches the original N4 Worley case.
        /// Phase N5.c: all metric × function combinations are functional.
        /// </summary>
        Worley = 3,
    }

    /// <summary>
    /// Distance metric for Worley/Voronoi noise.
    /// Only relevant when <see cref="TerrainNoiseSettings.noiseType"/> is <see cref="TerrainNoiseType.Worley"/>.
    /// Selects the <see cref="Noise.IVoronoiDistance"/> implementation in the noise runtime.
    ///
    /// Phase N5.b: declared. Phase N5.c: functional.
    /// </summary>
    public enum WorleyDistanceMetric : byte
    {
        /// <summary>Standard Euclidean distance. Default.</summary>
        Euclidean = 0,
        /// <summary>Smoothed Euclidean distance — blends cell boundaries.</summary>
        SmoothEuclidean = 1,
        /// <summary>Chebyshev (L∞) distance — diamond/square cells.</summary>
        Chebyshev = 2,
    }

    /// <summary>
    /// Cell function for Worley/Voronoi noise.
    /// Only relevant when <see cref="TerrainNoiseSettings.noiseType"/> is <see cref="TerrainNoiseType.Worley"/>.
    /// Selects the <see cref="Noise.IVoronoiFunction"/> type parameter in the noise runtime.
    ///
    /// Phase N5.b: declared. Phase N5.c: functional.
    /// </summary>
    public enum WorleyFunction : byte
    {
        /// <summary>Nearest cell distance. Default.</summary>
        F1 = 0,
        /// <summary>Second-nearest cell distance.</summary>
        F2 = 1,
        /// <summary>F2 minus F1 — produces edge ridges ("cracked earth").</summary>
        F2MinusF1 = 2,
        /// <summary>Each cell becomes an island plateau.</summary>
        CellAsIslands = 3,
    }

    // NOTE: FractalMode enum was declared here in N5.b but migrated to the Islands
    // namespace (Noise.cs) in N5.c, since it is a noise-runtime concept consumed by
    // Noise.Settings. The using directive at the top of this file brings it into scope.

    /// <summary>
    /// Serializable noise configuration for terrain height perturbation and domain warp.
    /// Wraps <see cref="Noise.Settings"/> fields plus <see cref="amplitude"/> (stage-side
    /// scaling) and <see cref="noiseType"/> (algorithm selection).
    ///
    /// Two instances are used by <see cref="MapTunables2D"/>: one for height noise,
    /// one for warp noise. The bridge method <see cref="MapNoiseBridge2D.FillNoise01"/>
    /// reads all fields except <see cref="amplitude"/>, which is consumed by the stage.
    ///
    /// Phase N4: initial implementation.
    /// Phase N5.b: added WorleyDistanceMetric, WorleyFunction, FractalMode, ridgedOffset,
    ///             ridgedGain. IEquatable for dirty-tracking.
    /// Phase N5.c: all N5.b fields are functional. FractalMode migrated to Islands namespace.
    ///             Worley case parameterized by metric × function (12 combinations).
    ///             Ridged multifractal algorithm implemented in noise runtime.
    /// </summary>
    [Serializable]
    public struct TerrainNoiseSettings : IEquatable<TerrainNoiseSettings>
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

        // --- N5.b additions (functional as of N5.c) ---

        /// <summary>
        /// Worley distance metric. Only relevant when noiseType == Worley.
        /// Default: Euclidean. Selects Worley / SmoothWorley / Chebyshev distance.
        /// </summary>
        public WorleyDistanceMetric worleyDistanceMetric;

        /// <summary>
        /// Worley cell function. Only relevant when noiseType == Worley.
        /// Default: F1. Selects F1 / F2 / F2MinusF1 / CellAsIslands.
        /// </summary>
        public WorleyFunction worleyFunction;

        /// <summary>
        /// Fractal accumulation mode. Standard = normal fBm. Ridged = ridged multifractal.
        /// Default: Standard. Applies to all noise types.
        /// </summary>
        public FractalMode fractalMode;

        /// <summary>
        /// Ridged multifractal offset. Only relevant when fractalMode == Ridged.
        /// Default: 1.0. Controls ridge sharpness (Musgrave canonical default).
        /// </summary>
        [Range(0f, 2f)] public float ridgedOffset;

        /// <summary>
        /// Ridged multifractal gain. Only relevant when fractalMode == Ridged.
        /// Default: 2.0. Controls heterogeneity feedback (Musgrave canonical default).
        /// </summary>
        [Range(0.5f, 4f)] public float ridgedGain;

        // --- Defaults ---

        /// <summary>Default settings for terrain height perturbation noise.</summary>
        public static TerrainNoiseSettings DefaultTerrain => new TerrainNoiseSettings
        {
            noiseType = TerrainNoiseType.Perlin,
            frequency = 8,
            octaves = 4,
            lacunarity = 2,
            persistence = 0.5f,
            amplitude = 0.35f,
            worleyDistanceMetric = WorleyDistanceMetric.Euclidean,
            worleyFunction = WorleyFunction.F1,
            fractalMode = FractalMode.Standard,
            ridgedOffset = 1.0f,
            ridgedGain = 2.0f,
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
            worleyDistanceMetric = WorleyDistanceMetric.Euclidean,
            worleyFunction = WorleyFunction.F1,
            fractalMode = FractalMode.Standard,
            ridgedOffset = 1.0f,
            ridgedGain = 2.0f,
        };

        // --- IEquatable (N5.b — for dirty-tracking in visualization components) ---

        public bool Equals(TerrainNoiseSettings other) =>
            noiseType == other.noiseType &&
            frequency == other.frequency &&
            octaves == other.octaves &&
            lacunarity == other.lacunarity &&
            Mathf.Approximately(persistence, other.persistence) &&
            Mathf.Approximately(amplitude, other.amplitude) &&
            worleyDistanceMetric == other.worleyDistanceMetric &&
            worleyFunction == other.worleyFunction &&
            fractalMode == other.fractalMode &&
            Mathf.Approximately(ridgedOffset, other.ridgedOffset) &&
            Mathf.Approximately(ridgedGain, other.ridgedGain);

        public override bool Equals(object obj) => obj is TerrainNoiseSettings o && Equals(o);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = (hash ^ (int)noiseType) * 16777619;
                hash = (hash ^ frequency) * 16777619;
                hash = (hash ^ octaves) * 16777619;
                hash = (hash ^ lacunarity) * 16777619;
                hash = (hash ^ persistence.GetHashCode()) * 16777619;
                hash = (hash ^ amplitude.GetHashCode()) * 16777619;
                hash = (hash ^ (int)fractalMode) * 16777619;
                return hash;
            }
        }
    }
}