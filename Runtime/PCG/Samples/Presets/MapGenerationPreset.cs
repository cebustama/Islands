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
    /// Phase W-aux.b: seaFloorLevel01 + seaFloorAmplitude01 fields.
    /// Phase J2: heightRedistributionExponent field.
    /// Phase N2: heightRemapCurve field (AnimationCurve → ScalarSpline bridge).
    /// Phase N4: TerrainNoiseSettings replaces noiseCellSize/noiseAmplitude/quantSteps.
    ///           Separate warp noise settings. heightQuantSteps tunable.
    /// Phase F3b: hillsThresholdL1 / hillsThresholdL2 for height-coherent hills.
    /// Phase N5.a: shapeMode (IslandShapeMode enum) for base shape selection.
    /// Phase N5.b: NoiseSettingsAsset slots (terrainNoiseAsset, warpNoiseAsset).
    ///             Refactored individual noise fields to embedded TerrainNoiseSettings structs.
    ///             Serialization break: field names changed from terrainNoiseType/terrainFrequency/...
    ///             to terrainNoiseSettings.noiseType/terrainNoiseSettings.frequency/...
    /// Phase N5.d: hillsNoiseBlend + hillsNoiseSettings / hillsNoiseAsset for hills noise modulation.
    /// Phase N5.e: Hills threshold UX remap — hillsThresholdL1/L2 → hillsL1/L2 (relative fractions).
    ///             Serialization break: field names changed. Old raw-threshold values are not
    ///             migrated (semantic change); existing presets fall back to new defaults.
    /// Phase M: enableBiomeStage toggle for Climate &amp; Biome Classification.
    /// M-fix.a: 10 biome climate tunables promoted to Inspector. Moisture defaults adjusted (M-fix.c folded in).
    /// Phase L–M: 2 river moisture tunables added (biomeRiverMoistureBonus, biomeRiverFlowNorm).
    ///            Active only when Stage_Hydrology2D runs before Stage_Biome2D.
    /// Phase W-aux.a: ToJson() + "Log Preset (JSON)" context menu for shareable
    ///            parameter dumps. Includes a derived block with the N5.e effective
    ///            hills thresholds and the resolved noise source per slot, since
    ///            neither is visible in the Inspector. Read-only; no pipeline effect.
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

        [Tooltip("Include the Biome classification stage (Phase M).\n" +
                 "Produces Temperature, Moisture, and Biome scalar fields.\n" +
                 "Requires Morphology enabled for CoastDist dependency.")]
        public bool enableBiomeStage = true;

        // ==================================================================
        // Biome Climate (Phase M / M-fix.a)
        // ==================================================================

        [Header("Biome Climate (Phase M)")]
        [Range(0f, 1f)]
        [Tooltip("Sea-level equatorial base temperature. 0.7 = warm tropical islands.")]
        public float biomeBaseTemperature = 0.7f;

        [Range(0f, 1f)]
        [Tooltip("Height-to-temperature reduction. 0.5 = highest peaks lose half base temp.")]
        public float biomeLapseRate = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Y-axis latitude gradient strength. 0.0 for single-island (no latitude).\n" +
                 "Non-zero for Phase W world maps.")]
        public float biomeLatitudeEffect = 0.0f;

        [Range(0f, 0.5f)]
        [Tooltip("Coastal temperature moderation strength. 1/(1+coastDist) falloff.")]
        public float biomeCoastModerationStrength = 0.1f;

        [Range(0f, 0.3f)]
        [Tooltip("Temperature noise amplitude. Low-frequency perturbation.")]
        public float biomeTempNoiseAmplitude = 0.05f;

        [Min(1)]
        [Tooltip("Temperature noise cell size (frequency). Coarse; 2× terrain noise freq.")]
        public int biomeTempNoiseCellSize = 16;

        [Range(0f, 1f)]
        [Tooltip("Coastal proximity moisture bonus at coast.")]
        public float biomeCoastalMoistureBonus = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Coastal moisture decay rate. Higher = faster inland decay.")]
        public float biomeCoastDecayRate = 0.3f;

        [Range(0f, 1f)]
        [Tooltip("Moisture noise amplitude. Perturbation; coast gradient is dominant.")]
        public float biomeMoistureNoiseAmplitude = 0.3f;

        [Min(1)]
        [Tooltip("Moisture noise cell size. 4–8× lower frequency than terrain noise.")]
        public int biomeMoistureNoiseCellSize = 32;

        [Range(0f, 1f)]
        [Tooltip("Maximum moisture bonus for cells at or above the river flow threshold.\n" +
                 "Active only when Stage_Hydrology2D is in the pipeline (Phase L).\n" +
                 "0 = no river enrichment. Default 0.4.")]
        public float biomeRiverMoistureBonus = 0.4f;

        [Min(0f)]
        [Tooltip("Flow accumulation normalization divisor.\n" +
                 "0 = auto (totalLandCells × 0.02, matching Phase L river threshold).\n" +
                 "Override with an explicit value when using a non-default river threshold.")]
        public float biomeRiverFlowNorm = 0f;

        // ==================================================================
        // Island Shape (N5.a)
        // ==================================================================

        [Header("Island Shape")]
        [Tooltip("Base shape generator for the island silhouette.\n" +
                 "Ellipse (default): radial smoothstep falloff + domain warp.\n" +
                 "Rectangle: axis-aligned rectangle with soft edges + domain warp.\n" +
                 "NoShape: pure noise — water threshold carves coastlines (continent-like).\n" +
                 "Custom: use an external shape mask (Texture2D on visualization component).\n\n" +
                 "External shape input (F2c MapShapeInput) always overrides this setting.")]
        public IslandShapeMode shapeMode = IslandShapeMode.Ellipse;

        [Range(0f, 1f)]
        [Tooltip("Island size as a fraction of the smaller map dimension.\n" +
                 "0.45 = island fills ~90% of the map width. Smaller values\n" +
                 "produce a smaller island with more surrounding ocean.\n" +
                 "For Rectangle mode: controls the half-extent scale.")]
        public float islandRadius01 = 0.45f;

        [Range(0.25f, 4f)]
        [Tooltip("Ellipse/Rectangle aspect ratio applied to the island silhouette.\n" +
                 "1.0 = circular/square island. > 1 = wider (east-west stretched).\n" +
                 "< 1 = taller (north-south stretched). Range [0.25 .. 4.0].")]
        public float islandAspectRatio = 1.00f;

        [Range(0f, 1f)]
        [Tooltip("Domain warp amplitude as a fraction of the map size.\n" +
                 "0 = no warp (clean ellipse/circle/rectangle outline).\n" +
                 "~0.15 = subtle organic coastline with natural bays.\n" +
                 "~0.30 = strong coastline variation with deep bays and peninsulas.\n" +
                 "Higher values produce increasingly irregular shapes.\n" +
                 "Applied to Ellipse and Rectangle modes. NoShape ignores warp geometrically.")]
        public float warpAmplitude01 = 0.00f;

        [Range(0f, 1f)]
        [Tooltip("Smoothstep inner edge of the radial/edge falloff.\n" +
                 "Controls how abruptly terrain transitions from full height to ocean.\n" +
                 "Lower values = sharper cliff-like coasts.\n" +
                 "Must be <= Smooth To (clamped internally if reversed).")]
        public float islandSmoothFrom01 = 0.30f;

        [Range(0f, 1f)]
        [Tooltip("Smoothstep outer edge of the radial/edge falloff.\n" +
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
                 "Default 0.50 gives balanced land/water ratio at default radius.\n" +
                 "For NoShape mode: this is the primary control for land/water balance.")]
        // W-aux.f: mirrors MapTunables2D.Default.waterThreshold01. Both were 0.50f before
        // the height normalization by (1 + amplitude/2); they must stay equal or
        // ToTunables_DefaultPreset_MatchesMapTunables2DDefault fails. A tuned preset ASSET
        // carries its own value (Default_MapPreset uses 0.412119f, compensated for its own
        // amplitude 0.22 and exponent 1.3) — this is the class default, not an asset value.
        public float waterThreshold01 = 0.42553192f;

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
        // Sea Floor (W-aux.b)
        // ==================================================================

        [Header("Sea Floor (W-aux.b)")]
        [Range(0f, 1f)]
        [Tooltip("Mean sea-floor elevation as a fraction of Water Threshold.\n" +
                 "0 = legacy flat ocean (golden-safe default).\n\n" +
                 "Applied as a LOWER BOUND to water cells only: coastal falloff\n" +
                 "gradients survive wherever they are higher, and the value is\n" +
                 "hard-clamped below the Land threshold, so Land never changes.\n\n" +
                 "Typical values: 0.30-0.40, paired with depth bands ~0.10 / ~0.25.")]
        public float seaFloorLevel01 = 0f;

        [Range(0f, 1f)]
        [Tooltip("Sea-floor relief amplitude as a fraction of Water Threshold,\n" +
                 "driven by the terrain noise already sampled for this map\n" +
                 "(no extra RNG draws, no change to determinism).\n" +
                 "0 = flat floor at Sea Floor Level.\n\n" +
                 "Typical values: 0.20-0.40 for visible abyssal ridges and shoals.")]
        public float seaFloorAmplitude01 = 0f;

        // ==================================================================
        // Noise Settings Assets (N5.b — optional override)
        // ==================================================================

        [Header("Noise Settings Assets (N5.b)")]
        [Tooltip("Optional reusable noise asset for terrain height perturbation.\n" +
                 "When assigned, overrides the inline Terrain Noise settings below.\n" +
                 "When null, inline settings are used.")]
        public NoiseSettingsAsset terrainNoiseAsset;

        [Tooltip("Optional reusable noise asset for domain warp.\n" +
                 "When assigned, overrides the inline Warp Noise settings below.\n" +
                 "When null, inline settings are used.")]
        public NoiseSettingsAsset warpNoiseAsset;

        [Tooltip("Optional reusable noise asset for hills noise modulation (N5.d).\n" +
                 "When assigned, overrides the inline Hills Noise settings below.\n" +
                 "When null, inline settings are used.\n" +
                 "Only relevant when Hills Noise Blend > 0.")]
        public NoiseSettingsAsset hillsNoiseAsset;

        // ==================================================================
        // Terrain Noise (N4 → N5.b struct embed)
        // ==================================================================

        [Header("Terrain Noise")]
        [Tooltip("Noise algorithm, frequency, octaves, and fractal settings for\n" +
                 "terrain height perturbation. Overridden by Terrain Noise Asset when assigned.")]
        public TerrainNoiseSettings terrainNoiseSettings = TerrainNoiseSettings.DefaultTerrain;

        // ==================================================================
        // Warp Noise (N4 → N5.b struct embed)
        // ==================================================================

        [Header("Warp Noise")]
        [Tooltip("Noise algorithm, frequency, octaves, and fractal settings for\n" +
                 "domain warp (coastline shape distortion). Overridden by Warp Noise Asset when assigned.\n" +
                 "Amplitude on this struct is typically 1.0 — actual warp displacement\n" +
                 "is scaled by Warp Amplitude 01 above.")]
        public TerrainNoiseSettings warpNoiseSettings = TerrainNoiseSettings.DefaultWarp;

        // ==================================================================
        // Hills (F3b′ — area-fraction quantile thresholds)
        // ==================================================================

        [Header("Hills (F3b′)")]
        [Range(0f, 1f)]
        [Tooltip("Hills budget — fraction of the LAND AREA that becomes hills-or-peaks (HillsL1 + HillsL2).\n" +
                 "0.0 = no hills. 1.0 = all land.\n" +
                 "Resolved per run: the pipeline finds the Height cut that yields this\n" +
                 "area on the actual map, so the result is stable across seeds.")]
        public float hillsL1 = 0.55f;

        [Range(0f, 1f)]
        [Tooltip("Peaks budget — fraction of the LAND AREA that becomes impassable peaks (HillsL2).\n" +
                 "Clamped to <= Hills L1 (peaks are a subset of the hills budget).\n" +
                 "Resolved per run like Hills L1. Measured on the raw threshold band —\n" +
                 "Hills Noise Blend shifts the exported layer around this target.")]
        public float hillsL2 = 0.20f;

        [Range(0f, 1f)]
        [Tooltip("Noise modulation of hill boundaries (N5.d).\n" +
                 "0.0 = pure height-threshold (default, golden-safe).\n" +
                 "0.5 = moderate noise variation — hill edges no longer exactly trace Height contours.\n" +
                 "1.0 = maximum noise influence on hill boundary shapes.\n" +
                 "Only affects classification thresholds; all invariants preserved.")]
        public float hillsNoiseBlend = 0f;

        // ==================================================================
        // Hills Noise (N5.d)
        // ==================================================================

        [Header("Hills Noise (N5.d)")]
        [Tooltip("Noise algorithm, frequency, octaves, and fractal settings for\n" +
                 "hills threshold modulation. Overridden by Hills Noise Asset when assigned.\n" +
                 "Only relevant when Hills Noise Blend > 0.\n" +
                 "The amplitude field is ignored — modulation depth is controlled by Hills Noise Blend.")]
        public TerrainNoiseSettings hillsNoiseSettings = TerrainNoiseSettings.DefaultHills;

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
        ///
        /// Phase N4: includes terrain noise, warp noise, and height quant settings.
        /// Phase F3b: includes hills threshold settings.
        /// Phase N5.a: includes shapeMode.
        /// Phase N5.b: resolves NoiseSettingsAsset slots (asset → inline fallback).
        /// Phase N5.d: includes hillsNoiseBlend + hillsNoise (asset → inline fallback).
        /// Phase N5.e: hillsL1/L2 relative fractions (remap computed in MapTunables2D ctor).
        /// Phase W-aux.b: includes sea-floor relief tunables.
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
            terrainNoise: terrainNoiseAsset != null
                ? terrainNoiseAsset.Settings
                : terrainNoiseSettings,
            warpNoise: warpNoiseAsset != null
                ? warpNoiseAsset.Settings
                : warpNoiseSettings,
            heightQuantSteps: heightQuantSteps,
            hillsL1: hillsL1,
            hillsL2: hillsL2,
            hillsNoiseBlend: hillsNoiseBlend,
            hillsNoise: hillsNoiseAsset != null
                ? hillsNoiseAsset.Settings
                : hillsNoiseSettings,
            shapeMode: shapeMode,
            seaFloorLevel01: seaFloorLevel01,
            seaFloorAmplitude01: seaFloorAmplitude01);

        // ==================================================================
        // W-aux.a — JSON dump (diagnostics only)
        // ==================================================================

        /// <summary>
        /// Logs this preset as JSON to the console. Right-click the asset header
        /// (or use the gear menu) and pick "Log Preset (JSON)".
        /// </summary>
        [ContextMenu("Log Preset (JSON)")]
        private void LogPresetJson()
        {
            Debug.Log($"[MapGenerationPreset] {name}\n{ToJson()}", this);
        }

        /// <summary>
        /// Serializes every pipeline-driving field to JSON, plus a "derived" block
        /// holding values the pipeline actually consumes but the Inspector never shows:
        /// the N5.e effective hills thresholds and which noise source won per slot.
        ///
        /// Hand-built rather than JsonUtility so that (a) derived values can be
        /// included, (b) AnimationCurve keys serialize readably, and (c) the output
        /// is stable and diffable across runs. Read-only: calling this cannot change
        /// generation output.
        /// </summary>
        public string ToJson()
        {
            var t = ToTunables();
            var sb = new System.Text.StringBuilder(2048);

            sb.Append("{\n");
            sb.Append($"  \"asset\": {Q(name)},\n");

            sb.Append("  \"runInputs\": {\n");
            sb.Append($"    \"seed\": {seed},\n");
            sb.Append($"    \"resolution\": {resolution}\n");
            sb.Append("  },\n");

            sb.Append("  \"stageToggles\": {\n");
            sb.Append($"    \"hills\": {B(enableHillsStage)},\n");
            sb.Append($"    \"shore\": {B(enableShoreStage)},\n");
            sb.Append($"    \"vegetation\": {B(enableVegetationStage)},\n");
            sb.Append($"    \"traversal\": {B(enableTraversalStage)},\n");
            sb.Append($"    \"morphology\": {B(enableMorphologyStage)},\n");
            sb.Append($"    \"biome\": {B(enableBiomeStage)}\n");
            sb.Append("  },\n");
            sb.Append("  \"stageTogglesNote\": \"regions + hydrology are component-scoped; "
                      + "this asset does not carry them\",\n");

            sb.Append("  \"islandShape\": {\n");
            sb.Append($"    \"shapeMode\": {Q(shapeMode.ToString())},\n");
            sb.Append($"    \"islandRadius01\": {F(islandRadius01)},\n");
            sb.Append($"    \"islandAspectRatio\": {F(islandAspectRatio)},\n");
            sb.Append($"    \"warpAmplitude01\": {F(warpAmplitude01)},\n");
            sb.Append($"    \"islandSmoothFrom01\": {F(islandSmoothFrom01)},\n");
            sb.Append($"    \"islandSmoothTo01\": {F(islandSmoothTo01)}\n");
            sb.Append("  },\n");

            sb.Append("  \"waterAndShore\": {\n");
            sb.Append($"    \"waterThreshold01\": {F(waterThreshold01)},\n");
            sb.Append($"    \"shallowWaterDepth01\": {F(shallowWaterDepth01)},\n");
            sb.Append($"    \"midWaterDepth01\": {F(midWaterDepth01)},\n");
            sb.Append($"    \"seaFloorLevel01\": {F(seaFloorLevel01)},\n");
            sb.Append($"    \"seaFloorAmplitude01\": {F(seaFloorAmplitude01)}\n");
            sb.Append("  },\n");

            sb.Append("  \"height\": {\n");
            sb.Append($"    \"heightQuantSteps\": {heightQuantSteps},\n");
            sb.Append($"    \"heightRedistributionExponent\": {F(heightRedistributionExponent)},\n");
            sb.Append($"    \"heightRemapCurve\": {CurveJson(heightRemapCurve)}\n");
            sb.Append("  },\n");

            sb.Append("  \"hills\": {\n");
            sb.Append($"    \"hillsL1_fraction\": {F(hillsL1)},\n");
            sb.Append($"    \"hillsL2_fraction\": {F(hillsL2)},\n");
            sb.Append($"    \"hillsNoiseBlend\": {F(hillsNoiseBlend)}\n");
            sb.Append("  },\n");

            sb.Append("  \"biomeClimate\": {\n");
            sb.Append($"    \"baseTemperature\": {F(biomeBaseTemperature)},\n");
            sb.Append($"    \"lapseRate\": {F(biomeLapseRate)},\n");
            sb.Append($"    \"latitudeEffect\": {F(biomeLatitudeEffect)},\n");
            sb.Append($"    \"coastModerationStrength\": {F(biomeCoastModerationStrength)},\n");
            sb.Append($"    \"tempNoiseAmplitude\": {F(biomeTempNoiseAmplitude)},\n");
            sb.Append($"    \"tempNoiseCellSize\": {biomeTempNoiseCellSize},\n");
            sb.Append($"    \"coastalMoistureBonus\": {F(biomeCoastalMoistureBonus)},\n");
            sb.Append($"    \"coastDecayRate\": {F(biomeCoastDecayRate)},\n");
            sb.Append($"    \"moistureNoiseAmplitude\": {F(biomeMoistureNoiseAmplitude)},\n");
            sb.Append($"    \"moistureNoiseCellSize\": {biomeMoistureNoiseCellSize},\n");
            sb.Append($"    \"riverMoistureBonus\": {F(biomeRiverMoistureBonus)},\n");
            sb.Append($"    \"riverFlowNorm\": {F(biomeRiverFlowNorm)}\n");
            sb.Append("  },\n");

            sb.Append("  \"noise\": {\n");
            sb.Append($"    \"terrain\": {NoiseJson(terrainNoiseAsset, terrainNoiseSettings)},\n");
            sb.Append($"    \"warp\": {NoiseJson(warpNoiseAsset, warpNoiseSettings)},\n");
            sb.Append($"    \"hills\": {NoiseJson(hillsNoiseAsset, hillsNoiseSettings)}\n");
            sb.Append("  },\n");

            sb.Append("  \"runBehavior\": {\n");
            sb.Append($"    \"clearBeforeRun\": {B(clearBeforeRun)}\n");
            sb.Append("  },\n");

            // Derived: F3b′ — hills thresholds are per-run quantiles computed inside
            // the stage, not derivable from tunables alone. The TEMP height probe
            // logs the realized thresholds per run.
            sb.Append("  \"derived\": {\n");
            sb.Append($"    \"hillsL1_areaFraction\": {F(t.hillsL1)},\n");
            sb.Append($"    \"hillsL2_areaFraction\": {F(t.hillsL2)},\n");
            sb.Append("    \"hillsThresholds\": \"per-run quantiles over Land (F3b-prime) — see hprobe log\"\n");
            sb.Append("  }\n");

            sb.Append("}");
            return sb.ToString();
        }

        // ---- JSON formatting helpers (invariant culture: no comma decimals) ----

        private static string F(float v) =>
            v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture);

        private static string B(bool v) => v ? "true" : "false";

        private static string Q(string s) =>
            s == null ? "null" : $"\"{s.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

        private static string NoiseJson(NoiseSettingsAsset asset, TerrainNoiseSettings inline)
        {
            bool fromAsset = asset != null;
            TerrainNoiseSettings n = fromAsset ? asset.Settings : inline;
            string source = fromAsset ? "asset:" + asset.name : "inline";
            return "{ "
                 + $"\"source\": {Q(source)}, "
                 + $"\"noiseType\": {Q(n.noiseType.ToString())}, "
                 + $"\"frequency\": {n.frequency}, "
                 + $"\"octaves\": {n.octaves}, "
                 + $"\"lacunarity\": {n.lacunarity}, "
                 + $"\"persistence\": {F(n.persistence)}, "
                 + $"\"amplitude\": {F(n.amplitude)}, "
                 + $"\"fractalMode\": {Q(EnumFieldToString(n, "fractalMode"))}, "
                 + $"\"worleyDistanceMetric\": {Q(n.worleyDistanceMetric.ToString())}, "
                 + $"\"worleyFunction\": {Q(n.worleyFunction.ToString())}, "
                 + $"\"ridgedOffset\": {F(n.ridgedOffset)}, "
                 + $"\"ridgedGain\": {F(n.ridgedGain)}"
                 + " }";
        }

        /// <summary>
        /// Reads an enum field of <see cref="TerrainNoiseSettings"/> by name and returns
        /// its string form.
        ///
        /// Needed for "fractalMode" only: that enum (Islands.FractalMode) was migrated to
        /// Noise.cs in N5.c and lives in the Islands.Runtime assembly, which
        /// Islands.PCG.Samples does not reference. Writing n.fractalMode directly is
        /// CS0012 — naming the type requires the assembly reference even though the
        /// containing struct is reachable. Reflection reads the value without a
        /// compile-time type reference.
        ///
        /// Trade-off: a field rename is not caught at compile time; the dump degrades to
        /// "unknown" instead of failing. Acceptable for a diagnostics dump. The
        /// alternative — adding an asmdef reference from Samples to Islands.Runtime — is
        /// a dependency-graph decision, not a diagnostics decision.
        /// </summary>
        private static string EnumFieldToString(TerrainNoiseSettings settings, string fieldName)
        {
            var f = typeof(TerrainNoiseSettings).GetField(fieldName);
            object v = f?.GetValue(settings);
            return v != null ? v.ToString() : "unknown";
        }

        private static string CurveJson(AnimationCurve curve)
        {
            if (curve == null || curve.length == 0) return "null";
            var sb = new System.Text.StringBuilder(128);
            sb.Append("[");
            for (int i = 0; i < curve.length; i++)
            {
                var k = curve[i];
                if (i > 0) sb.Append(", ");
                sb.Append($"{{ \"t\": {F(k.time)}, \"v\": {F(k.value)}, "
                          + $"\"inT\": {F(k.inTangent)}, \"outT\": {F(k.outTangent)} }}");
            }
            sb.Append("]");
            return sb.ToString();
        }
    }
}