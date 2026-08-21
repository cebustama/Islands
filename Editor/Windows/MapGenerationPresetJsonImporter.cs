// Phase W-aux.c block 2 — JSON import for MapGenerationPreset.
//
// Parser is a hand-written key scanner, NOT JsonUtility, by decision: with
// JsonUtility an absent key is indistinguishable from a default value, which
// makes the "absent fields preserve previous value" report impossible.
//
// Separated from the EditorWindow so the round-trip test can gate it directly
// (DoD: round-trip proven by test, not by inspection).
//
// Accepts the literal output of MapGenerationPreset.ToJson() without manual
// editing. Tolerant of whitespace and key reordering; not a general JSON
// parser (no nested arrays-of-objects except heightRemapCurve, which is
// handled as a captured bracket span).
//
// Never fails silently: every key ends in exactly one report bucket
// (Applied / Ignored / Unknown / Errors), and every known-appliable key
// never seen ends in AbsentPreserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Islands;                      // FractalMode (Islands.Runtime — referenced by Islands.PCG.Editor.asmdef)
using Islands.PCG.Layout.Maps;      // TerrainNoiseSettings, IslandShapeMode, Worley enums
using Islands.PCG.Samples;          // MapGenerationPreset

namespace Islands.PCG.Editor
{
    /// <summary>Import result. Four diagnostic categories + hard errors.</summary>
    public sealed class PresetImportReport
    {
        public readonly List<string> Applied = new List<string>();
        /// <summary>Recognized keys deliberately not applied (asset, stageTogglesNote, derived.*, noise.*.source).</summary>
        public readonly List<string> Ignored = new List<string>();
        /// <summary>Keys not in the known-field table.</summary>
        public readonly List<string> Unknown = new List<string>();
        /// <summary>Known appliable keys missing from the JSON — target keeps its previous value.</summary>
        public readonly List<string> AbsentPreserved = new List<string>();
        /// <summary>Malformed values or structure. A key with an error is NOT applied.</summary>
        public readonly List<string> Errors = new List<string>();

        public bool HasErrors => Errors.Count > 0;
    }

    public static class MapGenerationPresetJsonImporter
    {
        /// <summary>
        /// Applies the JSON onto <paramref name="target"/> in place.
        /// Caller owns Undo.RecordObject / SetDirty / asset lifecycle.
        /// </summary>
        public static PresetImportReport Import(MapGenerationPreset target, string json)
        {
            var report = new PresetImportReport();
            if (target == null) { report.Errors.Add("Target preset is null."); return report; }
            if (string.IsNullOrWhiteSpace(json)) { report.Errors.Add("JSON text is empty."); return report; }

            List<KeyValuePair<string, string>> pairs = Scan(json, report.Errors);

            var appliers = BuildApplierTable(target);
            var seen = new HashSet<string>();

            foreach (var kv in pairs)
            {
                string path = kv.Key;
                string raw = kv.Value;
                seen.Add(path);

                if (IsIgnoredPath(path))
                {
                    if (path.EndsWith(".source", StringComparison.Ordinal)
                        && raw.StartsWith("asset:", StringComparison.Ordinal))
                    {
                        report.Ignored.Add(
                            $"{path} = \"{raw}\" — asset not resolvable from JSON; " +
                            "values applied to the INLINE fields, asset slot left untouched. " +
                            "If the slot still holds an asset, the asset wins over the imported inline values.");
                    }
                    else
                    {
                        report.Ignored.Add(path);
                    }
                    continue;
                }

                if (appliers.TryGetValue(path, out var apply))
                {
                    string error = apply(raw);
                    if (error == null) report.Applied.Add(path);
                    else report.Errors.Add($"{path}: {error} (value kept: previous)");
                }
                else
                {
                    report.Unknown.Add(path);
                }
            }

            foreach (string known in appliers.Keys)
                if (!seen.Contains(known))
                    report.AbsentPreserved.Add(known);

            return report;
        }

        // ==================================================================
        // Known-field table: JSON path → applier. Returns null on success,
        // error message on parse failure (field untouched on failure).
        // ==================================================================

