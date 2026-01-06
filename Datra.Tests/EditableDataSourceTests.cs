#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// Tests for the editable data source architecture.
    /// Verifies event notification patterns and state management.
    /// </summary>
    public class EditableDataSourceTests
    {
        #region Test Helpers

        private class TestData : ITableData<string>
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public int Value { get; set; }
        }

        private class MockKeyValueRepository : IKeyValueDataRepository<string, TestData>
        {
            private readonly Dictionary<string, TestData> _data = new();
            public bool SaveWasCalled { get; private set; }
            public int SaveCallCount { get; private set; }

            public void AddInitialData(string id, string name, int value)
            {
                _data[id] = new TestData { Id = id, Name = name, Value = value };
            }

            // IKeyValueDataRepository implementation
            public IReadOnlyDictionary<string, TestData> GetAll() => _data;
            public TestData GetById(string id) => _data[id];
            public TestData? TryGetById(string id) => _data.TryGetValue(id, out var v) ? v : null;
            public bool Contains(string id) => _data.ContainsKey(id);
            public int Count => _data.Count;
            public void Add(TestData item) => _data[item.Id] = item;
            public bool Remove(string key) => _data.Remove(key);
            public bool UpdateKey(string oldKey, string newKey)
            {
                if (!_data.TryGetValue(oldKey, out var item)) return false;
                _data.Remove(oldKey);
                item.Id = newKey;
                _data[newKey] = item;
                return true;
            }
            public void Clear() => _data.Clear();

            // IDataRepository implementation
            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync()
            {
                SaveWasCalled = true;
                SaveCallCount++;
                return Task.CompletedTask;
            }
            public string GetLoadedFilePath() => "mock://test.csv";
            public IEnumerable<object> EnumerateItems() => _data.Values.Cast<object>();
            public int ItemCount => _data.Count;

            // IDataRepository<string, TestData> implementation
            public IEnumerable<TestData> Find(Func<TestData, bool> predicate) => _data.Values.Where(predicate);

            // IReadOnlyDictionary implementation
            public bool ContainsKey(string key) => _data.ContainsKey(key);
            public bool TryGetValue(string key, out TestData value) => _data.TryGetValue(key, out value!);
            public TestData this[string key] => _data[key];
            public IEnumerable<string> Keys => _data.Keys;
            public IEnumerable<TestData> Values => _data.Values;
            int IReadOnlyCollection<KeyValuePair<string, TestData>>.Count => _data.Count;
            public IEnumerator<KeyValuePair<string, TestData>> GetEnumerator() => _data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        #endregion

        #region ExecuteWithNotification Pattern Tests

        [Fact]
        public void Add_FiresOnModifiedStateChanged_WhenFirstModification()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            bool eventFired = false;
            bool? eventValue = null;

            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
                eventValue = hasModifications;
            };

            // Act
            dataSource.Add("new_item", new TestData { Id = "new_item", Name = "New", Value = 100 });

            // Assert
            Assert.True(eventFired, "OnModifiedStateChanged should be fired");
            Assert.True(eventValue, "Event should indicate hasModifications = true");
            Assert.True(dataSource.HasModifications);
        }

        [Fact]
        public void Delete_FiresOnModifiedStateChanged_WhenDeletingExistingItem()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            bool eventFired = false;

            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
            };

            // Act
            dataSource.Delete("item1");

            // Assert
            Assert.True(eventFired, "OnModifiedStateChanged should be fired");
            Assert.True(dataSource.HasModifications);
            Assert.Equal(ItemState.Deleted, dataSource.GetItemState("item1"));
        }

        [Fact]
        public void Delete_ThenAdd_ResultsInNoModifications()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            var eventValues = new List<bool>();

            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventValues.Add(hasModifications);
            };

            // Act
            dataSource.Add("item1", new TestData { Id = "item1", Name = "Item 1", Value = 10 });
            dataSource.Delete("item1");

            // Assert
            Assert.Equal(2, eventValues.Count);
            Assert.True(eventValues[0], "First event: has modifications");
            Assert.False(eventValues[1], "Second event: no modifications");
            Assert.False(dataSource.HasModifications);
        }

        [Fact]
        public void MarkModified_FiresEvent_OnlyOnceForSameItem()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            int eventCount = 0;

            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventCount++;
            };

            // Act
            dataSource.MarkModified("item1");
            dataSource.MarkModified("item1"); // Second call for same item
            dataSource.MarkModified("item1"); // Third call

            // Assert
            Assert.Equal(1, eventCount);
        }

        #endregion

        #region Revert Tests

        [Fact]
        public void Revert_ClearsAllModifications()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.Add("item2", new TestData { Id = "item2", Name = "Item 2", Value = 20 });
            dataSource.Delete("item1");
            Assert.True(dataSource.HasModifications);

            bool eventFired = false;
            bool? eventValue = null;
            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
                eventValue = hasModifications;
            };

            // Act
            dataSource.Revert();

            // Assert
            Assert.True(eventFired, "OnModifiedStateChanged should be fired");
            Assert.False(eventValue, "Event should indicate hasModifications = false");
            Assert.False(dataSource.HasModifications);
            Assert.True(dataSource.ContainsKey("item1"), "Deleted item should be restored");
            Assert.False(dataSource.ContainsKey("item2"), "Added item should be removed");
        }

        [Fact]
        public void Revert_WhenNoModifications_DoesNotFireEvent()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            bool eventFired = false;

            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
            };

            // Act
            dataSource.Revert(); // No modifications to revert

            // Assert
            Assert.False(eventFired, "Event should not fire when nothing changed");
        }

        #endregion

        #region Save Tests

        [Fact]
        public async Task SaveAsync_AppliesChangesToRepository()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.Add("item2", new TestData { Id = "item2", Name = "Item 2", Value = 20 });
            dataSource.Delete("item1");

            // Act
            await dataSource.SaveAsync();

            // Assert
            Assert.True(repo.SaveWasCalled, "Repository.SaveAsync should be called");
            Assert.False(dataSource.HasModifications, "Should have no modifications after save");
        }

        [Fact]
        public async Task SaveAsync_FiresOnModifiedStateChanged_WithFalse()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            dataSource.Add("item1", new TestData { Id = "item1", Name = "Item 1", Value = 10 });

            var eventValues = new List<bool>();
            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventValues.Add(hasModifications);
            };

            // Act
            await dataSource.SaveAsync();

            // Assert
            Assert.Single(eventValues);
            Assert.False(eventValues[0], "Event should indicate no modifications after save");
        }

        [Fact]
        public async Task SaveAsync_RefreshesBaseline()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);
            dataSource.Add("item1", new TestData { Id = "item1", Name = "Original", Value = 10 });
            await dataSource.SaveAsync();

            // Act - modify the saved item
            var workingCopy = dataSource.GetWorkingCopy("item1");
            workingCopy.Name = "Modified";
            dataSource.MarkModified("item1");

            // Verify baseline reflects saved state
            var baseline = dataSource.GetBaselineValue("item1");

            // Assert
            Assert.NotNull(baseline);
            Assert.Equal("Original", baseline!.Name);
        }

        #endregion

        #region TrackPropertyChange Tests

        [Fact]
        public void TrackPropertyChange_DetectsModification()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            // Act
            dataSource.TrackPropertyChange("item1", "Name", "Modified Name", out bool isPropertyModified);

            // Assert
            Assert.True(isPropertyModified);
            Assert.True(dataSource.IsPropertyModified("item1", "Name"));
            Assert.True(dataSource.HasModifications);
        }

        [Fact]
        public void TrackPropertyChange_RevertingToBaseline_ClearsModification()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            // First, modify the property
            dataSource.TrackPropertyChange("item1", "Name", "Modified", out _);
            Assert.True(dataSource.HasModifications);

            var eventValues = new List<bool>();
            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventValues.Add(hasModifications);
            };

            // Act - revert to original value
            dataSource.TrackPropertyChange("item1", "Name", "Item 1", out bool isPropertyModified);

            // Assert
            Assert.False(isPropertyModified, "Property should no longer be considered modified");
            Assert.False(dataSource.IsPropertyModified("item1", "Name"));
            Assert.False(dataSource.HasModifications);
            Assert.Contains(false, eventValues);
        }

        [Fact]
        public void GetPropertyBaselineValue_ReturnsOriginalValue()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Original Name", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            // Modify the property
            dataSource.TrackPropertyChange("item1", "Name", "Modified Name", out _);

            // Act
            var baselineValue = dataSource.GetPropertyBaselineValue("item1", "Name");

            // Assert
            Assert.Equal("Original Name", baselineValue);
        }

        #endregion

        #region GetModifiedProperties Tests

        [Fact]
        public void GetModifiedProperties_ReturnsAllModifiedPropertyNames()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.TrackPropertyChange("item1", "Name", "Modified Name", out _);
            dataSource.TrackPropertyChange("item1", "Value", 999, out _);

            // Act
            var modifiedProperties = dataSource.GetModifiedProperties("item1").ToList();

            // Assert
            Assert.Equal(2, modifiedProperties.Count);
            Assert.Contains("Name", modifiedProperties);
            Assert.Contains("Value", modifiedProperties);
        }

        #endregion

        #region ItemState Tests

        [Fact]
        public void GetItemState_ReturnsCorrectStates()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("existing", "Existing", 10);
            repo.AddInitialData("modified", "Original", 30);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.Add("added", new TestData { Id = "added", Name = "Added", Value = 20 });
            dataSource.Delete("existing");
            dataSource.MarkModified("modified");

            // Assert
            Assert.Equal(ItemState.Added, dataSource.GetItemState("added"));
            Assert.Equal(ItemState.Deleted, dataSource.GetItemState("existing"));
            Assert.Equal(ItemState.Modified, dataSource.GetItemState("modified"));
        }

        #endregion

        #region EnumerateItems Tests

        [Fact]
        public void EnumerateItems_ExcludesDeletedItems()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            repo.AddInitialData("item2", "Item 2", 20);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.Delete("item1");

            // Act
            var items = ((IEditableDataSource<string, TestData>)dataSource).EnumerateItems().ToList();

            // Assert
            Assert.Single(items);
            Assert.Equal("item2", items[0].Key);
        }

        [Fact]
        public void EnumerateItems_IncludesAddedItems()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.Add("item2", new TestData { Id = "item2", Name = "Item 2", Value = 20 });

            // Act
            var items = ((IEditableDataSource<string, TestData>)dataSource).EnumerateItems().ToList();

            // Assert
            Assert.Equal(2, items.Count);
            Assert.True(items.Any(i => i.Key == "item2"));
        }

        #endregion

        #region RefreshBaseline Tests

        [Fact]
        public void RefreshBaseline_ClearsModificationsAndFiresEvent()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            dataSource.MarkModified("item1");
            Assert.True(dataSource.HasModifications);

            bool eventFired = false;
            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
            };

            // Act
            dataSource.RefreshBaseline();

            // Assert
            Assert.True(eventFired);
            Assert.False(dataSource.HasModifications);
        }

        #endregion

        #region Count Tests

        [Fact]
        public void Count_ReflectsAdditionsAndDeletions()
        {
            // Arrange
            var repo = new MockKeyValueRepository();
            repo.AddInitialData("item1", "Item 1", 10);
            repo.AddInitialData("item2", "Item 2", 20);
            var dataSource = new EditableKeyValueDataSource<string, TestData>(repo);

            Assert.Equal(2, dataSource.Count);

            // Act
            dataSource.Add("item3", new TestData { Id = "item3", Name = "Item 3", Value = 30 });
            Assert.Equal(3, dataSource.Count);

            dataSource.Delete("item1");
            Assert.Equal(2, dataSource.Count);
        }

        #endregion
    }
}
