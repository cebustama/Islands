// Phase T2.0 — Layer 0 tests: SteppedHeightSampler2D.
// Placement: Runtime/PCG/Tests/EditMode/Inspection/.
//
// Synthetic MapDataExport construction uses the internal ctor via the existing
// InternalsVisibleTo grant on the layout assembly (precedent: BiomeTileOverrideTests
// Q-T-4 / Q-T-9). No pipeline run required.
//
// Boundary assertions are only made under a binary-exact range (NormMin 0, NormMax 1),
// where the reciprocal is exactly 1f and no rounding enters the mapping. Under any
// other range the level boundaries do not fall on exact decimal values — 0.70f under
// [0.4, 1] yields t*8 = 3.9999995, i.e. level 3, because 0.4f is slightly above 0.4
// and 0.7f slightly below 0.7 — so tests over such ranges pick inputs well inside a
// band. This is arithmetic, not imprecision: determinism is unaffected, the same
// input always yields the same level.

using System;
using Islands.PCG.Inspection;
using Islands.PCG.Layout.Maps;
using NUnit.Framework;

namespace Islands.PCG.Tests.EditMode.Inspection
{
    public sealed class SteppedHeightSampler2DTests
    {
        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static MapDataExport MakeHeightExport(int w, int h, float[] height)
        {
            var layers = new bool[(int)MapLayerId.COUNT][];
            var fields = new float[(int)MapFieldId.COUNT][];
            fields[(int)MapFieldId.Height] = height;
            return new MapDataExport(w, h, 1u, layers, fields);
        }

        private static MapDataExport MakeExportWithoutHeight(int w, int h)
        {
            var layers = new bool[(int)MapLayerId.COUNT][];
            var fields = new float[(int)MapFieldId.COUNT][];
            return new MapDataExport(w, h, 1u, layers, fields);
        }

        private static SteppedHeightPolicy Policy(int levels, float min, float max)
            => new SteppedHeightPolicy { LevelCount = levels, NormMin = min, NormMax = max };

        // -----------------------------------------------------------------
        // Guards
        // -----------------------------------------------------------------

        [Test]
        public void NullExport_Throws()
        {
            var policy = SteppedHeightPolicy.Default;
            Assert.Throws<ArgumentNullException>(
                () => SteppedHeightSampler2D.TrySampleLevels(null, in policy, out _));
        }

        [Test]
        public void LevelCountBelowOne_Throws()
        {
            var export = MakeHeightExport(2, 2, new float[4]);
            var policy = Policy(0, 0f, 1f);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SteppedHeightSampler2D.TrySampleLevels(export, in policy, out _));
        }

        [Test]
        public void NormMaxEqualOrBelowNormMin_Throws()
        {
            var export = MakeHeightExport(2, 2, new float[4]);

            var equalPolicy = Policy(4, 0.5f, 0.5f);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SteppedHeightSampler2D.TrySampleLevels(export, in equalPolicy, out _));

