using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.DataTypes;
using Datra.Localization;
using Datra.Unity.Editor.Interfaces;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Layout mode for DatraPropertyField
    /// </summary>
    public enum DatraFieldLayoutMode
    {
        Form,     // Full layout with label on top
        Table,    // Compact layout for table cells
        Inline    // Inline layout with label on left
    }

    /// <summary>
    /// A custom property field component with change tracking and revert functionality
    /// </summary>
    public class DatraPropertyField : VisualElement
    {
        private PropertyInfo property;
        private object target;
        private DatraFieldLayoutMode layoutMode;
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

        public DatraPropertyField(
            object target,
            PropertyInfo property,
            DatraFieldLayoutMode layoutMode = DatraFieldLayoutMode.Form,
            ILocaleProvider localeProvider = null)
        {
            this.target = target;
            this.property = property;
            this.layoutMode = layoutMode;
            this.localeProvider = localeProvider;

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
            
            if (layoutMode == DatraFieldLayoutMode.Table)
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
                
                // Header with label and indicators
                var headerContainer = new VisualElement();
                headerContainer.AddToClassList("property-field-header");
                
                if (layoutMode == DatraFieldLayoutMode.Inline)
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
                
                // Input container (separate for form mode)
                if (layoutMode == DatraFieldLayoutMode.Form)
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
            if (layoutMode == DatraFieldLayoutMode.Table)
            {
                // In table mode, input goes directly in the field container
                inputContainer = fieldContainer;
            }
            else
            {
                inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-input-container");
                if (inputContainer == null && layoutMode == DatraFieldLayoutMode.Inline)
                {
                    // For inline mode, input goes in the header container
                    inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-header");
                }
            }
            
            inputField = CreateInputField(propertyType, value);
            if (inputField != null)
            {
                inputField.AddToClassList("property-field-input");
                if (layoutMode == DatraFieldLayoutMode.Table)
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
            // Check for LocaleRef with FixedLocale attribute
            if (propertyType == typeof(LocaleRef))
            {
                var hasFixedLocale = property.GetCustomAttribute<Datra.Attributes.FixedLocaleAttribute>() != null;
                if (hasFixedLocale && localeProvider != null)
                {
                    return CreateLocaleRefField(value as LocaleRef?);
                }
                else
                {
                    // Regular LocaleRef field (if needed in future)
                    var textField = new TextField();
                    textField.value = value?.ToString() ?? "";
                    textField.isReadOnly = true;
                    return textField;
                }
            }
            else if (propertyType == typeof(string))
            {
                // Check for asset attributes
                if (AttributeFieldHandler.HasAssetAttributes(property))
                {
                    var assetType = AttributeFieldHandler.GetAssetTypeAttribute(property);
                    var folderPath = AttributeFieldHandler.GetFolderPathAttribute(property);

                    var assetField = new AssetFieldElement(assetType, folderPath, value as string ?? "", (newValue) =>
                    {
                        property.SetValue(target, newValue);
                        OnFieldValueChanged(newValue);
                    }, layoutMode == DatraFieldLayoutMode.Table);

                    return assetField;
                }
                else
                {
                    var textField = new TextField();
                    textField.value = value as string ?? "";
                    textField.RegisterValueChangedCallback(evt =>
                    {
                        property.SetValue(target, evt.newValue);
                        OnFieldValueChanged(evt.newValue);
                    });
                    return textField;
                }
            }
            else if (propertyType == typeof(int))
            {
                var intField = new IntegerField();
                intField.value = (int)(value ?? 0);
                intField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return intField;
            }
            else if (propertyType == typeof(float))
            {
                var floatField = new FloatField();
                floatField.value = (float)(value ?? 0f);
                floatField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return floatField;
            }
            else if (propertyType == typeof(bool))
            {
                var toggle = new Toggle();
                toggle.value = (bool)(value ?? false);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return toggle;
            }
            else if (propertyType.IsEnum)
            {
                var enumField = new EnumField((Enum)value);
                enumField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return enumField;
            }
            else if (propertyType == typeof(Vector2))
            {
                var vector2Field = new Vector2Field();
                vector2Field.value = (Vector2)(value ?? Vector2.zero);
                vector2Field.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return vector2Field;
            }
            else if (propertyType == typeof(Vector3))
            {
                var vector3Field = new Vector3Field();
                vector3Field.value = (Vector3)(value ?? Vector3.zero);
                vector3Field.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return vector3Field;
            }
            else if (propertyType == typeof(Color))
            {
                var colorField = new ColorField();
                colorField.value = (Color)(value ?? Color.white);
                colorField.RegisterValueChangedCallback(evt =>
                {
                    property.SetValue(target, evt.newValue);
                    OnFieldValueChanged(evt.newValue);
                });
                return colorField;
            }
            else if (propertyType == typeof(int[]))
            {
                return CreateArrayField<int>(value as int[], CreateIntElement);
            }
            else if (propertyType == typeof(string[]))
            {
                return CreateArrayField<string>(value as string[], CreateStringElement);
            }
            else if (propertyType == typeof(float[]))
            {
                return CreateArrayField<float>(value as float[], CreateFloatElement);
            }
            else if (IsDataRefArrayType(propertyType))
            {
                return CreateDataRefArrayField(propertyType, value as Array);
            }
            else if (IsEnumArrayType(propertyType))
            {
                return CreateEnumArrayField(propertyType, value as Array);
            }
            else if (IsDataRefType(propertyType))
            {
                return CreateDataRefField(propertyType, value);
            }
            else
            {
                // Log warning for unsupported type
                Debug.LogWarning($"[DatraPropertyField] Unsupported property type: {propertyType.FullName} for property '{property.Name}'. " +
                                $"Consider adding support for this type or using a custom editor.");
                
                // For unsupported types, create a read-only field with type info
                var container = new VisualElement();
                container.AddToClassList("unsupported-field-container");
                
                var readOnlyField = new TextField();
                readOnlyField.value = value?.ToString() ?? "null";
                readOnlyField.isReadOnly = true;
                readOnlyField.AddToClassList("unsupported-field");
                container.Add(readOnlyField);
                
                var typeInfo = new Label($"Type: {propertyType.Name}");
                typeInfo.AddToClassList("type-info");
                container.Add(typeInfo);
                
                return container;
            }
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
                modifiedIndicator.style.display = DisplayStyle.Flex;
                revertButton.style.display = DisplayStyle.Flex;
                modifiedIndicator.tooltip = "Modified";

                // In table mode, also add modified-cell class to parent table-cell
                if (layoutMode == DatraFieldLayoutMode.Table && parent != null)
                {
                    parent.AddToClassList("modified-cell");
                }
            }
            else
            {
                RemoveFromClassList("modified-cell");
                RemoveFromClassList("field-modified");
                modifiedIndicator.style.display = DisplayStyle.None;
                revertButton.style.display = DisplayStyle.None;

                // In table mode, also remove modified-cell class from parent table-cell
                if (layoutMode == DatraFieldLayoutMode.Table && parent != null)
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
            if (layoutMode == DatraFieldLayoutMode.Table)
            {
                // In table mode, update the compact display
                UpdateCompactArrayDisplay();
            }
            else
            {
                // In other modes, recreate the field
                var inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-input-container");
                if (inputContainer != null)
                {
                    inputContainer.Clear();
                }
                else if (layoutMode == DatraFieldLayoutMode.Inline)
                {
                    // For inline mode, clear the input field directly
                    if (inputField != null && inputField.parent != null)
                    {
                        inputField.parent.Remove(inputField);
                    }
                }
                
                CreateField();
            }
            
            UpdateModifiedState();
        }
        
        private VisualElement CreateArrayField<T>(T[] array, Func<int, T, VisualElement> createElement)
        {
            var container = new VisualElement();
            container.AddToClassList("array-field-container");
            
            // Table mode: compact horizontal layout
            if (layoutMode == DatraFieldLayoutMode.Table)
            {
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                
                // Edit button to open full editor (placed first)
                var editButton = new Button(() => OpenPropertyEditor());
                editButton.text = "✏";
                editButton.tooltip = $"Edit array ({array?.Length ?? 0} items)";
                editButton.AddToClassList("array-edit-button");
                editButton.style.marginRight = 4;
                container.Add(editButton);
                
                // Compact array display - show as comma-separated values
                var arrayDisplay = new TextField();
                arrayDisplay.isReadOnly = true;
                arrayDisplay.style.flexGrow = 1;
                arrayDisplay.AddToClassList("array-compact-display");
                
                if (array != null && array.Length > 0)
                {
                    var displayText = string.Join(", ", array);
                    if (displayText.Length > 50)
                    {
                        displayText = displayText.Substring(0, 47) + "...";
                    }
                    arrayDisplay.value = $"[{displayText}]";
                }
                else
                {
                    arrayDisplay.value = "[]";
                }
                
                container.Add(arrayDisplay);
                
                return container;
            }
            
            // Form/Inline mode: full vertical layout
            container.style.flexDirection = FlexDirection.Column;
            
            // Header with array info and add button
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("array-header");
            headerContainer.style.flexDirection = FlexDirection.Row;
            headerContainer.style.justifyContent = Justify.SpaceBetween;
            headerContainer.style.alignItems = Align.Center;
            headerContainer.style.marginBottom = 4;
            
            var sizeLabel = new Label($"Size: {array?.Length ?? 0}");
            sizeLabel.AddToClassList("array-size-label");
            headerContainer.Add(sizeLabel);
            
            var addButton = new Button(() => AddArrayElement<T>(container, createElement));
            addButton.text = "+";
            addButton.tooltip = "Add element";
            addButton.AddToClassList("array-add-button");
            addButton.style.width = 20;
            addButton.style.height = 20;
            headerContainer.Add(addButton);
            
            container.Add(headerContainer);
            
            // Elements container
            var elementsContainer = new VisualElement();
            elementsContainer.AddToClassList("array-elements");
            elementsContainer.style.marginLeft = 16;
            container.Add(elementsContainer);
            
            // Add existing elements
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    var elementContainer = CreateArrayElementContainer(i, array[i], createElement, container);
                    elementsContainer.Add(elementContainer);
                }
            }
            
            container.userData = elementsContainer;
            return container;
        }
        
        private VisualElement CreateArrayElementContainer<T>(int index, T value, Func<int, T, VisualElement> createElement, VisualElement arrayContainer)
        {
            var elementContainer = new VisualElement();
            elementContainer.AddToClassList("array-element");
            elementContainer.style.flexDirection = FlexDirection.Row;
            elementContainer.style.alignItems = Align.Center;
            elementContainer.style.marginBottom = 2;
            
            // Index label
            var indexLabel = new Label($"[{index}]");
            indexLabel.AddToClassList("array-index");
            indexLabel.style.minWidth = 30;
            indexLabel.style.marginRight = 8;
            elementContainer.Add(indexLabel);
            
            // Element field
            var elementField = createElement(index, value);
            elementField.style.flexGrow = 1;
            elementContainer.Add(elementField);
            
            // Remove button
            var removeButton = new Button(() => RemoveArrayElement(elementContainer, arrayContainer));
            removeButton.text = "−";
            removeButton.tooltip = "Remove element";
            removeButton.AddToClassList("array-remove-button");
            removeButton.style.width = 20;
            removeButton.style.height = 20;
            removeButton.style.marginLeft = 4;
            elementContainer.Add(removeButton);
            
            return elementContainer;
        }
        
        private VisualElement CreateIntElement(int index, int value)
        {
            var intField = new IntegerField();
            intField.value = value;
            intField.RegisterValueChangedCallback(evt => UpdateArrayValue());
            return intField;
        }
        
        private VisualElement CreateStringElement(int index, string value)
        {
            var textField = new TextField();
            textField.value = value ?? "";
            textField.RegisterValueChangedCallback(evt => UpdateArrayValue());
            return textField;
        }
        
        private VisualElement CreateFloatElement(int index, float value)
        {
            var floatField = new FloatField();
            floatField.value = value;
            floatField.RegisterValueChangedCallback(evt => UpdateArrayValue());
            return floatField;
        }
        
        private VisualElement CreateEnumArrayField(Type arrayType, Array array)
        {
            // Table mode: compact display
            if (layoutMode == DatraFieldLayoutMode.Table)
            {
                var container = new VisualElement();
                container.AddToClassList("array-field-container");
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                
                // Edit button to open full editor (placed first)
                var editButton = new Button(() => OpenPropertyEditor());
                editButton.text = "✏";
                editButton.tooltip = $"Edit array ({array?.Length ?? 0} items)";
                editButton.AddToClassList("array-edit-button");
                editButton.style.marginRight = 4;
                container.Add(editButton);
                
                // Compact array display - show as comma-separated values
                var arrayDisplay = new TextField();
                arrayDisplay.isReadOnly = true;
                arrayDisplay.style.flexGrow = 1;
                arrayDisplay.AddToClassList("array-compact-display");
                
                if (array != null && array.Length > 0)
                {
                    var values = new string[array.Length];
                    for (int i = 0; i < array.Length; i++)
                    {
                        values[i] = array.GetValue(i)?.ToString() ?? "";
                    }
                    var displayText = string.Join(", ", values);
                    if (displayText.Length > 50)
                    {
                        displayText = displayText.Substring(0, 47) + "...";
                    }
                    arrayDisplay.value = $"[{displayText}]";
                }
                else
                {
                    arrayDisplay.value = "[]";
                }
                
                container.Add(arrayDisplay);
                
                return container;
            }
            
            // Form/Inline mode: use full layout
            var elementType = arrayType.GetElementType();
            // Convert Array to object[] for CreateArrayField
            object[] objectArray = null;
            if (array != null)
            {
                objectArray = new object[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    objectArray[i] = array.GetValue(i);
                }
            }
            return CreateArrayField<object>(objectArray, (index, value) => CreateEnumElement(elementType, value));
        }
        
        private VisualElement CreateEnumElement(Type enumType, object value)
        {
            var enumField = new EnumField((Enum)(value ?? Enum.GetValues(enumType).GetValue(0)));
            enumField.RegisterValueChangedCallback(evt => UpdateArrayValue());
            return enumField;
        }
        
        private void AddArrayElement<T>(VisualElement arrayContainer, Func<int, T, VisualElement> createElement)
        {
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;
            
            var currentCount = elementsContainer.childCount;
            var defaultValue = default(T);
            
            var elementContainer = CreateArrayElementContainer(currentCount, defaultValue, createElement, arrayContainer);
            elementsContainer.Add(elementContainer);
            
            UpdateArrayValue();
            UpdateArraySizeLabel(arrayContainer);
        }
        
        private void RemoveArrayElement(VisualElement elementToRemove, VisualElement arrayContainer)
        {
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;
            
            elementsContainer.Remove(elementToRemove);
            
            // Update indices
            var elements = elementsContainer.Query<VisualElement>(className: "array-element").ToList();
            for (int i = 0; i < elements.Count; i++)
            {
                var indexLabel = elements[i].Q<Label>(className: "array-index");
                if (indexLabel != null)
                {
                    indexLabel.text = $"[{i}]";
                }
            }
            
            UpdateArrayValue();
            UpdateArraySizeLabel(arrayContainer);
        }
        
        private void UpdateArraySizeLabel(VisualElement arrayContainer)
        {
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;
            
            var sizeLabel = arrayContainer.Q<Label>(className: "array-size-label");
            if (sizeLabel != null)
            {
                sizeLabel.text = $"Size: {elementsContainer.childCount}";
            }
        }
        
        private void OpenPropertyEditor()
        {
            DatraPropertyEditorPopup.ShowEditor(property, target, () => {
                RefreshField();
                OnValueChanged?.Invoke(property.Name, property.GetValue(target));
            });
        }

        private void UpdateArrayValue()
        {
            if (inputField == null) return;
            
            var arrayContainer = inputField as VisualElement;
            if (arrayContainer == null) return;
            
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;
            
            var propertyType = property.PropertyType;
            
            if (propertyType == typeof(int[]))
            {
                var values = new System.Collections.Generic.List<int>();
                var intFields = elementsContainer.Query<IntegerField>().ToList();
                foreach (var field in intFields)
                {
                    values.Add(field.value);
                }
                property.SetValue(target, values.ToArray());
                OnFieldValueChanged(values.ToArray());
            }
            else if (propertyType == typeof(string[]))
            {
                var values = new System.Collections.Generic.List<string>();
                var textFields = elementsContainer.Query<TextField>().ToList();
                foreach (var field in textFields)
                {
                    values.Add(field.value);
                }
                property.SetValue(target, values.ToArray());
                OnFieldValueChanged(values.ToArray());
            }
            else if (propertyType == typeof(float[]))
            {
                var values = new System.Collections.Generic.List<float>();
                var floatFields = elementsContainer.Query<FloatField>().ToList();
                foreach (var field in floatFields)
                {
                    values.Add(field.value);
                }
                property.SetValue(target, values.ToArray());
                OnFieldValueChanged(values.ToArray());
            }
            else if (IsEnumArrayType(propertyType))
            {
                var elementType = propertyType.GetElementType();
                var values = new System.Collections.Generic.List<object>();
                var enumFields = elementsContainer.Query<EnumField>().ToList();
                foreach (var field in enumFields)
                {
                    values.Add(field.value);
                }
                var typedArray = Array.CreateInstance(elementType, values.Count);
                for (int i = 0; i < values.Count; i++)
                {
                    typedArray.SetValue(values[i], i);
                }
                property.SetValue(target, typedArray);
                OnFieldValueChanged(typedArray);
            }
            else if (IsDataRefArrayType(propertyType))
            {
                UpdateDataRefArrayValue();
            }
        }
        
        private bool IsDataRefType(Type type)
        {
            return type.IsGenericType && 
                   (type.GetGenericTypeDefinition() == typeof(StringDataRef<>) ||
                    type.GetGenericTypeDefinition() == typeof(IntDataRef<>));
        }
        
        private bool IsDataRefArrayType(Type type)
        {
            return type.IsArray && IsDataRefType(type.GetElementType());
        }
        
        private bool IsEnumArrayType(Type type)
        {
            return type.IsArray && type.GetElementType().IsEnum;
        }
        
        private VisualElement CreateDataRefField(Type dataRefType, object value)
        {
            var container = new VisualElement();
            container.AddToClassList("dataref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            
            // Get the generic arguments
            var genericArgs = dataRefType.GetGenericArguments();
            var referencedType = genericArgs[0];
            var keyType = dataRefType.GetGenericTypeDefinition() == typeof(IntDataRef<>) ? typeof(int) : typeof(string);
            
            // Display field (declare first)
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("dataref-display-field");
            
            // Update display value function
            void UpdateDisplayValue()
            {
                if (value != null)
                {
                    var keyValue = value.GetType().GetProperty("Value")?.GetValue(value);
                    if (keyValue != null)
                    {
                        displayField.value = $"[{keyValue}]";
                        
                        // Try to get the referenced object name
                        var dataContext = DatraBootstrapper.GetCurrentDataContext();
                        if (dataContext != null)
                        {
                            var evaluateMethod = value.GetType().GetMethod("Evaluate");
                            if (evaluateMethod != null)
                            {
                                try
                                {
                                    var referencedObject = evaluateMethod.Invoke(value, new[] { dataContext });
                                    if (referencedObject != null)
                                    {
                                        var nameProperty = referencedObject.GetType().GetProperty("Name") ??
                                                         referencedObject.GetType().GetProperty("StringId") ??
                                                         referencedObject.GetType().GetProperty("Title");
                                        var name = nameProperty?.GetValue(referencedObject)?.ToString();
                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            displayField.value = $"[{keyValue}] {name}";
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        displayField.value = "(None)";
                    }
                }
                else
                {
                    displayField.value = "(None)";
                }
            }
            
            // Select button
            var selectButton = new Button(() =>
            {
                var dataContext = DatraBootstrapper.GetCurrentDataContext();
                if (dataContext != null)
                {
                    DatraReferenceSelector.Show(referencedType, dataContext, (selectedId) =>
                    {
                        if (selectedId != null)
                        {
                            // Create new DataRef instance
                            var newDataRef = Activator.CreateInstance(dataRefType);
                            newDataRef.GetType().GetProperty("Value")?.SetValue(newDataRef, selectedId);
                            property.SetValue(target, newDataRef);
                            value = newDataRef;
                        }
                        else
                        {
                            // Create empty DataRef
                            var newDataRef = Activator.CreateInstance(dataRefType);
                            property.SetValue(target, newDataRef);
                            value = newDataRef;
                        }
                        container.userData = value; // Store updated value
                        UpdateDisplayValue();
                        OnFieldValueChanged(value);
                    });
                }
            });
            selectButton.text = "🔍";
            selectButton.AddToClassList("dataref-select-button");
            selectButton.style.width = 24;
            selectButton.style.minWidth = 24;
            selectButton.style.height = 20;
            selectButton.style.minHeight = 20;
            selectButton.style.paddingLeft = 0;
            selectButton.style.paddingRight = 0;
            selectButton.style.paddingTop = 0;
            selectButton.style.paddingBottom = 0;
            selectButton.style.marginRight = 2;
            selectButton.style.fontSize = 12;
            
            // Clear button
            var clearButton = new Button(() =>
            {
                var newDataRef = Activator.CreateInstance(dataRefType);
                property.SetValue(target, newDataRef);
                value = newDataRef;
                container.userData = value; // Store updated value
                UpdateDisplayValue();
                OnFieldValueChanged(value);
            });
            clearButton.text = "×";
            clearButton.tooltip = "Clear";
            clearButton.AddToClassList("dataref-clear-button");
            clearButton.style.width = 24;
            clearButton.style.minWidth = 24;
            clearButton.style.height = 20;
            clearButton.style.minHeight = 20;
            clearButton.style.paddingLeft = 0;
            clearButton.style.paddingRight = 0;
            clearButton.style.paddingTop = 0;
            clearButton.style.paddingBottom = 0;
            clearButton.style.marginRight = 4;
            clearButton.style.fontSize = 14;
            
            UpdateDisplayValue();
            
            // Add elements in order: buttons first, then display field
            container.Add(selectButton);
            container.Add(clearButton);
            container.Add(displayField);
            
            // Store initial value in userData
            container.userData = value;
            
            return container;
        }
        
        private VisualElement CreateDataRefArrayField(Type arrayType, Array array)
        {
            // Table mode: compact display
            if (layoutMode == DatraFieldLayoutMode.Table)
            {
                var container = new VisualElement();
                container.AddToClassList("array-field-container");
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignItems = Align.Center;
                
                // Edit button to open full editor (placed first)
                var editButton = new Button(() => OpenPropertyEditor());
                editButton.text = "✏";
                editButton.tooltip = $"Edit array ({array?.Length ?? 0} items)";
                editButton.AddToClassList("array-edit-button");
                editButton.style.marginRight = 4;
                container.Add(editButton);
                
                // Compact array display - show as comma-separated values
                var arrayDisplay = new TextField();
                arrayDisplay.isReadOnly = true;
                arrayDisplay.style.flexGrow = 1;
                arrayDisplay.AddToClassList("array-compact-display");
                
                if (array != null && array.Length > 0)
                {
                    var values = new string[array.Length];
                    var elementType2 = arrayType.GetElementType();
                    for (int i = 0; i < array.Length; i++)
                    {
                        var item = array.GetValue(i);
                        if (item != null)
                        {
                            var keyValue = elementType2.GetProperty("Value")?.GetValue(item);
                            values[i] = keyValue != null ? $"→{keyValue}" : "(None)";
                        }
                        else
                        {
                            values[i] = "(None)";
                        }
                    }
                    var displayText = string.Join(", ", values);
                    if (displayText.Length > 50)
                    {
                        displayText = displayText.Substring(0, 47) + "...";
                    }
                    arrayDisplay.value = $"[{displayText}]";
                }
                else
                {
                    arrayDisplay.value = "[]";
                }
                
                container.Add(arrayDisplay);
                
                return container;
            }
            
            // Form/Inline mode: use full layout
            var elementType = arrayType.GetElementType();
            // Convert Array to object[] for CreateArrayField
            object[] objectArray = null;
            if (array != null)
            {
                objectArray = new object[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    objectArray[i] = array.GetValue(i);
                }
            }
            return CreateArrayField<object>(objectArray, (index, value) => CreateDataRefElement(elementType, value));
        }
        
        private VisualElement CreateDataRefElement(Type dataRefType, object value)
        {
            // For array elements, we create a specialized DataRef field
            // that doesn't try to set the property directly
            var field = CreateDataRefFieldForArrayElement(dataRefType, value);
            
            // Store the current value in userData for later retrieval
            field.userData = value;
            
            return field;
        }
        
        private VisualElement CreateDataRefFieldForArrayElement(Type dataRefType, object value)
        {
            var container = new VisualElement();
            container.AddToClassList("dataref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            
            // Get the generic arguments
            var genericArgs = dataRefType.GetGenericArguments();
            var referencedType = genericArgs[0];
            var keyType = dataRefType.GetGenericTypeDefinition() == typeof(IntDataRef<>) ? typeof(int) : typeof(string);
            
            // Display field
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("dataref-display-field");
            
            // Update display value function
            void UpdateDisplayValue()
            {
                if (value != null)
                {
                    var keyValue = value.GetType().GetProperty("Value")?.GetValue(value);
                    if (keyValue != null)
                    {
                        displayField.value = $"[{keyValue}]";
                        
                        // Try to get the referenced object name
                        var dataContext = DatraBootstrapper.GetCurrentDataContext();
                        if (dataContext != null)
                        {
                            var evaluateMethod = value.GetType().GetMethod("Evaluate");
                            if (evaluateMethod != null)
                            {
                                try
                                {
                                    var referencedObject = evaluateMethod.Invoke(value, new[] { dataContext });
                                    if (referencedObject != null)
                                    {
                                        var nameProperty = referencedObject.GetType().GetProperty("Name") ??
                                                         referencedObject.GetType().GetProperty("StringId") ??
                                                         referencedObject.GetType().GetProperty("Title");
                                        var name = nameProperty?.GetValue(referencedObject)?.ToString();
                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            displayField.value = $"[{keyValue}] {name}";
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        displayField.value = "(None)";
                    }
                }
                else
                {
                    displayField.value = "(None)";
                }
            }
            
            UpdateDisplayValue();
            
            // Select button
            var selectButton = new Button(() =>
            {
                var dataContext = DatraBootstrapper.GetCurrentDataContext();
                if (dataContext != null)
                {
                    DatraReferenceSelector.Show(referencedType, dataContext, (selectedId) =>
                    {
                        if (selectedId != null)
                        {
                            // Create new DataRef instance
                            var newDataRef = Activator.CreateInstance(dataRefType);
                            newDataRef.GetType().GetProperty("Value")?.SetValue(newDataRef, selectedId);
                            value = newDataRef;
                        }
                        else
                        {
                            // Create empty DataRef
                            var newDataRef = Activator.CreateInstance(dataRefType);
                            value = newDataRef;
                        }
                        container.userData = value; // Store updated value
                        UpdateDisplayValue();
                        // Trigger array update
                        UpdateArrayValue();
                    });
                }
            });
            selectButton.text = "Select";
            selectButton.AddToClassList("dataref-select-button");
            selectButton.style.width = 60;
            
            // Clear button
            var clearButton = new Button(() =>
            {
                var newDataRef = Activator.CreateInstance(dataRefType);
                value = newDataRef;
                container.userData = value; // Store updated value
                UpdateDisplayValue();
                // Trigger array update
                UpdateArrayValue();
            });
            clearButton.text = "×";
            clearButton.tooltip = "Clear";
            clearButton.AddToClassList("dataref-clear-button");
            clearButton.style.width = 20;
            
            container.Add(displayField);
            container.Add(selectButton);
            container.Add(clearButton);
            
            // Store initial value in userData
            container.userData = value;
            
            return container;
        }
        
        private void UpdateDataRefArrayValue()
        {
            if (inputField == null) return;
            
            var arrayContainer = inputField as VisualElement;
            if (arrayContainer == null) return;
            
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;
            
            var elementType = property.PropertyType.GetElementType();
            var values = new System.Collections.Generic.List<object>();
            
            // Collect all DataRef values from the array elements
            var arrayElements = elementsContainer.Query<VisualElement>(className: "array-element").ToList();
            foreach (var element in arrayElements)
            {
                // Find the dataref container within this element
                var dataRefContainer = element.Q<VisualElement>(className: "dataref-field-container");
                if (dataRefContainer != null && dataRefContainer.userData != null)
                {
                    values.Add(dataRefContainer.userData);
                }
                else
                {
                    // Create empty DataRef if not found
                    values.Add(Activator.CreateInstance(elementType));
                }
            }
            
            var newArray = Array.CreateInstance(elementType, values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                newArray.SetValue(values[i], i);
            }
            
            property.SetValue(target, newArray);
            OnFieldValueChanged(newArray);
        }
        
        private void UpdateCompactArrayDisplay()
        {
            if (layoutMode != DatraFieldLayoutMode.Table || inputField == null) return;
            
            var arrayDisplay = inputField.Q<TextField>(className: "array-compact-display");
            if (arrayDisplay == null) return;
            
            var value = property.GetValue(target);
            var propertyType = property.PropertyType;
            
            if (propertyType.IsArray)
            {
                var array = value as Array;
                if (array != null && array.Length > 0)
                {
                    if (IsDataRefArrayType(propertyType))
                    {
                        // DataRef array
                        var values = new string[array.Length];
                        var elementType = propertyType.GetElementType();
                        for (int i = 0; i < array.Length; i++)
                        {
                            var item = array.GetValue(i);
                            if (item != null)
                            {
                                var keyValue = elementType.GetProperty("Value")?.GetValue(item);
                                values[i] = keyValue != null ? $"→{keyValue}" : "(None)";
                            }
                            else
                            {
                                values[i] = "(None)";
                            }
                        }
                        var displayText = string.Join(", ", values);
                        if (displayText.Length > 50)
                        {
                            displayText = displayText.Substring(0, 47) + "...";
                        }
                        arrayDisplay.value = $"[{displayText}]";
                    }
                    else if (IsEnumArrayType(propertyType))
                    {
                        // Enum array
                        var values = new string[array.Length];
                        for (int i = 0; i < array.Length; i++)
                        {
                            values[i] = array.GetValue(i)?.ToString() ?? "";
                        }
                        var displayText = string.Join(", ", values);
                        if (displayText.Length > 50)
                        {
                            displayText = displayText.Substring(0, 47) + "...";
                        }
                        arrayDisplay.value = $"[{displayText}]";
                    }
                    else
                    {
                        // Regular array
                        var displayText = string.Join(", ", array.Cast<object>());
                        if (displayText.Length > 50)
                        {
                            displayText = displayText.Substring(0, 47) + "...";
                        }
                        arrayDisplay.value = $"[{displayText}]";
                    }
                }
                else
                {
                    arrayDisplay.value = "[]";
                }
                
                // Update button tooltip
                var editButton = inputField.Q<Button>(className: "array-edit-button");
                if (editButton != null)
                {
                    editButton.tooltip = $"Edit array ({array?.Length ?? 0} items)";
                }
            }
        }

        /// <summary>
        /// Creates a readonly field for LocaleRef with edit button
        /// </summary>
        private VisualElement CreateLocaleRefField(LocaleRef? localeRefValue)
        {
            var container = new VisualElement();
            container.AddToClassList("locale-ref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexGrow = 1;

            // Edit button (moved to front)
            var editButton = new Button();
            editButton.text = "⋯";
            editButton.AddToClassList("locale-ref-edit-button");
            editButton.style.marginRight = 4;
            editButton.style.width = 24;
            editButton.style.minWidth = 24;
            editButton.tooltip = "Edit Locale";

            // Readonly text field showing the localized text
            var textField = new TextField();
            textField.AddToClassList("locale-ref-text-field");
            textField.isReadOnly = true;
            textField.style.flexGrow = 1;

            // Get localized text using provider
            if (localeRefValue.HasValue && localeProvider != null)
            {
                var localeRef = localeRefValue.Value;
                var localizedText = localeProvider.GetLocaleText(localeRef);
                textField.value = localizedText ?? "(Missing)";
                textField.tooltip = $"Key: {localeRef.Key}";
            }
            else
            {
                textField.value = "(No locale key)";
            }

            editButton.clicked += () =>
            {
                if (localeRefValue.HasValue && localeProvider != null)
                {
                    var localeRef = localeRefValue.Value;

                    // Get button's world bounds for popup positioning
                    var buttonWorldBound = editButton.worldBound;

                    localeProvider.ShowLocaleEditPopup(localeRef, buttonWorldBound, (updatedText) =>
                    {
                        // Update the displayed text after editing
                        textField.value = updatedText ?? "(Missing)";

                        // Notify that something changed
                        OnFieldValueChanged(localeRefValue);
                    });
                }
            };

            container.Add(editButton);
            container.Add(textField);

            return container;
        }

    }
}