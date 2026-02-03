using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;
using Islands.PCG.Layout.Bsp;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Lantern/Visualization testbed for grid-based dungeon generation (MaskGrid2D).
    /// Includes D2/D3/D5/E1 and Phase E2 (Room First BSP).
    /// </summary>
    public sealed class PCGDungeonVisualization : Visualization
    {
        private static readonly int NoiseId = Shader.PropertyToID("_Noise");
        private static readonly int MaskOffColorId = Shader.PropertyToID("_MaskOffColor");
        private static readonly int MaskOnColorId = Shader.PropertyToID("_MaskOnColor");

        private enum StrategyMode
        {
            SimpleRandomWalk = 0,
            IteratedRandomWalk = 1,
            RoomsCorridors = 2,
            CorridorFirst = 3,
            RoomFirstBsp = 4,
            RoomGrid = 5,
        }

        // -------------------------
        // Strategy + palette
        // -------------------------
        [Header("Strategy")]
        [SerializeField] private StrategyMode strategy = StrategyMode.IteratedRandomWalk;

        [Header("Palette (0/1)")]
        [SerializeField] private Color maskOffColor = Color.black;
        [SerializeField] private Color maskOnColor = Color.white;

        // -------------------------
        // Walk config (D2/D3)
        // -------------------------
        [Header("Random Walk (D2/D3)")]
        [SerializeField] private uint walkSeed = 1u;
        [SerializeField] private Vector2Int walkStart = new Vector2Int(8, 8);
        [SerializeField] private bool walkClearBeforeDraw = true;

        [Min(0)]
        [SerializeField] private int walkLength = 200;

        [Min(1)]
        [SerializeField] private int walkIterations = 20;

        [Min(0)]
        [SerializeField] private int walkLengthMin = 25;

        [Min(0)]
        [SerializeField] private int walkLengthMax = 75;

        [Range(0f, 1f)]
        [SerializeField] private float walkRandomStartChance = 0.2f;

        [Header("Walk Brush + Skew (D2/D3)")]
        [Min(0)]
        [SerializeField] private int walkBrushRadius = 1;

        [Range(-1f, 1f)]
        [SerializeField] private float walkSkewX = 0f;

        [Range(-1f, 1f)]
        [SerializeField] private float walkSkewY = 0f;

        [Min(0)]
        [SerializeField] private int walkMaxRetries = 8;

        // -------------------------
        // Rooms + Corridors (D5)
        // -------------------------
        [Header("Rooms + Corridors (D5)")]
        [SerializeField] private int rcSeed = 1;

        [Min(0)]
        [SerializeField] private int rcRoomCount = 12;

        [SerializeField] private Vector2Int rcRoomSizeMin = new Vector2Int(6, 6);
        [SerializeField] private Vector2Int rcRoomSizeMax = new Vector2Int(14, 14);

        [Min(1)]
        [SerializeField] private int rcPlacementAttemptsPerRoom = 32;

        [Min(0)]
        [SerializeField] private int rcRoomPadding = 1;

        [Min(0)]
        [SerializeField] private int rcCorridorBrushRadius = 1;

        [SerializeField] private bool rcClearBeforeDraw = true;
        [SerializeField] private bool rcAllowOverlap = false;

        // -------------------------
        // Corridor First (E1)
        // -------------------------
        [Header("Corridor First (E1)")]
        [SerializeField] private int cfSeed = 1;

        [Min(0)]
        [SerializeField] private int cfCorridorCount = 64;

        [Min(1)]
        [SerializeField] private int cfCorridorLengthMin = 6;

        [Min(1)]
        [SerializeField] private int cfCorridorLengthMax = 18;

        [Min(0)]
        [SerializeField] private int cfCorridorBrushRadius = 1;

        [Min(0)]
        [SerializeField] private int cfBorderPadding = 2;

        [SerializeField] private bool cfClearBeforeDraw = true;

        [SerializeField] private int cfRoomSpawnCount = 8;
        [Range(0f, 1f)]
        [SerializeField] private float cfRoomSpawnChance = 0.6f;

        [SerializeField] private Vector2Int cfRoomSizeMin = new Vector2Int(6, 6);
        [SerializeField] private Vector2Int cfRoomSizeMax = new Vector2Int(14, 14);

        [SerializeField] private bool cfEnsureRoomsAtDeadEnds = true;

        // -------------------------
        // Room First BSP (E2)
        // -------------------------
        [Header("Room First BSP (E2)")]
        [SerializeField] private int rbSeed = 1;

        [Min(0)]
        [SerializeField] private int rbSplitIterations = 6;

        [SerializeField] private Vector2Int rbMinLeafSize = new Vector2Int(16, 16);

        [Min(0)]
        [SerializeField] private int rbRoomPadding = 1;

        [Min(0)]
        [SerializeField] private int rbCorridorBrushRadius = 0;

        [SerializeField] private bool rbConnectWithManhattan = true;
        [SerializeField] private bool rbClearBeforeDraw = true;

        // -------------------------
        // Room Grid (E3)
        // -------------------------
        [Header("Room Grid (E3)")]
        [Tooltip("Seed for RoomGrid. Clamped to >= 1.")]
        [SerializeField] private int rgSeed = 1;

        [Tooltip("How many rooms to place (unique nodes on coarse grid).")]
        [Min(0)]
        [SerializeField] private int rgRoomCount = 16;

        [Tooltip("Coarse grid spacing in cells (>= 1).")]
        [Min(1)]
        [SerializeField] private int rgCellSize = 10;

        [Tooltip("Padding from borders used to build the coarse grid interior.")]
        [Min(0)]
        [SerializeField] private int rgBorderPadding = 1;

        [Tooltip("Minimum room size (width,height) in cells. Values are clamped to >= 1.")]
        [SerializeField] private Vector2Int rgRoomSizeMin = new Vector2Int(6, 6);

        [Tooltip("Maximum room size (width,height) in cells. Values are clamped to >= 1.")]
        [SerializeField] private Vector2Int rgRoomSizeMax = new Vector2Int(14, 14);

        [Tooltip("Brush radius for corridors (DrawLine). 0 = 1-cell wide.")]
        [Min(0)]
        [SerializeField] private int rgCorridorBrushRadius = 0;

        [Tooltip("If true, connects with Manhattan L (two DrawLine segments). Otherwise direct DrawLine.")]
        [SerializeField] private bool rgConnectWithManhattan = true;

        [Tooltip("If true, clears the mask before generating.")]
        [SerializeField] private bool rgClearBeforeDraw = true;

        // -------------------------
        // Runtime state
        // -------------------------
        private NativeArray<float4> packedNoise;
        private ComputeBuffer noiseBuffer;

        private MaskGrid2D mask;
        private int maskResolution = -1;

        private MaterialPropertyBlock mpb;

        private int updateCalls = 0;
        private bool loggedFirstUpdate = false;

        // Strategy caching
        private int lastResolution = -1;
        private StrategyMode lastStrategy = (StrategyMode)(-1);

        // Walk cache
        private bool walkDirty = true;
        private uint lastWalkSeed;
        private Vector2Int lastWalkStart;
        private bool lastWalkClearBeforeDraw;
        private int lastWalkLength;
        private int lastWalkIterations;
        private int lastWalkLengthMin;
        private int lastWalkLengthMax;
        private float lastWalkRandomStartChance;
        private int lastWalkBrushRadius;
        private float lastWalkSkewX;
        private float lastWalkSkewY;
        private int lastWalkMaxRetries;

        // Rooms+Corridors cache
        private bool roomsCorridorsDirty = true;
        private int lastRcSeed;
        private int lastRcRoomCount;
        private Vector2Int lastRcRoomSizeMin;
        private Vector2Int lastRcRoomSizeMax;
        private int lastRcPlacementAttemptsPerRoom;
        private int lastRcRoomPadding;
        private int lastRcCorridorBrushRadius;
        private bool lastRcClearBeforeDraw;
        private bool lastRcAllowOverlap;
        private int lastPlacedRcRooms = 0;

        // CorridorFirst cache
        private bool corridorFirstDirty = true;
        private int lastCfSeed;
        private int lastCfCorridorCount;
        private int lastCfCorridorLengthMin;
        private int lastCfCorridorLengthMax;
        private int lastCfCorridorBrushRadius;
        private int lastCfBorderPadding;
        private bool lastCfClearBeforeDraw;
        private int lastCfRoomSpawnCount;
        private float lastCfRoomSpawnChance;
        private Vector2Int lastCfRoomSizeMin;
        private Vector2Int lastCfRoomSizeMax;
        private bool lastCfEnsureRoomsAtDeadEnds;
        private int lastPlacedCfRooms = 0;

        // RoomFirst BSP cache
        private bool roomFirstBspDirty = true;
        private int lastRbSeed;
        private int lastRbSplitIterations;
        private Vector2Int lastRbMinLeafSize;
        private int lastRbRoomPadding;
        private int lastRbCorridorBrushRadius;
        private bool lastRbConnectWithManhattan;
        private bool lastRbClearBeforeDraw;
        private int lastRbLeafCount = 0;
        private int lastPlacedRbRooms = 0;

        private bool roomGridDirty = true;

        private int lastRgSeed;
        private int lastRgRoomCount;
        private int lastRgCellSize;
        private int lastRgBorderPadding;
        private Vector2Int lastRgRoomSizeMin;
        private Vector2Int lastRgRoomSizeMax;
        private int lastRgCorridorBrushRadius;
        private bool lastRgConnectWithManhattan;
        private bool lastRgClearBeforeDraw;

        private int lastPlacedRgRooms = 0;


        protected override void EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock)
        {
            mpb = propertyBlock;

            packedNoise = new NativeArray<float4>(dataLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            // IMPORTANT:
            // packedNoise is float4 packs (dataLength packs).
            // The shader expects _Noise to be a per-instance float buffer (count == resolution*resolution).
            // Therefore, buffer element count must be dataLength * 4 with stride sizeof(float).
            noiseBuffer = new ComputeBuffer(dataLength * 4, sizeof(float));
            mpb.SetBuffer(NoiseId, noiseBuffer);

            ApplyPaletteToMpb();

            lastResolution = -1;
            lastStrategy = (StrategyMode)(-1);

            // Mark everything dirty on enable (parity with other strategies)
            walkDirty = true;
            roomsCorridorsDirty = true;
            corridorFirstDirty = true;
            roomFirstBspDirty = true;
            roomGridDirty = true;

            // Cache all params so "ParamsChanged()" comparisons start from a defined baseline
            CacheWalkParams();
            CacheRoomsCorridorsParams();
            CacheCorridorFirstParams();
            CacheRoomFirstBspParams();
            CacheRoomGridParams();
        }

        protected override void DisableVisualization()
        {
            if (packedNoise.IsCreated) packedNoise.Dispose();

            if (noiseBuffer != null)
            {
                noiseBuffer.Release();
                noiseBuffer = null;
            }

            if (mask.IsCreated) mask.Dispose();
            maskResolution = -1;

            // Mark dirty so the next enable/update regenerates safely.
            roomGridDirty = true;

            mpb = null;
        }


        protected override void UpdateVisualization(
            NativeArray<float3x4> positions,
            int resolution,
            JobHandle handle)
        {
            // Base will SetData(positions/normals) right after calling us; make sure the shape jobs are done.
            handle.Complete();

            updateCalls++;
            ApplyPaletteToMpb();

            bool shouldLogThisCall = !loggedFirstUpdate;

            // Force regen if base resolution changes
            if (resolution != lastResolution)
            {
                lastResolution = resolution;
                walkDirty = true;
                roomsCorridorsDirty = true;
                corridorFirstDirty = true;
                roomFirstBspDirty = true;
                roomGridDirty = true;
            }

            if (strategy != lastStrategy)
            {
                lastStrategy = strategy;
                walkDirty = true;
                roomsCorridorsDirty = true;
                corridorFirstDirty = true;
                roomFirstBspDirty = true;
                roomGridDirty = true;
            }

            // Parameter dirty checks (only for active strategy)
            if (strategy == StrategyMode.SimpleRandomWalk || strategy == StrategyMode.IteratedRandomWalk)
            {
                if (WalkParamsChanged())
                {
                    CacheWalkParams();
                    walkDirty = true;
                }
            }
            else if (strategy == StrategyMode.RoomsCorridors)
            {
                if (RoomsCorridorsParamsChanged())
                {
                    CacheRoomsCorridorsParams();
                    roomsCorridorsDirty = true;
                }
            }
            else if (strategy == StrategyMode.CorridorFirst)
            {
                if (CorridorFirstParamsChanged())
                {
                    CacheCorridorFirstParams();
                    corridorFirstDirty = true;
                }
            }
            else if (strategy == StrategyMode.RoomFirstBsp)
            {
                if (RoomFirstBspParamsChanged())
                {
                    CacheRoomFirstBspParams();
                    roomFirstBspDirty = true;
                }
            }
            else if (strategy == StrategyMode.RoomGrid)
            {
                if (RoomGridParamsChanged())
                {
                    CacheRoomGridParams();
                    roomGridDirty = true;
                }
            }

            EnsureMaskAllocated(resolution);

            bool uploadedThisFrame = false;

            switch (strategy)
            {
                case StrategyMode.SimpleRandomWalk:
                    {
                        if (!walkDirty) break;

                        if (walkClearBeforeDraw) mask.Clear();

                        uint seed = (walkSeed == 0u) ? 1u : walkSeed;
                        var rng = new Unity.Mathematics.Random(seed);

                        int sx = Mathf.Clamp(walkStart.x, 0, resolution - 1);
                        int sy = Mathf.Clamp(walkStart.y, 0, resolution - 1);

                        SimpleRandomWalk2D.Walk(
                            ref mask,
                            ref rng,
                            new int2(sx, sy),
                            walkLength,
                            brushRadius: math.max(0, walkBrushRadius),
                            skewX: walkSkewX,
                            skewY: walkSkewY,
                            maxRetries: math.max(0, walkMaxRetries));

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        walkDirty = false;
                        break;
                    }

                case StrategyMode.IteratedRandomWalk:
                    {
                        if (!walkDirty) break;

                        if (walkClearBeforeDraw) mask.Clear();

                        uint seed = (walkSeed == 0u) ? 1u : walkSeed;
                        var rng = new Unity.Mathematics.Random(seed);

                        int sx = Mathf.Clamp(walkStart.x, 0, resolution - 1);
                        int sy = Mathf.Clamp(walkStart.y, 0, resolution - 1);

                        // IMPORTANT: your IteratedRandomWalk2D API is Carve(...) (no config struct).
                        IteratedRandomWalk2D.Carve(
                            ref mask,
                            ref rng,
                            new int2(sx, sy),
                            iterations: math.max(1, walkIterations),
                            walkLengthMin: math.max(0, walkLengthMin),
                            walkLengthMax: math.max(math.max(0, walkLengthMin), walkLengthMax),
                            brushRadius: math.max(0, walkBrushRadius),
                            randomStartChance: math.clamp(walkRandomStartChance, 0f, 1f),
                            skewX: walkSkewX,
                            skewY: walkSkewY,
                            maxRetries: math.max(0, walkMaxRetries));

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        walkDirty = false;
                        break;
                    }

                case StrategyMode.RoomsCorridors:
                    {
                        if (!roomsCorridorsDirty) break;

                        var cfg = new RoomsCorridorsComposer2D.RoomsCorridorsConfig
                        {
                            roomCount = math.max(0, rcRoomCount),
                            roomSizeMin = new int2(math.max(1, rcRoomSizeMin.x), math.max(1, rcRoomSizeMin.y)),
                            roomSizeMax = new int2(math.max(1, rcRoomSizeMax.x), math.max(1, rcRoomSizeMax.y)),
                            placementAttemptsPerRoom = math.max(1, rcPlacementAttemptsPerRoom),
                            roomPadding = math.max(0, rcRoomPadding),
                            corridorBrushRadius = math.max(0, rcCorridorBrushRadius),
                            clearBeforeGenerate = rcClearBeforeDraw,
                            allowOverlap = rcAllowOverlap,
                        };

                        uint seed = (uint)math.max(rcSeed, 1);
                        var rng = new Unity.Mathematics.Random(seed);

                        int centersLen = math.max(1, cfg.roomCount);
                        using var centers = new NativeArray<int2>(centersLen, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        RoomsCorridorsComposer2D.Generate(ref mask, ref rng, in cfg, centers, out int placedRooms);
                        lastPlacedRcRooms = placedRooms;

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        roomsCorridorsDirty = false;
                        break;
                    }

                case StrategyMode.CorridorFirst:
                    {
                        if (!corridorFirstDirty) break;

                        var cfg = new CorridorFirstDungeon2D.CorridorFirstConfig
                        {
                            corridorCount = math.max(0, cfCorridorCount),
                            corridorLengthMin = math.max(1, cfCorridorLengthMin),
                            corridorLengthMax = math.max(math.max(1, cfCorridorLengthMin), cfCorridorLengthMax),
                            corridorBrushRadius = math.max(0, cfCorridorBrushRadius),

                            roomSpawnCount = cfRoomSpawnCount,
                            roomSpawnChance = math.clamp(cfRoomSpawnChance, 0f, 1f),
                            roomSizeMin = new int2(math.max(1, cfRoomSizeMin.x), math.max(1, cfRoomSizeMin.y)),
                            roomSizeMax = new int2(math.max(1, cfRoomSizeMax.x), math.max(1, cfRoomSizeMax.y)),

                            borderPadding = math.max(0, cfBorderPadding),
                            clearBeforeGenerate = cfClearBeforeDraw,
                            ensureRoomsAtDeadEnds = cfEnsureRoomsAtDeadEnds,
                        };

                        uint seed = (uint)math.max(cfSeed, 1);
                        var rng = new Unity.Mathematics.Random(seed);

                        int endpointsCap = math.max(1, cfg.corridorCount * 2);
                        using var endpoints = new NativeArray<int2>(endpointsCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        int centersCap = math.max(1, cfg.roomSpawnCount > 0 ? cfg.roomSpawnCount : endpointsCap);
                        using var centers = new NativeArray<int2>(centersCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        CorridorFirstDungeon2D.Generate(ref mask, ref rng, in cfg, endpoints, centers, out int placedRooms);
                        lastPlacedCfRooms = placedRooms;

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        corridorFirstDirty = false;
                        break;
                    }

                case StrategyMode.RoomFirstBsp:
                    {
                        if (!roomFirstBspDirty) break;

                        var cfg = new RoomFirstBspDungeon2D.RoomFirstBspConfig
                        {
                            splitIterations = math.max(0, rbSplitIterations),
                            minLeafSize = new int2(math.max(1, rbMinLeafSize.x), math.max(1, rbMinLeafSize.y)),
                            roomPadding = math.max(0, rbRoomPadding),
                            corridorBrushRadius = math.max(0, rbCorridorBrushRadius),
                            connectWithManhattan = rbConnectWithManhattan,
                            clearBeforeGenerate = rbClearBeforeDraw,
                        };

                        uint seed = (uint)math.max(rbSeed, 1);
                        var rng = new Unity.Mathematics.Random(seed);

                        int maxLeaves = BspPartition2D.MaxLeavesUpperBound(cfg.splitIterations);
                        maxLeaves = math.max(1, maxLeaves);

                        using var leaves = new NativeArray<BspPartition2D.IntRect2D>(maxLeaves, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                        using var centers = new NativeArray<int2>(maxLeaves, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        RoomFirstBspDungeon2D.Generate(ref mask, ref rng, in cfg, leaves, centers, out int leafCount, out int placedRooms);
                        lastRbLeafCount = leafCount;
                        lastPlacedRbRooms = placedRooms;

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        roomFirstBspDirty = false;
                        break;
                    }

                case StrategyMode.RoomGrid:
                    {
                        if (!roomGridDirty) break;

                        var cfg = new RoomGridDungeon2D.RoomGridConfig
                        {
                            roomCount = math.max(0, rgRoomCount),
                            cellSize = math.max(1, rgCellSize),
                            borderPadding = math.max(0, rgBorderPadding),

                            roomSizeMin = new int2(math.max(1, rgRoomSizeMin.x), math.max(1, rgRoomSizeMin.y)),
                            roomSizeMax = new int2(math.max(1, rgRoomSizeMax.x), math.max(1, rgRoomSizeMax.y)),

                            corridorBrushRadius = math.max(0, rgCorridorBrushRadius),
                            connectWithManhattan = rgConnectWithManhattan,
                            clearBeforeGenerate = rgClearBeforeDraw,
                        };

                        var rng = LayoutSeedUtil.CreateRng(rgSeed);

                        int take = math.max(1, cfg.roomCount);
                        using var picked = new NativeArray<int>(take, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                        using var centers = new NativeArray<int2>(take, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        RoomGridDungeon2D.Generate(ref mask, ref rng, in cfg, picked, centers, out int placedRooms);
                        lastPlacedRgRooms = placedRooms;

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        roomGridDirty = false;
                        break;
                    }
            }

            if (shouldLogThisCall)
            {
                loggedFirstUpdate = true;

                int ones = mask.CountOnes();
                string extra =
                    (strategy == StrategyMode.RoomsCorridors) ? $" placedRooms={lastPlacedRcRooms}" :
                    (strategy == StrategyMode.CorridorFirst) ? $" placedRooms={lastPlacedCfRooms}" :
                    (strategy == StrategyMode.RoomFirstBsp) ? $" placedRooms={lastPlacedRbRooms} leaves={lastRbLeafCount}" :
                    (strategy == StrategyMode.RoomGrid) ? $" placedRooms={lastPlacedRgRooms}" :
                    string.Empty;

                Debug.Log($"[PCGDungeonVisualization] Update #{updateCalls} strategy={strategy} res={resolution} uploaded={uploadedThisFrame} ones={ones}{extra}");
            }
        }

        private void EnsureMaskAllocated(int resolution)
        {
            if (mask.IsCreated && maskResolution == resolution) return;

            if (mask.IsCreated) mask.Dispose();

            maskResolution = resolution;
            var domain = new GridDomain2D(resolution, resolution);
            mask = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
        }

        private void PackFromMaskAndUpload(int resolution)
        {
            int totalInstances = resolution * resolution;
            int packs = packedNoise.Length;

            for (int packIndex = 0; packIndex < packs; packIndex++)
            {
                int baseInstance = packIndex * 4;

                float v0 = (baseInstance + 0 < totalInstances) ? MaskInstanceValue(baseInstance + 0, resolution) : 0f;
                float v1 = (baseInstance + 1 < totalInstances) ? MaskInstanceValue(baseInstance + 1, resolution) : 0f;
                float v2 = (baseInstance + 2 < totalInstances) ? MaskInstanceValue(baseInstance + 2, resolution) : 0f;
                float v3 = (baseInstance + 3 < totalInstances) ? MaskInstanceValue(baseInstance + 3, resolution) : 0f;

                packedNoise[packIndex] = new float4(v0, v1, v2, v3);
            }

            // Upload as float stream: length == packedNoise.Length * 4
            noiseBuffer.SetData(packedNoise.Reinterpret<float>(sizeof(float) * 4));
        }

        private float MaskInstanceValue(int instanceIndex, int resolution)
        {
            int x = instanceIndex % resolution;
            int y = instanceIndex / resolution;
            return mask.Get(x, y) ? 1f : 0f;
        }

        private void ApplyPaletteToMpb()
        {
            if (mpb == null) return;
            mpb.SetColor(MaskOffColorId, maskOffColor);
            mpb.SetColor(MaskOnColorId, maskOnColor);
        }

        private void CacheWalkParams()
        {
            lastWalkSeed = walkSeed;
            lastWalkStart = walkStart;
            lastWalkClearBeforeDraw = walkClearBeforeDraw;

            lastWalkLength = walkLength;
            lastWalkIterations = walkIterations;
            lastWalkLengthMin = walkLengthMin;
            lastWalkLengthMax = walkLengthMax;
            lastWalkRandomStartChance = walkRandomStartChance;

            lastWalkBrushRadius = walkBrushRadius;
            lastWalkSkewX = walkSkewX;
            lastWalkSkewY = walkSkewY;
            lastWalkMaxRetries = walkMaxRetries;
        }

        private bool WalkParamsChanged()
        {
            return walkSeed != lastWalkSeed ||
                   walkStart != lastWalkStart ||
                   walkClearBeforeDraw != lastWalkClearBeforeDraw ||
                   walkLength != lastWalkLength ||
                   walkIterations != lastWalkIterations ||
                   walkLengthMin != lastWalkLengthMin ||
                   walkLengthMax != lastWalkLengthMax ||
                   !Mathf.Approximately(walkRandomStartChance, lastWalkRandomStartChance) ||
                   walkBrushRadius != lastWalkBrushRadius ||
                   !Mathf.Approximately(walkSkewX, lastWalkSkewX) ||
                   !Mathf.Approximately(walkSkewY, lastWalkSkewY) ||
                   walkMaxRetries != lastWalkMaxRetries;
        }

        private void CacheRoomsCorridorsParams()
        {
            lastRcSeed = rcSeed;
            lastRcRoomCount = rcRoomCount;
            lastRcRoomSizeMin = rcRoomSizeMin;
            lastRcRoomSizeMax = rcRoomSizeMax;
            lastRcPlacementAttemptsPerRoom = rcPlacementAttemptsPerRoom;
            lastRcRoomPadding = rcRoomPadding;
            lastRcCorridorBrushRadius = rcCorridorBrushRadius;
            lastRcClearBeforeDraw = rcClearBeforeDraw;
            lastRcAllowOverlap = rcAllowOverlap;
        }

        private bool RoomsCorridorsParamsChanged()
        {
            return rcSeed != lastRcSeed ||
                   rcRoomCount != lastRcRoomCount ||
                   rcRoomSizeMin != lastRcRoomSizeMin ||
                   rcRoomSizeMax != lastRcRoomSizeMax ||
                   rcPlacementAttemptsPerRoom != lastRcPlacementAttemptsPerRoom ||
                   rcRoomPadding != lastRcRoomPadding ||
                   rcCorridorBrushRadius != lastRcCorridorBrushRadius ||
                   rcClearBeforeDraw != lastRcClearBeforeDraw ||
                   rcAllowOverlap != lastRcAllowOverlap;
        }

        private void CacheCorridorFirstParams()
        {
            lastCfSeed = cfSeed;
            lastCfCorridorCount = cfCorridorCount;
            lastCfCorridorLengthMin = cfCorridorLengthMin;
            lastCfCorridorLengthMax = cfCorridorLengthMax;
            lastCfCorridorBrushRadius = cfCorridorBrushRadius;
            lastCfBorderPadding = cfBorderPadding;
            lastCfClearBeforeDraw = cfClearBeforeDraw;

            lastCfRoomSpawnCount = cfRoomSpawnCount;
            lastCfRoomSpawnChance = cfRoomSpawnChance;
            lastCfRoomSizeMin = cfRoomSizeMin;
            lastCfRoomSizeMax = cfRoomSizeMax;
            lastCfEnsureRoomsAtDeadEnds = cfEnsureRoomsAtDeadEnds;
        }

        private bool CorridorFirstParamsChanged()
        {
            return cfSeed != lastCfSeed ||
                   cfCorridorCount != lastCfCorridorCount ||
                   cfCorridorLengthMin != lastCfCorridorLengthMin ||
                   cfCorridorLengthMax != lastCfCorridorLengthMax ||
                   cfCorridorBrushRadius != lastCfCorridorBrushRadius ||
                   cfBorderPadding != lastCfBorderPadding ||
                   cfClearBeforeDraw != lastCfClearBeforeDraw ||
                   cfRoomSpawnCount != lastCfRoomSpawnCount ||
                   !Mathf.Approximately(cfRoomSpawnChance, lastCfRoomSpawnChance) ||
                   cfRoomSizeMin != lastCfRoomSizeMin ||
                   cfRoomSizeMax != lastCfRoomSizeMax ||
                   cfEnsureRoomsAtDeadEnds != lastCfEnsureRoomsAtDeadEnds;
        }

        private void CacheRoomFirstBspParams()
        {
            lastRbSeed = rbSeed;
            lastRbSplitIterations = rbSplitIterations;
            lastRbMinLeafSize = rbMinLeafSize;
            lastRbRoomPadding = rbRoomPadding;
            lastRbCorridorBrushRadius = rbCorridorBrushRadius;
            lastRbConnectWithManhattan = rbConnectWithManhattan;
            lastRbClearBeforeDraw = rbClearBeforeDraw;
        }

        private bool RoomFirstBspParamsChanged()
        {
            return rbSeed != lastRbSeed ||
                   rbSplitIterations != lastRbSplitIterations ||
                   rbMinLeafSize != lastRbMinLeafSize ||
                   rbRoomPadding != lastRbRoomPadding ||
                   rbCorridorBrushRadius != lastRbCorridorBrushRadius ||
                   rbConnectWithManhattan != lastRbConnectWithManhattan ||
                   rbClearBeforeDraw != lastRbClearBeforeDraw;
        }

        private void CacheRoomGridParams()
        {
            lastRgSeed = rgSeed;
            lastRgRoomCount = rgRoomCount;
            lastRgCellSize = rgCellSize;
            lastRgBorderPadding = rgBorderPadding;
            lastRgRoomSizeMin = rgRoomSizeMin;
            lastRgRoomSizeMax = rgRoomSizeMax;
            lastRgCorridorBrushRadius = rgCorridorBrushRadius;
            lastRgConnectWithManhattan = rgConnectWithManhattan;
            lastRgClearBeforeDraw = rgClearBeforeDraw;
        }

        private bool RoomGridParamsChanged()
        {
            return rgSeed != lastRgSeed ||
                   rgRoomCount != lastRgRoomCount ||
                   rgCellSize != lastRgCellSize ||
                   rgBorderPadding != lastRgBorderPadding ||
                   rgRoomSizeMin != lastRgRoomSizeMin ||
                   rgRoomSizeMax != lastRgRoomSizeMax ||
                   rgCorridorBrushRadius != lastRgCorridorBrushRadius ||
                   rgConnectWithManhattan != lastRgConnectWithManhattan ||
                   rgClearBeforeDraw != lastRgClearBeforeDraw;
        }

    }
}
