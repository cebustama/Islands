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
    /// Phase N4: TerrainNoiseSettings replaces noiseCellSize/noiseAmplitude/quantSteps.
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
        [SerializeField] private bool enableHillsStage = true;
        [SerializeField] private bool enableShoreStage = true;
        [SerializeField] private bool enableVegetationStage = true;
        [SerializeField] private bool enableTraversalStage = true;
        [SerializeField] private bool enableMorphologyStage = true;

        [Header("View Mode")]
        [SerializeField] private PCGViewMode viewMode = PCGViewMode.MaskLayer;

        [Header("Layer View  (View Mode = MaskLayer)")]
        [SerializeField] private MapLayerId viewLayer = MapLayerId.Land;

        [Header("Field View  (View Mode = ScalarField)")]
        [SerializeField] private MapFieldId viewField = MapFieldId.Height;
        [SerializeField] private float scalarMin = -1f;
        [SerializeField] private float scalarMax = 20f;

        [Header("Palette  (MaskLayer: OFF/ON — ScalarField: Low/High ramp)")]
        [SerializeField] private Color maskOffColor = new Color(0.1f, 0.2f, 0.7f, 1f);
        [SerializeField] private Color maskOnColor = new Color(0.0f, 0.4f, 0.0f, 1f);

        [Header("Layer Preset Colors  (MaskLayer mode only)")]
        [SerializeField] private bool useLayerPresetColors = false;

        [SerializeField]
        private Color[] layerPresetOnColors = new Color[(int)MapLayerId.COUNT]
        {
            new Color(0.10f, 0.60f, 0.10f, 1f),  // 0  Land
            new Color(0.00f, 0.10f, 0.60f, 1f),  // 1  DeepWater
            new Color(0.20f, 0.50f, 0.90f, 1f),  // 2  ShallowWater
            new Color(0.60f, 0.40f, 0.10f, 1f),  // 3  HillsL1
            new Color(0.40f, 0.25f, 0.00f, 1f),  // 4  HillsL2
            new Color(0.80f, 0.80f, 0.00f, 1f),  // 5  Paths
            new Color(0.90f, 0.55f, 0.00f, 1f),  // 6  Stairs
            new Color(0.00f, 0.35f, 0.10f, 1f),  // 7  Vegetation
            new Color(0.50f, 0.80f, 0.50f, 1f),  // 8  Walkable
            new Color(0.90f, 0.20f, 0.20f, 1f),  // 9  LandEdge
            new Color(0.30f, 0.75f, 0.30f, 1f),  // 10 LandInterior
            new Color(0.00f, 0.65f, 0.40f, 1f),  // 11 LandCore
            new Color(0.10f, 0.30f, 0.75f, 1f),  // 12 MidWater
        };

        [Header("F2 Tunables (Shape + Threshold)")]
        [Range(0f, 1f)][SerializeField] private float islandRadius01 = 0.45f;
        [Range(0f, 1f)][SerializeField] private float waterThreshold01 = 0.50f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothFrom01 = 0.30f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothTo01 = 0.70f;

        [Header("F2 Tunables (Island Shape — Ellipse + Warp)")]
        [Range(0.25f, 4f)][SerializeField] private float islandAspectRatio = 1.00f;
        [Range(0f, 1f)][SerializeField] private float warpAmplitude01 = 0.00f;

        [Header("Height Redistribution (J2)")]
        [Range(0.5f, 4f)][SerializeField] private float heightRedistributionExponent = 1.0f;

        [Header("Hills (F3b)")]
        [Range(0f, 1f)][SerializeField] private float hillsThresholdL1 = 0.65f;
        [Range(0f, 1f)][SerializeField] private float hillsThresholdL2 = 0.80f;

        // N4: terrain noise settings (replaces noiseCellSize, noiseAmplitude)
        [Header("Terrain Noise (N4)")]
        [SerializeField] private TerrainNoiseType terrainNoiseType = TerrainNoiseType.Perlin;
        [Range(1, 32)][SerializeField] private int terrainFrequency = 8;
        [Range(1, 6)][SerializeField] private int terrainOctaves = 4;
        [Range(2, 4)][SerializeField] private int terrainLacunarity = 2;
        [Range(0f, 1f)][SerializeField] private float terrainPersistence = 0.5f;
        [Range(0f, 1f)][SerializeField] private float terrainAmplitude = 0.35f;

        // N4: warp noise settings
        [Header("Warp Noise (N4)")]
        [SerializeField] private TerrainNoiseType warpNoiseType = TerrainNoiseType.Perlin;
        [Range(1, 32)][SerializeField] private int warpFrequency = 4;
        [Range(1, 6)][SerializeField] private int warpOctaves = 1;
        [Range(2, 4)][SerializeField] private int warpLacunarity = 2;
        [Range(0f, 1f)][SerializeField] private float warpPersistence = 0.5f;

        // N4: height quantization (replaces quantSteps)
        [Header("Height Quantization (N4)")]
        [Min(0)][SerializeField] private int heightQuantSteps = 1024;

        [Header("Run Behavior")]
        [SerializeField] private bool clearBeforeRun = true;

        private NativeArray<float4> packedNoise;
        private ComputeBuffer noiseBuffer;
        private MaterialPropertyBlock mpb;

        private MapContext2D ctx;
        private int ctxResolution = -1;
        private bool dirty = true;

        // ---- Dirty-tracking cache ----
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
        private float lastHeightRedistributionExponent;
        // F3b hills dirty tracking
        private float lastHillsThresholdL1;
        private float lastHillsThresholdL2;
        // N4 noise dirty tracking
        private TerrainNoiseType lastTerrainNoiseType, lastWarpNoiseType;
        private int lastTerrainFrequency, lastTerrainOctaves, lastTerrainLacunarity;
        private float lastTerrainPersistence, lastTerrainAmplitude;
        private int lastWarpFrequency, lastWarpOctaves, lastWarpLacunarity;
        private float lastWarpPersistence;
        private int lastHeightQuantSteps;
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
            bool eClear = preset != null ? preset.clearBeforeRun : clearBeforeRun;

            // N4: build tunables with noise settings
            var eTun = preset != null
                ? preset.ToTunables()
                : new MapTunables2D(
                      islandRadius01, waterThreshold01,
                      islandSmoothFrom01, islandSmoothTo01,
                      islandAspectRatio, warpAmplitude01,
                      heightRedistributionExponent,
                      default, // heightRemapSpline
                      terrainNoise: new TerrainNoiseSettings
                      {
                          noiseType = terrainNoiseType,
                          frequency = terrainFrequency,
                          octaves = terrainOctaves,
                          lacunarity = terrainLacunarity,
                          persistence = terrainPersistence,
                          amplitude = terrainAmplitude,
                      },
                      warpNoise: new TerrainNoiseSettings
                      {
                          noiseType = warpNoiseType,
                          frequency = warpFrequency,
                          octaves = warpOctaves,
                          lacunarity = warpLacunarity,
                          persistence = warpPersistence,
                          amplitude = 1.0f,
                      },
                      heightQuantSteps: heightQuantSteps,
                      hillsThresholdL1: hillsThresholdL1,
                      hillsThresholdL2: hillsThresholdL2);

            ApplyPaletteToMpb();
            EnsureContextAllocated(resolution);

            if (ParamsChanged())
            {
                CacheParams();
                dirty = true;
            }

            if (dirty)
            {
                // N4: feed noise settings to configurable stage.
                baseStage.terrainNoise = eTun.terrainNoise;
                baseStage.warpNoise = eTun.warpNoise;
                baseStage.heightQuantSteps = eTun.heightQuantSteps;

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
                if (ctx.IsFieldCreated(viewField))
                    PackFromFieldAndUpload(ref ctx.GetField(viewField), resolution);
                else
                    PackZerosAndUpload();
            }
            else
            {
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
            lastHeightRedistributionExponent = preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent;
            // F3b hills params
            lastHillsThresholdL1 = preset != null ? preset.hillsThresholdL1 : hillsThresholdL1;
            lastHillsThresholdL2 = preset != null ? preset.hillsThresholdL2 : hillsThresholdL2;
            // N4 noise params
            lastTerrainNoiseType = preset != null ? preset.terrainNoiseType : terrainNoiseType;
            lastTerrainFrequency = preset != null ? preset.terrainFrequency : terrainFrequency;
            lastTerrainOctaves = preset != null ? preset.terrainOctaves : terrainOctaves;
            lastTerrainLacunarity = preset != null ? preset.terrainLacunarity : terrainLacunarity;
            lastTerrainPersistence = preset != null ? preset.terrainPersistence : terrainPersistence;
            lastTerrainAmplitude = preset != null ? preset.terrainAmplitude : terrainAmplitude;
            lastWarpNoiseType = preset != null ? preset.warpNoiseType : warpNoiseType;
            lastWarpFrequency = preset != null ? preset.warpFrequency : warpFrequency;
            lastWarpOctaves = preset != null ? preset.warpOctaves : warpOctaves;
            lastWarpLacunarity = preset != null ? preset.warpLacunarity : warpLacunarity;
            lastWarpPersistence = preset != null ? preset.warpPersistence : warpPersistence;
            lastHeightQuantSteps = preset != null ? preset.heightQuantSteps : heightQuantSteps;
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
                || !Mathf.Approximately(preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent, lastHeightRedistributionExponent)
                // F3b hills params
                || !Mathf.Approximately(preset != null ? preset.hillsThresholdL1 : hillsThresholdL1, lastHillsThresholdL1)
                || !Mathf.Approximately(preset != null ? preset.hillsThresholdL2 : hillsThresholdL2, lastHillsThresholdL2)
                // N4 noise params
                || (preset != null ? preset.terrainNoiseType : terrainNoiseType) != lastTerrainNoiseType
                || (preset != null ? preset.terrainFrequency : terrainFrequency) != lastTerrainFrequency
                || (preset != null ? preset.terrainOctaves : terrainOctaves) != lastTerrainOctaves
                || (preset != null ? preset.terrainLacunarity : terrainLacunarity) != lastTerrainLacunarity
                || !Mathf.Approximately(preset != null ? preset.terrainPersistence : terrainPersistence, lastTerrainPersistence)
                || !Mathf.Approximately(preset != null ? preset.terrainAmplitude : terrainAmplitude, lastTerrainAmplitude)
                || (preset != null ? preset.warpNoiseType : warpNoiseType) != lastWarpNoiseType
                || (preset != null ? preset.warpFrequency : warpFrequency) != lastWarpFrequency
                || (preset != null ? preset.warpOctaves : warpOctaves) != lastWarpOctaves
                || (preset != null ? preset.warpLacunarity : warpLacunarity) != lastWarpLacunarity
                || !Mathf.Approximately(preset != null ? preset.warpPersistence : warpPersistence, lastWarpPersistence)
                || (preset != null ? preset.heightQuantSteps : heightQuantSteps) != lastHeightQuantSteps
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun;
        }
    }
}