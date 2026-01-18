#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Repositories;
using Datra.Repositories.Runtime;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// RuntimeSingleRepository, RuntimeTableRepository, RuntimeAssetRepository 테스트
    /// 읽기 전용 Repository로서 쓰기 작업 시 NotSupportedException 발생 확인
    /// </summary>
    public class RuntimeRepositoryTests
    {
        #region Test Data Classes

        public class TestSettings
        {
            public string Language { get; set; } = "ko";
            public int Volume { get; set; } = 100;
        }

        public class TestCharacter
        {
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public int Level { get; set; }
        }

        public class TestSceneData
        {
            public string SceneName { get; set; } = "";
            public int ObjectCount { get; set; }
        }

        #endregion

        #region Test Repository Implementations

        private class TestRuntimeSingleRepository : RuntimeSingleRepository<TestSettings>
        {
            private readonly TestSettings? _data;

            public TestRuntimeSingleRepository(TestSettings? data)
            {
                _data = data;
            }

            protected override Task<TestSettings?> LoadDataAsync()
            {
                return Task.FromResult(_data != null
                    ? DeepCloner.Clone(_data)
                    : null);
            }
        }

        private class TestRuntimeTableRepository : RuntimeTableRepository<string, TestCharacter>
        {
            private readonly List<TestCharacter> _data;

            public TestRuntimeTableRepository(IEnumerable<TestCharacter> data)
            {
                _data = data.ToList();
            }

            protected override async IAsyncEnumerable<(string key, TestCharacter data)> LoadAllDataAsync()
            {
                foreach (var item in _data)
                {
                    yield return (item.Id, DeepCloner.Clone(item)!);
                }
                await Task.CompletedTask;
            }
        }

        private class TestRuntimeAssetRepository : RuntimeAssetRepository<TestSceneData>
        {
            private readonly List<(AssetId id, AssetMetadata metadata, TestSceneData data, string path)> _assets;

            public TestRuntimeAssetRepository(
                IEnumerable<(AssetId id, AssetMetadata metadata, TestSceneData data, string path)> assets)
            {
                _assets = assets.ToList();
            }

            protected override Task<IEnumerable<AssetSummary>> LoadSummariesAsync()
            {
                var summaries = _assets.Select(a =>
                    new AssetSummary(a.id, a.metadata, a.path));
                return Task.FromResult(summaries);
            }

            protected override Task<Asset<TestSceneData>?> LoadAssetAsync(AssetId id)
            {
                var found = _assets.FirstOrDefault(a => a.id == id);
                if (found.id == default)
                    return Task.FromResult<Asset<TestSceneData>?>(null);

                var asset = new Asset<TestSceneData>(
                    found.id,
                    DeepCloner.Clone(found.metadata)!,
                    DeepCloner.Clone(found.data)!,
                    found.path);
                return Task.FromResult<Asset<TestSceneData>?>(asset);
            }
        }

        #endregion

        #region RuntimeSingleRepository Tests

        [Fact]
        public async Task RuntimeSingle_InitializeAsync_LoadsData()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "en", Volume = 80 });

            // Act
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.IsInitialized);
            var current = await repo.GetAsync();
            Assert.NotNull(current);
            Assert.Equal("en", current.Language);
            Assert.Equal(80, current.Volume);
        }

        [Fact]
        public async Task RuntimeSingle_Current_ReturnsLoadedData()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "ja", Volume = 50 });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Equal("ja", repo.Current?.Language);
            Assert.Equal(50, repo.Current?.Volume);
        }

        [Fact]
        public async Task RuntimeSingle_HasChanges_AlwaysFalse()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "ko", Volume = 100 });
            await repo.InitializeAsync();

            // Assert
            Assert.False(repo.HasChanges);
        }

        [Fact]
        public async Task RuntimeSingle_Set_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "ko", Volume = 100 });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Set(new TestSettings { Language = "en", Volume = 50 }));
        }

        [Fact]
        public async Task RuntimeSingle_TrackPropertyChange_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "ko", Volume = 100 });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.TrackPropertyChange("Volume", 50));
        }

        [Fact]
        public async Task RuntimeSingle_SaveAsync_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "ko", Volume = 100 });
            await repo.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                repo.SaveAsync());
        }

        [Fact]
        public async Task RuntimeSingle_Revert_DoesNothing()
        {
            // Arrange
            var repo = new TestRuntimeSingleRepository(
                new TestSettings { Language = "ko", Volume = 100 });
            await repo.InitializeAsync();

            // Act - Should not throw
            repo.Revert();

            // Assert
            Assert.False(repo.HasChanges);
        }

        #endregion

        #region RuntimeTableRepository Tests

        [Fact]
        public async Task RuntimeTable_InitializeAsync_LoadsAllData()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 },
                new TestCharacter { Id = "hero2", Name = "Mage", Level = 15 }
            });

            // Act
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.IsInitialized);
            Assert.Equal(2, repo.Count);
            Assert.Contains("hero1", repo.Keys);
            Assert.Contains("hero2", repo.Keys);
        }

        [Fact]
        public async Task RuntimeTable_GetAsync_ReturnsData()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Act
            var character = await repo.GetAsync("hero1");

            // Assert
            Assert.NotNull(character);
            Assert.Equal("Knight", character.Name);
            Assert.Equal(10, character.Level);
        }

        [Fact]
        public async Task RuntimeTable_TryGetLoaded_ReturnsData()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Act
            var character = repo.TryGetLoaded("hero1");

            // Assert
            Assert.NotNull(character);
            Assert.Equal("Knight", character.Name);
        }

        [Fact]
        public async Task RuntimeTable_Contains_ChecksExistence()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.Contains("hero1"));
            Assert.False(repo.Contains("nonexistent"));
        }

        [Fact]
        public async Task RuntimeTable_HasChanges_AlwaysFalse()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Assert
            Assert.False(repo.HasChanges);
        }

        [Fact]
        public async Task RuntimeTable_GetState_AlwaysUnchanged()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Assert
            Assert.Equal(ChangeState.Unchanged, repo.GetState("hero1"));
            Assert.Equal(ChangeState.Unchanged, repo.GetState("nonexistent"));
        }

        [Fact]
        public async Task RuntimeTable_Add_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(Array.Empty<TestCharacter>());
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Add(new TestCharacter { Id = "new", Name = "New", Level = 1 }));
        }

        [Fact]
        public async Task RuntimeTable_Update_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Update("hero1", new TestCharacter { Id = "hero1", Name = "Changed", Level = 99 }));
        }

        [Fact]
        public async Task RuntimeTable_Remove_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Remove("hero1"));
        }

        [Fact]
        public async Task RuntimeTable_GetWorkingCopy_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 }
            });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.GetWorkingCopy("hero1"));
        }

        [Fact]
        public async Task RuntimeTable_FindAsync_FiltersCorrectly()
        {
            // Arrange
            var repo = new TestRuntimeTableRepository(new[]
            {
                new TestCharacter { Id = "hero1", Name = "Knight", Level = 10 },
                new TestCharacter { Id = "hero2", Name = "Mage", Level = 20 },
                new TestCharacter { Id = "hero3", Name = "Archer", Level = 15 }
            });
            await repo.InitializeAsync();

            // Act
            var highLevel = await repo.FindAsync(c => c.Level >= 15);

            // Assert
            Assert.Equal(2, highLevel.Count());
        }

        #endregion

        #region RuntimeAssetRepository Tests

        [Fact]
        public async Task RuntimeAsset_InitializeAsync_LoadsSummaries()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1, DisplayName = "Scene1" },
                 new TestSceneData { SceneName = "Intro", ObjectCount = 50 },
                 "scenes/intro.json")
            });

            // Act
            await repo.InitializeAsync();

            // Assert
            Assert.True(repo.IsInitialized);
            Assert.Equal(1, repo.Count);
            Assert.Single(repo.Summaries);
        }

        [Fact]
        public async Task RuntimeAsset_GetAsync_LazyLoadsAsset()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1, DisplayName = "Scene1" },
                 new TestSceneData { SceneName = "Intro", ObjectCount = 50 },
                 "scenes/intro.json")
            });
            await repo.InitializeAsync();

            Assert.False(repo.IsLoaded(id1)); // Not loaded yet

            // Act
            var asset = await repo.GetAsync(id1);

            // Assert
            Assert.NotNull(asset);
            Assert.Equal("Intro", asset.Data.SceneName);
            Assert.True(repo.IsLoaded(id1));
        }

        [Fact]
        public async Task RuntimeAsset_GetByPathAsync_ReturnsCorrectAsset()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1 },
                 new TestSceneData { SceneName = "Battle", ObjectCount = 100 },
                 "scenes/battle.json")
            });
            await repo.InitializeAsync();

            // Act
            var asset = await repo.GetByPathAsync("scenes/battle.json");

            // Assert
            Assert.NotNull(asset);
            Assert.Equal("Battle", asset.Data.SceneName);
        }

        [Fact]
        public async Task RuntimeAsset_GetSummary_ReturnsSummary()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1, DisplayName = "TestScene" },
                 new TestSceneData { SceneName = "Test", ObjectCount = 10 },
                 "scenes/test.json")
            });
            await repo.InitializeAsync();

            // Act
            var summary = repo.GetSummary(id1);

            // Assert
            Assert.NotNull(summary);
            Assert.Equal(id1, summary.Id);
            Assert.Equal("TestScene", summary.DisplayName);
        }

        [Fact]
        public async Task RuntimeAsset_HasChanges_AlwaysFalse()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1 },
                 new TestSceneData { SceneName = "Test", ObjectCount = 10 },
                 "scenes/test.json")
            });
            await repo.InitializeAsync();

            // Assert
            Assert.False(repo.HasChanges);
        }

        [Fact]
        public async Task RuntimeAsset_Add_ThrowsNotSupported()
        {
            // Arrange
            var repo = new TestRuntimeAssetRepository(Array.Empty<(AssetId, AssetMetadata, TestSceneData, string)>());
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Add(new TestSceneData { SceneName = "New", ObjectCount = 1 }, "scenes/new.json"));
        }

        [Fact]
        public async Task RuntimeAsset_Update_ThrowsNotSupported()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1 },
                 new TestSceneData { SceneName = "Test", ObjectCount = 10 },
                 "scenes/test.json")
            });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Update(id1, new TestSceneData { SceneName = "Changed", ObjectCount = 99 }));
        }

        [Fact]
        public async Task RuntimeAsset_Remove_ThrowsNotSupported()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1 },
                 new TestSceneData { SceneName = "Test", ObjectCount = 10 },
                 "scenes/test.json")
            });
            await repo.InitializeAsync();

            // Act & Assert
            Assert.Throws<NotSupportedException>(() =>
                repo.Remove(id1));
        }

        [Fact]
        public async Task RuntimeAsset_SaveAsync_ThrowsNotSupported()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1 },
                 new TestSceneData { SceneName = "Test", ObjectCount = 10 },
                 "scenes/test.json")
            });
            await repo.InitializeAsync();

            // Act & Assert
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                repo.SaveAsync(id1));
        }

        [Fact]
        public async Task RuntimeAsset_FindAsync_FiltersBySummary()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var id2 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1, Category = "Tutorial" },
                 new TestSceneData { SceneName = "Tutorial", ObjectCount = 10 },
                 "scenes/tutorial.json"),
                (id2, new AssetMetadata { Guid = id2, Category = "Main" },
                 new TestSceneData { SceneName = "Main", ObjectCount = 100 },
                 "scenes/main.json")
            });
            await repo.InitializeAsync();

            // Act
            var tutorials = await repo.FindAsync(s => s.Category == "Tutorial");

            // Assert
            Assert.Single(tutorials);
            Assert.Equal("Tutorial", tutorials.First().Data.SceneName);
        }

        [Fact]
        public async Task RuntimeAsset_LoadedAssets_ReturnsOnlyLoaded()
        {
            // Arrange
            var id1 = AssetId.NewId();
            var id2 = AssetId.NewId();
            var repo = new TestRuntimeAssetRepository(new[]
            {
                (id1, new AssetMetadata { Guid = id1 },
                 new TestSceneData { SceneName = "Scene1", ObjectCount = 10 },
                 "scenes/scene1.json"),
                (id2, new AssetMetadata { Guid = id2 },
                 new TestSceneData { SceneName = "Scene2", ObjectCount = 20 },
                 "scenes/scene2.json")
            });
            await repo.InitializeAsync();

            // Load only one
            await repo.GetAsync(id1);

            // Assert
            Assert.Equal(1, repo.LoadedAssets.Count);
            Assert.True(repo.LoadedAssets.ContainsKey(id1));
            Assert.False(repo.LoadedAssets.ContainsKey(id2));
        }

        #endregion
    }
}
