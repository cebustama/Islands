using System;
using Islands.PCG.Layout.Maps;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Static adapter: reads a <see cref="MapDataExport"/> and stamps tiles into a Unity
    /// <see cref="UnityEngine.Tilemaps.Tilemap"/>.
    ///
    /// Adapters-last invariant: read-only consumer of <see cref="MapDataExport"/>.
    /// Never writes to pipeline state. Deterministic: same export + same priority table
    /// produces identical tilemap output.
    ///
    /// Priority: entries in <paramref name="priorityTable"/> are evaluated low→high.
    /// The last entry whose layer is ON at a cell wins. This is rendering priority only;
    /// pipeline generation order is unaffected.
    ///
    /// Layers absent from the export are silently skipped (no exception).
    /// Entries with a null <see cref="TilemapLayerEntry.Tile"/> are also skipped.
    ///
    /// Coordinate mapping: cell (x, y) → <c>Vector3Int(x, y, 0)</c>.
    /// If the result appears vertically flipped, set <paramref name="flipY"/> = true
    /// to mirror: <c>tileY = (Height - 1 - y)</c>.
    /// </summary>
    public static class TilemapAdapter2D
    {
        /// <summary>
        /// Populates <paramref name="tilemap"/> from <paramref name="export"/> using the
        /// caller-supplied priority table.
        /// </summary>
        /// <param name="export">The map snapshot to read. Must not be null.</param>
        /// <param name="tilemap">Target Unity Tilemap. Must not be null.</param>
        /// <param name="priorityTable">
        /// Ordered entries: each maps a <see cref="MapLayerId"/> to a <see cref="TileBase"/>.
        /// Position 0 = lowest priority, last position = highest priority.
        /// Absent layers and null tiles are silently skipped.
        /// Must not be null (may be empty).
        /// </param>
        /// <param name="fallbackTile">
        /// Tile placed at cells where no priority entry matches.
        /// Null (default) = leave those cells untouched (or empty after clearFirst).
        /// </param>
        /// <param name="clearFirst">
        /// If true (default), calls <c>tilemap.ClearAllTiles()</c> before stamping.
        /// Set false to composite on top of existing tilemap content.
        /// </param>
        /// <param name="flipY">
        /// If true, mirrors the Y coordinate: <c>tileY = (Height - 1 - y)</c>.
        /// Use when the map renders upside down relative to the grid origin convention.
        /// Default: false.
        /// </param>
        public static void Apply(
            MapDataExport export,
            UnityEngine.Tilemaps.Tilemap tilemap,
            TilemapLayerEntry[] priorityTable,
            TileBase fallbackTile = null,
            bool clearFirst = true,
            bool flipY = false)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            if (tilemap == null) throw new ArgumentNullException(nameof(tilemap));
            if (priorityTable == null) throw new ArgumentNullException(nameof(priorityTable));

            if (clearFirst)
                tilemap.ClearAllTiles();

            int entryCount = priorityTable.Length;

            // Pre-fetch layer arrays once to avoid repeated dictionary/array lookups per cell.
            // Null slot = layer not in export or tile is null (both skip gracefully).
            bool[][] cachedLayers = new bool[entryCount][];
            TileBase[] cachedTiles = new TileBase[entryCount];
            for (int e = 0; e < entryCount; e++)
            {
                MapLayerId id = priorityTable[e].LayerId;
                TileBase tile = priorityTable[e].Tile;
                // Skip entries with null tile — they cannot contribute a winner.
                if (tile != null && export.HasLayer(id))
                    cachedLayers[e] = export.GetLayer(id);
                cachedTiles[e] = tile; // kept for reference but only used if layer is present
            }

            int width = export.Width;
            int height = export.Height;

            for (int y = 0; y < height; y++)
            {
                int tileY = flipY ? (height - 1 - y) : y;
                int rowBase = y * width;

                for (int x = 0; x < width; x++)
                {
                    int idx = rowBase + x;
                    TileBase winner = fallbackTile;

                    // Scan low→high priority; last match overwrites.
                    for (int e = 0; e < entryCount; e++)
                    {
                        bool[] layer = cachedLayers[e];
                        if (layer != null && layer[idx])
                            winner = cachedTiles[e];
                    }

                    // Only call SetTile when we have something to place.
                    // This avoids stamping null over existing content when clearFirst=false.
                    if (winner != null)
                        tilemap.SetTile(new Vector3Int(x, tileY, 0), winner);
                }
            }
        }
    }
}