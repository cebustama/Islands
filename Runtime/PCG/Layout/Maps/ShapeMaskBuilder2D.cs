using Islands.PCG.Core;
using Islands.PCG.Grids;
using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// W.c — Shape-mask builder: derives a local-resolution silhouette from the
    /// 3×3 land/water neighborhood of a world tile (Phase_W_Design §9 resolution:
    /// bilinear interpolation + threshold).
    ///
    /// Sample lattice: the 9 samples sit at the CENTERS of the 3×3 world cells.
    /// The local map covers the area of the center cell, so every local cell maps
    /// to lattice coordinate 0.5 + (i + 0.5)/N per axis — strictly inside (0.5, 1.5).
    ///
    /// Threshold semantics: interpolated value >= threshold01 ⇒ land (deterministic
    /// tie-break). 0.5 places a straight coast exactly on the map edge; higher values
    /// pull the coastline inland (more visible sea), lower values push it outward.
    ///
    /// Out-of-world neighbors sample as water (0). Interior tiles produce all-ON
    /// masks; ocean tiles all-OFF (ocean tiles are zoomable by design).
    ///
    /// Determinism: pure float math in fixed row-major order; no RNG, no state.
    /// Ownership: the returned MaskGrid2D is owned by the caller (F2c contract —
    /// keep alive for the full pipeline run, then Dispose).
    /// </summary>
    public static class ShapeMaskBuilder2D
    {
        public const float DefaultThreshold01 = 0.5f;

        /// <summary>
        /// Samples the 3×3 land/water neighborhood of (tileX, tileY) into samples9,
        /// row-major (dy=-1 row first): samples9[(dy+1)*3 + (dx+1)] = 1f land, 0f water.
        /// Out-of-bounds neighbors are water. Throws if the tile itself is out of range.
        /// </summary>
        public static void SampleNeighborhood3x3(
            in MaskGrid2D worldLand, int tileX, int tileY, Span<float> samples9)
        {
            if (!worldLand.IsCreated)
                throw new ArgumentException("World land mask is not created.", nameof(worldLand));
            if (samples9.Length < 9)
                throw new ArgumentException("samples9 must hold at least 9 values.", nameof(samples9));

            GridDomain2D wd = worldLand.Domain;
            if ((uint)tileX >= (uint)wd.Width || (uint)tileY >= (uint)wd.Height)
                throw new ArgumentOutOfRangeException(nameof(tileX),
                    $"Tile ({tileX},{tileY}) outside world domain {wd.Width}x{wd.Height}.");

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int wx = tileX + dx;
                    int wy = tileY + dy;
                    bool land = wd.InBounds(wx, wy) && worldLand.GetUnchecked(wx, wy);
                    samples9[(dy + 1) * 3 + (dx + 1)] = land ? 1f : 0f;
                }
            }
        }

        /// <summary>
        /// Builds a local-resolution mask from 9 neighborhood samples via
        /// bilinear interpolation + threshold. Caller owns the returned mask.
        /// </summary>
        public static MaskGrid2D BuildFromNeighborhood3x3(
            in GridDomain2D localDomain, ReadOnlySpan<float> samples9,
            float threshold01, Allocator allocator)
        {
            if (samples9.Length < 9)
                throw new ArgumentException("samples9 must hold at least 9 values.", nameof(samples9));

            float th = math.saturate(threshold01);
            var mask = new MaskGrid2D(localDomain, allocator, clearToZero: true);

            int w = localDomain.Width;
            int h = localDomain.Height;
            float invW = 1f / w;
            float invH = 1f / h;

            for (int y = 0; y < h; y++)
            {
                float sy = 0.5f + (y + 0.5f) * invH;   // lattice coord, in (0.5, 1.5)
                int iy = sy >= 1f ? 1 : 0;
                float ty = sy - iy;
                int row0 = iy * 3;
                int row1 = row0 + 3;

                for (int x = 0; x < w; x++)
                {
                    float sx = 0.5f + (x + 0.5f) * invW;
                    int ix = sx >= 1f ? 1 : 0;
                    float tx = sx - ix;

                    float a = math.lerp(samples9[row0 + ix], samples9[row0 + ix + 1], tx);
                    float b = math.lerp(samples9[row1 + ix], samples9[row1 + ix + 1], tx);
                    float v = math.lerp(a, b, ty);

                    if (v >= th)
                        mask.SetUnchecked(x, y, true);
                }
            }
            return mask;
        }

        /// <summary>Convenience: sample the world tile's neighborhood and build in one call.</summary>
        public static MaskGrid2D BuildFromWorldTile(
            in MaskGrid2D worldLand, int tileX, int tileY,
            in GridDomain2D localDomain, float threshold01, Allocator allocator)
        {
            Span<float> s = stackalloc float[9];
            SampleNeighborhood3x3(in worldLand, tileX, tileY, s);
            return BuildFromNeighborhood3x3(in localDomain, s, threshold01, allocator);
        }
    }
}