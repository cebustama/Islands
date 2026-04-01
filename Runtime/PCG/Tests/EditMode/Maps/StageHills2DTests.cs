using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class StageHills2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        private const ulong ExpectedLandEdgeHash64 = 0x7D76B5E7C5A58F7BUL;
        private const ulong ExpectedLandInteriorHash64 = 0x241E85B81597683FUL;
        private const ulong ExpectedHillsL1Hash64 = 0xBB6041A743BD59CBUL;
        private const ulong ExpectedHillsL2Hash64 = 0x1F7608A3B36C1443UL;

        [Test]
        public void Stage_Hills2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong edgeA, out ulong interiorA, out ulong h1A, out ulong h2A, out _);
            RunOnce(in inputs, out ulong edgeB, out ulong interiorB, out ulong h1B, out ulong h2B, out _);

            Assert.AreEqual(edgeA, edgeB);
            Assert.AreEqual(interiorA, interiorB);
            Assert.AreEqual(h1A, h1B);
            Assert.AreEqual(h2A, h2B);
        }

        [Test]
        public void Stage_Hills2D_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong landBefore, out ulong deepBefore, out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);
                ref MaskGrid2D edge = ref ctx.GetLayer(MapLayerId.LandEdge);
                ref MaskGrid2D interior = ref ctx.GetLayer(MapLayerId.LandInterior);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);

                var union = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                var intersection = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                var subsetCheck = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

                try
                {
                    union.CopyFrom(edge);
                    union.Or(interior);
                    Assert.AreEqual(land.SnapshotHash64(includeDimensions: true), union.SnapshotHash64(includeDimensions: true), "LandEdge U LandInterior must equal Land.");

                    intersection.CopyFrom(edge);
                    intersection.And(interior);
                    Assert.AreEqual(0, intersection.CountOnes(), "LandEdge and LandInterior must be disjoint.");

                    subsetCheck.CopyFrom(hillsL1);
                    subsetCheck.AndNot(interior);
                    Assert.AreEqual(0, subsetCheck.CountOnes(), "HillsL1 must be subset of LandInterior.");

                    subsetCheck.CopyFrom(hillsL2);
                    subsetCheck.AndNot(hillsL1);
                    Assert.AreEqual(0, subsetCheck.CountOnes(), "HillsL2 must be subset of HillsL1.");

                    subsetCheck.CopyFrom(hillsL1);
                    subsetCheck.And(deep);
                    Assert.AreEqual(0, subsetCheck.CountOnes(), "HillsL1 must not overlap DeepWater.");

                    subsetCheck.CopyFrom(hillsL2);
                    subsetCheck.And(deep);
                    Assert.AreEqual(0, subsetCheck.CountOnes(), "HillsL2 must not overlap DeepWater.");

                    Assert.AreEqual(landBefore, land.SnapshotHash64(includeDimensions: true), "Stage_Hills2D must not mutate Land.");
                    Assert.AreEqual(deepBefore, deep.SnapshotHash64(includeDimensions: true), "Stage_Hills2D must not mutate DeepWater.");
                }
                finally
                {
                    union.Dispose();
                    intersection.Dispose();
                    subsetCheck.Dispose();
                }
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

            RunOnce(in inputs, out _, out _, out ulong edgeHash, out ulong interiorHash, out ulong h1Hash, out ulong h2Hash, out MapContext2D ctx);

            try
            {
                if (ExpectedLandEdgeHash64 == 0UL || ExpectedLandInteriorHash64 == 0UL || ExpectedHillsL1Hash64 == 0UL || ExpectedHillsL2Hash64 == 0UL)
                {
                    Assert.Fail(
                        "F3 stage goldens are not initialized.\n" +
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
