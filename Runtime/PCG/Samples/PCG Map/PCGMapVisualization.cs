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
    /// Visualization mode for the PCG map lantern.
    /// </summary>
    public enum PCGViewMode
    {
        /// <summary>Render a single MaskGrid2D layer as binary ON/OFF.</summary>
        MaskLayer,

        /// <summary>
        /// Render a single ScalarField2D as a normalized color ramp.
        /// maskOffColor = scalarMin end; maskOnColor = scalarMax end.
        /// </summary>
        ScalarField
    }

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
    /// - displays the selected MaskGrid2D layer (PCGViewMode.MaskLayer) or ScalarField2D
    ///   (PCGViewMode.ScalarField) via GPU buffer packing
    ///
    /// F2b: added islandAspectRatio and warpAmplitude01 Inspector fields.
    ///      BaseTerrainStage_Configurable updated to mirror Stage_BaseTerrain2D shape pipeline.
    /// Phase H: added PCGViewMode enum; ScalarField view mode for Height/CoastDist color-ramp
    ///          visualization (scalarMin/scalarMax normalization); per-layer preset ON colors
    ///          (useLayerPresetColors toggle + layerPresetOnColors array).
    /// Phase H3: added optional MapGenerationPreset slot (override-at-resolve pattern).
    ///           When assigned, all pipeline parameters are read from the preset; inline
    ///           Inspector fields remain active as fallback when preset is null.
    ///           Resolution is controlled by the base Visualization class and is NOT
    ///           overridable via the preset in this component.
    /// </summary>
    public sealed class PCGMapVisualization : Visualization
    {
        private static readonly int NoiseId = Shader.PropertyToID("_Noise");
        private static readonly int MaskOffColorId = Shader.PropertyToID("_MaskOffColor");
        private static readonly int MaskOnColorId = Shader.PropertyToID("_MaskOnColor");

        // =====================================================================
        // Inspector — Preset (optional, H3)
        // =====================================================================
        [Header("Preset (optional)")]
        [Tooltip("Assign a MapGenerationPreset asset to override all pipeline parameters.\n" +
                 "When null this component's own inline fields below are used (backward compatible).\n" +
                 "Note: resolution is always read from the base Visualization class, not the preset.")]
        [SerializeField] private MapGenerationPreset preset;

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

        [Header("View Mode")]
        [Tooltip("MaskLayer: renders the selected MapLayerId as binary ON/OFF.\n" +
                 "ScalarField: renders the selected MapFieldId as a normalized color ramp\n" +
                 "(maskOffColor = scalarMin end; maskOnColor = scalarMax end).")]
        [SerializeField] private PCGViewMode viewMode = PCGViewMode.MaskLayer;

        [Header("Layer View  (View Mode = MaskLayer)")]
        [Tooltip("Qu? capa (MaskGrid2D) quieres visualizar.\n" +
                 "Capas F2: Land, DeepWater.\n" +
                 "Capas F3: LandEdge, LandInterior, HillsL1, HillsL2.\n" +
                 "Capas F4: ShallowWater.\n" +
                 "Capas F5: Vegetation.\n" +
                 "Capas F6: Walkable, Stairs.\n" +
                 "Capas Phase G: LandCore.\n" +
                 "Si la capa no existe a?n, se muestra todo OFF.")]
        [SerializeField] private MapLayerId viewLayer = MapLayerId.Land;

        [Header("Field View  (View Mode = ScalarField)")]
        [Tooltip("Qu? campo escalar (ScalarField2D) quieres visualizar.\n" +
                 "Height (F2): values [0..1]. Suggested scalarMin=0, scalarMax=1.\n" +
                 "CoastDist (Phase G): -1 sentinel for water/unreached, 0 at coast, positive inland.\n" +
                 "  Suggested scalarMin=-1, scalarMax=20 (or the CoastDistMax tunable if set).\n" +
                 "Moisture: registered but not yet written (Phase M) — shows all-low ramp.\n" +
                 "Requires the producing stage to be enabled.")]
        [SerializeField] private MapFieldId viewField = MapFieldId.Height;

        [Tooltip("Scalar value mapped to maskOffColor (ramp low end).")]
        [SerializeField] private float scalarMin = -1f;

        [Tooltip("Scalar value mapped to maskOnColor (ramp high end).")]
        [SerializeField] private float scalarMax = 20f;

        [Header("Palette  (MaskLayer: OFF/ON — ScalarField: Low/High ramp)")]
        [SerializeField] private Color maskOffColor = new Color(0.1f, 0.2f, 0.7f, 1f);
        [SerializeField] private Color maskOnColor = new Color(0.0f, 0.4f, 0.0f, 1f);

        [Header("Layer Preset Colors  (MaskLayer mode only)")]
        [Tooltip("When enabled, maskOnColor is overridden at display time by the preset for the\n" +
                 "active viewLayer. maskOffColor is still used as-is.\n" +
                 "Note: changes to the color array do not trigger dirty detection; toggle this\n" +
                 "field or change the seed to force a refresh after editing preset colors.")]
        [SerializeField] private bool useLayerPresetColors = false;

        [Tooltip("One entry per MapLayerId (COUNT=12). Index matches MapLayerId integer value.")]
        [SerializeField]
        private Color[] layerPresetOnColors = new Color[(int)MapLayerId.COUNT]
        {
            new Color(0.10f, 0.60f, 0.10f, 1f),  // 0  Land          — green
            new Color(0.00f, 0.10f, 0.60f, 1f),  // 1  DeepWater     — dark blue
            new Color(0.20f, 0.50f, 0.90f, 1f),  // 2  ShallowWater  — light blue
            new Color(0.60f, 0.40f, 0.10f, 1f),  // 3  HillsL1       — tan
            new Color(0.40f, 0.25f, 0.00f, 1f),  // 4  HillsL2       — dark brown
            new Color(0.80f, 0.80f, 0.00f, 1f),  // 5  Paths         — yellow (unwritten)
            new Color(0.90f, 0.55f, 0.00f, 1f),  // 6  Stairs        — orange
            new Color(0.00f, 0.35f, 0.10f, 1f),  // 7  Vegetation    — dark green
            new Color(0.50f, 0.80f, 0.50f, 1f),  // 8  Walkable      — pale green
            new Color(0.90f, 0.20f, 0.20f, 1f),  // 9  LandEdge      — red
            new Color(0.30f, 0.75f, 0.30f, 1f),  // 10 LandInterior  — mid green
            new Color(0.00f, 0.65f, 0.40f, 1f),  // 11 LandCore      — teal
        };

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

        // ---- Dirty-tracking cache (H3: effective values cached, not raw fields) ----
        private MapGenerationPreset _lastPreset;
        private uint lastSeed;
        private bool lastEnableHillsStage;
        private bool lastEnableShoreStage;
        private bool lastEnableVegetationStage;
        private bool lastEnableTraversalStage;
        private bool lastEnableMorphologyStage;
        private PCGViewMode lastViewMode;
        private MapLayerId lastViewLayer;
        private MapFieldId lastViewField;
        private float lastScalarMin;
        private float lastScalarMax;
        private bool lastUseLayerPresetColors;
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

            // H3: resolve effective values (preset overrides inline fields when assigned).
            // Resolution intentionally excluded — controlled by base Visualization class.
            uint eSeed = preset != null ? preset.seed : seed;
            bool eHills = preset != null ? preset.enableHillsStage : enableHillsStage;
            bool eShore = preset != null ? preset.enableShoreStage : enableShoreStage;
            bool eVeg = preset != null ? preset.enableVegetationStage : enableVegetationStage;
            bool eTrav = preset != null ? preset.enableTraversalStage : enableTraversalStage;
            bool eMorph = preset != null ? preset.enableMorphologyStage : enableMorphologyStage;
            int eCell = preset != null ? preset.noiseCellSize : noiseCellSize;
            float eAmp = preset != null ? preset.noiseAmplitude : noiseAmplitude;
            int eQuant = preset != null ? preset.quantSteps : quantSteps;
            bool eClear = preset != null ? preset.clearBeforeRun : clearBeforeRun;
            var eTun = preset != null
                ? preset.ToTunables()
                : new MapTunables2D(
                      islandRadius01, waterThreshold01,
                      islandSmoothFrom01, islandSmoothTo01,
                      islandAspectRatio, warpAmplitude01);

            ApplyPaletteToMpb();
            EnsureContextAllocated(resolution);

            if (ParamsChanged())
            {
                CacheParams();
                dirty = true;
            }

            if (dirty)
            {
                baseStage.noiseCellSize = Mathf.Max(1, eCell);
                baseStage.noiseAmplitude = Mathf.Max(0f, eAmp);
                baseStage.quantSteps = Mathf.Max(0, eQuant);

                var inputs = new MapInputs(
                    seed: eSeed,
                    domain: new GridDomain2D(resolution, resolution),
                    tunables: eTun);

                var stages = eMorph ? stagesG
                           : eTrav ? stagesF6
                           : eVeg ? stagesF5
                           : eShore ? stagesF4
                           : eHills ? stagesF3
                           : stagesF2;

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: eClear);

                dirty = false;
            }

            // --- display ---
            if (viewMode == PCGViewMode.ScalarField)
            {
                // Scalar field: normalize values from [scalarMin..scalarMax] → [0..1] and pack.
                // -1f sentinel values (CoastDist water/unreached) map to 0 when scalarMin = -1.
                // Fields not yet written (e.g. Moisture before Phase M) show all-low ramp.
                if (ctx.IsFieldCreated(viewField))
                    PackFromFieldAndUpload(ref ctx.GetField(viewField), resolution);
                else
                    PackZerosAndUpload();
            }
            else
            {
                // Mask layer: binary ON/OFF. Optionally override maskOnColor with layer preset.
                if (useLayerPresetColors)
                {
                    int idx = (int)viewLayer;
                    if (idx >= 0 && idx < layerPresetOnColors.Length)
                        mpb.SetColor(MaskOnColorId, layerPresetOnColors[idx]);
                }

                if (ctx.IsLayerCreated(viewLayer))
                    PackFromMaskAndUpload(ref ctx.GetLayer(viewLayer), resolution);
                else
                    PackZerosAndUpload();
            }

            if (!loggedFirstUpdate)
            {
                loggedFirstUpdate = true;

                ulong hash = 0ul;
                if (viewMode == PCGViewMode.ScalarField)
                {
                    // No SnapshotHash64 on ScalarField2D; log field availability only.
                    hash = ctx.IsFieldCreated(viewField) ? 0xFFFFFFFFFFFFFFFFul : 0ul;
                }
                else
                {
                    hash = ctx.IsLayerCreated(viewLayer)
                        ? ctx.GetLayer(viewLayer).SnapshotHash64()
                        : 0ul;
                }

                Debug.Log(
                    $"[PCGMapVisualization] Update #{updateCalls} res={resolution} seed={eSeed} " +
                    $"aspect={eTun.islandAspectRatio:F2} warp={eTun.warpAmplitude01:F2} " +
                    $"hills={eHills} shore={eShore} " +
                    $"veg={eVeg} traversal={eTrav} " +
                    $"morphology={eMorph} " +
                    $"mode={viewMode} view={viewLayer} field={viewField} hash={hash:X16}");
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

        /// <summary>
        /// Packs normalized scalar field values into the GPU buffer.
        /// Maps [scalarMin..scalarMax] → [0..1] using math.saturate.
        /// The existing shader interpolates maskOffColor (0) to maskOnColor (1),
        /// giving a color ramp with no shader changes required.
        /// </summary>
        private void PackFromFieldAndUpload(ref ScalarField2D field, int resolution)
        {
            int totalInstances = resolution * resolution;
            int packs = packedNoise.Length;

            float range = scalarMax - scalarMin;
            float invRange = (range > 1e-6f) ? (1f / range) : 0f;

            for (int packIndex = 0; packIndex < packs; packIndex++)
            {
                int baseInstance = packIndex * 4;

                float v0 = (baseInstance + 0 < totalInstances) ? ScalarInstanceValue(ref field, baseInstance + 0, resolution, invRange) : 0f;
                float v1 = (baseInstance + 1 < totalInstances) ? ScalarInstanceValue(ref field, baseInstance + 1, resolution, invRange) : 0f;
                float v2 = (baseInstance + 2 < totalInstances) ? ScalarInstanceValue(ref field, baseInstance + 2, resolution, invRange) : 0f;
                float v3 = (baseInstance + 3 < totalInstances) ? ScalarInstanceValue(ref field, baseInstance + 3, resolution, invRange) : 0f;

                packedNoise[packIndex] = new float4(v0, v1, v2, v3);
            }

            noiseBuffer.SetData(packedNoise.Reinterpret<float>(sizeof(float) * 4));
        }

        private float ScalarInstanceValue(ref ScalarField2D field, int instanceIndex, int resolution, float invRange)
        {
            int x = instanceIndex % resolution;
            int y = instanceIndex / resolution;
            return math.saturate((field.GetUnchecked(x, y) - scalarMin) * invRange);
        }

        private void ApplyPaletteToMpb()
        {
            if (mpb == null) return;
            mpb.SetColor(MaskOffColorId, maskOffColor);
            mpb.SetColor(MaskOnColorId, maskOnColor);
        }

        // H3: CacheParams caches effective values (not raw fields) so that
        // both preset-field edits and inline-field edits are detected correctly.
        private void CacheParams()
        {
            _lastPreset = preset;
            lastSeed = preset != null ? preset.seed : seed;
            lastEnableHillsStage = preset != null ? preset.enableHillsStage : enableHillsStage;
            lastEnableShoreStage = preset != null ? preset.enableShoreStage : enableShoreStage;
            lastEnableVegetationStage = preset != null ? preset.enableVegetationStage : enableVegetationStage;
            lastEnableTraversalStage = preset != null ? preset.enableTraversalStage : enableTraversalStage;
            lastEnableMorphologyStage = preset != null ? preset.enableMorphologyStage : enableMorphologyStage;
            lastViewMode = viewMode;
            lastViewLayer = viewLayer;
            lastViewField = viewField;
            lastScalarMin = scalarMin;
            lastScalarMax = scalarMax;
            lastUseLayerPresetColors = useLayerPresetColors;
            lastMaskOffColor = maskOffColor;
            lastMaskOnColor = maskOnColor;
            lastIslandRadius01 = preset != null ? preset.islandRadius01 : islandRadius01;
            lastWaterThreshold01 = preset != null ? preset.waterThreshold01 : waterThreshold01;
            lastIslandSmoothFrom01 = preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01;
            lastIslandSmoothTo01 = preset != null ? preset.islandSmoothTo01 : islandSmoothTo01;
            lastIslandAspectRatio = preset != null ? preset.islandAspectRatio : islandAspectRatio;
            lastWarpAmplitude01 = preset != null ? preset.warpAmplitude01 : warpAmplitude01;
            lastNoiseCellSize = preset != null ? preset.noiseCellSize : noiseCellSize;
            lastNoiseAmplitude = preset != null ? preset.noiseAmplitude : noiseAmplitude;
            lastQuantSteps = preset != null ? preset.quantSteps : quantSteps;
            lastClearBeforeRun = preset != null ? preset.clearBeforeRun : clearBeforeRun;
        }

        private bool ParamsChanged()
        {
            return preset != _lastPreset
                || (preset != null ? preset.seed : seed) != lastSeed
                || (preset != null ? preset.enableHillsStage : enableHillsStage) != lastEnableHillsStage
                || (preset != null ? preset.enableShoreStage : enableShoreStage) != lastEnableShoreStage
                || (preset != null ? preset.enableVegetationStage : enableVegetationStage) != lastEnableVegetationStage
                || (preset != null ? preset.enableTraversalStage : enableTraversalStage) != lastEnableTraversalStage
                || (preset != null ? preset.enableMorphologyStage : enableMorphologyStage) != lastEnableMorphologyStage
                || viewMode != lastViewMode
                || viewLayer != lastViewLayer
                || viewField != lastViewField
                || !Mathf.Approximately(scalarMin, lastScalarMin)
                || !Mathf.Approximately(scalarMax, lastScalarMax)
                || useLayerPresetColors != lastUseLayerPresetColors
                || maskOffColor != lastMaskOffColor
                || maskOnColor != lastMaskOnColor
                || !Mathf.Approximately(preset != null ? preset.islandRadius01 : islandRadius01, lastIslandRadius01)
                || !Mathf.Approximately(preset != null ? preset.waterThreshold01 : waterThreshold01, lastWaterThreshold01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01, lastIslandSmoothFrom01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothTo01 : islandSmoothTo01, lastIslandSmoothTo01)
                || !Mathf.Approximately(preset != null ? preset.islandAspectRatio : islandAspectRatio, lastIslandAspectRatio)
                || !Mathf.Approximately(preset != null ? preset.warpAmplitude01 : warpAmplitude01, lastWarpAmplitude01)
                || (preset != null ? preset.noiseCellSize : noiseCellSize) != lastNoiseCellSize
                || !Mathf.Approximately(preset != null ? preset.noiseAmplitude : noiseAmplitude, lastNoiseAmplitude)
                || (preset != null ? preset.quantSteps : quantSteps) != lastQuantSteps
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun;
        }
    }
}