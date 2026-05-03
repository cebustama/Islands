// Phase V.b — Per-cell overlay: text + discrete color.
// Spec: planning/active/Phase_V_Design.md §6.
// Decision rationale:
//   V-DD-4  V.b reuses ScalarOverlaySource (text mode pipeline-fields-only per α decision)
//   V-DD-5  World-space Canvas + TextMeshProUGUI text overlay (URP 2D compatible)
//   V-DD-6  [ExecuteAlways] — runs in edit mode and play mode
//   V-DD-7  BiomeColorPalette SO for Biome; deterministic hash-color for BiomeRegionId
//   V-DD-8  Owns its own ScalarOverlayRenderer instance
//   V-DD-9  Pull-based refresh on RegenerationVersion change
//  D2/PathB View-aware text rendering — only emit glyphs for cells visible to the camera
//  D2/B-i   Inspector Camera ref, fallback to Camera.main
//
// Implementation choice α (text overlay supports pipeline fields only):
//   Supported: Height, CoastDist, Moisture, Temperature, Biome, BiomeRegionId,
//              FlowAccumulation. Selecting TerrainNoise/WarpNoiseX/WarpNoiseY/
//              HillsNoise/ShapeMask logs once per regen and renders nothing.
//   Color mode is unaffected (already restricted to Biome + BiomeRegionId per §6.3.3).
//
// View-aware text-rendering invariant: the Canvas text mesh is rebuilt each time the
// visible cell rect changes, and contains glyphs ONLY for cells inside that rect
// (clamped to map bounds and to a hard cap of 64×64 visible cells). This keeps the
// vertex count strictly within Canvas mesh batching limits regardless of map size.

