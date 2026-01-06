using System.Linq;
using Datra.Attributes;
using Datra.Services;
using UnityEditor;
using UnityEngine;

namespace Datra.Unity.Editor.Drawers
{
    /// <summary>
    /// PropertyDrawer for LocaleKeyAttribute.
    /// Provides a key selector UI for string fields marked with [LocaleKey].
    /// </summary>
    [CustomPropertyDrawer(typeof(LocaleKeyAttribute))]
    public class LocaleKeyDrawer : PropertyDrawer
    {
        private const float ButtonWidth = 22f;
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.PropertyField(position, property, label);
                EditorGUI.HelpBox(position, "[LocaleKey] can only be used on string fields.", MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            // Calculate rects
            var fieldRect = new Rect(position.x, position.y, position.width - ButtonWidth - Spacing, position.height);
            var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

            // Draw the text field
            EditorGUI.PropertyField(fieldRect, property, label);

            // Draw the selector button
            if (GUI.Button(buttonRect, "⋯"))
            {
                ShowKeySelectionMenu(property);
            }

            EditorGUI.EndProperty();
        }

        private void ShowKeySelectionMenu(SerializedProperty property)
        {
            var context = GetLocalizationContext();

            if (context == null)
            {
                var result = EditorUtility.DisplayDialog(
                    "Datra Editor",
                    "LocalizationContext is not available.\nOpen Datra Editor to enable key selection.",
                    "Open Datra Editor",
                    "Cancel");

                if (result)
                {
                    DatraEditorWindow.ShowWindow();
                }
                return;
            }

            var keys = context.GetAllKeys()?.OrderBy(k => k).ToList();

            if (keys == null || keys.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Datra Editor",
                    "No localization keys found.\nAdd keys in the Datra Editor first.",
                    "OK");
                return;
            }

            // Build menu with categories
            var menu = new GenericMenu();
            var currentValue = property.stringValue;

            // Group keys by prefix (category)
            var groupedKeys = keys
                .GroupBy(k => GetKeyCategory(k))
                .OrderBy(g => g.Key);

            foreach (var group in groupedKeys)
            {
                var category = string.IsNullOrEmpty(group.Key) ? "General" : group.Key;

                foreach (var key in group.OrderBy(k => k))
                {
                    var isSelected = key == currentValue;
                    var menuPath = $"{category}/{key}";

                    menu.AddItem(new GUIContent(menuPath), isSelected, () =>
                    {
                        property.stringValue = key;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
            }

            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Open Datra Editor..."), false, () =>
            {
                DatraEditorWindow.ShowWindow();
            });

            menu.ShowAsContext();
        }

        private LocalizationContext GetLocalizationContext()
        {
            var window = DatraEditorWindow.GetOpenedWindow();
            return window?.LocalizationContext;
        }

        private string GetKeyCategory(string key)
        {
            // Extract category from key prefix (e.g., "Button_Start" -> "Button")
            var underscoreIndex = key.IndexOf('_');
            if (underscoreIndex > 0)
            {
                return key.Substring(0, underscoreIndex);
            }

            // Check for dot notation (e.g., "Character.Name" -> "Character")
            var dotIndex = key.IndexOf('.');
            if (dotIndex > 0)
            {
                return key.Substring(0, dotIndex);
            }

            return string.Empty;
        }
    }
}
