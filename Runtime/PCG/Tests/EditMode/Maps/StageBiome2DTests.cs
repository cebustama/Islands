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
    /// Tests for Stage_Biome2D (Phase M — Climate &amp; Biome Classification).
    ///
    /// Invariants tested:
    ///   M-1: Determinism (same seed + tunables → identical field hashes).
    ///   M-2: Water sentinel (Biome == 0f for all non-Land cells).
    ///   M-3: Land coverage (Biome > 0f for all Land cells).
    ///   M-4: Temperature range [0, 1].
    ///   M-5: Moisture range [0, 1].
    ///   M-6: Beach consistency (warm LandEdge cells → Beach biome).
    ///   M-7: No-mutate (Height, CoastDist, Land, LandEdge unchanged after execution).
    ///   M-8: Valid biome range (all Land biome values are valid BiomeType enum values).
    /// </summary>
    public sealed class StageBiome2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // Golden hashes — zero until first successful run captures values.
        private const ulong ExpectedTemperatureHash = 0x6D4398BDE2385AF8UL;
        private const ulong ExpectedMoistureHash = 0xAB0779B315B726BFUL;
        private const ulong ExpectedBiomeHash = 0xF20A7D056CEB37F3UL;

        // =================================================================
        // M-1: Determinism
        // =================================================================

        [Test]
        public void Stage_Biome2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong tempA, out ulong moistA, out ulong biomeA, out _);
            RunOnce(in inputs, out ulong tempB, out ulong moistB, out ulong biomeB, out _);

            Assert.AreEqual(tempA, tempB, "Temperature must be deterministic.");
            Assert.AreEqual(moistA, moistB, "Moisture must be deterministic.");
            Assert.AreEqual(biomeA, biomeB, "Biome must be deterministic.");
        }

        // =================================================================
        // Goldens
        // =================================================================

        [Test]
        public void Stage_Biome2D_TemperatureGolden()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong tempHash, out _, out _, out _);

            if (ExpectedTemperatureHash == 0UL)
                Assert.Fail(
                    "Temperature golden not initialized.\n" +
                    $"Set ExpectedTemperatureHash = 0x{tempHash:X16}UL;");

            Assert.AreEqual(ExpectedTemperatureHash, tempHash,
                $"Temperature golden changed. Got=0x{tempHash:X16}");
        }

        [Test]
        public void Stage_Biome2D_MoistureGolden()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out ulong moistHash, out _, out _);

            if (ExpectedMoistureHash == 0UL)
                Assert.Fail(
                    "Moisture golden not initialized.\n" +
                    $"Set ExpectedMoistureHash = 0x{moistHash:X16}UL;");

            Assert.AreEqual(ExpectedMoistureHash, moistHash,
                $"Moisture golden changed. Got=0x{moistHash:X16}");
        }

        [Test]
        public void Stage_Biome2D_BiomeGolden()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out ulong biomeHash, out _);

            if (ExpectedBiomeHash == 0UL)
                Assert.Fail(
                    "Biome golden not initialized.\n" +
                    $"Set ExpectedBiomeHash = 0x{biomeHash:X16}UL;");

            Assert.AreEqual(ExpectedBiomeHash, biomeHash,
                $"Biome golden changed. Got=0x{biomeHash:X16}");
        }

        // =================================================================
        // M-2: Water sentinel
        // =================================================================

        [Test]
        public void Stage_Biome2D_WaterSentinel()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);

                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (!land.GetUnchecked(x, y))
                            Assert.AreEqual(0f, biome.Values[y * W + x],
                                $"Non-Land cell ({x},{y}) must have Biome == 0f.");
                    }
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // M-3: Land coverage
        // =================================================================

        [Test]
        public void Stage_Biome2D_LandCoverage()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);

                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (land.GetUnchecked(x, y))
                            Assert.Greater(biome.Values[y * W + x], 0f,
                                $"Land cell ({x},{y}) must have Biome > 0f.");
                    }
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // M-4: Temperature range
        // =================================================================

        [Test]
        public void Stage_Biome2D_TemperatureRange()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                ref ScalarField2D temp = ref ctx.GetField(MapFieldId.Temperature);
                for (int i = 0; i < temp.Values.Length; i++)
                {
                    float v = temp.Values[i];
                    Assert.GreaterOrEqual(v, 0f, $"Temperature[{i}] below 0.");
                    Assert.LessOrEqual(v, 1f, $"Temperature[{i}] above 1.");
                }
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // M-5: Moisture range
        // =================================================================

        [Test]
        public void Stage_Biome2D_MoistureRange()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                ref ScalarField2D moist = ref ctx.GetField(MapFieldId.Moisture);
                for (int i = 0; i < moist.Values.Length; i++)
                {
                    float v = moist.Values[i];
                    Assert.GreaterOrEqual(v, 0f, $"Moisture[{i}] below 0.");
                    Assert.LessOrEqual(v, 1f, $"Moisture[{i}] above 1.");
                }
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // M-6: Beach consistency
        // =================================================================

        [Test]
        public void Stage_Biome2D_BeachOverride()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D landEdge = ref ctx.GetLayer(MapLayerId.LandEdge);
                ref ScalarField2D temp = ref ctx.GetField(MapFieldId.Temperature);
                ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);

                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (!land.GetUnchecked(x, y)) continue;

                        int idx = y * W + x;
                        bool isEdge = landEdge.GetUnchecked(x, y);
                        bool isWarm = temp.Values[idx] >= BiomeTable.BeachMinTemperature;

                        if (isEdge && isWarm)
                        {
                            Assert.AreEqual((float)BiomeType.Beach, biome.Values[idx],
                                $"Warm LandEdge cell ({x},{y}) must be Beach. " +
                                $"Temp={temp.Values[idx]:F3}, Got biome={(int)biome.Values[idx]}");
                        }
                    }
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // M-7: No-mutate
        // =================================================================

        [Test]
        public void Stage_Biome2D_NoMutate()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs,
                out ulong heightBefore, out ulong coastDistBefore,
                out ulong landBefore, out ulong landEdgeBefore,
                out _, out _, out _,
                out MapContext2D ctx);
            try
            {
                ulong heightAfter = HashScalarField(ref ctx.GetField(MapFieldId.Height));
                ulong coastDistAfter = HashScalarField(ref ctx.GetField(MapFieldId.CoastDist));
                ulong landAfter = ctx.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
                ulong landEdgeAfter = ctx.GetLayer(MapLayerId.LandEdge).SnapshotHash64(includeDimensions: true);

                Assert.AreEqual(heightBefore, heightAfter, "Stage_Biome2D must not mutate Height.");
                Assert.AreEqual(coastDistBefore, coastDistAfter, "Stage_Biome2D must not mutate CoastDist.");
                Assert.AreEqual(landBefore, landAfter, "Stage_Biome2D must not mutate Land.");
                Assert.AreEqual(landEdgeBefore, landEdgeAfter, "Stage_Biome2D must not mutate LandEdge.");
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // M-8: Valid biome range
        // =================================================================

        [Test]
        public void Stage_Biome2D_ValidBiomeRange()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);

                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        if (!land.GetUnchecked(x, y)) continue;

                        float bv = biome.Values[y * W + x];
                        int bi = (int)bv;
                        Assert.AreEqual((float)bi, bv,
                            $"Biome at ({x},{y}) must be an integer-as-float. Got {bv}.");
                        Assert.GreaterOrEqual(bi, 1,
                            $"Land biome at ({x},{y}) must be >= 1. Got {bi}.");
                        Assert.Less(bi, (int)BiomeType.COUNT,
                            $"Land biome at ({x},{y}) must be < COUNT ({(int)BiomeType.COUNT}). Got {bi}.");
                    }
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // Optional-phase tests
        // =================================================================

        [Test]
        public void Stage_Biome2D_RunsWithoutPhaseL()
        {
            // Phase L (FlowAccumulation) not present — moisture = coast+noise only.
            // Should not throw.
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong tempHash, out ulong moistHash, out ulong biomeHash, out MapContext2D ctx);
            try
            {
                // Sanity: all three fields were written.
                Assert.AreNotEqual(0UL, tempHash, "Temperature field should be non-trivial.");
                Assert.AreNotEqual(0UL, moistHash, "Moisture field should be non-trivial.");
                Assert.AreNotEqual(0UL, biomeHash, "Biome field should be non-trivial.");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void Stage_Biome2D_RunsWithoutPhaseJ()
        {
            // Phase J (RegionId) not present — per-cell classification only.
            // Should not throw. Identical to normal run since Phase J is not consumed.
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out _, out MapContext2D ctx);
            try
            {
                Assert.IsTrue(ctx.IsFieldCreated(MapFieldId.Biome),
                    "Biome field must be created without Phase J.");
            }
            finally { ctx.Dispose(); }
        }

        // =================================================================
        // Helpers
        // =================================================================

        /// <summary>
        /// Run the full pre-M pipeline (F2→F3→F4→F5→F6→G) plus Stage_Biome2D.
        /// Returns field hashes and the live context for invariant inspection.
        /// </summary>
        private static void RunOnce(
            in MapInputs inputs,
            out ulong tempHash,
            out ulong moistHash,
            out ulong biomeHash,
            out MapContext2D ctx)
        {
            RunOnce(in inputs,
                out _, out _, out _, out _,
                out tempHash, out moistHash, out biomeHash,
                out ctx);
        }

        /// <summary>
        /// Full RunOnce with pre-stage input hashes for no-mutate verification.
        /// </summary>
        private static void RunOnce(
            in MapInputs inputs,
            out ulong heightHash,
            out ulong coastDistHash,
            out ulong landHash,
            out ulong landEdgeHash,
            out ulong tempHash,
            out ulong moistHash,
            out ulong biomeHash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            // Run prerequisite stages: F2 → F3 → F4 → F5 → F6 → G.
            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);
            new Stage_Vegetation2D().Execute(ref ctx, in inputs);
            new Stage_Traversal2D().Execute(ref ctx, in inputs);
            new Stage_Morphology2D().Execute(ref ctx, in inputs);

            // Capture input hashes before Biome stage runs.
            heightHash = HashScalarField(ref ctx.GetField(MapFieldId.Height));
            coastDistHash = HashScalarField(ref ctx.GetField(MapFieldId.CoastDist));
            landHash = ctx.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
            landEdgeHash = ctx.GetLayer(MapLayerId.LandEdge).SnapshotHash64(includeDimensions: true);

            // Run Phase M.
            new Stage_Biome2D().Execute(ref ctx, in inputs);

            // Capture output hashes.
            tempHash = HashScalarField(ref ctx.GetField(MapFieldId.Temperature));
            moistHash = HashScalarField(ref ctx.GetField(MapFieldId.Moisture));
            biomeHash = HashScalarField(ref ctx.GetField(MapFieldId.Biome));
        }

        // -----------------------------------------------------------------------
        // FNV-1a scalar field hash — matches golden test pattern
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