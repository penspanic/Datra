using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Views
{
    /// <summary>
    /// Base class for virtualized table views with search debouncing and filtering
    /// </summary>
    public abstract class VirtualizedTableView : DatraDataView
    {
        // Virtualization
        protected ListView listView;
        protected List<object> allItems;
        protected List<object> filteredItems;

        // Search
        protected SearchDebouncer searchDebouncer;
        protected string currentSearchTerm = "";

        // UI
        protected VisualElement tableContainer;
        protected VisualElement headerRow;
        protected ToolbarSearchField toolbarSearchField;

        // Constants
        protected virtual float RowHeight => 28f;
        protected virtual int SearchDebounceMs => 300;

        protected VirtualizedTableView() : base()
        {
            allItems = new List<object>();
            filteredItems = new List<object>();

            // Initialize search debouncer
            searchDebouncer = new SearchDebouncer(OnSearchDebounced, SearchDebounceMs);

            // Note: InitializeView() is called by base class (DatraDataView.InitializeBase)
        }

        protected override void InitializeView()
        {
            // Clear content container
            contentContainer.Clear();

            // Create toolbar
            var toolbar = CreateToolbar();
            headerContainer.Add(toolbar);

            // Let derived classes add additional UI (e.g., filter bars)
            CreateAdditionalHeaderUI();

            // Create main table container
            tableContainer = new VisualElement();
            tableContainer.AddToClassList("table-container");
            tableContainer.style.flexGrow = 1;
            tableContainer.style.flexDirection = FlexDirection.Column;

            // Create table header container with clipping
            var tableHeaderContainer = new VisualElement();
            tableHeaderContainer.AddToClassList("table-header-container");
            tableHeaderContainer.style.height = RowHeight;
            tableHeaderContainer.style.overflow = Overflow.Hidden;

            // Create table header row
            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.height = RowHeight;
            headerRow.style.position = Position.Relative;

            tableHeaderContainer.Add(headerRow);
            tableContainer.Add(tableHeaderContainer);

            // Create virtualized list view with horizontal scrolling enabled
            listView = new ListView();
            listView.AddToClassList("table-body-list");
            listView.fixedItemHeight = RowHeight;
            listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            listView.selectionType = SelectionType.None;
            listView.style.flexGrow = 1;
            listView.horizontalScrollingEnabled = true;

            // Set callbacks
            listView.makeItem = CreateRowTemplate;
            listView.bindItem = BindRow;

            // Sync header position with ListView's horizontal scroll
            listView.RegisterCallback<GeometryChangedEvent>(evt => {
                SyncHeaderWithListViewScroll();
            });

            tableContainer.Add(listView);
            contentContainer.Add(tableContainer);
        }

        /// <summary>
        /// Syncs header row position with ListView's horizontal scroll
        /// </summary>
        private void SyncHeaderWithListViewScroll()
        {
            // Find ListView's internal ScrollView
            var scrollView = listView?.Q<ScrollView>();
            if (scrollView != null)
            {
                scrollView.horizontalScroller.valueChanged -= OnListViewHorizontalScroll;
                scrollView.horizontalScroller.valueChanged += OnListViewHorizontalScroll;
            }
        }

        private void OnListViewHorizontalScroll(float value)
        {
            if (headerRow != null)
            {
                headerRow.style.left = -value;
            }
        }

        /// <summary>
        /// Creates the toolbar with search and action buttons
        /// </summary>
        protected virtual VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("table-toolbar");
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.height = 36;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.alignItems = Align.Center;

            // Add button
            var addButton = new Button(() => {
                if (!isReadOnly)
                    OnAddButtonClicked();
            });
            addButton.text = "➕ Add Row";
            addButton.AddToClassList("table-add-button");
            addButton.SetEnabled(!isReadOnly);
            toolbar.Add(addButton);

            // Search field
            toolbarSearchField = new ToolbarSearchField();
            toolbarSearchField.AddToClassList("table-search");
            toolbarSearchField.style.flexGrow = 1;
            toolbarSearchField.RegisterValueChangedCallback(evt => {
                currentSearchTerm = evt.newValue;
                searchDebouncer.Trigger(evt.newValue);
            });
            toolbar.Add(toolbarSearchField);

            searchField = toolbarSearchField; // Set base class field

            // Options button
            var optionsButton = new Button(ShowViewOptions);
            optionsButton.text = "⚙";
            optionsButton.tooltip = "View Options";
            optionsButton.AddToClassList("table-options-button");
            toolbar.Add(optionsButton);

            return toolbar;
        }

        /// <summary>
        /// Override to add additional header UI (e.g., filter bars)
        /// </summary>
        protected virtual void CreateAdditionalHeaderUI()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Refreshes the content by reloading data from repository
        /// </summary>
        public override void RefreshContent()
        {
            if (dataType == null || repository == null) return;

            // Clear header
            headerRow?.Clear();

            // Load all items from repository
            allItems = LoadItemsFromRepository();

            // Create header cells
            CreateHeaderCells();

            // Apply current filter
            ApplyFilter(currentSearchTerm);

            // Update footer
            UpdateFooter();
            UpdateModifiedState();
        }

        protected override void OnModificationsCleared()
        {
            base.OnModificationsCleared();

            // Rebuild ListView to clear visual modifications
            listView.Rebuild();
        }

        /// <summary>
        /// Loads all items from the dataSource (if available) or repository.
        /// DataSource reflects current editing state (including adds/deletes).
        /// </summary>
        protected virtual List<object> LoadItemsFromRepository()
        {
            // Prefer dataSource (reflects current editing state with adds/deletes)
            if (dataSource != null)
            {
                return dataSource.EnumerateItems().ToList();
            }

            // Fallback to repository (for read-only or legacy usage)
            return repository.EnumerateItems().ToList();
        }

        /// <summary>
        /// Applies filter and updates the list view
        /// </summary>
        protected virtual void ApplyFilter(string searchTerm)
        {
            // Filter items
            filteredItems = allItems.Where(item => MatchesFilter(item, searchTerm)).ToList();

            // Update ListView
            listView.itemsSource = filteredItems;
            listView.Rebuild();

            // Update statistics or other UI
            OnFilterApplied(filteredItems.Count, allItems.Count);
        }

        /// <summary>
        /// Called after debounce delay when search term changes
        /// </summary>
        protected virtual void OnSearchDebounced(string searchTerm)
        {
            ApplyFilter(searchTerm);
        }

        /// <summary>
        /// Called after filter is applied
        /// </summary>
        protected virtual void OnFilterApplied(int filteredCount, int totalCount)
        {
            // Override to update statistics, etc.
        }

        /// <summary>
        /// Creates a row template (called by ListView.makeItem)
        /// </summary>
        protected virtual VisualElement CreateRowTemplate()
        {
            var row = new VisualElement();
            row.AddToClassList("table-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = RowHeight;
            row.style.alignItems = Align.Center;

            // Create cells based on header structure
            CreateRowCells(row);

            return row;
        }

        /// <summary>
        /// Binds data to a row (called by ListView.bindItem)
        /// </summary>
        protected virtual void BindRow(VisualElement row, int index)
        {
            if (index < 0 || index >= filteredItems.Count) return;

            var item = filteredItems[index];

            // Store item reference for later use
            row.userData = item;

            // Clear previous row state classes
            row.RemoveFromClassList("modified-row");
            row.RemoveFromClassList("missing-locale-row");

            // Get first cell for indicator
            var firstCell = row.childCount > 0 ? row[0] : null;
            if (firstCell != null)
            {
                firstCell.RemoveFromClassList("modified-row-cell");
                firstCell.RemoveFromClassList("missing-locale-cell");
            }

            // Apply row state styling
            var rowState = GetRowState(item);
            if (rowState.isModified)
            {
                row.AddToClassList("modified-row");
                if (firstCell != null)
                    firstCell.AddToClassList("modified-row-cell");
            }
            if (rowState.isSpecial) // For localization missing rows
            {
                row.AddToClassList("missing-locale-row");
                if (firstCell != null)
                    firstCell.AddToClassList("missing-locale-cell");
            }

            // Bind data to cells
            BindRowData(row, item, index);
        }

        /// <summary>
        /// Gets the state of a row (modified, special status, etc.)
        /// Override in derived classes to provide custom row state logic
        /// </summary>
        protected virtual (bool isModified, bool isSpecial) GetRowState(object item)
        {
            // Default: check if item has any modifications via change tracker
            if (changeTracker != null && item != null)
            {
                try
                {
                    var itemKey = GetKeyFromItem(item);
                    if (itemKey != null)
                    {
                        var modifiedProps = changeTracker.GetModifiedProperties(itemKey);
                        bool isModified = modifiedProps.Any();
                        return (isModified, false);
                    }
                }
                catch (System.InvalidCastException)
                {
                    // Key type mismatch (e.g., Asset data with different key type)
                    // Return unmodified state
                }
            }
            return (false, false);
        }

        // Abstract methods for derived classes to implement

        /// <summary>
        /// Creates header cells
        /// </summary>
        protected abstract void CreateHeaderCells();

        /// <summary>
        /// Creates cells in a row template
        /// </summary>
        protected abstract void CreateRowCells(VisualElement row);

        /// <summary>
        /// Binds data to row cells
        /// </summary>
        protected abstract void BindRowData(VisualElement row, object item, int index);

        /// <summary>
        /// Checks if an item matches the filter criteria
        /// </summary>
        protected abstract bool MatchesFilter(object item, string searchTerm);

        /// <summary>
        /// Called when add button is clicked
        /// </summary>
        protected virtual void OnAddButtonClicked()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Shows view options menu
        /// </summary>
        protected virtual void ShowViewOptions()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Updates the visual state of a row without rebuilding (to avoid interrupting typing)
        /// Finds the row element and updates its state classes
        /// </summary>
        protected void UpdateRowStateVisuals(object item)
        {
            if (item == null || listView == null) return;

            // Find all visible rows in the ListView
            var visibleRows = listView.Query<VisualElement>(className: "table-row").ToList();

            foreach (var row in visibleRows)
            {
                // Check if this row corresponds to our item
                if (row.userData == item)
                {
                    // Update row state
                    var rowState = GetRowState(item);

                    // Update modified state
                    if (rowState.isModified)
                    {
                        row.AddToClassList("modified-row");
                        if (row.childCount > 0)
                            row[0].AddToClassList("modified-row-cell");
                    }
                    else
                    {
                        row.RemoveFromClassList("modified-row");
                        if (row.childCount > 0)
                            row[0].RemoveFromClassList("modified-row-cell");
                    }

                    // Update special state
                    if (rowState.isSpecial)
                    {
                        row.AddToClassList("missing-locale-row");
                        if (row.childCount > 0)
                            row[0].AddToClassList("missing-locale-cell");
                    }
                    else
                    {
                        row.RemoveFromClassList("missing-locale-row");
                        if (row.childCount > 0)
                            row[0].RemoveFromClassList("missing-locale-cell");
                    }

                    break; // Found the row, no need to continue
                }
            }
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public override void Cleanup()
        {
            searchDebouncer?.Dispose();
            base.Cleanup();
        }
    }
}
