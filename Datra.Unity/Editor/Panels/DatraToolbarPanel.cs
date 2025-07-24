using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace Datra.Unity.Editor.Panels
{
    public class DatraToolbarPanel : VisualElement
    {
        private Button saveAllButton;
        private Button reloadButton;
        private Button settingsButton;
        private Label projectLabel;
        private VisualElement modifiedIndicator;
        
        public event Action OnSaveAllClicked;
        public event Action OnReloadClicked;
        public event Action OnSettingsClicked;
        
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
            
            Add(leftSection);
            
            // Center section - Quick actions
            var centerSection = new VisualElement();
            centerSection.AddToClassList("toolbar-center");
            
            modifiedIndicator = new VisualElement();
            modifiedIndicator.AddToClassList("modified-indicator");
            modifiedIndicator.tooltip = "Unsaved changes";
            modifiedIndicator.style.display = DisplayStyle.None;
            centerSection.Add(modifiedIndicator);
            
            Add(centerSection);
            
            // Right section - Action buttons
            var rightSection = new VisualElement();
            rightSection.AddToClassList("toolbar-right");
            
            saveAllButton = new Button(() => OnSaveAllClicked?.Invoke());
            saveAllButton.text = "💾 Save All";
            saveAllButton.tooltip = "Save all modified data (Ctrl+S)";
            saveAllButton.AddToClassList("toolbar-button");
            saveAllButton.AddToClassList("save-button");
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
            saveAllButton.SetEnabled(hasModifications);
            
            if (hasModifications)
            {
                saveAllButton.AddToClassList("highlighted");
            }
            else
            {
                saveAllButton.RemoveFromClassList("highlighted");
            }
        }
        
        public void SetSaveButtonEnabled(bool enabled)
        {
            saveAllButton.SetEnabled(enabled);
        }
        
        public void SetReloadButtonEnabled(bool enabled)
        {
            reloadButton.SetEnabled(enabled);
        }
    }
}