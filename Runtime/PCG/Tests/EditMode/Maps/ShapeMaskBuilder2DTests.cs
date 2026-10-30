using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// W.c — ShapeMaskBuilder2D unit gates + end-to-end shape-path determinism.
    /// The F2c consumer golden (MapPipelineRunner2DGoldenF2cTests) and the stage's
    /// dimension-guard test are unchanged and not duplicated here.
    /// </summary>
    public sealed class ShapeMaskBuilder2DTests
    {
        private static readonly float[] WestWater = new float[]
        {
            0f, 1f, 1f,
            0f, 1f, 1f,
            0f, 1f, 1f,
        };

        [Test]
        public void Builder_WestWaterNeighborhood_PutsSeaOnWestEdge()
        {
            var d = new GridDomain2D(64, 64);
            var mask = ShapeMaskBuilder2D.BuildFromNeighborhood3x3(
                in d, WestWater, threshold01: 0.6f, Allocator.Persistent);
            try
            {
                Assert.IsFalse(mask.GetUnchecked(0, 32), "West edge should be water.");
                Assert.IsTrue(mask.GetUnchecked(32, 32), "Center should be land.");
                Assert.IsTrue(mask.GetUnchecked(63, 32), "East edge should be land.");
            }
            finally { mask.Dispose(); }
        }

        [Test]
        public void Builder_IsDeterministic_SameInputsSameHash()
        {
            var d = new GridDomain2D(64, 64);
            var a = ShapeMaskBuilder2D.BuildFromNeighborhood3x3(in d, WestWater, 0.6f, Allocator.Persistent);
            var b = ShapeMaskBuilder2D.BuildFromNeighborhood3x3(in d, WestWater, 0.6f, Allocator.Persistent);
            try
            {
                Assert.AreEqual(
                    a.SnapshotHash64(includeDimensions: true),
                    b.SnapshotHash64(includeDimensions: true));
            }
            finally { a.Dispose(); b.Dispose(); }
        }

        [Test]
        public void Builder_InteriorTile_AllLand_And_OceanTile_AllWater()
        {
            var d = new GridDomain2D(32, 32);
            var allLand = new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
            var allWater = new float[9];

            var landMask = ShapeMaskBuilder2D.BuildFromNeighborhood3x3(in d, allLand, 0.5f, Allocator.Persistent);
            var waterMask = ShapeMaskBuilder2D.BuildFromNeighborhood3x3(in d, allWater, 0.5f, Allocator.Persistent);
            try
            {
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        Assert.IsTrue(landMask.GetUnchecked(x, y), $"Interior tile: ({x},{y}) must be land.");
                        Assert.IsFalse(waterMask.GetUnchecked(x, y), $"Ocean tile: ({x},{y}) must be water.");
                    }
            }
            finally { landMask.Dispose(); waterMask.Dispose(); }
        }

        [Test]
        public void Sampler_WorldBorderTile_TreatsOutOfBoundsAsWater()
        {
            var wd = new GridDomain2D(8, 8);
            var world = new MaskGrid2D(wd, Allocator.Persistent, clearToZero: true);
            try
            {
                for (int y = 0; y < 8; y++)
                    for (int x = 0; x < 8; x++)
                        world.SetUnchecked(x, y, true);   // all-land world

                var ld = new GridDomain2D(64, 64);
                var mask = ShapeMaskBuilder2D.BuildFromWorldTile(
                    in world, 0, 0, in ld, 0.5f, Allocator.Persistent);
                try
                {
                    Assert.IsFalse(mask.GetUnchecked(0, 0),
                        "Corner tile: out-of-world neighborhood must read as water.");
                    Assert.IsTrue(mask.GetUnchecked(63, 63),
                        "Corner tile: inward corner must remain land.");
                }
                finally { mask.Dispose(); }
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void Sampler_TileOutOfRange_Throws()
        {
            var wd = new GridDomain2D(8, 8);
            var world = new MaskGrid2D(wd, Allocator.Persistent, clearToZero: true);
            try
            {
                var s = new float[9];
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => ShapeMaskBuilder2D.SampleNeighborhood3x3(in world, 8, 0, s));
            }
            finally { world.Dispose(); }
        }

        [Test]
        public void EndToEnd_WorldTileMask_SameMaskSameSeed_SameMap()
        {
            // World pass: base terrain only (no later stage mutates Land).
            var wDomain = new GridDomain2D(16, 16);
            var wInputs = new MapInputs(777u, wDomain, MapTunables2D.Default);
            var wCtx = new MapContext2D(wDomain, Allocator.Persistent);
            MaskGrid2D mask = default;
            try
            {
                wCtx.BeginRun(in wInputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref wCtx, in wInputs);
                ref MaskGrid2D worldLand = ref wCtx.GetLayer(MapLayerId.Land);

                var lDomain = new GridDomain2D(64, 64);
                mask = ShapeMaskBuilder2D.BuildFromWorldTile(
                    in worldLand, 8, 8, in lDomain, 0.5f, Allocator.Persistent);

                var lInputs = new MapInputs(12345u, lDomain, MapTunables2D.Default,
                    new MapShapeInput(mask));

                ulong RunOnce()
                {
                    var ctx = new MapContext2D(lDomain, Allocator.Persistent);
                    try
                    {
                        ctx.BeginRun(in lInputs, clearLayers: true);
                        new Stage_BaseTerrain2D().Execute(ref ctx, in lInputs);
                        return ctx.GetLayer(MapLayerId.Land).SnapshotHash64(includeDimensions: true);
                    }
                    finally { ctx.Dispose(); }
                }

                Assert.AreEqual(RunOnce(), RunOnce(),
                    "Same world-derived mask + same seed must produce an identical local map.");
            }
            finally
            {
                if (mask.IsCreated) mask.Dispose();
                wCtx.Dispose();
            }
        }
    }
}