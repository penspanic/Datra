using System.Threading.Tasks;

namespace Datra.Data.Interfaces
{
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
    }
}
