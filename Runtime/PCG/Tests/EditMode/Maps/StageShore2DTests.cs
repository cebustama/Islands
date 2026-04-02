using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class StageShore2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // Set to the actual value reported by Stage_Shore2D_GoldenHash_IsLocked on first run.
        private const ulong ExpectedShallowWaterHash64 = 0xC24753CA1E06940FUL;

        // -----------------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong hashA, out _);
            RunOnce(in inputs, out ulong hashB, out _);

            Assert.AreEqual(hashA, hashB, "Stage_Shore2D must produce identical ShallowWater on repeated runs.");
        }

        // -----------------------------------------------------------------------
        // Invariants
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deepWater = ref ctx.GetLayer(MapLayerId.DeepWater);
                ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);

                ulong landHashBefore = land.SnapshotHash64(includeDimensions: true);
                ulong deepHashBefore = deepWater.SnapshotHash64(includeDimensions: true);

                var overlap = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    // ShallowWater ∩ Land == ∅
                    overlap.CopyFrom(shallowWater);
                    overlap.And(land);
                    Assert.AreEqual(0, overlap.CountOnes(),
                        "ShallowWater must be disjoint from Land.");

                    // Every ShallowWater ON cell must have at least one 4-adjacent Land neighbor.
                    int w = domain.Width;
                    int h = domain.Height;

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            if (!shallowWater.GetUnchecked(x, y))
                                continue;

                            bool hasLandNeighbor =
                                (x > 0 && land.GetUnchecked(x - 1, y)) ||
                                (x + 1 < w && land.GetUnchecked(x + 1, y)) ||
                                (y > 0 && land.GetUnchecked(x, y - 1)) ||
                                (y + 1 < h && land.GetUnchecked(x, y + 1));

                            Assert.IsTrue(hasLandNeighbor,
                                $"ShallowWater cell ({x},{y}) has no 4-adjacent Land neighbor.");
                        }
                    }

                    // No-mutate checks
                    Assert.AreEqual(landHashBefore, land.SnapshotHash64(includeDimensions: true),
                        "Stage_Shore2D must not mutate Land.");
                    Assert.AreEqual(deepHashBefore, deepWater.SnapshotHash64(includeDimensions: true),
                        "Stage_Shore2D must not mutate DeepWater.");
                }
                finally
                {
                    overlap.Dispose();
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // Golden hash gate
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_GoldenHash_IsLocked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong shallowHash, out MapContext2D ctx);

            try
            {
                if (ExpectedShallowWaterHash64 == 0UL)
                {
                    Assert.Fail(
                        "F4 stage golden is not initialized.\n" +
                        $"Set ExpectedShallowWaterHash64 = 0x{shallowHash:X16}UL;");
                }

                Assert.AreEqual(ExpectedShallowWaterHash64, shallowHash,
                    $"ShallowWater golden changed. Got=0x{shallowHash:X16} Expected=0x{ExpectedShallowWaterHash64:X16}");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static void RunOnce(
            in MapInputs inputs,
            out ulong shallowWaterHash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);

            shallowWaterHash = ctx.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(includeDimensions: true);
        }
    }
}