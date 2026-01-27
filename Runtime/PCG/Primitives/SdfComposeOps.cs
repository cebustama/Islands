using Unity.Mathematics;

namespace Islands.PCG.Primitives
{
    /// <summary>
    /// Distance-space composition operations for Signed Distance Fields (SDF).
    ///
    /// These boolean-like ops act on continuous distances (negative inside, positive outside),
    /// and are applied BEFORE thresholding to a binary mask:
    /// - Union:     min(dA, dB)
    /// - Intersect: max(dA, dB)
    /// - Subtract:  max(dA, -dB)   (A without B)
    ///
    /// Smooth variants provide rounded blends (useful for organic caves / blobs / generative art).
    /// </summary>
    public static class SdfComposeOps
    {
        public static float Union(float dA, float dB) => math.min(dA, dB);
        public static float Intersect(float dA, float dB) => math.max(dA, dB);

        /// <summary>Subtract: A without B = max(dA, -dB)</summary>
        public static float Subtract(float dA, float dB) => math.max(dA, -dB);

        /// <summary>Smooth union (smooth-min). k is blend radius in distance units.</summary>
        public static float SmoothUnion(float dA, float dB, float k)
        {
            if (k <= 0f) return Union(dA, dB);
            float h = math.saturate(0.5f + 0.5f * (dB - dA) / k);
            return math.lerp(dB, dA, h) - k * h * (1f - h);
        }

        /// <summary>Smooth intersection (smooth-max). k is blend radius in distance units.</summary>
        public static float SmoothIntersect(float dA, float dB, float k)
        {
            if (k <= 0f) return Intersect(dA, dB);
            return -SmoothUnion(-dA, -dB, k);
        }

        /// <summary>Smooth subtract: A without B with rounded cut.</summary>
        public static float SmoothSubtract(float dA, float dB, float k)
        {
            if (k <= 0f) return Subtract(dA, dB);
            return SmoothIntersect(dA, -dB, k);
        }
    }
}
