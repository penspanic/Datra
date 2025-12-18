using System.Collections.Generic;
using Datra.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Datra.Serializers
{
    /// <summary>
    /// Common JSON serializer settings for Datra.
    /// Provides consistent settings for polymorphic type handling across all components.
    /// </summary>
    public static class DatraJsonSettings
    {
        /// <summary>
        /// Default settings for data serialization (with formatting and converters)
        /// </summary>
        public static JsonSerializerSettings CreateDefault() => new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new PortableTypeBinder(),
            Converters = new List<JsonConverter>
            {
                new DataRefJsonConverter(),
                new StringEnumConverter()
            }
        };

        /// <summary>
        /// Minimal settings for deep clone/comparison (no formatting, no custom converters)
        /// </summary>
        public static JsonSerializerSettings CreateForClone() => new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            SerializationBinder = new PortableTypeBinder(),
            NullValueHandling = NullValueHandling.Ignore
        };
    }
}
