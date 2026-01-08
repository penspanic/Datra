using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    public enum EnemyType
    {
        Normal,
        Elite,
        Boss,
        Miniboss
    }

    public enum Element
    {
        None,
        Fire,
        Water,
        Earth,
        Wind,
        Light,
        Dark
    }

    /// <summary>
    /// Enemy data stored in YAML format for testing YAML serialization.
    /// </summary>
    [TableData("Enemies.yaml", Format = DataFormat.Yaml)]
    public partial class EnemyData : ITableData<string>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public EnemyType Type { get; set; }
        public Element Element { get; set; }
        public int Level { get; set; }
        public int Health { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        public float Speed { get; set; }
        public double DropRate { get; set; }
        public bool IsFlyable { get; set; }
        public string[] Abilities { get; set; }
        public int[] DropItemIds { get; set; }

        // DataRef to dropped items
        public IntDataRef<ItemData> GuaranteedDrop { get; set; }
    }
}
