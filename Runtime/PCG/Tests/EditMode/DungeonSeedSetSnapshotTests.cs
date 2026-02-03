using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using Islands.PCG.Layout.Bsp;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Tests.EditMode
{
    /// <summary>
    /// Phase E4 — Seed-set regression suite.
    /// One consolidated golden-hash gate across curated seeds/configs for E1–E3 strategies:
    /// - Corridor First (E1)
    /// - Room First BSP (E2)
    /// - Room Grid (E3)
    ///
    /// Workflow:
    /// 1) Run once with expected hashes = 0UL => test fails and prints actual hashes.
    /// 2) Visually sanity-check those cases in Lantern if desired.
    /// 3) Paste printed hashes into the Expected fields and rerun => should pass.
    /// </summary>
    public class DungeonSeedSetSnapshotTests
    {
        private const ulong UNSET = 0UL;

        private struct Result
        {
            public ulong Hash;
            public int Ones;
            public int MetricA; // strategy-specific (e.g., placedRooms)
            public int MetricB; // strategy-specific (e.g., leafCount)
        }

        private struct CorridorFirstCase
        {
            public string Name;
            public int Width, Height;
            public int Seed;
            public CorridorFirstDungeon2D.CorridorFirstConfig Cfg;
            public ulong Expected;
        }

        private struct RoomFirstBspCase
        {
            public string Name;
            public int Width, Height;
            public int Seed;
            public RoomFirstBspDungeon2D.RoomFirstBspConfig Cfg;
            public ulong Expected;
        }

        private struct RoomGridCase
        {
            public string Name;
            public int Width, Height;
            public int Seed;
            public RoomGridDungeon2D.RoomGridConfig Cfg;
            public ulong Expected;
        }

        [Test]
        public void DungeonSeedSetSnapshot_Golden()
        {
            var sb = new StringBuilder(2048);
            int failures = 0;

            // -----------------------
            // E1 — Corridor First
            // -----------------------
            var corridorCases = new CorridorFirstCase[]
            {
                new CorridorFirstCase
                {
                    Name = "E1_CorridorFirst_96_seed12345",
                    Width = 96, Height = 96,
                    Seed = 12345,
                    Cfg = new CorridorFirstDungeon2D.CorridorFirstConfig
                    {
                        corridorCount = 14,
                        corridorLengthMin = 6,
                        corridorLengthMax = 14,
                        corridorBrushRadius = 0,

                        roomSpawnCount = 10,
                        roomSpawnChance = 0.50f,
                        roomSizeMin = new int2(6, 6),
                        roomSizeMax = new int2(14, 14),

                        borderPadding = 1,
                        clearBeforeGenerate = true,
                        ensureRoomsAtDeadEnds = true
                    },
                    Expected = 0xE126A81F491A0988UL
                },
                new CorridorFirstCase
                {
                    Name = "E1_CorridorFirst_64_seed2222_brush1",
                    Width = 64, Height = 64,
                    Seed = 2222,
                    Cfg = new CorridorFirstDungeon2D.CorridorFirstConfig
                    {
                        corridorCount = 10,
                        corridorLengthMin = 5,
                        corridorLengthMax = 12,
                        corridorBrushRadius = 1,

                        roomSpawnCount = 0,
                        roomSpawnChance = 0.55f,
                        roomSizeMin = new int2(5, 5),
                        roomSizeMax = new int2(11, 11),

                        borderPadding = 0,
                        clearBeforeGenerate = true,
                        ensureRoomsAtDeadEnds = false
                    },
                    Expected = 0xC1437266C0C1A8B0UL
                },
                new CorridorFirstCase
                {
                    Name = "E1_CorridorFirst_128x96_seed7777_dense",
                    Width = 128, Height = 96,
                    Seed = 7777,
                    Cfg = new CorridorFirstDungeon2D.CorridorFirstConfig
                    {
                        corridorCount = 30,
                        corridorLengthMin = 4,
                        corridorLengthMax = 16,
                        corridorBrushRadius = 0,

                        roomSpawnCount = 16,
                        roomSpawnChance = 0.40f,
                        roomSizeMin = new int2(5, 5),
                        roomSizeMax = new int2(11, 11),

                        borderPadding = 2,
                        clearBeforeGenerate = true,
                        ensureRoomsAtDeadEnds = true
                    },
                    Expected = 0x8110B380019CB96BUL
                },
            };

            for (int i = 0; i < corridorCases.Length; i++)
            {
                ValidateCase(
                    corridorCases[i].Name,
                    corridorCases[i].Expected,
                    () => ComputeCorridorFirst(in corridorCases[i]),
                    ref failures,
                    sb);
            }

            // -----------------------
            // E2 — Room First BSP
            // -----------------------
            var bspCases = new RoomFirstBspCase[]
            {
                new RoomFirstBspCase
                {
                    Name = "E2_RoomFirstBsp_96_seed3333_L",
                    Width = 96, Height = 96,
                    Seed = 3333,
                    Cfg = new RoomFirstBspDungeon2D.RoomFirstBspConfig
                    {
                        splitIterations = 4,
                        minLeafSize = new int2(18, 18),
                        roomPadding = 1,
                        corridorBrushRadius = 0,
                        connectWithManhattan = true,
                        clearBeforeGenerate = true
                    },
                    Expected = 0x2C510AC506B4D32FUL
                },
                new RoomFirstBspCase
                {
                    Name = "E2_RoomFirstBsp_96_seed3333_line",
                    Width = 96, Height = 96,
                    Seed = 3333,
                    Cfg = new RoomFirstBspDungeon2D.RoomFirstBspConfig
                    {
                        splitIterations = 4,
                        minLeafSize = new int2(18, 18),
                        roomPadding = 1,
                        corridorBrushRadius = 0,
                        connectWithManhattan = false,
                        clearBeforeGenerate = true
                    },
                    Expected = 0xCE42E3515C41D077UL
                },
                new RoomFirstBspCase
                {
                    Name = "E2_RoomFirstBsp_128_seed9001_brush1",
                    Width = 128, Height = 128,
                    Seed = 9001,
                    Cfg = new RoomFirstBspDungeon2D.RoomFirstBspConfig
                    {
                        splitIterations = 5,
                        minLeafSize = new int2(20, 20),
                        roomPadding = 2,
                        corridorBrushRadius = 1,
                        connectWithManhattan = true,
                        clearBeforeGenerate = true
                    },
                    Expected = 0x3785D3FA36D3A7EEUL
                },
            };

            for (int i = 0; i < bspCases.Length; i++)
            {
                ValidateCase(
                    bspCases[i].Name,
                    bspCases[i].Expected,
                    () => ComputeRoomFirstBsp(in bspCases[i]),
                    ref failures,
                    sb);
            }

            // -----------------------
            // E3 — Room Grid
            // -----------------------
            var gridCases = new RoomGridCase[]
            {
                new RoomGridCase
                {
                    Name = "E3_RoomGrid_96_seed4444",
                    Width = 96, Height = 96,
                    Seed = 4444,
                    Cfg = new RoomGridDungeon2D.RoomGridConfig
                    {
                        roomCount = 16,
                        cellSize = 10,
                        borderPadding = 1,

                        roomSizeMin = new int2(6, 6),
                        roomSizeMax = new int2(14, 14),

                        corridorBrushRadius = 0,
                        connectWithManhattan = true,
                        clearBeforeGenerate = true
                    },
                    Expected = 0xBAAFD41688231C32UL
                },
                new RoomGridCase
                {
                    Name = "E3_RoomGrid_64_seed4444_tighter",
                    Width = 64, Height = 64,
                    Seed = 4444,
                    Cfg = new RoomGridDungeon2D.RoomGridConfig
                    {
                        roomCount = 10,
                        cellSize = 8,
                        borderPadding = 1,

                        roomSizeMin = new int2(5, 5),
                        roomSizeMax = new int2(11, 11),

                        corridorBrushRadius = 0,
                        connectWithManhattan = true,
                        clearBeforeGenerate = true
                    },
                    Expected = 0x5D10088BD7DAD9E5UL
                },
                new RoomGridCase
                {
                    Name = "E3_RoomGrid_128x96_seed8888_noL_brush1",
                    Width = 128, Height = 96,
                    Seed = 8888,
                    Cfg = new RoomGridDungeon2D.RoomGridConfig
                    {
                        roomCount = 24,
                        cellSize = 12,
                        borderPadding = 2,

                        roomSizeMin = new int2(6, 6),
                        roomSizeMax = new int2(16, 16),

                        corridorBrushRadius = 1,
                        connectWithManhattan = false,
                        clearBeforeGenerate = true
                    },
                    Expected = 0xC011160E9B6359F0UL
                },
            };

            for (int i = 0; i < gridCases.Length; i++)
            {
                ValidateCase(
                    gridCases[i].Name,
                    gridCases[i].Expected,
                    () => ComputeRoomGrid(in gridCases[i]),
                    ref failures,
                    sb);
            }

            // Final gate
            if (failures > 0)
            {
                Assert.Fail(sb.ToString());
            }
        }

        // -----------------------
        // Validation helper
        // -----------------------
        private static void ValidateCase(
            string name,
            ulong expected,
            System.Func<Result> compute,
            ref int failures,
            StringBuilder sb)
        {
            // Determinism check: compute twice (fresh allocations/rng each time)
            Result r1 = compute();
            Result r2 = compute();

            if (r1.Hash != r2.Hash)
            {
                failures++;
                sb.AppendLine($"[NON-DETERMINISTIC] {name}");
                sb.AppendLine($"  run1: 0x{r1.Hash:X16} ones={r1.Ones} a={r1.MetricA} b={r1.MetricB}");
                sb.AppendLine($"  run2: 0x{r2.Hash:X16} ones={r2.Ones} a={r2.MetricA} b={r2.MetricB}");
                sb.AppendLine();
                return;
            }

            if (expected == UNSET)
            {
                failures++;
                sb.AppendLine($"[GOLDEN UNSET] {name}");
                sb.AppendLine($"  SET Expected = 0x{r1.Hash:X16}UL;  (ones={r1.Ones} a={r1.MetricA} b={r1.MetricB})");
                sb.AppendLine();
                return;
            }

            if (r1.Hash != expected)
            {
                failures++;
                sb.AppendLine($"[MISMATCH] {name}");
                sb.AppendLine($"  expected: 0x{expected:X16}");
                sb.AppendLine($"  actual:   0x{r1.Hash:X16}  (ones={r1.Ones} a={r1.MetricA} b={r1.MetricB})");
                sb.AppendLine();
            }
        }

        // -----------------------
        // Per-strategy runners
        // -----------------------
        private static Result ComputeCorridorFirst(in CorridorFirstCase c)
        {
            var domain = new GridDomain2D(c.Width, c.Height);

            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            try
            {
                Random rng = LayoutSeedUtil.CreateRng(c.Seed);

                int endpointCap = math.max(1, c.Cfg.corridorCount + 1);
                using var endpoints = new NativeArray<int2>(endpointCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                int outCap = math.max(1, domain.Length);
                using var roomCenters = new NativeArray<int2>(outCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                CorridorFirstDungeon2D.Generate(
                    ref mask,
                    ref rng,
                    in c.Cfg,
                    endpoints,
                    roomCenters,
                    out int placedRooms);

                return new Result
                {
                    Hash = mask.SnapshotHash64(),
                    Ones = mask.CountOnes(),
                    MetricA = placedRooms,
                    MetricB = 0
                };
            }
            finally
            {
                mask.Dispose();
            }
        }


        private static Result ComputeRoomFirstBsp(in RoomFirstBspCase c)
        {
            var domain = new GridDomain2D(c.Width, c.Height);

            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            try
            {
                Random rng = LayoutSeedUtil.CreateRng(c.Seed);

                int leafCap = math.max(1, BspPartition2D.MaxLeavesUpperBound(c.Cfg.splitIterations));
                using var leaves = new NativeArray<BspPartition2D.IntRect2D>(leafCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                using var centers = new NativeArray<int2>(leafCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                RoomFirstBspDungeon2D.Generate(
                    ref mask,
                    ref rng,
                    in c.Cfg,
                    leaves,
                    centers,
                    out int leafCount,
                    out int placedRooms);

                return new Result
                {
                    Hash = mask.SnapshotHash64(),
                    Ones = mask.CountOnes(),
                    MetricA = placedRooms,
                    MetricB = leafCount
                };
            }
            finally
            {
                mask.Dispose();
            }
        }


        private static Result ComputeRoomGrid(in RoomGridCase c)
        {
            var domain = new GridDomain2D(c.Width, c.Height);

            var mask = new MaskGrid2D(domain, Allocator.Temp, clearToZero: true);
            try
            {
                Random rng = LayoutSeedUtil.CreateRng(c.Seed);

                int take = math.max(1, c.Cfg.roomCount);
                using var picked = new NativeArray<int>(take, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                using var centers = new NativeArray<int2>(take, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                RoomGridDungeon2D.Generate(
                    ref mask,
                    ref rng,
                    in c.Cfg,
                    picked,
                    centers,
                    out int placedRooms);

                return new Result
                {
                    Hash = mask.SnapshotHash64(),
                    Ones = mask.CountOnes(),
                    MetricA = placedRooms,
                    MetricB = 0
                };
            }
            finally
            {
                mask.Dispose();
            }
        }

    }
}
