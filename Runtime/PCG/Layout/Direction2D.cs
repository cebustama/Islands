using Unity.Mathematics;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// Deterministic 2D direction utilities for grid-based algorithms.
    /// Provides cardinal direction sets and biased cardinal picking compatible with
    /// legacy "skewX/skewY" semantics (axis choice 50/50, then sign biased by skew).
    /// </summary>
    public static class Direction2D
    {
        /// <summary>
        /// Cardinal (4-neighborhood) directions in the order: +X, -X, +Y, -Y.
        /// </summary>
        public static readonly int2[] Cardinal =
        {
            new int2( 1,  0),
            new int2(-1,  0),
            new int2( 0,  1),
            new int2( 0, -1),
        };

        /// <summary>
        /// Picks a uniformly random cardinal direction using the provided deterministic RNG.
        /// </summary>
        /// <param name="rng">Seed-driven RNG (passed by ref to advance state deterministically).</param>
        /// <returns>A cardinal direction: (±1,0) or (0,±1).</returns>
        public static int2 PickCardinal(ref Random rng)
        {
            // NextInt(min, max) is min inclusive, max exclusive.
            int idx = rng.NextInt(0, 4);
            return Cardinal[idx];
        }

        /// <summary>
        /// Picks a biased cardinal direction replicating legacy skew semantics:
        /// 1) Choose axis horizontally/vertically with 50/50 probability.
        /// 2) Within the chosen axis, pick sign with probability:
        ///    rightChance = 0.5 + 0.5*skewX, upChance = 0.5 + 0.5*skewY.
        /// Uses only <see cref="Random.NextFloat"/> (no UnityEngine.Random).
        /// </summary>
        /// <param name="rng">Seed-driven RNG (passed by ref to advance state deterministically).</param>
        /// <param name="skewX">
        /// Horizontal bias in [-1, +1]. +1 strongly favors (+1,0), -1 strongly favors (-1,0).
        /// Values outside the range are clamped.
        /// </param>
        /// <param name="skewY">
        /// Vertical bias in [-1, +1]. +1 strongly favors (0,+1), -1 strongly favors (0,-1).
        /// Values outside the range are clamped.
        /// </param>
        /// <returns>A biased cardinal direction according to the legacy skew model.</returns>
        public static int2 PickSkewedCardinal(ref Random rng, float skewX, float skewY)
        {
            // Clamp skew to keep intent stable even if callers feed out-of-range values.
            skewX = math.clamp(skewX, -1f, 1f);
            skewY = math.clamp(skewY, -1f, 1f);

            // Axis selection: 50/50. (Legacy used < 0.5 style.)
            bool horizontal = rng.NextFloat() < 0.5f;

            if (horizontal)
            {
                float rightChance = math.saturate(0.5f + 0.5f * skewX);
                // Legacy semantics: compare using <=
                return (rng.NextFloat() <= rightChance) ? new int2(1, 0) : new int2(-1, 0);
            }
            else
            {
                float upChance = math.saturate(0.5f + 0.5f * skewY);
                // Legacy semantics: compare using <=
                return (rng.NextFloat() <= upChance) ? new int2(0, 1) : new int2(0, -1);
            }
        }
    }
}
