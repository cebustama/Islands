using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Islands.PCG.Tests.EditMode
{
    public class CorridorFirstDungeon2DTests
    {
        private static CorridorFirstDungeon2D.CorridorFirstConfig DefaultCfg => new CorridorFirstDungeon2D.CorridorFirstConfig
        {
            corridorCount = 12,
            corridorLengthMin = 6,
            corridorLengthMax = 14,
            corridorBrushRadius = 0,

            roomSpawnCount = 8,
            roomSpawnChance = 0.5f, // only used if roomSpawnCount <= 0
            roomSizeMin = new int2(6, 6),
            roomSizeMax = new int2(14, 14),

            borderPadding = 1,
            clearBeforeGenerate = true,
            ensureRoomsAtDeadEnds = true
        };

        [Test]
        public void CorridorFirst_SameSeedSameHash()
        {
            var cfg = DefaultCfg;
            var domain = new GridDomain2D(96, 96);

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            var rngA = LayoutSeedUtil.CreateRng(12345);
            var rngB = LayoutSeedUtil.CreateRng(12345);

            int endpointCount = math.max(1, cfg.corridorCount + 1);

            using var endpointsA = new NativeArray<int2>(endpointCount, Allocator.Temp);
            using var endpointsB = new NativeArray<int2>(endpointCount, Allocator.Temp);

            using var centersA = new NativeArray<int2>(math.max(endpointCount, 32), Allocator.Temp);
            using var centersB = new NativeArray<int2>(math.max(endpointCount, 32), Allocator.Temp);

            CorridorFirstDungeon2D.Generate(ref maskA, ref rngA, in cfg, endpointsA, centersA, out _);
            CorridorFirstDungeon2D.Generate(ref maskB, ref rngB, in cfg, endpointsB, centersB, out _);

            ulong hA = maskA.SnapshotHash64();
            ulong hB = maskB.SnapshotHash64();

            Assert.AreEqual(hA, hB);
        }

        [Test]
        public void CorridorFirst_DifferentSeedLikelyDifferentHash()
        {
            var cfg = DefaultCfg;
            var domain = new GridDomain2D(96, 96);

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            var rngA = LayoutSeedUtil.CreateRng(111);
            var rngB = LayoutSeedUtil.CreateRng(222);

            int endpointCount = math.max(1, cfg.corridorCount + 1);

            using var endpointsA = new NativeArray<int2>(endpointCount, Allocator.Temp);
            using var endpointsB = new NativeArray<int2>(endpointCount, Allocator.Temp);

            using var centersA = new NativeArray<int2>(math.max(endpointCount, 32), Allocator.Temp);
            using var centersB = new NativeArray<int2>(math.max(endpointCount, 32), Allocator.Temp);

            CorridorFirstDungeon2D.Generate(ref maskA, ref rngA, in cfg, endpointsA, centersA, out _);
            CorridorFirstDungeon2D.Generate(ref maskB, ref rngB, in cfg, endpointsB, centersB, out _);

            ulong hA = maskA.SnapshotHash64();
            ulong hB = maskB.SnapshotHash64();

            Assert.AreNotEqual(hA, hB);
        }

        [Test]
        public void CorridorFirst_GoldenHashGate()
        {
            var cfg = DefaultCfg;
            var domain = new GridDomain2D(96, 96);
            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            var rng = LayoutSeedUtil.CreateRng(12345);

            int endpointCount = math.max(1, cfg.corridorCount + 1);

            using var endpoints = new NativeArray<int2>(endpointCount, Allocator.Temp);
            using var centers = new NativeArray<int2>(math.max(endpointCount, 32), Allocator.Temp);

            CorridorFirstDungeon2D.Generate(ref mask, ref rng, in cfg, endpoints, centers, out _);

            ulong actual = mask.SnapshotHash64();

            // Lock-in pattern: set this once you visually validate Lantern output.
            const ulong GOLDEN = 0x5A75476E937F50FDUL;

            Assert.AreEqual(GOLDEN, actual);
        }
    }
}
