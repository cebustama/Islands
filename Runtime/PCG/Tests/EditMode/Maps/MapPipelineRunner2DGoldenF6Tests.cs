using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MapPipelineRunner2DGoldenF6Tests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 42u;

        // N5.e: zeroed for re-lock. Walkable/Stairs depend on HillsL2 which shifts
        // due to the hills threshold remap (effective L2 ≈ 0.8005 vs old 0.80).
        private const ulong ExpectedWalkableHash = 0xA9A213FFB5842CF7UL;
        private const ulong ExpectedStairsHash = 0x678993F8298D975FUL;

        [Test]
        public void Pipeline_F6_GoldenHash_IsLocked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            var ctx = new MapContext2D(domain, Allocator.Persistent);
            try
            {
                var stages = new IMapStage2D[]
                {
                    new Stage_BaseTerrain2D(),
                    new Stage_Hills2D(),
                    new Stage_Shore2D(),
                    new Stage_Vegetation2D(),
                    new Stage_Traversal2D(),
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages);

                ulong walkHash = ctx.GetLayer(MapLayerId.Walkable).SnapshotHash64(includeDimensions: true);
                ulong stairHash = ctx.GetLayer(MapLayerId.Stairs).SnapshotHash64(includeDimensions: true);

                if (ExpectedWalkableHash == 0UL)
                    Assert.Fail(
                        "F6/N5.e pipeline Walkable golden not initialized.\n" +
                        $"Set ExpectedWalkableHash = 0x{walkHash:X16}UL;");

                if (ExpectedStairsHash == 0UL)
                    Assert.Fail(
                        "F6/N5.e pipeline Stairs golden not initialized.\n" +
                        $"Set ExpectedStairsHash = 0x{stairHash:X16}UL;");

                Assert.AreEqual(ExpectedWalkableHash, walkHash,
                    $"F6 pipeline Walkable golden changed. Got=0x{walkHash:X16}");
                Assert.AreEqual(ExpectedStairsHash, stairHash,
                    $"F6 pipeline Stairs golden changed. Got=0x{stairHash:X16}");
            }
            finally { ctx.Dispose(); }
        }
    }
}