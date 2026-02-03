using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Generators;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MapPipelineRunner2DTests
    {
        private sealed class RectLandStage : IMapStage2D
        {
            public string Name => "rect_land_stage";

            private readonly int xMin, yMin, xMax, yMax;

            public RectLandStage(int xMin, int yMin, int xMax, int yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }

            public void Execute(ref MapContext2D ctx, in MapInputs inputs)
            {
                ref var land = ref ctx.EnsureLayer(MapLayerId.Land, clearToZero: true);

                // Deterministic + OOB-safe (clampToDomain=true).
                RectFillGenerator.FillRect(ref land, xMin, yMin, xMax, yMax, value: true, clampToDomain: true);
            }
        }

        [Test]
        public void MapPipelineRunner2D_IsDeterministic_WithTrivialStage()
        {
            var domain = new GridDomain2D(8, 8);
            var inputs = new MapInputs(seed: 123u, domain: domain, tunables: MapTunables2D.Default);

            var stages = new IMapStage2D[]
            {
                new RectLandStage(xMin: 2, yMin: 1, xMax: 6, yMax: 4)
            };

            var ctxA = new MapContext2D(domain, Allocator.Persistent);
            var ctxB = new MapContext2D(domain, Allocator.Persistent);

            try
            {
                MapPipelineRunner2D.Run(ref ctxA, in inputs, stages, clearLayers: true);
                MapPipelineRunner2D.Run(ref ctxB, in inputs, stages, clearLayers: true);

                ulong hA = ctxA.GetLayer(MapLayerId.Land).SnapshotHash64();
                ulong hB = ctxB.GetLayer(MapLayerId.Land).SnapshotHash64();

                Assert.AreEqual(hA, hB, "Same inputs/stages must produce identical SnapshotHash64.");
            }
            finally
            {
                ctxA.Dispose();
                ctxB.Dispose();
            }
        }

        [Test]
        public void MapPipelineRunner2D_GoldenHash_TrivialPipeline_IsLocked()
        {
            // This constant is computed for:
            // domain = 8x8
            // rect fill = [2,6) x [1,4) into Land
            // using MaskGrid2D.SnapshotHash64 (FNV-1a over width/height/len + word data)
            const ulong Expected = 0x1D50A4E5AC05D88Ful;

            var domain = new GridDomain2D(8, 8);
            var inputs = new MapInputs(seed: 999u, domain: domain, tunables: MapTunables2D.Default);

            var stages = new IMapStage2D[]
            {
                new RectLandStage(xMin: 2, yMin: 1, xMax: 6, yMax: 4)
            };

            var ctx = new MapContext2D(domain, Allocator.Persistent);

            try
            {
                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: true);
                ulong got = ctx.GetLayer(MapLayerId.Land).SnapshotHash64();

                Assert.AreEqual(Expected, got, "Golden hash changed: behavior drift or contract broke.");
            }
            finally
            {
                ctx.Dispose();
            }
        }
    }
}
