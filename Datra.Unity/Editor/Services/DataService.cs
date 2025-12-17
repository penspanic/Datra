using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Services
{
    /// <summary>
    /// Service implementation for data operations.
    /// Wraps IDataContext and repositories with save/reload functionality.
    /// </summary>
    public class DataService : IDataService
    {
        private readonly IDataContext _dataContext;
        private readonly Dictionary<Type, IDataRepository> _repositories;
        private readonly IChangeTrackingService _changeTracking;
        private readonly List<DataTypeInfo> _dataTypeInfos;

        public IDataContext DataContext => _dataContext;
        public IReadOnlyDictionary<Type, IDataRepository> Repositories => _repositories;

        public event Action<Type> OnDataChanged;

        public DataService(
            IDataContext dataContext,
            Dictionary<Type, IDataRepository> repositories,
            IChangeTrackingService changeTracking = null)
        {
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
            _changeTracking = changeTracking;
            _dataTypeInfos = dataContext.GetDataTypeInfos().ToList();
        }

        public IReadOnlyList<DataTypeInfo> GetDataTypeInfos()
        {
            return _dataTypeInfos;
        }

        public IDataRepository GetRepository(Type dataType)
        {
            _repositories.TryGetValue(dataType, out var repository);
            return repository;
        }

        public async Task<bool> SaveAsync(Type dataType, bool forceSave = false)
        {
            if (!_repositories.TryGetValue(dataType, out var repository))
            {
                return false;
            }

            // Check if save is needed
            if (!forceSave && _changeTracking != null && !_changeTracking.HasUnsavedChanges(dataType))
            {
                return true;
            }

            try
            {
                await repository.SaveAsync();

                // Update change tracker baseline
                _changeTracking?.InitializeBaseline(dataType);

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

            // Determine which types to save
            var typesToSave = forceSave
                ? _repositories.Keys.ToList()
                : (_changeTracking != null
                    ? _changeTracking.GetModifiedTypes().ToList()
                    : _repositories.Keys.ToList());

            foreach (var type in typesToSave)
            {
                if (!await SaveAsync(type, forceSave))
                {
                    success = false;
                }
            }

            return success;
        }

        public async Task<bool> ReloadAsync(Type dataType)
        {
            if (!_repositories.TryGetValue(dataType, out var repository))
            {
                return false;
            }

            try
            {
                await repository.LoadAsync();
                _changeTracking?.InitializeBaseline(dataType);
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
            try
            {
                await _dataContext.LoadAllAsync();
                _changeTracking?.InitializeAllBaselines();

                foreach (var type in _repositories.Keys)
                {
                    OnDataChanged?.Invoke(type);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