        private static Dictionary<string, Func<string, string>> BuildApplierTable(MapGenerationPreset p)
        {
            var t = new Dictionary<string, Func<string, string>>(StringComparer.Ordinal)
            {
                // runInputs
                ["runInputs.seed"] = raw => PU(raw, v => p.seed = v),
                ["runInputs.resolution"] = raw => PI(raw, v => p.resolution = v),

                // stageToggles
                ["stageToggles.hills"] = raw => PB(raw, v => p.enableHillsStage = v),
                ["stageToggles.shore"] = raw => PB(raw, v => p.enableShoreStage = v),
                ["stageToggles.vegetation"] = raw => PB(raw, v => p.enableVegetationStage = v),
                ["stageToggles.traversal"] = raw => PB(raw, v => p.enableTraversalStage = v),
                ["stageToggles.morphology"] = raw => PB(raw, v => p.enableMorphologyStage = v),
                ["stageToggles.biome"] = raw => PB(raw, v => p.enableBiomeStage = v),
                ["stageToggles.regions"] = raw => PB(raw, v => p.enableRegionsStage = v),
                ["stageToggles.hydrology"] = raw => PB(raw, v => p.enableHydrologyStage = v), 

                // islandShape
                ["islandShape.shapeMode"] = raw => PE<IslandShapeMode>(raw, v => p.shapeMode = v),
                ["islandShape.islandRadius01"] = raw => PF(raw, v => p.islandRadius01 = v),
                ["islandShape.islandAspectRatio"] = raw => PF(raw, v => p.islandAspectRatio = v),
                ["islandShape.warpAmplitude01"] = raw => PF(raw, v => p.warpAmplitude01 = v),
                ["islandShape.islandSmoothFrom01"] = raw => PF(raw, v => p.islandSmoothFrom01 = v),
                ["islandShape.islandSmoothTo01"] = raw => PF(raw, v => p.islandSmoothTo01 = v),

                // waterAndShore
                ["waterAndShore.waterThreshold01"] = raw => PF(raw, v => p.waterThreshold01 = v),
                ["waterAndShore.shallowWaterDepth01"] = raw => PF(raw, v => p.shallowWaterDepth01 = v),
                ["waterAndShore.midWaterDepth01"] = raw => PF(raw, v => p.midWaterDepth01 = v),
                ["waterAndShore.seaFloorLevel01"] = raw => PF(raw, v => p.seaFloorLevel01 = v),
                ["waterAndShore.seaFloorAmplitude01"] = raw => PF(raw, v => p.seaFloorAmplitude01 = v),

                // height
                ["height.heightQuantSteps"] = raw => PI(raw, v => p.heightQuantSteps = v),
                ["height.heightRedistributionExponent"] = raw => PF(raw, v => p.heightRedistributionExponent = v),
                ["height.heightRemapCurve"] = raw => ApplyCurve(raw, p),

                // hills
                ["hills.hillsL1_fraction"] = raw => PF(raw, v => p.hillsL1 = v),
                ["hills.hillsL2_fraction"] = raw => PF(raw, v => p.hillsL2 = v),
                ["hills.hillsNoiseBlend"] = raw => PF(raw, v => p.hillsNoiseBlend = v),

                // biomeClimate (JSON drops the biome* prefix — mapping is explicit here)
                ["biomeClimate.baseTemperature"] = raw => PF(raw, v => p.biomeBaseTemperature = v),
                ["biomeClimate.lapseRate"] = raw => PF(raw, v => p.biomeLapseRate = v),
                ["biomeClimate.latitudeEffect"] = raw => PF(raw, v => p.biomeLatitudeEffect = v),
                ["biomeClimate.coastModerationStrength"] = raw => PF(raw, v => p.biomeCoastModerationStrength = v),
                ["biomeClimate.tempNoiseAmplitude"] = raw => PF(raw, v => p.biomeTempNoiseAmplitude = v),
                ["biomeClimate.tempNoiseCellSize"] = raw => PI(raw, v => p.biomeTempNoiseCellSize = v),
                ["biomeClimate.coastalMoistureBonus"] = raw => PF(raw, v => p.biomeCoastalMoistureBonus = v),
                ["biomeClimate.coastDecayRate"] = raw => PF(raw, v => p.biomeCoastDecayRate = v),
                ["biomeClimate.moistureNoiseAmplitude"] = raw => PF(raw, v => p.biomeMoistureNoiseAmplitude = v),
                ["biomeClimate.moistureNoiseCellSize"] = raw => PI(raw, v => p.biomeMoistureNoiseCellSize = v),
                ["biomeClimate.riverMoistureBonus"] = raw => PF(raw, v => p.biomeRiverMoistureBonus = v),
                ["biomeClimate.riverFlowNorm"] = raw => PF(raw, v => p.biomeRiverFlowNorm = v),

                // hydrology (W.b)
                ["hydrology.riverThresholdFraction"] = raw => PF(raw, v => p.hydroRiverThresholdFraction = v),
                ["hydrology.minLakeArea"] = raw => PI(raw, v => p.hydroMinLakeArea = v),

                // vegetation (W.b)
                ["vegetation.moistureModulation"] = raw => PF(raw, v => p.vegetationMoistureModulation = v),

                // runBehavior
                ["runBehavior.clearBeforeRun"] = raw => PB(raw, v => p.clearBeforeRun = v),
            };

            // noise.{slot}.* — applied to the INLINE structs (see .source handling
            // in Import: asset slots are never modified by import).
            AddNoiseSlot(t, "noise.terrain",
                get: () => p.terrainNoiseSettings, set: v => p.terrainNoiseSettings = v);
            AddNoiseSlot(t, "noise.warp",
                get: () => p.warpNoiseSettings, set: v => p.warpNoiseSettings = v);
            AddNoiseSlot(t, "noise.hills",
                get: () => p.hillsNoiseSettings, set: v => p.hillsNoiseSettings = v);

            return t;
        }

