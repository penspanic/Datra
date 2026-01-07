using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Unity.Editor.Services;
using Datra.Services;
using UnityEngine;
using UnityEditor;

namespace Datra.Unity.Editor.Windows
{
    /// <summary>
    /// Editor window for syncing FixedLocale keys with data.
    /// Shows missing and orphan keys, allowing user to selectively sync them.
    /// </summary>
    public class FixedLocaleKeySyncWindow : EditorWindow
    {
        private FixedLocaleKeyAnalyzer.AnalysisResult _analysisResult;
        private LocalizationContext _localizationContext;
        private Action _onSyncComplete;

        // Selection state
        private HashSet<string> _selectedMissingKeys = new();
        private HashSet<string> _selectedOrphanKeys = new();

        // UI state
        private Vector2 _missingKeysScrollPos;
        private Vector2 _orphanKeysScrollPos;
        private bool _selectAllMissing = true;
        private bool _selectAllOrphan = true;
        private bool _isSyncing = false;

        // Styles
        private GUIStyle _headerStyle;
        private GUIStyle _countStyle;
        private GUIStyle _keyStyle;
        private GUIStyle _detailStyle;
        private bool _stylesInitialized = false;

        public static void Show(
            FixedLocaleKeyAnalyzer.AnalysisResult result,
            LocalizationContext localizationContext,
            Action onSyncComplete = null)
        {
            var window = GetWindow<FixedLocaleKeySyncWindow>(true, "FixedLocale Key Sync", true);
            window._analysisResult = result;
            window._localizationContext = localizationContext;
            window._onSyncComplete = onSyncComplete;

            // Select all by default
            window._selectedMissingKeys = new HashSet<string>(result.MissingKeys.Select(k => k.Key));
            window._selectedOrphanKeys = new HashSet<string>(result.OrphanKeys.Select(k => k.Key));
            window._selectAllMissing = true;
            window._selectAllOrphan = true;

            window.minSize = new Vector2(500, 400);
            window.maxSize = new Vector2(800, 600);

            // Center the window
            var position = window.position;
            position.center = new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height).center;
            window.position = position;

            window.Show();
        }

