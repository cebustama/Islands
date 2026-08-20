using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps.Operators;
using NUnit.Framework;
using System;
using Unity.Collections;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Operator-level unit tests for <see cref="HillsThresholdOps2D"/> (F3b′).
    ///
    /// Uses small synthetic populations with known order statistics. All tests
    /// are deterministic — no seeds, no noise, no pipeline.
    /// </summary>
    public sealed class HillsThresholdOps2DTests
    {
        // =====================================================================
        // Helpers
        // =====================================================================

        private static int CountAtOrAbove(float[] values, bool[] land, float th)
        {
            int c = 0;
            for (int i = 0; i < values.Length; i++)
                if (land[i] && values[i] >= th) c++;
            return c;
        }

        private static bool[] AllLand(int n)
        {
            var m = new bool[n];
            for (int i = 0; i < n; i++) m[i] = true;
            return m;
        }

        // =====================================================================
        // Exact fractions (no ties at the cut)
        // =====================================================================

        [Test]
        public void DistinctValues_RealizedFractionIsExact()
        {
            // 10 distinct values. f2 = 0.3 → k = 3 → th = 3rd from top = 8.0.
            float[] vals = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
            bool[] land = AllLand(10);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 10, 0.6f, 0.3f, out float thL1, out float thL2);

            Assert.AreEqual(8f, thL2, 1e-6f, "k=3 from top of 1..10 selects 8.");
            Assert.AreEqual(3, CountAtOrAbove(vals, land, thL2),
                "Realized L2 band must be exactly 3 cells (30%).");
            Assert.AreEqual(5f, thL1, 1e-6f, "k=6 from top of 1..10 selects 5.");
            Assert.AreEqual(6, CountAtOrAbove(vals, land, thL1),
                "Realized L1 band must be exactly 6 cells (60%).");
        }

        // =====================================================================
        // Tie rule: whole tie class enters; realized >= target
        // =====================================================================

        [Test]
        public void TieClassAtCut_IncludedWhole_RealizedAtLeastTarget()
        {
            // f = 0.4 over n = 10 → k = 4 → th = sorted[6] = 5.0, which sits
            // inside a 4-wide tie class. All four 5.0s must enter: realized 7/10.
            float[] vals = { 1f, 2f, 3f, 5f, 5f, 5f, 5f, 9f, 10f, 10f };
            bool[] land = AllLand(10);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 10, 1.0f, 0.4f, out _, out float thL2);

            Assert.AreEqual(5f, thL2, 1e-6f);
            int realized = CountAtOrAbove(vals, land, thL2);
            Assert.AreEqual(7, realized,
                "Whole tie class at the cut enters the band (no spatial tie-breaking).");
            Assert.GreaterOrEqual(realized, 4, "Realized fraction must be >= target.");
        }

        // =====================================================================
        // Ordering and clamping
        // =====================================================================

        [Test]
        public void F2GreaterThanF1_ClampedToF1_ThresholdsEqual()
        {
            float[] vals = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
            bool[] land = AllLand(10);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 10, 0.3f, 0.9f, out float thL1, out float thL2);

            Assert.AreEqual(thL1, thL2, 0f,
                "f2 > f1 must clamp to f1 (peaks ⊆ hills budget).");
            Assert.GreaterOrEqual(thL2, thL1);
        }

        [Test]
        public void ThL2_AlwaysGreaterOrEqualThL1()
        {
            float[] vals = { 3f, 3f, 3f, 7f, 7f, 9f };
            bool[] land = AllLand(6);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 6, 0.8f, 0.2f, out float thL1, out float thL2);

            Assert.GreaterOrEqual(thL2, thL1);
        }

        // =====================================================================
        // Sentinels
        // =====================================================================

        [Test]
        public void ZeroFraction_EmptyBand_PositiveInfinity()
        {
            float[] vals = { 1f, 2f, 3f, 4f };
            bool[] land = AllLand(4);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 4, 0.5f, 0f, out _, out float thL2);

            Assert.IsTrue(float.IsPositiveInfinity(thL2),
                "fraction 0 must yield +inf (Height >= +inf is false everywhere).");
            Assert.AreEqual(0, CountAtOrAbove(vals, land, thL2));
        }

        [Test]
        public void FractionOne_WholePopulation()
        {
            float[] vals = { 4f, 1f, 3f, 2f };
            bool[] land = AllLand(4);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 4, 1f, 1f, out float thL1, out float thL2);

            Assert.AreEqual(1f, thL1, 1e-6f, "fraction 1 selects the minimum.");
            Assert.AreEqual(4, CountAtOrAbove(vals, land, thL2));
        }

        [Test]
        public void NoLand_BothPositiveInfinity()
        {
            float[] vals = { 1f, 2f, 3f, 4f };
            bool[] land = new bool[4]; // all water

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 4, 0.5f, 0.2f, out float thL1, out float thL2);

            Assert.IsTrue(float.IsPositiveInfinity(thL1));
            Assert.IsTrue(float.IsPositiveInfinity(thL2));
        }

        // =====================================================================
        // Water cells excluded from the population
        // =====================================================================

        [Test]
        public void WaterCells_ExcludedFromQuantile()
        {
            // Land population is {6,7,8,9,10} (n=5). f2 = 0.4 → k = 2 → th = 9.
            // Water values (1..5) must not shift the cut.
            float[] vals = { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f, 10f };
            bool[] land = { false, false, false, false, false, true, true, true, true, true };

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 10, 1.0f, 0.4f, out _, out float thL2);

            Assert.AreEqual(9f, thL2, 1e-6f,
                "Quantile must be taken over Land only.");
        }

        // =====================================================================
        // Determinism and overload parity
        // =====================================================================

        [Test]
        public void SameInputs_SameOutputs()
        {
            float[] vals = { 0.5f, 0.51f, 0.7f, 0.7f, 0.72f, 0.9f, 0.44f, 0.44f };
            bool[] land = AllLand(8);

            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 8, 0.55f, 0.2f, out float a1, out float a2);
            HillsThresholdOps2D.ComputeAreaThresholds(
                vals, land, 8, 0.55f, 0.2f, out float b1, out float b2);

            Assert.AreEqual(a1, b1, 0f);
            Assert.AreEqual(a2, b2, 0f);
        }

        [Test]
        public void FieldOverload_MatchesArrayOverload()
        {
            // 4×2 grid; land where value >= 0.4.
            const int W = 4, H = 2;
            float[] vals = { 0.1f, 0.5f, 0.6f, 0.2f, 0.7f, 0.3f, 0.8f, 0.9f };
            bool[] land = new bool[W * H];
            for (int i = 0; i < vals.Length; i++) land[i] = vals[i] >= 0.4f;

            var domain = new GridDomain2D(W, H);
            var field = new ScalarField2D(domain, Allocator.Persistent);
            var mask = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        field.Values[y * W + x] = vals[y * W + x];
                        mask.SetUnchecked(x, y, land[y * W + x]);
                    }

                HillsThresholdOps2D.ComputeAreaThresholds(
                    in field, in mask, 0.6f, 0.3f, out float fThL1, out float fThL2);
                HillsThresholdOps2D.ComputeAreaThresholds(
                    vals, land, W * H, 0.6f, 0.3f, out float aThL1, out float aThL2);

                Assert.AreEqual(aThL1, fThL1, 0f,
                    "Runtime and diagnostics overloads must agree bit-for-bit.");
                Assert.AreEqual(aThL2, fThL2, 0f);
            }
            finally
            {
                field.Dispose();
                mask.Dispose();
            }
        }
    }
}