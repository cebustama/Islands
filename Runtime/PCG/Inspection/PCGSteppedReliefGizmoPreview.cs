// Phase T2.0 — gizmo relief host.
// Spec: PCG_Roadmap.md §"Phase T2 — 3D Relief Adapters", slice T2.0.
//
// Consumer of the Phase V.a inspection seam: pull-based refresh on
// IMapContextSource.RegenerationVersion (V-DD-9), the same pattern as
// PCGRuntimeOverlay. Parameter changes are dirty-tracked via OnValidate.
// Read-only: never mutates pipeline state. No Mesh, no material, no shader.
//
// Draws one flat horizontal surface per cell (a thin Gizmos cube) at the
// quantized base height produced by SteppedHeightSampler2D (T2 Layer 0),
// on the XZ plane with height along Y (Phase T1 design convention).
//
// T2.0-fix.a: all draw-time state moved into a single immutable snapshot,
// published by one reference assignment. OnDrawGizmos reads that reference
// once and never touches mutable fields, so a repaint can never observe a
// half-updated set of arrays and dimensions.
//
// T2.0-fix.c: source resolution follows the PCGRuntimeOverlay pattern — the
// resolved interface is compared against the Inspector field, so a failed cast
// leaves _src null and is retried on the next tick instead of being latched.
//
// T2.0-fix.b: the regeneration version is latched only after a successful
// export. Latching it on a failed export (source not yet rebuilt — the normal
// state for one tick after a domain reload) wedged the preview permanently and
// silently. The refresh now retries while there is no export, and both context
// menu entries report why nothing is drawn.

