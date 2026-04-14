using UnityEngine;
using UnityEngine.Tilemaps;

using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Configures a 2×2 mega-tile replacement rule for a specific pipeline layer.
    /// Evaluated by <see cref="MegaTileScanner"/> to find qualifying blocks, then
    /// stamped by <see cref="MegaTileStamper"/> using the four quadrant tiles.
    ///
    /// Quadrant naming is from the artist's visual perspective (screen space):
    ///   TL = top-left, TR = top-right, BL = bottom-left, BR = bottom-right.
    /// The stamper handles flipY mapping between pipeline coords and tilemap coords.
    ///
    /// Phase H8.
    /// </summary>
    [System.Serializable]
    public struct MegaTileRule
    {
        [Tooltip("Which pipeline mask layer to scan for qualifying 2×2 blocks.\n" +
                 "All four cells must have this layer set for a block to qualify.")]
        public MapLayerId targetLayer;

        [Tooltip("Visual top-left quadrant tile.")]
        public TileBase quadrantTL;

        [Tooltip("Visual top-right quadrant tile.")]
        public TileBase quadrantTR;

        [Tooltip("Visual bottom-left quadrant tile.")]
        public TileBase quadrantBL;

        [Tooltip("Visual bottom-right quadrant tile.")]
        public TileBase quadrantBR;

        /// <summary>True when all four quadrant tiles are assigned.</summary>
        public bool IsComplete =>
            quadrantTL != null && quadrantTR != null &&
            quadrantBL != null && quadrantBR != null;
    }
}