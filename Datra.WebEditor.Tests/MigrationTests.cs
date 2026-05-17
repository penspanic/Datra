#nullable enable
using System;
using System.Linq;
using Datra.DataTypes;
using Datra.Editor.Models;
using Datra.Editor.Schema;
using Datra.SampleData.Models;
using Datra.WebEditor.Abstractions;
using Datra.WebEditor.Handlers;
using Xunit;

namespace Datra.WebEditor.Tests;

/// <summary>
/// Smoke tests for the 2026-05-17 shared-schema migration. Verifies the bug fixes called out in
/// the migration ticket (CsvMetadata / Ref column leakage) on the actual source-gen'd
/// <see cref="RefTestData"/> type, and confirms the DataRef handler talks to
/// <see cref="DataRefTypeInfo"/> correctly.
/// </summary>
public class MigrationTests
{
    [Fact]
    public void TableRow_enumeration_drops_DatraIgnore_and_getter_only_props_on_RefTestData()
    {
        // RefTestData is partial — the source generator adds CsvMetadata (marked [DatraIgnore])
        // and a getter-only `Ref` expression-bodied property. Both must be filtered out, while
        // the author-declared CharacterRef / ItemRef / ItemRefs survive.
        var props = EditableMemberEnumerator.ForTableRow(typeof(RefTestData));
        var names = props.Select(p => p.Name).ToArray();

        Assert.DoesNotContain("Id", names);          // ForTableRow excludes the key column.
        Assert.DoesNotContain("CsvMetadata", names); // [DatraIgnore] suppressed.
        Assert.DoesNotContain("Ref", names);         // getter-only (CanWrite == false) suppressed.

        Assert.Contains("CharacterRef", names);
        Assert.Contains("ItemRef", names);
        Assert.Contains("ItemRefs", names);
    }

    [Fact]
    public void ForType_on_RefTestData_keeps_Id_but_still_drops_generated_helpers()
    {
        // Sanity-check the non-table-row path (used by DatraSingleView / DatraAssetView).
        var props = EditableMemberEnumerator.ForType(typeof(RefTestData));
        var names = props.Select(p => p.Name).ToArray();

        Assert.Contains("Id", names);
        Assert.DoesNotContain("CsvMetadata", names);
        Assert.DoesNotContain("Ref", names);
    }

    [Fact]
    public void DataRefTypeInfo_builds_StringDataRef_and_IntDataRef_via_handler_path()
    {
        // DataRefFieldHandler now routes all reflection through DataRefTypeInfo. The handler
        // itself is render-only; this exercises the underlying builder used by every onchange.
        var stringInfo = DataRefTypeInfo.TryCreate(typeof(StringDataRef<CharacterData>));
        Assert.NotNull(stringInfo);
        Assert.True(stringInfo!.IsStringKey);
        Assert.Equal(typeof(CharacterData), stringInfo.ReferencedType);

        var stringRef = stringInfo.Build("char_007");
        Assert.IsType<StringDataRef<CharacterData>>(stringRef);
        Assert.Equal("char_007", stringInfo.GetKey(stringRef));

        var intInfo = DataRefTypeInfo.TryCreate(typeof(IntDataRef<ItemData>));
        Assert.NotNull(intInfo);
        Assert.False(intInfo!.IsStringKey);
        Assert.Equal(typeof(int), intInfo.KeyType);

        var intRef = intInfo.Build(1001);
        Assert.IsType<IntDataRef<ItemData>>(intRef);
        Assert.Equal(1001, intInfo.GetKey(intRef));

        // CreateEmpty must round-trip back to a default-keyed instance.
        var empty = intInfo.CreateEmpty();
        Assert.IsType<IntDataRef<ItemData>>(empty);
        Assert.Equal(0, intInfo.GetKey(empty));
    }

    [Fact]
    public void DataRefFieldHandler_handles_DataRef_kinds()
    {
        // Reuses the schema classifier — guards against any future drift where the handler chain
        // resolves the wrong widget for a DataRef property.
        var handler = new DataRefFieldHandler();
        Assert.True(handler.CanHandle(typeof(StringDataRef<CharacterData>)));
        Assert.True(handler.CanHandle(typeof(IntDataRef<ItemData>)));
        Assert.False(handler.CanHandle(typeof(string)));
        Assert.False(handler.CanHandle(typeof(int)));
    }

    [Fact]
    public void Array_and_List_handlers_resolve_via_FieldKind()
    {
        var registry = new BlazorFieldTypeRegistry();
        registry.RegisterDefaultHandlers();

        Assert.IsType<ArrayFieldHandler>(registry.FindBlazorHandler(typeof(IntDataRef<ItemData>[])));
        Assert.IsType<ListFieldHandler>(registry.FindBlazorHandler(typeof(System.Collections.Generic.List<int>)));
    }
}
