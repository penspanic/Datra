#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Datra.Converters;
using Datra.Serializers;

namespace Datra.Repositories
{
    /// <summary>
    /// JSON 직렬화/역직렬화를 사용한 깊은 복사 유틸리티.
    /// Reflection 기반 (trim 불가능). Trim 환경에서 부르지 마라.
    /// </summary>
    public static class DeepCloner
    {
        private static readonly JsonSerializerOptions _options = CreateOptions();

        private static JsonSerializerOptions CreateOptions()
        {
            var opts = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                // Match Newtonsoft round-trip semantics: Datra data classes occasionally expose
                // public fields (e.g. PooledPrefab.Path). STJ excludes fields by default — turning
                // it on keeps DeepCloner.Clone behaviourally compatible.
                IncludeFields = true,
            };
            // DataRef structs expose System.Type-typed getters (DataType/KeyType) that STJ refuses
            // to serialize. The converter writes the raw key value, matching on-disk form.
            opts.Converters.Add(new DataRefSystemTextJsonConverterFactory());
            // Strip LocaleRef/NestedLocaleRef — computed at runtime, must not round-trip.
            opts.WithDatraContract();
            return opts;
        }

        /// <summary>
        /// 객체를 깊은 복사
        /// </summary>
#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Reflection-based round-trip clone. Not safe under trimming.")]
        [RequiresDynamicCode("Reflection-based round-trip clone may need runtime code generation.")]
#endif
        public static T Clone<T>(T source) where T : class
        {
            if (source == null)
                return null!;

            // Don't swallow serialization failures: returning `source` would alias the baseline
            // with the working copy and silently corrupt change tracking. Models that can't
            // round-trip through STJ (e.g., abstract types without [JsonPolymorphic]) must be
            // fixed at the model layer, not papered over here.
            var json = JsonSerializer.Serialize(source, _options);
            return JsonSerializer.Deserialize<T>(json, _options)!;
        }

        /// <summary>
        /// 두 값이 깊은 수준에서 동일한지 비교
        /// </summary>
#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Reflection-based deep compare. Not safe under trimming.")]
        [RequiresDynamicCode("Reflection-based deep compare may need runtime code generation.")]
#endif
        public static bool DeepEquals(object? a, object? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            if (a.GetType().IsValueType || a is string)
                return a.Equals(b);

            var jsonA = JsonSerializer.Serialize(a, a.GetType(), _options);
            var jsonB = JsonSerializer.Serialize(b, b.GetType(), _options);
            return jsonA == jsonB;
        }
    }
}
