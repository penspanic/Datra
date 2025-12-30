using System.Collections;
using System.Linq;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests.Integration
{
    /// <summary>
    /// Tests for verifying data loading in Datra Editor Window.
    /// </summary>
    public class DatraEditorLoadTests : EditorWindowTestBase
    {
        #region Helper Methods

        /// <summary>
        /// Check if a repository is an IAssetRepository
        /// </summary>
        private bool IsAssetRepository(IDataRepository repository)
        {
            if (repository == null) return false;
            var interfaces = repository.GetType().GetInterfaces();
            return interfaces.Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IAssetRepository<>));
        }

        /// <summary>
        /// Check if a repository is a table (key-value) repository (not single, not asset)
        /// </summary>
        private bool IsTableRepository(DataTypeInfo dataTypeInfo, IDataRepository repository)
        {
            return dataTypeInfo.RepositoryKind == RepositoryKind.Table;
        }

        #endregion

        [UnityTest]
        public IEnumerator NavigationPanel_ShowsDataTypeCategories()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            // Assert - Check for category labels
            yield return WaitForSeconds(0.5f);

            var hasDataTypes = window.ViewModel.DataTypes.Count > 0;
            Assert.IsTrue(hasDataTypes, "Should have data types loaded");

            Debug.Log($"Loaded {window.ViewModel.DataTypes.Count} data types");
        }

        [UnityTest]
        public IEnumerator NavigationPanel_ShowsSingleDataTypes()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();
            yield return WaitForSeconds(0.5f);

            // Assert - Check for Single Data types from SampleData
            var singleDataTypes = window.ViewModel.DataTypes
                .Where(dt => dt.RepositoryKind == RepositoryKind.Single)
                .ToList();

            Assert.Greater(singleDataTypes.Count, 0, "Should have at least one Single Data type");

            foreach (var dt in singleDataTypes)
            {
                Debug.Log($"Single Data: {dt.DataType.Name}");
            }

            var hasGameConfig = singleDataTypes.Any(dt => dt.DataType.Name == "GameConfigData");
            Assert.IsTrue(hasGameConfig, "Should have GameConfigData (Single Data)");
        }

        [UnityTest]
        public IEnumerator NavigationPanel_ShowsTableDataTypes()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();
            yield return WaitForSeconds(0.5f);

            // Assert - Check for Table Data types
            var tableDataTypes = window.ViewModel.DataTypes
                .Where(dt => dt.RepositoryKind == RepositoryKind.Table)
                .ToList();

            Assert.Greater(tableDataTypes.Count, 0, "Should have at least one Table Data type");

            foreach (var dt in tableDataTypes)
            {
                Debug.Log($"Table Data: {dt.DataType.Name}");
            }

            var hasItems = tableDataTypes.Any(dt =>
                dt.DataType.Name == "ItemData" ||
                dt.DataType.Name == "CharacterData");
            Assert.IsTrue(hasItems, "Should have ItemData or CharacterData (Table Data)");
        }

        [UnityTest]
        public IEnumerator NavigationPanel_ShowsAssetDataTypes()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();
            yield return WaitForSeconds(0.5f);

            // Assert - Check for Asset Data types from SampleData
            var assetDataTypes = window.ViewModel.DataTypes
                .Where(dt => window.Repositories.ContainsKey(dt.DataType))
                .Where(dt => IsAssetRepository(window.Repositories[dt.DataType]))
                .ToList();

            Assert.Greater(assetDataTypes.Count, 0, "Should have at least one Asset Data type");

            foreach (var dt in assetDataTypes)
            {
                Debug.Log($"Asset Data: {dt.DataType.Name}");
            }

            var hasScriptAsset = assetDataTypes.Any(dt => dt.DataType.Name == "ScriptAssetData");
            Assert.IsTrue(hasScriptAsset, "Should have ScriptAssetData (Asset Data)");
        }

        [UnityTest]
        public IEnumerator SingleDataRepository_HasData()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var gameConfigType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.DataType.Name == "GameConfigData");

            if (gameConfigType == null)
            {
                Debug.LogWarning("GameConfigData not found, skipping test");
                yield break;
            }

            var repository = window.Repositories[gameConfigType.DataType];
            Assert.IsNotNull(repository, "GameConfigData repository should exist");

            var getMethod = repository.GetType().GetMethod("Get");
            Assert.IsNotNull(getMethod, "Single repository should have Get method");

            var data = getMethod.Invoke(repository, null);
            Assert.IsNotNull(data, "GameConfigData should have data loaded");

            Debug.Log($"GameConfigData loaded: {data}");
        }

        [UnityTest]
        public IEnumerator TableDataRepository_HasMultipleItems()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var tableType = window.ViewModel.DataTypes
                .Where(dt => dt.RepositoryKind == RepositoryKind.Table)
                .FirstOrDefault(dt =>
                    dt.DataType.Name == "ItemData" || dt.DataType.Name == "CharacterData");

            if (tableType == null)
            {
                Debug.LogWarning("No table data type found, skipping test");
                yield break;
            }

            var repository = window.Repositories[tableType.DataType];
            Assert.IsNotNull(repository, $"{tableType.DataType.Name} repository should exist");

            var getAllMethod = repository.GetType().GetMethod("GetAll");
            Assert.IsNotNull(getAllMethod, "Table repository should have GetAll method");

            var data = getAllMethod.Invoke(repository, null) as System.Collections.IEnumerable;
            Assert.IsNotNull(data, $"{tableType.DataType.Name} should have data loaded");

            int count = 0;
            foreach (var item in data)
            {
                count++;
            }

            Assert.Greater(count, 0, $"{tableType.DataType.Name} should have at least one item");
            Debug.Log($"{tableType.DataType.Name} loaded: {count} items");
        }

        [UnityTest]
        public IEnumerator AssetDataRepository_HasAssets()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var assetType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.DataType.Name == "ScriptAssetData");

            if (assetType == null)
            {
                Debug.LogWarning("ScriptAssetData not found, skipping test");
                yield break;
            }

            var repository = window.Repositories[assetType.DataType];
            Assert.IsNotNull(repository, "ScriptAssetData repository should exist");

            // IAssetRepository implements IReadOnlyDictionary, check Count
            var countProperty = repository.GetType().GetProperty("Count");
            Assert.IsNotNull(countProperty, "Asset repository should have Count property");

            var count = (int)countProperty.GetValue(repository);
            Assert.Greater(count, 0, "ScriptAssetData should have at least one asset");

            Debug.Log($"ScriptAssetData loaded: {count} assets");

            // Also verify we can iterate through Values
            var valuesProperty = repository.GetType().GetProperty("Values");
            Assert.IsNotNull(valuesProperty, "Asset repository should have Values property");

            var values = valuesProperty.GetValue(repository) as System.Collections.IEnumerable;
            Assert.IsNotNull(values, "Should be able to get Values from asset repository");

            int iteratedCount = 0;
            foreach (var asset in values)
            {
                iteratedCount++;
                Debug.Log($"  Asset: {asset}");
            }

            Assert.AreEqual(count, iteratedCount, "Iterated count should match Count property");
        }

        [UnityTest]
        public IEnumerator AllRepositories_AreAccessible()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            // Assert all repositories are accessible
            foreach (var dataTypeInfo in window.ViewModel.DataTypes)
            {
                Assert.IsTrue(
                    window.Repositories.ContainsKey(dataTypeInfo.DataType),
                    $"Repository for {dataTypeInfo.DataType.Name} should be accessible");

                var repo = window.Repositories[dataTypeInfo.DataType];
                Assert.IsNotNull(repo, $"Repository for {dataTypeInfo.DataType.Name} should not be null");

                var repoKind = dataTypeInfo.RepositoryKind.ToString();
                Debug.Log($"Repository accessible: {dataTypeInfo.DataType.Name} ({repoKind})");
            }
        }
    }
}
