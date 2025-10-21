using System.Collections.Generic;
using System.Linq;

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

        /// <summary>
        /// The physical file path of the source model class (e.g., /path/to/CharacterData.cs)
        /// Used for emitting physical files next to the source
        /// </summary>
        public string SourceFilePath { get; set; }

        /// <summary>
        /// Get properties that should be included in serialization.
        /// Excludes: FixedLocale (computed properties that cannot be serialized)
        /// </summary>
        public IEnumerable<PropertyInfo> GetSerializableProperties()
        {
            return Properties.Where(p => !p.IsFixedLocale);
        }

        /// <summary>
        /// Get properties that should be included in constructor parameters.
        /// Excludes: FixedLocale (computed properties that cannot be assigned)
        /// </summary>
        public IEnumerable<PropertyInfo> GetConstructorProperties()
        {
            return Properties.Where(p => !p.IsFixedLocale);
        }
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

        // Enhanced type metadata for better code generation
        public string CleanTypeName { get; set; } // Type name without global:: prefix
        public string CleanElementType { get; set; } // Element type without global:: prefix for arrays
        public bool IsEnum { get; set; }
        public bool IsValueType { get; set; }
        public bool ElementIsEnum { get; set; } // For array elements
        public bool ElementIsValueType { get; set; } // For array elements

        /// <summary>
        /// Property marked with [FixedLocale] attribute.
        /// These are computed properties and should be EXCLUDED from:
        /// - Constructor parameters (cannot be assigned)
        /// - Serialization/Deserialization (computed at runtime)
        /// - CSV column generation (not stored in files)
        ///
        /// Example: public LocaleRef Name => LocaleRef.CreateFixed(nameof(CharacterData), Id, nameof(Name));
        /// </summary>
        public bool IsFixedLocale { get; set; }

        // For backward compatibility
        public bool IsStringDataRef => IsDataRef && DataRefKeyType == "string";
    }
}