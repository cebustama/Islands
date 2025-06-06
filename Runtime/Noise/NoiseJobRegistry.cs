// Written by ChatGPT o3, adapted by Claudio
using System.Collections.Generic;
using UnityEngine;

namespace Islands
{
    using static Noise;

    [CreateAssetMenu(menuName = "Islands/Noise/Noise Job Registry")]
    public class NoiseJobRegistry : ScriptableObject
    {
        #region singleton bootstrap ------------------------------------------------

        private static NoiseJobRegistry _instance;
        public static NoiseJobRegistry Instance
        {
            get
            {
                // When compiled, there is no ScriptableSingleton, so do it manually
                if (_instance == null)
                    _instance = Resources.Load<NoiseJobRegistry>("Noise/NoiseJobRegistry");
#if UNITY_EDITOR
            if (_instance == null)
                Debug.LogError("Create a NoiseJobRegistry asset in a Resources folder");
#endif
                return _instance;
            }
        }

        #endregion
        //--------------------------------------------------------------------------

        static readonly Dictionary<NoiseConfig.Key, ScheduleDelegate> _map = BuildMap();

        static Dictionary<NoiseConfig.Key, ScheduleDelegate> BuildMap()
        {
            var map = new Dictionary<NoiseConfig.Key, ScheduleDelegate>(256);

            // Lattice-based Noises (Value, Perlin)
            void AddLattice<G>(NoiseCategory cat, bool turbulence) where G : struct, IGradient
            {
                for (int dim = 1; dim <= 3; dim++)
                {
                    if (turbulence)
                    {
                        map.Add(new(cat, dim, false, true),
                            dim switch
                            {
                                1 => Job<Lattice1D<LatticeNormal, Turbulence<G>>>.ScheduleParallel,
                                2 => Job<Lattice2D<LatticeNormal, Turbulence<G>>>.ScheduleParallel,
                                _ => Job<Lattice3D<LatticeNormal, Turbulence<G>>>.ScheduleParallel
                            });
                        map.Add(new(cat, dim, true, true),
                            dim switch
                            {
                                1 => Job<Lattice1D<LatticeTiling, Turbulence<G>>>.ScheduleParallel,
                                2 => Job<Lattice2D<LatticeTiling, Turbulence<G>>>.ScheduleParallel,
                                _ => Job<Lattice3D<LatticeTiling, Turbulence<G>>>.ScheduleParallel
                            });
                    }
                    else
                    {
                        map.Add(new(cat, dim, false, false),
                            dim switch
                            {
                                1 => Job<Lattice1D<LatticeNormal, G>>.ScheduleParallel,
                                2 => Job<Lattice2D<LatticeNormal, G>>.ScheduleParallel,
                                _ => Job<Lattice3D<LatticeNormal, G>>.ScheduleParallel
                            });
                        map.Add(new(cat, dim, true, false),
                            dim switch
                            {
                                1 => Job<Lattice1D<LatticeTiling, G>>.ScheduleParallel,
                                2 => Job<Lattice2D<LatticeTiling, G>>.ScheduleParallel,
                                _ => Job<Lattice3D<LatticeTiling, G>>.ScheduleParallel
                            });
                    }
                }
            }

            // Simplex-based Noises
            void AddSimplex<G>(NoiseCategory cat, bool turbulence) where G : struct, IGradient
            {
                for (int dim = 1; dim <= 3; dim++)
                {
                    if (turbulence)
                    {
                        map.Add(new(cat, dim, false, true),
                            dim switch
                            {
                                1 => Job<Simplex1D<Turbulence<G>>>.ScheduleParallel,
                                2 => Job<Simplex2D<Turbulence<G>>>.ScheduleParallel,
                                _ => Job<Simplex3D<Turbulence<G>>>.ScheduleParallel
                            });
                    }
                    else
                    {
                        map.Add(new(cat, dim, false, false),
                            dim switch
                            {
                                1 => Job<Simplex1D<G>>.ScheduleParallel,
                                2 => Job<Simplex2D<G>>.ScheduleParallel,
                                _ => Job<Simplex3D<G>>.ScheduleParallel
                            });
                    }
                }
            }

            // Voronoi noise registration with explicit variations
            foreach (var dist in (VoronoiDistance[])System.Enum.GetValues(typeof(VoronoiDistance)))
                foreach (var func in (VoronoiFunction[])System.Enum.GetValues(typeof(VoronoiFunction)))
                    for (int dim = 1; dim <= 3; dim++)
                        foreach (var tiling in new[] { false, true })
                        {
                            map.Add(new(NoiseCategory.Voronoi, dim, tiling, false, dist, func),
                                (dim, dist, func, tiling) switch
                                {
                                    // 1D
                                    // Worley
                                    (1, VoronoiDistance.Worley, VoronoiFunction.F1, false) =>
                                        Job<Voronoi1D<LatticeNormal, Worley, F1>>.ScheduleParallel,
                                    (1, VoronoiDistance.Worley, VoronoiFunction.F2, false) =>
                                        Job<Voronoi1D<LatticeNormal, Worley, F2>>.ScheduleParallel,
                                    (1, VoronoiDistance.Worley, VoronoiFunction.F2MinusF1, false) =>
                                        Job<Voronoi1D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
                                    (1, VoronoiDistance.Worley, VoronoiFunction.F1, true) =>
                                        Job<Voronoi1D<LatticeTiling, Worley, F1>>.ScheduleParallel,
                                    (1, VoronoiDistance.Worley, VoronoiFunction.F2, true) =>
                                        Job<Voronoi1D<LatticeTiling, Worley, F2>>.ScheduleParallel,
                                    (1, VoronoiDistance.Worley, VoronoiFunction.F2MinusF1, true) =>
                                        Job<Voronoi1D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel,
                                    // Chebyshev
                                    (1, VoronoiDistance.Chebyshev, VoronoiFunction.F1, false) =>
                                        Job<Voronoi1D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
                                    (1, VoronoiDistance.Chebyshev, VoronoiFunction.F2, false) =>
                                        Job<Voronoi1D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
                                    (1, VoronoiDistance.Chebyshev, VoronoiFunction.F2MinusF1, false) =>
                                        Job<Voronoi1D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
                                    (1, VoronoiDistance.Chebyshev, VoronoiFunction.F1, true) =>
                                        Job<Voronoi1D<LatticeTiling, Chebyshev, F1>>.ScheduleParallel,
                                    (1, VoronoiDistance.Chebyshev, VoronoiFunction.F2, true) =>
                                        Job<Voronoi1D<LatticeTiling, Chebyshev, F2>>.ScheduleParallel,
                                    (1, VoronoiDistance.Chebyshev, VoronoiFunction.F2MinusF1, true) =>
                                        Job<Voronoi1D<LatticeTiling, Chebyshev, F2MinusF1>>.ScheduleParallel,
                                    // 2D
                                    // Worley
                                    (2, VoronoiDistance.Worley, VoronoiFunction.F1, false) =>
                                        Job<Voronoi2D<LatticeNormal, Worley, F1>>.ScheduleParallel,
                                    (2, VoronoiDistance.Worley, VoronoiFunction.F2, false) =>
                                        Job<Voronoi2D<LatticeNormal, Worley, F2>>.ScheduleParallel,
                                    (2, VoronoiDistance.Worley, VoronoiFunction.F2MinusF1, false) =>
                                        Job<Voronoi2D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
                                    (2, VoronoiDistance.Worley, VoronoiFunction.F1, true) =>
                                        Job<Voronoi2D<LatticeTiling, Worley, F1>>.ScheduleParallel,
                                    (2, VoronoiDistance.Worley, VoronoiFunction.F2, true) =>
                                        Job<Voronoi2D<LatticeTiling, Worley, F2>>.ScheduleParallel,
                                    (2, VoronoiDistance.Worley, VoronoiFunction.F2MinusF1, true) =>
                                        Job<Voronoi2D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel,
                                    // Chebyshev
                                    (2, VoronoiDistance.Chebyshev, VoronoiFunction.F1, false) =>
                                        Job<Voronoi2D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
                                    (2, VoronoiDistance.Chebyshev, VoronoiFunction.F2, false) =>
                                        Job<Voronoi2D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
                                    (2, VoronoiDistance.Chebyshev, VoronoiFunction.F2MinusF1, false) =>
                                        Job<Voronoi2D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
                                    (2, VoronoiDistance.Chebyshev, VoronoiFunction.F1, true) =>
                                        Job<Voronoi2D<LatticeTiling, Chebyshev, F1>>.ScheduleParallel,
                                    (2, VoronoiDistance.Chebyshev, VoronoiFunction.F2, true) =>
                                        Job<Voronoi2D<LatticeTiling, Chebyshev, F2>>.ScheduleParallel,
                                    (2, VoronoiDistance.Chebyshev, VoronoiFunction.F2MinusF1, true) =>
                                        Job<Voronoi2D<LatticeTiling, Chebyshev, F2MinusF1>>.ScheduleParallel,
                                    // 3D
                                    // Worley
                                    (3, VoronoiDistance.Worley, VoronoiFunction.F1, false) =>
                                        Job<Voronoi3D<LatticeNormal, Worley, F1>>.ScheduleParallel,
                                    (3, VoronoiDistance.Worley, VoronoiFunction.F2, false) =>
                                        Job<Voronoi3D<LatticeNormal, Worley, F2>>.ScheduleParallel,
                                    (3, VoronoiDistance.Worley, VoronoiFunction.F2MinusF1, false) =>
                                        Job<Voronoi3D<LatticeNormal, Worley, F2MinusF1>>.ScheduleParallel,
                                    (3, VoronoiDistance.Worley, VoronoiFunction.F1, true) =>
                                        Job<Voronoi3D<LatticeTiling, Worley, F1>>.ScheduleParallel,
                                    (3, VoronoiDistance.Worley, VoronoiFunction.F2, true) =>
                                        Job<Voronoi3D<LatticeTiling, Worley, F2>>.ScheduleParallel,
                                    (3, VoronoiDistance.Worley, VoronoiFunction.F2MinusF1, true) =>
                                        Job<Voronoi3D<LatticeTiling, Worley, F2MinusF1>>.ScheduleParallel,
                                    // Chebyshev
                                    (3, VoronoiDistance.Chebyshev, VoronoiFunction.F1, false) =>
                                        Job<Voronoi3D<LatticeNormal, Chebyshev, F1>>.ScheduleParallel,
                                    (3, VoronoiDistance.Chebyshev, VoronoiFunction.F2, false) =>
                                        Job<Voronoi3D<LatticeNormal, Chebyshev, F2>>.ScheduleParallel,
                                    (3, VoronoiDistance.Chebyshev, VoronoiFunction.F2MinusF1, false) =>
                                        Job<Voronoi3D<LatticeNormal, Chebyshev, F2MinusF1>>.ScheduleParallel,
                                    (3, VoronoiDistance.Chebyshev, VoronoiFunction.F1, true) =>
                                        Job<Voronoi3D<LatticeTiling, Chebyshev, F1>>.ScheduleParallel,
                                    (3, VoronoiDistance.Chebyshev, VoronoiFunction.F2, true) =>
                                        Job<Voronoi3D<LatticeTiling, Chebyshev, F2>>.ScheduleParallel,
                                    (3, VoronoiDistance.Chebyshev, VoronoiFunction.F2MinusF1, true) =>
                                        Job<Voronoi3D<LatticeTiling, Chebyshev, F2MinusF1>>.ScheduleParallel,
                                    _ => Job<Voronoi3D<LatticeNormal, Worley, F1>>.ScheduleParallel
                                });
                        }

            // Register all noise combinations
            AddLattice<Perlin>(NoiseCategory.Perlin, false);
            AddLattice<Perlin>(NoiseCategory.Perlin, true);
            AddLattice<Value>(NoiseCategory.Value, false);
            AddLattice<Value>(NoiseCategory.Value, true);

            AddSimplex<Simplex>(NoiseCategory.Simplex, false);
            AddSimplex<Simplex>(NoiseCategory.Simplex, true);
            AddSimplex<Value>(NoiseCategory.SimplexValue, false);
            AddSimplex<Value>(NoiseCategory.SimplexValue, true);

            return map;
        }

        public bool TryGet(NoiseConfig.Key key, out ScheduleDelegate schedule)
            => _map.TryGetValue(key, out schedule);
    }
}