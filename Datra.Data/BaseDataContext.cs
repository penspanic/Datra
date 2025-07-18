using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Data.Attributes;
using Datra.Data.Interfaces;
using Datra.Data.Loaders;
using Datra.Data.Repositories;

namespace Datra.Data
{
    /// <summary>
    /// Base class for all DataContext
    /// Classes generated by Source Generator inherit from this
    /// </summary>
    public abstract class BaseDataContext : IDataContext
    {
        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataLoaderFactory _loaderFactory;
        private readonly Dictionary<string, object> _repositories = new();
        
        protected IRawDataProvider RawDataProvider => _rawDataProvider;
        protected DataLoaderFactory LoaderFactory => _loaderFactory;
        
        protected BaseDataContext(IRawDataProvider rawDataProvider, DataLoaderFactory loaderFactory)
        {
            _rawDataProvider = rawDataProvider ?? throw new ArgumentNullException(nameof(rawDataProvider));
            _loaderFactory = loaderFactory ?? throw new ArgumentNullException(nameof(loaderFactory));
        }
        
        /// <summary>
        /// Initialize repositories - implemented by derived classes
        /// </summary>
        protected virtual void InitializeRepositories()
        {
            // Default implementation is empty - overridden by Source Generator
        }
        
        public virtual async Task LoadAllAsync()
        {
            var tasks = new List<Task>();
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (IsRepositoryProperty(property))
                {
                    tasks.Add(LoadRepositoryAsync(property));
                }
            }
            
            await Task.WhenAll(tasks);
        }
        
        public virtual async Task SaveAllAsync()
        {
            var tasks = new List<Task>();
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (IsRepositoryProperty(property))
                {
                    tasks.Add(SaveRepositoryAsync(property));
                }
            }
            
            await Task.WhenAll(tasks);
        }
        
        public virtual async Task ReloadAsync(string dataName)
        {
            var property = GetType().GetProperty(dataName);
            if (property == null || !IsRepositoryProperty(property))
            {
                throw new ArgumentException($"'{dataName}' is not a valid data name.");
            }
            
            await LoadRepositoryAsync(property);
        }
        
        private bool IsRepositoryProperty(PropertyInfo property)
        {
            var type = property.PropertyType;
            if (!type.IsGenericType) return false;
            
            var genericType = type.GetGenericTypeDefinition();
            return genericType == typeof(IDataRepository<,>) || 
                   genericType == typeof(ISingleDataRepository<>);
        }
        
        private async Task LoadRepositoryAsync(PropertyInfo property)
        {
            var dataType = GetDataType(property);
            var attribute = GetDataAttribute(dataType);
            
            if (attribute == null) return;
            
            var filePath = GetFilePath(attribute);
            var format = GetDataFormat(attribute);
            
            var rawData = await _rawDataProvider.LoadTextAsync(filePath);
            var loader = _loaderFactory.GetLoader(filePath, format);
            
            object repository;
            
            if (attribute is TableDataAttribute)
            {
                repository = LoadTableData(dataType, rawData, loader);
            }
            else
            {
                repository = LoadSingleData(dataType, rawData, loader);
            }
            
            property.SetValue(this, repository);
            _repositories[property.Name] = repository;
        }
        
        private async Task SaveRepositoryAsync(PropertyInfo property)
        {
            var repository = property.GetValue(this);
            if (repository == null) return;
            
            var dataType = GetDataType(property);
            var attribute = GetDataAttribute(dataType);
            
            if (attribute == null) return;
            
            var filePath = GetFilePath(attribute);
            var format = GetDataFormat(attribute);
            
            var loader = _loaderFactory.GetLoader(filePath, format);
            string rawData;
            
            if (attribute is TableDataAttribute)
            {
                rawData = SaveTableData(repository, dataType, loader);
            }
            else
            {
                rawData = SaveSingleData(repository, dataType, loader);
            }
            
            await _rawDataProvider.SaveTextAsync(filePath, rawData);
        }
        
        private Type GetDataType(PropertyInfo property)
        {
            var type = property.PropertyType;
            if (!type.IsGenericType) return null;
            
            var genericArgs = type.GetGenericArguments();
            return genericArgs.Last(); // Last generic argument is the data type
        }
        
        private Attribute GetDataAttribute(Type dataType)
        {
            return dataType.GetCustomAttribute<TableDataAttribute>() ??
                   (Attribute)dataType.GetCustomAttribute<SingleDataAttribute>();
        }
        
        private string GetFilePath(Attribute attribute)
        {
            return attribute switch
            {
                TableDataAttribute table => table.FilePath,
                SingleDataAttribute single => single.FilePath,
                _ => throw new InvalidOperationException("Unsupported attribute type.")
            };
        }
        
        private DataFormat GetDataFormat(Attribute attribute)
        {
            return attribute switch
            {
                TableDataAttribute table => table.Format,
                SingleDataAttribute single => single.Format,
                _ => DataFormat.Auto
            };
        }
        
        private object LoadTableData(Type dataType, string rawData, IDataLoader loader)
        {
            var keyType = dataType.GetInterface(typeof(ITableData<>).Name).GetGenericArguments()[0];
            var loadMethod = loader.GetType().GetMethod(nameof(IDataLoader.LoadTable))
                .MakeGenericMethod(keyType, dataType);
            
            var data = loadMethod.Invoke(loader, new object[] { rawData });
            
            var repositoryType = typeof(DataRepository<,>).MakeGenericType(keyType, dataType);
            return Activator.CreateInstance(repositoryType, data);
        }
        
        private object LoadSingleData(Type dataType, string rawData, IDataLoader loader)
        {
            var loadMethod = loader.GetType().GetMethod(nameof(IDataLoader.LoadSingle))
                .MakeGenericMethod(dataType);
            
            var data = loadMethod.Invoke(loader, new object[] { rawData });
            
            var repositoryType = typeof(SingleDataRepository<>).MakeGenericType(dataType);
            return Activator.CreateInstance(repositoryType, data);
        }
        
        private string SaveTableData(object repository, Type dataType, IDataLoader loader)
        {
            var keyType = dataType.GetInterface(typeof(ITableData<>).Name).GetGenericArguments()[0];
            var data = repository.GetType().GetMethod("GetAll").Invoke(repository, null);
            
            var saveMethod = loader.GetType().GetMethod(nameof(IDataLoader.SaveTable))
                .MakeGenericMethod(keyType, dataType);
            
            return (string)saveMethod.Invoke(loader, new object[] { data });
        }
        
        private string SaveSingleData(object repository, Type dataType, IDataLoader loader)
        {
            var data = repository.GetType().GetMethod("Get").Invoke(repository, null);
            
            var saveMethod = loader.GetType().GetMethod(nameof(IDataLoader.SaveSingle))
                .MakeGenericMethod(dataType);
            
            return (string)saveMethod.Invoke(loader, new object[] { data });
        }
    }
}
