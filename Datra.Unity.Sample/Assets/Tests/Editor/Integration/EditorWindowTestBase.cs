using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Interfaces;
using Datra.Unity.Editor;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Tests.Integration
{
    /// <summary>
    /// Base class for Datra Editor Window UI integration tests.
    /// Provides utilities for opening windows, querying UI elements, and simulating interactions.
    /// NOTE: These tests require a graphics device and cannot run in batch mode.
    /// </summary>
    public abstract class EditorWindowTestBase
    {
        protected DatraEditorWindow window;
        protected const float DefaultTimeout = 30f;
        protected bool isGraphicsAvailable = true;

        [SetUp]
        public void SetUp()
        {
            // Check if we're running in batch mode (no graphics)
            if (Application.isBatchMode)
            {
                isGraphicsAvailable = false;
                Debug.LogWarning("Skipping UI tests - running in batch mode without graphics device");
            }
        }

        [TearDown]
        public void TearDown()
        {
            CloseWindow();

            // Clear static caches to prevent test isolation issues
            DatraBootstrapper.ClearCurrentDataContext();

            // Wait for any pending EditorApplication.delayCall to complete
            // This prevents InitializeData from interfering with the next test
            EditorApplication.delayCall += () => { };
            System.Threading.Thread.Sleep(50);
        }

        /// <summary>
        /// Skip test if graphics are not available
        /// </summary>
        protected void SkipIfNoGraphics()
        {
            if (!isGraphicsAvailable)
            {
                Assert.Ignore("Test skipped - requires graphics device (not available in batch mode)");
            }
        }

        #region Window Management

        /// <summary>
        /// Opens the DatraEditorWindow and waits for initialization.
        /// </summary>
        protected IEnumerator OpenWindowAndWaitForLoad(float timeout = DefaultTimeout)
        {
            // Skip if no graphics available
            SkipIfNoGraphics();

            // Find the first available initializer
            var initializers = DatraBootstrapper.FindInitializers(forceRefresh: true);
            if (initializers.Count == 0)
            {
                Assert.Fail("No DataContext initializers found");
                yield break;
            }

            // Open window for the first initializer
            window = DatraEditorWindow.ShowWindowForInitializer(initializers[0]);
            Assert.IsNotNull(window, "Failed to open DatraEditorWindow");

            // Wait for window to initialize
            yield return WaitForCondition(
                () => window.ViewModel?.IsInitialized == true,
                timeout,
                "Window failed to initialize within timeout");
        }

        /// <summary>
        /// Opens the DatraEditorWindow for a specific initializer by name.
        /// </summary>
        protected IEnumerator OpenWindowForInitializer(string displayName, float timeout = DefaultTimeout)
        {
            // Skip if no graphics available
            SkipIfNoGraphics();

            var initializers = DatraBootstrapper.FindInitializers(forceRefresh: true);
            var initializer = initializers.FirstOrDefault(i => i.DisplayName == displayName);

            if (initializer == null)
            {
                Assert.Fail($"Initializer '{displayName}' not found");
                yield break;
            }

            window = DatraEditorWindow.ShowWindowForInitializer(initializer);
            Assert.IsNotNull(window, $"Failed to open DatraEditorWindow for '{displayName}'");

            yield return WaitForCondition(
                () => window.ViewModel?.IsInitialized == true,
                timeout,
                $"Window for '{displayName}' failed to initialize within timeout");
        }

        /// <summary>
        /// Closes the test window if open.
        /// </summary>
        protected void CloseWindow()
        {
            if (window != null)
            {
                window.Close();
                window = null;
            }

            // Also close any other DatraEditorWindow instances that might be lingering
            var allWindows = Resources.FindObjectsOfTypeAll<DatraEditorWindow>();
            foreach (var w in allWindows)
            {
                if (w != null)
                {
                    w.Close();
                }
            }
        }

        #endregion

        #region UI Queries

        /// <summary>
        /// Query a UI element by type and optional name.
        /// </summary>
        protected T QueryUI<T>(string name = null, string className = null) where T : VisualElement
        {
            if (window?.rootVisualElement == null) return null;

            if (!string.IsNullOrEmpty(name))
                return window.rootVisualElement.Q<T>(name);
            if (!string.IsNullOrEmpty(className))
                return window.rootVisualElement.Q<T>(className: className);
            return window.rootVisualElement.Q<T>();
        }

        /// <summary>
        /// Query all UI elements of a type with optional class filter.
        /// </summary>
        protected List<T> QueryAllUI<T>(string className = null) where T : VisualElement
        {
            if (window?.rootVisualElement == null) return new List<T>();

            if (!string.IsNullOrEmpty(className))
                return window.rootVisualElement.Query<T>(className: className).ToList();
            return window.rootVisualElement.Query<T>().ToList();
        }

        /// <summary>
        /// Find a UI element containing specific text.
        /// </summary>
        protected VisualElement FindElementWithText(string text, string className = null)
        {
            if (window?.rootVisualElement == null) return null;

            var elements = string.IsNullOrEmpty(className)
                ? window.rootVisualElement.Query<VisualElement>().ToList()
                : window.rootVisualElement.Query<VisualElement>(className: className).ToList();

            foreach (var element in elements)
            {
                if (element is Label label && label.text?.Contains(text) == true)
                    return element;
                if (element is Button button && button.text?.Contains(text) == true)
                    return element;
                if (element is TextElement textElement && textElement.text?.Contains(text) == true)
                    return element;
            }

            return null;
        }

        /// <summary>
        /// Find a Label containing specific text.
        /// </summary>
        protected Label FindLabelWithText(string text)
        {
            if (window?.rootVisualElement == null) return null;

            return window.rootVisualElement.Query<Label>()
                .Where(l => l.text?.Contains(text) == true)
                .First();
        }

        /// <summary>
        /// Check if any UI element contains the specified text.
        /// </summary>
        protected bool HasElementWithText(string text)
        {
            return FindElementWithText(text) != null;
        }

        #endregion

        #region Interactions

        /// <summary>
        /// Simulates a click on a VisualElement.
        /// </summary>
        protected void SimulateClick(VisualElement element)
        {
            if (element == null) return;

            // For buttons, invoke the clickable directly
            if (element is Button button && button.clickable != null)
            {
                // Use reflection to invoke the clicked action
                var clickedField = typeof(Clickable).GetField("clicked", BindingFlags.NonPublic | BindingFlags.Instance);
                var clicked = clickedField?.GetValue(button.clickable) as Action;
                clicked?.Invoke();
                return;
            }

            // Send pointer events for other elements
            using (var downEvent = PointerDownEvent.GetPooled())
            {
                downEvent.target = element;
                element.SendEvent(downEvent);
            }

            using (var upEvent = PointerUpEvent.GetPooled())
            {
                upEvent.target = element;
                element.SendEvent(upEvent);
            }

            using (var clickEvent = ClickEvent.GetPooled())
            {
                clickEvent.target = element;
                element.SendEvent(clickEvent);
            }
        }

        /// <summary>
        /// Simulates clicking on a navigation item by data type name.
        /// </summary>
        protected IEnumerator SelectDataTypeByName(string typeName, float waitTime = 0.5f)
        {
            var label = FindLabelWithText(typeName);
            Assert.IsNotNull(label, $"Data type '{typeName}' not found in navigation");

            // Find clickable parent (usually the row container)
            var clickableParent = FindClickableParent(label);
            SimulateClick(clickableParent ?? label);

            // Wait for UI to update
            yield return WaitForSeconds(waitTime);
        }

        /// <summary>
        /// Find the nearest clickable parent element.
        /// </summary>
        protected VisualElement FindClickableParent(VisualElement element)
        {
            var current = element.parent;
            while (current != null)
            {
                if (current is Button)
                    return current;
                if (current.pickingMode == PickingMode.Position)
                    return current;
                current = current.parent;
            }
            return element;
        }

        #endregion

        #region Wait Utilities

        /// <summary>
        /// Wait for a condition to be true.
        /// </summary>
        protected IEnumerator WaitForCondition(Func<bool> condition, float timeout, string failMessage)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            if (!condition())
            {
                Assert.Fail(failMessage);
            }
        }

        /// <summary>
        /// Wait for UI element to appear.
        /// </summary>
        protected IEnumerator WaitForElement<T>(string name = null, float timeout = DefaultTimeout) where T : VisualElement
        {
            yield return WaitForCondition(
                () => QueryUI<T>(name) != null,
                timeout,
                $"Element {typeof(T).Name}" + (name != null ? $" '{name}'" : "") + " did not appear");
        }

        /// <summary>
        /// Wait for text to appear in UI.
        /// </summary>
        protected IEnumerator WaitForText(string text, float timeout = DefaultTimeout)
        {
            yield return WaitForCondition(
                () => HasElementWithText(text),
                timeout,
                $"Text '{text}' did not appear in UI");
        }

        /// <summary>
        /// Simple wait for seconds.
        /// </summary>
        protected IEnumerator WaitForSeconds(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
        }

        #endregion

        #region Assertions

        /// <summary>
        /// Assert that data type appears in navigation panel.
        /// </summary>
        protected void AssertDataTypeInNavigation(string typeName)
        {
            Assert.IsTrue(HasElementWithText(typeName),
                $"Data type '{typeName}' should appear in navigation panel");
        }

        /// <summary>
        /// Assert that content area has visible data items.
        /// </summary>
        protected void AssertHasDataItems(string className = "data-item")
        {
            var items = QueryAllUI<VisualElement>(className);
            Assert.Greater(items.Count, 0, $"Should have at least one data item with class '{className}'");
        }

        /// <summary>
        /// Assert window is showing expected view mode.
        /// </summary>
        protected void AssertViewModeActive(string viewClassName)
        {
            var view = QueryUI<VisualElement>(className: viewClassName);
            Assert.IsNotNull(view, $"View with class '{viewClassName}' should be active");
        }

        /// <summary>
        /// Get count of data items in the current view.
        /// </summary>
        protected int GetDataItemCount(string className = "table-item")
        {
            return QueryAllUI<VisualElement>(className).Count;
        }

        #endregion
    }
}
