using UnityEngine;
using UnityEngine.Tilemaps;

using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Per-biome tile art overrides, layered additively on top of a base
    /// <see cref="TilesetConfig"/>. Authored in the Inspector as biome groups,
    /// each listing which layers get biome-specific tiles. Unlisted (biome, layer)
    /// pairs fall through to the base tileset — no sentinel, no error.
    ///
    /// Runtime lookup is O(1) per cell via a flat <c>TileBase[]</c> array of size
    /// <c>BiomeType.COUNT × MapLayerId.COUNT</c>, rebuilt lazily on first access
    /// and on <see cref="OnValidate"/>.
    ///
    /// Tile resolution priority per slot (H6 convention):
    ///   ruleTile assigned  → ruleTile
    ///   animatedTile only  → animatedTile
    ///   tile only          → tile
    ///   all null           → null (no override for this pair)
    ///
    /// Phase Q.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BiomeTileOverride",
        menuName = "Islands/PCG/Biome Tile Override",
        order = 102)]
    public sealed class BiomeTileOverride : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Nested types
        // ------------------------------------------------------------------

        /// <summary>
        /// One tile override slot for a specific pipeline layer within a biome group.
        /// Priority: ruleTile > animatedTile > tile (matches H6 convention).
        /// </summary>
        [System.Serializable]
        public struct LayerSlot
        {
            [Tooltip("Which pipeline layer this override applies to.")]
            public MapLayerId layerId;

            [Tooltip("Static tile. Ignored when Animated or Rule Tile assigned.")]
            public TileBase tile;

            [Tooltip("Animated tile. Ignored when Rule Tile assigned.")]
            public TileBase animatedTile;

            [Tooltip("Rule Tile (highest priority).")]
            public TileBase ruleTile;
        }

        /// <summary>
        /// A group of layer overrides for a single biome type.
        /// Only layers listed in <see cref="layers"/> are overridden; all others
        /// fall through to the base <see cref="TilesetConfig"/>.
        /// </summary>
        [System.Serializable]
        public struct BiomeGroup
        {
            [Tooltip("Biome this group applies to.")]
            public BiomeType biome;

            [Tooltip("Layer tile overrides for this biome. Only layers listed here are" +
                     " overridden; unlisted layers fall through to the base TilesetConfig.")]
            public LayerSlot[] layers;
        }

        // ------------------------------------------------------------------
        // Inspector fields
        // ------------------------------------------------------------------

        [Tooltip("Per-biome tile override groups. Each group specifies which layers get" +
                 " biome-specific tiles. Order within the array doesn't matter —" +
                 " duplicate biome entries are resolved last-wins.")]
        public BiomeGroup[] groups;

        // ------------------------------------------------------------------
        // Runtime lookup
        // ------------------------------------------------------------------

        private const int BiomeCount = (int)BiomeType.COUNT;
        private const int LayerCount = (int)MapLayerId.COUNT;

        /// <summary>
        /// Flat lookup: index = biome * LayerCount + layerId.
        /// Null entry = no override for that (biome, layer) pair.
        /// </summary>
        [System.NonSerialized]
        private TileBase[] _lookup;

        /// <summary>
        /// Returns the override tile for the given (biome, layer) pair, or null
        /// when no override is configured (caller should fall through to base tile).
        ///
        /// O(1) — single array index. No per-call allocation.
        /// </summary>
        public TileBase Resolve(BiomeType biome, MapLayerId layerId)
        {
            if (_lookup == null) RebuildLookup();

            int idx = (int)biome * LayerCount + (int)layerId;
            if (idx < 0 || idx >= _lookup.Length) return null;
            return _lookup[idx];
        }

        /// <summary>
        /// Rebuilds the flat lookup array from the authored <see cref="groups"/>.
        /// Called lazily on first <see cref="Resolve"/> and from <see cref="OnValidate"/>.
        /// </summary>
        public void RebuildLookup()
        {
            _lookup = new TileBase[BiomeCount * LayerCount];

            if (groups == null) return;

            for (int g = 0; g < groups.Length; g++)
            {
                BiomeGroup bg = groups[g];
                if (bg.layers == null) continue;

                int biomeBase = (int)bg.biome * LayerCount;

                for (int s = 0; s < bg.layers.Length; s++)
                {
                    LayerSlot slot = bg.layers[s];

                    // H6 priority: ruleTile > animatedTile > tile.
                    TileBase winner = null;
                    if (slot.ruleTile != null) winner = slot.ruleTile;
                    else if (slot.animatedTile != null) winner = slot.animatedTile;
                    else if (slot.tile != null) winner = slot.tile;

                    if (winner != null)
                    {
                        int idx = biomeBase + (int)slot.layerId;
                        // Duplicate biome groups: last-wins (array iteration order).
                        _lookup[idx] = winner;
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Editor hooks
        // ------------------------------------------------------------------

        private void OnValidate()
        {
            RebuildLookup();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Creates one <see cref="BiomeGroup"/> per <see cref="BiomeType"/>
        /// (excluding <see cref="BiomeType.Unclassified"/>) with recommended layer
        /// slots pre-listed but tile references null. Safe to run on a fresh asset.
        /// Overwrites existing groups.
        /// </summary>
        [ContextMenu("Populate Default Biome Groups")]
        private void PopulateDefaultBiomeGroups()
        {
            // Recommended layers for biome variation (Q-DD-5).
            var recommendedLayers = new[]
            {
                MapLayerId.Land,
                MapLayerId.Vegetation,
                MapLayerId.HillsL1,
                MapLayerId.HillsL2,
            };

            // One group per biome, excluding Unclassified.
            int biomeCount = (int)BiomeType.COUNT - 1; // skip Unclassified (0)
            groups = new BiomeGroup[biomeCount];

            for (int i = 0; i < biomeCount; i++)
            {
                BiomeType biome = (BiomeType)(i + 1); // 1..12

                var slots = new LayerSlot[recommendedLayers.Length];
                for (int s = 0; s < recommendedLayers.Length; s++)
                {
                    slots[s] = new LayerSlot
                    {
                        layerId = recommendedLayers[s],
                        tile = null,
                        animatedTile = null,
                        ruleTile = null,
                    };
                }

                groups[i] = new BiomeGroup
                {
                    biome = biome,
                    layers = slots,
                };
            }

            RebuildLookup();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[BiomeTileOverride] '{name}': populated {biomeCount} default biome groups.");
        }
#endif
    }
}