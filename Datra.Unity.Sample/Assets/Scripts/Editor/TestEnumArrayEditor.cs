using UnityEngine;
using UnityEditor;
using Datra.SampleData.Models;
using Datra.Unity.Editor.Components;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Sample.Editor
{
    public class TestEnumArrayEditor : EditorWindow
    {
        private CharacterData testData;
        private DatraPropertyTracker tracker;
        
        [MenuItem("Window/Datra/Test Enum Array Editor")]
        public static void ShowWindow()
        {
            var window = GetWindow<TestEnumArrayEditor>();
            window.titleContent = new GUIContent("Test Enum Array Editor");
            window.Show();
        }
        
        private void OnEnable()
        {
            // Create test data
            testData = new CharacterData("Test_ID", "Test Character", 10, 10, 100, 50,
                20,
                10,
                "TestClassName",
                CharacterGrade.Epic,
                new StatType[]
                {
                    StatType.Attack,
                    StatType.Defense,
                    StatType.Speed,
                    StatType.CriticalRate
                }, new[] { 100, 200, }
            );
            
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
            var title = new UnityEngine.UIElements.Label("CharacterData Enum Array Test");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);
            
            // Create scroll view
            var scrollView = new UnityEngine.UIElements.ScrollView();
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);
            
            // Create fields for all properties, focusing on the Stats array
            var properties = testData.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var property in properties)
            {
                if (property.CanWrite)
                {
                    var field = new DatraPropertyField(testData, property, tracker);
                    field.OnValueChanged += (propName, value) => 
                    {
                        Debug.Log($"Property '{propName}' changed");
                        if (propName == "Stats" && value is StatType[] stats)
                        {
                            Debug.Log($"  Stats array now has {stats.Length} elements:");
                            for (int i = 0; i < stats.Length; i++)
                            {
                                Debug.Log($"    [{i}]: {stats[i]}");
                            }
                        }
                    };
                    scrollView.Add(field);
                }
            }
            
            // Add save button
            var saveButton = new UnityEngine.UIElements.Button(() =>
            {
                Debug.Log("Current data state:");
                Debug.Log($"  Id: {testData.Id}");
                Debug.Log($"  Name: {testData.Name}");
                Debug.Log($"  Stats: [{string.Join(", ", testData.Stats ?? new StatType[0])}]");
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