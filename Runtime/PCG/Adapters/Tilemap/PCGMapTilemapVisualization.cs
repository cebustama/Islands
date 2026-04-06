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

        [Header("Island Shape")]
        [Range(0f, 1f)][SerializeField] private float islandRadius01 = 0.45f;
        [Range(0.25f, 4f)][SerializeField] private float islandAspectRatio = 1.00f;
        [Range(0f, 1f)][SerializeField] private float warpAmplitude01 = 0.00f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothFrom01 = 0.30f;
        [Range(0f, 1f)][SerializeField] private float islandSmoothTo01 = 0.70f;

        [Header("Water & Shore")]
        [Range(0f, 1f)][SerializeField] private float waterThreshold01 = 0.50f;
        [Range(0f, 0.5f)][SerializeField] private float shallowWaterDepth01 = 0f;
        [Range(0f, 0.5f)][SerializeField] private float midWaterDepth01 = 0f;

        [Header("Terrain Noise")]
        [Min(1)][SerializeField] private int noiseCellSize = 8;
        [Range(0f, 1f)][SerializeField] private float noiseAmplitude = 0.18f;
        [Min(0)][SerializeField] private int quantSteps = 1024;

        [Header("Height Redistribution (J2)")]
        [Range(0.5f, 4f)][SerializeField] private float heightRedistributionExponent = 1.0f;

        [Header("Height Remap (N2)")]
        [Tooltip("Height remap curve applied after power redistribution.\n" +
                 "Straight diagonal (0,0)→(1,1) = identity. Sampled to a spline at runtime.")]
        [SerializeField] private AnimationCurve heightRemapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

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

        [Header("Procedural Tiles")]
        [Tooltip("Generate solid-color tiles at runtime. Takes precedence over TilesetConfig.")]
        [SerializeField] private bool useProceduralTiles = false;
        [SerializeField] private ProceduralTileEntry[] proceduralColorTable = System.Array.Empty<ProceduralTileEntry>();
        [SerializeField] private Color proceduralFallbackColor = new Color(0.25f, 0.25f, 0.25f);

        [Header("Scalar Field Overlay")]
        [Tooltip("Tint tiles based on a scalar field (Height, CoastDist, etc.).\n" +
                 "Changing the field auto-sets min/max to sensible defaults.")]
        [SerializeField] private bool enableScalarOverlay = false;
        [SerializeField] private MapFieldId overlayField = MapFieldId.Height;
        [SerializeField] private float overlayMin = 0f;
        [SerializeField] private float overlayMax = 1f;
        [SerializeField] private Color overlayColorLow = new Color(0.40f, 0.40f, 0.65f, 1f);
        [SerializeField] private Color overlayColorHigh = Color.white;

        [Header("Scalar Heatmap Tilemap")]
        [Tooltip("Dedicated tilemap for scalar field heatmap (solid-color tiles).\n" +
                 "Must be under its own Grid if cell size differs from the base tilemap.\n" +
                 "When assigned and overlay enabled, replaces the per-cell tint approach.\n" +
                 "When null, the per-cell tint fallback is used instead.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap scalarHeatmapTilemap;
        [Range(0f, 1f)]
        [Tooltip("TilemapRenderer alpha for the heatmap tilemap.")]
        [SerializeField] private float heatmapAlpha = 0.65f;

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

        private IMapStage2D[] stagesF2, stagesF3, stagesF4, stagesF5, stagesF6, stagesG;

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
        private bool lastEnableVegetationStage, lastEnableTraversalStage, lastEnableMorphologyStage;
        private float lastIslandRadius01, lastWaterThreshold01;
        private float lastIslandSmoothFrom01, lastIslandSmoothTo01;
        private float lastIslandAspectRatio, lastWarpAmplitude01;
        private float lastHeightRedistributionExponent;
        private int lastHeightRemapCurveHash;
        private int lastNoiseCellSize;
        private float lastNoiseAmplitude;
        private int lastQuantSteps;
        private bool lastFlipY, lastClearBeforeRun;
        private bool lastUseProceduralTiles;
        private ulong lastProceduralHash;
        private Color lastProceduralFallbackColor;
        private bool lastEnableMultiLayer;
        private UnityEngine.Tilemaps.Tilemap lastOverlayTilemap, lastColliderTilemap;
        private TileBase lastColliderTile;
        private bool lastEnableColliderAutoSetup;
        private bool lastEnableScalarOverlay;
        private MapFieldId lastOverlayField;
        private float lastOverlayMin, lastOverlayMax;
        private Color lastOverlayColorLow, lastOverlayColorHigh;
        private UnityEngine.Tilemaps.Tilemap lastScalarHeatmapTilemap;
        private float lastHeatmapAlpha;

        /// <summary>Tracks whether overlay was applied on the previous rebuild (for Issue 1 reset pass).</summary>
        private bool _overlayWasApplied;

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
            ctx?.Dispose();
            ctx = null;
            ctxResolution = -1;
            baseStage = null; hillsStage = null; shoreStage = null;
            vegetationStage = null; traversalStage = null; morphologyStage = null;
            stagesF2 = stagesF3 = stagesF4 = stagesF5 = stagesF6 = stagesG = null;
        }

        private void Update()
        {
            if (overlayField != lastOverlayField)
                ApplyOverlayFieldDefaults();

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
            int eCell = preset != null ? preset.noiseCellSize : noiseCellSize;
            float eAmp = preset != null ? preset.noiseAmplitude : noiseAmplitude;
            int eQuant = preset != null ? preset.quantSteps : quantSteps;
            bool eClear = preset != null ? preset.clearBeforeRun : clearBeforeRun;
            var eTun = preset != null
                ? preset.ToTunables()
                : new MapTunables2D(
                      islandRadius01, waterThreshold01,
                      islandSmoothFrom01, islandSmoothTo01,
                      islandAspectRatio, warpAmplitude01,
                      heightRedistributionExponent,
                      ScalarSpline.FromAnimationCurve(heightRemapCurve));

            EnsureContextAllocated(eRes);

            baseStage.noiseCellSize = Mathf.Max(1, eCell);
            baseStage.noiseAmplitude = Mathf.Max(0f, eAmp);
            baseStage.quantSteps = Mathf.Max(0, eQuant);

            float eShallowDepth = preset != null ? preset.shallowWaterDepth01 : shallowWaterDepth01;
            float eMidDepth = preset != null ? preset.midWaterDepth01 : midWaterDepth01;
            shoreStage.ShallowWaterDepth01 = Mathf.Max(0f, eShallowDepth);
            shoreStage.MidWaterDepth01 = Mathf.Max(0f, eMidDepth);

            var inputs = new MapInputs(eSeed, new GridDomain2D(eRes, eRes), eTun);
            var stages = eMorph ? stagesG : eTrav ? stagesF6 : eVeg ? stagesF5
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

            ApplyScalarOverlay(eRes);

            int stamped = CountStampedTiles(eRes);
            dirty = false;
            updateCalls++;

            Debug.Log(
                $"[PCGMapTilemapVisualization] #{updateCalls} res={eRes} seed={eSeed} " +
                $"hills={eHills} shore={eShore} veg={eVeg} trav={eTrav} morph={eMorph} " +
                $"flipY={flipY} proc={useProceduralTiles} multi={enableMultiLayer} " +
                $"overlay={enableScalarOverlay}({overlayField}) heatmap={scalarHeatmapTilemap != null} tiles={stamped}/{eRes * eRes}");
        }

        // =====================================================================
        // Scalar field overlay (N2 + post-N2 fixes)
        // =====================================================================

        /// <summary>Quantized palette steps for the heatmap tilemap (capped for cache efficiency).</summary>
        private const int HeatmapPaletteSteps = 256;

        /// <summary>
        /// Top-level overlay dispatcher. Uses the dedicated heatmap tilemap when assigned,
        /// falls back to per-cell SetColor tint otherwise.
        /// </summary>
        private void ApplyScalarOverlay(int res)
        {
            bool hasOverlay = enableScalarOverlay && ctx.IsFieldCreated(overlayField);

            if (scalarHeatmapTilemap != null)
            {
                ApplyScalarHeatmapTilemap(res, hasOverlay);
                // Ensure the tint path is clean when heatmap is active.
                if (_overlayWasApplied)
                    ResetTintColors(res);
                _overlayWasApplied = false;
            }
            else
            {
                // Clear heatmap tilemap if it was previously assigned and is now null.
                // (No-op when it was never assigned.)
                ApplyScalarOverlayTint(res, hasOverlay);
            }
        }

        /// <summary>
        /// Dedicated heatmap tilemap path (Issue 2). Stamps solid-color procedural tiles
        /// onto a separate tilemap, colored by the scalar field value. Color palette is
        /// quantized to <see cref="HeatmapPaletteSteps"/> steps for cache efficiency.
        /// </summary>
        private void ApplyScalarHeatmapTilemap(int res, bool hasOverlay)
        {
            if (!hasOverlay)
            {
                scalarHeatmapTilemap.ClearAllTiles();
                // Sync renderer alpha even when disabled (avoids stale alpha on re-enable).
                SetHeatmapRendererAlpha();
                return;
            }

            ScalarField2D field = ctx.GetField(overlayField);
            float oRange = overlayMax - overlayMin;
            float oInvRange = (oRange > 1e-6f) ? (1f / oRange) : 0f;
            float invSteps = 1f / (HeatmapPaletteSteps - 1);

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int ty = flipY ? (res - 1 - y) : y;
                    var pos = new Vector3Int(x, ty, 0);

                    float v = math.saturate((field.GetUnchecked(x, y) - overlayMin) * oInvRange);
                    // Quantize to N steps for tile cache efficiency.
                    int step = (int)(v * (HeatmapPaletteSteps - 1) + 0.5f);
                    float qv = step * invSteps;
                    Color c = Color.Lerp(overlayColorLow, overlayColorHigh, qv);

                    TileBase tile = ProceduralTileFactory.GetOrCreate(c);
                    scalarHeatmapTilemap.SetTile(pos, tile);
                }
            }

            SetHeatmapRendererAlpha();
        }

        private void SetHeatmapRendererAlpha()
        {
            scalarHeatmapTilemap.color = new Color(1f, 1f, 1f, heatmapAlpha);
        }

        /// <summary>
        /// Per-cell tint fallback path (Issue 1 fix). Only touches tile colors when overlay
        /// is enabled. On enabled→disabled transition, resets tile colors via LockColor.
        /// </summary>
        private void ApplyScalarOverlayTint(int res, bool hasOverlay)
        {
            if (tilemap == null) return;

            if (!hasOverlay)
            {
                // Issue 1 fix: only run a reset pass on the enabled→disabled transition.
                if (_overlayWasApplied)
                    ResetTintColors(res);
                _overlayWasApplied = false;
                return;
            }

            // Overlay ON: tint tiles.
            ScalarField2D field = ctx.GetField(overlayField);
            float oRange = overlayMax - overlayMin;
            float oInvRange = (oRange > 1e-6f) ? (1f / oRange) : 0f;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int ty = flipY ? (res - 1 - y) : y;
                    var pos = new Vector3Int(x, ty, 0);
                    float v = math.saturate((field.GetUnchecked(x, y) - overlayMin) * oInvRange);
                    Color tint = Color.Lerp(overlayColorLow, overlayColorHigh, v);
                    tilemap.SetTileFlags(pos, TileFlags.None);
                    tilemap.SetColor(pos, tint);
                }
            }

            _overlayWasApplied = true;
        }

        /// <summary>
        /// Restores tile-asset built-in colors by re-locking the color flag.
        /// Used when overlay transitions from enabled to disabled, or when switching
        /// from tint path to heatmap path.
        /// </summary>
        private void ResetTintColors(int res)
        {
            if (tilemap == null) return;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    int ty = flipY ? (res - 1 - y) : y;
                    var pos = new Vector3Int(x, ty, 0);
                    tilemap.SetTileFlags(pos, TileFlags.LockColor);
                }
        }

        private void ApplyOverlayFieldDefaults()
        {
            switch (overlayField)
            {
                case MapFieldId.Height: overlayMin = 0f; overlayMax = 1f; break;
                case MapFieldId.CoastDist: overlayMin = -1f; overlayMax = 20f; break;
                default: overlayMin = 0f; overlayMax = 1f; break;
            }
        }

        [ContextMenu("Reset Overlay Range to Defaults")]
        private void ResetOverlayRangeToDefaults() => ApplyOverlayFieldDefaults();

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
            stagesF2 = new IMapStage2D[] { baseStage };
            stagesF3 = new IMapStage2D[] { baseStage, hillsStage };
            stagesF4 = new IMapStage2D[] { baseStage, hillsStage, shoreStage };
            stagesF5 = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage };
            stagesF6 = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage };
            stagesG = new IMapStage2D[] { baseStage, hillsStage, shoreStage, vegetationStage, traversalStage, morphologyStage };
        }

        // =====================================================================
        // Dirty tracking
        // =====================================================================
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
            lastIslandRadius01 = preset != null ? preset.islandRadius01 : islandRadius01;
            lastWaterThreshold01 = preset != null ? preset.waterThreshold01 : waterThreshold01;
            lastIslandSmoothFrom01 = preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01;
            lastIslandSmoothTo01 = preset != null ? preset.islandSmoothTo01 : islandSmoothTo01;
            lastIslandAspectRatio = preset != null ? preset.islandAspectRatio : islandAspectRatio;
            lastWarpAmplitude01 = preset != null ? preset.warpAmplitude01 : warpAmplitude01;
            lastHeightRedistributionExponent = preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent;
            lastHeightRemapCurveHash = ComputeCurveHash(preset != null ? preset.heightRemapCurve : heightRemapCurve);
            lastNoiseCellSize = preset != null ? preset.noiseCellSize : noiseCellSize;
            lastNoiseAmplitude = preset != null ? preset.noiseAmplitude : noiseAmplitude;
            lastQuantSteps = preset != null ? preset.quantSteps : quantSteps;
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
            lastEnableScalarOverlay = enableScalarOverlay;
            lastOverlayField = overlayField;
            lastOverlayMin = overlayMin;
            lastOverlayMax = overlayMax;
            lastOverlayColorLow = overlayColorLow;
            lastOverlayColorHigh = overlayColorHigh;
            lastScalarHeatmapTilemap = scalarHeatmapTilemap;
            lastHeatmapAlpha = heatmapAlpha;
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
                || !Mathf.Approximately(preset != null ? preset.islandRadius01 : islandRadius01, lastIslandRadius01)
                || !Mathf.Approximately(preset != null ? preset.waterThreshold01 : waterThreshold01, lastWaterThreshold01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothFrom01 : islandSmoothFrom01, lastIslandSmoothFrom01)
                || !Mathf.Approximately(preset != null ? preset.islandSmoothTo01 : islandSmoothTo01, lastIslandSmoothTo01)
                || !Mathf.Approximately(preset != null ? preset.islandAspectRatio : islandAspectRatio, lastIslandAspectRatio)
                || !Mathf.Approximately(preset != null ? preset.warpAmplitude01 : warpAmplitude01, lastWarpAmplitude01)
                || !Mathf.Approximately(preset != null ? preset.heightRedistributionExponent : heightRedistributionExponent, lastHeightRedistributionExponent)
                || ComputeCurveHash(preset != null ? preset.heightRemapCurve : heightRemapCurve) != lastHeightRemapCurveHash
                || (preset != null ? preset.noiseCellSize : noiseCellSize) != lastNoiseCellSize
                || !Mathf.Approximately(preset != null ? preset.noiseAmplitude : noiseAmplitude, lastNoiseAmplitude)
                || (preset != null ? preset.quantSteps : quantSteps) != lastQuantSteps
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
                || enableScalarOverlay != lastEnableScalarOverlay
                || overlayField != lastOverlayField
                || !Mathf.Approximately(overlayMin, lastOverlayMin)
                || !Mathf.Approximately(overlayMax, lastOverlayMax)
                || overlayColorLow != lastOverlayColorLow
                || overlayColorHigh != lastOverlayColorHigh
                || scalarHeatmapTilemap != lastScalarHeatmapTilemap
                || !Mathf.Approximately(heatmapAlpha, lastHeatmapAlpha);
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