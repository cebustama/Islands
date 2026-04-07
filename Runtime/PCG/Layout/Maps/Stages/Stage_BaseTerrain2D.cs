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
    /// N4 / F2c / J2 / N2 — Base terrain stage: ellipse + domain-warp silhouette, with optional
    /// external shape input, height redistribution, and spline remapping.
    ///
    /// Writes:
    /// - Field: Height    (ScalarField2D) in [0..1]
    /// - Layer: Land      (MaskGrid2D)    where Height >= waterThreshold01
    /// - Layer: DeepWater (MaskGrid2D)    = border-connected NOT Land (stable flood fill)
    ///
    /// Shape pipeline — no shape input (F2b path, default):
    ///   1. Sample warpX / warpY from coordinate-hashed noise via MapNoiseBridge2D.
    ///   2. Displace the pixel's sampling point by the warp vector * warpAmplitude.
    ///   3. Compute ellipse distance from the displaced point to map center,
    ///      with the x-axis scaled by 1/islandAspectRatio.
    ///   4. Apply smoothstep radial falloff on that ellipse distance.
    ///   5. Add height perturbation noise (coordinate-hashed) inside the island.
    ///
    /// Shape pipeline — external shape input (F2c path, opt-in):
    ///   1. Noise arrays are still filled (same bridge calls), so seed-derived fields
    ///      are always available for debugging / future use.
    ///   2. mask01 = shape.GetUnchecked(x, y) ? 1f : 0f  (replaces ellipse+warp).
    ///   3. Add height perturbation noise (same as F2b path).
    ///
    /// Height post-processing:
    ///   quantize → pow() redistribution (J2) → spline remap (N2) → Land threshold.
    ///
    /// Determinism:
    /// - All noise is generated via coordinate hashing (MapNoiseBridge2D.FillNoise01).
    ///   ctx.Rng is NOT consumed — downstream stages see it at its initial position.
    /// - Row-major loops only; no HashSet/Dictionary traversal.
    /// - Same seed + same settings => identical output regardless of execution environment.
    ///
    /// Phase N4: Replaced manual value noise (coarse grid of ctx.Rng.NextFloat() +
    /// bilinear interpolation) with proper noise runtime via MapNoiseBridge2D.
    /// Full golden break from pre-N4 output. Noise type, frequency, octaves, etc.
    /// are configurable via TerrainNoiseSettings on MapTunables2D.
    /// </summary>
    public sealed class Stage_BaseTerrain2D : IMapStage2D
    {
        public string Name => "base_terrain";

        // Stage salts for decorrelating noise fields via coordinate hashing.
        private const uint TerrainNoiseSalt = 0xF2A10001u;
        private const uint WarpXNoiseSalt = 0xF2A20002u;
        private const uint WarpYNoiseSalt = 0xF2A30003u;

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

            // J2: height redistribution exponent.
            float redistExp = t.heightRedistributionExponent;

            // N2: spline remapping.
            var heightSpline = t.heightRemapSpline;
            bool applySpline = !heightSpline.IsIdentity;

            // N4: noise settings from tunables.
            var terrainNoise = t.terrainNoise;
            var warpNoise = t.warpNoise;
            int quantSteps = t.heightQuantSteps;
            float terrainAmp = math.max(0f, terrainNoise.amplitude);
            float invQuant = (quantSteps > 1) ? (1f / quantSteps) : 0f;

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

            // --- N4: fill noise arrays via coordinate hashing (no ctx.Rng consumption) ---
            int cellCount = w * h;
            NativeArray<float> noise = default;
            NativeArray<float> warpX = default;
            NativeArray<float> warpY = default;
            try
            {
                noise = new NativeArray<float>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpX = new NativeArray<float>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpY = new NativeArray<float>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                MapNoiseBridge2D.FillNoise01(in d, noise, inputs.Seed, TerrainNoiseSalt, in terrainNoise);
                MapNoiseBridge2D.FillNoise01(in d, warpX, inputs.Seed, WarpXNoiseSalt, in warpNoise);
                MapNoiseBridge2D.FillNoise01(in d, warpY, inputs.Seed, WarpYNoiseSalt, in warpNoise);

                // Cache shape mask locally to avoid repeated struct copies in the hot loop.
                var shapeMask = useShape ? inputs.ShapeInput.Mask : default;

                // --- Main pass: row-major ---
                for (int y = 0; y < h; y++)
                {
                    int baseRow = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        int idx = baseRow + x;

                        // Height perturbation noise (always read regardless of path).
                        float n = noise[idx];

                        float mask01;

                        if (useShape)
                        {
                            // F2c path: external shape drives silhouette.
                            // warpX / warpY arrays filled but not read here.
                            mask01 = shapeMask.GetUnchecked(x, y) ? 1f : 0f;
                        }
                        else
                        {
                            // F2b path: ellipse + domain warp.
                            float wx = warpX[idx] * 2f - 1f;
                            float wy = warpY[idx] * 2f - 1f;

                            float2 p = new float2(x + 0.5f, y + 0.5f);
                            float2 pw = p + new float2(wx, wy) * warpAmp;

                            float2 v = pw - center;
                            float distSq = v.x * v.x * invAspectSq + v.y * v.y;

                            float radial01Sq = math.saturate(distSq * invRadiusSq);
                            float s = math.smoothstep(fromSq, toSq, radial01Sq);
                            mask01 = 1f - s;
                        }

                        // Height = island mask + noise perturbation (inside island only).
                        float h01 = mask01 + (n - 0.5f) * terrainAmp * mask01;
                        h01 = math.saturate(h01);

                        if (quantSteps > 1)
                            h01 = math.floor(h01 * quantSteps) * invQuant;

                        // J2: power redistribution — reshape height distribution.
                        // pow(x, 1.0) == x, so default exponent preserves existing goldens.
                        if (redistExp != 1.0f)
                            h01 = math.pow(h01, redistExp);

                        // N2: spline remapping — arbitrary piecewise-linear curve reshaping.
                        // Identity spline (or default with null arrays) preserves all goldens.
                        if (applySpline)
                            h01 = heightSpline.Evaluate(h01);

                        height.Values[idx] = h01;
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
    }
}