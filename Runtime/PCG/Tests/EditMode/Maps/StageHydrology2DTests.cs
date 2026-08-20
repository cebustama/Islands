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
    /// Stage-level tests for <see cref="Stage_Hydrology2D"/> (Phase L).
    ///
    /// Covers invariants L-1 through L-10 (from Phase_L_Design.md §9) and
    /// golden hash snapshots for FlowAccumulation, Rivers, and Lakes.
    ///
    /// ── Golden initialization ─────────────────────────────────────────────────
    ///   All golden constants are initialized to 0UL.  Run the suite once; the
    ///   Assert.Fail message reports the correct value — paste it into the constant.
    ///   Goldens are captured for: 64×64, seed=12345, default tunables.
    /// </summary>
    public sealed class StageHydrology2DTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // ── Golden constants — set to 0UL until first green run ───────────────
        private const ulong GoldenFlowAccumulation = 0xFF413C94785FCEACUL;
        private const ulong GoldenRivers = 0xA14DDE84505F6876UL;
        private const ulong GoldenLakes = 0x31F98F699FC42573UL;

        // =====================================================================
        // Pipeline runner — full F0→G→L
        // =====================================================================

        private static MapContext2D RunHydrology(in MapInputs inputs)
        {
            var ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            ctx.BeginRun(in inputs, clearLayers: true);
            new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
            new Stage_Hills2D().Execute(ref ctx, in inputs);
            new Stage_Shore2D().Execute(ref ctx, in inputs);
            new Stage_Vegetation2D().Execute(ref ctx, in inputs);
            new Stage_Traversal2D().Execute(ref ctx, in inputs);
            new Stage_Morphology2D().Execute(ref ctx, in inputs);
            new Stage_Hydrology2D().Execute(ref ctx, in inputs);
            return ctx;
        }

        private static MapInputs MakeInputs() =>
            new MapInputs(Seed, new GridDomain2D(W, H), MapTunables2D.Default);

        // =====================================================================
        // L-1 — Determinism
        // =====================================================================

        [Test]
        public void L1_Determinism_FlowAccumulation()
        {
            var inputs = MakeInputs();
            using var ctxA = RunHydrology(in inputs);
            using var ctxB = RunHydrology(in inputs);

            ulong ha = HashField(ref ctxA.GetField(MapFieldId.FlowAccumulation));
            ulong hb = HashField(ref ctxB.GetField(MapFieldId.FlowAccumulation));
            Assert.AreEqual(ha, hb, "FlowAccumulation must be identical across runs (L-1).");
        }

        [Test]
        public void L1_Determinism_Rivers()
        {
            var inputs = MakeInputs();
            using var ctxA = RunHydrology(in inputs);
            using var ctxB = RunHydrology(in inputs);

            ulong ha = ctxA.GetLayer(MapLayerId.Rivers).SnapshotHash64(includeDimensions: true);
            ulong hb = ctxB.GetLayer(MapLayerId.Rivers).SnapshotHash64(includeDimensions: true);
            Assert.AreEqual(ha, hb, "Rivers mask must be identical across runs (L-1).");
        }

        [Test]
        public void L1_Determinism_Lakes()
        {
            var inputs = MakeInputs();
            using var ctxA = RunHydrology(in inputs);
            using var ctxB = RunHydrology(in inputs);

            ulong ha = ctxA.GetLayer(MapLayerId.Lakes).SnapshotHash64(includeDimensions: true);
            ulong hb = ctxB.GetLayer(MapLayerId.Lakes).SnapshotHash64(includeDimensions: true);
            Assert.AreEqual(ha, hb, "Lakes mask must be identical across runs (L-1).");
        }

        // =====================================================================
        // L-2 — Rivers ⊆ Land
        // =====================================================================

        [Test]
        public void L2_Rivers_SubsetOf_Land()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            var domain = ctx.Domain;
            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D rivers = ref ctx.GetLayer(MapLayerId.Rivers);

            var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                scratch.CopyFrom(rivers);
                scratch.AndNot(land);
                Assert.AreEqual(0, scratch.CountOnes(),
                    "L-2 violated: some River cells are not on Land.");
            }
            finally { scratch.Dispose(); }
        }

        // =====================================================================
        // L-3 — Lakes ⊆ NOT-Land
        // =====================================================================

        [Test]
        public void L3_Lakes_SubsetOf_NotLand()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            var domain = ctx.Domain;
            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D lakes = ref ctx.GetLayer(MapLayerId.Lakes);

            var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                scratch.CopyFrom(lakes);
                scratch.And(land);
                Assert.AreEqual(0, scratch.CountOnes(),
                    "L-3 violated: some Lake cells are on Land.");
            }
            finally { scratch.Dispose(); }
        }

        // =====================================================================
        // L-4 — Lakes ∩ DeepWater == ∅
        // =====================================================================

        [Test]
        public void L4_Lakes_Disjoint_DeepWater()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            var domain = ctx.Domain;
            ref MaskGrid2D lakes = ref ctx.GetLayer(MapLayerId.Lakes);
            ref MaskGrid2D deepWater = ref ctx.GetLayer(MapLayerId.DeepWater);

            var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                scratch.CopyFrom(lakes);
                scratch.And(deepWater);
                Assert.AreEqual(0, scratch.CountOnes(),
                    "L-4 violated: some Lake cells are also DeepWater.");
            }
            finally { scratch.Dispose(); }
        }

        // =====================================================================
        // L-5 — Lakes ∩ ShallowWater == ∅
        // =====================================================================

        [Test]
        public void L5_Lakes_Disjoint_ShallowWater()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            var domain = ctx.Domain;
            ref MaskGrid2D lakes = ref ctx.GetLayer(MapLayerId.Lakes);
            ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);

            var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                scratch.CopyFrom(lakes);
                scratch.And(shallowWater);
                Assert.AreEqual(0, scratch.CountOnes(),
                    "L-5 violated: some Lake cells are also ShallowWater.");
            }
            finally { scratch.Dispose(); }
        }

        // =====================================================================
        // L-6 — Rivers ∩ Lakes == ∅ (by construction)
        // =====================================================================

        [Test]
        public void L6_Rivers_Disjoint_Lakes()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            var domain = ctx.Domain;
            ref MaskGrid2D rivers = ref ctx.GetLayer(MapLayerId.Rivers);
            ref MaskGrid2D lakes = ref ctx.GetLayer(MapLayerId.Lakes);

            var scratch = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            try
            {
                scratch.CopyFrom(rivers);
                scratch.And(lakes);
                Assert.AreEqual(0, scratch.CountOnes(),
                    "L-6 violated: Rivers and Lakes overlap (impossible by construction).");
            }
            finally { scratch.Dispose(); }
        }

        // =====================================================================
        // L-7 — FlowAccumulation range
        // =====================================================================

        [Test]
        public void L7_FlowAccumulation_Range_LandAtLeast1_WaterZero()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref ScalarField2D fa = ref ctx.GetField(MapFieldId.FlowAccumulation);

            int w = W, h = H;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float v = fa.Values[y * w + x];
                    if (land.GetUnchecked(x, y))
                        Assert.GreaterOrEqual(v, 1f,
                            $"L-7 violated: Land cell ({x},{y}) has flowAccum={v} < 1f.");
                    else
                        Assert.AreEqual(0f, v,
                            $"L-7 violated: non-Land cell ({x},{y}) has flowAccum={v} != 0f.");
                }
            }
        }

        // =====================================================================
        // L-8 — FlowAccumulation conservation
        // =====================================================================

        [Test]
        public void L8_FlowAccumulation_Conservation()
        {
            // L-8 (corrected from a mathematically incorrect prior assertion):
            //
            // ── Why the original "sum over Land == landCount" was wrong ──
            // AccumulateFlow ADDS a cell's value to its downstream neighbor — it
            // does NOT move/transfer it. After propagation, an upstream cell still
            // holds its own "1" AND that "1" has been added to all downstream cells
            // along its drainage path. So:
            //   sum(flowAccum over Land) = Σ_i (1 + path_length_to_outlet[i])
            //                            = landCount + Σ path_lengths
            //                            ≥ landCount    (equality only if no
            //                                            propagation happens at all)
            //
            // The TRUE mass-conservation invariant is at outlet cells:
            //   Σ flowAccum at coastal-sink cells == landCount
            // but that requires flowDir, which is a stage-internal temp buffer
            // not exposed via the context.
            //
            // ── What we test instead ──
            // Two weaker but provable invariants from the public field:
            //   (a) every Land cell preserves its own contribution: flowAccum ≥ 1
            //   (b) the Land-sum is at least landCount (lower bound from (a))
            // Combined with L-7 (non-Land == 0), these capture the no-loss property.
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref ScalarField2D fa = ref ctx.GetField(MapFieldId.FlowAccumulation);

            int landCount = land.CountOnes();
            Assert.Greater(landCount, 0, "Test requires non-empty Land for meaningful coverage.");

            int w = W, h = H;
            double sum = 0;
            int landBelowOne = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (land.GetUnchecked(x, y))
                    {
                        float v = fa.Values[y * w + x];
                        sum += v;
                        if (v < 1f - 1e-6f) landBelowOne++;
                    }

            // (a) every Land cell ≥ 1 — own contribution preserved.
            Assert.AreEqual(0, landBelowOne,
                $"L-8 violated: {landBelowOne} Land cells have flowAccum < 1 — own contribution lost.");
            // (b) total Land-sum ≥ landCount — no value destroyed during propagation.
            Assert.GreaterOrEqual(sum, (double)landCount,
                $"L-8 violated: sum of FlowAccumulation over Land ({sum:F0}) < landCount ({landCount}).");
        }

        // =====================================================================
        // L-10 — No-mutate: upstream layers unchanged
        // =====================================================================

        [Test]
        public void L10_NoMutate_UpstreamLayersUnchanged()
        {
            var inputs = MakeInputs();

            // Capture hashes before Hydrology runs.
            var ctxPre = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctxPre.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctxPre, in inputs);
                new Stage_Hills2D().Execute(ref ctxPre, in inputs);
                new Stage_Shore2D().Execute(ref ctxPre, in inputs);
                new Stage_Vegetation2D().Execute(ref ctxPre, in inputs);
                new Stage_Traversal2D().Execute(ref ctxPre, in inputs);
                new Stage_Morphology2D().Execute(ref ctxPre, in inputs);

                ulong preHeight = HashField(ref ctxPre.GetField(MapFieldId.Height));
                ulong preLand = ctxPre.GetLayer(MapLayerId.Land).SnapshotHash64(true);
                ulong preDeep = ctxPre.GetLayer(MapLayerId.DeepWater).SnapshotHash64(true);
                ulong preShallow = ctxPre.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(true);
                ulong preCoast = HashField(ref ctxPre.GetField(MapFieldId.CoastDist));

                // Now run Hydrology on the same context.
                new Stage_Hydrology2D().Execute(ref ctxPre, in inputs);

                Assert.AreEqual(preHeight, HashField(ref ctxPre.GetField(MapFieldId.Height)),
                    "L-10: Height field mutated by Hydrology.");
                Assert.AreEqual(preLand, ctxPre.GetLayer(MapLayerId.Land).SnapshotHash64(true),
                    "L-10: Land layer mutated by Hydrology.");
                Assert.AreEqual(preDeep, ctxPre.GetLayer(MapLayerId.DeepWater).SnapshotHash64(true),
                    "L-10: DeepWater layer mutated by Hydrology.");
                Assert.AreEqual(preShallow, ctxPre.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(true),
                    "L-10: ShallowWater layer mutated by Hydrology.");
                Assert.AreEqual(preCoast, HashField(ref ctxPre.GetField(MapFieldId.CoastDist)),
                    "L-10: CoastDist field mutated by Hydrology.");
            }
            finally { ctxPre.Dispose(); }
        }

        // =====================================================================
        // Golden hash tests — capture on first green run
        // =====================================================================

        [Test]
        public void L_Golden_FlowAccumulation_IsLocked()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);
            ulong h = HashField(ref ctx.GetField(MapFieldId.FlowAccumulation));

            if (GoldenFlowAccumulation == 0UL)
                Assert.Fail($"FlowAccumulation golden not initialized. Set GoldenFlowAccumulation = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenFlowAccumulation, h,
                $"FlowAccumulation golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void L_Golden_Rivers_IsLocked()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);
            ulong h = ctx.GetLayer(MapLayerId.Rivers).SnapshotHash64(includeDimensions: true);

            if (GoldenRivers == 0UL)
                Assert.Fail($"Rivers golden not initialized. Set GoldenRivers = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenRivers, h,
                $"Rivers golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void L_Golden_Lakes_IsLocked()
        {
            var inputs = MakeInputs();
            using var ctx = RunHydrology(in inputs);
            ulong h = ctx.GetLayer(MapLayerId.Lakes).SnapshotHash64(includeDimensions: true);

            if (GoldenLakes == 0UL)
                Assert.Fail($"Lakes golden not initialized. Set GoldenLakes = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenLakes, h,
                $"Lakes golden changed. Got=0x{h:X16}");
        }

        // =====================================================================
        // Edge cases
        // =====================================================================

        [Test]
        public void L_LakeFilter_Disabled_PreservesAllInlandWater()
        {
            // minLakeArea = 0 → no fragments removed.
            var inputs = MakeInputs();
            using var ctxA = RunHydrology(in inputs);

            var ctxB = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctxB.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctxB, in inputs);
                new Stage_Hills2D().Execute(ref ctxB, in inputs);
                new Stage_Shore2D().Execute(ref ctxB, in inputs);
                new Stage_Vegetation2D().Execute(ref ctxB, in inputs);
                new Stage_Traversal2D().Execute(ref ctxB, in inputs);
                new Stage_Morphology2D().Execute(ref ctxB, in inputs);
                new Stage_Hydrology2D { minLakeArea = 0 }.Execute(ref ctxB, in inputs);

                ulong hA = ctxA.GetLayer(MapLayerId.Lakes).SnapshotHash64(true);
                ulong hB = ctxB.GetLayer(MapLayerId.Lakes).SnapshotHash64(true);
                Assert.AreEqual(hA, hB, "minLakeArea=0 (default) should produce same Lakes as no filter.");
            }
            finally { ctxB.Dispose(); }
        }

        [Test]
        public void L_LakeFilter_Enabled_NoSmallComponents()
        {
            int filterSize = 4;
            var inputs = MakeInputs();

            var ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctx.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
                new Stage_Hills2D().Execute(ref ctx, in inputs);
                new Stage_Shore2D().Execute(ref ctx, in inputs);
                new Stage_Vegetation2D().Execute(ref ctx, in inputs);
                new Stage_Traversal2D().Execute(ref ctx, in inputs);
                new Stage_Morphology2D().Execute(ref ctx, in inputs);
                new Stage_Hydrology2D { minLakeArea = filterSize }.Execute(ref ctx, in inputs);

                // Verify: no 4-connected lake component has fewer than filterSize cells.
                ref MaskGrid2D lakes = ref ctx.GetLayer(MapLayerId.Lakes);
                int w = W, h = H;
                var visited = new bool[w * h];
                var queue = new System.Collections.Generic.Queue<int>();

                int[] dx4 = { 0, 1, 0, -1 };
                int[] dy4 = { -1, 0, 1, 0 };

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int idx = y * w + x;
                        if (!lakes.GetUnchecked(x, y)) continue;
                        if (visited[idx]) continue;

                        // BFS — count component.
                        int count = 0;
                        visited[idx] = true;
                        queue.Enqueue(idx);
                        while (queue.Count > 0)
                        {
                            int ci = queue.Dequeue();
                            count++;
                            int cx = ci % w, cy = ci / w;
                            for (int k = 0; k < 4; k++)
                            {
                                int nx = cx + dx4[k], ny = cy + dy4[k];
                                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                                int ni = ny * w + nx;
                                if (!lakes.GetUnchecked(nx, ny)) continue;
                                if (visited[ni]) continue;
                                visited[ni] = true;
                                queue.Enqueue(ni);
                            }
                        }

                        Assert.GreaterOrEqual(count, filterSize,
                            $"Lake component at ({x},{y}) has {count} cells < minLakeArea={filterSize}.");
                    }
                }
            }
            finally { ctx.Dispose(); }
        }

        // =====================================================================
        // Hash helpers
        // =====================================================================

        private static ulong HashField(ref ScalarField2D field)
        {
            const ulong FnvOffset = 1469598103934665603UL;
            const ulong FnvPrime = 1099511628211UL;
            ulong h = FnvOffset;
            for (int i = 0; i < field.Values.Length; i++)
            {
                uint bits = math.asuint(field.Values[i]);
                for (int b = 0; b < 4; b++)
                {
                    h ^= (byte)(bits >> (b * 8));
                    h *= FnvPrime;
                }
            }
            return h;
        }
    }
}