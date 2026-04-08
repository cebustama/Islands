using UnityEditor;
using UnityEngine;
using Islands;
using Islands.PCG.Layout.Maps;

namespace Islands.PCG.Editor
{
    /// <summary>
    /// Custom PropertyDrawer for <see cref="TerrainNoiseSettings"/>.
    ///
    /// Provides conditional field visibility in the Inspector:
    /// - Worley-specific fields (worleyDistanceMetric, worleyFunction) are hidden
    ///   when noiseType is not <see cref="TerrainNoiseType.Worley"/>.
    /// - Ridged-specific fields (ridgedOffset, ridgedGain) are hidden when
    ///   fractalMode is <see cref="FractalMode.Standard"/>.
    /// - All other fields (noiseType, frequency, octaves, lacunarity, persistence,
    ///   amplitude, fractalMode) are always visible.
    ///
    /// Used automatically wherever <see cref="TerrainNoiseSettings"/> is serialized:
    /// <see cref="NoiseSettingsAsset"/> Inspector, <see cref="MapGenerationPreset"/>
    /// Inspector, and all visualization component Inspectors.
    ///
    /// Phase N5.b. Updated N5.c (FractalMode namespace migration).
    /// </summary>
    [CustomPropertyDrawer(typeof(TerrainNoiseSettings))]
    public sealed class TerrainNoiseSettingsDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // Foldout header
            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = position.y + lineHeight + spacing;

            // Always visible: core noise parameters
            y = DrawField(position, y, property, "noiseType");
            y = DrawField(position, y, property, "frequency");
            y = DrawField(position, y, property, "octaves");
            y = DrawField(position, y, property, "lacunarity");
            y = DrawField(position, y, property, "persistence");
            y = DrawField(position, y, property, "amplitude");

            // Worley-only fields (hidden when noiseType != Worley)
            var noiseTypeProp = property.FindPropertyRelative("noiseType");
            var noiseType = (TerrainNoiseType)noiseTypeProp.enumValueIndex;
            if (noiseType == TerrainNoiseType.Worley)
            {
                y = DrawField(position, y, property, "worleyDistanceMetric");
                y = DrawField(position, y, property, "worleyFunction");
            }

            // Always visible: fractal mode
            y = DrawField(position, y, property, "fractalMode");

            // Ridged-only fields (hidden when fractalMode == Standard)
            var fractalModeProp = property.FindPropertyRelative("fractalMode");
            var fractalMode = (FractalMode)fractalModeProp.enumValueIndex;
            if (fractalMode == FractalMode.Ridged)
            {
                y = DrawField(position, y, property, "ridgedOffset");
                y = DrawField(position, y, property, "ridgedGain");
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // 1 foldout + 6 base fields + 1 fractalMode = 8 lines always
            int lines = 8;

            // Conditional lines
            var noiseTypeProp = property.FindPropertyRelative("noiseType");
            if (noiseTypeProp != null && (TerrainNoiseType)noiseTypeProp.enumValueIndex == TerrainNoiseType.Worley)
                lines += 2; // worleyDistanceMetric, worleyFunction

            var fractalModeProp = property.FindPropertyRelative("fractalMode");
            if (fractalModeProp != null && (FractalMode)fractalModeProp.enumValueIndex == FractalMode.Ridged)
                lines += 2; // ridgedOffset, ridgedGain

            return lines * (lineHeight + spacing);
        }

        private static float DrawField(Rect position, float y, SerializedProperty parent, string name)
        {
            var prop = parent.FindPropertyRelative(name);
            if (prop == null) return y;

            float h = EditorGUIUtility.singleLineHeight;
            Rect rect = new Rect(position.x, y, position.width, h);
            EditorGUI.PropertyField(rect, prop);
            return y + h + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}