using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// Stage F5 / Phase M2.a — Vegetation mask (biome-aware).
    ///
    /// Quantile-mapping revision:
    ///   - Per-cell acceptance is a GLOBAL QUANTILE of the vegetation noise field,
    ///     computed over the eligible population, instead of the absolute threshold
    ///     1 - vegetationDensity. The absolute mapping was not a per-cell probability:
    ///     the fBm field occupies roughly [0.29, 0.76] with ~71% of its mass inside a
    ///     0.20-wide window, so thresholds above the field's support accepted zero
    ///     cells regardless of biome or terrain (measured seed 56 / res 256).
    ///   - Spatial character is unchanged: same salt, frequency, octaves, lacunarity,
    ///     persistence and quantization. Only the cut point moves.
    ///   - Legacy path (Biome field absent) keeps the absolute 0.40 threshold and is
    ///     now a structurally separate early-out, not a branch inside the main loop.
    ///
    /// Reads (read-only):
    ///   <see cref="MapLayerId.LandInterior"/> — eligibility
    ///   <see cref="MapLayerId.HillsL2"/>      — per-biome peak policy
    ///   <see cref="MapFieldId.Biome"/>        — per-cell biome ID (optional)
    ///   <see cref="MapFieldId.Moisture"/>     — moisture modulation (optional, inert by default)
    ///
    /// Writes (authoritative):
    ///   <see cref="MapLayerId.Vegetation"/>
    ///
    /// Invariants:
    ///   M2a-1: Vegetation ⊆ Land
    ///   M2a-2: Vegetation ⊆ LandInterior
    ///   M2a-3 (reformulated, W-aux.c block 3):
    ///     biome path: Vegetation ∩ HillsL2 ⊆ { cells whose biome has vegetatesOnPeaks }
    ///     legacy path (Biome field absent): Vegetation ∩ HillsL2 == ∅
    ///   M2a-4: Vegetation ∩ ShallowWater == ∅
    ///   M2a-5: Determinism (same seed + tunables → identical output)
    ///   M2a-6: No-mutate (all inputs including Biome, Moisture unchanged)
    ///   M2a-7: Biome-zero suppression (water cells never vegetated)
    ///   M2a-8: Snow / zero-density biome suppression
    ///   M2a-9 (REFORMULATED — global quantile cut). On the biome path:
    ///     Let E be the eligible population (LandInterior ∧ valid biome ∧ density &gt; 0
    ///     ∧ not blocked by the peak policy) and let bucket(c) = clamp(floor(noise01(c)
    ///     * QuantSteps), 0, QuantSteps-1). For density d, cut(d) is the largest bucket
    ///     k such that |{ c ∈ E : bucket(c) ≥ k }| ≥ ceil(d · |E|).
    ///       (a) Exactness: c ∈ E is vegetated ⟺ bucket(c) ≥ cut(density(c)).
    ///       (b) Nesting:   d1 &gt; d2 ⟹ cut(d1) ≤ cut(d2), hence the accepted sets are
    ///                      nested over E. This is a deterministic set-inclusion
    ///                      property, NOT the former statistical tendency.
    ///       (c) Floor:     realized coverage over E is ≥ d, never below. The excess is
    ///                      bounded by the population share of the cut bucket (whole
    ///                      buckets are accepted), NOT by 1/QuantSteps.
    ///       (d) NOT guaranteed: per-biome coverage equal to d. Biomes are cut against
    ///                      the global distribution, so a biome sitting in a trough of
    ///                      the noise field may fall short of its nominal density and
    ///                      one on a crest may exceed it. Measured seed 56 / res 256:
    ///                      Grassland 0.00% at d=0.15, TemperateDesert 12.83% at d=0.05.
    ///       (e) COUPLING:  cut(d) depends on E, so any change to the eligibility
    ///                      policy — including flipping vegetatesOnPeaks on a single
    ///                      biome — shifts the threshold of EVERY biome. This is
    ///                      contract surface, not an implementation detail.
    ///     Legacy path: absolute threshold 0.40, no quantile, no coupling.
    ///
    /// RNG: Zero ctx.Rng consumption. All noise via MapNoiseBridge2D coordinate hashing
    /// with salt 0xB7C2F1A4u (preserved from F5 — spatial pattern unchanged). Exactly
    /// one FillSimplexPerlin01 call per Execute, as before: sample consumption parity
    /// with the pre-quantile revision is structural.
    /// </summary>
    public sealed class Stage_Vegetation2D : IMapStage2D
    {
        public string Name => "vegetation";

        // Public so the M2a-9 gate in StageVegetation2DTests can recompute the exact
        // noise field instead of duplicating these literals. Behaviour unchanged.
        public const uint NoiseSeedSalt = 0xB7C2F1A4u;
        public const int NoiseFrequency = 4;
        public const int NoiseOctaves = 3;
        public const int NoiseLacunarity = 2;
        public const float NoisePersistence = 0.5f;
        public const int QuantSteps = 1024;

        /// <summary>Legacy absolute threshold used when Biome field is absent (Option A fallback).</summary>
        private const float LegacyThreshold = 0.40f;

        /// <summary>Cut sentinel meaning "accept nothing" — no bucket index can reach it.</summary>
        private const int NoAcceptCut = QuantSteps;

        /// <summary>
        /// Moisture modulation strength. [0, 0.5]. Default 0 = disabled.
        /// Under quantile mapping this shifts the per-cell CUT BUCKET rather than an
        /// absolute threshold: wetter cells get a lower cut (more vegetation), drier
        /// cells a higher one. Direct analogue of the pre-quantile behaviour.
        ///   bucketShift = round(moistureModulation * (moisture - 0.5) * QuantSteps)
        ///   effectiveCut = clamp(cut - bucketShift, 0, QuantSteps)
        /// W.b: assigned by visualization components from MapGenerationPreset
        /// (vegetationMoistureModulation). Default 0 preserves legacy behaviour.
        /// Note: a non-zero value suspends M2a-9(a) and (c) by construction — the cut
        /// stops being uniform across the population. See the M2a-9 modulation note
        /// in map-pipeline-by-layers-ssot.md.
        /// </summary>
        public float moistureModulation = 0.0f;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D landInterior = ref ctx.GetLayer(MapLayerId.LandInterior);
            ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);

            ref MaskGrid2D vegetation = ref ctx.EnsureLayer(MapLayerId.Vegetation, clearToZero: true);

            bool hasBiome = ctx.IsFieldCreated(MapFieldId.Biome);

            NativeArray<float> noise01 = default;
            NativeArray<int> hist = default;
            NativeArray<int> cut = default;
            try
            {
                noise01 = new NativeArray<float>(d.Length, Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);

                MapNoiseBridge2D.FillSimplexPerlin01(
                    in d,
                    noise01,
                    seed: inputs.Seed,
                    seedSalt: NoiseSeedSalt,
                    frequency: NoiseFrequency,
                    octaves: NoiseOctaves,
                    lacunarity: NoiseLacunarity,
                    persistence: NoisePersistence,
                    quantSteps: QuantSteps);

                // -----------------------------------------------------------------
                // Legacy path (Phase M absent): absolute threshold, single pass.
                // Kept structurally separate so the two semantics cannot drift.
                // -----------------------------------------------------------------
                if (!hasBiome)
                {
                    for (int y = 0; y < h; y++)
                    {
                        int row = y * w;
                        for (int x = 0; x < w; x++)
                        {
                            if (!landInterior.GetUnchecked(x, y)) continue;
                            if (hillsL2.GetUnchecked(x, y)) continue;
                            if (noise01[row + x] >= LegacyThreshold)
                                vegetation.SetUnchecked(x, y, true);
                        }
                    }
                    return;
                }

                ScalarField2D biomeField = ctx.GetField(MapFieldId.Biome);

                bool useMoisture = moistureModulation > 0f
                                && ctx.IsFieldCreated(MapFieldId.Moisture);
                ScalarField2D moistureField = useMoisture ? ctx.GetField(MapFieldId.Moisture) : default;

                // -----------------------------------------------------------------
                // Pass 1 — histogram of the eligible population.
                // -----------------------------------------------------------------
                hist = new NativeArray<int>(QuantSteps, Allocator.Temp,
                    NativeArrayOptions.ClearMemory);
                int eligible = 0;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (!landInterior.GetUnchecked(x, y)) continue;
                        int idx = row + x;
                        if (!TryGetEligibleDensity(
                                (int)biomeField.Values[idx],
                                hillsL2.GetUnchecked(x, y),
                                out _))
                            continue;

                        hist[Bucket(noise01[idx])]++;
                        eligible++;
                    }
                }

                // -----------------------------------------------------------------
                // Cut bucket per biome. O(BiomeType.COUNT * QuantSteps), ~12k ops.
                // -----------------------------------------------------------------
                int biomeCount = (int)BiomeType.COUNT;
                cut = new NativeArray<int>(biomeCount, Allocator.Temp,
                    NativeArrayOptions.UninitializedMemory);
                for (int b = 0; b < biomeCount; b++)
                    cut[b] = (b <= 0)
                        ? NoAcceptCut
                        : ComputeCutBucket(hist, eligible, BiomeTable.Definitions[b].vegetationDensity);

                // -----------------------------------------------------------------
                // Pass 2 — apply. Guard chain is the same helper as pass 1, so the
                // two populations cannot diverge.
                // -----------------------------------------------------------------
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (!landInterior.GetUnchecked(x, y)) continue;
                        int idx = row + x;
                        int biomeId = (int)biomeField.Values[idx];
                        if (!TryGetEligibleDensity(biomeId, hillsL2.GetUnchecked(x, y), out _))
                            continue;

                        int effectiveCut = cut[biomeId];

                        if (useMoisture)
                        {
                            int shift = (int)math.round(
                                moistureModulation * (moistureField.Values[idx] - 0.5f) * QuantSteps);
                            effectiveCut = math.clamp(effectiveCut - shift, 0, NoAcceptCut);
                        }

                        if (Bucket(noise01[idx]) >= effectiveCut)
                            vegetation.SetUnchecked(x, y, true);
                    }
                }
            }
            finally
            {
                if (cut.IsCreated) cut.Dispose();
                if (hist.IsCreated) hist.Dispose();
                if (noise01.IsCreated) noise01.Dispose();
            }
        }

        /// <summary>
        /// Eligibility guard chain, shared by both passes. Order matters: the sentinel
        /// check runs before Definitions[] is indexed, and the peak policy runs after
        /// the density check so a zero-density biome is rejected for the right reason.
        /// M2a-7 / M2a-8 / M2a-3.
        /// </summary>
        private static bool TryGetEligibleDensity(int biomeId, bool isPeak, out float density)
        {
            density = 0f;
            if (biomeId <= 0 || biomeId >= (int)BiomeType.COUNT) return false;

            BiomeDef def = BiomeTable.Definitions[biomeId];
            if (def.vegetationDensity <= 0f) return false;
            if (isPeak && !def.vegetatesOnPeaks) return false;

            density = def.vegetationDensity;
            return true;
        }

        /// <summary>
        /// noise01 is already quantized to k/QuantSteps, so this recovers k exactly.
        /// </summary>
        private static int Bucket(float v) =>
            math.clamp((int)(v * QuantSteps), 0, QuantSteps - 1);

        /// <summary>
        /// M2a-9: largest bucket k whose descending cumulative count reaches
        /// ceil(density * population). Whole buckets are accepted, so realized
        /// coverage is ≥ nominal and never below — that tie-break rule IS the
        /// determinism guarantee and is contract surface.
        /// </summary>
        private static int ComputeCutBucket(NativeArray<int> hist, int population, float density)
        {
            if (population <= 0 || density <= 0f) return NoAcceptCut;

            int target = (int)math.ceil(density * population);
            if (target <= 0) return NoAcceptCut;

            int acc = 0;
            for (int k = QuantSteps - 1; k >= 0; k--)
            {
                acc += hist[k];
                if (acc >= target) return k;
            }
            return 0;
        }
    }
}