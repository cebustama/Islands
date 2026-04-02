using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;
using Unity.Mathematics;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// Phase G — Morphology (LandCore + CoastDist).
    ///
    /// Reads:
    /// - Land         (read-only)
    /// - LandEdge     (read-only)
    /// - LandInterior (read-only, not written; used only for invariant documentation)
    ///
    /// Writes:
    /// - LandCore   (MapLayerId 11) — Land eroded by ErodeRadius cells
    /// - CoastDist  (MapFieldId  2) — BFS distance field from LandEdge inward through Land
    ///
    /// Contracts:
    /// - LandCore ⊆ Land
    /// - LandCore ⊆ LandInterior  (guaranteed when ErodeRadius >= 1)
    /// - CoastDist == 0f at all LandEdge cells
    /// - CoastDist >= 0f at all Land cells reachable within CoastDistMax steps
    /// - CoastDist == -1f at water cells and Land cells beyond CoastDistMax
    /// - Does not consume ctx.Rng (no noise, no randomness)
    /// - Does not mutate Land, LandEdge, LandInterior, HillsL1, HillsL2,
    ///   ShallowWater, Vegetation, Walkable, Stairs, or Height
    /// </summary>
    public sealed class Stage_Morphology2D : IMapStage2D
    {
        public string Name => "morphology";

        /// <summary>
        /// Number of 4-neighbor erosion passes applied to Land to produce LandCore.
        /// Default: 3. Must be >= 0 (0 = LandCore is a copy of Land).
        /// </summary>
        public readonly int ErodeRadius;

        /// <summary>
        /// Maximum BFS distance for the CoastDist field.
        /// Cells further than this receive -1f.
        /// 0 = auto: resolved at Execute time to math.min(w, h) / 2.
        /// </summary>
        public readonly int CoastDistMax;

        public Stage_Morphology2D(int erodeRadius = 3, int coastDistMax = 0)
        {
            ErodeRadius = erodeRadius < 0 ? 0 : erodeRadius;
            CoastDistMax = coastDistMax < 0 ? 0 : coastDistMax;
        }

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D landEdge = ref ctx.GetLayer(MapLayerId.LandEdge);

            ref MaskGrid2D landCore = ref ctx.EnsureLayer(MapLayerId.LandCore, clearToZero: true);
            ref ScalarField2D coastDist = ref ctx.EnsureField(MapFieldId.CoastDist, clearToZero: true);

            int maxDist = CoastDistMax > 0 ? CoastDistMax : math.min(w, h) / 2;

            // LandCore: Land eroded inward by ErodeRadius cells
            MaskMorphologyOps2D.Erode4(in land, ref landCore, ErodeRadius);

            // CoastDist: BFS from LandEdge cells, propagating through Land
            // Distance 0 at LandEdge; increases inland; -1f at water or beyond maxDist
            MaskMorphologyOps2D.BfsDistanceField(in landEdge, in land, ref coastDist, maxDist);
        }
    }
}