using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;
using Islands.PCG.Operators;

namespace Islands.PCG.Tests.EditMode.Maps
{
    public sealed class StageBaseTerrain2DTests
    {
        // Keep these stable for goldens.
        private const int W = 64;
        private const int H = 64;
        private const uint Seed = 12345u;

        // ---------------------------------------------------------------------
        // GOLDENS (F2.1)
        // ---------------------------------------------------------------------
        // IMPORTANT:
        // 1) Run tests once.
        // 2) The golden test will FAIL and print the actual hashes.
        // 3) Copy/paste those hashes here to lock behavior.
        //
        // We use includeDimensions:true (MaskGrid2D.SnapshotHash64 default).
        private const ulong ExpectedLandHash64 = 0x3324CA0629C037B7UL;
        private const ulong ExpectedDeepWaterHash64 = 0x0BB222C9B6B41947UL;

        [Test]
        public void Stage_BaseTerrain2D_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            ulong landA, deepA;
            ulong landB, deepB;

            RunOnce(in inputs, out landA, out deepA, out _);
            RunOnce(in inputs, out landB, out deepB, out _);

            Assert.AreEqual(landA, landB, "Land hash drifted: stage is not deterministic for same inputs.");
            Assert.AreEqual(deepA, deepB, "DeepWater hash drifted: stage is not deterministic for same inputs.");
        }

