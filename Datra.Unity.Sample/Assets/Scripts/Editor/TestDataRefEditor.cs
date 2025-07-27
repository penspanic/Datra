using UnityEngine;
using UnityEditor;
using Datra.SampleData.Models;
using Datra.Unity.Editor.Components;
using Datra.DataTypes;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Sample.Editor
{
    public class TestDataRefEditor : EditorWindow
    {
        private RefTestData testData;
        private DatraPropertyTracker tracker;
        private Vector2 scrollPosition;
        
        [MenuItem("Window/Datra/Test DataRef Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<TestDataRefEditor>();
            window.titleContent = new GUIContent("Test DataRef Editor");
            window.Show();
        }
        
        private void OnEnable()
        {
            // Create test data
            testData = new RefTestData(
                "test_ref_1",
                new StringDataRef<CharacterData> { Value = "hero_001" },
                new IntDataRef<ItemData> { Value = 1001 },
                new[]
                {
                    new IntDataRef<ItemData> { Value = 1001 },
                    new IntDataRef<ItemData> { Value = 1002 },
                    new IntDataRef<ItemData> { Value = 1003 }
                });
            
            // Create tracker
            tracker = new DatraPropertyTracker();
            tracker.StartTracking(testData, false);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.SetPadding(new UnityEngine.UIElements.StyleLength(10));
            
            // Load stylesheets
            var styleSheet = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.StyleSheet>(
                "Packages/com.penspanic.datra.unity/Editor/Styles/DatraPropertyField.uss");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            
            // Create title
            var title = new UnityEngine.UIElements.Label("RefTestData DataRef Test");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);
            
            // Create scroll view
            var scrollView = new UnityEngine.UIElements.ScrollView();
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);
            
            // Create fields for all properties
            var properties = testData.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanWrite)
                {
                    var field = new DatraPropertyField(testData, property, tracker);
                    field.OnValueChanged += (propName, value) => 
                    {
                        Debug.Log($"Property '{propName}' changed");
                    };
                    scrollView.Add(field);
                }
            }
            
            // Add save button
            var saveButton = new UnityEngine.UIElements.Button(() =>
            {
                Debug.Log("Current data state:");
                Debug.Log($"  Id: {testData.Id}");
                Debug.Log($"  CharacterRef: {testData.CharacterRef.Value}");
                Debug.Log($"  ItemRef: {testData.ItemRef.Value}");
                if (testData.ItemRefs != null)
                {
                    Debug.Log($"  ItemRefs count: {testData.ItemRefs.Length}");
                    for (int i = 0; i < testData.ItemRefs.Length; i++)
                    {
                        Debug.Log($"    [{i}]: {testData.ItemRefs[i].Value}");
                    }
                }
            });
            saveButton.text = "Log Current Data";
            saveButton.style.marginTop = 10;
            saveButton.style.height = 30;
            root.Add(saveButton);
        }
        
        private void OnDisable()
        {
            //tracker?.Cleanup();
        }
    }
}