using System;
using Unity.Collections;
using Unity.Mathematics;
using Islands;
using Islands.PCG.Core;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Narrow bridge between Map Pipeline code and the shared Islands.Noise runtime.
    ///
    /// Conservative F3 rules (original method):
    /// - no new authoritative fields are introduced here
    /// - output is written into caller-owned temporary storage
    /// - sampling order is row-major and batched in fixed groups of 4 for determinism
    /// - values are remapped to [0..1] and quantized to stabilize threshold edges
    ///
    /// Phase N4 addition: <see cref="FillNoise01"/> supports configurable noise type
    /// via <see cref="TerrainNoiseSettings"/>. No quantization — caller handles
    /// post-processing. Original <see cref="FillSimplexPerlin01"/> is preserved
    /// for F3/F5 backward compatibility.
    ///
    /// Phase N5.c: Worley case parameterized by
    /// <see cref="WorleyDistanceMetric"/> × <see cref="WorleyFunction"/> (12 combinations).
    /// <see cref="Noise.Settings"/> populated with ridged multifractal parameters;
    /// branching handled inside <see cref="Noise.GetFractalNoise{N}"/>.
    /// Voronoi-family noise uses direct [0,1] passthrough (no bipolar remap)
    /// since Voronoi distances are non-negative.
    /// </summary>
    public static class MapNoiseBridge2D
    {
        // =================================================================
        // N4: Configurable noise type dispatch
        // N5.c: Extended Worley dispatch + ridged multifractal passthrough
        // =================================================================

        /// <summary>
        /// Fill a per-cell noise array using the noise type specified in
        /// <paramref name="noiseSettings"/>. Dispatches to the correct generic
        /// <see cref="Noise.INoise"/> instantiation.
        ///
        /// Output range: [0, 1]. No quantization applied (caller handles post-processing).
        /// Row-major, SIMD-batched in groups of 4 for determinism.
        ///
        /// Phase N5.c: Worley dispatches on metric × function. Ridged multifractal
        /// parameters passed through to <see cref="Noise.Settings"/>.
        /// Gradient noise (Perlin, Simplex, Value) uses bipolar remap [-1,1] → [0,1].
        /// Voronoi noise uses direct passthrough [0,1] → [0,1] since distances are
        /// non-negative.
        /// </summary>
        /// <param name="domain">Grid dimensions.</param>
        /// <param name="dst">Caller-owned array, length == domain.Length.</param>
        /// <param name="seed">Pipeline seed (typically <c>inputs.Seed</c>).</param>
        /// <param name="seedSalt">Per-field salt to decorrelate noise fields.</param>
        /// <param name="noiseSettings">Noise algorithm, frequency, octaves, etc.</param>
        public static void FillNoise01(
            in GridDomain2D domain,
            NativeArray<float> dst,
            uint seed,
            uint seedSalt,
            in TerrainNoiseSettings noiseSettings)
        {
            if (!dst.IsCreated)
                throw new InvalidOperationException("dst must be created.");
            if (dst.Length != domain.Length)
                throw new ArgumentException("dst length must match domain length.", nameof(dst));

            var settings = new Noise.Settings
            {
                seed = unchecked((int)(seed ^ seedSalt)),
                frequency = math.max(1, noiseSettings.frequency),
                octaves = math.clamp(noiseSettings.octaves, 1, 6),
                lacunarity = math.clamp(noiseSettings.lacunarity, 2, 4),
                persistence = math.clamp(noiseSettings.persistence, 0f, 1f),
                fractalMode = noiseSettings.fractalMode,
                ridgedOffset = noiseSettings.ridgedOffset,
                ridgedGain = noiseSettings.ridgedGain,
            };

            switch (noiseSettings.noiseType)
            {
                case TerrainNoiseType.Simplex:
                    FillNoise01Core<Noise.Simplex2D<Noise.Simplex>>(domain, dst, settings, remapBipolar: true);
                    break;
                case TerrainNoiseType.Value:
                    FillNoise01Core<Noise.Lattice2D<Noise.LatticeNormal, Noise.Value>>(domain, dst, settings, remapBipolar: true);
                    break;
                case TerrainNoiseType.Worley:
                    FillWorleyNoise01(domain, dst, settings, noiseSettings);
                    break;
                case TerrainNoiseType.Perlin:
                default:
                    FillNoise01Core<Noise.Simplex2D<Noise.Perlin>>(domain, dst, settings, remapBipolar: true);
                    break;
            }
        }

        // =================================================================
        // N5.c: Worley parameterized dispatch (metric × function)
        // =================================================================

        /// <summary>
        /// Dispatches the Worley noise case to the correct generic instantiation
        /// based on <see cref="WorleyDistanceMetric"/> × <see cref="WorleyFunction"/>.
        /// 3 metrics × 4 functions = 12 combinations.
        ///
        /// All Voronoi types use <c>remapBipolar: false</c> since Voronoi distances
        /// are non-negative [0,1]. This avoids the bright-bias that occurs when
        /// applying the gradient-noise bipolar remap to non-negative values.
        ///
        /// Default (Euclidean + F1) produces output with corrected normalization
        /// (full [0,1] dynamic range instead of the pre-fix [0.5,1.0] range).
        /// </summary>
        private static void FillWorleyNoise01(
            in GridDomain2D domain,
            NativeArray<float> dst,
            Noise.Settings settings,
            in TerrainNoiseSettings noiseSettings)
        {
            // Flat key: metric * 4 + function. Euclidean=0, SmoothEuclidean=1, Chebyshev=2.
            int key = (int)noiseSettings.worleyDistanceMetric * 4
                    + (int)noiseSettings.worleyFunction;

            switch (key)
            {
                // --- Euclidean (Worley) ---
                case 0: // Euclidean + F1
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Worley, Noise.F1>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 1: // Euclidean + F2
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Worley, Noise.F2>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 2: // Euclidean + F2MinusF1
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Worley, Noise.F2MinusF1>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 3: // Euclidean + CellAsIslands
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Worley, Noise.CellAsIslands>>(domain, dst, settings, remapBipolar: false);
                    break;

                // --- SmoothEuclidean (SmoothWorley) ---
                case 4: // SmoothEuclidean + F1
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.SmoothWorley, Noise.F1>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 5: // SmoothEuclidean + F2
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.SmoothWorley, Noise.F2>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 6: // SmoothEuclidean + F2MinusF1
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.SmoothWorley, Noise.F2MinusF1>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 7: // SmoothEuclidean + CellAsIslands
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.SmoothWorley, Noise.CellAsIslands>>(domain, dst, settings, remapBipolar: false);
                    break;

                // --- Chebyshev ---
                case 8: // Chebyshev + F1
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Chebyshev, Noise.F1>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 9: // Chebyshev + F2
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Chebyshev, Noise.F2>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 10: // Chebyshev + F2MinusF1
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Chebyshev, Noise.F2MinusF1>>(domain, dst, settings, remapBipolar: false);
                    break;
                case 11: // Chebyshev + CellAsIslands
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Chebyshev, Noise.CellAsIslands>>(domain, dst, settings, remapBipolar: false);
                    break;

                // Fallback — should not be reachable with valid enum values
                default:
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Worley, Noise.F1>>(domain, dst, settings, remapBipolar: false);
                    break;
            }
        }

        // =================================================================
        // Generic inner loop
        // N5.c-post: remapBipolar parameter for Voronoi-aware normalization
        // =================================================================

        /// <summary>
        /// Generic inner loop shared by all noise types. Samples noise at per-cell
        /// resolution using normalized [0,1] coordinates, remaps to [0,1].
        ///
        /// When <paramref name="remapBipolar"/> is <c>true</c> (gradient noise:
        /// Perlin, Simplex, Value), applies <c>n * 0.5 + 0.5</c> to remap [-1,1] → [0,1].
        /// When <c>false</c> (Voronoi noise), applies direct <c>saturate(n)</c> since
        /// Voronoi distances are already non-negative.
        /// </summary>
        private static void FillNoise01Core<N>(
            in GridDomain2D domain,
            NativeArray<float> dst,
            Noise.Settings settings,
            bool remapBipolar) where N : struct, Noise.INoise
        {
            int w = domain.Width;
            int h = domain.Height;
            float invW = 1f / math.max(1, w);
            float invH = 1f / math.max(1, h);

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float py = (y + 0.5f) * invH;

                for (int x = 0; x < w; x += 4)
                {
                    float4 px = new float4(
                        ((x + 0) < w) ? ((x + 0 + 0.5f) * invW) : 0f,
                        ((x + 1) < w) ? ((x + 1 + 0.5f) * invW) : 0f,
                        ((x + 2) < w) ? ((x + 2 + 0.5f) * invW) : 0f,
                        ((x + 3) < w) ? ((x + 3 + 0.5f) * invW) : 0f);

                    float4 pz = new float4(py);
                    float4x3 positions = new float4x3(px, 0f, pz);

                    float4 n = Noise.GetFractalNoise<N>(positions, settings).v;
                    float4 v = remapBipolar
                        ? math.saturate(n * 0.5f + 0.5f)
                        : math.saturate(n);

                    if (x + 0 < w) dst[row + x + 0] = v.x;
                    if (x + 1 < w) dst[row + x + 1] = v.y;
                    if (x + 2 < w) dst[row + x + 2] = v.z;
                    if (x + 3 < w) dst[row + x + 3] = v.w;
                }
            }
        }

        // =================================================================
        // Original F3 method — unchanged, used by Hills + Vegetation stages
        // =================================================================

        public static void FillSimplexPerlin01(
            in GridDomain2D domain,
            NativeArray<float> dst,
            uint seed,
            uint seedSalt,
            int frequency,
            int octaves,
            int lacunarity,
            float persistence,
            int quantSteps = 1024)
        {
            if (!dst.IsCreated) throw new InvalidOperationException("dst must be created.");
            if (dst.Length != domain.Length) throw new ArgumentException("dst length must match domain length.", nameof(dst));

            int w = domain.Width;
            int h = domain.Height;

            var settings = new Noise.Settings
            {
                seed = unchecked((int)(seed ^ seedSalt)),
                frequency = math.max(1, frequency),
                octaves = math.clamp(octaves, 1, 6),
                lacunarity = math.clamp(lacunarity, 2, 4),
                persistence = math.clamp(persistence, 0f, 1f)
            };

            float invW = 1f / math.max(1, w);
            float invH = 1f / math.max(1, h);
            float invQuant = quantSteps > 1 ? 1f / quantSteps : 0f;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                float py = (y + 0.5f) * invH;

                for (int x = 0; x < w; x += 4)
                {
                    float4 px = new float4(
                        ((x + 0) < w) ? ((x + 0 + 0.5f) * invW) : 0f,
                        ((x + 1) < w) ? ((x + 1 + 0.5f) * invW) : 0f,
                        ((x + 2) < w) ? ((x + 2 + 0.5f) * invW) : 0f,
                        ((x + 3) < w) ? ((x + 3 + 0.5f) * invW) : 0f);

                    float4 pz = new float4(py);
                    float4x3 positions = new float4x3(px, 0f, pz);

                    float4 n = Noise.GetFractalNoise<Noise.Simplex2D<Noise.Perlin>>(positions, settings).v;
                    float4 v = math.saturate(n * 0.5f + 0.5f);

                    if (quantSteps > 1)
                        v = math.floor(v * quantSteps) * invQuant;

                    if (x + 0 < w) dst[row + x + 0] = v.x;
                    if (x + 1 < w) dst[row + x + 1] = v.y;
                    if (x + 2 < w) dst[row + x + 2] = v.z;
                    if (x + 3 < w) dst[row + x + 3] = v.w;
                }
            }
        }
    }
}