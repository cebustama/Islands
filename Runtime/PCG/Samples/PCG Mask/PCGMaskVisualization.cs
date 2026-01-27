using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using Islands;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Generators;
using Islands.PCG.Operators;

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
    /// C6: Final demo wiring: one scene can flip modes and verify:
    /// - primitives look correct
    /// - mask boolean ops work
    /// - SDF-composed result matches mask-composed result (at least union/intersect)
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

            lastThresholdMode = thresholdMode;
            lastComposeMode = composeMode;

            lastCircleCenter01 = scalarCenter01;
            lastCircleRadius01 = scalarRadius01;

            lastBoxHalfExtents01 = boxHalfExtents01;
            lastBoxCenter01 = boxCenter01;
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
            }

            if (sourceMode != lastSourceMode)
            {
                lastSourceMode = sourceMode;
                scalarDirty = true;
                thresholdDirty = true;
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
                Debug.Log(
                    $"[PCGMaskVisualization] Update #{updateCalls} mode={sourceMode} res={resolution} uploaded={uploadedThisFrame} " +
                    $"previewGPU={CanUseGpuThresholdPreview()} threshold={threshold} mode={thresholdMode}"
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
