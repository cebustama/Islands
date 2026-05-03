using UnityEngine;
using Islands.PCG.Inspection;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// Free-look orthographic camera controller for the PCG Map Tilemap sample scene.
    /// Designed for Phase V.a smoke testing — lets the developer pan over the map and
    /// zoom in to inspect cells with the <c>PCGHoverTooltip</c>.
    ///
    /// Pure sample-side MonoBehaviour. Does not read or mutate pipeline state; the
    /// only PCG dependency is the read-only <see cref="IMapContextSource"/> interface
    /// (Phase V.a) used to discover the map's bounds for auto-framing.
    ///
    /// Default keymap:
    ///   WASD    pan
    ///   E / Q   zoom in / out
    ///   F       re-frame the map
    ///   Tab     toggle camera mode vs. player mode (when wired)
    ///
    /// Setup:
    ///   - Attach to the Camera GameObject (forces orthographic).
    ///   - Drag a viz component into <see cref="source"/> (anything implementing
    ///     <see cref="IMapContextSource"/> — typically <c>PCGMapTilemapVisualization</c>).
    ///   - On Play, the camera auto-frames the entire map.
    ///   - For player-mode handoff, optionally assign <see cref="playerController"/>
    ///     and/or <see cref="playerObject"/>; pressing Tab swaps active control.
    ///
    /// Note: if the scene also has a <c>CameraFollow2D</c> on the same Camera, disable
    /// it before using this controller — they will fight for the transform.
    /// </summary>
    [AddComponentMenu("Islands/PCG/Map Camera Controller 2D")]
    [RequireComponent(typeof(Camera))]
    public sealed class MapCameraController2D : MonoBehaviour
    {
        // =====================================================================
        // Inspector
        // =====================================================================

        [Header("Source")]
        [Tooltip("Component implementing IMapContextSource (e.g. PCGMapTilemapVisualization). " +
                 "Used to read the tilemap bounds for auto-framing and the regen version " +
                 "for optional auto-reframe on map regeneration.")]
        [SerializeField] private MonoBehaviour source;

        [Header("Pan")]
        [Tooltip("World units per second when zoom is at 1. Effective speed is multiplied " +
                 "by the current zoom level when 'Pan Speed Scales With Zoom' is on.")]
        [Min(0.1f)][SerializeField] private float panSpeed = 5f;

        [Tooltip("When on, panning at zoomed-out levels is proportionally faster. " +
                 "Recommended on for large maps.")]
        [SerializeField] private bool panSpeedScalesWithZoom = true;

        [Header("Zoom")]
        [Tooltip("Zoom rate as a fraction of current size per second. Higher = faster zoom.")]
        [Min(0.01f)][SerializeField] private float zoomSpeed = 1.5f;

        [Tooltip("Smallest allowed orthographic size (most zoomed in).")]
        [Min(0.01f)][SerializeField] private float zoomMin = 0.5f;

        [Tooltip("Largest allowed orthographic size (most zoomed out). " +
                 "Auto-expanded if the map's frame requires more.")]
        [Min(0.1f)][SerializeField] private float zoomMax = 100f;

        [Header("Auto-Frame")]
        [Tooltip("Frame the entire map on enable. If the pipeline has not yet run, " +
                 "framing is retried each frame until it succeeds.")]
        [SerializeField] private bool autoFrameOnStart = true;

        [Tooltip("Re-frame whenever the source's RegenerationVersion changes " +
                 "(e.g. seed bumped, resolution changed).")]
        [SerializeField] private bool autoFrameOnRegen = false;

        [Tooltip("Extra empty space around the framed map, as a fraction of view size. " +
                 "0 = exact fit, 0.05 = 5% padding.")]
        [Range(0f, 0.5f)][SerializeField] private float framePadding = 0.05f;

        [Header("Mode Toggle")]
        [Tooltip("Whether camera input is currently active. Toggled by the toggle key.")]
        [SerializeField] private bool cameraModeActive = true;

        [Tooltip("Key that swaps between camera mode and player mode.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Tooltip("Optional. Player MonoBehaviour to enable when in player mode and " +
                 "disable when in camera mode. Typically MapPlayerController2D.")]
        [SerializeField] private MonoBehaviour playerController;

        [Tooltip("Optional. Player GameObject to show in player mode and hide in " +
                 "camera mode. If unassigned, only the controller is toggled.")]
        [SerializeField] private GameObject playerObject;

        [Header("Keys")]
        [SerializeField] private KeyCode panUpKey = KeyCode.W;
        [SerializeField] private KeyCode panDownKey = KeyCode.S;
        [SerializeField] private KeyCode panLeftKey = KeyCode.A;
        [SerializeField] private KeyCode panRightKey = KeyCode.D;
        [SerializeField] private KeyCode zoomInKey = KeyCode.E;
        [SerializeField] private KeyCode zoomOutKey = KeyCode.Q;
        [SerializeField] private KeyCode reframeKey = KeyCode.F;

        // =====================================================================
        // Runtime state
        // =====================================================================

        private Camera _cam;
        private IMapContextSource _src;
        private bool _bindingChecked;
        private bool _framedOnce;
        private bool _pendingAutoFrame;
        private int _lastRegenVersion = -1;

        // =====================================================================
        // Unity lifecycle
        // =====================================================================

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            _cam.orthographic = true;
        }

        private void OnEnable()
        {
            ApplyMode();
            _pendingAutoFrame = autoFrameOnStart;
            _framedOnce = false;
        }

        private void Update()
        {
            ResolveSource();

            // Toggle works regardless of active state.
            if (Input.GetKeyDown(toggleKey))
            {
                cameraModeActive = !cameraModeActive;
                ApplyMode();
            }

            // Pending auto-frame retry — succeeds once the source has a valid context.
            if (_pendingAutoFrame && TryFrameMap())
                _pendingAutoFrame = false;

            // Auto-reframe on regen.
            if (autoFrameOnRegen && _src != null)
            {
                int v = _src.RegenerationVersion;
                if (v != _lastRegenVersion)
                {
                    _lastRegenVersion = v;
                    if (_framedOnce) TryFrameMap();
                }
            }

            if (!cameraModeActive) return;

            HandlePan();
            HandleZoom();

            if (Input.GetKeyDown(reframeKey))
                TryFrameMap();
        }

        // =====================================================================
        // Input
        // =====================================================================

        private void HandlePan()
        {
            float dx = 0f, dy = 0f;
            if (Input.GetKey(panLeftKey)) dx -= 1f;
            if (Input.GetKey(panRightKey)) dx += 1f;
            if (Input.GetKey(panDownKey)) dy -= 1f;
            if (Input.GetKey(panUpKey)) dy += 1f;
            if (dx == 0f && dy == 0f) return;

            // Normalize so diagonals are not faster than axis-aligned moves.
            float invLen = 1f / Mathf.Sqrt(dx * dx + dy * dy);
            dx *= invLen;
            dy *= invLen;

            float speed = panSpeed * (panSpeedScalesWithZoom ? _cam.orthographicSize : 1f);
            float dt = Time.unscaledDeltaTime;

            Vector3 p = transform.position;
            p.x += dx * speed * dt;
            p.y += dy * speed * dt;
            transform.position = p;
        }

        private void HandleZoom()
        {
            float dz = 0f;
            if (Input.GetKey(zoomInKey)) dz -= 1f;
            if (Input.GetKey(zoomOutKey)) dz += 1f;
            if (dz == 0f) return;

            // Multiplicative-style zoom: rate is proportional to current size, so the
            // perceptual zoom speed is constant whether you're at size 5 or size 50.
            float dt = Time.unscaledDeltaTime;
            float newSize = _cam.orthographicSize * (1f + dz * zoomSpeed * dt);
            _cam.orthographicSize = Mathf.Clamp(newSize, zoomMin, zoomMax);
        }

        // =====================================================================
        // Mode toggle
        // =====================================================================

        private void ApplyMode()
        {
            if (playerController != null)
                playerController.enabled = !cameraModeActive;
            if (playerObject != null)
                playerObject.SetActive(!cameraModeActive);
        }

        // =====================================================================
        // Source binding
        // =====================================================================

        private void ResolveSource()
        {
            if (source == null)
            {
                if (!_bindingChecked)
                {
                    Debug.LogWarning("[MapCameraController2D] Source not assigned. " +
                                     "Drag a viz component (e.g. PCGMapTilemapVisualization) " +
                                     "into the Source slot to enable auto-framing.", this);
                    _bindingChecked = true;
                }
                _src = null;
                return;
            }

            if (!ReferenceEquals(_src, source))
            {
                _src = source as IMapContextSource;
                if (_src == null && !_bindingChecked)
                {
                    Debug.LogWarning(
                        $"[MapCameraController2D] Source '{source.GetType().Name}' does not " +
                        $"implement IMapContextSource. Auto-framing disabled.", this);
                    _bindingChecked = true;
                }
            }
        }

        // =====================================================================
        // Auto-framing
        // =====================================================================

        /// <summary>
        /// Attempts to frame the entire map by reading bounds from the source's
        /// <see cref="IMapContextSource.Tilemap"/> and <see cref="IMapContextSource.Context"/>.
        /// Returns false if the source is not yet ready (pipeline has not run, or the
        /// source is a non-tilemap viz like the lantern).
        /// </summary>
        public bool TryFrameMap()
        {
            if (_src == null) return false;
            var tilemap = _src.Tilemap;
            var ctx = _src.Context;
            if (tilemap == null || ctx == null) return false;

            int w = ctx.Domain.Width;
            int h = ctx.Domain.Height;
            if (w <= 0 || h <= 0) return false;

            // World-space rect of cells [0..w-1] x [0..h-1].
            // CellToWorld returns the *origin* (lower-left) of the cell, so the upper
            // bound of the map is the origin of cell (w, h).
            Vector3 min = tilemap.CellToWorld(new Vector3Int(0, 0, 0));
            Vector3 max = tilemap.CellToWorld(new Vector3Int(w, h, 0));

            float worldW = Mathf.Abs(max.x - min.x);
            float worldH = Mathf.Abs(max.y - min.y);
            Vector3 center = (min + max) * 0.5f;

            // Required orthographic size = max(half-height, half-width / aspect).
            float aspect = _cam.aspect > 0.001f ? _cam.aspect : 1f;
            float requiredHalfH = Mathf.Max(worldH * 0.5f, (worldW * 0.5f) / aspect);
            requiredHalfH *= (1f + framePadding);

            // Auto-expand zoomMax if the map needs more headroom than configured.
            if (requiredHalfH > zoomMax)
                zoomMax = requiredHalfH;

            _cam.orthographicSize = Mathf.Clamp(requiredHalfH, zoomMin, zoomMax);

            Vector3 pos = transform.position;
            pos.x = center.x;
            pos.y = center.y;
            transform.position = pos;

            _framedOnce = true;
            return true;
        }

        // =====================================================================
        // Public API (for external mode switchers, hotkeys, etc.)
        // =====================================================================

        /// <summary>
        /// Whether the camera is currently in free-look mode (true) or has yielded
        /// control to the player controller (false).
        /// </summary>
        public bool CameraModeActive
        {
            get => cameraModeActive;
            set
            {
                if (cameraModeActive == value) return;
                cameraModeActive = value;
                ApplyMode();
            }
        }
    }
}