        private void InitStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14
            };

            _countStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold
            };

            _keyStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = EditorGUIUtility.isProSkin ? Color.cyan : new Color(0, 0.5f, 0.8f) }
            };

            _detailStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray }
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitStyles();

            if (_analysisResult == null)
            {
                EditorGUILayout.HelpBox("No analysis result available.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space(10);

            // Summary
            DrawSummary();

            EditorGUILayout.Space(10);

            // No issues case
            if (!_analysisResult.HasIssues)
            {
                EditorGUILayout.HelpBox("All FixedLocale keys are in sync! No action needed.", MessageType.Info);
                EditorGUILayout.Space(10);

                if (GUILayout.Button("Close", GUILayout.Height(30)))
                {
                    Close();
                }
                return;
            }

            // Missing keys section
            if (_analysisResult.MissingKeys.Count > 0)
            {
                DrawMissingKeysSection();
            }

            EditorGUILayout.Space(10);

            // Orphan keys section
            if (_analysisResult.OrphanKeys.Count > 0)
            {
                DrawOrphanKeysSection();
            }

            EditorGUILayout.Space(20);

            // Action buttons
            DrawActionButtons();
        }

        private void DrawSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.LabelField("Summary", _headerStyle);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Expected FixedLocale Keys: {_analysisResult.TotalExpectedKeys}");
            EditorGUILayout.LabelField($"Existing Fixed Keys: {_analysisResult.TotalExistingFixedKeys}");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            var missingColor = _analysisResult.MissingKeys.Count > 0 ? "orange" : "green";
            var orphanColor = _analysisResult.OrphanKeys.Count > 0 ? "red" : "green";

            EditorGUILayout.LabelField($"Missing Keys: ", GUILayout.Width(100));
            EditorGUILayout.LabelField($"{_analysisResult.MissingKeys.Count}", _countStyle, GUILayout.Width(50));

            EditorGUILayout.LabelField($"Orphan Keys: ", GUILayout.Width(100));
            EditorGUILayout.LabelField($"{_analysisResult.OrphanKeys.Count}", _countStyle, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawMissingKeysSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Missing Keys ({_analysisResult.MissingKeys.Count})", _headerStyle);

            EditorGUI.BeginChangeCheck();
            _selectAllMissing = EditorGUILayout.ToggleLeft("Select All", _selectAllMissing, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                if (_selectAllMissing)
                    _selectedMissingKeys = new HashSet<string>(_analysisResult.MissingKeys.Select(k => k.Key));
                else
                    _selectedMissingKeys.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("These keys should exist based on your data but are missing from LocalizationKeys.csv", MessageType.Warning);

            _missingKeysScrollPos = EditorGUILayout.BeginScrollView(_missingKeysScrollPos, GUILayout.MaxHeight(150));

            foreach (var key in _analysisResult.MissingKeys)
            {
                EditorGUILayout.BeginHorizontal();

                var isSelected = _selectedMissingKeys.Contains(key.Key);
                var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                if (newSelected != isSelected)
                {
                    if (newSelected)
                        _selectedMissingKeys.Add(key.Key);
                    else
                        _selectedMissingKeys.Remove(key.Key);

                    _selectAllMissing = _selectedMissingKeys.Count == _analysisResult.MissingKeys.Count;
                }

                EditorGUILayout.LabelField(key.Key, _keyStyle, GUILayout.MinWidth(200));
                EditorGUILayout.LabelField($"({key.TypeName}.{key.ItemId})", _detailStyle);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawOrphanKeysSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Orphan Keys ({_analysisResult.OrphanKeys.Count})", _headerStyle);

            EditorGUI.BeginChangeCheck();
            _selectAllOrphan = EditorGUILayout.ToggleLeft("Select All", _selectAllOrphan, GUILayout.Width(80));
            if (EditorGUI.EndChangeCheck())
            {
                if (_selectAllOrphan)
                    _selectedOrphanKeys = new HashSet<string>(_analysisResult.OrphanKeys.Select(k => k.Key));
                else
                    _selectedOrphanKeys.Clear();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("These fixed keys exist in LocalizationKeys.csv but no longer have corresponding data items", MessageType.Error);

            _orphanKeysScrollPos = EditorGUILayout.BeginScrollView(_orphanKeysScrollPos, GUILayout.MaxHeight(150));

            foreach (var key in _analysisResult.OrphanKeys)
            {
                EditorGUILayout.BeginHorizontal();

                var isSelected = _selectedOrphanKeys.Contains(key.Key);
                var newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));
                if (newSelected != isSelected)
                {
                    if (newSelected)
                        _selectedOrphanKeys.Add(key.Key);
                    else
                        _selectedOrphanKeys.Remove(key.Key);

                    _selectAllOrphan = _selectedOrphanKeys.Count == _analysisResult.OrphanKeys.Count;
                }

                EditorGUILayout.LabelField(key.Key, _keyStyle, GUILayout.MinWidth(200));
                EditorGUILayout.LabelField($"(was: {key.TypeName}.{key.ItemId})", _detailStyle);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            EditorGUI.BeginDisabledGroup(_isSyncing);

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(100), GUILayout.Height(30)))
            {
                Close();
            }

            var hasSelection = _selectedMissingKeys.Count > 0 || _selectedOrphanKeys.Count > 0;
            EditorGUI.BeginDisabledGroup(!hasSelection);

            var syncButtonText = _isSyncing ? "Syncing..." : $"Sync Selected ({_selectedMissingKeys.Count + _selectedOrphanKeys.Count})";
            if (GUILayout.Button(syncButtonText, GUILayout.Width(180), GUILayout.Height(30)))
            {
                PerformSync();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();
        }

        private async void PerformSync()
        {
            _isSyncing = true;
            Repaint();

            try
            {
                int addedCount = 0;
                int removedCount = 0;

                // Batch add missing keys (saves once at the end)
                if (_selectedMissingKeys.Count > 0)
                {
                    var keysToAdd = _selectedMissingKeys
                        .Select(key => _analysisResult.MissingKeys.FirstOrDefault(k => k.Key == key))
                        .Where(keyInfo => keyInfo != null)
                        .Select(keyInfo => (
                            key: keyInfo.Key,
                            description: keyInfo.SuggestedDescription,
                            category: keyInfo.SuggestedCategory,
                            isFixedKey: true
                        ));

                    addedCount = await _localizationContext.AddKeysBatchAsync(keysToAdd);
                }

                // Batch remove orphan keys (saves once at the end)
                if (_selectedOrphanKeys.Count > 0)
                {
                    removedCount = await _localizationContext.ForceDeleteKeysBatchAsync(_selectedOrphanKeys);
                }

                // Show result
                EditorUtility.DisplayDialog("Sync Complete",
                    $"Successfully synced FixedLocale keys!\n\nAdded: {addedCount}\nRemoved: {removedCount}",
                    "OK");

                _onSyncComplete?.Invoke();
                Close();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Sync Error", $"An error occurred during sync:\n{ex.Message}", "OK");
            }
            finally
            {
                _isSyncing = false;
                Repaint();
            }
        }
    }
}
