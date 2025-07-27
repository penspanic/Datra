using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    [TableData("Items.json")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int Price { get; set; }
        public ItemType Type { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
    }

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Material
    }
}