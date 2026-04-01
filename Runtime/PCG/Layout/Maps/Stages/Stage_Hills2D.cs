using Unity.Collections;
using Islands.PCG.Core;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F3 — Hills + topology.
    ///
    /// Reads:
    /// - Land
    /// - DeepWater
    ///
    /// Writes:
    /// - LandEdge
    /// - LandInterior
    /// - HillsL1
    /// - HillsL2
    ///
    /// Contracts:
    /// - LandEdge U LandInterior == Land
    /// - LandEdge ∩ LandInterior == ∅
    /// - HillsL1 ⊆ LandInterior
    /// - HillsL2 ⊆ HillsL1
    /// - Does not mutate Land / DeepWater / Height
    /// </summary>
    public sealed class Stage_Hills2D : IMapStage2D
    {
        public string Name => "hills";

        private const uint NoiseSeedSalt = 0xA511E9B3u;
        private const int NoiseFrequency = 5;
        private const int NoiseOctaves = 3;
        private const int NoiseLacunarity = 2;
        private const float NoisePersistence = 0.5f;
        private const int QuantSteps = 1024;

        private const float HillsL1Threshold = 0.58f;
        private const float HillsL2Threshold = 0.72f;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D deepWater = ref ctx.GetLayer(MapLayerId.DeepWater);

            ref MaskGrid2D landEdge = ref ctx.EnsureLayer(MapLayerId.LandEdge, clearToZero: true);
            ref MaskGrid2D landInterior = ref ctx.EnsureLayer(MapLayerId.LandInterior, clearToZero: true);
            ref MaskGrid2D hillsL1 = ref ctx.EnsureLayer(MapLayerId.HillsL1, clearToZero: true);
            ref MaskGrid2D hillsL2 = ref ctx.EnsureLayer(MapLayerId.HillsL2, clearToZero: true);

            MaskTopologyOps2D.ExtractEdgeAndInterior4(in land, ref landEdge, ref landInterior);

            NativeArray<float> noise01 = default;
            try
            {
                noise01 = new NativeArray<float>(d.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
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
                        if (!landInterior.GetUnchecked(x, y))
                            continue;

                        int i = row + x;
                        float n = noise01[i];

                        bool l1 = n >= HillsL1Threshold;
                        bool l2 = n >= HillsL2Threshold;

                        if (l1 && !deepWater.GetUnchecked(x, y))
                            hillsL1.SetUnchecked(x, y, true);

                        if (l2 && !deepWater.GetUnchecked(x, y))
                            hillsL2.SetUnchecked(x, y, true);
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
