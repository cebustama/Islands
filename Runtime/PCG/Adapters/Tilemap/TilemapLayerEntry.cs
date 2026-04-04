using System;
using Islands.PCG.Layout.Maps;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// One entry in the priority table passed to <see cref="TilemapAdapter2D.Apply"/>.
    ///
    /// Maps a single <see cref="MapLayerId"/> to the <see cref="TileBase"/> asset that
    /// represents it in the target <see cref="UnityEngine.Tilemaps.Tilemap"/>.
    ///
    /// Priority is positional: entries earlier in the array are lower priority (overwritten
    /// by later entries). This is rendering priority only — pipeline generation is unaffected.
    ///
    /// Serializable so it can be assigned in the Unity Inspector.
    /// </summary>
    [Serializable]
    public struct TilemapLayerEntry
    {
        /// <summary>The pipeline layer this entry represents.</summary>
        public MapLayerId LayerId;

        /// <summary>
        /// The tile asset to stamp for this layer. Null entries are valid —
        /// they act as "skip this layer" (do not overwrite the current winner).
        /// </summary>
        public TileBase Tile;
    }
}