using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MapPipelineRunner2DGoldenF4Tests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // Set to the actual value reported by the test on first run.
        private const ulong ExpectedShallowWaterHash64 = 0xC24753CA1E06940FUL;

        [Test]
        public void MapPipelineRunner2D_GoldenHash_F4Pipeline_IsLocked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            var stages = new IMapStage2D[]
            {
                new Stage_BaseTerrain2D(),
                new Stage_Hills2D(),
                new Stage_Shore2D()
            };

            var ctx = new MapContext2D(domain, Allocator.Persistent);
            try
            {
                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: true);

                ulong shallowHash = ctx.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(includeDimensions: true);

                if (ExpectedShallowWaterHash64 == 0UL)
                {
                    Assert.Fail(
                        "F4 pipeline golden is not initialized.\n" +
                        $"Set ExpectedShallowWaterHash64 = 0x{shallowHash:X16}UL;");
                }

                Assert.AreEqual(ExpectedShallowWaterHash64, shallowHash,
                    $"F4 pipeline ShallowWater golden changed. Got=0x{shallowHash:X16} Expected=0x{ExpectedShallowWaterHash64:X16}");
            }
            finally
            {
                ctx.Dispose();
            }
        }
    }
}