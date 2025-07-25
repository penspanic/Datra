using System.Collections.Generic;

namespace Datra.Generators.Models
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
        public bool IsDataRef { get; set; }
        public string DataRefKeyType { get; set; } // e.g., "string", "int"
        public string DataRefTargetType { get; set; } // e.g., "CharacterData" for DataRef<CharacterData>
        public bool IsArray { get; set; }
        public string ElementType { get; set; } // e.g., "int" for int[], "IntDataRef<ItemData>" for IntDataRef<ItemData>[]
        
        // For backward compatibility
        public bool IsStringDataRef => IsDataRef && DataRefKeyType == "string";
    }
}