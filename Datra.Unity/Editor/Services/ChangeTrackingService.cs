using System;
using System.Collections.Generic;
using System.Linq;
using Datra.Editor.Interfaces;
using Datra.Interfaces;
using Datra.Unity.Editor.Utilities;

namespace Datra.Unity.Editor.Services
{
    /// <summary>
    /// Service implementation for change tracking.
    /// Manages RepositoryChangeTracker instances and provides unified change detection.
    /// </summary>
    public class ChangeTrackingService : IChangeTrackingService
    {
        private readonly Dictionary<Type, IRepositoryChangeTracker> _changeTrackers;

        public event Action<Type, bool> OnModifiedStateChanged;

        public ChangeTrackingService()
        {
            _changeTrackers = new Dictionary<Type, IRepositoryChangeTracker>();
        }

        public ChangeTrackingService(Dictionary<Type, IRepositoryChangeTracker> existingTrackers)
        {
            _changeTrackers = existingTrackers ?? new Dictionary<Type, IRepositoryChangeTracker>();
            SubscribeToTrackerEvents();
        }

        private void SubscribeToTrackerEvents()
        {
            foreach (var kvp in _changeTrackers)
            {
                SubscribeToTracker(kvp.Key, kvp.Value);
            }
        }

        private void SubscribeToTracker(Type dataType, IRepositoryChangeTracker tracker)
        {
            if (tracker is INotifyModifiedStateChanged notifyTracker)
            {
                notifyTracker.OnModifiedStateChanged += (hasChanges) =>
                {
                    OnModifiedStateChanged?.Invoke(dataType, hasChanges);
                };
            }
        }

        public bool HasUnsavedChanges(Type dataType)
        {
            if (_changeTrackers.TryGetValue(dataType, out var tracker))
            {
                return tracker.HasModifications;
            }
            return false;
        }

        public bool HasAnyUnsavedChanges()
        {
            return _changeTrackers.Values.Any(t => t.HasModifications);
        }

        public IEnumerable<Type> GetModifiedTypes()
        {
            return _changeTrackers
                .Where(kvp => kvp.Value.HasModifications)
                .Select(kvp => kvp.Key);
        }

        public void InitializeBaseline(Type dataType)
        {
            if (_changeTrackers.TryGetValue(dataType, out var tracker))
            {
                // Get current data and reinitialize baseline
                // Note: This requires access to the repository data
                // For now, we use UpdateBaseline with null to just reset
                tracker.UpdateBaseline(null);
            }
        }

        public void InitializeAllBaselines()
        {
            foreach (var type in _changeTrackers.Keys)
            {
                InitializeBaseline(type);
            }
        }

        public void ResetChanges(Type dataType)
        {
            if (_changeTrackers.TryGetValue(dataType, out var tracker))
            {
                tracker.UpdateBaseline(null);
            }
        }

        public void RegisterType(Type dataType, object repository)
        {
            if (_changeTrackers.ContainsKey(dataType))
            {
                return; // Already registered
            }

            var tracker = CreateTrackerForRepository(dataType, repository);
            if (tracker != null)
            {
                _changeTrackers[dataType] = tracker;
                SubscribeToTracker(dataType, tracker);
            }
        }

        public void UnregisterType(Type dataType)
        {
            _changeTrackers.Remove(dataType);
        }

        /// <summary>
        /// Get the underlying change tracker for a type (for advanced usage)
        /// </summary>
        public IRepositoryChangeTracker GetTracker(Type dataType)
        {
            _changeTrackers.TryGetValue(dataType, out var tracker);
            return tracker;
        }

        private IRepositoryChangeTracker CreateTrackerForRepository(Type dataType, object repository)
        {
            try
            {
                var repoType = repository.GetType();

                // Check for ISingleDataRepository<TData>
                var singleRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(ISingleDataRepository<>));

                if (singleRepoInterface != null)
                {
                    var valueType = singleRepoInterface.GetGenericArguments()[0];
                    var trackerType = typeof(RepositoryChangeTracker<,>)
                        .MakeGenericType(typeof(string), valueType);
                    return Activator.CreateInstance(trackerType) as IRepositoryChangeTracker;
                }

                // Check for IDataRepository<TKey, TData>
                var dataRepoInterface = repoType.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IDataRepository<,>));

                if (dataRepoInterface != null)
                {
                    var genericArgs = dataRepoInterface.GetGenericArguments();
                    var keyType = genericArgs[0];
                    var valueType = genericArgs[1];

                    var trackerType = typeof(RepositoryChangeTracker<,>)
                        .MakeGenericType(keyType, valueType);
                    return Activator.CreateInstance(trackerType) as IRepositoryChangeTracker;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
