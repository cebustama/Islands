using System;
using Unity.Collections;
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout.Bsp
{
    /// <summary>
    /// Deterministic BSP partitioner for integer rectangles (pure layout, no carving).
    /// Produces a set of leaf rects by splitting a root rect using a seed-driven RNG.
    /// </summary>
    public static class BspPartition2D
    {
        /// <summary>
        /// Integer rectangle using [min, max) convention:
        /// x in [xMin, xMax), y in [yMin, yMax).
        /// </summary>
        public struct IntRect2D : IEquatable<IntRect2D>
        {
            public int xMin;
            public int yMin;
            public int xMax;
            public int yMax;

            public IntRect2D(int xMin, int yMin, int xMax, int yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }

            /// <summary>Width in cells (xMax - xMin).</summary>
            public int Width => xMax - xMin;

            /// <summary>Height in cells (yMax - yMin).</summary>
            public int Height => yMax - yMin;

            /// <summary>True if the rect is non-empty (xMax > xMin and yMax > yMin).</summary>
            public bool IsValid => xMax > xMin && yMax > yMin;

            /// <summary>Center cell (floor), useful for room-center connections.</summary>
            public int2 Center => new int2(xMin + (Width >> 1), yMin + (Height >> 1));

            public bool Equals(IntRect2D other) =>
                xMin == other.xMin && yMin == other.yMin && xMax == other.xMax && yMax == other.yMax;

            public override bool Equals(object obj) => obj is IntRect2D other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = xMin;
                    h = (h * 397) ^ yMin;
                    h = (h * 397) ^ xMax;
                    h = (h * 397) ^ yMax;
                    return h;
                }
            }

            public override string ToString() => $"[{xMin},{yMin}]..[{xMax},{yMax})";
        }

        /// <summary>
        /// BSP partition configuration.
        /// </summary>
        public struct BspPartitionConfig
        {
            /// <summary>Maximum number of successful split steps to attempt.</summary>
            public int splitIterations;

            /// <summary>
            /// Minimum allowed leaf size (in cells). A split is only allowed if both children remain >= minLeafSize.
            /// </summary>
            public int2 minLeafSize;
        }

        /// <summary>
        /// Returns an upper bound for the number of leaves in a full binary split tree after N splits.
        /// (Worst-case capacity requirement if every split succeeds.)
        /// </summary>
        public static int MaxLeavesUpperBound(int splitIterations)
        {
            // Worst case leaves: 2^splitIterations. Clamp exponent to avoid overflow.
            int s = math.clamp(splitIterations, 0, 30);
            return 1 << s;
        }

        /// <summary>
        /// Partitions a root rect into leaf rects using deterministic BSP splitting.
        /// Writes leaves into <paramref name="outLeaves"/> and returns the leaf count.
        /// </summary>
        /// <remarks>
        /// Determinism notes:
        /// - Uses only Unity.Mathematics.Random (passed by ref).
        /// - Selection of split candidates and split positions is RNG-driven.
        /// - If <paramref name="outLeaves"/> is too small, this method throws (avoids silent non-deterministic truncation).
        ///
        /// Safety notes:
        /// - Never produces children smaller than cfg.minLeafSize.
        /// - If no valid split exists (given minLeafSize), stops early.
        /// - Pure integer math; rects stay within the root rect.
        /// </remarks>
        /// <param name="root">Root rect to partition (typically the whole domain).</param>
        /// <param name="rng">Seed-driven RNG (passed by ref to advance deterministically).</param>
        /// <param name="cfg">Partition configuration.</param>
        /// <param name="outLeaves">Preallocated output array to receive leaf rects.</param>
        /// <returns>Number of leaves written into outLeaves.</returns>
        public static int PartitionLeaves(
            in IntRect2D root,
            ref Random rng,
            in BspPartitionConfig cfg,
            NativeArray<IntRect2D> outLeaves)
        {
            if (!outLeaves.IsCreated || outLeaves.Length <= 0)
                throw new ArgumentException("outLeaves must be created and non-empty.", nameof(outLeaves));

            if (!root.IsValid)
            {
                // Degenerate input: emit nothing useful but stay safe/deterministic.
                outLeaves[0] = root;
                return 1;
            }

            int2 minLeaf = cfg.minLeafSize;
            minLeaf.x = math.max(1, minLeaf.x);
            minLeaf.y = math.max(1, minLeaf.y);

            int splitIterations = math.max(0, cfg.splitIterations);

            // Enforce capacity upfront to avoid output changing depending on caller capacity.
            int requiredCap = MaxLeavesUpperBound(splitIterations);
            if (outLeaves.Length < requiredCap)
            {
                throw new ArgumentException(
                    $"outLeaves capacity too small for splitIterations={splitIterations}. " +
                    $"Need >= {requiredCap} (worst-case 2^N), got {outLeaves.Length}.",
                    nameof(outLeaves));
            }

            outLeaves[0] = root;
            int leafCount = 1;

            // For each split iteration, attempt to split one existing leaf.
            // If nothing can be split anymore (given minLeaf), stop early.
            for (int i = 0; i < splitIterations; i++)
            {
                bool splitDone = false;

                // Try up to leafCount times to find a splittable leaf this iteration.
                // This prevents getting stuck repeatedly choosing an unsplittable leaf.
                int attempts = leafCount;
                for (int a = 0; a < attempts; a++)
                {
                    int idx = rng.NextInt(0, leafCount);
                    IntRect2D candidate = outLeaves[idx];

                    if (TrySplit(candidate, ref rng, minLeaf, out IntRect2D left, out IntRect2D right))
                    {
                        outLeaves[idx] = left;
                        outLeaves[leafCount++] = right;
                        splitDone = true;
                        break;
                    }
                }

                if (!splitDone)
                    break;
            }

            return leafCount;
        }

        // -----------------------
        // Internal split utilities
        // -----------------------

        private static bool TrySplit(
            in IntRect2D rect,
            ref Random rng,
            int2 minLeafSize,
            out IntRect2D a,
            out IntRect2D b)
        {
            a = rect;
            b = rect;

            int w = rect.Width;
            int h = rect.Height;

            // If rect is already smaller than minimum leaf in either dimension, cannot split.
            if (w < minLeafSize.x || h < minLeafSize.y)
                return false;

            // Choose split orientation. Bias by aspect ratio using integer comparisons (stable).
            // If one dimension is significantly larger, split along that dimension.
            // Otherwise, choose randomly.
            bool splitVertical;
            // Threshold ~ 1.25 (5/4) without floats.
            if (w * 4 > h * 5) splitVertical = true;          // much wider => vertical split
            else if (h * 4 > w * 5) splitVertical = false;    // much taller => horizontal split
            else splitVertical = rng.NextBool();              // roughly square => random

            if (splitVertical)
            {
                // Need enough width to keep both children >= minLeafSize.x.
                if (w < (minLeafSize.x * 2))
                    return false;

                int minSplitX = rect.xMin + minLeafSize.x;
                int maxSplitX = rect.xMax - minLeafSize.x; // exclusive upper bound for split

                if (maxSplitX <= minSplitX)
                    return false;

                int splitX = rng.NextInt(minSplitX, maxSplitX);

                a = new IntRect2D(rect.xMin, rect.yMin, splitX, rect.yMax);
                b = new IntRect2D(splitX, rect.yMin, rect.xMax, rect.yMax);

                return a.Width >= minLeafSize.x && b.Width >= minLeafSize.x &&
                       a.Height >= minLeafSize.y && b.Height >= minLeafSize.y;
            }
            else
            {
                // Need enough height to keep both children >= minLeafSize.y.
                if (h < (minLeafSize.y * 2))
                    return false;

                int minSplitY = rect.yMin + minLeafSize.y;
                int maxSplitY = rect.yMax - minLeafSize.y; // exclusive upper bound for split

                if (maxSplitY <= minSplitY)
                    return false;

                int splitY = rng.NextInt(minSplitY, maxSplitY);

                a = new IntRect2D(rect.xMin, rect.yMin, rect.xMax, splitY);
                b = new IntRect2D(rect.xMin, splitY, rect.xMax, rect.yMax);

                return a.Width >= minLeafSize.x && b.Width >= minLeafSize.x &&
                       a.Height >= minLeafSize.y && b.Height >= minLeafSize.y;
            }
        }
    }
}
