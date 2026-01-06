using System.Linq;
using Datra.Attributes;
using Datra.Unity.Editor;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PropertyAttribute = UnityEngine.PropertyAttribute;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for LocaleKeyAttribute and LocaleKeyDrawer functionality.
    /// </summary>
    public class LocaleKeyDrawerTests
    {
        #region Attribute Tests

        [Test]
        public void LocaleKeyAttribute_InheritsFromPropertyAttribute()
        {
            // Assert
            Assert.IsTrue(typeof(PropertyAttribute).IsAssignableFrom(typeof(LocaleKeyAttribute)),
                "LocaleKeyAttribute should inherit from PropertyAttribute");
        }

        [Test]
        public void LocaleKeyAttribute_CanBeInstantiated()
        {
            // Act
            var attribute = new LocaleKeyAttribute();

            // Assert
            Assert.IsNotNull(attribute);
        }

        #endregion

        #region DatraEditorWindow Accessor Tests

        [Test]
        public void DatraEditorWindow_GetOpenedWindow_ReturnsNullWhenNoWindowOpen()
        {
            // Close any open windows first
            var existingWindows = Resources.FindObjectsOfTypeAll<DatraEditorWindow>();
            foreach (var w in existingWindows)
            {
                w.Close();
            }

            // Act
            var window = DatraEditorWindow.GetOpenedWindow();

            // Assert
            Assert.IsNull(window, "GetOpenedWindow should return null when no window is open");
        }

        [Test]
        public void DatraEditorWindow_GetOpenedWindow_ReturnsWindowWhenOpen()
        {
            // Skip in batch mode - no graphic device available
            if (Application.isBatchMode)
            {
                Assert.Inconclusive("Cannot test EditorWindow in batch mode - no graphic device");
                return;
            }

            // Arrange - Open a window
            DatraEditorWindow openedWindow = null;
            try
            {
                // Try to open window (may fail if no initializer is configured)
                var initializers = DatraBootstrapper.FindInitializers();
                if (initializers.Count > 0)
                {
                    openedWindow = DatraEditorWindow.ShowWindowForInitializer(initializers[0]);

                    // Act
                    var foundWindow = DatraEditorWindow.GetOpenedWindow();

                    // Assert
                    Assert.IsNotNull(foundWindow, "GetOpenedWindow should return the opened window");
                    Assert.AreEqual(openedWindow, foundWindow);
                }
                else
                {
                    Assert.Inconclusive("No Datra initializers found - skipping window test");
                }
            }
            finally
            {
                // Cleanup
                if (openedWindow != null)
                {
                    openedWindow.Close();
                }
            }
        }

        #endregion

        #region Test ScriptableObject for PropertyDrawer

        /// <summary>
        /// Test ScriptableObject with LocaleKey attribute for PropertyDrawer testing.
        /// </summary>
        private class TestLocaleKeyObject : ScriptableObject
        {
            [LocaleKey]
            public string localeKey;

            public string normalString;
        }

        [Test]
        public void LocaleKeyAttribute_CanBeAppliedToStringField()
        {
            // Arrange
            var type = typeof(TestLocaleKeyObject);
            var field = type.GetField("localeKey");

            // Act
            var attributes = field.GetCustomAttributes(typeof(LocaleKeyAttribute), false);

            // Assert
            Assert.AreEqual(1, attributes.Length, "Field should have LocaleKeyAttribute");
        }

        [Test]
        public void LocaleKeyAttribute_NotPresentOnNormalField()
        {
            // Arrange
            var type = typeof(TestLocaleKeyObject);
            var field = type.GetField("normalString");

            // Act
            var attributes = field.GetCustomAttributes(typeof(LocaleKeyAttribute), false);

            // Assert
            Assert.AreEqual(0, attributes.Length, "Normal field should not have LocaleKeyAttribute");
        }

        [Test]
        public void TestLocaleKeyObject_CanBeCreatedAndSerialized()
        {
            // Arrange
            var obj = ScriptableObject.CreateInstance<TestLocaleKeyObject>();

            try
            {
                // Act
                obj.localeKey = "Test_Key";
                var serializedObject = new SerializedObject(obj);
                var property = serializedObject.FindProperty("localeKey");

                // Assert
                Assert.IsNotNull(property, "Property should be serializable");
                Assert.AreEqual("Test_Key", property.stringValue);
            }
            finally
            {
                // Cleanup
                Object.DestroyImmediate(obj);
            }
        }

        #endregion

        #region PropertyDrawer Integration Tests

        [Test]
        public void LocaleKeyDrawer_PropertyDrawerIsRegistered()
        {
            // The PropertyDrawer should be automatically registered via [CustomPropertyDrawer]
            // We verify this by checking if the drawer type exists and has the correct attribute

            var drawerType = typeof(Datra.Unity.Editor.Drawers.LocaleKeyDrawer);
            var attributes = drawerType.GetCustomAttributes(typeof(CustomPropertyDrawer), false);

            Assert.AreEqual(1, attributes.Length, "LocaleKeyDrawer should have CustomPropertyDrawer attribute");

            var drawerAttribute = attributes[0] as CustomPropertyDrawer;
            Assert.IsNotNull(drawerAttribute);
        }

        #endregion
    }
}
