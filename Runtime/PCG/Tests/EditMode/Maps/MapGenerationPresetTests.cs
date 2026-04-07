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
/// Phase H3. Phase N4: noise field defaults updated. Phase F3b: hills threshold fields.
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
    public void DefaultValues_TerrainNoise_MatchN4Defaults()
    {
        Assert.AreEqual(TerrainNoiseType.Perlin, _preset.terrainNoiseType);
        Assert.AreEqual(8, _preset.terrainFrequency);
        Assert.AreEqual(4, _preset.terrainOctaves);
        Assert.AreEqual(2, _preset.terrainLacunarity);
        Assert.AreEqual(0.5f, _preset.terrainPersistence, 1e-6f);
        Assert.AreEqual(0.35f, _preset.terrainAmplitude, 1e-6f);
    }

    [Test]
    public void DefaultValues_WarpNoise_MatchN4Defaults()
    {
        Assert.AreEqual(TerrainNoiseType.Perlin, _preset.warpNoiseType);
        Assert.AreEqual(4, _preset.warpFrequency);
        Assert.AreEqual(1, _preset.warpOctaves);
        Assert.AreEqual(2, _preset.warpLacunarity);
        Assert.AreEqual(0.5f, _preset.warpPersistence, 1e-6f);
    }

    [Test]
    public void DefaultValues_HeightQuantSteps_MatchN4Default()
    {
        Assert.AreEqual(1024, _preset.heightQuantSteps);
    }

    [Test]
    public void DefaultValues_RunBehavior_ClearBeforeRunTrue()
    {
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
    public void ToTunables_DefaultPreset_NoiseSettingsMatchDefaults()
    {
        MapTunables2D t = _preset.ToTunables();

        // Terrain noise
        Assert.AreEqual(TerrainNoiseType.Perlin, t.terrainNoise.noiseType);
        Assert.AreEqual(8, t.terrainNoise.frequency);
        Assert.AreEqual(4, t.terrainNoise.octaves);
        Assert.AreEqual(2, t.terrainNoise.lacunarity);
        Assert.AreEqual(0.5f, t.terrainNoise.persistence, 1e-5f);
        Assert.AreEqual(0.35f, t.terrainNoise.amplitude, 1e-5f);

        // Warp noise
        Assert.AreEqual(TerrainNoiseType.Perlin, t.warpNoise.noiseType);
        Assert.AreEqual(4, t.warpNoise.frequency);
        Assert.AreEqual(1, t.warpNoise.octaves);
        Assert.AreEqual(1.0f, t.warpNoise.amplitude, 1e-5f);

        // Quant
        Assert.AreEqual(1024, t.heightQuantSteps);
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
    public void ToTunables_CustomNoiseFields_AreForwardedCorrectly()
    {
        _preset.terrainNoiseType = TerrainNoiseType.Simplex;
        _preset.terrainFrequency = 16;
        _preset.terrainOctaves = 6;
        _preset.terrainAmplitude = 0.50f;
        _preset.warpNoiseType = TerrainNoiseType.Value;
        _preset.warpFrequency = 2;
        _preset.heightQuantSteps = 32;

        MapTunables2D t = _preset.ToTunables();

        Assert.AreEqual(TerrainNoiseType.Simplex, t.terrainNoise.noiseType);
        Assert.AreEqual(16, t.terrainNoise.frequency);
        Assert.AreEqual(6, t.terrainNoise.octaves);
        Assert.AreEqual(0.50f, t.terrainNoise.amplitude, 1e-5f);
        Assert.AreEqual(TerrainNoiseType.Value, t.warpNoise.noiseType);
        Assert.AreEqual(2, t.warpNoise.frequency);
        Assert.AreEqual(32, t.heightQuantSteps);
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

    // ------------------------------------------------------------------
    // F3b Hills thresholds
    // ------------------------------------------------------------------

    [Test]
    public void DefaultValues_HillsThresholds_MatchF3bDefaults()
    {
        Assert.AreEqual(0.65f, _preset.hillsThresholdL1, 1e-6f);
        Assert.AreEqual(0.80f, _preset.hillsThresholdL2, 1e-6f);
    }

    [Test]
    public void ToTunables_HillsThresholds_AreForwardedCorrectly()
    {
        _preset.hillsThresholdL1 = 0.55f;
        _preset.hillsThresholdL2 = 0.90f;

        MapTunables2D t = _preset.ToTunables();

        Assert.AreEqual(0.55f, t.hillsThresholdL1, 1e-5f);
        Assert.AreEqual(0.90f, t.hillsThresholdL2, 1e-5f);
    }

    [Test]
    public void ToTunables_HillsL2BelowL1_IsClamped()
    {
        _preset.hillsThresholdL1 = 0.70f;
        _preset.hillsThresholdL2 = 0.50f; // intentionally below L1

        MapTunables2D t = _preset.ToTunables();

        Assert.GreaterOrEqual(t.hillsThresholdL2, t.hillsThresholdL1,
            "MapTunables2D must clamp hillsThresholdL2 >= hillsThresholdL1.");
    }

    [Test]
    public void ToTunables_DefaultPreset_HillsMatchMapTunables2DDefault()
    {
        MapTunables2D fromPreset = _preset.ToTunables();
        MapTunables2D expected = MapTunables2D.Default;

        Assert.AreEqual(expected.hillsThresholdL1, fromPreset.hillsThresholdL1, 1e-5f);
        Assert.AreEqual(expected.hillsThresholdL2, fromPreset.hillsThresholdL2, 1e-5f);
    }
}