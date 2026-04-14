using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;
using Islands.PCG.Operators;
using Islands.PCG.Samples;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Live tilemap visualization for the PCG Map Pipeline.
    ///
    /// Runs the full pipeline in the Editor every time any tunable changes
    /// ([ExecuteAlways] + dirty tracking), exports a <see cref="MapDataExport"/>,
    /// and stamps the result into an assigned Unity <see cref="UnityEngine.Tilemaps.Tilemap"/>.
    ///
    /// Tile resolution priority (high → low):
    ///   1. Procedural mode (useProceduralTiles = true)
    ///   2. TilesetConfig SO (tilesetConfig assigned)
    ///
    /// When a <see cref="MapGenerationPreset"/> is assigned, all pipeline parameters
    /// are read from the preset and the inline fields are hidden by the custom Editor.
    ///
    /// Phase H2c: initial implementation.
    /// Phase H2d: procedural tile mode.
    /// Phase H3: MapGenerationPreset + TilesetConfig slots.
    /// Phase H5: Multi-layer Tilemap support.
    /// Phase N2: heightRemapCurve + scalar field overlay + Inspector cleanup.
    /// Post-N2: overlay tint fix (Issue 1), scalar heatmap tilemap (Issue 2),
    ///          ShallowWater removed from collider partition (Issue 3).
    /// Phase N4: TerrainNoiseSettings replaces noiseCellSize/noiseAmplitude/quantSteps.
    ///           Separate warp noise settings. heightQuantSteps tunable.
    /// Phase N5.b: NoiseSettingsAsset slots. Refactored individual noise fields to embedded
    ///             TerrainNoiseSettings structs with IEquatable dirty-tracking.
    /// Phase N5.e: Hills threshold UX remap — hillsThresholdL1/L2 → hillsL1/L2 (relative fractions).
    /// Phase N6: Scalar overlay replaced — Texture2D + SpriteRenderer replaces heatmap tilemap.
    ///           Two independent overlay slots with ScalarOverlaySource (pipeline fields + noise previews).
    ///           Legacy per-cell tint and heatmap tilemap paths removed.
    /// Phase H8: Mega-tile support — 2×2 large terrain sprite replacement via adapter post-pass.
    ///           MegaTileScanner + MegaTileStamper. Pure adapter-side, no pipeline mutation.
    /// Phase M: enableBiomeStage toggle + Stage_Biome2D wiring. Temperature/Biome overlay sources.
    /// M-fix.a: 10 biome climate tunables promoted to Inspector (serialized fields + dirty tracking).
    ///          Moisture defaults adjusted (M-fix.c folded in). Golden break.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Islands/PCG/Map Tilemap Visualization")]
    public sealed class PCGMapTilemapVisualization : MonoBehaviour
    {
        // =====================================================================
        // Tilemap Target (always visible)
        // =====================================================================
        [Header("Tilemap Target")]
        [Tooltip("The Unity Tilemap to stamp. In Multi-layer mode this receives the base layers.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap tilemap;

        // =====================================================================
        // Preset (always visible)
        // =====================================================================
        [Header("Preset")]
        [Tooltip("Assign a MapGenerationPreset to control all pipeline parameters.\n" +
                 "When assigned, inline parameter fields are hidden and ignored.")]
        [SerializeField] private MapGenerationPreset preset;

        // =====================================================================
        // Tileset Config (always visible)
        // =====================================================================
        [Header("Tileset Config")]
        [Tooltip("Assign a TilesetConfig asset to provide tile art per layer.\n" +
                 "Ignored when Use Procedural Tiles is enabled.")]
        [SerializeField] private TilesetConfig tilesetConfig;

        // =====================================================================
        // Preset-controlled fields (hidden when preset assigned)
        // Section names and field order match MapGenerationPreset exactly.
        // =====================================================================

        [Header("Run Inputs")]
        [SerializeField] private uint seed = 1u;
        [Min(4)]
        [SerializeField] private int resolution = 64;

        [Header("Stage Toggles")]
        [SerializeField] private bool enableHillsStage = true;
        [SerializeField] private bool enableShoreStage = true;
        [SerializeField] private bool enableVegetationStage = true;
        [SerializeField] private bool enableTraversalStage = true;
        [SerializeField] private bool enableMorphologyStage = true;
        [SerializeField] private bool enableBiomeStage = true;
        [SerializeField] private bool enableRegionsStage = true;

        [Header("Island Shape")]
        [Range(0f, 1f)][SerializeField] private float islandRadius01 = 0.45f;
        [Range(0.25f, 4f)][SerializeField] private float islandAspectRatio = 1.00f;
        [Range(0f, 1f)][SerializeField] private float warpAmplitude01 = 0.00f;
        [SerializeField] private IslandShapeMode shapeMode = IslandShapeMode.Ellipse;
        [Range(0f, 1f)][SerializeField] private float islandSmoothFrom01 = 0.30f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothTo01 = 0.70f;

        [Header("Water & Shore")]
        [Range(0f, 1f)][SerializeField] private float waterThreshold01 = 0.50f;
        [Range(0f, 0.5f)][SerializeField] private float shallowWaterDepth01 = 0f;
        [Range(0f, 0.5f)][SerializeField] private float midWaterDepth01 = 0f;

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

        [Header("Height Redistribution (J2)")]
        [Range(0.5f, 4f)][SerializeField] private float heightRedistributionExponent = 1.0f;

        [Header("Hills (F3b / N5.e)")]
        [Range(0f, 1f)]
        [Tooltip("Hill slopes (HillsL1) — fraction of the land height range.\n" +
                 "0.0 = all land eligible for hills. 1.0 = no hills.\n" +
                 "Effective threshold = waterThreshold + hillsL1 × (1 − waterThreshold).\n" +
                 "Default 0.30 ≈ effective 0.65 at default water threshold.")]
        [SerializeField] private float hillsL1 = 0.30f;
        [Range(0f, 1f)]
        [Tooltip("Hill peaks (HillsL2) — fraction of the remaining range above L1.\n" +
                 "0.0 = L2 starts at L1 (L1 band empty). 1.0 = only highest cells are peaks.\n" +
                 "Effective threshold = L1_eff + hillsL2 × (1 − L1_eff).\n" +
                 "Default 0.43 ≈ effective 0.80 at default water threshold.")]
        [SerializeField] private float hillsL2 = 0.43f;
        [Range(0f, 1f)]
        [Tooltip("Noise modulation of hill boundaries (N5.d).\n" +
                 "0.0 = pure height-threshold (default).\n" +
                 "0.5 = moderate variation.\n" +
                 "1.0 = maximum noise influence.")]
        [SerializeField] private float hillsNoiseBlend = 0f;

        [Header("Height Remap (N2)")]
        [Tooltip("Height remap curve applied after power redistribution.\n" +
                 "Straight diagonal (0,0)→(1,1) = identity. Sampled to a spline at runtime.")]
        [SerializeField] private AnimationCurve heightRemapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Biome Climate (Phase M)")]
        [Range(0f, 1f)]
        [Tooltip("Sea-level equatorial base temperature. 0.7 = warm tropical islands.")]
        [SerializeField] private float biomeBaseTemperature = 0.7f;
        [Range(0f, 1f)]
        [Tooltip("Height-to-temperature reduction. 0.5 = highest peaks lose half base temp.")]
        [SerializeField] private float biomeLapseRate = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("Y-axis latitude gradient strength. 0.0 for single-island. Non-zero for Phase W world maps.")]
        [SerializeField] private float biomeLatitudeEffect = 0.0f;
        [Range(0f, 0.5f)]
        [Tooltip("Coastal temperature moderation. 1/(1+coastDist) falloff.")]
        [SerializeField] private float biomeCoastModerationStrength = 0.1f;
        [Range(0f, 0.3f)]
        [Tooltip("Temperature noise amplitude. Low-frequency perturbation.")]
        [SerializeField] private float biomeTempNoiseAmplitude = 0.05f;
        [Min(1)]
        [Tooltip("Temperature noise cell size (frequency). Coarse; 2× terrain noise freq.")]
        [SerializeField] private int biomeTempNoiseCellSize = 16;
        [Range(0f, 1f)]
        [Tooltip("Coastal proximity moisture bonus at coast.")]
        [SerializeField] private float biomeCoastalMoistureBonus = 0.5f;
        [Range(0f, 1f)]
        [Tooltip("Coastal moisture decay rate. Higher = faster inland decay.")]
        [SerializeField] private float biomeCoastDecayRate = 0.3f;
        [Range(0f, 1f)]
        [Tooltip("Moisture noise amplitude. Perturbation; coast gradient is dominant.")]
        [SerializeField] private float biomeMoistureNoiseAmplitude = 0.3f;
        [Min(1)]
        [Tooltip("Moisture noise cell size. 4–8× lower frequency than terrain noise.")]
        [SerializeField] private int biomeMoistureNoiseCellSize = 32;

        [Header("Run Behavior")]
        [SerializeField] private bool clearBeforeRun = true;

        // =====================================================================
        // Component-specific fields (always visible)
        // =====================================================================

        [Header("Tilemap Options")]
        [Tooltip("Mirrors Y axis. Use when the map renders upside down.")]
        [SerializeField] private bool flipY = false;

        [Header("Multi-layer Tilemaps (H5)")]
        [Tooltip("Separate base/overlay/collider layers across multiple Tilemaps.")]
        [SerializeField] private bool enableMultiLayer = false;
        [SerializeField] private UnityEngine.Tilemaps.Tilemap overlayTilemap;
        [SerializeField] private UnityEngine.Tilemaps.Tilemap colliderTilemap;
        [SerializeField] private TileBase colliderTile;
        [SerializeField] private bool enableColliderAutoSetup = true;

        [Header("Mega-Tiles (H8)")]
        [Tooltip("Enable 2×2 mega-tile replacement for qualifying layer clusters.\n" +
                 "Runs as an adapter post-pass after standard tile stamping.")]
        [SerializeField] private bool enableMegaTiles = false;

        [Tooltip("Rules for 2×2 mega-tile replacement. Evaluated in order; earlier rules claim cells first.\n" +
                 "Each rule targets a specific mask layer and provides four quadrant tiles (TL/TR/BL/BR).")]
        [SerializeField] private MegaTileRule[] megaTileRules = System.Array.Empty<MegaTileRule>();

        [Header("Procedural Tiles")]
        [Tooltip("Generate solid-color tiles at runtime. Takes precedence over TilesetConfig.")]
        [SerializeField] private bool useProceduralTiles = false;
        [SerializeField] private ProceduralTileEntry[] proceduralColorTable = System.Array.Empty<ProceduralTileEntry>();
        [SerializeField] private Color proceduralFallbackColor = new Color(0.25f, 0.25f, 0.25f);

        [Header("Scalar Overlay 1 (N6)")]
        [Tooltip("Enable first scalar overlay. Renders as a Texture2D sprite\n" +
                 "aligned on top of the tilemap — no per-cell tile stamping.")]
        [SerializeField] private bool enableOverlay1 = false;
        [Tooltip("Data source for overlay 1.\n" +
                 "Pipeline fields (Height, CoastDist, Moisture) read from the pipeline context.\n" +
                 "Noise previews (TerrainNoise, WarpNoiseX/Y, HillsNoise) are computed on-demand\n" +
                 "with the exact same salts and settings the pipeline stages use.")]
        [SerializeField] private ScalarOverlaySource overlaySource1 = ScalarOverlaySource.Height;
        [SerializeField] private float overlayMin1 = 0f;
        [SerializeField] private float overlayMax1 = 1f;
        [SerializeField] private Color overlayColorLow1 = new Color(0.10f, 0.10f, 0.44f, 1f);
        [SerializeField] private Color overlayColorHigh1 = Color.white;
        [Range(0f, 1f)]
        [Tooltip("Opacity of overlay 1. 0 = invisible, 1 = opaque.")]
        [SerializeField] private float overlayAlpha1 = 0.65f;

        [Header("Scalar Overlay 2 (N6)")]
        [Tooltip("Enable second scalar overlay for A/B comparison.\n" +
                 "Independent source, color ramp, and alpha.")]
        [SerializeField] private bool enableOverlay2 = false;
        [SerializeField] private ScalarOverlaySource overlaySource2 = ScalarOverlaySource.TerrainNoise;
        [SerializeField] private float overlayMin2 = 0f;
        [SerializeField] private float overlayMax2 = 1f;
        [SerializeField] private Color overlayColorLow2 = new Color(0.44f, 0.10f, 0.10f, 1f);
        [SerializeField] private Color overlayColorHigh2 = Color.white;
        [Range(0f, 1f)]
        [SerializeField] private float overlayAlpha2 = 0.65f;

        // =====================================================================
        // Runtime state
        // =====================================================================
        private MapContext2D ctx;
        private int ctxResolution = -1;
        private bool dirty = true;
        private int updateCalls;

        private BaseTerrainStage_Configurable baseStage;
        private Stage_Hills2D hillsStage;
        private Stage_Shore2D shoreStage;
        private Stage_Vegetation2D vegetationStage;
        private Stage_Traversal2D traversalStage;
        private Stage_Morphology2D morphologyStage;
        private Stage_Biome2D biomeStage;
        private Stage_Regions2D regionsStage;

        private IMapStage2D[] stagesF2, stagesF3, stagesF4, stagesF5, stagesF6, stagesG, stagesM, stagesM2a, stagesM2b;

        // =====================================================================
        // Dirty tracking cache
        // =====================================================================
        private MapGenerationPreset _lastPreset;
        private TilesetConfig _lastTilesetConfig;
        private ulong _lastTilesetConfigHash;
        private uint lastSeed;
        private int lastResolution;
        private bool lastEnableHillsStage, lastEnableShoreStage;
        private float lastShallowWaterDepth01, lastMidWaterDepth01;
        private bool lastEnableVegetationStage, lastEnableTraversalStage, lastEnableMorphologyStage, lastEnableBiomeStage;
        private bool lastEnableRegionsStage;
        private float lastIslandRadius01, lastWaterThreshold01;
        private float lastIslandSmoothFrom01, lastIslandSmoothTo01;
        private float lastIslandAspectRatio, lastWarpAmplitude01;
        private IslandShapeMode lastShapeMode;
        private float lastHeightRedistributionExponent;
        // F3b / N5.e hills dirty tracking
        private float lastHillsL1;
        private float lastHillsL2;
        private float lastHillsNoiseBlend;
        private int lastHeightRemapCurveHash;
        // Phase M: biome climate dirty tracking
        private float lastBiomeBaseTemperature, lastBiomeLapseRate, lastBiomeLatitudeEffect;
        private float lastBiomeCoastModerationStrength, lastBiomeTempNoiseAmplitude;
        private int lastBiomeTempNoiseCellSize;
        private float lastBiomeCoastalMoistureBonus, lastBiomeCoastDecayRate, lastBiomeMoistureNoiseAmplitude;
        private int lastBiomeMoistureNoiseCellSize;
        // N5.b: noise dirty tracking (replaces 11 individual fields)
        private NoiseSettingsAsset lastTerrainNoiseAsset, lastWarpNoiseAsset, lastHillsNoiseAsset;
        private TerrainNoiseSettings lastTerrainNoise, lastWarpNoise, lastHillsNoise;
        private int lastHeightQuantSteps;
        private bool lastFlipY, lastClearBeforeRun;
        private bool lastUseProceduralTiles;
        private ulong lastProceduralHash;
        private Color lastProceduralFallbackColor;
        private bool lastEnableMultiLayer;
        private UnityEngine.Tilemaps.Tilemap lastOverlayTilemap, lastColliderTilemap;
        private TileBase lastColliderTile;
        private bool lastEnableColliderAutoSetup;
        // H8: mega-tile dirty tracking
        private bool lastEnableMegaTiles;
        private ulong lastMegaTileHash;
        // N6: overlay dirty tracking (replaces old scalar overlay + heatmap tilemap fields)
        private bool lastEnableOverlay1;
        private ScalarOverlaySource lastOverlaySource1;
        private float lastOverlayMin1, lastOverlayMax1;
        private Color lastOverlayColorLow1, lastOverlayColorHigh1;
        private float lastOverlayAlpha1;
        private bool lastEnableOverlay2;
        private ScalarOverlaySource lastOverlaySource2;
        private float lastOverlayMin2, lastOverlayMax2;
        private Color lastOverlayColorLow2, lastOverlayColorHigh2;
        private float lastOverlayAlpha2;

        // N6: overlay renderers and working buffers
        private ScalarOverlayRenderer _overlayRenderer1;
        private ScalarOverlayRenderer _overlayRenderer2;
        private float[] _overlayBuffer1;
        private float[] _overlayBuffer2;

        /// <summary>True when a MapGenerationPreset is assigned (inline fields hidden).</summary>
        public bool HasPreset => preset != null;

        // =====================================================================
        // MonoBehaviour lifecycle
        // =====================================================================
        private void OnEnable()
        {
            AllocateStages();
            CacheParams();
            dirty = true;
            updateCalls = 0;
        }

        private void OnDisable()
        {
            _overlayRenderer1?.Dispose(); _overlayRenderer1 = null;
            _overlayRenderer2?.Dispose(); _overlayRenderer2 = null;
            _overlayBuffer1 = null;
            _overlayBuffer2 = null;

            ctx?.Dispose();
            ctx = null;
            ctxResolution = -1;
            baseStage = null; hillsStage = null; shoreStage = null;
            vegetationStage = null; traversalStage = null; morphologyStage = null;
            biomeStage = null;
            regionsStage = null;
            stagesF2 = stagesF3 = stagesF4 = stagesF5 = stagesF6 = stagesG = stagesM = stagesM2a = stagesM2b = null;
        }

        private void Update()
        {
            // N6: auto-apply sensible min/max when overlay source changes.
            if (overlaySource1 != lastOverlaySource1)
                ApplyOverlaySourceDefaults(overlaySource1, ref overlayMin1, ref overlayMax1);
            if (overlaySource2 != lastOverlaySource2)
                ApplyOverlaySourceDefaults(overlaySource2, ref overlayMin2, ref overlayMax2);

            if (ParamsChanged())
            {
                CacheParams();
                dirty = true;
            }
            if (!dirty) return;

            if (tilemap == null)
            {
                Debug.LogWarning("[PCGMapTilemapVisualization] Tilemap not assigned.", this);
                dirty = false;
                return;
            }

            // ---- Resolve effective values ----
            uint rawSeed = preset != null ? preset.seed : seed;
            uint eSeed = rawSeed < 1u ? 1u : rawSeed;
            int eRes = Mathf.Max(4, preset != null ? preset.resolution : resolution);
            bool eHills = preset != null ? preset.enableHillsStage : enableHillsStage;
            bool eShore = preset != null ? preset.enableShoreStage : enableShoreStage;
            bool eVeg = preset != null ? preset.enableVegetationStage : enableVegetationStage;
            bool eTrav = preset != null ? preset.enableTraversalStage : enableTraversalStage;
            bool eMorph = preset != null ? preset.enableMorphologyStage : enableMorphologyStage;
            bool eBiome = preset != null ? preset.enableBiomeStage : enableBiomeStage;
            bool eRegions = enableRegionsStage;
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
                      ScalarSpline.FromAnimationCurve(heightRemapCurve),
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

            // N4: feed noise settings to configurable stage.
            baseStage.terrainNoise = eTun.terrainNoise;
            baseStage.warpNoise = eTun.warpNoise;
            baseStage.heightQuantSteps = eTun.heightQuantSteps;

            float eShallowDepth = preset != null ? preset.shallowWaterDepth01 : shallowWaterDepth01;
            float eMidDepth = preset != null ? preset.midWaterDepth01 : midWaterDepth01;
            shoreStage.ShallowWaterDepth01 = Mathf.Max(0f, eShallowDepth);
            shoreStage.MidWaterDepth01 = Mathf.Max(0f, eMidDepth);

            // Phase M: feed biome climate tunables to stage instance.
            biomeStage.baseTemperature = preset != null ? preset.biomeBaseTemperature : biomeBaseTemperature;
            biomeStage.lapseRate = preset != null ? preset.biomeLapseRate : biomeLapseRate;
            biomeStage.latitudeEffect = preset != null ? preset.biomeLatitudeEffect : biomeLatitudeEffect;
            biomeStage.coastModerationStrength = preset != null ? preset.biomeCoastModerationStrength : biomeCoastModerationStrength;
            biomeStage.tempNoiseAmplitude = preset != null ? preset.biomeTempNoiseAmplitude : biomeTempNoiseAmplitude;
            biomeStage.tempNoiseCellSize = preset != null ? preset.biomeTempNoiseCellSize : biomeTempNoiseCellSize;
            biomeStage.coastalMoistureBonus = preset != null ? preset.biomeCoastalMoistureBonus : biomeCoastalMoistureBonus;
            biomeStage.coastDecayRate = preset != null ? preset.biomeCoastDecayRate : biomeCoastDecayRate;
            biomeStage.moistureNoiseAmplitude = preset != null ? preset.biomeMoistureNoiseAmplitude : biomeMoistureNoiseAmplitude;
            biomeStage.moistureNoiseCellSize = preset != null ? preset.biomeMoistureNoiseCellSize : biomeMoistureNoiseCellSize;

            var inputs = new MapInputs(eSeed, new GridDomain2D(eRes, eRes), eTun);
            var stages = (eBiome && eVeg && eRegions) ? stagesM2b : (eBiome && eVeg) ? stagesM2a : eBiome ? stagesM : eMorph ? stagesG : eTrav ? stagesF6 : eVeg ? stagesF5
                       : eShore ? stagesF4 : eHills ? stagesF3 : stagesF2;

            MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: eClear);
            MapDataExport export = MapExporter2D.Export(ctx);

            // ---- Resolve tile table: Procedural > TilesetConfig ----
            TilemapLayerEntry[] activeTable;
            TileBase activeFallback;

            if (useProceduralTiles)
            {
                activeTable = ProceduralTileFactory.BuildPriorityTable(proceduralColorTable);
                activeFallback = ProceduralTileFactory.GetOrCreate(proceduralFallbackColor);
            }
            else if (tilesetConfig != null)
            {
                TilemapLayerEntry[] fromConfig = tilesetConfig.ToLayerEntries();
                activeTable = fromConfig ?? System.Array.Empty<TilemapLayerEntry>();
                activeFallback = tilesetConfig.fallbackTile;
            }
            else
            {
                Debug.LogWarning("[PCGMapTilemapVisualization] No tile source. " +
                    "Enable Procedural Tiles or assign a TilesetConfig.", this);
                activeTable = System.Array.Empty<TilemapLayerEntry>();
                activeFallback = null;
            }

            // ---- Stamp ----
            if (enableMultiLayer)
                StampMultiLayer(export, activeTable, activeFallback);
            else
                TilemapAdapter2D.Apply(export, tilemap, activeTable, activeFallback, true, flipY);

            // ---- H8: Mega-tile post-pass ----
            if (enableMegaTiles && megaTileRules != null && megaTileRules.Length > 0)
            {
                var megaPlacements = MegaTileScanner.Scan(export, megaTileRules);
                if (megaPlacements.Count > 0)
                {
                    if (enableMultiLayer)
                        MegaTileStamper.ApplyMultiLayer(tilemap, overlayTilemap,
                            megaPlacements, megaTileRules, eRes, flipY);
                    else
                        MegaTileStamper.Apply(tilemap,
                            megaPlacements, megaTileRules, eRes, flipY);
                }
            }

            // N6: Texture2D overlays (replaces heatmap tilemap stamping).
            ApplyOverlays(eRes, eTun, eSeed);

            int stamped = CountStampedTiles(eRes);
            dirty = false;
            updateCalls++;

            Debug.Log(
                $"[PCGMapTilemapVisualization] #{updateCalls} res={eRes} seed={eSeed} " +
                $"shape={eTun.shapeMode} " +
                $"hills={eHills} shore={eShore} veg={eVeg} trav={eTrav} morph={eMorph} biome={eBiome} " +
                $"flipY={flipY} proc={useProceduralTiles} multi={enableMultiLayer} " +
                $"mega={enableMegaTiles}({megaTileRules?.Length ?? 0}r) " +
                $"overlay1={enableOverlay1}({overlaySource1}) overlay2={enableOverlay2}({overlaySource2}) " +
                $"tiles={stamped}/{eRes * eRes}");
        }

        // =====================================================================
        // Scalar overlays — N6 Texture2D + SpriteRenderer system
        // Replaces the N2/post-N2 heatmap tilemap and per-cell tint paths.
        // =====================================================================

        // Stage salts — must match the governed stages exactly.
        private const uint TerrainNoiseSalt = 0xF2A10001u;
        private const uint WarpXNoiseSalt = 0xF2A20002u;
        private const uint WarpYNoiseSalt = 0xF2A30003u;
        private const uint HillsNoiseSalt = 0xF3D50001u;

        /// <summary>
        /// Create/update/hide both overlay renderers based on current enable state.
        /// Called after the pipeline runs and tiles are stamped.
        /// </summary>
        private void ApplyOverlays(int res, MapTunables2D tunables, uint seed)
        {
            ApplyOneOverlay(
                ref _overlayRenderer1, ref _overlayBuffer1,
                enableOverlay1, overlaySource1,
                overlayMin1, overlayMax1,
                overlayColorLow1, overlayColorHigh1,
                overlayAlpha1, res, tunables, seed,
                "ScalarOverlay1", 100);

            ApplyOneOverlay(
                ref _overlayRenderer2, ref _overlayBuffer2,
                enableOverlay2, overlaySource2,
                overlayMin2, overlayMax2,
                overlayColorLow2, overlayColorHigh2,
                overlayAlpha2, res, tunables, seed,
                "ScalarOverlay2", 101);
        }

        private void ApplyOneOverlay(
            ref ScalarOverlayRenderer renderer, ref float[] buffer,
            bool enabled, ScalarOverlaySource source,
            float min, float max,
            Color colorLow, Color colorHigh,
            float alpha, int res,
            MapTunables2D tunables, uint seed,
            string goName, int sortingOrder)
        {
            if (!enabled)
            {
                if (renderer != null) renderer.SetVisible(false);
                return;
            }

            // Lazy-create renderer.
            if (renderer == null)
                renderer = new ScalarOverlayRenderer(transform, goName, sortingOrder);

            // Ensure buffer sized correctly.
            int len = res * res;
            if (buffer == null || buffer.Length != len)
                buffer = new float[len];

            // Fill scalar data from the selected source.
            FillOverlayBuffer(source, buffer, res, tunables, seed);

            // Upload to texture and align.
            renderer.SetData(buffer, res, res, min, max, colorLow, colorHigh, flipY);
            renderer.SetAlpha(alpha);
            renderer.SetVisible(true);
            renderer.AlignToTilemap(tilemap);
        }

        /// <summary>
        /// Fill a float[] buffer from the selected <see cref="ScalarOverlaySource"/>.
        /// Pipeline fields read from <c>ctx</c>; noise previews computed on-demand
        /// via <see cref="MapNoiseBridge2D.FillNoise01"/> with stage-matching salts.
        /// </summary>
        private void FillOverlayBuffer(
            ScalarOverlaySource source, float[] dst,
            int res, MapTunables2D tunables, uint seed)
        {
            int len = res * res;

            switch (source)
            {
                case ScalarOverlaySource.Height:
                    FillFromField(MapFieldId.Height, dst, res);
                    break;

                case ScalarOverlaySource.CoastDist:
                    FillFromField(MapFieldId.CoastDist, dst, res);
                    break;

                case ScalarOverlaySource.Moisture:
                    FillFromField(MapFieldId.Moisture, dst, res);
                    break;

                case ScalarOverlaySource.Temperature:
                    FillFromField(MapFieldId.Temperature, dst, res);
                    break;

                case ScalarOverlaySource.Biome:
                    FillFromField(MapFieldId.Biome, dst, res);
                    break;

                case ScalarOverlaySource.BiomeRegionId:
                    FillFromField(MapFieldId.BiomeRegionId, dst, res);
                    break;

                case ScalarOverlaySource.TerrainNoise:
                    FillFromNoise(dst, res, seed, TerrainNoiseSalt, tunables.terrainNoise);
                    break;

                case ScalarOverlaySource.WarpNoiseX:
                    FillFromNoise(dst, res, seed, WarpXNoiseSalt, tunables.warpNoise);
                    break;

                case ScalarOverlaySource.WarpNoiseY:
                    FillFromNoise(dst, res, seed, WarpYNoiseSalt, tunables.warpNoise);
                    break;

                case ScalarOverlaySource.HillsNoise:
                    FillFromNoise(dst, res, seed, HillsNoiseSalt, tunables.hillsNoise);
                    break;

                case ScalarOverlaySource.ShapeMask:
                    FillShapeMask(dst, res, tunables, seed);
                    break;

                default:
                    System.Array.Clear(dst, 0, len);
                    break;
            }
        }

        /// <summary>
        /// Read a pipeline scalar field into the managed float[] buffer.
        /// Fills zeros if the field is not created (e.g. Moisture before Phase M).
        /// </summary>
        private void FillFromField(MapFieldId fieldId, float[] dst, int res)
        {
            if (!ctx.IsFieldCreated(fieldId))
            {
                System.Array.Clear(dst, 0, dst.Length);
                return;
            }

            ScalarField2D field = ctx.GetField(fieldId);
            for (int y = 0; y < res; y++)
            {
                int row = y * res;
                for (int x = 0; x < res; x++)
                    dst[row + x] = field.GetUnchecked(x, y);
            }
        }

        /// <summary>
        /// Compute noise on-demand and copy to managed float[] buffer.
        /// Uses a temporary <see cref="NativeArray{T}"/> for the bridge call.
        /// </summary>
        private void FillFromNoise(
            float[] dst, int res, uint seed, uint salt,
            in TerrainNoiseSettings noiseSettings)
        {
            var domain = new GridDomain2D(res, res);
            var tmp = new NativeArray<float>(domain.Length, Unity.Collections.Allocator.Temp);
            try
            {
                MapNoiseBridge2D.FillNoise01(in domain, tmp, seed, salt, in noiseSettings);
                tmp.CopyTo(dst);
            }
            finally
            {
                if (tmp.IsCreated) tmp.Dispose();
            }
        }

        /// <summary>
        /// Recompute the pure island shape mask [0,1] on-demand.
        /// Mirrors the mask01 computation in Stage_BaseTerrain2D (Ellipse / Rectangle /
        /// NoShape paths) using the same warp noise salts and tunables. Does NOT include
        /// noise perturbation, redistribution, quantization, or spline remap — those are
        /// post-mask operations visible via the Height overlay.
        ///
        /// When shapeMode = NoShape the mask is uniformly 1.0 (height = pure noise).
        /// When an F2c external shape is active, the built-in shape is shown as fallback
        /// (the visualizer does not have access to the external MaskGrid2D).
        /// </summary>
        private void FillShapeMask(float[] dst, int res, MapTunables2D tunables, uint seed)
        {
            int w = res, h = res;

            // NoShape: mask is uniformly 1.0 (height = pure noise, no silhouette).
            if (tunables.shapeMode == IslandShapeMode.NoShape)
            {
                for (int i = 0; i < dst.Length; i++) dst[i] = 1f;
                return;
            }

            // Shared geometry — mirrors Stage_BaseTerrain2D exactly.
            float minDim = math.min((float)w, (float)h);
            float radius = math.max(1f, minDim * tunables.islandRadius01);
            float invRadiusSq = 1f / (radius * radius);
            float fromSq = tunables.islandSmoothFrom01 * tunables.islandSmoothFrom01;
            float toSq = tunables.islandSmoothTo01 * tunables.islandSmoothTo01;
            float2 center = new float2(w * 0.5f, h * 0.5f);
            float aspect = tunables.islandAspectRatio;
            float invAspectSq = 1f / (aspect * aspect);
            float warpAmp = tunables.warpAmplitude01 * minDim;

            // Rectangle half-extents.
            float rectHalfX = radius * aspect;
            float rectHalfY = radius;
            float invRectHalfX = rectHalfX > 0f ? 1f / rectHalfX : 0f;
            float invRectHalfY = rectHalfY > 0f ? 1f / rectHalfY : 0f;

            bool isRect = tunables.shapeMode == IslandShapeMode.Rectangle;

            var domain = new GridDomain2D(w, h);
            int cellCount = w * h;
            var warpXArr = new NativeArray<float>(cellCount, Unity.Collections.Allocator.Temp);
            var warpYArr = new NativeArray<float>(cellCount, Unity.Collections.Allocator.Temp);
            try
            {
                MapNoiseBridge2D.FillNoise01(in domain, warpXArr, seed, WarpXNoiseSalt, in tunables.warpNoise);
                MapNoiseBridge2D.FillNoise01(in domain, warpYArr, seed, WarpYNoiseSalt, in tunables.warpNoise);

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int idx = row + x;
                        float wx = warpXArr[idx] * 2f - 1f;
                        float wy = warpYArr[idx] * 2f - 1f;
                        float2 p = new float2(x + 0.5f, y + 0.5f);
                        float2 pw = p + new float2(wx, wy) * warpAmp;
                        float2 v = pw - center;

                        float mask01;
                        if (isRect)
                        {
                            float fracX = math.abs(v.x) * invRectHalfX;
                            float fracY = math.abs(v.y) * invRectHalfY;
                            float rectDist01 = math.max(fracX, fracY);
                            float rectDistSq = rectDist01 * rectDist01;
                            mask01 = 1f - math.smoothstep(fromSq, toSq, rectDistSq);
                        }
                        else
                        {
                            // Ellipse (default + Custom fallback).
                            float distSq = v.x * v.x * invAspectSq + v.y * v.y;
                            float radial01Sq = math.saturate(distSq * invRadiusSq);
                            mask01 = 1f - math.smoothstep(fromSq, toSq, radial01Sq);
                        }

                        dst[idx] = mask01;
                    }
                }
            }
            finally
            {
                if (warpXArr.IsCreated) warpXArr.Dispose();
                if (warpYArr.IsCreated) warpYArr.Dispose();
            }
        }

        /// <summary>
        /// Set sensible min/max defaults when the overlay source changes.
        /// </summary>
        private static void ApplyOverlaySourceDefaults(
            ScalarOverlaySource source, ref float min, ref float max)
        {
            switch (source)
            {
                case ScalarOverlaySource.CoastDist:
                    min = -1f; max = 20f;
                    break;
                case ScalarOverlaySource.Height:
                case ScalarOverlaySource.Moisture:
                case ScalarOverlaySource.Temperature:
                case ScalarOverlaySource.TerrainNoise:
                case ScalarOverlaySource.WarpNoiseX:
                case ScalarOverlaySource.WarpNoiseY:
                case ScalarOverlaySource.HillsNoise:
                case ScalarOverlaySource.ShapeMask:
                default:
                    min = 0f; max = 1f;
                    break;
                case ScalarOverlaySource.Biome:
                    min = 0f; max = 12f;
                    break;
                case ScalarOverlaySource.BiomeRegionId:
                    min = 0f; max = 20f;
                    break;
            }
        }

        [ContextMenu("Reset Overlay 1 Range to Defaults")]
        private void ResetOverlay1Defaults() =>
            ApplyOverlaySourceDefaults(overlaySource1, ref overlayMin1, ref overlayMax1);

        [ContextMenu("Reset Overlay 2 Range to Defaults")]
        private void ResetOverlay2Defaults() =>
            ApplyOverlaySourceDefaults(overlaySource2, ref overlayMin2, ref overlayMax2);


        // =====================================================================
        // Helpers
        // =====================================================================
        private int CountStampedTiles(int res)
        {
            int count = 0;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    if (tilemap.GetTile(new Vector3Int(x, flipY ? (res - 1 - y) : y, 0)) != null)
                        count++;
            return count;
        }

        private void EnsureContextAllocated(int res)
        {
            if (ctx != null && ctxResolution == res) return;
            ctx?.Dispose(); ctx = null; ctxResolution = res;
            ctx = new MapContext2D(new GridDomain2D(res, res), Allocator.Persistent);
            dirty = true;
        }

        private void AllocateStages()
        {
            baseStage = new BaseTerrainStage_Configurable();
            hillsStage = new Stage_Hills2D();
            shoreStage = new Stage_Shore2D();
            vegetationStage = new Stage_Vegetation2D();
            traversalStage = new Stage_Traversal2D();
            morphologyStage = new Stage_Morphology2D();
            biomeStage = new Stage_Biome2D();
            regionsStage = new Stage_Regions2D();
            stagesF2 = new IMapStage2D[] { baseStage };
            stagesF3 = new IMapStage2D[] { baseStage, hillsStage };
            stagesF4 = new IMapStage2D[] { baseStage, hillsStage, shoreStage };
            stagesF5 = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage };
            stagesF6 = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage };
            stagesG = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage };
            stagesM = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage, biomeStage };
            // M2.a: vegetation moves AFTER biome. Activated when both Biome and Vegetation toggles are on.
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
            _lastTilesetConfig = tilesetConfig;
            _lastTilesetConfigHash = ComputeTilesetConfigHash();
            lastSeed = preset != null ? preset.seed : seed;
            lastResolution = preset != null ? preset.resolution : resolution;
            lastEnableHillsStage = preset != null ? preset.enableHillsStage : enableHillsStage;
            lastEnableShoreStage = preset != null ? preset.enableShoreStage : enableShoreStage;
            lastShallowWaterDepth01 = preset != null ? preset.shallowWaterDepth01 : shallowWaterDepth01;
            lastMidWaterDepth01 = preset != null ? preset.midWaterDepth01 : midWaterDepth01;
            lastEnableVegetationStage = preset != null ? preset.enableVegetationStage : enableVegetationStage;
            lastEnableTraversalStage = preset != null ? preset.enableTraversalStage : enableTraversalStage;
            lastEnableMorphologyStage = preset != null ? preset.enableMorphologyStage : enableMorphologyStage;
            lastEnableBiomeStage = preset != null ? preset.enableBiomeStage : enableBiomeStage;
            lastEnableRegionsStage = enableRegionsStage;
            lastIslandRadius01 = preset != null ? preset.islandRadius01 : islandRadius01;
            lastWaterThreshold01 = preset != null ? preset.waterThreshold01 : waterThreshold01;
            lastIslandSmoothFrom01 = preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01;
            lastIslandSmoothTo01 = preset != null ? preset.islandSmoothTo01 : islandSmoothTo01;
            lastIslandAspectRatio = preset != null ? preset.islandAspectRatio : islandAspectRatio;
            lastWarpAmplitude01 = preset != null ? preset.warpAmplitude01 : warpAmplitude01;
            lastShapeMode = preset != null ? preset.shapeMode : shapeMode;
            lastHeightRedistributionExponent = preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent;
            // F3b / N5.e hills params
            lastHillsL1 = preset != null ? preset.hillsL1 : hillsL1;
            lastHillsL2 = preset != null ? preset.hillsL2 : hillsL2;
            lastHillsNoiseBlend = preset != null ? preset.hillsNoiseBlend : hillsNoiseBlend;
            lastHeightRemapCurveHash = ComputeCurveHash(preset != null ? preset.heightRemapCurve : heightRemapCurve);
            // Phase M: biome climate
            lastBiomeBaseTemperature = preset != null ? preset.biomeBaseTemperature : biomeBaseTemperature;
            lastBiomeLapseRate = preset != null ? preset.biomeLapseRate : biomeLapseRate;
            lastBiomeLatitudeEffect = preset != null ? preset.biomeLatitudeEffect : biomeLatitudeEffect;
            lastBiomeCoastModerationStrength = preset != null ? preset.biomeCoastModerationStrength : biomeCoastModerationStrength;
            lastBiomeTempNoiseAmplitude = preset != null ? preset.biomeTempNoiseAmplitude : biomeTempNoiseAmplitude;
            lastBiomeTempNoiseCellSize = preset != null ? preset.biomeTempNoiseCellSize : biomeTempNoiseCellSize;
            lastBiomeCoastalMoistureBonus = preset != null ? preset.biomeCoastalMoistureBonus : biomeCoastalMoistureBonus;
            lastBiomeCoastDecayRate = preset != null ? preset.biomeCoastDecayRate : biomeCoastDecayRate;
            lastBiomeMoistureNoiseAmplitude = preset != null ? preset.biomeMoistureNoiseAmplitude : biomeMoistureNoiseAmplitude;
            lastBiomeMoistureNoiseCellSize = preset != null ? preset.biomeMoistureNoiseCellSize : biomeMoistureNoiseCellSize;
            // N5.b: noise (asset + struct, replaces 11 individual fields)
            lastTerrainNoiseAsset = preset != null ? preset.terrainNoiseAsset : terrainNoiseAsset;
            lastWarpNoiseAsset = preset != null ? preset.warpNoiseAsset : warpNoiseAsset;
            lastTerrainNoise = ResolveTerrainNoise();
            lastWarpNoise = ResolveWarpNoise();
            lastHillsNoiseAsset = preset != null ? preset.hillsNoiseAsset : hillsNoiseAsset;
            lastHillsNoise = ResolveHillsNoise();
            lastHeightQuantSteps = preset != null ? preset.heightQuantSteps : heightQuantSteps;
            lastFlipY = flipY;
            lastClearBeforeRun = preset != null ? preset.clearBeforeRun : clearBeforeRun;
            lastUseProceduralTiles = useProceduralTiles;
            lastProceduralFallbackColor = proceduralFallbackColor;
            lastProceduralHash = ComputeProceduralHash();
            lastEnableMultiLayer = enableMultiLayer;
            lastOverlayTilemap = overlayTilemap;
            lastColliderTilemap = colliderTilemap;
            lastColliderTile = colliderTile;
            lastEnableColliderAutoSetup = enableColliderAutoSetup;
            // H8: mega-tiles
            lastEnableMegaTiles = enableMegaTiles;
            lastMegaTileHash = ComputeMegaTileHash();
            // N6: overlay dirty tracking
            lastEnableOverlay1 = enableOverlay1;
            lastOverlaySource1 = overlaySource1;
            lastOverlayMin1 = overlayMin1;
            lastOverlayMax1 = overlayMax1;
            lastOverlayColorLow1 = overlayColorLow1;
            lastOverlayColorHigh1 = overlayColorHigh1;
            lastOverlayAlpha1 = overlayAlpha1;
            lastEnableOverlay2 = enableOverlay2;
            lastOverlaySource2 = overlaySource2;
            lastOverlayMin2 = overlayMin2;
            lastOverlayMax2 = overlayMax2;
            lastOverlayColorLow2 = overlayColorLow2;
            lastOverlayColorHigh2 = overlayColorHigh2;
            lastOverlayAlpha2 = overlayAlpha2;
        }

        private bool ParamsChanged()
        {
            return preset != _lastPreset || tilesetConfig != _lastTilesetConfig
                || ComputeTilesetConfigHash() != _lastTilesetConfigHash
                || (preset != null ? preset.seed : seed) != lastSeed
                || (preset != null ? preset.resolution : resolution) != lastResolution
                || (preset != null ? preset.enableHillsStage : enableHillsStage) != lastEnableHillsStage
                || (preset != null ? preset.enableShoreStage : enableShoreStage) != lastEnableShoreStage
                || !Mathf.Approximately(preset != null ? preset.shallowWaterDepth01 : shallowWaterDepth01, lastShallowWaterDepth01)
                || !Mathf.Approximately(preset != null ? preset.midWaterDepth01 : midWaterDepth01, lastMidWaterDepth01)
                || (preset != null ? preset.enableVegetationStage : enableVegetationStage) != lastEnableVegetationStage
                || (preset != null ? preset.enableTraversalStage : enableTraversalStage) != lastEnableTraversalStage
                || (preset != null ? preset.enableMorphologyStage : enableMorphologyStage) != lastEnableMorphologyStage
                || (preset != null ? preset.enableBiomeStage : enableBiomeStage) != lastEnableBiomeStage
                || enableRegionsStage != lastEnableRegionsStage
                || !Mathf.Approximately(preset != null ? preset.islandRadius01 : islandRadius01, lastIslandRadius01)
                || !Mathf.Approximately(preset != null ? preset.waterThreshold01 : waterThreshold01, lastWaterThreshold01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01, lastIslandSmoothFrom01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothTo01 : islandSmoothTo01, lastIslandSmoothTo01)
                || !Mathf.Approximately(preset != null ? preset.islandAspectRatio : islandAspectRatio, lastIslandAspectRatio)
                || !Mathf.Approximately(preset != null ? preset.warpAmplitude01 : warpAmplitude01, lastWarpAmplitude01)
                || (preset != null ? preset.shapeMode : shapeMode) != lastShapeMode
                || !Mathf.Approximately(preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent, lastHeightRedistributionExponent)
                // F3b / N5.e hills params
                || !Mathf.Approximately(preset != null ? preset.hillsL1 : hillsL1, lastHillsL1)
                || !Mathf.Approximately(preset != null ? preset.hillsL2 : hillsL2, lastHillsL2)
                || !Mathf.Approximately(preset != null ? preset.hillsNoiseBlend : hillsNoiseBlend, lastHillsNoiseBlend)
                || ComputeCurveHash(preset != null ? preset.heightRemapCurve : heightRemapCurve) != lastHeightRemapCurveHash
                // Phase M: biome climate
                || !Mathf.Approximately(preset != null ? preset.biomeBaseTemperature : biomeBaseTemperature, lastBiomeBaseTemperature)
                || !Mathf.Approximately(preset != null ? preset.biomeLapseRate : biomeLapseRate, lastBiomeLapseRate)
                || !Mathf.Approximately(preset != null ? preset.biomeLatitudeEffect : biomeLatitudeEffect, lastBiomeLatitudeEffect)
                || !Mathf.Approximately(preset != null ? preset.biomeCoastModerationStrength : biomeCoastModerationStrength, lastBiomeCoastModerationStrength)
                || !Mathf.Approximately(preset != null ? preset.biomeTempNoiseAmplitude : biomeTempNoiseAmplitude, lastBiomeTempNoiseAmplitude)
                || (preset != null ? preset.biomeTempNoiseCellSize : biomeTempNoiseCellSize) != lastBiomeTempNoiseCellSize
                || !Mathf.Approximately(preset != null ? preset.biomeCoastalMoistureBonus : biomeCoastalMoistureBonus, lastBiomeCoastalMoistureBonus)
                || !Mathf.Approximately(preset != null ? preset.biomeCoastDecayRate : biomeCoastDecayRate, lastBiomeCoastDecayRate)
                || !Mathf.Approximately(preset != null ? preset.biomeMoistureNoiseAmplitude : biomeMoistureNoiseAmplitude, lastBiomeMoistureNoiseAmplitude)
                || (preset != null ? preset.biomeMoistureNoiseCellSize : biomeMoistureNoiseCellSize) != lastBiomeMoistureNoiseCellSize
                // N5.b: noise (asset ref + resolved struct comparison)
                || (preset != null ? preset.terrainNoiseAsset : terrainNoiseAsset) != lastTerrainNoiseAsset
                || (preset != null ? preset.warpNoiseAsset : warpNoiseAsset) != lastWarpNoiseAsset
                || !ResolveTerrainNoise().Equals(lastTerrainNoise)
                || !ResolveWarpNoise().Equals(lastWarpNoise)
                || (preset != null ? preset.hillsNoiseAsset : hillsNoiseAsset) != lastHillsNoiseAsset
                || !ResolveHillsNoise().Equals(lastHillsNoise)
                || (preset != null ? preset.heightQuantSteps : heightQuantSteps) != lastHeightQuantSteps
                || flipY != lastFlipY
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun
                || useProceduralTiles != lastUseProceduralTiles
                || proceduralFallbackColor != lastProceduralFallbackColor
                || ComputeProceduralHash() != lastProceduralHash
                || enableMultiLayer != lastEnableMultiLayer
                || overlayTilemap != lastOverlayTilemap
                || colliderTilemap != lastColliderTilemap
                || colliderTile != lastColliderTile
                || enableColliderAutoSetup != lastEnableColliderAutoSetup
                // H8: mega-tiles
                || enableMegaTiles != lastEnableMegaTiles
                || ComputeMegaTileHash() != lastMegaTileHash
                // N6: overlay params
                || enableOverlay1 != lastEnableOverlay1
                || overlaySource1 != lastOverlaySource1
                || !Mathf.Approximately(overlayMin1, lastOverlayMin1)
                || !Mathf.Approximately(overlayMax1, lastOverlayMax1)
                || overlayColorLow1 != lastOverlayColorLow1
                || overlayColorHigh1 != lastOverlayColorHigh1
                || !Mathf.Approximately(overlayAlpha1, lastOverlayAlpha1)
                || enableOverlay2 != lastEnableOverlay2
                || overlaySource2 != lastOverlaySource2
                || !Mathf.Approximately(overlayMin2, lastOverlayMin2)
                || !Mathf.Approximately(overlayMax2, lastOverlayMax2)
                || overlayColorLow2 != lastOverlayColorLow2
                || overlayColorHigh2 != lastOverlayColorHigh2
                || !Mathf.Approximately(overlayAlpha2, lastOverlayAlpha2);
        }

        // =====================================================================
        // Hash helpers
        // =====================================================================
        private ulong ComputeTilesetConfigHash()
        {
            const ulong O = 14695981039346656037UL; const ulong P = 1099511628211UL;
            ulong h = O;
            if (tilesetConfig == null) return h;
            var layers = tilesetConfig.layers;
            if (layers != null)
                for (int i = 0; i < layers.Length; i++)
                {
                    h ^= (ulong)(uint)(int)layers[i].layerId; h *= P;
                    h ^= (ulong)(uint)(layers[i].tile != null ? layers[i].tile.GetInstanceID() : 0); h *= P;
                    h ^= (ulong)(uint)(layers[i].animatedTile != null ? layers[i].animatedTile.GetInstanceID() : 0); h *= P;
                    h ^= (ulong)(uint)(layers[i].ruleTile != null ? layers[i].ruleTile.GetInstanceID() : 0); h *= P;
                    h ^= layers[i].enabled ? 1UL : 0UL; h *= P;
                }
            h ^= (ulong)(uint)(tilesetConfig.fallbackTile != null ? tilesetConfig.fallbackTile.GetInstanceID() : 0); h *= P;
            return h;
        }

        private ulong ComputeProceduralHash()
        {
            const ulong O = 14695981039346656037UL; const ulong P = 1099511628211UL;
            ulong h = O;
            if (proceduralColorTable == null) return h;
            for (int i = 0; i < proceduralColorTable.Length; i++)
            {
                Color32 c = proceduralColorTable[i].Color;
                h ^= (ulong)(uint)(int)proceduralColorTable[i].LayerId; h *= P;
                h ^= c.r; h *= P; h ^= c.g; h *= P; h ^= c.b; h *= P; h ^= c.a; h *= P;
            }
            return h;
        }

        private ulong ComputeMegaTileHash()
        {
            const ulong O = 14695981039346656037UL; const ulong P = 1099511628211UL;
            ulong h = O;
            h ^= enableMegaTiles ? 1UL : 0UL; h *= P;
            if (megaTileRules == null) return h;
            for (int i = 0; i < megaTileRules.Length; i++)
            {
                h ^= (ulong)(uint)(int)megaTileRules[i].targetLayer; h *= P;
                h ^= (ulong)(uint)(megaTileRules[i].quadrantTL != null ? megaTileRules[i].quadrantTL.GetInstanceID() : 0); h *= P;
                h ^= (ulong)(uint)(megaTileRules[i].quadrantTR != null ? megaTileRules[i].quadrantTR.GetInstanceID() : 0); h *= P;
                h ^= (ulong)(uint)(megaTileRules[i].quadrantBL != null ? megaTileRules[i].quadrantBL.GetInstanceID() : 0); h *= P;
                h ^= (ulong)(uint)(megaTileRules[i].quadrantBR != null ? megaTileRules[i].quadrantBR.GetInstanceID() : 0); h *= P;
            }
            return h;
        }

        private static int ComputeCurveHash(AnimationCurve curve)
        {
            if (curve == null) return 0;
            unchecked
            {
                int hash = (int)2166136261;
                hash = (hash ^ curve.length) * 16777619;
                for (int i = 0; i < curve.length; i++)
                {
                    var k = curve[i];
                    hash = (hash ^ k.time.GetHashCode()) * 16777619;
                    hash = (hash ^ k.value.GetHashCode()) * 16777619;
                }
                return hash;
            }
        }

        // =====================================================================
        // Multi-layer stamping (H5)
        // =====================================================================
        private static readonly MapLayerId[] s_baseLayers = {
            MapLayerId.DeepWater, MapLayerId.MidWater, MapLayerId.ShallowWater,
            MapLayerId.Land, MapLayerId.LandCore, MapLayerId.LandEdge };
        private static readonly MapLayerId[] s_overlayLayers = {
            MapLayerId.Vegetation, MapLayerId.HillsL1, MapLayerId.HillsL2, MapLayerId.Stairs };
        private static readonly MapLayerId[] s_colliderLayers = {
            MapLayerId.DeepWater, MapLayerId.MidWater, MapLayerId.HillsL2 };

        private void StampMultiLayer(MapDataExport export, TilemapLayerEntry[] activeTable, TileBase activeFallback)
        {
            var baseGroup = new TilemapLayerGroup
            {
                Tilemap = tilemap,
                PriorityTable = FilterTable(activeTable, s_baseLayers),
                FallbackTile = activeFallback,
                ClearFirst = true,
                FlipY = flipY
            };
            TilemapLayerGroup overlayGroup = null;
            if (overlayTilemap != null)
                overlayGroup = new TilemapLayerGroup
                {
                    Tilemap = overlayTilemap,
                    PriorityTable = FilterTable(activeTable, s_overlayLayers),
                    FallbackTile = null,
                    ClearFirst = true,
                    FlipY = flipY
                };
            TilemapLayerGroup colliderGroup = null;
            if (colliderTilemap != null && colliderTile != null)
            {
                if (enableColliderAutoSetup) TilemapAdapter2D.SetupCollider(colliderTilemap);
                colliderGroup = new TilemapLayerGroup
                {
                    Tilemap = colliderTilemap,
                    PriorityTable = BuildColliderTable(colliderTile, s_colliderLayers),
                    FallbackTile = null,
                    ClearFirst = true,
                    FlipY = flipY
                };
            }
            int count = 1 + (overlayGroup != null ? 1 : 0) + (colliderGroup != null ? 1 : 0);
            var groups = new TilemapLayerGroup[count]; int gi = 0;
            groups[gi++] = baseGroup;
            if (overlayGroup != null) groups[gi++] = overlayGroup;
            if (colliderGroup != null) groups[gi++] = colliderGroup;
            TilemapAdapter2D.ApplyLayered(export, groups);
        }

        private static TilemapLayerEntry[] FilterTable(TilemapLayerEntry[] source, MapLayerId[] ids)
        {
            if (source == null || source.Length == 0) return System.Array.Empty<TilemapLayerEntry>();
            var idSet = new HashSet<MapLayerId>(ids);
            var result = new List<TilemapLayerEntry>(ids.Length);
            for (int i = 0; i < source.Length; i++)
                if (idSet.Contains(source[i].LayerId)) result.Add(source[i]);
            return result.ToArray();
        }

        private static TilemapLayerEntry[] BuildColliderTable(TileBase tile, MapLayerId[] ids)
        {
            var result = new TilemapLayerEntry[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                result[i] = new TilemapLayerEntry { LayerId = ids[i], Tile = tile };
            return result;
        }

        // =====================================================================
        // Procedural palette presets (H2d)
        // =====================================================================
        [ContextMenu("Procedural Palette / Classic (Natural)")]
        private void SetPalette_Classic()
        {
            useProceduralTiles = true; proceduralFallbackColor = Hex("#0D1F2D");
            proceduralColorTable = new[] {
                E(MapLayerId.DeepWater,"#1A3A5C"), E(MapLayerId.MidWater,"#2D6080"),
                E(MapLayerId.ShallowWater,"#4A90A4"), E(MapLayerId.Land,"#5A8A3C"),
                E(MapLayerId.LandCore,"#3D6B1A"), E(MapLayerId.HillsL1,"#8B7355"),
                E(MapLayerId.HillsL2,"#5C3D1E"), E(MapLayerId.Vegetation,"#2D5A1B"),
                E(MapLayerId.Stairs,"#D4B483"), E(MapLayerId.LandEdge,"#7A9A4A") };
        }

        [ContextMenu("Procedural Palette / Prototyping (Debug)")]
        private void SetPalette_Prototyping()
        {
            useProceduralTiles = true; proceduralFallbackColor = new Color(0.15f, 0.15f, 0.15f);
            proceduralColorTable = new[] {
                E(MapLayerId.DeepWater,"#0033CC"), E(MapLayerId.MidWater,"#0066FF"),
                E(MapLayerId.ShallowWater,"#00CCFF"), E(MapLayerId.Land,"#33CC33"),
                E(MapLayerId.LandCore,"#880000"), E(MapLayerId.HillsL1,"#FFFF00"),
                E(MapLayerId.HillsL2,"#FF8800"), E(MapLayerId.Vegetation,"#CC00CC"),
                E(MapLayerId.Stairs,"#FFFFFF"), E(MapLayerId.LandEdge,"#FF0000") };
        }

        [ContextMenu("Procedural Palette / Twilight (Moody)")]
        private void SetPalette_Twilight()
        {
            useProceduralTiles = true; proceduralFallbackColor = Hex("#050A10");
            proceduralColorTable = new[] {
                E(MapLayerId.DeepWater,"#0D1B2A"), E(MapLayerId.MidWater,"#143448"),
                E(MapLayerId.ShallowWater,"#1B4D6B"), E(MapLayerId.Land,"#3D2B5A"),
                E(MapLayerId.LandCore,"#5A1B5A"), E(MapLayerId.HillsL1,"#6B4F7A"),
                E(MapLayerId.HillsL2,"#4A2D5C"), E(MapLayerId.Vegetation,"#1B4A3D"),
                E(MapLayerId.Stairs,"#C8A96E"), E(MapLayerId.LandEdge,"#7A4A6B") };
        }

        private static ProceduralTileEntry E(MapLayerId id, string hex) =>
            new ProceduralTileEntry { LayerId = id, Color = Hex(hex) };
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out Color c); return c; }
    }
}