using System.Collections;
using System.Linq;
using Datra;
using Datra.DataTypes;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Unity.Editor.Providers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for AssetRepository change tracking functionality.
    /// Verifies that IAssetRepository<T> can be tracked for modifications.
    /// </summary>
    public class AssetRepositoryChangeTrackingTests
    {
        private const string SampleDataBasePath = "Packages/com.penspanic.datra.sampledata/Resources";

        [UnityTest]
        public IEnumerator AssetRepository_CanLoadScriptAssets()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Act
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            // Assert
            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            Assert.Greater(context.ScriptAsset.Count, 0, "Should have loaded at least one script asset");
            Debug.Log($"Loaded {context.ScriptAsset.Count} script assets");
        }

        [UnityTest]
        public IEnumerator EditableAssetDataSource_CanTrackAssetRepository()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Load data
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            // Act - Create editable data source for AssetRepository
            // Cast to IAssetRepository (AssetRepository implements both interfaces)
            var editableRepo = context.ScriptAsset as IAssetRepository<ScriptAssetData>;
            Assert.IsNotNull(editableRepo, "ScriptAsset should implement IAssetRepository");

            // Create data source and initialize (loads all assets)
            var dataSource = new EditableAssetDataSource<ScriptAssetData>(editableRepo);

            var initTask = dataSource.InitializeAsync();
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            if (initTask.IsFaulted)
            {
                Assert.Fail($"InitializeAsync failed: {initTask.Exception?.InnerException?.Message ?? initTask.Exception?.Message}");
            }

            // Assert
            Assert.IsFalse(dataSource.HasModifications, "Should have no modifications initially");
            Assert.AreEqual(context.ScriptAsset.Count, dataSource.EnumerateItems().Count());
            Debug.Log($"EditableAssetDataSource initialized with {context.ScriptAsset.Count} assets");
        }

        [UnityTest]
        public IEnumerator EditableAssetDataSource_TracksAddedAssets()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Load data
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            // Create editable data source
            var editableRepo = context.ScriptAsset as IAssetRepository<ScriptAssetData>;
            Assert.IsNotNull(editableRepo, "ScriptAsset should implement IAssetRepository");

            var dataSource = new EditableAssetDataSource<ScriptAssetData>(editableRepo);

            // Initialize to load all assets
            var initTask = dataSource.InitializeAsync();
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            var initialCount = dataSource.EnumerateItems().Count();

            // Act - Add a new asset
            var newData = new ScriptAssetData { Name = "New Script", Description = "Test", Version = 1 };
            var newAsset = dataSource.AddNew(newData, "test_new_script.json");

            // Assert
            Assert.IsTrue(dataSource.HasModifications, "Should detect added asset");
            Assert.AreEqual(initialCount + 1, dataSource.EnumerateItems().Count(), "Should have one more item");
            Assert.AreEqual(ItemState.Added, dataSource.GetItemState(newAsset.Id), "Asset should be in Added state");

            Debug.Log($"Added asset tracking works. Added key: {newAsset.Id}");
        }

        [UnityTest]
        public IEnumerator EditableAssetDataSource_TracksDeletedAssets()
        {
            // Arrange
            var provider = new AssetDatabaseRawDataProvider(basePath: SampleDataBasePath);
            var context = new GameDataContext(provider);

            // Load data
            var loadTask = context.LoadAllAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }

            if (loadTask.IsFaulted)
            {
                Assert.Fail($"LoadAllAsync failed: {loadTask.Exception?.InnerException?.Message ?? loadTask.Exception?.Message}");
            }

            // Create editable data source
            var editableRepo = context.ScriptAsset as IAssetRepository<ScriptAssetData>;
            Assert.IsNotNull(editableRepo, "ScriptAsset should implement IAssetRepository");

            var dataSource = new EditableAssetDataSource<ScriptAssetData>(editableRepo);

            // Initialize to load all assets
            var initTask = dataSource.InitializeAsync();
            while (!initTask.IsCompleted)
            {
                yield return null;
            }

            var initialCount = dataSource.EnumerateItems().Count();

            // Get first asset's ID
            var firstAsset = dataSource.EnumerateItems().First().Value;
            var assetId = firstAsset.Id;

            // Act - Delete the asset
            dataSource.Delete(assetId);

            // Assert
            Assert.IsTrue(dataSource.HasModifications, "Should detect deleted asset");
            Assert.AreEqual(initialCount - 1, dataSource.EnumerateItems().Count(), "Should have one less item");
            Assert.AreEqual(ItemState.Deleted, dataSource.GetItemState(assetId), "Asset should be in Deleted state");

            Debug.Log($"Deleted asset tracking works. Deleted key: {assetId}");
        }

        [Test]
        public void IAssetRepository_HasLoadedAssetsProperty()
        {
            // Verify that IAssetRepository<T> has LoadedAssets property that returns IReadOnlyDictionary
            var repoType = typeof(IAssetRepository<ScriptAssetData>);
            var loadedAssetsProp = repoType.GetProperty("LoadedAssets");

            Assert.IsNotNull(loadedAssetsProp,
                "IAssetRepository<T> should have LoadedAssets property");

            var propType = loadedAssetsProp.PropertyType;
            Assert.IsTrue(propType.IsGenericType, "LoadedAssets should be a generic type");
            Assert.AreEqual(typeof(System.Collections.Generic.IReadOnlyDictionary<,>), propType.GetGenericTypeDefinition(),
                "LoadedAssets should be IReadOnlyDictionary<AssetId, Asset<T>>");

            var genericArgs = propType.GetGenericArguments();
            Assert.AreEqual(typeof(AssetId), genericArgs[0], "Key type should be AssetId");
            Assert.AreEqual(typeof(Asset<ScriptAssetData>), genericArgs[1], "Value type should be Asset<T>");

            Debug.Log($"IAssetRepository<T> correctly has LoadedAssets property of type {propType.Name}");
        }
    }
}
