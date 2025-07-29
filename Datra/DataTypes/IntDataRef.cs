using System;
using System.Collections.Generic;
using Datra.Interfaces;

namespace Datra.DataTypes
{
    public struct IntDataRef<T> : IDataRef<int, T> where T : class, ITableData<int>
    {
        public int Value { get; set; }
        
        public Type DataType => typeof(T);
        
        public Type KeyType => typeof(int);
        
        public bool HasValue => Value != 0; // Assuming 0 is not a valid ID
        
        public object? GetKeyValue() => Value;
        
        public T? Evaluate(BaseDataContext dataContext)
        {
            if (dataContext == null)
                throw new ArgumentNullException(nameof(dataContext));

            if (Value == 0)
                return default;

            if (!dataContext.Repositories.TryGetValue(typeof(T).FullName, out var repositoryObj))
                throw new InvalidOperationException($"Repository for type {typeof(T).FullName} not found in DataContext.");

            if (repositoryObj is not IDataRepository<int, T> repository)
                throw new InvalidCastException($"Repository for type {typeof(T).FullName} is not of the expected type IRepository<int, T>.");

            return repository.GetValueOrDefault(Value);
        }
    }
}