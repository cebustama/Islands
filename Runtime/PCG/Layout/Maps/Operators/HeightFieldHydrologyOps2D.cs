using System.Collections.Generic;

using Islands.PCG.Fields;
using Islands.PCG.Grids;

namespace Islands.PCG.Layout.Maps.Operators
{
    /// <summary>
    /// Hydrology operators for island heightmaps (Phase L).
    ///
    /// Implements the standard DEM hydrology pipeline:
    ///   1. <see cref="FillDepressions"/>       — Priority-Flood+ε inward from coastline.
    ///   2. <see cref="ComputeFlowDirectionsD8"/> — Steepest-descent D8 assignment.
    ///   3. <see cref="AccumulateFlow"/>          — Highest-to-lowest propagation.
    ///   4. <see cref="ExtractRivers"/>           — Threshold to binary river mask.
    ///
    /// ── Design invariants ────────────────────────────────────────────────────
    ///   • Pure, static, deterministic.  No RNG, no noise, no Unity API calls.
    ///   • Caller provides all output buffers; no internal allocations beyond
    ///     the stage-local heap and sort list inside each method.
    ///   • Temporary buffers (filledHeight, flowDir) are managed float[]/int[]
    ///     allocated by the stage and freed at stage exit.
    ///
    /// ── Neighbor order convention (all methods) ───────────────────────────────
    ///   8-connectivity (Moore neighbourhood), clockwise from North:
    ///     0=N  1=NE  2=E  3=SE  4=S  5=SW  6=W  7=NW
    ///   Cardinal directions have distance 1.0; diagonals have distance √2.
    ///   This fixed order guarantees deterministic tie-breaking everywhere.
    ///
    /// ── 8-connectivity rationale ──────────────────────────────────────────────
    ///   D8 flow directions use Moore-neighbourhood slopes.  Depression filling
    ///   must use the same connectivity for consistency — a raised depression
    ///   must be reachable by D8.  This differs from the pipeline's typical
    ///   4-connectivity (topology, morphology, shores) and is standard for
    ///   heightmap hydrology (Barnes 2014, Pass 2b).
    /// </summary>
    public static class HeightFieldHydrologyOps2D
    {
        // ── Neighbor tables ───────────────────────────────────────────────────
        // Clockwise from North: (dx, dy)
        private static readonly (int dx, int dy)[] DirOffsets =
        {
            ( 0, -1), ( 1, -1), ( 1,  0), ( 1,  1),
            ( 0,  1), (-1,  1), (-1,  0), (-1, -1)
        };

        // Euclidean distance for each direction (cardinal=1, diagonal=√2).
        private static readonly float[] DirDist =
        {
            1f, 1.41421356f, 1f, 1.41421356f,
            1f, 1.41421356f, 1f, 1.41421356f
        };

        // =====================================================================
        // Step 1 — Priority-Flood+ε Depression Filling
        // =====================================================================

        /// <summary>
        /// Fills depressions (local minima) in the height field using Priority-Flood+ε,
        /// adapted for island geometry: coastal Land cells (8-adjacent to any non-Land
        /// or out-of-bounds cell) are the drainage outlets.
        ///
        /// After this step, every Land cell has a monotonically non-increasing drainage
        /// path to the coastline, which guarantees a valid D8 flow direction exists for
        /// every Land cell.
        ///
        /// Uses a <see cref="SortedSet{T}"/> keyed on (filledHeight, cellIndex) for
        /// deterministic dequeue when two cells share the same height value.  Cell index
        /// is row-major: <c>index = x + y * w</c>.
        /// </summary>
        /// <param name="height">Source height field (read-only).</param>
        /// <param name="land">Land mask — only Land cells are processed.</param>
        /// <param name="filledHeight">
        /// Output: depression-free height values.  Non-Land cells receive 0f.
        /// Must be pre-allocated to length <c>w × h</c> by the caller.
        /// </param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        /// <param name="epsilon">
        /// Gradient increment applied when raising a depression cell.
        /// Default 1e-5f.  Ensures unambiguous D8 flow direction after filling
        /// without perceptible changes to the height field.
        /// </param>
        public static void FillDepressions(
            ref ScalarField2D height,
            ref MaskGrid2D land,
            float[] filledHeight,
            int w, int h,
            float epsilon = 1e-5f)
        {
            int total = w * h;
            var visited = new bool[total];

            // Initialise: copy height to buffer; zero non-Land cells.
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    filledHeight[idx] = land.GetUnchecked(x, y) ? height.Values[idx] : 0f;
                }
            }

