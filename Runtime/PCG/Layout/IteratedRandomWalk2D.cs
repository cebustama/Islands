using System;
using Unity.Mathematics;

using Islands.PCG.Grids;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// D3 — IteratedRandomWalk2D (the actual strategy)
    ///
    /// Runs multiple random walks, optionally restarting on an existing carved (ON) cell,
    /// accumulating floor into a single MaskGrid2D.
    ///
    /// Determinism:
    /// - All randomness comes from the provided Unity.Mathematics.Random (seed-driven).
    /// - IMPORTANT parity rule: when iterations=1 and walkLengthMin==walkLengthMax and randomStartChance==0,
    ///   this method avoids consuming extra RNG so that the internal direction sequence inside SimpleRandomWalk2D
    ///   can align with the non-iterated (D2) path.
    /// </summary>
    public static class IteratedRandomWalk2D
    {
        /// <summary>
        /// Carves floor into <paramref name="dst"/> by executing <paramref name="iterations"/> walks.
        ///
        /// Per-iteration:
        /// - walkLength is either fixed (if min==max) or sampled uniformly in [min, max] inclusive.
        /// - start position:
        ///   - i==0: always uses <paramref name="start"/> (no RNG consumed for restart chance).
        ///   - i&gt;0: with probability <paramref name="randomStartChance"/> AND if there are any ON cells,
        ///           picks a random ON cell (uniform among set bits). Otherwise continues from previous end.
        ///
        /// Returns the final end position (the position returned by the last SimpleRandomWalk2D.Walk call).
        /// </summary>
        public static int2 Carve(
            ref MaskGrid2D dst,
            ref Random rng,
            int2 start,
            int iterations,
            int walkLengthMin,
            int walkLengthMax,
            int brushRadius,
            float randomStartChance = 0f,
            float skewX = 0f,
            float skewY = 0f,
            int maxRetries = 8)
        {
            // --------------------
            // Guards / validation
            // --------------------
            if (!dst.IsCreated)
                throw new InvalidOperationException("IteratedRandomWalk2D.Carve: dst mask is not allocated/created.");

            if (iterations <= 0)
                return start; // no-op, deterministic

            if (walkLengthMin < 0)
                throw new ArgumentOutOfRangeException(nameof(walkLengthMin), "walkLengthMin must be >= 0.");
            if (walkLengthMax < 0)
                throw new ArgumentOutOfRangeException(nameof(walkLengthMax), "walkLengthMax must be >= 0.");
            if (walkLengthMax < walkLengthMin)
                throw new ArgumentOutOfRangeException(nameof(walkLengthMax), "walkLengthMax must be >= walkLengthMin.");

            if (brushRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(brushRadius), "brushRadius must be >= 0.");

            if (maxRetries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must be >= 1.");

            // Clamp chance + skew defensively (keeps behavior predictable if caller passes junk).
            randomStartChance = math.clamp(randomStartChance, 0f, 1f);
            skewX = math.clamp(skewX, -1f, 1f);
            skewY = math.clamp(skewY, -1f, 1f);

            // Ensure start is in-bounds (defensive). We keep it simple: clamp to domain.
            start = ClampToDomain(dst.Domain, start);

            // --------------------
            // Core loop
            // --------------------
            int2 end = start;

            // Track whether the mask currently has any ON cell to avoid repeated O(words) scans unless needed.
            // Note: we still need dst.TryGetRandomSetBit(...) to do its own popcount to select uniformly.
            bool hasAnyOn = dst.CountOnes() > 0;

            for (int i = 0; i < iterations; i++)
            {
                // Choose length.
                // Parity rule: if min==max, DO NOT consume rng.
                int len = (walkLengthMin == walkLengthMax)
                    ? walkLengthMin
                    : rng.NextInt(walkLengthMin, walkLengthMax + 1);

                // Choose start for this iteration.
                int2 iterStart;

                if (i == 0)
                {
                    // Parity rule: for i==0 we do NOT roll restart chance (no extra rng).
                    iterStart = start;
                }
                else
                {
                    bool doRandomStart = false;

                    // Only roll chance if we might actually use it.
                    // (Also avoids rng.NextFloat when chance==0.)
                    if (randomStartChance > 0f && hasAnyOn)
                    {
                        doRandomStart = rng.NextFloat() < randomStartChance;
                    }

                    if (doRandomStart)
                    {
                        // Uniform among ON bits (deterministic).
                        if (dst.TryGetRandomSetBit(ref rng, out int2 picked))
                            iterStart = picked;
                        else
                            iterStart = end; // fallback (shouldn't happen if hasAnyOn is true)
                    }
                    else
                    {
                        iterStart = end;
                    }
                }

                // Run one walk, accumulate into dst.
                end = SimpleRandomWalk2D.Walk(
                    ref dst,
                    ref rng,
                    start: iterStart,
                    walkLength: len,
                    brushRadius: brushRadius,
                    skewX: skewX,
                    skewY: skewY,
                    maxRetries: maxRetries);

                // After the first walk, the mask necessarily has ON cells.
                hasAnyOn = true;
            }

            return end;
        }

        private static int2 ClampToDomain(in Islands.PCG.Core.GridDomain2D domain, int2 p)
        {
            int x = math.clamp(p.x, 0, domain.Width - 1);
            int y = math.clamp(p.y, 0, domain.Height - 1);
            return new int2(x, y);
        }
    }
}
