using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Lantern/Visualization testbed for the Map Pipeline by Layers.
    /// Clean version:
    /// - Runs ONLY a configurable BaseTerrain F2 stage (no rect/donut debug patterns).
    /// - Displays a selected MaskGrid2D layer (Land / DeepWater) via GPU buffer packing.
    /// </summary>
    public sealed class PCGMapVisualization : Visualization
    {
        private static readonly int NoiseId = Shader.PropertyToID("_Noise");
        private static readonly int MaskOffColorId = Shader.PropertyToID("_MaskOffColor");
        private static readonly int MaskOnColorId = Shader.PropertyToID("_MaskOnColor");

        [Header("Run Inputs")]
        [Tooltip("Semilla determinista (uint). Misma semilla => mismo mapa.\n" +
                 "Cambiarla cambia el patrón de ruido y, por tanto, el contorno final tras el threshold.")]
        [SerializeField] private uint seed = 1u;

        [Header("Layer View")]
        [Tooltip("Qué capa (MaskGrid2D) quieres visualizar.\n" +
                 "- Land: tierra (ON)\n" +
                 "- DeepWater: agua conectada al borde (ON)\n" +
                 "Si la capa no existe aún, se muestra todo OFF.")]
        [SerializeField] private MapLayerId viewLayer = MapLayerId.Land;

        [Header("Palette (0/1)")]
        [Tooltip("Color para celdas OFF (bit=0) del mask visualizado.")]
        [SerializeField] private Color maskOffColor = new Color(0.1f, 0.2f, 0.7f, 1f);

        [Tooltip("Color para celdas ON (bit=1) del mask visualizado.")]
        [SerializeField] private Color maskOnColor = new Color(0.0f, 0.4f, 0.0f, 1f);

        [Header("F2 Tunables (Shape + Threshold)")]
        [Tooltip("Tamaño de la isla como fracción de min(width,height).\n" +
                 "Ej: 0.45 = isla mediana. Subir => isla más grande.")]
        [Range(0f, 1f)]
        [SerializeField] private float islandRadius01 = 0.45f;

        [Tooltip("Umbral (0..1) para decidir Land:\n" +
                 "Land = (Height >= waterThreshold01).\n" +
                 "Subir => menos tierra (más 'nivel del mar').\n" +
                 "Bajar => más tierra.")]
        [Range(0f, 1f)]
        [SerializeField] private float waterThreshold01 = 0.50f;

        [Tooltip("Inicio de la transición suave del borde (smoothstep).\n" +
                 "Más bajo => la caída empieza antes (isla efectiva más pequeña).\n" +
                 "Debe ser <= Smooth To (si no, se reordena determinísticamente).")]
        [Range(0f, 1f)]
        [SerializeField] private float islandSmoothFrom01 = 0.30f;

        [Tooltip("Fin de la transición suave del borde (smoothstep).\n" +
                 "Más alto => borde más ancho/suave.\n" +
                 "Más cercano a Smooth From => borde más duro/abrupto.")]
        [Range(0f, 1f)]
        [SerializeField] private float islandSmoothTo01 = 0.70f;

        [Header("F2 Tunables (Noise Inside Island)")]
        [Tooltip("Tamaño de celda del ruido 'coarse' (value-noise) en celdas del grid.\n" +
                 "Más grande => features más grandes/suaves.\n" +
                 "Más pequeño => más detalle (más 'rugoso').\n" +
                 "Determinismo: cambia también cuántos valores consume el RNG (pero sigue siendo determinista).")]
        [Min(1)]
        [SerializeField] private int noiseCellSize = 8;

        [Tooltip("Amplitud del ruido aplicada DENTRO de la isla.\n" +
                 "0 = isla perfectamente radial.\n" +
                 "Valores típicos: 0.05..0.25.\n" +
                 "Mucho + threshold alto => patrones 'filigrana' como los que viste.")]
        [Range(0f, 1f)]
        [SerializeField] private float noiseAmplitude = 0.18f;

        [Tooltip("Cuantización del height antes del threshold (estabilidad de borde).\n" +
                 "Ej: 1024 = pasos finos; 64 = pasos más gruesos.\n" +
                 "Esto reduce sensibilidad del threshold a pequeñas variaciones float.\n" +
                 "Setea 0 o 1 para desactivar.")]
        [Min(0)]
        [SerializeField] private int quantSteps = 1024;

        [Header("Run Behavior")]
        [Tooltip("Si está activo, el pipeline limpia capas/fields antes de ejecutar.\n" +
                 "Recomendado ON para evitar residuos al cambiar sliders.")]
        [SerializeField] private bool clearBeforeRun = true;

        // -------------------------
        // Runtime state
        // -------------------------
        private NativeArray<float4> packedNoise;
        private ComputeBuffer noiseBuffer;
        private MaterialPropertyBlock mpb;

        private MapContext2D ctx;
        private int ctxResolution = -1;

        private bool dirty = true;

        // Cached params for dirty checking
        private uint lastSeed;
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

        // Pipeline stage (kept to avoid allocations)
        private BaseTerrainStage_Configurable stage;
        private IMapStage2D[] stages;

        /// <summary>
        /// Same logic as Stage_BaseTerrain2D but with inspector-controlled noise parameters.
        /// Lives in Samples only (does not affect core/tests).
        /// </summary>
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

                // squared smooth thresholds (avoid sqrt)
                float from = t.islandSmoothFrom01;
                float to = t.islandSmoothTo01;
                float fromSq = from * from;
                float toSq = to * to;

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

                    // Fill coarse noise using ONLY ctx.Rng (seed-driven deterministic)
                    for (int ny = 0; ny < nh; ny++)
                    {
                        int row = ny * nw;
                        for (int nx = 0; nx < nw; nx++)
                        {
                            noise[row + nx] = ctx.Rng.NextFloat(); // [0..1)
                        }
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
                            float n = math.lerp(nx0, nx1, ty); // [0..1)

                            float2 p = new float2(x + 0.5f, y + 0.5f);
                            float2 v = p - center;
                            float distSq = v.x * v.x + v.y * v.y;

                            float radial01Sq = math.saturate(distSq * invRadiusSq);
                            float s = math.smoothstep(fromSq, toSq, radial01Sq);
                            float mask01 = 1f - s;

                            float h01 = mask01 + ((n - 0.5f) * amp * mask01);
                            h01 = math.saturate(h01);

                            // Quantize to reduce threshold-edge sensitivity
                            if (qs > 1)
                                h01 = math.floor(h01 * qs) * invQuant;

                            height.Values[baseRow + x] = h01;

                            bool isLand = h01 >= waterThreshold;
                            land.SetUnchecked(x, y, isLand);
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

            // Buffer expects float stream count == instances (resolution*resolution).
            // Base gives us packs; buffer length must be packs * 4.
            noiseBuffer = new ComputeBuffer(dataLength * 4, sizeof(float));
            mpb.SetBuffer(NoiseId, noiseBuffer);

            ApplyPaletteToMpb();

            stage = new BaseTerrainStage_Configurable();
            stages = new IMapStage2D[1] { stage };

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

            stage = null;
            stages = null;
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
                // Stage params from inspector (deterministic sanitization)
                stage.noiseCellSize = Mathf.Max(1, noiseCellSize);
                stage.noiseAmplitude = Mathf.Max(0f, noiseAmplitude);
                stage.quantSteps = Mathf.Max(0, quantSteps);

                // Tunables (MapTunables2D clamps + orders from/to deterministically)
                var tunables = new MapTunables2D(
                    islandRadius01: islandRadius01,
                    waterThreshold01: waterThreshold01,
                    islandSmoothFrom01: islandSmoothFrom01,
                    islandSmoothTo01: islandSmoothTo01
                );

                var inputs = new MapInputs(
                    seed: seed,
                    domain: new GridDomain2D(resolution, resolution),
                    tunables: tunables
                );

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

                Debug.Log($"[PCGMapVisualization] Update #{updateCalls} res={resolution} seed={seed} view={viewLayer} hash={h:X16}");
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
