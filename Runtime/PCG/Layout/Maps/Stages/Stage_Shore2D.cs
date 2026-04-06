using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F4 — Shore + ShallowWater + MidWater.
    ///
    /// Reads:
    /// - Land   (read-only)
    /// - Height (read-only; only when ShallowWaterDepth01 > 0 or MidWaterDepth01 > 0)
    ///
    /// Writes:
    /// - ShallowWater
    /// - MidWater (only when MidWaterDepth01 > 0, F4c)
    ///
    /// Contracts:
    /// - ShallowWater ⊆ NOT Land
    /// - ShallowWater ∩ Land == ∅
    /// - MidWater ⊆ NOT Land
    /// - MidWater ∩ Land == ∅
    /// - MidWater ∩ ShallowWater == ∅
    /// - ShallowWater ∩ DeepWater is intentionally non-empty
    /// - Does not mutate Land, DeepWater, or Height.
    /// - Does not consume ctx.Rng (no noise, no randomness in this stage).
    ///
    /// ShallowWater classification:
    ///   ShallowWaterDepth01 == 0: adjacency-only (1-cell ring, original F4).
    ///   ShallowWaterDepth01 > 0:  adjacency ring + height band (F4b).
    ///
    /// MidWater classification (F4c):
    ///   MidWaterDepth01 == 0: no MidWater layer (default; not allocated).
    ///   MidWaterDepth01 > 0:  NOT Land AND NOT ShallowWater AND
    ///                         Height >= waterThreshold - MidWaterDepth01.
    ///   Must be > ShallowWaterDepth01 for a visible band to appear.
    ///
    /// Phase F4: initial implementation (adjacency only).
    /// Phase F4b: ShallowWaterDepth01 height-based extension.
    /// Phase F4c: MidWaterDepth01 intermediate water layer.
    /// </summary>
    public sealed class Stage_Shore2D : IMapStage2D
    {
        public string Name => "shore";

        /// <summary>
        /// Height band below waterThreshold that qualifies as shallow water.
        /// 0.0 = adjacency-only (original F4 behavior).
        /// </summary>
        public float ShallowWaterDepth01;

        /// <summary>
        /// Height band below waterThreshold that qualifies as mid-depth water (F4c).
        /// 0.0 = no MidWater layer (default; layer is not allocated).
        /// Must be > ShallowWaterDepth01 for a visible band to appear.
        /// </summary>
        public float MidWaterDepth01;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D shallowWater = ref ctx.EnsureLayer(MapLayerId.ShallowWater, clearToZero: true);

            bool useShallowDepth = ShallowWaterDepth01 > 0f;
            bool useMidDepth = MidWaterDepth01 > 0f;
            bool useDepth = useShallowDepth || useMidDepth;

            float shallowThreshold = 0f;
            float midThreshold = 0f;
            ScalarField2D height = default;

            if (useDepth)
            {
                height = ctx.GetField(MapFieldId.Height);
                float wt = inputs.Tunables.waterThreshold01;
                shallowThreshold = wt - ShallowWaterDepth01;
                midThreshold = wt - MidWaterDepth01;
            }

            // ---- Pass 1: classify ShallowWater ----
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (land.GetUnchecked(x, y))
                        continue;

                    bool adjacentToLand =
                        (x > 0 && land.GetUnchecked(x - 1, y)) ||
                        (x + 1 < w && land.GetUnchecked(x + 1, y)) ||
                        (y > 0 && land.GetUnchecked(x, y - 1)) ||
                        (y + 1 < h && land.GetUnchecked(x, y + 1));

                    if (adjacentToLand)
                    {
                        shallowWater.SetUnchecked(x, y, true);
                        continue;
                    }

                    if (useShallowDepth && height.GetUnchecked(x, y) >= shallowThreshold)
                    {
                        shallowWater.SetUnchecked(x, y, true);
                    }
                }
            }

            // ---- Pass 2: classify MidWater (F4c) ----
            if (!useMidDepth)
                return;

            ref MaskGrid2D midWater = ref ctx.EnsureLayer(MapLayerId.MidWater, clearToZero: true);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (land.GetUnchecked(x, y))
                        continue;
                    if (shallowWater.GetUnchecked(x, y))
                        continue;

                    if (height.GetUnchecked(x, y) >= midThreshold)
                    {
                        midWater.SetUnchecked(x, y, true);
                    }
                }
            }
        }
    }
}