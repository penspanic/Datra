using System;
using System.Collections.Generic;
using UnityEngine;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.Unity.Serialization
{
    /// <summary>
    /// Unity-specific resolver for DataRef types
    /// Provides integration with Unity's serialization system
    /// </summary>
    public class UnityDataRefResolver
    {
        private readonly BaseDataContext _dataContext;
        private readonly Dictionary<Type, object> _repositoryCache = new Dictionary<Type, object>();
        
        public UnityDataRefResolver(BaseDataContext dataContext)
        {
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
        }
        
        /// <summary>
        /// Resolve an IntDataRef to its actual data
        /// </summary>
        public T Resolve<T>(IntDataRef<T> dataRef) where T : class, ITableData<int>
        {
            if (!dataRef.HasValue) return null;
            
            return dataRef.Evaluate(_dataContext);
        }
        
        /// <summary>
        /// Resolve a StringDataRef to its actual data
        /// </summary>
        public T Resolve<T>(StringDataRef<T> dataRef) where T : class, ITableData<string>
        {
            if (!dataRef.HasValue) return null;

            return dataRef.Evaluate(_dataContext);
        }
        
        /// <summary>
        /// Batch resolve multiple IntDataRefs
        /// </summary>
        public List<T> ResolveMany<T>(IEnumerable<IntDataRef<T>> dataRefs) where T : class, ITableData<int>
        {
            var result = new List<T>();
            var repository = GetRepository<int, T>();
            
            if (repository == null) return result;
            
            foreach (var dataRef in dataRefs)
            {
                if (dataRef.HasValue)
                {
                    var item = dataRef.Evaluate(_dataContext);
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Batch resolve multiple StringDataRefs
        /// </summary>
        public List<T> ResolveMany<T>(IEnumerable<StringDataRef<T>> dataRefs) where T : class, ITableData<string>
        {
            var result = new List<T>();
            var repository = GetRepository<string, T>();
            
            if (repository == null) return result;
            
            foreach (var dataRef in dataRefs)
            {
                if (dataRef.HasValue)
                {
                    var item = dataRef.Evaluate(_dataContext);
                    if (item != null)
                    {
                        result.Add(item);
                    }
                }
            }
            
            return result;
        }
        
        private IDataRepository<TKey, TData> GetRepository<TKey, TData>() where TData : class, ITableData<TKey>
        {
            var type = typeof(TData);
            
            if (_repositoryCache.TryGetValue(type, out var cached))
            {
                return cached as IDataRepository<TKey, TData>;
            }
            
            // Use reflection to find the repository in the DataContext
            var contextType = _dataContext.GetType();
            var repositoriesField = contextType.GetField("Repositories", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (repositoriesField != null)
            {
                var repositories = repositoriesField.GetValue(_dataContext) as Dictionary<string, object>;
                if (repositories != null && repositories.TryGetValue(type.FullName, out var repository))
                {
                    _repositoryCache[type] = repository;
                    return repository as IDataRepository<TKey, TData>;
                }
            }
            
            Debug.LogWarning($"[Datra] Repository for type {type.Name} not found in DataContext");
            return null;
        }
    }
}