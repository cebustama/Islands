using System;
using Islands.PCG.Layout.Maps;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Describes one layer group in a multi-layer tilemap layout for
    /// <see cref="TilemapAdapter2D.ApplyLayered"/>.
    ///
    /// Maps a priority table to a specific Unity <see cref="UnityEngine.Tilemaps.Tilemap"/>.
    /// Multiple groups stamp independently, enabling visual layering:
    ///   Base     (opaque)      — water and land ground tiles.
    ///   Overlay  (transparent) — decoration tiles (vegetation, hills, stairs).
    ///   Collider (invisible)   — physics collision cells.
    ///
    /// Groups with a null <see cref="Tilemap"/> or null <see cref="PriorityTable"/>
    /// are silently skipped by <see cref="TilemapAdapter2D.ApplyLayered"/>.
    ///
    /// Phase H5: initial implementation.
    /// </summary>
    [Serializable]
    public sealed class TilemapLayerGroup
    {
        /// <summary>
        /// Target Tilemap to stamp. Null = this group is silently skipped.
        /// </summary>
        public UnityEngine.Tilemaps.Tilemap Tilemap;

        /// <summary>
        /// Ordered priority table for this group (low → high priority).
        /// Last matching entry per cell wins.
        /// Null = this group is silently skipped. Empty = only FallbackTile is placed.
        /// </summary>
        public TilemapLayerEntry[] PriorityTable;

        /// <summary>
        /// Tile placed at cells where no priority entry matches.
        /// Null (default) = leave unmatched cells empty after ClearFirst.
        /// </summary>
        public TileBase FallbackTile;

        /// <summary>
        /// If true (default), calls <c>tilemap.ClearAllTiles()</c> before stamping.
        /// Set false to composite on top of existing content.
        /// </summary>
        public bool ClearFirst = true;

        /// <summary>
        /// If true, mirrors the Y coordinate: <c>tileY = (Height - 1 - y)</c>.
        /// Should match the value used for other groups to preserve alignment.
        /// </summary>
        public bool FlipY;
    }
}