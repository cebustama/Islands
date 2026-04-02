using Islands.PCG.Grids;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Optional external shape input for Stage_BaseTerrain2D (F2c).
    ///
    /// When HasShape is false (default / None), the stage uses its internal
    /// ellipse + domain-warp silhouette (F2b path). Existing F2b goldens are unaffected.
    ///
    /// When HasShape is true, <see cref="Mask"/> drives the land silhouette.
    /// The internal ellipse + warp computation is bypassed in the pixel loop, but
    /// all three RNG arrays (island noise, warpX, warpY) are still allocated and
    /// filled in the same order so that downstream stages see an identical RNG state
    /// regardless of whether a shape input is present.
    ///
    /// Ownership contract:
    /// The caller owns the <see cref="MaskGrid2D"/> and must keep it alive for the
    /// full duration of the pipeline run. <see cref="MapInputs"/> holds this struct
    /// by value and does NOT dispose the mask.
    ///
    /// Dimension contract:
    /// <see cref="Mask"/> must be the same width/height as the pipeline domain.
    /// Stage_BaseTerrain2D will throw <see cref="System.ArgumentException"/> if
    /// dimensions do not match.
    /// </summary>
    public readonly struct MapShapeInput
    {
        /// <summary>True when an external mask is provided; false for the default ellipse path.</summary>
        public readonly bool HasShape;

        /// <summary>
        /// The external shape mask. Only valid when <see cref="HasShape"/> is true.
        /// ON cells are land-eligible; OFF cells are forced to water.
        /// </summary>
        public readonly MaskGrid2D Mask;

        /// <summary>No-shape sentinel. Equivalent to <c>default(MapShapeInput)</c>.</summary>
        public static readonly MapShapeInput None = default;

        /// <summary>Creates a shape input from an existing mask. HasShape will be true.</summary>
        public MapShapeInput(MaskGrid2D mask)
        {
            HasShape = true;
            Mask = mask;
        }
    }
}