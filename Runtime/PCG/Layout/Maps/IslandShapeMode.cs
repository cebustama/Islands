namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Selects the built-in base shape generator for <see cref="Stage_BaseTerrain2D"/>.
    /// Controls how <c>mask01</c> is computed before height perturbation.
    ///
    /// When an external shape is provided via <see cref="MapShapeInput.HasShape"/>,
    /// it takes unconditional priority over this enum (F2c backward compatibility).
    ///
    /// Phase N5.a: initial implementation (Ellipse, Rectangle, NoShape, Custom).
    /// Future: PolarCoords mode for math-equation-based masks (star, blob, n-gon).
    /// </summary>
    public enum IslandShapeMode
    {
        /// <summary>
        /// Current F2b behavior — radial smoothstep falloff + domain warp.
        /// Default. Bit-identical to pre-N5.a output.
        /// </summary>
        Ellipse = 0,

        /// <summary>
        /// Axis-aligned rectangle with configurable margin and optional edge smoothing.
        /// Reuses existing tunables: islandRadius01 → half-extent scale,
        /// islandAspectRatio → width/height ratio, islandSmoothFrom01/To01 → edge band.
        /// Domain warp displaces the sampling point before edge-distance evaluation.
        /// </summary>
        Rectangle = 1,

        /// <summary>
        /// Raw noise + threshold. mask01 is not used; height IS pure noise.
        /// The water threshold alone carves coastlines, producing continent-like shapes
        /// entirely from noise — the pattern used by Minecraft and RimWorld for world maps.
        /// Simplest addition; highest creative impact.
        /// </summary>
        NoShape = 2,

        /// <summary>
        /// Signals Inspector/visualization consumers to show a Texture2D slot and rasterize
        /// it into a <see cref="MapShapeInput"/>. The governed stage treats Custom-without-HasShape
        /// as a fallback to Ellipse.
        /// </summary>
        Custom = 3,

        // Future: PolarCoords = 4,
        // Polar-coordinate shapes (star, blob, n-gon). Each is a function
        // (angle, radius) → mask01. Requires new tunables (point count, inner/outer radii).
    }
}