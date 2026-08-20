// Phase X1.a — gates for MapGenerationPresetDiagnostics.
// One preset per rule + a clean preset that fires nothing + diff filter gates.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Islands.PCG.Editor;
using Islands.PCG.Layout.Maps;
using Islands.PCG.Samples;

public sealed class MapGenerationPresetDiagnosticsTests
{
    private readonly List<Object> _toDestroy = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object o in _toDestroy)
            if (o != null) Object.DestroyImmediate(o);
        _toDestroy.Clear();
    }

    /// <summary>
    /// Baseline that fires no rule: default shape values (smooth window valid,
    /// mid-water off, no hills asset, smoothFrom 0.30 >= epsilon), vegetation
    /// stage off (R6 reports static table truth otherwise), and base
    /// temperature raised so the land ceiling clears the Hot band (R5).
    /// </summary>
    private MapGenerationPreset CleanPreset()
    {
        var p = ScriptableObject.CreateInstance<MapGenerationPreset>();
        _toDestroy.Add(p);
        p.enableVegetationStage = false;
        p.biomeBaseTemperature = 1.0f; // ceiling = 1.0 − 0.25 + 0.1 + 0.025 = 0.875 >= 0.75
        return p;
    }

    private static List<string> RuleIds(List<PresetFinding> findings)
        => findings.Select(f => f.RuleId).ToList();

    [Test]
    public void CleanPreset_ProducesNoFindings()
    {
        var findings = MapGenerationPresetDiagnostics.Diagnose(CleanPreset());
        Assert.IsEmpty(findings, string.Join(" | ", RuleIds(findings)));
    }

    [Test]
    public void ReversedSmoothWindow_FiresR1_AsInferred()
    {
        var p = CleanPreset();
        p.islandSmoothFrom01 = 0.70f;
        p.islandSmoothTo01 = 0.30f;

        var findings = MapGenerationPresetDiagnostics.Diagnose(p);
        var f = findings.Single(x => x.RuleId == "R1.DegenerateFalloff");
        Assert.AreEqual(PresetFindingBacking.Inferred, f.Backing);
        Assert.IsNull(f.BackingRun);
    }

    [Test]
    public void MidWaterNotDeeperThanShallow_FiresR2()
    {
        var p = CleanPreset();
        p.shallowWaterDepth01 = 0.30f;
        p.midWaterDepth01 = 0.20f;

        var findings = MapGenerationPresetDiagnostics.Diagnose(p);
        Assert.AreEqual(1, findings.Count(x => x.RuleId == "R2.InvisibleMidWaterBand"));
    }

    [Test]
    public void HillsAssetAssignedWithZeroBlend_FiresR3()
    {
        var p = CleanPreset();
        var asset = ScriptableObject.CreateInstance<NoiseSettingsAsset>();
        _toDestroy.Add(asset);
        p.hillsNoiseAsset = asset;
        p.hillsNoiseBlend = 0f;
        p.enableHillsStage = true;

        var findings = MapGenerationPresetDiagnostics.Diagnose(p);
        Assert.AreEqual(1, findings.Count(x => x.RuleId == "R3.InertHillsNoise"));
    }

    [Test]
    public void SmoothFromZero_FiresR4_AsMeasuredWithNamedRun()
    {
        var p = CleanPreset();
        p.islandSmoothFrom01 = 0f; // default terrain amplitude (0.35) > 0

        var findings = MapGenerationPresetDiagnostics.Diagnose(p);
        var f = findings.Single(x => x.RuleId == "R4.ClampSaturationPlateau");
        Assert.AreEqual(PresetFindingBacking.Measured, f.Backing);
        Assert.AreEqual(MapGenerationPresetDiagnostics.RunWAuxC23, f.BackingRun);
    }

    [Test]
    public void DefaultTemperatureParams_FireR5_AsMeasuredWithNamedRun()
    {
        var p = CleanPreset();
        p.biomeBaseTemperature = 0.7f; // ceiling = 0.7 − 0.25 + 0.1 + 0.025 = 0.575 < 0.75

        var findings = MapGenerationPresetDiagnostics.Diagnose(p);
        var f = findings.Single(x => x.RuleId == "R5.HotBandUnreachable");
        Assert.AreEqual(PresetFindingBacking.Measured, f.Backing);
        Assert.AreEqual(MapGenerationPresetDiagnostics.RunWAuxC23, f.BackingRun);
    }

    [Test]
    public void VegetationEnabled_FiresR6_ListingSubStepBiomesOnly()
    {
        var p = CleanPreset();
        p.enableVegetationStage = true;

        var findings = MapGenerationPresetDiagnostics.Diagnose(p);
        var f = findings.Single(x => x.RuleId == "R6.ZeroCoverageDensities");
        StringAssert.Contains("Shrubland", f.Message);   // 0.25 — below the step
        StringAssert.Contains("Grassland", f.Message);   // 0.15 — below the step
        StringAssert.DoesNotContain("Snow", f.Message);  // density 0 is intentional, excluded
        Assert.AreEqual(PresetFindingBacking.Measured, f.Backing);
    }

    // ------------------------------------------------------------------
    // Diff gates
    // ------------------------------------------------------------------

    [Test]
    public void DiffJson_IdenticalPreset_IsEmpty()
    {
        var p = CleanPreset();
        string json = p.ToJson();
        Assert.IsEmpty(MapGenerationPresetDiagnostics.DiffJson(json, json));
    }

    [Test]
    public void DiffJson_SeedChange_ReportsBothSides()
    {
        var a = CleanPreset();
        string jsonA = a.ToJson();
        a.seed = 999u;
        string jsonB = a.ToJson();

        var diff = MapGenerationPresetDiagnostics.DiffJson(jsonA, jsonB);
        Assert.IsTrue(diff.Any(l => l.StartsWith("−") && l.Contains("seed")), string.Join(" | ", diff));
        Assert.IsTrue(diff.Any(l => l.StartsWith("+") && l.Contains("999")), string.Join(" | ", diff));
    }

    [Test]
    public void DiffJson_AssetNameDifference_IsFilteredOut()
    {
        var a = CleanPreset();
        var b = CleanPreset();
        a.name = "PresetA";
        b.name = "PresetB"; // only the excluded "asset" line differs

        Assert.IsEmpty(MapGenerationPresetDiagnostics.DiffJson(a.ToJson(), b.ToJson()));
    }
}