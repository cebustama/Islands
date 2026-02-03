using System;
using Unity.Collections;
using Islands.PCG.Grids;
using Islands.PCG.Core;

namespace Islands.PCG.Operators
{
    /// <summary>
    /// Deterministic flood fill utilities for MaskGrid2D.
    /// </summary>
    public static class MaskFloodFillOps2D
    {
        /// <summary>
        /// Fills dst with all cells that are:
        /// - border-connected (4-neighborhood), and
        /// - traversable (i.e., solid.Get(x,y) == false).
        ///
        /// Determinism guarantees:
        /// - Border seed scan order is fixed:
        ///     1) top row (y=0) left->right
        ///     2) bottom row (y=H-1) left->right (if H>1)
        ///     3) left column (x=0) top+1->bottom-1 (if H>2)
        ///     4) right column (x=W-1) top+1->bottom-1 (if W>1 and H>2)
        /// - BFS is FIFO (array queue).
        /// - Neighbor order is fixed: Right, Left, Up, Down.
        ///
        /// OOB safety:
        /// - Never calls MaskGrid2D.Get/Set out of bounds.
        /// - Out-of-bounds neighbors are ignored (equivalent to OFF).
        ///
        /// Preconditions:
        /// - solid and dst must be created and share the same domain dimensions.
        /// - dst is overwritten (cleared at start).
        /// </summary>
        public static void FloodFillBorderConnected_NotSolid(ref MaskGrid2D solid, ref MaskGrid2D dst)
        {
            if (!solid.IsCreated) throw new InvalidOperationException("solid must be created.");
            if (!dst.IsCreated) throw new InvalidOperationException("dst must be created.");

            GridDomain2D d = solid.Domain;

            if (dst.Domain.Width != d.Width || dst.Domain.Height != d.Height)
                throw new ArgumentException("solid and dst domains must match.", nameof(dst));

            int w = d.Width;
            int h = d.Height;

            dst.Clear();

            int total = d.Length;
            if (total <= 0) return;

            var queue = new NativeArray<int>(total, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int head = 0;
            int tail = 0;

            try
            {
                // -------------------------
                // Seed from borders (fixed order)
                // -------------------------

                // 1) Top row y=0
                for (int x = 0; x < w; x++)
                    TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, x, 0);

                // 2) Bottom row y=h-1
                if (h > 1)
                {
                    int yb = h - 1;
                    for (int x = 0; x < w; x++)
                        TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, x, yb);
                }

                // 3/4) Left/Right columns, skipping corners
                if (h > 2)
                {
                    int yStart = 1;
                    int yEnd = h - 2;

                    // Left column x=0
                    for (int y = yStart; y <= yEnd; y++)
                        TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, 0, y);

                    // Right column x=w-1
                    if (w > 1)
                    {
                        int xr = w - 1;
                        for (int y = yStart; y <= yEnd; y++)
                            TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, xr, y);
                    }
                }

                // -------------------------
                // BFS flood fill (FIFO) with fixed neighbor order
                // Neighbor order: Right, Left, Up, Down
                // -------------------------
                while (head < tail)
                {
                    int idx = queue[head++];
                    d.Coord(idx, out int x, out int y);

                    TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, x + 1, y); // R
                    TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, x - 1, y); // L
                    TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, x, y + 1); // U
                    TryEnqueueIfTraversable(ref solid, ref dst, d, queue, w, h, ref tail, x, y - 1); // D
                }
            }
            finally
            {
                if (queue.IsCreated) queue.Dispose();
            }
        }

        private static void TryEnqueueIfTraversable(
            ref MaskGrid2D solid,
            ref MaskGrid2D dst,
            in GridDomain2D d,
            NativeArray<int> queue,
            int w,
            int h,
            ref int tail,
            int x,
            int y)
        {
            // OOB-safe: check before Get/Set
            if ((uint)x >= (uint)w || (uint)y >= (uint)h) return;

            // Traversable means NOT solid
            if (solid.Get(x, y)) return;

            // Already visited
            if (dst.Get(x, y)) return;

            // Enqueue
            dst.Set(x, y, true);

            // Defensive (should never overflow because each cell enqueues at most once)
            if ((uint)tail >= (uint)queue.Length)
                throw new InvalidOperationException("Flood fill queue overflow (should be impossible).");

            queue[tail++] = d.Index(x, y);
        }
    }
}
