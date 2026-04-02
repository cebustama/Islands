using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F5 — Vegetation.
    ///
    /// Reads:
    /// - Land          (read-only)
    /// - LandInterior  (read-only)
    /// - HillsL2       (read-only)
    /// - ShallowWater  (read-only)
    ///
    /// Writes:
    /// - Vegetation
    ///
    /// Contracts:
    /// - Vegetation ⊆ Land
    /// - Vegetation ⊆ LandInterior  (excludes shore-edge land ring)
    /// - Vegetation ∩ HillsL2 == ∅  (no vegetation on hill peaks)
    /// - Vegetation ∩ ShallowWater == ∅  (implied; explicit for contract clarity)
    /// - Does not mutate Land, LandInterior, HillsL2, ShallowWater, or Height.
    /// - Does not write MapFieldId.Moisture (deferred to Phase M).
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

        private const float VegetationThreshold = 0.40f;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;

            ref MaskGrid2D landInterior = ref ctx.GetLayer(MapLayerId.LandInterior);
            ref MaskGrid2D hillsL2 = ref ctx.GetLayer(MapLayerId.HillsL2);
            ref MaskGrid2D shallowWater = ref ctx.GetLayer(MapLayerId.ShallowWater);

            ref MaskGrid2D vegetation = ref ctx.EnsureLayer(MapLayerId.Vegetation, clearToZero: true);

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

                int w = d.Width;
                int h = d.Height;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        // Must be LandInterior (excludes LandEdge and all water)
                        if (!landInterior.GetUnchecked(x, y))
                            continue;

                        // Exclude hill peaks
                        if (hillsL2.GetUnchecked(x, y))
                            continue;

                        // Noise threshold
                        if (noise01[row + x] >= VegetationThreshold)
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