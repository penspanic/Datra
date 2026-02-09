#nullable disable
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Datra.Editor.Models;
using Datra.Unity.Editor.Components;

namespace Datra.Unity.Editor.Windows
{
    public class DatraPropertyEditorPopup : EditorWindow
    {
        private PropertyInfo property;
        private object target;
        
        private Action onValueChanged;
        private DatraPropertyField propertyField;
        
        public static void ShowEditor(PropertyInfo property, object target,  Action onValueChanged)
        {
            var window = CreateInstance<DatraPropertyEditorPopup>();
            window.titleContent = new GUIContent($"Edit {ObjectNames.NicifyVariableName(property.Name)}");
            window.property = property;
            window.target = target;
            
            window.onValueChanged = onValueChanged;
            
            // Set window size based on property type
            var width = 400f;
            var height = 300f;
            
            if (property.PropertyType.IsArray)
            {
                height = 500f;
            }
            
            window.minSize = new Vector2(width, 200f);
            window.maxSize = new Vector2(800f, 800f);
            
            var position = new Rect(
                (Screen.currentResolution.width - width) / 2,
                (Screen.currentResolution.height - height) / 2,
                width,
                height
            );
            window.position = position;
            
            window.ShowUtility();
        }
        
        private void CreateGUI()
        {
            if (property == null || target == null) return;

            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;

            // Create header
            var header = new Label(ObjectNames.NicifyVariableName(property.Name));
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginBottom = 10;
            header.style.flexShrink = 0;
            root.Add(header);

            // Create scroll view for content
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            root.Add(scrollView);

            // Create property field in Form mode with popup flag (no foldout wrapper)
            propertyField = new DatraPropertyField(target, property, FieldLayoutMode.Form, null, isPopupEditor: true);
            propertyField.OnValueChanged += (propName, newValue) => {
                onValueChanged?.Invoke();
            };
            scrollView.Add(propertyField);

            // Create footer with buttons (fixed at bottom)
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.marginTop = 10;
            footer.style.paddingTop = 10;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f);
            footer.style.flexShrink = 0;

            var closeButton = new Button(() => Close());
            closeButton.text = "Close";
            closeButton.style.width = 80;
            closeButton.style.height = 24;
            footer.Add(closeButton);

            root.Add(footer);
        }
    }
}