using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using System;

namespace Islands.PCG.Layout.Maps.Operators
{
    /// <summary>
    /// Area-quantile hill thresholds (F3b′ — hills window recalibration).
    ///
    /// Converts designer-facing AREA fractions into per-run Height-space thresholds
    /// via order statistics over the Land population:
    ///   hillsL1 = target fraction of Land cells with Height >= thL1 (slopes + peaks)
    ///   hillsL2 = target fraction of Land cells with Height >= thL2 (peaks)
    ///
    /// ── Design invariants ────────────────────────────────────────────────────
    ///   • Pure, static, deterministic. No RNG, no noise, no Unity API calls.
    ///     Same Height + Land + fractions ⇒ same thresholds.
    ///   • Selection is an exact order statistic on the sorted Land heights —
    ///     no histogram binning error.
    ///   • Tie rule: the threshold IS the value of the k-th-from-top cell and
    ///     classification is (Height >= th), so the whole tie class at the cut
    ///     is included. Realized fraction >= target, exceeded by at most the
    ///     size of that tie class (tie classes exist because Height is quantized
    ///     upstream; pow/spline are monotone and preserve them). No spatial
    ///     tie-breaking of any kind — the bias W-aux.e prohibited.
    ///   • f2 is clamped to f1 (peaks ⊆ hills budget), so thL2 >= thL1 by
    ///     construction: k2 &lt;= k1 ⇒ sorted[n−k2] >= sorted[n−k1].
    ///   • Empty band (k == 0: fraction 0, or no Land) returns
    ///     float.PositiveInfinity — (Height >= +inf) is false for every cell and
    ///     survives the N5.d blend offset arithmetic unchanged (+inf − x = +inf).
    ///   • Managed float[landCount] temporary, consistent with the
    ///     HeightFieldHydrologyOps2D precedent for stage-local buffers.
    /// </summary>
    public static class HillsThresholdOps2D
    {
        /// <summary>
        /// Runtime entry point (stage-side). Collects Land heights in row-major
        /// order, sorts ascending, selects both thresholds.
        /// </summary>
        public static void ComputeAreaThresholds(
            in ScalarField2D height, in MaskGrid2D land,
            float hillsL1AreaFrac, float hillsL2AreaFrac,
            out float thL1, out float thL2)
        {
            GridDomain2D d = height.Domain;
            int w = d.Width;
            int h = d.Height;

            // Pass 1: count Land cells.
            int n = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (land.GetUnchecked(x, y)) n++;

            // Pass 2: collect Land heights, row-major. Collection order is
            // irrelevant after sorting but fixed anyway for reproducibility of
            // the intermediate state.
            float[] vals = new float[n];
            int i = 0;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                    if (land.GetUnchecked(x, y))
                        vals[i++] = height.Values[row + x];
            }

            Array.Sort(vals);
            SelectFromSortedAscending(vals, n, hillsL1AreaFrac, hillsL2AreaFrac,
                out thL1, out thL2);
        }

        /// <summary>
        /// Diagnostics entry point (exporter-side managed arrays, e.g. the TEMP
        /// height probe). Funnels into the SAME selection core as the runtime
        /// path — one implementation, no mirror drift.
        /// </summary>
        public static void ComputeAreaThresholds(
            float[] height, bool[] land, int length,
            float hillsL1AreaFrac, float hillsL2AreaFrac,
            out float thL1, out float thL2)
        {
            int n = 0;
            for (int i = 0; i < length; i++)
                if (land[i]) n++;

            float[] vals = new float[n];
            int j = 0;
            for (int i = 0; i < length; i++)
                if (land[i]) vals[j++] = height[i];

            Array.Sort(vals);
            SelectFromSortedAscending(vals, n, hillsL1AreaFrac, hillsL2AreaFrac,
                out thL1, out thL2);
        }

        /// <summary>
        /// Order-statistic selection over an ascending-sorted population of n values.
        ///   k = round(frac · n) cells from the top; th = sortedAsc[n − k].
        ///   k == 0 → +inf (empty band). frac == 1 → th = min (whole population).
        /// </summary>
        private static void SelectFromSortedAscending(
            float[] sortedAsc, int n, float f1, float f2,
            out float thL1, out float thL2)
        {
            f1 = Clamp01(f1);
            f2 = Clamp01(f2);
            if (f2 > f1) f2 = f1;   // peaks ⊆ hills budget ⇒ thL2 >= thL1

            thL1 = SelectOne(sortedAsc, n, f1);
            thL2 = SelectOne(sortedAsc, n, f2);
        }

        private static float SelectOne(float[] sortedAsc, int n, float frac)
        {
            if (n <= 0) return float.PositiveInfinity;
            int k = (int)Math.Round(frac * (double)n, MidpointRounding.AwayFromZero);
            if (k <= 0) return float.PositiveInfinity;
            if (k >= n) return sortedAsc[0];
            return sortedAsc[n - k];
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}