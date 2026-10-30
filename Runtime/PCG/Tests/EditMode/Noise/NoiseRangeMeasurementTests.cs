using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Islands;
using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;

// Not "...EditMode.Noise": a namespace named Noise would shadow Islands.Noise in every
// sibling test namespace that writes Noise.Settings.
namespace Islands.PCG.Tests.EditMode.NoiseRange
{
    /// <summary>
    /// Measurement, not a golden gate: reports the real output range of each 2D noise
    /// (min, max, p1, p99, mean, SD) so the terrain thresholds can be reasoned about.
    /// Reference numbers come from the JS port (400 000 samples, 2026-10-30).
    ///
    /// Three views:
    /// 1. Single noise via <c>GetNoise4</c> (one octave, no fractal normalization).
    /// 2. 4-octave fBm two ways: as <see cref="Islands.Noise.GetFractalNoise{N}"/> computes it
    ///    today, and divided by the true amplitude sum (1 + p + p² + p³), which is what the
    ///    JS measurement did.
    /// 3. The pipeline view: <see cref="MapNoiseBridge2D.FillNoise01"/> with
    ///    <see cref="TerrainNoiseSettings.DefaultTerrain"/>, in [0,1] after remap and saturate.
    ///
    /// Results go to the test output (TestContext); the only assertions are sanity checks.
    /// </summary>
    [TestFixture]
    [Category("Measurement")]
    public sealed class NoiseRangeMeasurementTests
    {
        const int Side = 400;              // 400² × 3 seeds = 480 000 samples per noise
        const int Frequency = 32;
        static readonly int[] Seeds = { 56, 8, 243 };

        delegate float4 Sampler(float4x3 positions, int seed);

        [Test]
        public void SingleNoise_Ranges()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== Single noise, 1 octave, raw GetNoise4 (JS reference in brackets) ==");
            Report(sb, "Lattice2D<Value>", Single<Islands.Noise.Lattice2D<Islands.Noise.LatticeNormal, Islands.Noise.Value>>(), "");
            Report(sb, "Lattice2D<Perlin> (square)", Single<Islands.Noise.Lattice2D<Islands.Noise.LatticeNormal, Islands.Noise.Perlin>>(), "[±0.96, p1–p99 ±0.72]");
            Report(sb, "Simplex2D<Perlin>", Single<Islands.Noise.Simplex2D<Islands.Noise.Perlin>>(), "[±0.43, p1–p99 ±0.32]");
            Report(sb, "Simplex2D<PerlinOnSimplex>", Single<Islands.Noise.Simplex2D<Islands.Noise.PerlinOnSimplex>>(), "[expected peak ±1]");
            Report(sb, "Simplex2D<Simplex>", Single<Islands.Noise.Simplex2D<Islands.Noise.Simplex>>(), "[±1.00, p1–p99 ±0.86]");
            Report(sb, "Voronoi2D<Worley,F1>", Single<Islands.Noise.Voronoi2D<Islands.Noise.LatticeNormal, Islands.Noise.Worley, Islands.Noise.F1>>(), "");
            TestContext.WriteLine(sb.ToString());
        }

