using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Islands.PCG.Tests.EditMode
{
    public sealed class RoomsCorridorsComposer2DTests
    {
        private static RoomsCorridorsComposer2D.RoomsCorridorsConfig DefaultCfg()
        {
            return new RoomsCorridorsComposer2D.RoomsCorridorsConfig
            {
                roomCount = 12,
                roomSizeMin = new int2(6, 6),
                roomSizeMax = new int2(14, 14),
                placementAttemptsPerRoom = 30,
                roomPadding = 2,
                corridorBrushRadius = 1,
                clearBeforeGenerate = true,
                allowOverlap = true
            };
        }

        [Test]
        public void SameSeedAndConfig_ProducesSameSnapshotHash()
        {
            const int res = 64;
            var domain = new GridDomain2D(res, res);

            var cfg = DefaultCfg();

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            var centersA = new NativeArray<int2>(cfg.roomCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var centersB = new NativeArray<int2>(cfg.roomCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                var rngA = new Unity.Mathematics.Random(1u);
                RoomsCorridorsComposer2D.Generate(ref maskA, ref rngA, in cfg, centersA, out int placedA);

                var rngB = new Unity.Mathematics.Random(1u);
                RoomsCorridorsComposer2D.Generate(ref maskB, ref rngB, in cfg, centersB, out int placedB);

                Assert.Greater(placedA, 0, "Expected at least one room to be placed with this config.");
                Assert.AreEqual(placedA, placedB, "Placed room count should match for identical seed/config.");

                ulong hashA = maskA.SnapshotHash64();
                ulong hashB = maskB.SnapshotHash64();

                Assert.AreEqual(hashA, hashB, "Same seed/config must yield identical snapshot hash.");
            }
            finally
            {
                centersA.Dispose();
                centersB.Dispose();
                maskA.Dispose();
                maskB.Dispose();
            }
        }

        [Test]
        public void DifferentSeeds_ShouldAffectOutputHash_Sanity()
        {
            const int res = 64;
            var domain = new GridDomain2D(res, res);

            var cfg = DefaultCfg();

            // Compute baseline hash at seed=1, then ensure at least one of seeds 2..4 differs.
            ulong baseline;
            {
                var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
                var centers = new NativeArray<int2>(cfg.roomCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                try
                {
                    var rng = new Unity.Mathematics.Random(1u);
                    RoomsCorridorsComposer2D.Generate(ref mask, ref rng, in cfg, centers, out _);
                    baseline = mask.SnapshotHash64();
                }
                finally
                {
                    centers.Dispose();
                    mask.Dispose();
                }
            }

            bool foundDifferent = false;

            for (uint seed = 2; seed <= 4; seed++)
            {
                var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
                var centers = new NativeArray<int2>(cfg.roomCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                try
                {
                    var rng = new Unity.Mathematics.Random(seed);
                    RoomsCorridorsComposer2D.Generate(ref mask, ref rng, in cfg, centers, out _);

                    ulong h = mask.SnapshotHash64();
                    if (h != baseline)
                    {
                        foundDifferent = true;
                        break;
                    }
                }
                finally
                {
                    centers.Dispose();
                    mask.Dispose();
                }
            }

            Assert.IsTrue(foundDifferent,
                "Sanity check failed: seeds 2..4 produced the same hash as seed=1. " +
                "This likely means the seed is not affecting generation, or config is degenerate.");
        }

        [Test]
        public void GoldenHashGate_RoomsCorridors()
        {
            // 1) Run once, copy the reported hash from the Assert.Inconclusive message,
            // 2) Paste it into ExpectedHash below,
            // 3) From then on, any unintended change is detected immediately.

            const ulong ExpectedHash = 0xFE6D84CE0A6DE05BUL; // TODO: replace after first run

            const int res = 64;
            var domain = new GridDomain2D(res, res);

            var cfg = DefaultCfg();

            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var centers = new NativeArray<int2>(cfg.roomCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                var rng = new Unity.Mathematics.Random(1u);
                RoomsCorridorsComposer2D.Generate(ref mask, ref rng, in cfg, centers, out int placed);

                Assert.Greater(placed, 0, "Expected at least one room to be placed with this config.");

                ulong actual = mask.SnapshotHash64();

                if (ExpectedHash == 0UL)
                {
                    Assert.Inconclusive($"Set ExpectedHash to 0x{actual:X}UL to lock the golden gate.");
                }

                Assert.AreEqual(ExpectedHash, actual, "Golden hash mismatch: generation output changed.");
            }
            finally
            {
                centers.Dispose();
                mask.Dispose();
            }
        }
    }
}
