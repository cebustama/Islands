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
    /// coordinate-hashed noise via MapNoiseBridge2D + configurable base shape + domain warp +
    /// height perturbation + J2 height redistribution + N2 spline remap.
    ///
    /// Configurable fields override the defaults baked into the governed stage,
    /// enabling real-time tuning in the lantern / live visualization.
    ///
    /// Shape tunables (islandAspectRatio, warpAmplitude01, heightRedistributionExponent,
    /// heightRemapSpline) are read from <c>inputs.Tunables</c>, same as the governed stage.
    ///
    /// N5.a: shapeMode read from <c>inputs.Tunables.shapeMode</c>. Supports Ellipse (default),
    /// Rectangle, NoShape, and Custom (falls back to Ellipse). F2c external shape not supported
    /// in the configurable stage (lanterns do not inject MapShapeInput).
    ///
    /// IMPORTANT: Keep this class in sync with Stage_BaseTerrain2D whenever the shape
    /// pipeline changes. The two implementations must produce identical outputs for the
    /// same inputs when configurable field values match the governed defaults.
    ///
    /// Phase N4: Replaced RNG arrays + BilinearSample with MapNoiseBridge2D.FillNoise01.
    /// ctx.Rng is no longer consumed. Old noiseCellSize/noiseAmplitude/quantSteps fields
    /// replaced by terrainNoise, warpNoise, heightQuantSteps.
    ///
    /// Phase N5.a: Added IslandShapeMode switch matching the governed stage.
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

        // N4: configurable noise settings (override governed defaults for live tuning).
        public TerrainNoiseSettings terrainNoise = TerrainNoiseSettings.DefaultTerrain;
        public TerrainNoiseSettings warpNoise = TerrainNoiseSettings.DefaultWarp;
        public int heightQuantSteps = 1024;

        // Stage salts — must match Stage_BaseTerrain2D exactly.
        private const uint TerrainNoiseSalt = 0xF2A10001u;
        private const uint WarpXNoiseSalt = 0xF2A20002u;
        private const uint WarpYNoiseSalt = 0xF2A30003u;

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

            // N5.a: shape mode from tunables.
            IslandShapeMode shapeMode = t.shapeMode;

            // J2: height redistribution exponent.
            float redistExp = t.heightRedistributionExponent;

            // N2: spline remapping.
            var heightSpline = t.heightRemapSpline;
            bool applySpline = !heightSpline.IsIdentity;

            // N4: use local configurable settings (not from tunables).
            float terrainAmp = math.max(0f, terrainNoise.amplitude);
            int qs = heightQuantSteps;
            float invQuant = (qs > 1) ? (1f / qs) : 0f;

            float minDim = math.min((float)w, (float)h);
            float radius = math.max(1f, minDim * t.islandRadius01);
            float invRadiusSq = 1f / (radius * radius);
            float fromSq = t.islandSmoothFrom01 * t.islandSmoothFrom01;
            float toSq = t.islandSmoothTo01 * t.islandSmoothTo01;
            float2 center = new float2(w * 0.5f, h * 0.5f);

            float aspect = t.islandAspectRatio;
            float invAspectSq = 1f / (aspect * aspect);
            float warpAmp = t.warpAmplitude01 * minDim;

            // N5.a Rectangle: half-extents.
            float rectHalfX = radius * aspect;
            float rectHalfY = radius;
            float invRectHalfX = rectHalfX > 0f ? 1f / rectHalfX : 0f;
            float invRectHalfY = rectHalfY > 0f ? 1f / rectHalfY : 0f;

            int cellCount = w * h;
            NativeArray<float> noise = default;
            NativeArray<float> warpX = default;
            NativeArray<float> warpY = default;
            try
            {
                noise = new NativeArray<float>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpX = new NativeArray<float>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                warpY = new NativeArray<float>(cellCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                // N4: coordinate-hashed noise — matches governed stage exactly.
                MapNoiseBridge2D.FillNoise01(in d, noise, inputs.Seed, TerrainNoiseSalt, in terrainNoise);
                MapNoiseBridge2D.FillNoise01(in d, warpX, inputs.Seed, WarpXNoiseSalt, in warpNoise);
                MapNoiseBridge2D.FillNoise01(in d, warpY, inputs.Seed, WarpYNoiseSalt, in warpNoise);

                for (int y = 0; y < h; y++)
                {
                    int baseRow = y * w;

                    for (int x = 0; x < w; x++)
                    {
                        int idx = baseRow + x;
                        float n = noise[idx];

                        float h01;

                        // N5.a: NoShape — height IS pure noise.
                        if (shapeMode == IslandShapeMode.NoShape)
                        {
                            h01 = n;
                        }
                        else if (shapeMode == IslandShapeMode.Rectangle)
                        {
                            // N5.a Rectangle path: axis-aligned rectangle + domain warp.
                            float wx = warpX[idx] * 2f - 1f;
                            float wy = warpY[idx] * 2f - 1f;

                            float2 p = new float2(x + 0.5f, y + 0.5f);
                            float2 pw = p + new float2(wx, wy) * warpAmp;

                            float2 v = pw - center;

                            // Chebyshev-normalized distance: 0 at center, 1 at rectangle edge.
                            float fracX = math.abs(v.x) * invRectHalfX;
                            float fracY = math.abs(v.y) * invRectHalfY;
                            float rectDist01 = math.max(fracX, fracY);

                            float rectDistSq = rectDist01 * rectDist01;
                            float s = math.smoothstep(fromSq, toSq, rectDistSq);
                            float mask01 = 1f - s;

                            h01 = mask01 + (n - 0.5f) * terrainAmp * mask01;
                        }
                        else
                        {
                            // Ellipse path (default): also used as Custom fallback.
                            float wx = warpX[idx] * 2f - 1f;
                            float wy = warpY[idx] * 2f - 1f;

                            float2 p = new float2(x + 0.5f, y + 0.5f);
                            float2 pw = p + new float2(wx, wy) * warpAmp;

                            float2 v = pw - center;
                            float distSq = v.x * v.x * invAspectSq + v.y * v.y;

                            float radial01Sq = math.saturate(distSq * invRadiusSq);
                            float s = math.smoothstep(fromSq, toSq, radial01Sq);
                            float mask01 = 1f - s;

                            h01 = mask01 + (n - 0.5f) * terrainAmp * mask01;
                        }

                        h01 = math.saturate(h01);

                        if (qs > 1)
                            h01 = math.floor(h01 * qs) * invQuant;

                        // J2: power redistribution.
                        if (redistExp != 1.0f)
                            h01 = math.pow(h01, redistExp);

                        // N2: spline remapping.
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