        private static void AddNoiseSlot(
            Dictionary<string, Func<string, string>> t, string prefix,
            Func<TerrainNoiseSettings> get, Action<TerrainNoiseSettings> set)
        {
            // Struct read-modify-write per field. Fields are applied independently
            // so a malformed value in one field does not block the others.
            t[prefix + ".noiseType"] = raw => PE<TerrainNoiseType>(raw, v => { var s = get(); s.noiseType = v; set(s); });
            t[prefix + ".frequency"] = raw => PI(raw, v => { var s = get(); s.frequency = v; set(s); });
            t[prefix + ".octaves"] = raw => PI(raw, v => { var s = get(); s.octaves = v; set(s); });
            t[prefix + ".lacunarity"] = raw => PI(raw, v => { var s = get(); s.lacunarity = v; set(s); });
            t[prefix + ".persistence"] = raw => PF(raw, v => { var s = get(); s.persistence = v; set(s); });
            t[prefix + ".amplitude"] = raw => PF(raw, v => { var s = get(); s.amplitude = v; set(s); });
            t[prefix + ".fractalMode"] = raw => PE<FractalMode>(raw, v => { var s = get(); s.fractalMode = v; set(s); });
            t[prefix + ".worleyDistanceMetric"] = raw => PE<WorleyDistanceMetric>(raw, v => { var s = get(); s.worleyDistanceMetric = v; set(s); });
            t[prefix + ".worleyFunction"] = raw => PE<WorleyFunction>(raw, v => { var s = get(); s.worleyFunction = v; set(s); });
            t[prefix + ".ridgedOffset"] = raw => PF(raw, v => { var s = get(); s.ridgedOffset = v; set(s); });
            t[prefix + ".ridgedGain"] = raw => PF(raw, v => { var s = get(); s.ridgedGain = v; set(s); });
        }

        // ==================================================================
        // Recognized-but-not-applied paths
        // ==================================================================

        private static bool IsIgnoredPath(string path) =>
            path == "asset"
            || path == "stageTogglesNote"
            || path.StartsWith("derived.", StringComparison.Ordinal)
            || (path.StartsWith("noise.", StringComparison.Ordinal)
                && path.EndsWith(".source", StringComparison.Ordinal));

        // ==================================================================
        // Value parsers (InvariantCulture, mirror of ToJson formatting)
        // ==================================================================

        private static string PF(string raw, Action<float> set)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
            { set(v); return null; }
            return $"not a float: '{raw}'";
        }

