using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    [TableData("NewPropertyTestData.csv", Format = DataFormat.Csv)]
    public partial class NewPropertyTestData : ITableData<string>
    {
        public string Id { get; set; }

        // New property between Id and Name (doesn't exist in CSV)
        public string Category { get; set; }

        public string Name { get; set; }

        // New property between Name and Level (doesn't exist in CSV)
        public int Health { get; set; }

        public int Level { get; set; }

        // New property after Level (doesn't exist in CSV)
        public int Attack { get; set; }
    }
}