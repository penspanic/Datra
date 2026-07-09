#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.WebEditor.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Renders editors for generic dictionary fields. Keys are edited with plain type-based handlers;
/// values inherit the source member metadata so attribute-based handlers can customize dictionary
/// values in the same way they customize scalar fields.
/// </summary>
public sealed class DictionaryFieldHandler : IBlazorFieldHandler
{
    private readonly BlazorFieldTypeRegistry _registry;

    public DictionaryFieldHandler(BlazorFieldTypeRegistry registry) => _registry = registry;

    public int Priority => 24;

    public bool CanHandle(Type type, MemberInfo? member = null)
        => TypeClassifier.Classify(type, member) == FieldKind.Dictionary;

    public RenderFragment CreateField(FieldCreationContext context) => builder =>
    {
        if (!TypeClassifier.TryGetDictionaryArgs(context.FieldType, out var keyType, out var valueType)
            || keyType is null
            || valueType is null)
        {
            RenderMissing(builder, 0, context.FieldType.Name);
            return;
        }

        var entries = GetEntries(context.Value);

        if (context.LayoutMode == FieldLayoutMode.Table)
        {
            CollectionFieldRender.RenderCompact(
                builder, _registry, context, valueType, entries.Count,
                summary: BuildSummary(entries),
                renderItems: (b, seq) => RenderItems(b, seq, context, keyType, valueType, entries),
                onAdd: () => AddEntry(context, keyType, valueType));
            return;
        }

        RenderExpanded(builder, context, keyType, valueType, entries);
    };

