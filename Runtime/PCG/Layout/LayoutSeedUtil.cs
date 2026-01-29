using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// Small utility to standardize "int seed -> Unity.Mathematics.Random" creation.
    /// Ensures seed is always valid (>= 1).
    /// </summary>
    public static class LayoutSeedUtil
    {
        /// <summary>
        /// Creates a deterministic RNG from an int seed (clamped to >= 1).
        /// </summary>
        public static Random CreateRng(int seed)
        {
            uint s = (uint)math.max(seed, 1);
            return new Random(s);
        }
    }
}
