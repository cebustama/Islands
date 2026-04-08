using NUnit.Framework;
using UnityEngine;
using Islands.PCG.Samples;
using Islands.PCG.Layout.Maps;
using Islands;

/// <summary>
/// EditMode tests for <see cref="MapGenerationPreset"/>.
///
/// Verifies default field values, <see cref="MapGenerationPreset.ToTunables"/> output,
/// and MapTunables2D clamping/ordering behavior triggered through the preset.
///
/// Phase H3. Phase N4: noise field defaults updated. Phase F3b: hills threshold fields.
/// Phase N5.a: shapeMode field.
/// Phase N5.b: NoiseSettingsAsset slots. Refactored individual noise fields to
///             embedded TerrainNoiseSettings structs. New field defaults.
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
        Assert.AreEqual(TerrainNoiseType.Perlin, _preset.terrainNoiseSettings.noiseType);
        Assert.AreEqual(8, _preset.terrainNoiseSettings.frequency);
        Assert.AreEqual(4, _preset.terrainNoiseSettings.octaves);
        Assert.AreEqual(2, _preset.terrainNoiseSettings.lacunarity);
        Assert.AreEqual(0.5f, _preset.terrainNoiseSettings.persistence, 1e-6f);
        Assert.AreEqual(0.35f, _preset.terrainNoiseSettings.amplitude, 1e-6f);
    }

    [Test]
    public void DefaultValues_WarpNoise_MatchN4Defaults()
    {
        Assert.AreEqual(TerrainNoiseType.Perlin, _preset.warpNoiseSettings.noiseType);
        Assert.AreEqual(4, _preset.warpNoiseSettings.frequency);
        Assert.AreEqual(1, _preset.warpNoiseSettings.octaves);
        Assert.AreEqual(2, _preset.warpNoiseSettings.lacunarity);
        Assert.AreEqual(0.5f, _preset.warpNoiseSettings.persistence, 1e-6f);
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
    // N5.b: new field defaults
    // ------------------------------------------------------------------

    [Test]
    public void DefaultValues_TerrainNoise_N5bFieldsHaveIdentityDefaults()
    {
        Assert.AreEqual(WorleyDistanceMetric.Euclidean, _preset.terrainNoiseSettings.worleyDistanceMetric);
        Assert.AreEqual(WorleyFunction.F1, _preset.terrainNoiseSettings.worleyFunction);
        Assert.AreEqual(FractalMode.Standard, _preset.terrainNoiseSettings.fractalMode);
        Assert.AreEqual(1.0f, _preset.terrainNoiseSettings.ridgedOffset, 1e-6f);
        Assert.AreEqual(2.0f, _preset.terrainNoiseSettings.ridgedGain, 1e-6f);
    }

    [Test]
    public void DefaultValues_WarpNoise_N5bFieldsHaveIdentityDefaults()
    {
        Assert.AreEqual(WorleyDistanceMetric.Euclidean, _preset.warpNoiseSettings.worleyDistanceMetric);
        Assert.AreEqual(WorleyFunction.F1, _preset.warpNoiseSettings.worleyFunction);
        Assert.AreEqual(FractalMode.Standard, _preset.warpNoiseSettings.fractalMode);
        Assert.AreEqual(1.0f, _preset.warpNoiseSettings.ridgedOffset, 1e-6f);
        Assert.AreEqual(2.0f, _preset.warpNoiseSettings.ridgedGain, 1e-6f);
    }

    [Test]
    public void DefaultValues_NoiseAssets_AreNull()
    {
        Assert.IsNull(_preset.terrainNoiseAsset);
        Assert.IsNull(_preset.warpNoiseAsset);
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
        _preset.terrainNoiseSettings.noiseType = TerrainNoiseType.Simplex;
        _preset.terrainNoiseSettings.frequency = 16;
        _preset.terrainNoiseSettings.octaves = 6;
        _preset.terrainNoiseSettings.amplitude = 0.50f;
        _preset.warpNoiseSettings.noiseType = TerrainNoiseType.Value;
        _preset.warpNoiseSettings.frequency = 2;
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

    // ------------------------------------------------------------------
    // N5.a Shape Mode
    // ------------------------------------------------------------------

    [Test]
    public void DefaultValues_ShapeMode_IsEllipse()
    {
        Assert.AreEqual(IslandShapeMode.Ellipse, _preset.shapeMode);
    }

    [Test]
    public void ToTunables_DefaultPreset_ShapeModeMatchesDefault()
    {
        MapTunables2D fromPreset = _preset.ToTunables();
        MapTunables2D expected = MapTunables2D.Default;

        Assert.AreEqual(expected.shapeMode, fromPreset.shapeMode);
    }

    [Test]
    public void ToTunables_ShapeMode_IsForwardedCorrectly()
    {
        _preset.shapeMode = IslandShapeMode.Rectangle;
        Assert.AreEqual(IslandShapeMode.Rectangle, _preset.ToTunables().shapeMode);

        _preset.shapeMode = IslandShapeMode.NoShape;
        Assert.AreEqual(IslandShapeMode.NoShape, _preset.ToTunables().shapeMode);

        _preset.shapeMode = IslandShapeMode.Custom;
        Assert.AreEqual(IslandShapeMode.Custom, _preset.ToTunables().shapeMode);

        _preset.shapeMode = IslandShapeMode.Ellipse;
        Assert.AreEqual(IslandShapeMode.Ellipse, _preset.ToTunables().shapeMode);
    }

    // ------------------------------------------------------------------
    // N5.b NoiseSettingsAsset resolution
    // ------------------------------------------------------------------

    [Test]
    public void ToTunables_WithTerrainNoiseAsset_ReadsFromAsset()
    {
        var asset = ScriptableObject.CreateInstance<NoiseSettingsAsset>();
        try
        {
            // The asset's settings field is private with a public getter.
            // Use SerializedObject to set its values for testing.
            var so = new UnityEditor.SerializedObject(asset);
            var settingsProp = so.FindProperty("settings");
            settingsProp.FindPropertyRelative("noiseType").enumValueIndex = (int)TerrainNoiseType.Value;
            settingsProp.FindPropertyRelative("frequency").intValue = 16;
            settingsProp.FindPropertyRelative("octaves").intValue = 2;
            settingsProp.FindPropertyRelative("amplitude").floatValue = 0.75f;
            so.ApplyModifiedPropertiesWithoutUndo();

            _preset.terrainNoiseAsset = asset;

            MapTunables2D t = _preset.ToTunables();

            Assert.AreEqual(TerrainNoiseType.Value, t.terrainNoise.noiseType);
            Assert.AreEqual(16, t.terrainNoise.frequency);
            Assert.AreEqual(2, t.terrainNoise.octaves);
            Assert.AreEqual(0.75f, t.terrainNoise.amplitude, 1e-5f);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    [Test]
    public void ToTunables_NullTerrainNoiseAsset_ReadsInlineSettings()
    {
        _preset.terrainNoiseAsset = null;
        _preset.terrainNoiseSettings.noiseType = TerrainNoiseType.Worley;
        _preset.terrainNoiseSettings.frequency = 12;

        MapTunables2D t = _preset.ToTunables();

        Assert.AreEqual(TerrainNoiseType.Worley, t.terrainNoise.noiseType);
        Assert.AreEqual(12, t.terrainNoise.frequency);
    }

    [Test]
    public void ToTunables_WithWarpNoiseAsset_ReadsFromAsset()
    {
        var asset = ScriptableObject.CreateInstance<NoiseSettingsAsset>();
        try
        {
            var so = new UnityEditor.SerializedObject(asset);
            var settingsProp = so.FindProperty("settings");
            settingsProp.FindPropertyRelative("noiseType").enumValueIndex = (int)TerrainNoiseType.Simplex;
            settingsProp.FindPropertyRelative("frequency").intValue = 6;
            so.ApplyModifiedPropertiesWithoutUndo();

            _preset.warpNoiseAsset = asset;

            MapTunables2D t = _preset.ToTunables();

            Assert.AreEqual(TerrainNoiseType.Simplex, t.warpNoise.noiseType);
            Assert.AreEqual(6, t.warpNoise.frequency);
        }
        finally
        {
            Object.DestroyImmediate(asset);
        }
    }

    // ------------------------------------------------------------------
    // N5.b TerrainNoiseSettings.Equals
    // ------------------------------------------------------------------

    [Test]
    public void TerrainNoiseSettings_Equals_DefaultsAreEqual()
    {
        var a = TerrainNoiseSettings.DefaultTerrain;
        var b = TerrainNoiseSettings.DefaultTerrain;
        Assert.IsTrue(a.Equals(b));
    }

    [Test]
    public void TerrainNoiseSettings_Equals_DifferentFieldsAreNotEqual()
    {
        var a = TerrainNoiseSettings.DefaultTerrain;
        var b = TerrainNoiseSettings.DefaultTerrain;
        b.frequency = 16;
        Assert.IsFalse(a.Equals(b));
    }

    [Test]
    public void TerrainNoiseSettings_Equals_DifferentN5bFieldsAreNotEqual()
    {
        var a = TerrainNoiseSettings.DefaultTerrain;
        var b = TerrainNoiseSettings.DefaultTerrain;
        b.fractalMode = FractalMode.Ridged;
        Assert.IsFalse(a.Equals(b));

        var c = TerrainNoiseSettings.DefaultTerrain;
        c.worleyDistanceMetric = WorleyDistanceMetric.Chebyshev;
        Assert.IsFalse(a.Equals(c));
    }
}