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
        // W-aux.f re-lock.
        // F3b′ re-anchor: 0x6CDCB0E869BB070F -> 0x4C07AE61280A4079.
        private const ulong ExpectedVegetationHash64_Legacy = 0x4C07AE61280A4079UL;

        // M2.a biome-aware golden — re-anchored for the quantile-mapping revision
        // (global quantile cut over the eligible population replaces the absolute
        // 1 - vegetationDensity threshold). Breaks by design.
        // Value history: 0x41BB2F99C2BE043DUL (pre W-aux.c block 3)
        //             -> 0x5B1DB3468075FFDCUL (block 3, absolute threshold)
        //             -> re-anchor pending.
        // 0 = sentinel: the golden test reports the value to lock in on first run.
        // W-aux.f re-lock.
        // F3b′ re-anchor: 0x6D433B1023A09BB6 -> 0x0583D24540DC8A86.
        private const ulong ExpectedVegetationHash64_M2a = 0x0583D24540DC8A86UL;

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
            try { AssertSubsetInvariants(ref ctx, globalHillsL2Exclusion: true); }
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
            try { AssertSubsetInvariants(ref ctx, globalHillsL2Exclusion: false); }
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
        public void M2a_QuantileCut_IsExact_Nested_AndAboveNominal()
        {
            // M2a-9 reformulated. The former statistical assertion ("dense biomes
            // cover more") passed while the mapping was broken, so it is replaced by
            // an exact check of the quantile contract:
            //   (a) exactness — an eligible cell is vegetated IFF its bucket >= cut(d)
            //   (b) nesting   — d1 > d2  =>  cut(d1) <= cut(d2)
            //   (c) floor     — realized coverage over the eligible set >= d
            // The noise field, the eligible histogram and the cut are recomputed here
            // independently of the stage; comparing cell by cell is what makes (a)
            // exact. Deducing the cut from the lowest vegetated bucket would be
            // unsound: a biome with no cell sitting exactly at the cut would report a
            // cut above the real one.
            var inputs = MakeInputs();
            RunM2a(in inputs, out _, out MapContext2D ctx);

            var domain = new GridDomain2D(W, H);
            var noise01 = new NativeArray<float>(domain.Length, Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                MapNoiseBridge2D.FillSimplexPerlin01(
                    in domain, noise01,
                    seed: inputs.Seed,
                    seedSalt: Stage_Vegetation2D.NoiseSeedSalt,
                    frequency: Stage_Vegetation2D.NoiseFrequency,
                    octaves: Stage_Vegetation2D.NoiseOctaves,
                    lacunarity: Stage_Vegetation2D.NoiseLacunarity,
                    persistence: Stage_Vegetation2D.NoisePersistence,
                    quantSteps: Stage_Vegetation2D.QuantSteps);

                int steps = Stage_Vegetation2D.QuantSteps;
                int count = (int)BiomeType.COUNT;

                // Flatten the context into plain arrays. ctx.GetField / ctx.GetLayer
                // return ref locals, which C# forbids capturing in a local function —
                // and flattening also keeps the eligibility rule written exactly once.
                int[] eligBiome = new int[domain.Length];   // 0 = not eligible
                bool[] vegetated = new bool[domain.Length];
                int[] bucket = new int[domain.Length];
                {
                    ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);
                    ref MaskGrid2D landInterior = ref ctx.GetLayer(MapLayerId.LandInterior);
                    ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
                    ref MaskGrid2D veg = ref ctx.GetLayer(MapLayerId.Vegetation);

                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            int idx = y * W + x;
                            bucket[idx] = math.clamp((int)(noise01[idx] * steps), 0, steps - 1);
                            vegetated[idx] = veg.GetUnchecked(x, y);

                            if (!landInterior.GetUnchecked(x, y)) continue;
                            int b = (int)biome.Values[idx];
                            if (b <= 0 || b >= count) continue;
                            BiomeDef def = BiomeTable.Definitions[b];
                            if (def.vegetationDensity <= 0f) continue;
                            if (hillsL2.GetUnchecked(x, y) && !def.vegetatesOnPeaks) continue;
                            eligBiome[idx] = b;
                        }
                }

                // ---- Eligible histogram ----
                int[] hist = new int[steps];
                int[] eligPerBiome = new int[count];
                int nElig = 0;
                for (int idx = 0; idx < domain.Length; idx++)
                {
                    int b = eligBiome[idx];
                    if (b == 0) continue;
                    hist[bucket[idx]]++;
                    eligPerBiome[b]++;
                    nElig++;
                }

                Assert.Greater(nElig, 0, "M2a-9: no eligible cells — fixture is degenerate.");

                // ---- Independent cut computation ----
                int[] cut = new int[count];
                for (int b = 0; b < count; b++)
                    cut[b] = (b <= 0)
                        ? steps
                        : CutBucket(hist, nElig, BiomeTable.Definitions[b].vegetationDensity, steps);

                // ---- (a) exactness, cell by cell ----
                int mismatches = 0;
                for (int idx = 0; idx < domain.Length; idx++)
                {
                    int b = eligBiome[idx];
                    if (b == 0) continue;
                    bool expected = bucket[idx] >= cut[b];
                    if (vegetated[idx] != expected) mismatches++;
                }
                Assert.AreEqual(0, mismatches,
                    "M2a-9(a): vegetated set does not match the global quantile cut.");

                // ---- (b) nesting across densities ----
                for (int b1 = 1; b1 < count; b1++)
                    for (int b2 = 1; b2 < count; b2++)
                    {
                        float d1 = BiomeTable.Definitions[b1].vegetationDensity;
                        float d2 = BiomeTable.Definitions[b2].vegetationDensity;
                        if (d1 <= d2 || d1 <= 0f || d2 <= 0f) continue;
                        Assert.LessOrEqual(cut[b1], cut[b2],
                            $"M2a-9(b): cut for {(BiomeType)b1} (d={d1:F2}) sits above " +
                            $"{(BiomeType)b2} (d={d2:F2}).");
                    }

                // ---- (c) coverage floor over the eligible population ----
                for (int b = 1; b < count; b++)
                {
                    float d = BiomeTable.Definitions[b].vegetationDensity;
                    if (d <= 0f || eligPerBiome[b] == 0) continue;
                    int atOrAbove = 0;
                    for (int k = cut[b]; k < steps; k++) atOrAbove += hist[k];
                    float realized = (float)atOrAbove / nElig;
                    Assert.GreaterOrEqual(realized, d - 1e-6f,
                        $"M2a-9(c): realized coverage {realized:F4} for d={d:F2} is below nominal.");
                }
            }
            finally
            {
                if (noise01.IsCreated) noise01.Dispose();
                ctx.Dispose();
            }
        }

        /// <summary>
        /// Independent reimplementation of Stage_Vegetation2D's cut rule. Deliberately
        /// duplicated rather than exposed: the gate must be able to disagree with the
        /// stage.
        /// </summary>
        private static int CutBucket(int[] hist, int population, float density, int steps)
        {
            if (population <= 0 || density <= 0f) return steps;
            int target = (int)math.ceil(density * population);
            if (target <= 0) return steps;
            int acc = 0;
            for (int k = steps - 1; k >= 0; k--)
            {
                acc += hist[k];
                if (acc >= target) return k;
            }
            return 0;
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

        private static void AssertSubsetInvariants(ref MapContext2D ctx, bool globalHillsL2Exclusion)
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
                if (globalHillsL2Exclusion)
                {
                    scratch.CopyFrom(vegetation); scratch.And(hillsL2);
                    Assert.AreEqual(0, scratch.CountOnes(),
                        "M2a-3 (legacy path): Vegetation ∩ HillsL2 == ∅.");
                }
                else
                {
                    // M2a-3 reformulated (W-aux.c block 3): peak vegetation is allowed
                    // only on biomes whose BiomeDef declares vegetatesOnPeaks.
                    ref ScalarField2D biome = ref ctx.GetField(MapFieldId.Biome);
                    int peakViolations = 0;
                    for (int y = 0; y < H; y++)
                        for (int x = 0; x < W; x++)
                        {
                            if (!vegetation.GetUnchecked(x, y)) continue;
                            if (!hillsL2.GetUnchecked(x, y)) continue;
                            int b = (int)biome.Values[y * W + x];
                            bool allowed = b > 0 && b < (int)BiomeType.COUNT
                                && BiomeTable.Definitions[b].vegetatesOnPeaks;
                            if (!allowed) peakViolations++;
                        }
                    Assert.AreEqual(0, peakViolations,
                        "M2a-3 (block 3): Vegetation ∩ HillsL2 only on vegetatesOnPeaks biomes.");
                }
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