using Unity.Collections;
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
    /// Composite overworld-style map visualization for the PCG Map Pipeline (Phase H1).
    ///
    /// Composites all active pipeline layers into a single Texture2D, one cell at a time,
    /// using a fixed priority-ordered color table (low → high, later entries overwrite earlier):
    ///   DeepWater → ShallowWater → Land → LandCore → Vegetation → HillsL1 → HillsL2
    ///   → Stairs → LandEdge
    ///
    /// Phase H1: initial implementation.
    /// Phase H3: optional MapGenerationPreset slot.
    /// Phase N4: TerrainNoiseSettings replaces noiseCellSize/noiseAmplitude/quantSteps.
    /// Phase N5.a: IslandShapeMode selector.
    /// Phase N5.b: NoiseSettingsAsset slots. Refactored individual noise fields to embedded
    ///             TerrainNoiseSettings structs with IEquatable dirty-tracking.
    /// </summary>
    [ExecuteAlways]
    public sealed class PCGMapCompositeVisualization : MonoBehaviour
    {
        // -------------------------------------------------------------------------
        // CompositeLayerSlot
        // -------------------------------------------------------------------------
        [System.Serializable]
        public struct CompositeLayerSlot
        {
            [Tooltip("Human-readable layer name. Edit-safe; has no runtime effect.")]
            public string label;

            [Tooltip("Color used when this layer's cell is ON in the composite.")]
            public Color color;

            [Tooltip("Include this layer in the composite. Uncheck to hide even if the pipeline produced it.")]
            public bool enabled;
        }

        private static readonly MapLayerId[] s_compositePriority =
        {
            MapLayerId.DeepWater,
            MapLayerId.ShallowWater,
            MapLayerId.Land,
            MapLayerId.LandCore,
            MapLayerId.Vegetation,
            MapLayerId.HillsL1,
            MapLayerId.HillsL2,
            MapLayerId.Stairs,
            MapLayerId.LandEdge,
        };

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        // =====================================================================
        // Inspector — Preset (optional, H3)
        // =====================================================================
        [Header("Preset (optional)")]
        [Tooltip("Assign a MapGenerationPreset asset to override all pipeline parameters.\n" +
                 "When null this component's own inline fields are used (backward compatible).")]
        [SerializeField] private MapGenerationPreset preset;

        // =====================================================================
        // Inspector — Render Target
        // =====================================================================
        [Header("Render Target")]
        [Tooltip("MeshRenderer that receives the composite Texture2D via MaterialPropertyBlock.")]
        [SerializeField] private Renderer targetRenderer;

        // =====================================================================
        // Inspector — Run Inputs
        // =====================================================================
        [Header("Run Inputs")]
        [SerializeField] private uint seed = 1u;
        [Min(4)]
        [SerializeField] private int resolution = 64;

        // =====================================================================
        // Inspector — Pipeline
        // =====================================================================
        [Header("Pipeline")]
        [SerializeField] private bool enableHillsStage = true;
        [SerializeField] private bool enableShoreStage = true;
        [SerializeField] private bool enableVegetationStage = true;
        [SerializeField] private bool enableTraversalStage = true;
        [SerializeField] private bool enableMorphologyStage = true;
        [SerializeField] private bool enableBiomeStage = true;
        [SerializeField] private bool enableRegionsStage = true;

        // =====================================================================
        // Inspector — Layer Slots
        // =====================================================================
        [Header("Layer Slots  (one per MapLayerId, indexed 0–11)")]
        [SerializeField] private Color backgroundColor = new Color(0.02f, 0.02f, 0.08f, 1f);

        [SerializeField] private CompositeLayerSlot[] layerSlots = BuildDefaultSlots();

        // =====================================================================
        // Inspector — Scalar Field Overlay
        // =====================================================================
        [Header("Scalar Field Overlay (optional)")]
        [SerializeField] private bool enableScalarOverlay = false;
        [SerializeField] private MapFieldId overlayField = MapFieldId.Height;
        [SerializeField] private float overlayMin = 0f;
        [SerializeField] private float overlayMax = 1f;
        [SerializeField] private Color overlayColorLow = new Color(0.40f, 0.40f, 0.65f, 1f);
        [SerializeField] private Color overlayColorHigh = Color.white;

        // =====================================================================
        // Inspector — F2 Tunables
        // =====================================================================
        // N5.a: shape mode
        [Header("Island Shape (N5.a)")]
        [SerializeField] private IslandShapeMode shapeMode = IslandShapeMode.Ellipse;

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

        [Header("Hills (F3b / N5.e)")]
        [Range(0f, 1f)][SerializeField] private float hillsL1 = 0.30f;
        [Range(0f, 1f)][SerializeField] private float hillsL2 = 0.43f;
        [Range(0f, 1f)]
        [Tooltip("Noise modulation of hill boundaries (N5.d).\n" +
                 "0.0 = pure height-threshold (default).\n" +
                 "0.5 = moderate variation.\n" +
                 "1.0 = maximum noise influence.")]
        [SerializeField] private float hillsNoiseBlend = 0f;

        // N5.b: noise settings assets (optional override)
        [Header("Noise Settings Assets (N5.b)")]
        [Tooltip("Optional reusable noise asset for terrain height perturbation.\n" +
                 "When assigned, overrides inline Terrain Noise settings.")]
        [SerializeField] private NoiseSettingsAsset terrainNoiseAsset;
        [Tooltip("Optional reusable noise asset for domain warp.\n" +
                 "When assigned, overrides inline Warp Noise settings.")]
        [SerializeField] private NoiseSettingsAsset warpNoiseAsset;
        [Tooltip("Optional reusable noise asset for hills noise modulation (N5.d).\n" +
                 "When assigned, overrides inline Hills Noise settings.\n" +
                 "Only relevant when Hills Noise Blend > 0.")]
        [SerializeField] private NoiseSettingsAsset hillsNoiseAsset;

        // N5.b: embedded noise structs (replace individual N4 fields)
        [Header("Terrain Noise")]
        [SerializeField] private TerrainNoiseSettings terrainNoiseSettings = TerrainNoiseSettings.DefaultTerrain;

        [Header("Warp Noise")]
        [SerializeField] private TerrainNoiseSettings warpNoiseSettings = TerrainNoiseSettings.DefaultWarp;

        [Header("Hills Noise (N5.d)")]
        [SerializeField] private TerrainNoiseSettings hillsNoiseSettings = TerrainNoiseSettings.DefaultHills;

        // N4: height quantization (replaces quantSteps)
        [Header("Height Quantization (N4)")]
        [Min(0)][SerializeField] private int heightQuantSteps = 1024;

        [Header("Run Behavior")]
        [SerializeField] private bool clearBeforeRun = true;

        // =====================================================================
        // Runtime state
        // =====================================================================
        private MapContext2D ctx;
        private int ctxResolution = -1;

        private Texture2D compositeTexture;
        private int texResolution = -1;
        private Color32[] pixels;

        private MaterialPropertyBlock mpb;
        private bool dirty = true;
        private int updateCalls;

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
        private IMapStage2D[] stagesM;
        private IMapStage2D[] stagesM2a;
        private IMapStage2D[] stagesM2b;
        private Stage_Biome2D biomeStage;
        private Stage_Regions2D regionsStage;
        // Biome tunables (inline only — no preset wiring in this viz).
        [SerializeField] private float biomeBaseTemperature = 0.7f;
        [SerializeField] private float biomeLapseRate = 0.5f;
        [SerializeField] private float biomeLatitudeEffect = 0.0f;
        [SerializeField] private float biomeCoastModerationStrength = 0.1f;
        [SerializeField] private float biomeTempNoiseAmplitude = 0.05f;
        [SerializeField] private int biomeTempNoiseCellSize = 16;
        [SerializeField] private float biomeCoastalMoistureBonus = 0.5f;
        [SerializeField] private float biomeCoastDecayRate = 0.3f;
        [SerializeField] private float biomeMoistureNoiseAmplitude = 0.3f;
        [SerializeField] private int biomeMoistureNoiseCellSize = 32;

        // =====================================================================
        // Dirty tracking cache
        // =====================================================================
        private MapGenerationPreset _lastPreset;
        private uint lastSeed;
        private int lastResolution;
        private bool lastEnableHillsStage;
        private bool lastEnableShoreStage;
        private bool lastEnableVegetationStage;
        private bool lastEnableTraversalStage;
        private bool lastEnableMorphologyStage;
        private bool lastEnableBiomeStage;
        private bool lastEnableRegionsStage;
        private IslandShapeMode lastShapeMode;
        private float lastIslandRadius01;
        private float lastWaterThreshold01;
        private float lastIslandSmoothFrom01;
        private float lastIslandSmoothTo01;
        private float lastIslandAspectRatio;
        private float lastWarpAmplitude01;
        private float lastHeightRedistributionExponent;
        // F3b / N5.e hills dirty tracking
        private float lastHillsL1;
        private float lastHillsL2;
        private float lastHillsNoiseBlend;
        // N5.b: noise dirty tracking (replaces 11 individual fields)
        private NoiseSettingsAsset lastTerrainNoiseAsset, lastWarpNoiseAsset, lastHillsNoiseAsset;
        private TerrainNoiseSettings lastTerrainNoise, lastWarpNoise, lastHillsNoise;
        private int lastHeightQuantSteps;
        private bool lastClearBeforeRun;
        private ulong lastSlotsHash;
        private bool lastEnableScalarOverlay;
        private MapFieldId lastOverlayField;
        private float lastOverlayMin;
        private float lastOverlayMax;
        private Color lastBackgroundColor;

        // =====================================================================
        // Default slot table
        // =====================================================================
        private static CompositeLayerSlot[] BuildDefaultSlots() => new CompositeLayerSlot[]
        {
            new CompositeLayerSlot { label = "Land (0)",          color = new Color(0.20f, 0.60f, 0.20f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "DeepWater (1)",     color = new Color(0.05f, 0.10f, 0.50f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "ShallowWater (2)",  color = new Color(0.25f, 0.50f, 0.85f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "HillsL1 (3)",       color = new Color(0.65f, 0.50f, 0.20f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "HillsL2 (4)",       color = new Color(0.40f, 0.27f, 0.07f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "Paths (5)",         color = new Color(0.80f, 0.80f, 0.00f, 1f), enabled = false },
            new CompositeLayerSlot { label = "Stairs (6)",        color = new Color(0.90f, 0.55f, 0.10f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "Vegetation (7)",    color = new Color(0.05f, 0.35f, 0.10f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "Walkable (8)",      color = new Color(0.50f, 0.80f, 0.50f, 1f), enabled = false },
            new CompositeLayerSlot { label = "LandEdge (9)",      color = new Color(0.90f, 0.20f, 0.20f, 1f), enabled = true  },
            new CompositeLayerSlot { label = "LandInterior (10)", color = new Color(0.35f, 0.75f, 0.35f, 1f), enabled = false },
            new CompositeLayerSlot { label = "LandCore (11)",     color = new Color(0.10f, 0.65f, 0.45f, 1f), enabled = true  },
        };

        // =====================================================================
        // MonoBehaviour lifecycle
        // =====================================================================
        private void OnEnable()
        {
            mpb = new MaterialPropertyBlock();
            AllocateStages();
            CacheParams();
            dirty = true;
            updateCalls = 0;
        }

        private void OnDisable()
        {
            ctx?.Dispose();
            ctx = null;
            ctxResolution = -1;

            if (compositeTexture != null)
                DestroyImmediate(compositeTexture);
            compositeTexture = null;
            texResolution = -1;
            pixels = null;

            mpb = null;

            baseStage = null;
            hillsStage = null;
            shoreStage = null;
            vegetationStage = null;
            traversalStage = null;
            morphologyStage = null;
            biomeStage = null;
            regionsStage = null;
            stagesF2 = stagesF3 = stagesF4 = stagesF5 = stagesF6 = stagesG = stagesM = stagesM2a = stagesM2b = null;
        }

        private void Update()
        {
            if (ParamsChanged())
            {
                CacheParams();
                dirty = true;
            }

            if (!dirty) return;

            // H3: resolve effective values (preset overrides inline fields when assigned).
            uint eSeed = preset != null ? preset.seed : seed;
            int eRes = Mathf.Max(4, preset != null ? preset.resolution : resolution);
            bool eHills = preset != null ? preset.enableHillsStage : enableHillsStage;
            bool eShore = preset != null ? preset.enableShoreStage : enableShoreStage;
            bool eVeg = preset != null ? preset.enableVegetationStage : enableVegetationStage;
            bool eTrav = preset != null ? preset.enableTraversalStage : enableTraversalStage;
            bool eMorph = preset != null ? preset.enableMorphologyStage : enableMorphologyStage;
            bool eClear = preset != null ? preset.clearBeforeRun : clearBeforeRun;

            // N5.b: build tunables — preset handles its own asset resolution via ToTunables().
            // When no preset: component resolves asset → inline struct.
            var eTun = preset != null
                ? preset.ToTunables()
                : new MapTunables2D(
                      islandRadius01, waterThreshold01,
                      islandSmoothFrom01, islandSmoothTo01,
                      islandAspectRatio, warpAmplitude01,
                      heightRedistributionExponent,
                      default, // heightRemapSpline
                      terrainNoise: terrainNoiseAsset != null
                          ? terrainNoiseAsset.Settings
                          : terrainNoiseSettings,
                      warpNoise: warpNoiseAsset != null
                          ? warpNoiseAsset.Settings
                          : warpNoiseSettings,
                      heightQuantSteps: heightQuantSteps,
                      hillsL1: hillsL1,
                      hillsL2: hillsL2,
                      hillsNoiseBlend: hillsNoiseBlend,
                      hillsNoise: hillsNoiseAsset != null
                          ? hillsNoiseAsset.Settings
                          : hillsNoiseSettings,
                      shapeMode: shapeMode);

            EnsureContextAllocated(eRes);
            EnsureTextureAllocated(eRes);

            // ---- Run pipeline ----
            // N4: feed noise settings to configurable stage.
            baseStage.terrainNoise = eTun.terrainNoise;
            baseStage.warpNoise = eTun.warpNoise;
            baseStage.heightQuantSteps = eTun.heightQuantSteps;

            var inputs = new MapInputs(
                seed: eSeed,
                domain: new GridDomain2D(eRes, eRes),
                tunables: eTun);

            var stages = (enableBiomeStage && eVeg && enableRegionsStage) ? stagesM2b
                       : (enableBiomeStage && eVeg) ? stagesM2a
                       : enableBiomeStage ? stagesM
                       : eMorph ? stagesG
                       : eTrav ? stagesF6
                       : eVeg ? stagesF5
                       : eShore ? stagesF4
                       : eHills ? stagesF3
                       : stagesF2;

            biomeStage.baseTemperature = biomeBaseTemperature;
            biomeStage.lapseRate = biomeLapseRate;
            biomeStage.latitudeEffect = biomeLatitudeEffect;
            biomeStage.coastModerationStrength = biomeCoastModerationStrength;
            biomeStage.tempNoiseAmplitude = biomeTempNoiseAmplitude;
            biomeStage.tempNoiseCellSize = biomeTempNoiseCellSize;
            biomeStage.coastalMoistureBonus = biomeCoastalMoistureBonus;
            biomeStage.coastDecayRate = biomeCoastDecayRate;
            biomeStage.moistureNoiseAmplitude = biomeMoistureNoiseAmplitude;
            biomeStage.moistureNoiseCellSize = biomeMoistureNoiseCellSize;

            MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: eClear);

            // ---- Build composite texture ----
            BuildCompositeTexture(eRes);

            dirty = false;
            updateCalls++;

            Debug.Log(
                $"[PCGMapCompositeVisualization] Update #{updateCalls} res={eRes} seed={eSeed} " +
                $"shape={eTun.shapeMode} " +
                $"hills={eHills} shore={eShore} " +
                $"veg={eVeg} traversal={eTrav} " +
                $"morphology={eMorph} " +
                $"overlay={enableScalarOverlay}({overlayField}) " +
                $"compositeHash={ComputePixelHash():X16}");
        }

        // =====================================================================
        // Composite texture build
        // =====================================================================
        private void BuildCompositeTexture(int res)
        {
            CompositeLayerSlot[] slots = layerSlots;
            int slotCount = (int)MapLayerId.COUNT;
            if (slots == null || slots.Length != slotCount)
                slots = BuildDefaultSlots();

            int priorityCount = s_compositePriority.Length;
            bool[] slotActive = new bool[priorityCount];
            MaskGrid2D[] slotLayer = new MaskGrid2D[priorityCount];
            Color[] slotColor = new Color[priorityCount];

            for (int p = 0; p < priorityCount; p++)
            {
                MapLayerId lid = s_compositePriority[p];
                int idx = (int)lid;
                CompositeLayerSlot slot = slots[idx];
                bool active = slot.enabled && ctx.IsLayerCreated(lid);
                slotActive[p] = active;
                if (active)
                {
                    slotLayer[p] = ctx.GetLayer(lid);
                    slotColor[p] = slot.color;
                }
            }

            bool hasLand = ctx.IsLayerCreated(MapLayerId.Land);
            MaskGrid2D landLayer = hasLand ? ctx.GetLayer(MapLayerId.Land) : default;
            Color deepWaterFallback = slots[(int)MapLayerId.DeepWater].color;

            bool hasOverlay = enableScalarOverlay && ctx.IsFieldCreated(overlayField);
            float oRange = overlayMax - overlayMin;
            float oInvRange = (oRange > 1e-6f) ? (1f / oRange) : 0f;
            ScalarField2D overlayData = hasOverlay ? ctx.GetField(overlayField) : default;
            Color oLow = overlayColorLow;
            Color oHigh = overlayColorHigh;
            float oMin = overlayMin;
            Color bgCol = backgroundColor;

            for (int y = 0; y < res; y++)
            {
                int rowBase = y * res;

                for (int x = 0; x < res; x++)
                {
                    Color cellColor = (hasLand && !landLayer.GetUnchecked(x, y))
                        ? deepWaterFallback
                        : bgCol;

                    for (int p = 0; p < priorityCount; p++)
                    {
                        if (slotActive[p] && slotLayer[p].GetUnchecked(x, y))
                            cellColor = slotColor[p];
                    }

                    if (hasOverlay)
                    {
                        float v = math.saturate((overlayData.GetUnchecked(x, y) - oMin) * oInvRange);
                        Color tint = Color.Lerp(oLow, oHigh, v);
                        cellColor *= tint;
                    }

                    pixels[rowBase + x] = (Color32)cellColor;
                }
            }

            compositeTexture.SetPixels32(pixels);
            compositeTexture.Apply(updateMipmaps: false);

            Renderer r = targetRenderer != null ? targetRenderer : GetComponent<Renderer>();
            if (r != null)
            {
                mpb.SetTexture(MainTexId, compositeTexture);
                r.SetPropertyBlock(mpb);
            }
        }

        private ulong ComputePixelHash()
        {
            const ulong FnvOffset = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong hash = FnvOffset;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                hash ^= c.r; hash *= FnvPrime;
                hash ^= c.g; hash *= FnvPrime;
                hash ^= c.b; hash *= FnvPrime;
            }
            return hash;
        }

        private ulong ComputeSlotsHash()
        {
            const ulong FnvOffset = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong h = FnvOffset;
            if (layerSlots == null) return h;

            for (int i = 0; i < layerSlots.Length; i++)
            {
                CompositeLayerSlot s = layerSlots[i];
                Color32 c = (Color32)s.color;
                h ^= s.enabled ? 1UL : 0UL; h *= FnvPrime;
                h ^= c.r; h *= FnvPrime;
                h ^= c.g; h *= FnvPrime;
                h ^= c.b; h *= FnvPrime;
            }
            return h;
        }

        private void EnsureContextAllocated(int res)
        {
            if (ctx != null && ctxResolution == res) return;
            ctx?.Dispose();
            ctx = null;
            ctxResolution = res;
            ctx = new MapContext2D(new GridDomain2D(res, res), Allocator.Persistent);
            dirty = true;
        }

        private void EnsureTextureAllocated(int res)
        {
            if (compositeTexture != null && texResolution == res) return;

            if (compositeTexture != null)
                DestroyImmediate(compositeTexture);

            texResolution = res;
            compositeTexture = new Texture2D(res, res, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "PCGMapCompositeVisualization_Tex",
            };
            pixels = new Color32[res * res];
        }

        private void AllocateStages()
        {
            baseStage = new BaseTerrainStage_Configurable();
            hillsStage = new Stage_Hills2D();
            shoreStage = new Stage_Shore2D();
            vegetationStage = new Stage_Vegetation2D();
            traversalStage = new Stage_Traversal2D();
            morphologyStage = new Stage_Morphology2D();

            stagesF2 = new IMapStage2D[] { baseStage };
            stagesF3 = new IMapStage2D[] { baseStage, hillsStage };
            stagesF4 = new IMapStage2D[] { baseStage, hillsStage, shoreStage };
            stagesF5 = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage };
            stagesF6 = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage };
            biomeStage = new Stage_Biome2D();
            regionsStage = new Stage_Regions2D();
            stagesG = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage };
            stagesM = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage, biomeStage };
            stagesM2a = new IMapStage2D[] { baseStage, hillsStage, shoreStage, traversalStage, morphologyStage, biomeStage, vegetationStage };
            stagesM2b = new IMapStage2D[] { baseStage, hillsStage, shoreStage, traversalStage, morphologyStage, biomeStage, vegetationStage, regionsStage };
        }

        // =====================================================================
        // Dirty tracking
        // =====================================================================

        // N5.b: resolve effective noise for dirty-tracking (asset → inline, preset → component)
        private TerrainNoiseSettings ResolveTerrainNoise()
        {
            if (preset != null)
                return preset.terrainNoiseAsset != null ? preset.terrainNoiseAsset.Settings : preset.terrainNoiseSettings;
            return terrainNoiseAsset != null ? terrainNoiseAsset.Settings : terrainNoiseSettings;
        }

        private TerrainNoiseSettings ResolveWarpNoise()
        {
            if (preset != null)
                return preset.warpNoiseAsset != null ? preset.warpNoiseAsset.Settings : preset.warpNoiseSettings;
            return warpNoiseAsset != null ? warpNoiseAsset.Settings : warpNoiseSettings;
        }

        private TerrainNoiseSettings ResolveHillsNoise()
        {
            if (preset != null)
                return preset.hillsNoiseAsset != null ? preset.hillsNoiseAsset.Settings : preset.hillsNoiseSettings;
            return hillsNoiseAsset != null ? hillsNoiseAsset.Settings : hillsNoiseSettings;
        }

        private void CacheParams()
        {
            _lastPreset = preset;
            lastSeed = preset != null ? preset.seed : seed;
            lastResolution = preset != null ? preset.resolution : resolution;
            lastEnableHillsStage = preset != null ? preset.enableHillsStage : enableHillsStage;
            lastEnableShoreStage = preset != null ? preset.enableShoreStage : enableShoreStage;
            lastEnableVegetationStage = preset != null ? preset.enableVegetationStage : enableVegetationStage;
            lastEnableTraversalStage = preset != null ? preset.enableTraversalStage : enableTraversalStage;
            lastEnableMorphologyStage = preset != null ? preset.enableMorphologyStage : enableMorphologyStage;
            lastEnableBiomeStage = enableBiomeStage;
            lastEnableRegionsStage = enableRegionsStage;
            lastShapeMode = preset != null ? preset.shapeMode : shapeMode;
            lastIslandRadius01 = preset != null ? preset.islandRadius01 : islandRadius01;
            lastWaterThreshold01 = preset != null ? preset.waterThreshold01 : waterThreshold01;
            lastIslandSmoothFrom01 = preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01;
            lastIslandSmoothTo01 = preset != null ? preset.islandSmoothTo01 : islandSmoothTo01;
            lastIslandAspectRatio = preset != null ? preset.islandAspectRatio : islandAspectRatio;
            lastWarpAmplitude01 = preset != null ? preset.warpAmplitude01 : warpAmplitude01;
            lastHeightRedistributionExponent = preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent;
            // F3b / N5.e hills params
            lastHillsL1 = preset != null ? preset.hillsL1 : hillsL1;
            lastHillsL2 = preset != null ? preset.hillsL2 : hillsL2;
            lastHillsNoiseBlend = preset != null ? preset.hillsNoiseBlend : hillsNoiseBlend;
            // N5.b: noise (asset + struct, replaces 11 individual fields)
            lastTerrainNoiseAsset = preset != null ? preset.terrainNoiseAsset : terrainNoiseAsset;
            lastWarpNoiseAsset = preset != null ? preset.warpNoiseAsset : warpNoiseAsset;
            lastTerrainNoise = ResolveTerrainNoise();
            lastWarpNoise = ResolveWarpNoise();
            lastHillsNoiseAsset = preset != null ? preset.hillsNoiseAsset : hillsNoiseAsset;
            lastHillsNoise = ResolveHillsNoise();
            lastHeightQuantSteps = preset != null ? preset.heightQuantSteps : heightQuantSteps;
            lastClearBeforeRun = preset != null ? preset.clearBeforeRun : clearBeforeRun;
            lastEnableScalarOverlay = enableScalarOverlay;
            lastOverlayField = overlayField;
            lastOverlayMin = overlayMin;
            lastOverlayMax = overlayMax;
            lastBackgroundColor = backgroundColor;
            lastSlotsHash = ComputeSlotsHash();
        }

        private bool ParamsChanged()
        {
            return preset != _lastPreset
                || (preset != null ? preset.seed : seed) != lastSeed
                || (preset != null ? preset.resolution : resolution) != lastResolution
                || (preset != null ? preset.enableHillsStage : enableHillsStage) != lastEnableHillsStage
                || (preset != null ? preset.enableShoreStage : enableShoreStage) != lastEnableShoreStage
                || (preset != null ? preset.enableVegetationStage : enableVegetationStage) != lastEnableVegetationStage
                || (preset != null ? preset.enableTraversalStage : enableTraversalStage) != lastEnableTraversalStage
                || (preset != null ? preset.enableMorphologyStage : enableMorphologyStage) != lastEnableMorphologyStage
                || enableBiomeStage != lastEnableBiomeStage
                || enableRegionsStage != lastEnableRegionsStage
                || (preset != null ? preset.shapeMode : shapeMode) != lastShapeMode
                || !Mathf.Approximately(preset != null ? preset.islandRadius01 : islandRadius01, lastIslandRadius01)
                || !Mathf.Approximately(preset != null ? preset.waterThreshold01 : waterThreshold01, lastWaterThreshold01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01, lastIslandSmoothFrom01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothTo01 : islandSmoothTo01, lastIslandSmoothTo01)
                || !Mathf.Approximately(preset != null ? preset.islandAspectRatio : islandAspectRatio, lastIslandAspectRatio)
                || !Mathf.Approximately(preset != null ? preset.warpAmplitude01 : warpAmplitude01, lastWarpAmplitude01)
                || !Mathf.Approximately(preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent, lastHeightRedistributionExponent)
                // F3b / N5.e hills params
                || !Mathf.Approximately(preset != null ? preset.hillsL1 : hillsL1, lastHillsL1)
                || !Mathf.Approximately(preset != null ? preset.hillsL2 : hillsL2, lastHillsL2)
                || !Mathf.Approximately(preset != null ? preset.hillsNoiseBlend : hillsNoiseBlend, lastHillsNoiseBlend)
                // N5.b: noise (asset ref + resolved struct comparison)
                || (preset != null ? preset.terrainNoiseAsset : terrainNoiseAsset) != lastTerrainNoiseAsset
                || (preset != null ? preset.warpNoiseAsset : warpNoiseAsset) != lastWarpNoiseAsset
                || !ResolveTerrainNoise().Equals(lastTerrainNoise)
                || !ResolveWarpNoise().Equals(lastWarpNoise)
                || (preset != null ? preset.hillsNoiseAsset : hillsNoiseAsset) != lastHillsNoiseAsset
                || !ResolveHillsNoise().Equals(lastHillsNoise)
                || (preset != null ? preset.heightQuantSteps : heightQuantSteps) != lastHeightQuantSteps
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun
                || enableScalarOverlay != lastEnableScalarOverlay
                || overlayField != lastOverlayField
                || !Mathf.Approximately(overlayMin, lastOverlayMin)
                || !Mathf.Approximately(overlayMax, lastOverlayMax)
                || backgroundColor != lastBackgroundColor
                || ComputeSlotsHash() != lastSlotsHash;
        }

    }
}