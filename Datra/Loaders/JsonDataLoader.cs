using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Datra.Interfaces;
using Datra.Converters;

namespace Datra.Loaders
{
    /// <summary>
    /// Loader for loading/saving data in JSON format
    /// </summary>
    public class JsonDataLoader : IDataLoader
    {
        private readonly JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new List<JsonConverter> { new DataRefJsonConverter() }
        };
        
        public T LoadSingle<T>(string text) where T : class, new()
        {
            return JsonConvert.DeserializeObject<T>(text, _settings) 
                   ?? throw new InvalidOperationException("Failed to deserialize JSON data.");
        }
        
        public Dictionary<TKey, T> LoadTable<TKey, T>(string text) 
            where T : class, ITableData<TKey>, new()
        {
            // Convert JSON array data to Dictionary
            var items = JsonConvert.DeserializeObject<List<T>>(text, _settings)
                       ?? throw new InvalidOperationException("Failed to deserialize JSON table data.");
            
            return items.ToDictionary(item => item.Id);
        }
        
        public string SaveSingle<T>(T data) where T : class
        {
            return JsonConvert.SerializeObject(data, _settings);
        }
        
        public string SaveTable<TKey, T>(Dictionary<TKey, T> table) 
            where T : class, ITableData<TKey>
        {
            // Convert Dictionary to array for saving (more readable format)
            var items = table.Values.ToList();
            return JsonConvert.SerializeObject(items, _settings);
        }
    }
}
