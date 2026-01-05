using System;
using System.Reflection;
using UnityEngine.UIElements;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Unity.Editor.Interfaces;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for LocaleRef fields with FixedLocale attribute
    /// </summary>
    public class LocaleRefFieldHandler : IFieldTypeHandler
    {
        public int Priority => 100; // Highest priority - very specific type

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            if (type != typeof(LocaleRef))
                return false;

            // Only handle if has FixedLocale attribute
            if (member is PropertyInfo property)
            {
                return property.GetCustomAttribute<FixedLocaleAttribute>() != null;
            }

            return false;
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var localeRefValue = context.Value is LocaleRef lr ? lr : (LocaleRef?)null;
            var localeProvider = context.LocaleProvider;

            var container = new VisualElement();
            container.AddToClassList("locale-ref-field-container");
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexGrow = 1;

            // Edit button
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
                textField.value = localeProvider == null ? "(No locale provider)" : "(No locale key)";
            }

            editButton.clicked += () =>
            {
                if (localeRefValue.HasValue && localeProvider != null)
                {
                    var localeRef = localeRefValue.Value;
                    var buttonWorldBound = editButton.worldBound;

                    localeProvider.ShowLocaleEditPopup(localeRef, buttonWorldBound, updatedText =>
                    {
                        textField.value = updatedText ?? "(Missing)";
                        // LocaleRef key is readonly - changes are tracked via LocalizationContext
                        // MarkAsModified() is called in ShowLocaleEditPopup's onModified callback
                        // No need to call OnValueChanged since the LocaleRef value itself doesn't change
                    });
                }
            };

            container.Add(editButton);
            container.Add(textField);

            return container;
        }
    }
}
