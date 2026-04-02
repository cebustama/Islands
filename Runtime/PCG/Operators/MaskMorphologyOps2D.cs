using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using System;
using Unity.Collections;

namespace Islands.PCG.Operators
{
    /// <summary>
    /// Deterministic morphological operators over MaskGrid2D.
    ///
    /// Contracts:
    /// - 4-neighborhood (W/E/S/N)
    /// - Out-of-bounds neighbors count as OFF
    /// - Row-major scans only; fixed neighbor visit order W/E/S/N
    /// - No unordered collections
    /// - src and dst must be distinct allocations (no in-place support)
    /// </summary>
    public static class MaskMorphologyOps2D
    {
        // -----------------------------------------------------------------------
        // Erosion
        // -----------------------------------------------------------------------

        /// <summary>
        /// Erodes src by one cell (4-neighborhood) into dst.
        /// A cell is ON in dst iff it is ON in src AND all 4 cardinal neighbors are ON in src.
        /// dst is cleared before writing.
        /// </summary>
        public static void Erode4Once(in MaskGrid2D src, ref MaskGrid2D dst)
        {
            ValidateCompatible(in src, in dst, nameof(src), nameof(dst));
            dst.Clear();

            int w = src.Domain.Width;
            int h = src.Domain.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!src.GetUnchecked(x, y)) continue;

                    bool west = x > 0 && src.GetUnchecked(x - 1, y);
                    bool east = x < w - 1 && src.GetUnchecked(x + 1, y);
                    bool south = y > 0 && src.GetUnchecked(x, y - 1);
                    bool north = y < h - 1 && src.GetUnchecked(x, y + 1);

