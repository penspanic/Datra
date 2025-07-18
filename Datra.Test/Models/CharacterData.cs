using Datra.Data.Attributes;
using Datra.Data.Interfaces;

namespace Datra.Test.Models
{
    [TableData("Characters.csv", Format = DataFormat.Csv)]
    public partial class CharacterData : ITableData<string>
    {
        public string Id { get; }
        public string Name { get; }
        public int Level { get; }
        public int Health { get; }
        public int Mana { get; }
        public int Strength { get; }
        public int Intelligence { get; }
        public int Agility { get; }
        public string ClassName { get; }
    }
}