using System.Text;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Adapters.Tilemap;
using TMPro;
using UnityEngine;
using Islands.PCG.Fields;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Whole-map inspection overlay. Two independent display modes:
    ///   - Text overlay: per-cell numeric label drawn over each visible cell.
    ///   - Discrete color overlay: per-cell color from a palette (Biome) or
    ///     hash (BiomeRegionId).
    ///
    /// Both modes refresh on the source's <see cref="IMapContextSource.RegenerationVersion"/>
    /// transition, on Inspector edit, and (for text) when the camera view changes.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Islands/PCG/Inspection/PCG Runtime Overlay")]
    public sealed class PCGRuntimeOverlay : MonoBehaviour
    {
        // =====================================================================
        // Hard limits — Phase V.b D2 Path B
        // =====================================================================

        /// <summary>Hard cap on visible cell-rect dimension when text mode is active.
        /// Beyond this, text mode hides itself and logs once per regen.
        /// At 64×64 × 4 chars × 4 verts ≈ 65 536 verts — strictly inside TMP's
        /// 16-bit index buffer (65 535 verts). Keep in lockstep with this math.</summary>
        public const int TextHardCapVisibleCells = 64;

        // =====================================================================
        // Inspector — source binding
        // =====================================================================

        [Header("Source")]
        [Tooltip("Component that exposes the live MapContext2D. Must implement " +
                 "IMapContextSource (e.g. PCGMapTilemapVisualization).")]
        [SerializeField] private MonoBehaviour source;

        [Header("Camera (text mode view culling)")]
        [Tooltip("Camera used to compute the visible cell rect for text-mode rendering. " +
                 "Falls back to Camera.main when null.")]
        [SerializeField] private Camera cam;

        // =====================================================================
        // Inspector — text mode
        // =====================================================================

        [Header("Text Overlay")]
        [SerializeField] private bool enableTextOverlay = false;

        [Tooltip("Field whose per-cell value is drawn as a numeric label. " +
                 "Phase V.b α: pipeline fields only (Height, CoastDist, Moisture, " +
                 "Temperature, Biome, BiomeRegionId, FlowAccumulation). Selecting " +
                 "a noise/derived preview source logs once per regen and renders nothing.")]
        [SerializeField] private ScalarOverlaySource textField = ScalarOverlaySource.Biome;

        public enum TextFormat { Auto, F0, F1, F2, F3 }

        [SerializeField] private TextFormat textFormat = TextFormat.Auto;
        [SerializeField] private Color textColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        [Tooltip("Font size as a fraction of cellSize. 0.35 keeps a 4-character label inside one cell.")]
        [Range(0.10f, 1.00f)]
        [SerializeField] private float textFontSizeFrac = 0.35f;
        [Tooltip("TMP font asset. If null, TMP_Settings.defaultFontAsset is used.")]
        [SerializeField] private TMP_FontAsset textFontAsset;

        // =====================================================================
        // Inspector — color mode
        // =====================================================================

        [Header("Color Overlay")]
        [SerializeField] private bool enableColorOverlay = true;

        [Tooltip("Field whose per-cell value drives palette lookup. " +
                 "Restricted to Biome (uses palette) and BiomeRegionId (hash-derived). " +
                 "Other selections are reverted on validation.")]
        [SerializeField] private ScalarOverlaySource colorField = ScalarOverlaySource.Biome;

        [SerializeField] private BiomeColorPalette colorPalette;

        [Range(0f, 1f)]
        [SerializeField] private float colorAlpha = 0.65f;

        [Tooltip("Color rendered for biome ordinals not present in the palette. " +
                 "Default magenta is intentionally loud — signals palette/biome desync.")]
        [SerializeField] private Color unmappedColor = Color.magenta;

        [Tooltip("Sorting order offset for the color overlay sprite. Layered above " +
                 "the existing scalar overlay slots (which use 100 / 101).")]
        [SerializeField] private int colorSortingOrder = 200;

        // =====================================================================
        // Runtime state — source binding
        // =====================================================================

        private IMapContextSource _src;
        private bool _bindingChecked;

        // =====================================================================
        // Runtime state — refresh tracking
        // =====================================================================

        private int _lastRegenVersion = -1;
        private bool _structDirty = true;

        // Text-mode camera-state hash (D2 Path B B-ii).
        private Vector3 _lastCamPos;
        private float _lastCamOrtho;
        private bool _lastCamValid;

        // Last computed visible cell rect; -1 sentinel forces rebuild on first pass.
        private int _lastVisX0 = -1, _lastVisY0 = -1, _lastVisX1 = -1, _lastVisY1 = -1;

        // Per-regen logging guards (one log per regen).
        private bool _loggedTextSourceUnsupported;
        private bool _loggedTextFieldMissing;
        private bool _loggedColorFieldMissing;
        private bool _loggedTextSuppressedByCap;

        // =====================================================================
        // Runtime state — owned subobjects
        // =====================================================================

        private GameObject _textGO;
        private Canvas _canvas;
        private TextMeshProUGUI _text;
        private readonly StringBuilder _sb = new StringBuilder(8192);

        private ScalarOverlayRenderer _colorRenderer;
        private Color32[] _colorBuffer;
        private int _colorBufferW, _colorBufferH;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void OnEnable()
        {
            // Clean up any stale text GO from a previous domain reload or code swap.
            // The text GO is HideFlags.DontSave, so it survives code reloads but our
            // _text reference resets to null. Detect and destroy.
            if (_text == null && _textGO != null) DisposeText();

            _structDirty = true;
            _bindingChecked = false;
            _lastRegenVersion = -1;
            _lastVisX0 = _lastVisY0 = _lastVisX1 = _lastVisY1 = -1;
            ResetPerRegenLogs();
        }

        private void OnDisable()
        {
            HideText();
            HideColor();
        }

        private void OnDestroy()
        {
            DisposeText();
            DisposeColor();
        }

        private void OnValidate()
        {
            _structDirty = true;

            // Color-mode field validation per §6.3.3: revert + log on unsupported field.
            if (colorField != ScalarOverlaySource.Biome &&
                colorField != ScalarOverlaySource.BiomeRegionId)
            {
                Debug.LogWarning(
                    $"[PCGRuntimeOverlay] colorField '{colorField}' is not supported in color mode. " +
                    "Reverting to Biome. Continuous fields belong in the existing scalar overlay slots.",
                    this);
                colorField = ScalarOverlaySource.Biome;
            }
        }

        [ContextMenu("Rebuild Now")]
        private void RebuildNow()
        {
            _structDirty = true;
        }

        private void Update()
        {
            if (!ResolveSource())
            {
                HideText();
                HideColor();
                return;
            }

            var ctx = _src.Context;
            var tilemap = _src.Tilemap;
            if (ctx == null || tilemap == null)
            {
                HideText();
                HideColor();
                return;
            }

            int regen = _src.RegenerationVersion;
            bool regenChanged = regen != _lastRegenVersion;
            if (regenChanged) ResetPerRegenLogs();

            // ---- Camera state (text mode only) ----
            bool camChanged = false;
            if (enableTextOverlay)
            {
                var c = ResolveCamera();
                if (c != null)
                {
                    bool first = !_lastCamValid;
                    Vector3 pos = c.transform.position;
                    float ortho = c.orthographic ? c.orthographicSize : 0f;
                    if (first || pos != _lastCamPos || !Mathf.Approximately(ortho, _lastCamOrtho))
                    {
                        _lastCamPos = pos;
                        _lastCamOrtho = ortho;
                        _lastCamValid = true;
                        camChanged = true;
                    }
                }
            }

            // ---- Decide whether anything needs rebuilding ----
            bool needsRebuild = regenChanged || _structDirty || camChanged;
            if (!needsRebuild)
                return;

            // ---- Color rebuild ----
            if (enableColorOverlay)
                RebuildColor(ctx, tilemap);
            else
                HideColor();

            // ---- Text rebuild ----
            if (enableTextOverlay)
                RebuildText(ctx, tilemap);
            else
                HideText();

            _lastRegenVersion = regen;
            _structDirty = false;
        }

        // =====================================================================
        // Source binding
        // =====================================================================

        private bool ResolveSource()
        {
            if (source == null)
            {
                if (!_bindingChecked)
                {
                    Debug.LogWarning(
                        "[PCGRuntimeOverlay] Source not assigned. Drag an IMapContextSource " +
                        "(e.g. PCGMapTilemapVisualization) into the Source slot.",
                        this);
                    _bindingChecked = true;
                }
                _src = null;
                return false;
            }

            if (!ReferenceEquals(_src, source))
            {
                _src = source as IMapContextSource;
                if (_src == null && !_bindingChecked)
                {
                    Debug.LogWarning(
                        $"[PCGRuntimeOverlay] Source '{source.GetType().Name}' does not implement IMapContextSource.",
                        this);
                    _bindingChecked = true;
                }
            }

            return _src != null;
        }

        private Camera ResolveCamera()
        {
            if (cam != null) return cam;
            if (Camera.main != null) return Camera.main;
            return null;
        }

        private void ResetPerRegenLogs()
        {
            _loggedTextSourceUnsupported = false;
            _loggedTextFieldMissing = false;
            _loggedColorFieldMissing = false;
            _loggedTextSuppressedByCap = false;
        }

        // =====================================================================
        // Color overlay (Mode B) — §6.3
        // =====================================================================

        private void RebuildColor(MapContext2D ctx, UnityEngine.Tilemaps.Tilemap tilemap)
        {
            int w = ctx.Domain.Width;
            int h = ctx.Domain.Height;

            // Map color field (a ScalarOverlaySource) to its source MapFieldId.
            MapFieldId fieldId;
            switch (colorField)
            {
                case ScalarOverlaySource.Biome: fieldId = MapFieldId.Biome; break;
                case ScalarOverlaySource.BiomeRegionId: fieldId = MapFieldId.BiomeRegionId; break;
                default:
                    // OnValidate already reverts other selections; defensive guard.
                    HideColor();
                    return;
            }

            if (!ctx.IsFieldCreated(fieldId))
            {
                if (!_loggedColorFieldMissing)
                {
                    Debug.Log(
                        $"[PCGRuntimeOverlay] Color overlay: field {fieldId} not created. " +
                        "Enable the producing stage (Phase M for Biome, Phase M2.b for BiomeRegionId).",
                        this);
                    _loggedColorFieldMissing = true;
                }
                HideColor();
                return;
            }

            EnsureColorRenderer();
            EnsureColorBuffer(w, h);

            ref var field = ref ctx.GetField(fieldId);

            switch (colorField)
            {
                case ScalarOverlaySource.Biome: FillBiomeColors(field, w, h); break;
                case ScalarOverlaySource.BiomeRegionId: FillRegionIdColors(field, w, h); break;
            }

            // Pass flipY=false: we already wrote in source row-major order; the renderer's
            // SetDataDirect handles the texture-row flip when the source flips Y.
            _colorRenderer.SetDataDirect(_colorBuffer, w, h, _src.FlipY);
            _colorRenderer.AlignToTilemap(tilemap);
            _colorRenderer.SetAlpha(colorAlpha);
            _colorRenderer.SetSortingOrder(colorSortingOrder);
            _colorRenderer.SetVisible(true);
        }

        private void FillBiomeColors(ScalarField2D field, int w, int h)
        {
            // Pre-resolve the table-equivalent: query the palette once per (unique) ordinal
            // is a micro-opt not worth the complexity here; ordinals 0..12 are tiny.
            byte aByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(colorAlpha) * 255f);

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    float v = field.Get(x, y);
                    int ord = Mathf.RoundToInt(v);

                    Color c;
                    if (ord == 0)
                    {
                        // Unclassified / water sentinel — transparent.
                        c = new Color(0f, 0f, 0f, 0f);
                    }
                    else if (colorPalette != null)
                    {
                        c = colorPalette.Lookup(ord);
                    }
                    else
                    {
                        c = unmappedColor;
                    }

                    Color32 c32 = (Color32)c;
                    // Honor sentinel transparency; otherwise apply the configured alpha.
                    c32.a = (ord == 0) ? (byte)0 : aByte;
                    _colorBuffer[row + x] = c32;
                }
            }
        }

        private void FillRegionIdColors(ScalarField2D field, int w, int h)
        {
            byte aByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(colorAlpha) * 255f);

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int id = Mathf.RoundToInt(field.Get(x, y));
                    Color32 c32;
                    if (id == 0)
                    {
                        // Water / non-Land sentinel — transparent.
                        c32 = new Color32(0, 0, 0, 0);
                    }
                    else
                    {
                        // Deterministic hash-color per V-DD-7: HSV(hash(id)/uint.Max, 0.55, 0.85).
                        uint h32 = HashRegionId((uint)id);
                        float hue = (h32 & 0x00FFFFFFu) / (float)0x01000000u; // [0,1)
                        Color rgb = Color.HSVToRGB(hue, 0.55f, 0.85f);
                        c32 = (Color32)rgb;
                        c32.a = aByte;
                    }
                    _colorBuffer[row + x] = c32;
                }
            }
        }

        /// <summary>FNV-1a 32-bit hash. Deterministic, no allocation, stable across runs.</summary>
        private static uint HashRegionId(uint id)
        {
            const uint FnvOffset = 2166136261u;
            const uint FnvPrime = 16777619u;
            uint h = FnvOffset;
            unchecked
            {
                h = (h ^ ((id >> 0) & 0xFFu)) * FnvPrime;
                h = (h ^ ((id >> 8) & 0xFFu)) * FnvPrime;
                h = (h ^ ((id >> 16) & 0xFFu)) * FnvPrime;
                h = (h ^ ((id >> 24) & 0xFFu)) * FnvPrime;
            }
            return h;
        }

        private void EnsureColorRenderer()
        {
            if (_colorRenderer != null) return;
            _colorRenderer = new ScalarOverlayRenderer(transform, "PCGRuntimeOverlay_Color", colorSortingOrder);
        }

        private void EnsureColorBuffer(int w, int h)
        {
            if (_colorBuffer != null && _colorBufferW == w && _colorBufferH == h) return;
            _colorBuffer = new Color32[w * h];
            _colorBufferW = w;
            _colorBufferH = h;
        }

        private void HideColor()
        {
            if (_colorRenderer != null) _colorRenderer.SetVisible(false);
        }

        private void DisposeColor()
        {
            if (_colorRenderer != null)
            {
                _colorRenderer.Dispose();
                _colorRenderer = null;
            }
            _colorBuffer = null;
            _colorBufferW = _colorBufferH = 0;
        }

        // =====================================================================
        // Text overlay (Mode A) — §6.2 + D2 Path B
        // =====================================================================

        private void RebuildText(MapContext2D ctx, UnityEngine.Tilemaps.Tilemap tilemap)
        {
            // α decision: text mode supports pipeline fields only.
            if (!IsPipelineField(textField, out MapFieldId fieldId))
            {
                if (!_loggedTextSourceUnsupported)
                {
                    Debug.Log(
                        $"[PCGRuntimeOverlay] Text overlay: source '{textField}' is a noise/derived preview, " +
                        "not a pipeline field. Use the existing scalar overlay slots on " +
                        "PCGMapTilemapVisualization for noise inspection.",
                        this);
                    _loggedTextSourceUnsupported = true;
                }
                HideText();
                return;
            }

            if (!ctx.IsFieldCreated(fieldId))
            {
                if (!_loggedTextFieldMissing)
                {
                    Debug.Log(
                        $"[PCGRuntimeOverlay] Text overlay: field {fieldId} not created. " +
                        "Enable the producing stage.",
                        this);
                    _loggedTextFieldMissing = true;
                }
                HideText();
                return;
            }

            int mapW = ctx.Domain.Width;
            int mapH = ctx.Domain.Height;

            // ---- View-aware visible cell rect (D2 Path B) ----
            ComputeVisibleCellRect(tilemap, mapW, mapH,
                out int vx0, out int vy0, out int vx1, out int vy1);

            int visW = vx1 - vx0 + 1;
            int visH = vy1 - vy0 + 1;

            if (visW <= 0 || visH <= 0)
            {
                HideText();
                return;
            }

            // ---- Hard-cap suppression (vertex-budget guard) ----
            if (visW > TextHardCapVisibleCells || visH > TextHardCapVisibleCells)
            {
                if (!_loggedTextSuppressedByCap)
                {
                    Debug.Log(
                        $"[PCGRuntimeOverlay] Text overlay suppressed: visible {visW}x{visH} > hard cap " +
                        $"{TextHardCapVisibleCells}. Zoom in or use the color overlay.",
                        this);
                    _loggedTextSuppressedByCap = true;
                }
                HideText();
                return;
            }

            // ---- Build the TMP mesh ----
            EnsureText(tilemap);

            Grid grid = tilemap.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;

            int labelWidth = ResolveLabelWidth(fieldId);
            ref var field = ref ctx.GetField(fieldId);

            float fontSize = Mathf.Max(0.05f, textFontSizeFrac * cellSize.y);

            // Layout: position + size the TMP container over the visible cell rect.
            LayoutText(tilemap, cellSize, vx0, vy0, vx1, vy1);

            // Content: one line per cell row, TMP tags for exact cell alignment.
            BuildTextString(field, fieldId, mapW, mapH, vx0, vy0, vx1, vy1,
                            labelWidth, fontSize, cellSize);

            _text.text = _sb.ToString();
            _text.ForceMeshUpdate();
            _textGO.SetActive(true);

            _lastVisX0 = vx0; _lastVisY0 = vy0; _lastVisX1 = vx1; _lastVisY1 = vy1;
        }

        private static bool IsPipelineField(ScalarOverlaySource s, out MapFieldId fieldId)
        {
            switch (s)
            {
                case ScalarOverlaySource.Height: fieldId = MapFieldId.Height; return true;
                case ScalarOverlaySource.CoastDist: fieldId = MapFieldId.CoastDist; return true;
                case ScalarOverlaySource.Moisture: fieldId = MapFieldId.Moisture; return true;
                case ScalarOverlaySource.Temperature: fieldId = MapFieldId.Temperature; return true;
                case ScalarOverlaySource.Biome: fieldId = MapFieldId.Biome; return true;
                case ScalarOverlaySource.BiomeRegionId: fieldId = MapFieldId.BiomeRegionId; return true;
                case ScalarOverlaySource.FlowAccumulation: fieldId = MapFieldId.FlowAccumulation; return true;
                default: fieldId = MapFieldId.Height; return false;
            }
        }

        private void ComputeVisibleCellRect(
            UnityEngine.Tilemaps.Tilemap tilemap, int mapW, int mapH,
            out int x0, out int y0, out int x1, out int y1)
        {
            // Default: whole map (no camera available).
            x0 = 0; y0 = 0; x1 = mapW - 1; y1 = mapH - 1;

            var c = ResolveCamera();
            if (c == null || !c.orthographic) return;

            Grid grid = tilemap.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;
            if (cellSize.x <= 1e-6f || cellSize.y <= 1e-6f) return;

            // Camera viewport in world space (orthographic).
            float halfH = c.orthographicSize;
            float halfW = halfH * c.aspect;
            Vector3 camPos = c.transform.position;
            Vector3 origin = tilemap.transform.position;

            float minWorldX = camPos.x - halfW;
            float maxWorldX = camPos.x + halfW;
            float minWorldY = camPos.y - halfH;
            float maxWorldY = camPos.y + halfH;

            // World → cell. Tilemap origin = bottom-left of the grid; flipY is resolved
            // when reading values, not when computing the visible rect.
            int cx0 = Mathf.FloorToInt((minWorldX - origin.x) / cellSize.x);
            int cx1 = Mathf.FloorToInt((maxWorldX - origin.x) / cellSize.x);
            int cy0 = Mathf.FloorToInt((minWorldY - origin.y) / cellSize.y);
            int cy1 = Mathf.FloorToInt((maxWorldY - origin.y) / cellSize.y);

            x0 = Mathf.Clamp(cx0, 0, mapW - 1);
            x1 = Mathf.Clamp(cx1, 0, mapW - 1);
            y0 = Mathf.Clamp(cy0, 0, mapH - 1);
            y1 = Mathf.Clamp(cy1, 0, mapH - 1);
        }

        /// <summary>
        /// Position and size the TMP container so it spans the visible cell rect in
        /// world units. Font size, line height, and character advance are controlled
        /// via TMP rich-text tags (<c>&lt;line-height&gt;</c> and <c>&lt;mspace&gt;</c>)
        /// emitted by <see cref="BuildTextString"/>. This avoids relying on TMP's
        /// font-metric-dependent <c>lineSpacing</c> property, which does not map
        /// predictably to world units.
        /// </summary>
        private void LayoutText(
            UnityEngine.Tilemaps.Tilemap tilemap, Vector3 cellSize,
            int vx0, int vy0, int vx1, int vy1)
        {
            int visW = vx1 - vx0 + 1;
            int visH = vy1 - vy0 + 1;

            Vector3 origin = tilemap.transform.position;

            // Position at top-left of visible rect (pivot is top-left).
            // +1 on vy1 because the cell at vy1 extends from vy1*cellSize to (vy1+1)*cellSize.
            float worldX = origin.x + vx0 * cellSize.x;
            float worldY = origin.y + (vy1 + 1) * cellSize.y;
            float worldZ = origin.z - 0.01f; // slightly toward camera

            // Since the GO is at scene root (no parent), position IS world position.
            _textGO.transform.position = new Vector3(worldX, worldY, worldZ);
            _textGO.transform.localScale = Vector3.one;

            // RectTransform setup: pivot at top-left, anchors zeroed out so sizeDelta
            // is the rect's absolute size. World-space Canvas with localScale=(1,1,1)
            // maps sizeDelta 1:1 to world units.
            var rt = _text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f); // top-left pivot
            rt.sizeDelta = new Vector2(visW * cellSize.x + 10f, visH * cellSize.y + 10f);

            // Font size: fraction of cellSize controlled by Inspector slider.
            float fontSize = Mathf.Max(0.05f, textFontSizeFrac * cellSize.y);
            _text.fontSize = fontSize;
            _text.enableAutoSizing = false;

            // Reset lineSpacing to 0 — row height is controlled entirely by
            // <line-height> tags in BuildTextString.
            _text.lineSpacing = 0f;

            _text.color = textColor;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// Build the rich-text string for the visible cell rect. Uses TMP tags:
        ///   - <c>&lt;mspace=Xem&gt;</c> forces every character to advance by X×fontSize
        ///     world units, giving exact cell-aligned columns regardless of font metrics.
        ///   - <c>&lt;line-height=Xem&gt;</c> forces every line to span X×fontSize world
        ///     units, giving exact cell-aligned rows.
        ///
        /// Each label is right-padded with spaces to <paramref name="labelWidth"/> chars
        /// so columns stay aligned. Iterates Y from top (vy1) to bottom (vy0) to match
        /// TopLeft alignment — first emitted line renders at the top of the container.
        /// </summary>
        private void BuildTextString(
            ScalarField2D field, MapFieldId fieldId,
            int mapW, int mapH,
            int vx0, int vy0, int vx1, int vy1,
            int labelWidth, float fontSize, Vector3 cellSize)
        {
            _sb.Clear();

            string fmt = ResolveFormat(fieldId);

            // Compute TMP tag values in em (1em = fontSize world units).
            // mspace: each character advances cellSize.x / labelWidth world units.
            float mspaceEm = (cellSize.x / labelWidth) / fontSize;
            // line-height: each line spans exactly cellSize.y world units.
            float lineHeightEm = cellSize.y / fontSize;

            // Emit prefix tags (apply to entire text block).
            _sb.Append("<mspace=");
            _sb.Append(mspaceEm.ToString("F3"));
            _sb.Append("em><line-height=");
            _sb.Append(lineHeightEm.ToString("F3"));
            _sb.Append("em>");

            // FlipY: data row at context (x, ctxY) shows at tilemap row ty.
            for (int ty = vy1; ty >= vy0; ty--)
            {
                int srcY = _src.FlipY ? (mapH - 1 - ty) : ty;

                for (int tx = vx0; tx <= vx1; tx++)
                {
                    float v = field.Get(tx, srcY);
                    string label = FormatValue(fieldId, v, fmt);
                    _sb.Append(label);
                    // Right-pad to labelWidth so each cell occupies the same char count.
                    for (int p = label.Length; p < labelWidth; p++) _sb.Append(' ');
                }
                if (ty > vy0) _sb.Append('\n');
            }
        }

        /// <summary>Per-field column width in characters. Drives padding in BuildTextString
        /// and font-size sanity. Matches the formats from §6.2.4.</summary>
        private int ResolveLabelWidth(MapFieldId id)
        {
            switch (id)
            {
                case MapFieldId.Height:
                case MapFieldId.Moisture:
                case MapFieldId.Temperature:
                    return 5; // "0.00 " — 4 chars + 1 separator
                case MapFieldId.Biome:
                    return 3; // "12 "
                case MapFieldId.CoastDist:
                case MapFieldId.BiomeRegionId:
                    return 4; // "999 "
                case MapFieldId.FlowAccumulation:
                    return 6; // "99999 "
                default:
                    return 5;
            }
        }

        private string ResolveFormat(MapFieldId id)
        {
            if (textFormat != TextFormat.Auto)
                return textFormat.ToString(); // "F0".."F3"

            // Per §6.2.4
            switch (id)
            {
                case MapFieldId.Height:
                case MapFieldId.Moisture:
                case MapFieldId.Temperature:
                    return "F2";
                case MapFieldId.CoastDist:
                case MapFieldId.BiomeRegionId:
                case MapFieldId.FlowAccumulation:
                case MapFieldId.Biome:
                    return "F0";
                default:
                    return "F2";
            }
        }

        private static string FormatValue(MapFieldId id, float v, string fmt)
        {
            // Biome and integer fields: round to int and emit without decimals.
            if (fmt == "F0")
                return Mathf.RoundToInt(v).ToString();
            return v.ToString(fmt);
        }

        private void EnsureText(UnityEngine.Tilemaps.Tilemap tilemap)
        {
            if (_text != null) return;

            // World-space Canvas — participates in URP 2D sorting, unlike the
            // MeshRenderer used by TextMeshPro 3D (which is invisible under URP 2D
            // Renderer because URP 2D uses the sprite sorting system).
            _textGO = new GameObject("PCGRuntimeOverlay_Text");
            _textGO.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            // Root-level: no parent. transform.position maps 1:1 to world space.

            _canvas = _textGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            // No CanvasScaler — RectTransform sizeDelta is in world units directly
            // when Canvas localScale = (1,1,1).

            // Match sorting layer to the tilemap renderer.
            var tr = tilemap.GetComponent<UnityEngine.Tilemaps.TilemapRenderer>();
            if (tr != null)
                _canvas.sortingLayerID = tr.sortingLayerID;
            _canvas.sortingOrder = colorSortingOrder + 1;

            // TextMeshProUGUI on the Canvas GO — single-object world-space text.
            _text = _textGO.AddComponent<TextMeshProUGUI>();
            _text.color = textColor;
            _text.alignment = TextAlignmentOptions.TopLeft;
            _text.enableWordWrapping = false;
            _text.overflowMode = TextOverflowModes.Overflow;
            _text.enableAutoSizing = false;
            _text.raycastTarget = false;
            _text.richText = true;

            if (textFontAsset != null) _text.font = textFontAsset;
        }

        private void HideText()
        {
            if (_textGO != null) _textGO.SetActive(false);
        }

        private void DisposeText()
        {
            if (_textGO != null)
            {
                if (Application.isPlaying) Destroy(_textGO);
                else DestroyImmediate(_textGO);
                _textGO = null;
                _canvas = null;
                _text = null;
            }
        }
    }
}