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
        // GOLDENS (F2.1) — Ellipse mode (default)
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

        // =====================================================================
        // N5.a — Shape Mode tests
        // =====================================================================

        // --- Ellipse default backward compatibility ---
        [Test]
        public void N5a_Ellipse_Default_MatchesPreN5aGoldens()
        {
            // Ellipse mode is the default. The goldens must match the pre-N5.a
            // hashes exactly, proving zero behavioral change at defaults.
            var domain = new GridDomain2D(W, H);
            var tunables = MapTunables2D.Default;

            Assert.AreEqual(IslandShapeMode.Ellipse, tunables.shapeMode,
                "MapTunables2D.Default should use Ellipse shape mode.");

            var inputs = new MapInputs(Seed, domain, tunables);
            RunOnce(in inputs, out ulong landHash, out ulong deepHash, out MapContext2D ctx);
            ctx.Dispose();

            Assert.AreEqual(ExpectedLandHash64, landHash,
                $"Ellipse mode should produce pre-N5.a Land golden. Got=0x{landHash:X16}");
            Assert.AreEqual(ExpectedDeepWaterHash64, deepHash,
                $"Ellipse mode should produce pre-N5.a DeepWater golden. Got=0x{deepHash:X16}");
        }

        // --- Rectangle mode ---
        // Golden strategy: placeholder 0x0; first run prints actual hashes.
        private const ulong ExpectedRectangleLandHash64 = 0xAABAAF5DAE1FDA06UL;
        private const ulong ExpectedRectangleDeepWaterHash64 = 0x616E6EF677B501EAUL;

        private static MapTunables2D RectangleTunables() => new MapTunables2D(
            islandRadius01: 0.45f,
            waterThreshold01: 0.50f,
            islandSmoothFrom01: 0.30f,
            islandSmoothTo01: 0.70f,
            islandAspectRatio: 1.00f,
            warpAmplitude01: 0.00f,
            heightRedistributionExponent: 1.00f,
            heightRemapSpline: default,
            terrainNoise: TerrainNoiseSettings.DefaultTerrain,
            warpNoise: TerrainNoiseSettings.DefaultWarp,
            heightQuantSteps: 1024,
            hillsL1: 0.30f,
            hillsL2: 0.43f,
            shapeMode: IslandShapeMode.Rectangle);

        [Test]
        public void N5a_Rectangle_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, RectangleTunables());

            RunOnce(in inputs, out ulong landA, out ulong deepA, out _);
            RunOnce(in inputs, out ulong landB, out ulong deepB, out _);

            Assert.AreEqual(landA, landB, "Rectangle: Land hash drifted.");
            Assert.AreEqual(deepA, deepB, "Rectangle: DeepWater hash drifted.");
        }

        [Test]
        public void N5a_Rectangle_DiffersFromEllipse()
        {
            var domain = new GridDomain2D(W, H);
            var ellipseInputs = new MapInputs(Seed, domain, MapTunables2D.Default);
            var rectInputs = new MapInputs(Seed, domain, RectangleTunables());

            RunOnce(in ellipseInputs, out ulong ellipseLand, out _, out _);
            RunOnce(in rectInputs, out ulong rectLand, out _, out _);

            Assert.AreNotEqual(ellipseLand, rectLand,
                "Rectangle mode should produce different output from Ellipse at same seed/tunables.");
        }

        [Test]
        public void N5a_Rectangle_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, RectangleTunables());

            RunOnce(in inputs, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);

                Assert.Greater(land.CountOnes(), 0, "Rectangle: Land has 0 ON cells.");
                Assert.Greater(deep.CountOnes(), 0, "Rectangle: DeepWater has 0 ON cells.");

                // DeepWater ∩ Land == ∅
                var intersection = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    intersection.CopyFrom(deep);
                    intersection.And(land);
                    Assert.AreEqual(0, intersection.CountOnes(),
                        "Rectangle: DeepWater intersects Land.");
                }
                finally { intersection.Dispose(); }
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void N5a_Rectangle_GoldenHashes_Locked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, RectangleTunables());

            RunOnce(in inputs, out ulong landHash, out ulong deepHash, out MapContext2D ctx);
            ctx.Dispose();

            if (ExpectedRectangleLandHash64 == 0x0000000000000000
                || ExpectedRectangleDeepWaterHash64 == 0x0000000000000000)
            {
                Assert.Fail(
                    "N5.a Rectangle goldens not initialized.\n" +
                    $"Set ExpectedRectangleLandHash64      = 0x{landHash:X16}UL;\n" +
                    $"Set ExpectedRectangleDeepWaterHash64 = 0x{deepHash:X16}UL;");
            }

            Assert.AreEqual(ExpectedRectangleLandHash64, landHash,
                $"Rectangle Land golden changed. Got=0x{landHash:X16}");
            Assert.AreEqual(ExpectedRectangleDeepWaterHash64, deepHash,
                $"Rectangle DeepWater golden changed. Got=0x{deepHash:X16}");
        }

        // --- NoShape mode ---
        // Golden strategy: placeholder 0x0; first run prints actual hashes.
        private const ulong ExpectedNoShapeLandHash64 = 0xB9148007D4091B9EUL;
        private const ulong ExpectedNoShapeDeepWaterHash64 = 0xF99432113C8AAE6AUL;

        private static MapTunables2D NoShapeTunables() => new MapTunables2D(
            islandRadius01: 0.45f,
            waterThreshold01: 0.50f,
            islandSmoothFrom01: 0.30f,
            islandSmoothTo01: 0.70f,
            islandAspectRatio: 1.00f,
            warpAmplitude01: 0.00f,
            heightRedistributionExponent: 1.00f,
            heightRemapSpline: default,
            terrainNoise: TerrainNoiseSettings.DefaultTerrain,
            warpNoise: TerrainNoiseSettings.DefaultWarp,
            heightQuantSteps: 1024,
            hillsL1: 0.30f,
            hillsL2: 0.43f,
            shapeMode: IslandShapeMode.NoShape);

        [Test]
        public void N5a_NoShape_IsDeterministic()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, NoShapeTunables());

            RunOnce(in inputs, out ulong landA, out ulong deepA, out _);
            RunOnce(in inputs, out ulong landB, out ulong deepB, out _);

            Assert.AreEqual(landA, landB, "NoShape: Land hash drifted.");
            Assert.AreEqual(deepA, deepB, "NoShape: DeepWater hash drifted.");
        }

        [Test]
        public void N5a_NoShape_DiffersFromEllipse()
        {
            var domain = new GridDomain2D(W, H);
            var ellipseInputs = new MapInputs(Seed, domain, MapTunables2D.Default);
            var noShapeInputs = new MapInputs(Seed, domain, NoShapeTunables());

            RunOnce(in ellipseInputs, out ulong ellipseLand, out _, out _);
            RunOnce(in noShapeInputs, out ulong noShapeLand, out _, out _);

            Assert.AreNotEqual(ellipseLand, noShapeLand,
                "NoShape mode should produce different output from Ellipse at same seed/tunables.");
        }

        [Test]
        public void N5a_NoShape_ProducesLandAndWater()
        {
            // NoShape with default noise + waterThreshold=0.5 should produce both
            // land and water (continent-like). Degenerate all-land or all-water is a bug.
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, NoShapeTunables());

            RunOnce(in inputs, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                int totalCells = W * H;
                int landCount = land.CountOnes();

                Assert.Greater(landCount, 0,
                    "NoShape: Land has 0 ON cells — noise should produce some land at threshold 0.5.");
                Assert.Less(landCount, totalCells,
                    "NoShape: All cells are Land — noise should produce some water at threshold 0.5.");
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void N5a_NoShape_Invariants_Hold()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, NoShapeTunables());

            RunOnce(in inputs, out _, out _, out MapContext2D ctx);
            try
            {
                ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
                ref MaskGrid2D deep = ref ctx.GetLayer(MapLayerId.DeepWater);

                // DeepWater ∩ Land == ∅
                var intersection = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                try
                {
                    intersection.CopyFrom(deep);
                    intersection.And(land);
                    Assert.AreEqual(0, intersection.CountOnes(),
                        "NoShape: DeepWater intersects Land.");
                }
                finally { intersection.Dispose(); }
            }
            finally { ctx.Dispose(); }
        }

        [Test]
        public void N5a_NoShape_GoldenHashes_Locked()
        {
            var domain = new GridDomain2D(W, H);
            var inputs = new MapInputs(Seed, domain, NoShapeTunables());

            RunOnce(in inputs, out ulong landHash, out ulong deepHash, out MapContext2D ctx);
            ctx.Dispose();

            if (ExpectedNoShapeLandHash64 == 0x0000000000000000
                || ExpectedNoShapeDeepWaterHash64 == 0x0000000000000000)
            {
                Assert.Fail(
                    "N5.a NoShape goldens not initialized.\n" +
                    $"Set ExpectedNoShapeLandHash64      = 0x{landHash:X16}UL;\n" +
                    $"Set ExpectedNoShapeDeepWaterHash64 = 0x{deepHash:X16}UL;");
            }

            Assert.AreEqual(ExpectedNoShapeLandHash64, landHash,
                $"NoShape Land golden changed. Got=0x{landHash:X16}");
            Assert.AreEqual(ExpectedNoShapeDeepWaterHash64, deepHash,
                $"NoShape DeepWater golden changed. Got=0x{deepHash:X16}");
        }

        // --- Custom mode (falls back to Ellipse without shape input) ---
        [Test]
        public void N5a_Custom_WithoutShapeInput_MatchesEllipse()
        {
            // Custom without a MapShapeInput should fall back to Ellipse behavior.
            var domain = new GridDomain2D(W, H);
            var customTunables = new MapTunables2D(
                islandRadius01: 0.45f,
                waterThreshold01: 0.50f,
                islandSmoothFrom01: 0.30f,
                islandSmoothTo01: 0.70f,
                islandAspectRatio: 1.00f,
                warpAmplitude01: 0.00f,
                heightRedistributionExponent: 1.00f,
                heightRemapSpline: default,
                terrainNoise: TerrainNoiseSettings.DefaultTerrain,
                warpNoise: TerrainNoiseSettings.DefaultWarp,
                heightQuantSteps: 1024,
                hillsL1: 0.30f,
                hillsL2: 0.43f,
                shapeMode: IslandShapeMode.Custom);

            var ellipseInputs = new MapInputs(Seed, domain, MapTunables2D.Default);
            var customInputs = new MapInputs(Seed, domain, customTunables);

            RunOnce(in ellipseInputs, out ulong ellipseLand, out ulong ellipseDeep, out _);
            RunOnce(in customInputs, out ulong customLand, out ulong customDeep, out _);

            Assert.AreEqual(ellipseLand, customLand,
                "Custom mode without shape input should produce identical Land to Ellipse.");
            Assert.AreEqual(ellipseDeep, customDeep,
                "Custom mode without shape input should produce identical DeepWater to Ellipse.");
        }

        // --- F2c HasShape takes priority over shapeMode ---
        [Test]
        public void N5a_ShapeInput_TakesPriorityOverShapeMode()
        {
            // Even with shapeMode=NoShape, providing a MapShapeInput should use the
            // external shape, not the NoShape path.
            var domain = new GridDomain2D(W, H);
            var shape = BuildCenterCircleMask(domain, radius: 20);
            try
            {
                var noShapeWithExternalShape = new MapTunables2D(
                    islandRadius01: 0.45f,
                    waterThreshold01: 0.50f,
                    islandSmoothFrom01: 0.30f,
                    islandSmoothTo01: 0.70f,
                    shapeMode: IslandShapeMode.NoShape);

                var inputs = new MapInputs(Seed, domain, noShapeWithExternalShape,
                    new MapShapeInput(shape));

                RunOnce(in inputs, out _, out _, out MapContext2D ctx);
                try
                {
                    ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);

                    // Land ⊆ shape must hold (F2c contract).
                    var overflow = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
                    try
                    {
                        overflow.CopyFrom(land);
                        overflow.AndNot(shape);
                        Assert.AreEqual(0, overflow.CountOnes(),
                            "With HasShape=true and shapeMode=NoShape, F2c should take priority. " +
                            "Land cells exist outside the shape mask.");
                    }
                    finally { overflow.Dispose(); }
                }
                finally { ctx.Dispose(); }
            }
            finally { shape.Dispose(); }
        }

        // --- Seed variation ---
        [Test]
        public void N5a_Rectangle_DifferentSeedsProduceDifferentOutput()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = RectangleTunables();

            var inputs1 = new MapInputs(Seed, domain, tunables);
            var inputs2 = new MapInputs(Seed + 1, domain, tunables);

            RunOnce(in inputs1, out ulong land1, out _, out _);
            RunOnce(in inputs2, out ulong land2, out _, out _);

            Assert.AreNotEqual(land1, land2,
                "Rectangle: Different seeds should produce different Land hashes.");
        }

        [Test]
        public void N5a_NoShape_DifferentSeedsProduceDifferentOutput()
        {
            var domain = new GridDomain2D(W, H);
            var tunables = NoShapeTunables();

            var inputs1 = new MapInputs(Seed, domain, tunables);
            var inputs2 = new MapInputs(Seed + 1, domain, tunables);

            RunOnce(in inputs1, out ulong land1, out _, out _);
            RunOnce(in inputs2, out ulong land2, out _, out _);

            Assert.AreNotEqual(land1, land2,
                "NoShape: Different seeds should produce different Land hashes.");
        }
    }
}