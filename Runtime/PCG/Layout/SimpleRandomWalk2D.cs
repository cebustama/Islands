using System;
using Unity.Mathematics;
using Islands.PCG.Grids;
using Islands.PCG.Operators;

using Random = Unity.Mathematics.Random;

namespace Islands.PCG.Layout
{
    public static class SimpleRandomWalk2D
    {
        public static int2 Walk(
            ref MaskGrid2D dst,
            ref Random rng,
            int2 start,
            int walkLength,
            int brushRadius,
            float skewX = 0f,
            float skewY = 0f,
            int maxRetries = 8)
        {
            if (walkLength < 0) throw new ArgumentOutOfRangeException(nameof(walkLength), "walkLength must be >= 0.");
            if (brushRadius < 0) throw new ArgumentOutOfRangeException(nameof(brushRadius), "brushRadius must be >= 0.");
            if (maxRetries < 1) throw new ArgumentOutOfRangeException(nameof(maxRetries), "maxRetries must be >= 1.");

            int w = dst.Domain.Width;
            int h = dst.Domain.Height;

            // Contract: caller should provide a valid start. Throw early if not.
            if ((uint)start.x >= (uint)w || (uint)start.y >= (uint)h)
                throw new ArgumentOutOfRangeException(nameof(start), $"start {start} out of bounds for {w}x{h}.");

            int2 pos = start;

            // Carve start cell
            Carve(ref dst, pos, brushRadius);

            for (int i = 0; i < walkLength; i++)
            {
                bool moved = false;

                // Bounce: re-pick direction until we find an in-bounds step
                for (int attempt = 0; attempt < maxRetries; attempt++)
                {
                    int2 dir = Direction2D.PickSkewedCardinal(ref rng, skewX, skewY);
                    int2 next = pos + dir;

                    if ((uint)next.x < (uint)w && (uint)next.y < (uint)h)
                    {
                        pos = next;
                        Carve(ref dst, pos, brushRadius);
                        moved = true;
                        break;
                    }
                }

                // Fallback: StopEarly
                if (!moved) break;
            }

            return pos;
        }

        private static void Carve(ref MaskGrid2D dst, int2 p, int brushRadius)
        {
            if (brushRadius <= 0)
            {
                dst.SetUnchecked(p.x, p.y, true);
            }
            else
            {
                MaskRasterOps2D.StampDisc(ref dst, p.x, p.y, brushRadius, value: true);
            }
        }
    }
}
