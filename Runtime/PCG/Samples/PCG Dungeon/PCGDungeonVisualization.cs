using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Lean dungeon-strategy visualization testbed for grid-based generation.
    ///
    /// Supported strategies:
    /// - Simple Random Walk (Phase D2)
    /// - Iterated Random Walk (Phase D3)
    /// - Rooms + Corridors (Phase D5)
    /// - Corridor First (Phase E1)
    ///
    /// This component writes into a MaskGrid2D (no Tilemaps/HashSets) and uploads the result
    /// as packed float4 data to the "_Noise" buffer expected by the Islands GPU instancing path.
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
        }

        // -------------------------
        // Strategy + palette
        // -------------------------
        [Header("Strategy")]
        [Tooltip("Which dungeon strategy to visualize.")]
        [SerializeField] private StrategyMode strategy = StrategyMode.IteratedRandomWalk;

        [Header("Palette (0/1)")]
        [Tooltip("Color used when a cell is OFF (mask value = 0).")]
        [SerializeField] private Color maskOffColor = Color.black;

        [Tooltip("Color used when a cell is ON (mask value = 1).")]
        [SerializeField] private Color maskOnColor = Color.white;

        [Header("Logs")]
        [Tooltip("If enabled, emits a debug log line describing the generated mask.")]
        [SerializeField] private bool enableLogs = false;

        [Tooltip("Log on every N UpdateVisualization calls. 0 = log only the first call.")]
        [SerializeField] private int logEveryNUpdates = 0;

        // -------------------------
        // Random Walk (Phase D2 / D3)
        // -------------------------
        [Header("Random Walk (D2/D3)")]
        [Tooltip("Seed for Unity.Mathematics.Random. If 0, it is clamped to 1.")]
        [SerializeField] private uint walkSeed = 1u;

        [Tooltip("Starting grid cell for the walk. Clamped to [0..resolution-1].")]
        [SerializeField] private Vector2Int walkStart = new Vector2Int(32, 32);

        [Tooltip("If true, clears the mask before carving the walk.")]
        [SerializeField] private bool walkClearBeforeDraw = true;

        [Tooltip("D2 only: total number of attempted steps in the walk.")]
        [Min(0)]
        [SerializeField] private int walkLength = 200;

        [Tooltip("D3 only: number of walk 'iterations' (multiple short walks combined).")]
        [Min(1)]
        [SerializeField] private int walkIterations = 20;

        [Tooltip("D3 only: minimum length of each walk iteration.")]
        [Min(0)]
        [SerializeField] private int walkLengthMin = 25;

        [Tooltip("D3 only: maximum length of each walk iteration.")]
        [Min(0)]
        [SerializeField] private int walkLengthMax = 100;

        [Tooltip("D3 only: chance that each iteration restarts from a random existing floor cell.")]
        [Range(0f, 1f)]
        [SerializeField] private float walkRandomStartChance = 0.25f;

        [Tooltip("Brush radius for carving. 0 = single-cell carve; >0 = disc brush per step.")]
        [Min(0)]
        [SerializeField] private int walkBrushRadius = 0;

        [Tooltip("Directional bias: positive = bias right (X) / up (Y), negative = left / down.")]
        [SerializeField] private float walkSkewX = 0f;

        [Tooltip("Directional bias: positive = bias up, negative = bias down.")]
        [SerializeField] private float walkSkewY = 0f;

        [Tooltip("Max retries per step when a move would go out-of-bounds (keeps OOB-safe).")]
        [Min(1)]
        [SerializeField] private int walkMaxRetries = 8;

        // -------------------------
        // Rooms + Corridors (Phase D5)
        // -------------------------
        [Header("Rooms + Corridors (D5)")]
        [Tooltip("Seed for Rooms+Corridors. Clamped to >= 1.")]
        [SerializeField] private int rcSeed = 1;

        [Tooltip("Target number of rooms to attempt to place.")]
        [Min(0)]
        [SerializeField] private int rcRoomCount = 12;

        [Tooltip("Minimum room size (width,height) in cells. Values are clamped to >= 1.")]
        [SerializeField] private Vector2Int rcRoomSizeMin = new Vector2Int(6, 6);

        [Tooltip("Maximum room size (width,height) in cells. Values are clamped to >= 1.")]
        [SerializeField] private Vector2Int rcRoomSizeMax = new Vector2Int(14, 14);

        [Tooltip("Placement attempts per room. Higher = denser layouts but more CPU.")]
        [Min(1)]
        [SerializeField] private int rcPlacementAttemptsPerRoom = 20;

        [Tooltip("Padding (in cells) around rooms to reduce overlap / enforce spacing.")]
        [Min(0)]
        [SerializeField] private int rcRoomPadding = 2;

        [Tooltip("Corridor brush radius used by the composer when connecting rooms.")]
        [Min(0)]
        [SerializeField] private int rcCorridorBrushRadius = 0;

        [Tooltip("If true, clears the mask before generating rooms/corridors.")]
        [SerializeField] private bool rcClearBeforeDraw = true;

        [Tooltip("If true, rooms may overlap existing filled cells (useful for high density).")]
        [SerializeField] private bool rcAllowOverlap = true;

        // -------------------------
        // Corridor First (Phase E1)
        // -------------------------
        [Header("Corridor First (E1)")]
        [Tooltip("Seed for Corridor-First. Clamped to >= 1.")]
        [SerializeField] private int cfSeed = 1;

        [Tooltip("Number of corridor segments to carve (each is a DrawLine).")]
        [Min(0)]
        [SerializeField] private int cfCorridorCount = 64;

        [Tooltip("Minimum corridor segment length (cells).")]
        [Min(1)]
        [SerializeField] private int cfCorridorLengthMin = 6;

        [Tooltip("Maximum corridor segment length (cells).")]
        [Min(1)]
        [SerializeField] private int cfCorridorLengthMax = 18;

        [Tooltip("Brush radius used when carving corridors (DrawLine). 0 = single-cell line.")]
        [Min(0)]
        [SerializeField] private int cfCorridorBrushRadius = 0;

        [Tooltip("Padding from borders. Start/endpoints are clamped to [padding..res-1-padding].")]
        [Min(0)]
        [SerializeField] private int cfBorderPadding = 1;

        [Tooltip("If true, clears the mask before generating corridors/rooms.")]
        [SerializeField] private bool cfClearBeforeDraw = true;

        [Header("Corridor First Rooms")]
        [Tooltip("If > 0: pick exactly N unique corridor endpoints (seeded shuffle) to place rooms.\nIf <= 0: use Cf Room Spawn Chance per endpoint.")]
        [SerializeField] private int cfRoomSpawnCount = 8;

        [Tooltip("If Cf Room Spawn Count <= 0: chance per endpoint to place a room.")]
        [Range(0f, 1f)]
        [SerializeField] private float cfRoomSpawnChance = 0.6f;

        [Tooltip("Minimum room size (width,height) in cells. Values are clamped to >= 1.")]
        [SerializeField] private Vector2Int cfRoomSizeMin = new Vector2Int(6, 6);

        [Tooltip("Maximum room size (width,height) in cells. Values are clamped to >= 1.")]
        [SerializeField] private Vector2Int cfRoomSizeMax = new Vector2Int(14, 14);

        [Tooltip("If true, performs a dead-end scan after endpoint rooms and stamps rooms at dead-ends.")]
        [SerializeField] private bool cfEnsureRoomsAtDeadEnds = true;

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

        // Dirty tracking (minimal)
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

        /// <summary>
        /// Sets the colors used for mask value 0 and 1.
        /// Requires shader properties: _MaskOffColor, _MaskOnColor.
        /// </summary>
        public void SetMaskColors(Color offColor, Color onColor)
        {
            maskOffColor = offColor;
            maskOnColor = onColor;
            ApplyPaletteToMpb();
        }

        protected override void EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock)
        {
            mpb = propertyBlock;

            packedNoise = new NativeArray<float4>(dataLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            noiseBuffer = new ComputeBuffer(dataLength * 4, sizeof(float));
            mpb.SetBuffer(NoiseId, noiseBuffer);

            ApplyPaletteToMpb();

            // Force full regen on first update
            lastResolution = -1;
            lastStrategy = (StrategyMode)(-1);

            walkDirty = true;
            roomsCorridorsDirty = true;
            corridorFirstDirty = true;

            CacheWalkParams();
            CacheRoomsCorridorsParams();
            CacheCorridorFirstParams();
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

            mpb = null;

            updateCalls = 0;
            loggedFirstUpdate = false;

            lastResolution = -1;
            lastStrategy = (StrategyMode)(-1);

            walkDirty = true;
            roomsCorridorsDirty = true;
            corridorFirstDirty = true;
        }

        protected override void UpdateVisualization(NativeArray<float3x4> positions, int resolution, JobHandle handle)
        {
            updateCalls++;
            handle.Complete();

            bool shouldLogThisCall =
                enableLogs &&
                (!loggedFirstUpdate || (logEveryNUpdates > 0 && (updateCalls % logEveryNUpdates == 0)));

            if (resolution != lastResolution)
            {
                lastResolution = resolution;
                walkDirty = true;
                roomsCorridorsDirty = true;
                corridorFirstDirty = true;
            }

            if (strategy != lastStrategy)
            {
                lastStrategy = strategy;
                walkDirty = true;
                roomsCorridorsDirty = true;
                corridorFirstDirty = true;
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
                            walkBrushRadius,
                            walkSkewX,
                            walkSkewY,
                            walkMaxRetries);

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

                        int iters = math.max(1, walkIterations);
                        int lenMin = math.max(0, walkLengthMin);
                        int lenMax = math.max(lenMin, walkLengthMax);

                        IteratedRandomWalk2D.Carve(
                            ref mask,
                            ref rng,
                            new int2(sx, sy),
                            iters,
                            lenMin,
                            lenMax,
                            walkBrushRadius,
                            walkRandomStartChance,
                            walkSkewX,
                            walkSkewY,
                            walkMaxRetries);

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

                        var rng = LayoutSeedUtil.CreateRng(cfSeed);

                        int endpointsCap = math.max(1, cfg.corridorCount + 1);
                        using var endpoints = new NativeArray<int2>(endpointsCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        // Only used to record room centers; stamping happens regardless of capacity.
                        int centersCap = math.max(32, endpointsCap * 2);
                        using var centers = new NativeArray<int2>(centersCap, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

                        CorridorFirstDungeon2D.Generate(ref mask, ref rng, in cfg, endpoints, centers, out int placedRooms);
                        lastPlacedCfRooms = placedRooms;

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        corridorFirstDirty = false;
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

            noiseBuffer.SetData(packedNoise);
        }

        private float MaskInstanceValue(int instanceIndex, int resolution)
        {
            int x = instanceIndex % resolution;
            int y = instanceIndex / resolution;
            return mask.GetUnchecked(x, y) ? 1f : 0f;
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

        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyPaletteToMpb();
            if (enabled) transform.hasChanged = true;
        }
    }
}
