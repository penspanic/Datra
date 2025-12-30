using System.Collections;
using System.Linq;
using Datra.Interfaces;
using Datra.Unity.Editor;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests.Integration
{
    /// <summary>
    /// Tests for Datra Editor menu access and window initialization.
    /// </summary>
    public class DatraEditorMenuTests : EditorWindowTestBase
    {
        [Test]
        public void FindInitializers_ReturnsAtLeastOne()
        {
            // Act
            var initializers = DatraBootstrapper.FindInitializers(forceRefresh: true);

            // Assert
            Assert.IsNotNull(initializers, "Initializers list should not be null");
            Assert.Greater(initializers.Count, 0, "Should find at least one DataContext initializer");

            // Log found initializers for debugging
            foreach (var init in initializers)
            {
                UnityEngine.Debug.Log($"Found initializer: {init.DisplayName}");
            }
        }

        [Test]
        public void FindInitializers_ContainsValidInitializer()
        {
            // Act
            var initializers = DatraBootstrapper.FindInitializers(forceRefresh: true);

            // Assert - Check that at least one initializer exists and has a valid display name
            Assert.Greater(initializers.Count, 0, "Should find at least one initializer");

            var firstInit = initializers[0];
            Assert.IsFalse(string.IsNullOrEmpty(firstInit.DisplayName),
                "Initializer should have a display name");

            UnityEngine.Debug.Log($"Found initializers: {string.Join(", ", initializers.Select(i => i.DisplayName))}");
        }

        [UnityTest]
        public IEnumerator ShowWindow_OpensSuccessfully()
        {
            // Act
            yield return OpenWindowAndWaitForLoad();

            // Assert
            Assert.IsNotNull(window, "Window should be opened");
            Assert.IsTrue(window.hasFocus || window != null, "Window should exist");
        }

        [UnityTest]
        public IEnumerator ShowWindow_InitializesViewModel()
        {
            // Act
            yield return OpenWindowAndWaitForLoad();

            // Assert
            Assert.IsNotNull(window.ViewModel, "ViewModel should be initialized");
            Assert.IsTrue(window.ViewModel.IsInitialized, "ViewModel should be marked as initialized");
        }

        [UnityTest]
        public IEnumerator ShowWindow_InitializesDataContext()
        {
            // Act
            yield return OpenWindowAndWaitForLoad();

            // Assert
            Assert.IsNotNull(window.DataContext, "DataContext should be initialized");
        }

        [UnityTest]
        public IEnumerator ShowWindow_LoadsRepositories()
        {
            // Act
            yield return OpenWindowAndWaitForLoad();

            // Assert
            Assert.IsNotNull(window.Repositories, "Repositories should be initialized");
            Assert.Greater(window.Repositories.Count, 0, "Should have at least one repository");

            // Log repositories for debugging
            foreach (var repo in window.Repositories)
            {
                UnityEngine.Debug.Log($"Loaded repository for type: {repo.Key.Name}");
            }
        }

        [UnityTest]
        public IEnumerator ShowWindow_LoadsDataTypeInfos()
        {
            // Act
            yield return OpenWindowAndWaitForLoad();

            // Assert
            var dataTypes = window.ViewModel.DataTypes;
            Assert.IsNotNull(dataTypes, "DataTypes should not be null");
            Assert.Greater(dataTypes.Count, 0, "Should have at least one data type");

            // Log data types for debugging
            foreach (var dt in dataTypes)
            {
                var kind = dt.IsSingleData ? "Single" : "KeyValue/Asset";
                UnityEngine.Debug.Log($"Data type: {dt.DataType.Name} ({kind})");
            }
        }

        [UnityTest]
        public IEnumerator MultipleWindows_CanOpenDifferentInitializers()
        {
            // Skip if no graphics available
            SkipIfNoGraphics();

            // Arrange
            var initializers = DatraBootstrapper.FindInitializers(forceRefresh: true);

            if (initializers.Count < 2)
            {
                UnityEngine.Debug.Log("Skipping test - only one initializer available");
                yield break;
            }

            // Act - Open first window
            var window1 = DatraEditorWindow.ShowWindowForInitializer(initializers[0]);
            yield return WaitForCondition(
                () => window1?.ViewModel?.IsInitialized == true,
                DefaultTimeout,
                "First window failed to initialize");

            // Act - Open second window
            var window2 = DatraEditorWindow.ShowWindowForInitializer(initializers[1]);
            yield return WaitForCondition(
                () => window2?.ViewModel?.IsInitialized == true,
                DefaultTimeout,
                "Second window failed to initialize");

            // Assert
            Assert.IsNotNull(window1, "First window should be opened");
            Assert.IsNotNull(window2, "Second window should be opened");
            Assert.AreNotSame(window1, window2, "Should be different window instances");

            // Cleanup
            window1?.Close();
            window2?.Close();
        }
    }
}
