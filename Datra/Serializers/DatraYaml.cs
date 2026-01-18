#nullable enable
using System;
using System.IO;
using YamlDotNet.Serialization;

namespace Datra.Serializers
{
    /// <summary>
    /// Static YAML serialization API using Datra's default settings.
    /// Provides simple Serialize/Deserialize methods with consistent type handling.
    /// Uses PascalCase naming convention for consistency with Datra's JSON serialization.
    /// </summary>
    public static class DatraYaml
    {
        private static readonly ISerializer _serializer;
        private static readonly IDeserializer _deserializer;

        static DatraYaml()
        {
            _deserializer = DatraYamlSettings.CreateDeserializer();
            _serializer = DatraYamlSettings.CreateSerializer();
        }

        /// <summary>
        /// Serializes an object to YAML string.
        /// </summary>
        public static string Serialize<T>(T obj)
        {
            return _serializer.Serialize(obj);
        }

        /// <summary>
        /// Serializes an object to YAML string.
        /// </summary>
        public static string Serialize(object obj)
        {
            return _serializer.Serialize(obj);
        }

        /// <summary>
        /// Deserializes YAML string to an object of type T.
        /// </summary>
        public static T Deserialize<T>(string yaml)
        {
            using var reader = new StringReader(yaml);
            return _deserializer.Deserialize<T>(reader)!;
        }

        /// <summary>
        /// Deserializes YAML string to an object of the specified type.
        /// </summary>
        public static object? Deserialize(string yaml, Type type)
        {
            using var reader = new StringReader(yaml);
            return _deserializer.Deserialize(reader, type);
        }
    }
}
