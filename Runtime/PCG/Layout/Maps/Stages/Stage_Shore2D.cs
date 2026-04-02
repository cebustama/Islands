using Islands.PCG.Core;
using Islands.PCG.Grids;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F4 — Shore + ShallowWater.
    ///
    /// Reads:
    /// - Land (read-only)
    ///
    /// Writes:
    /// - ShallowWater
    ///
    /// Contracts:
    /// - ShallowWater = NOT Land AND (4-adjacent to at least one Land cell)
    /// - ShallowWater ⊆ NOT Land
    /// - ShallowWater ∩ Land == ∅
    /// - ShallowWater ∩ DeepWater is intentionally non-empty:
    ///   coastal cells adjacent to land are DeepWater (border-connected ocean) by F2
    ///   and ShallowWater (land-adjacent ring) by F4. The two layers are not redundant —
    ///   DeepWater is origin-based (flood fill from border), ShallowWater is proximity-based.
    /// - Ring thickness is exactly 1 cell (4-adjacent only). No tunable.
    /// - Does not mutate Land, DeepWater, or Height.
    /// - Does not consume ctx.Rng (no noise, no randomness in this stage).
    /// - Lake-edge cells adjacent to land are included in ShallowWater as a natural
    ///   side-effect. Lake detection and labeling as a distinct concept belongs to Phase L.
    /// </summary>
    public sealed class Stage_Shore2D : IMapStage2D
    {
        public string Name => "shore";

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D shallowWater = ref ctx.EnsureLayer(MapLayerId.ShallowWater, clearToZero: true);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Only water cells are candidates.
                    if (land.GetUnchecked(x, y))
                        continue;

                    // Mark if any cardinal neighbor is Land.
                    bool adjacentToLand =
                        (x > 0 && land.GetUnchecked(x - 1, y)) ||
                        (x + 1 < w && land.GetUnchecked(x + 1, y)) ||
                        (y > 0 && land.GetUnchecked(x, y - 1)) ||
                        (y + 1 < h && land.GetUnchecked(x, y + 1));

                    if (adjacentToLand)
                        shallowWater.SetUnchecked(x, y, true);
                }
            }
        }
    }
}