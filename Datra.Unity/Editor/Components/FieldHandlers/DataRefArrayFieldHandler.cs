using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.Unity.Editor.UI;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for DataRef array types (StringDataRef<T>[], IntDataRef<T>[])
    /// </summary>
    public class DataRefArrayFieldHandler : BaseArrayFieldHandler
    {
        public override int Priority => 35;

        protected override string ElementFieldClassName => "dataref-field-container";

        public override bool CanHandle(Type type, MemberInfo member = null)
        {
            return type.IsArray && DataRefFieldHandler.IsDataRefType(type.GetElementType());
        }

        protected override Type GetElementType(Type arrayType)
        {
            return arrayType.GetElementType();
        }

        protected override string GetElementDisplayText(object element, Type elementType)
        {
            if (element != null)
            {
                var keyValue = elementType.GetProperty("Value")?.GetValue(element);
                return keyValue != null ? $"→{keyValue}" : "(None)";
            }
            return "(None)";
        }

        protected override VisualElement CreateElementField(Type elementType, object value, Action onChanged)
        {
            var container = new VisualElement();
            container.AddToClassList(ElementFieldClassName);
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var genericArgs = elementType.GetGenericArguments();
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
                        TryResolveDisplayName(displayField, currentValue, keyValue);
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
                        var newDataRef = Activator.CreateInstance(elementType);
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
                var newDataRef = Activator.CreateInstance(elementType);
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

        private void TryResolveDisplayName(TextField displayField, object dataRef, object keyValue)
        {
            var dataContext = DatraBootstrapper.GetCurrentDataContext();
            if (dataContext == null) return;

            var evaluateMethod = dataRef.GetType().GetMethod("Evaluate");
            if (evaluateMethod == null) return;

            try
            {
                var referencedObject = evaluateMethod.Invoke(dataRef, new object[] { dataContext });
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

        protected override object GetElementValue(VisualElement elementField)
        {
            return elementField.userData;
        }

        protected override void UpdateArrayValue(VisualElement arrayContainer, Type elementType, FieldCreationContext context)
        {
            var userData = arrayContainer.userData as ArrayUserData;
            if (userData == null) return;

            var elementsContainer = userData.ElementsContainer;
            var values = new List<object>();
            var dataRefContainers = elementsContainer.Query<VisualElement>(className: ElementFieldClassName).ToList();

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
    }
}