        [Test]
        public void Stage_BaseTerrain2D_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out _, out _, out MapContext2D ctx);

            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);

                // Sanity: should not degenerate (unless you intentionally allow edge cases)
                Assert.Greater(land.CountOnes(), 0, "Sanity: Land has 0 ON cells (unexpected for default tunables).");
                Assert.Greater(deep.CountOnes(), 0, "Sanity: DeepWater has 0 ON cells (unexpected for default tunables).");

                // Invariant 1: DeepWater ∩ Land == ∅
                var intersection = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    intersection.CopyFrom(deep);
                    intersection.And(land);

                    int overlap = intersection.CountOnes();
                    Assert.AreEqual(0, overlap, "Invariant broken: DeepWater intersects Land (overlap > 0).");
                }
                finally
                {
                    intersection.Dispose();
                }

                // Invariant 2: DeepWater equals flood-fill(border-connected NOT Land) rerun
                var expectedDeep = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    MaskFloodFillOps2D.FloodFillBorderConnected_NotSolid(ref land, ref expectedDeep);

                    ulong expectedHash = expectedDeep.SnapshotHash64(includeDimensions: true);
                    ulong gotHash = deep.SnapshotHash64(includeDimensions: true);

                    Assert.AreEqual(
                        expectedHash, gotHash,
                        "DeepWater differs from flood-fill(border-connected NOT Land). Contract drift or bug.");
                }
                finally
                {
                    expectedDeep.Dispose();
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        [Test]
        public void Stage_BaseTerrain2D_GoldenHashes_Locked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, MapTunables2D.Default);

            RunOnce(in inputs, out ulong landHash, out ulong deepHash, out MapContext2D ctx);

            try
            {
                // If goldens are not set yet, fail once with copy/paste values.
                if (ExpectedLandHash64 == 0x0000000000000000
                    || ExpectedDeepWaterHash64 == 0x0000000000000000)
                {
                    Assert.Fail(
                        "Goldens are not initialized.\n" +
                        $"Set ExpectedLandHash64      = 0x{landHash:X16}UL;\n" +
                        $"Set ExpectedDeepWaterHash64 = 0x{deepHash:X16}UL;\n");
                }

                Assert.AreEqual(
                    ExpectedLandHash64, landHash,
                    $"Land golden changed. Got=0x{landHash:X16} Expected=0x{ExpectedLandHash64:X16}");

                Assert.AreEqual(
                    ExpectedDeepWaterHash64, deepHash,
                    $"DeepWater golden changed. Got=0x{deepHash:X16} Expected=0x{ExpectedDeepWaterHash64:X16}");
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // ---------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------
        private static void RunOnce(in MapInputs inputs, out ulong landHash, out ulong deepHash, out MapContext2D ctx)
        {
            ctx = new MapContext2D(inputs.Domain, Allocator.Persistent);

            // Ensure deterministic start state
            ctx.BeginRun(in inputs, clearLayers: true);

            var stage = new Stage_BaseTerrain2D();
            stage.Execute(ref ctx, in inputs);

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);

            landHash = land.SnapshotHash64(includeDimensions: true);
            deepHash = deep.SnapshotHash64(includeDimensions: true);
        }

        // =====================================================================
        // F2c — Shape Input tests
        // =====================================================================
        // Shape: deterministic center-circle (radius 20, 64x64 grid).
        // Built manually (no RNG); same seed as F2b tests.
        //
        // Golden strategy: placeholder 0x0 on first delivery.
        // Run once; the locked test will fail and print the actual hashes.
        // Copy/paste those hashes into the two constants below to lock behavior.
        private const ulong ExpectedShapeInputLandHash64 = 0xD986402B40273547UL;
        private const ulong ExpectedShapeInputDeepWaterHash64 = 0xD5F1514F5471CC2FUL;

        [Test]
        public void Stage_BaseTerrain2D_WithShapeInput_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var shape = BuildCenterCircleMask(domain, radius: 20);
            try
            {
                var inputs = new MapInputs(Seed, domain, MapTunables2D.Default,
                    new MapShapeInput(shape));

                RunOnce(in inputs, out ulong landA, out ulong deepA, out MapContext2D ctxA);
                ctxA.Dispose();
                RunOnce(in inputs, out ulong landB, out ulong deepB, out MapContext2D ctxB);
                ctxB.Dispose();

                Assert.AreEqual(landA, landB,
                    "Land hash drifted with shape input: stage is not deterministic.");
                Assert.AreEqual(deepA, deepB,
                    "DeepWater hash drifted with shape input: stage is not deterministic.");
            }
            finally { shape.Dispose(); }
        }

        [Test]
        public void Stage_BaseTerrain2D_WithShapeInput_LandSubsetOfShape()
        {
            var domain = new GridDomain2D(W, H);
            var shape = BuildCenterCircleMask(domain, radius: 20);
            try
            {
                var inputs = new MapInputs(Seed, domain, MapTunables2D.Default,
                    new MapShapeInput(shape));

                RunOnce(in inputs, out _, out _, out MapContext2D ctx);
                try
                {
                    ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);

                    // Land ∩ NOT(shape) must be empty: no land cell outside the shape mask.
                    var overflow = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                    try
                    {
                        overflow.CopyFrom(land);
                        overflow.AndNot(shape);  // overflow = land & ~shape
                        Assert.AreEqual(0, overflow.CountOnes(),
                            "Land cells exist outside the shape mask — shape boundary not respected.");
                    }
                    finally { overflow.Dispose(); }
                }
                finally { ctx.Dispose(); }
            }
            finally { shape.Dispose(); }
        }

        [Test]
        public void Stage_BaseTerrain2D_WithShapeInput_GoldenHashes_Locked()
        {
            var domain = new GridDomain2D(W, H);
            var shape = BuildCenterCircleMask(domain, radius: 20);
            try
            {
                var inputs = new MapInputs(Seed, domain, MapTunables2D.Default,
                    new MapShapeInput(shape));

                RunOnce(in inputs, out ulong landHash, out ulong deepHash, out MapContext2D ctx);
                ctx.Dispose();

                if (ExpectedShapeInputLandHash64 == 0x0000000000000000
                    || ExpectedShapeInputDeepWaterHash64 == 0x0000000000000000)
                {
                    Assert.Fail(
                        "F2c shape-input goldens not initialized.\n" +
                        $"Set ExpectedShapeInputLandHash64      = 0x{landHash:X16}UL;\n" +
                        $"Set ExpectedShapeInputDeepWaterHash64 = 0x{deepHash:X16}UL;");
                }

                Assert.AreEqual(ExpectedShapeInputLandHash64, landHash,
                    $"F2c Land golden changed. Got=0x{landHash:X16} Expected=0x{ExpectedShapeInputLandHash64:X16}");
                Assert.AreEqual(ExpectedShapeInputDeepWaterHash64, deepHash,
                    $"F2c DeepWater golden changed. Got=0x{deepHash:X16} Expected=0x{ExpectedShapeInputDeepWaterHash64:X16}");
            }
            finally { shape.Dispose(); }
        }

        // --- Shape builder (deterministic, no RNG) ---
        // Center-circle: ON if pixel center is within `radius` of grid center.
        private static MaskGrid2D BuildCenterCircleMask(GridDomain2D domain, float radius)
        {
            var mask = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            float cx = domain.Width * 0.5f;
            float cy = domain.Height * 0.5f;
            float r2 = radius * radius;

            for (int y = 0; y < domain.Height; y++)
                for (int x = 0; x < domain.Width; x++)
                {
                    float dx = (x + 0.5f) - cx;
                    float dy = (y + 0.5f) - cy;
                    mask.SetUnchecked(x, y, dx * dx + dy * dy <= r2);
                }
            return mask;
        }
    }
}