using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Localization;
using Datra.Services;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Popup window for editing locale translations across all loaded languages.
    /// Does NOT save - only modifies LocalizationContext and tracks changes.
    /// User must manually save in LocalizationView.
    /// </summary>
    public class LocaleEditPopup : EditorWindow
    {
        private LocalizationContext localizationContext;
        private LocalizationChangeTracker changeTracker;
        private string localeKey;
        private Action onModified;

        private Dictionary<LanguageCode, string> editedTexts;
        private Vector2 scrollPosition;

        public static void ShowWindow(
            LocalizationContext context,
            LocalizationChangeTracker tracker,
            string key,
            Rect buttonWorldBound,
            Action onModified = null)
        {
            var window = GetWindow<LocaleEditPopup>(true, "Edit Locale", true);
            window.localizationContext = context ?? throw new ArgumentNullException(nameof(context));
            window.changeTracker = tracker;
            window.localeKey = key;
            window.onModified = onModified;
            window.editedTexts = new Dictionary<LanguageCode, string>();

            // Set window size
            int languageCount = context.GetLoadedLanguages().Count();
            float height = 80 + (languageCount * 24) + 80; // Header + rows + buttons
            window.minSize = new Vector2(500, Mathf.Min(height, 600));
            window.maxSize = new Vector2(800, 800);

            // Position near button if possible
            if (buttonWorldBound != Rect.zero)
            {
                var screenPos = GUIUtility.GUIToScreenPoint(new Vector2(buttonWorldBound.x, buttonWorldBound.yMax));
                window.position = new Rect(screenPos.x, screenPos.y, 500, Mathf.Min(height, 600));
            }

            window.Show();
        }

        private void OnGUI()
        {
            if (localizationContext == null)
            {
                GUILayout.Label("Error: LocalizationContext is null", EditorStyles.boldLabel);
                return;
            }

            GUILayout.Label($"Edit Locale: {localeKey}", EditorStyles.boldLabel);
            GUILayout.Space(10);

            var loadedLanguages = localizationContext.GetLoadedLanguages().ToList();

            // Initialize edited texts on first draw
            if (editedTexts.Count == 0)
            {
                foreach (var languageCode in loadedLanguages)
                {
                    editedTexts[languageCode] = localizationContext.GetText(localeKey, languageCode);
                }
            }

            // Scroll view for languages
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            foreach (var languageCode in loadedLanguages.OrderBy(l => l.ToIsoCode()))
            {
                EditorGUILayout.BeginHorizontal();

                // Language label
                GUILayout.Label($"{GetLanguageDisplayName(languageCode)}:", GUILayout.Width(120));

                // Text field
                if (editedTexts.ContainsKey(languageCode))
                {
                    editedTexts[languageCode] = EditorGUILayout.TextField(editedTexts[languageCode]);
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            GUILayout.FlexibleSpace();

            // Buttons
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                this.Close();
            }

            if (GUILayout.Button("Apply", GUILayout.Width(80)))
            {
                ApplyChanges();
                this.Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ApplyChanges()
        {
            // Apply changes to each language
            foreach (var kvp in editedTexts)
            {
                var languageCode = kvp.Key;
                var newText = kvp.Value;

                // Get current text for this language
                var currentText = localizationContext.GetText(localeKey, languageCode);

                // Only update if changed
                if (newText != currentText)
                {
                    localizationContext.SetText(localeKey, newText, languageCode);

                    // Track change in change tracker
                    if (changeTracker != null && changeTracker.IsLanguageInitialized(languageCode))
                    {
                        changeTracker.TrackTextChange(localeKey, newText);
                    }
                }
            }

            // Notify modification
            onModified?.Invoke();
        }

        private string GetLanguageDisplayName(LanguageCode code)
        {
            return $"{code} ({code.ToIsoCode()})";
        }
    }
}
