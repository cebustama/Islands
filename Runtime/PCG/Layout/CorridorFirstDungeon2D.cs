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
    /// Phase E1 — Corridor First (grid-only).
    /// Carves corridors first (DrawLine), then stamps rooms at corridor endpoints and (optionally) dead-ends.
    /// No Tilemaps. Deterministic via Unity.Mathematics.Random only.
    /// </summary>
    public static class CorridorFirstDungeon2D
    {
        public struct CorridorFirstConfig
        {
            public int corridorCount;
            public int corridorLengthMin, corridorLengthMax;
            public int corridorBrushRadius;

            // Rooms at endpoints
            public int roomSpawnCount;     // if > 0 => take N endpoints after seeded shuffle
            public float roomSpawnChance;  // if roomSpawnCount <= 0 => per-endpoint chance
            public int2 roomSizeMin, roomSizeMax;

            // Best-effort margin to keep carving away from edges.
            public int borderPadding;

            public bool clearBeforeGenerate;
            public bool ensureRoomsAtDeadEnds;
        }

        private static readonly int2[] kDirs =
        {
            new int2( 1, 0),
            new int2(-1, 0),
            new int2( 0, 1),
            new int2( 0,-1),
        };

        /// <summary>
        /// Generates corridor-first dungeon into an allocated MaskGrid2D.
        /// Caller provides scratch arrays to avoid allocations.
        /// </summary>
        public static void Generate(
            ref MaskGrid2D mask,
            ref Random rng,
            in CorridorFirstConfig cfg,
            NativeArray<int2> scratchCorridorEndpoints,
            NativeArray<int2> outRoomCenters,
            out int placedRooms)
        {
            int w = mask.Domain.Width;
            int h = mask.Domain.Height;

            placedRooms = 0;

            if (w <= 0 || h <= 0)
                return;

            // Sanitize config (cheap, deterministic-friendly)
            int corridorCount = math.max(0, cfg.corridorCount);
            int lenMin = math.max(1, cfg.corridorLengthMin);
            int lenMax = math.max(lenMin, cfg.corridorLengthMax);
            int brush = math.max(0, cfg.corridorBrushRadius);

            int2 roomMin = new int2(math.max(1, cfg.roomSizeMin.x), math.max(1, cfg.roomSizeMin.y));
            int2 roomMax = new int2(math.max(roomMin.x, cfg.roomSizeMax.x), math.max(roomMin.y, cfg.roomSizeMax.y));

            int border = math.max(0, cfg.borderPadding);

            if (cfg.clearBeforeGenerate)
                mask.Clear();

            // Clamp border to something that still leaves at least 1 cell.
            int xMinI = 0 + border;
            int xMaxI = (w - 1) - border;
            int yMinI = 0 + border;
            int yMaxI = (h - 1) - border;

            if (xMinI > xMaxI) { xMinI = 0; xMaxI = w - 1; }
            if (yMinI > yMaxI) { yMinI = 0; yMaxI = h - 1; }

            int2 start = new int2(w >> 1, h >> 1);
            start.x = math.clamp(start.x, xMinI, xMaxI);
            start.y = math.clamp(start.y, yMinI, yMaxI);

            // Record endpoints: start + each corridor end (recommended Length >= corridorCount+1)
            int endpointCap = scratchCorridorEndpoints.IsCreated ? scratchCorridorEndpoints.Length : 0;
            if (endpointCap <= 0)
                throw new ArgumentException("scratchCorridorEndpoints must be created and non-empty.", nameof(scratchCorridorEndpoints));

            int endpointsWritten = 0;
            scratchCorridorEndpoints[endpointsWritten++] = start;

            // Ensure at least something is carved
            MaskRasterOps2D.StampDisc(ref mask, start.x, start.y, 0, value: true);

            int2 current = start;

            // Carve corridors
            for (int i = 0; i < corridorCount; i++)
            {
                int2 dir = kDirs[rng.NextInt(0, 4)];
                int length = (lenMin == lenMax) ? lenMin : rng.NextInt(lenMin, lenMax + 1);

                int2 target = current + dir * length;

                target.x = math.clamp(target.x, xMinI, xMaxI);
                target.y = math.clamp(target.y, yMinI, yMaxI);

                // Safe raster op
                MaskRasterOps2D.DrawLine(ref mask, current, target, brushRadius: brush, value: true);

                current = target;

                if (endpointsWritten < endpointCap)
                    scratchCorridorEndpoints[endpointsWritten++] = current;
            }

            // Dedupe endpoints in-place (small N; O(N^2) is fine)
            int uniqueCount = DeduplicateInPlace(scratchCorridorEndpoints, endpointsWritten);

            // Place rooms at endpoints (rect stamp)
            placedRooms = 0;
            int outCap = outRoomCenters.IsCreated ? outRoomCenters.Length : 0;
            if (outCap <= 0)
                throw new ArgumentException("outRoomCenters must be created and non-empty.", nameof(outRoomCenters));

            int spawnCount = cfg.roomSpawnCount;

            if (spawnCount > 0)
            {
                // Fisher–Yates shuffle (seeded)
                ShufflePrefix(ref rng, scratchCorridorEndpoints, uniqueCount);

                int take = math.min(spawnCount, math.min(uniqueCount, outCap));
                for (int i = 0; i < take; i++)
                {
                    int2 c = scratchCorridorEndpoints[i];
                    StampRoomRect(ref mask, ref rng, c, roomMin, roomMax);

                    outRoomCenters[placedRooms++] = c;
                }
            }
            else
            {
                float chance = math.clamp(cfg.roomSpawnChance, 0f, 1f);

                for (int i = 0; i < uniqueCount && placedRooms < outCap; i++)
                {
                    if (rng.NextFloat() < chance)
                    {
                        int2 c = scratchCorridorEndpoints[i];
                        StampRoomRect(ref mask, ref rng, c, roomMin, roomMax);

                        outRoomCenters[placedRooms++] = c;
                    }
                }
            }

            // Optional: ensure rooms at dead ends
            if (cfg.ensureRoomsAtDeadEnds)
            {
                int coverRadius = (math.max(roomMax.x, roomMax.y) >> 1) + 1;

                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int2 p = new int2(x, y);

                        if (!MaskNeighborOps2D.IsDeadEnd4(in mask, p))
                            continue;

                        if (IsNearAnyRoomCenter(outRoomCenters, placedRooms, p, coverRadius))
                            continue;

                        StampRoomRect(ref mask, ref rng, p, roomMin, roomMax);

                        if (placedRooms < outCap)
                            outRoomCenters[placedRooms++] = p;
                    }
                }
            }
        }

        private static void StampRoomRect(ref MaskGrid2D mask, ref Random rng, int2 center, int2 sizeMin, int2 sizeMax)
        {
            int w = (sizeMin.x == sizeMax.x) ? sizeMin.x : rng.NextInt(sizeMin.x, sizeMax.x + 1);
            int h = (sizeMin.y == sizeMax.y) ? sizeMin.y : rng.NextInt(sizeMin.y, sizeMax.y + 1);

            w = math.max(1, w);
            h = math.max(1, h);

            int halfW = w >> 1;
            int halfH = h >> 1;

            int xMin = center.x - halfW;
            int yMin = center.y - halfH;

            int xMax = xMin + w;
            int yMax = yMin + h;

            // Safe: clampToDomain
            RectFillGenerator.FillRect(ref mask, xMin, yMin, xMax, yMax, value: true, clampToDomain: true);
        }

        private static bool IsNearAnyRoomCenter(NativeArray<int2> centers, int count, int2 p, int radius)
        {
            int r2 = radius * radius;
            for (int i = 0; i < count; i++)
            {
                int2 d = centers[i] - p;
                if (math.dot(d, d) <= r2)
                    return true;
            }
            return false;
        }

        private static int DeduplicateInPlace(NativeArray<int2> a, int count)
        {
            int write = 0;
            for (int i = 0; i < count; i++)
            {
                int2 v = a[i];
                bool dup = false;

                for (int j = 0; j < write; j++)
                {
                    if (a[j].Equals(v))
                    {
                        dup = true;
                        break;
                    }
                }

                if (!dup)
                    a[write++] = v;
            }
            return write;
        }

        private static void ShufflePrefix(ref Random rng, NativeArray<int2> a, int count)
        {
            // Fisher–Yates
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                int2 tmp = a[i];
                a[i] = a[j];
                a[j] = tmp;
            }
        }
    }
}
