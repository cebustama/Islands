using System.Collections.Generic;

using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Scans a <see cref="MapDataExport"/> for qualifying 2×2 blocks and produces
    /// a list of <see cref="MegaTilePlacement"/> values.
    ///
    /// Algorithm: greedy top-left row-major scan with strict 4/4 qualification.
    /// A cell is never claimed by more than one placement. Earlier rules (lower
    /// index in the rules array) take priority over later ones.
    ///
    /// Deterministic: same export + same rules ⇒ identical placement list.
    /// Read-only: does not modify the export.
    ///
    /// Phase H8.
    /// </summary>
    public static class MegaTileScanner
    {
        /// <summary>
        /// Scans the export for qualifying 2×2 mega-tile blocks.
        /// </summary>
        /// <param name="export">Pipeline export (read-only).</param>
        /// <param name="rules">Rules to evaluate in order. Earlier rules claim first.</param>
        /// <returns>List of placements in scan order. Empty if no blocks qualify.</returns>
        public static List<MegaTilePlacement> Scan(MapDataExport export, MegaTileRule[] rules)
        {
            var placements = new List<MegaTilePlacement>();
            if (export == null || rules == null || rules.Length == 0)
                return placements;

            int w = export.Width;
            int h = export.Height;
            if (w < 2 || h < 2)
                return placements;

            // Shared claimed array across all rules — a cell claimed by an earlier
            // rule is excluded from later rules.
            bool[] claimed = new bool[w * h];

            for (int ri = 0; ri < rules.Length; ri++)
            {
                var rule = rules[ri];
                if (!export.HasLayer(rule.targetLayer))
                    continue;

                bool[] layer = export.GetLayer(rule.targetLayer);
                ScanOneRule(layer, w, h, claimed, ri, placements);
            }

            return placements;
        }

        /// <summary>
        /// Single-rule convenience overload for testing.
        /// </summary>
        public static List<MegaTilePlacement> Scan(MapDataExport export, MapLayerId targetLayer)
        {
            var rules = new MegaTileRule[] { new MegaTileRule { targetLayer = targetLayer } };
            return Scan(export, rules);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Internal scan — greedy top-left row-major
        // ─────────────────────────────────────────────────────────────────────

        private static void ScanOneRule(
            bool[] layer, int w, int h, bool[] claimed, int ruleIndex,
            List<MegaTilePlacement> placements)
        {
            // Scan row-major: y outer (bottom to top), x inner (left to right).
            // Block origin (x, y) is the bottom-left cell.
            // The four cells of a 2×2 block:
            //   (x,   y+1) = TL    (x+1, y+1) = TR
            //   (x,   y  ) = BL    (x+1, y  ) = BR

            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    int bl = y * w + x;
                    int br = bl + 1;
                    int tl = (y + 1) * w + x;
                    int tr = tl + 1;

                    if (layer[bl] && layer[br] && layer[tl] && layer[tr]
                        && !claimed[bl] && !claimed[br] && !claimed[tl] && !claimed[tr])
                    {
                        placements.Add(new MegaTilePlacement(x, y, ruleIndex));
                        claimed[bl] = true;
                        claimed[br] = true;
                        claimed[tl] = true;
                        claimed[tr] = true;
                    }
                }
            }
        }
    }
}