// Phase X1.a — preset diagnostics (authoring track, first iteration).
//
// Pure Editor-side analysis: preset -> findings. No pipeline execution, no
// asset mutation, no UI dependency. The wizard renders the result; tests gate
// the logic directly (same separation that proved out for the JSON importer).
//
// X1 hard rule: every finding is labeled Measured or Inferred. Measured
// findings MUST name the run that backs them. A plausible-but-false hint is
// worse than no hint — the measured density->coverage curve is the proof that
// intuition fails in this domain.
//
// Golden-neutral by construction: nothing here can alter generation output.

using System;
using System.Collections.Generic;
using System.Globalization;
using Islands.PCG.Layout.Maps;   // BiomeTable, NoiseSettingsAsset
using Islands.PCG.Samples;       // MapGenerationPreset

namespace Islands.PCG.Editor
{
    public enum PresetFindingSeverity { Info, Warning }

    /// <summary>Evidence backing. Measured requires a named run.</summary>
    public enum PresetFindingBacking { Measured, Inferred }

    /// <summary>One diagnostic result. Immutable after construction by convention.</summary>
    public sealed class PresetFinding
    {
        public string RuleId;
        public PresetFindingSeverity Severity;
        public PresetFindingBacking Backing;
        /// <summary>Named run backing a Measured finding; null for Inferred.</summary>
        public string BackingRun;
        public string Message;

        public string Label => Backing == PresetFindingBacking.Measured
            ? $"[measured: {BackingRun}]"
            : "[inferred]";
    }

    /// <summary>
    /// Deterministic, pipeline-free contradiction checks over a
    /// <see cref="MapGenerationPreset"/>, plus a filtered JSON diff helper.
    /// </summary>
    public static class MapGenerationPresetDiagnostics
    {
        /// <summary>The only measured run available today (W-aux.c blocks 2–3).</summary>
        public const string RunWAuxC23 = "seed 56, res 256, Default_MapPreset, 2026-08-19";

        /// <summary>
        /// Measured vegetation-density step: below ~0.4 no biome vegetates anywhere.
        /// Curve: 0.85→100%, 0.65→95.5%, 0.60→91.8%, 0.25→0.26%, 0.05→0%.
        /// </summary>
        public const float MeasuredVegetationDensityStep = 0.4f;

        /// <summary>Whittaker Hot band lower bound (BiomeTable: 4 uniform bands).</summary>
        public const float HotBandLowerBound = 0.75f;

        /// <summary>Below this, the geometric plateau is considered removed (R4 trigger).</summary>
        public const float SmoothFromEpsilon = 0.05f;

        /// <summary>
        /// Stage_Hydrology2D.riverThresholdFraction default. The biomeRiverFlowNorm
        /// auto mode (0) normalizes by totalLandCells × this DEFAULT — not by the
        /// configured fraction (Stage_Biome2D.ComputeMoisture). R7 trigger anchor.
        /// </summary>
        public const float RiverThresholdFractionDefault = 0.02f;

        public static List<PresetFinding> Diagnose(MapGenerationPreset preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            var findings = new List<PresetFinding>();
            CheckDegenerateFalloff(preset, findings);
            CheckInvisibleMidWaterBand(preset, findings);
            CheckInertHillsNoise(preset, findings);
            CheckClampSaturationPlateau(preset, findings);
            CheckHotBandUnreachable(preset, findings);
            CheckZeroCoverageVegetationDensities(preset, findings);
            CheckRiverFlowNormDecoupled(preset, findings);
            return findings;
        }

        // ------------------------------------------------------------------
        // R1 — falloff window reversed. Tunables clamp internally, so the
        // result is a hard-edged cut, not the smooth ring the sliders imply.
        // ------------------------------------------------------------------
        private static void CheckDegenerateFalloff(MapGenerationPreset p, List<PresetFinding> o)
        {
            if (p.islandSmoothFrom01 >= p.islandSmoothTo01)
                o.Add(new PresetFinding
                {
                    RuleId = "R1.DegenerateFalloff",
                    Severity = PresetFindingSeverity.Warning,
                    Backing = PresetFindingBacking.Inferred,
                    Message = $"islandSmoothFrom01 ({F(p.islandSmoothFrom01)}) >= islandSmoothTo01 "
                            + $"({F(p.islandSmoothTo01)}): the smooth window is empty and is clamped "
                            + "internally — the island edge degenerates to a hard cut."
                });
        }

        // ------------------------------------------------------------------
        // R2 — mid-water band enabled but not deeper than the shallow band:
        // the band occupies zero depth range and can never appear.
        // ------------------------------------------------------------------
        private static void CheckInvisibleMidWaterBand(MapGenerationPreset p, List<PresetFinding> o)
        {
            if (p.midWaterDepth01 > 0f && p.midWaterDepth01 <= p.shallowWaterDepth01)
                o.Add(new PresetFinding
                {
                    RuleId = "R2.InvisibleMidWaterBand",
                    Severity = PresetFindingSeverity.Warning,
                    Backing = PresetFindingBacking.Inferred,
                    Message = $"midWaterDepth01 ({F(p.midWaterDepth01)}) is > 0 but <= "
                            + $"shallowWaterDepth01 ({F(p.shallowWaterDepth01)}): the mid-water "
                            + "band is enabled yet has no depth range of its own — it will never render."
                });
        }

