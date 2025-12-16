#nullable enable

using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.SampleData2.Models
{
    /// <summary>
    /// Data model for shop items
    /// </summary>
    [TableData("ShopItems.csv", Format = DataFormat.Csv)]
    public partial class ShopItemData : ITableData<string>
    {
        /// <summary>
        /// Unique identifier for the shop item
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display name of the item in shop
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Item price in gold
        /// </summary>
        public int Price { get; set; }

        /// <summary>
        /// Maximum purchase limit per day (0 = unlimited)
        /// </summary>
        public int DailyLimit { get; set; }

        /// <summary>
        /// Whether this item is currently available
        /// </summary>
        public bool IsAvailable { get; set; }
    }
}
