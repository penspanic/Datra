using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datra;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for column width persistence in DatraUserPreferences and table views.
    /// </summary>
    public class ColumnWidthPersistenceTests
    {
        private const string TestViewKey = "TestColumnWidths_UnitTest";

        [SetUp]
        public void SetUp()
        {
            // Clean up test preferences
            var fullKey = $"Datra_ColumnWidths_{TestViewKey}";
            if (EditorPrefs.HasKey(fullKey))
            {
                EditorPrefs.DeleteKey(fullKey);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up test preferences
            var fullKey = $"Datra_ColumnWidths_{TestViewKey}";
            if (EditorPrefs.HasKey(fullKey))
            {
                EditorPrefs.DeleteKey(fullKey);
            }
        }

        [Test]
        public void GetColumnWidths_NoSavedData_ReturnsEmptyDictionary()
        {
            var widths = DatraUserPreferences.GetColumnWidths(TestViewKey);

            Assert.IsNotNull(widths);
            Assert.AreEqual(0, widths.Count);
        }

        [Test]
        public void SetColumnWidths_ThenGet_ReturnsSameData()
        {
            // Arrange
            var widths = new Dictionary<string, float>
            {
                { "__Actions", 80f },
                { "__ID", 120f },
                { "Name", 200f },
                { "Health", 100f }
            };

            // Act
            DatraUserPreferences.SetColumnWidths(TestViewKey, widths);
            var loaded = DatraUserPreferences.GetColumnWidths(TestViewKey);

            // Assert
            Assert.AreEqual(widths.Count, loaded.Count);
            foreach (var kvp in widths)
            {
                Assert.IsTrue(loaded.ContainsKey(kvp.Key), $"Key '{kvp.Key}' should exist");
                Assert.AreEqual(kvp.Value, loaded[kvp.Key], 0.001f, $"Width for '{kvp.Key}' should match");
            }
        }

        [Test]
        public void SetColumnWidths_OverwritesPreviousData()
        {
            // Arrange
            var initial = new Dictionary<string, float> { { "A", 100f }, { "B", 200f } };
            var updated = new Dictionary<string, float> { { "A", 150f }, { "C", 300f } };

            // Act
            DatraUserPreferences.SetColumnWidths(TestViewKey, initial);
            DatraUserPreferences.SetColumnWidths(TestViewKey, updated);
            var loaded = DatraUserPreferences.GetColumnWidths(TestViewKey);

            // Assert
            Assert.AreEqual(2, loaded.Count);
            Assert.AreEqual(150f, loaded["A"], 0.001f);
            Assert.IsFalse(loaded.ContainsKey("B"), "Old key 'B' should not exist after overwrite");
            Assert.AreEqual(300f, loaded["C"], 0.001f);
        }

        [Test]
        public void SetColumnWidths_EmptyDictionary_CanBeLoaded()
        {
            // Arrange
            var empty = new Dictionary<string, float>();

            // Act
            DatraUserPreferences.SetColumnWidths(TestViewKey, empty);
            var loaded = DatraUserPreferences.GetColumnWidths(TestViewKey);

            // Assert
            Assert.IsNotNull(loaded);
            Assert.AreEqual(0, loaded.Count);
        }

        [Test]
        public void SetColumnWidths_NestedColumnNames_WorkCorrectly()
        {
            // Arrange - nested type column names with dot notation
            var widths = new Dictionary<string, float>
            {
                { "TestPooledPrefab.Path", 250f },
                { "TestPooledPrefab.InitialCount", 100f },
                { "TestPooledPrefab.MaxCount", 100f }
            };

            // Act
            DatraUserPreferences.SetColumnWidths(TestViewKey, widths);
            var loaded = DatraUserPreferences.GetColumnWidths(TestViewKey);

            // Assert
            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual(250f, loaded["TestPooledPrefab.Path"], 0.001f);
            Assert.AreEqual(100f, loaded["TestPooledPrefab.InitialCount"], 0.001f);
        }

        [Test]
        public void DifferentViewKeys_AreIndependent()
        {
            var key1 = TestViewKey + "_1";
            var key2 = TestViewKey + "_2";

            try
            {
                // Arrange
                var widths1 = new Dictionary<string, float> { { "A", 100f } };
                var widths2 = new Dictionary<string, float> { { "A", 200f }, { "B", 300f } };

                // Act
                DatraUserPreferences.SetColumnWidths(key1, widths1);
                DatraUserPreferences.SetColumnWidths(key2, widths2);
                var loaded1 = DatraUserPreferences.GetColumnWidths(key1);
                var loaded2 = DatraUserPreferences.GetColumnWidths(key2);

                // Assert
                Assert.AreEqual(1, loaded1.Count);
                Assert.AreEqual(100f, loaded1["A"], 0.001f);

                Assert.AreEqual(2, loaded2.Count);
                Assert.AreEqual(200f, loaded2["A"], 0.001f);
                Assert.AreEqual(300f, loaded2["B"], 0.001f);
            }
            finally
            {
                // Cleanup
                EditorPrefs.DeleteKey($"Datra_ColumnWidths_{key1}");
                EditorPrefs.DeleteKey($"Datra_ColumnWidths_{key2}");
            }
        }
    }
}
