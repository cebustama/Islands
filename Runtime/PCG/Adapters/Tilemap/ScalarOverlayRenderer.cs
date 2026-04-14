using UnityEngine;
using UnityEngine.Tilemaps;

namespace Islands.PCG.Adapters.Tilemap
{
    /// <summary>
    /// Manages a single Texture2D-based scalar overlay rendered as a <see cref="SpriteRenderer"/>
    /// child of the visualization component.
    ///
    /// Each instance owns:
    /// - A child <see cref="GameObject"/> (hidden, non-persistent)
    /// - A <see cref="SpriteRenderer"/> for display
    /// - A <see cref="Texture2D"/> at map resolution (FilterMode.Point, RGBA32)
    /// - A <see cref="Sprite"/> linking the texture to the renderer
    /// - A <see cref="Color32"/> working buffer for pixel writes
    ///
    /// Performance: one <see cref="Texture2D.Apply"/> call per overlay update replaces
    /// the previous N×N <c>SetTile</c> calls (e.g. 65,536 calls at 256×256).
    ///
    /// Phase N6. Internal to the tilemap adapter package.
    /// </summary>
    internal sealed class ScalarOverlayRenderer : System.IDisposable
    {
        private GameObject _go;
        private SpriteRenderer _sr;
        private Texture2D _tex;
        private Sprite _sprite;
        private Color32[] _colors;
        private int _width;
        private int _height;

        // =====================================================================
        // Construction / Disposal
        // =====================================================================

        /// <summary>
        /// Create the overlay renderer as a child of <paramref name="parent"/>.
        /// The child GameObject is created with <see cref="HideFlags.DontSave"/> |
        /// <see cref="HideFlags.NotEditable"/> so it does not persist to the scene
        /// or clutter the Hierarchy.
        /// </summary>
        /// <param name="parent">Parent transform (typically the visualization component).</param>
        /// <param name="name">GameObject name (for debugging, e.g. "ScalarOverlay1").</param>
        /// <param name="sortingOrder">SpriteRenderer sorting order offset above the base tilemap.</param>
        public ScalarOverlayRenderer(Transform parent, string name, int sortingOrder)
        {
            _go = new GameObject(name);
            _go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            _go.transform.SetParent(parent, worldPositionStays: false);

            _sr = _go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = sortingOrder;
        }

        /// <summary>
        /// Destroy all owned Unity objects. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_sprite != null) Object.DestroyImmediate(_sprite);
            if (_tex != null) Object.DestroyImmediate(_tex);
            if (_go != null) Object.DestroyImmediate(_go);
            _sprite = null;
            _tex = null;
            _go = null;
            _sr = null;
            _colors = null;
            _width = 0;
            _height = 0;
        }

        // =====================================================================
        // Display Control
        // =====================================================================

        /// <summary>Show or hide the overlay.</summary>
        public void SetVisible(bool visible)
        {
            if (_go != null) _go.SetActive(visible);
        }

        /// <summary>Set overlay transparency. 0 = invisible, 1 = opaque.</summary>
        public void SetAlpha(float alpha)
        {
            if (_sr == null) return;
            Color c = _sr.color;
            c.a = Mathf.Clamp01(alpha);
            _sr.color = c;
        }

        // =====================================================================
        // Data Upload
        // =====================================================================

        /// <summary>
        /// Fill the overlay texture from scalar values with a two-color ramp.
        ///
        /// Values are normalized to [0,1] via <c>(value - min) / (max - min)</c>,
        /// then linearly interpolated between <paramref name="colorLow"/> and
        /// <paramref name="colorHigh"/>.
        ///
        /// When <paramref name="flipY"/> is true, the texture rows are written in
        /// reverse Y order so the overlay visually aligns with the tilemap's flipped
        /// coordinate mapping.
        ///
        /// Ends with a single <see cref="Texture2D.Apply"/> call — the only GPU upload.
        /// </summary>
        /// <param name="values">Scalar data, length == width × height, row-major.</param>
        /// <param name="width">Grid width (columns).</param>
        /// <param name="height">Grid height (rows).</param>
        /// <param name="min">Value mapped to colorLow.</param>
        /// <param name="max">Value mapped to colorHigh.</param>
        /// <param name="colorLow">Color at min end of the ramp.</param>
        /// <param name="colorHigh">Color at max end of the ramp.</param>
        /// <param name="flipY">Mirror vertically to match tilemap flipY.</param>
        public void SetData(
            float[] values, int width, int height,
            float min, float max,
            Color colorLow, Color colorHigh,
            bool flipY)
        {
            EnsureTexture(width, height);

            float range = max - min;
            float invRange = (range > 1e-6f) ? (1f / range) : 0f;

            for (int y = 0; y < height; y++)
            {
                // When flipY: map row 0 (bottom of data) → texture row height-1 (top of image),
                // so the visual result matches the tilemap's flipped stamping.
                int srcY = flipY ? (height - 1 - y) : y;
                int srcRow = srcY * width;
                int dstRow = y * width;

                for (int x = 0; x < width; x++)
                {
                    float v = Mathf.Clamp01((values[srcRow + x] - min) * invRange);
                    _colors[dstRow + x] = (Color32)Color.Lerp(colorLow, colorHigh, v);
                }
            }

            _tex.SetPixels32(_colors);
            _tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        // =====================================================================
        // Alignment
        // =====================================================================

        /// <summary>
        /// Position and scale the sprite so each texture pixel covers exactly one
        /// tilemap cell. Reads cell size from the tilemap's <see cref="GridLayout"/>.
        /// Matches sorting layer to the tilemap's <see cref="TilemapRenderer"/>.
        /// </summary>
        public void AlignToTilemap(UnityEngine.Tilemaps.Tilemap target)
        {
            if (target == null || _go == null || _tex == null) return;

            Grid grid = target.layoutGrid;
            Vector3 cellSize = grid != null ? grid.cellSize : Vector3.one;

            // Position: bottom-left of the overlay coincides with the tilemap origin.
            _go.transform.position = target.transform.position;

            // Scale: sprite is created with pixelsPerUnit=1, so 1 pixel = 1 world unit
            // at scale 1. Scaling by cellSize makes each pixel = one cell.
            _go.transform.localScale = new Vector3(cellSize.x, cellSize.y, 1f);

            // Match sorting layer so the overlay renders in the same layer group.
            var tr = target.GetComponent<TilemapRenderer>();
            if (tr != null)
                _sr.sortingLayerID = tr.sortingLayerID;
        }

        // =====================================================================
        // Internals
        // =====================================================================

        /// <summary>
        /// Ensure the texture, sprite, and color buffer match the requested dimensions.
        /// Recreates all three when resolution changes.
        /// </summary>
        private void EnsureTexture(int width, int height)
        {
            if (_tex != null && _width == width && _height == height)
                return;

            // Tear down old resources.
            if (_sprite != null) { Object.DestroyImmediate(_sprite); _sprite = null; }
            if (_tex != null) { Object.DestroyImmediate(_tex); _tex = null; }

            _width = width;
            _height = height;
            _colors = new Color32[width * height];

            _tex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            // Pivot at (0,0) = bottom-left. pixelsPerUnit = 1 (scaling handled by transform).
            Rect rect = new Rect(0f, 0f, width, height);
            Vector2 pivot = Vector2.zero;
            _sprite = Sprite.Create(_tex, rect, pivot, pixelsPerUnit: 1f);

            _sr.sprite = _sprite;
        }
    }
}