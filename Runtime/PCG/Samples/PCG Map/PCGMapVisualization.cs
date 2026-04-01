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
    /// - configurable BaseTerrain-like sample stage for F2 shape tuning
    /// - optional governed F3 Stage_Hills2D appended after base terrain
    /// - displays the selected MaskGrid2D layer via GPU buffer packing
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
        [Tooltip("Si está activo, añade la etapa F3 Hills + topology después del terreno base.")]
        [SerializeField] private bool enableHillsStage = true;

        [Header("Layer View")]
        [Tooltip("Qué capa (MaskGrid2D) quieres visualizar.\n" +
                 "Capas F2: Land, DeepWater.\n" +
                 "Capas F3: LandEdge, LandInterior, HillsL1, HillsL2.\n" +
                 "Si la capa no existe aún, se muestra todo OFF.")]
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
        private MapLayerId lastViewLayer;
        private Color lastMaskOffColor;
        private Color lastMaskOnColor;
        private float lastIslandRadius01;
        private float lastWaterThreshold01;
        private float lastIslandSmoothFrom01;
        private float lastIslandSmoothTo01;
        private int lastNoiseCellSize;
        private float lastNoiseAmplitude;
        private int lastQuantSteps;
        private bool lastClearBeforeRun;

        private bool loggedFirstUpdate = false;
        private int updateCalls = 0;

        private BaseTerrainStage_Configurable baseStage;
        private Stage_Hills2D hillsStage;
        private IMapStage2D[] stagesF2;
        private IMapStage2D[] stagesF3;

        private sealed class BaseTerrainStage_Configurable : IMapStage2D
        {
            public string Name => "base_terrain_configurable";

            public int noiseCellSize;
            public float noiseAmplitude;
            public int quantSteps;

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
                float radius = minDim * t.islandRadius01;
                if (radius < 1f) radius = 1f;

                float invRadiusSq = 1f / (radius * radius);
                float fromSq = t.islandSmoothFrom01 * t.islandSmoothFrom01;
                float toSq = t.islandSmoothTo01 * t.islandSmoothTo01;
                float2 center = new float2(w * 0.5f, h * 0.5f);

                int cs = noiseCellSize < 1 ? 1 : noiseCellSize;
                float amp = math.max(0f, noiseAmplitude);
                int qs = quantSteps;

                int nw = (w / cs) + 2;
                int nh = (h / cs) + 2;

                NativeArray<float> noise = default;
                try
                {
                    noise = new NativeArray<float>(nw * nh, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                    for (int ny = 0; ny < nh; ny++)
                    {
                        int row = ny * nw;
                        for (int nx = 0; nx < nw; nx++)
                            noise[row + nx] = ctx.Rng.NextFloat();
                    }

                    float invQuant = (qs > 0) ? (1f / qs) : 0f;

                    for (int y = 0; y < h; y++)
                    {
                        int gy = y / cs;
                        float ty = ((y % cs) + 0.5f) / cs;
                        int baseRow = y * w;

                        for (int x = 0; x < w; x++)
                        {
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
                            float n = math.lerp(nx0, nx1, ty);

                            float2 p = new float2(x + 0.5f, y + 0.5f);
                            float2 v = p - center;
                            float distSq = v.x * v.x + v.y * v.y;

                            float radial01Sq = math.saturate(distSq * invRadiusSq);
                            float s = math.smoothstep(fromSq, toSq, radial01Sq);
                            float mask01 = 1f - s;

                            float h01 = mask01 + ((n - 0.5f) * amp * mask01);
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
                }
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
            stagesF2 = new IMapStage2D[1] { baseStage };
            stagesF3 = new IMapStage2D[2] { baseStage, hillsStage };

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
            stagesF2 = null;
            stagesF3 = null;
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
                    islandSmoothTo01: islandSmoothTo01);

                var inputs = new MapInputs(
                    seed: seed,
                    domain: new GridDomain2D(resolution, resolution),
                    tunables: tunables);

                var stages = enableHillsStage ? stagesF3 : stagesF2;
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

                Debug.Log($"[PCGMapVisualization] Update #{updateCalls} res={resolution} seed={seed} hills={enableHillsStage} view={viewLayer} hash={h:X16}");
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
            lastViewLayer = viewLayer;
            lastMaskOffColor = maskOffColor;
            lastMaskOnColor = maskOnColor;
            lastIslandRadius01 = islandRadius01;
            lastWaterThreshold01 = waterThreshold01;
            lastIslandSmoothFrom01 = islandSmoothFrom01;
            lastIslandSmoothTo01 = islandSmoothTo01;
            lastNoiseCellSize = noiseCellSize;
            lastNoiseAmplitude = noiseAmplitude;
            lastQuantSteps = quantSteps;
            lastClearBeforeRun = clearBeforeRun;
        }

        private bool ParamsChanged()
        {
            return seed != lastSeed
                   || enableHillsStage != lastEnableHillsStage
                   || viewLayer != lastViewLayer
                   || maskOffColor != lastMaskOffColor
                   || maskOnColor != lastMaskOnColor
                   || !Mathf.Approximately(islandRadius01, lastIslandRadius01)
                   || !Mathf.Approximately(waterThreshold01, lastWaterThreshold01)
                   || !Mathf.Approximately(islandSmoothFrom01, lastIslandSmoothFrom01)
                   || !Mathf.Approximately(islandSmoothTo01, lastIslandSmoothTo01)
                   || noiseCellSize != lastNoiseCellSize
                   || !Mathf.Approximately(noiseAmplitude, lastNoiseAmplitude)
                   || quantSteps != lastQuantSteps
                   || clearBeforeRun != lastClearBeforeRun;
        }
    }
}
