using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for enum array types
    /// </summary>
    public class EnumArrayFieldHandler : IFieldTypeHandler
    {
        public int Priority => 25;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type.IsArray && type.GetElementType()?.IsEnum == true;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var elementType = context.FieldType.GetElementType();
            var array = context.Value as Array;

            var container = new VisualElement();
            container.AddToClassList("array-field-container");

            // Table mode: compact display
            if (context.LayoutMode == DatraFieldLayoutMode.Table)
            {
                return CreateCompactDisplay(container, array, context);
            }

            // Form/Inline mode: full layout
            return CreateFullLayout(container, array, elementType, context);
        }

        private VisualElement CreateCompactDisplay(VisualElement container, Array array, FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Compact array display (declare first for closure)
            var arrayDisplay = new TextField();
            arrayDisplay.isReadOnly = true;
            arrayDisplay.style.flexGrow = 1;
            arrayDisplay.AddToClassList("array-compact-display");
            UpdateDisplayValue(arrayDisplay, array);

            // Edit button - opens property editor popup
            var editButton = new Button(() =>
            {
                if (context.Property != null && context.Target != null)
                {
                    DatraPropertyEditorPopup.ShowEditor(context.Property, context.Target, () =>
                    {
                        var newArray = context.Property.GetValue(context.Target) as Array;
                        UpdateDisplayValue(arrayDisplay, newArray);
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

        private VisualElement CreateFullLayout(VisualElement container, Array array, Type elementType, FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Column;

            // Header
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

            container.userData = new ArrayUserData { ElementsContainer = elementsContainer, ElementType = elementType };
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

            // Enum field
            var defaultValue = value as Enum ?? (Enum)Enum.GetValues(elementType).GetValue(0);
            var enumField = new EnumField(defaultValue);
            enumField.style.flexGrow = 1;
            enumField.RegisterValueChangedCallback(_ => UpdateArrayValue(arrayContainer, context));
            elementContainer.Add(enumField);

            // Remove button
            var removeButton = new Button(() => RemoveElement(elementContainer, arrayContainer, context));
            removeButton.text = "−";
            removeButton.tooltip = "Remove element";
            removeButton.AddToClassList("array-remove-button");
            removeButton.style.width = 20;
            removeButton.style.height = 20;
            removeButton.style.marginLeft = 4;
            elementContainer.Add(removeButton);

            return elementContainer;
        }

        private void AddElement(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var currentCount = elementsContainer.childCount;
            var defaultValue = Enum.GetValues(elementType).GetValue(0);

            var elementContainer = CreateElementContainer(currentCount, defaultValue, elementType, arrayContainer, context);
            elementsContainer.Add(elementContainer);

            UpdateArrayValue(arrayContainer, context);
            UpdateSizeLabel(arrayContainer, elementsContainer.childCount);
        }

        private void RemoveElement(VisualElement elementToRemove, VisualElement arrayContainer, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
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

            UpdateArrayValue(arrayContainer, context);
            UpdateSizeLabel(arrayContainer, elementsContainer.childCount);
        }

        private void UpdateArrayValue(VisualElement arrayContainer, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var elementType = userData.ElementType;

            var values = new List<object>();
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

            context.OnValueChanged?.Invoke(typedArray);
        }

        private void UpdateSizeLabel(VisualElement arrayContainer, int count)
        {
            var sizeLabel = arrayContainer.Q<Label>(className: "array-size-label");
            if (sizeLabel != null)
            {
                sizeLabel.text = $"Size: {count}";
            }
        }

        private void UpdateDisplayValue(TextField arrayDisplay, Array array)
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
                arrayDisplay.value = $"[{displayText}]";
            }
            else
            {
                arrayDisplay.value = "[]";
            }
        }

        private class ArrayUserData
        {
            public VisualElement ElementsContainer { get; set; }
            public Type ElementType { get; set; }
        }
    }
}
