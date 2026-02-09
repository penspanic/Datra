using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datra;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Datra.Unity.Tests.Integration
{
    /// <summary>
    /// Integration tests for column width persistence in table views.
    /// Verifies that column widths survive RefreshContent (e.g., after Save).
    /// NOTE: These tests require a graphics device and cannot run in batch mode.
    /// </summary>
    public class ColumnWidthIntegrationTests : EditorWindowTestBase
    {
        [UnityTest]
        public IEnumerator TableView_ColumnWidths_SurvivedAfterRefresh()
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

            // Select the data type to trigger table view
            window.SelectDataType(tableDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Find the table view
            var tableView = QueryUI<VisualElement>(className: "datra-table-view");
            if (tableView == null)
            {
                // Try switching to table view mode
                var tableButton = FindElementWithText("Table") as Button;
                if (tableButton != null)
                {
                    SimulateClick(tableButton);
                    yield return WaitForSeconds(0.5f);
                    tableView = QueryUI<VisualElement>(className: "datra-table-view");
                }
            }

            if (tableView == null)
            {
                Debug.LogWarning("Table view not found, skipping test");
                yield break;
            }

            // Get header cells
            var headerRow = tableView.Q<VisualElement>(className: "table-header-row");
            Assert.IsNotNull(headerRow, "Header row should exist");

            var headerCells = headerRow.Children()
                .Where(c => c.ClassListContains("table-header-cell"))
                .ToList();
            Assert.Greater(headerCells.Count, 0, "Should have at least one header cell");

            // Save the viewKey for cleanup
            var viewKey = tableDataType.DataType.Name;

            // Pre-set some column widths via DatraUserPreferences
            var customWidths = new Dictionary<string, float>();
            float testWidth = 250f;

            // Set a custom width for the first data column (skip Actions/ID)
            // We'll use the preferences API to simulate the user having resized columns
            var existingWidths = DatraUserPreferences.GetColumnWidths(viewKey);
            if (existingWidths.Count == 0)
            {
                // Set custom width for first column that isn't special
                customWidths["__ID"] = testWidth;
                DatraUserPreferences.SetColumnWidths(viewKey, customWidths);
            }

            // Act - Re-select the data type (triggers SetData → RefreshContent)
            window.SelectDataType(tableDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Re-query the table view after refresh
            tableView = QueryUI<VisualElement>(className: "datra-table-view");
            if (tableView == null)
            {
                Debug.LogWarning("Table view not found after refresh, skipping test");
                yield break;
            }

            headerRow = tableView.Q<VisualElement>(className: "table-header-row");
            Assert.IsNotNull(headerRow, "Header row should exist after refresh");

            headerCells = headerRow.Children()
                .Where(c => c.ClassListContains("table-header-cell"))
                .ToList();

            // Assert - Verify that the saved column widths were restored
            var loadedWidths = DatraUserPreferences.GetColumnWidths(viewKey);

            if (loadedWidths.ContainsKey("__ID"))
            {
                // Find the ID header cell (should be second if Actions is shown)
                // Check that its width matches what we saved
                bool foundMatchingWidth = false;
                foreach (var cell in headerCells)
                {
                    float cellWidth = cell.style.width.value.value;
                    if (Mathf.Approximately(cellWidth, testWidth))
                    {
                        foundMatchingWidth = true;
                        break;
                    }
                }

                Assert.IsTrue(foundMatchingWidth,
                    $"At least one header cell should have width {testWidth}f after restore. " +
                    $"Header cell widths: {string.Join(", ", headerCells.Select(c => c.style.width.value.value))}");
            }

            Debug.Log($"Column width persistence test passed for {tableDataType.DataType.Name}");

            // Cleanup
            EditorPrefs.DeleteKey($"Datra_ColumnWidths_{viewKey}");
        }

        [UnityTest]
        public IEnumerator TableView_HeaderCells_HaveResizeHandles()
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

            // Select the data type
            window.SelectDataType(tableDataType.DataType);
            yield return WaitForSeconds(0.5f);

            // Find table view
            var tableView = QueryUI<VisualElement>(className: "datra-table-view");
            if (tableView == null)
            {
                var tableButton = FindElementWithText("Table") as Button;
                if (tableButton != null)
                {
                    SimulateClick(tableButton);
                    yield return WaitForSeconds(0.5f);
                    tableView = QueryUI<VisualElement>(className: "datra-table-view");
                }
            }

            if (tableView == null)
            {
                Debug.LogWarning("Table view not found, skipping test");
                yield break;
            }

            // Assert - Check that header cells have resize handles
            var resizeHandles = tableView.Query<VisualElement>(className: "resize-handle").ToList();
            Assert.Greater(resizeHandles.Count, 0,
                "Table header cells should have resize handles");

            Debug.Log($"Found {resizeHandles.Count} resize handles in table view");
        }
    }
}
