using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Datra.Interfaces;
using Datra.Converters;

namespace Datra.Serializers
{
    /// <summary>
    /// Serializer for YAML format data with polymorphism support.
    /// </summary>
    public class YamlDataSerializer : IDataSerializer
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;
        private readonly PortableTypeResolver _typeResolver;
        private readonly HashSet<Type> _polymorphicBaseTypes;

        /// <summary>
        /// Creates a new YamlDataSerializer with default settings.
        /// </summary>
        public YamlDataSerializer() : this(null)
        {
        }

        /// <summary>
        /// Creates a new YamlDataSerializer with the specified polymorphic base types.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that should be serialized with $type field for polymorphism.</param>
        public YamlDataSerializer(IEnumerable<Type>? polymorphicBaseTypes)
        {
            _typeResolver = new PortableTypeResolver();
            _polymorphicBaseTypes = polymorphicBaseTypes != null
                ? new HashSet<Type>(polymorphicBaseTypes)
                : new HashSet<Type>();

            var dataRefConverter = new DataRefYamlConverter();
            var localeRefConverter = new LocaleRefYamlConverter();
            var polymorphicConverter = new PolymorphicYamlTypeConverter(_typeResolver, _polymorphicBaseTypes);

            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(polymorphicConverter)
                .WithTypeConverter(dataRefConverter)
                .WithTypeConverter(localeRefConverter)
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeInspector(inner => new WritablePropertiesTypeInspector(inner))
                .WithTypeConverter(polymorphicConverter)
                .WithTypeConverter(dataRefConverter)
                .WithTypeConverter(localeRefConverter)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();
        }

        /// <summary>
        /// Registers a base type for polymorphic serialization.
        /// Objects of this type or its derived types will include $type field in YAML.
        /// </summary>
        /// <typeparam name="T">The base type to register.</typeparam>
        public void RegisterPolymorphicType<T>()
        {
            _polymorphicBaseTypes.Add(typeof(T));
        }

        /// <summary>
        /// Registers a base type for polymorphic serialization.
        /// Objects of this type or its derived types will include $type field in YAML.
        /// </summary>
        /// <param name="type">The base type to register.</param>
        public void RegisterPolymorphicType(Type type)
        {
            _polymorphicBaseTypes.Add(type);
        }

        /// <summary>
        /// Gets the registered polymorphic base types.
        /// </summary>
        public IReadOnlyCollection<Type> PolymorphicBaseTypes => _polymorphicBaseTypes;

        public T DeserializeSingle<T>(string text) where T : class, new()
        {
            using var reader = new StringReader(text);
            return _deserializer.Deserialize<T>(reader)
                   ?? throw new InvalidOperationException("Failed to deserialize YAML data.");
        }

        public Dictionary<TKey, T> DeserializeTable<TKey, T>(string text)
            where T : class, ITableData<TKey>, new()
        {
            using var reader = new StringReader(text);
            var items = _deserializer.Deserialize<List<T>>(reader)
                       ?? throw new InvalidOperationException("Failed to deserialize YAML table data.");

            return items.ToDictionary(item => item.Id);
        }

        public string SerializeSingle<T>(T data) where T : class
        {
            return _serializer.Serialize(data);
        }

        public string SerializeTable<TKey, T>(Dictionary<TKey, T> table)
            where T : class, ITableData<TKey>
        {
            var items = table.Values.ToList();
            return _serializer.Serialize(items);
        }
    }
}