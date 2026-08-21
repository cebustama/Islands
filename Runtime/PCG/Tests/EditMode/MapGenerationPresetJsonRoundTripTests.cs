// Phase W-aux.c block 2 — round-trip gate for the preset JSON wizard.
//
// DoD: the importer accepts the literal output of ToJson() and the re-export
// matches the original except the 'asset' line, the 'stageTogglesNote' line,
// and the 'derived' block. Proven here by test, not by inspection.
//
// Sibling file to MapGenerationPresetTests by decision: keeps the wizard gate
// separate from the defaults/ToTunables tests.
//
// Assembly note: this file needs a reference to the Islands.PCG.Editor
// assembly (importer lives there). If the EditMode test asmdef does not
// already reference it, add it — surfaced in the batch notes.

using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Islands;                      // FractalMode
using Islands.PCG.Editor;           // MapGenerationPresetJsonImporter
using Islands.PCG.Layout.Maps;      // TerrainNoiseSettings enums, IslandShapeMode
using Islands.PCG.Samples;          // MapGenerationPreset

public sealed class MapGenerationPresetJsonRoundTripTests
{
    // ----------------------------------------------------------------------
    // Fixture: a preset with every group set to non-default values, so the
    // round-trip cannot pass by accidentally matching defaults.
    // Float values use <= 6 decimals (ToJson formats "0.######"), so
    // format → parse → format is textually stable.
    // ----------------------------------------------------------------------

