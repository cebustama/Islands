using Unity.Mathematics;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F3b — Height-Coherent Hills + boundary topology.
    ///
    /// Reads:
    /// - Land      (MapLayerId 0)   — eligibility mask
    /// - Height    (MapFieldId 0)   — elevation source for threshold classification
    ///
    /// Writes:
    /// - HillsL1       (MapLayerId 7)  — passable slopes: Land AND Height >= thL1 AND NOT HillsL2
    /// - HillsL2       (MapLayerId 8)  — impassable peaks: Land AND Height >= thL2
    /// - LandEdge      (MapLayerId 9)  — Land cells 4-adjacent to any non-Land cell
    /// - LandInterior  (MapLayerId 10) — Land AND NOT LandEdge
    ///
    /// Contracts:
    /// - HillsL2 ⊆ Land
    /// - HillsL1 ⊆ Land
    /// - HillsL1 ∩ HillsL2 == ∅
    /// - LandEdge ∪ LandInterior == Land
    /// - LandEdge ∩ LandInterior == ∅
    /// - Does not mutate Land, DeepWater, or Height.
    /// - Does not consume ctx.Rng (no noise, no randomness).
    ///
    /// Phase F3b replaces the original topology-based hills (noise threshold on LandInterior)
    /// with height-threshold classification. Hills now correlate spatially with the Height
    /// field — peaks appear where terrain is highest. Thresholds are read from
    /// <see cref="MapTunables2D.hillsThresholdL1"/> and <see cref="MapTunables2D.hillsThresholdL2"/>.
    ///
    /// LandEdge / LandInterior derivation is unchanged (delegated to
    /// <see cref="MaskTopologyOps2D.ExtractEdgeAndInterior4"/>).
    /// </summary>
    public sealed class Stage_Hills2D : IMapStage2D
    {
        public string Name => "hills";

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);

            ref MaskGrid2D landEdge = ref ctx.EnsureLayer(MapLayerId.LandEdge, clearToZero: true);
            ref MaskGrid2D landInterior = ref ctx.EnsureLayer(MapLayerId.LandInterior, clearToZero: true);
            ref MaskGrid2D hillsL1 = ref ctx.EnsureLayer(MapLayerId.HillsL1, clearToZero: true);
            ref MaskGrid2D hillsL2 = ref ctx.EnsureLayer(MapLayerId.HillsL2, clearToZero: true);

            // LandEdge / LandInterior: unchanged 4-neighbor boundary detection.
            MaskTopologyOps2D.ExtractEdgeAndInterior4(in land, ref landEdge, ref landInterior);

            // Height-threshold classification.
            // Thresholds are constructor-validated in MapTunables2D (clamped, L2 >= L1).
            float thL1 = inputs.Tunables.hillsThresholdL1;
            float thL2 = inputs.Tunables.hillsThresholdL2;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (!land.GetUnchecked(x, y))
                        continue;

                    float hv = height.Values[row + x];

                    if (hv >= thL2)
                        hillsL2.SetUnchecked(x, y, true);
                    else if (hv >= thL1)
                        hillsL1.SetUnchecked(x, y, true);
                }
            }
        }
    }
}