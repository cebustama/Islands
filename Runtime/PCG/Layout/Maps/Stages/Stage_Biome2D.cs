using Unity.Collections;
using Unity.Mathematics;

using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// Phase M — Climate &amp; Biome Classification.
    ///
    /// Three sub-stages executed sequentially within a single <see cref="IMapStage2D"/>:
    ///   M.1 — Temperature field (elevation lapse, latitude, coast moderation, noise).
    ///   M.2 — Moisture field (coastal proximity, noise; FlowAccumulation enrichment when Phase L present).
    ///   M.3 — Biome classification (Whittaker 4×4 lookup + Beach override).
    ///
    /// Reads (read-only):
    ///   <see cref="MapFieldId.Height"/>    — lapse rate (M.1)
    ///   <see cref="MapFieldId.CoastDist"/> — coast moderation (M.1), coastal moisture (M.2)
    ///   <see cref="MapLayerId.Land"/>      — water sentinel (M.3)
    ///   <see cref="MapLayerId.LandEdge"/>  — Beach override (M.3)
    ///
    /// Writes (authoritative):
    ///   <see cref="MapFieldId.Temperature"/> — [0,1] for all cells
    ///   <see cref="MapFieldId.Moisture"/>    — [0,1] for all cells (first authoritative write)
    ///   <see cref="MapFieldId.Biome"/>       — 0f for water; (float)BiomeType for land
    ///
    /// Contracts:
    ///   M-1: Determinism (same seed + tunables → identical fields).
    ///   M-2: Water sentinel (Biome == 0f for all non-Land cells).
    ///   M-3: Land coverage (Biome > 0f for all Land cells).
    ///   M-4: Temperature range [0, 1].
    ///   M-5: Moisture range [0, 1].
    ///   M-6: Beach consistency (warm LandEdge cells → Beach).
    ///   M-7: No-mutate (Height, CoastDist, Land, LandEdge unchanged).
    ///   M-8: Valid biome range (all Land biome values are valid BiomeType enum values).
    ///
    /// RNG: Zero ctx.Rng consumption. All noise via <see cref="MapNoiseBridge2D.FillNoise01"/>
    /// coordinate hashing with stage salt 0xB10E.
    ///
    /// Pipeline position: after Stage_Morphology2D (G), before Phase M2.
    /// </summary>
    public sealed class Stage_Biome2D : IMapStage2D
    {
        public string Name => "biome";

        // =================================================================
        // Stage salts — unique per noise field, decorrelated from F2/F3/G.
        // Base prefix 0xB10E ("biome") with sub-stage suffixes.
        // =================================================================

        private const uint TempNoiseSalt = 0xB10E0001u;
        private const uint MoistNoiseSalt = 0xB10E0002u;

        // =================================================================
        // M.1 Temperature tunables (stage-local)
        // =================================================================

        /// <summary>Sea-level equatorial base temperature. [0, 1]. Default 0.7 for warm tropical islands.</summary>
        public float baseTemperature = 0.7f;

        /// <summary>Height-to-temperature reduction. [0, 1]. 0.5 = highest peaks lose half base temp.</summary>
        public float lapseRate = 0.5f;

        /// <summary>Y-axis latitude gradient strength. [0, 1]. 0.0 for single-island (no latitude). Non-zero for Phase W world maps.</summary>
        public float latitudeEffect = 0.0f;

        /// <summary>Coastal temperature moderation strength. [0, 0.5]. 1/(1+coastDist) falloff.</summary>
        public float coastModerationStrength = 0.1f;

        /// <summary>Temperature noise amplitude. [0, 0.3]. Low-frequency perturbation.</summary>
        public float tempNoiseAmplitude = 0.05f;

        /// <summary>Temperature noise cell size (frequency parameter for MapNoiseBridge2D). Coarse; 2× terrain noise freq.</summary>
        public int tempNoiseCellSize = 16;

        // =================================================================
        // M.2 Moisture tunables (stage-local)
        // =================================================================

        /// <summary>Coastal proximity moisture bonus at coast. [0, 1].</summary>
        public float coastalMoistureBonus = 0.5f;

        /// <summary>Coastal moisture decay rate. [0, 1]. Higher = faster inland decay.</summary>
        public float coastDecayRate = 0.3f;

        /// <summary>Moisture noise amplitude. [0, 1]. Perturbation; coast gradient now dominant.</summary>
        public float moistureNoiseAmplitude = 0.3f;

        /// <summary>Moisture noise cell size. 4–8× lower frequency than terrain noise to prevent biome fragmentation.</summary>
        public int moistureNoiseCellSize = 32;

        // Phase L enrichment tunables — unused until FlowAccumulation field exists.
        // Kept as documentation of the future interface; see Phase_L_Design.md.
        // public float riverMoistureBonus = 0.4f;
        // public float riverFlowNorm     = 50.0f;

        // =================================================================
        // Execute
        // =================================================================

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            // ---- Read-only inputs ----
            ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);
            ref ScalarField2D coastDist = ref ctx.GetField(MapFieldId.CoastDist);
            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref MaskGrid2D landEdge = ref ctx.GetLayer(MapLayerId.LandEdge);

            // ---- Outputs ----
            ref ScalarField2D temperature = ref ctx.EnsureField(MapFieldId.Temperature);
            ref ScalarField2D moisture = ref ctx.EnsureField(MapFieldId.Moisture);
            ref ScalarField2D biome = ref ctx.EnsureField(MapFieldId.Biome);

            // ---- M.1 Temperature ----
            ComputeTemperature(
                ref temperature, in height, in coastDist,
                in d, inputs.Seed, w, h);

            // ---- M.2 Moisture ----
            ComputeMoisture(
                ref moisture, in coastDist,
                in d, inputs.Seed, w, h);

            // ---- M.3 Biome Classification ----
            ClassifyBiomes(
                ref biome, in temperature, in moisture,
                in land, in landEdge, w, h);
        }

        // =================================================================
        // M.1 — Temperature Field
        // =================================================================

        private void ComputeTemperature(
            ref ScalarField2D temperature,
            in ScalarField2D height,
            in ScalarField2D coastDist,
            in GridDomain2D domain,
            uint seed,
            int w, int h)
        {
            // Noise via coordinate hashing — no ctx.Rng consumption.
            var tempNoise = new NativeArray<float>(domain.Length, Allocator.Temp);
            try
            {
                var noiseSettings = new TerrainNoiseSettings
                {
                    noiseType = TerrainNoiseType.Perlin,
                    frequency = math.max(1, tempNoiseCellSize),
                    octaves = 2,
                    lacunarity = 2,
                    persistence = 0.5f,
                };
                MapNoiseBridge2D.FillNoise01(in domain, tempNoise, seed, TempNoiseSalt, in noiseSettings);

                float invDomainH = h > 0 ? 1f / h : 0f;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;

                    // Latitude: 0 at center, 1 at top/bottom edges.
                    float latNorm = math.abs(y * invDomainH - 0.5f) * 2.0f;
                    float latFactor = latitudeEffect * latNorm;

                    for (int x = 0; x < w; x++)
                    {
                        int idx = row + x;

                        float hv = height.Values[idx];
                        float cd = coastDist.Values[idx];

                        // Coast moderation: 1/(1+max(cd,0)) — strongest at coast, negligible inland.
                        // For water cells (cd < 0), max(cd, 0) = 0 → maximum moderation.
                        // This keeps water-cell temperatures moderate for visual continuity.
                        float coastMod = coastModerationStrength / (1.0f + math.max(cd, 0f));

                        float tempRaw = baseTemperature
                                      - lapseRate * hv
                                      - latFactor
                                      + coastMod
                                      + tempNoiseAmplitude * (tempNoise[idx] - 0.5f);

                        temperature.Values[idx] = math.saturate(tempRaw);
                    }
                }
            }
            finally
            {
                if (tempNoise.IsCreated) tempNoise.Dispose();
            }
        }

        // =================================================================
        // M.2 — Moisture Field
        // =================================================================

        private void ComputeMoisture(
            ref ScalarField2D moisture,
            in ScalarField2D coastDist,
            in GridDomain2D domain,
            uint seed,
            int w, int h)
        {
            // Noise via coordinate hashing — coarse frequency to avoid biome fragmentation.
            var moistNoise = new NativeArray<float>(domain.Length, Allocator.Temp);
            try
            {
                var noiseSettings = new TerrainNoiseSettings
                {
                    noiseType = TerrainNoiseType.Perlin,
                    frequency = math.max(1, moistureNoiseCellSize),
                    octaves = 2,
                    lacunarity = 2,
                    persistence = 0.5f,
                };
                MapNoiseBridge2D.FillNoise01(in domain, moistNoise, seed, MoistNoiseSalt, in noiseSettings);

                // Phase L enrichment: when FlowAccumulation field exists, river proximity
                // contributes to moisture. Not yet implemented — Phase L adds the field
                // and this stage gains a ctx.IsFieldCreated check + accumulation read.
                // See Phase_L_Design.md § "Contract between L and M".

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int idx = row + x;

                        float cd = coastDist.Values[idx];

                        // Coastal factor: bonus / (1 + max(cd, 0) * decayRate).
                        // Water cells (cd < 0) get maximum coastal moisture.
                        float coastFactor = coastalMoistureBonus
                                          / (1.0f + math.max(cd, 0f) * coastDecayRate);

                        float moistRaw = moistureNoiseAmplitude * moistNoise[idx]
                                       + coastFactor;

                        moisture.Values[idx] = math.saturate(moistRaw);
                    }
                }
            }
            finally
            {
                if (moistNoise.IsCreated) moistNoise.Dispose();
            }
        }

        // =================================================================
        // M.3 — Biome Classification
        // =================================================================

        private static void ClassifyBiomes(
            ref ScalarField2D biome,
            in ScalarField2D temperature,
            in ScalarField2D moisture,
            in MaskGrid2D land,
            in MaskGrid2D landEdge,
            int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    int idx = row + x;

                    if (!land.GetUnchecked(x, y))
                    {
                        // M-2: Water sentinel.
                        biome.Values[idx] = 0f;
                        continue;
                    }

                    float temp01 = temperature.Values[idx];
                    float moist01 = moisture.Values[idx];

                    // Whittaker table lookup.
                    BiomeType bt = BiomeTable.Lookup(temp01, moist01);

                    // Beach override: warm LandEdge cells → Beach.
                    // Geographic feature override, not a climate outcome.
                    if (landEdge.GetUnchecked(x, y)
                        && temp01 >= BiomeTable.BeachMinTemperature)
                    {
                        bt = BiomeType.Beach;
                    }

                    // Store as int-as-float.
                    biome.Values[idx] = (float)bt;
                }
            }
        }
    }
}