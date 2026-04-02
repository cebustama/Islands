using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// F2c — Pipeline-level golden for Stage_BaseTerrain2D with external shape input.
    ///
    /// The no-shape-input path is covered by MapPipelineRunner2DGoldenF2Tests (unchanged).
    /// This file covers only the F2c shape-input path.
    ///
    /// Golden hashes: placeholder 0x0. Run once; the test will fail and print actual hashes.
    /// Copy/paste to lock.
    /// </summary>
    public sealed class MapPipelineRunner2DGoldenF2cTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // TODO: fill after first run.
        private const ulong ExpectedLandHash64 = 0xD986402B40273547UL;
        private const ulong ExpectedDeepWaterHash64 = 0xD5F1514F5471CC2FUL;

        [Test]
        public void MapPipelineRunner2D_GoldenHash_F2cShapePath_IsLocked()
        {
            var domain = new GridDomain2D(W, H);
            var shape = BuildCenterCircleMask(domain, radius: 20);

            try
            {
                var inputs = new MapInputs(Seed, domain, MapTunables2D.Default,
                    new MapShapeInput(shape));

                var stages = new IMapStage2D[]
                {
                    new Stage_BaseTerrain2D()
                };

                var ctx = new MapContext2D(domain, Allocator.Persistent);
                try
                {
                    ctx.BeginRun(in inputs, clearLayers: true);

                    foreach (var stage in stages)
                        stage.Execute(ref ctx, in inputs);

                    ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                    ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);

                    ulong landHash = land.SnapshotHash64(includeDimensions: true);
                    ulong deepHash = deep.SnapshotHash64(includeDimensions: true);

                    if (ExpectedLandHash64 == 0UL || ExpectedDeepWaterHash64 == 0UL)
                    {
                        Assert.Fail(
                            "F2c pipeline goldens not initialized.\n" +
                            $"Set ExpectedLandHash64      = 0x{landHash:X16}UL;\n" +
                            $"Set ExpectedDeepWaterHash64 = 0x{deepHash:X16}UL;");
                    }

                    Assert.AreEqual(ExpectedLandHash64, landHash,
                        $"F2c Land golden changed. Got=0x{landHash:X16} Expected=0x{ExpectedLandHash64:X16}");
                    Assert.AreEqual(ExpectedDeepWaterHash64, deepHash,
                        $"F2c DeepWater golden changed. Got=0x{deepHash:X16} Expected=0x{ExpectedDeepWaterHash64:X16}");
                }
                finally { ctx.Dispose(); }
            }
            finally { shape.Dispose(); }
        }

        private static MaskGrid2D BuildCenterCircleMask(GridDomain2D domain, float radius)
        {
            var mask = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            float cx = domain.Width * 0.5f;
            float cy = domain.Height * 0.5f;
            float r2 = radius * radius;

            for (int y = 0; y < domain.Height; y++)
                for (int x = 0; x < domain.Width; x++)
                {
                    float dx = (x + 0.5f) - cx;
                    float dy = (y + 0.5f) - cy;
                    mask.SetUnchecked(x, y, dx * dx + dy * dy <= r2);
                }
            return mask;
        }
    }
}