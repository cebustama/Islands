using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;
using Islands.PCG.Operators;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Lantern/Visualization testbed for the Map Pipeline by Layers.
    ///
    /// Current sample path:
    /// - configurable BaseTerrain-like sample stage for F2 shape tuning (ellipse + domain warp)
    /// - optional governed F3 Stage_Hills2D appended after base terrain
    /// - optional governed F4 Stage_Shore2D appended after hills
    /// - optional governed F5 Stage_Vegetation2D appended after shore
    /// - optional governed F6 Stage_Traversal2D appended after vegetation
    /// - optional governed Phase G Stage_Morphology2D appended after traversal
    /// - displays the selected MaskGrid2D layer via GPU buffer packing
    ///
    /// F2b: added islandAspectRatio and warpAmplitude01 Inspector fields.
    ///      BaseTerrainStage_Configurable updated to mirror Stage_BaseTerrain2D shape pipeline.
    /// </summary>
    public sealed class PCGMapVisualization : Visualization
    {
        private static readonly int NoiseId = Shader.PropertyToID("_Noise");
        private static readonly int MaskOffColorId = Shader.PropertyToID("_MaskOffColor");
        private static readonly int MaskOnColorId = Shader.PropertyToID("_MaskOnColor");

        [Header("Run Inputs")]
        [Tooltip("Semilla determinista (uint). Misma semilla => mismo mapa.")]
        [SerializeField] private uint seed = 1u;

        [Header("Pipeline")]
        [Tooltip("Si est? activo, a?ade la etapa F3 Hills + topology despu?s del terreno base.")]
        [SerializeField] private bool enableHillsStage = true;

        [Tooltip("Si est? activo, a?ade la etapa F4 Shore (ShallowWater) despu?s de Hills.\n" +
                 "Requiere Enable Hills Stage activo para resultados correctos.")]
        [SerializeField] private bool enableShoreStage = true;

        [Tooltip("Si est? activo, a?ade la etapa F5 Vegetation despu?s de Shore.\n" +
                 "Requiere Enable Shore Stage activo para resultados correctos.")]
        [SerializeField] private bool enableVegetationStage = true;

        [Tooltip("Si est? activo, a?ade la etapa F6 Traversal (Walkable + Stairs) despu?s de Vegetation.\n" +
                 "Requiere Enable Vegetation Stage activo para resultados correctos.")]
        [SerializeField] private bool enableTraversalStage = true;

        [Tooltip("Si est? activo, a?ade la etapa Phase G Morphology (LandCore + CoastDist) despu?s de Traversal.\n" +
                 "Requiere Enable Traversal Stage activo para resultados correctos.")]
        [SerializeField] private bool enableMorphologyStage = true;

        [Header("Layer View")]
        [Tooltip("Qu? capa (MaskGrid2D) quieres visualizar.\n" +
                 "Capas F2: Land, DeepWater.\n" +
                 "Capas F3: LandEdge, LandInterior, HillsL1, HillsL2.\n" +
                 "Capas F4: ShallowWater.\n" +
                 "Capas F5: Vegetation.\n" +
                 "Capas F6: Walkable, Stairs.\n" +
                 "Capas Phase G: LandCore.\n" +
                 "Si la capa no existe a?n, se muestra todo OFF.")]
        [SerializeField] private MapLayerId viewLayer = MapLayerId.Land;

        [Header("Palette (0/1)")]
        [SerializeField] private Color maskOffColor = new Color(0.1f, 0.2f, 0.7f, 1f);
        [SerializeField] private Color maskOnColor = new Color(0.0f, 0.4f, 0.0f, 1f);

        [Header("F2 Tunables (Shape + Threshold)")]
        [Range(0f, 1f)]
        [SerializeField] private float islandRadius01 = 0.45f;

        [Range(0f, 1f)]
        [SerializeField] private float waterThreshold01 = 0.50f;

        [Range(0f, 1f)]
        [SerializeField] private float islandSmoothFrom01 = 0.30f;

        [Range(0f, 1f)]
        [SerializeField] private float islandSmoothTo01 = 0.70f;

        [Header("F2 Tunables (Island Shape — Ellipse + Warp)")]
        [Tooltip("Ellipse aspect ratio. 1.0 = circle. >1 = wider. <1 = taller. Range [0.25..4.0].")]
        [Range(0.25f, 4f)]
        [SerializeField] private float islandAspectRatio = 1.00f;

        [Tooltip("Domain warp amplitude as a fraction of map size. " +
                 "0 = no warp (pure circle/ellipse). ~0.15 = subtle organic coast. ~0.30 = strong bays.")]
        [Range(0f, 1f)]
        [SerializeField] private float warpAmplitude01 = 0.00f;

        [Header("F2 Tunables (Noise Inside Island)")]
        [Min(1)]
        [SerializeField] private int noiseCellSize = 8;

        [Range(0f, 1f)]
        [SerializeField] private float noiseAmplitude = 0.18f;

        [Min(0)]
        [SerializeField] private int quantSteps = 1024;

        [Header("Run Behavior")]
        [SerializeField] private bool clearBeforeRun = true;

        private NativeArray<float4> packedNoise;
        private ComputeBuffer noiseBuffer;
        private MaterialPropertyBlock mpb;

        private MapContext2D ctx;
        private int ctxResolution = -1;
        private bool dirty = true;

        private uint lastSeed;
        private bool lastEnableHillsStage;
        private bool lastEnableShoreStage;
        private bool lastEnableVegetationStage;
        private bool lastEnableTraversalStage;
        private bool lastEnableMorphologyStage;
        private MapLayerId lastViewLayer;
        private Color lastMaskOffColor;
        private Color lastMaskOnColor;
        private float lastIslandRadius01;
        private float lastWaterThreshold01;
        private float lastIslandSmoothFrom01;
        private float lastIslandSmoothTo01;
        private float lastIslandAspectRatio;
        private float lastWarpAmplitude01;
        private int lastNoiseCellSize;
        private float lastNoiseAmplitude;
        private int lastQuantSteps;
        private bool lastClearBeforeRun;

        private bool loggedFirstUpdate = false;
        private int updateCalls = 0;

        private BaseTerrainStage_Configurable baseStage;
        private Stage_Hills2D hillsStage;
        private Stage_Shore2D shoreStage;
        private Stage_Vegetation2D vegetationStage;
        private Stage_Traversal2D traversalStage;
        private Stage_Morphology2D morphologyStage;
        private IMapStage2D[] stagesF2;
        private IMapStage2D[] stagesF3;
        private IMapStage2D[] stagesF4;
        private IMapStage2D[] stagesF5;
        private IMapStage2D[] stagesF6;
        private IMapStage2D[] stagesG;

        /// <summary>
        /// Inspector-configurable base terrain stage for live preview tuning.
        /// Mirrors Stage_BaseTerrain2D exactly: ellipse + domain warp + height perturbation.
        /// Additional fields (noiseCellSize, noiseAmplitude, quantSteps) override the
        /// constants baked into the governed stage, enabling real-time tuning in the lantern.
        ///
        /// Shape tunables (islandAspectRatio, warpAmplitude01) are read from inputs.Tunables,
        /// same as the governed stage — no special bridging required.
        ///
        /// IMPORTANT: Keep this class in sync with Stage_BaseTerrain2D whenever the shape
        /// pipeline changes. The two implementations must produce identical outputs for the
        /// same inputs and RNG state.
        /// </summary>
        private sealed class BaseTerrainStage_Configurable : IMapStage2D
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

        protected override void EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock)
        {
            mpb = propertyBlock;

            packedNoise = new NativeArray<float4>(dataLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            noiseBuffer = new ComputeBuffer(dataLength * 4, sizeof(float));
            mpb.SetBuffer(NoiseId, noiseBuffer);

            ApplyPaletteToMpb();

            baseStage = new BaseTerrainStage_Configurable();
            hillsStage = new Stage_Hills2D();
            shoreStage = new Stage_Shore2D();
            vegetationStage = new Stage_Vegetation2D();
            traversalStage = new Stage_Traversal2D();
            morphologyStage = new Stage_Morphology2D();

            stagesF2 = new IMapStage2D[1] { baseStage };
            stagesF3 = new IMapStage2D[2] { baseStage, hillsStage };
            stagesF4 = new IMapStage2D[3] { baseStage, hillsStage, shoreStage };
            stagesF5 = new IMapStage2D[4] { baseStage, hillsStage, shoreStage, vegetationStage };
            stagesF6 = new IMapStage2D[5] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage };
            stagesG = new IMapStage2D[6] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage };

            CacheParams();
            dirty = true;
            loggedFirstUpdate = false;
            updateCalls = 0;
        }

        protected override void DisableVisualization()
        {
            if (packedNoise.IsCreated) packedNoise.Dispose();

            if (noiseBuffer != null)
            {
                noiseBuffer.Release();
                noiseBuffer = null;
            }

            ctx?.Dispose();
            ctx = null;

            ctxResolution = -1;
            mpb = null;

            baseStage = null;
            hillsStage = null;
            shoreStage = null;
            vegetationStage = null;
            traversalStage = null;
            morphologyStage = null;
            stagesF2 = null;
            stagesF3 = null;
            stagesF4 = null;
            stagesF5 = null;
            stagesF6 = null;
            stagesG = null;
        }

        protected override void UpdateVisualization(
            NativeArray<float3x4> positions,
            int resolution,
            JobHandle handle)
        {
            handle.Complete();

            updateCalls++;
            ApplyPaletteToMpb();
            EnsureContextAllocated(resolution);

            if (ParamsChanged())
            {
                CacheParams();
                dirty = true;
            }

            if (dirty)
            {
                baseStage.noiseCellSize = Mathf.Max(1, noiseCellSize);
                baseStage.noiseAmplitude = Mathf.Max(0f, noiseAmplitude);
                baseStage.quantSteps = Mathf.Max(0, quantSteps);

                var tunables = new MapTunables2D(
                    islandRadius01: islandRadius01,
                    waterThreshold01: waterThreshold01,
                    islandSmoothFrom01: islandSmoothFrom01,
                    islandSmoothTo01: islandSmoothTo01,
                    islandAspectRatio: islandAspectRatio,
                    warpAmplitude01: warpAmplitude01);

                var inputs = new MapInputs(
                    seed: seed,
                    domain: new GridDomain2D(resolution, resolution),
                    tunables: tunables);

                var stages = enableMorphologyStage ? stagesG
                           : enableTraversalStage ? stagesF6
                           : enableVegetationStage ? stagesF5
                           : enableShoreStage ? stagesF4
                           : enableHillsStage ? stagesF3
                           : stagesF2;

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: clearBeforeRun);

                dirty = false;
            }

            if (ctx.IsLayerCreated(viewLayer))
            {
                ref var layer = ref ctx.GetLayer(viewLayer);
                PackFromMaskAndUpload(ref layer, resolution);
            }
            else
            {
                PackZerosAndUpload();
            }

            if (!loggedFirstUpdate)
            {
                loggedFirstUpdate = true;
                ulong h = ctx.IsLayerCreated(viewLayer)
                    ? ctx.GetLayer(viewLayer).SnapshotHash64()
                    : 0ul;

                Debug.Log(
                    $"[PCGMapVisualization] Update #{updateCalls} res={resolution} seed={seed} " +
                    $"aspect={islandAspectRatio:F2} warp={warpAmplitude01:F2} " +
                    $"hills={enableHillsStage} shore={enableShoreStage} " +
                    $"veg={enableVegetationStage} traversal={enableTraversalStage} " +
                    $"morphology={enableMorphologyStage} view={viewLayer} hash={h:X16}");
            }
        }

        private void EnsureContextAllocated(int resolution)
        {
            if (ctx != null && ctxResolution == resolution) return;

            ctx?.Dispose();
            ctx = null;

            ctxResolution = resolution;
            ctx = new MapContext2D(new GridDomain2D(resolution, resolution), Allocator.Persistent);
            dirty = true;
        }

        private void PackZerosAndUpload()
        {
            for (int i = 0; i < packedNoise.Length; i++)
                packedNoise[i] = default;

            noiseBuffer.SetData(packedNoise.Reinterpret<float>(sizeof(float) * 4));
        }

        private void PackFromMaskAndUpload(ref MaskGrid2D mask, int resolution)
        {
            int totalInstances = resolution * resolution;
            int packs = packedNoise.Length;

            for (int packIndex = 0; packIndex < packs; packIndex++)
            {
                int baseInstance = packIndex * 4;

                float v0 = (baseInstance + 0 < totalInstances) ? MaskInstanceValue(ref mask, baseInstance + 0, resolution) : 0f;
                float v1 = (baseInstance + 1 < totalInstances) ? MaskInstanceValue(ref mask, baseInstance + 1, resolution) : 0f;
                float v2 = (baseInstance + 2 < totalInstances) ? MaskInstanceValue(ref mask, baseInstance + 2, resolution) : 0f;
                float v3 = (baseInstance + 3 < totalInstances) ? MaskInstanceValue(ref mask, baseInstance + 3, resolution) : 0f;

                packedNoise[packIndex] = new float4(v0, v1, v2, v3);
            }

            noiseBuffer.SetData(packedNoise.Reinterpret<float>(sizeof(float) * 4));
        }

        private static float MaskInstanceValue(ref MaskGrid2D mask, int instanceIndex, int resolution)
        {
            int x = instanceIndex % resolution;
            int y = instanceIndex / resolution;
            return mask.Get(x, y) ? 1f : 0f;
        }

        private void ApplyPaletteToMpb()
        {
            if (mpb == null) return;
            mpb.SetColor(MaskOffColorId, maskOffColor);
            mpb.SetColor(MaskOnColorId, maskOnColor);
        }

        private void CacheParams()
        {
            lastSeed = seed;
            lastEnableHillsStage = enableHillsStage;
            lastEnableShoreStage = enableShoreStage;
            lastEnableVegetationStage = enableVegetationStage;
            lastEnableTraversalStage = enableTraversalStage;
            lastEnableMorphologyStage = enableMorphologyStage;
            lastViewLayer = viewLayer;
            lastMaskOffColor = maskOffColor;
            lastMaskOnColor = maskOnColor;
            lastIslandRadius01 = islandRadius01;
            lastWaterThreshold01 = waterThreshold01;
            lastIslandSmoothFrom01 = islandSmoothFrom01;
            lastIslandSmoothTo01 = islandSmoothTo01;
            lastIslandAspectRatio = islandAspectRatio;
            lastWarpAmplitude01 = warpAmplitude01;
            lastNoiseCellSize = noiseCellSize;
            lastNoiseAmplitude = noiseAmplitude;
            lastQuantSteps = quantSteps;
            lastClearBeforeRun = clearBeforeRun;
        }

        private bool ParamsChanged()
        {
            return seed != lastSeed
                || enableHillsStage != lastEnableHillsStage
                || enableShoreStage != lastEnableShoreStage
                || enableVegetationStage != lastEnableVegetationStage
                || enableTraversalStage != lastEnableTraversalStage
                || enableMorphologyStage != lastEnableMorphologyStage
                || viewLayer != lastViewLayer
                || maskOffColor != lastMaskOffColor
                || maskOnColor != lastMaskOnColor
                || !Mathf.Approximately(islandRadius01, lastIslandRadius01)
                || !Mathf.Approximately(waterThreshold01, lastWaterThreshold01)
                || !Mathf.Approximately(islandSmoothFrom01, lastIslandSmoothFrom01)
                || !Mathf.Approximately(islandSmoothTo01, lastIslandSmoothTo01)
                || !Mathf.Approximately(islandAspectRatio, lastIslandAspectRatio)
                || !Mathf.Approximately(warpAmplitude01, lastWarpAmplitude01)
                || noiseCellSize != lastNoiseCellSize
                || !Mathf.Approximately(noiseAmplitude, lastNoiseAmplitude)
                || quantSteps != lastQuantSteps
                || clearBeforeRun != lastClearBeforeRun;
        }
    }
}