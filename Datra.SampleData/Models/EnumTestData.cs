using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    [TableData("EnumTestData.csv", Format = DataFormat.Csv)]
    public partial class EnumTestData : ITableData<int>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Single enum without namespace
        public QualityType Quality { get; set; }

        // Array of enums without namespace
        public QualityType[] AllowedQualities { get; set; }

        // Single enum with namespace
        public StatType StatType { get; set; }

        // Array of enums with namespace
        public StatType[] StatTypes { get; set; }
    }
}