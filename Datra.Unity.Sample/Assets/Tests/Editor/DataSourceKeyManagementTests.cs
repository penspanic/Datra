using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra;
using Datra.Interfaces;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
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

        private class MockTableRepository : ITableRepository<string, TestTableData>
        {
            private readonly Dictionary<string, TestTableData> _data = new();
            private readonly Dictionary<string, TestTableData> _baseline = new();
            private readonly HashSet<string> _addedKeys = new();
            private readonly HashSet<string> _modifiedKeys = new();
            private readonly HashSet<string> _deletedKeys = new();

            public void AddItem(string id, string name, int value)
            {
                var item = new TestTableData { Id = id, Name = name, Value = value };
                _data[id] = item;
                _baseline[id] = new TestTableData { Id = id, Name = name, Value = value };
            }

            // IRepository
            public Task InitializeAsync() => Task.CompletedTask;
            public bool IsInitialized => true;

            // ITableRepository - Metadata
            public int Count => _data.Count - _deletedKeys.Count;
            public bool Contains(string key) => _data.ContainsKey(key) && !_deletedKeys.Contains(key);
            public IEnumerable<string> Keys => _data.Keys.Where(k => !_deletedKeys.Contains(k));

            // ITableRepository - Reading (async)
            public Task<TestTableData?> GetAsync(string key) => Task.FromResult(TryGetLoaded(key));
            public Task<IReadOnlyDictionary<string, TestTableData>> GetAllAsync() => Task.FromResult<IReadOnlyDictionary<string, TestTableData>>(_data);
            public Task<IEnumerable<TestTableData>> FindAsync(Func<TestTableData, bool> predicate) =>
                Task.FromResult(_data.Values.Where(predicate));

            // ITableRepository - Loaded data (sync)
            public TestTableData? TryGetLoaded(string key) => _data.TryGetValue(key, out var v) && !_deletedKeys.Contains(key) ? v : null;
            public IReadOnlyDictionary<string, TestTableData> LoadedItems => _data;

            // ITableRepository - Writing
            public void Add(TestTableData data) => Add(data.Id, data);
            public void Add(string key, TestTableData data)
            {
                _data[key] = data;
                _addedKeys.Add(key);
            }
            public void Update(string key, TestTableData data)
            {
                _data[key] = data;
                if (!_addedKeys.Contains(key)) _modifiedKeys.Add(key);
            }
            public void Remove(string key) => _deletedKeys.Add(key);

            // ITableRepository - Working Copy
            public TestTableData GetWorkingCopy(string key) => _data[key];
            public void MarkAsModified(string key) { if (!_addedKeys.Contains(key)) _modifiedKeys.Add(key); }

            // IChangeTracking
            public bool HasChanges => _addedKeys.Count > 0 || _modifiedKeys.Count > 0 || _deletedKeys.Count > 0;
            public void Revert()
            {
                foreach (var key in _addedKeys) _data.Remove(key);
                foreach (var key in _modifiedKeys.Where(k => _baseline.ContainsKey(k)))
                    _data[key] = new TestTableData { Id = _baseline[key].Id, Name = _baseline[key].Name, Value = _baseline[key].Value };
                _addedKeys.Clear();
                _modifiedKeys.Clear();
                _deletedKeys.Clear();
            }
            public Task SaveAsync() => Task.CompletedTask;
            public event Action<bool>? OnModifiedStateChanged;

            // IChangeTracking<TKey>
            public ChangeState GetState(string key)
            {
                if (_addedKeys.Contains(key)) return ChangeState.Added;
                if (_deletedKeys.Contains(key)) return ChangeState.Deleted;
                if (_modifiedKeys.Contains(key)) return ChangeState.Modified;
                return ChangeState.Unchanged;
            }
            public IEnumerable<string> GetChangedKeys() => _addedKeys.Concat(_modifiedKeys).Concat(_deletedKeys);
            public IEnumerable<string> GetAddedKeys() => _addedKeys;
            public IEnumerable<string> GetModifiedKeys() => _modifiedKeys;
            public IEnumerable<string> GetDeletedKeys() => _deletedKeys;
            public TData? GetBaseline<TData>(string key) where TData : class =>
                _baseline.TryGetValue(key, out var v) ? v as TData : null;
            public bool IsPropertyModified(string key, string propertyName) => _modifiedKeys.Contains(key);
            public IEnumerable<string> GetModifiedProperties(string key) => _modifiedKeys.Contains(key) ? new[] { "Name", "Value" } : Array.Empty<string>();
            public object? GetPropertyBaseline(string key, string propertyName) => null;
            public void TrackPropertyChange(string key, string propertyName, object? newValue) { if (!_addedKeys.Contains(key)) _modifiedKeys.Add(key); }
            public void Revert(string key)
            {
                if (_addedKeys.Contains(key)) { _data.Remove(key); _addedKeys.Remove(key); }
                else if (_baseline.TryGetValue(key, out var v)) { _data[key] = new TestTableData { Id = v.Id, Name = v.Name, Value = v.Value }; _modifiedKeys.Remove(key); }
                _deletedKeys.Remove(key);
            }
            public void RevertProperty(string key, string propertyName) => Revert(key);
        }

        private class MockSingleRepository : ISingleRepository<TestSingleData>
        {
            private TestSingleData? _data;
            private TestSingleData? _baseline;

            public void SetInitialData(TestSingleData data)
            {
                _data = data;
                _baseline = new TestSingleData { Title = data.Title, Count = data.Count };
            }

            // IRepository
            public Task InitializeAsync()
            {
                _data = new TestSingleData { Title = "Default", Count = 0 };
                _baseline = new TestSingleData { Title = "Default", Count = 0 };
                return Task.CompletedTask;
            }
            public bool IsInitialized => _data != null;

            // ISingleRepository - Reading
            public Task<TestSingleData?> GetAsync() => Task.FromResult(_data);
            public TestSingleData? Current => _data;

            // ISingleRepository - Writing
            public void Set(TestSingleData data) => _data = data;

            // ISingleRepository - Change tracking
            public TestSingleData? Baseline => _baseline;
            public bool IsPropertyModified(string propertyName) => false;
            public IEnumerable<string> GetModifiedProperties() => Array.Empty<string>();
            public object? GetPropertyBaseline(string propertyName) => null;
            public void TrackPropertyChange(string propertyName, object? newValue) { }
            public void RevertProperty(string propertyName) { }

            // IChangeTracking
            public bool HasChanges => false;
            public void Revert() { if (_baseline != null) _data = new TestSingleData { Title = _baseline.Title, Count = _baseline.Count }; }
            public Task SaveAsync() => Task.CompletedTask;
            public event Action<bool>? OnModifiedStateChanged;
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
