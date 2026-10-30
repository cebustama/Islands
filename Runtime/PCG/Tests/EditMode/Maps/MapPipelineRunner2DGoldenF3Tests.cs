using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class MapPipelineRunner2DGoldenF3Tests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // N5.e: zeroed for re-lock. Effective hillsThresholdL2 changed from 0.80 to
        // ~0.8005 due to remap float precision. HillsL1/L2 hashes differ; LandEdge/
        // LandInterior are structurally unaffected but re-locked together.
        // Re-anchored 2026-10-31, noise range A+D (PerlinOnSimplex + fBm sum); was 0x17D1FE919DCC3C33UL
        private const ulong ExpectedLandEdgeHash64 = 0x11E9570C8520F0DBUL;
        // Re-anchored 2026-10-31, noise range A+D (PerlinOnSimplex + fBm sum); was 0x228E6C047D7792EFUL
        private const ulong ExpectedLandInteriorHash64 = 0xD2ECCC8514180CABUL;
        // W-aux.f re-lock: height ceiling de-saturation + waterThreshold recalibration.
        // LandEdge / LandInterior verified UNCHANGED — Land topology survived the fix.
        // F3b′ re-anchor: HillsL1 0xCB0433856C6A4A3B -> 0xD2332B641B094A45.
        //                 HillsL2 0xA6E4DF1C6BE62012 -> 0x4767CE09E8D5C696.
        // Re-anchored 2026-10-31, noise range A+D (PerlinOnSimplex + fBm sum); was 0x532B332F40A43C82UL
        private const ulong ExpectedHillsL1Hash64 = 0xD2332B641B094A45UL;
        // Re-anchored 2026-10-31, noise range A+D (PerlinOnSimplex + fBm sum); was 0xB05ADEBE66AE4D5CUL
        private const ulong ExpectedHillsL2Hash64 = 0x4767CE09E8D5C696UL;

        [Test]
        public void MapPipelineRunner2D_GoldenHash_F3Pipeline_IsLocked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            var stages = new IMapStage2D[]
            {
                new Stage_BaseTerrain2D(),
                new Stage_Hills2D()
            };

            var ctx = new MapContext2D(domain, Allocator.Persistent);
            try
            {
                MapPipelineRunner2D.Run(ref ctx, in inputs, stages, clearLayers: true);

                ulong edgeHash = ctx.GetLayer(MapLayerId.LandEdge).SnapshotHash64(includeDimensions: true);
                ulong interiorHash = ctx.GetLayer(MapLayerId.LandInterior).SnapshotHash64(includeDimensions: true);
                ulong h1Hash = ctx.GetLayer(MapLayerId.HillsL1).SnapshotHash64(includeDimensions: true);
                ulong h2Hash = ctx.GetLayer(MapLayerId.HillsL2).SnapshotHash64(includeDimensions: true);

                if (ExpectedLandEdgeHash64 == 0UL || ExpectedLandInteriorHash64 == 0UL || ExpectedHillsL1Hash64 == 0UL || ExpectedHillsL2Hash64 == 0UL)
                {
                    Assert.Fail(
                        "F3/N5.e pipeline goldens are not initialized.\n" +
                        $"Set ExpectedLandEdgeHash64     = 0x{edgeHash:X16}UL;\n" +
                        $"Set ExpectedLandInteriorHash64 = 0x{interiorHash:X16}UL;\n" +
                        $"Set ExpectedHillsL1Hash64      = 0x{h1Hash:X16}UL;\n" +
                        $"Set ExpectedHillsL2Hash64      = 0x{h2Hash:X16}UL;\n");
                }

                Assert.AreEqual(ExpectedLandEdgeHash64, edgeHash, "F3 pipeline LandEdge golden changed.");
                Assert.AreEqual(ExpectedLandInteriorHash64, interiorHash, "F3 pipeline LandInterior golden changed.");
                Assert.AreEqual(ExpectedHillsL1Hash64, h1Hash, "F3 pipeline HillsL1 golden changed.");
                Assert.AreEqual(ExpectedHillsL2Hash64, h2Hash, "F3 pipeline HillsL2 golden changed.");
            }
            finally
            {
                ctx.Dispose();
            }
        }
    }
}