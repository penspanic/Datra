using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

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
    }
}