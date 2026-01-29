using Unity.Mathematics;
using Islands.PCG.Grids;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// OOB-safe neighbor queries for MaskGrid2D (cardinal / 4-neighborhood).
    /// Out-of-bounds neighbors are treated as OFF.
    /// </summary>
    public static class MaskNeighborOps2D
    {
        /// <summary>
        /// Counts ON neighbors in the 4-neighborhood (W/E/S/N). OOB counts as OFF.
        /// If p is OOB, returns 0.
        /// </summary>
        public static int CountCardinalOn(in MaskGrid2D mask, int2 p)
        {
            int w = mask.Domain.Width;
            int h = mask.Domain.Height;

            int x = p.x;
            int y = p.y;

            if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                return 0;

            int c = 0;

            // W
            if ((uint)(x - 1) < (uint)w && mask.GetUnchecked(x - 1, y)) c++;
            // E
            if ((uint)(x + 1) < (uint)w && mask.GetUnchecked(x + 1, y)) c++;
            // S
            if ((uint)(y - 1) < (uint)h && mask.GetUnchecked(x, y - 1)) c++;
            // N
            if ((uint)(y + 1) < (uint)h && mask.GetUnchecked(x, y + 1)) c++;

            return c;
        }

        /// <summary>
        /// Dead-end predicate in 4-neighborhood:
        /// - p must be ON
        /// - exactly 1 cardinal neighbor is ON
        /// Safe: returns false if p is OOB.
        /// </summary>
        public static bool IsDeadEnd4(in MaskGrid2D mask, int2 p)
        {
            int w = mask.Domain.Width;
            int h = mask.Domain.Height;

            if ((uint)p.x >= (uint)w || (uint)p.y >= (uint)h)
                return false;

            if (!mask.GetUnchecked(p.x, p.y))
                return false;

            return CountCardinalOn(mask, p) == 1;
        }
    }
}
