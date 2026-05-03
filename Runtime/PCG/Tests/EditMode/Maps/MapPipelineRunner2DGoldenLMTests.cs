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
    /// Full-pipeline golden tests for the Phase L → Phase M integration.
    ///
    /// Covers the F0→G→L→M configuration where Stage_Biome2D consumes
    /// <see cref="MapFieldId.FlowAccumulation"/> for river moisture enrichment.
    ///
    /// ── Why a separate golden file? ───────────────────────────────────────────
    ///   The existing M / M2a / M2b goldens run WITHOUT Phase L.  Stage_Biome2D
    ///   detects FlowAccumulation via <c>ctx.IsFieldCreated</c> — when absent,
    ///   <c>riverFactor = 0</c> and output is bit-identical to the pre-L baseline.
    ///   Those goldens are therefore NOT invalidated by the Phase L integration.
    ///
    ///   This file captures the new L+M configuration as a separate baseline.
    ///
    /// ── Golden initialization ─────────────────────────────────────────────────
    ///   All constants are 0UL until first green run.  The Assert.Fail message
    ///   reports the correct value.  Standard config: 64×64, seed=12345,
    ///   default tunables for all stages including default riverMoistureBonus=0.4.
    /// </summary>
    public sealed class MapPipelineRunner2DGoldenLMTests
    {
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // ── Phase L+M new goldens (F0→G→L→M) ─────────────────────────────────
        private const ulong GoldenFlowAccumLM = 0xD549F3F32D57C771UL; // unchanged from L-only golden
        private const ulong GoldenRiversLM = 0x7BE94E82265A2FEAUL; // unchanged from L-only golden
        private const ulong GoldenLakesLM = 0UL; // unchanged from L-only golden
        private const ulong GoldenMoistureLM = 0x0A2EFAAF55D33A97UL; // DIFFERENT from M-only — river enrichment active
        private const ulong GoldenTemperatureLM = 0x4A10E758A2C6AD88UL; // unchanged from M-only — temperature unaffected by L
        private const ulong GoldenBiomeLM = 0x0051483ABC36882AUL; // MAY DIFFER from M-only — biome follows moisture

        // =====================================================================
        // Full pipeline F0→G→L→M
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
            new Stage_Biome2D().Execute(ref ctx, in inputs);
            return ctx;
        }

        private static MapInputs MakeInputs() =>
            new MapInputs(Seed, new GridDomain2D(W, H), MapTunables2D.Default);

        // =====================================================================
        // Goldens — Phase L+M outputs
        // =====================================================================

        [Test]
        public void LM_Pipeline_Golden_FlowAccumulation()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = HashField(ref ctx.GetField(MapFieldId.FlowAccumulation));

            if (GoldenFlowAccumLM == 0UL)
                Assert.Fail($"FlowAccumulation LM golden not initialized. Set GoldenFlowAccumLM = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenFlowAccumLM, h,
                $"FlowAccumulation LM golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void LM_Pipeline_Golden_Moisture()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = HashField(ref ctx.GetField(MapFieldId.Moisture));

            if (GoldenMoistureLM == 0UL)
                Assert.Fail($"Moisture LM golden not initialized. Set GoldenMoistureLM = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenMoistureLM, h,
                $"Moisture LM golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void LM_Pipeline_Golden_Temperature()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = HashField(ref ctx.GetField(MapFieldId.Temperature));

            if (GoldenTemperatureLM == 0UL)
                Assert.Fail($"Temperature LM golden not initialized. Set GoldenTemperatureLM = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenTemperatureLM, h,
                $"Temperature LM golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void LM_Pipeline_Golden_Biome()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = HashField(ref ctx.GetField(MapFieldId.Biome));

            if (GoldenBiomeLM == 0UL)
                Assert.Fail($"Biome LM golden not initialized. Set GoldenBiomeLM = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenBiomeLM, h,
                $"Biome LM golden changed. Got=0x{h:X16}");
        }

        [Test]
        public void LM_Pipeline_Golden_Rivers()
        {
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);
            ulong h = ctx.GetLayer(MapLayerId.Rivers).SnapshotHash64(includeDimensions: true);

            if (GoldenRiversLM == 0UL)
                Assert.Fail($"Rivers LM golden not initialized. Set GoldenRiversLM = 0x{h:X16}UL;");
            Assert.AreEqual(GoldenRiversLM, h, $"Rivers LM golden changed. Got=0x{h:X16}");
        }

        // =====================================================================
        // Key integration invariant: river enrichment actually changes moisture
        // =====================================================================

        [Test]
        public void LM_RiverEnrichment_MoistureHigherNearRivers()
        {
            // Verify that moisture is detectably higher near high-accumulation cells
            // when Phase L is active vs. when it is not.
            var inputs = MakeInputs();

            // Run with Phase L (river enrichment active).
            using var ctxWithL = RunFull(in inputs);

            // Run without Phase L (no FlowAccumulation → riverFactor = 0).
            var ctxNoL = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctxNoL.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctxNoL, in inputs);
                new Stage_Hills2D().Execute(ref ctxNoL, in inputs);
                new Stage_Shore2D().Execute(ref ctxNoL, in inputs);
                new Stage_Vegetation2D().Execute(ref ctxNoL, in inputs);
                new Stage_Traversal2D().Execute(ref ctxNoL, in inputs);
                new Stage_Morphology2D().Execute(ref ctxNoL, in inputs);
                // NOTE: No Hydrology — FlowAccumulation absent.
                new Stage_Biome2D().Execute(ref ctxNoL, in inputs);

                // Moisture hashes must differ when riverMoistureBonus > 0 and any river cells exist.
                ulong moistWithL = HashField(ref ctxWithL.GetField(MapFieldId.Moisture));
                ulong moistNoL = HashField(ref ctxNoL.GetField(MapFieldId.Moisture));

                // Check that rivers exist (otherwise the test is vacuous).
                int riverCount = ctxWithL.GetLayer(MapLayerId.Rivers).CountOnes();
                if (riverCount == 0)
                {
                    Assert.Inconclusive(
                        "No river cells in this configuration — enrichment effect cannot be verified.");
                    return;
                }

                Assert.AreNotEqual(moistWithL, moistNoL,
                    "Moisture with Phase L active should differ from moisture without Phase L " +
                    "(river moisture enrichment must have a measurable effect when rivers exist).");
            }
            finally { ctxNoL.Dispose(); }
        }

        [Test]
        public void LM_Determinism_FullPipeline()
        {
            var inputs = MakeInputs();
            using var ctxA = RunFull(in inputs);
            using var ctxB = RunFull(in inputs);

            ulong moA = HashField(ref ctxA.GetField(MapFieldId.Moisture));
            ulong moB = HashField(ref ctxB.GetField(MapFieldId.Moisture));
            Assert.AreEqual(moA, moB, "L+M Moisture must be deterministic.");

            ulong biA = HashField(ref ctxA.GetField(MapFieldId.Biome));
            ulong biB = HashField(ref ctxB.GetField(MapFieldId.Biome));
            Assert.AreEqual(biA, biB, "L+M Biome must be deterministic.");
        }

        [Test]
        public void LM_RiverFlowNorm_Auto_MatchesExplicit()
        {
            // riverFlowNorm = 0 (auto) should produce identical output to
            // the explicit value it auto-computes (landCount * 0.02f).
            var inputs = MakeInputs();

            // Run with auto norm (default).
            using var ctxAuto = RunFull(in inputs);

            // Compute the landCount to derive the explicit equivalent.
            int landCount;
            {
                var ctxTemp = new MapContext2D(inputs.Domain, Allocator.Persistent);
                try
                {
                    ctxTemp.BeginRun(in inputs, clearLayers: true);
                    new Stage_BaseTerrain2D().Execute(ref ctxTemp, in inputs);
                    landCount = ctxTemp.GetLayer(MapLayerId.Land).CountOnes();
                }
                finally { ctxTemp.Dispose(); }
            }

            float explicitNorm = math.max(1f, landCount * 0.02f);

            // Run with explicit norm.
            var ctxExplicit = new MapContext2D(inputs.Domain, Allocator.Persistent);
            try
            {
                ctxExplicit.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Hills2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Shore2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Vegetation2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Traversal2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Morphology2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Hydrology2D().Execute(ref ctxExplicit, in inputs);
                new Stage_Biome2D { riverFlowNorm = explicitNorm }
                    .Execute(ref ctxExplicit, in inputs);

                ulong moAuto = HashField(ref ctxAuto.GetField(MapFieldId.Moisture));
                ulong moExplicit = HashField(ref ctxExplicit.GetField(MapFieldId.Moisture));
                Assert.AreEqual(moAuto, moExplicit,
                    "Auto riverFlowNorm must produce identical output to its explicit equivalent.");
            }
            finally { ctxExplicit.Dispose(); }
        }

        [Test]
        public void LM_NoMutate_FlowAccumulationUnchanged()
        {
            // Stage_Biome2D must not write to FlowAccumulation.
            var inputs = MakeInputs();
            using var ctx = RunFull(in inputs);

            // Re-run Hydrology alone on a fresh context and capture hash.
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
                new Stage_Hydrology2D().Execute(ref ctxRef, in inputs);

                ulong refHash = HashField(ref ctxRef.GetField(MapFieldId.FlowAccumulation));
                ulong lmHash = HashField(ref ctx.GetField(MapFieldId.FlowAccumulation));

                Assert.AreEqual(refHash, lmHash,
                    "Stage_Biome2D must not mutate FlowAccumulation (M-7).");
            }
            finally { ctxRef.Dispose(); }
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