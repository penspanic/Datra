using System.Collections.Generic;

namespace Datra.Data.Generators.Models
{
    internal class DataModelInfo
    {
        public string TypeName { get; set; }
        public string PropertyName { get; set; }
        public bool IsTableData { get; set; }
        public string KeyType { get; set; }
        public string FilePath { get; set; }
        public string Format { get; set; }
        public List<PropertyInfo> Properties { get; set; } = new List<PropertyInfo>();
    }

    internal class PropertyInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsNullable { get; set; }
    }
}