        // ------------------------------------------------------------------
        // R3 — hills noise asset assigned but blend = 0: the asset is resolved
        // and carried around, never mixed in. Configuration contradicts intent.
        // ------------------------------------------------------------------
        private static void CheckInertHillsNoise(MapGenerationPreset p, List<PresetFinding> o)
        {
            if (p.enableHillsStage && p.hillsNoiseAsset != null && p.hillsNoiseBlend <= 0f)
                o.Add(new PresetFinding
                {
                    RuleId = "R3.InertHillsNoise",
                    Severity = PresetFindingSeverity.Warning,
                    Backing = PresetFindingBacking.Inferred,
                    Message = $"hillsNoiseAsset '{p.hillsNoiseAsset.name}' is assigned but "
                            + "hillsNoiseBlend = 0: the noise is never blended into the hills "
                            + "bands. Raise the blend or clear the asset slot."
                });
        }

        // ------------------------------------------------------------------
        // R4 — clamp-saturation plateau. h01 = mask01·(1 + (n − 0.5)·amp) is
        // clamped to 1, so every cell with mask01 >= 1/(1 + 0.5·amp) can clip.
        // Fires only when the geometric plateau was removed (smoothFrom ~ 0),
        // i.e. exactly when the residual plateau contradicts the user's intent.
        // Monotone remaps (redistribution exponent, remap curve, quantization)
        // run after the clamp and satisfy f(1) = 1 — none can remove it.
        // ------------------------------------------------------------------
        private static void CheckClampSaturationPlateau(MapGenerationPreset p, List<PresetFinding> o)
        {
            float amp = p.terrainNoiseAsset != null
                ? p.terrainNoiseAsset.Settings.amplitude
                : p.terrainNoiseSettings.amplitude;

            if (p.islandSmoothFrom01 >= SmoothFromEpsilon || amp <= 0f)
                return;

            float onset = 1f / (1f + 0.5f * amp);
            o.Add(new PresetFinding
            {
                RuleId = "R4.ClampSaturationPlateau",
                Severity = PresetFindingSeverity.Info,
                Backing = PresetFindingBacking.Measured,
                BackingRun = RunWAuxC23,
                Message = $"islandSmoothFrom01 ≈ 0 removes the geometric plateau, but clamp "
                        + $"saturation is predicted: with terrain amplitude {F(amp)}, cells with "
                        + $"mask01 >= {F(onset)} clip to height 1.0. No monotone remap "
                        + "(redistribution exponent, remap curve, quantization) can remove this "
                        + "— they act after the clamp and keep f(1)=1. On the backing run "
                        + "(amplitude 0.22) the model predicted ~11–12% plateau area; measured 11.72%."
            });
        }

        // ------------------------------------------------------------------
        // R5 — Hot-band biomes unreachable on land. Land cells have height >=
        // waterThreshold01; latitude only subtracts; coast moderation maxes at
        // its strength (coastDist = 0); noise adds at most +0.5·amplitude
        // (Stage_Biome2D: tempNoiseAmplitude·(noise01 − 0.5)).
        // ------------------------------------------------------------------
        private static void CheckHotBandUnreachable(MapGenerationPreset p, List<PresetFinding> o)
        {
            if (!p.enableBiomeStage)
                return;

            float ceiling = p.biomeBaseTemperature
                          - p.biomeLapseRate * p.waterThreshold01
                          + p.biomeCoastModerationStrength
                          + 0.5f * p.biomeTempNoiseAmplitude;

            if (ceiling < HotBandLowerBound)
                o.Add(new PresetFinding
                {
                    RuleId = "R5.HotBandUnreachable",
                    Severity = PresetFindingSeverity.Warning,
                    Backing = PresetFindingBacking.Measured,
                    BackingRun = RunWAuxC23,
                    Message = $"Land temperature ceiling ≈ {F(ceiling)} "
                            + "(base − lapse·waterThreshold + coastModeration + 0.5·tempNoiseAmp) "
                            + $"is below the Hot band lower bound ({F(HotBandLowerBound)}): "
                            + "Hot-band biomes are unreachable on land with this preset. "
                            + "On the backing run, Temperature.max = 0.874 came from water cells, not land."
                });
        }

