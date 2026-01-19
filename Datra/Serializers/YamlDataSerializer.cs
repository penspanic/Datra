using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using Datra.Interfaces;

namespace Datra.Serializers
{
    /// <summary>
    /// Serializer for YAML format data with polymorphism support.
    /// </summary>
    public class YamlDataSerializer : IDataSerializer
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        /// <summary>
        /// Creates a new YamlDataSerializer with optional polymorphic type support and custom converters.
        /// </summary>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism. Null for no polymorphism.</param>
        /// <param name="customConverters">Custom type converters to register. These take priority over built-in polymorphic handling.</param>
        public YamlDataSerializer(
            IEnumerable<Type>? polymorphicBaseTypes = null,
            IEnumerable<IYamlTypeConverter>? customConverters = null)
        {
            _deserializer = DatraYamlSettings.CreateDeserializer(polymorphicBaseTypes, customConverters);
            _serializer = DatraYamlSettings.CreateSerializer(polymorphicBaseTypes, customConverters);
        }

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