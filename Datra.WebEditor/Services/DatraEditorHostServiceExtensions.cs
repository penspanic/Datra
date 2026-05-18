#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.DataTypes;
using Datra.Editor.Interfaces;
using Datra.Editor.Schema;
using Datra.Interfaces;

namespace Datra.WebEditor.Services;

/// <summary>
/// Reflected dispatch helpers for the generic edit operations on
/// <see cref="IEditableDataSource{TKey,TData}"/>. The host stores the non-generic data source
/// (because the type parameters aren't known until runtime) so the UI layer routes through these
/// extensions to invoke typed adds/deletes/key lookups.
/// </summary>
public static class DatraEditorHostServiceExtensions
{
    /// <summary>
    /// Resolve the generic interface for a data source, e.g. <c>IEditableDataSource&lt;string, Unit&gt;</c>.
    /// </summary>
    public static Type? GetEditableInterface(this IEditableDataSource source)
    {
        return source.GetType().GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEditableDataSource<,>));
    }

    /// <summary>(TKey, TData) for the data source, or null if not key-value typed.</summary>
    public static (Type Key, Type Data)? GetKeyValueTypes(this IEditableDataSource source)
    {
        var iface = source.GetEditableInterface();
        if (iface is null) return null;
        var args = iface.GetGenericArguments();
        return (args[0], args[1]);
    }

    /// <summary>
    /// Add a new item by key. Throws if the data source doesn't expose a key-value generic
    /// interface (e.g. single-value, asset, or localisation sources).
    /// </summary>
    public static void AddItem(this IEditableDataSource source, object key, object value)
    {
        var iface = source.GetEditableInterface()
            ?? throw new InvalidOperationException("Data source is not key-value typed.");
        var method = iface.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance)!;
        method.Invoke(source, new[] { key, value });
    }

    /// <summary>Delete by key.</summary>
    public static void DeleteItem(this IEditableDataSource source, object key)
    {
        var iface = source.GetEditableInterface()
            ?? throw new InvalidOperationException("Data source is not key-value typed.");
        var method = iface.GetMethod("Delete", BindingFlags.Public | BindingFlags.Instance)!;
        method.Invoke(source, new[] { key });
    }

    /// <summary>Whether the data source already contains a given key.</summary>
    public static bool ContainsKey(this IEditableDataSource source, object key)
    {
        var iface = source.GetEditableInterface()
            ?? throw new InvalidOperationException("Data source is not key-value typed.");
        var method = iface.GetMethod("ContainsKey", BindingFlags.Public | BindingFlags.Instance)!;
        return (bool)method.Invoke(source, new[] { key })!;
    }

    /// <summary>
    /// Construct a new TData instance for an "add row" flow. Thin shim over
    /// <see cref="DefaultValueFactory.CreateDefault"/> — kept public for backward compatibility
    /// with callers in the test suite and external consumers.
    /// </summary>
    public static object? CreateDefaultValue(Type dataType)
        => DefaultValueFactory.CreateDefault(dataType);

    /// <summary>
    /// Parse a raw string into the data source's key type. Supports string and the common integral
    /// primitives — extend here as Datra grows additional key types.
    /// </summary>
    public static (bool ok, object? key) TryParseKey(Type keyType, string raw)
    {
        if (string.IsNullOrEmpty(raw)) return (false, null);

        if (keyType == typeof(string)) return (true, raw);

        if (keyType == typeof(int) && int.TryParse(raw, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var ivalue)) return (true, ivalue);

        if (keyType == typeof(long) && long.TryParse(raw, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var lvalue)) return (true, lvalue);

        if (keyType == typeof(Guid) && Guid.TryParse(raw, out var guid)) return (true, guid);

        if (keyType == typeof(AssetId) && AssetId.TryParse(raw, out var assetId)) return (true, assetId);

        if (keyType.IsEnum && Enum.TryParse(keyType, raw, ignoreCase: true, out var evalue))
            return (true, evalue);

        return (false, null);
    }

    /// <summary>
    /// Stamp a freshly-created value's <see cref="ITableData{TKey}"/> key field so consumers that
    /// rely on Datra's auto-key-from-data convention still work. No-op if the type doesn't
    /// expose an "Id" property.
    /// </summary>
    public static void StampKey(object value, object key)
    {
        if (value is null) return;
        var prop = value.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null && prop.CanWrite && prop.PropertyType == key.GetType())
        {
            prop.SetValue(value, key);
        }
    }
}
