using Unity.Mathematics;

using Islands.PCG.Fields;
using Islands.PCG.Primitives;

namespace Islands.PCG.Operators
{
    /// <summary>
    /// How to combine two Signed Distance Fields (SDF) in distance-space.
    /// These mirror boolean-like operations but operate on continuous distances:
    /// - Union:    min(dA, dB)
    /// - Intersect:max(dA, dB)
    /// - Subtract: max(dA, -dB)  (A without B)
    /// </summary>
    public enum SdfCombineMode
    {
        Union,
        Intersect,
        Subtract
    }

    /// <summary>
    /// Raster operations that write composed SDFs into an existing ScalarField2D.
    /// This is the glue between continuous SDF math (Sdf2D + SdfComposeOps) and grid data (ScalarField2D).
    /// </summary>
    public static class SdfComposeRasterOps
    {
        /// <summary>
        /// Writes a composed SDF into <paramref name="dst"/> by combining a Circle and a Box per cell.
        ///
        /// Conventions:
        /// - Sampling is done at cell centers: p = (x + 0.5, y + 0.5) in grid units.
        /// - Signed distance: negative inside, 0 on boundary, positive outside.
        /// </summary>
        /// <param name="dst">Destination scalar field (grid units).</param>
        /// <param name="circleCenter">Circle center in grid units.</param>
        /// <param name="circleRadius">Circle radius in grid units.</param>
        /// <param name="boxCenter">Box center in grid units.</param>
        /// <param name="boxHalfExtents">Box half-extents in grid units.</param>
        /// <param name="mode">Combine mode (union / intersect / subtract).</param>
        /// <param name="invertDistance">If true, writes -dOut (swap inside/outside).</param>
        public static void WriteCircleBoxCompositeSdf(
            ref ScalarField2D dst,
            float2 circleCenter,
            float circleRadius,
            float2 boxCenter,
            float2 boxHalfExtents,
            SdfCombineMode mode,
            bool invertDistance = false
        )
        {
            int width = dst.Domain.Width;
            int height = dst.Domain.Height;

            for (int y = 0; y < height; y++)
            {
                float py = y + 0.5f;
                for (int x = 0; x < width; x++)
                {
                    float2 p = new float2(x + 0.5f, py);

                    float dCircle = Sdf2D.Circle(p, circleCenter, circleRadius);
                    float dBox = Sdf2D.Box(p, boxCenter, boxHalfExtents);

                    float dOut;
                    switch (mode)
                    {
                        case SdfCombineMode.Union:
                            dOut = SdfComposeOps.Union(dCircle, dBox);
                            break;

                        case SdfCombineMode.Intersect:
                            dOut = SdfComposeOps.Intersect(dCircle, dBox);
                            break;

                        case SdfCombineMode.Subtract:
                            dOut = SdfComposeOps.Subtract(dCircle, dBox);
                            break;

                        default:
                            dOut = dCircle;
                            break;
                    }

                    if (invertDistance) dOut = -dOut;

                    dst.SetUnchecked(x, y, dOut);
                }
            }
        }
    }
}
