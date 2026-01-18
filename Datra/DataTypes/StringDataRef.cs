#nullable enable
using System;
using System.Collections.Generic;
using Datra.Interfaces;

namespace Datra.DataTypes
{
    public struct StringDataRef<T> : IDataRef<string, T> where T : class, ITableData<string>
    {
        public StringDataRef(string value) => Value = value;

        public string Value { get; set; }
        public Type DataType => typeof(T);
        public Type KeyType => typeof(string);
        public bool HasValue => !string.IsNullOrEmpty(Value);
        
        public object? GetKeyValue() => Value;
        
        public T? Evaluate(BaseDataContext dataContext)
        {
            if (dataContext == null)
                throw new ArgumentNullException(nameof(dataContext));

            if (string.IsNullOrEmpty(Value))
                return default;

            if (!dataContext.Repositories.TryGetValue(typeof(T).FullName, out var repositoryObj))
                throw new InvalidOperationException($"Repository for type {typeof(T).FullName} not found in DataContext.");

            if (repositoryObj is not ITableRepository<string, T> repository)
                throw new InvalidCastException($"Repository for type {typeof(T).FullName} is not of the expected type ITableRepository<string, T>.");

            return repository.TryGetLoaded(Value);
        }
    }
}