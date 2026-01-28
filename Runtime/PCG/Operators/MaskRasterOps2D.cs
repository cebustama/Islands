using System;
using Unity.Mathematics;
using Islands.PCG.Grids;

namespace Islands.PCG.Operators
{
    public static class MaskRasterOps2D
    {
        public static void StampDisc(ref MaskGrid2D dst, int cx, int cy, int radius, bool value = true)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius), "radius must be >= 0.");

            int w = dst.Domain.Width;
            int h = dst.Domain.Height;

            // Radius 0 = single cell (still clamp to bounds)
            if (radius == 0)
            {
                if ((uint)cx < (uint)w && (uint)cy < (uint)h)
                    dst.SetUnchecked(cx, cy, value);
                return;
            }

            int r2 = radius * radius;

            int xMin = math.max(0, cx - radius);
            int xMax = math.min(w - 1, cx + radius);
            int yMin = math.max(0, cy - radius);
            int yMax = math.min(h - 1, cy + radius);

            for (int y = yMin; y <= yMax; y++)
            {
                int dy = y - cy;
                int dy2 = dy * dy;

                for (int x = xMin; x <= xMax; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dy2 <= r2)
                    {
                        dst.SetUnchecked(x, y, value);
                    }
                }
            }
        }

        /// <summary>
        /// Rasterizes an integer line segment using Bresenham (single-loop, all octants).
        /// Endpoint-inclusive (A and B are stamped if in-bounds).
        /// Safe operator: out-of-bounds writes are skipped (never throws).
        ///
        /// Thickness: brushRadius == 0 stamps a single cell per line point.
        /// Otherwise stamps a filled disc of radius brushRadius per line point.
        /// </summary>
        public static void DrawLine(ref MaskGrid2D dst, int2 a, int2 b, int brushRadius = 0, bool value = true)
        {
            if (brushRadius < 0) throw new ArgumentOutOfRangeException(nameof(brushRadius), "brushRadius must be >= 0.");

            int w = dst.Domain.Width;
            int h = dst.Domain.Height;

            int x0 = a.x, y0 = a.y;
            int x1 = b.x, y1 = b.y;

            int dx = math.abs(x1 - x0);
            int sx = x0 < x1 ? 1 : -1;

            int dy = -math.abs(y1 - y0); // negative by convention
            int sy = y0 < y1 ? 1 : -1;

            int err = dx + dy; // dx - abs(dy)

            while (true)
            {
                // Stamp at current point (safe / no-throw).
                if (brushRadius == 0)
                {
                    if ((uint)x0 < (uint)w && (uint)y0 < (uint)h)
                        dst.SetUnchecked(x0, y0, value);
                }
                else
                {
                    // StampDisc already clamps to bounds internally.
                    StampDisc(ref dst, x0, y0, brushRadius, value);
                }

                // Endpoint-inclusive: stamp first, then break when we reach B.
                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = err << 1; // 2*err

                if (e2 >= dy)
                {
                    err += dy;
                    x0 += sx;
                }

                if (e2 <= dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }
    }
}
