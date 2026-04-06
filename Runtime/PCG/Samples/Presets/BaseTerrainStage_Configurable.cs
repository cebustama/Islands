using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Inspector-configurable base terrain stage for live preview tuning.
    /// Mirrors <see cref="Islands.PCG.Layout.Maps.Stages.Stage_BaseTerrain2D"/> exactly:
    /// ellipse + domain warp + height perturbation + J2 height redistribution.
    ///
    /// Three fields (<see cref="noiseCellSize"/>, <see cref="noiseAmplitude"/>,
    /// <see cref="quantSteps"/>) override the constants baked into the governed stage,
    /// enabling real-time tuning in the lantern / live visualization.
    ///
    /// Shape tunables (islandAspectRatio, warpAmplitude01, heightRedistributionExponent)
    /// are read from <c>inputs.Tunables</c>, same as the governed stage.
    ///
    /// IMPORTANT: Keep this class in sync with Stage_BaseTerrain2D whenever the shape
    /// pipeline changes. The two implementations must produce identical outputs for the
    /// same inputs, RNG state, and configurable field values matching the governed constants.
    ///
    /// Consumers: PCGMapVisualization, PCGMapCompositeVisualization, PCGMapTilemapVisualization.
    /// Previously each consumer held a private nested copy; consolidated in J2.
    ///
    /// Lives in Islands.PCG.Samples.Shared asmdef (Runtime/PCG/Samples/Presets/).
    /// Sample-side only — not a runtime pipeline contract.
    /// </summary>
    public sealed class BaseTerrainStage_Configurable : IMapStage2D
    {
        public string Name => "base_terrain_configurable";

        // Overrides for the constants baked into Stage_BaseTerrain2D.
        public int noiseCellSize;
        public float noiseAmplitude;
        public int quantSteps;

        // WarpCellSize matches Stage_BaseTerrain2D constant (must stay in sync).
        private const int WarpCellSize = 16;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref ScalarField2D height = ref ctx.EnsureField(MapFieldId.Height, clearToZero: true);
            ref MaskGrid2D land = ref ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);
            ref MaskGrid2D deepWater = ref ctx.EnsureLayer(MapLayerId.DeepWater, clearToZero: true);

            var t = inputs.Tunables;
            float waterThreshold = t.waterThreshold01;

            // J2: height redistribution exponent.
            float redistExp = t.heightRedistributionExponent;

            float minDim = math.min((float)w, (float)h);
            float radius = math.max(1f, minDim * t.islandRadius01);
            float invRadiusSq = 1f / (radius * radius);
            float fromSq = t.islandSmoothFrom01 * t.islandSmoothFrom01;
            float toSq = t.islandSmoothTo01 * t.islandSmoothTo01;
            float2 center = new float2(w * 0.5f, h * 0.5f);

            float aspect = t.islandAspectRatio;
            float invAspectSq = 1f / (aspect * aspect);
            float warpAmp = t.warpAmplitude01 * minDim;

            int cs = noiseCellSize < 1 ? 1 : noiseCellSize;
            float amp = math.max(0f, noiseAmplitude);
            int qs = quantSteps;

            int nw = (w / cs) + 2;
            int nh = (h / cs) + 2;
            int wcs = WarpCellSize;
            int mw = (w / wcs) + 2;
            int mh = (h / wcs) + 2;

            float invQuant = (qs > 1) ? (1f / qs) : 0f;

            NativeArray<float> noise = default;
            NativeArray<float> warpX = default;
            NativeArray<float> warpY = default;
            try
            {
                noise = new NativeArray<float>(nw * nh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpX = new NativeArray<float>(mw * mh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpY = new NativeArray<float>(mw * mh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                // Fill order matches Stage_BaseTerrain2D exactly (island noise, warpX, warpY).
                for (int i = 0; i < noise.Length; i++) noise[i] = ctx.Rng.NextFloat();
                for (int i = 0; i < warpX.Length; i++) warpX[i] = ctx.Rng.NextFloat();
                for (int i = 0; i < warpY.Length; i++) warpY[i] = ctx.Rng.NextFloat();

                for (int y = 0; y < h; y++)
                {
                    int baseRow = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        float n = BilinearSample(noise, x, y, cs, nw);
                        float wx = BilinearSample(warpX, x, y, wcs, mw) * 2f - 1f;
                        float wy = BilinearSample(warpY, x, y, wcs, mw) * 2f - 1f;

                        float2 p = new float2(x + 0.5f, y + 0.5f);
                        float2 pw = p + new float2(wx, wy) * warpAmp;

                        float2 v = pw - center;
                        float distSq = v.x * v.x * invAspectSq + v.y * v.y;

                        float radial01Sq = math.saturate(distSq * invRadiusSq);
                        float s = math.smoothstep(fromSq, toSq, radial01Sq);
                        float mask01 = 1f - s;

                        float h01 = mask01 + (n - 0.5f) * amp * mask01;
                        h01 = math.saturate(h01);

                        if (qs > 1)
                            h01 = math.floor(h01 * qs) * invQuant;

                        // J2: power redistribution — reshape height distribution.
                        // pow(x, 1.0) == x, so default exponent preserves all existing goldens.
                        if (redistExp != 1.0f)
                            h01 = math.pow(h01, redistExp);

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

        // Matches Stage_BaseTerrain2D.BilinearSample exactly.
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