using System;
using System.Collections.Generic;
using Datra.Localization;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Datra.Unity.Editor.Panels
{
    public class DatraToolbarPanel : VisualElement
    {
        private Button saveButton;
        private Button saveAllButton;
        private Button reloadButton;
        private Button settingsButton;
        private Label projectLabel;
        private VisualElement modifiedIndicator;
        private VisualElement languageContainer;
        private DropdownField languageDropdown;
        private VisualElement contextContainer;
        private DropdownField contextDropdown;
        private List<string> contextNames = new List<string>();

        public event Action OnSaveClicked;
        public event Action OnSaveAllClicked;
        public event Action OnForceSaveClicked;
        public event Action OnForceSaveAllClicked;
        public event Action OnReloadClicked;
        public event Action OnSettingsClicked;
        public event Action<LanguageCode> OnLanguageChanged;
        public event Action<int> OnContextChanged;
        
        public DatraToolbarPanel()
        {
            AddToClassList("datra-toolbar");
            Initialize();
        }
        
        private void Initialize()
        {
            // Left section - Logo and project info
            var leftSection = new VisualElement();
            leftSection.AddToClassList("toolbar-left");
            
            var logoContainer = new VisualElement();
            logoContainer.AddToClassList("logo-container");
            
            var logo = new VisualElement();
            logo.AddToClassList("datra-logo");
            logoContainer.Add(logo);
            
            var titleLabel = new Label("Datra Editor");
            titleLabel.AddToClassList("toolbar-title");
            logoContainer.Add(titleLabel);
            
            leftSection.Add(logoContainer);
            
            projectLabel = new Label();
            projectLabel.AddToClassList("project-label");
            leftSection.Add(projectLabel);

            // Context selector container (hidden by default, shown when multiple contexts available)
            contextContainer = new VisualElement();
            contextContainer.AddToClassList("toolbar-context-container");
            contextContainer.style.flexDirection = FlexDirection.Row;
            contextContainer.style.alignItems = Align.Center;
            contextContainer.style.display = DisplayStyle.None;
            contextContainer.style.marginLeft = 12;

            var contextIcon = new Label("📦");
            contextIcon.style.marginRight = 4;
            contextIcon.style.fontSize = 14;
            contextContainer.Add(contextIcon);

            contextDropdown = new DropdownField();
            contextDropdown.AddToClassList("toolbar-context-dropdown");
            contextDropdown.style.minWidth = 150;
            contextDropdown.RegisterValueChangedCallback(OnContextDropdownChanged);
            contextContainer.Add(contextDropdown);

            leftSection.Add(contextContainer);

            Add(leftSection);
            
            // Center section - Quick actions
            var centerSection = new VisualElement();
            centerSection.AddToClassList("toolbar-center");

            modifiedIndicator = new VisualElement();
            modifiedIndicator.AddToClassList("modified-indicator");
            modifiedIndicator.tooltip = "Unsaved changes";
            modifiedIndicator.style.display = DisplayStyle.None;
            centerSection.Add(modifiedIndicator);

            // Language dropdown container
            languageContainer = new VisualElement();
            languageContainer.AddToClassList("toolbar-language-container");
            languageContainer.style.flexDirection = FlexDirection.Row;
            languageContainer.style.alignItems = Align.Center;
            languageContainer.style.display = DisplayStyle.None; // Hidden by default until languages are loaded

            var globeIcon = new Label("🌐");
            globeIcon.style.marginRight = 4;
            globeIcon.style.fontSize = 16;
            languageContainer.Add(globeIcon);

            languageDropdown = new DropdownField();
            languageDropdown.AddToClassList("toolbar-language-dropdown");
            languageDropdown.style.minWidth = 100;
            languageContainer.Add(languageDropdown);

            centerSection.Add(languageContainer);

            Add(centerSection);
            
            // Right section - Action buttons
            var rightSection = new VisualElement();
            rightSection.AddToClassList("toolbar-right");

            // Save button for current data
            saveButton = new Button(() => OnSaveClicked?.Invoke());
            saveButton.text = "💾 Save";
            saveButton.tooltip = "Save current data\nRight-click for Force Save";
            saveButton.AddToClassList("toolbar-button");
            saveButton.AddToClassList("save-button");

            // Add context menu for Save button
            saveButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1) // Right click
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Force Save"), false, () => OnForceSaveClicked?.Invoke());
                    menu.ShowAsContext();
                    evt.StopPropagation();
                }
            });
            rightSection.Add(saveButton);

            // Save All button
            saveAllButton = new Button(() => OnSaveAllClicked?.Invoke());
            saveAllButton.text = "💾 Save All";
            saveAllButton.tooltip = "Save all modified data (Ctrl+S)\nRight-click for Force Save All";
            saveAllButton.AddToClassList("toolbar-button");
            saveAllButton.AddToClassList("save-all-button");

            // Add context menu for Save All button
            saveAllButton.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 1) // Right click
                {
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Force Save All"), false, () => OnForceSaveAllClicked?.Invoke());
                    menu.ShowAsContext();
                    evt.StopPropagation();
                }
            });
            rightSection.Add(saveAllButton);

            reloadButton = new Button(() => OnReloadClicked?.Invoke());
            reloadButton.text = "↻ Reload";
            reloadButton.tooltip = "Reload all data from disk";
            reloadButton.AddToClassList("toolbar-button");
            rightSection.Add(reloadButton);
            
            // Separator
            var separator = new VisualElement();
            separator.AddToClassList("toolbar-separator");
            rightSection.Add(separator);
            
            settingsButton = new Button(() => OnSettingsClicked?.Invoke());
            settingsButton.text = "⚙";
            settingsButton.tooltip = "Settings";
            settingsButton.AddToClassList("toolbar-button");
            settingsButton.AddToClassList("icon-button");
            rightSection.Add(settingsButton);
            
            Add(rightSection);
        }
        
        public void SetProjectName(string projectName)
        {
            projectLabel.text = string.IsNullOrEmpty(projectName) ? "" : $"Project: {projectName}";
        }
        
        public void SetModifiedState(bool hasModifications)
        {
            modifiedIndicator.style.display = hasModifications ? DisplayStyle.Flex : DisplayStyle.None;
            // Don't disable buttons - just change visual state
            // Users can always Force Save even when there are no modifications

            if (hasModifications)
            {
                saveButton.AddToClassList("highlighted");
                saveAllButton.AddToClassList("highlighted");
            }
            else
            {
                saveButton.RemoveFromClassList("highlighted");
                saveAllButton.RemoveFromClassList("highlighted");
            }
        }

        public void SetSaveButtonEnabled(bool enabled)
        {
            // Keep buttons always enabled for Force Save functionality
            // Only used during save operations to prevent double-clicks
            saveButton.SetEnabled(enabled);
            saveAllButton.SetEnabled(enabled);
        }

        public void SetCurrentDataModified(bool isModified)
        {
            // Don't disable Save button - just change visual state
            // Users can always Force Save
            if (isModified)
            {
                saveButton.AddToClassList("highlighted");
            }
            else
            {
                saveButton.RemoveFromClassList("highlighted");
            }
        }
        
        public void SetReloadButtonEnabled(bool enabled)
        {
            reloadButton.SetEnabled(enabled);
        }

        /// <summary>
        /// Set up the language dropdown with available languages
        /// </summary>
        public void SetupLanguages(IEnumerable<LanguageCode> languages, LanguageCode currentLanguage)
        {
            var languageList = new List<LanguageCode>(languages);
            if (languageList.Count == 0)
            {
                languageContainer.style.display = DisplayStyle.None;
                return;
            }

            var languageNames = new List<string>();
            var currentIndex = 0;

            for (int i = 0; i < languageList.Count; i++)
            {
                var lang = languageList[i];
                languageNames.Add(lang.ToIsoCode());
                if (lang == currentLanguage)
                {
                    currentIndex = i;
                }
            }

            languageDropdown.choices = languageNames;
            languageDropdown.index = currentIndex;

            // Unregister previous callback if any
            languageDropdown.UnregisterValueChangedCallback(OnLanguageDropdownChanged);

            // Register new callback
            languageDropdown.RegisterValueChangedCallback(OnLanguageDropdownChanged);

            languageContainer.style.display = DisplayStyle.Flex;
        }

        private void OnLanguageDropdownChanged(ChangeEvent<string> evt)
        {
            if (string.IsNullOrEmpty(evt.newValue)) return;

            var languageCode = LanguageCodeExtensions.TryParse(evt.newValue);
            if (languageCode.HasValue)
            {
                OnLanguageChanged?.Invoke(languageCode.Value);
            }
        }

        /// <summary>
        /// Update the current language selection without triggering the event
        /// </summary>
        public void SetCurrentLanguage(LanguageCode languageCode)
        {
            var isoCode = languageCode.ToIsoCode();
            if (languageDropdown.choices != null && languageDropdown.choices.Contains(isoCode))
            {
                languageDropdown.SetValueWithoutNotify(isoCode);
            }
        }

        /// <summary>
        /// Set up the context selector with available DataContext names
        /// </summary>
        public void SetupContextSelector(List<string> names)
        {
            contextNames = names ?? new List<string>();

            if (contextNames.Count <= 1)
            {
                contextContainer.style.display = DisplayStyle.None;
                return;
            }

            contextDropdown.choices = contextNames;
            contextDropdown.index = 0;
            contextContainer.style.display = DisplayStyle.Flex;
        }

        private void OnContextDropdownChanged(ChangeEvent<string> evt)
        {
            if (string.IsNullOrEmpty(evt.newValue)) return;

            var index = contextNames.IndexOf(evt.newValue);
            if (index >= 0)
            {
                OnContextChanged?.Invoke(index);
            }
        }

        /// <summary>
        /// Update the current context selection without triggering the event
        /// </summary>
        public void SetCurrentContext(int index)
        {
            if (index >= 0 && index < contextNames.Count)
            {
                contextDropdown.SetValueWithoutNotify(contextNames[index]);
            }
        }
    }
}