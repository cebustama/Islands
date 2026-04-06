using UnityEditor;
using UnityEngine;
using Islands.PCG.Adapters.Tilemap;

namespace Islands.PCG.Editor
{
    /// <summary>
    /// Custom Inspector for <see cref="PCGMapTilemapVisualization"/>.
    /// When a MapGenerationPreset is assigned, hides the inline pipeline parameter
    /// fields. Section headers come from [Header] attributes on the component fields.
    /// Phase N2. Post-N2: scalar heatmap tilemap fields.
    /// </summary>
    [CustomEditor(typeof(PCGMapTilemapVisualization))]
    public sealed class PCGMapTilemapVisualizationEditor : UnityEditor.Editor
    {
        // Always visible
        private SerializedProperty tilemap, preset, tilesetConfig;
        private SerializedProperty flipY;
        private SerializedProperty enableMultiLayer, overlayTilemap, colliderTilemap, colliderTile, enableColliderAutoSetup;
        private SerializedProperty useProceduralTiles, proceduralColorTable, proceduralFallbackColor;
        private SerializedProperty enableScalarOverlay, overlayField, overlayMin, overlayMax, overlayColorLow, overlayColorHigh;
        private SerializedProperty scalarHeatmapTilemap, heatmapAlpha;

        // Preset-controlled (hidden when preset assigned)
        private SerializedProperty seed, resolution;
        private SerializedProperty enableHillsStage, enableShoreStage, enableVegetationStage, enableTraversalStage, enableMorphologyStage;
        private SerializedProperty islandRadius01, islandAspectRatio, warpAmplitude01, islandSmoothFrom01, islandSmoothTo01;
        private SerializedProperty waterThreshold01, shallowWaterDepth01, midWaterDepth01;
        private SerializedProperty noiseCellSize, noiseAmplitude, quantSteps;
        private SerializedProperty heightRedistributionExponent, heightRemapCurve;
        private SerializedProperty clearBeforeRun;

