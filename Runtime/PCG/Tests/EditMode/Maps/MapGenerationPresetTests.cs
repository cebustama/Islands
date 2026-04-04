using NUnit.Framework;
using UnityEngine;
using Islands.PCG.Samples;
using Islands.PCG.Layout.Maps;

/// <summary>
/// EditMode tests for <see cref="MapGenerationPreset"/>.
///
/// Verifies default field values, <see cref="MapGenerationPreset.ToTunables"/> output,
/// and MapTunables2D clamping/ordering behavior triggered through the preset.
///
/// Phase H3.
/// </summary>
[TestFixture]
public class MapGenerationPresetTests
{
    private MapGenerationPreset _preset;

    [SetUp]
    public void SetUp()
    {
        _preset = ScriptableObject.CreateInstance<MapGenerationPreset>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_preset);
    }

    // ------------------------------------------------------------------
    // Default values
    // ------------------------------------------------------------------

    [Test]
    public void DefaultValues_RunInputs_Match()
    {
        Assert.AreEqual(1u, _preset.seed);
        Assert.AreEqual(64, _preset.resolution);
    }

    [Test]
    public void DefaultValues_StageToggles_AllTrue()
    {
        Assert.IsTrue(_preset.enableHillsStage);
        Assert.IsTrue(_preset.enableShoreStage);
        Assert.IsTrue(_preset.enableVegetationStage);
        Assert.IsTrue(_preset.enableTraversalStage);
        Assert.IsTrue(_preset.enableMorphologyStage);
    }

    [Test]
    public void DefaultValues_ShapeTunables_MatchComponentDefaults()
    {
        Assert.AreEqual(0.45f, _preset.islandRadius01, 1e-6f);
        Assert.AreEqual(0.50f, _preset.waterThreshold01, 1e-6f);
        Assert.AreEqual(0.30f, _preset.islandSmoothFrom01, 1e-6f);
        Assert.AreEqual(0.70f, _preset.islandSmoothTo01, 1e-6f);
        Assert.AreEqual(1.00f, _preset.islandAspectRatio, 1e-6f);
        Assert.AreEqual(0.00f, _preset.warpAmplitude01, 1e-6f);
    }

    [Test]
    public void DefaultValues_NoiseTunables_MatchComponentDefaults()
    {
        Assert.AreEqual(8, _preset.noiseCellSize);
        Assert.AreEqual(0.18f, _preset.noiseAmplitude, 1e-6f);
        Assert.AreEqual(1024, _preset.quantSteps);
        Assert.IsTrue(_preset.clearBeforeRun);
    }

    // ------------------------------------------------------------------
    // ToTunables
    // ------------------------------------------------------------------

    [Test]
    public void ToTunables_DefaultPreset_MatchesMapTunables2DDefault()
    {
        MapTunables2D fromPreset = _preset.ToTunables();
        MapTunables2D expected = MapTunables2D.Default;

        Assert.AreEqual(expected.islandRadius01, fromPreset.islandRadius01, 1e-5f);
        Assert.AreEqual(expected.waterThreshold01, fromPreset.waterThreshold01, 1e-5f);
        Assert.AreEqual(expected.islandSmoothFrom01, fromPreset.islandSmoothFrom01, 1e-5f);
        Assert.AreEqual(expected.islandSmoothTo01, fromPreset.islandSmoothTo01, 1e-5f);
        Assert.AreEqual(expected.islandAspectRatio, fromPreset.islandAspectRatio, 1e-5f);
        Assert.AreEqual(expected.warpAmplitude01, fromPreset.warpAmplitude01, 1e-5f);
    }

    [Test]
    public void ToTunables_CustomFields_AreForwardedCorrectly()
    {
        _preset.islandRadius01 = 0.30f;
        _preset.waterThreshold01 = 0.65f;
        _preset.islandSmoothFrom01 = 0.20f;
        _preset.islandSmoothTo01 = 0.80f;
        _preset.islandAspectRatio = 1.50f;
        _preset.warpAmplitude01 = 0.25f;

        MapTunables2D t = _preset.ToTunables();

        Assert.AreEqual(0.30f, t.islandRadius01, 1e-5f);
        Assert.AreEqual(0.65f, t.waterThreshold01, 1e-5f);
        Assert.AreEqual(0.20f, t.islandSmoothFrom01, 1e-5f);
        Assert.AreEqual(0.80f, t.islandSmoothTo01, 1e-5f);
        Assert.AreEqual(1.50f, t.islandAspectRatio, 1e-5f);
        Assert.AreEqual(0.25f, t.warpAmplitude01, 1e-5f);
    }

    [Test]
    public void ToTunables_SmoothFromGreaterThanTo_IsAutoOrdered()
    {
        // MapTunables2D guarantees from <= to by swapping if necessary.
        _preset.islandSmoothFrom01 = 0.80f;
        _preset.islandSmoothTo01 = 0.20f;

        MapTunables2D t = _preset.ToTunables();

        Assert.LessOrEqual(t.islandSmoothFrom01, t.islandSmoothTo01,
            "MapTunables2D must always produce from <= to.");
    }

    [Test]
    public void ToTunables_OutOfRangeAspect_IsClamped()
    {
        _preset.islandAspectRatio = 99f; // above [0.25..4.0] clamp

        MapTunables2D t = _preset.ToTunables();

        Assert.LessOrEqual(t.islandAspectRatio, 4.0f);
    }
}