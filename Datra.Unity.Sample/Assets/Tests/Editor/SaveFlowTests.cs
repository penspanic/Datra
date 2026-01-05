using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Unity.Editor.Controllers;
using Datra.Unity.Editor.Panels;
using Datra.Unity.Editor.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for Option B save flow architecture.
    /// View fires OnSaveRequested event → Window performs save → View.OnSaveCompleted callback
    /// </summary>
    public class SaveFlowTests
    {
        #region DatraDataView Tests

        [Test]
        public void DatraDataView_SaveChanges_FiresOnSaveRequestedEvent()
        {
            // Arrange
            var view = new TestableDataView();
            Type receivedType = null;
            IDataRepository receivedRepo = null;

            view.OnSaveRequested += (type, repo) =>
            {
                receivedType = type;
                receivedRepo = repo;
            };

            var mockRepo = new MockRepository();
            view.SetTestData(typeof(string), mockRepo);

            // Act
            view.TriggerSave();

            // Assert
            Assert.AreEqual(typeof(string), receivedType);
            Assert.AreEqual(mockRepo, receivedRepo);
        }

        [Test]
        public void DatraDataView_SaveChanges_DoesNotSaveDirectly()
        {
            // Arrange
            var view = new TestableDataView();
            var mockDataSource = new MockEditableDataSource();
            view.SetTestDataSource(mockDataSource);

            // Act
            view.TriggerSave();

            // Assert - SaveAsync should NOT be called on dataSource
            Assert.IsFalse(mockDataSource.SaveWasCalled, "View should not call dataSource.SaveAsync() directly");
        }

        [Test]
        public void DatraDataView_OnSaveCompleted_True_DoesNotThrow()
        {
            // Arrange
            var view = new TestableDataView();

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => view.OnSaveCompleted(true));
        }

        [Test]
        public void DatraDataView_OnSaveCompleted_False_DoesNotThrow()
        {
            // Arrange
            var view = new TestableDataView();

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() => view.OnSaveCompleted(false));
        }

        [Test]
        public void DatraDataView_SaveChanges_WhenReadOnly_DoesNotFireEvent()
        {
            // Arrange
            var view = new TestableDataView();
            view.IsReadOnly = true;
            bool eventFired = false;

            view.OnSaveRequested += (type, repo) => eventFired = true;

            // Act
            view.TriggerSave();

            // Assert
            Assert.IsFalse(eventFired, "OnSaveRequested should not fire when view is read-only");
        }

        #endregion

        #region DatraViewModeController Tests

        [Test]
        public void DatraViewModeController_NotifySaveCompleted_DoesNotThrow()
        {
            // Arrange
            var contentContainer = new VisualElement();
            var headerContainer = new VisualElement();
            var controller = new DatraViewModeController(contentContainer, headerContainer);

            // Act & Assert - method exists and doesn't throw
            Assert.DoesNotThrow(() => controller.NotifySaveCompleted(true));
            Assert.DoesNotThrow(() => controller.NotifySaveCompleted(false));
        }

        #endregion

        #region Panel Tests

        [Test]
        public void DataInspectorPanel_NotifySaveCompleted_DoesNotThrow()
        {
            var panel = new DataInspectorPanel();

            // Should not throw
            Assert.DoesNotThrow(() => panel.NotifySaveCompleted(true));
            Assert.DoesNotThrow(() => panel.NotifySaveCompleted(false));
        }

        [Test]
        public void LocalizationInspectorPanel_NotifySaveCompleted_DoesNotThrow()
        {
            var panel = new LocalizationInspectorPanel();

            // Should not throw (localizationView is null, so it's a no-op)
            Assert.DoesNotThrow(() => panel.NotifySaveCompleted(true));
            Assert.DoesNotThrow(() => panel.NotifySaveCompleted(false));
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Testable subclass of DatraTableView (concrete implementation)
        /// </summary>
        private class TestableDataView : DatraTableView
        {
            public TestableDataView() : base()
            {
            }

            public void TriggerSave()
            {
                SaveChanges();
            }

            public void SetTestData(Type type, IDataRepository repo)
            {
                // Use reflection to set protected fields
                var dataTypeField = typeof(DatraDataView).GetField("dataType",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                dataTypeField?.SetValue(this, type);

                var repoField = typeof(DatraDataView).GetField("repository",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                repoField?.SetValue(this, repo);
            }

            public void SetTestDataSource(MockEditableDataSource dataSource)
            {
                var field = typeof(DatraDataView).GetField("dataSource",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                field?.SetValue(this, dataSource);
            }
        }

        /// <summary>
        /// Mock repository for testing
        /// </summary>
        private class MockRepository : IDataRepository
        {
            public bool SaveWasCalled { get; private set; }
            public bool LoadWasCalled { get; private set; }

            public Task LoadAsync()
            {
                LoadWasCalled = true;
                return Task.CompletedTask;
            }

            public Task SaveAsync()
            {
                SaveWasCalled = true;
                return Task.CompletedTask;
            }

            public IEnumerable<object> EnumerateItems()
            {
                return Array.Empty<object>();
            }

            public int ItemCount => 0;

            public string GetLoadedFilePath() => string.Empty;
        }

        /// <summary>
        /// Mock editable data source for testing
        /// </summary>
        private class MockEditableDataSource : IEditableDataSource
        {
            public bool SaveWasCalled { get; private set; }
            public bool HasModifications => false;
            public int Count => 0;

            public event Action<bool> OnModifiedStateChanged;

            public IEnumerable<object> EnumerateItems() => Array.Empty<object>();
            public ItemState GetItemState(object key) => ItemState.Unchanged;
            public bool IsPropertyModified(object key, string propertyName) => false;
            public IEnumerable<string> GetModifiedProperties(object key) => Array.Empty<string>();
            public object GetPropertyBaselineValue(object key, string propertyName) => null;

            public Task SaveAsync()
            {
                SaveWasCalled = true;
                return Task.CompletedTask;
            }

            public void Revert()
            {
            }
        }

        #endregion
    }
}
