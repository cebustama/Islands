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
    /// Phase N4: TerrainNoiseSettings + WarpNoiseSettings + heightQuantSteps.
    /// Phase F3b: hillsThresholdL1 / hillsThresholdL2.
    /// Phase N5.a: shapeMode (IslandShapeMode).
    /// Phase N5.b: NoiseSettingsAsset slots. Refactored individual noise fields to
    ///             embedded TerrainNoiseSettings structs drawn via PropertyDrawer.
    /// Phase N5.d: hillsNoiseBlend + hillsNoiseSettings / hillsNoiseAsset.
    /// Phase N5.e: Hills threshold UX remap — hillsThresholdL1/L2 → hillsL1/L2.
    /// Phase N6: Replaced scalar overlay with Texture2D + SpriteRenderer two-slot system.
    ///           Removed scalarHeatmapTilemap and heatmapAlpha fields.
    /// Phase H8: Mega-tile toggle + rules array (always visible, conditional children).
    /// Phase M: enableBiomeStage toggle for Climate &amp; Biome Classification.
    /// M-fix.a: 10 biome climate tunables promoted to Inspector.
    /// Phase L+M: enableHydrologyStage toggle + 3 hydrology tunables + 2 river moisture tunables.
    /// Phase Q: biomeTileOverride field (hidden when procedural tiles active).
    /// Phase W.a: logGoldenHashes diagnostics section (always visible, never preset-gated)
    ///            + moved the five preset-orphan fields (enableRegionsStage,
    ///            enableHydrologyStage, hydroEpsilon, hydroRiverThresholdFraction,
    ///            hydroMinLakeArea) out of the preset-hidden block. The component reads
    ///            those five from the inline fields unconditionally — MapGenerationPreset
    ///            does not carry them — so hiding them behind a preset made live
    ///            parameters unreachable in the Inspector.
    /// </summary>
    [CustomEditor(typeof(PCGMapTilemapVisualization))]
    public sealed class PCGMapTilemapVisualizationEditor : UnityEditor.Editor
    {
        // Always visible
        private SerializedProperty tilemap, preset, tilesetConfig;
        // Phase Q: biome tile override (hidden when procedural tiles active)
        private SerializedProperty biomeTileOverride;
        private SerializedProperty flipY;
        private SerializedProperty enableMultiLayer, overlayTilemap, colliderTilemap, colliderTile, enableColliderAutoSetup;
        // H8: mega-tiles (always visible)
        private SerializedProperty enableMegaTiles, megaTileRules;
        private SerializedProperty useProceduralTiles, proceduralColorTable, proceduralFallbackColor;

        // N6: overlay slots (always visible)
        private SerializedProperty enableOverlay1, overlaySource1, overlayMin1, overlayMax1;
        private SerializedProperty overlayColorLow1, overlayColorHigh1, overlayAlpha1;
        private SerializedProperty enableOverlay2, overlaySource2, overlayMin2, overlayMax2;
        private SerializedProperty overlayColorLow2, overlayColorHigh2, overlayAlpha2;

        // W.a: diagnostics (always visible — never preset-gated)
        private SerializedProperty logGoldenHashes;

        // W.a: preset-orphan stage toggles — the component reads these from the inline
        // fields even when a preset is assigned, so they must stay visible.
        private SerializedProperty enableRegionsStage, enableHydrologyStage;

        // Preset-controlled (hidden when preset assigned)
        private SerializedProperty seed, resolution;
        private SerializedProperty enableHillsStage, enableShoreStage, enableVegetationStage, enableTraversalStage, enableMorphologyStage;
        private SerializedProperty enableBiomeStage;
        // M-fix.a: biome climate tunables
        private SerializedProperty biomeBaseTemperature, biomeLapseRate, biomeLatitudeEffect;
        private SerializedProperty biomeCoastModerationStrength, biomeTempNoiseAmplitude, biomeTempNoiseCellSize;
        private SerializedProperty biomeCoastalMoistureBonus, biomeCoastDecayRate, biomeMoistureNoiseAmplitude, biomeMoistureNoiseCellSize;
        // Phase L+M: river moisture tunables
        private SerializedProperty biomeRiverMoistureBonus, biomeRiverFlowNorm;
        // Phase L: hydrology tunables — also preset-orphan (see note above).
        private SerializedProperty hydroEpsilon, hydroRiverThresholdFraction, hydroMinLakeArea;
        // N5.a: shape mode
        private SerializedProperty shapeMode;
        private SerializedProperty islandRadius01, islandAspectRatio, warpAmplitude01, islandSmoothFrom01, islandSmoothTo01;
        private SerializedProperty waterThreshold01, shallowWaterDepth01, midWaterDepth01;
        // N5.b: noise settings assets
        private SerializedProperty terrainNoiseAsset, warpNoiseAsset;
        // N5.d: hills noise asset
        private SerializedProperty hillsNoiseAsset;
        // N5.b: embedded noise structs (drawn via TerrainNoiseSettingsDrawer)
        private SerializedProperty terrainNoiseSettings, warpNoiseSettings;
        // N5.d: hills noise struct
        private SerializedProperty hillsNoiseSettings;
        // N4: height quantization
        private SerializedProperty heightQuantSteps;
        private SerializedProperty heightRedistributionExponent, heightRemapCurve;
        // F3b / N5.e: hills relative fractions
        private SerializedProperty hillsL1, hillsL2;
        // N5.d: hills noise blend
        private SerializedProperty hillsNoiseBlend;
        private SerializedProperty clearBeforeRun;

        private void OnEnable()
        {
            tilemap = serializedObject.FindProperty("tilemap");
            preset = serializedObject.FindProperty("preset");
            tilesetConfig = serializedObject.FindProperty("tilesetConfig");
            // Phase Q
            biomeTileOverride = serializedObject.FindProperty("biomeTileOverride");
            seed = serializedObject.FindProperty("seed");
            resolution = serializedObject.FindProperty("resolution");
            // W.a: diagnostics
            logGoldenHashes = serializedObject.FindProperty("logGoldenHashes");
            enableHillsStage = serializedObject.FindProperty("enableHillsStage");
            enableShoreStage = serializedObject.FindProperty("enableShoreStage");
            enableVegetationStage = serializedObject.FindProperty("enableVegetationStage");
            enableTraversalStage = serializedObject.FindProperty("enableTraversalStage");
            enableMorphologyStage = serializedObject.FindProperty("enableMorphologyStage");
            enableBiomeStage = serializedObject.FindProperty("enableBiomeStage");
            enableRegionsStage = serializedObject.FindProperty("enableRegionsStage");
            // Phase L: hydrology toggle
            enableHydrologyStage = serializedObject.FindProperty("enableHydrologyStage");
            // M-fix.a: biome climate tunables
            biomeBaseTemperature = serializedObject.FindProperty("biomeBaseTemperature");
            biomeLapseRate = serializedObject.FindProperty("biomeLapseRate");
            biomeLatitudeEffect = serializedObject.FindProperty("biomeLatitudeEffect");
            biomeCoastModerationStrength = serializedObject.FindProperty("biomeCoastModerationStrength");
            biomeTempNoiseAmplitude = serializedObject.FindProperty("biomeTempNoiseAmplitude");
            biomeTempNoiseCellSize = serializedObject.FindProperty("biomeTempNoiseCellSize");
            biomeCoastalMoistureBonus = serializedObject.FindProperty("biomeCoastalMoistureBonus");
            biomeCoastDecayRate = serializedObject.FindProperty("biomeCoastDecayRate");
            biomeMoistureNoiseAmplitude = serializedObject.FindProperty("biomeMoistureNoiseAmplitude");
            biomeMoistureNoiseCellSize = serializedObject.FindProperty("biomeMoistureNoiseCellSize");
            // Phase L+M: river moisture tunables
            biomeRiverMoistureBonus = serializedObject.FindProperty("biomeRiverMoistureBonus");
            biomeRiverFlowNorm = serializedObject.FindProperty("biomeRiverFlowNorm");
            // Phase L: hydrology tunables
            hydroEpsilon = serializedObject.FindProperty("hydroEpsilon");
            hydroRiverThresholdFraction = serializedObject.FindProperty("hydroRiverThresholdFraction");
            hydroMinLakeArea = serializedObject.FindProperty("hydroMinLakeArea");
            // N5.a
            shapeMode = serializedObject.FindProperty("shapeMode");
            islandRadius01 = serializedObject.FindProperty("islandRadius01");
            islandAspectRatio = serializedObject.FindProperty("islandAspectRatio");
            warpAmplitude01 = serializedObject.FindProperty("warpAmplitude01");
            islandSmoothFrom01 = serializedObject.FindProperty("islandSmoothFrom01");
            islandSmoothTo01 = serializedObject.FindProperty("islandSmoothTo01");
            waterThreshold01 = serializedObject.FindProperty("waterThreshold01");
            shallowWaterDepth01 = serializedObject.FindProperty("shallowWaterDepth01");
            midWaterDepth01 = serializedObject.FindProperty("midWaterDepth01");
            // N5.b: noise settings assets
            terrainNoiseAsset = serializedObject.FindProperty("terrainNoiseAsset");
            warpNoiseAsset = serializedObject.FindProperty("warpNoiseAsset");
            // N5.d: hills noise asset
            hillsNoiseAsset = serializedObject.FindProperty("hillsNoiseAsset");
            // N5.b: embedded noise structs
            terrainNoiseSettings = serializedObject.FindProperty("terrainNoiseSettings");
            warpNoiseSettings = serializedObject.FindProperty("warpNoiseSettings");
            // N5.d: hills noise struct
            hillsNoiseSettings = serializedObject.FindProperty("hillsNoiseSettings");
            // N4: height quantization
            heightQuantSteps = serializedObject.FindProperty("heightQuantSteps");
            heightRedistributionExponent = serializedObject.FindProperty("heightRedistributionExponent");
            heightRemapCurve = serializedObject.FindProperty("heightRemapCurve");
            // F3b / N5.e: hills relative fractions
            hillsL1 = serializedObject.FindProperty("hillsL1");
            hillsL2 = serializedObject.FindProperty("hillsL2");
            // N5.d: hills noise blend
            hillsNoiseBlend = serializedObject.FindProperty("hillsNoiseBlend");
            clearBeforeRun = serializedObject.FindProperty("clearBeforeRun");
            flipY = serializedObject.FindProperty("flipY");
            enableMultiLayer = serializedObject.FindProperty("enableMultiLayer");
            overlayTilemap = serializedObject.FindProperty("overlayTilemap");
            colliderTilemap = serializedObject.FindProperty("colliderTilemap");
            colliderTile = serializedObject.FindProperty("colliderTile");
            enableColliderAutoSetup = serializedObject.FindProperty("enableColliderAutoSetup");
            // H8: mega-tiles
            enableMegaTiles = serializedObject.FindProperty("enableMegaTiles");
            megaTileRules = serializedObject.FindProperty("megaTileRules");
            useProceduralTiles = serializedObject.FindProperty("useProceduralTiles");
            proceduralColorTable = serializedObject.FindProperty("proceduralColorTable");
            proceduralFallbackColor = serializedObject.FindProperty("proceduralFallbackColor");
            // N6: overlay slots
            enableOverlay1 = serializedObject.FindProperty("enableOverlay1");
            overlaySource1 = serializedObject.FindProperty("overlaySource1");
            overlayMin1 = serializedObject.FindProperty("overlayMin1");
            overlayMax1 = serializedObject.FindProperty("overlayMax1");
            overlayColorLow1 = serializedObject.FindProperty("overlayColorLow1");
            overlayColorHigh1 = serializedObject.FindProperty("overlayColorHigh1");
            overlayAlpha1 = serializedObject.FindProperty("overlayAlpha1");
            enableOverlay2 = serializedObject.FindProperty("enableOverlay2");
            overlaySource2 = serializedObject.FindProperty("overlaySource2");
            overlayMin2 = serializedObject.FindProperty("overlayMin2");
            overlayMax2 = serializedObject.FindProperty("overlayMax2");
            overlayColorLow2 = serializedObject.FindProperty("overlayColorLow2");
            overlayColorHigh2 = serializedObject.FindProperty("overlayColorHigh2");
            overlayAlpha2 = serializedObject.FindProperty("overlayAlpha2");
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

            // Phase Q: biome override (hidden when procedural tiles active)
            if (!useProceduralTiles.boolValue)
                EditorGUILayout.PropertyField(biomeTileOverride); // draws [Header("Biome Tile Override (Phase Q)")]

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
                EditorGUILayout.PropertyField(enableBiomeStage);
                // W.a: enableRegionsStage / enableHydrologyStage are NOT drawn here —
                // the component ignores the preset for them. Drawn always-visible below.

                EditorGUILayout.PropertyField(shapeMode);          // N5.a
                EditorGUILayout.PropertyField(islandRadius01);     // draws [Header("Island Shape")]
                EditorGUILayout.PropertyField(islandAspectRatio);
                EditorGUILayout.PropertyField(warpAmplitude01);
                EditorGUILayout.PropertyField(islandSmoothFrom01);
                EditorGUILayout.PropertyField(islandSmoothTo01);

                EditorGUILayout.PropertyField(waterThreshold01);  // draws [Header("Water & Shore")]
                EditorGUILayout.PropertyField(shallowWaterDepth01);
                EditorGUILayout.PropertyField(midWaterDepth01);

                // N5.b: noise settings assets
                EditorGUILayout.PropertyField(terrainNoiseAsset); // draws [Header("Noise Settings Assets (N5.b)")]
                EditorGUILayout.PropertyField(warpNoiseAsset);
                EditorGUILayout.PropertyField(hillsNoiseAsset);   // N5.d

                // N5.b: terrain noise struct (PropertyDrawer handles conditional visibility)
                bool hasTerrainAsset = terrainNoiseAsset.objectReferenceValue != null;
                if (hasTerrainAsset)
                    EditorGUILayout.HelpBox(
                        "Terrain noise asset assigned \u2014 inline settings below are ignored.",
                        MessageType.Info);
                using (new EditorGUI.DisabledScope(hasTerrainAsset))
                    EditorGUILayout.PropertyField(terrainNoiseSettings); // draws [Header("Terrain Noise")]

                // N5.b: warp noise struct (PropertyDrawer handles conditional visibility)
                bool hasWarpAsset = warpNoiseAsset.objectReferenceValue != null;
                if (hasWarpAsset)
                    EditorGUILayout.HelpBox(
                        "Warp noise asset assigned \u2014 inline settings below are ignored.",
                        MessageType.Info);
                using (new EditorGUI.DisabledScope(hasWarpAsset))
                    EditorGUILayout.PropertyField(warpNoiseSettings); // draws [Header("Warp Noise")]

                // N4: height quantization
                EditorGUILayout.PropertyField(heightQuantSteps);  // draws [Header("Height Quantization (N4)")]

                EditorGUILayout.PropertyField(heightRedistributionExponent); // draws [Header("Height Redistribution (J2)")]

                // F3b / N5.e: hills relative fractions + N5.d blend
                EditorGUILayout.PropertyField(hillsL1);           // draws [Header("Hills (F3b / N5.e)")]
                EditorGUILayout.PropertyField(hillsL2);
                EditorGUILayout.PropertyField(hillsNoiseBlend);   // N5.d

                // N5.d: hills noise struct
                bool hasHillsAsset = hillsNoiseAsset.objectReferenceValue != null;
                if (hasHillsAsset)
                    EditorGUILayout.HelpBox(
                        "Hills noise asset assigned \u2014 inline settings below are ignored.",
                        MessageType.Info);
                using (new EditorGUI.DisabledScope(hasHillsAsset))
                    EditorGUILayout.PropertyField(hillsNoiseSettings); // draws [Header("Hills Noise (N5.d)")]

                // M-fix.a: biome climate tunables (visible when biome stage enabled)
                if (enableBiomeStage.boolValue)
                {
                    EditorGUILayout.PropertyField(biomeBaseTemperature);    // draws [Header("Biome Climate (Phase M)")]
                    EditorGUILayout.PropertyField(biomeLapseRate);
                    EditorGUILayout.PropertyField(biomeLatitudeEffect);
                    EditorGUILayout.PropertyField(biomeCoastModerationStrength);
                    EditorGUILayout.PropertyField(biomeTempNoiseAmplitude);
                    EditorGUILayout.PropertyField(biomeTempNoiseCellSize);
                    EditorGUILayout.PropertyField(biomeCoastalMoistureBonus);
                    EditorGUILayout.PropertyField(biomeCoastDecayRate);
                    EditorGUILayout.PropertyField(biomeMoistureNoiseAmplitude);
                    EditorGUILayout.PropertyField(biomeMoistureNoiseCellSize);
                    // Phase L+M: river moisture tunables (shown under biome when hydrology also on)
                    EditorGUILayout.PropertyField(biomeRiverMoistureBonus); // draws [Header("River Moisture (Phase L+M)")]
                    EditorGUILayout.PropertyField(biomeRiverFlowNorm);
                }

                // W.a: hydrology tunables are NOT drawn here — the component ignores the
                // preset for them. Drawn always-visible below.

                EditorGUILayout.PropertyField(heightRemapCurve);             // draws [Header("Height Remap (N2)")]
                EditorGUILayout.PropertyField(clearBeforeRun);               // draws [Header("Run Behavior")]
            }

            // --- W.a: Always visible — stages the preset does NOT carry ---
            // MapGenerationPreset has no fields for these; PCGMapTilemapVisualization
            // resolves them from the inline fields unconditionally
            // (eRegions = enableRegionsStage; eHydro = enableHydrologyStage).
            // Hiding them behind a preset made live parameters unreachable.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Stages Not Covered By Preset", EditorStyles.boldLabel);
            if (hasPreset)
                EditorGUILayout.HelpBox(
                    "These are read from the component, not from the preset asset.",
                    MessageType.None);

            EditorGUILayout.PropertyField(enableRegionsStage);
            EditorGUILayout.PropertyField(enableHydrologyStage); // Phase L

            if (enableHydrologyStage.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(hydroEpsilon);      // draws [Header("Hydrology (Phase L)")]
                EditorGUILayout.PropertyField(hydroRiverThresholdFraction);
                EditorGUILayout.PropertyField(hydroMinLakeArea);
                EditorGUI.indentLevel--;
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

            // --- H8: Mega-Tiles ---
            EditorGUILayout.PropertyField(enableMegaTiles); // draws [Header("Mega-Tiles (H8)")]
            if (enableMegaTiles.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(megaTileRules, new GUIContent("Rules"), true);
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

            // --- N6: Scalar Overlay 1 ---
            EditorGUILayout.PropertyField(enableOverlay1); // draws [Header("Scalar Overlay 1 (N6)")]
            if (enableOverlay1.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(overlaySource1, new GUIContent("Source"));
                EditorGUILayout.PropertyField(overlayMin1, new GUIContent("Min"));
                EditorGUILayout.PropertyField(overlayMax1, new GUIContent("Max"));
                EditorGUILayout.PropertyField(overlayColorLow1, new GUIContent("Color Low"));
                EditorGUILayout.PropertyField(overlayColorHigh1, new GUIContent("Color High"));
                EditorGUILayout.PropertyField(overlayAlpha1, new GUIContent("Alpha"));
                EditorGUI.indentLevel--;
            }

            // --- N6: Scalar Overlay 2 ---
            EditorGUILayout.PropertyField(enableOverlay2); // draws [Header("Scalar Overlay 2 (N6)")]
            if (enableOverlay2.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(overlaySource2, new GUIContent("Source"));
                EditorGUILayout.PropertyField(overlayMin2, new GUIContent("Min"));
                EditorGUILayout.PropertyField(overlayMax2, new GUIContent("Max"));
                EditorGUILayout.PropertyField(overlayColorLow2, new GUIContent("Color Low"));
                EditorGUILayout.PropertyField(overlayColorHigh2, new GUIContent("Color High"));
                EditorGUILayout.PropertyField(overlayAlpha2, new GUIContent("Alpha"));
                EditorGUI.indentLevel--;
            }

            // --- W.a: Diagnostics (always visible, never preset-gated) ---
            // Golden capture must work with a world-scale preset assigned, which is
            // exactly the case the preset-hidden block would have blocked.
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Diagnostics (Phase W.a)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(logGoldenHashes, new GUIContent("Log Golden Hashes"));

            if (logGoldenHashes.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "Hashes are written to the Console after each rebuild, using the same "
                    + "FNV-1a scheme as the golden tests.\n\n"
                    + "Toggling this flag does not itself trigger a rebuild — use the button "
                    + "below, or change any tracked tunable.\n\n"
                    + "Costs O(cells) per field on every rebuild. Turn it off after capturing.",
                    MessageType.Info);

                if (GUILayout.Button("Force Rebuild (capture goldens now)"))
                {
                    // Disable/enable cycles the component through OnDisable/OnEnable,
                    // which disposes the context and sets dirty = true. Public API only —
                    // no reflection into the component's private rebuild state.
                    var comp = (PCGMapTilemapVisualization)target;
                    comp.enabled = false;
                    comp.enabled = true;
                }
            }

            // W-aux.a: map statistics dump. Reads the live context via the component's
            // public API — no rebuild, no reflection. Not a serialized field, so the
            // strict-whitelist binding rule does not apply (nothing to bind).
            if (GUILayout.Button("Log Map Stats (JSON to Console)"))
            {
                ((PCGMapTilemapVisualization)target).LogMapStats();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}