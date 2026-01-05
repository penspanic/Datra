using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Datra.Helpers
{
    /// <summary>
    /// Helper class for string template formatting with named placeholders.
    /// Supports {PropertyName} style placeholders that are replaced with values from anonymous objects or dictionaries.
    /// </summary>
    public static class StringTemplateHelper
    {
        // Cache for property info to avoid repeated reflection
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> PropertyCache = new();

        // Regex to find placeholders like {Name}, {Value}, etc.
        private static readonly Regex PlaceholderRegex = new(@"\{([a-zA-Z_][a-zA-Z0-9_]*)\}", RegexOptions.Compiled);

        /// <summary>
        /// Formats a template string by replacing placeholders with values from an anonymous object.
        /// </summary>
        /// <param name="template">The template string with {PropertyName} placeholders</param>
        /// <param name="values">An anonymous object or class instance containing the values</param>
        /// <returns>The formatted string with placeholders replaced</returns>
        /// <example>
        /// var result = StringTemplateHelper.Format(
        ///     "Deal {Damage}% damage to {Count} enemies",
        ///     new { Damage = 150, Count = 3 }
        /// );
        /// // Result: "Deal 150% damage to 3 enemies"
        /// </example>
        public static string Format(string template, object? values)
        {
            if (string.IsNullOrEmpty(template))
                return template ?? string.Empty;

            if (values == null)
                return template;

            var type = values.GetType();
            var properties = GetCachedProperties(type);

            return PlaceholderRegex.Replace(template, match =>
            {
                var propertyName = match.Groups[1].Value;

                foreach (var prop in properties)
                {
                    if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        var value = prop.GetValue(values);
                        return value?.ToString() ?? string.Empty;
                    }
                }

                // Placeholder not found, return as-is
                return match.Value;
            });
        }

        /// <summary>
        /// Formats a template string by replacing placeholders with values from a dictionary.
        /// </summary>
        /// <param name="template">The template string with {Key} placeholders</param>
        /// <param name="values">A dictionary containing key-value pairs</param>
        /// <returns>The formatted string with placeholders replaced</returns>
        /// <example>
        /// var values = new Dictionary&lt;string, object&gt; { { "Damage", 150 }, { "Count", 3 } };
        /// var result = StringTemplateHelper.Format(
        ///     "Deal {Damage}% damage to {Count} enemies",
        ///     values
        /// );
        /// // Result: "Deal 150% damage to 3 enemies"
        /// </example>
        public static string Format(string template, IDictionary<string, object?> values)
        {
            if (string.IsNullOrEmpty(template))
                return template ?? string.Empty;

            if (values == null || values.Count == 0)
                return template;

            return PlaceholderRegex.Replace(template, match =>
            {
                var key = match.Groups[1].Value;

                // Try exact match first
                if (values.TryGetValue(key, out var value))
                    return value?.ToString() ?? string.Empty;

                // Try case-insensitive match
                foreach (var kvp in values)
                {
                    if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
                        return kvp.Value?.ToString() ?? string.Empty;
                }

                // Placeholder not found, return as-is
                return match.Value;
            });
        }

        /// <summary>
        /// Extracts all placeholder names from a template string.
        /// </summary>
        /// <param name="template">The template string to analyze</param>
        /// <returns>A list of placeholder names found in the template</returns>
        public static IReadOnlyList<string> GetPlaceholders(string template)
        {
            if (string.IsNullOrEmpty(template))
                return Array.Empty<string>();

            var result = new List<string>();
            var matches = PlaceholderRegex.Matches(template);

            foreach (Match match in matches)
            {
                var name = match.Groups[1].Value;
                if (!result.Contains(name))
                    result.Add(name);
            }

            return result;
        }

        /// <summary>
        /// Checks if a template string contains any placeholders.
        /// </summary>
        /// <param name="template">The template string to check</param>
        /// <returns>True if the template contains placeholders</returns>
        public static bool HasPlaceholders(string template)
        {
            if (string.IsNullOrEmpty(template))
                return false;

            return PlaceholderRegex.IsMatch(template);
        }

        private static PropertyInfo[] GetCachedProperties(Type type)
        {
            return PropertyCache.GetOrAdd(type, t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        }
    }
}
