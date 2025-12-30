#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra.Interfaces
{
    /// <summary>
    /// The kind of repository for a data type
    /// </summary>
    public enum RepositoryKind
    {
        /// <summary>Single data instance (ISingleDataRepository)</summary>
        Single,
        /// <summary>Table data with key-value pairs (IKeyValueDataRepository)</summary>
        Table,
        /// <summary>Asset data with file-based storage (IAssetRepository)</summary>
        Asset
    }

    /// <summary>
    /// Context interface for managing all static data in the game
    /// Similar pattern to EntityFramework's DbContext
    /// </summary>
    public interface IDataContext
    {
        /// <summary>
        /// Load all data asynchronously
        /// </summary>
        Task LoadAllAsync();

        /// <summary>
        /// Save all data asynchronously
        /// </summary>
        Task SaveAllAsync();

        /// <summary>
        /// Reload specific dataset
        /// </summary>
        Task ReloadAsync(string dataName);

        /// <summary>
        /// Get information about all data types managed by this context
        /// </summary>
        IReadOnlyList<DataTypeInfo> GetDataTypeInfos();
    }

    /// <summary>
    /// Information about a data type managed by the context
    /// </summary>
    public class DataTypeInfo
    {
        /// <summary>
        /// The full type name
        /// </summary>
        public string TypeName { get; private set; }

        /// <summary>
        /// The System.Type of the data
        /// </summary>
        public System.Type DataType { get; private set; }

        /// <summary>
        /// The configured file path where the data should be loaded from
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// The actual file path where the data was loaded from (if loaded)
        /// </summary>
        public string? LoadedFilePath { get; private set; }

        /// <summary>
        /// Whether the data has been loaded
        /// </summary>
        public bool IsLoaded { get; private set; }

        /// <summary>
        /// The kind of repository (Single, Table, or Asset)
        /// </summary>
        public RepositoryKind RepositoryKind { get; private set; }

        /// <summary>
        /// The property name in the context
        /// </summary>
        public string PropertyName { get; private set; }

        /// <summary>
        /// Constructor for creating DataTypeInfo
        /// </summary>
        public DataTypeInfo(string typeName, System.Type dataType, string filePath,
            string propertyName, RepositoryKind repositoryKind)
        {
            TypeName = typeName ?? throw new System.ArgumentNullException(nameof(typeName));
            DataType = dataType ?? throw new System.ArgumentNullException(nameof(dataType));
            FilePath = filePath ?? throw new System.ArgumentNullException(nameof(filePath));
            PropertyName = propertyName ?? throw new System.ArgumentNullException(nameof(propertyName));
            RepositoryKind = repositoryKind;
            IsLoaded = false;
            LoadedFilePath = null;
        }

        /// <summary>
        /// Updates the loaded state and file path after loading
        /// </summary>
        public void UpdateLoadedState(string loadedFilePath)
        {
            LoadedFilePath = loadedFilePath;
            IsLoaded = true;
        }
    }
}
