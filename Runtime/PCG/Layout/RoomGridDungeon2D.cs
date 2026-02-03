using System;
using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Grids;
using Islands.PCG.Generators;
using Islands.PCG.Operators;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// Phase E3 — Room Grid (layout-only minimal slice).
    /// Picks room centers on a coarse grid using a deterministic, seed-driven "grid-walk" path,
    /// stamps rooms at centers (FillRect clamp), and connects sequential rooms with corridors.
    /// </summary>
    public static class RoomGridDungeon2D
    {
        public struct RoomGridConfig
        {
            public int roomCount;

            /// <summary>Coarse grid spacing in cells (>= 1).</summary>
            public int cellSize;

            public int2 roomSizeMin, roomSizeMax;

            public int corridorBrushRadius;
            public int borderPadding;

            public bool connectWithManhattan;
            public bool clearBeforeGenerate;
        }

        private static readonly int2[] kDirs =
        {
            new int2( 1, 0),
            new int2(-1, 0),
            new int2( 0, 1),
            new int2( 0,-1),
        };

        /// <summary>
        /// Generates a coarse-grid room layout into mask. Caller supplies scratch to avoid allocations.
        /// scratchPickedNodeIndices length must be >= cfg.roomCount (or >= 1 if cfg.roomCount == 0).
        /// outRoomCenters length must be >= cfg.roomCount (or >= 1 if cfg.roomCount == 0).
        /// </summary>
        public static void Generate(
            ref MaskGrid2D mask,
            ref Random rng,
            in RoomGridConfig cfg,
            NativeArray<int> scratchPickedNodeIndices,
            NativeArray<int2> outRoomCenters,
            out int placedRooms)
        {
            int w = mask.Domain.Width;
            int h = mask.Domain.Height;

            placedRooms = 0;

            if (w <= 0 || h <= 0)
                return;

            int roomCount = math.max(0, cfg.roomCount);
            int cellSize = math.max(1, cfg.cellSize);
            int border = math.max(0, cfg.borderPadding);

            int2 roomMin = new int2(math.max(1, cfg.roomSizeMin.x), math.max(1, cfg.roomSizeMin.y));
            int2 roomMax = new int2(math.max(roomMin.x, cfg.roomSizeMax.x), math.max(roomMin.y, cfg.roomSizeMax.y));

            if (cfg.clearBeforeGenerate)
                mask.Clear();

            if (!scratchPickedNodeIndices.IsCreated || scratchPickedNodeIndices.Length <= 0)
                throw new ArgumentException("scratchPickedNodeIndices must be created and non-empty.", nameof(scratchPickedNodeIndices));
            if (!outRoomCenters.IsCreated || outRoomCenters.Length <= 0)
                throw new ArgumentException("outRoomCenters must be created and non-empty.", nameof(outRoomCenters));

            if (roomCount == 0)
                return;

            // Interior bounds (best-effort). If padding collapses space, fall back to full domain.
            int xMinI = border;
            int yMinI = border;
            int xMaxI = (w - 1) - border;
            int yMaxI = (h - 1) - border;

            if (xMinI > xMaxI) { xMinI = 0; xMaxI = w - 1; }
            if (yMinI > yMaxI) { yMinI = 0; yMaxI = h - 1; }

            // Coarse-grid origin (place centers inside interior).
            int2 origin = new int2(xMinI + (cellSize >> 1), yMinI + (cellSize >> 1));
            origin.x = math.clamp(origin.x, xMinI, xMaxI);
            origin.y = math.clamp(origin.y, yMinI, yMaxI);

            int nx = (xMaxI >= origin.x) ? ((xMaxI - origin.x) / cellSize + 1) : 0;
            int ny = (yMaxI >= origin.y) ? ((yMaxI - origin.y) / cellSize + 1) : 0;

            int nodeCount = nx * ny;

            // If the coarse grid degenerates, place a single room at domain center.
            if (nodeCount <= 0)
            {
                int2 c = new int2(w >> 1, h >> 1);
                StampRoomRect(ref mask, ref rng, c, roomMin, roomMax);
                outRoomCenters[0] = c;
                placedRooms = 1;
                return;
            }

            int take = math.min(roomCount, math.min(nodeCount, outRoomCenters.Length));
            if (scratchPickedNodeIndices.Length < take)
                throw new ArgumentException($"scratchPickedNodeIndices length must be >= {take}.", nameof(scratchPickedNodeIndices));

            // Pick a start node and do a deterministic grid-walk to select unique nodes.
            int startIndex = rng.NextInt(0, nodeCount);

            scratchPickedNodeIndices[0] = startIndex;
            placedRooms = 0;

            int2 prevCenter = IndexToCenter(startIndex, nx, cellSize, origin);

            // Place first room
            StampRoomRect(ref mask, ref rng, prevCenter, roomMin, roomMax);
            outRoomCenters[placedRooms++] = prevCenter;

            int currentIndex = startIndex;

            const int kNeighborTries = 12;
            const int kGlobalTries = 64;

            for (int i = 1; i < take; i++)
            {
                int nextIndex = -1;

                // Prefer stepping to a neighbor node (path-like)
                for (int t = 0; t < kNeighborTries; t++)
                {
                    int2 cur = IndexToNode(currentIndex, nx);
                    int2 dir = kDirs[rng.NextInt(0, 4)];
                    int2 nxt = cur + dir;

                    if ((uint)nxt.x >= (uint)nx || (uint)nxt.y >= (uint)ny)
                        continue;

                    int cand = nxt.y * nx + nxt.x;
                    if (ContainsIndex(scratchPickedNodeIndices, i, cand))
                        continue;

                    nextIndex = cand;
                    break;
                }

                // Fallback: sample any unused node
                if (nextIndex < 0)
                {
                    for (int t = 0; t < kGlobalTries; t++)
                    {
                        int cand = rng.NextInt(0, nodeCount);
                        if (!ContainsIndex(scratchPickedNodeIndices, i, cand))
                        {
                            nextIndex = cand;
                            break;
                        }
                    }
                }

                if (nextIndex < 0)
                    break; // fully saturated / no progress (rare for small take)

                scratchPickedNodeIndices[i] = nextIndex;
                currentIndex = nextIndex;

                int2 center = IndexToCenter(nextIndex, nx, cellSize, origin);

                // Stamp room
                StampRoomRect(ref mask, ref rng, center, roomMin, roomMax);

                // Connect to previous
                if (cfg.connectWithManhattan)
                {
                    int2 corner = new int2(center.x, prevCenter.y);
                    MaskRasterOps2D.DrawLine(ref mask, prevCenter, corner, cfg.corridorBrushRadius, value: true);
                    MaskRasterOps2D.DrawLine(ref mask, corner, center, cfg.corridorBrushRadius, value: true);
                }
                else
                {
                    MaskRasterOps2D.DrawLine(ref mask, prevCenter, center, cfg.corridorBrushRadius, value: true);
                }

                outRoomCenters[placedRooms++] = center;
                prevCenter = center;
            }
        }

        private static bool ContainsIndex(NativeArray<int> a, int count, int value)
        {
            for (int i = 0; i < count; i++)
                if (a[i] == value) return true;
            return false;
        }

        private static int2 IndexToNode(int index, int nx)
        {
            int y = index / nx;
            int x = index - y * nx;
            return new int2(x, y);
        }

        private static int2 IndexToCenter(int index, int nx, int cellSize, int2 origin)
        {
            int2 n = IndexToNode(index, nx);
            return origin + new int2(n.x * cellSize, n.y * cellSize);
        }

        private static void StampRoomRect(ref MaskGrid2D mask, ref Random rng, int2 center, int2 sizeMin, int2 sizeMax)
        {
            int rw = (sizeMin.x == sizeMax.x) ? sizeMin.x : rng.NextInt(sizeMin.x, sizeMax.x + 1);
            int rh = (sizeMin.y == sizeMax.y) ? sizeMin.y : rng.NextInt(sizeMin.y, sizeMax.y + 1);

            rw = math.max(1, rw);
            rh = math.max(1, rh);

            int halfW = rw >> 1;
            int halfH = rh >> 1;

            int xMin = center.x - halfW;
            int yMin = center.y - halfH;

            int xMax = xMin + rw;
            int yMax = yMin + rh;

            RectFillGenerator.FillRect(ref mask, xMin, yMin, xMax, yMax, value: true, clampToDomain: true);
        }
    }
}
