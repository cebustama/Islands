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
    /// </summary>
    public static class MapNoiseBridge2D
    {
        // =================================================================
        // N4: Configurable noise type dispatch
        // =================================================================

        /// <summary>
        /// Fill a per-cell noise array using the noise type specified in
        /// <paramref name="noiseSettings"/>. Dispatches to the correct generic
        /// <see cref="Noise.INoise"/> instantiation.
        ///
        /// Output range: [0, 1]. No quantization applied (caller handles post-processing).
        /// Row-major, SIMD-batched in groups of 4 for determinism.
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
                persistence = math.clamp(noiseSettings.persistence, 0f, 1f)
            };

            switch (noiseSettings.noiseType)
            {
                case TerrainNoiseType.Simplex:
                    FillNoise01Core<Noise.Simplex2D<Noise.Simplex>>(domain, dst, settings);
                    break;
                case TerrainNoiseType.Value:
                    FillNoise01Core<Noise.Lattice2D<Noise.LatticeNormal, Noise.Value>>(domain, dst, settings);
                    break;
                case TerrainNoiseType.Worley:
                    FillNoise01Core<Noise.Voronoi2D<Noise.LatticeNormal, Noise.Worley, Noise.F1>>(domain, dst, settings);
                    break;
                case TerrainNoiseType.Perlin:
                default:
                    FillNoise01Core<Noise.Simplex2D<Noise.Perlin>>(domain, dst, settings);
                    break;
            }
        }

        /// <summary>
        /// Generic inner loop shared by all noise types. Samples noise at per-cell
        /// resolution using normalized [0,1] coordinates, remaps [-1,1] → [0,1].
        /// </summary>
        private static void FillNoise01Core<N>(
            in GridDomain2D domain,
            NativeArray<float> dst,
            Noise.Settings settings) where N : struct, Noise.INoise
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
                    float4 v = math.saturate(n * 0.5f + 0.5f);

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