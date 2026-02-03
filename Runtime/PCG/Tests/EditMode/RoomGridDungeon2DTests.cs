using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;

namespace Islands.PCG.Tests.EditMode
{
    public class RoomGridDungeon2DTests
    {
        private static RoomGridDungeon2D.RoomGridConfig DefaultCfg => new RoomGridDungeon2D.RoomGridConfig
        {
            roomCount = 16,
            cellSize = 10,
            borderPadding = 1,

            roomSizeMin = new int2(6, 6),
            roomSizeMax = new int2(14, 14),

            corridorBrushRadius = 0,
            connectWithManhattan = true,
            clearBeforeGenerate = true
        };

        [Test]
        public void RoomGrid_SameSeedSameHash()
        {
            var cfg = DefaultCfg;
            var domain = new GridDomain2D(96, 96);

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            var rngA = LayoutSeedUtil.CreateRng(12345);
            var rngB = LayoutSeedUtil.CreateRng(12345);

            int take = math.max(1, cfg.roomCount);
            using var pickedA = new NativeArray<int>(take, Allocator.Temp);
            using var pickedB = new NativeArray<int>(take, Allocator.Temp);

            using var centersA = new NativeArray<int2>(take, Allocator.Temp);
            using var centersB = new NativeArray<int2>(take, Allocator.Temp);

            RoomGridDungeon2D.Generate(ref maskA, ref rngA, in cfg, pickedA, centersA, out _);
            RoomGridDungeon2D.Generate(ref maskB, ref rngB, in cfg, pickedB, centersB, out _);

            Assert.AreEqual(maskA.SnapshotHash64(), maskB.SnapshotHash64());
        }

        [Test]
        public void RoomGrid_DifferentSeedLikelyDifferentHash()
        {
            var cfg = DefaultCfg;
            var domain = new GridDomain2D(96, 96);

            var maskA = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var maskB = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);

            var rngA = LayoutSeedUtil.CreateRng(111);
            var rngB = LayoutSeedUtil.CreateRng(222);

            int take = math.max(1, cfg.roomCount);
            using var pickedA = new NativeArray<int>(take, Allocator.Temp);
            using var pickedB = new NativeArray<int>(take, Allocator.Temp);

            using var centersA = new NativeArray<int2>(take, Allocator.Temp);
            using var centersB = new NativeArray<int2>(take, Allocator.Temp);

            RoomGridDungeon2D.Generate(ref maskA, ref rngA, in cfg, pickedA, centersA, out _);
            RoomGridDungeon2D.Generate(ref maskB, ref rngB, in cfg, pickedB, centersB, out _);

            Assert.AreNotEqual(maskA.SnapshotHash64(), maskB.SnapshotHash64());
        }

        [Test]
        public void RoomGrid_GoldenHashGate()
        {
            var cfg = DefaultCfg;
            var domain = new GridDomain2D(96, 96);

            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            var rng = LayoutSeedUtil.CreateRng(12345);

            int take = math.max(1, cfg.roomCount);
            using var picked = new NativeArray<int>(take, Allocator.Temp);
            using var centers = new NativeArray<int2>(take, Allocator.Temp);

            RoomGridDungeon2D.Generate(ref mask, ref rng, in cfg, picked, centers, out _);

            ulong actual = mask.SnapshotHash64();

            // Set this once after Lantern visual verification.
            const ulong GOLDEN = 0x7AFA78556BFC1734UL;

            Assert.AreEqual(GOLDEN, actual);
        }
    }
}
