using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MapPipelineRunner2DGoldenF5Tests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 42u;

        // Set to values reported on first run.
        private const ulong ExpectedVegetationHash = 0xF3B336350FA3C892UL;

        [Test]
        public void Pipeline_F5_GoldenHash_IsLocked()
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
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages);

                ulong vegHash = ctx.GetLayer(MapLayerId.Vegetation)
                                   .SnapshotHash64(includeDimensions: true);

                if (ExpectedVegetationHash == 0UL)
                    Assert.Fail(
                        "F5 pipeline golden not initialized.\n" +
                        $"Set ExpectedVegetationHash = 0x{vegHash:X16}UL;");

                Assert.AreEqual(ExpectedVegetationHash, vegHash,
                    $"F5 pipeline Vegetation golden changed. Got=0x{vegHash:X16}");
            }
            finally { ctx.Dispose(); }
        }
    }
}