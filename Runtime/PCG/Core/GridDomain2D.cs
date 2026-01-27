using System;
using Unity.Mathematics;

namespace Islands.PCG.Core
{
    /// <summary>
    /// Defines the discrete 2D grid domain for PCG operations.
    /// Provides deterministic index/coordinate mapping and bounds checks.
    /// </summary>
    public readonly struct GridDomain2D
    {
        public readonly int Width;
        public readonly int Height;

        public int Length => Width * Height;

        public GridDomain2D(int width, int height)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be > 0.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be > 0.");

            Width = width;
            Height = height;
        }

        public int Index(int x, int y) => x + y * Width;

        public bool InBounds(int x, int y) =>
            (uint)x < (uint)Width && (uint)y < (uint)Height;

        /// <summary>
        /// Converts a linear index [0..Length-1] into grid coordinates (x,y).
        /// Useful for debug, texture extraction, and any algorithm iterating linearly.
        /// </summary>
        public void Coord(int index, out int x, out int y)
        {
            // Optional safety: uncomment if you want bounds checking in editor/dev builds.
            // if ((uint)index >= (uint)Length) throw new ArgumentOutOfRangeException(nameof(index));

            x = index % Width;
            y = index / Width;
        }

        /// <summary>
        /// Same as Coord(index, out x, out y) but returns an int2.
        /// </summary>
        public int2 Coord(int index)
        {
            Coord(index, out int x, out int y);
            return new int2(x, y);
        }
    }
}
