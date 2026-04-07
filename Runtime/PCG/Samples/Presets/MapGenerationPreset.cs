using UnityEngine;
using Islands.PCG.Fields;
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
    /// Phase H3: initial implementation.
    /// Phase F4b: shallowWaterDepth01 field.
    /// Phase F4c: midWaterDepth01 field.
    /// Phase J2: heightRedistributionExponent field.
    /// Phase N2: heightRemapCurve field (AnimationCurve → ScalarSpline bridge).
    /// Phase N4: TerrainNoiseSettings replaces noiseCellSize/noiseAmplitude/quantSteps.
    ///           Separate warp noise settings. heightQuantSteps tunable.
    /// Phase F3b: hillsThresholdL1 / hillsThresholdL2 for height-coherent hills.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapGenerationPreset",
        menuName = "Islands/PCG/Map Generation Preset",
        order = 100)]
    public sealed class MapGenerationPreset : ScriptableObject
    {
        // ==================================================================
        // Run Inputs
        // ==================================================================

        [Header("Run Inputs")]
        [Tooltip("Deterministic seed (uint). Same seed + same tunables = same map.\n" +
                 "Change to generate a different island. Minimum effective value is 1.")]
        public uint seed = 1u;

        [Tooltip("Map grid resolution (cells per side). Higher values produce more\n" +
                 "detailed maps but take longer to generate. Minimum 4.\n\n" +
                 "Honored by PCGMapCompositeVisualization, PCGMapTilemapVisualization,\n" +
                 "and PCGMapTilemapSample. PCGMapVisualization reads resolution from\n" +
                 "its base Visualization class — this field is ignored there.")]
        [Min(4)]
        public int resolution = 64;

        // ==================================================================
        // Stage Toggles
        // ==================================================================

        [Header("Stage Toggles")]
        [Tooltip("Include the Hills + topology stage (F3/F3b).\n" +
                 "Produces HillsL1, HillsL2 (from Height thresholds), LandEdge, LandInterior layers.\n" +
                 "Disable to see flat base terrain only.")]
        public bool enableHillsStage = true;

        [Tooltip("Include the Shore + ShallowWater + MidWater stage (F4).\n" +
                 "Produces ShallowWater ring around land and optional MidWater band.\n" +
                 "Requires Hills enabled for correct layer dependencies.")]
        public bool enableShoreStage = true;

        [Tooltip("Include the Vegetation stage (F5).\n" +
                 "Produces Vegetation mask on LandInterior cells via noise threshold.\n" +
                 "Requires Shore enabled for correct layer dependencies.")]
        public bool enableVegetationStage = true;

        [Tooltip("Include the Traversal stage (F6).\n" +
                 "Produces Walkable and Stairs layers for navigation.\n" +
                 "Requires Vegetation enabled for correct layer dependencies.")]
        public bool enableTraversalStage = true;

        [Tooltip("Include the Morphology stage (Phase G).\n" +
                 "Produces LandCore (eroded interior) and CoastDist (distance-from-coast field).\n" +
                 "Requires Traversal enabled for correct layer dependencies.")]
        public bool enableMorphologyStage = true;

        // ==================================================================
        // Island Shape
        // ==================================================================

        [Header("Island Shape")]
        [Range(0f, 1f)]
        [Tooltip("Island size as a fraction of the smaller map dimension.\n" +
                 "0.45 = island fills ~90% of the map width. Smaller values\n" +
                 "produce a smaller island with more surrounding ocean.")]
        public float islandRadius01 = 0.45f;

        [Range(0.25f, 4f)]
        [Tooltip("Ellipse aspect ratio applied to the island silhouette.\n" +
                 "1.0 = circular island. > 1 = wider (east-west stretched).\n" +
                 "< 1 = taller (north-south stretched). Range [0.25 .. 4.0].")]
        public float islandAspectRatio = 1.00f;

        [Range(0f, 1f)]
        [Tooltip("Domain warp amplitude as a fraction of the map size.\n" +
                 "0 = no warp (clean ellipse/circle outline).\n" +
                 "~0.15 = subtle organic coastline with natural bays.\n" +
                 "~0.30 = strong coastline variation with deep bays and peninsulas.\n" +
                 "Higher values produce increasingly irregular shapes.")]
        public float warpAmplitude01 = 0.00f;

        [Range(0f, 1f)]
        [Tooltip("Smoothstep inner edge of the radial falloff.\n" +
                 "Controls how abruptly terrain transitions from full height to ocean.\n" +
                 "Lower values = sharper cliff-like coasts.\n" +
                 "Must be <= Smooth To (clamped internally if reversed).")]
        public float islandSmoothFrom01 = 0.30f;

        [Range(0f, 1f)]
        [Tooltip("Smoothstep outer edge of the radial falloff.\n" +
                 "Controls how far the terrain gradient extends toward the map edge.\n" +
                 "Higher values = more gradual coastal slopes.\n" +
                 "Must be >= Smooth From (clamped internally if reversed).")]
        public float islandSmoothTo01 = 0.70f;

        // ==================================================================
        // Water & Shore
        // ==================================================================

        [Header("Water & Shore")]
        [Range(0f, 1f)]
        [Tooltip("Height threshold that separates Land from water.\n" +
                 "Cells with Height >= this value become Land; cells below become water.\n" +
                 "Higher values = smaller island (more ocean). Lower = larger island.\n" +
                 "Default 0.50 gives balanced land/water ratio at default radius.")]
        public float waterThreshold01 = 0.50f;

        [Range(0f, 0.5f)]
        [Tooltip("Height band below the water threshold for ShallowWater classification.\n" +
                 "0 = adjacency-only (original 1-cell ring around land).\n" +
                 "> 0 = water cells with Height >= (waterThreshold - this value)\n" +
                 "are also marked ShallowWater, producing a variable-width\n" +
                 "coastal shelf that follows terrain height contours.\n" +
                 "The 1-cell adjacency ring is always included regardless.\n\n" +
                 "Typical values: 0.05 = subtle shelf, 0.15 = wide shelf.")]
        public float shallowWaterDepth01 = 0f;

        [Range(0f, 0.5f)]
        [Tooltip("Height band below the water threshold for MidWater classification.\n" +
                 "0 = no MidWater layer (only Shallow + Deep).\n" +
                 "> 0 = water cells between the shallow and mid thresholds\n" +
                 "become MidWater, creating a 3-band depth system:\n" +
                 "ShallowWater (shallowest) -> MidWater -> DeepWater (deepest).\n\n" +
                 "Must be greater than Shallow Water Depth for a visible band.\n" +
                 "Keep below ~80% of Water Threshold to preserve visible deep ocean.\n\n" +
                 "Typical values: 0.15 = subtle mid band, 0.30 = wide mid band.")]
        public float midWaterDepth01 = 0f;

        // ==================================================================
        // Terrain Noise (N4)
        // ==================================================================

        [Header("Terrain Noise (N4)")]
        [Tooltip("Noise algorithm for terrain height perturbation.\n" +
                 "Perlin (default) = smooth, low artifacts.\n" +
                 "Simplex = slightly different gradient character.\n" +
                 "Value = blobby grid artifacts (for comparison).\n" +
                 "Worley = cell-based (experimental for terrain).")]
        public TerrainNoiseType terrainNoiseType = TerrainNoiseType.Perlin;

        [Range(1, 32)]
        [Tooltip("Noise frequency — features across the map. Resolution-independent.\n" +
                 "Higher = finer features. 8 ≈ medium detail (matches old noiseCellSize=8 at 64×64).")]
        public int terrainFrequency = 8;

        [Range(1, 6)]
        [Tooltip("fBm octaves. More = finer detail layered on top of base features.\n" +
                 "1 = smooth single-scale. 4 = good natural variation. 6 = maximum detail.")]
        public int terrainOctaves = 4;

        [Range(2, 4)]
        [Tooltip("Frequency multiplier per octave. 2 = each octave doubles frequency.")]
        public int terrainLacunarity = 2;

        [Range(0f, 1f)]
        [Tooltip("Amplitude decay per octave. 0.5 = each octave halves amplitude.\n" +
                 "Lower = less fine detail relative to base. Higher = more fine detail.")]
        public float terrainPersistence = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("How much height variation noise adds to the island silhouette.\n" +
                 "0 = perfectly smooth dome. 0.35 = natural variation (default).\n" +
                 "Higher values produce more varied terrain with potential inland lakes.")]
        public float terrainAmplitude = 0.35f;

        // ==================================================================
        // Warp Noise (N4)
        // ==================================================================

        [Header("Warp Noise (N4)")]
        [Tooltip("Noise algorithm for domain warp (coastline shape distortion).\n" +
                 "Same options as terrain noise; lower frequency produces broader warp.")]
        public TerrainNoiseType warpNoiseType = TerrainNoiseType.Perlin;

        [Range(1, 32)]
        [Tooltip("Warp noise frequency. Lower = broader distortion features.\n" +
                 "4 ≈ old WarpCellSize=16 at 64×64 resolution.")]
        public int warpFrequency = 4;

        [Range(1, 6)]
        [Tooltip("Warp noise octaves. 1 = simple smooth warping (default).")]
        public int warpOctaves = 1;

        [Range(2, 4)]
        public int warpLacunarity = 2;

        [Range(0f, 1f)]
        public float warpPersistence = 0.5f;

        // ==================================================================
        // Hills (F3b)
        // ==================================================================

        [Header("Hills (F3b)")]
        [Range(0f, 1f)]
        [Tooltip("Height threshold for hill slopes (HillsL1).\n" +
                 "Land cells with Height >= this become passable slopes.\n" +
                 "0.65 = default. Lower values = more slope coverage.\n" +
                 "Must be <= Hills Peak Threshold (clamped internally if reversed).")]
        public float hillsThresholdL1 = 0.65f;

        [Range(0f, 1f)]
        [Tooltip("Height threshold for hill peaks (HillsL2).\n" +
                 "Land cells with Height >= this become impassable peaks.\n" +
                 "0.80 = default. Lower values = more peak coverage.\n" +
                 "Must be >= Hills Slope Threshold (clamped internally if reversed).")]
        public float hillsThresholdL2 = 0.80f;

        // ==================================================================
        // Height Quantization (N4 — moved from constant)
        // ==================================================================

        [Header("Height Quantization (N4)")]
        [Min(0)]
        [Tooltip("Rounds height values into discrete elevation bands.\n" +
                 "0 = no quantization (smooth gradients).\n" +
                 "4–16 = dramatic terraced appearance.\n" +
                 "1024 = effectively smooth (default).")]
        public int heightQuantSteps = 1024;

        // ==================================================================
        // Height Redistribution (J2)
        // ==================================================================

        [Header("Height Redistribution (J2)")]
        [Range(0.5f, 4f)]
        [Tooltip("Power-curve exponent applied to the Height field after quantization.\n" +
                 "1.0 = no change (identity). > 1.0 = flattens lowlands, sharpens peaks.\n" +
                 "< 1.0 = raises lowlands, compresses peaks.\n" +
                 "2.0 is a strong effect; 1.5 is subtle. Range [0.5 .. 4.0].")]
        public float heightRedistributionExponent = 1.0f;

        // ==================================================================
        // Height Remap (N2)
        // ==================================================================

        [Header("Height Remap (N2)")]
        [Tooltip("Height remap curve applied after power redistribution.\n" +
                 "The X axis is the input height [0..1]; the Y axis is the output height [0..1].\n" +
                 "A straight diagonal line (0,0)→(1,1) is the identity (no remapping).\n" +
                 "Pull the middle down to flatten lowlands; push the top up to exaggerate peaks.\n" +
                 "Uses Unity's AnimationCurve editor; sampled into a piecewise-linear spline at runtime.")]
        public AnimationCurve heightRemapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // ==================================================================
        // Run Behavior
        // ==================================================================

        [Header("Run Behavior")]
        [Tooltip("Clear all pipeline layers before each run.\n" +
                 "When true (default), each generation starts from a clean state.\n" +
                 "When false, new results composite on top of existing layer data\n" +
                 "(useful for debugging but not for normal generation).")]
        public bool clearBeforeRun = true;

        // ==================================================================
        // Helpers
        // ==================================================================

        /// <summary>
        /// Produces a <see cref="MapTunables2D"/> from this preset's fields.
        /// MapTunables2D clamps and orders all values deterministically.
        /// The AnimationCurve is sampled into a piecewise-linear ScalarSpline (N2).
        /// Phase N4: includes terrain noise, warp noise, and height quant settings.
        /// Phase F3b: includes hills threshold settings.
        /// </summary>
        public MapTunables2D ToTunables() => new MapTunables2D(
            islandRadius01: islandRadius01,
            waterThreshold01: waterThreshold01,
            islandSmoothFrom01: islandSmoothFrom01,
            islandSmoothTo01: islandSmoothTo01,
            islandAspectRatio: islandAspectRatio,
            warpAmplitude01: warpAmplitude01,
            heightRedistributionExponent: heightRedistributionExponent,
            heightRemapSpline: ScalarSpline.FromAnimationCurve(heightRemapCurve),
            terrainNoise: new TerrainNoiseSettings
            {
                noiseType = terrainNoiseType,
                frequency = terrainFrequency,
                octaves = terrainOctaves,
                lacunarity = terrainLacunarity,
                persistence = terrainPersistence,
                amplitude = terrainAmplitude,
            },
            warpNoise: new TerrainNoiseSettings
            {
                noiseType = warpNoiseType,
                frequency = warpFrequency,
                octaves = warpOctaves,
                lacunarity = warpLacunarity,
                persistence = warpPersistence,
                amplitude = 1.0f, // warp scaling comes from warpAmplitude01
            },
            heightQuantSteps: heightQuantSteps,
            hillsThresholdL1: hillsThresholdL1,
            hillsThresholdL2: hillsThresholdL2);
    }
}