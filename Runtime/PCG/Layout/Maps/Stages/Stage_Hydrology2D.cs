using System.Collections.Generic;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// Phase L — Hydrology: River and Lake Detection.
    ///
    /// Derives water-feature masks and a flow accumulation field from the existing
    /// Height field via topological simulation — no noise, no RNG.
    ///
    /// ── Algorithm ─────────────────────────────────────────────────────────────
    ///
    ///   Sub-stage L.1 — River Generation (four sequential steps):
    ///
    ///   Step 1 — Priority-Flood+ε (HeightFieldHydrologyOps2D.FillDepressions)
    ///     Eliminates local minima so every Land cell drains monotonically to
    ///     the coastline.  Floods inward from coastal Land cells (8-adjacent to
    ///     non-Land or OOB).  Depressions are raised to coast_height + k·ε.
    ///     Uses a SortedSet min-heap keyed on (height, cellIndex) for determinism.
    ///
    ///   Step 2 — D8 flow directions (HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8)
    ///     Assigns each Land cell a direction (0–7) pointing to its steepest
    ///     downslope 8-neighbor.  Non-Land neighbors treated as sea level (h=0).
    ///
    ///   Step 3 — Flow accumulation (HeightFieldHydrologyOps2D.AccumulateFlow)
    ///     Sorts Land cells high-to-low and propagates upstream counts downstream.
    ///     Writes <see cref="MapFieldId.FlowAccumulation"/> — raw cell counts,
    ///     0f for non-Land, ≥1f for all Land cells.
    ///
    ///   Step 4 — River extraction (HeightFieldHydrologyOps2D.ExtractRivers)
    ///     Thresholds FlowAccumulation at <c>totalLandCells × riverThresholdFraction</c>.
    ///     Writes <see cref="MapLayerId.Rivers"/> ⊆ Land.
    ///
    ///   Sub-stage L.2 — Lake Detection
    ///     Lakes = NOT Land AND NOT DeepWater AND NOT ShallowWater.
    ///     A simple boolean mask — no CCA required unless <see cref="MinLakeArea"/> > 0.
    ///     Optional: BFS CCA removes connected components smaller than MinLakeArea.
    ///     Writes <see cref="MapLayerId.Lakes"/>.
    ///
    /// ── Reads (read-only) ──────────────────────────────────────────────────────
    ///   <see cref="MapFieldId.Height"/>        (F2) — height field for simulation
    ///   <see cref="MapLayerId.Land"/>          (F2) — land/water boundary
    ///   <see cref="MapLayerId.DeepWater"/>     (F2) — border-connected ocean
    ///   <see cref="MapLayerId.ShallowWater"/>  (F4) — 1-cell coastal ring
    ///
    /// ── Writes (authoritative) ─────────────────────────────────────────────────
    ///   <see cref="MapFieldId.FlowAccumulation"/> — raw upstream cell count
    ///   <see cref="MapLayerId.Rivers"/>            — river mask (⊆ Land)
    ///   <see cref="MapLayerId.Lakes"/>             — inland water bodies
    ///
    /// ── Contracts ─────────────────────────────────────────────────────────────
    ///   L-1  Determinism — same seed + tunables → identical outputs.
    ///   L-2  Rivers ⊆ Land.
    ///   L-3  Lakes ⊆ NOT-Land.
    ///   L-4  Lakes ∩ DeepWater == ∅.
    ///   L-5  Lakes ∩ ShallowWater == ∅.
    ///   L-6  Rivers ∩ Lakes == ∅ (by construction: rivers on Land, lakes off Land).
    ///   L-7  FlowAccumulation: 0f for non-Land, ≥1f for all Land cells.
    ///   L-8  FlowAccumulation conservation: total flow exiting to non-Land == total
    ///        Land cell count (all drainage reaches the ocean).
    ///   L-9  Rivers == threshold filter of FlowAccumulation (no post-processing).
    ///   L-10 No-mutate: all existing fields/layers unchanged.
    ///
    /// ── RNG ────────────────────────────────────────────────────────────────────
    ///   Zero ctx.Rng consumption.  All algorithms deterministic by construction.
    ///
    /// ── Temp buffers ───────────────────────────────────────────────────────────
    ///   filledHeight (float[])  — stage-local, freed at stage exit.
    ///   flowDir      (int[])    — stage-local, freed at stage exit.
    ///   On a 256×256 map: ~512 KB combined.  GC-collected after Execute returns.
    ///
    /// ── Visual smoke test ──────────────────────────────────────────────────────
    ///   (1) Baseline sanity: FlowAccumulation overlay (source=6); bright spots
    ///       at river mouths, dark on peaks. No bright non-Land cells.
    ///   (2) Rivers layer isolated: enable Rivers-only procedural color (e.g. cyan);
    ///       thin branching tree structures draining to coast. No isolated inland blobs.
    ///   (3) Cross-check: Rivers ∩ Land — all river cells should be land-colored
    ///       in the base tilemap. No river cells in water zones.
    ///   (4) Seed variation: two seeds produce different river networks with the
    ///       same general character (draining from peaks to coast).
    ///   Console hash log: capture FlowAccumulation, Rivers, Lakes hashes via golden
    ///   snapshot after first green run to lock the determinism baseline.
    ///   Red flags: bright FlowAccumulation on water cells; rivers ending inland
    ///   (not reaching coast); Lakes overlapping DeepWater or ShallowWater.
    ///
    /// ── Pipeline position ──────────────────────────────────────────────────────
    ///   After Stage_Morphology2D (Phase G), before Stage_Biome2D (Phase M).
    ///   Append-only; does not alter any prior stage output.
    /// </summary>
    public sealed class Stage_Hydrology2D : IMapStage2D
    {
        public string Name => "hydrology";

        // =====================================================================
        // Tunables
        // =====================================================================

        /// <summary>
        /// Priority-Flood+ε gradient increment.
        /// Applied when raising a depression cell above its outlet.
        /// Range [1e-6, 1e-3]. Default 1e-5f.
        /// </summary>
        public float epsilon = 1e-5f;

        /// <summary>
        /// River threshold as a fraction of total Land cells.
        /// A Land cell becomes a river if at least this fraction of all Land cells
        /// drain through it.  Auto-scales with resolution.
        /// Range [0.005, 0.10]. Default 0.02f (2%).
        /// </summary>
        public float riverThresholdFraction = 0.02f;

        /// <summary>
        /// Minimum lake component size in cells.
        /// Lake fragments smaller than this are removed (treated as unclassified water).
        /// 0 = disabled (no filtering). Default 0.
        /// </summary>
        public int minLakeArea = 0;

        // =====================================================================
        // Execute
        // =====================================================================

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;
            int total = d.Length;

            // ── Read-only inputs ──
            ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);
            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D deepWater = ref ctx.GetLayer(MapLayerId.DeepWater);
            ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);

            // ── Authoritative outputs ──
            ref ScalarField2D flowAccum = ref ctx.EnsureField(MapFieldId.FlowAccumulation, clearToZero: true);
            ref MaskGrid2D rivers = ref ctx.EnsureLayer(MapLayerId.Rivers, clearToZero: true);
            ref MaskGrid2D lakes = ref ctx.EnsureLayer(MapLayerId.Lakes, clearToZero: true);

            // ── Stage-local temp buffers (freed when Execute returns) ──
            var filledHeight = new float[total];
            var flowDir = new int[total];

            // ── Sub-stage L.1: River Generation ──

            // Step 1: Priority-Flood+ε
            HeightFieldHydrologyOps2D.FillDepressions(ref height, ref land, filledHeight, w, h, epsilon);

            // Step 2: D8 flow directions
            HeightFieldHydrologyOps2D.ComputeFlowDirectionsD8(filledHeight, ref land, flowDir, w, h);

            // Step 3: Flow accumulation → FlowAccumulation field
            HeightFieldHydrologyOps2D.AccumulateFlow(filledHeight, ref land, flowDir, ref flowAccum, w, h);

            // Step 4: River extraction
            int landCount = land.CountOnes();
            float threshold = landCount * riverThresholdFraction;
            HeightFieldHydrologyOps2D.ExtractRivers(ref flowAccum, ref land, threshold, ref rivers, w, h);

            // ── Sub-stage L.2: Lake Detection ──

            // Lakes = NOT Land AND NOT DeepWater AND NOT ShallowWater.
            // Excludes the 1-cell ShallowWater ring — a 1-cell-wide depression is
            // entirely ShallowWater with no Lakes cells (natural visual layering).
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isLake = !land.GetUnchecked(x, y)
                               && !deepWater.GetUnchecked(x, y)
                               && !shallowWater.GetUnchecked(x, y);
                    lakes.SetUnchecked(x, y, isLake);
                }
            }

            // Optional: remove lake fragments smaller than minLakeArea via 4-way BFS CCA.
            if (minLakeArea > 1)
                FilterSmallLakes(ref lakes, w, h, minLakeArea);
        }

        // =====================================================================
        // Lake size filter — 4-way BFS CCA (inline, no external dependency)
        // =====================================================================

        /// <summary>
        /// Removes connected lake components (4-connectivity) with fewer than
        /// <paramref name="minArea"/> cells.  Cleared cells become unclassified water
        /// (not Land, not Deep, not Shallow, not Lake) — appropriate for tiny puddles.
        /// </summary>
        private static void FilterSmallLakes(ref MaskGrid2D lakes, int w, int h, int minArea)
        {
            int total = w * h;
            var visited = new bool[total];

            // 4-connected neighbor offsets
            var n4 = new (int dx, int dy)[] { (0, -1), (1, 0), (0, 1), (-1, 0) };

            var component = new List<int>(minArea * 4);
            var queue = new Queue<int>(minArea * 4);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int root = y * w + x;
                    if (!lakes.GetUnchecked(x, y)) continue;
                    if (visited[root]) continue;

                    // BFS from this lake cell.
                    component.Clear();
                    queue.Clear();

                    visited[root] = true;
                    queue.Enqueue(root);
                    component.Add(root);

                    while (queue.Count > 0)
                    {
                        int ci = queue.Dequeue();
                        int cx = ci % w;
                        int cy = ci / w;

                        for (int k = 0; k < 4; k++)
                        {
                            int nx = cx + n4[k].dx;
                            int ny = cy + n4[k].dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;

                            int ni = ny * w + nx;
                            if (!lakes.GetUnchecked(nx, ny)) continue;
                            if (visited[ni]) continue;

                            visited[ni] = true;
                            queue.Enqueue(ni);
                            component.Add(ni);
                        }
                    }

                    // Remove this component if it is below the minimum area.
                    if (component.Count < minArea)
                    {
                        for (int k = 0; k < component.Count; k++)
                        {
                            int ci = component[k];
                            lakes.SetUnchecked(ci % w, ci / w, false);
                        }
                    }
                }
            }
        }
    }
}