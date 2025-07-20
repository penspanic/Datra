using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.Tests.Models
{
    [TableData("RefTestDataList.csv", Format = DataFormat.Csv)]
    public partial class RefTestData : ITableData<string>
    {
        public string Id { get; set; }
        public StringDataRef<CharacterData> CharacterRef { get; set; }
        public IntDataRef<ItemData> ItemRef { get; set; }
    }
}
