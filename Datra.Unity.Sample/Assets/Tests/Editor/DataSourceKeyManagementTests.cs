using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using NUnit.Framework;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Integration tests for DataSource key management.
    /// Tests the unified IEditableDataSource.GetItemKey() and TrackPropertyChange() interface.
    /// These tests verify that Views can use the non-generic interface methods without reflection.
    /// </summary>
    public class DataSourceKeyManagementTests
    {
        #region Test Data Classes

        private class TestTableData : ITableData<string>
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public int Value { get; set; }
        }

        private class TestSingleData
        {
            public string Title { get; set; } = "";
            public int Count { get; set; }
        }

        #endregion

        #region Mock Repositories

        private class MockTableRepository : IKeyValueDataRepository<string, TestTableData>
        {
            private readonly Dictionary<string, TestTableData> _data = new();

            public void AddItem(string id, string name, int value)
            {
                _data[id] = new TestTableData { Id = id, Name = name, Value = value };
            }

            public IReadOnlyDictionary<string, TestTableData> GetAll() => _data;
            public TestTableData GetById(string id) => _data[id];
            public TestTableData TryGetById(string id) => _data.TryGetValue(id, out var v) ? v : null;
            public bool Contains(string id) => _data.ContainsKey(id);
            public int Count => _data.Count;
            public void Add(TestTableData item) => _data[item.Id] = item;
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

            public Task LoadAsync() => Task.CompletedTask;
            public Task SaveAsync() => Task.CompletedTask;
            public string GetLoadedFilePath() => "mock://test.csv";
            public IEnumerable<object> EnumerateItems() => _data.Values;
            public int ItemCount => _data.Count;
            public IEnumerable<TestTableData> Find(Func<TestTableData, bool> predicate)
            {
                foreach (var item in _data.Values)
                    if (predicate(item))
                        yield return item;
            }

            public bool ContainsKey(string key) => _data.ContainsKey(key);
            public bool TryGetValue(string key, out TestTableData value) => _data.TryGetValue(key, out value);
            public TestTableData this[string key] => _data[key];
            public IEnumerable<string> Keys => _data.Keys;
            public IEnumerable<TestTableData> Values => _data.Values;
            int System.Collections.Generic.IReadOnlyCollection<KeyValuePair<string, TestTableData>>.Count => _data.Count;
            public IEnumerator<KeyValuePair<string, TestTableData>> GetEnumerator() => _data.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private class MockSingleRepository : ISingleDataRepository<TestSingleData>
        {
            private TestSingleData _data;

            public void SetInitialData(TestSingleData data) => _data = data;

            public bool IsLoaded => _data != null;
            public TestSingleData Get() => _data ?? throw new InvalidOperationException("Not loaded");
            public void Set(TestSingleData data) => _data = data;
            public Task LoadAsync()
            {
                _data = new TestSingleData { Title = "Default", Count = 0 };
                return Task.CompletedTask;
            }
            public Task SaveAsync() => Task.CompletedTask;
            public string GetLoadedFilePath() => "mock://single.json";
            public IEnumerable<object> EnumerateItems() => _data != null ? new[] { _data } : Array.Empty<object>();
            public int ItemCount => _data != null ? 1 : 0;
        }

        #endregion

        #region GetItemKey Tests - KeyValue DataSource

        [Test]
        public void KeyValueDataSource_GetItemKey_WithKeyValuePair_ReturnsKey()
        {
            // Arrange
            var repo = new MockTableRepository();
            repo.AddItem("item1", "Item One", 100);
            var dataSource = new EditableKeyValueDataSource<string, TestTableData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            var kvp = new KeyValuePair<string, TestTableData>("item1", new TestTableData { Id = "item1" });

            // Act
            var key = nonGeneric.GetItemKey(kvp);

            // Assert
            Assert.AreEqual("item1", key);
        }

        [Test]
        public void KeyValueDataSource_GetItemKey_WithDirectData_ReturnsId()
        {
            // Arrange
            var repo = new MockTableRepository();
            var dataSource = new EditableKeyValueDataSource<string, TestTableData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            var data = new TestTableData { Id = "test_id", Name = "Test", Value = 42 };

            // Act
            var key = nonGeneric.GetItemKey(data);

            // Assert
            Assert.AreEqual("test_id", key);
        }

        #endregion

        #region GetItemKey Tests - Single DataSource

        [Test]
        public void SingleDataSource_GetItemKey_WithData_ReturnsSingleKey()
        {
            // Arrange
            var repo = new MockSingleRepository();
            repo.SetInitialData(new TestSingleData { Title = "Test", Count = 5 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            var data = new TestSingleData { Title = "Test", Count = 5 };

            // Act
            var key = nonGeneric.GetItemKey(data);

            // Assert
            Assert.AreEqual(EditableSingleDataSource<TestSingleData>.SingleKey, key);
        }

        #endregion

        #region TrackPropertyChange Tests - Interface Method (No Reflection)

        [Test]
        public void KeyValueDataSource_TrackPropertyChange_ViaInterface_DetectsModification()
        {
            // Arrange
            var repo = new MockTableRepository();
            repo.AddItem("item1", "Original Name", 100);
            var dataSource = new EditableKeyValueDataSource<string, TestTableData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            // Act - use interface method (no reflection!)
            nonGeneric.TrackPropertyChange("item1", "Name", "Modified Name", out bool isModified);

            // Assert
            Assert.IsTrue(isModified);
            Assert.IsTrue(dataSource.HasModifications);
            Assert.IsTrue(dataSource.IsPropertyModified("item1", "Name"));
        }

        [Test]
        public void SingleDataSource_TrackPropertyChange_ViaInterface_WithLegacyKey_DetectsModification()
        {
            // Arrange
            var repo = new MockSingleRepository();
            repo.SetInitialData(new TestSingleData { Title = "Original", Count = 10 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            // Act - use "single" key (legacy key from DatraDataView)
            nonGeneric.TrackPropertyChange("single", "Title", "Modified", out bool isModified);

            // Assert - should work due to backward compatibility
            Assert.IsTrue(isModified, "Should accept 'single' key for backward compatibility");
            Assert.IsTrue(dataSource.HasModifications);
        }

        [Test]
        public void SingleDataSource_TrackPropertyChange_ViaInterface_WithConstantKey_DetectsModification()
        {
            // Arrange
            var repo = new MockSingleRepository();
            repo.SetInitialData(new TestSingleData { Title = "Original", Count = 10 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            // Act - use constant SingleKey
            nonGeneric.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out bool isModified);

            // Assert
            Assert.IsTrue(isModified);
            Assert.IsTrue(dataSource.HasModifications);
        }

        [Test]
        public void TrackPropertyChange_RevertingToBaseline_ClearsModification()
        {
            // Arrange
            var repo = new MockTableRepository();
            repo.AddItem("item1", "Original Name", 100);
            var dataSource = new EditableKeyValueDataSource<string, TestTableData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            // First modify
            nonGeneric.TrackPropertyChange("item1", "Name", "Modified", out _);
            Assert.IsTrue(dataSource.HasModifications);

            // Act - revert to original
            nonGeneric.TrackPropertyChange("item1", "Name", "Original Name", out bool isModified);

            // Assert
            Assert.IsFalse(isModified);
            Assert.IsFalse(dataSource.HasModifications);
        }

        #endregion

        #region Event Notification Tests

        [Test]
        public void TrackPropertyChange_ViaInterface_FiresOnModifiedStateChanged()
        {
            // Arrange
            var repo = new MockTableRepository();
            repo.AddItem("item1", "Original", 100);
            var dataSource = new EditableKeyValueDataSource<string, TestTableData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            bool eventFired = false;
            bool? eventValue = null;
            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
                eventValue = hasModifications;
            };

            // Act
            nonGeneric.TrackPropertyChange("item1", "Name", "Modified", out _);

            // Assert
            Assert.IsTrue(eventFired);
            Assert.IsTrue(eventValue);
        }

        #endregion

        #region Integration Tests - Simulating View Usage

        [Test]
        public void SimulateView_GetKey_TrackChange_WithoutReflection()
        {
            // This test simulates what DatraTableView/DatraFormView do:
            // 1. Get item key using dataSource.GetItemKey()
            // 2. Track property change using dataSource.TrackPropertyChange()

            // Arrange
            var repo = new MockTableRepository();
            repo.AddItem("hero_001", "Knight", 50);
            var dataSource = new EditableKeyValueDataSource<string, TestTableData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            var item = new TestTableData { Id = "hero_001", Name = "Knight", Value = 50 };

            // Step 1: Get key (no reflection)
            var itemKey = nonGeneric.GetItemKey(item);
            Assert.AreEqual("hero_001", itemKey);

            // Step 2: Track property change (no reflection)
            nonGeneric.TrackPropertyChange(itemKey, "Name", "Paladin", out bool isModified);
            Assert.IsTrue(isModified);
            Assert.IsTrue(nonGeneric.HasModifications);

            // Step 3: Check modification state via interface
            Assert.IsTrue(nonGeneric.IsPropertyModified(itemKey, "Name"));
        }

        #endregion
    }
}
