using Datra.Data.Attributes;
using Datra.Data.Interfaces;

namespace Datra.Unity.Sample.Models
{
    [TableData("Items.json")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int Price { get; }
        public ItemType Type { get; }
        public int Attack { get; }
        public int Defense { get; }
    }

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Material
    }
}