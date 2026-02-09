#nullable disable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using Datra.Editor.Models;
using Datra.Unity.Editor.Windows;

namespace Datra.Unity.Editor.Components.FieldHandlers
{
    /// <summary>
    /// Handler for Dictionary&lt;TKey, TValue&gt; types
    /// </summary>
    public class DictionaryFieldHandler : IUnityFieldHandler
    {
        public int Priority => 23;  // Higher than ListFieldHandler (22)

        public bool CanHandle(Type type, MemberInfo member = null)
        {
            if (!type.IsGenericType)
                return false;

            return type.GetGenericTypeDefinition() == typeof(Dictionary<,>);
        }

        public VisualElement CreateField(FieldCreationContext context)
        {
            var genericArgs = context.FieldType.GetGenericArguments();
            var keyType = genericArgs[0];
            var valueType = genericArgs[1];
            var dictionary = context.Value;

            var container = new VisualElement();
            container.AddToClassList("dictionary-field-container");

            if (context.LayoutMode == FieldLayoutMode.Table)
            {
                return CreateCompactDisplay(container, dictionary, keyType, valueType, context);
            }

            return CreateFullLayout(container, dictionary, keyType, valueType, context);
        }

        #region Compact Display

        private VisualElement CreateCompactDisplay(
            VisualElement container,
            object dictionary,
            Type keyType,
            Type valueType,
            FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            var count = GetDictionaryCount(dictionary);
            var displayText = $"{{{count} entries}}";

            // Display field (declared first for lambda capture)
            var displayField = new TextField();
            displayField.isReadOnly = true;
            displayField.value = displayText;
            displayField.style.flexGrow = 1;
            displayField.AddToClassList("dictionary-compact-display");

            // Edit button
            var editButton = new Button(() =>
            {
                if (context.Property != null && context.Target != null)
                {
                    DatraPropertyEditorPopup.ShowEditor(context.Property, context.Target, () =>
                    {
                        var newDict = context.Property.GetValue(context.Target);
                        displayField.value = $"{{{GetDictionaryCount(newDict)} entries}}";
                        context.OnValueChanged?.Invoke(newDict);
                    });
                }
            });
            editButton.text = "✏";
            editButton.tooltip = $"Edit dictionary ({displayText})";
            editButton.AddToClassList("dictionary-edit-button");
            editButton.style.marginRight = 4;

            container.Add(editButton);
            container.Add(displayField);
            return container;
        }

        #endregion

        #region Full Layout

        private VisualElement CreateFullLayout(
            VisualElement container,
            object dictionary,
            Type keyType,
            Type valueType,
            FieldCreationContext context)
        {
            container.style.flexDirection = FlexDirection.Column;

            var entries = GetDictionaryEntries(dictionary);

            // Entries container
            var entriesContainer = new VisualElement();
            entriesContainer.AddToClassList("dictionary-entries");

            // Add existing entries
            foreach (var entry in entries)
            {
                var entryContainer = CreateEntryContainer(entry.Key, entry.Value, keyType, valueType, container, context);
                entriesContainer.Add(entryContainer);
            }

            Foldout foldout = null;

            if (context.IsPopupEditor)
            {
                // Popup mode: no foldout, direct layout
                entriesContainer.style.marginLeft = 0;
                container.Add(entriesContainer);

                // Add button at bottom
                var addContainer = new VisualElement();
                addContainer.style.flexDirection = FlexDirection.Row;
                addContainer.style.justifyContent = Justify.FlexEnd;
                addContainer.style.marginTop = 8;

                var addButton = new Button(() => AddEntry(container, keyType, valueType, context));
                addButton.text = "+";
                addButton.tooltip = "Add entry";
                addButton.AddToClassList("dictionary-add-button");
                addButton.style.width = 24;
                addButton.style.height = 20;
                addContainer.Add(addButton);

                container.Add(addContainer);
            }
            else
            {
                // Form mode: wrap in foldout
                foldout = new Foldout();
                foldout.text = $"Size: {entries.Count}";
                foldout.value = false; // Collapsed by default
                foldout.AddToClassList("dictionary-foldout");

                entriesContainer.style.marginLeft = 8;
                foldout.Add(entriesContainer);

                // Add button container (inside foldout)
                var addContainer = new VisualElement();
                addContainer.style.flexDirection = FlexDirection.Row;
                addContainer.style.justifyContent = Justify.FlexEnd;
                addContainer.style.marginTop = 4;

                var addButton = new Button(() =>
                {
                    AddEntry(container, keyType, valueType, context);
                    foldout.value = true; // Expand when adding
                });
                addButton.text = "+";
                addButton.tooltip = "Add entry";
                addButton.AddToClassList("dictionary-add-button");
                addButton.style.width = 24;
                addButton.style.height = 20;
                addContainer.Add(addButton);

                foldout.Add(addContainer);
                container.Add(foldout);
            }

            container.userData = new DictionaryUserData
            {
                EntriesContainer = entriesContainer,
                KeyType = keyType,
                ValueType = valueType,
                Foldout = foldout
            };

            return container;
        }

