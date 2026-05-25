#nullable enable
using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datra.Converters;
using Datra.Interfaces;

namespace Datra.Serializers
{
    /// <summary>
    /// Trim- and AOT-safe JSON serializer. Built on System.Text.Json source-gen.
    /// The consumer supplies a <see cref="JsonSerializerContext"/> that declares every
    /// <c>[TableData]</c> / <c>[SingleData]</c> model (plus their list/array/dictionary forms)
    /// via <c>[JsonSerializable]</c>; the context's <see cref="JsonSerializerContext.Options"/>
    /// drive all serialization.
    /// </summary>
    /// <remarks>
    /// Polymorphism via legacy <c>$type</c> tags is not implemented; declare derived types
    /// on the base via STJ's <c>[JsonDerivedType]</c> on the source-gen context if needed.
    /// </remarks>
    public sealed class SystemTextJsonDataSerializer : IDataSerializer
    {
        private readonly JsonSerializerOptions _options;

        public SystemTextJsonDataSerializer(JsonSerializerContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            // Derive options from the context, then layer Datra's required converters and contract modifier.
            _options = new JsonSerializerOptions(context.Options);
            if (!_options.Converters.Any(c => c is DataRefSystemTextJsonConverterFactory))
            {
                _options.Converters.Add(new DataRefSystemTextJsonConverterFactory());
            }
            _options.WithDatraContract();
        }

        private SystemTextJsonDataSerializer(JsonSerializerOptions options)
        {
            _options = options;
        }

        /// <summary>
        /// Reflection-mode serializer. Explicitly opt-in — not trim/AOT safe.
        /// Used by tooling, editors, and tests where source-gen contexts are impractical.
        /// </summary>
#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Reflection-based JSON serialization. Provide a JsonSerializerContext for trim/AOT safety.")]
        [RequiresDynamicCode("Reflection-based JSON serialization may need runtime code generation.")]
#endif
        public static SystemTextJsonDataSerializer CreateReflectionUnsafe()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                NumberHandling = JsonNumberHandling.AllowReadingFromString,
            };
            opts.Converters.Add(new DataRefSystemTextJsonConverterFactory());
            opts.Converters.Add(new JsonStringEnumConverter());
            opts.WithDatraContract();
            return new SystemTextJsonDataSerializer(opts);
        }

        public T DeserializeSingle<T>(string text) where T : class, new()
        {
            return JsonSerializer.Deserialize<T>(text, _options)
                   ?? throw new InvalidOperationException("Failed to deserialize JSON data.");
        }

        public Dictionary<TKey, T> DeserializeTable<TKey, T>(string text)
            where T : class, ITableData<TKey>, new()
        {
            var items = JsonSerializer.Deserialize<List<T>>(text, _options)
                        ?? throw new InvalidOperationException("Failed to deserialize JSON table data.");
            var dict = new Dictionary<TKey, T>(items.Count);
            foreach (var item in items)
            {
                dict[item.Id] = item;
            }
            return dict;
        }

        public string SerializeSingle<T>(T data) where T : class
        {
            return JsonSerializer.Serialize(data, _options);
        }

        public string SerializeTable<TKey, T>(Dictionary<TKey, T> table)
            where T : class, ITableData<TKey>
        {
            var items = table.Values.ToList();
            return JsonSerializer.Serialize(items, _options);
        }
    }
}
