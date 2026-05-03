// Phase V.a — Hover tooltip.
// Spec: planning/active/Phase_V_Design.md §5.
// Decision rationale: V-DD-3 (V.a iterates MapFieldId directly), V-DD-5 (TextMeshPro),
// V-DD-6 ([ExecuteAlways]), V-DD-9 (pull-based refresh via RegenerationVersion),
// V-DD-10 (hover iterates ALL MapLayerIds, ignores routing partitions),
// V-DD-11 (skip uncreated fields), V-DD-12 (cursor-following, viewport-clamped).
//
// Read-only inspection tool. Does not mutate the pipeline. Safe to leave on at all times.

using System.Text;
using Islands.PCG.Layout.Maps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Islands.PCG.Inspection
{
    /// <summary>
    /// Hover tooltip for the PCG pipeline. Reports cell (x, y), every set
    /// <see cref="MapLayerId"/> at the cell, and every created
    /// <see cref="MapFieldId"/> value at the cell. Bound to any component
    /// implementing <see cref="IMapContextSource"/>.
    ///
    /// Phase V is read-only tooling — no pipeline state is mutated, no goldens,
    /// no determinism gates.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("Islands/PCG/Inspection/PCG Hover Tooltip")]
    public sealed class PCGHoverTooltip : MonoBehaviour
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("Source")]
        [Tooltip("Component that exposes the live MapContext2D. Must implement IMapContextSource. " +
                 "Drag any of: PCGMapTilemapVisualization, PCGMapCompositeVisualization, PCGMapVisualization.")]
        [SerializeField] private MonoBehaviour source;

        [Header("Camera")]
        [Tooltip("Camera used for ScreenToWorldPoint. Defaults to Camera.main when null.")]
        [SerializeField] private Camera cam;

        [Header("Canvas (optional)")]
        [Tooltip("If null, a screen-space-overlay canvas is auto-created on enable.")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Sort order applied when auto-creating the canvas.")]
        [SerializeField] private int autoCanvasSortingOrder = 32000;

        [Header("Tooltip Style")]
        [Tooltip("Pixel offset from the cursor in screen space.")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(12f, -12f);
        [Tooltip("Inner padding around the text in pixels.")]
        [SerializeField] private Vector4 padding = new Vector4(8f, 6f, 8f, 6f); // l, t, r, b
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.78f);
        [SerializeField] private Color textColor = Color.white;
        [Range(8, 32)][SerializeField] private int fontSize = 12;
        [Tooltip("Font asset for the tooltip label. If null, TMP_Settings.defaultFontAsset is used.")]
        [SerializeField] private TMP_FontAsset fontAsset;

        // =====================================================================
        // Runtime state
        // =====================================================================

        private IMapContextSource _src;
        private bool _bindingChecked;

        private RectTransform _panelRT;
        private Image _panelImage;
        private TextMeshProUGUI _label;
        private bool _ownsCanvas; // true if we instantiated the Canvas GameObject ourselves

        private int _lastCellX = int.MinValue, _lastCellY = int.MinValue;
        private int _lastRegenVersion = -1;
        private bool _lastShown;

        private readonly StringBuilder _sb = new StringBuilder(512);

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void OnEnable()
        {
            EnsureCanvas();
            EnsurePanel();
            HidePanel();
            _bindingChecked = false;
        }

        private void OnDisable()
        {
            HidePanel();
        }

        private void OnDestroy()
        {
            if (_ownsCanvas && canvas != null)
            {
                if (Application.isPlaying) Destroy(canvas.gameObject);
                else DestroyImmediate(canvas.gameObject);
            }
        }

        private void Update()
        {
            if (!ResolveSource())
            {
                HidePanel();
                return;
            }

            var ctx = _src.Context;
            var tilemap = _src.Tilemap;
            if (ctx == null || tilemap == null)
            {
                HidePanel();
                return;
            }

            var camera = ResolveCamera();
            if (camera == null)
            {
                HidePanel();
                return;
            }

            // 1. Mouse → world
            Vector3 mousePos = Input.mousePosition;
            // Z distance from camera to map plane. For an orthographic 2D camera, any
            // positive value works; we use the absolute camera Z so the result lands on
            // the world XY plane the tilemap renders on.
            mousePos.z = Mathf.Abs(camera.transform.position.z);
            Vector3 world = camera.ScreenToWorldPoint(mousePos);

            // 2. World → cell
            if (!_src.TryWorldToCell(world, out int cellX, out int cellY))
            {
                HidePanel();
                return;
            }

            // 3. Rebuild text only when something changed
            int regen = _src.RegenerationVersion;
            bool cellChanged = cellX != _lastCellX || cellY != _lastCellY;
            bool regenChanged = regen != _lastRegenVersion;
            bool justShown = !_lastShown;

            if (cellChanged || regenChanged || justShown)
            {
                BuildTooltipText(ctx, cellX, cellY);
                _label.text = _sb.ToString();
                _label.ForceMeshUpdate();

                _lastCellX = cellX;
                _lastCellY = cellY;
                _lastRegenVersion = regen;
            }

            // 4. Position + show
            ShowPanel();
            PositionPanel(mousePos);
        }

        // =====================================================================
        // Binding
        // =====================================================================

        private bool ResolveSource()
        {
            if (source == null)
            {
                if (!_bindingChecked)
                {
                    Debug.LogWarning("[PCGHoverTooltip] Source not assigned. Drag a viz component into the Source slot.", this);
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
                        $"[PCGHoverTooltip] Source '{source.GetType().Name}' does not implement IMapContextSource.",
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

        // =====================================================================
        // Tooltip text — §5.3
        // =====================================================================

        private void BuildTooltipText(MapContext2D ctx, int x, int y)
        {
            _sb.Clear();

            // Header: cell coords (always present)
            _sb.Append('(').Append(x).Append(", ").Append(y).Append(')');

            // Layers section
            bool wroteLayerHeader = false;
            int layerCount = (int)MapLayerId.COUNT;
            for (int i = 0; i < layerCount; i++)
            {
                var id = (MapLayerId)i;
                if (!ctx.IsLayerCreated(id)) continue;
                ref var grid = ref ctx.GetLayer(id);
                if (!grid.Get(x, y)) continue;

                if (!wroteLayerHeader)
                {
                    _sb.Append('\n').Append('\n').Append("Layers:");
                    wroteLayerHeader = true;
                }
                _sb.Append('\n').Append("  ").Append(id.ToString());
            }

            // Fields section
            bool wroteFieldHeader = false;
            int fieldCount = (int)MapFieldId.COUNT;
            for (int i = 0; i < fieldCount; i++)
            {
                var id = (MapFieldId)i;
                if (!ctx.IsFieldCreated(id)) continue;
                ref var field = ref ctx.GetField(id);
                float v = field.Get(x, y);

                if (!wroteFieldHeader)
                {
                    _sb.Append('\n').Append('\n').Append("Fields:");
                    wroteFieldHeader = true;
                }
                _sb.Append('\n').Append("  ");
                AppendFieldLine(id, v);
            }
        }

        private void AppendFieldLine(MapFieldId id, float value)
        {
            // Left-justified label, fixed width for visual alignment with monospace fonts.
            // We don't enforce monospace, but the padding still helps readability.
            const int LabelWidth = 14;
            string label = id.ToString();
            _sb.Append(label);
            for (int p = label.Length; p < LabelWidth; p++) _sb.Append(' ');
            _sb.Append(": ");

            switch (id)
            {
                case MapFieldId.Height:
                case MapFieldId.Moisture:
                case MapFieldId.Temperature:
                    _sb.Append(value.ToString("F3"));
                    break;

                case MapFieldId.CoastDist:
                case MapFieldId.BiomeRegionId:
                case MapFieldId.FlowAccumulation:
                    _sb.Append(Mathf.RoundToInt(value).ToString());
                    break;

                case MapFieldId.Biome:
                    {
                        int ord = Mathf.RoundToInt(value);
                        string name = (ord >= 0 && ord < (int)BiomeType.COUNT)
                            ? ((BiomeType)ord).ToString()
                            : "?";
                        _sb.Append(name).Append(" (").Append(ord).Append(')');
                        break;
                    }

                default:
                    _sb.Append(value.ToString("F3"));
                    break;
            }
        }

        // =====================================================================
        // Canvas + panel construction
        // =====================================================================

        private void EnsureCanvas()
        {
            if (canvas != null) { _ownsCanvas = false; return; }

            var go = new GameObject("PCGHoverTooltip_Canvas");
            go.hideFlags = HideFlags.DontSave;
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = autoCanvasSortingOrder;
            go.AddComponent<CanvasScaler>();
            go.AddComponent<GraphicRaycaster>();
            _ownsCanvas = true;
        }

        private void EnsurePanel()
        {
            if (canvas == null) return;
            if (_panelRT != null) return;

            var panelGO = new GameObject("PCGHoverTooltip_Panel");
            panelGO.hideFlags = HideFlags.DontSave;
            panelGO.transform.SetParent(canvas.transform, false);

            _panelRT = panelGO.AddComponent<RectTransform>();
            _panelRT.anchorMin = Vector2.zero;
            _panelRT.anchorMax = Vector2.zero;
            _panelRT.pivot = new Vector2(0f, 1f); // top-left

            _panelImage = panelGO.AddComponent<Image>();
            _panelImage.color = panelColor;
            _panelImage.raycastTarget = false;

            var labelGO = new GameObject("Label");
            labelGO.hideFlags = HideFlags.DontSave;
            labelGO.transform.SetParent(panelGO.transform, false);
            var labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(padding.x, padding.w);   // left, bottom
            labelRT.offsetMax = new Vector2(-padding.z, -padding.y); // -right, -top

            _label = labelGO.AddComponent<TextMeshProUGUI>();
            _label.color = textColor;
            _label.fontSize = fontSize;
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            _label.raycastTarget = false;
            _label.text = string.Empty;
            if (fontAsset != null) _label.font = fontAsset;
        }

        private void ShowPanel()
        {
            if (_panelRT == null) return;
            if (!_lastShown)
            {
                _panelRT.gameObject.SetActive(true);
                _lastShown = true;
            }
        }

        private void HidePanel()
        {
            if (_panelRT == null) return;
            if (_lastShown)
            {
                _panelRT.gameObject.SetActive(false);
                _lastShown = false;
            }
            _lastCellX = int.MinValue;
            _lastCellY = int.MinValue;
        }

        private void PositionPanel(Vector3 screenMouse)
        {
            // Size the panel from the label's preferred dimensions plus padding.
            Vector2 textSize = _label.GetPreferredValues(_label.text);
            float w = textSize.x + padding.x + padding.z;
            float h = textSize.y + padding.y + padding.w;
            _panelRT.sizeDelta = new Vector2(w, h);

            // Anchor the pivot (top-left) at cursor + offset.
            float px = screenMouse.x + cursorOffset.x;
            float py = screenMouse.y + cursorOffset.y;

            // Viewport clamp — keep the rect fully on screen.
            float screenW = Screen.width;
            float screenH = Screen.height;
            if (px + w > screenW) px = Mathf.Max(0f, screenW - w);
            if (py - h < 0f) py = Mathf.Min(screenH, h);
            if (px < 0f) px = 0f;
            if (py > screenH) py = screenH;

            _panelRT.anchoredPosition = new Vector2(px, py);

            // Live restyling for OnValidate-driven Inspector tweaks.
            if (_panelImage.color != panelColor) _panelImage.color = panelColor;
            if (_label.color != textColor) _label.color = textColor;
            if (_label.fontSize != fontSize) _label.fontSize = fontSize;
        }
    }
}