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
    /// explicit layer identity, <see cref="TileBase"/> asset, optional animated
    /// <see cref="TileBase"/> asset, and enabled toggle — plus a fallback
    /// <see cref="TileBase"/> for unmatched cells.
    ///
    /// When assigned to a tilemap component's <c>tilesetConfig</c> slot it
    /// replaces the component's inline <see cref="TilemapLayerEntry"/> array as the
    /// source of tile-to-layer bindings (procedural tile mode takes precedence over
    /// both).  When null the inline array is used unchanged.
    ///
    /// Priority ordering:
    ///   The array order IS the stamp priority — entries earlier in the array are
    ///   lower priority (later entries overwrite them). Reorder entries freely in
    ///   the Inspector to change priority without breaking the LayerId mapping.
    ///   The default order matches the recommended visual priority for JRPG-style maps:
    ///   DeepWater → ShallowWater → Land → LandInterior → LandCore → Vegetation
    ///   → HillsL1 → HillsL2 → Stairs → LandEdge → Walkable → Paths
    ///   (Hills overwrite Vegetation; LandEdge is a high-priority coast highlight.)
    ///
    /// Phase H3: initial implementation.
    /// Phase H3 fix (null-means-skip): unassigned/disabled entries emit null tile,
    ///   matching the TilemapAdapter2D skip contract. fallbackTile is the per-cell
    ///   adapter fallback, not a per-entry tile.
    /// Phase H3 fix (explicit layerId): each LayerEntry stores its own MapLayerId,
    ///   decoupling identity from array position so the Inspector array can be freely
    ///   reordered to adjust priority without scrambling the LayerId-to-tile mapping.
    /// Phase H4 (animated tile slot): LayerEntry gains an optional animatedTile field
    ///   (TileBase). When assigned, animatedTile takes precedence over tile at stamp
    ///   time, enabling AnimatedTile assets (e.g. ocean waves on DeepWater) without
    ///   changing TilemapAdapter2D or any runtime pipeline contract. The static tile
    ///   field remains the non-animated fallback.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TilesetConfig",
        menuName = "Islands/PCG/Tileset Config",
        order = 101)]
    public sealed class TilesetConfig : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Visual priority order (low → high) used for BuildDefaultLayers().
        // Reordering entries in the Inspector changes priority at runtime.
        // Hills appear AFTER Vegetation so mountains overwrite forest tiles.
        // ------------------------------------------------------------------
        private static readonly MapLayerId[] s_defaultPriorityOrder =
        {
            MapLayerId.DeepWater,     // 0 — base ocean
            MapLayerId.ShallowWater,  // 1 — coastal water (overlaps DeepWater)
            MapLayerId.Land,          // 2 — grass base
            MapLayerId.LandInterior,  // 3 — interior tint (coast-edge excluded)
            MapLayerId.LandCore,      // 4 — deep interior tint (eroded, Phase G)
            MapLayerId.Vegetation,    // 5 — forest (overwrites interior land)
            MapLayerId.HillsL1,       // 6 — hills (overwrites vegetation)
            MapLayerId.HillsL2,       // 7 — mountain peaks (overwrites hills)
            MapLayerId.Stairs,        // 8 — mountain passes (overwrites HillsL1 edge)
            MapLayerId.LandEdge,      // 9 — coast highlight (high priority)
            MapLayerId.Walkable,      // 10 — traversal (usually no tile; informational)
            MapLayerId.Paths,         // 11 — path network (reserved; Phase O)
        };

        // ------------------------------------------------------------------
        // LayerEntry
        // ------------------------------------------------------------------

        /// <summary>
        /// One layer-to-tile binding inside a <see cref="TilesetConfig"/>.
        ///
        /// <see cref="layerId"/> stores which pipeline layer this entry represents.
        /// Array position determines stamp priority (low → high); reorder freely.
        ///
        /// Tile resolution priority (H4):
        ///   enabled + animatedTile assigned → animatedTile stamped
        ///   enabled + tile assigned         → tile stamped
        ///   enabled + both null             → null (skipped)
        ///   disabled                        → null (skipped)
        /// </summary>
        [System.Serializable]
        public struct LayerEntry
        {
            [Tooltip("Human-readable layer name. Edit freely — no runtime effect.")]
            public string label;

            [Tooltip("Which pipeline layer this entry represents.\n" +
                     "Each MapLayerId should appear exactly once across all entries.\n" +
                     "Array ORDER determines priority (low→high) — reorder entries to " +
                     "change which layer draws on top of which.")]
            public MapLayerId layerId;

            [Tooltip("Static tile asset to stamp when this layer's cell is ON.\n" +
                     "Leave null to skip this layer (it will not affect the tilemap).\n" +
                     "Ignored when Animated Tile is assigned (animated wins).")]
            public TileBase tile;

            [Tooltip("Animated tile asset (e.g. AnimatedTile from 2D Tilemap Extras) for this layer.\n" +
                     "When assigned, takes precedence over the static Tile field at stamp time.\n" +
                     "Natural candidates: DeepWater (ocean waves), ShallowWater (coastal ripples).\n" +
                     "Leave null to use the static Tile instead. Enabled toggle applies to both.\n\n" +
                     "Requires the 2D Tilemap Extras package (Package Manager → " +
                     "com.unity.2d.tilemap.extras). Sprite sheets are sliced per " +
                     "Documentation~/reference/tileset-import-guide.md.")]
            public TileBase animatedTile;

            [Tooltip("Include this layer in the stamp pass. " +
                     "Disabled entries are skipped (produce null in the priority table).")]
            public bool enabled;
        }

        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Tooltip("One entry per MapLayerId. Array ORDER is stamp priority (low → high).\n" +
                 "Reorder entries in the Inspector to change which layer draws on top.\n\n" +
                 "Tile resolution per entry (H4):\n" +
                 "  enabled + Animated Tile assigned → animated tile stamped\n" +
                 "  enabled + Tile assigned          → static tile stamped\n" +
                 "  enabled + both null              → skipped\n" +
                 "  disabled                         → skipped\n\n" +
                 "Assign an animated tile only to layers you want to animate (e.g. DeepWater).")]
        public LayerEntry[] layers = BuildDefaultLayers();

        [Tooltip("Tile stamped for cells not matched by any enabled, tile-assigned entry.\n" +
                 "Applied per-cell by the adapter — NOT propagated into individual entries.\n\n" +
                 "Typical use: assign your DeepWater tile here so unmatched cells\n" +
                 "fall back to open ocean.")]
        public TileBase fallbackTile;

        // ------------------------------------------------------------------
        // API
        // ------------------------------------------------------------------

        /// <summary>
        /// Converts this config to a <see cref="TilemapLayerEntry"/> array for
        /// <see cref="TilemapAdapter2D.Apply"/>.
        ///
        /// Array order is preserved as the priority order (low → high).
        /// Returns <c>null</c> if <see cref="layers"/> length does not match
        /// <see cref="MapLayerId.COUNT"/>; callers fall back to their inline array.
        ///
        /// Entry behavior (H4 tile resolution priority):
        ///   enabled + animatedTile → animatedTile stamped when layerId's cell is ON
        ///   enabled + tile         → tile stamped when layerId's cell is ON
        ///   enabled + both null    → null (skipped; layerId has no visual effect)
        ///   disabled               → null (skipped)
        ///
        /// <see cref="fallbackTile"/> is NOT propagated into entries — pass it
        /// separately as the per-cell adapter fallback.
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
                // LayerId is stored explicitly in the entry — decoupled from position.
                // Array order = stamp priority (low → high); reorder without breaking mapping.
                //
                // H4 tile resolution priority (animated wins over static):
                //   enabled + animatedTile → animatedTile
                //   enabled + tile         → tile
                //   enabled + both null    → null (skipped)
                //   disabled               → null (skipped)
                //
                // fallbackTile is the per-cell adapter fallback — NOT substituted here.
                TileBase entryTile = null;
                if (layers[i].enabled)
                {
                    if (layers[i].animatedTile != null)
                        entryTile = layers[i].animatedTile;
                    else if (layers[i].tile != null)
                        entryTile = layers[i].tile;
                }

                result[i] = new TilemapLayerEntry
                {
                    LayerId = layers[i].layerId,   // explicit — not derived from position
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
                    enabled = true,
                };
            }
            return arr;
        }
    }
}