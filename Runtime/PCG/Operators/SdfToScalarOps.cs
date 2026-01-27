using Unity.Mathematics;

using Islands.PCG.Fields;
using Islands.PCG.Primitives;

namespace Islands.PCG.Operators
{
    /// <summary>
    /// Raster operations that write Signed Distance Fields (SDF) into an existing ScalarField2D.
    /// "Rasterize" here means: loop over every grid cell, sample the SDF at the cell center,
    /// and store the signed distance value in the scalar field.
    ///
    /// Convention:
    /// - Sample point p is at cell center: (x + 0.5, y + 0.5)
    /// - SDF is negative inside, 0 on boundary, positive outside (as implemented by Sdf2D).
    /// </summary>
    public static class SdfToScalarOps
    {
        public static void WriteCircleSdf(ref ScalarField2D dst, float2 center, float radius)
        {
            radius = math.max(radius, 1e-6f);

            int w = dst.Domain.Width;
            int h = dst.Domain.Height;

            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f;

                for (int x = 0; x < w; x++)
                {
                    float2 p = new float2(x + 0.5f, py);
                    float d = Sdf2D.Circle(p, center, radius);
                    dst.SetUnchecked(x, y, d);
                }
            }
        }

        public static void WriteBoxSdf(ref ScalarField2D dst, float2 center, float2 halfExtents)
        {
            halfExtents = math.max(halfExtents, new float2(1e-6f, 1e-6f));

            int w = dst.Domain.Width;
            int h = dst.Domain.Height;

            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f;

                for (int x = 0; x < w; x++)
                {
                    float2 p = new float2(x + 0.5f, py);
                    float d = Sdf2D.Box(p, center, halfExtents);
                    dst.SetUnchecked(x, y, d);
                }
            }
        }

        public static void WriteCapsuleSdf(ref ScalarField2D dst, float2 a, float2 b, float radius)
        {
            radius = math.max(radius, 1e-6f);

            int w = dst.Domain.Width;
            int h = dst.Domain.Height;

            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f;

                for (int x = 0; x < w; x++)
                {
                    float2 p = new float2(x + 0.5f, py);
                    float d = Sdf2D.Capsule(p, a, b, radius);
                    dst.SetUnchecked(x, y, d);
                }
            }
        }
    }
}
