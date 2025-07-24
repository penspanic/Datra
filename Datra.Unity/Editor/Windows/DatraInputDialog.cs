using UnityEngine;
using UnityEditor;

namespace Datra.Unity.Editor.Windows
{
    public class DatraInputDialog : EditorWindow
    {
        private string inputValue = "";
        private string message = "";
        private System.Action<string> onConfirm;
        private bool shouldClose = false;
        
        public static void Show(string title, string message, string defaultValue, System.Action<string> onConfirm)
        {
            var window = GetWindow<DatraInputDialog>(true, title, true);
            window.message = message;
            window.inputValue = defaultValue;
            window.onConfirm = onConfirm;
            window.minSize = new Vector2(300, 100);
            window.maxSize = new Vector2(400, 100);
            
            // Center the window
            var position = window.position;
            position.center = new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height).center;
            window.position = position;
            
            window.ShowModal();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField(message, EditorStyles.wordWrappedLabel);
            
            EditorGUILayout.Space(5);
            
            GUI.SetNextControlName("InputField");
            inputValue = EditorGUILayout.TextField(inputValue);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Cancel", GUILayout.Width(80)))
            {
                shouldClose = true;
            }
            
            GUI.enabled = !string.IsNullOrWhiteSpace(inputValue);
            if (GUILayout.Button("OK", GUILayout.Width(80)) || (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return))
            {
                onConfirm?.Invoke(inputValue);
                shouldClose = true;
            }
            GUI.enabled = true;
            
            EditorGUILayout.EndHorizontal();
            
            // Focus the input field
            if (GUI.GetNameOfFocusedControl() != "InputField")
            {
                EditorGUI.FocusTextInControl("InputField");
            }
            
            if (shouldClose)
            {
                Close();
            }
        }
    }
}