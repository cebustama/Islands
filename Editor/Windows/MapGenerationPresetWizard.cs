// Phase W-aux.c block 2 — MapGenerationPreset import/export wizard.
//
// Thin UI over MapGenerationPresetJsonImporter (the importer is the tested
// surface; this window owns only asset lifecycle, Undo, and presentation).
//
// Editor-only, golden-neutral, purely additive: this window cannot alter
// generation output except by the user explicitly importing values into a
// preset asset.

using UnityEditor;
using UnityEngine;
using Islands.PCG.Samples;

namespace Islands.PCG.Editor
{
    public sealed class MapGenerationPresetWizard : EditorWindow
    {
        [MenuItem("Islands/PCG/Map Generation Preset Wizard")]
        private static void Open()
        {
            var w = GetWindow<MapGenerationPresetWizard>("Preset Wizard");
            w.minSize = new Vector2(480f, 420f);
        }

        private MapGenerationPreset _target;
        private string _jsonText = string.Empty;
        private Vector2 _jsonScroll;
        private Vector2 _reportScroll;
        private PresetImportReport _lastReport;
        private string _lastAction = string.Empty;

        // Phase X1.a — diagnostics + diff state
        private Vector2 _findingsScroll;
        private MapGenerationPreset _comparePreset;
        private System.Collections.Generic.List<PresetFinding> _findings;
        private System.Collections.Generic.List<string> _diffLines;

        private void OnGUI()
        {
            // W.b gap — declared, not hidden (project decision: the wizard
            // inherits the gap and must state it in its UI).
            EditorGUILayout.HelpBox(
                "Not carried by this JSON: enableRegionsStage, enableHydrologyStage, "
                + "hydroEpsilon, hydroRiverThresholdFraction, hydroMinLakeArea. "
                + "These are component-scoped fields (closed by Phase W.b). "
                + "Import/export here never touches them.",
                MessageType.Warning);

            _target = (MapGenerationPreset)EditorGUILayout.ObjectField(
                "Loaded preset", _target, typeof(MapGenerationPreset), allowSceneObjects: false);

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Preset JSON", EditorStyles.boldLabel);
            _jsonScroll = EditorGUILayout.BeginScrollView(
                _jsonScroll, GUILayout.MinHeight(160f), GUILayout.MaxHeight(260f));
            _jsonText = EditorGUILayout.TextArea(_jsonText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(_jsonText)))
                {
                    if (GUILayout.Button("Import → new asset…"))
                        ImportIntoNewAsset();

                    using (new EditorGUI.DisabledScope(_target == null))
                    {
                        if (GUILayout.Button("Import → loaded preset (overwrite)"))
                            ImportIntoLoadedAsset();
                    }
                }

                using (new EditorGUI.DisabledScope(_target == null))
                {
                    if (GUILayout.Button("Export loaded preset → JSON"))
                    {
                        _jsonText = _target.ToJson();
                        _lastReport = null;
                        _lastAction = $"Exported '{_target.name}' to the text area.";
                    }
                }
            }

