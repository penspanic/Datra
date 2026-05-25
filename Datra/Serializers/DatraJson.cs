#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Datra.Converters;

namespace Datra.Serializers
{
    /// <summary>
    /// Static JSON helpers backed by System.Text.Json (reflection mode).
    /// Convenience for editor / tooling / tests; not trim/AOT safe.
    /// Production code paths should prefer <see cref="SystemTextJsonDataSerializer"/>
    /// constructed with a source-gen <see cref="JsonSerializerContext"/>.
    /// </summary>
#if NET8_0_OR_GREATER
    [RequiresUnreferencedCode("DatraJson.* uses reflection-based STJ. Not trim/AOT safe.")]
    [RequiresDynamicCode("DatraJson.* may need runtime code generation.")]
#endif
    public static class DatraJson
    {
        private static readonly JsonSerializerOptions _options;

        public static JsonSerializerOptions Options => _options;

        static DatraJson()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
            };
            _options.Converters.Add(new DataRefSystemTextJsonConverterFactory());
            _options.Converters.Add(new JsonStringEnumConverter());
            _options.WithDatraContract();
        }

        public static void AddConverter(JsonConverter converter)
        {
            _options.Converters.Add(converter);
        }

        public static string Serialize<T>(T obj)
            => JsonSerializer.Serialize(obj, _options);

        public static string Serialize(object obj)
            => JsonSerializer.Serialize(obj, obj?.GetType() ?? typeof(object), _options);

        public static T Deserialize<T>(string json)
            => JsonSerializer.Deserialize<T>(json, _options)!;

        public static object? Deserialize(string json, Type type)
            => JsonSerializer.Deserialize(json, type, _options);

        public static async Task<T> DeserializeAsync<T>(Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = await reader.ReadToEndAsync();
            return Deserialize<T>(json);
        }
    }
}
