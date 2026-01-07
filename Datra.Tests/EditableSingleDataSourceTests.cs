#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// Tests for EditableSingleDataSource.
    /// Verifies key handling, property change tracking, and compatibility with "single" key.
    /// </summary>
    public class EditableSingleDataSourceTests
    {
        #region Test Helpers

        private class TestSingleData
        {
            public string Name { get; set; } = "";
            public int Value { get; set; }
            public bool IsActive { get; set; }
        }

        private class MockSingleDataRepository : ISingleDataRepository<TestSingleData>
        {
            private TestSingleData? _data;
            public bool SaveWasCalled { get; private set; }

            public bool IsLoaded => _data != null;
            public TestSingleData Get() => _data ?? throw new InvalidOperationException("Not loaded");
            public void Set(TestSingleData data) => _data = data;
            public Task LoadAsync()
            {
                _data = new TestSingleData { Name = "Default", Value = 100, IsActive = true };
                return Task.CompletedTask;
            }
            public Task SaveAsync()
            {
                SaveWasCalled = true;
                return Task.CompletedTask;
            }
            public string GetLoadedFilePath() => "mock://single.json";
            public IEnumerable<object> EnumerateItems() => _data != null ? new[] { _data } : Array.Empty<object>();
            public int ItemCount => _data != null ? 1 : 0;

            // Helper method for test setup
            public void SetData(TestSingleData data) => _data = data;
        }

        #endregion

        #region GetItemKey Tests

        [Fact]
        public void GetItemKey_WithData_ReturnsSingleKey()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Test", Value = 42 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            var data = new TestSingleData { Name = "Test", Value = 42 };

            // Act
            var key = dataSource.GetItemKey(data);

            // Assert
            Assert.Equal(EditableSingleDataSource<TestSingleData>.SingleKey, key);
        }

        [Fact]
        public void GetItemKey_WithNull_ReturnsNull()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Test", Value = 42 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            // Act
            var key = dataSource.GetItemKey(null!);

            // Assert
            Assert.Null(key);
        }

        [Fact]
        public void GetItemKey_WithKeyValuePair_ReturnsSingleKey()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Test", Value = 42 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            var data = new TestSingleData { Name = "Test", Value = 42 };
            var kvp = new KeyValuePair<string, TestSingleData>(EditableSingleDataSource<TestSingleData>.SingleKey, data);

            // Act
            var key = dataSource.GetItemKey(kvp);

            // Assert
            Assert.Equal(EditableSingleDataSource<TestSingleData>.SingleKey, key);
        }

        #endregion

        #region TrackPropertyChange Tests - Key Compatibility

        [Fact]
        public void TrackPropertyChange_WithSingleKey_DetectsModification()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            // Act - use the constant SingleKey
            dataSource.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Name", "Modified", out bool isModified);

            // Assert
            Assert.True(isModified);
            Assert.True(dataSource.HasModifications);
        }

        [Fact]
        public void TrackPropertyChange_WithLegacySingleKey_DetectsModification()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            // Act - use "single" (legacy key from DatraDataView)
            nonGeneric.TrackPropertyChange("single", "Name", "Modified", out bool isModified);

            // Assert - should work due to backward compatibility
            Assert.True(isModified, "Should accept 'single' key for backward compatibility");
            Assert.True(dataSource.HasModifications);
        }

        [Fact]
        public void TrackPropertyChange_WithInvalidKey_DoesNotModify()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);
            IEditableDataSource nonGeneric = dataSource;

            // Act - use invalid key
            nonGeneric.TrackPropertyChange("invalid_key", "Name", "Modified", out bool isModified);

            // Assert
            Assert.False(isModified);
            Assert.False(dataSource.HasModifications);
        }

        [Fact]
        public void TrackPropertyChange_RevertToBaseline_ClearsModification()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            // First modify
            dataSource.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Name", "Modified", out _);
            Assert.True(dataSource.HasModifications);

            // Act - revert to original
            dataSource.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Name", "Original", out bool isModified);

            // Assert
            Assert.False(isModified);
            Assert.False(dataSource.HasModifications);
        }

        #endregion

        #region Event Tests

        [Fact]
        public void TrackPropertyChange_FiresOnModifiedStateChanged()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            bool eventFired = false;
            bool? eventValue = null;
            dataSource.OnModifiedStateChanged += (hasModifications) =>
            {
                eventFired = true;
                eventValue = hasModifications;
            };

            // Act
            dataSource.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Name", "Modified", out _);

            // Assert
            Assert.True(eventFired);
            Assert.True(eventValue);
        }

        #endregion

        #region Save and Revert Tests

        [Fact]
        public async Task SaveAsync_AppliesChanges()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            dataSource.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Name", "Modified", out _);

            // Act
            await dataSource.SaveAsync();

            // Assert
            Assert.True(repo.SaveWasCalled);
            Assert.False(dataSource.HasModifications);
        }

        [Fact]
        public void Revert_ClearsModifications()
        {
            // Arrange
            var repo = new MockSingleDataRepository();
            repo.SetData(new TestSingleData { Name = "Original", Value = 100 });
            var dataSource = new EditableSingleDataSource<TestSingleData>(repo);

            dataSource.TrackPropertyChange(EditableSingleDataSource<TestSingleData>.SingleKey, "Name", "Modified", out _);
            Assert.True(dataSource.HasModifications);

            // Act
            dataSource.Revert();

            // Assert
            Assert.False(dataSource.HasModifications);
        }

        #endregion
    }
}
