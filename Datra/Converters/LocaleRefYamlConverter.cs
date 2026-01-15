#nullable enable
using System;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using Datra.DataTypes;
using Datra.Localization;

namespace Datra.Converters
{
    /// <summary>
    /// YAML converter for LocaleRef and NestedLocaleRef types.
    /// - LocaleRef: Serializes as Key string, deserializes from string
    /// - NestedLocaleRef: Skips serialization (static readonly), deserializes as empty
    /// </summary>
    public class LocaleRefYamlConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type)
        {
            return type == typeof(LocaleRef) || type == typeof(NestedLocaleRef);
        }

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            if (type == typeof(LocaleRef))
            {
                return ReadLocaleRef(parser);
            }
            else if (type == typeof(NestedLocaleRef))
            {
                return ReadNestedLocaleRef(parser);
            }

            throw new YamlException($"Unsupported type: {type}");
        }

        private LocaleRef ReadLocaleRef(IParser parser)
        {
            if (parser.TryConsume<Scalar>(out var scalar))
            {
                if (string.IsNullOrEmpty(scalar.Value))
                {
                    return default;
                }
                return new LocaleRef { Key = scalar.Value };
            }

            throw new YamlException("Expected scalar for LocaleRef");
        }

        private NestedLocaleRef ReadNestedLocaleRef(IParser parser)
        {
            if (parser.TryConsume<Scalar>(out _))
            {
                return default;
            }

            throw new YamlException("Expected scalar for NestedLocaleRef");
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            if (type == typeof(LocaleRef))
            {
                WriteLocaleRef(emitter, value);
            }
            else if (type == typeof(NestedLocaleRef))
            {
                WriteNestedLocaleRef(emitter, value);
            }
        }

        private void WriteLocaleRef(IEmitter emitter, object? value)
        {
            if (value == null)
            {
                emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Plain, true, false));
                return;
            }

            var localeRef = (LocaleRef)value;
            var key = localeRef.Key ?? string.Empty;
            emitter.Emit(new Scalar(null, null, key, ScalarStyle.Plain, true, false));
        }

        private void WriteNestedLocaleRef(IEmitter emitter, object? value)
        {
            // NestedLocaleRef should not be serialized - emit empty string
            // This handles static readonly properties that shouldn't be in output
            emitter.Emit(new Scalar(null, null, string.Empty, ScalarStyle.Plain, true, false));
        }
    }
}
