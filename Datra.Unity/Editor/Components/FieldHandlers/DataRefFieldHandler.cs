using System;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.DataTypes;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for DataRef types (StringDataRef<T>, IntDataRef<T>)
    /// </summary>
    public class DataRefFieldHandler : IFieldTypeHandler
    {
        public int Priority => 40;

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return IsDataRefType(type);
        }

        public static bool IsDataRefType(Type type)
        {
            return type.IsGenericType &&
                   (type.GetGenericTypeDefinition() == typeof(StringDataRef<>) ||
                    type.GetGenericTypeDefinition() == typeof(IntDataRef<>));
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var container = new VisualElement();
            container.AddToClassList("dataref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var dataRefType = context.FieldType;
            var genericArgs = dataRefType.GetGenericArguments();
            var referencedType = genericArgs[0];

            // Display field
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("dataref-display-field");

            // Store current value for closures
            var currentValue = context.Value;

            void UpdateDisplayValue()
            {
                if (currentValue != null)
                {
                    var keyValue = currentValue.GetType().GetProperty("Value")?.GetValue(currentValue);
                    if (keyValue != null)
                    {
                        displayField.value = $"[{keyValue}]";

                        // Try to get the referenced object name
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
                        context.OnValueChanged?.Invoke(currentValue);
                    });
                }
            });
            selectButton.text = "🔍";
            selectButton.AddToClassList("dataref-select-button");
            ApplyButtonStyle(selectButton, 24, 20);
            selectButton.style.marginRight = 2;

            // Clear button
            var clearButton = new Button(() =>
            {
                var newDataRef = Activator.CreateInstance(dataRefType);
                currentValue = newDataRef;
                container.userData = currentValue;
                UpdateDisplayValue();
                context.OnValueChanged?.Invoke(currentValue);
            });
            clearButton.text = "×";
            clearButton.tooltip = "Clear";
            clearButton.AddToClassList("dataref-clear-button");
            ApplyButtonStyle(clearButton, 24, 20);
            clearButton.style.marginRight = 4;
            clearButton.style.fontSize = 14;

            UpdateDisplayValue();

            // Add elements in order
            container.Add(selectButton);
            container.Add(clearButton);
            container.Add(displayField);

            container.userData = currentValue;
            return container;
        }

        private static void ApplyButtonStyle(Button button, int width, int height)
        {
            button.style.width = width;
            button.style.minWidth = width;
            button.style.height = height;
            button.style.minHeight = height;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
        }
    }
}
