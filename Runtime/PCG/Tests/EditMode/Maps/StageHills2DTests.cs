using NUnit.Framework;
using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Operators;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Tests for Stage_Hills2D (F3b — height-threshold classification).
    /// N5.d additions: hills noise modulation tests.
    /// N5.e additions: hills threshold UX remap tests.
    ///
    /// Contract changes from F3 → F3b:
    /// - HillsL1 and HillsL2 are now disjoint (was HillsL2 ⊆ HillsL1).
    /// - HillsL1/L2 ⊆ Land (was HillsL1 ⊆ LandInterior).
    /// - Hills are derived from Height field thresholds (was independent noise on LandInterior).
    ///
    /// F3b′: hillsL1/L2 are AREA fractions of Land; per-run thresholds computed
    /// by HillsThresholdOps2D inside the stage (order statistics over Land).
    /// f2 clamped <= f1 (peaks ⊆ hills budget).
    /// </summary>
    public sealed class StageHills2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // N5.e golden hashes — zero until first successful run captures new values.
        // Phase N5.e changes effective hillsThresholdL2 from 0.80 to ~0.8005,
        // breaking HillsL1/L2 hashes. LandEdge/LandInterior are unaffected but
        // re-locked together for a clean capture cycle.
        private const ulong ExpectedLandEdgeHash64 = 0x17D1FE919DCC3C33UL;
        private const ulong ExpectedLandInteriorHash64 = 0x228E6C047D7792EFUL;
        // F3b′ re-anchor: HillsL1 0xCB0433856C6A4A3B -> 0x532B332F40A43C82.
        //                 HillsL2 0xA6E4DF1C6BE62012 -> 0xB05ADEBE66AE4D5C.
        // LandEdge / LandInterior unchanged — hills does not derive them.
        private const ulong ExpectedHillsL1Hash64 = 0x532B332F40A43C82UL;
        private const ulong ExpectedHillsL2Hash64 = 0xB05ADEBE66AE4D5CUL;

        [Test]
        public void Stage_Hills2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong edgeA, out ulong interiorA, out ulong h1A, out ulong h2A, out _);
            RunOnce(in inputs, out ulong edgeB, out ulong interiorB, out ulong h1B, out ulong h2B, out _);

            Assert.AreEqual(edgeA, edgeB, "LandEdge must be deterministic.");
            Assert.AreEqual(interiorA, interiorB, "LandInterior must be deterministic.");
            Assert.AreEqual(h1A, h1B, "HillsL1 must be deterministic.");
            Assert.AreEqual(h2A, h2B, "HillsL2 must be deterministic.");
        }

        [Test]
        public void Stage_Hills2D_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong landBefore, out ulong deepBefore,
                    out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);
                ref MaskGrid2D edge = ref ctx.GetLayer(MapLayerId.LandEdge);
                ref MaskGrid2D interior = ref ctx.GetLayer(MapLayerId.LandInterior);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);

                var temp = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

                try
                {
                    // LandEdge ∪ LandInterior == Land
                    temp.CopyFrom(edge);
                    temp.Or(interior);
                    Assert.AreEqual(land.SnapshotHash64(includeDimensions: true),
                                    temp.SnapshotHash64(includeDimensions: true),
                                    "LandEdge ∪ LandInterior must equal Land.");

                    // LandEdge ∩ LandInterior == ∅
                    temp.CopyFrom(edge);
                    temp.And(interior);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "LandEdge and LandInterior must be disjoint.");

                    // HillsL1 ⊆ Land
                    temp.CopyFrom(hillsL1);
                    temp.AndNot(land);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL1 must be a subset of Land.");

                    // HillsL2 ⊆ Land
                    temp.CopyFrom(hillsL2);
                    temp.AndNot(land);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL2 must be a subset of Land.");

                    // HillsL1 ∩ HillsL2 == ∅ (F3b: disjoint, not overlapping)
                    temp.CopyFrom(hillsL1);
                    temp.And(hillsL2);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL1 and HillsL2 must be disjoint.");

                    // No-mutate: Land, DeepWater unchanged.
                    // Height no-mutate is guaranteed by construction (Stage_Hills2D only
                    // reads Height, never calls EnsureField(Height)). Land hash stability
                    // is the primary gate — if Height changed, Land would change too
                    // (Land = Height >= waterThreshold in Stage_BaseTerrain2D).
                    Assert.AreEqual(landBefore, land.SnapshotHash64(includeDimensions: true),
                                    "Stage_Hills2D must not mutate Land.");
                    Assert.AreEqual(deepBefore, deep.SnapshotHash64(includeDimensions: true),
                                    "Stage_Hills2D must not mutate DeepWater.");
                }
                finally
                {
                    temp.Dispose();
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_Hills2D_HeightCoherence()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = MapTunables2D.Default;
            var inputs = new MapInputs(Seed, domain, tunables);

            RunOnce(in inputs, out _, out _, out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
                ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);

                // F3b′: thresholds are per-run quantiles — recompute via the same
                // operator the stage uses (blend = 0 in Default, so classification
                // must match these thresholds exactly).
                HillsThresholdOps2D.ComputeAreaThresholds(
                    in height, in land, tunables.hillsL1, tunables.hillsL2,
                    out float thL1, out float thL2);

                int w = domain.Width;
                int h = domain.Height;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!land.GetUnchecked(x, y))
                            continue;

                        float hv = height.GetUnchecked(x, y);
                        bool isL1 = hillsL1.GetUnchecked(x, y);
                        bool isL2 = hillsL2.GetUnchecked(x, y);

                        if (isL2)
                        {
                            Assert.IsTrue(hv >= thL2,
                                $"HillsL2 cell ({x},{y}) has Height {hv:F4} < thresholdL2 {thL2:F4}.");
                        }
                        else if (isL1)
                        {
                            Assert.IsTrue(hv >= thL1,
                                $"HillsL1 cell ({x},{y}) has Height {hv:F4} < thresholdL1 {thL1:F4}.");
                            Assert.IsTrue(hv < thL2,
                                $"HillsL1 cell ({x},{y}) has Height {hv:F4} >= thresholdL2 {thL2:F4} — should be HillsL2.");
                        }
                        else
                        {
                            Assert.IsTrue(hv < thL1,
                                $"Non-hill Land cell ({x},{y}) has Height {hv:F4} >= thresholdL1 {thL1:F4} — should be HillsL1.");
                        }
                    }
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // =================================================================
        // F3b′ — Area-fraction tunables (quantile thresholds)
        // =================================================================

        [Test]
        public void F3bPrime_Tunables_StoreClampedAreaFractions()
        {
            var tunables = new MapTunables2D(
                islandRadius01: 0.45f,
                waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f,
                islandSmoothTo01: 0.70f,
                hillsL1: 0.60f,
                hillsL2: 0.25f);

            Assert.AreEqual(0.60f, tunables.hillsL1, 1e-6f,
                "hillsL1 is stored as a raw area fraction (no remap).");
            Assert.AreEqual(0.25f, tunables.hillsL2, 1e-6f,
                "hillsL2 is stored as a raw area fraction (no remap).");
        }

        [Test]
        public void F3bPrime_Tunables_L2ClampedToL1()
        {
            // Peaks are a subset of the hills budget: f2 > f1 clamps to f1.
            var tunables = new MapTunables2D(
                islandRadius01: 0.45f,
                waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f,
                islandSmoothTo01: 0.70f,
                hillsL1: 0.30f,
                hillsL2: 0.90f);

            Assert.AreEqual(0.30f, tunables.hillsL2, 1e-6f,
                "hillsL2 must be clamped to hillsL1 (peaks ⊆ hills budget).");
            Assert.LessOrEqual(tunables.hillsL2, tunables.hillsL1);
        }

        // =================================================================
        // Golden hashes
        // =================================================================

        [Test]
        public void Stage_Hills2D_GoldenHashes_Locked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _,
                    out ulong edgeHash, out ulong interiorHash, out ulong h1Hash, out ulong h2Hash,
                    out MapContext2D ctx);

            try
            {
                if (ExpectedLandEdgeHash64 == 0UL || ExpectedLandInteriorHash64 == 0UL ||
                    ExpectedHillsL1Hash64 == 0UL || ExpectedHillsL2Hash64 == 0UL)
                {
                    Assert.Fail(
                        "F3b/N5.e stage goldens are not initialized.\n" +
                        $"Set ExpectedLandEdgeHash64     = 0x{edgeHash:X16}UL;\n" +
                        $"Set ExpectedLandInteriorHash64 = 0x{interiorHash:X16}UL;\n" +
                        $"Set ExpectedHillsL1Hash64      = 0x{h1Hash:X16}UL;\n" +
                        $"Set ExpectedHillsL2Hash64      = 0x{h2Hash:X16}UL;\n");
                }

                Assert.AreEqual(ExpectedLandEdgeHash64, edgeHash, "LandEdge golden changed.");
                Assert.AreEqual(ExpectedLandInteriorHash64, interiorHash, "LandInterior golden changed.");
                Assert.AreEqual(ExpectedHillsL1Hash64, h1Hash, "HillsL1 golden changed.");
                Assert.AreEqual(ExpectedHillsL2Hash64, h2Hash, "HillsL2 golden changed.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // =================================================================
        // N5.d — Hills Noise Modulation
        // =================================================================

        [Test]
        public void N5d_Blend0_MatchesPreN5dGoldens()
        {
            // Default hillsNoiseBlend = 0.0 → identical to pre-N5.d at current defaults.
            // N5.e note: effective thresholds changed from 0.65/0.80 to ~0.65/0.8005.
            // This test locks blend=0 to the same goldens as the main golden test.
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _,
                    out ulong edgeHash, out ulong interiorHash, out ulong h1Hash, out ulong h2Hash,
                    out MapContext2D ctx);

            try
            {
                Assert.AreEqual(ExpectedLandEdgeHash64, edgeHash, "N5.d blend=0 must match LandEdge golden.");
                Assert.AreEqual(ExpectedLandInteriorHash64, interiorHash, "N5.d blend=0 must match LandInterior golden.");
                Assert.AreEqual(ExpectedHillsL1Hash64, h1Hash, "N5.d blend=0 must match HillsL1 golden.");
                Assert.AreEqual(ExpectedHillsL2Hash64, h2Hash, "N5.d blend=0 must match HillsL2 golden.");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void N5d_BlendPositive_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = new MapTunables2D(
                islandRadius01: 0.45f, waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f, islandSmoothTo01: 0.70f,
                hillsNoiseBlend: 0.5f);
            var inputs = new MapInputs(Seed, domain, tunables);

            RunOnce(in inputs, out _, out _, out _, out _, out ulong h1A, out ulong h2A, out _);
            RunOnce(in inputs, out _, out _, out _, out _, out ulong h1B, out ulong h2B, out _);

            Assert.AreEqual(h1A, h1B, "HillsL1 must be deterministic with blend > 0.");
            Assert.AreEqual(h2A, h2B, "HillsL2 must be deterministic with blend > 0.");
        }

        [Test]
        public void N5d_BlendPositive_DiffersFromBlend0()
        {
            var domain = new GridDomain2D(W, H);

            var tunables0 = MapTunables2D.Default; // blend = 0
            var inputs0 = new MapInputs(Seed, domain, tunables0);
            RunOnce(in inputs0, out _, out _, out _, out _, out ulong h1_0, out ulong h2_0, out _);

            var tunablesBlend = new MapTunables2D(
                islandRadius01: 0.45f,
                // W-aux.f: must track Default — this test compares against
                // MapTunables2D.Default and only isolates the blend when every other
                // tunable matches. A hardcoded 0.50f makes it pass for the wrong reason.
                waterThreshold01: MapTunables2D.Default.waterThreshold01,
                islandSmoothFrom01: 0.30f, islandSmoothTo01: 0.70f,
                hillsNoiseBlend: 0.5f);
            var inputsBlend = new MapInputs(Seed, domain, tunablesBlend);
            RunOnce(in inputsBlend, out _, out _, out _, out _, out ulong h1_b, out ulong h2_b, out _);

            // At least one of L1/L2 should differ (noise shifts thresholds).
            bool differs = h1_0 != h1_b || h2_0 != h2_b;
            Assert.IsTrue(differs, "Blend > 0 should produce different hills than blend = 0.");
        }

        [Test]
        public void N5d_BlendPositive_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = new MapTunables2D(
                islandRadius01: 0.45f, waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f, islandSmoothTo01: 0.70f,
                hillsNoiseBlend: 0.8f);
            var inputs = new MapInputs(Seed, domain, tunables);

            RunOnce(in inputs, out ulong landBefore, out ulong deepBefore,
                    out _, out _, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);
                ref MaskGrid2D edge = ref ctx.GetLayer(MapLayerId.LandEdge);
                ref MaskGrid2D interior = ref ctx.GetLayer(MapLayerId.LandInterior);
                ref MaskGrid2D hillsL1 = ref ctx.GetLayer(MapLayerId.HillsL1);
                ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);

                var temp = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

                try
                {
                    // LandEdge ∪ LandInterior == Land
                    temp.CopyFrom(edge);
                    temp.Or(interior);
                    Assert.AreEqual(land.SnapshotHash64(includeDimensions: true),
                                    temp.SnapshotHash64(includeDimensions: true),
                                    "LandEdge ∪ LandInterior must equal Land.");

                    // LandEdge ∩ LandInterior == ∅
                    temp.CopyFrom(edge);
                    temp.And(interior);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "LandEdge and LandInterior must be disjoint.");

                    // HillsL1 ⊆ Land
                    temp.CopyFrom(hillsL1);
                    temp.AndNot(land);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL1 must be a subset of Land.");

                    // HillsL2 ⊆ Land
                    temp.CopyFrom(hillsL2);
                    temp.AndNot(land);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL2 must be a subset of Land.");

                    // HillsL1 ∩ HillsL2 == ∅
                    temp.CopyFrom(hillsL1);
                    temp.And(hillsL2);
                    Assert.AreEqual(0, temp.CountOnes(),
                                    "HillsL1 and HillsL2 must be disjoint.");

                    // No-mutate: Land, DeepWater unchanged.
                    Assert.AreEqual(landBefore, land.SnapshotHash64(includeDimensions: true),
                                    "Stage_Hills2D must not mutate Land.");
                    Assert.AreEqual(deepBefore, deep.SnapshotHash64(includeDimensions: true),
                                    "Stage_Hills2D must not mutate DeepWater.");
                }
                finally
                {
                    temp.Dispose();
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void N5d_BlendPositive_SeedVariation()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = new MapTunables2D(
                islandRadius01: 0.45f, waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f, islandSmoothTo01: 0.70f,
                hillsNoiseBlend: 0.5f);

            var inputs1 = new MapInputs(1u, domain, tunables);
            RunOnce(in inputs1, out _, out _, out _, out _, out ulong h1_s1, out _, out _);

            var inputs2 = new MapInputs(2u, domain, tunables);
            RunOnce(in inputs2, out _, out _, out _, out _, out ulong h1_s2, out _, out _);

            var inputs3 = new MapInputs(3u, domain, tunables);
            RunOnce(in inputs3, out _, out _, out _, out _, out ulong h1_s3, out _, out _);

            Assert.AreNotEqual(h1_s1, h1_s2, "Different seeds should produce different hills (seeds 1 vs 2).");
            Assert.AreNotEqual(h1_s2, h1_s3, "Different seeds should produce different hills (seeds 2 vs 3).");
        }

        // =================================================================
        // Helpers
        // =================================================================

        private static void RunOnce(
            in MapInputs inputs,
            out ulong landHash,
            out ulong deepHash,
            out ulong edgeHash,
            out ulong interiorHash,
            out ulong h1Hash,
            out ulong h2Hash,
            out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);

            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            landHash = ctx.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
            deepHash = ctx.GetLayer(MapLayerId.DeepWater).SnapshotHash64(includeDimensions: true);

            new Stage_Hills2D().Execute(ref ctx, in inputs);

            edgeHash = ctx.GetLayer(MapLayerId.LandEdge).SnapshotHash64(includeDimensions: true);
            interiorHash = ctx.GetLayer(MapLayerId.LandInterior).SnapshotHash64(includeDimensions: true);
            h1Hash = ctx.GetLayer(MapLayerId.HillsL1).SnapshotHash64(includeDimensions: true);
            h2Hash = ctx.GetLayer(MapLayerId.HillsL2).SnapshotHash64(includeDimensions: true);
        }

        private static void RunOnce(
            in MapInputs inputs,
            out ulong edgeHash,
            out ulong interiorHash,
            out ulong h1Hash,
            out ulong h2Hash,
            out MapContext2D ctx)
        {
            RunOnce(in inputs, out _, out _, out edgeHash, out interiorHash, out h1Hash, out h2Hash, out ctx);
        }
    }
}