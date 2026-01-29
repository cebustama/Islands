using System;
using Unity.Collections;
using Unity.Mathematics;
using Islands.PCG.Grids;
using Islands.PCG.Generators;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout
{
    /// <summary>
    /// Minimal, deterministic, grid-only dungeon composer:
    /// 1) Places axis-aligned rectangular rooms into a MaskGrid2D.
    /// 2) Connects placed rooms in placement order with rasterized corridors (DrawLine).
    ///
    /// This is a "first slice" meant to unblock the port from Tilemaps to pure grids.
    /// </summary>
    public static class RoomsCorridorsComposer2D
    {
        /// <summary>
        /// Configuration for the minimal Rooms+Corridors composition pass.
        /// All values are in grid/cell units.
        /// </summary>
        public struct RoomsCorridorsConfig
        {
            public int roomCount;

            /// <summary>Inclusive ranges for room width/height.</summary>
            public int2 roomSizeMin;
            public int2 roomSizeMax;

            /// <summary>How many random placement attempts to try per room.</summary>
            public int placementAttemptsPerRoom;

            /// <summary>Optional border padding. Rooms will be sampled so they fit within this margin.</summary>
            public int roomPadding;

            /// <summary>Corridor thickness (0 = 1-cell line). Uses MaskRasterOps2D.DrawLine brushRadius.</summary>
            public int corridorBrushRadius;

            /// <summary>If true, clears the mask before generation (recommended for this slice).</summary>
            public bool clearBeforeGenerate;

            /// <summary>
            /// If true, rooms are allowed to overlap existing filled cells.
            /// If false, room placement performs a cheap "area empty" scan before stamping.
            /// </summary>
            public bool allowOverlap;
        }

        /// <summary>
        /// Generates a room+corrridor mask into an existing MaskGrid2D (no Tilemaps, no allocations inside).
        /// Rooms are filled rectangles, corridors connect rooms in placement order using DrawLine.
        /// </summary>
        /// <param name="mask">Destination mask (must already be allocated).</param>
        /// <param name="rng">Seed-driven RNG (passed by ref to advance deterministically).</param>
        /// <param name="cfg">Composition configuration.</param>
        /// <param name="outRoomCenters">
        /// Preallocated output array. Must have Length >= cfg.roomCount.
        /// Only indices [0..placedRooms-1] are valid outputs.
        /// </param>
        /// <param name="placedRooms">How many rooms were successfully placed.</param>
        public static void Generate(
            ref MaskGrid2D mask,
            ref Unity.Mathematics.Random rng,
            in RoomsCorridorsConfig cfg,
            NativeArray<int2> outRoomCenters,
            out int placedRooms)
        {
            if (!mask.IsCreated) throw new ArgumentException("MaskGrid2D must be allocated.", nameof(mask));
            if (!outRoomCenters.IsCreated) throw new ArgumentException("outRoomCenters must be allocated.", nameof(outRoomCenters));
            if (cfg.roomCount < 0) throw new ArgumentOutOfRangeException(nameof(cfg.roomCount), "roomCount must be >= 0.");
            if (outRoomCenters.Length < cfg.roomCount)
                throw new ArgumentException($"outRoomCenters.Length ({outRoomCenters.Length}) must be >= cfg.roomCount ({cfg.roomCount}).",
                    nameof(outRoomCenters));

            if (cfg.corridorBrushRadius < 0)
                throw new ArgumentOutOfRangeException(nameof(cfg.corridorBrushRadius), "corridorBrushRadius must be >= 0.");

            int attemptsPerRoom = math.max(1, cfg.placementAttemptsPerRoom);
            int padding = math.max(0, cfg.roomPadding);

            // Sanitize size ranges (inclusive).
            int wMin = math.max(1, math.min(cfg.roomSizeMin.x, cfg.roomSizeMax.x));
            int wMax = math.max(1, math.max(cfg.roomSizeMin.x, cfg.roomSizeMax.x));
            int hMin = math.max(1, math.min(cfg.roomSizeMin.y, cfg.roomSizeMax.y));
            int hMax = math.max(1, math.max(cfg.roomSizeMin.y, cfg.roomSizeMax.y));

            int W = mask.Domain.Width;
            int H = mask.Domain.Height;

            if (cfg.clearBeforeGenerate)
                mask.Clear();

            placedRooms = 0;

            // -------------------------
            // 1) Place rooms (rect fill)
            // -------------------------
            for (int i = 0; i < cfg.roomCount; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < attemptsPerRoom; attempt++)
                {
                    int roomW = rng.NextInt(wMin, wMax + 1); // inclusive max
                    int roomH = rng.NextInt(hMin, hMax + 1);

                    // Sample xMin/yMin directly (simpler, avoids half-extent edge cases).
                    int xMinMin = padding;
                    int xMinMaxInclusive = (W - padding) - roomW; // because xMax is exclusive
                    int yMinMin = padding;
                    int yMinMaxInclusive = (H - padding) - roomH;

                    if (xMinMaxInclusive < xMinMin || yMinMaxInclusive < yMinMin)
                    {
                        // Room can't fit with these constraints; try another attempt (consumes RNG deterministically).
                        continue;
                    }

                    int xMin = rng.NextInt(xMinMin, xMinMaxInclusive + 1);
                    int yMin = rng.NextInt(yMinMin, yMinMaxInclusive + 1);

                    int xMax = xMin + roomW; // exclusive
                    int yMax = yMin + roomH; // exclusive

                    if (!cfg.allowOverlap)
                    {
                        // Cheap empty scan in-bounds (bounds were sampled to be valid).
                        if (AnySetInRect(in mask, xMin, yMin, xMax, yMax))
                            continue;
                    }

                    // Stamp room.
                    RectFillGenerator.FillRect(
                        ref mask,
                        xMin, yMin,
                        xMax, yMax,
                        value: true,
                        clampToDomain: true);

                    // Store a representative integer center for corridor routing.
                    int2 center = new int2(xMin + roomW / 2, yMin + roomH / 2);
                    outRoomCenters[placedRooms] = center;
                    placedRooms++;

                    placed = true;
                    break;
                }

                // If a room fails to place, we just skip it and continue.
                // This keeps the algorithm robust for small domains / strict constraints.
                if (!placed)
                    continue;
            }

            // ------------------------------------
            // 2) Connect rooms with raster corridors
            // ------------------------------------
            for (int i = 1; i < placedRooms; i++)
            {
                int2 a = outRoomCenters[i - 1];
                int2 b = outRoomCenters[i];

                MaskRasterOps2D.DrawLine(
                    ref mask,
                    a, b,
                    brushRadius: cfg.corridorBrushRadius,
                    value: true);
            }
        }

        private static bool AnySetInRect(in MaskGrid2D mask, int xMin, int yMin, int xMax, int yMax)
        {
            // RectFillGenerator uses [min, max) convention; we follow the same here.
            for (int y = yMin; y < yMax; y++)
                for (int x = xMin; x < xMax; x++)
                    if (mask.GetUnchecked(x, y))
                        return true;

            return false;
        }
    }
}
