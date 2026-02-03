using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using Islands.PCG.Layout.Bsp;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Tests.EditMode
{
    public sealed class RoomFirstBspDungeon2DTests
    {
        private static RoomFirstBspDungeon2D.RoomFirstBspConfig DefaultCfg()
        {
            return new RoomFirstBspDungeon2D.RoomFirstBspConfig
            {
                splitIterations = 5,                 // worst-case leaves = 32
                minLeafSize = new int2(16, 16),
                roomPadding = 2,
                corridorBrushRadius = 1,
                connectWithManhattan = true,
                clearBeforeGenerate = true
            };
        }

        [Test]
        public void SameSeedAndConfig_ProducesSameSnapshotHash()
        {
            const int res = 96;

            var cfg = DefaultCfg();
            var domain = new GridDomain2D(res, res);

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            int leavesCap = BspPartition2D.MaxLeavesUpperBound(cfg.splitIterations);

            var leavesA = new NativeArray<BspPartition2D.IntRect2D>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var leavesB = new NativeArray<BspPartition2D.IntRect2D>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var centersA = new NativeArray<int2>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var centersB = new NativeArray<int2>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                var rngA = LayoutSeedUtil.CreateRng(12345);
                var rngB = LayoutSeedUtil.CreateRng(12345);

                RoomFirstBspDungeon2D.Generate(
                    ref maskA, ref rngA, in cfg,
                    leavesA, centersA,
                    out int leafCountA, out int placedRoomsA);

                RoomFirstBspDungeon2D.Generate(
                    ref maskB, ref rngB, in cfg,
                    leavesB, centersB,
                    out int leafCountB, out int placedRoomsB);

                Assert.AreEqual(leafCountA, leafCountB, "Same seed/config must yield identical BSP leaf count.");
                Assert.AreEqual(placedRoomsA, placedRoomsB, "Same seed/config must yield identical placed room count.");

                ulong hashA = maskA.SnapshotHash64();
                ulong hashB = maskB.SnapshotHash64();

                Assert.AreEqual(hashA, hashB, "Same seed/config must yield identical snapshot hash.");
            }
            finally
            {
                centersA.Dispose();
                centersB.Dispose();
                leavesA.Dispose();
                leavesB.Dispose();
                maskA.Dispose();
                maskB.Dispose();
            }
        }

        [Test]
        public void DifferentSeeds_ShouldAffectOutputHash_Sanity()
        {
            const int res = 96;

            var cfg = DefaultCfg();
            var domain = new GridDomain2D(res, res);

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            int leavesCap = BspPartition2D.MaxLeavesUpperBound(cfg.splitIterations);

            var leavesA = new NativeArray<BspPartition2D.IntRect2D>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var leavesB = new NativeArray<BspPartition2D.IntRect2D>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            var centersA = new NativeArray<int2>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var centersB = new NativeArray<int2>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                var rngA = LayoutSeedUtil.CreateRng(111);
                var rngB = LayoutSeedUtil.CreateRng(222);

                RoomFirstBspDungeon2D.Generate(
                    ref maskA, ref rngA, in cfg,
                    leavesA, centersA,
                    out _, out int placedRoomsA);

                RoomFirstBspDungeon2D.Generate(
                    ref maskB, ref rngB, in cfg,
                    leavesB, centersB,
                    out _, out int placedRoomsB);

                // Optional sanity: both should place at least one room in normal configs.
                Assert.Greater(placedRoomsA, 0, "Sanity: expected at least one room for seed A.");
                Assert.Greater(placedRoomsB, 0, "Sanity: expected at least one room for seed B.");

                ulong hashA = maskA.SnapshotHash64();
                ulong hashB = maskB.SnapshotHash64();

                Assert.AreNotEqual(hashA, hashB, "Different seeds should (almost surely) produce different snapshot hashes.");
            }
            finally
            {
                centersA.Dispose();
                centersB.Dispose();
                leavesA.Dispose();
                leavesB.Dispose();
                maskA.Dispose();
                maskB.Dispose();
            }
        }

        [Test]
        public void GoldenHashGate_RoomFirstBsp()
        {
            const int res = 96;

            var cfg = DefaultCfg();
            var domain = new GridDomain2D(res, res);

            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            int leavesCap = BspPartition2D.MaxLeavesUpperBound(cfg.splitIterations);

            var leaves = new NativeArray<BspPartition2D.IntRect2D>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var centers = new NativeArray<int2>(leavesCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                var rng = LayoutSeedUtil.CreateRng(12345);

                RoomFirstBspDungeon2D.Generate(
                    ref mask, ref rng, in cfg,
                    leaves, centers,
                    out _, out int placedRooms);

                // Optional sanity checks for the golden config
                Assert.Greater(placedRooms, 0, "Golden config should place at least one room.");
                Assert.Greater(mask.CountOnes(), 0, "Golden config should carve at least one ON cell.");

                ulong actual = mask.SnapshotHash64();

                // Lock-in pattern: set this once you visually validate Lantern output.
                const ulong GOLDEN = 0x23A63F312B9CDF98UL;

                Assert.AreEqual(GOLDEN, actual);
            }
            finally
            {
                centers.Dispose();
                leaves.Dispose();
                mask.Dispose();
            }
        }
    }
}
