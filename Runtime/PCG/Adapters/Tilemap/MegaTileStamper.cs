using Islands.PCG.Layout.Maps;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Stamps mega-tile quadrant sprites onto a Unity <see cref="UnityEngine.Tilemaps.Tilemap"/>.
    ///
    /// Called AFTER <see cref="TilemapAdapter2D.Apply"/> (overwrite approach).
    /// The standard adapter stamps 1×1 tiles for all cells first; the stamper then
    /// overwrites claimed cells with the appropriate quadrant sub-sprites.
    ///
    /// Coordinate mapping: the scanner produces placements in pipeline coordinates
    /// (y=0 at bottom). The stamper maps to tilemap coordinates using the same
    /// flipY convention as TilemapAdapter2D.
    ///
    /// Quadrant naming convention (artist/visual perspective):
    ///   TL = top-left on screen, TR = top-right, BL = bottom-left, BR = bottom-right.
    ///
    /// In pipeline coordinates the block origin (x, y) is BL, (x, y+1) is TL.
    /// With flipY=true, visual top is at lower tilemap Y, so:
    ///   pipeline (x,   y+1) → tilemap (x,   H-2-y)  = visual TL
    ///   pipeline (x+1, y+1) → tilemap (x+1, H-2-y)  = visual TR
    ///   pipeline (x,   y  ) → tilemap (x,   H-1-y)  = visual BL
    ///   pipeline (x+1, y  ) → tilemap (x+1, H-1-y)  = visual BR
    ///
    /// With flipY=false, pipeline coords map directly:
    ///   pipeline (x,   y+1) → tilemap (x,   y+1) = visual TL
    ///   pipeline (x+1, y+1) → tilemap (x+1, y+1) = visual TR
    ///   pipeline (x,   y  ) → tilemap (x,   y  ) = visual BL
    ///   pipeline (x+1, y  ) → tilemap (x+1, y  ) = visual BR
    ///
    /// Phase H8.
    /// </summary>
    public static class MegaTileStamper
    {
        /// <summary>
        /// Stamps all placements onto the target tilemap.
        /// </summary>
        /// <param name="tilemap">Target tilemap to overwrite cells on.</param>
        /// <param name="placements">Scan result from <see cref="MegaTileScanner.Scan"/>.</param>
        /// <param name="rules">The same rule array used for scanning.</param>
        /// <param name="height">Pipeline grid height (for flipY conversion).</param>
        /// <param name="flipY">Whether to mirror Y axis (must match the adapter).</param>
        public static void Apply(
            UnityEngine.Tilemaps.Tilemap tilemap,
            List<MegaTilePlacement> placements,
            MegaTileRule[] rules,
            int height,
            bool flipY)
        {
            if (tilemap == null || placements == null || rules == null)
                return;

            for (int i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                if (p.RuleIndex < 0 || p.RuleIndex >= rules.Length)
                    continue;

                var rule = rules[p.RuleIndex];
                if (!rule.IsComplete)
                    continue;

                int x = p.X;
                int y = p.Y;

                if (flipY)
                {
                    // Pipeline (x,   y+1) is top row in pipeline → visual top-left
                    // flipY: tileY = height - 1 - pipelineY
                    int tileTL_Y = height - 1 - (y + 1); // = height - 2 - y
                    int tileBL_Y = height - 1 - y;

                    tilemap.SetTile(new Vector3Int(x, tileTL_Y, 0), rule.quadrantTL);
                    tilemap.SetTile(new Vector3Int(x + 1, tileTL_Y, 0), rule.quadrantTR);
                    tilemap.SetTile(new Vector3Int(x, tileBL_Y, 0), rule.quadrantBL);
                    tilemap.SetTile(new Vector3Int(x + 1, tileBL_Y, 0), rule.quadrantBR);
                }
                else
                {
                    // No flip — pipeline top is tilemap top.
                    tilemap.SetTile(new Vector3Int(x, y + 1, 0), rule.quadrantTL);
                    tilemap.SetTile(new Vector3Int(x + 1, y + 1, 0), rule.quadrantTR);
                    tilemap.SetTile(new Vector3Int(x, y, 0), rule.quadrantBL);
                    tilemap.SetTile(new Vector3Int(x + 1, y, 0), rule.quadrantBR);
                }
            }
        }

        /// <summary>
        /// Stamps onto a specific tilemap layer (overlay or base).
        /// If the rule's target layer is in the overlay set, stamp on the overlay tilemap;
        /// otherwise stamp on the base tilemap. This overload handles the routing.
        /// </summary>
        public static void ApplyMultiLayer(
            UnityEngine.Tilemaps.Tilemap baseTilemap,
            UnityEngine.Tilemaps.Tilemap overlayTilemap,
            List<MegaTilePlacement> placements,
            MegaTileRule[] rules,
            int height,
            bool flipY)
        {
            if (placements == null || rules == null)
                return;

            // HillsL1, HillsL2, Vegetation, Stairs are overlay layers.
            // Everything else goes to base.
            for (int i = 0; i < placements.Count; i++)
            {
                var p = placements[i];
                if (p.RuleIndex < 0 || p.RuleIndex >= rules.Length)
                    continue;

                var rule = rules[p.RuleIndex];
                var target = IsOverlayLayer(rule.targetLayer) && overlayTilemap != null
                    ? overlayTilemap
                    : baseTilemap;

                if (target == null || !rule.IsComplete)
                    continue;

                int x = p.X;
                int y = p.Y;

                if (flipY)
                {
                    int tileTL_Y = height - 2 - y;
                    int tileBL_Y = height - 1 - y;
                    target.SetTile(new Vector3Int(x, tileTL_Y, 0), rule.quadrantTL);
                    target.SetTile(new Vector3Int(x + 1, tileTL_Y, 0), rule.quadrantTR);
                    target.SetTile(new Vector3Int(x, tileBL_Y, 0), rule.quadrantBL);
                    target.SetTile(new Vector3Int(x + 1, tileBL_Y, 0), rule.quadrantBR);
                }
                else
                {
                    target.SetTile(new Vector3Int(x, y + 1, 0), rule.quadrantTL);
                    target.SetTile(new Vector3Int(x + 1, y + 1, 0), rule.quadrantTR);
                    target.SetTile(new Vector3Int(x, y, 0), rule.quadrantBL);
                    target.SetTile(new Vector3Int(x + 1, y, 0), rule.quadrantBR);
                }
            }
        }

        private static bool IsOverlayLayer(MapLayerId id)
        {
            return id == MapLayerId.Vegetation
                || id == MapLayerId.HillsL1
                || id == MapLayerId.HillsL2
                || id == MapLayerId.Stairs;
        }
    }
}