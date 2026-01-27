using System;
using Islands.PCG.Grids;

namespace Islands.PCG.Generators
{
    /// <summary>
    /// Test generator: writes a checkerboard pattern into a MaskGrid2D.
    /// Useful to validate x/y mapping, packing to float4, and visualization correctness.
    /// </summary>
    public static class CheckerFillGenerator
    {
        /// <summary>
        /// Fills the entire mask with a checkerboard pattern.
        /// A "cell" is a square block of size cellSize×cellSize.
        /// </summary>
        /// <param name="mask">Target mask grid.</param>
        /// <param name="cellSize">Checker cell size in grid units (>=1).</param>
        /// <param name="xOffset">Pattern offset in X (shifts the checker pattern).</param>
        /// <param name="yOffset">Pattern offset in Y (shifts the checker pattern).</param>
        /// <param name="invert">If true, swaps on/off squares.</param>
        /// <param name="onValue">Value for the "on" squares.</param>
        /// <param name="offValue">Value for the "off" squares.</param>
        /// <param name="clearBeforeDraw">If true, clears first (only useful if offValue is false).</param>
        public static void FillCheckerboard(
            ref MaskGrid2D mask,
            int cellSize = 1,
            int xOffset = 0,
            int yOffset = 0,
            bool invert = false,
            bool onValue = true,
            bool offValue = false,
            bool clearBeforeDraw = false)
        {
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize), "cellSize must be >= 1.");

            if (clearBeforeDraw && !offValue)
                mask.Clear();

            int w = mask.Domain.Width;
            int h = mask.Domain.Height;

            for (int y = 0; y < h; y++)
            {
                int cy = (y + yOffset) / cellSize;
                for (int x = 0; x < w; x++)
                {
                    int cx = (x + xOffset) / cellSize;
                    bool isOn = ((cx + cy) & 1) == 0;
                    if (invert) isOn = !isOn;

                    bool v = isOn ? onValue : offValue;

                    // If we cleared and offValue=false, we can skip writing "off" cells.
                    if (!clearBeforeDraw || offValue || v)
                        mask.SetUnchecked(x, y, v);
                }
            }
        }

        /// <summary>
        /// Convenience: clears then fills a checkerboard with onValue=true, offValue=false.
        /// </summary>
        public static void ClearAndFillCheckerboard(
            ref MaskGrid2D mask,
            int cellSize = 1,
            int xOffset = 0,
            int yOffset = 0,
            bool invert = false)
        {
            mask.Clear();
            FillCheckerboard(ref mask, cellSize, xOffset, yOffset, invert, onValue: true, offValue: false, clearBeforeDraw: false);
        }
    }
}
