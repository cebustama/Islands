using Unity.Collections;
using Unity.Mathematics;
using Islands.PCG.Core;
using Islands.PCG.Fields;
using Islands.PCG.Grids;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Operators;

namespace Islands.PCG.Layout.Maps.Stages
{
    /// <summary>
    /// F3b — Height-Coherent Hills + boundary topology.
    /// N5.d — Optional per-cell noise modulation of hill thresholds.
    ///
    /// Reads:
    /// - Land      (MapLayerId 0)   — eligibility mask
    /// - Height    (MapFieldId 0)   — elevation source for threshold classification
    ///
    /// Writes:
    /// - HillsL1       (MapLayerId 7)  — passable slopes: Land AND Height >= effThL1 AND NOT HillsL2
    /// - HillsL2       (MapLayerId 8)  — impassable peaks: Land AND Height >= effThL2
    /// - LandEdge      (MapLayerId 9)  — Land cells 4-adjacent to any non-Land cell
    /// - LandInterior  (MapLayerId 10) — Land AND NOT LandEdge
    ///
    /// Contracts:
    /// - HillsL2 ⊆ Land
    /// - HillsL1 ⊆ Land
    /// - HillsL1 ∩ HillsL2 == ∅
    /// - LandEdge ∪ LandInterior == Land
    /// - LandEdge ∩ LandInterior == ∅
    /// - Does not mutate Land, DeepWater, or Height.
    /// - Does not consume ctx.Rng. Optional coordinate-hashed noise when hillsNoiseBlend > 0.
    ///
    /// Phase F3b replaces the original topology-based hills (noise threshold on LandInterior)
    /// with height-threshold classification. Hills now correlate spatially with the Height
    /// field — peaks appear where terrain is highest. Thresholds are read from
    /// <see cref="MapTunables2D.hillsThresholdL1"/> and <see cref="MapTunables2D.hillsThresholdL2"/>.
    ///
    /// Phase N5.d adds optional per-cell noise offset to the thresholds via
    /// <see cref="MapTunables2D.hillsNoiseBlend"/>. When blend > 0, a noise array is filled
    /// via <see cref="MapNoiseBridge2D.FillNoise01"/> (coordinate hashing, deterministic)
    /// and each cell's effective thresholds are shifted by ±(blend × noise × ModulationRange).
    /// Both thresholds shift by the same offset, preserving the L1–L2 gap and all invariants.
    /// blend = 0.0 skips noise entirely (no allocation, golden-safe).
    ///
    /// LandEdge / LandInterior derivation is unchanged (delegated to
    /// <see cref="MaskTopologyOps2D.ExtractEdgeAndInterior4"/>).
    /// </summary>
    public sealed class Stage_Hills2D : IMapStage2D
    {
        public string Name => "hills";

        /// <summary>
        /// Stage salt for hills noise modulation. Decorrelates from terrain noise (F2)
        /// and warp noise (F2b). F3 family prefix + N5.d identifier.
        /// </summary>
        private const uint HillsNoiseSalt = 0xF3D50001u;

        /// <summary>
        /// Maximum range of threshold modulation in height-space units.
        /// At blend=1.0, effective thresholds shift by ±(ModulationRange/2) = ±0.15.
        /// This is the full peak-to-peak swing; the per-cell offset is
        /// blend × (noise - 0.5) × ModulationRange.
        /// </summary>
        private const float ModulationRange = 0.30f;

        public void Execute(ref MapContext2D ctx, in MapInputs inputs)
        {
            GridDomain2D d = ctx.Domain;
            int w = d.Width;
            int h = d.Height;

            ref MaskGrid2D land = ref ctx.GetLayer(MapLayerId.Land);
            ref ScalarField2D height = ref ctx.GetField(MapFieldId.Height);

            ref MaskGrid2D landEdge = ref ctx.EnsureLayer(MapLayerId.LandEdge, clearToZero: true);
            ref MaskGrid2D landInterior = ref ctx.EnsureLayer(MapLayerId.LandInterior, clearToZero: true);
            ref MaskGrid2D hillsL1 = ref ctx.EnsureLayer(MapLayerId.HillsL1, clearToZero: true);
            ref MaskGrid2D hillsL2 = ref ctx.EnsureLayer(MapLayerId.HillsL2, clearToZero: true);

            // LandEdge / LandInterior: unchanged 4-neighbor boundary detection.
            MaskTopologyOps2D.ExtractEdgeAndInterior4(in land, ref landEdge, ref landInterior);

            // Height-threshold classification.
            // Thresholds are constructor-validated in MapTunables2D (clamped, L2 >= L1).
            float thL1 = inputs.Tunables.hillsThresholdL1;
            float thL2 = inputs.Tunables.hillsThresholdL2;
            float blend = inputs.Tunables.hillsNoiseBlend;

            // N5.d: optional noise modulation of thresholds.
            // When blend > 0, fill a noise array and use it to shift thresholds per cell.
            // Allocator.Temp is frame-scoped in Unity — no manual dispose needed.
            NativeArray<float> noiseArr = default;
            if (blend > 0f)
            {
                noiseArr = new NativeArray<float>(d.Length, Allocator.Temp);
                MapNoiseBridge2D.FillNoise01(
                    in d, noiseArr, inputs.Seed, HillsNoiseSalt,
                    in inputs.Tunables.hillsNoise);
            }

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (!land.GetUnchecked(x, y))
                        continue;

                    float hv = height.Values[row + x];

                    // Per-cell threshold offset: same offset for both thresholds
                    // preserves the L1–L2 gap, guaranteeing effThL2 >= effThL1.
                    float effThL1 = thL1;
                    float effThL2 = thL2;

                    if (blend > 0f)
                    {
                        float offset = blend * (noiseArr[row + x] - 0.5f) * ModulationRange;
                        effThL1 = thL1 - offset;
                        effThL2 = thL2 - offset;
                    }

                    if (hv >= effThL2)
                        hillsL2.SetUnchecked(x, y, true);
                    else if (hv >= effThL1)
                        hillsL1.SetUnchecked(x, y, true);
                }
            }
        }
    }
}