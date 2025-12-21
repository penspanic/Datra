using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.DataTypes;
using Datra.Localization;
using Datra.Editor.Models;
using Datra.Unity.Editor.Interfaces;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;
using Datra.Unity.Editor.Windows;
using Datra.Unity.Editor.Components.FieldHandlers;
using FieldCreationContext = Datra.Unity.Editor.Components.FieldHandlers.FieldCreationContext;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// A custom property field component with change tracking and revert functionality
    /// </summary>
    public class DatraPropertyField : VisualElement
    {
        private PropertyInfo property;
        private object target;
        private FieldLayoutMode layoutMode;
        private bool isModified = false;

        private VisualElement fieldContainer;
        private Label propertyLabel;
        private VisualElement inputField;
        private Button revertButton;
        private VisualElement modifiedIndicator;

        // Localization support
        private ILocaleProvider localeProvider;

        public event Action<string, object> OnValueChanged;
        public event Action<string> OnRevertRequested;

        /// <summary>
        /// Checks if DatraPropertyField can handle editing this property
        /// </summary>
        public static bool CanHandle(PropertyInfo property, ILocaleProvider localeProvider = null)
        {
            if (property == null)
                return false;

            // LocaleRef with FixedLocale attribute - can handle even if CanWrite=false
            if (property.PropertyType == typeof(LocaleRef))
            {
                var hasFixedLocale = property.GetCustomAttribute<Datra.Attributes.FixedLocaleAttribute>() != null;
                if (hasFixedLocale && localeProvider != null)
                    return true;
                // Regular LocaleRef without FixedLocale or no provider - cannot handle
                return false;
            }

            // Regular properties - need CanWrite
            return property.CanWrite;
        }

        // Whether this field is being edited in a popup editor
        private bool isPopupEditor;

        public DatraPropertyField(
            object target,
            PropertyInfo property,
            FieldLayoutMode layoutMode = FieldLayoutMode.Form,
            ILocaleProvider localeProvider = null,
            bool isPopupEditor = false)
        {
            this.target = target;
            this.property = property;
            this.layoutMode = layoutMode;
            this.localeProvider = localeProvider;
            this.isPopupEditor = isPopupEditor;

            AddToClassList("datra-property-field");
            AddToClassList($"layout-{layoutMode.ToString().ToLower()}");

            Initialize();
            CreateField();
            UpdateModifiedState();
        }
        
        private void Initialize()
        {
            // Main container
            fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("property-field-container");
            
            if (layoutMode == FieldLayoutMode.Table)
            {
                // Table layout: minimal, no label, inline indicators
                this.style.flexGrow = 1; // Make the field fill the cell
                fieldContainer.style.flexDirection = FlexDirection.Row;
                fieldContainer.style.alignItems = Align.Center;
                fieldContainer.style.flexGrow = 1; // Container also fills the field
                Add(fieldContainer);

                // No header in table mode - indicators will be inline
                modifiedIndicator = new Label("🟡");
                modifiedIndicator.AddToClassList("property-modified-indicator");
                modifiedIndicator.AddToClassList("table-mode");
                modifiedIndicator.tooltip = "Modified";
                modifiedIndicator.style.display = DisplayStyle.None;
                modifiedIndicator.style.fontSize = 10;
                modifiedIndicator.style.marginLeft = 4;
                modifiedIndicator.style.marginRight = 2;
                modifiedIndicator.style.unityTextAlign = TextAnchor.MiddleCenter;
                fieldContainer.Add(modifiedIndicator);

                // Revert button inline
                revertButton = new Button(() => RevertValue());
                revertButton.text = "↺";
                revertButton.tooltip = "Revert";
                revertButton.AddToClassList("property-revert-button");
                revertButton.AddToClassList("table-mode");
                revertButton.style.display = DisplayStyle.None;
                revertButton.style.width = 20;
                revertButton.style.height = 18;
                revertButton.style.fontSize = 12;
                revertButton.style.marginRight = 4;
                fieldContainer.Add(revertButton);
            }
            else
            {
                // Form/Inline layout: full header with label
                Add(fieldContainer);

                // Skip header in popup mode (title already shows property name)
                if (!isPopupEditor)
                {
                    // Header with label and indicators
                    var headerContainer = new VisualElement();
                    headerContainer.AddToClassList("property-field-header");

                    if (layoutMode == FieldLayoutMode.Inline)
                    {
                        headerContainer.style.flexDirection = FlexDirection.Row;
                        headerContainer.style.alignItems = Align.Center;
                    }

                    fieldContainer.Add(headerContainer);

                    // Property label
                    propertyLabel = new Label(ObjectNames.NicifyVariableName(property.Name));
                    propertyLabel.AddToClassList("property-field-label");
                    headerContainer.Add(propertyLabel);

                    // Modified indicator
                    modifiedIndicator = new VisualElement();
                    modifiedIndicator.AddToClassList("property-modified-indicator");
                    modifiedIndicator.tooltip = "This field has been modified";
                    headerContainer.Add(modifiedIndicator);

                    // Revert button
                    revertButton = new Button(() => RevertValue());
                    revertButton.text = "↺";
                    revertButton.tooltip = "Revert to original value";
                    revertButton.AddToClassList("property-revert-button");
                    headerContainer.Add(revertButton);
                }
                
                // Input container (separate for form mode)
                if (layoutMode == FieldLayoutMode.Form)
                {
                    var inputContainer = new VisualElement();
                    inputContainer.AddToClassList("property-field-input-container");
                    fieldContainer.Add(inputContainer);
                }
            }
        }
        
        private void CreateField()
        {
            var value = property.GetValue(target);
            var propertyType = property.PropertyType;
            
            // Find or create input container based on layout mode
            VisualElement inputContainer;
            if (layoutMode == FieldLayoutMode.Table)
            {
                // In table mode, input goes directly in the field container
                inputContainer = fieldContainer;
            }
            else
            {
                inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-input-container");
                if (inputContainer == null && layoutMode == FieldLayoutMode.Inline)
                {
                    // For inline mode, input goes in the header container
                    inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-header");
                }
            }
            
            inputField = CreateInputField(propertyType, value);
            if (inputField != null)
            {
                inputField.AddToClassList("property-field-input");
                if (layoutMode == FieldLayoutMode.Table)
                {
                    inputField.style.flexGrow = 1;
                    inputField.style.minHeight = 20;
                    // Insert before indicators
                    inputContainer.Insert(0, inputField);
                }
                else
                {
                    inputContainer.Add(inputField);
                }
            }
            else
            {
                // Log error if field creation failed
                Debug.LogError($"[DatraPropertyField] Failed to create field for property '{property.Name}' of type {propertyType.FullName}");
            }
        }
        
        private VisualElement CreateInputField(Type propertyType, object value)
        {
            // Special case: LocaleRef without FixedLocale attribute - show readonly
            if (propertyType == typeof(LocaleRef))
            {
                var hasFixedLocale = property.GetCustomAttribute<Datra.Attributes.FixedLocaleAttribute>() != null;
                if (!hasFixedLocale || localeProvider == null)
                {
                    var textField = new TextField();
                    textField.value = value?.ToString() ?? "";
                    textField.isReadOnly = true;
                    return textField;
                }
            }

            // Use FieldTypeRegistry for all supported types
            var context = new FieldCreationContext(
                property,
                target,
                value,
                layoutMode,
                newValue =>
                {
                    property.SetValue(target, newValue);
                    OnFieldValueChanged(newValue);
                },
                localeProvider,
                isPopupEditor);

            return FieldTypeRegistry.CreateField(context);
        }
        
        private void OnFieldValueChanged(object newValue)
        {
            OnValueChanged?.Invoke(property.Name, newValue);
        }

        /// <summary>
        /// Set modified state externally (controlled by parent view's change tracker)
        /// </summary>
        public void SetModified(bool modified)
        {
            isModified = modified;
            UpdateModifiedState();
        }

        private void UpdateModifiedState()
        {
            if (isModified)
            {
                AddToClassList("modified-cell");
                AddToClassList("field-modified");
                if (modifiedIndicator != null)
                {
                    modifiedIndicator.style.display = DisplayStyle.Flex;
                    modifiedIndicator.tooltip = "Modified";
                }
                if (revertButton != null)
                    revertButton.style.display = DisplayStyle.Flex;

                // In table mode, also add modified-cell class to parent table-cell
                if (layoutMode == FieldLayoutMode.Table && parent != null)
                {
                    parent.AddToClassList("modified-cell");
                }
            }
            else
            {
                RemoveFromClassList("modified-cell");
                RemoveFromClassList("field-modified");
                if (modifiedIndicator != null)
                    modifiedIndicator.style.display = DisplayStyle.None;
                if (revertButton != null)
                    revertButton.style.display = DisplayStyle.None;

                // In table mode, also remove modified-cell class from parent table-cell
                if (layoutMode == FieldLayoutMode.Table && parent != null)
                {
                    parent.RemoveFromClassList("modified-cell");
                }
            }
        }

        private void RevertValue()
        {
            // Notify parent view to handle revert with external tracker
            OnRevertRequested?.Invoke(property.Name);
        }
        
        public void RefreshField()
        {
            // Remove existing input field
            if (inputField != null && inputField.parent != null)
            {
                inputField.parent.Remove(inputField);
            }

            // Recreate the field
            CreateField();
            UpdateModifiedState();
        }
    }
}