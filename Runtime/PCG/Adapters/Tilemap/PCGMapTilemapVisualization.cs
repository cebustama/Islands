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
using Islands.PCG.Samples; // H3: MapGenerationPreset

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Live tilemap visualization for the PCG Map Pipeline (Phase H2c / H2d).
    ///
    /// Runs the full pipeline in the Editor every time any tunable changes
    /// ([ExecuteAlways] + dirty tracking), exports a <see cref="MapDataExport"/>,
    /// and stamps the result into an assigned Unity <see cref="UnityEngine.Tilemaps.Tilemap"/>
    /// via <see cref="TilemapAdapter2D.Apply"/>.
    ///
    /// Tile resolution priority (high → low):
    ///   1. Procedural mode (useProceduralTiles = true)
    ///   2. TilesetConfig SO (tilesetConfig assigned, H3)
    ///   3. Inline priority table (priorityTable array)
    ///
    /// Phase H2c: initial implementation.
    /// Phase H2d: procedural tile mode added.
    /// Phase H3: optional MapGenerationPreset slot (pipeline parameter override) and
    ///           optional TilesetConfig slot (tile art set override). Both use
    ///           override-at-resolve — inline fields remain active as fallback when null.
    ///           Editing SO fields while assigned does not auto-refresh; toggle any other
    ///           Inspector field (or reassign the SO) to force a redraw.
    /// Phase H4: ComputeTilesetConfigHash() extended to include animatedTile InstanceID
    ///           per layer entry, so assigning or swapping an AnimatedTile asset in the
    ///           TilesetConfig Inspector triggers a real-time rebuild.
    /// Phase H6: ComputeTilesetConfigHash() extended to include ruleTile InstanceID per
    ///           layer entry, so assigning or swapping a RuleTile asset triggers rebuild.
    /// Phase F4b: shallowWaterDepth01 Inspector field added. When > 0, Shore stage marks
    ///           water cells above (waterThreshold - depth) as ShallowWater, producing a
    ///           variable-width coastal shelf. Adjacency ring always included.
    /// Phase F4c: midWaterDepth01 Inspector field added. When > 0, Shore stage writes a
    ///           MidWater layer for water cells between shallow and deep thresholds,
    ///           producing a 3-band water depth system.
    /// Phase H5: Multi-layer Tilemap support. Enable Multi Layer to separate pipeline
    ///           layers across base (opaque), overlay (transparent), and collider
    ///           (invisible physics) Tilemaps. SetupCollider auto-adds physics components
    ///           to the collider Tilemap. Single-tilemap path is fully backward compatible.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Islands/PCG/Map Tilemap Visualization")]
    public sealed class PCGMapTilemapVisualization : MonoBehaviour
    {
        // =====================================================================
        // Inspector — Tilemap Target
        // =====================================================================
        [Header("Tilemap Target")]
        [Tooltip("The Unity Tilemap to stamp. Must be assigned before the visualization runs.\n" +
                 "In Multi-layer mode this receives the base (opaque) layers:\n" +
                 "DeepWater, ShallowWater, Land, LandCore, LandEdge.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap tilemap;

        // =====================================================================
        // Inspector — Preset (optional, H3)
        // =====================================================================
        [Header("Preset (optional)")]
        [Tooltip("Assign a MapGenerationPreset asset to override all pipeline parameters " +
                 "(seed, resolution, stage toggles, all F2 tunables).\n" +
                 "When null this component's own inline fields are used (backward compatible).\n" +
                 "Note: editing preset fields while assigned does not auto-refresh — " +
                 "toggle any other Inspector field or reassign the preset to force a redraw.")]
        [SerializeField] private MapGenerationPreset preset;

        // =====================================================================
        // Inspector — TilesetConfig (optional, H3)
        // =====================================================================
        [Header("Tileset Config (optional)")]
        [Tooltip("Assign a TilesetConfig asset to override the inline Priority Table with a " +
                 "complete tile art set. Ignored when Use Procedural Tiles is enabled.\n" +
                 "When null the inline Priority Table below is used (backward compatible).\n" +
                 "Note: editing TilesetConfig fields while assigned does not auto-refresh — " +
                 "toggle any other Inspector field or reassign the config to force a redraw.")]
        [SerializeField] private TilesetConfig tilesetConfig;

        // =====================================================================
        // Inspector — Run Inputs
        // =====================================================================
        [Header("Run Inputs")]
        [Tooltip("Deterministic seed (uint >= 1). Same seed + same tunables => same map.")]
        [SerializeField] private uint seed = 1u;

        [Tooltip("Map grid resolution (width = height in cells). Changing this reallocates the MapContext2D.")]
        [Min(4)]
        [SerializeField] private int resolution = 64;

        // =====================================================================
        // Inspector — Pipeline Stage Toggles
        // =====================================================================
        [Header("Pipeline")]
        [Tooltip("Include F3 Hills + topology stage.")]
        [SerializeField] private bool enableHillsStage = true;

        [Tooltip("Include F4 Shore (ShallowWater) stage. Requires Hills enabled for correct results.")]
        [SerializeField] private bool enableShoreStage = true;

        [Tooltip("Height band below waterThreshold that qualifies as shallow water (F4b).\n" +
                 "0 = adjacency-only (original 1-cell ring). > 0 = water cells with Height\n" +
                 "above (waterThreshold - this value) are also marked ShallowWater, producing\n" +
                 "a variable-width coastal shelf that follows terrain contours.\n" +
                 "The 1-cell adjacency ring is always included regardless of this value.\n" +
                 "Typical: 0.05 = subtle shelf, 0.15 = wide shelf.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float shallowWaterDepth01 = 0f;

        [Tooltip("Height band below waterThreshold for mid-depth water (F4c).\n" +
                 "0 = no MidWater layer (default). > 0 = water cells between\n" +
                 "shallow and deep thresholds become MidWater. Must be greater\n" +
                 "than Shallow Water Depth for a visible band to appear.\n" +
                 "Typical: 0.15 = subtle mid band, 0.30 = wide mid band.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float midWaterDepth01 = 0f;

        [Tooltip("Include F5 Vegetation stage. Requires Shore enabled for correct results.")]
        [SerializeField] private bool enableVegetationStage = true;

        [Tooltip("Include F6 Traversal (Walkable + Stairs) stage. Requires Vegetation enabled.")]
        [SerializeField] private bool enableTraversalStage = true;

        [Tooltip("Include Phase G Morphology (LandCore + CoastDist) stage. Requires Traversal enabled.")]
        [SerializeField] private bool enableMorphologyStage = true;

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

        [Tooltip("Domain warp amplitude as a fraction of map size. 0 = no warp. ~0.15 = subtle organic coast.")]
        [Range(0f, 1f)]
        [SerializeField] private float warpAmplitude01 = 0.00f;

        [Header("Height Redistribution (J2)")]
        [Tooltip("Power-curve exponent applied to terrain height.\n" +
                 "1.0 = no change (identity). > 1 = flat lowlands, sharp peaks.\n" +
                 "< 1 = raised lowlands, compressed peaks. Range [0.5..4.0].")]
        [Range(0.5f, 4f)]
        [SerializeField] private float heightRedistributionExponent = 1.0f;

        [Header("F2 Tunables (Noise Inside Island)")]
        [Min(1)][SerializeField] private int noiseCellSize = 8;
        [Range(0f, 1f)][SerializeField] private float noiseAmplitude = 0.18f;
        [Min(0)][SerializeField] private int quantSteps = 1024;

        // =====================================================================
        // Inspector — Tilemap Options
        // =====================================================================
        [Header("Tilemap Options")]
        [Tooltip("If true, mirrors Y: tileY = (Height - 1 - y). Use when the map renders upside down.")]
        [SerializeField] private bool flipY = false;

        [Tooltip("If true (default), clears the tilemap before each stamp. " +
                 "Set false to composite on top of existing content.")]
        [SerializeField] private bool clearBeforeRun = true;

        // =====================================================================
        // Inspector — Multi-layer Tilemaps (H5)
        // =====================================================================
        [Header("Multi-layer Tilemaps (H5)")]
        [Tooltip("When enabled, stamps base layers to the main Tilemap, overlay layers to\n" +
                 "Overlay Tilemap (if assigned), and physics collision cells to Collider\n" +
                 "Tilemap (if assigned).\n\n" +
                 "Base layers:    DeepWater, MidWater, ShallowWater, Land, LandCore, LandEdge\n" +
                 "Overlay layers: Vegetation, HillsL1, HillsL2, Stairs\n" +
                 "Collider cells: DeepWater, MidWater, ShallowWater, HillsL2 (non-walkable)\n\n" +
                 "When disabled, the existing single-tilemap Apply() path is used (backward compatible).")]
        [SerializeField] private bool enableMultiLayer = false;

        [Tooltip("Overlay Tilemap (H5): receives Vegetation, HillsL1, HillsL2, Stairs.\n" +
                 "Set its Tilemap Renderer Order In Layer above the main Tilemap.\n" +
                 "Enable transparency on its material so underlying tiles show through.\n" +
                 "Ignored when Enable Multi Layer is disabled or this field is null.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap overlayTilemap;

        [Tooltip("Collider Tilemap (H5): receives a sentinel tile at non-walkable cells\n" +
                 "(DeepWater, ShallowWater, HillsL2) to drive physics collision.\n" +
                 "Set Tilemap Renderer Color Alpha = 0 to hide this layer visually.\n" +
                 "Ignored when Enable Multi Layer is disabled or this field is null.\n" +
                 "Enable Auto-setup Collider to add physics components automatically.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap colliderTilemap;

        [Tooltip("Tile stamped at non-walkable cells on the Collider Tilemap.\n" +
                 "Any non-null tile works — use a small invisible or solid-color tile.\n" +
                 "Null = collider group is skipped even if Collider Tilemap is assigned.")]
        [SerializeField] private TileBase colliderTile;

        [Tooltip("When true, calls TilemapAdapter2D.SetupCollider() on the Collider Tilemap\n" +
                 "before each stamp. Adds TilemapCollider2D + CompositeCollider2D +\n" +
                 "Rigidbody2D (static) if not already present. Idempotent — safe to leave on.")]
        [SerializeField] private bool enableColliderAutoSetup = true;

        // =====================================================================
        // Inspector — Priority Table (inline fallback)
        // =====================================================================
        [Header("Priority Table  (low → high priority)")]
        [Tooltip("Ordered entries: earlier = lower priority. Last matching layer wins per cell.\n" +
                 "Typical order: DeepWater, ShallowWater, Land, Vegetation, HillsL1, HillsL2,\n" +
                 "Stairs, LandEdge, LandCore. Assign a TileBase to each. Null entries are skipped.\n" +
                 "Ignored when Use Procedural Tiles or a TilesetConfig is assigned.")]
        [SerializeField] private TilemapLayerEntry[] priorityTable = System.Array.Empty<TilemapLayerEntry>();

        [Tooltip("Tile placed at cells where no priority entry matches. Optional — leave null to " +
                 "leave unmatched cells empty. Ignored when Use Procedural Tiles is enabled.")]
        [SerializeField] private TileBase fallbackTile;

        // =====================================================================
        // Inspector — Procedural Tiles (H2d)
        // =====================================================================
        [Header("Procedural Tiles")]
        [Tooltip("When enabled, solid-color tiles are generated at runtime from the Procedural Color " +
                 "Table below. No pre-authored tile art required. Takes precedence over TilesetConfig " +
                 "and the Priority Table. Useful for rapid prototyping and design iteration.")]
        [SerializeField] private bool useProceduralTiles = false;

        [Tooltip("Maps each layer to a solid color. Low→high priority (last match per cell wins). " +
                 "Only active when Use Procedural Tiles is enabled.")]
        [SerializeField] private ProceduralTileEntry[] proceduralColorTable = System.Array.Empty<ProceduralTileEntry>();

        [Tooltip("Color used for cells where no procedural color entry matches. " +
                 "Only active when Use Procedural Tiles is enabled.")]
        [SerializeField] private Color proceduralFallbackColor = new Color(0.25f, 0.25f, 0.25f);

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

        private IMapStage2D[] stagesF2;
        private IMapStage2D[] stagesF3;
        private IMapStage2D[] stagesF4;
        private IMapStage2D[] stagesF5;
        private IMapStage2D[] stagesF6;
        private IMapStage2D[] stagesG;

        // =====================================================================
        // Dirty tracking cache (H3: effective values cached; SO refs tracked)
        // =====================================================================
        private MapGenerationPreset _lastPreset;
        private TilesetConfig _lastTilesetConfig;
        private uint lastSeed;
        private int lastResolution;
        private bool lastEnableHillsStage;
        private bool lastEnableShoreStage;
        private float lastShallowWaterDepth01;  // F4b
        private float lastMidWaterDepth01;      // F4c
        private bool lastEnableVegetationStage;
        private bool lastEnableTraversalStage;
        private bool lastEnableMorphologyStage;
        private float lastIslandRadius01;
        private float lastWaterThreshold01;
        private float lastIslandSmoothFrom01;
        private float lastIslandSmoothTo01;
        private float lastIslandAspectRatio;
        private float lastWarpAmplitude01;
        private float lastHeightRedistributionExponent;
        private int lastNoiseCellSize;
        private float lastNoiseAmplitude;
        private int lastQuantSteps;
        private bool lastFlipY;
        private bool lastClearBeforeRun;
        private ulong lastPriorityHash;
        private TileBase lastFallbackTile;

        // H3 — TilesetConfig content hash (detects tile/enabled edits inside the assigned SO)
        private ulong _lastTilesetConfigHash;

        // H2d — procedural tile dirty cache
        private bool lastUseProceduralTiles;
        private ulong lastProceduralHash;
        private Color lastProceduralFallbackColor;

        // H5 — multi-layer dirty cache
        private bool lastEnableMultiLayer;
        private UnityEngine.Tilemaps.Tilemap lastOverlayTilemap;
        private UnityEngine.Tilemaps.Tilemap lastColliderTilemap;
        private TileBase lastColliderTile;
        private bool lastEnableColliderAutoSetup;

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

            if (tilemap == null)
            {
                Debug.LogWarning("[PCGMapTilemapVisualization] Tilemap is not assigned — skipping generation.", this);
                dirty = false;
                return;
            }

            // H3: resolve effective values (preset overrides inline fields when assigned).
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
                      heightRedistributionExponent);

            EnsureContextAllocated(eRes);

            // ---- Configure configurable base stage ----
            baseStage.noiseCellSize = Mathf.Max(1, eCell);
            baseStage.noiseAmplitude = Mathf.Max(0f, eAmp);
            baseStage.quantSteps = Mathf.Max(0, eQuant);

            // ---- Configure shore stage (F4b / F4c) ----
            float eShallowDepth = preset != null ? preset.shallowWaterDepth01 : shallowWaterDepth01;
            float eMidDepth = preset != null ? preset.midWaterDepth01 : midWaterDepth01;
            shoreStage.ShallowWaterDepth01 = Mathf.Max(0f, eShallowDepth);
            shoreStage.MidWaterDepth01 = Mathf.Max(0f, eMidDepth);

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

            // ---- Run pipeline ----
            MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: eClear);

            // ---- Export ----
            MapDataExport export = MapExporter2D.Export(ctx);

            // ---- Resolve active tile table ----
            // Priority: Procedural > TilesetConfig > inline priorityTable
            TilemapLayerEntry[] activeTable;
            TileBase activeFallback;

            if (useProceduralTiles)
            {
                activeTable = ProceduralTileFactory.BuildPriorityTable(proceduralColorTable);
                activeFallback = ProceduralTileFactory.GetOrCreate(proceduralFallbackColor);
            }
            else if (tilesetConfig != null)
            {
                // H3: TilesetConfig SO overrides the inline priority table.
                TilemapLayerEntry[] fromConfig = tilesetConfig.ToLayerEntries();
                activeTable = fromConfig ?? priorityTable;
                activeFallback = (tilesetConfig.fallbackTile != null)
                    ? tilesetConfig.fallbackTile
                    : fallbackTile;
            }
            else
            {
                activeTable = priorityTable;
                activeFallback = fallbackTile;
            }

            // ---- Stamp tilemap(s) ----
            // H5: multi-layer path separates base, overlay, and collider groups.
            // Single-tilemap path (enableMultiLayer = false) is fully backward compatible.
            if (enableMultiLayer)
            {
                StampMultiLayer(export, activeTable, activeFallback);
            }
            else
            {
                TilemapAdapter2D.Apply(
                    export,
                    tilemap,
                    activeTable,
                    activeFallback,
                    clearFirst: true,
                    flipY: flipY);
            }

            // ---- Count stamped tiles for console diagnostics ----
            int stamped = CountStampedTiles(eRes);

            dirty = false;
            updateCalls++;

            Debug.Log(
                $"[PCGMapTilemapVisualization] Update #{updateCalls} res={eRes} seed={eSeed} " +
                $"hills={eHills} shore={eShore} " +
                $"veg={eVeg} traversal={eTrav} " +
                $"morphology={eMorph} flipY={flipY} " +
                $"proceduralTiles={useProceduralTiles} " +
                $"multiLayer={enableMultiLayer} " +
                $"tilesStamped={stamped}/{eRes * eRes}");
        }

        // =====================================================================
        // Tilemap diagnostics
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

        // =====================================================================
        // Context lifecycle
        // =====================================================================
        private void EnsureContextAllocated(int res)
        {
            if (ctx != null && ctxResolution == res) return;
            ctx?.Dispose();
            ctx = null;
            ctxResolution = res;
            ctx = new MapContext2D(new GridDomain2D(res, res), Allocator.Persistent);
            dirty = true;
        }

        // =====================================================================
        // Stage allocation
        // =====================================================================
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
        // Dirty tracking (H3: effective values cached; SO refs tracked by reference)
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
            lastNoiseCellSize = preset != null ? preset.noiseCellSize : noiseCellSize;
            lastNoiseAmplitude = preset != null ? preset.noiseAmplitude : noiseAmplitude;
            lastQuantSteps = preset != null ? preset.quantSteps : quantSteps;
            lastFlipY = flipY;
            lastClearBeforeRun = preset != null ? preset.clearBeforeRun : clearBeforeRun;
            lastFallbackTile = fallbackTile;
            lastPriorityHash = ComputePriorityHash();

            lastUseProceduralTiles = useProceduralTiles;
            lastProceduralFallbackColor = proceduralFallbackColor;
            lastProceduralHash = ComputeProceduralHash();

            // H5 — multi-layer dirty cache
            lastEnableMultiLayer = enableMultiLayer;
            lastOverlayTilemap = overlayTilemap;
            lastColliderTilemap = colliderTilemap;
            lastColliderTile = colliderTile;
            lastEnableColliderAutoSetup = enableColliderAutoSetup;
        }

        private bool ParamsChanged()
        {
            return preset != _lastPreset
                || tilesetConfig != _lastTilesetConfig
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
                || (preset != null ? preset.noiseCellSize : noiseCellSize) != lastNoiseCellSize
                || !Mathf.Approximately(preset != null ? preset.noiseAmplitude : noiseAmplitude, lastNoiseAmplitude)
                || (preset != null ? preset.quantSteps : quantSteps) != lastQuantSteps
                || flipY != lastFlipY
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun
                || fallbackTile != lastFallbackTile
                || ComputePriorityHash() != lastPriorityHash
                || useProceduralTiles != lastUseProceduralTiles
                || proceduralFallbackColor != lastProceduralFallbackColor
                || ComputeProceduralHash() != lastProceduralHash
                // H5 — multi-layer dirty checks
                || enableMultiLayer != lastEnableMultiLayer
                || overlayTilemap != lastOverlayTilemap
                || colliderTilemap != lastColliderTilemap
                || colliderTile != lastColliderTile
                || enableColliderAutoSetup != lastEnableColliderAutoSetup;
        }

        // FNV-1a over TilesetConfig content (layerId + tile InstanceIDs + animatedTile InstanceIDs
        // + ruleTile InstanceIDs + enabled booleans + fallback InstanceID).
        // Detects tile swaps, animated tile swaps, rule tile swaps, enabled-toggle edits,
        // layerId changes, and fallback changes made directly to the SO asset while it is assigned.
        // Reference-equality alone cannot detect these in-place edits.
        // Runs every Update() frame; cost is O(MapLayerId.COUNT) — negligible (12 iterations).
        // H4: animatedTile InstanceID added to loop so Inspector edits to animated slots trigger rebuild.
        // H6: ruleTile InstanceID added to loop so Inspector edits to rule tile slots trigger rebuild.
        private ulong ComputeTilesetConfigHash()
        {
            const ulong FnvOffset = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong h = FnvOffset;
            if (tilesetConfig == null) return h;

            var layers = tilesetConfig.layers;
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    int tileId = layers[i].tile != null ? layers[i].tile.GetInstanceID() : 0;
                    int animTileId = layers[i].animatedTile != null ? layers[i].animatedTile.GetInstanceID() : 0; // H4
                    int ruleTileId = layers[i].ruleTile != null ? layers[i].ruleTile.GetInstanceID() : 0; // H6
                    int layerId = (int)layers[i].layerId;
                    h ^= (ulong)(uint)layerId; h *= FnvPrime;
                    h ^= (ulong)(uint)tileId; h *= FnvPrime;
                    h ^= (ulong)(uint)animTileId; h *= FnvPrime;  // H4: animated tile slot
                    h ^= (ulong)(uint)ruleTileId; h *= FnvPrime;  // H6: rule tile slot
                    h ^= layers[i].enabled ? 1UL : 0UL; h *= FnvPrime;
                }
            }

            int fallbackId = tilesetConfig.fallbackTile != null
                ? tilesetConfig.fallbackTile.GetInstanceID()
                : 0;
            h ^= (ulong)(uint)fallbackId; h *= FnvPrime;

            return h;
        }

        private ulong ComputePriorityHash()
        {
            const ulong FnvOffset = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong h = FnvOffset;
            if (priorityTable == null) return h;

            for (int i = 0; i < priorityTable.Length; i++)
            {
                int layerVal = (int)priorityTable[i].LayerId;
                int tileId = priorityTable[i].Tile != null
                    ? priorityTable[i].Tile.GetInstanceID()
                    : 0;

                h ^= (ulong)(uint)layerVal; h *= FnvPrime;
                h ^= (ulong)(uint)tileId; h *= FnvPrime;
            }
            return h;
        }

        private ulong ComputeProceduralHash()
        {
            const ulong FnvOffset = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;

            ulong h = FnvOffset;
            if (proceduralColorTable == null) return h;

            for (int i = 0; i < proceduralColorTable.Length; i++)
            {
                int layerVal = (int)proceduralColorTable[i].LayerId;
                Color32 c = proceduralColorTable[i].Color;

                h ^= (ulong)(uint)layerVal; h *= FnvPrime;
                h ^= c.r; h *= FnvPrime;
                h ^= c.g; h *= FnvPrime;
                h ^= c.b; h *= FnvPrime;
                h ^= c.a; h *= FnvPrime;
            }
            return h;
        }

        // =====================================================================
        // H5 — Multi-layer stamping
        // =====================================================================

        // Static layer partition tables for multi-layer mode.
        //   Base:     opaque ground tiles — water and land.
        //   Overlay:  semi-transparent decoration tiles — vegetation, hills, stairs.
        //   Collider: non-walkable physics cells (DeepWater + MidWater + ShallowWater + HillsL2).
        // Reordering within each group preserves priority semantics (last match per cell wins).
        private static readonly MapLayerId[] s_baseLayers =
        {
            MapLayerId.DeepWater, MapLayerId.MidWater, MapLayerId.ShallowWater,
            MapLayerId.Land, MapLayerId.LandCore, MapLayerId.LandEdge,
        };

        private static readonly MapLayerId[] s_overlayLayers =
        {
            MapLayerId.Vegetation, MapLayerId.HillsL1, MapLayerId.HillsL2, MapLayerId.Stairs,
        };

        private static readonly MapLayerId[] s_colliderLayers =
        {
            MapLayerId.DeepWater, MapLayerId.MidWater, MapLayerId.ShallowWater, MapLayerId.HillsL2,
        };

        private void StampMultiLayer(
            MapDataExport export,
            TilemapLayerEntry[] activeTable,
            TileBase activeFallback)
        {
            // Base group — main Tilemap, ground and water tiles.
            var baseGroup = new TilemapLayerGroup
            {
                Tilemap = tilemap,
                PriorityTable = FilterTable(activeTable, s_baseLayers),
                FallbackTile = activeFallback,
                ClearFirst = true,
                FlipY = flipY,
            };

            // Overlay group — optional transparent Tilemap, vegetation + hills.
            TilemapLayerGroup overlayGroup = null;
            if (overlayTilemap != null)
            {
                overlayGroup = new TilemapLayerGroup
                {
                    Tilemap = overlayTilemap,
                    PriorityTable = FilterTable(activeTable, s_overlayLayers),
                    FallbackTile = null,
                    ClearFirst = true,
                    FlipY = flipY,
                };
            }

            // Collider group — optional invisible Tilemap, non-walkable physics cells.
            TilemapLayerGroup colliderGroup = null;
            if (colliderTilemap != null && colliderTile != null)
            {
                if (enableColliderAutoSetup)
                    TilemapAdapter2D.SetupCollider(colliderTilemap);

                colliderGroup = new TilemapLayerGroup
                {
                    Tilemap = colliderTilemap,
                    PriorityTable = BuildColliderTable(colliderTile, s_colliderLayers),
                    FallbackTile = null,
                    ClearFirst = true,
                    FlipY = flipY,
                };
            }

            // Assemble the groups array (only include assigned/valid groups).
            int count = 1
                + (overlayGroup != null ? 1 : 0)
                + (colliderGroup != null ? 1 : 0);
            var groups = new TilemapLayerGroup[count];
            int gi = 0;
            groups[gi++] = baseGroup;
            if (overlayGroup != null) groups[gi++] = overlayGroup;
            if (colliderGroup != null) groups[gi++] = colliderGroup;

            TilemapAdapter2D.ApplyLayered(export, groups);
        }

        /// <summary>
        /// Returns entries from <paramref name="source"/> whose LayerId is contained in
        /// <paramref name="ids"/>, preserving order. Unmatched entries are excluded.
        /// </summary>
        private static TilemapLayerEntry[] FilterTable(
            TilemapLayerEntry[] source,
            MapLayerId[] ids)
        {
            if (source == null || source.Length == 0)
                return System.Array.Empty<TilemapLayerEntry>();

            var idSet = new HashSet<MapLayerId>(ids);
            var result = new List<TilemapLayerEntry>(ids.Length);
            for (int i = 0; i < source.Length; i++)
                if (idSet.Contains(source[i].LayerId))
                    result.Add(source[i]);
            return result.ToArray();
        }

        /// <summary>
        /// Builds a priority table that maps every id in <paramref name="ids"/> to
        /// <paramref name="tile"/>. Used for the collider group where all non-walkable
        /// cells receive the same sentinel tile regardless of which layer they belong to.
        /// </summary>
        private static TilemapLayerEntry[] BuildColliderTable(
            TileBase tile,
            MapLayerId[] ids)
        {
            var result = new TilemapLayerEntry[ids.Length];
            for (int i = 0; i < ids.Length; i++)
                result[i] = new TilemapLayerEntry { LayerId = ids[i], Tile = tile };
            return result;
        }

        // =====================================================================
        // Procedural palette presets (H2d)
        //
        // Context-menu helpers that populate proceduralColorTable with a
        // ready-to-use color scheme and enable procedural mode in one click.
        // Priority order is always low→high. LandCore sits immediately after Land
        // so higher-priority layers (HillsL1/L2, Vegetation, Stairs, LandEdge)
        // correctly overwrite it rather than being hidden beneath it.
        // =====================================================================

        /// <summary>
        /// Populates the Procedural Color Table with a natural, earthy palette
        /// suitable for gameplay mock-ups and readable map reviews.
        /// Enables Use Procedural Tiles automatically.
        /// </summary>
        [ContextMenu("Procedural Palette / Classic (Natural)")]
        private void SetPalette_Classic()
        {
            useProceduralTiles = true;
            proceduralFallbackColor = Hex("#0D1F2D"); // deep ocean fallback

            proceduralColorTable = new[]
            {
                Entry(MapLayerId.DeepWater,   "#1A3A5C"), // dark navy
                Entry(MapLayerId.MidWater,   "#2D6080"), // slate blue (F4c)
                Entry(MapLayerId.ShallowWater,"#4A90A4"), // steel blue
                Entry(MapLayerId.Land,        "#5A8A3C"), // grass green
                Entry(MapLayerId.LandCore,    "#3D6B1A"), // deep olive / island interior  ← above Land, below terrain features
                Entry(MapLayerId.HillsL1,     "#8B7355"), // tan / light rock
                Entry(MapLayerId.HillsL2,     "#5C3D1E"), // dark brown / high rock
                Entry(MapLayerId.Vegetation,  "#2D5A1B"), // forest green
                Entry(MapLayerId.Stairs,      "#D4B483"), // sandy yellow / mountain pass
                Entry(MapLayerId.LandEdge,    "#7A9A4A"), // olive / coast edge
            };
        }

        /// <summary>
        /// Populates the Procedural Color Table with a high-contrast debug palette
        /// where every layer gets a visually distinct, saturated color.
        /// Best for checking layer coverage and priority ordering.
        /// Enables Use Procedural Tiles automatically.
        /// </summary>
        [ContextMenu("Procedural Palette / Prototyping (Debug)")]
        private void SetPalette_Prototyping()
        {
            useProceduralTiles = true;
            proceduralFallbackColor = new Color(0.15f, 0.15f, 0.15f); // near-black fallback

            proceduralColorTable = new[]
            {
                Entry(MapLayerId.DeepWater,   "#0033CC"), // saturated blue
                Entry(MapLayerId.MidWater,   "#0066FF"), // medium blue (F4c)
                Entry(MapLayerId.ShallowWater,"#00CCFF"), // cyan
                Entry(MapLayerId.Land,        "#33CC33"), // bright green
                Entry(MapLayerId.LandCore,    "#880000"), // dark red  ← above Land, below terrain features
                Entry(MapLayerId.HillsL1,     "#FFFF00"), // yellow
                Entry(MapLayerId.HillsL2,     "#FF8800"), // orange
                Entry(MapLayerId.Vegetation,  "#CC00CC"), // magenta
                Entry(MapLayerId.Stairs,      "#FFFFFF"), // white
                Entry(MapLayerId.LandEdge,    "#FF0000"), // red
            };
        }

        /// <summary>
        /// Populates the Procedural Color Table with a moody twilight palette —
        /// deep purples, teals, and muted gold accents.
        /// Good for atmosphere checks and screenshot reviews.
        /// Enables Use Procedural Tiles automatically.
        /// </summary>
        [ContextMenu("Procedural Palette / Twilight (Moody)")]
        private void SetPalette_Twilight()
        {
            useProceduralTiles = true;
            proceduralFallbackColor = Hex("#050A10"); // near-black ocean fallback

            proceduralColorTable = new[]
            {
                Entry(MapLayerId.DeepWater,   "#0D1B2A"), // deep midnight navy
                Entry(MapLayerId.MidWater,   "#143448"), // dark steel blue (F4c)
                Entry(MapLayerId.ShallowWater,"#1B4D6B"), // dark teal
                Entry(MapLayerId.Land,        "#3D2B5A"), // dark purple
                Entry(MapLayerId.LandCore,    "#5A1B5A"), // deep magenta-purple interior  ← above Land, below terrain features
                Entry(MapLayerId.HillsL1,     "#6B4F7A"), // muted violet
                Entry(MapLayerId.HillsL2,     "#4A2D5C"), // deep violet / crag
                Entry(MapLayerId.Vegetation,  "#1B4A3D"), // dark teal green
                Entry(MapLayerId.Stairs,      "#C8A96E"), // warm gold / mountain pass
                Entry(MapLayerId.LandEdge,    "#7A4A6B"), // mauve coast
            };
        }

        // ---- Palette helpers ------------------------------------------------

        private static ProceduralTileEntry Entry(MapLayerId id, string hex) =>
            new ProceduralTileEntry { LayerId = id, Color = Hex(hex) };

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

    }
}