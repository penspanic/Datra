using System.Collections.Generic;
using System.Reflection;
using Datra.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

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
            ContractResolver = new WritablePropertiesOnlyContractResolver(),
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

    /// <summary>
    /// Contract resolver that only serializes properties with setters.
    /// Excludes getter-only properties like generated Ref and LocaleRef properties.
    /// Anonymous types are exempted (all their properties are getter-only).
    /// </summary>
    public class WritablePropertiesOnlyContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (member is PropertyInfo propInfo)
            {
                // Anonymous types have only getter-only properties — always serialize them
                if (!propInfo.CanWrite && !IsAnonymousType(propInfo.DeclaringType))
                {
                    property.ShouldSerialize = _ => false;
                }
            }

            return property;
        }

        private static bool IsAnonymousType(System.Type type)
        {
            return type != null
                && type.Namespace == null
                && type.IsSealed
                && type.IsNotPublic
                && type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false);
        }
    }
}