        // ------------------------------------------------------------------
        // R6 — biome densities below the measured step. threshold = 1 − density
        // is NOT a per-cell probability: measured curve shows a step between
        // 0.25 (→0.26% coverage) and 0.60 (→91.8%). Densities under ~0.4 yield
        // ~zero coverage wherever the biome appears. BiomeTable is static, so
        // this reports table-level truth, gated on the vegetation stage.
        // ------------------------------------------------------------------
        private static void CheckZeroCoverageVegetationDensities(MapGenerationPreset p, List<PresetFinding> o)
        {
            if (!p.enableVegetationStage)
                return;

            var affected = new List<string>();
            foreach (var def in BiomeTable.Definitions)
                if (def.vegetationDensity > 0f && def.vegetationDensity < MeasuredVegetationDensityStep)
                    affected.Add($"{def.displayName} ({F(def.vegetationDensity)})");

            if (affected.Count == 0)
                return;

            o.Add(new PresetFinding
            {
                RuleId = "R6.ZeroCoverageDensities",
                Severity = PresetFindingSeverity.Info,
                Backing = PresetFindingBacking.Measured,
                BackingRun = RunWAuxC23,
                Message = "Biomes below the measured density step (~0.4) are expected to produce "
                        + "~0% vegetation coverage wherever they appear: "
                        + string.Join(", ", affected) + ". "
                        + "threshold = 1 − density is NOT a per-cell probability "
                        + "(measured: density 0.25 → 0.26% coverage, 0.05 → 0%)."
            });
        }

        // ------------------------------------------------------------------
        // R7 — river-flow normalization decoupled from the configured river
        // threshold. biomeRiverFlowNorm = 0 (auto) normalizes river moisture
        // by totalLandCells × the DEFAULT threshold fraction (0.02), not the
        // configured one (Stage_Biome2D.ComputeMoisture). If the preset
        // changes hydroRiverThresholdFraction, biome moisture is normalized
        // against a river definition the map is not actually using.
        // ------------------------------------------------------------------
        private static void CheckRiverFlowNormDecoupled(MapGenerationPreset p, List<PresetFinding> o)
        {
            if (!p.enableHydrologyStage || !p.enableBiomeStage)
                return;
            if (p.biomeRiverFlowNorm != 0f)
                return;
            if (p.hydroRiverThresholdFraction == RiverThresholdFractionDefault)
                return;

            o.Add(new PresetFinding
            {
                RuleId = "R7.RiverFlowNormDecoupled",
                Severity = PresetFindingSeverity.Warning,
                Backing = PresetFindingBacking.Inferred,
                Message = $"biomeRiverFlowNorm = 0 (auto) normalizes river moisture by "
                        + $"totalLandCells × {F(RiverThresholdFractionDefault)} — the DEFAULT "
                        + $"threshold fraction — but hydroRiverThresholdFraction is "
                        + $"{F(p.hydroRiverThresholdFraction)}. Biome moisture is normalized "
                        + "against a river definition this preset does not use. Set an "
                        + "explicit biomeRiverFlowNorm or keep the default threshold."
            });
        }

        // ==================================================================
        // Preset JSON diff — filtered line comparison over ToJson() output.
        //
        // Excludes the same non-reimportable surface the round-trip test
        // excludes: "asset", "stageTogglesNote", and the "derived" block.
        // Line-based by design (ToJson is stable and diffable); limitation:
        // duplicate identical lines (e.g. equal curve keys) are compared as a
        // set, so a moved duplicate is not reported. Acceptable for X1.a.
        // ==================================================================

        /// <summary>Lines removed from A ("− ...") and added in B ("+ ...").</summary>
        public static List<string> DiffJson(string jsonA, string jsonB)
        {
            var a = FilterDiffLines(jsonA);
            var b = FilterDiffLines(jsonB);
            var setA = new HashSet<string>(a);
            var setB = new HashSet<string>(b);

            var diff = new List<string>();
            foreach (string line in a)
                if (!setB.Contains(line)) diff.Add("− " + line);
            foreach (string line in b)
                if (!setA.Contains(line)) diff.Add("+ " + line);
            return diff;
        }

        private static List<string> FilterDiffLines(string json)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(json))
                return result;

            bool inDerived = false;
            int derivedDepth = 0;

            foreach (string raw in json.Split('\n'))
            {
                string t = raw.TrimEnd('\r').Trim();

                if (inDerived)
                {
                    derivedDepth += Opens(t) - Closes(t);
                    if (derivedDepth <= 0) inDerived = false;
                    continue;
                }
                if (t.StartsWith("\"derived\"", StringComparison.Ordinal))
                {
                    derivedDepth = Opens(t) - Closes(t);
                    inDerived = derivedDepth > 0;
                    continue;
                }
                if (t.StartsWith("\"asset\"", StringComparison.Ordinal)) continue;
                if (t.StartsWith("\"stageTogglesNote\"", StringComparison.Ordinal)) continue;
                if (!t.Contains(":")) continue; // structural braces/brackets only

                result.Add(t);
            }
            return result;
        }

        private static int Opens(string s)
        {
            int n = 0;
            foreach (char c in s) if (c == '{' || c == '[') n++;
            return n;
        }

        private static int Closes(string s)
        {
            int n = 0;
            foreach (char c in s) if (c == '}' || c == ']') n++;
            return n;
        }

        private static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    }
}