#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Repositories;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// EditableSingleRepository, EditableTableRepository, EditableAssetRepository 테스트
    /// </summary>
    public class EditableRepositoryTests
    {
        #region Test Data Classes

        public class TestConfig
        {
            public string Name { get; set; } = "";
            public int MaxPlayers { get; set; }
            public bool IsDebugMode { get; set; }
        }

        public class TestItem
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public int Price { get; set; }
        }

        public class TestGraphData
        {
            public string Title { get; set; } = "";
            public int NodeCount { get; set; }
        }

        #endregion

        #region Test Repository Implementations

        /// <summary>
        /// 테스트용 EditableSingleRepository 구현
        /// </summary>
        private class TestSingleRepository : EditableSingleRepository<TestConfig>
        {
            private TestConfig? _storedData;
            public int SaveCount { get; private set; }

            public void SetStoredData(TestConfig? data) => _storedData = data;

            protected override Task<TestConfig?> LoadDataAsync()
            {
                return Task.FromResult(_storedData != null
                    ? DeepCloner.Clone(_storedData)
                    : null);
            }

            protected override Task SaveDataAsync(TestConfig data)
            {
                _storedData = DeepCloner.Clone(data);
                SaveCount++;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 테스트용 EditableTableRepository 구현
        /// </summary>
        private class TestTableRepository : EditableTableRepository<string, TestItem>
        {
            private readonly Dictionary<string, TestItem> _storedData = new();
            public int SaveCount { get; private set; }

            public void AddStoredData(TestItem item) => _storedData[item.Id] = DeepCloner.Clone(item)!;

            protected override string ExtractKey(TestItem data) => data.Id;

            protected override async IAsyncEnumerable<(string key, TestItem data)> LoadAllDataAsync()
            {
                foreach (var kvp in _storedData)
                {
                    yield return (kvp.Key, DeepCloner.Clone(kvp.Value)!);
                }
                await Task.CompletedTask;
            }

            protected override Task<TestItem?> LoadDataAsync(string key)
            {
                return Task.FromResult(_storedData.TryGetValue(key, out var item)
                    ? DeepCloner.Clone(item)
                    : null);
            }

            protected override Task SaveAllDataAsync(
                IEnumerable<(string key, TestItem data)> addedItems,
                IEnumerable<(string key, TestItem data)> modifiedItems,
                IEnumerable<string> deletedKeys)
            {
                // Add new items
                foreach (var (key, data) in addedItems)
                {
                    _storedData[key] = DeepCloner.Clone(data)!;
                }

                // Update modified items
                foreach (var (key, data) in modifiedItems)
                {
                    _storedData[key] = DeepCloner.Clone(data)!;
                }

                // Delete removed items
                foreach (var key in deletedKeys)
                {
                    _storedData.Remove(key);
                }

                SaveCount++;
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 테스트용 EditableAssetRepository 구현
        /// </summary>
        private class TestAssetRepository : EditableAssetRepository<TestGraphData>
        {
            private readonly Dictionary<AssetId, (Asset<TestGraphData> asset, string filePath)> _storedAssets = new();
            private readonly Dictionary<string, AssetId> _pathToId = new();
            public int SaveCount { get; private set; }
            public List<AssetId> DeletedAssets { get; } = new();

            public void AddStoredAsset(Asset<TestGraphData> asset, string filePath)
            {
                _storedAssets[asset.Id] = (asset, filePath);
                _pathToId[filePath] = asset.Id;
            }

            protected override Task<IEnumerable<AssetSummary>> LoadSummariesAsync()
            {
                var summaries = _storedAssets.Values.Select(x =>
                    new AssetSummary(x.asset.Id, x.asset.Metadata, x.filePath));
                return Task.FromResult(summaries);
            }

            protected override Task<Asset<TestGraphData>?> LoadAssetAsync(AssetId id)
            {
                if (_storedAssets.TryGetValue(id, out var data))
                {
                    // Return a deep clone
                    var clonedData = DeepCloner.Clone(data.asset.Data);
                    var clonedAsset = new Asset<TestGraphData>(
                        data.asset.Id,
                        DeepCloner.Clone(data.asset.Metadata)!,
                        clonedData!,
                        data.filePath);
                    return Task.FromResult<Asset<TestGraphData>?>(clonedAsset);
                }
                return Task.FromResult<Asset<TestGraphData>?>(null);
            }

            protected override Task SaveAssetAsync(Asset<TestGraphData> asset)
            {
                var filePath = asset.FilePath;
                _storedAssets[asset.Id] = (new Asset<TestGraphData>(
                    asset.Id,
                    DeepCloner.Clone(asset.Metadata)!,
                    DeepCloner.Clone(asset.Data)!,
                    filePath), filePath);
                _pathToId[filePath] = asset.Id;
                SaveCount++;
                return Task.CompletedTask;
            }

            protected override Task DeleteAssetAsync(AssetId id)
            {
                if (_storedAssets.TryGetValue(id, out var assetData))
                {
                    _pathToId.Remove(assetData.filePath);
                }
                _storedAssets.Remove(id);
                DeletedAssets.Add(id);
                return Task.CompletedTask;
            }
        }

        #endregion

        #region EditableSingleRepository Tests

        [Fact]
        public async Task SingleRepository_InitializeAsync_LoadsData()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Test", MaxPlayers = 10 });

            // Act
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.IsInitialized);
            var current = await repo.GetAsync();
            Assert.NotNull(current);
            Assert.Equal("Test", current.Name);
        }

        [Fact]
        public async Task SingleRepository_Set_TracksChanges()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Original", MaxPlayers = 5 });
            await repo.InitializeAsync();

            // Act
            repo.Set(new TestConfig { Name = "Updated", MaxPlayers = 10 });

            // Assert
            Assert.True(repo.HasChanges);
            Assert.Equal("Updated", repo.Current?.Name);
            Assert.Equal("Original", repo.Baseline?.Name);
        }

        [Fact]
        public async Task SingleRepository_TrackPropertyChange_TracksModifiedProperties()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Test", MaxPlayers = 5, IsDebugMode = false });
            await repo.InitializeAsync();

            // Act
            repo.TrackPropertyChange("MaxPlayers", 10);

            // Assert
            Assert.True(repo.HasChanges);
            Assert.True(repo.IsPropertyModified("MaxPlayers"));
            Assert.False(repo.IsPropertyModified("Name"));
            Assert.Contains("MaxPlayers", repo.GetModifiedProperties());
        }

        [Fact]
        public async Task SingleRepository_RevertProperty_RestoresBaseline()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Test", MaxPlayers = 5 });
            await repo.InitializeAsync();

            repo.TrackPropertyChange("MaxPlayers", 10);
            Assert.True(repo.IsPropertyModified("MaxPlayers"));

            // Act
            repo.RevertProperty("MaxPlayers");

            // Assert
            Assert.False(repo.IsPropertyModified("MaxPlayers"));
            Assert.Equal(5, repo.GetPropertyBaseline("MaxPlayers"));
        }

        [Fact]
        public async Task SingleRepository_Revert_RestoresAllToBaseline()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Original", MaxPlayers = 5 });
            await repo.InitializeAsync();

            repo.Set(new TestConfig { Name = "Changed", MaxPlayers = 99 });

            // Act
            repo.Revert();

            // Assert
            Assert.False(repo.HasChanges);
            Assert.Equal("Original", repo.Current?.Name);
        }

        [Fact]
        public async Task SingleRepository_SaveAsync_PersistsChanges()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Original", MaxPlayers = 5 });
            await repo.InitializeAsync();

            repo.Set(new TestConfig { Name = "NewValue", MaxPlayers = 20 });

            // Act
            await repo.SaveAsync();

            // Assert
            Assert.False(repo.HasChanges);
            Assert.Equal(1, repo.SaveCount);
            Assert.Equal("NewValue", repo.Baseline?.Name);
        }

        #endregion

        #region EditableTableRepository Tests

        [Fact]
        public async Task TableRepository_InitializeAsync_LoadsAllData()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            repo.AddStoredData(new TestItem { Id = "item2", Name = "Shield", Price = 150 });

            // Act
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.IsInitialized);
            Assert.Equal(2, repo.Count);
            Assert.Contains("item1", repo.Keys);
            Assert.Contains("item2", repo.Keys);
        }

        [Fact]
        public async Task TableRepository_Add_TracksAsAdded()
        {
            // Arrange
            var repo = new TestTableRepository();
            await repo.InitializeAsync();

            // Act
            repo.Add(new TestItem { Id = "new1", Name = "Potion", Price = 50 });

            // Assert
            Assert.True(repo.HasChanges);
            Assert.Equal(ChangeState.Added, repo.GetState("new1"));
            Assert.Contains("new1", repo.GetAddedKeys());
        }

        [Fact]
        public async Task TableRepository_Update_TracksAsModified()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            await repo.InitializeAsync();

            // Act
            repo.Update("item1", new TestItem { Id = "item1", Name = "Epic Sword", Price = 500 });

            // Assert
            Assert.True(repo.HasChanges);
            Assert.Equal(ChangeState.Modified, repo.GetState("item1"));
            Assert.Contains("item1", repo.GetModifiedKeys());
        }

        [Fact]
        public async Task TableRepository_Remove_TracksAsDeleted()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            await repo.InitializeAsync();

            // Act
            repo.Remove("item1");

            // Assert
            Assert.True(repo.HasChanges);
            Assert.Equal(ChangeState.Deleted, repo.GetState("item1"));
            Assert.Contains("item1", repo.GetDeletedKeys());
        }

        [Fact]
        public async Task TableRepository_GetWorkingCopy_ReturnsMutableCopy()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            await repo.InitializeAsync();

            // Act
            var workingCopy = repo.GetWorkingCopy("item1");
            workingCopy.Price = 200;
            repo.MarkAsModified("item1");

            // Assert
            Assert.True(repo.HasChanges);
            var baseline = repo.GetBaseline<TestItem>("item1");
            Assert.Equal(100, baseline?.Price); // Baseline unchanged
        }

        [Fact]
        public async Task TableRepository_PropertyLevelTracking_Works()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            await repo.InitializeAsync();

            // Act
            repo.TrackPropertyChange("item1", "Price", 200);

            // Assert
            Assert.True(repo.IsPropertyModified("item1", "Price"));
            Assert.False(repo.IsPropertyModified("item1", "Name"));
            Assert.Equal(100, repo.GetPropertyBaseline("item1", "Price"));
        }

        [Fact]
        public async Task TableRepository_Revert_RestoresItem()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            await repo.InitializeAsync();

            repo.Update("item1", new TestItem { Id = "item1", Name = "Changed", Price = 999 });

            // Act
            repo.Revert("item1");

            // Assert
            Assert.Equal(ChangeState.Unchanged, repo.GetState("item1"));
            var item = repo.TryGetLoaded("item1");
            Assert.Equal("Sword", item?.Name);
        }

        [Fact]
        public async Task TableRepository_SaveAsync_PersistsAllChanges()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            await repo.InitializeAsync();

            repo.Add(new TestItem { Id = "item2", Name = "Shield", Price = 150 });
            repo.Update("item1", new TestItem { Id = "item1", Name = "Epic Sword", Price = 500 });

            // Act
            await repo.SaveAsync();

            // Assert
            Assert.False(repo.HasChanges);
            Assert.Equal(1, repo.SaveCount);
        }

        [Fact]
        public async Task TableRepository_GetAllAsync_ReturnsAllLoaded()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "item1", Name = "Sword", Price = 100 });
            repo.AddStoredData(new TestItem { Id = "item2", Name = "Shield", Price = 150 });
            await repo.InitializeAsync();

            // Act
            var all = await repo.GetAllAsync();

            // Assert
            Assert.Equal(2, all.Count);
        }

        [Fact]
        public async Task TableRepository_FindAsync_FiltersCorrectly()
        {
            // Arrange
            var repo = new TestTableRepository();
            repo.AddStoredData(new TestItem { Id = "cheap", Name = "Potion", Price = 10 });
            repo.AddStoredData(new TestItem { Id = "expensive", Name = "Sword", Price = 500 });
            await repo.InitializeAsync();

            // Act
            var expensive = await repo.FindAsync(item => item.Price > 100);

            // Assert
            Assert.Single(expensive);
            Assert.Equal("Sword", expensive.First().Name);
        }

        #endregion

        #region EditableAssetRepository Tests

        [Fact]
        public async Task AssetRepository_InitializeAsync_LoadsSummaries()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            var asset1 = new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1, DisplayName = "Graph1" },
                new TestGraphData { Title = "Test Graph", NodeCount = 5 },
                "graphs/test.json");
            repo.AddStoredAsset(asset1, "graphs/test.json");

            // Act
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.IsInitialized);
            Assert.Equal(1, repo.Count);
            Assert.Single(repo.Summaries);
        }

        [Fact]
        public async Task AssetRepository_GetAsync_LazyLoadsAsset()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            var asset1 = new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1, DisplayName = "Graph1" },
                new TestGraphData { Title = "Test Graph", NodeCount = 5 },
                "graphs/test.json");
            repo.AddStoredAsset(asset1, "graphs/test.json");
            await repo.InitializeAsync();

            // Act
            var loaded = await repo.GetAsync(id1);

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal("Test Graph", loaded.Data.Title);
            Assert.True(repo.IsLoaded(id1));
        }

        [Fact]
        public async Task AssetRepository_Add_CreatesNewAsset()
        {
            // Arrange
            var repo = new TestAssetRepository();
            await repo.InitializeAsync();

            // Act
            var asset = repo.Add(
                new TestGraphData { Title = "New Graph", NodeCount = 10 },
                "graphs/new.json");

            // Assert
            Assert.True(repo.HasChanges);
            Assert.True(asset.Id.IsValid);
            Assert.Equal(ChangeState.Added, repo.GetState(asset.Id));
        }

        [Fact]
        public async Task AssetRepository_Update_TracksAsModified()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            var asset1 = new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1 },
                new TestGraphData { Title = "Original", NodeCount = 5 },
                "graphs/test.json");
            repo.AddStoredAsset(asset1, "graphs/test.json");
            await repo.InitializeAsync();
            await repo.GetAsync(id1); // Load it first

            // Act
            repo.Update(id1, new TestGraphData { Title = "Updated", NodeCount = 20 });

            // Assert
            Assert.True(repo.HasChanges);
            Assert.Equal(ChangeState.Modified, repo.GetState(id1));
        }

        [Fact]
        public async Task AssetRepository_Remove_TracksAsDeleted()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            var asset1 = new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1 },
                new TestGraphData { Title = "ToDelete", NodeCount = 1 },
                "graphs/delete.json");
            repo.AddStoredAsset(asset1, "graphs/delete.json");
            await repo.InitializeAsync();
            await repo.GetAsync(id1); // Load to establish baseline before removing

            // Act
            var removed = repo.Remove(id1);

            // Assert
            Assert.True(removed);
            Assert.True(repo.HasChanges);
            Assert.Equal(ChangeState.Deleted, repo.GetState(id1));
        }

        [Fact]
        public async Task AssetRepository_SaveAsync_PersistsIndividualAsset()
        {
            // Arrange
            var repo = new TestAssetRepository();
            await repo.InitializeAsync();

            var asset = repo.Add(
                new TestGraphData { Title = "NewGraph", NodeCount = 3 },
                "graphs/new.json");

            // Act
            await repo.SaveAsync(asset.Id);

            // Assert
            Assert.Equal(ChangeState.Unchanged, repo.GetState(asset.Id));
            Assert.Equal(1, repo.SaveCount);
        }

        [Fact]
        public async Task AssetRepository_GetByPathAsync_ReturnsCorrectAsset()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            var asset1 = new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1 },
                new TestGraphData { Title = "PathTest", NodeCount = 7 },
                "graphs/specific.json");
            repo.AddStoredAsset(asset1, "graphs/specific.json");
            await repo.InitializeAsync();

            // Act
            var loaded = await repo.GetByPathAsync("graphs/specific.json");

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal("PathTest", loaded.Data.Title);
        }

        [Fact]
        public async Task AssetRepository_FindAsync_FiltersBySummary()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            var id2 = AssetId.NewId();

            repo.AddStoredAsset(new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1, Category = "Tutorial" },
                new TestGraphData { Title = "Tutorial1", NodeCount = 2 },
                "graphs/tutorial1.json"), "graphs/tutorial1.json");

            repo.AddStoredAsset(new Asset<TestGraphData>(
                id2,
                new AssetMetadata { Guid = id2, Category = "Main" },
                new TestGraphData { Title = "Main1", NodeCount = 10 },
                "graphs/main1.json"), "graphs/main1.json");

            await repo.InitializeAsync();

            // Act
            var tutorials = await repo.FindAsync(s => s.Category == "Tutorial");

            // Assert
            Assert.Single(tutorials);
            Assert.Equal("Tutorial1", tutorials.First().Data.Title);
        }

        [Fact]
        public async Task AssetRepository_GetWorkingCopy_ReturnsMutableCopy()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            repo.AddStoredAsset(new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1 },
                new TestGraphData { Title = "Original", NodeCount = 5 },
                "graphs/test.json"), "graphs/test.json");
            await repo.InitializeAsync();
            await repo.GetAsync(id1);

            // Act
            var workingCopy = repo.GetWorkingCopy(id1);
            workingCopy.Data.NodeCount = 99;
            repo.MarkAsModified(id1);

            // Assert
            Assert.True(repo.HasChanges);
            var baseline = repo.GetBaseline<Asset<TestGraphData>>(id1);
            Assert.Equal(5, baseline?.Data.NodeCount); // Baseline unchanged
        }

        [Fact]
        public async Task AssetRepository_UpdateMetadata_ModifiesMetadata()
        {
            // Arrange
            var repo = new TestAssetRepository();
            var id1 = AssetId.NewId();
            repo.AddStoredAsset(new Asset<TestGraphData>(
                id1,
                new AssetMetadata { Guid = id1, DisplayName = "Original Name" },
                new TestGraphData { Title = "Test", NodeCount = 1 },
                "graphs/test.json"), "graphs/test.json");
            await repo.InitializeAsync();
            await repo.GetAsync(id1);

            // Act
            repo.UpdateMetadata(id1, meta => meta.DisplayName = "Updated Name");

            // Assert
            Assert.True(repo.HasChanges);
            var workingCopy = repo.GetWorkingCopy(id1);
            Assert.Equal("Updated Name", workingCopy.Metadata.DisplayName);
        }

        #endregion

        #region Event Tests

        [Fact]
        public async Task SingleRepository_OnModifiedStateChanged_FiresOnChange()
        {
            // Arrange
            var repo = new TestSingleRepository();
            repo.SetStoredData(new TestConfig { Name = "Test", MaxPlayers = 5 });
            await repo.InitializeAsync();

            bool eventFired = false;
            bool? hasChangesValue = null;
            repo.OnModifiedStateChanged += (hasChanges) =>
            {
                eventFired = true;
                hasChangesValue = hasChanges;
            };

            // Act
            repo.Set(new TestConfig { Name = "Changed", MaxPlayers = 10 });

            // Assert
            Assert.True(eventFired);
            Assert.True(hasChangesValue);
        }

        [Fact]
        public async Task TableRepository_OnModifiedStateChanged_FiresOnAdd()
        {
            // Arrange
            var repo = new TestTableRepository();
            await repo.InitializeAsync();

            bool eventFired = false;
            repo.OnModifiedStateChanged += (_) => eventFired = true;

            // Act
            repo.Add(new TestItem { Id = "new", Name = "Test", Price = 100 });

            // Assert
            Assert.True(eventFired);
        }

        #endregion
    }
}
