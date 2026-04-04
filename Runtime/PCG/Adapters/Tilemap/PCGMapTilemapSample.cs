using Unity.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;
using Islands.PCG.Samples; // H3: MapGenerationPreset

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Sample MonoBehaviour: runs the full PCG map pipeline and stamps the result into a
    /// Unity <see cref="UnityEngine.Tilemaps.Tilemap"/> via <see cref="TilemapAdapter2D.Apply"/>.
    ///
    /// Provides the first fully playable game-map output from the Islands PCG pipeline.
    ///
    /// Phase H3: optional MapGenerationPreset and TilesetConfig slots added.
    /// When assigned, preset overrides all pipeline parameters (seed, resolution,
    /// stage toggles, F2 tunables); TilesetConfig overrides the inline priority table.
    /// Both use override-at-resolve — inline fields remain active when null.
    ///
    /// Sample-side only — no runtime contracts changed. Adapters-last invariant preserved.
    ///
    /// ── Inspector Setup ──────────────────────────────────────────────────────────
    /// 1. Create a Grid GameObject in your scene; attach a Tilemap child.
    /// 2. Attach this component to any GameObject in the same scene.
    /// 3. Assign the Tilemap reference.
    /// 4. (H3) Assign a MapGenerationPreset to drive pipeline params from a shared asset.
    /// 5. (H3) Assign a TilesetConfig to drive tile bindings from a shared asset.
    ///    — OR — Populate the inline Priority Table manually.
    /// 6. Hit Play or use the right-click context menu "Generate" to stamp the map.
    ///
    /// ── Coordinate orientation ───────────────────────────────────────────────────
    /// Cell (x, y) maps to Tilemap position (x, y, 0). Enable Flip Y if upside down.
    /// </summary>
    [AddComponentMenu("Islands/PCG/Map Tilemap Sample")]
    public sealed class PCGMapTilemapSample : MonoBehaviour
    {
        // ─────────────────────────────────────────────
        // Inspector — Preset (optional, H3)
        // ─────────────────────────────────────────────

        [Header("Preset (optional)")]
        [Tooltip("Assign a MapGenerationPreset asset to override seed, resolution, " +
                 "stage toggles, and all F2 tunables.\n" +
                 "When null this component's own inline fields are used (backward compatible).")]
        [SerializeField] private MapGenerationPreset preset;

        // ─────────────────────────────────────────────
        // Inspector — Tileset Config (optional, H3)
        // ─────────────────────────────────────────────

        [Header("Tileset Config (optional)")]
        [Tooltip("Assign a TilesetConfig asset to override the inline Priority Table " +
                 "with a complete tile art set.\n" +
                 "When null the inline Priority Table is used (backward compatible).")]
        [SerializeField] private TilesetConfig tilesetConfig;

        // ─────────────────────────────────────────────
        // Inspector — Grid
        // ─────────────────────────────────────────────

        [Header("Grid")]
        [Tooltip("Grid resolution in cells (square). Typical range: 32–256.")]
        [SerializeField] private int resolution = 64;

        [Tooltip("Run seed (>= 1). Same seed + same tunables = same map.")]
        [SerializeField] private uint seed = 1;

        [Tooltip("Pipeline tunables. Leave as Default for standard island generation.\n" +
                 "Overridden by a MapGenerationPreset when one is assigned.")]
        [SerializeField] private MapTunables2D tunables = MapTunables2D.Default;

        // ─────────────────────────────────────────────
        // Inspector — Tilemap
        // ─────────────────────────────────────────────

        [Header("Tilemap")]
        [Tooltip("The Unity Tilemap to stamp. Must be assigned before generating.")]
        [SerializeField] private UnityEngine.Tilemaps.Tilemap tilemap;

        [Tooltip("If true, Y is mirrored: tileY = (Height - 1 - y). Use when the map is upside down.")]
        [SerializeField] private bool flipY = false;

        [Header("Priority Table  (low → high priority)")]
        [Tooltip("Ordered entries: earlier = lower priority. Last matching layer wins per cell.\n" +
                 "Ignored when a TilesetConfig is assigned.")]
        [SerializeField] private TilemapLayerEntry[] priorityTable = System.Array.Empty<TilemapLayerEntry>();

        [Tooltip("Tile placed at cells where no priority entry matches. Optional.")]
        [SerializeField] private TileBase fallbackTile;

        // ─────────────────────────────────────────────
        // Lifecycle
        // ─────────────────────────────────────────────

        private void Start() => Generate();

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        /// <summary>
        /// Runs the full pipeline and stamps the Tilemap.
        /// Safe to call from the Inspector context menu or from other scripts.
        /// </summary>
        [ContextMenu("Generate")]
        public void Generate()
        {
            if (tilemap == null)
            {
                Debug.LogError("[PCGMapTilemapSample] Tilemap is not assigned.", this);
                return;
            }

            // H3: resolve effective values (preset overrides inline fields when assigned).
            uint rawSeed = preset != null ? preset.seed : seed;
            uint eSeed = rawSeed < 1u ? 1u : rawSeed;
            int eRes = Mathf.Max(4, preset != null ? preset.resolution : resolution);
            var eTunables = preset != null ? preset.ToTunables() : tunables;

            // H3: TilesetConfig overrides inline priority table.
            TilemapLayerEntry[] eTable;
            TileBase eFallback;

            if (tilesetConfig != null)
            {
                TilemapLayerEntry[] fromConfig = tilesetConfig.ToLayerEntries();
                eTable = fromConfig ?? priorityTable;
                eFallback = (tilesetConfig.fallbackTile != null) ? tilesetConfig.fallbackTile : fallbackTile;
            }
            else
            {
                eTable = priorityTable;
                eFallback = fallbackTile;
            }

            var domain = new GridDomain2D(eRes, eRes);
            var inputs = new MapInputs(eSeed, domain, eTunables);

            // ─── Allocate context and stages ───────────────────────────────────
            var ctx = new MapContext2D(domain, Allocator.Temp);
            try
            {
                var stages = new IMapStage2D[]
                {
                    new Stage_BaseTerrain2D(),
                    new Stage_Hills2D(),
                    new Stage_Shore2D(),
                    new Stage_Vegetation2D(),
                    new Stage_Traversal2D(),
                    new Stage_Morphology2D(),
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: true);

                // ─── Export ────────────────────────────────────────────────────
                MapDataExport export = MapExporter2D.Export(ctx);

                // ─── Apply to Tilemap ──────────────────────────────────────────
                TilemapAdapter2D.Apply(
                    export,
                    tilemap,
                    eTable,
                    eFallback,
                    clearFirst: true,
                    flipY: flipY);

                // ─── Count stamped tiles (for console diagnostics) ─────────────
                int stamped = 0;
                for (int y = 0; y < eRes; y++)
                    for (int x = 0; x < eRes; x++)
                        if (tilemap.GetTile(new Vector3Int(x, y, 0)) != null)
                            stamped++;

                Debug.Log(
                    $"[PCGMapTilemapSample] Generated {eRes}x{eRes} seed={eSeed} " +
                    $"tilesStamped={stamped}/{eRes * eRes} flipY={flipY}");
            }
            finally
            {
                ctx.Dispose();
            }
        }
    }
}