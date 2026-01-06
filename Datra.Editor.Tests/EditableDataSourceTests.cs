#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Xunit;

namespace Datra.Editor.Tests
{
    #region Test Data Classes

    public class TestTableData : ITableData<int>
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Value { get; set; }
    }

    public class TestSingleData
    {
        public string Title { get; set; } = "";
        public int Version { get; set; }
    }

    #endregion

    #region Mock Repositories

    public class MockKeyValueRepository<TKey, TData> : IKeyValueDataRepository<TKey, TData>
        where TKey : notnull
        where TData : class, ITableData<TKey>
    {
        private readonly Dictionary<TKey, TData> _data = new();
        public int SaveCount { get; private set; }
        public List<TKey> RemovedKeys { get; } = new();
        public List<TData> AddedItems { get; } = new();

        public void SetData(IEnumerable<TData> items)
        {
            _data.Clear();
            foreach (var item in items)
            {
                _data[item.Id] = item;
            }
        }

        public IReadOnlyDictionary<TKey, TData> GetAll() => _data;
        public TData GetById(TKey id) => _data[id];
        public TData? TryGetById(TKey id) => _data.TryGetValue(id, out var v) ? v : null;
        public bool Contains(TKey id) => _data.ContainsKey(id);
        public int Count => _data.Count;
        public int ItemCount => _data.Count;

        public void Add(TData data)
        {
            _data[data.Id] = data;
            AddedItems.Add(data);
        }

        public bool Remove(TKey key)
        {
            RemovedKeys.Add(key);
            return _data.Remove(key);
        }

        public bool UpdateKey(TKey oldKey, TKey newKey)
        {
            if (!_data.TryGetValue(oldKey, out var item)) return false;
            _data.Remove(oldKey);
            _data[newKey] = item;
            return true;
        }

        public void Clear() => _data.Clear();

        public Task LoadAsync() => Task.CompletedTask;

        public Task SaveAsync()
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public string GetLoadedFilePath() => "mock://path";

        public IEnumerable<object> EnumerateItems() => _data.Values;

        public IEnumerable<TData> Find(Func<TData, bool> predicate) => _data.Values.Where(predicate);

        // IReadOnlyDictionary implementation
        public TData this[TKey key] => _data[key];
        public IEnumerable<TKey> Keys => _data.Keys;
        public IEnumerable<TData> Values => _data.Values;
        public bool ContainsKey(TKey key) => _data.ContainsKey(key);
        public bool TryGetValue(TKey key, out TData value) => _data.TryGetValue(key, out value!);
        public IEnumerator<KeyValuePair<TKey, TData>> GetEnumerator() => _data.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class MockSingleRepository<TData> : ISingleDataRepository<TData>
        where TData : class
    {
        private TData? _data;
        public int SaveCount { get; private set; }

        public void SetData(TData data) => _data = data;
        public TData Get() => _data ?? throw new InvalidOperationException("Not loaded");
        public void Set(TData data) => _data = data;
        public bool IsLoaded => _data != null;

        public Task LoadAsync() => Task.CompletedTask;

        public Task SaveAsync()
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public string GetLoadedFilePath() => "mock://single";
        public IEnumerable<object> EnumerateItems() => _data != null ? new[] { _data } : Array.Empty<object>();
        public int ItemCount => _data != null ? 1 : 0;
    }

    #endregion

    #region EditableKeyValueDataSource Tests

    public class EditableKeyValueDataSourceTests
    {
        private MockKeyValueRepository<int, TestTableData> CreateRepository(params TestTableData[] items)
        {
            var repo = new MockKeyValueRepository<int, TestTableData>();
            repo.SetData(items);
            return repo;
        }

        [Fact]
        public void Constructor_InitializesBaseline()
        {
            var repo = CreateRepository(
                new TestTableData { Id = 1, Name = "Item1" },
                new TestTableData { Id = 2, Name = "Item2" }
            );

            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            Assert.Equal(2, source.Count);
            Assert.False(source.HasModifications);
        }

        [Fact]
        public void EnumerateItems_ReturnsAllItems()
        {
            var repo = CreateRepository(
                new TestTableData { Id = 1, Name = "Item1" },
                new TestTableData { Id = 2, Name = "Item2" }
            );

            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            var items = source.EnumerateItems().ToList();
            Assert.Equal(2, items.Count);
        }

        [Fact]
        public void GetItem_ReturnsBaselineWhenUnmodified()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            var item = source.GetItem(1);

            Assert.Equal("Original", item.Name);
        }

        [Fact]
        public void TrackPropertyChange_MarksAsModified()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out bool isModified);

            Assert.True(isModified);
            Assert.True(source.HasModifications);
            Assert.Equal(ItemState.Modified, source.GetItemState(1));
        }

        [Fact]
        public void TrackPropertyChange_ReturnsModifiedValue()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            var item = source.GetItem(1);

            Assert.Equal("Modified", item.Name);
        }

        [Fact]
        public void TrackPropertyChange_RevertToBaseline_ClearsModified()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            Assert.True(source.HasModifications);

            source.TrackPropertyChange(1, "Name", "Original", out bool isModified);

            Assert.False(isModified);
            Assert.False(source.HasModifications);
        }

        [Fact]
        public void Add_MarksAsAdded()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Existing" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Add(2, new TestTableData { Id = 2, Name = "New" });

            Assert.True(source.HasModifications);
            Assert.Equal(ItemState.Added, source.GetItemState(2));
            Assert.Equal(2, source.Count);
        }

        [Fact]
        public void Delete_MarksAsDeleted()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "ToDelete" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Delete(1);

            Assert.True(source.HasModifications);
            Assert.Equal(ItemState.Deleted, source.GetItemState(1));
            Assert.Equal(0, source.Count);
        }

        [Fact]
        public void Delete_AddedItem_RemovesCompletely()
        {
            var repo = CreateRepository();
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Add(1, new TestTableData { Id = 1, Name = "New" });
            Assert.True(source.HasModifications);

            source.Delete(1);

            Assert.False(source.HasModifications);
            Assert.Equal(0, source.Count);
        }

        [Fact]
        public void EnumerateItems_ExcludesDeleted()
        {
            var repo = CreateRepository(
                new TestTableData { Id = 1, Name = "Keep" },
                new TestTableData { Id = 2, Name = "Delete" }
            );
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Delete(2);
            var items = ((IEditableDataSource<int, TestTableData>)source).EnumerateItems().ToList();

            Assert.Single(items);
            Assert.Equal(1, items[0].Value.Id);
        }

        [Fact]
        public void EnumerateItems_IncludesAdded()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Existing" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Add(2, new TestTableData { Id = 2, Name = "Added" });
            var items = source.EnumerateItems().ToList();

            Assert.Equal(2, items.Count);
        }

        [Fact]
        public void Revert_ClearsAllChanges()
        {
            var repo = CreateRepository(
                new TestTableData { Id = 1, Name = "Item1" },
                new TestTableData { Id = 2, Name = "Item2" }
            );
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            // Make various changes
            source.TrackPropertyChange(1, "Name", "Modified", out _);
            source.Delete(2);
            source.Add(3, new TestTableData { Id = 3, Name = "New" });

            Assert.True(source.HasModifications);

            source.Revert();

            Assert.False(source.HasModifications);
            Assert.Equal(2, source.Count);
            Assert.Equal(ItemState.Unchanged, source.GetItemState(1));
            Assert.Equal(ItemState.Unchanged, source.GetItemState(2));
        }

        [Fact]
        public void Revert_RestoresDeletedItems()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Item" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Delete(1);
            Assert.Equal(0, source.Count);

            source.Revert();

            Assert.Equal(1, source.Count);
            Assert.True(source.ContainsKey(1));
        }

        [Fact]
        public void Revert_RemovesAddedItems()
        {
            var repo = CreateRepository();
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.Add(1, new TestTableData { Id = 1, Name = "New" });
            Assert.Equal(1, source.Count);

            source.Revert();

            Assert.Equal(0, source.Count);
        }

        [Fact]
        public void Revert_RestoresModifiedValues()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original", Value = 100 });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            source.TrackPropertyChange(1, "Value", 999, out _);

            source.Revert();

            var item = source.GetItem(1);
            Assert.Equal("Original", item.Name);
            // Note: Value will be from baseline clone
        }

        [Fact]
        public async Task SaveAsync_AppliesChangesToRepository()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            source.Add(2, new TestTableData { Id = 2, Name = "New" });
            source.Delete(1);

            await source.SaveAsync();

            Assert.Equal(1, repo.SaveCount);
            Assert.Contains(1, repo.RemovedKeys);
            Assert.Contains(repo.AddedItems, i => i.Id == 2);
        }

        [Fact]
        public async Task SaveAsync_ClearsModificationsAfterSave()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            Assert.True(source.HasModifications);

            await source.SaveAsync();

            Assert.False(source.HasModifications);
        }

        [Fact]
        public void GetBaselineValue_ReturnsOriginalValue()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            var baseline = source.GetBaselineValue(1);

            Assert.NotNull(baseline);
            Assert.Equal("Original", baseline!.Name);
        }

        [Fact]
        public void IsPropertyModified_ReturnsCorrectState()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original", Value = 100 });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);

            Assert.True(source.IsPropertyModified(1, "Name"));
            Assert.False(source.IsPropertyModified(1, "Value"));
        }

        [Fact]
        public void GetModifiedProperties_ReturnsOnlyModified()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original", Value = 100 });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            source.TrackPropertyChange(1, "Value", 200, out _);

            var modified = source.GetModifiedProperties(1).ToList();

            Assert.Equal(2, modified.Count);
            Assert.Contains("Name", modified);
            Assert.Contains("Value", modified);
        }

        [Fact]
        public void OnModifiedStateChanged_FiresOnFirstModification()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);
            bool? lastState = null;

            source.OnModifiedStateChanged += state => lastState = state;

            source.TrackPropertyChange(1, "Name", "Modified", out _);

            Assert.True(lastState);
        }

        [Fact]
        public void OnModifiedStateChanged_FiresOnRevert()
        {
            var repo = CreateRepository(new TestTableData { Id = 1, Name = "Original" });
            var source = new EditableKeyValueDataSource<int, TestTableData>(repo);
            bool? lastState = null;

            source.TrackPropertyChange(1, "Name", "Modified", out _);
            source.OnModifiedStateChanged += state => lastState = state;

            source.Revert();

            Assert.False(lastState);
        }
    }

    #endregion

    #region EditableSingleDataSource Tests

    public class EditableSingleDataSourceTests
    {
        private MockSingleRepository<TestSingleData> CreateRepository(TestSingleData? data = null)
        {
            var repo = new MockSingleRepository<TestSingleData>();
            if (data != null) repo.SetData(data);
            return repo;
        }

        [Fact]
        public void Constructor_InitializesBaseline()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Test", Version = 1 });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            Assert.Equal(1, source.Count);
            Assert.False(source.HasModifications);
        }

        [Fact]
        public void GetCurrentData_ReturnsBaselineWhenUnmodified()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            var data = source.GetCurrentData();

            Assert.NotNull(data);
            Assert.Equal("Original", data!.Title);
        }

        [Fact]
        public void TrackPropertyChange_MarksAsModified()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            source.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out bool isModified);

            Assert.True(isModified);
            Assert.True(source.HasModifications);
        }

        [Fact]
        public void TrackPropertyChange_UpdatesWorkingCopy()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            source.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out _);
            var data = source.GetCurrentData();

            Assert.Equal("Modified", data!.Title);
        }

        [Fact]
        public void Revert_ClearsModifications()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            source.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out _);
            Assert.True(source.HasModifications);

            source.Revert();

            Assert.False(source.HasModifications);
        }

        [Fact]
        public void Revert_RestoresOriginalValue()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            source.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out _);
            source.Revert();

            var data = source.GetCurrentData();
            Assert.Equal("Original", data!.Title);
        }

        [Fact]
        public async Task SaveAsync_AppliesChangesToRepository()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            source.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out _);

            await source.SaveAsync();

            Assert.Equal(1, repo.SaveCount);
            Assert.Equal("Modified", repo.Get().Title);
        }

        [Fact]
        public void Add_ThrowsNotSupported()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Test" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            Assert.Throws<NotSupportedException>(() =>
                source.Add("key", new TestSingleData()));
        }

        [Fact]
        public void Delete_ThrowsNotSupported()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Test" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            Assert.Throws<NotSupportedException>(() =>
                source.Delete(EditableSingleDataSource<TestSingleData>.SingleKey));
        }

        [Fact]
        public void GetItemState_ReturnsModifiedWhenChanged()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            source.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Title", "Modified", out _);

            Assert.Equal(ItemState.Modified, source.GetItemState(EditableSingleDataSource<TestSingleData>.SingleKey));
        }

        [Fact]
        public void GetItemState_ReturnsUnchangedWhenNotModified()
        {
            var repo = CreateRepository(new TestSingleData { Title = "Original" });
            var source = new EditableSingleDataSource<TestSingleData>(repo);

            Assert.Equal(ItemState.Unchanged, source.GetItemState(EditableSingleDataSource<TestSingleData>.SingleKey));
        }
    }

    #endregion

    #region DataSource vs Repository Integration Tests

    /// <summary>
    /// These tests verify the critical contract that Views rely on:
    /// DataSource.EnumerateItems() reflects current editing state,
    /// while Repository.EnumerateItems() remains unchanged until Save.
    /// </summary>
    public class DataSourceRepositoryIntegrationTests
    {
        [Fact]
        public void AfterDelete_DataSourceExcludesItem_RepositoryStillHasIt()
        {
            // Arrange
            var repo = new MockKeyValueRepository<int, TestTableData>();
            repo.SetData(new[]
            {
                new TestTableData { Id = 1, Name = "Keep" },
                new TestTableData { Id = 2, Name = "ToDelete" }
            });
            var dataSource = new EditableKeyValueDataSource<int, TestTableData>(repo);

            // Act
            dataSource.Delete(2);

            // Assert - DataSource should exclude deleted item
            var dataSourceItems = ((IEditableDataSource<int, TestTableData>)dataSource).EnumerateItems().ToList();
            Assert.Single(dataSourceItems);
            Assert.Equal(1, dataSourceItems[0].Value.Id);

            // Assert - Repository should still have both items (unchanged until Save)
            var repoItems = repo.EnumerateItems().ToList();
            Assert.Equal(2, repoItems.Count);
        }

        [Fact]
        public void AfterAdd_DataSourceIncludesItem_RepositoryDoesNot()
        {
            // Arrange
            var repo = new MockKeyValueRepository<int, TestTableData>();
            repo.SetData(new[] { new TestTableData { Id = 1, Name = "Existing" } });
            var dataSource = new EditableKeyValueDataSource<int, TestTableData>(repo);

            // Act
            dataSource.Add(2, new TestTableData { Id = 2, Name = "NewItem" });

            // Assert - DataSource should include new item
            var dataSourceItems = dataSource.EnumerateItems().ToList();
            Assert.Equal(2, dataSourceItems.Count);

            // Assert - Repository should still have only 1 item (unchanged until Save)
            var repoItems = repo.EnumerateItems().ToList();
            Assert.Single(repoItems);
        }

        [Fact]
        public void AfterRevert_DataSourceMatchesRepository()
        {
            // Arrange
            var repo = new MockKeyValueRepository<int, TestTableData>();
            repo.SetData(new[]
            {
                new TestTableData { Id = 1, Name = "Item1" },
                new TestTableData { Id = 2, Name = "Item2" }
            });
            var dataSource = new EditableKeyValueDataSource<int, TestTableData>(repo);

            // Act - Make various changes then revert
            dataSource.Delete(1);
            dataSource.Add(3, new TestTableData { Id = 3, Name = "New" });
            dataSource.TrackPropertyChange(2, "Name", "Modified", out _);

            // Verify changes are reflected
            Assert.Equal(2, dataSource.EnumerateItems().Count()); // 2 (deleted 1, added 3)

            // Revert all changes
            dataSource.Revert();

            // Assert - DataSource should match repository again
            var dataSourceItems = dataSource.EnumerateItems().ToList();
            var repoItems = repo.EnumerateItems().ToList();
            Assert.Equal(repoItems.Count, dataSourceItems.Count);
        }

        [Fact]
        public async Task AfterSave_BothDataSourceAndRepositoryMatch()
        {
            // Arrange
            var repo = new MockKeyValueRepository<int, TestTableData>();
            repo.SetData(new[] { new TestTableData { Id = 1, Name = "Original" } });
            var dataSource = new EditableKeyValueDataSource<int, TestTableData>(repo);

            // Act - Add new item and save
            dataSource.Add(2, new TestTableData { Id = 2, Name = "New" });
            await dataSource.SaveAsync();

            // Assert - Both should have 2 items now
            var dataSourceCount = dataSource.EnumerateItems().Count();
            var repoCount = repo.EnumerateItems().Count();
            Assert.Equal(2, dataSourceCount);
            Assert.Equal(2, repoCount);
        }

        [Fact]
        public void ViewShouldUseDataSource_NotRepository_ForCurrentState()
        {
            // This test documents the contract that Views MUST follow:
            // Use dataSource.EnumerateItems() for display, NOT repository.EnumerateItems()

            // Arrange
            var repo = new MockKeyValueRepository<int, TestTableData>();
            repo.SetData(new[]
            {
                new TestTableData { Id = 1, Name = "Keep" },
                new TestTableData { Id = 2, Name = "Delete" },
                new TestTableData { Id = 3, Name = "Modify" }
            });
            var dataSource = new EditableKeyValueDataSource<int, TestTableData>(repo);

            // Act - Simulate user editing
            dataSource.Delete(2);
            dataSource.Add(4, new TestTableData { Id = 4, Name = "New" });
            dataSource.TrackPropertyChange(3, "Name", "Modified", out _);

            // What View should show (from dataSource):
            var viewItems = ((IEditableDataSource<int, TestTableData>)dataSource).EnumerateItems().ToList();
            Assert.Equal(3, viewItems.Count); // 1, 3, 4 (2 is deleted)

            // What View should NOT use (repository is unchanged):
            var repoItems = repo.EnumerateItems().ToList();
            Assert.Equal(3, repoItems.Count); // 1, 2, 3 (no changes)

            // Verify specific items in view
            var viewIds = viewItems.Select(kv => kv.Key).OrderBy(x => x).ToList();
            Assert.Equal(new[] { 1, 3, 4 }, viewIds);
        }
    }

    #endregion
}
