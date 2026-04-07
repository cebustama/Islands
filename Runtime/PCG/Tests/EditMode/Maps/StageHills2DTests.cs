using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Tests for Stage_Hills2D (F3b — height-threshold classification).
    ///
    /// Contract changes from F3 → F3b:
    /// - HillsL1 and HillsL2 are now disjoint (was HillsL2 ⊆ HillsL1).
    /// - HillsL1/L2 ⊆ Land (was HillsL1 ⊆ LandInterior).
    /// - Hills are derived from Height field thresholds (was independent noise on LandInterior).
    /// </summary>
    public sealed class StageHills2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // F3b golden hashes — zero until first successful run captures new values.
        private const ulong ExpectedLandEdgeHash64 = 0x17D1FE919DCC3C33UL;
        private const ulong ExpectedLandInteriorHash64 = 0x228E6C047D7792EFUL;
        private const ulong ExpectedHillsL1Hash64 = 0xD8B3DCF4A4AC3BA0UL;
        private const ulong ExpectedHillsL2Hash64 = 0x29F3B1EA4B818E4FUL;

        [Test]
        public void Stage_Hills2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong edgeA, out ulong interiorA, out ulong h1A, out ulong h2A, out _);
            RunOnce(in inputs, out ulong edgeB, out ulong interiorB, out ulong h1B, out ulong h2B, out _);

            Assert.AreEqual(edgeA, edgeB, "LandEdge must be deterministic.");
            Assert.AreEqual(interiorA, interiorB, "LandInterior must be deterministic.");
            Assert.AreEqual(h1A, h1B, "HillsL1 must be deterministic.");
            Assert.AreEqual(h2A, h2B, "HillsL2 must be deterministic.");
        }

        [Test]
        public void Stage_Hills2D_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong landBefore, out ulong deepBefore,
                    out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);
                ref MaskGrid2D edge = ref ctx.GetLayer(MapLayerId.LandEdge);
                ref MaskGrid2D interior = ref ctx.GetLayer(MapLayerId.LandInterior);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);

                var temp = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

                try
                {
                    // LandEdge ∪ LandInterior == Land
                    temp.CopyFrom(edge);
                    temp.Or(interior);
                    Assert.AreEqual(land.SnapshotHash64(includeDimensions: true),
                                    temp.SnapshotHash64(includeDimensions: true),
                                    "LandEdge ∪ LandInterior must equal Land.");

                    // LandEdge ∩ LandInterior == ∅
                    temp.CopyFrom(edge);
                    temp.And(interior);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "LandEdge and LandInterior must be disjoint.");

                    // HillsL1 ⊆ Land
                    temp.CopyFrom(hillsL1);
                    temp.AndNot(land);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL1 must be a subset of Land.");

                    // HillsL2 ⊆ Land
                    temp.CopyFrom(hillsL2);
                    temp.AndNot(land);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL2 must be a subset of Land.");

                    // HillsL1 ∩ HillsL2 == ∅ (F3b: disjoint, not overlapping)
                    temp.CopyFrom(hillsL1);
                    temp.And(hillsL2);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL1 and HillsL2 must be disjoint.");

                    // No-mutate: Land, DeepWater unchanged.
                    // Height no-mutate is guaranteed by construction (Stage_Hills2D only
                    // reads Height, never calls EnsureField(Height)). Land hash stability
                    // is the primary gate — if Height changed, Land would change too
                    // (Land = Height >= waterThreshold in Stage_BaseTerrain2D).
                    Assert.AreEqual(landBefore, land.SnapshotHash64(includeDimensions: true),
                                    "Stage_Hills2D must not mutate Land.");
                    Assert.AreEqual(deepBefore, deep.SnapshotHash64(includeDimensions: true),
                                    "Stage_Hills2D must not mutate DeepWater.");
                }
                finally
                {
                    temp.Dispose();
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_Hills2D_HeightCoherence()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = MapTunables2D.Default;
            var inputs = new MapInputs(Seed, domain, tunables);

            RunOnce(in inputs, out _, out _, out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
                ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);

                float thL1 = tunables.hillsThresholdL1;
                float thL2 = tunables.hillsThresholdL2;

                int w = domain.Width;
                int h = domain.Height;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!land.GetUnchecked(x, y))
                            continue;

                        float hv = height.GetUnchecked(x, y);
                        bool isL1 = hillsL1.GetUnchecked(x, y);
                        bool isL2 = hillsL2.GetUnchecked(x, y);

                        if (isL2)
                        {
                            Assert.IsTrue(hv >= thL2,
                                $"HillsL2 cell ({x},{y}) has Height {hv:F4} < thresholdL2 {thL2:F4}.");
                        }
                        else if (isL1)
                        {
                            Assert.IsTrue(hv >= thL1,
                                $"HillsL1 cell ({x},{y}) has Height {hv:F4} < thresholdL1 {thL1:F4}.");
                            Assert.IsTrue(hv < thL2,
                                $"HillsL1 cell ({x},{y}) has Height {hv:F4} >= thresholdL2 {thL2:F4} — should be HillsL2.");
                        }
                        else
                        {
                            Assert.IsTrue(hv < thL1,
                                $"Non-hill Land cell ({x},{y}) has Height {hv:F4} >= thresholdL1 {thL1:F4} — should be HillsL1.");
                        }
                    }
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_Hills2D_ThresholdClamping_L2BelowL1()
        {
            // If L2 < L1 is provided, clamping forces L2 = L1.
            // All qualifying cells go to L2; L1 should be empty.
            var domain = new GridDomain2D(W, H);
            var tunables = new MapTunables2D(
                islandRadius01: 0.45f,
                waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f,
                islandSmoothTo01: 0.70f,
                hillsThresholdL1: 0.70f,
                hillsThresholdL2: 0.50f); // intentionally below L1
            var inputs = new MapInputs(Seed, domain, tunables);

            RunOnce(in inputs, out _, out _, out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);

                // After clamping, L2 = L1 = 0.70. No cell can satisfy
                // Height >= 0.70 AND Height < 0.70, so HillsL1 is empty.
                Assert.AreEqual(0, hillsL1.CountOnes(),
                    "When thL2 is clamped to thL1, HillsL1 should be empty.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_Hills2D_GoldenHashes_Locked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _,
                    out ulong edgeHash, out ulong interiorHash, out ulong h1Hash, out ulong h2Hash,
                    out MapContext2D ctx);

            try
            {
                if (ExpectedLandEdgeHash64 == 0UL || ExpectedLandInteriorHash64 == 0UL ||
                    ExpectedHillsL1Hash64 == 0UL || ExpectedHillsL2Hash64 == 0UL)
                {
                    Assert.Fail(
                        "F3b stage goldens are not initialized.\n" +
                        $"Set ExpectedLandEdgeHash64     = 0x{edgeHash:X16}UL;\n" +
                        $"Set ExpectedLandInteriorHash64 = 0x{interiorHash:X16}UL;\n" +
                        $"Set ExpectedHillsL1Hash64      = 0x{h1Hash:X16}UL;\n" +
                        $"Set ExpectedHillsL2Hash64      = 0x{h2Hash:X16}UL;\n");
                }

                Assert.AreEqual(ExpectedLandEdgeHash64, edgeHash, "LandEdge golden changed.");
                Assert.AreEqual(ExpectedLandInteriorHash64, interiorHash, "LandInterior golden changed.");
                Assert.AreEqual(ExpectedHillsL1Hash64, h1Hash, "HillsL1 golden changed.");
                Assert.AreEqual(ExpectedHillsL2Hash64, h2Hash, "HillsL2 golden changed.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static void RunOnce(
            in MapInputs inputs,
            out ulong landHash,
            out ulong deepHash,
            out ulong edgeHash,
            out ulong interiorHash,
            out ulong h1Hash,
            out ulong h2Hash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            landHash = ctx.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
            deepHash = ctx.GetLayer(MapLayerId.DeepWater).SnapshotHash64(includeDimensions: true);

            new Stage_Hills2D().Execute(ref ctx, in inputs);

            edgeHash = ctx.GetLayer(MapLayerId.LandEdge).SnapshotHash64(includeDimensions: true);
            interiorHash = ctx.GetLayer(MapLayerId.LandInterior).SnapshotHash64(includeDimensions: true);
            h1Hash = ctx.GetLayer(MapLayerId.HillsL1).SnapshotHash64(includeDimensions: true);
            h2Hash = ctx.GetLayer(MapLayerId.HillsL2).SnapshotHash64(includeDimensions: true);
        }

        private static void RunOnce(
            in MapInputs inputs,
            out ulong edgeHash,
            out ulong interiorHash,
            out ulong h1Hash,
            out ulong h2Hash,
            out MapContext2D ctx)
        {
            RunOnce(in inputs, out _, out _, out edgeHash, out interiorHash, out h1Hash, out h2Hash, out ctx);
        }
    }
}