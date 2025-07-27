using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    public enum CharacterGrade
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [TableData("Characters.csv", Format = DataFormat.Csv)]
    public partial class CharacterData : ITableData<string>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Health { get; set; }
        public int Mana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Agility { get; set; }
        public string ClassName { get; set; }
        public CharacterGrade Grade { get; set; }
        public StatType[] Stats { get; set; } // Array of stat types
        public int[] UpgradeCosts { get; set; } // Array of upgrade costs for each level
    }
}
