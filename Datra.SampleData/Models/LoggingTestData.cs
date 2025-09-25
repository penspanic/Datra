using Datra.Attributes;
using Datra.Interfaces;
using Datra.DataTypes;

namespace Datra.SampleData.Models
{
    public enum SkillType
    {
        Physical,
        Magical,
        Support,
        Ultimate
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Mythic
    }

    [TableData("LoggingTest.csv", Format = DataFormat.Csv)]
    public partial class LoggingTestData : ITableData<string>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public float Power { get; set; }
        public bool IsActive { get; set; }
        public SkillType SkillType { get; set; }
        public Rarity Rarity { get; set; }
        public int[] Costs { get; set; }
        public SkillType[] AvailableTypes { get; set; }
        public StringDataRef<CharacterData> CharacterRef { get; set; }
        public IntDataRef<ItemData>[] RequiredItems { get; set; }
    }
}