    private static MapGenerationPreset MakeNonDefaultPreset()
    {
        var p = ScriptableObject.CreateInstance<MapGenerationPreset>();

        p.seed = 987654321u;
        p.resolution = 192;

        p.enableHillsStage = true;
        p.enableShoreStage = false;
        p.enableVegetationStage = true;
        p.enableTraversalStage = false;
        p.enableMorphologyStage = true;
        p.enableBiomeStage = false;
        p.enableRegionsStage = false;
        p.enableHydrologyStage = true;

        p.shapeMode = IslandShapeMode.Rectangle;
        p.islandRadius01 = 0.37f;
        p.islandAspectRatio = 1.75f;
        p.warpAmplitude01 = 0.21f;
        p.islandSmoothFrom01 = 0.12f;
        p.islandSmoothTo01 = 0.88f;

        p.waterThreshold01 = 0.463f;
        p.shallowWaterDepth01 = 0.07f;
        p.midWaterDepth01 = 0.19f;
        p.seaFloorLevel01 = 0.33f;
        p.seaFloorAmplitude01 = 0.27f;

        p.heightQuantSteps = 512;
        p.heightRedistributionExponent = 1.7f;
        p.heightRemapCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0.5f, 0.5f),
            new Keyframe(0.4f, 0.3f, 1.25f, 1.25f),
            new Keyframe(1f, 1f, 2f, 2f));

        p.hillsL1 = 0.41f;
        p.hillsL2 = 0.57f;
        p.hillsNoiseBlend = 0.22f;

        p.biomeBaseTemperature = 0.79f;
        p.biomeLapseRate = 0.55f;
        p.biomeLatitudeEffect = 0.13f;
        p.biomeCoastModerationStrength = 0.17f;
        p.biomeTempNoiseAmplitude = 0.09f;
        p.biomeTempNoiseCellSize = 24;
        p.biomeCoastalMoistureBonus = 0.44f;
        p.biomeCoastDecayRate = 0.36f;
        p.biomeMoistureNoiseAmplitude = 0.29f;
        p.biomeMoistureNoiseCellSize = 48;
        p.biomeRiverMoistureBonus = 0.31f;
        p.biomeRiverFlowNorm = 12.5f;

        p.hydroRiverThresholdFraction = 0.035f;
        p.hydroMinLakeArea = 6;
        p.vegetationMoistureModulation = 0.25f;

        p.terrainNoiseSettings = new TerrainNoiseSettings
        {
            noiseType = TerrainNoiseType.Worley,
            frequency = 6,
            octaves = 3,
            lacunarity = 3,
            persistence = 0.45f,
            amplitude = 0.28f,
            worleyDistanceMetric = WorleyDistanceMetric.Chebyshev,
            worleyFunction = WorleyFunction.F2MinusF1,
            fractalMode = FractalMode.Ridged,
            ridgedOffset = 0.66f,
            ridgedGain = 2.75f,
        };
        p.warpNoiseSettings = new TerrainNoiseSettings
        {
            noiseType = TerrainNoiseType.Perlin,
            frequency = 5,
            octaves = 2,
            lacunarity = 2,
            persistence = 0.6f,
            amplitude = 1f,
            worleyDistanceMetric = WorleyDistanceMetric.Euclidean,
            worleyFunction = WorleyFunction.F1,
            fractalMode = FractalMode.Standard,
            ridgedOffset = 1f,
            ridgedGain = 2f,
        };
        p.hillsNoiseSettings = new TerrainNoiseSettings
        {
            noiseType = TerrainNoiseType.Value,
            frequency = 7,
            octaves = 2,
            lacunarity = 2,
            persistence = 0.5f,
            amplitude = 1f,
            worleyDistanceMetric = WorleyDistanceMetric.SmoothEuclidean,
            worleyFunction = WorleyFunction.F2,
            fractalMode = FractalMode.Standard,
            ridgedOffset = 1f,
            ridgedGain = 2f,
        };

        p.clearBeforeRun = false;
        return p;
    }

    /// <summary>
    /// Drops the lines the DoD excludes from comparison: the "asset" line,
    /// the "stageTogglesNote" line, and the whole "derived" block.
    /// Both sides of the comparison are filtered identically.
    /// </summary>
    private static string FilterExcludedLines(string json)
    {
        var sb = new StringBuilder(json.Length);
        bool inDerived = false;
        foreach (string line in json.Split('\n'))
        {
            if (inDerived)
            {
                if (line.TrimEnd() == "  }" || line.TrimEnd() == "  },")
                    inDerived = false;
                continue;
            }
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("\"asset\":")) continue;
            if (trimmed.StartsWith("\"stageTogglesNote\":")) continue;
            if (trimmed.StartsWith("\"derived\":")) { inDerived = true; continue; }
            sb.Append(line).Append('\n');
        }
        return sb.ToString();
    }

    // ----------------------------------------------------------------------
    // The gate
    // ----------------------------------------------------------------------

    [Test]
    public void RoundTrip_ExportImportExport_MatchesExceptExcludedSections()
    {
        MapGenerationPreset source = MakeNonDefaultPreset();
        MapGenerationPreset target = ScriptableObject.CreateInstance<MapGenerationPreset>();
        try
        {
            string json1 = source.ToJson();
            PresetImportReport report = MapGenerationPresetJsonImporter.Import(target, json1);

            Assert.IsFalse(report.HasErrors,
                "Import of literal ToJson() output must not error:\n"
                + string.Join("\n", report.Errors));
            Assert.IsEmpty(report.Unknown,
                "Literal ToJson() output must contain no unknown fields:\n"
                + string.Join("\n", report.Unknown));
            Assert.IsEmpty(report.AbsentPreserved,
                "Literal ToJson() output must cover every known field:\n"
                + string.Join("\n", report.AbsentPreserved));

            string json2 = target.ToJson();
            Assert.AreEqual(FilterExcludedLines(json1), FilterExcludedLines(json2),
                "Re-export must match original except asset / stageTogglesNote / derived.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void Import_UnknownField_IsReportedNotSilentlyDropped()
    {
        MapGenerationPreset source = MakeNonDefaultPreset();
        MapGenerationPreset target = ScriptableObject.CreateInstance<MapGenerationPreset>();
        try
        {
            // Inject a deliberately unknown group + field (DoD requirement).
            string json = source.ToJson().Replace(
                "  \"runInputs\": {",
                "  \"bogusGroup\": { \"bogusField\": 42 },\n  \"runInputs\": {");

            PresetImportReport report = MapGenerationPresetJsonImporter.Import(target, json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Errors));
            CollectionAssert.Contains(report.Unknown, "bogusGroup.bogusField");
            Assert.AreEqual(source.seed, target.seed,
                "Known fields must still apply around the unknown one.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void Import_AbsentField_PreservesPreviousValue_AndReportsIt()
    {
        MapGenerationPreset source = MakeNonDefaultPreset();
        MapGenerationPreset target = ScriptableObject.CreateInstance<MapGenerationPreset>();
        try
        {
            const float sentinel = 0.123456f;
            target.waterThreshold01 = sentinel;

            // Remove the waterThreshold01 line entirely.
            var kept = new List<string>();
            foreach (string line in source.ToJson().Split('\n'))
                if (!line.TrimStart().StartsWith("\"waterThreshold01\":"))
                    kept.Add(line);
            string json = string.Join("\n", kept);

            PresetImportReport report = MapGenerationPresetJsonImporter.Import(target, json);

            CollectionAssert.Contains(report.AbsentPreserved, "waterAndShore.waterThreshold01");
            Assert.AreEqual(sentinel, target.waterThreshold01,
                "Absent field must preserve the target's previous value.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void Import_AssetSource_ReportsIgnoredWithInlineNote_AndAppliesValuesInline()
    {
        MapGenerationPreset source = MakeNonDefaultPreset();
        MapGenerationPreset target = ScriptableObject.CreateInstance<MapGenerationPreset>();
        try
        {
            // Simulate a dump produced with a terrain NoiseSettingsAsset assigned.
            string json = source.ToJson().Replace(
                "\"source\": \"inline\", \"noiseType\": \"Worley\"",
                "\"source\": \"asset:SomeNoiseAsset\", \"noiseType\": \"Worley\"");
            StringAssert.Contains("asset:SomeNoiseAsset", json,
                "fixture self-check: replacement must have targeted the terrain slot");

            PresetImportReport report = MapGenerationPresetJsonImporter.Import(target, json);

            Assert.IsFalse(report.HasErrors, string.Join("\n", report.Errors));
            Assert.IsTrue(
                report.Ignored.Exists(s => s.Contains("asset:SomeNoiseAsset")
                                        && s.Contains("INLINE")),
                "Unresolvable asset source must be reported, not silently dropped.");
            Assert.AreEqual(TerrainNoiseType.Worley, target.terrainNoiseSettings.noiseType,
                "Dumped values must be applied to the inline struct.");
            Assert.IsNull(target.terrainNoiseAsset,
                "Import must never touch the asset slot.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }

    [Test]
    public void Import_MalformedValue_ErrorsAndLeavesFieldUntouched()
    {
        MapGenerationPreset source = MakeNonDefaultPreset();
        MapGenerationPreset target = ScriptableObject.CreateInstance<MapGenerationPreset>();
        try
        {
            const int sentinel = 77;
            target.resolution = sentinel;

            string json = source.ToJson().Replace(
                "\"resolution\": 192", "\"resolution\": banana");

            PresetImportReport report = MapGenerationPresetJsonImporter.Import(target, json);

            Assert.IsTrue(report.HasErrors, "Malformed value must surface as an error.");
            Assert.IsTrue(report.Errors.Exists(e => e.Contains("runInputs.resolution")));
            Assert.AreEqual(sentinel, target.resolution,
                "Malformed field must not be applied.");
            Assert.AreEqual(source.seed, target.seed,
                "Other fields must still apply.");
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }
}