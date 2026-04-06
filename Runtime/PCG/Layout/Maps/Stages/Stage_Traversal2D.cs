using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F6 — Traversal (Walkable + Stairs).
    ///
    /// Reads:
    /// - Land         (read-only)
    /// - ShallowWater (read-only)   [post-N2 Issue 3]
    /// - HillsL1      (read-only)
    /// - HillsL2      (read-only)
    ///
    /// Writes:
    /// - Walkable
    /// - Stairs
    ///
    /// Contracts:
    /// - Walkable = (Land OR ShallowWater) AND NOT HillsL2
    /// - Walkable ⊇ (Land AND NOT HillsL2)   — all previously-walkable cells remain walkable
    /// - Walkable ∩ HillsL2 == ∅
    /// - HillsL1 slopes are passable (included in Walkable); only HillsL2 peaks are excluded.
    /// - ShallowWater cells are walkable (player can wade).
    /// - Stairs = HillsL1 AND NOT HillsL2 AND (4-adjacent to ≥1 HillsL2 cell)
    /// - Stairs ⊆ HillsL1
    /// - Stairs ∩ HillsL2 == ∅
    /// - Stairs ⊆ Walkable (by construction: all Stairs satisfy Land AND NOT HillsL2)
    /// - Stairs may be empty if HillsL2 is empty; this is not a defect.
    /// - Does not mutate Land, ShallowWater, HillsL1, HillsL2, Vegetation, or Height.
    /// - Does not consume ctx.Rng (no noise, no randomness).
    /// - Does not write MapLayerId.Paths (deferred to Phase O).
    /// </summary>
    public sealed class Stage_Traversal2D : IMapStage2D
    {
        public string Name => "traversal";

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);
            ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
            ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);

            ref MaskGrid2D walkable = ref ctx.EnsureLayer(MapLayerId.Walkable, clearToZero: true);
            ref MaskGrid2D stairs = ref ctx.EnsureLayer(MapLayerId.Stairs, clearToZero: true);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isLand = land.GetUnchecked(x, y);
                    bool isShallowWater = shallowWater.GetUnchecked(x, y);
                    bool isHillsL2 = hillsL2.GetUnchecked(x, y);

                    // Walkable = (Land OR ShallowWater) AND NOT HillsL2
                    if ((isLand || isShallowWater) && !isHillsL2)
                        walkable.SetUnchecked(x, y, true);

                    // Stairs candidate: HillsL1 AND NOT HillsL2
                    if (!hillsL1.GetUnchecked(x, y) || isHillsL2)
                        continue;

                    // Must be 4-adjacent to at least one HillsL2 cell
                    if (HasHillsL2Neighbor(ref hillsL2, x, y, w, h))
                        stairs.SetUnchecked(x, y, true);
                }
            }
        }

        // Returns true if any cardinal neighbor of (x,y) is set in hillsL2.
        // Out-of-bounds neighbors are treated as OFF (consistent with 4-neighborhood contract).
        private static bool HasHillsL2Neighbor(
            ref MaskGrid2D hillsL2, int x, int y, int w, int h)
        {
            if (y > 0 && hillsL2.GetUnchecked(x, y - 1)) return true;
            if (y < h - 1 && hillsL2.GetUnchecked(x, y + 1)) return true;
            if (x > 0 && hillsL2.GetUnchecked(x - 1, y)) return true;
            if (x < w - 1 && hillsL2.GetUnchecked(x + 1, y)) return true;
            return false;
        }
    }
}