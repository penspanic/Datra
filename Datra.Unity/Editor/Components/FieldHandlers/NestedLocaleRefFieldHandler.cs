#nullable disable
using System;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Localization;
using Datra.Unity.Editor.Interfaces;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for NestedLocaleRef fields with NestedLocale attribute.
    /// Creates UI for editing nested locale values within collection elements.
    /// </summary>
    public class NestedLocaleRefFieldHandler : IUnityFieldHandler
    {
        public int Priority => 101; // Higher than LocaleRefFieldHandler

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            if (type != typeof(NestedLocaleRef))
                return false;

            // Only handle if has NestedLocale attribute
            if (member is PropertyInfo property)
            {
                return property.GetCustomAttribute<NestedLocaleAttribute>() != null;
            }

            return false;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var nestedLocaleValue = context.Value is NestedLocaleRef nlr ? nlr : default;
            var localeProvider = context.LocaleProvider;

            var container = new VisualElement();
            container.AddToClassList("nested-locale-ref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexGrow = 1;

            // Edit button
            var editButton = new Button();
            editButton.text = "\u22EF"; // "⋯"
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

            // Check if we have the required context
            var hasContext = context.CollectionElementIndex.HasValue &&
                             context.RootDataObject != null &&
                             context.CollectionElement != null &&
                             localeProvider != null &&
                             nestedLocaleValue.HasValue;

            LocaleRef? resolvedLocaleRef = null;

            if (hasContext)
            {
                // Evaluate the nested locale to get the actual key
                resolvedLocaleRef = localeProvider.EvaluateNestedLocale(
                    nestedLocaleValue,
                    context.RootDataObject,
                    context.CollectionElementIndex.Value,
                    context.CollectionElement);

                var localizedText = localeProvider.GetLocaleText(resolvedLocaleRef.Value);
                textField.value = localizedText ?? "(Missing)";
                textField.tooltip = $"Key: {resolvedLocaleRef.Value.Key}";
            }
            else
            {
                // Missing context - show warning
                if (localeProvider == null)
                    textField.value = "(No locale provider)";
                else if (!context.CollectionElementIndex.HasValue)
                    textField.value = "(No element index)";
                else if (context.RootDataObject == null)
                    textField.value = "(No root object)";
                else
                    textField.value = "(No nested locale)";

                editButton.SetEnabled(false);
            }

            editButton.clicked += () =>
            {
                if (resolvedLocaleRef.HasValue && localeProvider != null)
                {
                    var localeRef = resolvedLocaleRef.Value;
                    var buttonWorldBound = editButton.worldBound;

                    localeProvider.ShowLocaleEditPopup(localeRef, buttonWorldBound, updatedText =>
                    {
                        textField.value = updatedText ?? "(Missing)";
                        // NestedLocaleRef is readonly, changes are tracked via LocalizationContext
                        context.OnValueChanged?.Invoke(nestedLocaleValue);
                    });
                }
            };

            container.Add(editButton);
            container.Add(textField);

            return container;
        }
    }
}
