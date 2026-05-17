#nullable enable
using System.Collections.Generic;
using Datra.DataTypes;
using Datra.SampleData2.Models;
using Datra.WebEditor.Abstractions;
using Datra.WebEditor.Handlers;
using Xunit;

namespace Datra.WebEditor.Tests;

public class FieldRegistryTests
{
    private static BlazorFieldTypeRegistry Build()
    {
        var registry = new BlazorFieldTypeRegistry();
        registry.RegisterDefaultHandlers();
        return registry;
    }

    [Theory]
    [InlineData(typeof(string), typeof(StringFieldHandler))]
    [InlineData(typeof(int), typeof(IntFieldHandler))]
    [InlineData(typeof(long), typeof(LongFieldHandler))]
    [InlineData(typeof(float), typeof(FloatFieldHandler))]
    [InlineData(typeof(double), typeof(DoubleFieldHandler))]
    [InlineData(typeof(bool), typeof(BoolFieldHandler))]
    [InlineData(typeof(System.DateTime), typeof(DateTimeFieldHandler))]
    public void Default_chain_resolves_primitive_handlers(System.Type fieldType, System.Type expected)
    {
        var registry = Build();
        var handler = registry.FindBlazorHandler(fieldType);
        Assert.NotNull(handler);
        Assert.IsType(expected, handler);
    }

    [Fact]
    public void Enum_handler_wins_over_int_for_enum_types()
    {
        var registry = Build();
        var handler = registry.FindBlazorHandler(typeof(SampleEnum));
        Assert.IsType<EnumFieldHandler>(handler);
    }

    [Fact]
    public void List_handler_wins_over_nested_for_generic_lists()
    {
        var registry = Build();
        var handler = registry.FindBlazorHandler(typeof(List<string>));
        Assert.IsType<ListFieldHandler>(handler);
    }

    [Fact]
    public void DataRef_handler_wins_over_nested_for_StringDataRef()
    {
        var registry = Build();
        var handler = registry.FindBlazorHandler(typeof(StringDataRef<ShopItemData>));
        Assert.IsType<DataRefFieldHandler>(handler);
    }

    [Fact]
    public void Nested_handler_catches_arbitrary_class()
    {
        var registry = Build();
        var handler = registry.FindBlazorHandler(typeof(ShopItemData));
        Assert.IsType<NestedTypeFieldHandler>(handler);
    }

    [Fact]
    public void Custom_handler_can_override_built_in_chain()
    {
        var registry = Build();
        registry.RegisterHandler(new HighPriorityStringHandler());
        var handler = registry.FindBlazorHandler(typeof(string));
        Assert.IsType<HighPriorityStringHandler>(handler);
    }

    private enum SampleEnum { A, B, C }

    private sealed class HighPriorityStringHandler : IBlazorFieldHandler
    {
        public int Priority => 1000;
        public bool CanHandle(System.Type type, System.Reflection.MemberInfo? member = null) => type == typeof(string);
        public Microsoft.AspNetCore.Components.RenderFragment CreateField(Datra.Editor.Models.FieldCreationContext context) =>
            _ => { };
    }
}
