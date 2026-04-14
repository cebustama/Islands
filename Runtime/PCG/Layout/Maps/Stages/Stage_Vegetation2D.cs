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
    /// Phase M2.a refactor:
    ///   - Reads <see cref="MapFieldId.Biome"/> (Phase M) for per-biome density.
    ///   - Per-cell threshold = 1 - BiomeDef.vegetationDensity.
    ///   - Optional moisture modulation (default disabled).
    ///   - Pipeline position moved after Stage_Biome2D (M).
    ///   - Falls back to global 0.40 threshold when Biome field absent (Option A).
    ///
    /// Reads (read-only):
    ///   <see cref="MapLayerId.LandInterior"/> — eligibility
    ///   <see cref="MapLayerId.HillsL2"/>      — exclusion
    ///   <see cref="MapFieldId.Biome"/>        — per-cell biome ID (NEW, optional)
    ///   <see cref="MapFieldId.Moisture"/>     — moisture modulation (NEW, optional)
    ///
    /// Writes (authoritative):
    ///   <see cref="MapLayerId.Vegetation"/>
    ///
    /// Invariants:
    ///   M2a-1: Vegetation ⊆ Land
    ///   M2a-2: Vegetation ⊆ LandInterior
    ///   M2a-3: Vegetation ∩ HillsL2 == ∅
    ///   M2a-4: Vegetation ∩ ShallowWater == ∅
    ///   M2a-5: Determinism (same seed + tunables → identical output)
    ///   M2a-6: No-mutate (all inputs including Biome, Moisture unchanged)
    ///   M2a-7: Biome-zero suppression (water cells never vegetated)
    ///   M2a-8: Snow / zero-density biome suppression
    ///   M2a-9: Coverage monotonicity (higher density → statistically more coverage)
    ///
    /// RNG: Zero ctx.Rng consumption. All noise via MapNoiseBridge2D coordinate hashing
    /// with salt 0xB7C2F1A4u (preserved from F5 — spatial pattern unchanged).
    /// </summary>
    public sealed class Stage_Vegetation2D : IMapStage2D
    {
        public string Name => "vegetation";

        private const uint NoiseSeedSalt = 0xB7C2F1A4u;
        private const int NoiseFrequency = 4;
        private const int NoiseOctaves = 3;
        private const int NoiseLacunarity = 2;
        private const float NoisePersistence = 0.5f;
        private const int QuantSteps = 1024;

        /// <summary>Legacy global threshold used when Biome field is absent (Option A fallback).</summary>
        private const float LegacyThreshold = 0.40f;

        /// <summary>
        /// Moisture modulation strength. [0, 0.5]. Default 0 = disabled.
        /// When non-zero, wetter cells get slightly more vegetation
        /// (threshold lowered) and drier cells slightly less.
        ///   moistureBonus = moistureModulation * (moisture - 0.5)
        ///   adjustedThreshold = clamp(threshold - moistureBonus, 0, 1)
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

            // M2.a: detect Biome field presence (Phase M absence fallback).
            bool hasBiome = ctx.IsFieldCreated(MapFieldId.Biome);
            ScalarField2D biomeField = hasBiome ? ctx.GetField(MapFieldId.Biome) : default;

            // Moisture modulation: only when Moisture field exists AND modulation > 0.
            bool useMoisture = moistureModulation > 0f
                            && ctx.IsFieldCreated(MapFieldId.Moisture);
            ScalarField2D moistureField = useMoisture ? ctx.GetField(MapFieldId.Moisture) : default;

            NativeArray<float> noise01 = default;
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

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        // Eligibility: must be LandInterior (excludes LandEdge and all water).
                        if (!landInterior.GetUnchecked(x, y))
                            continue;

                        // Exclusion: hill peaks.
                        if (hillsL2.GetUnchecked(x, y))
                            continue;

                        int idx = row + x;

                        float threshold;
                        if (hasBiome)
                        {
                            int biomeId = (int)biomeField.Values[idx];

                            // M2a-7: water sentinel guard. LandInterior excludes water,
                            // so this should not trigger; explicit guard for contract safety.
                            if (biomeId <= 0 || biomeId >= (int)BiomeType.COUNT)
                                continue;

                            float density = BiomeTable.Definitions[biomeId].vegetationDensity;

                            // M2a-8: zero-density biomes (Snow, Unclassified) → no vegetation.
                            if (density <= 0f)
                                continue;

                            threshold = 1.0f - density;

                            if (useMoisture)
                            {
                                float moistBonus = moistureModulation
                                    * (moistureField.Values[idx] - 0.5f);
                                threshold = math.clamp(threshold - moistBonus, 0f, 1f);
                            }
                        }
                        else
                        {
                            // Phase M absence: legacy global threshold (Option A).
                            threshold = LegacyThreshold;
                        }

                        if (noise01[idx] >= threshold)
                            vegetation.SetUnchecked(x, y, true);
                    }
                }
            }
            finally
            {
                if (noise01.IsCreated) noise01.Dispose();
            }
        }
    }
}