#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.TypeInspectors;

namespace Datra.Converters
{
    /// <summary>
    /// Type inspector that filters out getter-only properties during serialization.
    /// Properties must have a setter to be serialized.
    /// </summary>
    public class WritablePropertiesTypeInspector : TypeInspectorSkeleton
    {
        private readonly ITypeInspector _innerTypeInspector;

        public WritablePropertiesTypeInspector(ITypeInspector innerTypeInspector)
        {
            _innerTypeInspector = innerTypeInspector ?? throw new ArgumentNullException(nameof(innerTypeInspector));
        }

        public override string GetEnumName(Type enumType, string name)
        {
            return _innerTypeInspector.GetEnumName(enumType, name);
        }

        public override string GetEnumValue(object enumValue)
        {
            return _innerTypeInspector.GetEnumValue(enumValue);
        }

        public override IEnumerable<IPropertyDescriptor> GetProperties(Type type, object? container)
        {
            return _innerTypeInspector.GetProperties(type, container)
                .Where(p => p.CanWrite);
        }
    }
}
