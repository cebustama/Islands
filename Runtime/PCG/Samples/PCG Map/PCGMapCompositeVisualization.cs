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
    /// LandCore sits just above Land so it tints the deep interior a distinct color, while
    /// all terrain features (Vegetation, Hills, Stairs, LandEdge) still render on top.
    ///
    /// An optional scalar field tint overlay (Height or CoastDist) can be blended
    /// multiplicatively on top of the composite.
    ///
    /// Rendering: CPU-written Texture2D uploaded each dirty frame (Phase I2 will add a
    /// GPU equivalent using the existing buffer infrastructure).
    ///
    /// Phase H1: initial implementation.
    /// Phase H3: optional MapGenerationPreset slot (override-at-resolve pattern).
    ///           When assigned, all pipeline parameters are read from the preset; inline
    ///           Inspector fields remain active as fallback when preset is null.
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
        [Tooltip("MeshRenderer that receives the composite Texture2D via MaterialPropertyBlock.\n" +
                 "Leave empty to use GetComponent<Renderer>() on this GameObject.\n" +
                 "The GameObject needs a Quad mesh with an Unlit/Texture (or equivalent) material.")]
        [SerializeField] private Renderer targetRenderer;

        // =====================================================================
        // Inspector — Run Inputs
        // =====================================================================
        [Header("Run Inputs")]
        [Tooltip("Deterministic seed (uint). Same seed + same tunables => same map.")]
        [SerializeField] private uint seed = 1u;

        [Tooltip("Map grid resolution (width = height in cells).\n" +
                 "Changing resolution reallocates the MapContext2D and Texture2D.")]
        [Min(4)]
        [SerializeField] private int resolution = 64;

        // =====================================================================
        // Inspector — Pipeline
        // =====================================================================
        [Header("Pipeline")]
        [Tooltip("Include F3 Hills + topology stage.")]
        [SerializeField] private bool enableHillsStage = true;

        [Tooltip("Include F4 Shore (ShallowWater) stage. Requires Hills enabled for correct results.")]
        [SerializeField] private bool enableShoreStage = true;

        [Tooltip("Include F5 Vegetation stage. Requires Shore enabled for correct results.")]
        [SerializeField] private bool enableVegetationStage = true;

        [Tooltip("Include F6 Traversal (Walkable + Stairs) stage. Requires Vegetation enabled.")]
        [SerializeField] private bool enableTraversalStage = true;

        [Tooltip("Include Phase G Morphology (LandCore + CoastDist) stage. Requires Traversal enabled.\n" +
                 "LandCore tints the deep interior teal (priority: above Land, below Vegetation).")]
        [SerializeField] private bool enableMorphologyStage = true;

        // =====================================================================
        // Inspector — Layer Slots
        // =====================================================================
        [Header("Layer Slots  (one per MapLayerId, indexed 0–11)")]
        [Tooltip("Background color rendered for cells not covered by any priority layer.")]
        [SerializeField] private Color backgroundColor = new Color(0.02f, 0.02f, 0.08f, 1f);

        [Tooltip("One slot per MapLayerId (COUNT=12), indexed by MapLayerId integer value.\n\n" +
                 "Active priority order (low → high):\n" +
                 "  DeepWater(1) → ShallowWater(2) → Land(0) → LandCore(11)\n" +
                 "  → Vegetation(7) → HillsL1(3) → HillsL2(4) → Stairs(6) → LandEdge(9)\n\n" +
                 "Slots not in the priority list (Paths=5, Walkable=8, LandInterior=10)\n" +
                 "are ignored by the composite regardless of their enabled flag.")]
        [SerializeField] private CompositeLayerSlot[] layerSlots = BuildDefaultSlots();

        // =====================================================================
        // Inspector — Scalar Field Overlay
        // =====================================================================
        [Header("Scalar Field Overlay (optional)")]
        [Tooltip("When enabled, the selected scalar field is blended multiplicatively over the composite.")]
        [SerializeField] private bool enableScalarOverlay = false;

        [Tooltip("Which scalar field to use as the overlay tint source.\n" +
                 "Height (F2): range [0..1]. Suggested overlayMin=0, overlayMax=1.\n" +
                 "CoastDist (Phase G): -1 sentinel for water/unreached, 0 at coast, positive inland.\n" +
                 "  Suggested overlayMin=-1, overlayMax=20.\n" +
                 "Requires the producing stage to be enabled.")]
        [SerializeField] private MapFieldId overlayField = MapFieldId.Height;

        [Tooltip("Scalar value mapped to Overlay Color Low (ramp low end).")]
        [SerializeField] private float overlayMin = 0f;

        [Tooltip("Scalar value mapped to Overlay Color High (ramp high end).")]
        [SerializeField] private float overlayMax = 1f;

        [Tooltip("Multiplicative tint at the low end of the overlay ramp. White = no tint.")]
        [SerializeField] private Color overlayColorLow = new Color(0.40f, 0.40f, 0.65f, 1f);

        [Tooltip("Multiplicative tint at the high end of the overlay ramp.")]
        [SerializeField] private Color overlayColorHigh = Color.white;

        // =====================================================================
        // Inspector — F2 Tunables
        // =====================================================================
        [Header("F2 Tunables (Shape + Threshold)")]
        [Range(0f, 1f)][SerializeField] private float islandRadius01 = 0.45f;
        [Range(0f, 1f)][SerializeField] private float waterThreshold01 = 0.50f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothFrom01 = 0.30f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothTo01 = 0.70f;

        [Header("F2 Tunables (Island Shape — Ellipse + Warp)")]
        [Tooltip("Ellipse aspect ratio. 1.0 = circle. >1 = wider. <1 = taller. Range [0.25..4.0].")]
        [Range(0.25f, 4f)]
        [SerializeField] private float islandAspectRatio = 1.00f;

        [Tooltip("Domain warp amplitude as a fraction of map size. " +
                 "0 = no warp. ~0.15 = subtle organic coast. ~0.30 = strong bays.")]
        [Range(0f, 1f)]
        [SerializeField] private float warpAmplitude01 = 0.00f;

        [Header("F2 Tunables (Noise Inside Island)")]
        [Min(1)][SerializeField] private int noiseCellSize = 8;
        [Range(0f, 1f)][SerializeField] private float noiseAmplitude = 0.18f;
        [Min(0)][SerializeField] private int quantSteps = 1024;

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

        // =====================================================================
        // Dirty tracking cache (H3: effective values cached)
        // =====================================================================
        private MapGenerationPreset _lastPreset;
        private uint lastSeed;
        private int lastResolution;
        private bool lastEnableHillsStage;
        private bool lastEnableShoreStage;
        private bool lastEnableVegetationStage;
        private bool lastEnableTraversalStage;
        private bool lastEnableMorphologyStage;
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
            {
                DestroyImmediate(compositeTexture);
                compositeTexture = null;
            }
            pixels = null;
            texResolution = -1;
            mpb = null;

            baseStage = null;
            hillsStage = null;
            shoreStage = null;
            vegetationStage = null;
            traversalStage = null;
            morphologyStage = null;
            stagesF2 = stagesF3 = stagesF4 = stagesF5 = stagesF6 = stagesG = null;
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

            EnsureContextAllocated(eRes);
            EnsureTextureAllocated(eRes);

            // ---- Run pipeline ----
            baseStage.noiseCellSize = Mathf.Max(1, eCell);
            baseStage.noiseAmplitude = Mathf.Max(0f, eAmp);
            baseStage.quantSteps = Mathf.Max(0, eQuant);

            var inputs = new MapInputs(
                seed: eSeed,
                domain: new GridDomain2D(eRes, eRes),
                tunables: eTun);

            var stages = eMorph ? stagesG
                       : eTrav ? stagesF6
                       : eVeg ? stagesF5
                       : eShore ? stagesF4
                       : eHills ? stagesF3
                       : stagesF2;

            MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: eClear);

            // ---- Build composite texture ----
            BuildCompositeTexture(eRes);

            dirty = false;
            updateCalls++;

            Debug.Log(
                $"[PCGMapCompositeVisualization] Update #{updateCalls} res={eRes} seed={eSeed} " +
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
            stagesG = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage };
        }

        // =====================================================================
        // Dirty tracking (H3: effective values cached, not raw fields)
        // =====================================================================
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
                || !Mathf.Approximately(preset != null ? preset.islandRadius01 : islandRadius01, lastIslandRadius01)
                || !Mathf.Approximately(preset != null ? preset.waterThreshold01 : waterThreshold01, lastWaterThreshold01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01, lastIslandSmoothFrom01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothTo01 : islandSmoothTo01, lastIslandSmoothTo01)
                || !Mathf.Approximately(preset != null ? preset.islandAspectRatio : islandAspectRatio, lastIslandAspectRatio)
                || !Mathf.Approximately(preset != null ? preset.warpAmplitude01 : warpAmplitude01, lastWarpAmplitude01)
                || (preset != null ? preset.noiseCellSize : noiseCellSize) != lastNoiseCellSize
                || !Mathf.Approximately(preset != null ? preset.noiseAmplitude : noiseAmplitude, lastNoiseAmplitude)
                || (preset != null ? preset.quantSteps : quantSteps) != lastQuantSteps
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun
                || enableScalarOverlay != lastEnableScalarOverlay
                || overlayField != lastOverlayField
                || !Mathf.Approximately(overlayMin, lastOverlayMin)
                || !Mathf.Approximately(overlayMax, lastOverlayMax)
                || backgroundColor != lastBackgroundColor
                || ComputeSlotsHash() != lastSlotsHash;
        }

        // =====================================================================
        // BaseTerrainStage_Configurable
        //
        // IMPORTANT: Keep in sync with Stage_BaseTerrain2D and the identical copy
        // inside PCGMapVisualization.  Both must produce bit-identical outputs for
        // the same inputs and RNG state.  The two copies exist because the class is
        // a private nested sample-side helper; factoring it out as `internal` is a
        // follow-up task once three or more consumers exist.
        // =====================================================================
        private sealed class BaseTerrainStage_Configurable : IMapStage2D
        {
            public string Name => "base_terrain_configurable";

            public int noiseCellSize;
            public float noiseAmplitude;
            public int quantSteps;

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
}