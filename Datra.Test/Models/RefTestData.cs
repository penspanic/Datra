using Datra.Data.Attributes;
using Datra.Data.Interfaces;

namespace Datra.Test.Models
{
    [TableData("Characters.csv", Format = DataFormat.Csv)]
    public partial class RefTestData : ITableData<string>
    {
        public string Id { get; }
    }
}