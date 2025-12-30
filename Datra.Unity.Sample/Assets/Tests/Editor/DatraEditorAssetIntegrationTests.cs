using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Integration tests for Datra Editor with AssetRepository.
    /// Tests the full flow from loading to change tracking.
    /// </summary>
    public class DatraEditorAssetIntegrationTests
    {
        private const string SampleDataBasePath = "Packages/com.penspanic.datra.sampledata/Resources";

        [UnityTest]
        public IEnumerator CreateDataSourceForAssetRepository_CreatesValidDataSource()
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

            // Act - Create EditableAssetDataSource for the repository
            var editableRepo = context.ScriptAsset as IEditableAssetRepository<ScriptAssetData>;
            Assert.IsNotNull(editableRepo, "ScriptAsset should implement IEditableAssetRepository");

            var dataSource = new EditableAssetDataSource<ScriptAssetData>(editableRepo);

            // Assert
            Assert.IsNotNull(dataSource, "Should create a valid data source");
            Assert.IsFalse(dataSource.HasModifications, "Should have no modifications initially");
            Assert.AreEqual(context.ScriptAsset.Count, dataSource.EnumerateItems().Count());

            Debug.Log($"Successfully created EditableAssetDataSource for AssetRepository<ScriptAssetData>");
        }

        [UnityTest]
        public IEnumerator AssetRepository_ChangeTracking_AddAndRevert()
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

            // Create data source
            var editableRepo = context.ScriptAsset as IEditableAssetRepository<ScriptAssetData>;
            Assert.IsNotNull(editableRepo, "ScriptAsset should implement IEditableAssetRepository");

            var dataSource = new EditableAssetDataSource<ScriptAssetData>(editableRepo);
            Assert.IsNotNull(dataSource);

            var initialCount = dataSource.EnumerateItems().Count();

            // Act 1 - Add a new asset
            var newAsset = dataSource.AddNew(new ScriptAssetData { Name = "Test" }, "test.json");

            Assert.IsTrue(dataSource.HasModifications, "Should detect added asset");
            Assert.AreEqual(ItemState.Added, dataSource.GetItemState(newAsset.Id), "New asset should be in Added state");
            Assert.AreEqual(initialCount + 1, dataSource.EnumerateItems().Count());

            // Act 2 - Revert
            dataSource.Revert();

            // Assert 2 - Should have no modifications after revert
            Assert.IsFalse(dataSource.HasModifications, "Should have no modifications after revert");
            Assert.AreEqual(initialCount, dataSource.EnumerateItems().Count());

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

            var editableRepo = context.ScriptAsset as IEditableAssetRepository<ScriptAssetData>;
            Assert.IsNotNull(editableRepo, "ScriptAsset should implement IEditableAssetRepository");

            if (context.ScriptAsset.Count < 1)
            {
                Debug.LogWarning("Need at least 1 asset for this test, skipping...");
                yield break;
            }

            var dataSource = new EditableAssetDataSource<ScriptAssetData>(editableRepo);
            Assert.IsNotNull(dataSource);

            // Get existing asset
            var existingAsset = dataSource.EnumerateItems().First().Value;
            var existingId = existingAsset.Id;
            var initialCount = dataSource.EnumerateItems().Count();

            // Act - Add new asset and delete existing one
            var newAsset = dataSource.AddNew(new ScriptAssetData { Name = "New" }, "new.json");
            dataSource.Delete(existingId);

            // Assert
            Assert.IsTrue(dataSource.HasModifications, "Should have modifications");
            Assert.AreEqual(ItemState.Added, dataSource.GetItemState(newAsset.Id), "New asset should be in Added state");
            Assert.AreEqual(ItemState.Deleted, dataSource.GetItemState(existingId), "Existing asset should be in Deleted state");

            // Count should be same (one added, one deleted)
            Assert.AreEqual(initialCount, dataSource.EnumerateItems().Count());

            Debug.Log("Multiple operations tracking test passed!");
        }
    }
}