        private VisualElement CreateEntryContainer(
            object key,
            object value,
            Type keyType,
            Type valueType,
            VisualElement dictionaryContainer,
            FieldCreationContext context)
        {
            var entryContainer = new VisualElement();
            entryContainer.AddToClassList("dictionary-entry");
            entryContainer.style.flexDirection = FlexDirection.Row;
            entryContainer.style.alignItems = Align.Center;
            entryContainer.style.marginBottom = 4;
            entryContainer.style.paddingLeft = 8;
            entryContainer.style.paddingRight = 8;
            entryContainer.style.paddingTop = 2;
            entryContainer.style.paddingBottom = 2;
            entryContainer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            entryContainer.userData = new KeyValuePair<object, object>(key, value);

            // Key field
            var keyField = CreateSimpleField(keyType, key, newKey =>
            {
                UpdateEntryKey(entryContainer, newKey, dictionaryContainer, keyType, valueType, context);
            });
            keyField.AddToClassList("dictionary-key-field");
            keyField.style.minWidth = 80;
            keyField.style.marginRight = 8;

            // Arrow label
            var arrowLabel = new Label("→");
            arrowLabel.style.marginRight = 8;
            arrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            // Value field
            var valueField = CreateSimpleField(valueType, value, newValue =>
            {
                UpdateEntryValue(entryContainer, newValue, dictionaryContainer, keyType, valueType, context);
            });
            valueField.AddToClassList("dictionary-value-field");
            valueField.style.flexGrow = 1;

            // Remove button
            var removeButton = new Button(() => RemoveEntry(entryContainer, dictionaryContainer, keyType, valueType, context));
            removeButton.text = "−";
            removeButton.tooltip = "Remove entry";
            removeButton.AddToClassList("dictionary-remove-button");
            removeButton.style.width = 20;
            removeButton.style.height = 20;
            removeButton.style.marginLeft = 4;

            entryContainer.Add(keyField);
            entryContainer.Add(arrowLabel);
            entryContainer.Add(valueField);
            entryContainer.Add(removeButton);

            return entryContainer;
        }

        private VisualElement CreateSimpleField(Type type, object value, Action<object> onChanged)
        {
            if (type == typeof(int))
            {
                var field = new IntegerField();
                field.value = value != null ? Convert.ToInt32(value) : 0;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(float))
            {
                var field = new FloatField();
                field.value = value != null ? Convert.ToSingle(value) : 0f;
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(string))
            {
                var field = new TextField();
                field.value = value as string ?? "";
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type == typeof(bool))
            {
                var field = new Toggle();
                field.value = value != null && Convert.ToBoolean(value);
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }
            if (type.IsEnum)
            {
                var enumValue = value as Enum ?? (Enum)Enum.GetValues(type).GetValue(0);
                var field = new EnumField(enumValue);
                field.RegisterValueChangedCallback(evt => onChanged(evt.newValue));
                return field;
            }

            // Fallback
            var readOnly = new TextField();
            readOnly.value = value?.ToString() ?? "";
            readOnly.isReadOnly = true;
            return readOnly;
        }

        #endregion

        #region Entry Operations

