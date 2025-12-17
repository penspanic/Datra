using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Localization;
using Datra.Unity.Editor.Services;
using Datra.Unity.Editor.ViewModels;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for DatraEditorViewModel and service layer.
    /// These tests verify the MVVM architecture works correctly.
    /// </summary>
    public class DatraEditorViewModelTests
    {
        #region Mock Implementations

        /// <summary>
        /// Mock implementation of IDataService for testing
        /// </summary>
        private class MockDataService : IDataService
        {
            public IDataContext DataContext => null;
            public IReadOnlyDictionary<Type, IDataRepository> Repositories { get; } = new Dictionary<Type, IDataRepository>();

            public event Action<Type> OnDataChanged;

            public bool SaveWasCalled { get; private set; }
            public bool SaveAllWasCalled { get; private set; }
            public bool ReloadWasCalled { get; private set; }
            public Type LastSavedType { get; private set; }
            public bool ShouldSucceed { get; set; } = true;

            private List<DataTypeInfo> _dataTypeInfos = new List<DataTypeInfo>();

            public void AddDataTypeInfo(DataTypeInfo info)
            {
                _dataTypeInfos.Add(info);
            }

            public IReadOnlyList<DataTypeInfo> GetDataTypeInfos() => _dataTypeInfos;
            public IDataRepository GetRepository(Type dataType) => null;

            public Task<bool> SaveAsync(Type dataType, bool forceSave = false)
            {
                SaveWasCalled = true;
                LastSavedType = dataType;
                return Task.FromResult(ShouldSucceed);
            }

            public Task<bool> SaveAllAsync(bool forceSave = false)
            {
                SaveAllWasCalled = true;
                return Task.FromResult(ShouldSucceed);
            }

            public Task<bool> ReloadAsync(Type dataType)
            {
                ReloadWasCalled = true;
                return Task.FromResult(ShouldSucceed);
            }

            public Task<bool> ReloadAllAsync()
            {
                ReloadWasCalled = true;
                return Task.FromResult(ShouldSucceed);
            }

            public void TriggerDataChanged(Type type) => OnDataChanged?.Invoke(type);
        }

        /// <summary>
        /// Mock implementation of IChangeTrackingService for testing
        /// </summary>
        private class MockChangeTrackingService : IChangeTrackingService
        {
            private HashSet<Type> _modifiedTypes = new HashSet<Type>();

            public event Action<Type, bool> OnModifiedStateChanged;

            public bool HasUnsavedChanges(Type dataType) => _modifiedTypes.Contains(dataType);
            public bool HasAnyUnsavedChanges() => _modifiedTypes.Count > 0;
            public IEnumerable<Type> GetModifiedTypes() => _modifiedTypes;

            public void InitializeBaseline(Type dataType) { }
            public void InitializeAllBaselines() { }
            public void ResetChanges(Type dataType) => _modifiedTypes.Remove(dataType);
            public void RegisterType(Type dataType, object repository) { }
            public void UnregisterType(Type dataType) { }

            public void SetModified(Type dataType, bool isModified)
            {
                if (isModified)
                    _modifiedTypes.Add(dataType);
                else
                    _modifiedTypes.Remove(dataType);

                OnModifiedStateChanged?.Invoke(dataType, isModified);
            }
        }

        #endregion

        #region ViewModel Tests

        [Test]
        public void ViewModel_CanBeCreated_WithDataService()
        {
            // Arrange
            var mockDataService = new MockDataService();

            // Act
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Assert
            Assert.IsNotNull(viewModel);
            Assert.IsTrue(viewModel.IsInitialized);
            Assert.IsNull(viewModel.SelectedDataType);
            Assert.IsFalse(viewModel.IsLocalizationSelected);
        }

        [Test]
        public void ViewModel_SelectDataType_UpdatesState()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);
            var testType = typeof(string);

            // Act
            viewModel.SelectDataTypeCommand(testType);

            // Assert
            Assert.AreEqual(testType, viewModel.SelectedDataType);
            Assert.IsFalse(viewModel.IsLocalizationSelected);
        }

        [Test]
        public void ViewModel_SelectDataType_WithNull_SetsNullType()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Act - null is allowed, will set SelectedDataType to null
            viewModel.SelectDataTypeCommand(null);

            // Assert
            Assert.IsNull(viewModel.SelectedDataType);
        }

        [Test]
        public void ViewModel_HasAnyUnsavedChanges_ReturnsCorrectValue()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var mockChangeTracking = new MockChangeTrackingService();
            var viewModel = new DatraEditorViewModel(mockDataService, mockChangeTracking);

            // Assert - initially no changes
            Assert.IsFalse(viewModel.HasAnyUnsavedChanges);

            // Act - mark a type as modified
            mockChangeTracking.SetModified(typeof(string), true);

            // Assert - now has changes
            Assert.IsTrue(viewModel.HasAnyUnsavedChanges);
        }

        [UnityTest]
        public IEnumerator ViewModel_SaveCommand_CallsDataService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);
            var testType = typeof(string);
            viewModel.SelectDataTypeCommand(testType);

            // Act
            var saveTask = viewModel.SaveCommand();
            while (!saveTask.IsCompleted) yield return null;

            // Assert
            Assert.IsTrue(mockDataService.SaveWasCalled);
            Assert.AreEqual(testType, mockDataService.LastSavedType);
        }

        [UnityTest]
        public IEnumerator ViewModel_SaveAllCommand_CallsDataService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Act
            var saveTask = viewModel.SaveAllCommand();
            while (!saveTask.IsCompleted) yield return null;

            // Assert
            Assert.IsTrue(mockDataService.SaveAllWasCalled);
        }

        [UnityTest]
        public IEnumerator ViewModel_ReloadCommand_CallsDataService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Act
            var reloadTask = viewModel.ReloadCommand();
            while (!reloadTask.IsCompleted) yield return null;

            // Assert
            Assert.IsTrue(mockDataService.ReloadWasCalled);
        }

        [Test]
        public void ViewModel_RaisesOperationCompleted_OnSuccessfulSave()
        {
            // Arrange
            var mockDataService = new MockDataService();
            mockDataService.ShouldSucceed = true;
            var viewModel = new DatraEditorViewModel(mockDataService);
            viewModel.SelectDataTypeCommand(typeof(string));

            string completedMessage = null;
            viewModel.OnOperationCompleted += msg => completedMessage = msg;

            // Act
            viewModel.SaveCommand().Wait();

            // Assert
            Assert.IsNotNull(completedMessage);
            Assert.That(completedMessage, Does.Contain("saved"));
        }

        [Test]
        public void ViewModel_RaisesOperationFailed_OnFailedSave()
        {
            // Arrange
            var mockDataService = new MockDataService();
            mockDataService.ShouldSucceed = false;
            var viewModel = new DatraEditorViewModel(mockDataService);
            viewModel.SelectDataTypeCommand(typeof(string));

            string failedMessage = null;
            viewModel.OnOperationFailed += msg => failedMessage = msg;

            // Act
            viewModel.SaveCommand().Wait();

            // Assert
            Assert.IsNotNull(failedMessage);
            Assert.That(failedMessage, Does.Contain("Failed"));
        }

        [Test]
        public void ViewModel_PropertyChanged_RaisedOnStateChange()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            var propertyChangedCalled = false;
            viewModel.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(viewModel.SelectedDataType))
                    propertyChangedCalled = true;
            };

            // Act
            viewModel.SelectDataTypeCommand(typeof(string));

            // Assert
            Assert.IsTrue(propertyChangedCalled);
        }

        [Test]
        public void ViewModel_SelectLocalization_UpdatesState()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);
            viewModel.SelectDataTypeCommand(typeof(string));

            // Act
            viewModel.SelectLocalizationCommand();

            // Assert
            Assert.IsNull(viewModel.SelectedDataType);
            Assert.IsTrue(viewModel.IsLocalizationSelected);
        }

        [Test]
        public void ViewModel_SelectDataType_ClearsLocalizationSelection()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);
            viewModel.SelectLocalizationCommand();

            // Act
            viewModel.SelectDataTypeCommand(typeof(int));

            // Assert
            Assert.AreEqual(typeof(int), viewModel.SelectedDataType);
            Assert.IsFalse(viewModel.IsLocalizationSelected);
        }

        [Test]
        public void ViewModel_HasCurrentDataUnsavedChanges_ForDataType()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var mockChangeTracking = new MockChangeTrackingService();
            var viewModel = new DatraEditorViewModel(mockDataService, mockChangeTracking);
            viewModel.SelectDataTypeCommand(typeof(string));

            // Assert - initially no changes
            Assert.IsFalse(viewModel.HasCurrentDataUnsavedChanges);

            // Act - mark the selected type as modified
            mockChangeTracking.SetModified(typeof(string), true);

            // Assert - now has changes for current data
            Assert.IsTrue(viewModel.HasCurrentDataUnsavedChanges);
        }

        [Test]
        public void ViewModel_HasCurrentDataUnsavedChanges_OnlyForSelectedType()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var mockChangeTracking = new MockChangeTrackingService();
            var viewModel = new DatraEditorViewModel(mockDataService, mockChangeTracking);
            viewModel.SelectDataTypeCommand(typeof(string));

            // Mark a different type as modified
            mockChangeTracking.SetModified(typeof(int), true);

            // Assert - current data (string) has no changes
            Assert.IsFalse(viewModel.HasCurrentDataUnsavedChanges);

            // Assert - but there are overall unsaved changes
            Assert.IsTrue(viewModel.HasAnyUnsavedChanges);
        }

        [Test]
        public void ViewModel_HasUnsavedChanges_ForSpecificType()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var mockChangeTracking = new MockChangeTrackingService();
            var viewModel = new DatraEditorViewModel(mockDataService, mockChangeTracking);

            mockChangeTracking.SetModified(typeof(string), true);

            // Assert
            Assert.IsTrue(viewModel.HasUnsavedChanges(typeof(string)));
            Assert.IsFalse(viewModel.HasUnsavedChanges(typeof(int)));
        }

        [UnityTest]
        public IEnumerator ViewModel_ForceSaveCurrentAsync_CallsDataServiceWithForceSave()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);
            viewModel.SelectDataTypeCommand(typeof(string));

            // Act
            var saveTask = viewModel.ForceSaveCurrentAsync();
            while (!saveTask.IsCompleted) yield return null;

            // Assert
            Assert.IsTrue(mockDataService.SaveWasCalled);
            Assert.AreEqual(typeof(string), mockDataService.LastSavedType);
        }

        [UnityTest]
        public IEnumerator ViewModel_ForceSaveAllAsync_CallsDataService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Act
            var saveTask = viewModel.ForceSaveAllAsync();
            while (!saveTask.IsCompleted) yield return null;

            // Assert
            Assert.IsTrue(mockDataService.SaveAllWasCalled);
        }

        [Test]
        public void ViewModel_ModifiedStateChanged_PropagatesFromChangeTracking()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var mockChangeTracking = new MockChangeTrackingService();
            var viewModel = new DatraEditorViewModel(mockDataService, mockChangeTracking);

            Type changedType = null;
            bool? hasChanges = null;
            viewModel.OnModifiedStateChanged += (type, modified) =>
            {
                changedType = type;
                hasChanges = modified;
            };

            // Act
            mockChangeTracking.SetModified(typeof(string), true);

            // Assert
            Assert.AreEqual(typeof(string), changedType);
            Assert.IsTrue(hasChanges);
        }

        [Test]
        public void ViewModel_ProjectName_CanBeSetAndRetrieved()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Act
            viewModel.ProjectName = "TestProject";

            // Assert
            Assert.AreEqual("TestProject", viewModel.ProjectName);
        }

        [Test]
        public void ViewModel_DataTypes_ReturnsFromDataService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            mockDataService.AddDataTypeInfo(new DataTypeInfo(
                typeName: "System.String",
                dataType: typeof(string),
                filePath: "strings.csv",
                propertyName: "Strings",
                isSingleData: false
            ));

            var viewModel = new DatraEditorViewModel(mockDataService);

            // Assert
            Assert.AreEqual(1, viewModel.DataTypes.Count);
            Assert.AreEqual(typeof(string), viewModel.DataTypes[0].DataType);
        }

        [Test]
        public void ViewModel_DataService_ExposesUnderlyingService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            // Assert
            Assert.AreSame(mockDataService, viewModel.DataService);
        }

        [Test]
        public void ViewModel_ChangeTracking_ExposesUnderlyingService()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var mockChangeTracking = new MockChangeTrackingService();
            var viewModel = new DatraEditorViewModel(mockDataService, mockChangeTracking);

            // Assert
            Assert.AreSame(mockChangeTracking, viewModel.ChangeTracking);
        }

        [Test]
        public void ViewModel_SaveWithNoSelection_RaisesFailedEvent()
        {
            // Arrange
            var mockDataService = new MockDataService();
            var viewModel = new DatraEditorViewModel(mockDataService);

            string failedMessage = null;
            viewModel.OnOperationFailed += msg => failedMessage = msg;

            // Act - save without selecting anything
            viewModel.SaveCommand().Wait();

            // Assert
            Assert.IsNotNull(failedMessage);
            Assert.That(failedMessage, Does.Contain("No data type selected"));
        }

        #endregion
    }
}
