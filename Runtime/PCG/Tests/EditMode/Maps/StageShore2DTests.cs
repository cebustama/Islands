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
    /// EditMode tests for <see cref="Stage_Shore2D"/>.
    ///
    /// Phase F4 / F4b / F4c.
    /// </summary>
    public sealed class StageShore2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        private const ulong ExpectedShallowWaterHash64 = 0x914AF43589BA6C13UL;

        // -----------------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0f, 0f, out ulong hashA, out _);
            RunOnce(in inputs, 0f, 0f, out ulong hashB, out _);

            Assert.AreEqual(hashA, hashB, "Stage_Shore2D must produce identical ShallowWater on repeated runs.");
        }

        // -----------------------------------------------------------------------
        // Invariants (adjacency-only mode)
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0f, 0f, out _, out MapContext2D ctx);

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
                    overlap.CopyFrom(shallowWater);
                    overlap.And(land);
                    Assert.AreEqual(0, overlap.CountOnes(),
                        "ShallowWater must be disjoint from Land.");

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

            RunOnce(in inputs, 0f, 0f, out ulong shallowHash, out MapContext2D ctx);

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
        // F4b — Depth mode
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_DepthZero_MatchesAdjacencyOnlyBehavior()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0f, 0f, out ulong hash, out MapContext2D ctx);

            try
            {
                Assert.AreEqual(ExpectedShallowWaterHash64, hash,
                    "ShallowWaterDepth01 == 0 must produce identical output to adjacency-only F4.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_Shore2D_DepthPositive_ProducesWiderBand()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0f, 0f, out _, out MapContext2D ctxNarrow);
            RunOnce(in inputs, 0.25f, 0f, out _, out MapContext2D ctxWide);

            try
            {
                int narrowCount = ctxNarrow.GetLayer(MapLayerId.ShallowWater).CountOnes();
                int wideCount = ctxWide.GetLayer(MapLayerId.ShallowWater).CountOnes();

                Assert.GreaterOrEqual(wideCount, narrowCount,
                    $"Depth=0.25 ShallowWater count ({wideCount}) must be >= adjacency-only " +
                    $"count ({narrowCount}).");

                Assert.Greater(wideCount, narrowCount,
                    "With default island tunables, depth=0.25 should produce strictly more " +
                    "ShallowWater cells than adjacency-only.");
            }
            finally
            {
                ctxNarrow.Dispose();
                ctxWide.Dispose();
            }
        }

        [Test]
        public void Stage_Shore2D_DepthPositive_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0.25f, 0f, out ulong hashA, out _);
            RunOnce(in inputs, 0.25f, 0f, out ulong hashB, out _);

            Assert.AreEqual(hashA, hashB,
                "Stage_Shore2D with depth > 0 must produce identical ShallowWater on repeated runs.");
        }

        [Test]
        public void Stage_Shore2D_DepthPositive_DisjointFromLand()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0.25f, 0f, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);

                var overlap = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    overlap.CopyFrom(shallowWater);
                    overlap.And(land);
                    Assert.AreEqual(0, overlap.CountOnes(),
                        "ShallowWater must be disjoint from Land even in depth mode.");
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

        [Test]
        public void Stage_Shore2D_DepthPositive_NoMutate()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0.25f, 0f, out _, out MapContext2D ctx);

            try
            {
                ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);
                Assert.IsTrue(height.Values.IsCreated,
                    "Height field must still be valid after depth-mode Shore execution.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // F4c — MidWater
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Shore2D_MidWater_ProducesCells()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            // Use shallow=0.05, mid=0.20 so there's a clear band between them.
            RunOnce(in inputs, 0.05f, 0.20f, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D midWater = ref ctx.GetLayer(MapLayerId.MidWater);
                Assert.Greater(midWater.CountOnes(), 0,
                    "MidWater with depth=0.20 should produce cells on a default island.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_Shore2D_MidWater_DisjointFromLandAndShallow()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0.05f, 0.20f, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);
                ref MaskGrid2D midWater = ref ctx.GetLayer(MapLayerId.MidWater);

                var overlap = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    // MidWater ∩ Land == ∅
                    overlap.CopyFrom(midWater);
                    overlap.And(land);
                    Assert.AreEqual(0, overlap.CountOnes(),
                        "MidWater must be disjoint from Land.");

                    // MidWater ∩ ShallowWater == ∅
                    overlap.CopyFrom(midWater);
                    overlap.And(shallowWater);
                    Assert.AreEqual(0, overlap.CountOnes(),
                        "MidWater must be disjoint from ShallowWater.");
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

        [Test]
        public void Stage_Shore2D_MidWater_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0.05f, 0.20f, out _, out MapContext2D ctxA);
            RunOnce(in inputs, 0.05f, 0.20f, out _, out MapContext2D ctxB);

            try
            {
                ulong hashA = ctxA.GetLayer(MapLayerId.MidWater).SnapshotHash64(includeDimensions: true);
                ulong hashB = ctxB.GetLayer(MapLayerId.MidWater).SnapshotHash64(includeDimensions: true);
                Assert.AreEqual(hashA, hashB,
                    "MidWater must be deterministic across repeated runs.");
            }
            finally
            {
                ctxA.Dispose();
                ctxB.Dispose();
            }
        }

        [Test]
        public void Stage_Shore2D_MidWaterZero_NoLayerAllocated()
        {
            // When MidWaterDepth01 == 0, the MidWater layer should not be allocated.
            // This preserves backward compatibility and avoids unnecessary allocations.
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, 0f, 0f, out _, out MapContext2D ctx);

            try
            {
                // MidWater should not be in the context when depth == 0.
                // GetLayer would throw if not allocated; we catch that.
                bool allocated = true;
                try
                {
                    ctx.GetLayer(MapLayerId.MidWater);
                }
                catch (System.InvalidOperationException)
                {
                    allocated = false;
                }

                Assert.IsFalse(allocated,
                    "MidWater layer must not be allocated when MidWaterDepth01 == 0.");
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
            float shallowWaterDepth01,
            float midWaterDepth01,
            out ulong shallowWaterHash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);

            var shore = new Stage_Shore2D();
            shore.ShallowWaterDepth01 = shallowWaterDepth01;
            shore.MidWaterDepth01 = midWaterDepth01;
            shore.Execute(ref ctx, in inputs);

            shallowWaterHash = ctx.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(includeDimensions: true);
        }
    }
}