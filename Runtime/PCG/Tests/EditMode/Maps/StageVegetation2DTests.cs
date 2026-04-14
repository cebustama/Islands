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
    public sealed class StageVegetation2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // Legacy (Phase M absent) golden — preserved as fallback path verification.
        // Set to value reported on first run.
        private const ulong ExpectedVegetationHash64_Legacy = 0xE7876A1519EC45D3UL;

        // M2.a biome-aware golden — new. Capture from first green run.
        private const ulong ExpectedVegetationHash64_M2a = 0x41BB2F99C2BE043DUL;

        // -----------------------------------------------------------------------
        // Determinism
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Vegetation2D_IsDeterministic_Legacy()
        {
            var inputs = MakeInputs();
            RunLegacy(in inputs, out ulong hashA, out _);
            RunLegacy(in inputs, out ulong hashB, out _);
            Assert.AreEqual(hashA, hashB,
                "Stage_Vegetation2D must produce identical Vegetation on repeated runs (legacy path).");
        }

        [Test]
        public void Stage_Vegetation2D_IsDeterministic_M2a()
        {
            var inputs = MakeInputs();
            RunM2a(in inputs, out ulong hashA, out _);
            RunM2a(in inputs, out ulong hashB, out _);
            Assert.AreEqual(hashA, hashB,
                "M2.a biome-aware Vegetation must be deterministic.");
        }

        // -----------------------------------------------------------------------
        // Invariants — legacy path (existing test, preserved)
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Vegetation2D_Invariants_Hold_Legacy()
        {
            var inputs = MakeInputs();
            RunLegacy(in inputs, out _, out MapContext2D ctx);
            try { AssertSubsetInvariants(ref ctx); }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // M2a invariants — biome-aware path
        // -----------------------------------------------------------------------

        [Test]
        public void M2a_SubsetInvariants_Hold()
        {
            var inputs = MakeInputs();
            RunM2a(in inputs, out _, out MapContext2D ctx);
            try { AssertSubsetInvariants(ref ctx); }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void M2a_NoMutate_BiomeAndMoisture()
        {
            var inputs = MakeInputs();
            var ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctx.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
                new Stage_Hills2D().Execute(ref ctx, in inputs);
                new Stage_Shore2D().Execute(ref ctx, in inputs);
                new Stage_Traversal2D().Execute(ref ctx, in inputs);
                new Stage_Morphology2D().Execute(ref ctx, in inputs);
                new Stage_Biome2D().Execute(ref ctx, in inputs);

                ulong biomeBefore = HashField(ref ctx.GetField(MapFieldId.Biome));
                ulong moistBefore = HashField(ref ctx.GetField(MapFieldId.Moisture));
                ulong tempBefore = HashField(ref ctx.GetField(MapFieldId.Temperature));

                new Stage_Vegetation2D().Execute(ref ctx, in inputs);

                Assert.AreEqual(biomeBefore, HashField(ref ctx.GetField(MapFieldId.Biome)),
                    "M2a-6: Vegetation stage must not mutate Biome field.");
                Assert.AreEqual(moistBefore, HashField(ref ctx.GetField(MapFieldId.Moisture)),
                    "M2a-6: Vegetation stage must not mutate Moisture field.");
                Assert.AreEqual(tempBefore, HashField(ref ctx.GetField(MapFieldId.Temperature)),
                    "M2a-6: Vegetation stage must not mutate Temperature field.");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void M2a_BiomeZeroSuppression_NoVegetationOnWater()
        {
            var inputs = MakeInputs();
            RunM2a(in inputs, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D veg = ref ctx.GetLayer(MapLayerId.Vegetation);
                int violations = 0;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        if (!land.GetUnchecked(x, y) && veg.GetUnchecked(x, y))
                            violations++;
                Assert.AreEqual(0, violations,
                    "M2a-7: water cells must never carry vegetation.");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void M2a_SnowSuppression_NoVegetationOnSnowBiome()
        {
            var inputs = MakeInputs();
            RunM2a(in inputs, out _, out MapContext2D ctx);
            try
            {
                ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);
                ref MaskGrid2D veg = ref ctx.GetLayer(MapLayerId.Vegetation);
                int violations = 0;
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int b = (int)biome.Values[y * W + x];
                        if (b == (int)BiomeType.Snow && veg.GetUnchecked(x, y))
                            violations++;
                    }
                Assert.AreEqual(0, violations,
                    "M2a-8: Snow biome (vegetationDensity=0) must never carry vegetation.");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void M2a_CoverageMonotonicity_DenseBiomesExceedSparseBiomes()
        {
            // M2a-9 statistical: bucket cells by biome, compute coverage ratios,
            // assert higher-density biomes show higher ratios than lower-density ones.
            // Tolerant: requires sample size >= 8 cells per bucket; skips otherwise.
            var inputs = MakeInputs();
            RunM2a(in inputs, out _, out MapContext2D ctx);
            try
            {
                ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);
                ref MaskGrid2D veg = ref ctx.GetLayer(MapLayerId.Vegetation);

                int count = (int)BiomeType.COUNT;
                int[] cells = new int[count];
                int[] vegOn = new int[count];
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                    {
                        int b = (int)biome.Values[y * W + x];
                        if (b <= 0 || b >= count) continue;
                        cells[b]++;
                        if (veg.GetUnchecked(x, y)) vegOn[b]++;
                    }

                float Ratio(BiomeType bt) =>
                    cells[(int)bt] >= 8 ? (float)vegOn[(int)bt] / cells[(int)bt] : -1f;

                float rRain = Ratio(BiomeType.TropicalRainforest);
                float rTemp = Ratio(BiomeType.TemperateForest);
                float rShrub = Ratio(BiomeType.Shrubland);
                float rDesert = Ratio(BiomeType.SubtropicalDesert);

                // Only assert pairs where both buckets had enough samples.
                if (rRain >= 0 && rDesert >= 0)
                    Assert.Greater(rRain, rDesert,
                        $"M2a-9: TropicalRainforest coverage ({rRain:F2}) must exceed SubtropicalDesert ({rDesert:F2}).");
                if (rTemp >= 0 && rShrub >= 0)
                    Assert.Greater(rTemp, rShrub,
                        $"M2a-9: TemperateForest coverage ({rTemp:F2}) must exceed Shrubland ({rShrub:F2}).");
            }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Golden gates
        // -----------------------------------------------------------------------

        [Test]
        public void Stage_Vegetation2D_GoldenHash_Legacy_IsLocked()
        {
            var inputs = MakeInputs();
            RunLegacy(in inputs, out ulong vegHash, out MapContext2D ctx);
            try
            {
                if (ExpectedVegetationHash64_Legacy == 0UL)
                    Assert.Fail($"Legacy golden not initialized. Set = 0x{vegHash:X16}UL;");
                Assert.AreEqual(ExpectedVegetationHash64_Legacy, vegHash,
                    $"Legacy Vegetation golden changed. Got=0x{vegHash:X16}");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void Stage_Vegetation2D_GoldenHash_M2a_IsLocked()
        {
            var inputs = MakeInputs();
            RunM2a(in inputs, out ulong vegHash, out MapContext2D ctx);
            try
            {
                if (ExpectedVegetationHash64_M2a == 0UL)
                    Assert.Fail($"M2a golden not initialized. Set = 0x{vegHash:X16}UL;");
                Assert.AreEqual(ExpectedVegetationHash64_M2a, vegHash,
                    $"M2a Vegetation golden changed. Got=0x{vegHash:X16}");
            }
            finally { ctx.Dispose(); }
        }

        // -----------------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------------

        private static MapInputs MakeInputs() =>
            new MapInputs(Seed, new GridDomain2D(W, H), MapTunables2D.Default);

        private static void RunLegacy(in MapInputs inputs, out ulong vegHash, out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);
            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);
            new Stage_Vegetation2D().Execute(ref ctx, in inputs);
            vegHash = ctx.GetLayer(MapLayerId.Vegetation).SnapshotHash64(includeDimensions: true);
        }

        private static void RunM2a(in MapInputs inputs, out ulong vegHash, out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);
            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);
            new Stage_Traversal2D().Execute(ref ctx, in inputs);
            new Stage_Morphology2D().Execute(ref ctx, in inputs);
            new Stage_Biome2D().Execute(ref ctx, in inputs);
            new Stage_Vegetation2D().Execute(ref ctx, in inputs);
            vegHash = ctx.GetLayer(MapLayerId.Vegetation).SnapshotHash64(includeDimensions: true);
        }

        private static void AssertSubsetInvariants(ref MapContext2D ctx)
        {
            var domain = new GridDomain2D(W, H);
            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D landInterior = ref ctx.GetLayer(MapLayerId.LandInterior);
            ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
            ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);
            ref MaskGrid2D vegetation = ref ctx.GetLayer(MapLayerId.Vegetation);

            ulong landHash = land.SnapshotHash64(includeDimensions: true);
            ulong intHash = landInterior.SnapshotHash64(includeDimensions: true);
            ulong hillsHash = hillsL2.SnapshotHash64(includeDimensions: true);
            ulong shoreHash = shallowWater.SnapshotHash64(includeDimensions: true);

            var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                scratch.CopyFrom(vegetation); scratch.AndNot(land);
                Assert.AreEqual(0, scratch.CountOnes(), "M2a-1: Vegetation ⊆ Land.");
                scratch.CopyFrom(vegetation); scratch.AndNot(landInterior);
                Assert.AreEqual(0, scratch.CountOnes(), "M2a-2: Vegetation ⊆ LandInterior.");
                scratch.CopyFrom(vegetation); scratch.And(hillsL2);
                Assert.AreEqual(0, scratch.CountOnes(), "M2a-3: Vegetation ∩ HillsL2 == ∅.");
                scratch.CopyFrom(vegetation); scratch.And(shallowWater);
                Assert.AreEqual(0, scratch.CountOnes(), "M2a-4: Vegetation ∩ ShallowWater == ∅.");

                Assert.AreEqual(landHash, land.SnapshotHash64(includeDimensions: true), "no-mutate Land");
                Assert.AreEqual(intHash, landInterior.SnapshotHash64(includeDimensions: true), "no-mutate LandInterior");
                Assert.AreEqual(hillsHash, hillsL2.SnapshotHash64(includeDimensions: true), "no-mutate HillsL2");
                Assert.AreEqual(shoreHash, shallowWater.SnapshotHash64(includeDimensions: true), "no-mutate ShallowWater");
            }
            finally { scratch.Dispose(); }
        }

        private static ulong HashField(ref ScalarField2D field)
        {
            const ulong fnvOffset = 1469598103934665603UL;
            const ulong fnvPrime = 1099511628211UL;
            ulong h = fnvOffset;
            for (int i = 0; i < field.Values.Length; i++)
            {
                uint bits = math.asuint(field.Values[i]);
                for (int b = 0; b < 4; b++) { h ^= (byte)(bits >> (b * 8)); h *= fnvPrime; }
            }
            return h;
        }
    }
}