        private void AddEntry(VisualElement dictionaryContainer, Type keyType, Type valueType, FieldCreationContext context)
        {
            var userData = dictionaryContainer.userData as DictionaryUserData;
            if (userData == null) return;

            var entriesContainer = userData.EntriesContainer;

            var defaultKey = GetDefaultValue(keyType);
            var defaultValue = GetDefaultValue(valueType);

            var entryContainer = CreateEntryContainer(defaultKey, defaultValue, keyType, valueType, dictionaryContainer, context);
            entriesContainer.Add(entryContainer);

            UpdateDictionaryValue(dictionaryContainer, keyType, valueType, context);
            UpdateSizeLabel(dictionaryContainer, entriesContainer.childCount);
        }

        private void RemoveEntry(
            VisualElement entryToRemove,
            VisualElement dictionaryContainer,
            Type keyType,
            Type valueType,
            FieldCreationContext context)
        {
            var userData = dictionaryContainer.userData as DictionaryUserData;
            if (userData == null) return;

            var entriesContainer = userData.EntriesContainer;
            entriesContainer.Remove(entryToRemove);

            UpdateDictionaryValue(dictionaryContainer, keyType, valueType, context);
            UpdateSizeLabel(dictionaryContainer, entriesContainer.childCount);
        }

        private void UpdateEntryKey(
            VisualElement entryContainer,
            object newKey,
            VisualElement dictionaryContainer,
            Type keyType,
            Type valueType,
            FieldCreationContext context)
        {
            var currentPair = (KeyValuePair<object, object>)entryContainer.userData;
            entryContainer.userData = new KeyValuePair<object, object>(newKey, currentPair.Value);
            UpdateDictionaryValue(dictionaryContainer, keyType, valueType, context);
        }

        private void UpdateEntryValue(
            VisualElement entryContainer,
            object newValue,
            VisualElement dictionaryContainer,
            Type keyType,
            Type valueType,
            FieldCreationContext context)
        {
            var currentPair = (KeyValuePair<object, object>)entryContainer.userData;
            entryContainer.userData = new KeyValuePair<object, object>(currentPair.Key, newValue);
            UpdateDictionaryValue(dictionaryContainer, keyType, valueType, context);
        }

        private void UpdateDictionaryValue(
            VisualElement dictionaryContainer,
            Type keyType,
            Type valueType,
            FieldCreationContext context)
        {
            var userData = dictionaryContainer.userData as DictionaryUserData;
            if (userData == null) return;

            var entriesContainer = userData.EntriesContainer;
            var entries = entriesContainer.Query<VisualElement>(className: "dictionary-entry").ToList();

            var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
            var newDict = (IDictionary)Activator.CreateInstance(dictType);

            foreach (var entry in entries)
            {
                var pair = (KeyValuePair<object, object>)entry.userData;
                if (pair.Key != null)
                {
                    try
                    {
                        var convertedKey = Convert.ChangeType(pair.Key, keyType);
                        var convertedValue = pair.Value != null ? Convert.ChangeType(pair.Value, valueType) : GetDefaultValue(valueType);
                        newDict[convertedKey] = convertedValue;
                    }
                    catch
                    {
                        // Skip invalid entries
                    }
                }
            }

            context.OnValueChanged?.Invoke(newDict);
        }

        private void UpdateSizeLabel(VisualElement dictionaryContainer, int count)
        {
            var userData = dictionaryContainer.userData as DictionaryUserData;
            if (userData?.Foldout != null)
            {
                userData.Foldout.text = $"Size: {count}";
            }
        }

        #endregion

        #region Helpers

        private int GetDictionaryCount(object dictionary)
        {
            if (dictionary == null) return 0;
            var dict = dictionary as IDictionary;
            return dict?.Count ?? 0;
        }

        private List<KeyValuePair<object, object>> GetDictionaryEntries(object dictionary)
        {
            var result = new List<KeyValuePair<object, object>>();
            if (dictionary == null) return result;

            var dict = dictionary as IDictionary;
            if (dict == null) return result;

            foreach (DictionaryEntry entry in dict)
            {
                result.Add(new KeyValuePair<object, object>(entry.Key, entry.Value));
            }

            return result;
        }

        private object GetDefaultValue(Type type)
        {
            if (type == typeof(string)) return "";
            if (type.IsValueType) return Activator.CreateInstance(type);
            return null;
        }

        #endregion

        private class DictionaryUserData
        {
            public VisualElement EntriesContainer { get; set; }
            public Type KeyType { get; set; }
            public Type ValueType { get; set; }
            public Foldout Foldout { get; set; }
        }
    }
}
