using UnityEngine;

namespace Islands.PCG.Layout.Maps
{
    /// <summary>
    /// Reusable noise configuration asset. Wraps a single <see cref="TerrainNoiseSettings"/>
    /// struct as a ScriptableObject, created via the Asset menu.
    ///
    /// Can be shared across multiple <see cref="MapGenerationPreset"/> assets and
    /// visualization components. Override-at-resolve pattern: when assigned to a
    /// preset or component's noise asset slot, the asset's settings replace the
    /// inline <see cref="TerrainNoiseSettings"/> values. When null, inline values
    /// are used unchanged (fully backward-compatible).
    ///
    /// The custom PropertyDrawer (<c>TerrainNoiseSettingsDrawer</c>) provides
    /// conditional field visibility in the Inspector: Worley-specific fields are
    /// hidden when noiseType is not Worley; ridged fields are hidden when
    /// fractalMode is Standard.
    ///
    /// Phase N5.b.
    /// </summary>
    [CreateAssetMenu(
        fileName = "NoiseSettingsAsset",
        menuName = "Islands/PCG/Noise Settings",
        order = 110)]
    public sealed class NoiseSettingsAsset : ScriptableObject
    {
        [SerializeField] private TerrainNoiseSettings settings = TerrainNoiseSettings.DefaultTerrain;

        /// <summary>The noise configuration stored in this asset.</summary>
        public TerrainNoiseSettings Settings => settings;
    }
}