using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    [TableData("MetadataTestData.csv", Format = DataFormat.Csv)]
    public partial class MetadataTestData : ITableData<string>
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public string Description { get; set; }
    }
}