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

            // Create table header (separate from base class headerContainer)
            var tableHeaderContainer = new VisualElement();
            tableHeaderContainer.AddToClassList("table-header-container");
            tableHeaderContainer.style.height = RowHeight;
            tableHeaderContainer.style.overflow = Overflow.Hidden; // Clip header overflow

            headerRow = new VisualElement();
            headerRow.AddToClassList("table-header-row");
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.height = RowHeight;

            tableHeaderContainer.Add(headerRow);
            tableContainer.Add(tableHeaderContainer);

            // Create scroll view for horizontal scrolling
            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.AddToClassList("table-body-scroll");
            scrollView.style.flexGrow = 1;

            // Sync horizontal scroll with header
            scrollView.horizontalScroller.valueChanged += (value) => {
                headerRow.style.left = -value;
            };

            // Create virtualized list view
            listView = new ListView();
            listView.AddToClassList("table-body-list");
            listView.style.flexGrow = 1;
            listView.fixedItemHeight = RowHeight;
            listView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            listView.selectionType = SelectionType.None;

            // Set callbacks
            listView.makeItem = CreateRowTemplate;
            listView.bindItem = BindRow;

            scrollView.Add(listView);
            tableContainer.Add(scrollView);
            contentContainer.Add(tableContainer);
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

        /// <summary>
        /// Loads all items from the repository
        /// </summary>
        protected virtual List<object> LoadItemsFromRepository()
        {
            var items = new List<object>();

            if (IsTableData(dataType))
            {
                var getAllMethod = repository.GetType().GetMethod("GetAll");
                var data = getAllMethod?.Invoke(repository, null) as System.Collections.IEnumerable;

                if (data != null)
                {
                    foreach (var item in data)
                    {
                        var actualData = ExtractActualData(item);
                        items.Add(actualData);
                    }
                }
            }
            else
            {
                // Single data
                var getMethod = repository.GetType().GetMethod("Get");
                var singleData = getMethod?.Invoke(repository, null);
                if (singleData != null)
                {
                    items.Add(singleData);
                }
            }

            return items;
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

            // Bind data to cells
            BindRowData(row, item, index);
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
        /// Cleanup
        /// </summary>
        public override void Cleanup()
        {
            searchDebouncer?.Dispose();
            base.Cleanup();
        }
    }
}
