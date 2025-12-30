using System.Collections;
using System.Linq;
using Datra.DataTypes;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Unity.Editor.Providers;
using Datra.Unity.Editor.Utilities;
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
        public IEnumerator RepositoryChangeTracker_CanTrackAssetRepository()
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

            // Act - Create change tracker for AssetRepository
            var tracker = new RepositoryChangeTracker<AssetId, Asset<ScriptAssetData>>();

            // Initialize baseline from repository (IAssetRepository implements IReadOnlyDictionary)
            tracker.InitializeBaseline(context.ScriptAsset);

            // Assert
            Assert.IsFalse(tracker.HasModifications, "Should have no modifications initially");
            Debug.Log($"Change tracker initialized with {context.ScriptAsset.Count} assets");
        }

        [UnityTest]
        public IEnumerator RepositoryChangeTracker_TracksAddedAssets()
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

            // Create change tracker
            var tracker = new RepositoryChangeTracker<AssetId, Asset<ScriptAssetData>>();
            tracker.InitializeBaseline(context.ScriptAsset);

            // Act - Track a new asset being added
            var newAssetId = AssetId.NewId();
            var newData = new ScriptAssetData { Name = "New Script", Description = "Test", Version = 1 };
            var newAsset = new Asset<ScriptAssetData>(newAssetId, newData);

            tracker.TrackAdd(newAssetId, newAsset);

            // Assert
            Assert.IsTrue(tracker.HasModifications, "Should detect added asset");
            Assert.IsTrue(tracker.IsAdded(newAssetId), "Asset should be marked as added");
            Assert.Contains(newAssetId, tracker.GetAddedKeys().ToList());

            Debug.Log($"Added asset tracking works. Added key: {newAssetId}");
        }

        [UnityTest]
        public IEnumerator RepositoryChangeTracker_TracksDeletedAssets()
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

            // Create change tracker
            var tracker = new RepositoryChangeTracker<AssetId, Asset<ScriptAssetData>>();
            tracker.InitializeBaseline(context.ScriptAsset);

            // Get first asset's ID
            var firstAsset = context.ScriptAsset.Values.First();
            var assetId = firstAsset.Id;

            // Act - Track asset deletion
            tracker.TrackDelete(assetId);

            // Assert
            Assert.IsTrue(tracker.HasModifications, "Should detect deleted asset");
            Assert.IsTrue(tracker.IsDeleted(assetId), "Asset should be marked as deleted");
            Assert.Contains(assetId, tracker.GetDeletedKeys().ToList());

            Debug.Log($"Deleted asset tracking works. Deleted key: {assetId}");
        }

        [Test]
        public void IAssetRepository_ImplementsIReadOnlyDictionary()
        {
            // Verify that IAssetRepository<T> implements IReadOnlyDictionary
            var repoType = typeof(IAssetRepository<ScriptAssetData>);
            var interfaces = repoType.GetInterfaces();

            var readOnlyDictInterface = interfaces.FirstOrDefault(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(System.Collections.Generic.IReadOnlyDictionary<,>));

            Assert.IsNotNull(readOnlyDictInterface,
                "IAssetRepository<T> should implement IReadOnlyDictionary<AssetId, Asset<T>>");

            var genericArgs = readOnlyDictInterface.GetGenericArguments();
            Assert.AreEqual(typeof(AssetId), genericArgs[0], "Key type should be AssetId");
            Assert.AreEqual(typeof(Asset<ScriptAssetData>), genericArgs[1], "Value type should be Asset<T>");

            Debug.Log($"IAssetRepository<T> correctly implements {readOnlyDictInterface.Name}");
        }
    }
}
