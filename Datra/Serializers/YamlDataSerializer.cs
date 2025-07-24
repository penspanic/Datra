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
    /// Serializer for YAML format data
    /// </summary>
    public class YamlDataSerializer : IDataSerializer
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;
        
        public YamlDataSerializer()
        {
            var converter = new DataRefYamlConverter();
            
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .WithTypeConverter(converter)
                .Build();
            
            _serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .WithTypeConverter(converter)
                .Build();
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