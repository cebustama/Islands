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
    /// Conservative F3 rules:
    /// - no new authoritative fields are introduced here
    /// - output is written into caller-owned temporary storage
    /// - sampling order is row-major and batched in fixed groups of 4 for determinism
    /// - values are remapped to [0..1] and quantized to stabilize threshold edges
    /// </summary>
    public static class MapNoiseBridge2D
    {
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