        [Test]
        public void Fbm4_Ranges_AsComputedVsTrueNormalization()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== fBm 4 octaves, lacunarity 2, persistence 0.5 ==");
            Report(sb, "Simplex2D<Perlin>  GetFractalNoise", Fractal<Islands.Noise.Simplex2D<Islands.Noise.Perlin>>(), "");
            Report(sb, "Simplex2D<Perlin>  true sum", TrueFbm<Islands.Noise.Simplex2D<Islands.Noise.Perlin>>(), "[±0.32, p1–p99 ±0.22]");
            Report(sb, "Simplex2D<PerlinOnSimplex> GetFractalNoise", Fractal<Islands.Noise.Simplex2D<Islands.Noise.PerlinOnSimplex>>(), "");
            Report(sb, "Simplex2D<Simplex> GetFractalNoise", Fractal<Islands.Noise.Simplex2D<Islands.Noise.Simplex>>(), "");
            Report(sb, "Simplex2D<Simplex> true sum", TrueFbm<Islands.Noise.Simplex2D<Islands.Noise.Simplex>>(), "[±0.86, p1–p99 ±0.59]");
            TestContext.WriteLine(sb.ToString());
        }

        [Test]
        public void Bridge_DefaultTerrain_PipelineView()
        {
            var sb = new StringBuilder();
            sb.AppendLine("== MapNoiseBridge2D.FillNoise01, res 256, values in [0,1] ==");
            foreach (var (name, settings) in new[]
            {
                ("DefaultTerrain (Perlin, f8, 4 oct)", TerrainNoiseSettings.DefaultTerrain),
                ("DefaultWarp    (Perlin, f4, 1 oct)", TerrainNoiseSettings.DefaultWarp),
                ("DefaultHills   (Perlin, f6, 2 oct)", TerrainNoiseSettings.DefaultHills),
                ("DefaultTerrain as Simplex", WithType(TerrainNoiseSettings.DefaultTerrain, TerrainNoiseType.Simplex)),
            })
            {
                var all = new List<float>();
                int at0 = 0, at1 = 0;
                foreach (int seed in Seeds)
                {
                    var domain = new GridDomain2D(256, 256);
                    using var dst = new NativeArray<float>(domain.Length, Allocator.Temp);
                    MapNoiseBridge2D.FillNoise01(domain, dst, (uint)seed, 0u, settings);
                    for (int i = 0; i < dst.Length; i++)
                    {
                        float v = dst[i];
                        if (v <= 0f) at0++;
                        if (v >= 1f) at1++;
                        all.Add(v);
                    }
                }
                Report(sb, name, all.ToArray(), $"clipped at 0: {at0}, at 1: {at1} of {all.Count}");
            }
            TestContext.WriteLine(sb.ToString());
        }

        // ------------------------------------------------------------------

        static TerrainNoiseSettings WithType(TerrainNoiseSettings s, TerrainNoiseType t)
        {
            s.noiseType = t;
            return s;
        }

        static float[] Single<N>() where N : struct, Islands.Noise.INoise =>
            Collect((p, seed) => default(N).GetNoise4(p, SmallXXHash4.Seed(seed), Frequency).v);

        static float[] Fractal<N>() where N : struct, Islands.Noise.INoise =>
            Collect((p, seed) => Islands.Noise.GetFractalNoise<N>(p, FbmSettings(seed)).v);

        static float[] TrueFbm<N>() where N : struct, Islands.Noise.INoise =>
            Collect((p, seed) =>
            {
                var s = FbmSettings(seed);
                var hash = SmallXXHash4.Seed(s.seed);
                int frequency = s.frequency;
                float amplitude = 1f, amplitudeSum = 0f;
                float4 sum = 0f;
                for (int o = 0; o < s.octaves; o++)
                {
                    sum += amplitude * default(N).GetNoise4(p, hash + o, frequency).v;
                    amplitudeSum += amplitude;
                    frequency *= s.lacunarity;
                    amplitude *= s.persistence;
                }
                return sum / amplitudeSum;
            });

        static Islands.Noise.Settings FbmSettings(int seed) => new Islands.Noise.Settings
        {
            seed = seed,
            frequency = Frequency,
            octaves = 4,
            lacunarity = 2,
            persistence = 0.5f,
            fractalMode = FractalMode.Standard,
        };

        static float[] Collect(Sampler sampler)
        {
            var values = new float[Side * Side * Seeds.Length];
            int k = 0;
            float inv = 1f / Side;
            foreach (int seed in Seeds)
            {
                for (int y = 0; y < Side; y++)
                {
                    float pz = (y + 0.5f) * inv;
                    for (int x = 0; x < Side; x += 4)
                    {
                        float4 px = (new float4(x, x + 1, x + 2, x + 3) + 0.5f) * inv;
                        float4 v = sampler(new float4x3(px, 0f, pz), seed);
                        values[k++] = v.x; values[k++] = v.y; values[k++] = v.z; values[k++] = v.w;
                    }
                }
            }
            return values;
        }

        static void Report(StringBuilder sb, string name, float[] v, string note)
        {
            double sum = 0, sum2 = 0;
            foreach (float f in v)
            {
                Assert.IsFalse(float.IsNaN(f) || float.IsInfinity(f), $"{name}: non-finite sample");
                sum += f; sum2 += (double)f * f;
            }
            double mean = sum / v.Length;
            double sd = Math.Sqrt(Math.Max(0, sum2 / v.Length - mean * mean));
            var sorted = (float[])v.Clone();
            Array.Sort(sorted);
            float p01 = sorted[(int)(0.01 * (sorted.Length - 1))];
            float p99 = sorted[(int)(0.99 * (sorted.Length - 1))];
            sb.AppendLine(
                $"{name,-38} min {sorted[0],8:F4}  max {sorted[^1],8:F4}  " +
                $"p1 {p01,8:F4}  p99 {p99,8:F4}  mean {mean,8:F4}  SD {sd,7:F4}  {note}");
        }
    }
}
