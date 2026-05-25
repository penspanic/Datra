#nullable enable
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Datra.DataTypes;
using Datra.Localization;

namespace Datra.Serializers
{
    /// <summary>
    /// STJ option helpers that re-create Datra's Newtonsoft-era contract semantics.
    /// </summary>
    internal static class DatraJsonHelpers
    {
        /// <summary>
        /// Strips properties whose CLR type is a Datra computed locale type
        /// (<see cref="LocaleRef"/>, <see cref="NestedLocaleRef"/>) — these are evaluated at
        /// runtime from key fragments and must not round-trip via JSON.
        /// Works for both reflection-based and source-gen resolvers.
        /// </summary>
        public static JsonSerializerOptions WithDatraContract(this JsonSerializerOptions options)
        {
            // Wrap whatever resolver the consumer set (default reflection or a source-gen context).
            var inner = options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver();
            options.TypeInfoResolver = inner.WithAddedModifier(typeInfo =>
            {
                if (typeInfo.Kind != JsonTypeInfoKind.Object)
                    return;

                for (int i = typeInfo.Properties.Count - 1; i >= 0; i--)
                {
                    var prop = typeInfo.Properties[i];
                    if (prop.PropertyType == typeof(LocaleRef) ||
                        prop.PropertyType == typeof(NestedLocaleRef))
                    {
                        typeInfo.Properties.RemoveAt(i);
                    }
                }
            });
            return options;
        }
    }
}
