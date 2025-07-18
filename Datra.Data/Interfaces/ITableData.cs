namespace Datra.Data.Interfaces
{
    /// <summary>
    /// Interface for Key-Value table data
    /// </summary>
    /// <typeparam name="TKey">Key type (int, string, etc.)</typeparam>
    public interface ITableData<TKey>
    {
        /// <summary>
        /// Unique identifier of the data
        /// </summary>
        TKey Id { get; }
    }
}
