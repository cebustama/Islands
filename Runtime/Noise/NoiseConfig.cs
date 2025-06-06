// Written by ChatGPT o3, adapted by Claudio
using UnityEngine;

namespace Islands
{
    public enum NoiseCategory { Value, Perlin, Voronoi, Simplex, SimplexValue }
    public enum VoronoiDistance { Worley, Chebyshev }
    public enum VoronoiFunction { F1, F2, F2MinusF1 }

    [CreateAssetMenu(menuName = "Islands/Noise/Noise Config", fileName = "New Noise Config")]
    public class NoiseConfig : ScriptableObject
    {
        // --------- generic selection ------------------------------------------------
        public NoiseCategory category = NoiseCategory.Perlin;
        [Range(1, 3)] public int dimensions = 3;
        public bool tiling;
        public bool turbulence;               // ignored by Voronoi

        // --------- Voronoi-specific knobs ------------------------------------------
        public VoronoiDistance voronoiDistance;
        public VoronoiFunction voronoiFunction;

        // --------- core numeric parameters -----------------------------------------
        public Noise.Settings settings = Noise.Settings.Default;

        // --------- API helpers ------------------------------------------------------
        public bool NeedsVoronoiExtras => category == NoiseCategory.Voronoi;
        public bool SupportsTurbulence =>
            category == NoiseCategory.Perlin ||
            category == NoiseCategory.Value ||
            category == NoiseCategory.Simplex ||
            category == NoiseCategory.SimplexValue;

        public struct Key
        {
            public readonly NoiseCategory cat;
            public readonly int dim;
            public readonly bool tiling;
            public readonly bool turbulence;
            public readonly VoronoiDistance dist;
            public readonly VoronoiFunction func;

            public Key(NoiseConfig c)
            {
                cat = c.category;
                dim = c.dimensions;
                tiling = c.tiling;
                turbulence = c.turbulence && c.SupportsTurbulence;
                dist = c.voronoiDistance;
                func = c.voronoiFunction;
            }

            // Non-Voronoi constructor
            public Key(NoiseCategory cat, int dim, bool tiling, bool turbulence)
            {
                this.cat = cat;
                this.dim = dim;
                this.tiling = tiling;
                this.turbulence = turbulence;
                dist = VoronoiDistance.Worley;
                func = VoronoiFunction.F1;
            }

            // Voronoi constructor
            public Key(NoiseCategory cat, int dim, bool tiling, bool turbulence,
                VoronoiDistance dist, VoronoiFunction func)
            {
                this.cat = cat;
                this.dim = dim;
                this.tiling = tiling;
                this.turbulence = turbulence;
                this.dist = dist;
                this.func = func;
            }
        }
    }
}