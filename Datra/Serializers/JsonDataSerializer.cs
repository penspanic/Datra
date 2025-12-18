using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Datra.Interfaces;
using Datra.Converters;

namespace Datra.Serializers
{
    /// <summary>
    /// Serializer for JSON format data
    /// </summary>
    public class JsonDataSerializer : IDataSerializer
    {
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            // Enable polymorphic type handling - embeds $type for abstract/interface types
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new List<JsonConverter>
            {
                new DataRefJsonConverter(),
                new Newtonsoft.Json.Converters.StringEnumConverter()
            }
        };
        
        public T DeserializeSingle<T>(string text) where T : class, new()
        {
            return JsonConvert.DeserializeObject<T>(text, _settings) 
                   ?? throw new InvalidOperationException("Failed to deserialize JSON data.");
        }
        
        public Dictionary<TKey, T> DeserializeTable<TKey, T>(string text) 
            where T : class, ITableData<TKey>, new()
        {
            // Convert JSON array data to Dictionary
            var items = JsonConvert.DeserializeObject<List<T>>(text, _settings)
                       ?? throw new InvalidOperationException("Failed to deserialize JSON table data.");
            
            return items.ToDictionary(item => item.Id);
        }
        
        public string SerializeSingle<T>(T data) where T : class
        {
            return JsonConvert.SerializeObject(data, _settings);
        }
        
        public string SerializeTable<TKey, T>(Dictionary<TKey, T> table) 
            where T : class, ITableData<TKey>
        {
            // Convert Dictionary to array for saving (more readable format)
            var items = table.Values.ToList();
            return JsonConvert.SerializeObject(items, _settings);
        }
    }
}
