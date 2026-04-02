using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class StageTraversal2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // Set to values reported on first run.
        private const ulong ExpectedWalkableHash64 = 0x2461F5FF61399257UL;
        private const ulong ExpectedStairsHash64 = 0xC4A0355A024FD9B0UL;

        // -----------------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Traversal2D_IsDeterministic()
        {
            var inputs = MakeInputs();
            RunOnce(in inputs, out ulong walkA, out ulong stairsA, out _);
            RunOnce(in inputs, out ulong walkB, out ulong stairsB, out _);
            Assert.AreEqual(walkA, walkB,
                "Stage_Traversal2D must produce identical Walkable on repeated runs.");
            Assert.AreEqual(stairsA, stairsB,
                "Stage_Traversal2D must produce identical Stairs on repeated runs.");
        }

        // -----------------------------------------------------------------------
        // Invariants
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Traversal2D_Invariants_Hold()
        {
            var inputs = MakeInputs();
            RunOnce(in inputs, out _, out _, out MapContext2D ctx);
            try
            {
                var domain = new GridDomain2D(W, H);

                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
                ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);
                ref MaskGrid2D vegetation = ref ctx.GetLayer(MapLayerId.Vegetation);
                ref MaskGrid2D walkable = ref ctx.GetLayer(MapLayerId.Walkable);
                ref MaskGrid2D stairs = ref ctx.GetLayer(MapLayerId.Stairs);

                // Capture upstream hashes before assertions for no-mutate check
                ulong landHash = land.SnapshotHash64(includeDimensions: true);
                ulong h1Hash = hillsL1.SnapshotHash64(includeDimensions: true);
                ulong h2Hash = hillsL2.SnapshotHash64(includeDimensions: true);
                ulong shoreHash = shallowWater.SnapshotHash64(includeDimensions: true);
                ulong vegHash = vegetation.SnapshotHash64(includeDimensions: true);

                var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    // Walkable ⊆ Land
                    scratch.CopyFrom(walkable);
                    scratch.AndNot(land);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Walkable must be a subset of Land.");

                    // Walkable ∩ HillsL2 == ∅
                    scratch.CopyFrom(walkable);
                    scratch.And(hillsL2);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Walkable must not overlap HillsL2.");

                    // Stairs ⊆ HillsL1
                    scratch.CopyFrom(stairs);
                    scratch.AndNot(hillsL1);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Stairs must be a subset of HillsL1.");

                    // Stairs ∩ HillsL2 == ∅
                    scratch.CopyFrom(stairs);
                    scratch.And(hillsL2);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Stairs must not overlap HillsL2.");

                    // Stairs ⊆ Walkable
                    scratch.CopyFrom(stairs);
                    scratch.AndNot(walkable);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Stairs must be a subset of Walkable.");

                    // No-mutate checks
                    Assert.AreEqual(landHash, land.SnapshotHash64(includeDimensions: true),
                        "Stage_Traversal2D must not mutate Land.");
                    Assert.AreEqual(h1Hash, hillsL1.SnapshotHash64(includeDimensions: true),
                        "Stage_Traversal2D must not mutate HillsL1.");
                    Assert.AreEqual(h2Hash, hillsL2.SnapshotHash64(includeDimensions: true),
                        "Stage_Traversal2D must not mutate HillsL2.");
                    Assert.AreEqual(shoreHash, shallowWater.SnapshotHash64(includeDimensions: true),
                        "Stage_Traversal2D must not mutate ShallowWater.");
                    Assert.AreEqual(vegHash, vegetation.SnapshotHash64(includeDimensions: true),
                        "Stage_Traversal2D must not mutate Vegetation.");
                }
                finally { scratch.Dispose(); }
            }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Golden hash gate
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Traversal2D_GoldenHash_IsLocked()
        {
            var inputs = MakeInputs();
            RunOnce(in inputs, out ulong walkHash, out ulong stairsHash, out MapContext2D ctx);
            try
            {
                if (ExpectedWalkableHash64 == 0UL)
                    Assert.Fail(
                        "F6 Walkable golden not initialized.\n" +
                        $"Set ExpectedWalkableHash64 = 0x{walkHash:X16}UL;");

                if (ExpectedStairsHash64 == 0UL)
                    Assert.Fail(
                        "F6 Stairs golden not initialized.\n" +
                        $"Set ExpectedStairsHash64 = 0x{stairsHash:X16}UL;");

                Assert.AreEqual(ExpectedWalkableHash64, walkHash,
                    $"Walkable golden changed. Got=0x{walkHash:X16} Expected=0x{ExpectedWalkableHash64:X16}");
                Assert.AreEqual(ExpectedStairsHash64, stairsHash,
                    $"Stairs golden changed. Got=0x{stairsHash:X16} Expected=0x{ExpectedStairsHash64:X16}");
            }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static MapInputs MakeInputs()
        {
            var domain = new GridDomain2D(W, H);
            return new MapInputs(Seed, domain, MapTunables2D.Default);
        }

        private static void RunOnce(
            in MapInputs inputs,
            out ulong walkableHash,
            out ulong stairsHash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);
            new Stage_Vegetation2D().Execute(ref ctx, in inputs);
            new Stage_Traversal2D().Execute(ref ctx, in inputs);

            walkableHash = ctx.GetLayer(MapLayerId.Walkable).SnapshotHash64(includeDimensions: true);
            stairsHash = ctx.GetLayer(MapLayerId.Stairs).SnapshotHash64(includeDimensions: true);
        }
    }
}