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
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Islands/PCG/Map Tilemap Visualization")]
    public sealed class PCGMapTilemapVisualization : MonoBehaviour
    {
        // =====================================================================
        // Inspector — Tilemap Target
        // =====================================================================
        [Header("Tilemap Target")]
        [Tooltip("The Unity Tilemap to stamp. Must be assigned before the visualization runs.")]
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
                      islandAspectRatio, warpAmplitude01);

            EnsureContextAllocated(eRes);

            // ---- Configure configurable base stage ----
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

            // ---- Stamp tilemap ----
            TilemapAdapter2D.Apply(
                export,
                tilemap,
                activeTable,
                activeFallback,
                clearFirst: true,
                flipY: flipY);

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
            lastFlipY = flipY;
            lastClearBeforeRun = preset != null ? preset.clearBeforeRun : clearBeforeRun;
            lastFallbackTile = fallbackTile;
            lastPriorityHash = ComputePriorityHash();

            lastUseProceduralTiles = useProceduralTiles;
            lastProceduralFallbackColor = proceduralFallbackColor;
            lastProceduralHash = ComputeProceduralHash();
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
                || flipY != lastFlipY
                || (preset != null ? preset.clearBeforeRun : clearBeforeRun) != lastClearBeforeRun
                || fallbackTile != lastFallbackTile
                || ComputePriorityHash() != lastPriorityHash
                || useProceduralTiles != lastUseProceduralTiles
                || proceduralFallbackColor != lastProceduralFallbackColor
                || ComputeProceduralHash() != lastProceduralHash;
        }

        // FNV-1a over TilesetConfig content (layerId + tile InstanceIDs + animatedTile InstanceIDs
        // + enabled booleans + fallback InstanceID).
        // Detects tile swaps, animated tile swaps, enabled-toggle edits, layerId changes, and
        // fallback changes made directly to the SO asset while it is assigned.
        // Reference-equality alone cannot detect these in-place edits.
        // Runs every Update() frame; cost is O(MapLayerId.COUNT) — negligible (12 iterations).
        // H4: animatedTile InstanceID added to loop so Inspector edits to animated slots trigger rebuild.
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
                    int layerId = (int)layers[i].layerId;
                    h ^= (ulong)(uint)layerId; h *= FnvPrime;
                    h ^= (ulong)(uint)tileId; h *= FnvPrime;
                    h ^= (ulong)(uint)animTileId; h *= FnvPrime;  // H4: animated tile slot
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

        // =====================================================================
        // BaseTerrainStage_Configurable (keep in sync with other copies)
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