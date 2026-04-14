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
    /// Pipeline golden for Phase M2.b — Biome Region Detection.
    /// Seed 42, 64×64, default tunables, default stage tunables.
    ///
    /// Does NOT invalidate M2a goldens (Temperature, Moisture, Biome unchanged).
    /// Stage_Regions2D appends BiomeRegionId as a new field; all prior outputs
    /// are read-only from its perspective (contract R-6).
    ///
    /// Two tests:
    ///   Pipeline_M2b_GoldenHash_IsLocked       — locks BiomeRegionId output hash.
    ///   Pipeline_M2b_DoesNotInvalidate_M2a      — confirms prior field hashes unchanged.
    /// </summary>
    public sealed class MapPipelineRunner2DGoldenM2bTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 42u;

        // ── Capture instructions ─────────────────────────────────────────────
        // Run with 0UL. The test fails and prints the real hash in the message.
        // Paste that value here, then re-run to lock.
        private const ulong ExpectedBiomeRegionIdHash = 0x6CAE7B67362E5D3DUL;

        // ── M2a hashes reproduced here for the no-invalidate cross-check ─────
        // These must match MapPipelineRunner2DGoldenM2Tests constants exactly.
        private const ulong ExpectedTemperatureHash = 0xB21849253CD4A4A5UL;
        private const ulong ExpectedMoistureHash = 0x4B4106BD43EAA656UL;
        private const ulong ExpectedBiomeHash = 0x83F2BC89009F7C13UL;

        // =====================================================================
        // Test 1 — BiomeRegionId golden
        // =====================================================================

        [Test]
        public void Pipeline_M2b_GoldenHash_IsLocked()
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
                    new Stage_Traversal2D(),
                    new Stage_Morphology2D(),
                    new Stage_Biome2D(),
                    new Stage_Vegetation2D(),
                    new Stage_Regions2D(),
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages);

                ulong regionHash = HashScalarField(ref ctx.GetField(MapFieldId.BiomeRegionId));

                if (ExpectedBiomeRegionIdHash == 0UL)
                    Assert.Fail(
                        "Phase M2.b BiomeRegionId golden not initialized.\n" +
                        $"Set ExpectedBiomeRegionIdHash = 0x{regionHash:X16}UL;");

                Assert.AreEqual(ExpectedBiomeRegionIdHash, regionHash,
                    $"Phase M2.b BiomeRegionId golden changed. Got=0x{regionHash:X16}");
            }
            finally { ctx.Dispose(); }
        }

        // =====================================================================
        // Test 2 — M2b does not invalidate M2a outputs
        // =====================================================================

        [Test]
        public void Pipeline_M2b_DoesNotInvalidate_M2a_Goldens()
        {
            // Verify Stage_Regions2D does not mutate Temperature, Moisture, or Biome
            // (contract R-6: no-mutate on all inputs).
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
                    new Stage_Traversal2D(),
                    new Stage_Morphology2D(),
                    new Stage_Biome2D(),
                    new Stage_Vegetation2D(),
                    new Stage_Regions2D(),
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages);

                ulong tempHash = HashScalarField(ref ctx.GetField(MapFieldId.Temperature));
                ulong moistHash = HashScalarField(ref ctx.GetField(MapFieldId.Moisture));
                ulong biomeHash = HashScalarField(ref ctx.GetField(MapFieldId.Biome));

                Assert.AreEqual(ExpectedTemperatureHash, tempHash,
                    "Stage_Regions2D must not change Temperature.");
                Assert.AreEqual(ExpectedMoistureHash, moistHash,
                    "Stage_Regions2D must not change Moisture.");
                Assert.AreEqual(ExpectedBiomeHash, biomeHash,
                    "Stage_Regions2D must not change Biome.");
            }
            finally { ctx.Dispose(); }
        }

        // =====================================================================
        // Helpers — identical to MapPipelineRunner2DGoldenM2Tests
        // =====================================================================

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