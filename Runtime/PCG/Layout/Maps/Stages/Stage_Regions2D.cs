using System.Collections.Generic;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// Phase M2.b — Biome Region Detection and Naming.
    ///
    /// Partitions the land surface into contiguous biome regions via 4-way
    /// connected-component analysis (CCA) over <see cref="MapFieldId.Biome"/>,
    /// merges undersized specks, and optionally assigns deterministic names via
    /// <see cref="RegionNameTableAsset"/>.
    ///
    /// ── Algorithm ──────────────────────────────────────────────────────────
    ///
    ///   Pass 1 — CCA (D1/D2)
    ///     BFS over land cells in row-major order.  Two cells are in the same
    ///     component iff they share an edge (4-way, no diagonals — D1) AND
    ///     have the exact same <see cref="BiomeType"/> value (D2).
    ///     Anchor = first cell of each component encountered in row-major scan.
    ///
    ///   Pass 2 — Adjacency
    ///     Single scan; records which distinct components share an edge.
    ///
    ///   Pass 3 — Speck merge (D3)
    ///     Components with CellCount &lt; <see cref="SpeckThreshold"/> are merged
    ///     into their largest neighbor (by current CellCount).  Ties broken by
    ///     the lowest anchor row-major index (deterministic).  Specks processed
    ///     in ascending anchor order.  A speck grown to ≥ SpeckThreshold by a
    ///     prior merge in the same pass is not re-merged.
    ///
    ///   Pass 4 — Compact ID assignment + field write
    ///     Final region IDs are 1-based and compacted in row-major encounter
    ///     order.  Water cells remain 0.
    ///
    ///   Pass 5 — Name registry
    ///     When <see cref="NameTable"/> is assigned, each final region receives a
    ///     name via hash(seed, biome, anchorRowMajor) into the biome's pool.
    ///     Names live in <see cref="LastBuiltRegistry"/>; they do NOT enter the
    ///     field grid (D6).
    ///
    /// ── Reads (read-only) ──────────────────────────────────────────────────
    ///   <see cref="MapLayerId.Land"/>       — land sentinel
    ///   <see cref="MapFieldId.Biome"/>      — per-cell BiomeType (int-as-float)
    ///
    /// ── Writes (authoritative) ─────────────────────────────────────────────
    ///   <see cref="MapFieldId.BiomeRegionId"/> — 0 for water; 1-based int for land
    ///
    /// ── Contracts ──────────────────────────────────────────────────────────
    ///   R-1  Determinism — same seed + tunables → identical field + registry.
    ///   R-2  Water sentinel — BiomeRegionId == 0 for all non-Land cells.
    ///   R-3  Land coverage — BiomeRegionId > 0 for all Land cells with valid biome.
    ///   R-4  Biome consistency — all cells in a region share the same BiomeType.
    ///   R-5  Connectivity — all cells in a region are 4-connected.
    ///   R-6  No-mutate — Land, Biome and all other existing fields unchanged.
    ///   R-7  Cross-seed stability is an explicit NON-GOAL.  Region IDs are
    ///         intra-map stable only; they will differ across seeds.  Callers
    ///         must not persist region IDs across generation runs.
    ///   R-8  Speck threshold — no final region has CellCount &lt; SpeckThreshold
    ///         unless it is entirely isolated (no land neighbours at all).
    ///
    /// ── RNG ────────────────────────────────────────────────────────────────
    ///   Zero ctx.Rng consumption.  Name selection uses a pure integer hash of
    ///   (seed, biome, anchorRowMajor) — no UnityEngine.Random.
    ///
    /// ── Visual smoke test ──────────────────────────────────────────────────
    ///   ScalarOverlaySource: BiomeRegionId field, palette = "region".
    ///   (1) Baseline sanity: distinct colour per integer value; water = black (0).
    ///   (2) Biome isolation: overlay BiomeRegionId alongside Biome; no region
    ///       should straddle a biome colour boundary.
    ///   (3) Speck check: zoom in to coast/biome borders; no isolated 1–3-cell
    ///       islands of a single colour (all should be absorbed into neighbours).
    ///   (4) Seed variation: two runs with different seeds produce visually
    ///       different partitions with the same biome colour structure.
    ///   Console hash log: capture BiomeRegionId hash via golden snapshot after
    ///   capture to lock determinism baseline.
    ///   Red flags: any white/default colour (unwritten cell), any region crossing
    ///   a biome boundary, any persistent 1-cell isolated region near coasts.
    ///
    /// ── Pipeline position ──────────────────────────────────────────────────
    ///   After Stage_Vegetation2D (M2.a tail) — D7.
    ///   Append-only; does not alter any prior stage output.
    /// </summary>
    public sealed class Stage_Regions2D : IMapStage2D
    {
        public string Name => "regions";

        // =====================================================================
        // Tunables
        // =====================================================================

        /// <summary>
        /// Minimum cell count a region must have to survive as-is.
        /// Components with fewer cells are merged into their largest land
        /// neighbour (D3).  Must be ≥ 1.
        /// </summary>
        public int SpeckThreshold = 4;

        /// <summary>
        /// Optional: assign names to regions.  When null, <see cref="LastBuiltRegistry"/>
        /// is populated with empty-string names (region IDs and biomes still recorded).
        /// </summary>
        public RegionNameTableAsset NameTable;

        // =====================================================================
        // Post-execute output (D6: names live in sibling registry, not in field)
        // =====================================================================

        /// <summary>
        /// Built during <see cref="Execute"/>; populated after each call.
        /// Maps final 1-based region IDs to their metadata and (optionally) names.
        /// Callers retrieve this after Execute; do not cache across generation runs (R-7).
        /// </summary>
        public RegionNameRegistry2D LastBuiltRegistry { get; private set; }

        // =====================================================================
        // Execute
        // =====================================================================

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;
            int total = d.Length;

            // ---- Read-only inputs ----
            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref ScalarField2D biomeField = ref ctx.GetField(MapFieldId.Biome);

            // ---- Authoritative output ----
            ref ScalarField2D regionField = ref ctx.EnsureField(MapFieldId.BiomeRegionId);

            // ---- Pass 1: 4-way CCA in row-major order ----
            int[] cellLabel = new int[total]; // 0 = water / unvisited
            var components = new List<ComponentInfo>();
            RunCCA(in land, in biomeField, cellLabel, components, w, h);

            // ---- Pass 2: build inter-component adjacency ----
            HashSet<int>[] neighborSets = BuildAdjacency(cellLabel, components.Count, w, h);

            // ---- Pass 3: speck merge ----
            int[] redirect = MergeSpecks(components, neighborSets, SpeckThreshold);

            // ---- Pass 4: compact ID assignment + field write ----
            int[] compactId = WriteField(ref regionField, cellLabel, components, redirect, total);

            // ---- Pass 5: name registry ----
            LastBuiltRegistry = BuildRegistry(components, redirect, compactId, inputs.Seed, NameTable);
        }

        // =====================================================================
        // Pass 1 — Connected Component Analysis
        // =====================================================================

        private static void RunCCA(
            in MaskGrid2D land,
            in ScalarField2D biome,
            int[] cellLabel,
            List<ComponentInfo> components,
            int w, int h)
        {
            var queue = new Queue<int>(256);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;

                    // Skip water or already-visited cells.
                    if (!land.GetUnchecked(x, y)) continue;
                    if (cellLabel[idx] != 0) continue;

                    int biomeId = (int)biome.Values[idx];
                    if (biomeId <= 0) continue; // water sentinel in biome field

                    // New component discovered — BFS from this anchor.
                    int componentId = components.Count + 1; // 1-based
                    int anchorIndex = idx;
                    int cellCount = 0;

                    queue.Clear();
                    queue.Enqueue(idx);
                    cellLabel[idx] = componentId;

                    while (queue.Count > 0)
                    {
                        int ci = queue.Dequeue();
                        cellCount++;

                        int cx = ci % w;
                        int cy = ci / w;

                        // 4-way: left, right, up, down.
                        TryLabel(cx - 1, cy, w, h, componentId, biomeId,
                            in land, in biome, cellLabel, queue);
                        TryLabel(cx + 1, cy, w, h, componentId, biomeId,
                            in land, in biome, cellLabel, queue);
                        TryLabel(cx, cy - 1, w, h, componentId, biomeId,
                            in land, in biome, cellLabel, queue);
                        TryLabel(cx, cy + 1, w, h, componentId, biomeId,
                            in land, in biome, cellLabel, queue);
                    }

                    components.Add(new ComponentInfo
                    {
                        BiomeId = biomeId,
                        AnchorIndex = anchorIndex,
                        CellCount = cellCount,
                    });
                }
            }
        }

        private static void TryLabel(
            int x, int y, int w, int h,
            int componentId, int biomeId,
            in MaskGrid2D land,
            in ScalarField2D biome,
            int[] cellLabel,
            Queue<int> queue)
        {
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;

            int idx = y * w + x;
            if (cellLabel[idx] != 0) return;
            if (!land.GetUnchecked(x, y)) return;
            if ((int)biome.Values[idx] != biomeId) return;

            cellLabel[idx] = componentId;
            queue.Enqueue(idx);
        }

        // =====================================================================
        // Pass 2 — Inter-component Adjacency
        // =====================================================================

        private static HashSet<int>[] BuildAdjacency(
            int[] cellLabel, int componentCount, int w, int h)
        {
            var sets = new HashSet<int>[componentCount];
            for (int i = 0; i < componentCount; i++) sets[i] = new HashSet<int>();

            int total = w * h;
            for (int idx = 0; idx < total; idx++)
            {
                int a = cellLabel[idx];
                if (a == 0) continue;

                int x = idx % w;
                int y = idx / w;

                // Check right and down only; the reverse edge is recorded from the
                // neighbour's turn, so each pair is recorded once (set deduplicates).
                if (x + 1 < w)
                {
                    int b = cellLabel[idx + 1];
                    if (b != 0 && b != a)
                    {
                        sets[a - 1].Add(b);
                        sets[b - 1].Add(a);
                    }
                }

                if (y + 1 < h)
                {
                    int b = cellLabel[idx + w];
                    if (b != 0 && b != a)
                    {
                        sets[a - 1].Add(b);
                        sets[b - 1].Add(a);
                    }
                }
            }

            return sets;
        }

        // =====================================================================
        // Pass 3 — Speck Merge (D3)
        // =====================================================================

        private static int[] MergeSpecks(
            List<ComponentInfo> components,
            HashSet<int>[] neighborSets,
            int threshold)
        {
            int n = components.Count;

            // redirect[i] = target component id (1-based, i in [1..n]).
            // Initialised to identity.
            int[] redirect = new int[n + 1];
            for (int i = 1; i <= n; i++) redirect[i] = i;

            // Collect specks in ascending anchor order.
            // Components were built in row-major scan so AnchorIndex is already
            // ascending, but sort explicitly for contract clarity.
            var specks = new List<int>(32);
            for (int i = 1; i <= n; i++)
            {
                if (components[i - 1].CellCount < threshold)
                    specks.Add(i);
            }

            specks.Sort((a, b) =>
                components[a - 1].AnchorIndex.CompareTo(components[b - 1].AnchorIndex));

            foreach (int speckId in specks)
            {
                // Resolve through redirect chain (handles cascaded merges).
                int resolved = Resolve(speckId, redirect);

                // If this component was already grown to threshold by a prior merge, skip.
                if (components[resolved - 1].CellCount >= threshold) continue;

                // Find best neighbour: largest count; tie → smallest anchor index.
                int bestId = -1;
                int bestCount = -1;
                int bestAnchor = int.MaxValue;

                foreach (int rawNb in neighborSets[speckId - 1])
                {
                    int nb = Resolve(rawNb, redirect);
                    if (nb == resolved) continue; // collapsed to self

                    ComponentInfo nbInfo = components[nb - 1];
                    if (nbInfo.CellCount > bestCount
                        || (nbInfo.CellCount == bestCount && nbInfo.AnchorIndex < bestAnchor))
                    {
                        bestId = nb;
                        bestCount = nbInfo.CellCount;
                        bestAnchor = nbInfo.AnchorIndex;
                    }
                }

                if (bestId < 0) continue; // fully isolated speck — leave as-is (R-8 note)

                // Merge: redirect resolved → bestId; grow bestId count.
                ComponentInfo winner = components[bestId - 1];
                components[bestId - 1] = new ComponentInfo
                {
                    BiomeId = winner.BiomeId,
                    AnchorIndex = winner.AnchorIndex,
                    CellCount = winner.CellCount + components[resolved - 1].CellCount,
                };

                redirect[resolved] = bestId;
            }

            return redirect;
        }

        /// <summary>Resolves a component ID through the redirect chain (no path compression; maps are small).</summary>
        private static int Resolve(int id, int[] redirect)
        {
            while (redirect[id] != id) id = redirect[id];
            return id;
        }

        // =====================================================================
        // Pass 4 — Write BiomeRegionId field
        // =====================================================================

        /// <summary>
        /// Assigns compact 1-based IDs in row-major encounter order and writes
        /// them to <paramref name="regionField"/>.  Returns the compactId mapping
        /// (indexed by resolved component ID, 1-based) for use by the registry builder.
        /// </summary>
        private static int[] WriteField(
            ref ScalarField2D regionField,
            int[] cellLabel,
            List<ComponentInfo> components,
            int[] redirect,
            int total)
        {
            int n = components.Count;
            int[] compactId = new int[n + 1]; // 0 = not yet assigned
            int nextId = 1;

            for (int idx = 0; idx < total; idx++)
            {
                int raw = cellLabel[idx];
                if (raw == 0)
                {
                    regionField.Values[idx] = 0f; // R-2: water sentinel
                    continue;
                }

                int resolved = Resolve(raw, redirect);

                if (compactId[resolved] == 0)
                    compactId[resolved] = nextId++;

                regionField.Values[idx] = (float)compactId[resolved];
            }

            return compactId;
        }

        // =====================================================================
        // Pass 5 — Name registry
        // =====================================================================

        private static RegionNameRegistry2D BuildRegistry(
            List<ComponentInfo> components,
            int[] redirect,
            int[] compactId,
            uint seed,
            RegionNameTableAsset nameTable)
        {
            var registry = new RegionNameRegistry2D();
            int n = components.Count;

            for (int i = 1; i <= n; i++)
            {
                // Only record canonical (non-redirected) components.
                if (Resolve(i, redirect) != i) continue;

                int finalId = compactId[i];
                if (finalId == 0) continue; // never written (shouldn't happen)

                ComponentInfo info = components[i - 1];
                string name = nameTable != null
                    ? nameTable.SelectName(seed, (BiomeType)info.BiomeId, info.AnchorIndex)
                    : string.Empty;

                registry.Add(finalId, new RegionNameRegistry2D.RegionEntry
                {
                    BiomeId = info.BiomeId,
                    AnchorIndex = info.AnchorIndex,
                    CellCount = info.CellCount,
                    Name = name,
                });
            }

            return registry;
        }

        // =====================================================================
        // ComponentInfo — internal scratch struct
        // =====================================================================

        private struct ComponentInfo
        {
            /// <summary>BiomeType int value shared by all cells in this component.</summary>
            public int BiomeId;

            /// <summary>Row-major index of the first cell encountered during CCA scan.</summary>
            public int AnchorIndex;

            /// <summary>Current cell count (updated during speck merge).</summary>
            public int CellCount;
        }
    }
}