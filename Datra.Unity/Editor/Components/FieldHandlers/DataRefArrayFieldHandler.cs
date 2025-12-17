using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.DataTypes;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for DataRef array types (StringDataRef<T>[], IntDataRef<T>[])
    /// </summary>
    public class DataRefArrayFieldHandler : IFieldTypeHandler
    {
        public int Priority => 35;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return type.IsArray && DataRefFieldHandler.IsDataRefType(type.GetElementType());
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
                return CreateCompactDisplay(container, array, elementType, context);
            }

            // Form/Inline mode: full layout
            return CreateFullLayout(container, array, elementType, context);
        }

        private VisualElement CreateCompactDisplay(VisualElement container, Array array, Type elementType, FieldCreationContext context)
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

        private void UpdateDisplayValue(TextField arrayDisplay, Array array, Type elementType)
        {
            if (array != null && array.Length > 0)
            {
                var values = new string[array.Length];
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
            else
            {
                arrayDisplay.value = "[]";
            }
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

            // DataRef field
            var dataRefField = CreateDataRefElementField(elementType, value, () => UpdateArrayValue(arrayContainer, context));
            dataRefField.style.flexGrow = 1;
            elementContainer.Add(dataRefField);

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

        private VisualElement CreateDataRefElementField(Type dataRefType, object value, Action onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("dataref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var genericArgs = dataRefType.GetGenericArguments();
            var referencedType = genericArgs[0];

            // Display field
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("dataref-display-field");

            var currentValue = value;

            void UpdateDisplayValue()
            {
                if (currentValue != null)
                {
                    var keyValue = currentValue.GetType().GetProperty("Value")?.GetValue(currentValue);
                    if (keyValue != null)
                    {
                        displayField.value = $"[{keyValue}]";

                        var dataContext = DatraBootstrapper.GetCurrentDataContext();
                        if (dataContext != null)
                        {
                            var evaluateMethod = currentValue.GetType().GetMethod("Evaluate");
                            if (evaluateMethod != null)
                            {
                                try
                                {
                                    var referencedObject = evaluateMethod.Invoke(currentValue, new object[] { dataContext });
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
                    DatraReferenceSelector.Show(referencedType, dataContext, selectedId =>
                    {
                        var newDataRef = Activator.CreateInstance(dataRefType);
                        if (selectedId != null)
                        {
                            newDataRef.GetType().GetProperty("Value")?.SetValue(newDataRef, selectedId);
                        }
                        currentValue = newDataRef;
                        container.userData = currentValue;
                        UpdateDisplayValue();
                        onChanged?.Invoke();
                    });
                }
            });
            selectButton.text = "🔍";
            selectButton.AddToClassList("dataref-select-button");
            selectButton.style.width = 24;
            selectButton.style.height = 20;
            selectButton.style.marginRight = 2;

            // Clear button
            var clearButton = new Button(() =>
            {
                var newDataRef = Activator.CreateInstance(dataRefType);
                currentValue = newDataRef;
                container.userData = currentValue;
                UpdateDisplayValue();
                onChanged?.Invoke();
            });
            clearButton.text = "×";
            clearButton.tooltip = "Clear";
            clearButton.AddToClassList("dataref-clear-button");
            clearButton.style.width = 24;
            clearButton.style.height = 20;
            clearButton.style.marginRight = 4;

            UpdateDisplayValue();

            container.Add(selectButton);
            container.Add(clearButton);
            container.Add(displayField);

            container.userData = currentValue;
            return container;
        }

        private void AddElement(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var currentCount = elementsContainer.childCount;
            var defaultValue = Activator.CreateInstance(elementType);

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
            var dataRefContainers = elementsContainer.Query<VisualElement>(className: "dataref-field-container").ToList();
            foreach (var container in dataRefContainers)
            {
                values.Add(container.userData ?? Activator.CreateInstance(elementType));
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

        private class ArrayUserData
        {
            public VisualElement ElementsContainer { get; set; }
            public Type ElementType { get; set; }
        }
    }
}
