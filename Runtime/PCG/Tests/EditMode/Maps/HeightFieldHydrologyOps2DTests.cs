using System;
using NUnit.Framework;
using Unity.Collections;

using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Layout.Maps.Operators;
using Islands.PCG.Core;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// Operator-level unit tests for <see cref="HeightFieldHydrologyOps2D"/>.
    ///
    /// Uses small synthetic grids (4×4 to 8×8) to verify each operator step in
    /// isolation with known inputs and predictable outputs.  All tests are
    /// deterministic — no seeds, no noise.
    /// </summary>
    public sealed class HeightFieldHydrologyOps2DTests
    {
        // =====================================================================
        // Helpers — synthetic grid construction
        // =====================================================================

        /// <summary>
        /// Creates a ScalarField2D pre-filled with the given values (row-major).
        /// </summary>
        private static ScalarField2D MakeField(int w, int h, float[] values)
        {
            var domain = new GridDomain2D(w, h);
            var field = new ScalarField2D(domain, Allocator.Persistent);
            for (int i = 0; i < values.Length; i++)
                field.Values[i] = values[i];
            return field;
        }

        /// <summary>
        /// Creates a MaskGrid2D with cells set to true wherever the corresponding
        /// value in <paramref name="waterThreshold"/> is exceeded by
        /// <paramref name="heightValues"/>.
        /// </summary>
        private static MaskGrid2D MakeLandMask(int w, int h, float[] heightValues, float threshold)
        {
            var domain = new GridDomain2D(w, h);
            var mask = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    mask.SetUnchecked(x, y, heightValues[y * w + x] >= threshold);
            return mask;
        }

        // =====================================================================
        // FillDepressions tests
        // =====================================================================

        [Test]
        public void FillDepressions_FlatIsland_RetainsOriginalHeight()
        {
            // 3×3 grid: centre cell is land, edges are water.
            // A flat land cell with no depressions should not be altered.
            int w = 3, h = 3;
            float[] heights = {
                0.4f, 0.4f, 0.4f,
                0.4f, 0.8f, 0.4f,   // centre land cell at 0.8
                0.4f, 0.4f, 0.4f
            };
            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f); // only centre ≥ 0.5

            try
            {
                var filledHeight = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filledHeight, w, h);

                // Centre Land cell: should not have been raised.
                int ci = 1 * w + 1;
                Assert.AreEqual(heights[ci], filledHeight[ci], 1e-7f,
                    "Flat land cell with no depressions should retain original height.");
                // Non-Land cells: zero.
                for (int i = 0; i < w * h; i++)
                    if (i != ci)
                        Assert.AreEqual(0f, filledHeight[i], "Non-Land cells must be 0f.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void FillDepressions_SingleDepression_RaisedToOutlet()
        {
            // 5×5 grid: an inland depression surrounded by HIGHER LAND walls.
            //
            // ── Why land walls, not water ──
            // Priority-Flood SEEDS coastal cells (any land cell with a non-Land
            // 8-neighbor) at their own height and marks them visited. The flood
            // loop only updates UNVISITED cells. So if the "depression" is itself
            // coastal (touches water), it never gets raised — the algorithm
            // assumes coastal cells already drain to the ocean.
            //
            // The previous design had a 1×3 land strip on top/bottom water; ALL
            // three cells were coastal, so the depression never entered the flood
            // and stayed at 0.55. The new layout buries the depression in a 3×3
            // land block so its 8-neighbors are all land — only THEN is it inland.
            //
            // Layout:
            //   row 0:  water everywhere
            //   row 1:  water 0.95 0.95 0.95 water    ← upper wall
            //   row 2:  water 0.70 0.55 0.65 water    ← depression (2,2)
            //   row 3:  water 0.95 0.95 0.95 water    ← lower wall
            //   row 4:  water everywhere
            //
            // (2,2) has all-land 8-neighbors → not coastal → flood raises it to
            // its lowest spillway, which is (3,2)=0.65 (right side). After fill
            // the depression sits at 0.65 + ε.
            int w = 5, h = 5;
            float[] heights = {
                0.30f, 0.30f, 0.30f, 0.30f, 0.30f,
                0.30f, 0.95f, 0.95f, 0.95f, 0.30f,
                0.30f, 0.70f, 0.55f, 0.65f, 0.30f,
                0.30f, 0.95f, 0.95f, 0.95f, 0.30f,
                0.30f, 0.30f, 0.30f, 0.30f, 0.30f
            };
            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);

            try
            {
                var filledHeight = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filledHeight, w, h);

                int idxL = 2 * w + 1; // left side cell  (originally 0.70, coastal via col 0 water)
                int idxD = 2 * w + 2; // depression      (originally 0.55, inland)
                int idxR = 2 * w + 3; // right side cell (originally 0.65, coastal via col 4 water)

                // Depression must be strictly raised above its original height.
                Assert.Greater(filledHeight[idxD], 0.55f,
                    "Inland depression must be raised above its original height (0.55).");
                // Depression must reach at least the LOWEST spillway (0.65).
                Assert.GreaterOrEqual(filledHeight[idxD], 0.65f - 1e-6f,
                    "Depression must be raised to at least the lowest outlet height (0.65).");
                // Side cells are coastal — seeded at own height, never raised.
                Assert.AreEqual(0.65f, filledHeight[idxR], 1e-6f,
                    "Lowest side cell (right, 0.65) is coastal — must not be raised.");
                Assert.AreEqual(0.70f, filledHeight[idxL], 1e-6f,
                    "Higher side cell (left, 0.70) is coastal — must not be raised.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void FillDepressions_PeakNeverLowered()
        {
            // Peak cells (highest land) should never be reduced by depression filling.
            int w = 5, h = 5;
            var heights = new float[w * h];
            // Radial distance from centre — higher in the middle.
            float cx = 2f, cy = 2f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = MathF.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    heights[y * w + x] = MathF.Max(0f, 0.9f - d * 0.2f);
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);

            try
            {
                var filledHeight = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filledHeight, w, h);

                // Centre peak at (2,2) should not have been lowered.
                int pi = 2 * w + 2;
                Assert.GreaterOrEqual(filledHeight[pi], heights[pi] - 1e-6f,
                    "Peak cells must not be lowered by depression filling.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void FillDepressions_AllLandCellsDrainToCoast()
        {
            // After fill, every Land cell should be able to reach the coast by
            // always stepping to an equal-or-lower filled height neighbor.
            int w = 8, h = 8;
            var heights = new float[w * h];
            // Random-ish but reproducible heights.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    uint hash = (uint)(x * 2654435761u ^ y * 2246822519u);
                    heights[y * w + x] = (hash % 1000) / 1000f;
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.35f);

            try
            {
                var filledHeight = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filledHeight, w, h, 1e-5f);

                // Every Land cell must have at least one 8-neighbor that is
                // non-Land or has lower/equal filled height.
                int[] dx8 = { 0, 1, 1, 1, 0, -1, -1, -1 };
                int[] dy8 = { -1, -1, 0, 1, 1, 1, 0, -1 };

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!land.GetUnchecked(x, y)) continue;

                        int idx = y * w + x;
                        float fh = filledHeight[idx];
                        bool canDrain = false;

                        for (int k = 0; k < 8; k++)
                        {
                            int nx = x + dx8[k], ny = y + dy8[k];
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                            { canDrain = true; break; }
                            if (!land.GetUnchecked(nx, ny))
                            { canDrain = true; break; }
                            if (filledHeight[ny * w + nx] <= fh)
                            { canDrain = true; break; }
                        }

                        Assert.IsTrue(canDrain,
                            $"Land cell ({x},{y}) has no drainage path after fill.");
                    }
                }
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void FillDepressions_Deterministic()
        {
            int w = 8, h = 8;
            var heights = new float[w * h];
            for (int i = 0; i < heights.Length; i++)
            {
                uint hash = (uint)(i * 2654435761u);
                heights[i] = (hash % 10000) / 10000f;
            }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.3f);

            try
            {
                var bufA = new float[w * h];
                var bufB = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, bufA, w, h);
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, bufB, w, h);

                for (int i = 0; i < bufA.Length; i++)
                    Assert.AreEqual(bufA[i], bufB[i], $"FillDepressions output differs at index {i}.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        // =====================================================================
        // ComputeFlowDirectionsD8 tests
        // =====================================================================

        [Test]
        public void D8_UniformSlope_AllPointDownhill()
        {
            // Eastward-descending ramp on an ALL-LAND plateau. Why all-land:
            // a water boundary (h=0 to D8) would dominate every cell's flow
            // direction — slope to the boundary (~0.8) is much steeper than
            // slope to a slightly-lower land neighbor (~0.1), so the gradient
            // direction would be masked by "coastal cliff" routing.
            //
            // Using a flat-but-higher (0.99) plateau above and below the ramp
            // ensures the ramp cells have only one downhill choice — east.
            int w = 5, h = 3;
            float[] heights = {
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f,
                0.90f, 0.80f, 0.70f, 0.60f, 0.50f,
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f
            };
            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.1f); // all cells are land

            try
            {
                var filled = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);

                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);

                // Inner ramp cells (x=1,2,3) — only downhill option is East (dir=2).
                Assert.AreEqual(2, flowDir[1 * w + 1], "Cell (1,1) should flow East.");
                Assert.AreEqual(2, flowDir[1 * w + 2], "Cell (2,1) should flow East.");
                Assert.AreEqual(2, flowDir[1 * w + 3], "Cell (3,1) should flow East.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void D8_NonLandCells_GetMinusOne()
        {
            // Carry-over assertion from the original test (split out): non-Land
            // cells must always produce flowDir == -1 regardless of geometry.
            int w = 4, h = 4;
            float[] heights = {
                0.2f, 0.2f, 0.2f, 0.2f,
                0.2f, 0.8f, 0.8f, 0.2f,
                0.2f, 0.8f, 0.8f, 0.2f,
                0.2f, 0.2f, 0.2f, 0.2f
            };
            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);

            try
            {
                var filled = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (!land.GetUnchecked(x, y))
                            Assert.AreEqual(-1, flowDir[y * w + x],
                                $"Non-land cell ({x},{y}) must have flowDir = -1.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void D8_DiagonalPreference_SteepestWins()
        {
            // 3×3 grid: centre land cell with one very steep diagonal neighbor.
            // The cardinal direction that is less steep should lose.
            int w = 3, h = 3;
            float[] heights = {
                0.9f, 0.5f, 0.9f,
                0.5f, 0.8f, 0.5f,
                0.9f, 0.5f, 0.9f
            };
            // All cells are land; centre at 0.8 has 8 neighbors at 0.9 or 0.5.
            // Steepest: cardinal neighbors (h=0.5), slope = (0.8-0.5)/1 = 0.3
            //           diagonal neighbors (h=0.9), slope = (0.8-0.9)/√2 = negative
            // Centre should drain to a cardinal (h=0.5) neighbor.

            var heightField = MakeField(w, h, heights);
            var domain = new GridDomain2D(w, h);
            var land = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    land.SetUnchecked(x, y, true);

            try
            {
                var filled = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);

                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);

                // Centre cell: direction should be cardinal (0, 2, 4, or 6), not diagonal.
                int centreDir = flowDir[1 * w + 1];
                bool isCardinal = centreDir == 0 || centreDir == 2 || centreDir == 4 || centreDir == 6;
                Assert.IsTrue(isCardinal,
                    $"Centre cell should drain to a cardinal (steepest) neighbor, got dir={centreDir}.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        [Test]
        public void D8_CoastalCells_HaveValidDirection()
        {
            // After Priority-Flood, all land cells (including coastal) must have a valid
            // non-negative flow direction (they drain to the coast or to lower land).
            int w = 8, h = 8;
            var heights = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = MathF.Max(MathF.Abs(x - 3.5f), MathF.Abs(y - 3.5f));
                    heights[y * w + x] = MathF.Max(0f, 0.9f - d * 0.2f);
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);

            try
            {
                var filled = new float[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);

                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (land.GetUnchecked(x, y))
                            Assert.GreaterOrEqual(flowDir[y * w + x], 0,
                                $"Land cell ({x},{y}) should have a valid flow direction.");
            }
            finally { heightField.Dispose(); land.Dispose(); }
        }

        // =====================================================================
        // AccumulateFlow tests
        // =====================================================================

        [Test]
        public void AccumulateFlow_AllLandCells_AtLeast1()
        {
            // Every Land cell must have flowAccum >= 1 (counts itself).
            int w = 7, h = 7;
            var heights = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = MathF.Sqrt((x - 3f) * (x - 3f) + (y - 3f) * (y - 3f));
                    heights[y * w + x] = MathF.Max(0f, 0.95f - d * 0.18f);
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);
            var domain = new GridDomain2D(w, h);
            var flowAccum = new ScalarField2D(domain, Allocator.Persistent);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref flowAccum, w, h);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (land.GetUnchecked(x, y))
                            Assert.GreaterOrEqual(flowAccum.Values[y * w + x], 1f,
                                $"Land cell ({x},{y}) must have flowAccum >= 1f.");
            }
            finally { heightField.Dispose(); land.Dispose(); flowAccum.Dispose(); }
        }

        [Test]
        public void AccumulateFlow_NonLandCells_Zero()
        {
            int w = 7, h = 7;
            var heights = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = MathF.Sqrt((x - 3f) * (x - 3f) + (y - 3f) * (y - 3f));
                    heights[y * w + x] = MathF.Max(0f, 0.9f - d * 0.2f);
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);
            var domain = new GridDomain2D(w, h);
            var flowAccum = new ScalarField2D(domain, Allocator.Persistent);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref flowAccum, w, h);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (!land.GetUnchecked(x, y))
                            Assert.AreEqual(0f, flowAccum.Values[y * w + x],
                                $"Non-Land cell ({x},{y}) must have flowAccum == 0f.");
            }
            finally { heightField.Dispose(); land.Dispose(); flowAccum.Dispose(); }
        }

        [Test]
        public void AccumulateFlow_LinearDrainage_DownstreamCountEqualsN()
        {
            // 5×3 all-land grid (chosen for h=3 — see "Why h=3" below).
            //
            //   row 0:  0.99 0.99 0.99 0.99 0.99    ← top plateau (border)
            //   row 1:  0.95 0.90 0.80 0.70 0.60    ← buffer + descending chain
            //   row 2:  0.99 0.99 0.99 0.99 0.99    ← bottom plateau (border)
            //
            // ── Why h=3 (not h=4) ──
            // The previous design had plateau cells in INTERIOR rows (no OOB
            // neighbor in any direction). Their D8 routing went diagonally into
            // the chain — slope to a chain cell (~0.13) beat slope to a sibling
            // plateau cell (0). Result: chain accum was 7, not 4.
            //
            // With h=3, every plateau cell is on the top or bottom border, so it
            // has an OOB neighbor (h=0 to D8) giving a steepness of 0.99 — far
            // greater than any slope into the chain. ALL plateau cells drain
            // straight off the map and never contribute to the chain.
            //
            // The (0,1) buffer cell at 0.95 is on the left border too, so it
            // drains W to OOB rather than E into the chain.
            //
            // Expected accumulation:
            //   (1,1) = 1            (chain start, only its own value)
            //   (2,1) = 2            (own + 1 from (1,1))
            //   (3,1) = 3
            //   (4,1) = 4            (rightmost — receives entire upstream chain,
            //                         then exits E to OOB)
            int w = 5, h = 3;
            float[] heights = {
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f,
                0.95f, 0.90f, 0.80f, 0.70f, 0.60f,
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f
            };

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.1f); // all cells are land
            var domain = new GridDomain2D(w, h);
            var flowAccum = new ScalarField2D(domain, Allocator.Persistent);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref flowAccum, w, h);

                // Rightmost chain cell (4,1) — accumulates all 4 chain cells.
                Assert.AreEqual(4f, flowAccum.Values[1 * w + 4], 0.01f,
                    "Rightmost chain cell should accumulate all 4 upstream counts (1+1+1+1).");
                Assert.AreEqual(3f, flowAccum.Values[1 * w + 3], 0.01f, "(3,1) should have accum=3.");
                Assert.AreEqual(2f, flowAccum.Values[1 * w + 2], 0.01f, "(2,1) should have accum=2.");
                Assert.AreEqual(1f, flowAccum.Values[1 * w + 1], 0.01f, "(1,1) should have accum=1.");
            }
            finally { heightField.Dispose(); land.Dispose(); flowAccum.Dispose(); }
        }

        [Test]
        public void AccumulateFlow_TwoTributaries_SumAtConfluence()
        {
            // 7×3 all-land grid: two horizontal arms converge diagonally onto a
            // confluence/drain cell in the bottom border row.
            //
            //   row 0:  0.99 0.99 0.99 0.99 0.99 0.99 0.99    ← top plateau
            //   row 1:  0.95 0.75 0.65 0.99 0.65 0.75 0.95    ← arms + buffer
            //   row 2:  0.99 0.99 0.99 0.55 0.99 0.99 0.99    ← drain at (3,2)
            //
            // Flow:
            //   left arm:   (1,1)=0.75 → E (2,1)=0.65 → SE (3,2)=0.55 → S OOB
            //   right arm:  (5,1)=0.75 → W (4,1)=0.65 → SW (3,2)=0.55 → S OOB
            //   inter-arm plateau (3,1)=0.99 has no OOB exit and no other downhill
            //   neighbor → flows S to (3,2), contributing +1 contamination.
            //
            // Expected: confluence = own (1) + 2 from left arm + 2 from right arm
            //                       + 1 from (3,1) plateau cell           = 6
            //
            // ── Why exact = 6, not the "ideal" 5 ──
            // The all-land plateau strategy keeps chain semantics correct (no
            // coastal-cliff routing) but inevitably includes ONE interior plateau
            // cell — (3,1), the only land cell with no OOB neighbor and no path
            // around the chain — that drains into the confluence. The previous
            // 5×6 design had FOUR such interior cells (gave accum=9). Compressing
            // to 7×3 minimizes them to exactly one.
            int w = 7, h = 3;
            float[] heights = {
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f, 0.99f, 0.99f,
                0.95f, 0.75f, 0.65f, 0.99f, 0.65f, 0.75f, 0.95f,
                0.99f, 0.99f, 0.99f, 0.55f, 0.99f, 0.99f, 0.99f
            };

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.1f); // all cells are land
            var domain = new GridDomain2D(w, h);
            var flowAccum = new ScalarField2D(domain, Allocator.Persistent);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref flowAccum, w, h);

                // Confluence/drain at (3,2).
                float confluenceAccum = flowAccum.Values[2 * w + 3];

                // Lower bound: own + sum-of-arms = 1 + 2 + 2 = 5 must be reached.
                Assert.GreaterOrEqual(confluenceAccum, 5f,
                    $"Confluence must accumulate at least own (1) + left arm (2) + right arm (2) = 5. Got {confluenceAccum}.");
                // Upper bound: at most 1 contamination from the inter-arm plateau cell.
                Assert.LessOrEqual(confluenceAccum, 6f,
                    $"Confluence accum bounded by own + arms + 1 plateau contamination = 6. Got {confluenceAccum}.");

                // Each arm's downstream cell — own + 1 upstream = 2.
                Assert.AreEqual(2f, flowAccum.Values[1 * w + 2], 0.01f,
                    "Left arm downstream cell (2,1) should accumulate own + 1 upstream = 2.");
                Assert.AreEqual(2f, flowAccum.Values[1 * w + 4], 0.01f,
                    "Right arm downstream cell (4,1) should accumulate own + 1 upstream = 2.");
            }
            finally { heightField.Dispose(); land.Dispose(); flowAccum.Dispose(); }
        }

        [Test]
        public void AccumulateFlow_Deterministic()
        {
            int w = 8, h = 8;
            var heights = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = MathF.Sqrt((x - 3.5f) * (x - 3.5f) + (y - 3.5f) * (y - 3.5f));
                    heights[y * w + x] = MathF.Max(0f, 0.9f - d * 0.18f);
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);
            var domain = new GridDomain2D(w, h);
            var accumA = new ScalarField2D(domain, Allocator.Persistent);
            var accumB = new ScalarField2D(domain, Allocator.Persistent);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref accumA, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref accumB, w, h);

                for (int i = 0; i < w * h; i++)
                    Assert.AreEqual(accumA.Values[i], accumB.Values[i],
                        $"AccumulateFlow output differs at index {i}.");
            }
            finally { heightField.Dispose(); land.Dispose(); accumA.Dispose(); accumB.Dispose(); }
        }

        // =====================================================================
        // ExtractRivers tests
        // =====================================================================

        [Test]
        public void ExtractRivers_ThresholdAt50pct_OnlyHighAccumIsCounted()
        {
            // 5×4 all-land grid with a 4-cell descending chain (1,1)..(4,1) at
            // heights 0.90, 0.80, 0.70, 0.60. Plateau cells (0.99) surround the
            // chain to prevent coastal-cliff routing (see linear drainage test).
            //
            // After accumulation:
            //   chain cells:  (1,1)=1, (2,1)=2, (3,1)=3, (4,1)=4
            //   plateau:      every cell = 1 (drains OOB without accumulating)
            //
            // Threshold semantics: "50% of max chain accum" — max=4, threshold=2.
            // Cells with accum ≥ 2 qualify as rivers: (2,1), (3,1), (4,1).
            // The test name preserves the original "ThresholdAt50pct" intent —
            // 50% of the dominant chain's peak accumulation.
            int w = 5, h = 4;
            float[] heights = {
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f,
                0.99f, 0.90f, 0.80f, 0.70f, 0.60f,
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f,
                0.99f, 0.99f, 0.99f, 0.99f, 0.99f
            };

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.1f); // all cells are land
            var domain = new GridDomain2D(w, h);
            var flowAccum = new ScalarField2D(domain, Allocator.Persistent);
            var rivers = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref flowAccum, w, h);

                // Threshold = 2 (50% of max chain accum = 4).
                float threshold = 2f;
                HeightFieldHydrologyOps2D.ExtractRivers(ref flowAccum, ref land, threshold, ref rivers, w, h);

                // High-accumulation chain cells (accum ≥ 2) → rivers.
                Assert.IsTrue(rivers.GetUnchecked(4, 1), "Rightmost chain cell (accum=4) should be a river.");
                Assert.IsTrue(rivers.GetUnchecked(3, 1), "Cell (3,1) (accum=3) should be a river.");
                Assert.IsTrue(rivers.GetUnchecked(2, 1), "Cell (2,1) (accum=2) should be a river.");
                // Chain start (accum=1) → not a river.
                Assert.IsFalse(rivers.GetUnchecked(1, 1), "Chain start (accum=1) should not be a river.");
                // Plateau cells (accum=1 each) → not rivers.
                Assert.IsFalse(rivers.GetUnchecked(0, 0), "Plateau cell should not be a river.");
                Assert.IsFalse(rivers.GetUnchecked(2, 2), "Plateau cell should not be a river.");
            }
            finally { heightField.Dispose(); land.Dispose(); flowAccum.Dispose(); rivers.Dispose(); }
        }

        [Test]
        public void ExtractRivers_RiversSubsetOfLand()
        {
            int w = 8, h = 8;
            var heights = new float[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = MathF.Sqrt((x - 3.5f) * (x - 3.5f) + (y - 3.5f) * (y - 3.5f));
                    heights[y * w + x] = MathF.Max(0f, 0.9f - d * 0.18f);
                }

            var heightField = MakeField(w, h, heights);
            var land = MakeLandMask(w, h, heights, 0.5f);
            var domain = new GridDomain2D(w, h);
            var flowAccum = new ScalarField2D(domain, Allocator.Persistent);
            var rivers = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);

            try
            {
                var filled = new float[w * h];
                var flowDir = new int[w * h];
                HeightFieldHydrologyOps2D.FillDepressions(ref heightField, ref land, filled, w, h);
                HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filled, ref land, flowDir, w, h);
                HeightFieldHydrologyOps2D.AccumulateFlow(filled, ref land, flowDir, ref flowAccum, w, h);
                HeightFieldHydrologyOps2D.ExtractRivers(ref flowAccum, ref land, 2f, ref rivers, w, h);

                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (rivers.GetUnchecked(x, y))
                            Assert.IsTrue(land.GetUnchecked(x, y),
                                $"River cell ({x},{y}) is not on Land — L-2 violation.");
            }
            finally { heightField.Dispose(); land.Dispose(); flowAccum.Dispose(); rivers.Dispose(); }
        }
    }
}