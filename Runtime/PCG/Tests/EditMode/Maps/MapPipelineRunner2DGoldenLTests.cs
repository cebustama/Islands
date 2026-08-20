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
    /// Full-pipeline golden tests for Phase L — Hydrology.
    ///
    /// Captures hashes for all layers/fields produced by the F0→G→L pipeline
    /// at the Phase L baseline.  These goldens are append-only: they do NOT
    /// invalidate existing F0–G goldens (no upstream stage is modified by L).
    ///
    /// ── Golden initialization ─────────────────────────────────────────────────
    ///   All constants are 0UL until first green run.  The Assert.Fail message
    ///   reports the correct value.  Paste it into the constant and re-run.
    ///   Standard config: 64×64, seed=12345, default tunables, all stages active.
    ///
    /// ── Scope note ───────────────────────────────────────────────────────────
    ///   These goldens cover F0–L only.  The companion tests for F0–L–M will be
    ///   added in Phase M's golden update pass once Phase M is updated to consume
    ///   FlowAccumulation (see Phase_L_Design.md §13).
    /// </summary>
    public sealed class MapPipelineRunner2DGoldenLTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // ── Existing upstream goldens (unchanged by Phase L) ──────────────────
        // These values match MapPipelineRunner2DGoldenM2bTests (or equivalent).
        // They are repeated here to verify Phase L does not break upstream outputs.
        // Update these only if an upstream stage changes (not from L itself).
        // Set to 0UL if not yet captured in this test file — they are covered by
        // their own golden test files and are repeated here as regression guards.
        private const ulong GoldenHeight = 0UL; // from F0–G pipeline
        private const ulong GoldenLand = 0UL;
        private const ulong GoldenDeepWater = 0UL;
        private const ulong GoldenShallowWater = 0UL;
        private const ulong GoldenCoastDist = 0UL;

        // ── Phase L new outputs ───────────────────────────────────────────────
        private const ulong GoldenFlowAccumL = 0xFF413C94785FCEACUL;
        private const ulong GoldenRiversL = 0xA14DDE84505F6876UL;
        private const ulong GoldenLakesL = 0x31F98F699FC42573UL;

        // =====================================================================
        // Full pipeline F0→G→L
        // =====================================================================

        private static MapContext2D RunFull(in MapInputs inputs)
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
        // Phase L output goldens
        // =====================================================================

        [Test]
        public void L_Pipeline_Golden_FlowAccumulation()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = HashField(ref ctx.GetField(MapFieldId.FlowAccumulation));

            if (GoldenFlowAccumL == 0UL)
                Assert.Fail($"FlowAccumulation pipeline golden not initialized. Set GoldenFlowAccumL = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenFlowAccumL, h,
                $"FlowAccumulation pipeline golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void L_Pipeline_Golden_Rivers()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = ctx.GetLayer(MapLayerId.Rivers).SnapshotHash64(includeDimensions: true);

            if (GoldenRiversL == 0UL)
                Assert.Fail($"Rivers pipeline golden not initialized. Set GoldenRiversL = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenRiversL, h,
                $"Rivers pipeline golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void L_Pipeline_Golden_Lakes()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = ctx.GetLayer(MapLayerId.Lakes).SnapshotHash64(includeDimensions: true);

            if (GoldenLakesL == 0UL)
                Assert.Fail($"Lakes pipeline golden not initialized. Set GoldenLakesL = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenLakesL, h,
                $"Lakes pipeline golden changed. Got=0x{h:X16}");
        }

        // =====================================================================
        // Upstream regression guards — verify L does not break prior outputs
        // =====================================================================

        [Test]
        public void L_Pipeline_Regression_UpstreamLayersUnchanged()
        {
            // Run the pipeline without Hydrology and capture reference hashes.
            var inputs = MakeInputs();
            var ctxRef = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctxRef.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctxRef, in inputs);
                new Stage_Hills2D().Execute(ref ctxRef, in inputs);
                new Stage_Shore2D().Execute(ref ctxRef, in inputs);
                new Stage_Vegetation2D().Execute(ref ctxRef, in inputs);
                new Stage_Traversal2D().Execute(ref ctxRef, in inputs);
                new Stage_Morphology2D().Execute(ref ctxRef, in inputs);

                ulong refHeight = HashField(ref ctxRef.GetField(MapFieldId.Height));
                ulong refLand = ctxRef.GetLayer(MapLayerId.Land).SnapshotHash64(true);
                ulong refDeep = ctxRef.GetLayer(MapLayerId.DeepWater).SnapshotHash64(true);
                ulong refShallow = ctxRef.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(true);
                ulong refCoast = HashField(ref ctxRef.GetField(MapFieldId.CoastDist));

                // Add Hydrology on top.
                new Stage_Hydrology2D().Execute(ref ctxRef, in inputs);

                Assert.AreEqual(refHeight, HashField(ref ctxRef.GetField(MapFieldId.Height)),
                    "Hydrology must not mutate Height.");
                Assert.AreEqual(refLand, ctxRef.GetLayer(MapLayerId.Land).SnapshotHash64(true),
                    "Hydrology must not mutate Land.");
                Assert.AreEqual(refDeep, ctxRef.GetLayer(MapLayerId.DeepWater).SnapshotHash64(true),
                    "Hydrology must not mutate DeepWater.");
                Assert.AreEqual(refShallow, ctxRef.GetLayer(MapLayerId.ShallowWater).SnapshotHash64(true),
                    "Hydrology must not mutate ShallowWater.");
                Assert.AreEqual(refCoast, HashField(ref ctxRef.GetField(MapFieldId.CoastDist)),
                    "Hydrology must not mutate CoastDist.");
            }
            finally { ctxRef.Dispose(); }
        }

        [Test]
        public void L_Pipeline_SeedVariation_ProducesDifferentNetworks()
        {
            var inputsA = MakeInputs();
            var inputsB = new MapInputs(99999u, new GridDomain2D(W, H), MapTunables2D.Default);

            using var ctxA = RunFull(in inputsA);
            using var ctxB = RunFull(in inputsB);

            ulong hA = ctxA.GetLayer(MapLayerId.Rivers).SnapshotHash64(true);
            ulong hB = ctxB.GetLayer(MapLayerId.Rivers).SnapshotHash64(true);
            // Different seeds should (with overwhelming probability) produce different networks.
            Assert.AreNotEqual(hA, hB,
                "Two different seeds should produce different river networks.");
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