                    if (west && east && south && north)
                        dst.SetUnchecked(x, y, true);
                }
            }
        }

        /// <summary>
        /// Erodes src by <paramref name="radius"/> cells into dst.
        /// radius == 0 copies src into dst unchanged.
        /// Allocates one temporary MaskGrid2D (Allocator.Temp) for multi-pass ping-pong.
        /// </summary>
        public static void Erode4(in MaskGrid2D src, ref MaskGrid2D dst, int radius)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius), "radius must be >= 0.");

            ValidateCompatible(in src, in dst, nameof(src), nameof(dst));

            if (radius == 0) { dst.CopyFrom(src); return; }
            if (radius == 1) { Erode4Once(in src, ref dst); return; }

            // Multi-pass: ping-pong between dst and a temp buffer.
            // Pass 0 (before loop): src  -> dst
            // Pass i (odd):         dst  -> temp
            // Pass i (even):        temp -> dst
            // After loop, if radius is even the last write landed in temp; copy back.
            var temp = new MaskGrid2D(src.Domain, Allocator.Temp, clearToZero: true);
            try
            {
                Erode4Once(in src, ref dst);

                for (int i = 1; i < radius; i++)
                {
                    if (i % 2 == 1) Erode4Once(in dst, ref temp);
                    else Erode4Once(in temp, ref dst);
                }

                if (radius % 2 == 0)
                    dst.CopyFrom(temp);
            }
            finally
            {
                if (temp.IsCreated) temp.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // BFS distance field
        // -----------------------------------------------------------------------

        /// <summary>
        /// Multi-source BFS distance field.
        ///
        /// Seeds every ON cell in <paramref name="seeds"/> at distance 0, then propagates
        /// through cells that are ON in <paramref name="passable"/> using 4-neighborhood BFS.
        ///
        /// Writes integer distances (cast to float) into <paramref name="field"/>.
        /// Cells not reached (non-passable, or passable but beyond maxDist) receive -1f.
        ///
        /// Precondition: seeds cells should be a subset of passable cells. Seed cells that
        /// are NOT in passable are still seeded at distance 0 but will not propagate further.
        ///
        /// Contracts:
        /// - field is cleared to -1f before BFS.
        /// - Seeds are enqueued in row-major order for determinism.
        /// - Neighbor visit order: W / E / S / N.
        /// - BFS stops propagating once nextDist > maxDist; seed cells at dist 0 are always written.
        /// </summary>
        public static void BfsDistanceField(
            in MaskGrid2D seeds,
            in MaskGrid2D passable,
            ref ScalarField2D field,
            int maxDist)
        {
            if (!seeds.IsCreated) throw new InvalidOperationException("seeds must be created.");
            if (!passable.IsCreated) throw new InvalidOperationException("passable must be created.");
            if (!field.IsCreated) throw new InvalidOperationException("field must be created.");
            if (maxDist < 0)
                throw new ArgumentOutOfRangeException(nameof(maxDist), "maxDist must be >= 0.");

            GridDomain2D d = seeds.Domain;
            int w = d.Width;
            int h = d.Height;

            if (passable.Domain.Width != w || passable.Domain.Height != h)
                throw new ArgumentException("passable domain must match seeds domain.", nameof(passable));
            if (field.Domain.Width != w || field.Domain.Height != h)
                throw new ArgumentException("field domain must match seeds domain.", nameof(field));

            int len = d.Length;

            // Sentinel: -1f = not reached
            field.Clear(-1f);

            // dist[i] == -1 means not yet visited; >= 0 means visited
            var dist = new NativeArray<int>(len, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            var queue = new NativeArray<int>(len, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            try
            {
                for (int i = 0; i < len; i++) dist[i] = -1;

                int head = 0, tail = 0;

                // Seed: row-major scan for deterministic queue order
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (!seeds.GetUnchecked(x, y)) continue;
                        int idx = d.Index(x, y);
                        if (dist[idx] >= 0) continue;
                        dist[idx] = 0;
                        field.SetUnchecked(x, y, 0f);
                        queue[tail++] = idx;
                    }
                }

                // BFS
                while (head < tail)
                {
                    int idx = queue[head++];
                    d.Coord(idx, out int cx, out int cy);
                    int nextDist = dist[idx] + 1;
                    if (nextDist > maxDist) continue;

                    // W
                    TryVisit(in passable, dist, field, queue, ref tail, d, w, h, cx - 1, cy, nextDist);
                    // E
                    TryVisit(in passable, dist, field, queue, ref tail, d, w, h, cx + 1, cy, nextDist);
                    // S
                    TryVisit(in passable, dist, field, queue, ref tail, d, w, h, cx, cy - 1, nextDist);
                    // N
                    TryVisit(in passable, dist, field, queue, ref tail, d, w, h, cx, cy + 1, nextDist);
                }
            }
            finally
            {
                if (dist.IsCreated) dist.Dispose();
                if (queue.IsCreated) queue.Dispose();
            }
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static void TryVisit(
            in MaskGrid2D passable,
            NativeArray<int> dist,
            ScalarField2D field,
            NativeArray<int> queue,
            ref int tail,
            in GridDomain2D d,
            int w, int h,
            int nx, int ny,
            int nextDist)
        {
            if ((uint)nx >= (uint)w || (uint)ny >= (uint)h) return;
            if (!passable.GetUnchecked(nx, ny)) return;
            int nIdx = d.Index(nx, ny);
            if (dist[nIdx] >= 0) return;
            dist[nIdx] = nextDist;
            field.SetUnchecked(nx, ny, (float)nextDist);
            queue[tail++] = nIdx;
        }

        private static void ValidateCompatible(
            in MaskGrid2D a, in MaskGrid2D b,
            string nameA, string nameB)
        {
            if (!a.IsCreated) throw new InvalidOperationException($"{nameA} must be created.");
            if (!b.IsCreated) throw new InvalidOperationException($"{nameB} must be created.");
            if (a.Domain.Width != b.Domain.Width ||
                a.Domain.Height != b.Domain.Height)
                throw new ArgumentException($"{nameA} and {nameB} domains must match.");
        }
    }
}