            // Min-heap ordered by (filledHeight, cellIndex).
            // SortedSet guarantees O(log n) add/remove and stable ordering.
            var heap = new SortedSet<(float fh, int idx)>(HeapComparer.Instance);

            // ── Seed: coastal Land cells (8-adjacent to any non-Land or OOB) ──
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!land.GetUnchecked(x, y)) continue;

                    int idx = y * w + x;
                    bool coastal = false;

                    for (int d = 0; d < 8 && !coastal; d++)
                    {
                        int nx = x + DirOffsets[d].dx;
                        int ny = y + DirOffsets[d].dy;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h || !land.GetUnchecked(nx, ny))
                            coastal = true;
                    }

                    if (!coastal) continue;
                    visited[idx] = true;
                    heap.Add((filledHeight[idx], idx));
                }
            }

            // ── Flood inward ──
            while (heap.Count > 0)
            {
                var (fh, ci) = heap.Min;
                heap.Remove(heap.Min);

                int cx = ci % w;
                int cy = ci / w;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DirOffsets[d].dx;
                    int ny = cy + DirOffsets[d].dy;
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                    int ni = ny * w + nx;
                    if (!land.GetUnchecked(nx, ny)) continue;
                    if (visited[ni]) continue;

                    visited[ni] = true;

                    // Raise depression: neighbor must be strictly above current.
                    if (filledHeight[ni] <= fh)
                        filledHeight[ni] = fh + epsilon;

                    heap.Add((filledHeight[ni], ni));
                }
            }
        }

        // =====================================================================
        // Step 2 — D8 Flow Direction Assignment
        // =====================================================================

        /// <summary>
        /// Assigns each Land cell a D8 flow direction (0–7) pointing toward its
        /// steepest downslope 8-neighbor.  Non-Land cells receive -1.
        ///
        /// Non-Land and out-of-bounds neighbors are treated as sea level (height 0),
        /// so coastal Land cells always drain toward the ocean.
        ///
        /// Tie-breaking: first direction in fixed clockwise order (N→NE→...→NW) wins.
        /// After Priority-Flood+ε, flat-region ties are eliminated in practice; the
        /// tie-break handles any residual float-precision edge cases.
        /// </summary>
        /// <param name="filledHeight">Depression-free height buffer from FillDepressions.</param>
        /// <param name="land">Land mask.</param>
        /// <param name="flowDir">
        /// Output: direction index 0–7 for Land cells, -1 for non-Land.
        /// Must be pre-allocated to length <c>w × h</c>.
        /// </param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        public static void ComputeFlowDirectionsD8(
            float[] filledHeight,
            ref MaskGrid2D land,
            int[] flowDir,
            int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;

                    if (!land.GetUnchecked(x, y))
                    {
                        flowDir[idx] = -1;
                        continue;
                    }

                    float fh = filledHeight[idx];
                    float bestSlope = 0f;
                    int bestDir = -1;

                    for (int d = 0; d < 8; d++)
                    {
                        int nx = x + DirOffsets[d].dx;
                        int ny = y + DirOffsets[d].dy;

                        float nh;
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h)
                            nh = 0f;                       // OOB → sea level
                        else if (!land.GetUnchecked(nx, ny))
                            nh = 0f;                       // non-Land → sea level
                        else
                            nh = filledHeight[ny * w + nx];

                        float slope = (fh - nh) / DirDist[d];
                        if (slope > bestSlope)
                        {
                            bestSlope = slope;
                            bestDir = d;
                        }
                    }

                    // bestDir == -1 only if fh <= 0 for all neighbors (shouldn't happen
                    // post-Priority-Flood on interior Land cells, but guarded).
                    flowDir[idx] = bestDir;
                }
            }
        }

        // =====================================================================
        // Step 3 — Flow Accumulation
        // =====================================================================

        /// <summary>
        /// Computes upstream cell counts via a highest-to-lowest traversal.
        ///
        /// Algorithm:
        ///   1. Sort all Land cells by filledHeight descending (tiebreak: ascending
        ///      row-major index for determinism).
        ///   2. Initialize each Land cell's count to 1 (counts itself).
        ///   3. Traverse high-to-low: each cell passes its accumulated count to its
        ///      D8 downstream neighbor (Land-to-Land only; ocean sinks are ignored).
        ///
        /// Output contract: <c>flowAccum.Values[i] == 0f</c> for non-Land cells;
        /// <c>≥ 1f</c> for all Land cells.  Raw counts — no normalization.
        /// </summary>
        /// <param name="filledHeight">Depression-free height buffer (used for sort order).</param>
        /// <param name="land">Land mask.</param>
        /// <param name="flowDir">D8 directions from <see cref="ComputeFlowDirectionsD8"/>.</param>
        /// <param name="flowAccum">
        /// Output: registered ScalarField2D (<see cref="MapFieldId.FlowAccumulation"/>).
        /// Written in-place.
        /// </param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        public static void AccumulateFlow(
            float[] filledHeight,
            ref MaskGrid2D land,
            int[] flowDir,
            ref ScalarField2D flowAccum,
            int w, int h)
        {
            int total = w * h;

            // ── Initialise ──
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    flowAccum.Values[i] = land.GetUnchecked(x, y) ? 1f : 0f;
                }

            // ── Collect and sort Land cells ──
            var landCells = new List<int>(total / 2);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (land.GetUnchecked(x, y)) landCells.Add(y * w + x);

            // Descending filledHeight; ascending index on tie (row-major, deterministic).
            landCells.Sort((a, b) =>
            {
                float fa = filledHeight[a];
                float fb = filledHeight[b];
                if (fa > fb) return -1;
                if (fa < fb) return 1;
                return a.CompareTo(b);
            });

            // ── Propagate downstream ──
            for (int ci = 0; ci < landCells.Count; ci++)
            {
                int idx = landCells[ci];
                int d = flowDir[idx];
                if (d < 0) continue;   // coastal sink — flow drains to ocean

                int cx = idx % w;
                int cy = idx / w;
                int nx = cx + DirOffsets[d].dx;
                int ny = cy + DirOffsets[d].dy;

                if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                if (!land.GetUnchecked(nx, ny)) continue;   // drain to ocean

                flowAccum.Values[ny * w + nx] += flowAccum.Values[idx];
            }
        }

        // =====================================================================
        // Step 4 — River Extraction
        // =====================================================================

        /// <summary>
        /// Thresholds <paramref name="flowAccum"/> to produce a binary river mask.
        /// A cell is a river iff <c>Land[x,y] AND flowAccum[x,y] ≥ threshold</c>.
        /// </summary>
        /// <param name="flowAccum">Flow accumulation field.</param>
        /// <param name="land">Land mask.</param>
        /// <param name="threshold">Minimum upstream cell count for river classification.</param>
        /// <param name="rivers">Output mask (<see cref="MapLayerId.Rivers"/>).</param>
        /// <param name="w">Grid width.</param>
        /// <param name="h">Grid height.</param>
        public static void ExtractRivers(
            ref ScalarField2D flowAccum,
            ref MaskGrid2D land,
            float threshold,
            ref MaskGrid2D rivers,
            int w, int h)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    rivers.SetUnchecked(x, y, land.GetUnchecked(x, y) && flowAccum.Values[i] >= threshold);
                }
        }

        // =====================================================================
        // Internal — deterministic min-heap comparer
        // =====================================================================

        /// <summary>
        /// Comparer for the Priority-Flood SortedSet.
        /// Primary key: ascending filledHeight (min-heap).
        /// Secondary key: ascending row-major cell index (deterministic tie-break).
        /// </summary>
        private sealed class HeapComparer : IComparer<(float fh, int idx)>
        {
            public static readonly HeapComparer Instance = new HeapComparer();

            public int Compare((float fh, int idx) a, (float fh, int idx) b)
            {
                int c = a.fh.CompareTo(b.fh);
                return c != 0 ? c : a.idx.CompareTo(b.idx);
            }
        }
    }
}