        private static string PI(string raw, Action<int> set)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            { set(v); return null; }
            return $"not an int: '{raw}'";
        }

        private static string PU(string raw, Action<uint> set)
        {
            if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v))
            { set(v); return null; }
            return $"not a uint: '{raw}'";
        }

        private static string PB(string raw, Action<bool> set)
        {
            if (raw == "true") { set(true); return null; }
            if (raw == "false") { set(false); return null; }
            return $"not a bool: '{raw}'";
        }

        private static string PE<TEnum>(string raw, Action<TEnum> set) where TEnum : struct
        {
            if (Enum.TryParse(raw, ignoreCase: false, out TEnum v)
                && Enum.IsDefined(typeof(TEnum), v))
            { set(v); return null; }
            return $"not a {typeof(TEnum).Name}: '{raw}'";
        }

        private static readonly Regex CurveKeyRegex = new Regex(
            "\\{\\s*\"t\"\\s*:\\s*([^,\\s]+)\\s*,\\s*\"v\"\\s*:\\s*([^,\\s]+)\\s*,\\s*" +
            "\"inT\"\\s*:\\s*([^,\\s]+)\\s*,\\s*\"outT\"\\s*:\\s*([^,}\\s]+)\\s*\\}",
            RegexOptions.Compiled);

        private static string ApplyCurve(string raw, MapGenerationPreset p)
        {
            if (raw == "null")
            {
                // ToJson emits null only for a null/empty curve. Mirror that state.
                p.heightRemapCurve = new AnimationCurve();
                return null;
            }
            var matches = CurveKeyRegex.Matches(raw);
            if (matches.Count == 0)
                return $"curve array not recognized: '{Truncate(raw, 60)}'";

            var keys = new Keyframe[matches.Count];
            var ci = CultureInfo.InvariantCulture;
            for (int i = 0; i < matches.Count; i++)
            {
                var m = matches[i];
                if (!float.TryParse(m.Groups[1].Value, NumberStyles.Float, ci, out float kt) ||
                    !float.TryParse(m.Groups[2].Value, NumberStyles.Float, ci, out float kv) ||
                    !float.TryParse(m.Groups[3].Value, NumberStyles.Float, ci, out float ki) ||
                    !float.TryParse(m.Groups[4].Value, NumberStyles.Float, ci, out float ko))
                    return $"curve key {i} not parseable";
                keys[i] = new Keyframe(kt, kv, ki, ko);
            }
            p.heightRemapCurve = new AnimationCurve(keys);
            return null;
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s.Substring(0, max) + "…";

        // ==================================================================
        // Scanner: JSON text → flat (dot-path, raw value) pairs.
        // Objects nest into the path; arrays are captured as raw bracket spans.
        // ==================================================================

        internal static List<KeyValuePair<string, string>> Scan(string json, List<string> errors)
        {
            var result = new List<KeyValuePair<string, string>>();
            var stack = new Stack<string>();
            int i = 0, n = json.Length;

            void SkipWs() { while (i < n && char.IsWhiteSpace(json[i])) i++; }

            string ReadString()
            {
                i++; // opening quote
                var sb = new StringBuilder();
                while (i < n && json[i] != '"')
                {
                    if (json[i] == '\\' && i + 1 < n) { sb.Append(json[i + 1]); i += 2; }
                    else sb.Append(json[i++]);
                }
                if (i < n) i++; // closing quote
                return sb.ToString();
            }

            SkipWs();
            if (i >= n || json[i] != '{') { errors.Add("Root object '{' not found."); return result; }
            i++;

            while (i < n)
            {
                SkipWs();
                if (i >= n) break;
                char c = json[i];

                if (c == '}') { i++; if (stack.Count > 0) stack.Pop(); SkipWs(); if (i < n && json[i] == ',') i++; continue; }
                if (c == ',') { i++; continue; }
                if (c != '"') { errors.Add($"Unexpected character '{c}' at offset {i}."); i++; continue; }

                string key = ReadString();
                SkipWs();
                if (i >= n || json[i] != ':') { errors.Add($"Missing ':' after key \"{key}\"."); continue; }
                i++;
                SkipWs();

                string path = stack.Count == 0
                    ? key
                    : string.Join(".", stack.Reverse()) + "." + key;

                if (i < n && json[i] == '{') { stack.Push(key); i++; continue; }

                if (i < n && json[i] == '[')
                {
                    int depth = 0, start = i;
                    while (i < n)
                    {
                        if (json[i] == '[') depth++;
                        else if (json[i] == ']') { depth--; if (depth == 0) { i++; break; } }
                        i++;
                    }
                    result.Add(new KeyValuePair<string, string>(path, json.Substring(start, i - start)));
                }
                else if (i < n && json[i] == '"')
                {
                    result.Add(new KeyValuePair<string, string>(path, ReadString()));
                }
                else
                {
                    int start = i;
                    while (i < n && json[i] != ',' && json[i] != '}' && json[i] != '\n') i++;
                    result.Add(new KeyValuePair<string, string>(path, json.Substring(start, i - start).Trim()));
                }

                SkipWs();
                if (i < n && json[i] == ',') i++;
            }

            return result;
        }
    }
}