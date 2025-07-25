using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.Interfaces;
using Datra.Serializers;
using Datra.Repositories;

namespace Datra
{
    /// <summary>
    /// Base class for all DataContext
    /// Classes generated by Source Generator inherit from this
    /// </summary>
    public abstract class BaseDataContext : IDataContext
    {
        internal readonly Dictionary<string, object> Repositories = new();

        private readonly IRawDataProvider _rawDataProvider;
        private readonly DataSerializerFactory _serializerFactory;
        
        protected IRawDataProvider RawDataProvider => _rawDataProvider;
        protected DataSerializerFactory SerializerFactory => _serializerFactory;
        
        protected BaseDataContext(IRawDataProvider rawDataProvider, DataSerializerFactory serializerFactory)
        {
            _rawDataProvider = rawDataProvider ?? throw new ArgumentNullException(nameof(rawDataProvider));
            _serializerFactory = serializerFactory ?? throw new ArgumentNullException(nameof(serializerFactory));
        }
        
        /// <summary>
        /// Initialize repositories - implemented by derived classes
        /// </summary>
        protected virtual void InitializeRepositories()
        {
            // Default implementation is empty - overridden by Source Generator
        }
        
        /// <summary>
        /// Register a repository for a specific data type
        /// </summary>
        protected void RegisterRepository<TKey, TData>(string propertyName, IDataRepository<TKey, TData> repository) 
            where TData : class, ITableData<TKey>
        {
            Repositories[propertyName] = repository;
            Repositories[typeof(TData).FullName] = repository;
        }
        
        /// <summary>
        /// Register a single data repository
        /// </summary>
        protected void RegisterSingleRepository<TData>(string propertyName, ISingleDataRepository<TData> repository) 
            where TData : class
        {
            Repositories[propertyName] = repository;
            Repositories[typeof(TData).FullName] = repository;
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
            var serializer = _serializerFactory.GetSerializer(filePath, format);
            
            object repository;
            
            if (attribute is TableDataAttribute)
            {
                repository = LoadTableData(dataType, rawData, serializer);
            }
            else
            {
                repository = LoadSingleData(dataType, rawData, serializer);
            }
            
            property.SetValue(this, repository);
            Repositories[property.Name] = repository;
            Repositories[dataType.FullName] = repository;
        }
        
        private async Task SaveRepositoryAsync(PropertyInfo property)
        {
            var repository = property.GetValue(this);
            if (repository == null) return;
            
            // Check if repository has SaveAsync method and use it directly
            var saveAsyncMethod = repository.GetType().GetMethod("SaveAsync");
            if (saveAsyncMethod != null)
            {
                var task = (Task)saveAsyncMethod.Invoke(repository, null);
                await task;
                return;
            }
            
            // Fallback to old behavior for repositories without SaveAsync
            var dataType = GetDataType(property);
            var attribute = GetDataAttribute(dataType);
            
            if (attribute == null) return;
            
            var filePath = GetFilePath(attribute);
            var format = GetDataFormat(attribute);
            
            var serializer = _serializerFactory.GetSerializer(filePath, format);
            string rawData;
            
            if (attribute is TableDataAttribute)
            {
                rawData = SaveTableData(repository, dataType, serializer);
            }
            else
            {
                rawData = SaveSingleData(repository, dataType, serializer);
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
        
        private object LoadTableData(Type dataType, string rawData, IDataSerializer serializer)
        {
            var keyType = dataType.GetInterface(typeof(ITableData<>).Name).GetGenericArguments()[0];
            var loadMethod = serializer.GetType().GetMethod(nameof(IDataSerializer.DeserializeTable))
                .MakeGenericMethod(keyType, dataType);
            
            var data = loadMethod.Invoke(serializer, new object[] { rawData });
            
            var repositoryType = typeof(DataRepository<,>).MakeGenericType(keyType, dataType);
            return Activator.CreateInstance(repositoryType, data);
        }
        
        private object LoadSingleData(Type dataType, string rawData, IDataSerializer serializer)
        {
            var loadMethod = serializer.GetType().GetMethod(nameof(IDataSerializer.DeserializeSingle))
                .MakeGenericMethod(dataType);
            
            var data = loadMethod.Invoke(serializer, new object[] { rawData });
            
            var repositoryType = typeof(SingleDataRepository<>).MakeGenericType(dataType);
            return Activator.CreateInstance(repositoryType, data);
        }
        
        private string SaveTableData(object repository, Type dataType, IDataSerializer serializer)
        {
            var keyType = dataType.GetInterface(typeof(ITableData<>).Name).GetGenericArguments()[0];
            var data = repository.GetType().GetMethod("GetAll").Invoke(repository, null);
            
            var saveMethod = serializer.GetType().GetMethod(nameof(IDataSerializer.SerializeTable))
                .MakeGenericMethod(keyType, dataType);
            
            return (string)saveMethod.Invoke(serializer, new object[] { data });
        }
        
        private string SaveSingleData(object repository, Type dataType, IDataSerializer serializer)
        {
            var data = repository.GetType().GetMethod("Get").Invoke(repository, null);
            
            var saveMethod = serializer.GetType().GetMethod(nameof(IDataSerializer.SerializeSingle))
                .MakeGenericMethod(dataType);
            
            return (string)saveMethod.Invoke(serializer, new object[] { data });
        }
    }
}
