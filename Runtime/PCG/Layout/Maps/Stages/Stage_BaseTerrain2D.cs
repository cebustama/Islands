using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F2.1 — Base terrain stage (minimal “island-like” vertical slice).
    ///
    /// Writes:
    /// - Field: Height (ScalarField2D) in [0..1]
    /// - Layer: Land (MaskGrid2D) where Height >= waterThreshold01
    /// - Layer: DeepWater (MaskGrid2D) = border-connected NOT Land (stable flood fill)
    ///
    /// Determinism:
    /// - Uses only ctx.Rng for randomness (seed-driven, stage-order dependent, stable consumption count).
    /// - Row-major loops only.
    /// - No HashSet/Dictionary traversal.
    ///
    /// OOB-safe:
    /// - No neighbor reads here.
    /// - DeepWater uses MaskFloodFillOps2D which is OOB-safe.
    /// </summary>
    public sealed class Stage_BaseTerrain2D : IMapStage2D
    {
        public string Name => "base_terrain";

        // ---- Minimal, fixed constants for F2 (keep stable to preserve goldens) ----

        // Coarse value-noise cell size (bigger => smoother, smaller => noisier)
        private const int NoiseCellSize = 8;

        // Noise amplitude applied inside the island shape only
        private const float NoiseAmplitude = 0.18f;

        // Deterministic quantization steps to reduce threshold-edge sensitivity
        // (keeps masks more stable if you later swap noise implementation)
        private const int QuantSteps = 1024;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            // --- Ensure outputs exist (deterministic allocation rules already enforced by MapContext2D) ---
            ref ScalarField2D height = ref ctx.EnsureField(MapFieldId.Height, clearToZero: true);
            ref MaskGrid2D land = ref ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);
            ref MaskGrid2D deepWater = ref ctx.EnsureLayer(MapLayerId.DeepWater, clearToZero: true);

            // Tunables (already clamped/sanitized deterministically)
            var t = inputs.Tunables;

            float waterThreshold = t.waterThreshold01;

            // Radius in cells
            float minDim = math.min((float)w, (float)h);
            float radius = minDim * t.islandRadius01;
            if (radius < 1f) radius = 1f; // avoid divide-by-zero / degenerate
            float invRadiusSq = 1f / (radius * radius);

            // Use squared thresholds to match our squared radial parameter (avoid sqrt for extra stability)
            float fromSq = t.islandSmoothFrom01 * t.islandSmoothFrom01;
            float toSq = t.islandSmoothTo01 * t.islandSmoothTo01;

            // Island center in continuous coords; sample at cell centers (x+0.5, y+0.5)
            float2 center = new float2(w * 0.5f, h * 0.5f);

            // --- Build a small coarse noise grid using ONLY ctx.Rng (deterministic) ---
            int cs = NoiseCellSize < 1 ? 1 : NoiseCellSize;
            int nw = (w / cs) + 2;
            int nh = (h / cs) + 2;

            NativeArray<float> noise = default;
            try
            {
                noise = new NativeArray<float>(nw * nh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                // Fill in row-major order => deterministic RNG consumption count and order
                for (int ny = 0; ny < nh; ny++)
                {
                    int row = ny * nw;
                    for (int nx = 0; nx < nw; nx++)
                    {
                        noise[row + nx] = ctx.Rng.NextFloat(); // [0..1)
                    }
                }

                // --- Main pass (row-major) ---
                float invQuant = (QuantSteps > 0) ? (1f / QuantSteps) : 0f;

                for (int y = 0; y < h; y++)
                {
                    int gy = y / cs;
                    float ty = ((y % cs) + 0.5f) / cs;

                    int baseRow = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        // Coarse value noise (bilinear in a fixed way; no neighbor reads on the authoritative grids)
                        int gx = x / cs;
                        float tx = ((x % cs) + 0.5f) / cs;

                        int i00 = gx + gy * nw;
                        int i10 = (gx + 1) + gy * nw;
                        int i01 = gx + (gy + 1) * nw;
                        int i11 = (gx + 1) + (gy + 1) * nw;

                        float n00 = noise[i00];
                        float n10 = noise[i10];
                        float n01 = noise[i01];
                        float n11 = noise[i11];

                        float nx0 = math.lerp(n00, n10, tx);
                        float nx1 = math.lerp(n01, n11, tx);
                        float n = math.lerp(nx0, nx1, ty); // [0..1)

                        // Radial falloff (squared distance)
                        float2 p = new float2(x + 0.5f, y + 0.5f);
                        float2 v = p - center;
                        float distSq = v.x * v.x + v.y * v.y;

                        float radial01Sq = math.saturate(distSq * invRadiusSq); // 0..1
                        float s = math.smoothstep(fromSq, toSq, radial01Sq);
                        float mask01 = 1f - s; // 1 in center, 0 toward/outside edge

                        // Height: base island mask + small noise (only inside island)
                        float h01 = mask01 + ((n - 0.5f) * NoiseAmplitude * mask01);
                        h01 = math.saturate(h01);

                        // Quantize deterministically (optional but helps threshold stability)
                        if (QuantSteps > 1)
                        {
                            // floor is deterministic; keeps [0..1] in discrete steps
                            h01 = math.floor(h01 * QuantSteps) * invQuant;
                        }

                        // Write authoritative outputs
                        height.Values[baseRow + x] = h01;

                        bool isLand = h01 >= waterThreshold;
                        land.SetUnchecked(x, y, isLand);
                    }
                }

                // --- DeepWater = border-connected NOT Land (deterministic flood fill) ---
                // Interpreting Land==solid, Water==traversable.
                MaskFloodFillOps2D.FloodFillBorderConnected_NotSolid(ref land, ref deepWater);
            }
            finally
            {
                if (noise.IsCreated) noise.Dispose();
            }
        }
    }
}
