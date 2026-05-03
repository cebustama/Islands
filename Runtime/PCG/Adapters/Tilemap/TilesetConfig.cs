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
    /// Phase L: Rivers and Lakes added. ToLayerEntries() made robust against
    ///          pre-Phase-L assets (13-entry arrays still resolve correctly for
    ///          the layers they cover). Migration context menu added.
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
            MapLayerId.Lakes,         // 1 — inland water bodies (Phase L; overwrites DeepWater)
            MapLayerId.MidWater,      // 2 — intermediate depth (F4c; overwrites DeepWater)
            MapLayerId.ShallowWater,  // 3 — coastal water (overwrites MidWater)
            MapLayerId.Land,          // 4 — grass base
            MapLayerId.LandInterior,  // 5 — interior tint (coast-edge excluded)
            MapLayerId.LandCore,      // 6 — deep interior tint (eroded, Phase G)
            MapLayerId.Vegetation,    // 7 — forest (overwrites interior land)
            MapLayerId.HillsL1,       // 8 — hills (overwrites vegetation)
            MapLayerId.HillsL2,       // 9 — mountain peaks (overwrites hills)
            MapLayerId.Rivers,        // 10 — rivers (Phase L; overwrites land)
            MapLayerId.Stairs,        // 11 — mountain passes (overwrites HillsL1 edge)
            MapLayerId.LandEdge,      // 12 — coast highlight (high priority)
            MapLayerId.Walkable,      // 13 — traversal (usually no tile; informational)
            MapLayerId.Paths,         // 14 — path network (reserved; Phase O)
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
        /// Converts this config to a <see cref="TilemapLayerEntry"/> array suitable
        /// for <see cref="TilemapAdapter2D"/>.
        ///
        /// Phase L: now resolves entries by <see cref="MapLayerId"/> key rather than
        /// array index, so pre-Phase-L assets (13-entry arrays) continue to work for
        /// the layers they cover.  Missing layers (Rivers, Lakes) simply produce no
        /// tile stamp — the fallback tile applies to unmatched cells as before.
        ///
        /// Returns null only when the layers array is null or empty.
        /// </summary>
        public TilemapLayerEntry[] ToLayerEntries()
        {
            if (layers == null || layers.Length == 0)
            {
                Debug.LogWarning(
                    $"[TilesetConfig] '{name}': layers array is null or empty. " +
                    "Falling back to component's inline array.");
                return null;
            }

            int count = (int)MapLayerId.COUNT;

            if (layers.Length != count)
            {
                // Warn but continue — build a keyed lookup from whatever entries exist.
                Debug.LogWarning(
                    $"[TilesetConfig] '{name}': layers.Length ({layers.Length}) " +
                    $"!= MapLayerId.COUNT ({count}). " +
                    "Some layers may be missing. Use 'Migrate to Phase L' context menu " +
                    "to add Rivers and Lakes without losing existing tile assignments.");
            }

            // Build lookup by layerId (robust against any array length or order).
            var lookup = new System.Collections.Generic.Dictionary<MapLayerId, LayerEntry>(layers.Length);
            foreach (var entry in layers)
                lookup[entry.layerId] = entry;

            var result = new TilemapLayerEntry[count];
            for (int i = 0; i < count; i++)
            {
                var layerId = (MapLayerId)i;
                TileBase entryTile = null;

                if (lookup.TryGetValue(layerId, out LayerEntry e) && e.enabled)
                {
                    if (e.ruleTile != null) entryTile = e.ruleTile;
                    else if (e.animatedTile != null) entryTile = e.animatedTile;
                    else if (e.tile != null) entryTile = e.tile;
                }

                result[i] = new TilemapLayerEntry
                {
                    LayerId = layerId,
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

        // ------------------------------------------------------------------
        // Migration — context menu (Editor only)
        // ------------------------------------------------------------------

#if UNITY_EDITOR
        /// <summary>
        /// Appends missing Phase L layers (Rivers, Lakes) to an existing
        /// 13-entry TilesetConfig without wiping any existing tile assignments.
        ///
        /// Safe to run multiple times — already-present layers are skipped.
        /// Run this on every existing TilesetConfig asset after updating to Phase L.
        /// </summary>
        [ContextMenu("Migrate to Phase L (add Rivers + Lakes)")]
        private void MigrateToPhaseL()
        {
            var phaseL = new[] { MapLayerId.Rivers, MapLayerId.Lakes };

            // Collect which layerIds are already present.
            var present = new System.Collections.Generic.HashSet<MapLayerId>();
            if (layers != null)
                foreach (var e in layers) present.Add(e.layerId);

            bool anyAdded = false;
            foreach (var id in phaseL)
            {
                if (present.Contains(id)) continue;

                var newEntry = new LayerEntry
                {
                    label = id.ToString(),
                    layerId = id,
                    tile = null,
                    animatedTile = null,
                    ruleTile = null,
                    enabled = true,
                };

                // Append to array.
                int oldLen = layers?.Length ?? 0;
                var next = new LayerEntry[oldLen + 1];
                if (layers != null) System.Array.Copy(layers, next, oldLen);
                next[oldLen] = newEntry;
                layers = next;
                anyAdded = true;

                Debug.Log($"[TilesetConfig] '{name}': added entry for {id}.");
            }

            if (!anyAdded)
            {
                Debug.Log($"[TilesetConfig] '{name}': already up to date — no entries added.");
                return;
            }

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"[TilesetConfig] '{name}': migration complete. layers.Length = {layers.Length}.");
        }
#endif
    }
}