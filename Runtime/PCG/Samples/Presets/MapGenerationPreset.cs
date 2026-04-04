using UnityEngine;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Samples
{
    /// <summary>
    /// ScriptableObject preset that wraps all pipeline-driving parameters shared
    /// across PCG map visualization and sample components.
    ///
    /// When assigned to a component's preset slot the component reads effective
    /// values from this asset; when null the component's own inline Inspector
    /// fields are used unchanged (fully backward-compatible).
    ///
    /// Display-only settings (palette colors, view mode, view layer, scalar range)
    /// are intentionally excluded — they remain per-component.
    ///
    /// Note: PCGMapVisualization reads resolution from its base Visualization class
    /// and cannot override it via the preset.  Resolution is honored by
    /// PCGMapCompositeVisualization, PCGMapTilemapVisualization, and
    /// PCGMapTilemapSample.
    ///
    /// Phase H3: initial implementation.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapGenerationPreset",
        menuName = "Islands/PCG/Map Generation Preset",
        order = 100)]
    public sealed class MapGenerationPreset : ScriptableObject
    {
        // ------------------------------------------------------------------
        // Run Inputs
        // ------------------------------------------------------------------

        [Header("Run Inputs")]
        [Tooltip("Deterministic seed (uint). Same seed => same map.")]
        public uint seed = 1u;

        [Tooltip("Map grid resolution (cells per side). " +
                 "Honored by PCGMapCompositeVisualization, PCGMapTilemapVisualization, " +
                 "and PCGMapTilemapSample. PCGMapVisualization reads resolution from its " +
                 "base Visualization class — this field is ignored there.")]
        [Min(4)]
        public int resolution = 64;

        // ------------------------------------------------------------------
        // Stage Toggles
        // ------------------------------------------------------------------

        [Header("Stage Toggles")]
        [Tooltip("Include the F3 Hills + topology stage after base terrain.")]
        public bool enableHillsStage = true;

        [Tooltip("Include the F4 Shore (ShallowWater) stage. Requires Hills enabled.")]
        public bool enableShoreStage = true;

        [Tooltip("Include the F5 Vegetation stage. Requires Shore enabled.")]
        public bool enableVegetationStage = true;

        [Tooltip("Include the F6 Traversal (Walkable + Stairs) stage. Requires Vegetation enabled.")]
        public bool enableTraversalStage = true;

        [Tooltip("Include the Phase G Morphology (LandCore + CoastDist) stage. Requires Traversal enabled.")]
        public bool enableMorphologyStage = true;

        // ------------------------------------------------------------------
        // F2 Tunables — Shape + Threshold
        // ------------------------------------------------------------------

        [Header("F2 — Shape + Threshold")]
        [Range(0f, 1f)]
        [Tooltip("Island size in [0..1] relative to min(width, height).")]
        public float islandRadius01 = 0.45f;

        [Range(0f, 1f)]
        [Tooltip("Height threshold: cells with Height >= this are Land.")]
        public float waterThreshold01 = 0.50f;

        [Range(0f, 1f)]
        [Tooltip("Smoothstep inner edge of the radial falloff. [0..1].")]
        public float islandSmoothFrom01 = 0.30f;

        [Range(0f, 1f)]
        [Tooltip("Smoothstep outer edge of the radial falloff. Clamped >= From internally. [0..1].")]
        public float islandSmoothTo01 = 0.70f;

        // ------------------------------------------------------------------
        // F2 Tunables — Ellipse + Warp
        // ------------------------------------------------------------------

        [Header("F2 — Ellipse + Warp")]
        [Range(0.25f, 4f)]
        [Tooltip("Ellipse aspect ratio. 1.0 = circle. >1 = wider. <1 = taller. Clamped [0.25..4].")]
        public float islandAspectRatio = 1.00f;

        [Range(0f, 1f)]
        [Tooltip("Domain warp amplitude as a fraction of min(width, height). " +
                 "0 = no warp (pure ellipse). ~0.15 = subtle organic coast. ~0.30 = strong bays.")]
        public float warpAmplitude01 = 0.00f;

        // ------------------------------------------------------------------
        // Noise
        // ------------------------------------------------------------------

        [Header("Noise")]
        [Min(1)]
        [Tooltip("Voronoi cell size in grid cells. Larger = fewer, bigger cells.")]
        public int noiseCellSize = 8;

        [Range(0f, 1f)]
        [Tooltip("Noise amplitude multiplier inside the island silhouette.")]
        public float noiseAmplitude = 0.18f;

        [Min(0)]
        [Tooltip("Height quantization steps. 0 = off. Larger = more distinct terrain rings.")]
        public int quantSteps = 1024;

        // ------------------------------------------------------------------
        // Run Behavior
        // ------------------------------------------------------------------

        [Header("Run Behavior")]
        [Tooltip("Clear all context layers before each pipeline run.")]
        public bool clearBeforeRun = true;

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Produces a <see cref="MapTunables2D"/> from this preset's shape fields.
        /// MapTunables2D clamps and orders all values deterministically.
        /// </summary>
        public MapTunables2D ToTunables() => new MapTunables2D(
            islandRadius01: islandRadius01,
            waterThreshold01: waterThreshold01,
            islandSmoothFrom01: islandSmoothFrom01,
            islandSmoothTo01: islandSmoothTo01,
            islandAspectRatio: islandAspectRatio,
            warpAmplitude01: warpAmplitude01);
    }
}