            var invertedPolicy = Policy(4, 0.8f, 0.4f);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => SteppedHeightSampler2D.TrySampleLevels(export, in invertedPolicy, out _));
        }

        [Test]
        public void MissingHeight_ReturnsFalse_NullLevels()
        {
            var export = MakeExportWithoutHeight(2, 2);
            var policy = SteppedHeightPolicy.Default;

            bool ok = SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] levels);

            Assert.IsFalse(ok);
            Assert.IsNull(levels);
        }

        // -----------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------

        [Test]
        public void SameInput_TwoRuns_IdenticalArrays_DistinctInstances()
        {
            const int w = 8, h = 8;
            var height = new float[w * h];
            for (int i = 0; i < height.Length; i++)
                height[i] = i / (float)(height.Length - 1); // deterministic ramp

            var export = MakeHeightExport(w, h, height);
            var policy = Policy(8, 0.4f, 1f);

            bool ok1 = SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] a);
            bool ok2 = SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] b);

            Assert.IsTrue(ok1);
            Assert.IsTrue(ok2);
            Assert.AreNotSame(a, b);           // fresh allocation per call
            CollectionAssert.AreEqual(a, b);   // bit-identical content
        }

        // -----------------------------------------------------------------
        // Ordering
        // -----------------------------------------------------------------

        [Test]
        public void RowMajor_IndexIsXPlusYTimesWidth()
        {
            // 2x2, values chosen so each cell lands on a distinct level under (4, 0, 1):
            //   (0,0)=0.0 → 0   (1,0)=0.25 → 1
            //   (0,1)=0.5 → 2   (1,1)=0.75 → 3
            var export = MakeHeightExport(2, 2, new[] { 0.00f, 0.25f, 0.50f, 0.75f });
            var policy = Policy(4, 0f, 1f);

            Assert.IsTrue(SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] levels));

            Assert.AreEqual(0, levels[0 + 0 * 2]);
            Assert.AreEqual(1, levels[1 + 0 * 2]);
            Assert.AreEqual(2, levels[0 + 1 * 2]);
            Assert.AreEqual(3, levels[1 + 1 * 2]);
        }

        // -----------------------------------------------------------------
        // Quantization — boundaries and clamping
        // -----------------------------------------------------------------

        [Test]
        public void QuantizationBoundaries_NominalRange()
        {
            // (4, 0, 1) with binary-exact inputs: floor(t * 4), top clamped to 3.
            var export = MakeHeightExport(6, 1,
                new[] { 0f, 0.249f, 0.25f, 0.75f, 1f, 2f /* above max, clamps */ });
            var policy = Policy(4, 0f, 1f);

            Assert.IsTrue(SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] levels));
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 3, 3, 3 }, levels);
        }

        // -----------------------------------------------------------------
        // Quantization — chosen range behavior (open decision resolved 2026-08-21:
        // configurable NormMin/NormMax; below-min clamps to level 0)
        // -----------------------------------------------------------------

        [Test]
        public void ConfiguredRange_SpendsFullSpanOnLandBand()
        {
            // Land-shaped band [0.4, 1.0] over 8 levels: the band floor maps to
            // level 0, near-max land maps to the top level, and sub-threshold
            // (water) values clamp to 0 instead of consuming levels.
            // 0.75f sits mid-band (t*8 = 4.67), away from any boundary — see the file
            // header on why boundary values are not asserted under this range.
            var export = MakeHeightExport(4, 1, new[] { 0.10f, 0.40f, 0.75f, 0.999f });
            var policy = Policy(8, 0.4f, 1f);

            Assert.IsTrue(SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] levels));

            Assert.AreEqual(0, levels[0]); // water band → clamped to 0
            Assert.AreEqual(0, levels[1]); // band floor → level 0
            Assert.AreEqual(4, levels[2]); // mid-band → level 4
            Assert.AreEqual(7, levels[3]); // near band top → top level
        }

        [Test]
        public void NominalRange_CompressesLandBand()
        {
            // Documents the risk the configured range avoids: over [0, 1], the
            // minimum measured land Height (~0.412 post-W-aux.f) starts at level 3
            // of 8 — nearly half the span is spent below the coastline.
            var export = MakeHeightExport(1, 1, new[] { 0.412f });
            var policy = Policy(8, 0f, 1f);

            Assert.IsTrue(SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] levels));
            Assert.AreEqual(3, levels[0]);
        }

        [Test]
        public void SingleLevel_EverythingIsLevelZero()
        {
            var export = MakeHeightExport(3, 1, new[] { 0f, 0.5f, 1f });
            var policy = Policy(1, 0f, 1f);

            Assert.IsTrue(SteppedHeightSampler2D.TrySampleLevels(export, in policy, out int[] levels));
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, levels);
        }

        // -----------------------------------------------------------------
        // Level → world mapping (step scaling and offset)
        // -----------------------------------------------------------------

        [Test]
        public void LevelToWorldY_AppliesStepAndOffset()
        {
            Assert.AreEqual(2.0f, SteppedHeightSampler2D.LevelToWorldY(0, 0.5f, 2f));
            Assert.AreEqual(3.5f, SteppedHeightSampler2D.LevelToWorldY(3, 0.5f, 2f));
            Assert.AreEqual(0.25f, SteppedHeightSampler2D.LevelToWorldY(5, 0.25f, -1f));
        }
    }
}