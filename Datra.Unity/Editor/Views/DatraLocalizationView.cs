using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Localization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Services;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Views
{
    public class DatraLocalizationView : VisualElement
    {
        private LocalizationContext localizationContext;
        private DropdownField languageDropdown;
        private VisualElement keyListContainer;
        private TextField searchField;
        private ScrollView scrollView;
        private string currentLanguage;
        private Dictionary<string, TextField> textFields = new Dictionary<string, TextField>();
        private VisualElement loadingOverlay;
        private bool isLoading = false;
        
        // Events
        public event Action OnDataModified;
        public event Action OnSaveCompleted;
        
        public DatraLocalizationView()
        {
            AddToClassList("datra-localization-view");
            Initialize();
        }
        
        private void Initialize()
        {
            style.flexGrow = 1;
            style.flexDirection = FlexDirection.Column;
            
            // Toolbar
            var toolbar = new VisualElement();
            toolbar.AddToClassList("localization-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.SetPadding(new StyleLength(8));
            toolbar.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);
            
            // Language selector
            var languageLabel = new Label("Language:");
            languageLabel.style.marginRight = 8;
            languageLabel.style.alignSelf = Align.Center;
            toolbar.Add(languageLabel);
            
            languageDropdown = new DropdownField();
            languageDropdown.style.width = 150;
            languageDropdown.style.marginRight = 20;
            languageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            toolbar.Add(languageDropdown);
            
            // Search field
            var searchLabel = new Label("Search:");
            searchLabel.style.marginRight = 8;
            searchLabel.style.alignSelf = Align.Center;
            toolbar.Add(searchLabel);
            
            searchField = new TextField();
            searchField.style.width = 200;
            searchField.RegisterValueChangedCallback(OnSearchChanged);
            toolbar.Add(searchField);
            
            // Refresh button
            var refreshButton = new Button(RefreshView);
            refreshButton.text = "Refresh";
            refreshButton.style.marginLeft = StyleKeyword.Auto;
            toolbar.Add(refreshButton);
            
            // Save button
            var saveButton = new Button(SaveChanges);
            saveButton.text = "Save All";
            saveButton.style.marginLeft = 8;
            toolbar.Add(saveButton);
            
            Add(toolbar);
            
            // Content area with scroll view
            scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            
            keyListContainer = new VisualElement();
            keyListContainer.AddToClassList("localization-key-list");
            keyListContainer.style.SetPadding(new StyleLength(8));
            scrollView.Add(keyListContainer);
            
            Add(scrollView);
            
            // Create loading overlay
            CreateLoadingOverlay();
        }
        
        private void CreateLoadingOverlay()
        {
            loadingOverlay = new VisualElement();
            loadingOverlay.style.position = Position.Absolute;
            loadingOverlay.style.left = 0;
            loadingOverlay.style.top = 0;
            loadingOverlay.style.right = 0;
            loadingOverlay.style.bottom = 0;
            loadingOverlay.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            loadingOverlay.style.alignItems = Align.Center;
            loadingOverlay.style.justifyContent = Justify.Center;
            loadingOverlay.style.display = DisplayStyle.None;
            
            var loadingContainer = new VisualElement();
            loadingContainer.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            // Border radius if supported (Unity 2021.2+)
            // loadingContainer.style.borderRadius = new StyleLength(8);
            loadingContainer.style.SetPadding(new StyleLength(20));
            
            var loadingLabel = new Label("Loading language data...");
            loadingLabel.style.fontSize = 14;
            loadingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            loadingContainer.Add(loadingLabel);
            
            loadingOverlay.Add(loadingContainer);
            Add(loadingOverlay);
        }
        
        private void ShowLoading(bool show)
        {
            isLoading = show;
            if (loadingOverlay != null)
            {
                loadingOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Disable/enable interactive elements
            if (languageDropdown != null)
                languageDropdown.SetEnabled(!show);
            if (searchField != null)
                searchField.SetEnabled(!show);
        }
        
        public async void SetLocalizationContext(LocalizationContext context)
        {
            localizationContext = context;
            
            if (context == null)
            {
                ShowEmptyState();
                return;
            }
            
            // Get available languages
            var languages = GetAvailableLanguages();
            if (languages.Count > 0)
            {
                languageDropdown.choices = languages;
                currentLanguage = context.CurrentLanguage ?? languages[0];
                languageDropdown.value = currentLanguage;
                
                // Show loading and wait for initial language load
                ShowLoading(true);
                await LoadLanguageAsync(currentLanguage);
                ShowLoading(false);
            }
            else
            {
                RefreshView();
            }
        }

        private List<string> GetAvailableLanguages()
        {
            if (localizationContext == null)
                return new List<string>();
            
            // Get languages from LocalizationContext
            var languages = localizationContext.GetAvailableLanguages().ToList();
            return languages.Select(c => c.ToIsoCode()).ToList();
        }
        
        private async void OnLanguageChanged(ChangeEvent<string> evt)
        {
            currentLanguage = evt.newValue;
            ShowLoading(true);
            await LoadLanguageAsync(currentLanguage);
            ShowLoading(false);
        }
        
        private async Task LoadLanguageAsync(string language)
        {
            if (localizationContext == null) return;
            
            try
            {
                await localizationContext.LoadLanguageAsync(language);
                RefreshView();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load language {language}: {e.Message}");
            }
        }
        
        public void RefreshView()
        {
            if (localizationContext == null) return;

            keyListContainer.Clear();
            textFields.Clear();
            
            // Get all localization keys
            var keys = GetAllLocalizationKeys();
            var searchTerm = searchField.value?.ToLower() ?? "";
            
            // Create header
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.SetPadding(new StyleLength(4));
            headerRow.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            headerRow.style.borderBottomWidth = 1;
            headerRow.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            
            var keyHeader = new Label("Key");
            keyHeader.style.width = 300;
            keyHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(keyHeader);
            
            var valueHeader = new Label("Value");
            valueHeader.style.flexGrow = 1;
            valueHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerRow.Add(valueHeader);
            
            keyListContainer.Add(headerRow);
            
            // Add key-value pairs
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(searchTerm) && !key.ToLower().Contains(searchTerm))
                    continue;
                
                var row = CreateLocalizationRow(key);
                keyListContainer.Add(row);
            }
        }
        
        private VisualElement CreateLocalizationRow(string key)
        {
            var row = new VisualElement();
            row.AddToClassList("localization-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.SetPadding(new StyleLength(4));
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.1f, 0.1f, 0.1f);

            // Check if this is a fixed key
            var keyData = localizationContext.GetKeyData(key);
            bool isFixedKey = keyData != null && keyData.IsFixedKey;

            // Key label or container
            if (isFixedKey)
            {
                // Fixed keys: use a styled container with label
                var keyContainer = new VisualElement();
                keyContainer.style.width = 300;
                keyContainer.style.flexDirection = FlexDirection.Row;
                keyContainer.style.alignItems = Align.Center;
                keyContainer.style.backgroundColor = new Color(0.25f, 0.25f, 0.28f);
                keyContainer.style.borderLeftWidth = 3;
                keyContainer.style.borderLeftColor = new Color(0.4f, 0.6f, 0.8f); // Blue accent
                keyContainer.style.SetPadding(new StyleLength(4));

                var keyLabel = new Label(key);
                keyLabel.style.color = new Color(0.7f, 0.8f, 0.9f); // Lighter text
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                keyLabel.tooltip = $"{key}\n(Fixed key - cannot be edited)";

                // Add a lock icon using Unicode
                var lockIcon = new Label("🔒"); // 
                lockIcon.style.marginRight = 4;
                lockIcon.style.fontSize = 12;
                lockIcon.tooltip = "Fixed key";

                keyContainer.Add(lockIcon);
                keyContainer.Add(keyLabel);
                row.Add(keyContainer);
            }
            else
            {
                // Regular keys: simple label
                var keyLabel = new Label(key);
                keyLabel.style.width = 300;
                keyLabel.style.alignSelf = Align.Center;
                keyLabel.style.SetPadding(new StyleLength(4));
                keyLabel.tooltip = key;
                row.Add(keyLabel);
            }

            // Value text field
            var valueField = new TextField();
            valueField.style.flexGrow = 1;
            valueField.multiline = true;
            valueField.value = localizationContext.GetText(key);
            valueField.RegisterValueChangedCallback(evt => {
                OnDataModified?.Invoke();
            });

            textFields[key] = valueField;
            row.Add(valueField);

            return row;
        }
        
        private List<string> GetAllLocalizationKeys()
        {
            // Use the GetAllKeys method from LocalizationContext
            return localizationContext.GetAllKeys().OrderBy(k => k).ToList();
        }
        
        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            RefreshView();
        }
        
        public async void SaveChanges()
        {
            if (localizationContext == null) return;
            
            try
            {
                // Update all values in the localization context
                foreach (var kvp in textFields)
                {
                    var key = kvp.Key;
                    var value = kvp.Value.value;
                    
                    // Update the localization context with new values
                    localizationContext.SetText(key, value);
                }
                
                // Save the current language data to file
                await localizationContext.SaveCurrentLanguageAsync();
                
                // Refresh Unity's asset database to reflect the changes
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Save", $"Localization changes for '{currentLanguage}' saved successfully!", "OK");
                Debug.Log($"Saved localization data for language: {currentLanguage}");
                OnSaveCompleted?.Invoke();
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error", $"Failed to save localization changes: {e.Message}", "OK");
                Debug.LogError($"Failed to save localization changes: {e}");
            }
        }
        
        private void ShowEmptyState()
        {
            keyListContainer.Clear();
            
            var emptyLabel = new Label("No localization context available");
            emptyLabel.style.alignSelf = Align.Center;
            emptyLabel.style.marginTop = 20;
            keyListContainer.Add(emptyLabel);
        }
    }
}