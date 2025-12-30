using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Integration tests for Datra Editor with AssetRepository.
    /// Tests the full flow from loading to change tracking.
    /// </summary>
    public class DatraEditorAssetIntegrationTests
    {
        private const string SampleDataBasePath = "Packages/com.penspanic.datra.sampledata/Resources";

        [UnityTest]
        public IEnumerator CreateChangeTrackerForAssetRepository_CreatesValidTracker()
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

            // Act - Simulate what DatraEditorWindow.CreateChangeTrackerForRepository does
            var repository = context.ScriptAsset;
            var dataType = typeof(ScriptAssetData);
            var tracker = CreateChangeTrackerForAssetRepository(repository, dataType);

            // Assert
            Assert.IsNotNull(tracker, "Should create a valid tracker");
            Assert.IsFalse(tracker.HasModifications, "Should have no modifications initially");

            Debug.Log($"Successfully created change tracker for AssetRepository<{dataType.Name}>");
        }

        [UnityTest]
        public IEnumerator AssetRepository_ChangeTracking_AddAndDelete()
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

            // Create tracker
            var repository = context.ScriptAsset;
            var tracker = CreateChangeTrackerForAssetRepository(repository, typeof(ScriptAssetData));
            Assert.IsNotNull(tracker);

            // Act 1 - Add a new asset
            var newAssetId = AssetId.NewId();
            var newAsset = new Asset<ScriptAssetData>(newAssetId, new ScriptAssetData { Name = "Test" });
            tracker.TrackAdd(newAssetId, newAsset);

            Assert.IsTrue(tracker.HasModifications, "Should detect added asset");
            Assert.IsTrue(tracker.IsAdded(newAssetId), "New asset should be marked as added");

            // Act 2 - Revert
            ((IRepositoryChangeTracker)tracker).RevertAll();

            // Assert 2 - Should have no modifications after revert
            Assert.IsFalse(tracker.HasModifications, "Should have no modifications after revert");

            Debug.Log("Add and revert tracking test passed!");
        }

        [UnityTest]
        public IEnumerator AssetRepository_MultipleOperations_TracksCorrectly()
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

            var repository = context.ScriptAsset;
            if (repository.Count < 1)
            {
                Debug.LogWarning("Need at least 1 asset for this test, skipping...");
                yield break;
            }

            var tracker = CreateChangeTrackerForAssetRepository(repository, typeof(ScriptAssetData));
            Assert.IsNotNull(tracker);

            // Get existing asset
            var existingAsset = repository.Values.First();
            var existingId = existingAsset.Id;

            // Act - Add new asset and delete existing one
            var newAssetId = AssetId.NewId();
            var newAsset = new Asset<ScriptAssetData>(newAssetId, new ScriptAssetData { Name = "New" });

            tracker.TrackAdd(newAssetId, newAsset);
            tracker.TrackDelete(existingId);

            // Assert
            Assert.IsTrue(tracker.HasModifications, "Should have modifications");
            Assert.IsTrue(tracker.IsAdded(newAssetId), "New asset should be added");
            Assert.IsTrue(tracker.IsDeleted(existingId), "Existing asset should be deleted");

            var addedKeys = tracker.GetAddedKeys().ToList();
            var deletedKeys = tracker.GetDeletedKeys().ToList();

            Assert.AreEqual(1, addedKeys.Count, "Should have 1 added key");
            Assert.AreEqual(1, deletedKeys.Count, "Should have 1 deleted key");

            Debug.Log("Multiple operations tracking test passed!");
        }

        /// <summary>
        /// Helper method that mimics DatraEditorWindow.CreateChangeTrackerForRepository for IAssetRepository
        /// </summary>
        private RepositoryChangeTracker<AssetId, Asset<ScriptAssetData>> CreateChangeTrackerForAssetRepository(
            IAssetRepository<ScriptAssetData> repository,
            Type dataType)
        {
            try
            {
                // Create tracker with concrete types
                var tracker = new RepositoryChangeTracker<AssetId, Asset<ScriptAssetData>>();

                // Initialize baseline from repository
                tracker.InitializeBaseline(repository);

                return tracker;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create change tracker: {e.Message}");
                return null;
            }
        }
    }
}