            // ---- Phase X1.a — preset diagnostics & diff ----
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Preset diagnostics (X1.a)", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (GUILayout.Button("Diagnose loaded preset"))
                {
                    _findings = MapGenerationPresetDiagnostics.Diagnose(_target);
                    _lastAction = $"Diagnosed '{_target.name}': {_findings.Count} finding(s).";
                }
            }
            _comparePreset = (MapGenerationPreset)EditorGUILayout.ObjectField(
                "Compare against", _comparePreset, typeof(MapGenerationPreset), allowSceneObjects: false);
            using (new EditorGUI.DisabledScope(_target == null || _comparePreset == null))
            {
                if (GUILayout.Button("Diff: compare → loaded"))
                {
                    _diffLines = MapGenerationPresetDiagnostics.DiffJson(
                        _comparePreset.ToJson(), _target.ToJson());
                    _lastAction =
                        $"Diff '{_comparePreset.name}' → '{_target.name}': {_diffLines.Count} changed line(s).";
                }
            }

            using (new EditorGUI.DisabledScope(_findings == null && _diffLines == null))
            {
                if (GUILayout.Button("Log diagnostics + diff to Console"))
                    LogDiagnosticsToConsole();
            }

            _findingsScroll = EditorGUILayout.BeginScrollView(
                _findingsScroll, GUILayout.MinHeight(160f), GUILayout.ExpandHeight(true));
            DrawFindings();
            DrawDiff();
            EditorGUILayout.EndScrollView();

            DrawReport();
        }

        // ------------------------------------------------------------------
        // Import paths
        // ------------------------------------------------------------------

        private void ImportIntoNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Map Generation Preset",
                "MapGenerationPreset", "asset",
                "Choose where to save the imported preset asset.");
            if (string.IsNullOrEmpty(path))
                return;

            var preset = ScriptableObject.CreateInstance<MapGenerationPreset>();
            var report = MapGenerationPresetJsonImporter.Import(preset, _jsonText);

            if (report.Applied.Count == 0)
            {
                // Nothing usable parsed — do not create a junk asset, and say why.
                DestroyImmediate(preset);
                _lastReport = report;
                _lastAction = "Import aborted: no known field could be applied. No asset created.";
                return;
            }

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            _target = preset;
            EditorGUIUtility.PingObject(preset);

            _lastReport = report;
            _lastAction = $"Created '{path}'.";
        }

        private void ImportIntoLoadedAsset()
        {
            if (_target == null) return;

            bool ok = EditorUtility.DisplayDialog(
                "Overwrite preset",
                $"Apply the pasted JSON over '{_target.name}'?\n\n"
                + "Fields present in the JSON overwrite the asset's current values. "
                + "Fields absent from the JSON keep their current values "
                + "(reported after import). This is undoable (Ctrl/Cmd+Z).",
                "Overwrite", "Cancel");
            if (!ok) return;

            Undo.RecordObject(_target, "Import preset JSON");
            _lastReport = MapGenerationPresetJsonImporter.Import(_target, _jsonText);
            EditorUtility.SetDirty(_target);

            _lastAction = _lastReport.HasErrors
                ? $"Imported into '{_target.name}' WITH ERRORS — see report."
                : $"Imported into '{_target.name}'.";
        }

        // ------------------------------------------------------------------
        // Diagnostic panel — four categories + errors, never silent
        // ------------------------------------------------------------------

        private void DrawReport()
        {
            EditorGUILayout.Space(6f);
            if (!string.IsNullOrEmpty(_lastAction))
                EditorGUILayout.LabelField(_lastAction, EditorStyles.miniBoldLabel);
            if (_lastReport == null)
                return;

            if (_lastReport.HasErrors)
                EditorGUILayout.HelpBox(
                    $"{_lastReport.Errors.Count} error(s): the listed fields were NOT applied.",
                    MessageType.Error);

            _reportScroll = EditorGUILayout.BeginScrollView(_reportScroll);
            DrawCategory($"Errors ({_lastReport.Errors.Count})", _lastReport.Errors);
            DrawCategory($"Applied ({_lastReport.Applied.Count})", _lastReport.Applied);
            DrawCategory($"Ignored — recognized, deliberately not applied ({_lastReport.Ignored.Count})",
                _lastReport.Ignored);
            DrawCategory($"Unknown fields ({_lastReport.Unknown.Count})", _lastReport.Unknown);
            DrawCategory($"Absent — previous value preserved ({_lastReport.AbsentPreserved.Count})",
                _lastReport.AbsentPreserved);
            EditorGUILayout.EndScrollView();
        }

        private void LogDiagnosticsToConsole()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[Preset Wizard X1.a] ")
              .AppendLine(_target != null ? _target.name : "(no preset)");

            if (_findings != null)
            {
                sb.Append("--- Diagnostics: ").Append(_findings.Count).AppendLine(" finding(s) ---");
                foreach (PresetFinding f in _findings)
                {
                    sb.Append(f.RuleId).Append(' ').AppendLine(f.Label);
                    sb.AppendLine(f.Message);
                }
            }

            if (_diffLines != null)
            {
                sb.Append("--- Diff ")
                  .Append(_comparePreset != null ? _comparePreset.name : "(compare)")
                  .Append(" -> ")
                  .Append(_target != null ? _target.name : "(loaded)")
                  .Append(": ").Append(_diffLines.Count).AppendLine(" line(s) ---");
                foreach (string s in _diffLines)
                    sb.AppendLine(s);
            }

            Debug.Log(sb.ToString(), _target);
        }

        private void DrawFindings()
        {
            if (_findings == null) return;
            EditorGUILayout.Space(4f);
            if (_findings.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No findings. Rules cover known contradictions only — silence is not a guarantee.",
                    MessageType.Info);
                return;
            }
            foreach (PresetFinding f in _findings)
                EditorGUILayout.HelpBox(
                    $"{f.RuleId} {f.Label}\n{f.Message}",
                    f.Severity == PresetFindingSeverity.Warning ? MessageType.Warning : MessageType.Info);
        }

        private void DrawDiff()
        {
            if (_diffLines == null) return;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"Preset diff — {_diffLines.Count} line(s) (asset / stageTogglesNote / derived excluded)",
                EditorStyles.boldLabel);
            if (_diffLines.Count == 0)
                EditorGUILayout.LabelField("• no differences", EditorStyles.wordWrappedMiniLabel);
            foreach (string s in _diffLines)
                EditorGUILayout.LabelField("• " + s, EditorStyles.wordWrappedMiniLabel);
        }

        private static void DrawCategory(string title, System.Collections.Generic.List<string> items)
        {
            if (items.Count == 0) return;
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (string s in items)
                EditorGUILayout.LabelField("• " + s, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2f);
        }
    }
}