        private void OnEnable()
        {
            tilemap = serializedObject.FindProperty("tilemap");
            preset = serializedObject.FindProperty("preset");
            tilesetConfig = serializedObject.FindProperty("tilesetConfig");
            seed = serializedObject.FindProperty("seed");
            resolution = serializedObject.FindProperty("resolution");
            enableHillsStage = serializedObject.FindProperty("enableHillsStage");
            enableShoreStage = serializedObject.FindProperty("enableShoreStage");
            enableVegetationStage = serializedObject.FindProperty("enableVegetationStage");
            enableTraversalStage = serializedObject.FindProperty("enableTraversalStage");
            enableMorphologyStage = serializedObject.FindProperty("enableMorphologyStage");
            islandRadius01 = serializedObject.FindProperty("islandRadius01");
            islandAspectRatio = serializedObject.FindProperty("islandAspectRatio");
            warpAmplitude01 = serializedObject.FindProperty("warpAmplitude01");
            islandSmoothFrom01 = serializedObject.FindProperty("islandSmoothFrom01");
            islandSmoothTo01 = serializedObject.FindProperty("islandSmoothTo01");
            waterThreshold01 = serializedObject.FindProperty("waterThreshold01");
            shallowWaterDepth01 = serializedObject.FindProperty("shallowWaterDepth01");
            midWaterDepth01 = serializedObject.FindProperty("midWaterDepth01");
            noiseCellSize = serializedObject.FindProperty("noiseCellSize");
            noiseAmplitude = serializedObject.FindProperty("noiseAmplitude");
            quantSteps = serializedObject.FindProperty("quantSteps");
            heightRedistributionExponent = serializedObject.FindProperty("heightRedistributionExponent");
            heightRemapCurve = serializedObject.FindProperty("heightRemapCurve");
            clearBeforeRun = serializedObject.FindProperty("clearBeforeRun");
            flipY = serializedObject.FindProperty("flipY");
            enableMultiLayer = serializedObject.FindProperty("enableMultiLayer");
            overlayTilemap = serializedObject.FindProperty("overlayTilemap");
            colliderTilemap = serializedObject.FindProperty("colliderTilemap");
            colliderTile = serializedObject.FindProperty("colliderTile");
            enableColliderAutoSetup = serializedObject.FindProperty("enableColliderAutoSetup");
            useProceduralTiles = serializedObject.FindProperty("useProceduralTiles");
            proceduralColorTable = serializedObject.FindProperty("proceduralColorTable");
            proceduralFallbackColor = serializedObject.FindProperty("proceduralFallbackColor");
            enableScalarOverlay = serializedObject.FindProperty("enableScalarOverlay");
            overlayField = serializedObject.FindProperty("overlayField");
            overlayMin = serializedObject.FindProperty("overlayMin");
            overlayMax = serializedObject.FindProperty("overlayMax");
            overlayColorLow = serializedObject.FindProperty("overlayColorLow");
            overlayColorHigh = serializedObject.FindProperty("overlayColorHigh");
            scalarHeatmapTilemap = serializedObject.FindProperty("scalarHeatmapTilemap");
            heatmapAlpha = serializedObject.FindProperty("heatmapAlpha");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool hasPreset = ((PCGMapTilemapVisualization)target).HasPreset;

            // --- Always visible: Tilemap Target, Preset, Tileset Config ---
            EditorGUILayout.PropertyField(tilemap);   // draws [Header("Tilemap Target")]
            EditorGUILayout.PropertyField(preset);    // draws [Header("Preset")]

            if (hasPreset)
                EditorGUILayout.HelpBox(
                    "Pipeline parameters are read from the assigned preset asset.",
                    MessageType.Info);

            EditorGUILayout.PropertyField(tilesetConfig); // draws [Header("Tileset Config")]

            // --- Preset-controlled (hidden when preset assigned) ---
            if (!hasPreset)
            {
                // Matches MapGenerationPreset section order exactly.
                EditorGUILayout.PropertyField(seed);       // draws [Header("Run Inputs")]
                EditorGUILayout.PropertyField(resolution);

                EditorGUILayout.PropertyField(enableHillsStage);  // draws [Header("Stage Toggles")]
                EditorGUILayout.PropertyField(enableShoreStage);
                EditorGUILayout.PropertyField(enableVegetationStage);
                EditorGUILayout.PropertyField(enableTraversalStage);
                EditorGUILayout.PropertyField(enableMorphologyStage);

                EditorGUILayout.PropertyField(islandRadius01);  // draws [Header("Island Shape")]
                EditorGUILayout.PropertyField(islandAspectRatio);
                EditorGUILayout.PropertyField(warpAmplitude01);
                EditorGUILayout.PropertyField(islandSmoothFrom01);
                EditorGUILayout.PropertyField(islandSmoothTo01);

                EditorGUILayout.PropertyField(waterThreshold01);  // draws [Header("Water & Shore")]
                EditorGUILayout.PropertyField(shallowWaterDepth01);
                EditorGUILayout.PropertyField(midWaterDepth01);

                EditorGUILayout.PropertyField(noiseCellSize);  // draws [Header("Terrain Noise")]
                EditorGUILayout.PropertyField(noiseAmplitude);
                EditorGUILayout.PropertyField(quantSteps);

                EditorGUILayout.PropertyField(heightRedistributionExponent); // draws [Header("Height Redistribution (J2)")]
                EditorGUILayout.PropertyField(heightRemapCurve);             // draws [Header("Height Remap (N2)")]
                EditorGUILayout.PropertyField(clearBeforeRun);               // draws [Header("Run Behavior")]
            }

            // --- Always visible: component-specific ---
            EditorGUILayout.PropertyField(flipY);  // draws [Header("Tilemap Options")]

            EditorGUILayout.PropertyField(enableMultiLayer);  // draws [Header("Multi-layer Tilemaps (H5)")]
            if (enableMultiLayer.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(overlayTilemap);
                EditorGUILayout.PropertyField(colliderTilemap);
                EditorGUILayout.PropertyField(colliderTile);
                EditorGUILayout.PropertyField(enableColliderAutoSetup);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(useProceduralTiles);  // draws [Header("Procedural Tiles")]
            if (useProceduralTiles.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(proceduralColorTable, true);
                EditorGUILayout.PropertyField(proceduralFallbackColor);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(enableScalarOverlay);  // draws [Header("Scalar Field Overlay")]
            if (enableScalarOverlay.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(overlayField);
                EditorGUILayout.PropertyField(overlayMin);
                EditorGUILayout.PropertyField(overlayMax);
                EditorGUILayout.PropertyField(overlayColorLow);
                EditorGUILayout.PropertyField(overlayColorHigh);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.PropertyField(scalarHeatmapTilemap);  // draws [Header("Scalar Heatmap Tilemap")]
            if (scalarHeatmapTilemap.objectReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(heatmapAlpha);
                EditorGUI.indentLevel--;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}