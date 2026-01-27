using System;
using Islands.PCG.Fields;
using Islands.PCG.Grids;

namespace Islands.PCG.Operators
{
    /// <summary>
    /// Comparison modes for thresholding scalar fields into binary masks.
    /// </summary>
    public enum ThresholdMode
    {
        Greater,
        GreaterEqual,
        Less,
        LessEqual
    }

    /// <summary>
    /// Operators that convert scalar fields into binary masks.
    /// </summary>
    public static class ScalarToMaskOps
    {
        /// <summary>
        /// Converts a ScalarField2D into a MaskGrid2D by applying a threshold.
        ///
        /// If greaterEqual is true:
        ///     dst(x,y) = src(x,y) >= threshold
        /// else:
        ///     dst(x,y) = src(x,y) > threshold
        ///
        /// This is a deterministic, data-oriented building block used for:
        /// - heightmaps -> land/water
        /// - density fields -> caves/solids
        /// - SDF fields -> filled shapes (<= 0), walls bands, etc.
        /// </summary>
        /// <param name="src">Source scalar field.</param>
        /// <param name="dst">Destination mask grid. Must have the same domain as src.</param>
        /// <param name="threshold">Threshold value.</param>
        /// <param name="greaterEqual">If true uses >=, otherwise uses >.</param>
        public static void Threshold(
            in ScalarField2D src,
            ref MaskGrid2D dst,
            float threshold,
            bool greaterEqual = true
        )
        {
            // Keep existing API intact; delegate to the directional overload.
            ThresholdMode mode = greaterEqual ? ThresholdMode.GreaterEqual : ThresholdMode.Greater;
            Threshold(in src, ref dst, threshold, mode);
        }

        /// <summary>
        /// Converts a ScalarField2D into a MaskGrid2D by applying a threshold with explicit comparison direction.
        ///
        /// Typical SDF fill convention:
        ///     "inside" is distance <= 0  (negative = inside)
        /// So you usually want:
        ///     Threshold(src, ref dst, 0f, ThresholdMode.LessEqual)
        /// </summary>
        /// <param name="src">Source scalar field.</param>
        /// <param name="dst">Destination mask grid. Must have the same domain as src.</param>
        /// <param name="threshold">Threshold value.</param>
        /// <param name="mode">Comparison mode.</param>
        public static void Threshold(
            in ScalarField2D src,
            ref MaskGrid2D dst,
            float threshold,
            ThresholdMode mode
        )
        {
            EnsureCompatibleDomains(src, dst);

            int width = src.Domain.Width;
            int height = src.Domain.Height;

            // Deterministic: purely functional on src.Values.
            // Fast path: pick the comparator outside the hot loops.
            switch (mode)
            {
                case ThresholdMode.GreaterEqual:
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            dst.SetUnchecked(x, y, src.GetUnchecked(x, y) >= threshold);
                    break;

                case ThresholdMode.Greater:
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            dst.SetUnchecked(x, y, src.GetUnchecked(x, y) > threshold);
                    break;

                case ThresholdMode.LessEqual:
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            dst.SetUnchecked(x, y, src.GetUnchecked(x, y) <= threshold);
                    break;

                case ThresholdMode.Less:
                    for (int y = 0; y < height; y++)
                        for (int x = 0; x < width; x++)
                            dst.SetUnchecked(x, y, src.GetUnchecked(x, y) < threshold);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown ThresholdMode.");
            }
        }

        private static void EnsureCompatibleDomains(in ScalarField2D src, in MaskGrid2D dst)
        {
            // Both are structs; compare width/height explicitly.
            if (src.Domain.Width != dst.Domain.Width || src.Domain.Height != dst.Domain.Height)
            {
                throw new ArgumentException(
                    $"Domain mismatch: ScalarField2D is {src.Domain.Width}x{src.Domain.Height}, " +
                    $"MaskGrid2D is {dst.Domain.Width}x{dst.Domain.Height}."
                );
            }

            // Optional: catch usage errors early.
            if (!src.IsCreated)
            {
                throw new ArgumentException("ScalarField2D.Values is not created (field not allocated).");
            }

            if (!dst.IsCreated)
            {
                throw new ArgumentException("MaskGrid2D storage is not created (mask not allocated).");
            }
        }
    }
}
