using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Generators;
using Islands.PCG.Operators;
using Islands.PCG.Layout;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Debug visualization for a binary PCG mask (MaskGrid2D), uploaded as a float buffer "_Noise".
    /// Reuses the existing Islands GPU instancing path (NoiseGPU.hlsl):
    /// _Positions[unity_InstanceID], _Normals[unity_InstanceID], _Noise[unity_InstanceID].
    ///
    /// Phase B: ScalarField2D -> Threshold -> MaskGrid2D -> pack/upload.
    /// Phase C: SDF (signed distance) rasterized into ScalarField2D via SdfToScalarOps -> Threshold -> filled mask.
    /// Phase C (C4): SDF compose (circle+box) rasterized via SdfComposeRasterOps -> Threshold -> filled mask.
    /// Phase C (C5): Mask boolean ops (union/intersect/subtract) in mask-space using word-wise ops on MaskGrid2D.
    ///
    /// Phase D (D2): SimpleRandomWalk2D carver writes directly into MaskGrid2D (no Tilemaps).
    /// Phase D (D3): IteratedRandomWalk2D strategy (multiple walks + optional restart on existing floor).
    ///
    /// Phase D (D4): Raster debug modes (disc + line) writing directly into MaskGrid2D via MaskRasterOps2D.
    ///
    /// Optional GPU threshold preview is kept, but only supports Greater/GreaterEqual (shader uses _ThresholdGE).
    /// </summary>
    public sealed class PCGMaskVisualization : Visualization
    {
        private static readonly int NoiseId = Shader.PropertyToID("_Noise");
        private static readonly int MaskOffColorId = Shader.PropertyToID("_MaskOffColor");
        private static readonly int MaskOnColorId = Shader.PropertyToID("_MaskOnColor");

        // Shader uniforms for GPU preview (existing shader supports only > or >=)
        private static readonly int UseThresholdPreviewId = Shader.PropertyToID("_UseThresholdPreview"); // 0/1
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");                   // float
        private static readonly int ThresholdGEId = Shader.PropertyToID("_ThresholdGE");               // 0/1

        private enum SourceMode
        {
            RectMask = 0,
            CheckerMask = 1,
            ThresholdedScalar = 2,

            // SDF path (C3): rasterize SDF -> ScalarField2D -> Threshold -> mask
            SdfCircleMask = 3,
            SdfBoxMask = 4,
            SdfCapsuleMask = 5,

            // SDF compose path (C4): compose two SDFs in distance-space -> Threshold -> mask
            SdfCircleBoxCompositeMask = 6,

            // C5: combine TWO masks in mask-space (fast word-wise ops)
            MaskUnion = 7,
            MaskIntersect = 8,
            MaskSubtract = 9,

            // Phase D (D2): pure-grid simple random walk carver (MaskGrid2D only)
            SimpleRandomWalkMask = 10,

            // Phase D (D3): iterated random walk strategy (multiple walks, optional restart)
            IteratedRandomWalkMask = 11,

            // Phase D (D4.3): raster debug modes
            RasterDiscMask = 12,
            RasterLineMask = 13,
        }

        [Header("Mask Source")]
        [SerializeField] private SourceMode sourceMode = SourceMode.CheckerMask;

        [Tooltip("If true, clears the mask before drawing (RectMask only).")]
        [SerializeField] private bool clearBeforeDraw = true;

        [Header("RectMask Settings")]
        [SerializeField] private Vector2Int rectMin = new Vector2Int(2, 2);
        [SerializeField] private Vector2Int rectMax = new Vector2Int(20, 20);
        [SerializeField] private bool rectValue = true;

        [Header("CheckerMask Settings")]
        [Min(1)]
        [SerializeField] private int checkerCellSize = 2;
        [SerializeField] private int checkerXOffset = 0;
        [SerializeField] private int checkerYOffset = 0;
        [SerializeField] private bool checkerInvert = false;

        [Header("Scalar / Threshold Settings")]
        [Tooltip("Threshold value used by ThresholdedScalar and SDF modes. For SDF fill, typically 0.")]
        [SerializeField] private float threshold = 0.5f;

        [Tooltip("Comparison direction for scalar -> mask.\n" +
                 "For SDF fill, typical is: threshold=0 and mode=LessEqual (inside = distance <= 0).")]
        [SerializeField] private ThresholdMode thresholdMode = ThresholdMode.GreaterEqual;

        [Tooltip("Center of the scalar/SDF circle in normalized UV space [0..1]. Used by ThresholdedScalar + SdfCircleMask + Composite + C5 mask ops.")]
        [SerializeField] private Vector2 scalarCenter01 = new Vector2(0.5f, 0.5f);

        [Tooltip("Radius of the SDF circle in normalized units [0..1]. Used by ThresholdedScalar + SdfCircleMask + Composite + C5 mask ops.")]
        [Range(0.001f, 1f)]
        [SerializeField] private float scalarRadius01 = 0.35f;

        [Tooltip("Negates scalar values.\n- ThresholdedScalar: invert radial gradient.\n- SDF modes: negate signed distance (swap inside/outside).")]
        [SerializeField] private bool invertScalar = false;

        [Header("SDF Box Settings")]
        [Tooltip("Half extents of the SDF box in normalized units [0..1]. Used by SdfBoxMask + Composite + C5 mask ops.")]
        [SerializeField] private Vector2 boxHalfExtents01 = new Vector2(0.25f, 0.15f);

        [Tooltip("Center of the SDF box in normalized UV space [0..1]. Used by SdfBoxMask + Composite + C5 mask ops.")]
        [SerializeField] private Vector2 boxCenter01 = new Vector2(0.5f, 0.5f);

        [Header("SDF Capsule Settings")]
        [Tooltip("Capsule segment start (normalized [0..1]).")]
        [SerializeField] private Vector2 capsuleA01 = new Vector2(0.25f, 0.5f);

        [Tooltip("Capsule segment end (normalized [0..1]).")]
        [SerializeField] private Vector2 capsuleB01 = new Vector2(0.75f, 0.5f);

        [Tooltip("Capsule radius (normalized [0..1]).")]
        [Range(0.001f, 1f)]
        [SerializeField] private float capsuleRadius01 = 0.10f;

        [Header("SDF Compose Settings (C4)")]
        [Tooltip("How to combine circle + box distances in composite mode (union / intersect / subtract).")]
        [SerializeField] private SdfCombineMode composeMode = SdfCombineMode.Union;

        [Header("Simple / Iterated Random Walk (Phase D)")]
        [Tooltip("Seed for Unity.Mathematics.Random (must be non-zero).")]
        [SerializeField] private uint walkSeed = 1u;

        [Tooltip("Start cell in grid coordinates (clamped to [0..resolution-1]).")]
        [SerializeField] private Vector2Int walkStart = new Vector2Int(32, 32);

        // D2 parameter (single walk length)
        [Min(0)]
        [SerializeField] private int walkLength = 200;

        // D3 parameters (iterated strategy)
        [Min(1)]
        [SerializeField] private int walkIterations = 20;

        [Min(0)]
        [SerializeField] private int walkLengthMin = 25;

        [Min(0)]
        [SerializeField] private int walkLengthMax = 100;

        [Range(0f, 1f)]
        [SerializeField] private float walkRandomStartChance = 0.25f;

        [Min(0)]
        [SerializeField] private int walkBrushRadius = 0;

        [Tooltip("Direction bias. Positive skewX biases right, negative biases left. Positive skewY biases up, negative biases down.")]
        [SerializeField] private float walkSkewX = 0f;

        [SerializeField] private float walkSkewY = 0f;

        [Min(1)]
        [SerializeField] private int walkMaxRetries = 8;

        [Tooltip("If true, clears the mask before carving the walk.")]
        [SerializeField] private bool walkClearBeforeDraw = true;

        [Header("Raster Shapes (Phase D4)")]
        [Tooltip("Disc center in grid coordinates (clamped to [0..resolution-1]).")]
        [SerializeField] private Vector2Int discCenter = new Vector2Int(32, 32);

        [Min(0)]
        [SerializeField] private int discRadius = 8;

        [SerializeField] private bool discValue = true;

        [Tooltip("Line start in grid coordinates (clamped to [0..resolution-1]).")]
        [SerializeField] private Vector2Int lineA = new Vector2Int(8, 32);

        [Tooltip("Line end in grid coordinates (clamped to [0..resolution-1]).")]
        [SerializeField] private Vector2Int lineB = new Vector2Int(56, 32);

        [Min(0)]
        [SerializeField] private int lineBrushRadius = 0;

        [SerializeField] private bool lineValue = true;

        [Header("GPU Preview (Recommended for ThresholdedScalar)")]
        [Tooltip("If ON (and SourceMode=ThresholdedScalar, mode is Greater/GreaterEqual), uploads scalar to _Noise once and applies threshold in the shader.\n" +
                 "Dragging Threshold becomes very cheap (no CPU loops, no buffer upload).")]
        [SerializeField] private bool previewThresholdInShader = true;

        [Header("Default Mask Colors (0/1)")]
        [SerializeField] private Color maskOffColor = Color.black;
        [SerializeField] private Color maskOnColor = Color.white;

        [Header("Debug Logs")]
        [SerializeField] private bool enableLogs = false;

        [Tooltip("Log on every N UpdateVisualization calls. 0 = log only first call.")]
        [SerializeField] private int logEveryNUpdates = 0;

        private NativeArray<float4> packedNoise; // packed scalar or packed mask
        private ComputeBuffer noiseBuffer;

        private MaskGrid2D mask;
        private int maskResolution = -1;

        // C5: extra masks for boolean ops
        private MaskGrid2D maskA;
        private MaskGrid2D maskB;
        private int auxMaskResolution = -1;

        private ScalarField2D scalar;
        private int scalarResolution = -1;

        private int updateCalls = 0;
        private bool loggedFirstUpdate = false;

        private MaterialPropertyBlock mpb;

        // cached params to avoid recompute/upload
        private int lastResolution = -1;
        private SourceMode lastSourceMode = (SourceMode)(-1);

        private Vector2 lastCircleCenter01;
        private float lastCircleRadius01;

        private Vector2 lastBoxHalfExtents01;
        private Vector2 lastBoxCenter01;

        private SdfCombineMode lastComposeMode;

        private Vector2 lastCapsuleA01;
        private Vector2 lastCapsuleB01;
        private float lastCapsuleRadius01;

        private bool lastInvertScalar;
        private bool scalarDirty = true;

        private float lastThreshold = float.NaN;
        private ThresholdMode lastThresholdMode;
        private bool thresholdDirty = true;

        // Phase D: random walk dirty tracking
        private uint lastWalkSeed;
        private Vector2Int lastWalkStart;
        private int lastWalkLength;

        // D3 caches
        private int lastWalkIterations;
        private int lastWalkLengthMin;
        private int lastWalkLengthMax;
        private float lastWalkRandomStartChance;

        private int lastWalkBrushRadius;
        private float lastWalkSkewX;
        private float lastWalkSkewY;
        private int lastWalkMaxRetries;
        private bool lastWalkClearBeforeDraw;
        private bool walkDirty = true;

        // Phase D4: raster dirty tracking
        private Vector2Int lastDiscCenter;
        private int lastDiscRadius;
        private bool lastDiscValue;

        private Vector2Int lastLineA;
        private Vector2Int lastLineB;
        private int lastLineBrushRadius;
        private bool lastLineValue;

        private bool rasterDirty = true;

        /// <summary>
        /// Runtime API used by PCGMaskPaletteController: sets colors for mask value 0 and 1.
        /// Requires shader properties: _MaskOffColor, _MaskOnColor.
        /// </summary>
        public void SetMaskColors(Color offColor, Color onColor)
        {
            maskOffColor = offColor;
            maskOnColor = onColor;
            ApplyPaletteToMpb();
        }

        private void ApplyPaletteToMpb()
        {
            if (mpb == null) return;
            mpb.SetColor(MaskOffColorId, maskOffColor);
            mpb.SetColor(MaskOnColorId, maskOnColor);
        }

        protected override void EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock)
        {
            mpb = propertyBlock;

            packedNoise = new NativeArray<float4>(dataLength, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            noiseBuffer = new ComputeBuffer(dataLength * 4, sizeof(float));
            mpb.SetBuffer(NoiseId, noiseBuffer);

            // Palette + defaults
            ApplyPaletteToMpb();
            mpb.SetFloat(UseThresholdPreviewId, 0f);
            mpb.SetFloat(ThresholdId, threshold);
            mpb.SetFloat(ThresholdGEId, thresholdMode == ThresholdMode.GreaterEqual ? 1f : 0f);

            // Force first update to generate everything
            lastResolution = -1;
            lastSourceMode = (SourceMode)(-1);
            scalarDirty = true;
            thresholdDirty = true;
            walkDirty = true;
            rasterDirty = true;

            lastThresholdMode = thresholdMode;
            lastComposeMode = composeMode;

            lastCircleCenter01 = scalarCenter01;
            lastCircleRadius01 = scalarRadius01;

            lastBoxHalfExtents01 = boxHalfExtents01;
            lastBoxCenter01 = boxCenter01;

            lastCapsuleA01 = capsuleA01;
            lastCapsuleB01 = capsuleB01;
            lastCapsuleRadius01 = capsuleRadius01;

            lastInvertScalar = invertScalar;

            // Phase D cache
            lastWalkSeed = walkSeed;
            lastWalkStart = walkStart;
            lastWalkLength = walkLength;

            lastWalkIterations = walkIterations;
            lastWalkLengthMin = walkLengthMin;
            lastWalkLengthMax = walkLengthMax;
            lastWalkRandomStartChance = walkRandomStartChance;

            lastWalkBrushRadius = walkBrushRadius;
            lastWalkSkewX = walkSkewX;
            lastWalkSkewY = walkSkewY;
            lastWalkMaxRetries = walkMaxRetries;
            lastWalkClearBeforeDraw = walkClearBeforeDraw;

            // Phase D4 cache
            lastDiscCenter = discCenter;
            lastDiscRadius = discRadius;
            lastDiscValue = discValue;

            lastLineA = lineA;
            lastLineB = lineB;
            lastLineBrushRadius = lineBrushRadius;
            lastLineValue = lineValue;
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

            if (maskA.IsCreated) maskA.Dispose();
            if (maskB.IsCreated) maskB.Dispose();
            auxMaskResolution = -1;

            if (scalar.IsCreated) scalar.Dispose();
            scalarResolution = -1;

            mpb = null;

            updateCalls = 0;
            loggedFirstUpdate = false;

            lastResolution = -1;
            lastSourceMode = (SourceMode)(-1);
            scalarDirty = true;
            thresholdDirty = true;
            walkDirty = true;
            rasterDirty = true;
        }

        protected override void UpdateVisualization(NativeArray<float3x4> positions, int resolution, JobHandle handle)
        {
            updateCalls++;

            bool shouldLogThisCall =
                enableLogs &&
                (!loggedFirstUpdate || (logEveryNUpdates > 0 && (updateCalls % logEveryNUpdates == 0)));

            handle.Complete();

            // Detect mode/resolution changes
            if (resolution != lastResolution)
            {
                lastResolution = resolution;
                scalarDirty = true;
                thresholdDirty = true;
                walkDirty = true;
                rasterDirty = true;
            }

            if (sourceMode != lastSourceMode)
            {
                lastSourceMode = sourceMode;
                scalarDirty = true;
                thresholdDirty = true;
                walkDirty = true;
                rasterDirty = true;
            }

            // Threshold changes affect ALL scalar-based modes
            if (!Mathf.Approximately(threshold, lastThreshold) || thresholdMode != lastThresholdMode)
            {
                lastThreshold = threshold;
                lastThresholdMode = thresholdMode;
                thresholdDirty = true;
            }

            // Inversion affects all scalar-based modes too
            if (invertScalar != lastInvertScalar)
            {
                lastInvertScalar = invertScalar;
                scalarDirty = true;
            }

            // -------------------------
            // Dirty tracking (by mode)
            // -------------------------

            // Circle params are used by: ThresholdedScalar, SdfCircleMask, Composite, Mask* modes
            bool usesCircle =
                sourceMode == SourceMode.ThresholdedScalar ||
                sourceMode == SourceMode.SdfCircleMask ||
                sourceMode == SourceMode.SdfCircleBoxCompositeMask ||
                sourceMode == SourceMode.MaskUnion ||
                sourceMode == SourceMode.MaskIntersect ||
                sourceMode == SourceMode.MaskSubtract;

            if (usesCircle)
            {
                if (scalarCenter01 != lastCircleCenter01)
                {
                    lastCircleCenter01 = scalarCenter01;
                    scalarDirty = true;
                }

                if (!Mathf.Approximately(scalarRadius01, lastCircleRadius01))
                {
                    lastCircleRadius01 = scalarRadius01;
                    scalarDirty = true;
                }
            }

            // Box params are used by: SdfBoxMask, Composite, Mask* modes
            bool usesBox =
                sourceMode == SourceMode.SdfBoxMask ||
                sourceMode == SourceMode.SdfCircleBoxCompositeMask ||
                sourceMode == SourceMode.MaskUnion ||
                sourceMode == SourceMode.MaskIntersect ||
                sourceMode == SourceMode.MaskSubtract;

            if (usesBox)
            {
                if (boxHalfExtents01 != lastBoxHalfExtents01)
                {
                    lastBoxHalfExtents01 = boxHalfExtents01;
                    scalarDirty = true;
                }

                if (boxCenter01 != lastBoxCenter01)
                {
                    lastBoxCenter01 = boxCenter01;
                    scalarDirty = true;
                }
            }

            if (sourceMode == SourceMode.SdfCircleBoxCompositeMask)
            {
                if (composeMode != lastComposeMode)
                {
                    lastComposeMode = composeMode;
                    scalarDirty = true;
                }
            }

            if (sourceMode == SourceMode.SdfCapsuleMask)
            {
                if (capsuleA01 != lastCapsuleA01 ||
                    capsuleB01 != lastCapsuleB01 ||
                    !Mathf.Approximately(capsuleRadius01, lastCapsuleRadius01))
                {
                    lastCapsuleA01 = capsuleA01;
                    lastCapsuleB01 = capsuleB01;
                    lastCapsuleRadius01 = capsuleRadius01;
                    scalarDirty = true;
                }
            }

            // Phase D params (D2 + D3)
            bool usesWalk =
                sourceMode == SourceMode.SimpleRandomWalkMask ||
                sourceMode == SourceMode.IteratedRandomWalkMask;

            if (usesWalk)
            {
                if (walkSeed != lastWalkSeed ||
                    walkStart != lastWalkStart ||
                    walkLength != lastWalkLength ||
                    walkIterations != lastWalkIterations ||
                    walkLengthMin != lastWalkLengthMin ||
                    walkLengthMax != lastWalkLengthMax ||
                    !Mathf.Approximately(walkRandomStartChance, lastWalkRandomStartChance) ||
                    walkBrushRadius != lastWalkBrushRadius ||
                    !Mathf.Approximately(walkSkewX, lastWalkSkewX) ||
                    !Mathf.Approximately(walkSkewY, lastWalkSkewY) ||
                    walkMaxRetries != lastWalkMaxRetries ||
                    walkClearBeforeDraw != lastWalkClearBeforeDraw)
                {
                    lastWalkSeed = walkSeed;
                    lastWalkStart = walkStart;
                    lastWalkLength = walkLength;

                    lastWalkIterations = walkIterations;
                    lastWalkLengthMin = walkLengthMin;
                    lastWalkLengthMax = walkLengthMax;
                    lastWalkRandomStartChance = walkRandomStartChance;

                    lastWalkBrushRadius = walkBrushRadius;
                    lastWalkSkewX = walkSkewX;
                    lastWalkSkewY = walkSkewY;
                    lastWalkMaxRetries = walkMaxRetries;
                    lastWalkClearBeforeDraw = walkClearBeforeDraw;
                    walkDirty = true;
                }
            }

            // Phase D4 params (raster)
            bool usesDisc = sourceMode == SourceMode.RasterDiscMask;
            if (usesDisc)
            {
                if (discCenter != lastDiscCenter ||
                    discRadius != lastDiscRadius ||
                    discValue != lastDiscValue)
                {
                    lastDiscCenter = discCenter;
                    lastDiscRadius = discRadius;
                    lastDiscValue = discValue;
                    rasterDirty = true;
                }
            }

            bool usesLine = sourceMode == SourceMode.RasterLineMask;
            if (usesLine)
            {
                if (lineA != lastLineA ||
                    lineB != lastLineB ||
                    lineBrushRadius != lastLineBrushRadius ||
                    lineValue != lastLineValue)
                {
                    lastLineA = lineA;
                    lastLineB = lineB;
                    lastLineBrushRadius = lineBrushRadius;
                    lastLineValue = lineValue;
                    rasterDirty = true;
                }
            }

            EnsureMaskAllocated(resolution);

            // Default: shader preview OFF unless explicitly enabled
            if (mpb != null) mpb.SetFloat(UseThresholdPreviewId, 0f);

            bool uploadedThisFrame = false;

            switch (sourceMode)
            {
                case SourceMode.RectMask:
                    {
                        if (clearBeforeDraw) mask.Clear();
                        RectFillGenerator.FillRect(
                            ref mask,
                            rectMin.x, rectMin.y,
                            rectMax.x, rectMax.y,
                            rectValue,
                            clampToDomain: true);

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        break;
                    }

                case SourceMode.CheckerMask:
                    {
                        CheckerFillGenerator.ClearAndFillCheckerboard(
                            ref mask,
                            cellSize: math.max(1, checkerCellSize),
                            xOffset: checkerXOffset,
                            yOffset: checkerYOffset,
                            invert: checkerInvert);

                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        break;
                    }

                case SourceMode.ThresholdedScalar:
                    {
                        EnsureScalarAllocated(resolution);

                        if (scalarDirty)
                        {
                            FillScalarRadialPattern(resolution);
                            scalarDirty = false;

                            if (CanUseGpuThresholdPreview())
                            {
                                PackFromScalarAndUpload(resolution);
                                uploadedThisFrame = true;
                            }
                            else
                            {
                                thresholdDirty = true;
                            }
                        }

                        if (CanUseGpuThresholdPreview())
                        {
                            if (mpb != null)
                            {
                                mpb.SetFloat(UseThresholdPreviewId, 1f);
                                mpb.SetFloat(ThresholdId, threshold);
                                mpb.SetFloat(ThresholdGEId, thresholdMode == ThresholdMode.GreaterEqual ? 1f : 0f);
                            }

                            thresholdDirty = false;
                        }
                        else
                        {
                            if (thresholdDirty)
                            {
                                ScalarToMaskOps.Threshold(in scalar, ref mask, threshold, thresholdMode);
                                PackFromMaskAndUpload(resolution);
                                uploadedThisFrame = true;
                                thresholdDirty = false;
                            }
                        }

                        break;
                    }

                case SourceMode.SdfCircleMask:
                    {
                        EnsureScalarAllocated(resolution);

                        if (scalarDirty)
                        {
                            BuildScalarSdfCircleInGridUnits(resolution);
                            scalarDirty = false;
                            thresholdDirty = true;
                        }

                        if (thresholdDirty)
                        {
                            ScalarToMaskOps.Threshold(in scalar, ref mask, threshold, thresholdMode);
                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;
                            thresholdDirty = false;
                        }

                        break;
                    }

                case SourceMode.SdfBoxMask:
                    {
                        EnsureScalarAllocated(resolution);

                        if (scalarDirty)
                        {
                            BuildScalarSdfBoxInGridUnits(resolution);
                            scalarDirty = false;
                            thresholdDirty = true;
                        }

                        if (thresholdDirty)
                        {
                            ScalarToMaskOps.Threshold(in scalar, ref mask, threshold, thresholdMode);
                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;
                            thresholdDirty = false;
                        }

                        break;
                    }

                case SourceMode.SdfCapsuleMask:
                    {
                        EnsureScalarAllocated(resolution);

                        if (scalarDirty)
                        {
                            BuildScalarSdfCapsuleInGridUnits(resolution);
                            scalarDirty = false;
                            thresholdDirty = true;
                        }

                        if (thresholdDirty)
                        {
                            ScalarToMaskOps.Threshold(in scalar, ref mask, threshold, thresholdMode);
                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;
                            thresholdDirty = false;
                        }

                        break;
                    }

                case SourceMode.SdfCircleBoxCompositeMask:
                    {
                        EnsureScalarAllocated(resolution);

                        if (scalarDirty)
                        {
                            BuildScalarSdfCircleBoxCompositeInGridUnits(resolution);
                            scalarDirty = false;
                            thresholdDirty = true;
                        }

                        if (thresholdDirty)
                        {
                            ScalarToMaskOps.Threshold(in scalar, ref mask, threshold, thresholdMode);
                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;
                            thresholdDirty = false;
                        }

                        break;
                    }

                // ------------------------
                // C5: mask boolean ops
                // ------------------------
                case SourceMode.MaskUnion:
                case SourceMode.MaskIntersect:
                case SourceMode.MaskSubtract:
                    {
                        EnsureScalarAllocated(resolution);
                        EnsureAuxMasksAllocated(resolution);

                        if (scalarDirty || thresholdDirty)
                        {
                            // maskA = circle (SDF -> scalar -> threshold)
                            BuildScalarSdfCircleInGridUnits(resolution);
                            ScalarToMaskOps.Threshold(in scalar, ref maskA, threshold, thresholdMode);

                            // maskB = box (SDF -> scalar -> threshold) using boxCenter01
                            BuildScalarSdfBoxInGridUnits(resolution);
                            ScalarToMaskOps.Threshold(in scalar, ref maskB, threshold, thresholdMode);

                            // Combine result in mask (word-wise ops on MaskGrid2D)
                            mask.CopyFrom(maskA);

                            if (sourceMode == SourceMode.MaskUnion) mask.Or(maskB);
                            else if (sourceMode == SourceMode.MaskIntersect) mask.And(maskB);
                            else mask.AndNot(maskB);

                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;

                            scalarDirty = false;
                            thresholdDirty = false;
                        }

                        break;
                    }

                // ------------------------
                // Phase D (D2): Simple Random Walk
                // ------------------------
                case SourceMode.SimpleRandomWalkMask:
                    {
                        if (walkDirty)
                        {
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
                        }

                        break;
                    }

                // ------------------------
                // Phase D (D3): Iterated Random Walk Strategy
                // ------------------------
                case SourceMode.IteratedRandomWalkMask:
                    {
                        if (walkDirty)
                        {
                            if (walkClearBeforeDraw) mask.Clear();

                            uint seed = (walkSeed == 0u) ? 1u : walkSeed;
                            var rng = new Unity.Mathematics.Random(seed);

                            int sx = Mathf.Clamp(walkStart.x, 0, resolution - 1);
                            int sy = Mathf.Clamp(walkStart.y, 0, resolution - 1);

                            // Normalize to avoid exceptions from invalid inspector combos.
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
                        }

                        break;
                    }

                // ------------------------
                // Phase D (D4.3): Raster Disc
                // ------------------------
                case SourceMode.RasterDiscMask:
                    {
                        if (rasterDirty)
                        {
                            mask.Clear();

                            int cx = Mathf.Clamp(discCenter.x, 0, resolution - 1);
                            int cy = Mathf.Clamp(discCenter.y, 0, resolution - 1);
                            int r = math.max(0, discRadius);

                            MaskRasterOps2D.StampDisc(ref mask, cx, cy, r, discValue);

                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;
                            rasterDirty = false;
                        }

                        break;
                    }

                // ------------------------
                // Phase D (D4.3): Raster Line
                // ------------------------
                case SourceMode.RasterLineMask:
                    {
                        if (rasterDirty)
                        {
                            mask.Clear();

                            int2 a = new int2(
                                Mathf.Clamp(lineA.x, 0, resolution - 1),
                                Mathf.Clamp(lineA.y, 0, resolution - 1));

                            int2 b = new int2(
                                Mathf.Clamp(lineB.x, 0, resolution - 1),
                                Mathf.Clamp(lineB.y, 0, resolution - 1));

                            int brush = math.max(0, lineBrushRadius);

                            MaskRasterOps2D.DrawLine(ref mask, a, b, brush, lineValue);

                            PackFromMaskAndUpload(resolution);
                            uploadedThisFrame = true;
                            rasterDirty = false;
                        }

                        break;
                    }

                default:
                    {
                        CheckerFillGenerator.ClearAndFillCheckerboard(ref mask, cellSize: 2);
                        PackFromMaskAndUpload(resolution);
                        uploadedThisFrame = true;
                        break;
                    }
            }

            if (shouldLogThisCall)
            {
                loggedFirstUpdate = true;

                int ones = -1;
                if (sourceMode == SourceMode.SimpleRandomWalkMask ||
                    sourceMode == SourceMode.IteratedRandomWalkMask ||
                    sourceMode == SourceMode.RasterDiscMask ||
                    sourceMode == SourceMode.RasterLineMask)
                {
                    ones = mask.CountOnes();
                }

                Debug.Log(
                    $"[PCGMaskVisualization] Update #{updateCalls} mode={sourceMode} res={resolution} uploaded={uploadedThisFrame} " +
                    $"previewGPU={CanUseGpuThresholdPreview()} threshold={threshold} mode={thresholdMode} ones={ones}"
                );
            }
        }

        private bool CanUseGpuThresholdPreview()
        {
            if (!previewThresholdInShader) return false;
            if (sourceMode != SourceMode.ThresholdedScalar) return false;
            return thresholdMode == ThresholdMode.Greater || thresholdMode == ThresholdMode.GreaterEqual;
        }

        private void EnsureMaskAllocated(int resolution)
        {
            if (mask.IsCreated && maskResolution == resolution) return;

            if (mask.IsCreated) mask.Dispose();

            maskResolution = resolution;
            var domain = new GridDomain2D(resolution, resolution);
            mask = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
        }

        private void EnsureAuxMasksAllocated(int resolution)
        {
            if (maskA.IsCreated && maskB.IsCreated && auxMaskResolution == resolution) return;

            if (maskA.IsCreated) maskA.Dispose();
            if (maskB.IsCreated) maskB.Dispose();

            auxMaskResolution = resolution;
            var domain = new GridDomain2D(resolution, resolution);
            maskA = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
            maskB = new MaskGrid2D(domain, Allocator.Persistent, clearToZero: true);
        }

        private void EnsureScalarAllocated(int resolution)
        {
            if (scalar.IsCreated && scalarResolution == resolution) return;

            if (scalar.IsCreated) scalar.Dispose();

            scalarResolution = resolution;
            var domain = new GridDomain2D(resolution, resolution);
            scalar = new ScalarField2D(domain, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void FillScalarRadialPattern(int resolution)
        {
            float2 center = new float2(
                math.clamp(scalarCenter01.x, 0f, 1f),
                math.clamp(scalarCenter01.y, 0f, 1f));

            float radius = math.max(1e-6f, scalarRadius01);

            for (int y = 0; y < resolution; y++)
            {
                float v = (y + 0.5f) / resolution;
                for (int x = 0; x < resolution; x++)
                {
                    float u = (x + 0.5f) / resolution;

                    float2 uv = new float2(u, v);
                    float dist = math.distance(uv, center);

                    float value = 1f - math.saturate(dist / radius);
                    if (invertScalar) value = 1f - value;

                    scalar.SetUnchecked(x, y, value);
                }
            }
        }

        private void BuildScalarSdfCircleInGridUnits(int resolution)
        {
            float2 centerGU = new float2(
                math.clamp(scalarCenter01.x, 0f, 1f) * resolution,
                math.clamp(scalarCenter01.y, 0f, 1f) * resolution);

            float radiusGU = math.max(1e-6f, scalarRadius01 * resolution);

            SdfToScalarOps.WriteCircleSdf(ref scalar, centerGU, radiusGU);

            if (invertScalar) NegateScalarInPlace(resolution);
        }

        /// <summary>
        /// Box SDF rasterization using boxCenter01 + boxHalfExtents01 (C6 requirement).
        /// </summary>
        private void BuildScalarSdfBoxInGridUnits(int resolution)
        {
            float2 centerGU = new float2(
                math.clamp(boxCenter01.x, 0f, 1f) * resolution,
                math.clamp(boxCenter01.y, 0f, 1f) * resolution);

            float2 halfExtentsGU = new float2(
                math.max(1e-6f, boxHalfExtents01.x * resolution),
                math.max(1e-6f, boxHalfExtents01.y * resolution));

            SdfToScalarOps.WriteBoxSdf(ref scalar, centerGU, halfExtentsGU);

            if (invertScalar) NegateScalarInPlace(resolution);
        }

        private void BuildScalarSdfCapsuleInGridUnits(int resolution)
        {
            float2 aGU = new float2(
                math.clamp(capsuleA01.x, 0f, 1f) * resolution,
                math.clamp(capsuleA01.y, 0f, 1f) * resolution);

            float2 bGU = new float2(
                math.clamp(capsuleB01.x, 0f, 1f) * resolution,
                math.clamp(capsuleB01.y, 0f, 1f) * resolution);

            float radiusGU = math.max(1e-6f, capsuleRadius01 * resolution);

            SdfToScalarOps.WriteCapsuleSdf(ref scalar, aGU, bGU, radiusGU);

            if (invertScalar) NegateScalarInPlace(resolution);
        }

        private void BuildScalarSdfCircleBoxCompositeInGridUnits(int resolution)
        {
            float2 circleCenterGU = new float2(
                math.clamp(scalarCenter01.x, 0f, 1f) * resolution,
                math.clamp(scalarCenter01.y, 0f, 1f) * resolution);

            float circleRadiusGU = math.max(1e-6f, scalarRadius01 * resolution);

            float2 boxCenterGU = new float2(
                math.clamp(boxCenter01.x, 0f, 1f) * resolution,
                math.clamp(boxCenter01.y, 0f, 1f) * resolution);

            float2 boxHalfExtentsGU = new float2(
                math.max(1e-6f, boxHalfExtents01.x * resolution),
                math.max(1e-6f, boxHalfExtents01.y * resolution));

            SdfComposeRasterOps.WriteCircleBoxCompositeSdf(
                ref scalar,
                circleCenterGU,
                circleRadiusGU,
                boxCenterGU,
                boxHalfExtentsGU,
                composeMode,
                invertDistance: invertScalar
            );
        }

        private void NegateScalarInPlace(int resolution)
        {
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                    scalar.SetUnchecked(x, y, -scalar.GetUnchecked(x, y));
        }

        private void PackFromScalarAndUpload(int resolution)
        {
            int totalInstances = resolution * resolution;
            int packs = packedNoise.Length;

            for (int packIndex = 0; packIndex < packs; packIndex++)
            {
                int baseInstance = packIndex * 4;

                float v0 = (baseInstance + 0 < totalInstances) ? ScalarInstanceValue(baseInstance + 0, resolution) : 0f;
                float v1 = (baseInstance + 1 < totalInstances) ? ScalarInstanceValue(baseInstance + 1, resolution) : 0f;
                float v2 = (baseInstance + 2 < totalInstances) ? ScalarInstanceValue(baseInstance + 2, resolution) : 0f;
                float v3 = (baseInstance + 3 < totalInstances) ? ScalarInstanceValue(baseInstance + 3, resolution) : 0f;

                packedNoise[packIndex] = new float4(v0, v1, v2, v3);
            }

            noiseBuffer.SetData(packedNoise);
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

        private float ScalarInstanceValue(int instanceIndex, int resolution)
        {
            int x = instanceIndex % resolution;
            int y = instanceIndex / resolution;
            return scalar.GetUnchecked(x, y);
        }

        private float MaskInstanceValue(int instanceIndex, int resolution)
        {
            int x = instanceIndex % resolution;
            int y = instanceIndex / resolution;
            return mask.GetUnchecked(x, y) ? 1f : 0f;
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyPaletteToMpb();
            if (enabled) transform.hasChanged = true;
        }
    }
}
