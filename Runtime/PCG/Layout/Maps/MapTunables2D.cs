using Unity.Mathematics;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Map-wide tunables that multiple stages may read.
    /// Keep this small; stage-specific configs should live on the stage itself.
    /// </summary>
    public readonly struct MapTunables2D
    {
        /// <summary>Island size in [0..1] relative to min(width,height).</summary>
        public readonly float islandRadius01;

        /// <summary>Water threshold in [0..1] (used by base terrain stages).</summary>
        public readonly float waterThreshold01;

        /// <summary>Smoothstep shaping for radial mask (from <= to, both clamped to [0..1]).</summary>
        public readonly float islandSmoothFrom01;
        public readonly float islandSmoothTo01;

        public static MapTunables2D Default => new MapTunables2D(
            islandRadius01: 0.45f,
            waterThreshold01: 0.50f,
            islandSmoothFrom01: 0.30f,
            islandSmoothTo01: 0.70f
        );

        public MapTunables2D(
            float islandRadius01,
            float waterThreshold01,
            float islandSmoothFrom01,
            float islandSmoothTo01)
        {
            // Deterministic sanitation (pure clamps + ordering)
            float r = math.clamp(islandRadius01, 0f, 1f);
            float wt = math.clamp(waterThreshold01, 0f, 1f);

            float a = math.clamp(islandSmoothFrom01, 0f, 1f);
            float b = math.clamp(islandSmoothTo01, 0f, 1f);
            if (a > b) (a, b) = (b, a);

            this.islandRadius01 = r;
            this.waterThreshold01 = wt;
            this.islandSmoothFrom01 = a;
            this.islandSmoothTo01 = b;
        }
    }
}
