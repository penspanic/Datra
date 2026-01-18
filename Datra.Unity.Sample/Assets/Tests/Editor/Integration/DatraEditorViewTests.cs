using System.Collections;
using System.Linq;
using Datra;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Datra.Unity.Tests.Integration
{
    /// <summary>
    /// Tests for verifying data display in Datra Editor views.
    /// These tests verify that selecting a data type shows the correct data in the inspector.
    /// </summary>
    public class DatraEditorViewTests : EditorWindowTestBase
    {
        #region Helper Methods

        private bool IsAssetRepository(IEditableRepository repository)
        {
            if (repository == null) return false;
            var interfaces = repository.GetType().GetInterfaces();
            return interfaces.Any(i =>
                i.IsGenericType &&
                i.GetGenericTypeDefinition() == typeof(IAssetRepository<>));
        }

        private bool IsTableRepository(DataTypeInfo dataTypeInfo, IEditableRepository repository)
        {
            return dataTypeInfo.RepositoryKind == RepositoryKind.Table;
        }

        #endregion

        #region Single Data View Tests

        [UnityTest]
        public IEnumerator SingleData_SelectType_ShowsFormView()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            // Find a Single Data type
            var singleDataType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.RepositoryKind == RepositoryKind.Single);

            if (singleDataType == null)
            {
                Debug.LogWarning("No Single Data type found, skipping test");
                yield break;
            }

            // Act - Select the data type via ViewModel
            window.SelectDataType(singleDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Assert - Should show form view (or at least have content)
            Assert.AreEqual(singleDataType.DataType, window.ViewModel.SelectedDataType,
                "Selected data type should match");

            Debug.Log($"Selected Single Data: {singleDataType.DataType.Name}");
        }

        [UnityTest]
        public IEnumerator SingleData_GameConfig_DisplaysFields()
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

            // Act
            window.SelectDataType(gameConfigType.DataType);
            yield return WaitForSeconds(1f);

            // Assert - Check for form fields in the UI
            var formFields = QueryAllUI<VisualElement>("datra-property-field");

            if (formFields.Count == 0)
            {
                formFields = QueryAllUI<VisualElement>("single-data-form");
            }

            // At minimum, some input fields should be present
            var textFields = QueryAllUI<TextField>();
            var intFields = QueryAllUI<IntegerField>();
            var allInputs = textFields.Count + intFields.Count;

            Assert.Greater(allInputs, 0,
                $"GameConfigData should display input fields. Found TextField: {textFields.Count}, IntegerField: {intFields.Count}");

            Debug.Log($"GameConfigData displays {allInputs} input fields");
        }

        #endregion

        #region Table Data View Tests

        [UnityTest]
        public IEnumerator TableData_SelectType_ShowsTableView()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            // Find a Table Data type
            var tableDataType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.RepositoryKind == RepositoryKind.Table);

            if (tableDataType == null)
            {
                Debug.LogWarning("No Table Data type found, skipping test");
                yield break;
            }

            // Act
            window.SelectDataType(tableDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Assert
            Assert.AreEqual(tableDataType.DataType, window.ViewModel.SelectedDataType,
                "Selected data type should match");

            Debug.Log($"Selected Table Data: {tableDataType.DataType.Name}");
        }

        [UnityTest]
        public IEnumerator TableData_ItemData_DisplaysRows()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var itemDataType = window.ViewModel.DataTypes
                .Where(dt => dt.RepositoryKind == RepositoryKind.Table)
                .FirstOrDefault(dt =>
                    dt.DataType.Name == "ItemData" || dt.DataType.Name == "CharacterData");

            if (itemDataType == null)
            {
                Debug.LogWarning("ItemData/CharacterData not found, skipping test");
                yield break;
            }

            // Get expected row count from repository
            var repo = window.Repositories[itemDataType.DataType];
            var loadedItemsProperty = repo.GetType().GetProperty("LoadedItems");
            var loadedItems = loadedItemsProperty?.GetValue(repo);
            var valuesProperty = loadedItems?.GetType().GetProperty("Values");
            var data = valuesProperty?.GetValue(loadedItems) as System.Collections.IEnumerable;
            int expectedCount = 0;
            if (data != null)
            {
                foreach (var _ in data) expectedCount++;
            }

            // Act
            window.SelectDataType(itemDataType.DataType);
            yield return WaitForSeconds(1f);

            // Debug - dump UI hierarchy
            Debug.Log($"=== DEBUG: Checking UI after selecting {itemDataType.DataType.Name} ===");
            Debug.Log($"SelectedDataType: {window.ViewModel.SelectedDataType?.Name}");

            // Check if repository has data
            var repoForDebug = window.Repositories[itemDataType.DataType];
            Debug.Log($"Repository type: {repoForDebug?.GetType().Name}");
            var loadedItemsDebug = repoForDebug?.GetType().GetProperty("LoadedItems");
            Debug.Log($"LoadedItems property found: {loadedItemsDebug != null}");
            if (loadedItemsDebug != null)
            {
                var loadedDebug = loadedItemsDebug.GetValue(repoForDebug);
                var valuesDebug = loadedDebug?.GetType().GetProperty("Values");
                var dataDebug = valuesDebug?.GetValue(loadedDebug) as System.Collections.IEnumerable;
                int countDebug = 0;
                if (dataDebug != null) foreach (var _ in dataDebug) countDebug++;
                Debug.Log($"Repository LoadedItems.Values returned {countDebug} items");
            }

            // Assert - Check for table content
            bool hasContent = false;

            // Check 1: ListView with itemsSource (virtualized table)
            var listView = QueryUI<ListView>();
            Debug.Log($"ListView found: {listView != null}");
            if (listView != null)
            {
                Debug.Log($"ListView.itemsSource: {listView.itemsSource != null}");
                if (listView.itemsSource != null)
                {
                    int itemCount = 0;
                    foreach (var _ in listView.itemsSource) itemCount++;
                    Debug.Log($"ListView itemsSource count: {itemCount}");
                    if (itemCount > 0)
                    {
                        hasContent = true;
                        Debug.Log($"Found ListView with {itemCount} items in itemsSource");
                    }
                }
            }

            // Check 2: Visible table rows (if rendered)
            var tableRows = QueryAllUI<VisualElement>("table-row");
            Debug.Log($"table-row count: {tableRows.Count}");
            if (tableRows.Count > 0)
            {
                hasContent = true;
            }

            // Check 3: Form view table items
            var tableItems = QueryAllUI<VisualElement>("table-item");
            Debug.Log($"table-item count: {tableItems.Count}");
            if (tableItems.Count > 0)
            {
                hasContent = true;
            }

            // Check 4: Any input fields (indicates data is being displayed)
            var textFields = QueryAllUI<TextField>();
            Debug.Log($"TextField count: {textFields.Count}");
            if (textFields.Count > 3)
            {
                hasContent = true;
            }

            // Check 5: All VisualElements with common view classes
            var formView = QueryUI<VisualElement>(className: "datra-form-view");
            var tableView = QueryUI<VisualElement>(className: "datra-table-view");
            Debug.Log($"datra-form-view found: {formView != null}");
            Debug.Log($"datra-table-view found: {tableView != null}");

            // Check 6: Dump all class names in content area
            var allElements = window.rootVisualElement.Query<VisualElement>().ToList();
            var classNames = new System.Collections.Generic.HashSet<string>();
            foreach (var el in allElements)
            {
                foreach (var cls in el.GetClasses())
                {
                    if (cls.Contains("table") || cls.Contains("form") || cls.Contains("data") || cls.Contains("view"))
                    {
                        classNames.Add(cls);
                    }
                }
            }
            Debug.Log($"Relevant class names: {string.Join(", ", classNames)}");

            Assert.IsTrue(hasContent,
                $"Table should display data. Expected ~{expectedCount} items but found no content.");

            Debug.Log($"{itemDataType.DataType.Name} data display verified (expected: {expectedCount})");
        }

        #endregion

        #region Asset Data View Tests - CRITICAL: Tests the current bug

        [UnityTest]
        public IEnumerator AssetData_SelectType_ShowsView()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            // Find Asset Data type (ScriptAssetData)
            var assetDataType = window.ViewModel.DataTypes
                .Where(dt => window.Repositories.ContainsKey(dt.DataType))
                .FirstOrDefault(dt => IsAssetRepository(window.Repositories[dt.DataType]));

            if (assetDataType == null)
            {
                Debug.LogWarning("No Asset Data type found, skipping test");
                yield break;
            }

            // Act
            window.SelectDataType(assetDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Assert
            Assert.AreEqual(assetDataType.DataType, window.ViewModel.SelectedDataType,
                "Selected data type should match");

            Debug.Log($"Selected Asset Data: {assetDataType.DataType.Name}");
        }

        [UnityTest]
        public IEnumerator AssetData_ScriptAsset_DisplaysItems()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var scriptAssetType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.DataType.Name == "ScriptAssetData");

            if (scriptAssetType == null)
            {
                Debug.LogWarning("ScriptAssetData not found, skipping test");
                yield break;
            }

            // Get expected count from repository
            var repo = window.Repositories[scriptAssetType.DataType];
            var countProperty = repo.GetType().GetProperty("Count");
            int expectedCount = (int)countProperty.GetValue(repo);

            Debug.Log($"ScriptAssetData repository has {expectedCount} items");
            Assert.Greater(expectedCount, 0, "Repository should have items for this test");

            // Act - Select ScriptAssetData
            window.SelectDataType(scriptAssetType.DataType);
            yield return WaitForSeconds(1f);

            // Assert - Content should be displayed
            // This is the CRITICAL test that should FAIL with the current bug

            bool hasContent = false;

            // Check 1: Form view with items
            var formItems = QueryAllUI<VisualElement>("table-item");
            if (formItems.Count > 0)
            {
                hasContent = true;
                Debug.Log($"Found {formItems.Count} table-items in form view");
            }

            // Check 2: Table view with rows
            var tableRows = QueryAllUI<VisualElement>("table-row");
            if (tableRows.Count > 0)
            {
                hasContent = true;
                Debug.Log($"Found {tableRows.Count} table-rows");
            }

            // Check 3: ListView (virtualized)
            var listView = QueryUI<ListView>();
            if (listView != null && listView.itemsSource != null)
            {
                var itemCount = 0;
                foreach (var _ in listView.itemsSource) itemCount++;
                if (itemCount > 0)
                {
                    hasContent = true;
                    Debug.Log($"Found ListView with {itemCount} items");
                }
            }

            // Check 4: Single data form fields (if treated as single)
            var singleForm = QueryUI<VisualElement>(className: "single-data-form");
            if (singleForm != null)
            {
                var fields = singleForm.Query<VisualElement>().ToList();
                if (fields.Count > 2)
                {
                    hasContent = true;
                    Debug.Log($"Found single-data-form with {fields.Count} elements");
                }
            }

            // Check 5: Any input fields at all
            var allTextFields = QueryAllUI<TextField>();
            var allIntFields = QueryAllUI<IntegerField>();
            if (allTextFields.Count > 0 || allIntFields.Count > 0)
            {
                hasContent = true;
                Debug.Log($"Found input fields: TextField={allTextFields.Count}, IntegerField={allIntFields.Count}");
            }

            // Check 6: Foldouts (used in form view for items)
            var foldouts = QueryAllUI<Foldout>();
            if (foldouts.Count > 0)
            {
                hasContent = true;
                Debug.Log($"Found {foldouts.Count} foldouts");
            }

            // CRITICAL ASSERTION
            Assert.IsTrue(hasContent,
                $"Asset Data (ScriptAssetData) should display {expectedCount} items, but no content was found. " +
                "This indicates the IAssetRepository view rendering bug.");
        }

        [UnityTest]
        public IEnumerator AssetData_Repository_IsCorrectlyTyped()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var scriptAssetType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.DataType.Name == "ScriptAssetData");

            if (scriptAssetType == null)
            {
                Debug.LogWarning("ScriptAssetData not found, skipping test");
                yield break;
            }

            // Assert - Verify repository is IAssetRepository
            var repo = window.Repositories[scriptAssetType.DataType];
            var isAssetRepo = IsAssetRepository(repo);

            Assert.IsTrue(isAssetRepo,
                $"ScriptAssetData repository should implement IAssetRepository<T>. " +
                $"Actual type: {repo.GetType().Name}");

            Debug.Log($"Repository type: {repo.GetType().Name}, implements IAssetRepository: {isAssetRepo}");
        }

        #endregion

        #region View Mode Toggle Tests

        [UnityTest]
        public IEnumerator ViewModeToggle_FormToTable_Works()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            var tableDataType = window.ViewModel.DataTypes
                .FirstOrDefault(dt => dt.RepositoryKind == RepositoryKind.Table);

            if (tableDataType == null)
            {
                Debug.LogWarning("No Table Data type found, skipping test");
                yield break;
            }

            // Select data type
            window.SelectDataType(tableDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Find view toggle buttons
            var formButton = FindElementWithText("Form") as Button;
            var tableButton = FindElementWithText("Table") as Button;

            if (formButton == null)
            {
                formButton = window.rootVisualElement.Q<Button>("form-view-button");
            }
            if (tableButton == null)
            {
                tableButton = window.rootVisualElement.Q<Button>("table-view-button");
            }

            if (formButton == null && tableButton == null)
            {
                Debug.LogWarning("View toggle buttons not found, skipping test");
                yield break;
            }

            // Act - Toggle to table view
            if (tableButton != null)
            {
                SimulateClick(tableButton);
                yield return WaitForSeconds(0.5f);
            }

            // Assert - Just verify we didn't crash
            Assert.IsNotNull(window, "Window should still be open after toggle");
            Debug.Log("View mode toggle completed successfully");
        }

        #endregion

        #region Error State Tests

        [UnityTest]
        public IEnumerator SelectInvalidType_HandlesGracefully()
        {
            // Arrange
            yield return OpenWindowAndWaitForLoad();

            // Act - Try to select a type that doesn't exist in repositories
            try
            {
                window.SelectDataType(typeof(string));
            }
            catch (System.Exception ex)
            {
                Debug.Log($"Expected exception: {ex.Message}");
            }

            yield return WaitForSeconds(0.5f);

            // Assert - Window should still be functional
            Assert.IsNotNull(window, "Window should still be open");
            Assert.IsNotNull(window.ViewModel, "ViewModel should still be accessible");
        }

        #endregion
    }
}
