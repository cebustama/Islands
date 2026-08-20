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
    /// F3b′ (hills window recalibration): hillsL1 / hillsL2 redefined as AREA
    /// fractions of Land. The ctor stores clamped fractions (f2 <= f1); the
    /// per-run Height-space thresholds are computed by HillsThresholdOps2D
    /// inside Stage_Hills2D via order statistics over the actual Land height
    /// distribution. Replaces the N5.e range remap, whose [waterThreshold, 1.0]
    /// anchor no longer matches the field (max Height < 1.0 post-W-aux.f and
    /// varies per seed). Golden break for F3+ hashes.
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
        // F3b′ additions (area-fraction hills tunables; replace N5.e thresholds)
        // ------------------------------------------------------------------

        /// <summary>
        /// Target fraction of the LAND AREA classified as hills-or-peaks
        /// (HillsL1 ∪ HillsL2). [0..1]. Resolved to a per-run Height threshold by
        /// <c>HillsThresholdOps2D</c> (order statistic over the Land height
        /// distribution) — NOT a height value. 0 = no hills; 1 = all land.
        /// </summary>
        public readonly float hillsL1;

        /// <summary>
        /// Target fraction of the LAND AREA classified as impassable peaks
        /// (HillsL2). [0..1]. Clamped to &lt;= <see cref="hillsL1"/> by construction
        /// (peaks are a subset of the hills budget). Resolved per run like hillsL1.
        /// The declared target is measured on the raw threshold band (pre-blend);
        /// hillsNoiseBlend shifts the exported layer around it.
        /// </summary>
        public readonly float hillsL2;

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
        // W-aux.b additions
        // ------------------------------------------------------------------

        /// <summary>
        /// Mean sea-floor elevation, expressed as a fraction of <see cref="waterThreshold01"/>.
        /// 0.0 = legacy flat ocean (identity; preserves all pre-W-aux.b goldens).
        ///
        /// Effective floor per cell =
        ///   (seaFloorLevel01 + (terrainNoise01 - 0.5) * seaFloorAmplitude01) * waterThreshold01
        /// hard-clamped to [0, waterThreshold01 - eps] and applied as a LOWER BOUND to
        /// sub-threshold cells only. Existing falloff gradients survive wherever they are
        /// higher, and no water cell can ever cross the Land threshold.
        /// [0..1].
        /// </summary>
        public readonly float seaFloorLevel01;

        /// <summary>
        /// Sea-floor relief amplitude as a fraction of <see cref="waterThreshold01"/>,
        /// driven by the terrain noise array already sampled for this map — zero extra
        /// RNG draws, so consumption parity is untouched.
        /// 0.0 = flat floor at <see cref="seaFloorLevel01"/>. [0..1].
        /// </summary>
        public readonly float seaFloorAmplitude01;

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
        /// Phase N5.e: hillsL1/L2 relative fractions replace raw thresholds. (superseded)
        /// Phase F3b′: hillsL1/L2 redefined as AREA fractions of Land (0.55 / 0.20):
        ///   ~55% of land is hills-or-peaks, ~20% is peaks, resolved per run via
        ///   order statistics. Golden break for F3+ hashes.
        /// Phase W-aux.b: seaFloorLevel01 / seaFloorAmplitude01 = 0.0 (bit-identical to pre-W-aux.b).
        /// Phase W-aux.f: Height is normalized by (1 + terrainNoise.amplitude/2), so the
        ///   perturbed mask core no longer clips to a flat 1.0 plateau. waterThreshold01
        ///   moves 0.50 -> 0.4255319 to hold the default coastline. Land topology is
        ///   expected to survive; Height-VALUED goldens (Temperature, Moisture, Biome,
        ///   Hills, Vegetation) break by design and are re-anchored.
        /// </summary>
        public static MapTunables2D Default => new MapTunables2D(
            shapeMode: IslandShapeMode.Ellipse,
            islandRadius01: 0.45f,
            // W-aux.f: compensates the height normalization by (1 + amplitude/2).
            // 0.50f / (1f + 0.35f * 0.5f), with DefaultTerrain.amplitude = 0.35 and
            // heightRedistributionExponent = 1.0. Holds the default coastline in place
            // while the height field is rescaled. Not auto-derived on purpose: the
            // factor depends on amplitude AND the redistribution exponent, and a
            // silently self-adjusting threshold would hide recalibration from goldens.
            waterThreshold01: 0.42553192f,
            islandSmoothFrom01: 0.30f,
            islandSmoothTo01: 0.70f,
            islandAspectRatio: 1.00f,
            warpAmplitude01: 0.00f,
            heightRedistributionExponent: 1.00f,
            heightRemapSpline: default,
            terrainNoise: TerrainNoiseSettings.DefaultTerrain,
            warpNoise: TerrainNoiseSettings.DefaultWarp,
            heightQuantSteps: 1024,
            hillsL1: 0.55f,
            hillsL2: 0.20f,
            hillsNoiseBlend: 0.0f,
            hillsNoise: TerrainNoiseSettings.DefaultHills,
            seaFloorLevel01: 0.0f,
            seaFloorAmplitude01: 0.0f
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
        /// <param name="hillsL1">Target fraction of LAND AREA that is hills-or-peaks (HillsL1 ∪ HillsL2). [0..1]. Default = 0.55.
        ///   Resolved to a per-run Height threshold by order statistics. (F3b′)</param>
        /// <param name="hillsL2">Target fraction of LAND AREA that is peaks (HillsL2). [0..1]. Default = 0.20.
        ///   Clamped to &lt;= hillsL1. Resolved per run. (F3b′)</param>
        /// <param name="hillsNoiseBlend">Noise modulation blend for hill boundaries. [0..1]. Default = 0.0 (no noise). (N5.d)</param>
        /// <param name="hillsNoise">Noise settings for hills modulation. Default = Perlin freq 6. Amplitude ignored. (N5.d)</param>
        /// <param name="seaFloorLevel01">Mean sea-floor elevation as a fraction of waterThreshold01. 0.0 = flat ocean (identity). [0..1]. (W-aux.b)</param>
        /// <param name="seaFloorAmplitude01">Sea-floor relief amplitude as a fraction of waterThreshold01, driven by terrain noise. 0.0 = flat floor. [0..1]. (W-aux.b)</param>
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
            float hillsL1 = 0.55f,
            float hillsL2 = 0.20f,
            float hillsNoiseBlend = 0.0f,
            TerrainNoiseSettings hillsNoise = default,
            IslandShapeMode shapeMode = IslandShapeMode.Ellipse,
            float seaFloorLevel01 = 0f,
            float seaFloorAmplitude01 = 0f)
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

            // F3b′: hills tunables are AREA fractions of Land, resolved to
            // Height-space thresholds per run by HillsThresholdOps2D (order
            // statistics over the actual Land height distribution). No remap
            // here — the ctor cannot see the field. f2 is clamped to f1:
            // peaks are a subset of the hills budget.
            float hl1_in = math.clamp(hillsL1, 0f, 1f);
            float hl2_in = math.clamp(hillsL2, 0f, 1f);
            if (hl2_in > hl1_in) hl2_in = hl1_in;
            this.hillsL1 = hl1_in;
            this.hillsL2 = hl2_in;

            // N5.d: hills noise modulation.
            this.hillsNoiseBlend = math.clamp(hillsNoiseBlend, 0f, 1f);
            this.hillsNoise = hillsNoise.frequency > 0
                ? hillsNoise
                : TerrainNoiseSettings.DefaultHills;

            // W-aux.b: sea-floor relief. Both 0 = identity (flat ocean, golden-safe).
            this.seaFloorLevel01 = math.clamp(seaFloorLevel01, 0f, 1f);
            this.seaFloorAmplitude01 = math.clamp(seaFloorAmplitude01, 0f, 1f);
        }
    }
}