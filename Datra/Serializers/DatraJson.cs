#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Datra.Serializers
{
    /// <summary>
    /// Static JSON serialization API using Datra's default settings.
    /// Provides simple Serialize/Deserialize methods with consistent polymorphic type handling.
    /// </summary>
    public static class DatraJson
    {
        private static readonly JsonSerializerSettings _settings;

        /// <summary>
        /// Gets the underlying JsonSerializerSettings for advanced usage.
        /// </summary>
        public static JsonSerializerSettings Settings => _settings;

        static DatraJson()
        {
            _settings = DatraJsonSettings.CreateDefault();
        }

        /// <summary>
        /// Adds a custom JsonConverter to the settings.
        /// Use this to register project-specific converters.
        /// </summary>
        public static void AddConverter(JsonConverter converter)
        {
            _settings.Converters.Add(converter);
        }

        /// <summary>
        /// Serializes an object to JSON string.
        /// </summary>
        public static string Serialize<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, typeof(T), _settings);
        }

        /// <summary>
        /// Serializes an object to JSON string.
        /// </summary>
        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, _settings);
        }

        /// <summary>
        /// Deserializes JSON string to an object of type T.
        /// </summary>
        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, _settings)!;
        }

        /// <summary>
        /// Deserializes JSON string to an object of the specified type.
        /// </summary>
        public static object? Deserialize(string json, Type type)
        {
            return JsonConvert.DeserializeObject(json, type, _settings);
        }

        /// <summary>
        /// Deserializes JSON from a stream asynchronously.
        /// </summary>
        public static async Task<T> DeserializeAsync<T>(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            return Deserialize<T>(json);
        }
    }
}