    private void RenderExpanded(
        RenderTreeBuilder builder,
        FieldCreationContext context,
        Type keyType,
        Type valueType,
        IReadOnlyList<DictionaryEntrySnapshot> entries)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "datra-list datra-dictionary");

        builder.OpenElement(2, "div");
        builder.AddAttribute(3, "class", "datra-list__header");

        builder.OpenElement(4, "span");
        builder.AddAttribute(5, "class", "datra-list__count");
        builder.AddContent(6, entries.Count == 0 ? "empty" : $"{entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}");
        builder.CloseElement();

        if (!context.IsReadOnly)
        {
            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "type", "button");
            builder.AddAttribute(9, "class", "datra-btn datra-btn--ghost datra-list__add");
            builder.AddAttribute(10, "onclick", EventCallback.Factory.Create(this, () =>
                AddEntry(context, keyType, valueType)));
            builder.AddContent(11, "+ add");
            builder.CloseElement();
        }

        builder.CloseElement();

        if (entries.Count > 0)
        {
            builder.OpenElement(12, "div");
            builder.AddAttribute(13, "class", "datra-list__items");
            RenderItems(builder, 14, context, keyType, valueType, entries);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private void RenderItems(
        RenderTreeBuilder builder,
        int startSeq,
        FieldCreationContext context,
        Type keyType,
        Type valueType,
        IReadOnlyList<DictionaryEntrySnapshot> entries)
    {
        var keyHandler = _registry.FindBlazorHandler(keyType);
        var valueHandler = _registry.FindBlazorHandler(valueType, context.SourceMember);

        var seq = startSeq;
        for (var i = 0; i < entries.Count; i++)
        {
            var index = i;
            var entry = entries[i];

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-list__row datra-dictionary__row");

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-dictionary__key");
            if (keyHandler is not null)
            {
                var keyContext = new FieldCreationContext(
                    keyType,
                    entry.Key,
                    index,
                    context.LayoutMode == FieldLayoutMode.Table ? FieldLayoutMode.Form : context.LayoutMode,
                    newKey => ChangeEntryKey(context, keyType, valueType, entry.Key, newKey),
                    context.LocaleService,
                    context.IsReadOnly,
                    sourceMember: null,
                    fieldPath: BuildDictionaryPath(context, entry.Key, "key"))
                {
                    CollectionElementKey = entry.Key,
                    CollectionElement = entry.Key,
                    RootDataObject = context.RootDataObject
                };
                builder.AddContent(seq++, keyHandler.CreateField(keyContext));
            }
            else
            {
                RenderMissing(builder, seq++, keyType.Name);
            }
            builder.CloseElement();

            builder.OpenElement(seq++, "span");
            builder.AddAttribute(seq++, "class", "datra-dictionary__arrow");
            builder.AddContent(seq++, "->");
            builder.CloseElement();

            builder.OpenElement(seq++, "div");
            builder.AddAttribute(seq++, "class", "datra-list__field datra-dictionary__value");
            if (valueHandler is not null)
            {
                var valueContext = new FieldCreationContext(
                    valueType,
                    entry.Value,
                    index,
                    context.LayoutMode == FieldLayoutMode.Table ? FieldLayoutMode.Form : context.LayoutMode,
                    newValue => ChangeEntryValue(context, keyType, valueType, entry.Key, newValue),
                    context.LocaleService,
                    context.IsReadOnly,
                    context.SourceMember,
                    BuildDictionaryPath(context, entry.Key, null))
                {
                    CollectionElementKey = entry.Key,
                    CollectionElement = entry.Value,
                    RootDataObject = context.RootDataObject
                };
                builder.AddContent(seq++, valueHandler.CreateField(valueContext));
            }
            else
            {
                RenderMissing(builder, seq++, valueType.Name);
            }
            builder.CloseElement();

            if (!context.IsReadOnly)
            {
                builder.OpenElement(seq++, "button");
                builder.AddAttribute(seq++, "type", "button");
                builder.AddAttribute(seq++, "class", "datra-btn datra-btn--danger datra-list__remove");
                builder.AddAttribute(seq++, "title", "remove");
                builder.AddAttribute(seq++, "onclick", EventCallback.Factory.Create(this, () =>
                    RemoveEntry(context, keyType, valueType, entry.Key)));
                builder.AddContent(seq++, "×");
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }

    private static void AddEntry(FieldCreationContext context, Type keyType, Type valueType)
    {
        var dictionary = EnsureDictionary(context, keyType, valueType);
        if (dictionary is null) return;

        var key = CreateUniqueKey(dictionary, keyType, valueType);
        if (key is null && keyType.IsValueType) return;

        var value = DefaultValueFactory.CreateDefault(valueType);
        if (TrySetValue(dictionary, keyType, valueType, key, value))
            context.OnValueChanged?.Invoke(dictionary);
    }

    private static void ChangeEntryKey(
        FieldCreationContext context,
        Type keyType,
        Type valueType,
        object? oldKey,
        object? newKey)
    {
        var dictionary = EnsureDictionary(context, keyType, valueType);
        if (dictionary is null) return;

        var convertedOldKey = ConvertValue(oldKey, keyType);
        var convertedNewKey = ConvertValue(newKey, keyType);
        if (convertedNewKey is null && keyType.IsValueType) return;
        if (Equals(convertedOldKey, convertedNewKey)) return;
        if (ContainsKey(dictionary, keyType, valueType, convertedNewKey)) return;

        if (!TryGetValue(dictionary, keyType, valueType, convertedOldKey, out var value)) return;
        if (!TryRemove(dictionary, keyType, valueType, convertedOldKey)) return;
        if (TrySetValue(dictionary, keyType, valueType, convertedNewKey, value))
            context.OnValueChanged?.Invoke(dictionary);
    }

    private static void ChangeEntryValue(
        FieldCreationContext context,
        Type keyType,
        Type valueType,
        object? key,
        object? newValue)
    {
        var dictionary = EnsureDictionary(context, keyType, valueType);
        if (dictionary is null) return;

        if (TrySetValue(dictionary, keyType, valueType, key, newValue))
            context.OnValueChanged?.Invoke(dictionary);
    }

    private static void RemoveEntry(FieldCreationContext context, Type keyType, Type valueType, object? key)
    {
        var dictionary = context.Value;
        if (dictionary is null) return;

        if (TryRemove(dictionary, keyType, valueType, key))
            context.OnValueChanged?.Invoke(dictionary);
    }

    private static object? EnsureDictionary(FieldCreationContext context, Type keyType, Type valueType)
    {
        if (context.Value is not null)
            return context.Value;

        var concreteType = context.FieldType.IsInterface || context.FieldType.IsAbstract
            ? typeof(Dictionary<,>).MakeGenericType(keyType, valueType)
            : context.FieldType;

        if (Activator.CreateInstance(concreteType) is not { } created)
            return null;

        context.Value = created;
        context.OnValueChanged?.Invoke(created);
        return created;
    }

    private static IReadOnlyList<DictionaryEntrySnapshot> GetEntries(object? dictionary)
    {
        var entries = new List<DictionaryEntrySnapshot>();
        if (dictionary is null) return entries;

        if (dictionary is IDictionary nonGeneric)
        {
            foreach (DictionaryEntry entry in nonGeneric)
                entries.Add(new DictionaryEntrySnapshot(entry.Key, entry.Value));
            return entries;
        }

        if (dictionary is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is null) continue;
                var itemType = item.GetType();
                var key = itemType.GetProperty("Key")?.GetValue(item);
                var value = itemType.GetProperty("Value")?.GetValue(item);
                entries.Add(new DictionaryEntrySnapshot(key, value));
            }
        }

        return entries;
    }

    private static bool TryGetValue(
        object dictionary,
        Type keyType,
        Type valueType,
        object? key,
        out object? value)
    {
        value = null;

        if (dictionary is IDictionary nonGeneric)
        {
            var convertedKey = ConvertValue(key, keyType);
            if (convertedKey is null) return false;
            if (!nonGeneric.Contains(convertedKey)) return false;
            value = nonGeneric[convertedKey];
            return true;
        }

        var interfaceType = typeof(IDictionary<,>).MakeGenericType(keyType, valueType);
        var method = interfaceType.GetMethod("TryGetValue");
        if (method is null) return false;

        var args = new[] { ConvertValue(key, keyType), null };
        var found = method.Invoke(dictionary, args) is true;
        value = args[1];
        return found;
    }

    private static bool ContainsKey(object dictionary, Type keyType, Type valueType, object? key)
    {
        var convertedKey = ConvertValue(key, keyType);
        if (convertedKey is null) return false;

        if (dictionary is IDictionary nonGeneric)
            return nonGeneric.Contains(convertedKey);

        var interfaceType = typeof(IDictionary<,>).MakeGenericType(keyType, valueType);
        var method = interfaceType.GetMethod("ContainsKey");
        return method?.Invoke(dictionary, new[] { convertedKey }) is true;
    }

    private static bool TrySetValue(object dictionary, Type keyType, Type valueType, object? key, object? value)
    {
        var convertedKey = ConvertValue(key, keyType);
        if (convertedKey is null)
            return false;

        var convertedValue = ConvertValue(value, valueType);
        if (convertedValue is null && valueType.IsValueType)
            convertedValue = DefaultValueFactory.CreateDefault(valueType);

        if (dictionary is IDictionary nonGeneric)
        {
            nonGeneric[convertedKey] = convertedValue;
            return true;
        }

        var interfaceType = typeof(IDictionary<,>).MakeGenericType(keyType, valueType);
        var indexer = interfaceType.GetProperty("Item");
        if (indexer is null) return false;

        indexer.SetValue(dictionary, convertedValue, new[] { convertedKey });
        return true;
    }

    private static bool TryRemove(object dictionary, Type keyType, Type valueType, object? key)
    {
        var convertedKey = ConvertValue(key, keyType);
        if (convertedKey is null)
            return false;

        if (dictionary is IDictionary nonGeneric)
        {
            if (!nonGeneric.Contains(convertedKey)) return false;
            nonGeneric.Remove(convertedKey);
            return true;
        }

        var interfaceType = typeof(IDictionary<,>).MakeGenericType(keyType, valueType);
        var method = interfaceType.GetMethod("Remove", new[] { keyType });
        return method?.Invoke(dictionary, new[] { convertedKey }) is true;
    }

    private static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
            return null;

        targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (targetType.IsInstanceOfType(value))
            return value;

        if (targetType.IsEnum)
        {
            if (value is string enumText)
                return Enum.Parse(targetType, enumText);
            return Enum.ToObject(targetType, value);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    private static object? CreateUniqueKey(object dictionary, Type keyType, Type valueType)
    {
        if (keyType == typeof(string))
        {
            const string baseKey = "key";
            if (!ContainsKey(dictionary, keyType, valueType, baseKey))
                return baseKey;

            for (var i = 1; i < 1000; i++)
            {
                var candidate = $"{baseKey}{i}";
                if (!ContainsKey(dictionary, keyType, valueType, candidate))
                    return candidate;
            }
        }

        if (keyType == typeof(int))
        {
            for (var i = 0; i < 1000; i++)
            {
                if (!ContainsKey(dictionary, keyType, valueType, i))
                    return i;
            }
        }

        var defaultKey = DefaultValueFactory.CreateDefault(keyType);
        return ContainsKey(dictionary, keyType, valueType, defaultKey) ? null : defaultKey;
    }

    private static string BuildSummary(IReadOnlyList<DictionaryEntrySnapshot> entries)
    {
        if (entries.Count == 0) return "empty";

        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(ArrayFieldHandler.FormatElement(entry.Key));
            sb.Append('=');
            sb.Append(ArrayFieldHandler.FormatElement(entry.Value));
            if (sb.Length > 40)
            {
                sb.Length = 37;
                sb.Append("...");
                break;
            }
        }
        return sb.ToString();
    }

    private static string BuildDictionaryPath(FieldCreationContext context, object? key, string? suffix)
    {
        var basePath = context.FieldPath ?? context.SourceMember?.Name;
        var keyText = ArrayFieldHandler.FormatElement(key);
        var path = string.IsNullOrEmpty(basePath) ? $"[{keyText}]" : $"{basePath}[{keyText}]";
        return string.IsNullOrEmpty(suffix) ? path : $"{path}.{suffix}";
    }

    private static void RenderMissing(RenderTreeBuilder builder, int seq, string typeName)
    {
        builder.OpenElement(seq, "span");
        builder.AddAttribute(seq + 1, "class", "datra-list__missing");
        builder.AddContent(seq + 2, $"no handler for {typeName}");
        builder.CloseElement();
    }

    private readonly record struct DictionaryEntrySnapshot(object? Key, object? Value);
}
