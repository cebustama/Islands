using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Stages;
using Islands.PCG.Adapters.Tilemap;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Unit tests for <see cref="MegaTileScanner"/>.
    ///
    /// Covers: empty layer, single cell, exact 2×2, 4×4, 3×2 greedy,
    /// L-shape non-overlap, determinism, full pipeline round-trip,
    /// read-only invariant.
    ///
    /// Phase H8.
    /// </summary>
    public sealed class MegaTileScannerTests
    {
        private const uint TestSeed = 42u;

        // ─────────────────────────────────────────────────────────────────────
        // Test 1: Empty L2 layer → zero placements
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_EmptyL2_ZeroPlacements()
        {
            var export = BuildExport(4, 4);
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);
            Assert.AreEqual(0, placements.Count);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 2: Single L2 cell → zero placements
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_SingleL2Cell_ZeroPlacements()
        {
            var export = BuildExport(4, 4, (x, y) => x == 1 && y == 1);
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);
            Assert.AreEqual(0, placements.Count);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 3: Exact 2×2 L2 block → one placement at (0,0)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_Exact2x2_OnePlacement()
        {
            var export = BuildExport(2, 2, (x, y) => true);
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);
            Assert.AreEqual(1, placements.Count);
            Assert.AreEqual(0, placements[0].X);
            Assert.AreEqual(0, placements[0].Y);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 4: 4×4 all-L2 block → four placements (2×2 grid of blocks)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_4x4Block_FourPlacements()
        {
            var export = BuildExport(4, 4, (x, y) => true);
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);
            Assert.AreEqual(4, placements.Count);

            // Greedy row-major: expect (0,0), (2,0), (0,2), (2,2)
            AssertPlacementExists(placements, 0, 0);
            AssertPlacementExists(placements, 2, 0);
            AssertPlacementExists(placements, 0, 2);
            AssertPlacementExists(placements, 2, 2);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 5: 3×2 L2 block → one placement (greedy claims left, orphan column)
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_3x2Block_OnePlacement()
        {
            // 3 wide × 2 tall block starting at (0,0)
            var export = BuildExport(4, 4, (x, y) => x < 3 && y < 2);
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);
            Assert.AreEqual(1, placements.Count);
            Assert.AreEqual(0, placements[0].X);
            Assert.AreEqual(0, placements[0].Y);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 6: L-shaped region → correct non-overlapping placements
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_LShape_NoOverlap()
        {
            // L-shape: 4 wide bottom row (2 tall), 2 wide left column extends up (4 tall total)
            //
            // Pipeline coords (y up):
            // y=3: XX..
            // y=2: XX..
            // y=1: XXXX
            // y=0: XXXX
            var export = BuildExport(4, 4, (x, y) =>
                (y < 2 && x < 4) || (y >= 2 && x < 2));
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);

            // Verify no cell appears in two placements
            var claimedCells = new HashSet<(int, int)>();
            foreach (var p in placements)
            {
                var cells = new[] {
                    (p.X, p.Y), (p.X+1, p.Y), (p.X, p.Y+1), (p.X+1, p.Y+1)
                };
                foreach (var c in cells)
                {
                    Assert.IsFalse(claimedCells.Contains(c),
                        $"Cell ({c.Item1},{c.Item2}) claimed by two placements.");
                    claimedCells.Add(c);
                }
            }

            // Greedy row-major on this L: (0,0), (2,0), (0,2) — 3 placements
            Assert.AreEqual(3, placements.Count);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 7: Non-overlap invariant — no cell in two different placements
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_NonOverlapInvariant_6x6()
        {
            // Fill a 6×6 region to stress the greedy scan
            var export = BuildExport(6, 6, (x, y) => true);
            var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);

            var claimedCells = new HashSet<(int, int)>();
            foreach (var p in placements)
            {
                for (int dy = 0; dy <= 1; dy++)
                    for (int dx = 0; dx <= 1; dx++)
                    {
                        var cell = (p.X + dx, p.Y + dy);
                        Assert.IsFalse(claimedCells.Contains(cell),
                            $"Cell ({cell.Item1},{cell.Item2}) claimed by multiple placements.");
                        claimedCells.Add(cell);
                    }
            }

            // 6×6 all true → 3×3 = 9 placements
            Assert.AreEqual(9, placements.Count);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 8: Determinism — same input → same placements
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_Deterministic_SameInput()
        {
            // Irregular region to exercise edge cases
            var export = BuildExport(8, 8, (x, y) => x < 7 && y < 6);

            var a = MegaTileScanner.Scan(export, MapLayerId.HillsL2);
            var b = MegaTileScanner.Scan(export, MapLayerId.HillsL2);

            Assert.AreEqual(a.Count, b.Count, "Placement count differs between runs.");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, $"Placement {i} X differs.");
                Assert.AreEqual(a[i].Y, b[i].Y, $"Placement {i} Y differs.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 9: Full pipeline round-trip — all placement cells are within L2
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_FullPipelineRoundTrip_PlacementsWithinL2()
        {
            // Run the real pipeline to get a natural L2 distribution
            var domain = new GridDomain2D(64, 64);
            var tunables = MapTunables2D.Default;
            var inputs = new MapInputs(TestSeed, domain, tunables);
            var ctx = new MapContext2D(domain, Allocator.Persistent);
            try
            {
                ctx.BeginRun(in inputs, clearLayers: true);
                new Stage_BaseTerrain2D().Execute(ref ctx, in inputs);
                new Stage_Hills2D().Execute(ref ctx, in inputs);

                var export = MapExporter2D.Export(ctx);

                if (!export.HasLayer(MapLayerId.HillsL2))
                {
                    Assert.Inconclusive("HillsL2 layer not present in default pipeline.");
                    return;
                }

                bool[] l2 = export.GetLayer(MapLayerId.HillsL2);
                var placements = MegaTileScanner.Scan(export, MapLayerId.HillsL2);

                foreach (var p in placements)
                {
                    int w = export.Width;
                    Assert.IsTrue(l2[p.X + p.Y * w], $"BL ({p.X},{p.Y}) not L2.");
                    Assert.IsTrue(l2[p.X + 1 + p.Y * w], $"BR ({p.X + 1},{p.Y}) not L2.");
                    Assert.IsTrue(l2[p.X + (p.Y + 1) * w], $"TL ({p.X},{p.Y + 1}) not L2.");
                    Assert.IsTrue(l2[p.X + 1 + (p.Y + 1) * w], $"TR ({p.X + 1},{p.Y + 1}) not L2.");
                }
            }
            finally
            {
                ctx.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Test 10: Scanner does not modify MapDataExport
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void Scan_DoesNotModifyExport()
        {
            var export = BuildExport(4, 4, (x, y) => true);

            // Snapshot the layer before scan
            bool[] before = (bool[])export.GetLayer(MapLayerId.HillsL2).Clone();

            MegaTileScanner.Scan(export, MapLayerId.HillsL2);

            bool[] after = export.GetLayer(MapLayerId.HillsL2);
            Assert.AreEqual(before.Length, after.Length);
            for (int i = 0; i < before.Length; i++)
                Assert.AreEqual(before[i], after[i], $"Export modified at index {i}.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a MapDataExport with HillsL2 populated by a predicate.
        /// When predicate is null, all cells are false (empty layer).
        /// </summary>
        private static MapDataExport BuildExport(int w, int h,
            System.Func<int, int, bool> cellPredicate = null)
        {
            var domain = new GridDomain2D(w, h);
            var inputs = new MapInputs(TestSeed, domain, MapTunables2D.Default);
            var ctx = new MapContext2D(domain, Allocator.Persistent);
            try
            {
                ctx.BeginRun(in inputs, clearLayers: true);
                ref var l2 = ref ctx.EnsureLayer(MapLayerId.HillsL2, clearToZero: true);
                if (cellPredicate != null)
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (cellPredicate(x, y))
                                l2.Set(x, y, true);
                return MapExporter2D.Export(ctx);
            }
            finally
            {
                ctx.Dispose();
            }
        }

        private static void AssertPlacementExists(List<MegaTilePlacement> placements, int x, int y)
        {
            for (int i = 0; i < placements.Count; i++)
                if (placements[i].X == x && placements[i].Y == y)
                    return;
            Assert.Fail($"Expected placement at ({x},{y}) not found.");
        }
    }
}