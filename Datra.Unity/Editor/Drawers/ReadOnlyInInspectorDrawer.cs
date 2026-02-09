#nullable disable
using Datra.Attributes;
using UnityEditor;
using UnityEngine;

namespace Datra.Unity.Editor.Drawers
{
    [CustomPropertyDrawer(typeof(ReadOnlyInInspector))]
    public class ReadOnlyInInspectorDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var prev = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = prev;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }

}