using Islands.PCG.Layout.Maps;
using UnityEngine;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Scene-view stepped relief preview over a live <see cref="IMapContextSource"/>.
    /// Re-exports and resamples only when the source regenerates or a local
    /// parameter changes. Adapter-side output; the core never depends on it.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Islands/PCG/Stepped Relief Gizmo Preview")]
    public sealed class PCGSteppedReliefGizmoPreview : MonoBehaviour
    {
        // =====================================================================
        // Inspector — source binding
        // =====================================================================
        [Header("Source")]
        [Tooltip("Component implementing IMapContextSource (e.g. PCGMapTilemapVisualization).")]
        [SerializeField] private MonoBehaviour source;

        // =====================================================================
        // Inspector — quantization (feeds SteppedHeightPolicy, T2 Layer 0)
        // =====================================================================
        [Header("Quantization")]
        [Min(1)]
        [Tooltip("Number of discrete elevation levels.")]
        [SerializeField] private int levelCount = 8;

        [Range(0f, 1f)]
        [Tooltip("Height mapped to level 0. Set this to the active waterThreshold01 " +
                 "so the full level span is spent on land (post-W-aux.f, land Height " +
                 "starts at the water threshold, not at 0).")]
        [SerializeField] private float normMin = 0.42553192f;

        [Range(0f, 1f)]
        [Tooltip("Height mapped to the top level. 1.0 leaves a small unused band " +
                 "(measured per-seed maxima ≈ 0.94–0.96) in exchange for cross-seed " +
                 "comparability.")]
        [SerializeField] private float normMax = 1f;

        // =====================================================================
        // Inspector — world mapping
        // =====================================================================
        [Header("World Mapping")]
        [Min(0.0001f)]
        [Tooltip("World-space height of one step.")]
        [SerializeField] private float stepWorldHeight = 0.5f;

        [Tooltip("World-space Y of level 0.")]
        [SerializeField] private float yOffset = 0f;

        [Min(0.01f)]
        [Tooltip("World-space size of one cell on the XZ plane.")]
        [SerializeField] private float cellSize = 1f;

        // =====================================================================
        // Inspector — display
        // =====================================================================
        [Header("Display")]
        [Tooltip("Skip cells whose raw Height is below Norm Min (the water band).")]
        [SerializeField] private bool skipWaterCells = true;

        [SerializeField] private Color lowColor = new Color(0.36f, 0.28f, 0.18f);
        [SerializeField] private Color highColor = new Color(0.95f, 0.95f, 0.88f);

        // =====================================================================
        // Draw-time state — immutable snapshot
        // =====================================================================

        /// <summary>
        /// Everything the draw path needs, captured together. Fields are readonly and
        /// the instance is published by a single assignment to <see cref="_snapshot"/>,
        /// so any reader sees a fully consistent set or nothing at all.
        /// </summary>
        private sealed class ReliefSnapshot
        {
            public readonly int[] Levels;   // row-major, length == Width * Height
            public readonly bool[] Skip;    // same length, or null when not skipping
            public readonly int Width;
            public readonly int Height;
            public readonly int MaxLevel;   // LevelCount - 1 as sampled, >= 0

            public ReliefSnapshot(int[] levels, bool[] skip, int width, int height, int maxLevel)
            {
                Levels = levels;
                Skip = skip;
                Width = width;
                Height = height;
                MaxLevel = maxLevel;
            }
        }

        private ReliefSnapshot _snapshot;

        // =====================================================================
        // Refresh bookkeeping (never read by the draw path)
        // =====================================================================
        private IMapContextSource _src;          // resolved interface, null if invalid
        private int _lastRegen = -1;             // advanced only on a successful export
        private MapDataExport _export;           // cached snapshot of last regeneration
        private bool _paramsDirty = true;
        private string _lastWarning;             // warn-once latch

        // ---------------------------------------------------------------------

        private void OnValidate()
        {
            // Keep the policy valid under Inspector edits; BuildSnapshot re-checks anyway.
            if (normMax <= normMin) normMax = Mathf.Min(1f, normMin + 1e-4f);
            _paramsDirty = true;
        }

        private void OnDisable()
        {
            // Drop derived state; keep serialized params. Re-enable refreshes.
            _snapshot = null;
            _export = null;
            _lastRegen = -1;
        }

        private void Update()
        {
            ResolveSource();
            if (_src == null)
            {
                _snapshot = null;
                _export = null;
                return;
            }

            int regen = _src.RegenerationVersion;

            // Refresh the export when the source regenerated, and keep retrying while
            // we have none: the source's context is null for at least one tick after a
            // domain reload, and that tick must not become a permanent state.
            if (regen != _lastRegen || _export == null)
            {
                MapContext2D ctx = _src.Context;
                MapDataExport fresh = ctx != null ? MapExporter2D.Export(ctx) : null;

                if (fresh != null)
                {
                    _export = fresh;
                    _lastRegen = regen;   // latch only on success
                    _paramsDirty = true;  // rebuild against the new export
                }
                else
                {
                    _export = null;
                    _snapshot = null;
                }
            }

            // Rebuild on parameter change, and also whenever we hold an export but no
            // snapshot — the recovery path after any failed build.
            if (_export != null && (_paramsDirty || _snapshot == null))
            {
                _snapshot = BuildSnapshot();
                _paramsDirty = false;
            }
        }

        /// <summary>
        /// Resolves the Inspector field to the inspection interface. Mirrors
        /// PCGRuntimeOverlay: the comparison is against the resolved interface, so a
        /// failed cast leaves <see cref="_src"/> null and the resolution is retried on
        /// every tick. A binding must never be able to latch into a failed state.
        /// </summary>
        private void ResolveSource()
        {
            if (source == null)
            {
                _src = null;
                return;
            }

            if (!ReferenceEquals(_src, source))
            {
                _src = source as IMapContextSource;
                _lastRegen = -1; // force re-export against the newly bound source
                _export = null;

                if (_src == null)
                    WarnOnce($"Source '{source.GetType().Name}' does not implement IMapContextSource.");
            }
        }

        /// <summary>
        /// Samples Layer 0 and packages the result. Returns null when there is
        /// nothing drawable; never returns a partially populated snapshot.
        /// </summary>
        private ReliefSnapshot BuildSnapshot()
        {
            if (_export == null)
                return null;

            var policy = new SteppedHeightPolicy
            {
                LevelCount = levelCount,
                NormMin = normMin,
                NormMax = normMax
            };

            if (policy.LevelCount < 1)
            {
                WarnOnce("Level Count must be at least 1; nothing drawn.");
                return null;
            }

            if (!(policy.NormMax > policy.NormMin))
            {
                WarnOnce("Norm Max must be greater than Norm Min; nothing drawn.");
                return null;
            }

            if (!SteppedHeightSampler2D.TrySampleLevels(_export, in policy, out int[] levels))
            {
                WarnOnce("Height field not exported by the source; nothing to draw.");
                return null;
            }

            bool[] skip = null;
            if (skipWaterCells)
            {
                float[] h = _export.GetField(MapFieldId.Height);
                skip = new bool[h.Length];
                for (int i = 0; i < h.Length; i++)
                    skip[i] = h[i] < policy.NormMin;
            }

            _lastWarning = null; // healthy state clears the latch

            return new ReliefSnapshot(
                levels, skip, _export.Width, _export.Height, policy.LevelCount - 1);
        }

        private void WarnOnce(string msg)
        {
            if (msg == _lastWarning) return;
            _lastWarning = msg;
            Debug.LogWarning($"[PCGSteppedReliefGizmoPreview] {msg}", this);
        }

        // ---------------------------------------------------------------------

        private void OnDrawGizmos()
        {
            ReliefSnapshot snap = _snapshot; // one read; the rest of the method uses only this
            if (snap == null)
                return;

            Vector3 origin = transform.position;
            float thickness = Mathf.Max(0.01f, stepWorldHeight * 0.05f);
            float colorDivisor = Mathf.Max(1, snap.MaxLevel);
            var size = new Vector3(cellSize, thickness, cellSize);

            for (int y = 0; y < snap.Height; y++)
            {
                float z = (y + 0.5f) * cellSize; // Z axis = context +y (row-major rows)
                for (int x = 0; x < snap.Width; x++)
                {
                    int i = x + y * snap.Width;
                    if (snap.Skip != null && snap.Skip[i]) continue;

                    int level = snap.Levels[i];
                    float wy = SteppedHeightSampler2D.LevelToWorldY(level, stepWorldHeight, yOffset);

                    Gizmos.color = Color.Lerp(lowColor, highColor, level / colorDivisor);
                    Gizmos.DrawCube(origin + new Vector3((x + 0.5f) * cellSize, wy, z), size);
                }
            }
        }

        // ---------------------------------------------------------------------
        // Smoke-test probes
        // ---------------------------------------------------------------------

        /// <summary>
        /// Forces a full re-export and rebuild, bypassing the regeneration-version
        /// check. Use when the Scene view has not ticked since the source rebuilt.
        /// </summary>
        [ContextMenu("Force Refresh")]
        private void ForceRefresh()
        {
            _src = null;        // forces ResolveSource to re-resolve the binding
            _lastRegen = -1;
            _export = null;
            _snapshot = null;
            _paramsDirty = true;
            _lastWarning = null;

            Update();

            Debug.Log($"[T2.0] Force Refresh — {DescribeState()}", this);
        }

        /// <summary>
        /// T2.0 smoke-test probe. Reports how many distinct levels the drawn cells
        /// actually occupy, and the per-level cell counts. Read-only. Counts against
        /// the snapshot's own level span, not the current Inspector value, so lowering
        /// Level Count before the next resample cannot put a stale level out of range.
        /// </summary>
        [ContextMenu("Log Level Histogram")]
        private void LogLevelHistogram()
        {
            ReliefSnapshot snap = _snapshot;
            if (snap == null)
            {
                Debug.LogWarning($"[T2.0] Nothing sampled — {DescribeState()}", this);
                return;
            }

            var counts = new int[snap.MaxLevel + 1];
            int drawn = 0;
            for (int i = 0; i < snap.Levels.Length; i++)
            {
                if (snap.Skip != null && snap.Skip[i]) continue;
                counts[snap.Levels[i]]++;
                drawn++;
            }

            int occupied = 0;
            var sb = new System.Text.StringBuilder();
            for (int l = 0; l < counts.Length; l++)
            {
                if (counts[l] > 0) occupied++;
                sb.Append($" L{l}={counts[l]}");
            }

            Debug.Log(
                $"[T2.0] cells={snap.Levels.Length} drawn={drawn} " +
                $"levels occupied={occupied}/{counts.Length} " +
                $"normMin={normMin} normMax={normMax}" + sb, this);
        }

        /// <summary>Human-readable reason for the current draw state.</summary>
        private string DescribeState()
        {
            if (source == null) return "no Source assigned.";
            if (_src == null) return $"Source '{source.GetType().Name}' does not implement IMapContextSource.";
            if (_src.Context == null) return "the Source has no live context yet — let it generate once.";
            if (_export == null) return "no export captured yet — move the mouse over the Scene view to tick.";
            if (_snapshot == null) return "export present but sampling produced nothing — check the warnings above.";
            return $"OK: {_snapshot.Width}x{_snapshot.Height}, levels 0..{_snapshot.MaxLevel}.";
        }
    }
}