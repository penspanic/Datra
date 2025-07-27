using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.DataTypes;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// A custom property field component with change tracking and revert functionality
    /// </summary>
    public class DatraPropertyField : VisualElement
    {
        private PropertyInfo property;
        private object target;
        private DatraPropertyTracker tracker;
        
        private VisualElement fieldContainer;
        private Label propertyLabel;
        private VisualElement inputField;
        private Button revertButton;
        private VisualElement modifiedIndicator;
        
        public event Action<string, object> OnValueChanged;
        
        public DatraPropertyField(object target, PropertyInfo property, DatraPropertyTracker tracker)
        {
            this.target = target;
            this.property = property;
            this.tracker = tracker;
            
            AddToClassList("datra-property-field");
            
            Initialize();
            CreateField();
            UpdateModifiedState();
            
            // Subscribe to tracker events
            tracker.OnPropertyModified += OnTrackerPropertyModified;
        }
        
        private void Initialize()
        {
            // Main container
            fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("property-field-container");
            Add(fieldContainer);
            
            // Header with label and indicators
            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("property-field-header");
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
            
            // Input container
            var inputContainer = new VisualElement();
            inputContainer.AddToClassList("property-field-input-container");
            fieldContainer.Add(inputContainer);
        }
        
        private void CreateField()
        {
            var value = property.GetValue(target);
            var propertyType = property.PropertyType;
            var inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-input-container");
            
            inputField = CreateInputField(propertyType, value);
            if (inputField != null)
            {
                inputField.AddToClassList("property-field-input");
                inputContainer.Add(inputField);
            }
        }
        
        private VisualElement CreateInputField(Type propertyType, object value)
        {
            if (propertyType == typeof(string))
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
            else if (IsEnumArrayType(propertyType))
            {
                return CreateEnumArrayField(propertyType, value as Array);
            }
            else if (IsDataRefType(propertyType))
            {
                return CreateDataRefField(propertyType, value);
            }
            else if (IsDataRefArrayType(propertyType))
            {
                return CreateDataRefArrayField(propertyType, value as Array);
            }
            else
            {
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
            tracker.TrackChange(target, property.Name, newValue);
            OnValueChanged?.Invoke(property.Name, newValue);
        }
        
        private void OnTrackerPropertyModified(string key, bool isModified)
        {
            var expectedKey = $"{target.GetHashCode()}_{property.Name}";
            if (key == expectedKey)
            {
                UpdateModifiedState();
            }
        }
        
        private void UpdateModifiedState()
        {
            var isModified = tracker.IsPropertyModified(target, property.Name);
            
            if (isModified)
            {
                AddToClassList("field-modified");
                modifiedIndicator.style.display = DisplayStyle.Flex;
                revertButton.style.display = DisplayStyle.Flex;
                
                // Update tooltip with original value
                var originalValue = tracker.GetOriginalValue(target, property.Name);
                modifiedIndicator.tooltip = $"Modified (Original: {originalValue?.ToString() ?? "null"})";
            }
            else
            {
                RemoveFromClassList("field-modified");
                modifiedIndicator.style.display = DisplayStyle.None;
                revertButton.style.display = DisplayStyle.None;
            }
        }
        
        private void RevertValue()
        {
            tracker.RevertProperty(target, property.Name);
            RefreshField();
        }
        
        public void RefreshField()
        {
            var inputContainer = fieldContainer.Q<VisualElement>(className: "property-field-input-container");
            inputContainer.Clear();
            
            CreateField();
            UpdateModifiedState();
        }
        
        public void Cleanup()
        {
            // Unsubscribe from tracker events
            if (tracker != null)
            {
                tracker.OnPropertyModified -= OnTrackerPropertyModified;
            }
        }
        
        private VisualElement CreateArrayField<T>(T[] array, Func<int, T, VisualElement> createElement)
        {
            var container = new VisualElement();
            container.AddToClassList("array-field-container");
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
            
            // Display field
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("dataref-display-field");
            
            // Update display value
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
            selectButton.text = "Select";
            selectButton.AddToClassList("dataref-select-button");
            selectButton.style.width = 60;
            
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
            clearButton.style.width = 20;
            
            container.Add(displayField);
            container.Add(selectButton);
            container.Add(clearButton);
            
            // Store initial value in userData
            container.userData = value;
            
            return container;
        }
        
        private VisualElement CreateDataRefArrayField(Type arrayType, Array array)
        {
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
        
    }
}