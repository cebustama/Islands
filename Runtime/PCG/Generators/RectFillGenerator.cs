using System;
using Unity.Mathematics;
using Islands.PCG.Grids;

namespace Islands.PCG.Generators
{
    /// <summary>
    /// Minimal "hello world" generator that writes a filled axis-aligned rectangle into a MaskGrid2D.
    /// Used to validate MaskGrid2D operations and to feed early debug visualizations.
    /// </summary>
    public static class RectFillGenerator
    {
        /// <summary>
        /// Fills an axis-aligned rectangle in the mask with the given value.
        /// Rect is [xMin, xMax) x [yMin, yMax) (min inclusive, max exclusive).
        /// </summary>
        public static void FillRect(
            ref MaskGrid2D mask,
            int xMin, int yMin,
            int xMax, int yMax,
            bool value = true,
            bool clampToDomain = true)
        {
            if (xMin > xMax) (xMin, xMax) = (xMax, xMin);
            if (yMin > yMax) (yMin, yMax) = (yMax, yMin);

            if (clampToDomain)
            {
                xMin = math.clamp(xMin, 0, mask.Domain.Width);
                xMax = math.clamp(xMax, 0, mask.Domain.Width);
                yMin = math.clamp(yMin, 0, mask.Domain.Height);
                yMax = math.clamp(yMax, 0, mask.Domain.Height);
            }
            else
            {
                if (xMin < 0 || yMin < 0 || xMax > mask.Domain.Width || yMax > mask.Domain.Height)
                    throw new ArgumentOutOfRangeException(
                        $"Rect [{xMin},{xMax})x[{yMin},{yMax}) is out of domain {mask.Domain.Width}x{mask.Domain.Height}.");
            }

            for (int y = yMin; y < yMax; y++)
                for (int x = xMin; x < xMax; x++)
                    mask.SetUnchecked(x, y, value);
        }

        /// <summary>
        /// Clears the mask then fills a rectangle. Handy for quick demos.
        /// </summary>
        public static void ClearAndFillRect(
            ref MaskGrid2D mask,
            int xMin, int yMin,
            int xMax, int yMax,
            bool value = true,
            bool clampToDomain = true)
        {
            mask.Clear();
            FillRect(ref mask, xMin, yMin, xMax, yMax, value, clampToDomain);
        }
    }
}
