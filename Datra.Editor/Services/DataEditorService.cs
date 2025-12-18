#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Editor.Interfaces;
using Datra.Interfaces;

namespace Datra.Editor.Services
{
    /// <summary>
    /// Default implementation of IDataEditorService.
    /// Manages data editing operations with file-based change tracking.
    /// </summary>
    public class DataEditorService : IDataEditorService
    {
        private readonly IChangeTrackingService _changeTracker;
        private readonly Dictionary<Type, IDataRepository> _repositories = new();
        private readonly Dictionary<Type, DataFilePath> _typeToFilePath = new();
        private readonly Dictionary<DataFilePath, Type> _filePathToType = new();

        public IDataContext DataContext { get; }
        public IReadOnlyDictionary<Type, IDataRepository> Repositories => _repositories;

        public event Action<Type>? OnDataChanged;
        public event Action<Type, bool>? OnModifiedStateChanged;

        public DataEditorService(IDataContext dataContext, IChangeTrackingService changeTracker)
        {
            DataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _changeTracker = changeTracker ?? throw new ArgumentNullException(nameof(changeTracker));

            // Forward change tracking events (convert file path to type)
            _changeTracker.OnModifiedStateChanged += (filePath, isModified) =>
            {
                if (_filePathToType.TryGetValue(filePath, out var type))
                {
                    OnModifiedStateChanged?.Invoke(type, isModified);
                }
            };
        }

        /// <summary>
        /// Register a repository for a data type with file path tracking
        /// </summary>
        public void RegisterRepository(Type dataType, IDataRepository repository, DataFilePath filePath, Func<string> contentProvider)
        {
            _repositories[dataType] = repository;
            _typeToFilePath[dataType] = filePath;
            _filePathToType[filePath] = dataType;
            _changeTracker.RegisterFile(filePath, contentProvider);
        }

        public IReadOnlyList<DataTypeInfo> GetDataTypeInfos()
        {
            return DataContext.GetDataTypeInfos();
        }

        public IDataRepository? GetRepository(Type dataType)
        {
            return _repositories.TryGetValue(dataType, out var repo) ? repo : null;
        }

        /// <summary>
        /// Get the file path for a data type
        /// </summary>
        public DataFilePath? GetFilePath(Type dataType)
        {
            return _typeToFilePath.TryGetValue(dataType, out var path) ? path : (DataFilePath?)null;
        }

        public async Task<bool> SaveAsync(Type dataType, bool forceSave = false)
        {
            if (!_repositories.TryGetValue(dataType, out var repository))
                return false;

            if (!forceSave && !HasChanges(dataType))
                return true; // Nothing to save

            try
            {
                await repository.SaveAsync();

                // Update baseline after successful save
                if (_typeToFilePath.TryGetValue(dataType, out var filePath))
                {
                    _changeTracker.ResetChanges(filePath);
                }
                OnDataChanged?.Invoke(dataType);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> SaveAllAsync(bool forceSave = false)
        {
            var success = true;
            var typesToSave = forceSave
                ? _repositories.Keys.ToList()
                : GetModifiedTypes().ToList();

            foreach (var type in typesToSave)
            {
                if (!await SaveAsync(type, forceSave))
                    success = false;
            }

            return success;
        }

        public async Task<bool> ReloadAsync(Type dataType)
        {
            if (!_repositories.TryGetValue(dataType, out var repository))
                return false;

            try
            {
                await repository.LoadAsync();

                // Reset baseline after reload
                if (_typeToFilePath.TryGetValue(dataType, out var filePath))
                {
                    _changeTracker.ResetChanges(filePath);
                }
                OnDataChanged?.Invoke(dataType);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> ReloadAllAsync()
        {
            var success = true;
            foreach (var type in _repositories.Keys.ToList())
            {
                if (!await ReloadAsync(type))
                    success = false;
            }
            return success;
        }

        public bool HasChanges(Type dataType)
        {
            if (!_typeToFilePath.TryGetValue(dataType, out var filePath))
                return false;
            return _changeTracker.HasUnsavedChanges(filePath);
        }

        public bool HasAnyChanges()
        {
            return _changeTracker.HasAnyUnsavedChanges();
        }

        public IEnumerable<Type> GetModifiedTypes()
        {
            foreach (var filePath in _changeTracker.GetModifiedFiles())
            {
                if (_filePathToType.TryGetValue(filePath, out var type))
                {
                    yield return type;
                }
            }
        }

        /// <summary>
        /// Initialize baselines for all registered types
        /// </summary>
        public void InitializeBaselines()
        {
            _changeTracker.InitializeAllBaselines();
        }
    }
}
