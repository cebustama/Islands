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
    public sealed class MapPipelineRunner2DGoldenGTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 42u;

        // Set to values reported on first run.
        private const ulong ExpectedLandCoreHash = 0xE8AD63D8271FD20BUL;
        private const ulong ExpectedCoastDistHash = 0x7D0040328856B553UL;

        [Test]
        public void Pipeline_G_GoldenHash_IsLocked()
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
                };

                MapPipelineRunner2D.Run(ref ctx, in inputs, stages);

                ulong coreHash = ctx.GetLayer(MapLayerId.LandCore).SnapshotHash64(includeDimensions: true);
                ulong distHash = HashScalarField(ref ctx.GetField(MapFieldId.CoastDist));

                if (ExpectedLandCoreHash == 0UL)
                    Assert.Fail(
                        "Phase G pipeline LandCore golden not initialized.\n" +
                        $"Set ExpectedLandCoreHash = 0x{coreHash:X16}UL;");

                if (ExpectedCoastDistHash == 0UL)
                    Assert.Fail(
                        "Phase G pipeline CoastDist golden not initialized.\n" +
                        $"Set ExpectedCoastDistHash = 0x{distHash:X16}UL;");

                Assert.AreEqual(ExpectedLandCoreHash, coreHash,
                    $"Phase G pipeline LandCore golden changed. Got=0x{coreHash:X16}");
                Assert.AreEqual(ExpectedCoastDistHash, distHash,
                    $"Phase G pipeline CoastDist golden changed. Got=0x{distHash:X16}");
            }
            finally { ctx.Dispose(); }
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