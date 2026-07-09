using System;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Text.Json.Serialization;
using Datra.Attributes;
using Datra.Utilities;
using YamlDotNet.Serialization;

namespace Datra.Serializers
{
    /// <summary>
    /// Factory for creating appropriate serializers based on data format.
    /// JSON path is backed by System.Text.Json.
    /// </summary>
    public class DataSerializerFactory
    {
        private readonly IDataSerializer _jsonSerializer;
        private readonly Func<IDataSerializer> _createYamlSerializer;
        private IDataSerializer? _yamlSerializer;

        /// <summary>
        /// Reflection-mode JSON. Trim/AOT-unsafe — explicit opt-in suitable for tooling/tests.
        /// </summary>
#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Default ctor uses reflection-based STJ. Construct with a JsonSerializerContext for trim/AOT safety.")]
        [RequiresDynamicCode("Default ctor uses reflection-based STJ.")]
#endif
        public DataSerializerFactory()
        {
            _jsonSerializer = SystemTextJsonDataSerializer.CreateReflectionUnsafe();
            _createYamlSerializer = CreateDefaultYamlSerializer;
        }

        /// <summary>
        /// Trim/AOT-safe JSON path via a source-gen JsonSerializerContext.
        /// </summary>
        public DataSerializerFactory(JsonSerializerContext jsonContext)
        {
            if (jsonContext == null) throw new ArgumentNullException(nameof(jsonContext));
            _jsonSerializer = new SystemTextJsonDataSerializer(jsonContext);
            _createYamlSerializer = CreateDefaultYamlSerializer;
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Polymorphic YAML/JSON via reflection.")]
        [RequiresDynamicCode("Polymorphic YAML/JSON via reflection.")]
#endif
        public DataSerializerFactory(IEnumerable<Type> polymorphicBaseTypes)
            : this(polymorphicBaseTypes, customYamlConverters: null)
        {
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Polymorphic YAML/JSON via reflection.")]
        [RequiresDynamicCode("Polymorphic YAML/JSON via reflection.")]
#endif
        public DataSerializerFactory(
            IEnumerable<Type>? polymorphicBaseTypes,
            IEnumerable<IYamlTypeConverter>? customYamlConverters)
            : this(polymorphicBaseTypes, customYamlConverters, excludedTypes: null)
        {
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Polymorphic YAML/JSON via reflection.")]
        [RequiresDynamicCode("Polymorphic YAML/JSON via reflection.")]
#endif
        public DataSerializerFactory(
            IEnumerable<Type>? polymorphicBaseTypes,
            IEnumerable<IYamlTypeConverter>? customYamlConverters,
            IEnumerable<Type>? excludedTypes)
        {
            _jsonSerializer = SystemTextJsonDataSerializer.CreateReflectionUnsafe();
            _createYamlSerializer = () => new YamlDataSerializer(polymorphicBaseTypes, customYamlConverters, excludedTypes);
        }

        public IDataSerializer GetSerializer(string filePath, DataFormat format = DataFormat.Auto)
        {
            if (format == DataFormat.Auto)
            {
                format = DataFormatHelper.DetectFormat(filePath);
            }

            return format switch
            {
                DataFormat.Json => _jsonSerializer,
                DataFormat.Yaml => GetYamlSerializer(),
                DataFormat.Csv => throw new NotSupportedException("CSV format should be handled by source-generated serializers, not by DataSerializer."),
                _ => throw new NotSupportedException($"Data format {format} is not supported.")
            };
        }

        private IDataSerializer GetYamlSerializer()
            => _yamlSerializer ?? (_yamlSerializer = _createYamlSerializer());

        private static IDataSerializer CreateDefaultYamlSerializer()
            => new YamlDataSerializer();
    }
}
