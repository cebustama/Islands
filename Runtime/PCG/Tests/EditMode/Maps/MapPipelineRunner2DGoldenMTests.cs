using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Pipeline golden for the full F0–M pipeline.
    /// Seed 42, 64×64, default tunables, default biome stage tunables.
    ///
    /// Does NOT invalidate existing F0–G goldens. Phase M appends three new fields
    /// (Temperature, Moisture, Biome) without modifying any prior stage outputs.
    /// </summary>
    public sealed class MapPipelineRunner2DGoldenMTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 42u;

        // Set to values reported on first run.
        private const ulong ExpectedTemperatureHash = 0xB21849253CD4A4A5UL;
        private const ulong ExpectedMoistureHash = 0x4B4106BD43EAA656UL;
        private const ulong ExpectedBiomeHash = 0x83F2BC89009F7C13UL;

        [Test]
        public void Pipeline_M_GoldenHash_IsLocked()
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
                    new Stage_Morphology2D(),
                    new Stage_Biome2D(),
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages);

                ulong tempHash = HashScalarField(ref ctx.GetField(MapFieldId.Temperature));
                ulong moistHash = HashScalarField(ref ctx.GetField(MapFieldId.Moisture));
                ulong biomeHash = HashScalarField(ref ctx.GetField(MapFieldId.Biome));

                if (ExpectedTemperatureHash == 0UL)
                    Assert.Fail(
                        "Phase M pipeline Temperature golden not initialized.\n" +
                        $"Set ExpectedTemperatureHash = 0x{tempHash:X16}UL;");

                if (ExpectedMoistureHash == 0UL)
                    Assert.Fail(
                        "Phase M pipeline Moisture golden not initialized.\n" +
                        $"Set ExpectedMoistureHash = 0x{moistHash:X16}UL;");

                if (ExpectedBiomeHash == 0UL)
                    Assert.Fail(
                        "Phase M pipeline Biome golden not initialized.\n" +
                        $"Set ExpectedBiomeHash = 0x{biomeHash:X16}UL;");

                Assert.AreEqual(ExpectedTemperatureHash, tempHash,
                    $"Phase M pipeline Temperature golden changed. Got=0x{tempHash:X16}");
                Assert.AreEqual(ExpectedMoistureHash, moistHash,
                    $"Phase M pipeline Moisture golden changed. Got=0x{moistHash:X16}");
                Assert.AreEqual(ExpectedBiomeHash, biomeHash,
                    $"Phase M pipeline Biome golden changed. Got=0x{biomeHash:X16}");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void Pipeline_M_DoesNotInvalidate_G_Goldens()
        {
            // Verify that adding Stage_Biome2D to the pipeline does not change
            // any prior stage outputs (Height, Land, CoastDist, LandCore, etc.).
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            // Run without M.
            var ctxG = new MapContext2D(domain, Allocator.Persistent);
            try
            {
                var stagesG = new IMapStage2D[]
                {
                    new Stage_BaseTerrain2D(),
                    new Stage_Hills2D(),
                    new Stage_Shore2D(),
                    new Stage_Vegetation2D(),
                    new Stage_Traversal2D(),
                    new Stage_Morphology2D(),
                };
                MapPipelineRunner2D.Run(ref ctxG, in inputs, stagesG);

                ulong landHashG = ctxG.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
                ulong coreHashG = ctxG.GetLayer(MapLayerId.LandCore).SnapshotHash64(includeDimensions: true);
                ulong heightHashG = HashScalarField(ref ctxG.GetField(MapFieldId.Height));
                ulong coastHashG = HashScalarField(ref ctxG.GetField(MapFieldId.CoastDist));

                // Run with M.
                var ctxM = new MapContext2D(domain, Allocator.Persistent);
                try
                {
                    var stagesM = new IMapStage2D[]
                    {
                        new Stage_BaseTerrain2D(),
                        new Stage_Hills2D(),
                        new Stage_Shore2D(),
                        new Stage_Vegetation2D(),
                        new Stage_Traversal2D(),
                        new Stage_Morphology2D(),
                        new Stage_Biome2D(),
                    };
                    MapPipelineRunner2D.Run(ref ctxM, in inputs, stagesM);

                    ulong landHashM = ctxM.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
                    ulong coreHashM = ctxM.GetLayer(MapLayerId.LandCore).SnapshotHash64(includeDimensions: true);
                    ulong heightHashM = HashScalarField(ref ctxM.GetField(MapFieldId.Height));
                    ulong coastHashM = HashScalarField(ref ctxM.GetField(MapFieldId.CoastDist));

                    Assert.AreEqual(landHashG, landHashM, "Adding Stage_Biome2D must not change Land.");
                    Assert.AreEqual(coreHashG, coreHashM, "Adding Stage_Biome2D must not change LandCore.");
                    Assert.AreEqual(heightHashG, heightHashM, "Adding Stage_Biome2D must not change Height.");
                    Assert.AreEqual(coastHashG, coastHashM, "Adding Stage_Biome2D must not change CoastDist.");
                }
                finally { ctxM.Dispose(); }
            }
            finally { ctxG.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static ulong HashScalarField(ref ScalarField2D field)
        {
            const ulong fnvOffset = 1469598103934665603UL;
            const ulong fnvPrime = 1099511628211UL;

            ulong h = fnvOffset;
            h = FnvMixU64(h, (ulong)field.Domain.Width, fnvPrime);
            h = FnvMixU64(h, (ulong)field.Domain.Height, fnvPrime);
            h = FnvMixU64(h, (ulong)field.Domain.Length, fnvPrime);

            for (int i = 0; i < field.Values.Length; i++)
            {
                uint bits = math.asuint(field.Values[i]);
                h = FnvMixU64(h, (ulong)bits, fnvPrime);
            }

            return h;
        }

        private static ulong FnvMixU64(ulong h, ulong value, ulong fnvPrime)
        {
            h ^= (byte)(value); h *= fnvPrime;
            h ^= (byte)(value >> 8); h *= fnvPrime;
            h ^= (byte)(value >> 16); h *= fnvPrime;
            h ^= (byte)(value >> 24); h *= fnvPrime;
            h ^= (byte)(value >> 32); h *= fnvPrime;
            h ^= (byte)(value >> 40); h *= fnvPrime;
            h ^= (byte)(value >> 48); h *= fnvPrime;
            h ^= (byte)(value >> 56); h *= fnvPrime;
            return h;
        }
    }
}