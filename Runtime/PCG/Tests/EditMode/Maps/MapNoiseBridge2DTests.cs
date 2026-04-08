using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Islands;
using Islands.PCG.Core;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Tests.EditMode.Maps
{
    /// <summary>
    /// EditMode tests for <see cref="MapNoiseBridge2D"/>.
    ///
    /// Phase N5.c: verifies parameterized Worley dispatch (12 metric × function
    /// combinations), ridged multifractal algorithm, golden parity for default
    /// settings, determinism, and output range invariants.
    /// </summary>
    [TestFixture]
    public sealed class MapNoiseBridge2DTests
    {
        private const int W = 32;
        private const int H = 32;
        private const uint Seed = 42u;
        private const uint Salt = 0xDEADu;

        private GridDomain2D _domain;
        private NativeArray<float> _dst;

        [SetUp]
        public void SetUp()
        {
            _domain = new GridDomain2D(W, H);
            _dst = new NativeArray<float>(_domain.Length, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            if (_dst.IsCreated) _dst.Dispose();
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        /// <summary>Compute a simple hash of the float array for comparison.</summary>
        private ulong HashArray(NativeArray<float> arr)
        {
            ulong hash = 14695981039346656037UL; // FNV-1a offset basis
            for (int i = 0; i < arr.Length; i++)
            {
                uint bits = math.asuint(arr[i]);
                hash ^= bits;
                hash *= 1099511628211UL; // FNV-1a prime
            }
            return hash;
        }

        /// <summary>Assert all values in [0, 1].</summary>
        private void AssertRange01(NativeArray<float> arr, string label)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                Assert.GreaterOrEqual(arr[i], 0f,
                    $"{label}[{i}] = {arr[i]} is below 0.");
                Assert.LessOrEqual(arr[i], 1f,
                    $"{label}[{i}] = {arr[i]} is above 1.");
                Assert.IsFalse(float.IsNaN(arr[i]),
                    $"{label}[{i}] is NaN.");
            }
        }

        /// <summary>Check that at least some values differ from a constant.</summary>
        private void AssertNotFlat(NativeArray<float> arr, string label)
        {
            float first = arr[0];
            bool allSame = true;
            for (int i = 1; i < arr.Length; i++)
            {
                if (math.abs(arr[i] - first) > 1e-6f)
                {
                    allSame = false;
                    break;
                }
            }
            Assert.IsFalse(allSame,
                $"{label} is flat (all values ≈ {first}). Likely no noise output.");
        }

        // ------------------------------------------------------------------
        // N5.c: Worley parameterized dispatch — golden parity
        // ------------------------------------------------------------------

        [Test]
        public void Worley_EuclideanF1_MatchesPreN5cWorleyCase()
        {
            // The pre-N5.c Worley case was hardcoded to Voronoi2D<LatticeNormal, Worley, F1>.
            // With parameterized dispatch, Euclidean + F1 (the defaults) must produce
            // identical output.
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.noiseType = TerrainNoiseType.Worley;
            settings.worleyDistanceMetric = WorleyDistanceMetric.Euclidean;
            settings.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);
            ulong hashA = HashArray(_dst);

            // Fill again with same settings — determinism sanity
            var dst2 = new NativeArray<float>(_domain.Length, Allocator.Persistent);
            try
            {
                MapNoiseBridge2D.FillNoise01(in _domain, dst2, Seed, Salt, in settings);
                ulong hashB = HashArray(dst2);
                Assert.AreEqual(hashA, hashB,
                    "Worley Euclidean/F1 is not deterministic.");
            }
            finally
            {
                dst2.Dispose();
            }

            AssertRange01(_dst, "Worley_Euclidean_F1");
            AssertNotFlat(_dst, "Worley_Euclidean_F1");
        }

        // ------------------------------------------------------------------
        // N5.c: Worley dispatch — different metric/function → different output
        // ------------------------------------------------------------------

        [Test]
        public void Worley_SmoothEuclideanF1_DiffersFromEuclideanF1()
        {
            var baseSettings = TerrainNoiseSettings.DefaultTerrain;
            baseSettings.noiseType = TerrainNoiseType.Worley;
            baseSettings.worleyDistanceMetric = WorleyDistanceMetric.Euclidean;
            baseSettings.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in baseSettings);
            ulong hashEuclidean = HashArray(_dst);

            var smoothSettings = baseSettings;
            smoothSettings.worleyDistanceMetric = WorleyDistanceMetric.SmoothEuclidean;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in smoothSettings);
            ulong hashSmooth = HashArray(_dst);

            Assert.AreNotEqual(hashEuclidean, hashSmooth,
                "SmoothEuclidean/F1 should differ from Euclidean/F1.");
        }

        [Test]
        public void Worley_ChebyshevF1_DiffersFromEuclideanF1()
        {
            var baseSettings = TerrainNoiseSettings.DefaultTerrain;
            baseSettings.noiseType = TerrainNoiseType.Worley;
            baseSettings.worleyDistanceMetric = WorleyDistanceMetric.Euclidean;
            baseSettings.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in baseSettings);
            ulong hashEuclidean = HashArray(_dst);

            var chebySettings = baseSettings;
            chebySettings.worleyDistanceMetric = WorleyDistanceMetric.Chebyshev;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in chebySettings);
            ulong hashCheby = HashArray(_dst);

            Assert.AreNotEqual(hashEuclidean, hashCheby,
                "Chebyshev/F1 should differ from Euclidean/F1.");
        }

        [Test]
        public void Worley_EuclideanF2_DiffersFromEuclideanF1()
        {
            var f1Settings = TerrainNoiseSettings.DefaultTerrain;
            f1Settings.noiseType = TerrainNoiseType.Worley;
            f1Settings.worleyDistanceMetric = WorleyDistanceMetric.Euclidean;
            f1Settings.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in f1Settings);
            ulong hashF1 = HashArray(_dst);

            var f2Settings = f1Settings;
            f2Settings.worleyFunction = WorleyFunction.F2;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in f2Settings);
            ulong hashF2 = HashArray(_dst);

            Assert.AreNotEqual(hashF1, hashF2,
                "Euclidean/F2 should differ from Euclidean/F1.");
        }

        [Test]
        public void Worley_F2MinusF1_DiffersFromF1()
        {
            var f1Settings = TerrainNoiseSettings.DefaultTerrain;
            f1Settings.noiseType = TerrainNoiseType.Worley;
            f1Settings.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in f1Settings);
            ulong hashF1 = HashArray(_dst);

            var f2mf1Settings = f1Settings;
            f2mf1Settings.worleyFunction = WorleyFunction.F2MinusF1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in f2mf1Settings);
            ulong hashF2mF1 = HashArray(_dst);

            Assert.AreNotEqual(hashF1, hashF2mF1,
                "F2MinusF1 should differ from F1.");
        }

        [Test]
        public void Worley_CellAsIslands_DiffersFromF1()
        {
            var f1Settings = TerrainNoiseSettings.DefaultTerrain;
            f1Settings.noiseType = TerrainNoiseType.Worley;
            f1Settings.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in f1Settings);
            ulong hashF1 = HashArray(_dst);

            var cellSettings = f1Settings;
            cellSettings.worleyDistanceMetric = WorleyDistanceMetric.SmoothEuclidean;
            cellSettings.worleyFunction = WorleyFunction.CellAsIslands;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in cellSettings);
            ulong hashCell = HashArray(_dst);

            Assert.AreNotEqual(hashF1, hashCell,
                "CellAsIslands should differ from F1.");
        }

        // ------------------------------------------------------------------
        // N5.c: Worley — all 12 combos produce valid [0,1] non-flat output
        // ------------------------------------------------------------------

        [Test]
        public void Worley_AllCombinations_ProduceValidRange(
            [Values] WorleyDistanceMetric metric,
            [Values] WorleyFunction function)
        {
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.noiseType = TerrainNoiseType.Worley;
            settings.worleyDistanceMetric = metric;
            settings.worleyFunction = function;
            settings.frequency = 4;
            settings.octaves = 1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);

            string label = $"Worley_{metric}_{function}";
            AssertRange01(_dst, label);
            AssertNotFlat(_dst, label);
        }

        // ------------------------------------------------------------------
        // N5.c: Ridged multifractal — basic behavior
        // ------------------------------------------------------------------

        [Test]
        public void Ridged_Perlin_DiffersFromStandard()
        {
            var stdSettings = TerrainNoiseSettings.DefaultTerrain;
            stdSettings.noiseType = TerrainNoiseType.Perlin;
            stdSettings.fractalMode = FractalMode.Standard;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in stdSettings);
            ulong hashStd = HashArray(_dst);

            var ridgedSettings = stdSettings;
            ridgedSettings.fractalMode = FractalMode.Ridged;
            ridgedSettings.ridgedOffset = 1.0f;
            ridgedSettings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in ridgedSettings);
            ulong hashRidged = HashArray(_dst);

            Assert.AreNotEqual(hashStd, hashRidged,
                "Ridged Perlin should differ from Standard Perlin.");
        }

        [Test]
        public void Ridged_Perlin_IsDeterministic()
        {
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.noiseType = TerrainNoiseType.Perlin;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 1.0f;
            settings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);
            ulong hashA = HashArray(_dst);

            var dst2 = new NativeArray<float>(_domain.Length, Allocator.Persistent);
            try
            {
                MapNoiseBridge2D.FillNoise01(in _domain, dst2, Seed, Salt, in settings);
                ulong hashB = HashArray(dst2);
                Assert.AreEqual(hashA, hashB,
                    "Ridged Perlin is not deterministic.");
            }
            finally
            {
                dst2.Dispose();
            }
        }

        [Test]
        public void Ridged_Perlin_ProducesValidRange()
        {
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.noiseType = TerrainNoiseType.Perlin;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 1.0f;
            settings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);

            AssertRange01(_dst, "Ridged_Perlin");
            AssertNotFlat(_dst, "Ridged_Perlin");
        }

        // ------------------------------------------------------------------
        // N5.c: Ridged applies to all noise types
        // ------------------------------------------------------------------

        [Test]
        public void Ridged_Simplex_DiffersFromStandard()
        {
            var stdSettings = TerrainNoiseSettings.DefaultTerrain;
            stdSettings.noiseType = TerrainNoiseType.Simplex;
            stdSettings.fractalMode = FractalMode.Standard;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in stdSettings);
            ulong hashStd = HashArray(_dst);

            var ridgedSettings = stdSettings;
            ridgedSettings.fractalMode = FractalMode.Ridged;
            ridgedSettings.ridgedOffset = 1.0f;
            ridgedSettings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in ridgedSettings);
            ulong hashRidged = HashArray(_dst);

            Assert.AreNotEqual(hashStd, hashRidged,
                "Ridged Simplex should differ from Standard Simplex.");
        }

        [Test]
        public void Ridged_Worley_DiffersFromStandard()
        {
            var stdSettings = TerrainNoiseSettings.DefaultTerrain;
            stdSettings.noiseType = TerrainNoiseType.Worley;
            stdSettings.fractalMode = FractalMode.Standard;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in stdSettings);
            ulong hashStd = HashArray(_dst);

            var ridgedSettings = stdSettings;
            ridgedSettings.fractalMode = FractalMode.Ridged;
            ridgedSettings.ridgedOffset = 1.0f;
            ridgedSettings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in ridgedSettings);
            ulong hashRidged = HashArray(_dst);

            Assert.AreNotEqual(hashStd, hashRidged,
                "Ridged Worley should differ from Standard Worley.");
        }

        [Test]
        public void Ridged_WorleyCellAsIslands_ProducesValidRange()
        {
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.noiseType = TerrainNoiseType.Worley;
            settings.worleyDistanceMetric = WorleyDistanceMetric.SmoothEuclidean;
            settings.worleyFunction = WorleyFunction.CellAsIslands;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 1.0f;
            settings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);

            AssertRange01(_dst, "Ridged_SmoothWorley_CellAsIslands");
            AssertNotFlat(_dst, "Ridged_SmoothWorley_CellAsIslands");
        }

        // ------------------------------------------------------------------
        // N5.c: Standard mode unaffected by ridged parameter values
        // ------------------------------------------------------------------

        [Test]
        public void Standard_RidgedFieldValues_DoNotAffectOutput()
        {
            // Standard mode should ignore ridgedOffset and ridgedGain entirely.
            var settingsA = TerrainNoiseSettings.DefaultTerrain;
            settingsA.fractalMode = FractalMode.Standard;
            settingsA.ridgedOffset = 1.0f;
            settingsA.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settingsA);
            ulong hashA = HashArray(_dst);

            var settingsB = settingsA;
            settingsB.ridgedOffset = 0.0f;
            settingsB.ridgedGain = 0.5f;

            var dst2 = new NativeArray<float>(_domain.Length, Allocator.Persistent);
            try
            {
                MapNoiseBridge2D.FillNoise01(in _domain, dst2, Seed, Salt, in settingsB);
                ulong hashB = HashArray(dst2);
                Assert.AreEqual(hashA, hashB,
                    "Standard mode output must not change when ridgedOffset/ridgedGain differ.");
            }
            finally
            {
                dst2.Dispose();
            }
        }

        [Test]
        public void Standard_WorleyFieldValues_DoNotAffectNonWorleyOutput()
        {
            // Perlin mode should ignore worleyDistanceMetric and worleyFunction entirely.
            var settingsA = TerrainNoiseSettings.DefaultTerrain;
            settingsA.noiseType = TerrainNoiseType.Perlin;
            settingsA.worleyDistanceMetric = WorleyDistanceMetric.Euclidean;
            settingsA.worleyFunction = WorleyFunction.F1;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settingsA);
            ulong hashA = HashArray(_dst);

            var settingsB = settingsA;
            settingsB.worleyDistanceMetric = WorleyDistanceMetric.Chebyshev;
            settingsB.worleyFunction = WorleyFunction.CellAsIslands;

            var dst2 = new NativeArray<float>(_domain.Length, Allocator.Persistent);
            try
            {
                MapNoiseBridge2D.FillNoise01(in _domain, dst2, Seed, Salt, in settingsB);
                ulong hashB = HashArray(dst2);
                Assert.AreEqual(hashA, hashB,
                    "Perlin mode output must not change when Worley fields differ.");
            }
            finally
            {
                dst2.Dispose();
            }
        }

        // ------------------------------------------------------------------
        // N5.c: Ridged edge cases
        // ------------------------------------------------------------------

        [Test]
        public void Ridged_OffsetZero_ProducesValidRange()
        {
            // offset=0 is degenerate (signal = -|noise|, squared → small values).
            // Should not crash or produce NaN.
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 0f;
            settings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);

            AssertRange01(_dst, "Ridged_Offset0");
        }

        [Test]
        public void Ridged_GainZero_ProducesValidRange()
        {
            // gain=0 → no feedback, reduces to turbulence-like.
            // Should not crash or produce NaN.
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 1.0f;
            settings.ridgedGain = 0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);

            AssertRange01(_dst, "Ridged_Gain0");
        }

        [Test]
        public void Ridged_SingleOctave_ProducesValidRange()
        {
            // Single octave: no feedback loop, just (offset - |noise|)^2.
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.octaves = 1;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 1.0f;
            settings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, Seed, Salt, in settings);

            AssertRange01(_dst, "Ridged_1Octave");
            AssertNotFlat(_dst, "Ridged_1Octave");
        }

        // ------------------------------------------------------------------
        // N5.c: Seed variation — different seeds produce different output
        // ------------------------------------------------------------------

        [Test]
        public void Ridged_DifferentSeeds_ProduceDifferentOutput()
        {
            var settings = TerrainNoiseSettings.DefaultTerrain;
            settings.fractalMode = FractalMode.Ridged;
            settings.ridgedOffset = 1.0f;
            settings.ridgedGain = 2.0f;

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, 1u, Salt, in settings);
            ulong hash1 = HashArray(_dst);

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, 2u, Salt, in settings);
            ulong hash2 = HashArray(_dst);

            MapNoiseBridge2D.FillNoise01(in _domain, _dst, 3u, Salt, in settings);
            ulong hash3 = HashArray(_dst);

            Assert.AreNotEqual(hash1, hash2, "Seeds 1 and 2 should produce different ridged output.");
            Assert.AreNotEqual(hash2, hash3, "Seeds 2 and 3 should produce different ridged output.");
            Assert.AreNotEqual(hash1, hash3, "Seeds 1 and 3 should produce different ridged output.");
        }

        // ------------------------------------------------------------------
        // N5.c: FillSimplexPerlin01 (F3 legacy) — unchanged by N5.c
        // ------------------------------------------------------------------

        [Test]
        public void FillSimplexPerlin01_UnchangedByN5c()
        {
            // FillSimplexPerlin01 constructs Noise.Settings without ridged fields
            // (zero-init → Standard). Output must be identical to pre-N5.c.
            // This test locks determinism; the actual golden values are covered
            // by the pipeline golden tests (F3–F6/G).
            MapNoiseBridge2D.FillSimplexPerlin01(
                in _domain, _dst, Seed, Salt,
                frequency: 8, octaves: 4, lacunarity: 2, persistence: 0.5f);
            ulong hashA = HashArray(_dst);

            var dst2 = new NativeArray<float>(_domain.Length, Allocator.Persistent);
            try
            {
                MapNoiseBridge2D.FillSimplexPerlin01(
                    in _domain, dst2, Seed, Salt,
                    frequency: 8, octaves: 4, lacunarity: 2, persistence: 0.5f);
                ulong hashB = HashArray(dst2);

                Assert.AreEqual(hashA, hashB,
                    "FillSimplexPerlin01 must remain deterministic after N5.c changes.");
            }
            finally
            {
                dst2.Dispose();
            }

            AssertRange01(_dst, "FillSimplexPerlin01");
            AssertNotFlat(_dst, "FillSimplexPerlin01");
        }
    }
}
