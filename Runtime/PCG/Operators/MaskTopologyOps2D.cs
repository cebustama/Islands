using System;
using Unity.Collections;
using Unity.Mathematics;
using Islands.PCG.Core;
using Islands.PCG.Grids;

namespace Islands.PCG.Operators
{
    /// <summary>
    /// Deterministic topology helpers over MaskGrid2D.
    ///
    /// Contracts:
    /// - 4-neighborhood (W/E/S/N)
    /// - out-of-bounds neighbors count as OFF
    /// - row-major scans only
    /// - no unordered collections
    /// </summary>
    public static class MaskTopologyOps2D
    {
        public readonly struct MaskComponent2D
        {
            public readonly int Id;
            public readonly int Area;
            public readonly int AnchorIndex;
            public readonly int2 AnchorCell;
            public readonly int2 Min;
            public readonly int2 MaxExclusive;

            public MaskComponent2D(int id, int area, int anchorIndex, int2 anchorCell, int2 min, int2 maxExclusive)
            {
                Id = id;
                Area = area;
                AnchorIndex = anchorIndex;
                AnchorCell = anchorCell;
                Min = min;
                MaxExclusive = maxExclusive;
            }
        }

        public static void ExtractEdge4(in MaskGrid2D src, ref MaskGrid2D edge)
        {
            ExtractEdgeAndInterior4(in src, ref edge, ref UnsafeNullMask.Instance);
        }

        public static void ExtractInterior4(in MaskGrid2D src, ref MaskGrid2D interior)
        {
            ExtractEdgeAndInterior4(in src, ref UnsafeNullMask.Instance, ref interior);
        }

        public static void ExtractEdgeAndInterior4(in MaskGrid2D src, ref MaskGrid2D edge, ref MaskGrid2D interior)
        {
            if (!src.IsCreated) throw new InvalidOperationException("src must be created.");

            bool writeEdge = edge.IsCreated;
            bool writeInterior = interior.IsCreated;

            if (!writeEdge && !writeInterior)
                throw new ArgumentException("At least one destination mask must be created.");

            if (writeEdge && !Compatible(in src, in edge))
                throw new ArgumentException("edge domain must match src.", nameof(edge));

            if (writeInterior && !Compatible(in src, in interior))
                throw new ArgumentException("interior domain must match src.", nameof(interior));

            if (writeEdge) edge.Clear();
            if (writeInterior) interior.Clear();

            int w = src.Domain.Width;
            int h = src.Domain.Height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!src.GetUnchecked(x, y))
                        continue;

                    bool west = x > 0 && src.GetUnchecked(x - 1, y);
                    bool east = x + 1 < w && src.GetUnchecked(x + 1, y);
                    bool south = y > 0 && src.GetUnchecked(x, y - 1);
                    bool north = y + 1 < h && src.GetUnchecked(x, y + 1);

                    bool isInterior = west && east && south && north;

                    if (writeInterior && isInterior)
                        interior.SetUnchecked(x, y, true);

                    if (writeEdge && !isInterior)
                        edge.SetUnchecked(x, y, true);
                }
            }
        }

        /// <summary>
        /// Labels 4-connected ON components in stable row-major discovery order.
        ///
        /// - labels must have length == src.Domain.Length
        /// - OFF cells are set to -1
        /// - component ids are 0..count-1 in first-discovered order
        /// - neighbor order is fixed: Right, Left, Up, Down
        /// </summary>
        public static int LabelConnectedComponents4(
            in MaskGrid2D src,
            NativeArray<int> labels,
            NativeList<MaskComponent2D> components)
        {
            if (!src.IsCreated) throw new InvalidOperationException("src must be created.");
            if (!labels.IsCreated) throw new InvalidOperationException("labels must be created.");
            if (!components.IsCreated) throw new InvalidOperationException("components must be created.");
            if (labels.Length != src.Domain.Length)
                throw new ArgumentException("labels length must equal src.Domain.Length.", nameof(labels));

            GridDomain2D d = src.Domain;
            int w = d.Width;
            int h = d.Height;

            for (int i = 0; i < labels.Length; i++) labels[i] = -1;
            components.Clear();

            var queue = new NativeArray<int>(d.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                int componentId = 0;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int startIndex = d.Index(x, y);
                        if (!src.GetUnchecked(x, y) || labels[startIndex] >= 0)
                            continue;

                        int head = 0;
                        int tail = 0;
                        queue[tail++] = startIndex;
                        labels[startIndex] = componentId;

                        int area = 0;
                        int minX = x, minY = y, maxX = x + 1, maxY = y + 1;
                        int anchorIndex = startIndex;
                        int2 anchorCell = new int2(x, y);

                        while (head < tail)
                        {
                            int idx = queue[head++];
                            d.Coord(idx, out int cx, out int cy);

                            area++;
                            if (cx < minX) minX = cx;
                            if (cy < minY) minY = cy;
                            if (cx + 1 > maxX) maxX = cx + 1;
                            if (cy + 1 > maxY) maxY = cy + 1;

                            TryEnqueue(in src, labels, in d, queue, ref tail, componentId, cx + 1, cy); // R
                            TryEnqueue(in src, labels, in d, queue, ref tail, componentId, cx - 1, cy); // L
                            TryEnqueue(in src, labels, in d, queue, ref tail, componentId, cx, cy + 1); // U
                            TryEnqueue(in src, labels, in d, queue, ref tail, componentId, cx, cy - 1); // D
                        }

                        components.Add(new MaskComponent2D(
                            id: componentId,
                            area: area,
                            anchorIndex: anchorIndex,
                            anchorCell: anchorCell,
                            min: new int2(minX, minY),
                            maxExclusive: new int2(maxX, maxY)));

                        componentId++;
                    }
                }

                return componentId;
            }
            finally
            {
                if (queue.IsCreated) queue.Dispose();
            }
        }

        private static void TryEnqueue(
            in MaskGrid2D src,
            NativeArray<int> labels,
            in GridDomain2D d,
            NativeArray<int> queue,
            ref int tail,
            int componentId,
            int x,
            int y)
        {
            int w = d.Width;
            int h = d.Height;

            if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                return;

            if (!src.GetUnchecked(x, y))
                return;

            int idx = d.Index(x, y);
            if (labels[idx] >= 0)
                return;

            labels[idx] = componentId;
            queue[tail++] = idx;
        }

        private static bool Compatible(in MaskGrid2D a, in MaskGrid2D b) =>
            a.IsCreated && b.IsCreated && a.Domain.Width == b.Domain.Width && a.Domain.Height == b.Domain.Height;

        /// <summary>
        /// Private helper so the public API can share one implementation without allocating.
        /// Never call methods on this sentinel except IsCreated.
        /// </summary>
        private static class UnsafeNullMask
        {
            public static MaskGrid2D Instance;
        }
    }
}
