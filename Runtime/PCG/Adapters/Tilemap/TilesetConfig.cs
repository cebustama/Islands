using UnityEngine;
using UnityEngine.Tilemaps;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// ScriptableObject configuration for a complete tileset used by the PCG
    /// map tilemap components.
    ///
    /// Provides one <see cref="LayerEntry"/> per <see cref="MapLayerId"/> — label,
    /// explicit layer identity, tile assets (static, animated, rule tile),
    /// and enabled toggle — plus a fallback <see cref="TileBase"/> for unmatched cells.
    ///
    /// Priority ordering:
    ///   The array order IS the stamp priority — entries earlier in the array are
    ///   lower priority (later entries overwrite them).
    ///
    /// Phase H3: initial implementation.
    /// Phase H4: animatedTile slot.
    /// Phase H6: ruleTile slot (context-aware neighbor-matching).
    /// Phase F4c: MidWater added to default priority order.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TilesetConfig",
        menuName = "Islands/PCG/Tileset Config",
        order = 101)]
    public sealed class TilesetConfig : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Visual priority order (low → high) used for BuildDefaultLayers().
        // ------------------------------------------------------------------
        private static readonly MapLayerId[] s_defaultPriorityOrder =
        {
            MapLayerId.DeepWater,     // 0 — base ocean (deepest)
            MapLayerId.MidWater,      // 1 — intermediate depth (F4c; overwrites DeepWater)
            MapLayerId.ShallowWater,  // 2 — coastal water (overwrites MidWater)
            MapLayerId.Land,          // 3 — grass base
            MapLayerId.LandInterior,  // 4 — interior tint (coast-edge excluded)
            MapLayerId.LandCore,      // 5 — deep interior tint (eroded, Phase G)
            MapLayerId.Vegetation,    // 6 — forest (overwrites interior land)
            MapLayerId.HillsL1,       // 7 — hills (overwrites vegetation)
            MapLayerId.HillsL2,       // 8 — mountain peaks (overwrites hills)
            MapLayerId.Stairs,        // 9 — mountain passes (overwrites HillsL1 edge)
            MapLayerId.LandEdge,      // 10 — coast highlight (high priority)
            MapLayerId.Walkable,      // 11 — traversal (usually no tile; informational)
            MapLayerId.Paths,         // 12 — path network (reserved; Phase O)
        };

        // ------------------------------------------------------------------
        // LayerEntry
        // ------------------------------------------------------------------

        /// <summary>
        /// One layer-to-tile binding inside a <see cref="TilesetConfig"/>.
        ///
        /// Tile resolution priority (H6):
        ///   enabled + ruleTile assigned      → ruleTile stamped
        ///   enabled + animatedTile assigned   → animatedTile stamped
        ///   enabled + tile assigned           → tile stamped
        ///   enabled + all null               → null (skipped)
        ///   disabled                          → null (skipped)
        /// </summary>
        [System.Serializable]
        public struct LayerEntry
        {
            [Tooltip("Human-readable layer name. Edit freely — no runtime effect.")]
            public string label;

            [Tooltip("Which pipeline layer this entry represents.")]
            public MapLayerId layerId;

            [Tooltip("Static tile asset. Ignored when Animated Tile or Rule Tile is assigned.")]
            public TileBase tile;

            [Tooltip("Animated tile asset. Ignored when Rule Tile is assigned.")]
            public TileBase animatedTile;

            [Tooltip("Rule Tile asset (context-aware neighbor-matching). Highest priority.")]
            public TileBase ruleTile;

            [Tooltip("Include this layer in the stamp pass.")]
            public bool enabled;
        }

        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Tooltip("One entry per MapLayerId. Array ORDER is stamp priority (low → high).")]
        public LayerEntry[] layers = BuildDefaultLayers();

        [Tooltip("Tile stamped for cells not matched by any enabled, tile-assigned entry.")]
        public TileBase fallbackTile;

        // ------------------------------------------------------------------
        // API
        // ------------------------------------------------------------------

        /// <summary>
        /// Converts this config to a <see cref="TilemapLayerEntry"/> array.
        /// H6 tile resolution: ruleTile > animatedTile > tile.
        /// </summary>
        public TilemapLayerEntry[] ToLayerEntries()
        {
            if (layers == null || layers.Length != (int)MapLayerId.COUNT)
            {
                Debug.LogWarning(
                    $"[TilesetConfig] '{name}': layers.Length ({layers?.Length ?? 0}) " +
                    $"!= MapLayerId.COUNT ({(int)MapLayerId.COUNT}). " +
                    "Falling back to component's inline array.");
                return null;
            }

            var result = new TilemapLayerEntry[(int)MapLayerId.COUNT];
            for (int i = 0; i < result.Length; i++)
            {
                TileBase entryTile = null;
                if (layers[i].enabled)
                {
                    if (layers[i].ruleTile != null)
                        entryTile = layers[i].ruleTile;
                    else if (layers[i].animatedTile != null)
                        entryTile = layers[i].animatedTile;
                    else if (layers[i].tile != null)
                        entryTile = layers[i].tile;
                }

                result[i] = new TilemapLayerEntry
                {
                    LayerId = layers[i].layerId,
                    Tile = entryTile,
                };
            }
            return result;
        }

        // ------------------------------------------------------------------
        // Default layers — visual priority order
        // ------------------------------------------------------------------

        private static LayerEntry[] BuildDefaultLayers()
        {
            int count = s_defaultPriorityOrder.Length;
            var arr = new LayerEntry[count];
            for (int i = 0; i < count; i++)
            {
                MapLayerId id = s_defaultPriorityOrder[i];
                arr[i] = new LayerEntry
                {
                    label = id.ToString(),
                    layerId = id,
                    tile = null,
                    animatedTile = null,
                    ruleTile = null,
                    enabled = true,
                };
            }
            return arr;
        }
    }
}