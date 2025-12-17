using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Base class for array field handlers providing common UI scaffolding
    /// </summary>
    public abstract class BaseArrayFieldHandler : IFieldTypeHandler
    {
        public abstract int Priority { get; }
        public abstract bool CanHandle(Type type, MemberInfo member = null);

        /// <summary>
        /// Get the element type of the array
        /// </summary>
        protected abstract Type GetElementType(Type arrayType);

        /// <summary>
        /// Create an input field for a single array element
        /// </summary>
        protected abstract VisualElement CreateElementField(Type elementType, object value, Action onChanged);

        /// <summary>
        /// Get the value from an element field
        /// </summary>
        protected abstract object GetElementValue(VisualElement elementField);

        /// <summary>
        /// Get display text for a single element (used in compact mode)
        /// </summary>
        protected abstract string GetElementDisplayText(object element, Type elementType);

        /// <summary>
        /// Get default value for new element
        /// </summary>
        protected virtual object GetDefaultValue(Type elementType)
        {
            if (elementType == typeof(string)) return "";
            if (elementType.IsValueType) return Activator.CreateInstance(elementType);
            return null;
        }

        /// <summary>
        /// CSS class name for element fields (used in value extraction)
        /// </summary>
        protected abstract string ElementFieldClassName { get; }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var elementType = GetElementType(context.FieldType);
            var array = context.Value as Array;

            var container = new VisualElement();
            container.AddToClassList("array-field-container");

            if (context.LayoutMode == DatraFieldLayoutMode.Table)
            {
                return CreateCompactDisplay(container, array, elementType, context);
            }

            return CreateFullLayout(container, array, elementType, context);
        }

        protected virtual VisualElement CreateCompactDisplay(VisualElement container, Array array, Type elementType, FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Compact array display (declare first for closure)
            var arrayDisplay = new TextField();
            arrayDisplay.isReadOnly = true;
            arrayDisplay.style.flexGrow = 1;
            arrayDisplay.AddToClassList("array-compact-display");
            UpdateDisplayValue(arrayDisplay, array, elementType);

            // Edit button - opens property editor popup
            var editButton = new Button(() =>
            {
                if (context.Property != null && context.Target != null)
                {
                    DatraPropertyEditorPopup.ShowEditor(context.Property, context.Target, () =>
                    {
                        var newArray = context.Property.GetValue(context.Target) as Array;
                        UpdateDisplayValue(arrayDisplay, newArray, elementType);
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

        protected void UpdateDisplayValue(TextField arrayDisplay, Array array, Type elementType)
        {
            if (array != null && array.Length > 0)
            {
                var values = new string[array.Length];
                for (int i = 0; i < array.Length; i++)
                {
                    values[i] = GetElementDisplayText(array.GetValue(i), elementType);
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

        protected virtual VisualElement CreateFullLayout(VisualElement container, Array array, Type elementType, FieldCreationContext context)
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

            container.userData = new ArrayUserData { ElementsContainer = elementsContainer, ElementType = elementType };
            return container;
        }

        protected virtual VisualElement CreateElementContainer(int index, object value, Type elementType, VisualElement arrayContainer, FieldCreationContext context)
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

        protected void AddElement(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var currentCount = elementsContainer.childCount;
            var defaultValue = GetDefaultValue(elementType);

            var elementContainer = CreateElementContainer(currentCount, defaultValue, elementType, arrayContainer, context);
            elementsContainer.Add(elementContainer);

            UpdateArrayValue(arrayContainer, elementType, context);
            UpdateSizeLabel(arrayContainer, elementsContainer.childCount);
        }

        protected void RemoveElement(VisualElement elementToRemove, VisualElement arrayContainer, Type elementType, FieldCreationContext context)
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

            UpdateArrayValue(arrayContainer, elementType, context);
            UpdateSizeLabel(arrayContainer, elementsContainer.childCount);
        }

        protected virtual void UpdateArrayValue(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var values = new List<object>();
            var fields = elementsContainer.Query<VisualElement>(className: ElementFieldClassName).ToList();

            foreach (var field in fields)
            {
                values.Add(GetElementValue(field));
            }

            var typedArray = Array.CreateInstance(elementType, values.Count);
            for (int i = 0; i < values.Count; i++)
            {
                typedArray.SetValue(values[i], i);
            }

            context.OnValueChanged?.Invoke(typedArray);
        }

        protected void UpdateSizeLabel(VisualElement arrayContainer, int count)
        {
            var sizeLabel = arrayContainer.Q<Label>(className: "array-size-label");
            if (sizeLabel != null)
            {
                sizeLabel.text = $"Size: {count}";
            }
        }

        protected class ArrayUserData
        {
            public VisualElement ElementsContainer { get; set; }
            public Type ElementType { get; set; }
        }
    }
}
