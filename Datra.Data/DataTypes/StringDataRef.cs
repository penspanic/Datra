using System;
using Datra.Data.Interfaces;

namespace Datra.Data.DataTypes
{
    public struct StringDataRef<T> where T : class, ITableData<string>
    {
        public string Value { get; set; }
        public T? Evaluate(BaseDataContext dataContext)
        {
            if (dataContext == null)
                throw new ArgumentNullException(nameof(dataContext));

            if (string.IsNullOrEmpty(Value))
                return default;

            if (!dataContext.Repositories.TryGetValue(typeof(T).FullName, out var repositoryObj))
                throw new InvalidOperationException($"Repository for type {typeof(T).FullName} not found in DataContext.");

            if (repositoryObj is not IDataRepository<string, T> repository)
                throw new InvalidCastException($"Repository for type {typeof(T).FullName} is not of the expected type IRepository<string, T>.");

            return repository.GetById(Value);
        }
    }
}