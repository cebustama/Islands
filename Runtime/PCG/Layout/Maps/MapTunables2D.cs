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
    /// implementation; goldens differ because warp arrays are always filled from ctx.Rng.
    /// </summary>
    public readonly struct MapTunables2D
    {
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
        /// </summary>
        public readonly float islandAspectRatio;

        /// <summary>
        /// Domain warp amplitude as a fraction of min(width, height).
        /// 0.0 = no warp (pure ellipse / circle). ~0.15 = subtle organic coast.
        /// ~0.30 = strong bays and peninsulas. Clamped to [0..1].
        ///
        /// The two warp noise arrays are always allocated and filled from ctx.Rng
        /// regardless of this value, keeping RNG consumption count stable so that
        /// changing warpAmplitude01 never shifts the RNG state seen by downstream stages.
        /// </summary>
        public readonly float warpAmplitude01;

        // ------------------------------------------------------------------
        // Default
        // ------------------------------------------------------------------

        /// <summary>
        /// Default tunables: circular island (aspect 1.0, no warp),
        /// matching pre-F2b geometry. Suitable for all golden tests.
        /// </summary>
        public static MapTunables2D Default => new MapTunables2D(
            islandRadius01: 0.45f,
            waterThreshold01: 0.50f,
            islandSmoothFrom01: 0.30f,
            islandSmoothTo01: 0.70f,
            islandAspectRatio: 1.00f,
            warpAmplitude01: 0.00f
        );

        // ------------------------------------------------------------------
        // Constructor
        // ------------------------------------------------------------------

        /// <param name="islandRadius01">Island size fraction of min(w,h). [0..1].</param>
        /// <param name="waterThreshold01">Land/water height threshold. [0..1].</param>
        /// <param name="islandSmoothFrom01">Smoothstep inner edge. [0..1].</param>
        /// <param name="islandSmoothTo01">Smoothstep outer edge. [0..1].</param>
        /// <param name="islandAspectRatio">Ellipse x/y ratio. 1.0 = circle. [0.25..4.0].</param>
        /// <param name="warpAmplitude01">Domain warp strength as fraction of min(w,h). [0..1].</param>
        public MapTunables2D(
            float islandRadius01,
            float waterThreshold01,
            float islandSmoothFrom01,
            float islandSmoothTo01,
            float islandAspectRatio = 1.0f,
            float warpAmplitude01 = 0.0f)
        {
            // Clamp and order all values deterministically (pure math, no RNG).
            float r = math.clamp(islandRadius01, 0f, 1f);
            float wt = math.clamp(waterThreshold01, 0f, 1f);

            float a = math.clamp(islandSmoothFrom01, 0f, 1f);
            float b = math.clamp(islandSmoothTo01, 0f, 1f);
            if (a > b) (a, b) = (b, a);   // guarantee from <= to

            float aspect = math.clamp(islandAspectRatio, 0.25f, 4.0f);
            float warp = math.clamp(warpAmplitude01, 0f, 1f);

            this.islandRadius01 = r;
            this.waterThreshold01 = wt;
            this.islandSmoothFrom01 = a;
            this.islandSmoothTo01 = b;
            this.islandAspectRatio = aspect;
            this.warpAmplitude01 = warp;
        }
    }
}