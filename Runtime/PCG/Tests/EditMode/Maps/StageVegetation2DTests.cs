using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class StageVegetation2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // Set to value reported on first run.
        private const ulong ExpectedVegetationHash64 = 0xE7876A1519EC45D3UL;

        // -----------------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Vegetation2D_IsDeterministic()
        {
            var inputs = MakeInputs();
            RunOnce(in inputs, out ulong hashA, out _);
            RunOnce(in inputs, out ulong hashB, out _);
            Assert.AreEqual(hashA, hashB,
                "Stage_Vegetation2D must produce identical Vegetation on repeated runs.");
        }

        // -----------------------------------------------------------------------
        // Invariants
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Vegetation2D_Invariants_Hold()
        {
            var inputs = MakeInputs();
            RunOnce(in inputs, out _, out MapContext2D ctx);
            try
            {
                var domain = new GridDomain2D(W, H);

                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D landInterior = ref ctx.GetLayer(MapLayerId.LandInterior);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
                ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);
                ref MaskGrid2D vegetation = ref ctx.GetLayer(MapLayerId.Vegetation);

                // Hash upstream layers before assertions (no-mutate baseline)
                ulong landHash = land.SnapshotHash64(includeDimensions: true);
                ulong intHash = landInterior.SnapshotHash64(includeDimensions: true);
                ulong hillsHash = hillsL2.SnapshotHash64(includeDimensions: true);
                ulong shoreHash = shallowWater.SnapshotHash64(includeDimensions: true);

                var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    // Vegetation ⊆ Land
                    scratch.CopyFrom(vegetation);
                    scratch.AndNot(land);   // scratch = Vegetation AND NOT Land
                    Assert.AreEqual(0, scratch.CountOnes(), "Vegetation must be a subset of Land.");

                    // Vegetation ⊆ LandInterior
                    scratch.CopyFrom(vegetation);
                    scratch.AndNot(landInterior);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Vegetation must be a subset of LandInterior.");

                    // Vegetation ∩ HillsL2 == ∅
                    scratch.CopyFrom(vegetation);
                    scratch.And(hillsL2);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Vegetation must not overlap HillsL2.");

                    // Vegetation ∩ ShallowWater == ∅
                    scratch.CopyFrom(vegetation);
                    scratch.And(shallowWater);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "Vegetation must not overlap ShallowWater.");

                    // No-mutate checks
                    Assert.AreEqual(landHash, land.SnapshotHash64(includeDimensions: true),
                        "Stage_Vegetation2D must not mutate Land.");
                    Assert.AreEqual(intHash, landInterior.SnapshotHash64(includeDimensions: true),
                        "Stage_Vegetation2D must not mutate LandInterior.");
                    Assert.AreEqual(hillsHash, hillsL2.SnapshotHash64(includeDimensions: true),
                        "Stage_Vegetation2D must not mutate HillsL2.");
                    Assert.AreEqual(shoreHash, shallowWater.SnapshotHash64(includeDimensions: true),
                        "Stage_Vegetation2D must not mutate ShallowWater.");
                }
                finally { scratch.Dispose(); }
            }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Golden hash gate
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Vegetation2D_GoldenHash_IsLocked()
        {
            var inputs = MakeInputs();
            RunOnce(in inputs, out ulong vegHash, out MapContext2D ctx);
            try
            {
                if (ExpectedVegetationHash64 == 0UL)
                    Assert.Fail(
                        "F5 stage golden not initialized.\n" +
                        $"Set ExpectedVegetationHash64 = 0x{vegHash:X16}UL;");

                Assert.AreEqual(ExpectedVegetationHash64, vegHash,
                    $"Vegetation golden changed. Got=0x{vegHash:X16} Expected=0x{ExpectedVegetationHash64:X16}");
            }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static MapInputs MakeInputs()
        {
            var domain = new GridDomain2D(W, H);
            return new MapInputs(Seed, domain, MapTunables2D.Default);
        }

        private static void RunOnce(
            in MapInputs inputs,
            out ulong vegetationHash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);
            new Stage_Vegetation2D().Execute(ref ctx, in inputs);

            vegetationHash = ctx.GetLayer(MapLayerId.Vegetation)
                               .SnapshotHash64(includeDimensions: true);
        }
    }
}