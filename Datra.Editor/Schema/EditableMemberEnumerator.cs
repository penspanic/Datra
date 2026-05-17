#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Attributes;

namespace Datra.Editor.Schema
{
    /// <summary>
    /// Single source of truth for "which members on a model are editable in any UI". Replaces the
    /// per-handler property-walking logic currently duplicated in <c>Datra.Unity</c>
    /// (<c>NestedTypeFieldHandler</c>, <c>DatraTableView</c>) and <c>Datra.WebEditor</c>
    /// (<c>NestedTypeFieldHandler</c>).
    /// </summary>
    /// <remarks>
    /// Filters applied (in order):
    /// <list type="number">
    /// <item>public instance properties only;</item>
    /// <item>must have both a getter and a setter;</item>
    /// <item>no indexer parameters;</item>
    /// <item>no <see cref="DatraIgnoreAttribute"/> on the property;</item>
    /// <item>not shadowed by a public field of the same name (case-insensitive) — mirrors
    ///   <c>DatraTableView.GetMembers</c>'s "skip property when field exists" rule.</item>
    /// </list>
    /// The <c>[DatraIgnore]</c> filter is the fix for Web today shipping CsvMetadata / generated
    /// <c>Ref</c> property leakage (which Unity already handles correctly).
    /// </remarks>
    public static class EditableMemberEnumerator
    {
        /// <summary>Properties an editor UI should expose for the given model type.</summary>
        public static IReadOnlyList<PropertyInfo> ForType(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var fieldNames = new HashSet<string>(
                fields
                    .Where(f => !ContainsCompilerGeneratedMarker(f.Name))
                    .Select(f => f.Name),
                StringComparer.OrdinalIgnoreCase);

            var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var result = new List<PropertyInfo>(props.Length);
            foreach (var prop in props)
            {
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (prop.GetIndexParameters().Length != 0) continue;
                if (prop.GetCustomAttribute<DatraIgnoreAttribute>() != null) continue;
                if (fieldNames.Contains(prop.Name)) continue; // shadowed by a public field
                result.Add(prop);
            }
            return result;
        }

        /// <summary>
        /// Same as <see cref="ForType"/>, but excludes the type's primary key property
        /// (heuristic: a public <c>Id</c> property — matches <see cref="Datra.Interfaces.ITableData{TKey}"/>).
        /// Table views show the key in a dedicated column, so it shouldn't appear as a generic field.
        /// </summary>
        public static IReadOnlyList<PropertyInfo> ForTableRow(Type type)
        {
            var all = ForType(type);
            // Match by name only — ITableData{TKey} is an interface, so the concrete property may
            // come from the type directly (not interface mapping). "Id" is the contract.
            var filtered = all.Where(p => !string.Equals(p.Name, "Id", StringComparison.Ordinal)).ToList();
            return filtered;
        }

        private static bool ContainsCompilerGeneratedMarker(string name)
            => name.IndexOf('<') >= 0 || name.IndexOf('>') >= 0;
    }
}
