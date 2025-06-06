using Unity.Burst;

namespace Islands
{
    using static Noise;

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public static class NoiseBurstExplicitCompilation
    {
        static ScheduleDelegate[,] noiseJobs = {
        {
            Job<Lattice1D<LatticeNormal, Perlin>>.ScheduleParallel,
            Job<Lattice1D<LatticeTiling, Perlin>>.ScheduleParallel,
            Job<Lattice2D<LatticeNormal, Perlin>>.ScheduleParallel,
            Job<Lattice2D<LatticeTiling, Perlin>>.ScheduleParallel,
            Job<Lattice3D<LatticeNormal, Perlin>>.ScheduleParallel,
            Job<Lattice3D<LatticeTiling, Perlin>>.ScheduleParallel
        },
        {
            Job<Lattice1D<LatticeNormal, Turbulence<Perlin>>>.ScheduleParallel,
            Job<Lattice1D<LatticeTiling, Turbulence<Perlin>>>.ScheduleParallel,
            Job<Lattice2D<LatticeNormal, Turbulence<Perlin>>>.ScheduleParallel,
            Job<Lattice2D<LatticeTiling, Turbulence<Perlin>>>.ScheduleParallel,
            Job<Lattice3D<LatticeNormal, Turbulence<Perlin>>>.ScheduleParallel,
            Job<Lattice3D<LatticeTiling, Turbulence<Perlin>>>.ScheduleParallel
        },
        {
            Job<Lattice1D<LatticeNormal, Value>>.ScheduleParallel,
            Job<Lattice1D<LatticeTiling, Value>>.ScheduleParallel,
            Job<Lattice2D<LatticeNormal, Value>>.ScheduleParallel,
            Job<Lattice2D<LatticeTiling, Value>>.ScheduleParallel,
            Job<Lattice3D<LatticeNormal, Value>>.ScheduleParallel,
            Job<Lattice3D<LatticeTiling, Value>>.ScheduleParallel
        },
        {
            Job<Lattice1D<LatticeNormal, Turbulence<Value>>>.ScheduleParallel,
            Job<Lattice1D<LatticeTiling, Turbulence<Value>>>.ScheduleParallel,
            Job<Lattice2D<LatticeNormal, Turbulence<Value>>>.ScheduleParallel,
            Job<Lattice2D<LatticeTiling, Turbulence<Value>>>.ScheduleParallel,
            Job<Lattice3D<LatticeNormal, Turbulence<Value>>>.ScheduleParallel,
            Job<Lattice3D<LatticeTiling, Turbulence<Value>>>.ScheduleParallel
        },
        {
            Job<Simplex1D<Simplex>>.ScheduleParallel,
            Job<Simplex1D<Simplex>>.ScheduleParallel,
            Job<Simplex2D<Simplex>>.ScheduleParallel,
            Job<Simplex2D<Simplex>>.ScheduleParallel,
            Job<Simplex3D<Simplex>>.ScheduleParallel,
            Job<Simplex3D<Simplex>>.ScheduleParallel
        },
        {
            Job<Simplex1D<Turbulence<Simplex>>>.ScheduleParallel,
            Job<Simplex1D<Turbulence<Simplex>>>.ScheduleParallel,
            Job<Simplex2D<Turbulence<Simplex>>>.ScheduleParallel,
            Job<Simplex2D<Turbulence<Simplex>>>.ScheduleParallel,
            Job<Simplex3D<Turbulence<Simplex>>>.ScheduleParallel,
            Job<Simplex3D<Turbulence<Simplex>>>.ScheduleParallel
        },
        {
            Job<Simplex1D<Value>>.ScheduleParallel,
            Job<Simplex1D<Value>>.ScheduleParallel,
            Job<Simplex2D<Value>>.ScheduleParallel,
            Job<Simplex2D<Value>>.ScheduleParallel,
            Job<Simplex3D<Value>>.ScheduleParallel,
            Job<Simplex3D<Value>>.ScheduleParallel
        },
        {
            Job<Simplex1D<Turbulence<Value>>>.ScheduleParallel,
            Job<Simplex1D<Turbulence<Value>>>.ScheduleParallel,
            Job<Simplex2D<Turbulence<Value>>>.ScheduleParallel,
            Job<Simplex2D<Turbulence<Value>>>.ScheduleParallel,
            Job<Simplex3D<Turbulence<Value>>>.ScheduleParallel,
            Job<Simplex3D<Turbulence<Value>>>.ScheduleParallel
        },
        {
            Job<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            Job<Voronoi1D<LatticeTiling, Worley, F1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeTiling, Worley, F1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeTiling, Worley, F1>>.ScheduleParallel
        },
        {
            Job<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            Job<Voronoi1D<LatticeTiling, Worley, F2>>.ScheduleParallel,
            Job<Voronoi2D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            Job<Voronoi2D<LatticeTiling, Worley, F2>>.ScheduleParallel,
            Job<Voronoi3D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            Job<Voronoi3D<LatticeTiling, Worley, F2>>.ScheduleParallel
        },
        {
            Job<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi1D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel
        },
        {
            Job<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
            Job<Voronoi1D<LatticeTiling, Worley, F1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeTiling, Chebyshev, F1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeTiling, Chebyshev, F1>>.ScheduleParallel
        },
        {
            Job<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
            Job<Voronoi1D<LatticeTiling, Worley, F2>>.ScheduleParallel,
            Job<Voronoi2D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
            Job<Voronoi2D<LatticeTiling, Chebyshev, F2>>.ScheduleParallel,
            Job<Voronoi3D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
            Job<Voronoi3D<LatticeTiling, Chebyshev, F2>>.ScheduleParallel
        },
        {
            Job<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi1D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi2D<LatticeTiling, Chebyshev, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
            Job<Voronoi3D<LatticeTiling, Chebyshev, F2MinusF1>>.ScheduleParallel
        },
    };
    }
}