using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Datra.Data.Interfaces;
using Datra.Data.Converters;

namespace Datra.Data.Loaders
{
    /// <summary>
    /// Loader for loading/saving data in YAML format
    /// </summary>
    public class YamlDataLoader : IDataLoader
    {
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;
        
        public YamlDataLoader()
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
        
        public T LoadSingle<T>(string text) where T : class, new()
        {
            using var reader = new StringReader(text);
            return _deserializer.Deserialize<T>(reader)
                   ?? throw new InvalidOperationException("Failed to deserialize YAML data.");
        }
        
        public Dictionary<TKey, T> LoadTable<TKey, T>(string text) 
            where T : class, ITableData<TKey>, new()
        {
            using var reader = new StringReader(text);
            var items = _deserializer.Deserialize<List<T>>(reader)
                       ?? throw new InvalidOperationException("Failed to deserialize YAML table data.");
            
            return items.ToDictionary(item => item.Id);
        }
        
        public string SaveSingle<T>(T data) where T : class
        {
            return _serializer.Serialize(data);
        }
        
        public string SaveTable<TKey, T>(Dictionary<TKey, T> table) 
            where T : class, ITableData<TKey>
        {
            var items = table.Values.ToList();
            return _serializer.Serialize(items);
        }
    }
}