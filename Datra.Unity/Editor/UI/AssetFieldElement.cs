#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Attributes;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.UI
{
    /// <summary>
    /// Custom UI element for asset selection with attribute-based filtering
    /// </summary>
    public class AssetFieldElement : VisualElement
    {
        private readonly AssetTypeAttribute assetType;
        private readonly FolderPathAttribute folderPath;
        private readonly Action<string> onValueChanged;
        private readonly bool isTableMode;
        
        private TextField pathField;
        private ObjectField objectField;
        private Button browseButton;
        private Button clearButton;
        private Label validationLabel;
        
        private string currentPath;
        private bool isValid = true;
        
        public string Value
        {
            get => currentPath;
            set
            {
                SetValue(value);
            }
        }
        
        public AssetFieldElement(AssetTypeAttribute assetType, FolderPathAttribute folderPath, string initialValue, Action<string> onValueChanged, bool isTableMode = false)
        {
            this.assetType = assetType;
            this.folderPath = folderPath;
            this.onValueChanged = onValueChanged;
            this.currentPath = initialValue;
            this.isTableMode = isTableMode;
            
            AddToClassList("asset-field-element");
            if (isTableMode)
            {
                AddToClassList("table-mode");
            }
            
            CreateUI();
            SetValue(initialValue);
        }
        
        private void CreateUI()
        {
            if (isTableMode)
            {
                CreateTableModeUI();
            }
            else
            {
                CreateFormModeUI();
            }
            
            // Enable drag & drop for both modes
            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }
        
        private void CreateTableModeUI()
        {
            // Simple ObjectField for table mode
            var unityType = AttributeFieldHandler.GetUnityAssetType(assetType?.Type);
            objectField = new ObjectField();
            objectField.objectType = unityType;
            objectField.AddToClassList("asset-field-object");
            objectField.RegisterValueChangedCallback(evt => OnObjectChanged(evt.newValue));
            objectField.style.flexGrow = 1;
            
            Add(objectField);
            
            // Hidden path field for data storage
            pathField = new TextField();
            pathField.style.display = DisplayStyle.None;
            Add(pathField);
        }
        
        private void CreateFormModeUI()
        {
            // Main container
            style.flexDirection = FlexDirection.Column;
            
            // Input row container
            var inputRow = new VisualElement();
            inputRow.AddToClassList("asset-field-input-row");
            inputRow.style.flexDirection = FlexDirection.Row;
            inputRow.style.alignItems = Align.Center;
            Add(inputRow);
            
            // Path field
            pathField = new TextField();
            pathField.AddToClassList("asset-field-path");
            pathField.style.flexGrow = 1;
            pathField.RegisterValueChangedCallback(evt => OnPathChanged(evt.newValue));
            inputRow.Add(pathField);
            
            // Object field (hidden but used for drag & drop)
            var unityType = AttributeFieldHandler.GetUnityAssetType(assetType?.Type);
            objectField = new ObjectField();
            objectField.objectType = unityType;
            objectField.style.display = DisplayStyle.None;
            objectField.RegisterValueChangedCallback(evt => OnObjectChanged(evt.newValue));
            inputRow.Add(objectField);
            
            // Browse button
            browseButton = new Button(OnBrowseClicked);
            browseButton.text = "Browse";
            browseButton.AddToClassList("asset-field-browse-button");
            inputRow.Add(browseButton);
            
            // Clear button
            clearButton = new Button(OnClearClicked);
            clearButton.text = "×";
            clearButton.tooltip = "Clear";
            clearButton.AddToClassList("asset-field-clear-button");
            inputRow.Add(clearButton);
            
            // Validation label
            validationLabel = new Label();
            validationLabel.AddToClassList("asset-field-validation");
            validationLabel.style.display = DisplayStyle.None;
            Add(validationLabel);
            
            // Info row
            var infoRow = new VisualElement();
            infoRow.AddToClassList("asset-field-info-row");
            infoRow.style.flexDirection = FlexDirection.Row;
            infoRow.style.marginTop = 2;
            Add(infoRow);
            
            // Type info
            if (assetType != null)
            {
                var typeLabel = new Label($"Type: {GetDisplayTypeName(assetType.Type)}");
                typeLabel.AddToClassList("asset-field-info");
                infoRow.Add(typeLabel);
            }
            
            // Folder info
            if (folderPath != null)
            {
                var folderLabel = new Label($"Folder: {folderPath.Path}");
                folderLabel.AddToClassList("asset-field-info");
                infoRow.Add(folderLabel);
            }
            
            // Component requirements
            if (assetType?.RequiredComponents != null && assetType.RequiredComponents.Length > 0)
            {
                var componentsLabel = new Label($"Required: {string.Join(", ", assetType.RequiredComponents)}");
                componentsLabel.AddToClassList("asset-field-info");
                infoRow.Add(componentsLabel);
            }
        }
        
        private string GetDisplayTypeName(string typeString)
        {
            if (typeString.StartsWith("Unity."))
            {
                return typeString.Substring(6); // Remove "Unity." prefix
            }
            return typeString;
        }
        
        private void SetValue(string path)
        {
            currentPath = path;
            pathField.SetValueWithoutNotify(path);
            
            // Update object field
            if (!string.IsNullOrEmpty(path))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                
                // Special handling for Sprite type
                if (assetType?.Type == UnityAssetTypes.Sprite && asset is Texture2D)
                {
                    // Try to load as sprite
                    var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                    if (sprites.Length > 0)
                    {
                        asset = sprites[0]; // Use first sprite
                    }
                }
                
                objectField.SetValueWithoutNotify(asset);
            }
            else
            {
                objectField.SetValueWithoutNotify(null);
            }
            
            ValidatePath();
            UpdateClearButtonVisibility();
        }
        
        private void OnPathChanged(string newPath)
        {
            if (currentPath != newPath)
            {
                currentPath = newPath;
                ValidatePath();
                UpdateClearButtonVisibility();
                onValueChanged?.Invoke(newPath);
            }
        }
        
        private void OnObjectChanged(UnityEngine.Object newObject)
        {
            if (newObject != null)
            {
                var path = AssetDatabase.GetAssetPath(newObject);
                SetValue(path);
                onValueChanged?.Invoke(path);
            }
        }
        
        private void OnBrowseClicked()
        {
            ShowAssetPicker();
        }
        
        private void OnClearClicked()
        {
            SetValue("");
            onValueChanged?.Invoke("");
        }
        
        private void UpdateClearButtonVisibility()
        {
            if (!isTableMode && clearButton != null)
            {
                clearButton.style.display = string.IsNullOrEmpty(currentPath) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }
        
        private void ValidatePath()
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                isValid = true;
                UpdateValidationDisplay();
                return;
            }
            
            string errorMessage;
            isValid = AttributeFieldHandler.ValidateAssetPath(currentPath, assetType, folderPath, out errorMessage);
            
            if (isTableMode)
            {
                // In table mode, update visual state
                UpdateValidationDisplay();
                if (!isValid && objectField != null)
                {
                    objectField.tooltip = errorMessage ?? "Asset does not match the required constraints";
                }
            }
            else
            {
                // In form mode, show validation label
                if (!isValid)
                {
                    validationLabel.text = errorMessage ?? "Asset does not match the required constraints";
                    validationLabel.style.display = DisplayStyle.Flex;
                    validationLabel.style.color = Color.red;
                    pathField.AddToClassList("field-invalid");
                }
                else
                {
                    validationLabel.style.display = DisplayStyle.None;
                    pathField.RemoveFromClassList("field-invalid");
                }
            }
        }
        
        private void UpdateValidationDisplay()
        {
            if (isTableMode)
            {
                if (!isValid)
                {
                    AddToClassList("invalid");
                }
                else
                {
                    RemoveFromClassList("invalid");
                }
            }
        }
        
        private void ShowAssetPicker()
        {
            // Get filtered assets
            var filteredPaths = AttributeFieldHandler.GetFilteredAssetPaths(assetType, folderPath);
            
            if (filteredPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("No Assets Found", 
                    "No assets found matching the specified constraints.", "OK");
                return;
            }
            
            // Create menu
            var menu = new GenericMenu();
            
            // Add "None" option
            menu.AddItem(new GUIContent("None"), string.IsNullOrEmpty(currentPath), () => {
                SetValue("");
                onValueChanged?.Invoke("");
            });
            
            menu.AddSeparator("");
            
            // Group by folder
            var groupedPaths = new Dictionary<string, List<string>>();
            foreach (var path in filteredPaths)
            {
                var directory = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                if (!groupedPaths.ContainsKey(directory))
                {
                    groupedPaths[directory] = new List<string>();
                }
                groupedPaths[directory].Add(path);
            }
            
            // Add items to menu
            foreach (var group in groupedPaths)
            {
                foreach (var path in group.Value)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (asset != null)
                    {
                        var displayName = $"{group.Key}/{asset.name}";
                        var isSelected = path == currentPath;
                        
                        menu.AddItem(new GUIContent(displayName), isSelected, () => {
                            SetValue(path);
                            onValueChanged?.Invoke(path);
                        });
                    }
                }
            }
            
            menu.ShowAsContext();
        }
        
        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.paths.Length > 0)
            {
                var path = DragAndDrop.paths[0];
                string errorMessage;
                if (AttributeFieldHandler.ValidateAssetPath(path, assetType, folderPath, out errorMessage))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                }
                else
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                }
            }
            evt.StopPropagation();
        }
        
        private void OnDragPerform(DragPerformEvent evt)
        {
            if (DragAndDrop.paths.Length > 0)
            {
                var path = DragAndDrop.paths[0];
                string errorMessage;
                if (AttributeFieldHandler.ValidateAssetPath(path, assetType, folderPath, out errorMessage))
                {
                    DragAndDrop.AcceptDrag();
                    SetValue(path);
                    onValueChanged?.Invoke(path);
                }
                else
                {
                    // Show error dialog for drag & drop
                    EditorUtility.DisplayDialog("Invalid Asset", 
                        errorMessage ?? "Asset does not match the required constraints", "OK");
                }
            }
            evt.StopPropagation();
        }
    }
}