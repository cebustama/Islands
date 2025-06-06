//https://catlikecoding.com/unity/tutorials/pseudorandom-noise/simplex-noise/
using Unity.Mathematics;

using static Unity.Mathematics.math;

namespace Islands
{
    public static partial class Noise
    {

        public struct Simplex1D<G> : INoise
            where G : struct, IGradient
        {

            public Sample4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency)
            {
                // Zoom sample into noise space
                positions *= frequency;
                // Find the two surrounding integer lattice points
                int4 x0 = (int4)floor(positions.c0), x1 = x0 + 1;

                // Compute each corner’s kernel, sum them, then apply final smoothing
                Sample4 s = default(G).EvaluateCombined(
                    Kernel(hash.Eat(x0), x0, positions) + Kernel(hash.Eat(x1), x1, positions)
                );
                s.dx *= frequency;
                return s;
            }

            // limiting the influence of each lattice point
            static Sample4 Kernel(SmallXXHash4 hash, float4 lx, float4x3 positions)
            {
                // Offset inside the cell
                float4 x = positions.c0 - lx;

                float4 f = 1f - x * x;
                Sample4 g = default(G).Evaluate(hash, x);
                return new Sample4
                {
                    // C²-smooth falloff: (1 – x²)³
                    v = f * f * f * g.v,
                    // Product rule
                    dx = f * f * (f * g.dx - 6f * x * g.v)
                };
            }
        }

        public struct Simplex2D<G> : INoise
            where G : struct, IGradient
        {

            public Sample4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency)
            {
                // scale down the frequency for ease of comparing with square-based versions
                positions *= frequency * (1f / sqrt(3f));
                // skew factor v = (3 - √3) / 6 ≈ 0.2113
                float4 skew = (positions.c0 + positions.c2) * ((sqrt(3f) - 1f) / 2f);
                float4 sx = positions.c0 + skew, sz = positions.c2 + skew;
                // now floor(sx), floor(sz) gives you
                // the “rhombus” cell indices you need.
                int4
                    x0 = (int4)floor(sx), x1 = x0 + 1,
                    z0 = (int4)floor(sz), z1 = z0 + 1;

                // whether the relative skewed X exceed Z. Which triangle of the rhombus?
                bool4 xGz = sx - x0 > sz - z0;
                // Pick the “third” corner (either (x1,z0) or (x0,z1))
                int4 xC = select(x0, x1, xGz), zC = select(z1, z0, xGz);

                // Hash the three X-corners, but pick h0 or h1 *before* final avalanche
                SmallXXHash4
                    h0 = hash.Eat(x0), h1 = hash.Eat(x1),
                    hC = SmallXXHash4.Select(h0, h1, xGz);

                // Feed each X-hash into the Z coordinate, call Kernel, sum, final eval
                Sample4 s = default(G).EvaluateCombined(
                    Kernel(h0.Eat(z0), x0, z0, positions) +
                    Kernel(h1.Eat(z1), x1, z1, positions) +
                    Kernel(hC.Eat(zC), xC, zC, positions)
                );
                s.dx *= frequency * (1f / sqrt(3f));
                s.dz *= frequency * (1f / sqrt(3f));
                return s;
            }

            static Sample4 Kernel(
                SmallXXHash4 hash, float4 lx, float4 lz, float4x3 positions
            )
            {
                float4 unskew = (lx + lz) * ((3f - sqrt(3f)) / 6f);
                float4 x = positions.c0 - lx + unskew, z = positions.c2 - lz + unskew;
                float4 f = 0.5f - x * x - z * z;
                Sample4 g = default(G).Evaluate(hash, x, z);
                return new Sample4
                {
                    v = f * g.v,
                    dx = f * g.dx - 6f * x * g.v,
                    dz = f * g.dz - 6f * z * g.v
                } * f * f * select(0f, 8f, f >= 0f);
            }
        }

        public struct Simplex3D<G> : INoise
            where G : struct, IGradient
        {

            public Sample4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency)
            {
                positions *= frequency * 0.6f;
                float4 skew = (positions.c0 + positions.c1 + positions.c2) * (1f / 3f);
                float4
                    sx = positions.c0 + skew,
                    sy = positions.c1 + skew,
                    sz = positions.c2 + skew;

                // Integer cell in skewed space
                int4
                    x0 = (int4)floor(sx), x1 = x0 + 1,
                    y0 = (int4)floor(sy), y1 = y0 + 1,
                    z0 = (int4)floor(sz), z1 = z0 + 1;

                // Which of the six tetrahedra inside this cube?
                bool4
                    xGy = sx - x0 > sy - y0,
                    xGz = sx - x0 > sz - z0,
                    yGz = sy - y0 > sz - z0;

                // two per-axis booleans xA/xB, yA/yB, zA/zB
                bool4
                    xA = xGy & xGz,
                    xB = xGy | (xGz & yGz),
                    yA = !xGy & yGz,
                    yB = !xGy | (xGz & yGz),
                    zA = (xGy & !xGz) | (!xGy & !yGz),
                    zB = !(xGz & yGz);

                // Use those to pick two “variable” offsets along each axis
                int4
                    xCA = select(x0, x1, xA),
                    xCB = select(x0, x1, xB),
                    yCA = select(y0, y1, yA),
                    yCB = select(y0, y1, yB),
                    zCA = select(z0, z1, zA),
                    zCB = select(z0, z1, zB);

                // // We need four corner hashes:
                //   • (x0,y0,z0)
                //   • (x1,y1,z1)
                //   • (xCA,yCA,zCA)
                //   • (xCB,yCB,zCB)
                SmallXXHash4
                    h0 = hash.Eat(x0), h1 = hash.Eat(x1),
                    // Pick partial hash for “A” and “B” variants (no avalanche yet):
                    hA = SmallXXHash4.Select(h0, h1, xA),
                    hB = SmallXXHash4.Select(h0, h1, xB);

                Sample4 s = default(G).EvaluateCombined(
                    // corner (x0,y0,z0)
                    Kernel(h0.Eat(y0).Eat(z0), x0, y0, z0, positions) +
                    // corner (x1,y1,z1)
                    Kernel(h1.Eat(y1).Eat(z1), x1, y1, z1, positions) +
                    // corner A
                    Kernel(hA.Eat(yCA).Eat(zCA), xCA, yCA, zCA, positions) +
                    // corner B
                    Kernel(hB.Eat(yCB).Eat(zCB), xCB, yCB, zCB, positions)
                );
                s.dx *= frequency * 0.6f;
                s.dy *= frequency * 0.6f;
                s.dz *= frequency * 0.6f;
                return s;
            }

            static Sample4 Kernel(
                SmallXXHash4 hash, float4 lx, float4 ly, float4 lz, float4x3 positions
            )
            {
                // Un-skew back into real simplex space
                float4 unskew = (lx + ly + lz) * (1f / 6f);

                float4
                    x = positions.c0 - lx + unskew,
                    y = positions.c1 - ly + unskew,
                    z = positions.c2 - lz + unskew;

                // Radial C² falloff: f = (½ – (x²+y²+z²))³ * 8
                float4 f = 0.5f - x * x - y * y - z * z;
                Sample4 g = default(G).Evaluate(hash, x, y, z);
                return new Sample4
                {
                    v = f * g.v,
                    dx = f * g.dx - 6f * x * g.v,
                    dy = f * g.dy - 6f * y * g.v,
                    dz = f * g.dz - 6f * z * g.v
                } * f * f * select(0f, 8f, f >= 0f);
            }
        }
    }
}