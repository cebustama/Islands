using Islands.PCG.Fields;
using Unity.Mathematics;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Map-wide tunables that multiple stages may read.
    /// Keep this small; stage-specific configs should live on the stage itself.
    ///
    /// F2b additions: islandAspectRatio, warpAmplitude01.
    /// Both are consumed only by Stage_BaseTerrain2D (and its configurable lantern twin).
    /// Default values (1.0 / 0.0) produce the same circle geometry as the pre-F2b
    /// implementation. Goldens differ because warp arrays are always filled from ctx.Rng.
    ///
    /// J2 addition: heightRedistributionExponent.
    /// Consumed by Stage_BaseTerrain2D after height quantization, before land threshold.
    /// Default 1.0 = identity (pow(x, 1) == x); existing goldens unaffected.
    ///
    /// N2 addition: heightRemapSpline.
    /// Piecewise-linear spline applied after J2 redistribution, before land threshold.
    /// Default (null arrays) = identity; existing goldens unaffected.
    ///
    /// N4 additions: terrainNoise, warpNoise, heightQuantSteps.
    /// Replace the hardcoded NoiseCellSize/NoiseAmplitude/QuantSteps/WarpCellSize constants
    /// in Stage_BaseTerrain2D with configurable noise runtime parameters. Noise is now
    /// generated via coordinate hashing (MapNoiseBridge2D) instead of sequential RNG,
    /// eliminating all ctx.Rng consumption in the base terrain stage.
    /// Full golden break — all hashes change.
    ///
    /// F3b additions: hillsThresholdL1, hillsThresholdL2.
    /// Consumed by Stage_Hills2D for height-threshold hill classification.
    /// Replaces topology-based hill placement with Height field thresholds.
    /// Full golden break for F3+ hashes.
    ///
    /// N5.a addition: shapeMode.
    /// Selects the built-in base shape generator (Ellipse, Rectangle, NoShape, Custom).
    /// Default Ellipse preserves all existing goldens (bit-identical to pre-N5.a).
    ///
    /// N5.d additions: hillsNoiseBlend, hillsNoise.
    /// Optional per-cell noise modulation of hill height thresholds. Default 0.0 blend
    /// preserves all existing goldens (bit-identical to pre-N5.d). hillsNoise configures
    /// the noise algorithm via TerrainNoiseSettings (amplitude field ignored — modulation
    /// depth is controlled by hillsNoiseBlend).
    ///
    /// N5.e: Hills threshold UX remap.
    /// Constructor now accepts hillsL1 / hillsL2 as relative fractions [0,1] instead
    /// of raw Height-space thresholds. The remap computes effective thresholds:
    ///   L1_eff = waterThreshold + hillsL1 * (1 - waterThreshold)
    ///   L2_eff = L1_eff + hillsL2 * (1 - L1_eff)
    /// Stored fields hillsThresholdL1 / hillsThresholdL2 remain as effective raw
    /// thresholds consumed by Stage_Hills2D — no stage changes required.
    /// L2 >= L1 is guaranteed by construction. Golden break for F3+ hashes.
    /// </summary>
    public readonly struct MapTunables2D
    {
        // ------------------------------------------------------------------
        // N5.a addition
        // ------------------------------------------------------------------

        /// <summary>
        /// Selects the built-in base shape generator. Default <see cref="IslandShapeMode.Ellipse"/>
        /// preserves pre-N5.a behavior. Overridden when <see cref="MapShapeInput.HasShape"/> is true.
        /// </summary>
        public readonly IslandShapeMode shapeMode;

        // ------------------------------------------------------------------
        // F2 original fields
        // ------------------------------------------------------------------

        /// <summary>Island size in [0..1] relative to min(width, height).</summary>
        public readonly float islandRadius01;

        /// <summary>Water threshold in [0..1] — cells with Height >= this are Land.</summary>
        public readonly float waterThreshold01;

        /// <summary>
        /// Smoothstep shaping for the radial/ellipse falloff.
        /// from &lt;= to, both clamped to [0..1].
        /// </summary>
        public readonly float islandSmoothFrom01;
        public readonly float islandSmoothTo01;

        // ------------------------------------------------------------------
        // F2b additions
        // ------------------------------------------------------------------

        /// <summary>
        /// Ellipse aspect ratio applied before domain warp.
        /// 1.0 = circle. >1 = wider (x-stretched). &lt;1 = taller (y-stretched).
        /// Clamped to [0.25, 4.0].
        /// For Rectangle mode: controls width/height ratio of the rectangle.
        /// </summary>
        public readonly float islandAspectRatio;

        /// <summary>
        /// Domain warp amplitude as a fraction of min(width, height).
        /// 0.0 = no warp (pure ellipse / circle). ~0.15 = subtle organic coast.
        /// ~0.30 = strong bays and peninsulas. Clamped to [0..1].
        /// Applied to Ellipse and Rectangle modes. NoShape ignores warp geometrically.
        /// </summary>
        public readonly float warpAmplitude01;

        // ------------------------------------------------------------------
        // J2 addition
        // ------------------------------------------------------------------

        /// <summary>
        /// Power-curve exponent applied to the Height field after quantization.
        /// pow(height01, exponent) reshapes the height distribution:
        ///   1.0 = identity (no change; preserves existing goldens).
        ///   >1.0 = flattens lowlands, sharpens peaks (e.g. 2.0 is a strong effect).
        ///   &lt;1.0 = raises lowlands, compresses peaks.
        /// Clamped to [0.5, 4.0].
        /// </summary>
        public readonly float heightRedistributionExponent;

        // ------------------------------------------------------------------
        // N2 addition
        // ------------------------------------------------------------------

        /// <summary>
        /// Piecewise-linear spline applied to the Height field after pow() redistribution.
        /// Provides arbitrary designer-tunable curve reshaping of elevation distribution.
        /// Identity spline (or default) = no remapping; preserves existing goldens.
        /// Consumed by Stage_BaseTerrain2D after J2 redistribution, before Land threshold.
        /// </summary>
        public readonly ScalarSpline heightRemapSpline;

        // ------------------------------------------------------------------
        // N4 additions
        // ------------------------------------------------------------------

        /// <summary>
        /// Noise settings for terrain height perturbation.
        /// Replaces the old NoiseCellSize + NoiseAmplitude constants in Stage_BaseTerrain2D.
        /// Consumed via <see cref="MapNoiseBridge2D.FillNoise01"/>.
        /// </summary>
        public readonly TerrainNoiseSettings terrainNoise;

        /// <summary>
        /// Noise settings for domain warp (coastline shape distortion).
        /// Replaces the old WarpCellSize constant + RNG arrays in Stage_BaseTerrain2D.
        /// Actual warp displacement = warpAmplitude01 * minDim * warpNoise sample.
        /// Consumed via <see cref="MapNoiseBridge2D.FillNoise01"/>.
        /// </summary>
        public readonly TerrainNoiseSettings warpNoise;

        /// <summary>
        /// Height quantization steps. Rounds continuous height values into discrete
        /// elevation bands, producing visible contour rings.
        /// 0 = no quantization (smooth gradients). 1024 = effectively smooth.
        /// Low values (4–16) = dramatic terraced appearance.
        /// Moved from a hardcoded constant in Stage_BaseTerrain2D to a tunable in Phase N4.
        /// </summary>
        public readonly int heightQuantSteps;

        // ------------------------------------------------------------------
        // F3b additions (N5.e: stored as effective raw thresholds)
        // ------------------------------------------------------------------

        /// <summary>
        /// Effective height threshold for HillsL1 (passable slopes).
        /// Land cells with Height >= this value become HillsL1 (unless >= hillsThresholdL2).
        /// [0..1]. Computed from the relative hillsL1 input via N5.e remap:
        ///   hillsThresholdL1 = waterThreshold + hillsL1 * (1 - waterThreshold).
        /// </summary>
        public readonly float hillsThresholdL1;

        /// <summary>
        /// Effective height threshold for HillsL2 (impassable peaks).
        /// Land cells with Height >= this value become HillsL2.
        /// Always >= hillsThresholdL1 (guaranteed by N5.e remap construction).
        /// [0..1]. Computed from the relative hillsL2 input via N5.e remap:
        ///   hillsThresholdL2 = hillsThresholdL1 + hillsL2 * (1 - hillsThresholdL1).
        /// </summary>
        public readonly float hillsThresholdL2;

        // ------------------------------------------------------------------
        // N5.d additions
        // ------------------------------------------------------------------

        /// <summary>
        /// Noise modulation blend factor for hill boundary variation.
        /// 0.0 = pure height-threshold (current F3b behavior, golden-safe).
        /// 0.5 = moderate noise modulation — thresholds shift ±noise, producing organic
        ///       hill boundaries that loosely follow height but with irregular edges.
        /// 1.0 = maximum noise influence.
        /// [0..1]. Default 0.0.
        /// </summary>
        public readonly float hillsNoiseBlend;

        /// <summary>
        /// Noise settings for hills noise modulation (N5.d).
        /// Configures algorithm, frequency, octaves, etc. for the noise field that
        /// offsets hill classification thresholds. The <see cref="TerrainNoiseSettings.amplitude"/>
        /// field is ignored — modulation depth is controlled by <see cref="hillsNoiseBlend"/>.
        /// Default: Perlin, freq 6, octaves 2 (medium-scale organic variation).
        /// </summary>
        public readonly TerrainNoiseSettings hillsNoise;

        // ------------------------------------------------------------------
        // Default
        // ------------------------------------------------------------------

        /// <summary>
        /// Default tunables: circular island (aspect 1.0, no warp, no redistribution,
        /// no spline remap), with Perlin fBm terrain noise at frequency 8.
        /// Phase N4: full golden break from pre-N4 defaults.
        /// Phase F3b: full golden break for F3+ hashes.
        /// Phase N5.a: shapeMode = Ellipse (bit-identical to pre-N5.a).
        /// Phase N5.d: hillsNoiseBlend = 0.0 (bit-identical to pre-N5.d).
        /// Phase N5.e: hillsL1/L2 relative fractions replace raw thresholds.
        ///   Effective thresholds ≈ 0.65 / 0.8005 (golden break from pre-N5.e 0.65 / 0.80).
        /// </summary>
        public static MapTunables2D Default => new MapTunables2D(
            shapeMode: IslandShapeMode.Ellipse,
            islandRadius01: 0.45f,
            waterThreshold01: 0.50f,
            islandSmoothFrom01: 0.30f,
            islandSmoothTo01: 0.70f,
            islandAspectRatio: 1.00f,
            warpAmplitude01: 0.00f,
            heightRedistributionExponent: 1.00f,
            heightRemapSpline: default,
            terrainNoise: TerrainNoiseSettings.DefaultTerrain,
            warpNoise: TerrainNoiseSettings.DefaultWarp,
            heightQuantSteps: 1024,
            hillsL1: 0.30f,
            hillsL2: 0.43f,
            hillsNoiseBlend: 0.0f,
            hillsNoise: TerrainNoiseSettings.DefaultHills
        );

        // ------------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------------

        /// <param name="shapeMode">Built-in base shape generator. Default = Ellipse (pre-N5.a behavior).</param>
        /// <param name="islandRadius01">Island size fraction of min(w,h). [0..1].</param>
        /// <param name="waterThreshold01">Land/water height threshold. [0..1].</param>
        /// <param name="islandSmoothFrom01">Smoothstep inner edge. [0..1].</param>
        /// <param name="islandSmoothTo01">Smoothstep outer edge. [0..1].</param>
        /// <param name="islandAspectRatio">Ellipse/rectangle x/y ratio. 1.0 = circle/square. [0.25..4.0].</param>
        /// <param name="warpAmplitude01">Domain warp strength as fraction of min(w,h). [0..1].</param>
        /// <param name="heightRedistributionExponent">Height power-curve exponent. 1.0 = identity. [0.5..4.0].</param>
        /// <param name="heightRemapSpline">Piecewise-linear height remap curve. default = identity (no remap).</param>
        /// <param name="terrainNoise">Noise settings for height perturbation. Default = Perlin fBm freq 8.</param>
        /// <param name="warpNoise">Noise settings for domain warp. Default = Perlin freq 4.</param>
        /// <param name="heightQuantSteps">Height quantization steps. 0 = none, 1024 = smooth. Default = 1024.</param>
        /// <param name="hillsL1">Fraction of land height range for HillsL1 threshold. [0..1]. Default = 0.30.
        ///   Effective threshold = waterThreshold + hillsL1 * (1 - waterThreshold). (N5.e)</param>
        /// <param name="hillsL2">Fraction of remaining range above L1 for HillsL2 threshold. [0..1]. Default = 0.43.
        ///   Effective threshold = L1_eff + hillsL2 * (1 - L1_eff). (N5.e)</param>
        /// <param name="hillsNoiseBlend">Noise modulation blend for hill boundaries. [0..1]. Default = 0.0 (no noise). (N5.d)</param>
        /// <param name="hillsNoise">Noise settings for hills modulation. Default = Perlin freq 6. Amplitude ignored. (N5.d)</param>
        public MapTunables2D(
            float islandRadius01,
            float waterThreshold01,
            float islandSmoothFrom01,
            float islandSmoothTo01,
            float islandAspectRatio = 1.0f,
            float warpAmplitude01 = 0.0f,
            float heightRedistributionExponent = 1.0f,
            ScalarSpline heightRemapSpline = default,
            TerrainNoiseSettings terrainNoise = default,
            TerrainNoiseSettings warpNoise = default,
            int heightQuantSteps = 1024,
            float hillsL1 = 0.30f,
            float hillsL2 = 0.43f,
            float hillsNoiseBlend = 0.0f,
            TerrainNoiseSettings hillsNoise = default,
            IslandShapeMode shapeMode = IslandShapeMode.Ellipse)
        {
            this.shapeMode = shapeMode;

            // Clamp and order all values deterministically (pure math, no RNG).
            float r = math.clamp(islandRadius01, 0f, 1f);
            float wt = math.clamp(waterThreshold01, 0f, 1f);

            float a = math.clamp(islandSmoothFrom01, 0f, 1f);
            float b = math.clamp(islandSmoothTo01, 0f, 1f);
            if (a > b) (a, b) = (b, a);   // guarantee from <= to

            float aspect = math.clamp(islandAspectRatio, 0.25f, 4.0f);
            float warp = math.clamp(warpAmplitude01, 0f, 1f);
            float redistExp = math.clamp(heightRedistributionExponent, 0.5f, 4.0f);

            this.islandRadius01 = r;
            this.waterThreshold01 = wt;
            this.islandSmoothFrom01 = a;
            this.islandSmoothTo01 = b;
            this.islandAspectRatio = aspect;
            this.warpAmplitude01 = warp;
            this.heightRedistributionExponent = redistExp;

            // ScalarSpline is validated at its own construction time.
            // default (null arrays) is a valid identity spline — no allocation needed.
            this.heightRemapSpline = heightRemapSpline;

            // N4: noise settings stored as-is; clamping happens in the bridge.
            // Default-struct check: if frequency is 0, use defaults.
            this.terrainNoise = terrainNoise.frequency > 0
                ? terrainNoise
                : TerrainNoiseSettings.DefaultTerrain;
            this.warpNoise = warpNoise.frequency > 0
                ? warpNoise
                : TerrainNoiseSettings.DefaultWarp;
            this.heightQuantSteps = math.max(0, heightQuantSteps);

            // N5.e: Hills UX remap — relative fractions → effective raw thresholds.
            // L1: fraction of [waterThreshold, 1.0] range.
            // L2: fraction of [L1_effective, 1.0] range.
            // Guarantees L2_effective >= L1_effective by construction (both inputs >= 0,
            // both multiplied by non-negative remaining range).
            float hl1_in = math.clamp(hillsL1, 0f, 1f);
            float hl2_in = math.clamp(hillsL2, 0f, 1f);
            float hl1_eff = wt + hl1_in * (1f - wt);
            float hl2_eff = hl1_eff + hl2_in * (1f - hl1_eff);
            this.hillsThresholdL1 = hl1_eff;
            this.hillsThresholdL2 = hl2_eff;

            // N5.d: hills noise modulation.
            this.hillsNoiseBlend = math.clamp(hillsNoiseBlend, 0f, 1f);
            this.hillsNoise = hillsNoise.frequency > 0
                ? hillsNoise
                : TerrainNoiseSettings.DefaultHills;
        }
    }
}