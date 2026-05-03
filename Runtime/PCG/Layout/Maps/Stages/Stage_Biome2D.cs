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
    ///   <see cref="MapFieldId.Height"/>           — lapse rate (M.1)
    ///   <see cref="MapFieldId.CoastDist"/>        — coast moderation (M.1), coastal moisture (M.2)
    ///   <see cref="MapFieldId.FlowAccumulation"/> — river moisture enrichment (M.2, optional — Phase L)
    ///   <see cref="MapLayerId.Land"/>             — water sentinel (M.3)
    ///   <see cref="MapLayerId.LandEdge"/>         — Beach override (M.3)
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
    ///   M-7: No-mutate (Height, CoastDist, Land, LandEdge, FlowAccumulation unchanged).
    ///   M-8: Valid biome range (all Land biome values are valid BiomeType enum values).
    ///
    /// Phase L backward compatibility:
    ///   When <see cref="MapFieldId.FlowAccumulation"/> is absent (Phase L not in pipeline),
    ///   <see cref="riverMoistureBonus"/> has no effect and M-1..M-8 produce bit-identical
    ///   output to the pre-Phase-L baseline.  Existing M / M2a / M2b goldens remain valid.
    ///
    /// RNG: Zero ctx.Rng consumption. All noise via <see cref="MapNoiseBridge2D.FillNoise01"/>
    /// coordinate hashing with stage salt 0xB10E.
    ///
    /// Pipeline position: after Stage_Morphology2D (G), before Phase M2.
    /// When Phase L is active: after Stage_Hydrology2D (L), before Phase M2.
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

        // =================================================================
        // M.2 Phase L enrichment tunables (active when FlowAccumulation exists)
        // =================================================================

        /// <summary>
        /// Maximum moisture bonus applied to cells at or above the river flow threshold.
        /// [0, 1]. Default 0.4.
        ///
        /// Applied only when <see cref="MapFieldId.FlowAccumulation"/> exists in the context
        /// (i.e. Stage_Hydrology2D ran before Stage_Biome2D).  When Phase L is absent,
        /// this tunable has zero effect and produces bit-identical output to pre-Phase-L runs.
        /// </summary>
        public float riverMoistureBonus = 0.4f;

        /// <summary>
        /// Flow accumulation normalization divisor.  0 (default) = auto-compute as
        /// <c>totalLandCells × 0.02f</c>, matching Phase L's default riverThresholdFraction.
        /// At the threshold, <c>riverFactor == riverMoistureBonus</c>; below it, proportionally less.
        ///
        /// Override with an explicit positive value when using a non-default
        /// <see cref="Stage_Hydrology2D.riverThresholdFraction"/> in the same pipeline.
        /// </summary>
        public float riverFlowNorm = 0f;

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

            // ---- Phase L: optional FlowAccumulation enrichment ----
            // IsFieldCreated returns false when Phase L is not in the active stage set.
            // When false, riverFactor == 0 throughout M.2 → bit-identical output to pre-L baseline.
            bool hasFlowAccum = ctx.IsFieldCreated(MapFieldId.FlowAccumulation);
            // Capture by value (copies the NativeArray header — safe for read-only use in M.2).
            ScalarField2D flowAccumSnapshot = hasFlowAccum
                ? ctx.GetField(MapFieldId.FlowAccumulation)
                : default;
            // Auto riverFlowNorm: when hasFlowAccum, compute totalLandCells × 0.02f once.
            // CountOnes() is an O(N) scan — called at most once per Execute.
            int landCount = hasFlowAccum ? land.CountOnes() : 0;

            // ---- Outputs ----
            ref ScalarField2D temperature = ref ctx.EnsureField(MapFieldId.Temperature);
            ref ScalarField2D moisture = ref ctx.EnsureField(MapFieldId.Moisture);
            ref ScalarField2D biome = ref ctx.EnsureField(MapFieldId.Biome);

            // ---- M.1 Temperature ----
            ComputeTemperature(
                ref temperature, in height, in coastDist,
                in d, inputs.Seed, w, h);

            // ---- M.2 Moisture (+ optional Phase L river enrichment) ----
            ComputeMoisture(
                ref moisture, in coastDist,
                flowAccumSnapshot, hasFlowAccum, landCount,
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

        /// <param name="flowAccum">
        /// Copy of the FlowAccumulation field (by value).  Ignored when
        /// <paramref name="hasFlowAccum"/> is false.
        /// </param>
        /// <param name="hasFlowAccum">True when Phase L ran before this stage.</param>
        /// <param name="landCount">Total Land cell count (for auto riverFlowNorm).</param>
        private void ComputeMoisture(
            ref ScalarField2D moisture,
            in ScalarField2D coastDist,
            ScalarField2D flowAccum,
            bool hasFlowAccum,
            int landCount,
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

                // ── Phase L river enrichment setup ──────────────────────────
                // effectiveFlowNorm: the accumulation value that maps to riverMoistureBonus × 1.0.
                // Recommended: same fraction as Phase L's riverThresholdFraction (default 0.02),
                // so cells AT the river threshold get the full river moisture bonus.
                // Auto-computed when riverFlowNorm == 0; explicit value overrides.
                float effectiveFlowNorm = 0f;
                if (hasFlowAccum)
                    effectiveFlowNorm = riverFlowNorm > 0f
                        ? riverFlowNorm
                        : math.max(1f, landCount * 0.02f);

                // ── Main moisture loop ───────────────────────────────────────
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

                        // River proximity factor (Phase L enrichment).
                        // riverFactor = 0 when Phase L absent — no change to existing output.
                        float riverFactor = 0f;
                        if (hasFlowAccum)
                        {
                            float fa = flowAccum.Values[idx];
                            riverFactor = riverMoistureBonus
                                        * math.saturate(fa / effectiveFlowNorm);
                        }

                        float moistRaw = moistureNoiseAmplitude * moistNoise[idx]
                                       + coastFactor
                                       + riverFactor;

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