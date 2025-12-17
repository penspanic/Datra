using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for basic array types (int[], string[], float[])
    /// </summary>
    public class ArrayFieldHandler : IFieldTypeHandler
    {
        public int Priority => 20;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            if (!type.IsArray)
                return false;

            var elementType = type.GetElementType();
            return elementType == typeof(int) ||
                   elementType == typeof(string) ||
                   elementType == typeof(float);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var elementType = context.FieldType.GetElementType();
            var array = context.Value as Array;

            var container = new VisualElement();
            container.AddToClassList("array-field-container");

            // Table mode: compact horizontal layout
            if (context.LayoutMode == DatraFieldLayoutMode.Table)
            {
                return CreateCompactArrayField(container, array, elementType, context);
            }

            // Form/Inline mode: full vertical layout
            return CreateFullArrayField(container, array, elementType, context);
        }

        private VisualElement CreateCompactArrayField(VisualElement container, Array array, Type elementType, FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Compact array display (declare first for closure)
            var arrayDisplay = new TextField();
            arrayDisplay.isReadOnly = true;
            arrayDisplay.style.flexGrow = 1;
            arrayDisplay.AddToClassList("array-compact-display");
            UpdateCompactDisplay(arrayDisplay, array);

            // Edit button - opens property editor popup
            var editButton = new Button(() =>
            {
                if (context.Property != null && context.Target != null)
                {
                    DatraPropertyEditorPopup.ShowEditor(context.Property, context.Target, () =>
                    {
                        // Refresh the display after editing
                        var newArray = context.Property.GetValue(context.Target) as Array;
                        UpdateCompactDisplay(arrayDisplay, newArray);
                        context.OnValueChanged?.Invoke(newArray);
                    });
                }
            });
            editButton.text = "✏";
            editButton.tooltip = $"Edit array ({array?.Length ?? 0} items)";
            editButton.AddToClassList("array-edit-button");
            editButton.style.marginRight = 4;

            container.Add(editButton);
            container.Add(arrayDisplay);

            return container;
        }

        private VisualElement CreateFullArrayField(VisualElement container, Array array, Type elementType, FieldCreationContext context)
        {
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

            var addButton = new Button(() => AddElement(container, elementType, context));
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
                    var elementContainer = CreateElementContainer(i, array.GetValue(i), elementType, container, context);
                    elementsContainer.Add(elementContainer);
                }
            }

            container.userData = elementsContainer;
            return container;
        }

        private VisualElement CreateElementContainer(int index, object value, Type elementType, VisualElement arrayContainer, FieldCreationContext context)
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
            var elementField = CreateElementField(elementType, value, () => UpdateArrayValue(arrayContainer, elementType, context));
            elementField.style.flexGrow = 1;
            elementContainer.Add(elementField);

            // Remove button
            var removeButton = new Button(() => RemoveElement(elementContainer, arrayContainer, elementType, context));
            removeButton.text = "−";
            removeButton.tooltip = "Remove element";
            removeButton.AddToClassList("array-remove-button");
            removeButton.style.width = 20;
            removeButton.style.height = 20;
            removeButton.style.marginLeft = 4;
            elementContainer.Add(removeButton);

            return elementContainer;
        }

        private VisualElement CreateElementField(Type elementType, object value, Action onChanged)
        {
            if (elementType == typeof(int))
            {
                var intField = new IntegerField();
                intField.value = value != null ? Convert.ToInt32(value) : 0;
                intField.RegisterValueChangedCallback(_ => onChanged());
                return intField;
            }
            else if (elementType == typeof(string))
            {
                var textField = new TextField();
                textField.value = value as string ?? "";
                textField.RegisterValueChangedCallback(_ => onChanged());
                return textField;
            }
            else if (elementType == typeof(float))
            {
                var floatField = new FloatField();
                floatField.value = value != null ? Convert.ToSingle(value) : 0f;
                floatField.RegisterValueChangedCallback(_ => onChanged());
                return floatField;
            }

            // Fallback
            var readOnly = new TextField();
            readOnly.value = value?.ToString() ?? "";
            readOnly.isReadOnly = true;
            return readOnly;
        }

        private void AddElement(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;

            var currentCount = elementsContainer.childCount;
            var defaultValue = elementType == typeof(string) ? "" : Activator.CreateInstance(elementType);

            var elementContainer = CreateElementContainer(currentCount, defaultValue, elementType, arrayContainer, context);
            elementsContainer.Add(elementContainer);

            UpdateArrayValue(arrayContainer, elementType, context);
            UpdateSizeLabel(arrayContainer, elementsContainer.childCount);
        }

        private void RemoveElement(VisualElement elementToRemove, VisualElement arrayContainer, Type elementType, FieldCreationContext context)
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

            UpdateArrayValue(arrayContainer, elementType, context);
            UpdateSizeLabel(arrayContainer, elementsContainer.childCount);
        }

        private void UpdateArrayValue(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var elementsContainer = arrayContainer.userData as VisualElement;
            if (elementsContainer == null) return;

            Array newArray;

            if (elementType == typeof(int))
            {
                var values = new List<int>();
                var fields = elementsContainer.Query<IntegerField>().ToList();
                foreach (var field in fields)
                {
                    values.Add(field.value);
                }
                newArray = values.ToArray();
            }
            else if (elementType == typeof(string))
            {
                var values = new List<string>();
                var fields = elementsContainer.Query<TextField>().ToList();
                foreach (var field in fields)
                {
                    values.Add(field.value);
                }
                newArray = values.ToArray();
            }
            else if (elementType == typeof(float))
            {
                var values = new List<float>();
                var fields = elementsContainer.Query<FloatField>().ToList();
                foreach (var field in fields)
                {
                    values.Add(field.value);
                }
                newArray = values.ToArray();
            }
            else
            {
                return;
            }

            context.OnValueChanged?.Invoke(newArray);
        }

        private void UpdateSizeLabel(VisualElement arrayContainer, int count)
        {
            var sizeLabel = arrayContainer.Q<Label>(className: "array-size-label");
            if (sizeLabel != null)
            {
                sizeLabel.text = $"Size: {count}";
            }
        }

        private void UpdateCompactDisplay(TextField displayField, Array array)
        {
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
                displayField.value = $"[{displayText}]";
            }
            else
            {
                displayField.value = "[]";
            }
        }
    }
}
