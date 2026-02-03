using System;
using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Grids;
using Islands.PCG.Generators;
using Islands.PCG.Operators;
using Islands.PCG.Layout.Bsp;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// Phase E2.2 — Room First (BSP) dungeon strategy (grid-only).
    /// 1) BSP partitions the domain into leaf rects (pure layout).
    /// 2) For each leaf: shrink by padding and stamp a rectangular room into MaskGrid2D.
    /// 3) Connect room centers with corridors (Manhattan "L" or direct line) using MaskRasterOps2D.
    ///
    /// Determinism:
    /// - Seed-driven RNG only (Unity.Mathematics.Random, passed by ref).
    /// - No GUID shuffles or non-deterministic collections.
    ///
    /// Safety:
    /// - Rooms are stamped via RectFillGenerator.FillRect with clampToDomain=true.
    /// - Corridors are carved via MaskRasterOps2D.DrawLine (OOB-safe in your pipeline).
    /// </summary>
    public static class RoomFirstBspDungeon2D
    {
        /// <summary>
        /// Configuration for Room First BSP dungeon generation.
        /// </summary>
        public struct RoomFirstBspConfig
        {
            /// <summary>How many BSP split iterations to attempt (worst-case leaves = 2^splitIterations).</summary>
            public int splitIterations;

            /// <summary>Minimum allowed leaf size (in cells). Splits never produce children smaller than this.</summary>
            public int2 minLeafSize;

            /// <summary>Padding applied to each leaf before stamping the room (shrinks leaf inward).</summary>
            public int roomPadding;

            /// <summary>Brush radius for corridor carving (0 = single-cell line, >0 = disc brush).</summary>
            public int corridorBrushRadius;

            /// <summary>If true, connect centers with a Manhattan "L" (two segments). If false, connect with a single line.</summary>
            public bool connectWithManhattan;

            /// <summary>If true, clears the mask before generating.</summary>
            public bool clearBeforeGenerate;
        }

        /// <summary>
        /// Generates a Room-First BSP dungeon layout into an allocated MaskGrid2D.
        /// Caller supplies scratch arrays to avoid allocations in the runtime generator.
        /// </summary>
        /// <param name="mask">Target mask to write into.</param>
        /// <param name="rng">Seed-driven RNG (passed by ref, advances deterministically).</param>
        /// <param name="cfg">Generation configuration.</param>
        /// <param name="scratchLeaves">
        /// Preallocated BSP leaf output buffer. Must be large enough for worst-case leaves (2^splitIterations),
        /// or BspPartition2D.PartitionLeaves will throw to avoid silent truncation.
        /// </param>
        /// <param name="outRoomCenters">
        /// Output buffer for room centers (written in leaf order for all successfully stamped rooms).
        /// Must be created and large enough for the expected number of rooms (typically >= max leaves).
        /// </param>
        /// <param name="leafCount">Total number of BSP leaves generated (layout output count).</param>
        /// <param name="placedRooms">Number of rooms successfully stamped (<= leafCount, <= outRoomCenters.Length).</param>
        public static void Generate(
            ref MaskGrid2D mask,
            ref Random rng,
            in RoomFirstBspConfig cfg,
            NativeArray<BspPartition2D.IntRect2D> scratchLeaves,
            NativeArray<int2> outRoomCenters,
            out int leafCount,
            out int placedRooms)
        {
            int w = mask.Domain.Width;
            int h = mask.Domain.Height;

            leafCount = 0;
            placedRooms = 0;

            if (w <= 0 || h <= 0)
                return;

            if (!scratchLeaves.IsCreated || scratchLeaves.Length <= 0)
                throw new ArgumentException("scratchLeaves must be created and non-empty.", nameof(scratchLeaves));

            if (!outRoomCenters.IsCreated || outRoomCenters.Length <= 0)
                throw new ArgumentException("outRoomCenters must be created and non-empty.", nameof(outRoomCenters));

            // Sanitize config
            var partCfg = new BspPartition2D.BspPartitionConfig
            {
                splitIterations = math.max(0, cfg.splitIterations),
                minLeafSize = new int2(math.max(1, cfg.minLeafSize.x), math.max(1, cfg.minLeafSize.y))
            };

            int padding = math.max(0, cfg.roomPadding);
            int brush = math.max(0, cfg.corridorBrushRadius);
            bool manhattan = cfg.connectWithManhattan;

            if (cfg.clearBeforeGenerate)
                mask.Clear();

            // Root rect uses [min,max) convention matching RectFillGenerator.FillRect.
            var root = new BspPartition2D.IntRect2D(0, 0, w, h);

            // BSP layout (pure)
            leafCount = BspPartition2D.PartitionLeaves(root, ref rng, partCfg, scratchLeaves);

            // Stamp rooms & collect centers
            int centerWrite = 0;

            for (int i = 0; i < leafCount; i++)
            {
                if (centerWrite >= outRoomCenters.Length)
                    break;

                var leaf = scratchLeaves[i];
                var room = Shrink(leaf, padding);

                if (!room.IsValid)
                    continue;

                // Stamp full shrunk leaf (minimal stable slice)
                RectFillGenerator.FillRect(
                    ref mask,
                    room.xMin, room.yMin,
                    room.xMax, room.yMax,
                    value: true,
                    clampToDomain: true);

                outRoomCenters[centerWrite++] = room.Center;
            }

            placedRooms = centerWrite;

            // Connect centers in placement order
            if (placedRooms <= 1)
                return;

            int2 prev = outRoomCenters[0];

            for (int i = 1; i < placedRooms; i++)
            {
                int2 cur = outRoomCenters[i];

                if (manhattan)
                {
                    // Choose elbow deterministically from rng
                    bool xFirst = rng.NextBool();

                    int2 elbow = xFirst
                        ? new int2(cur.x, prev.y)
                        : new int2(prev.x, cur.y);

                    MaskRasterOps2D.DrawLine(ref mask, prev, elbow, brushRadius: brush, value: true);
                    MaskRasterOps2D.DrawLine(ref mask, elbow, cur, brushRadius: brush, value: true);
                }
                else
                {
                    MaskRasterOps2D.DrawLine(ref mask, prev, cur, brushRadius: brush, value: true);
                }

                prev = cur;
            }
        }

        private static BspPartition2D.IntRect2D Shrink(in BspPartition2D.IntRect2D r, int padding)
        {
            if (padding <= 0)
                return r;

            return new BspPartition2D.IntRect2D(
                r.xMin + padding,
                r.yMin + padding,
                r.xMax - padding,
                r.yMax - padding);
        }
    }
}
