using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// F2.2 — Pipeline-level golden for the F2 base terrain stage.
    ///
    /// This keeps the old "trivial pipeline" golden test intact (drift alarm),
    /// and adds a second golden that runs the real F2 stage through MapPipelineRunner2D.
    ///
    /// Determinism hazards avoided:
    /// - Stages executed in stable array order.
    /// - Goldens are based on MaskGrid2D bit hashes (SnapshotHash64), not float fields.
    /// - No unordered collection traversal is relied upon by this test.
    /// </summary>
    public sealed class MapPipelineRunner2DGoldenF2Tests
    {
        // Keep stable (must match StageBaseTerrain2DTests to reduce golden maintenance).
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // ---------------------------------------------------------------------
        // GOLDENS (F2.2)
        // ---------------------------------------------------------------------
        // These should match the stage-level goldens (F2.1). If you intentionally
        // change the stage behavior, update both places together.
        private const ulong ExpectedLandHash64 = 0x56F997102CA872E7UL;
        private const ulong ExpectedDeepWaterHash64 = 0x451D80227667D2A7UL;

        [Test]
        public void MapPipelineRunner2D_GoldenHash_F2Pipeline_IsLocked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            var stages = new IMapStage2D[]
            {
                new Stage_BaseTerrain2D()
            };

            var ctx = new MapContext2D(domain, Allocator.Persistent);

            try
            {
                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: true);

                ulong landHash = ctx.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
                ulong deepHash = ctx.GetLayer(MapLayerId.DeepWater).SnapshotHash64(includeDimensions: true);

                // If goldens are not set yet, fail once with copy/paste values.
                if (ExpectedLandHash64 == 0UL || ExpectedDeepWaterHash64 == 0UL)
                {
                    Assert.Fail(
                        "F2 pipeline goldens are not initialized.\n" +
                        $"Set ExpectedLandHash64      = 0x{landHash:X16}UL;\n" +
                        $"Set ExpectedDeepWaterHash64 = 0x{deepHash:X16}UL;\n");
                }

                Assert.AreEqual(
                    ExpectedLandHash64, landHash,
                    $"F2 pipeline Land golden changed. Got=0x{landHash:X16} Expected=0x{ExpectedLandHash64:X16}");

                Assert.AreEqual(
                    ExpectedDeepWaterHash64, deepHash,
                    $"F2 pipeline DeepWater golden changed. Got=0x{deepHash:X16} Expected=0x{ExpectedDeepWaterHash64:X16}");
            }
            finally
            {
                ctx.Dispose();
            }
        }
    }
}
