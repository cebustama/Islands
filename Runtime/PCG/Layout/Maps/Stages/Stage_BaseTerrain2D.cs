using System;
using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F2c — Base terrain stage: ellipse + domain-warp silhouette, with optional external shape input.
    ///
    /// Writes:
    /// - Field: Height    (ScalarField2D) in [0..1]
    /// - Layer: Land      (MaskGrid2D)    where Height >= waterThreshold01
    /// - Layer: DeepWater (MaskGrid2D)    = border-connected NOT Land (stable flood fill)
    ///
    /// Shape pipeline — no shape input (F2b path, default):
    ///   1. Sample warpX / warpY from two low-frequency coarse noise grids (WarpCellSize).
    ///   2. Displace the pixel's sampling point by the warp vector * warpAmplitude.
    ///   3. Compute ellipse distance from the displaced point to map center,
    ///      with the x-axis scaled by 1/islandAspectRatio.
    ///   4. Apply smoothstep radial falloff on that ellipse distance.
    ///   5. Add small high-frequency height perturbation noise (NoiseCellSize) inside the island.
    ///
    /// Shape pipeline — external shape input (F2c path, opt-in):
    ///   1–3. All three RNG arrays (island noise, warpX, warpY) are still allocated and filled in
    ///        the same order, so downstream stages see an identical RNG state regardless of path.
    ///   4. mask01 = shape.GetUnchecked(x, y) ? 1f : 0f  (replaces ellipse+warp).
    ///   5. Add small high-frequency height perturbation noise (same as F2b path).
    ///
    /// Determinism:
    /// - Uses only ctx.Rng for all randomness (seed-driven, stage-order stable).
    /// - RNG consumption order: island noise → warpX noise → warpY noise.
    ///   This order is fixed regardless of tunable values or shape-input presence.
    /// - Row-major loops only; no HashSet/Dictionary traversal.
    ///
    /// OOB-safe:
    /// - No neighbor reads on authoritative grids in the height loop.
    /// - Warp displacement is applied only to the distance calculation (F2b path only).
    /// - Shape mask reads use GetUnchecked (caller guarantees matching dimensions).
    /// - DeepWater uses MaskFloodFillOps2D which is OOB-safe.
    ///
    /// Backward compatibility:
    /// - MapInputs.ShapeInput defaults to HasShape = false => F2b path => F2b goldens unchanged.
    /// - islandAspectRatio = 1.0 + warpAmplitude01 = 0.0 => geometrically identical circle
    ///   to the pre-F2b implementation. Goldens differ because warp arrays are always allocated
    ///   and filled from ctx.Rng even at zero amplitude.
    /// </summary>
    public sealed class Stage_BaseTerrain2D : IMapStage2D
    {
        public string Name => "base_terrain";

        // --- Island height perturbation noise (higher frequency) ---
        private const int NoiseCellSize = 8;
        private const float NoiseAmplitude = 0.18f;

        // Deterministic quantization steps.
        private const int QuantSteps = 1024;

        // --- Domain warp noise (lower frequency) ---
        // Fixed constant: RNG count is tunable-independent and shape-path-independent.
        private const int WarpCellSize = 16;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            // --- F2c: shape-input guard ---
            bool useShape = inputs.ShapeInput.HasShape;
            if (useShape)
            {
                var sm = inputs.ShapeInput.Mask;
                if (sm.Domain.Width != w || sm.Domain.Height != h)
                    throw new ArgumentException(
                        $"ShapeInput dimensions ({sm.Domain.Width}x{sm.Domain.Height}) " +
                        $"must match pipeline domain ({w}x{h}).",
                        nameof(inputs));
            }

            ref ScalarField2D height = ref ctx.EnsureField(MapFieldId.Height, clearToZero: true);
            ref MaskGrid2D land = ref ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);
            ref MaskGrid2D deepWater = ref ctx.EnsureLayer(MapLayerId.DeepWater, clearToZero: true);

            var t = inputs.Tunables;
            float waterThreshold = t.waterThreshold01;

            // --- Shared geometry (F2b path; computed regardless so variables are in scope) ---
            float minDim = math.min((float)w, (float)h);
            float radius = math.max(1f, minDim * t.islandRadius01);
            float invRadiusSq = 1f / (radius * radius);

            float fromSq = t.islandSmoothFrom01 * t.islandSmoothFrom01;
            float toSq = t.islandSmoothTo01 * t.islandSmoothTo01;

            float2 center = new float2(w * 0.5f, h * 0.5f);

            float aspect = t.islandAspectRatio;
            float invAspectSq = 1f / (aspect * aspect);

            float warpAmp = t.warpAmplitude01 * minDim;

            // --- Noise grid dimensions ---
            int cs = NoiseCellSize;
            int nw = (w / cs) + 2;
            int nh = (h / cs) + 2;

            int wcs = WarpCellSize;
            int mw = (w / wcs) + 2;
            int mh = (h / wcs) + 2;

            float invQuant = (QuantSteps > 1) ? (1f / QuantSteps) : 0f;

            NativeArray<float> noise = default;
            NativeArray<float> warpX = default;
            NativeArray<float> warpY = default;
            try
            {
                noise = new NativeArray<float>(nw * nh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpX = new NativeArray<float>(mw * mh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpY = new NativeArray<float>(mw * mh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                // Fill in row-major order.
                // ALL THREE arrays are always filled regardless of useShape or tunable values,
                // so downstream stages always see the same RNG state.
                for (int i = 0; i < noise.Length; i++) noise[i] = ctx.Rng.NextFloat();
                for (int i = 0; i < warpX.Length; i++) warpX[i] = ctx.Rng.NextFloat();
                for (int i = 0; i < warpY.Length; i++) warpY[i] = ctx.Rng.NextFloat();

                // Cache shape mask locally to avoid repeated struct copies in the hot loop.
                var shapeMask = useShape ? inputs.ShapeInput.Mask : default;

                // --- Main pass: row-major ---
                for (int y = 0; y < h; y++)
                {
                    int baseRow = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        // Island height perturbation noise (always sampled regardless of path).
                        float n = BilinearSample(noise, x, y, cs, nw);

                        float mask01;

                        if (useShape)
                        {
                            // F2c path: external shape drives silhouette.
                            // warpX / warpY arrays filled but not read here (RNG count preserved).
                            mask01 = shapeMask.GetUnchecked(x, y) ? 1f : 0f;
                        }
                        else
                        {
                            // F2b path: ellipse + domain warp.
                            float wx = BilinearSample(warpX, x, y, wcs, mw) * 2f - 1f;
                            float wy = BilinearSample(warpY, x, y, wcs, mw) * 2f - 1f;

                            float2 p = new float2(x + 0.5f, y + 0.5f);
                            float2 pw = p + new float2(wx, wy) * warpAmp;

                            float2 v = pw - center;
                            float distSq = v.x * v.x * invAspectSq + v.y * v.y;

                            float radial01Sq = math.saturate(distSq * invRadiusSq);
                            float s = math.smoothstep(fromSq, toSq, radial01Sq);
                            mask01 = 1f - s;
                        }

                        // Height = island mask + noise perturbation (inside island only).
                        float h01 = mask01 + (n - 0.5f) * NoiseAmplitude * mask01;
                        h01 = math.saturate(h01);

                        if (QuantSteps > 1)
                            h01 = math.floor(h01 * QuantSteps) * invQuant;

                        height.Values[baseRow + x] = h01;
                        land.SetUnchecked(x, y, h01 >= waterThreshold);
                    }
                }

                MaskFloodFillOps2D.FloodFillBorderConnected_NotSolid(ref land, ref deepWater);
            }
            finally
            {
                if (noise.IsCreated) noise.Dispose();
                if (warpX.IsCreated) warpX.Dispose();
                if (warpY.IsCreated) warpY.Dispose();
            }
        }

        /// <summary>
        /// Bilinear sample from a coarse noise grid at pixel position (px, py).
        /// </summary>
        private static float BilinearSample(
            NativeArray<float> grid, int px, int py, int cellSize, int gridWidth)
        {
            int gx = px / cellSize;
            float tx = ((px % cellSize) + 0.5f) / cellSize;

            int gy = py / cellSize;
            float ty = ((py % cellSize) + 0.5f) / cellSize;

            float n00 = grid[gx + gy * gridWidth];
            float n10 = grid[(gx + 1) + gy * gridWidth];
            float n01 = grid[gx + (gy + 1) * gridWidth];
            float n11 = grid[(gx + 1) + (gy + 1) * gridWidth];

            return math.lerp(math.lerp(n00, n10, tx),
                             math.lerp(n01, n11, tx), ty);
        }
    }
}