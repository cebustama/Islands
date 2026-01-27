using Unity.Mathematics;

namespace Islands.PCG.Primitives
{
    /// <summary>
    /// 2D Signed Distance Function (SDF) primitives (pure math).
    ///
    /// Convention:
    /// - Signed distance: negative inside, 0 on boundary, positive outside.
    /// - All functions are deterministic and allocation-free.
    ///
    /// Notes:
    /// - Segment() returns an unsigned distance to a line segment (>= 0).
    /// - Capsule() is signed: distance to segment minus radius (negative inside the capsule).
    /// </summary>
    public static class Sdf2D
    {
        /// <summary>
        /// Signed distance to a circle.
        /// d = length(p - center) - radius
        /// </summary>
        public static float Circle(float2 p, float2 center, float radius)
        {
            return math.length(p - center) - radius;
        }

        /// <summary>
        /// Signed distance to an axis-aligned box (rectangle) centered at 'center'
        /// with half extents 'halfExtents' (hx, hy).
        ///
        /// Negative inside, 0 on edges, positive outside.
        /// </summary>
        public static float Box(float2 p, float2 center, float2 halfExtents)
        {
            float2 d = math.abs(p - center) - halfExtents;

            // Outside distance (Euclidean)
            float outside = math.length(math.max(d, 0f));

            // Inside distance (negative or zero)
            float inside = math.min(math.max(d.x, d.y), 0f);

            return outside + inside;
        }

        /// <summary>
        /// Unsigned distance from point p to the line segment [a, b].
        /// Always >= 0.
        /// </summary>
        public static float Segment(float2 p, float2 a, float2 b)
        {
            float2 ab = b - a;
            float abLenSq = math.dot(ab, ab);

            // Degenerate segment (a == b)
            if (abLenSq <= 1e-12f)
                return math.length(p - a);

            float t = math.dot(p - a, ab) / abLenSq;
            t = math.clamp(t, 0f, 1f);

            float2 closest = a + t * ab;
            return math.length(p - closest);
        }

        /// <summary>
        /// Signed distance to a capsule defined by segment [a, b] with radius 'radius'.
        /// Negative inside the capsule.
        /// </summary>
        public static float Capsule(float2 p, float2 a, float2 b, float radius)
        {
            return Segment(p, a, b) - radius;
        }
    }
}
