using System;
using System.Collections.Generic;
using System.Reflection;
using Datra.Editor.Interfaces;
using Datra.Editor.Models;
using Datra.WebEditor.Abstractions;
using Datra.WebEditor.Handlers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace Datra.Tests
{
    public class DatraWebEditorFieldHandlerTests
    {
        [Fact]
        public void RegisterDefaultHandlers_IncludesDictionaryHandler()
        {
            var registry = new BlazorFieldTypeRegistry();

            registry.RegisterDefaultHandlers();

            var handler = registry.FindBlazorHandler(typeof(Dictionary<string, string>));
            Assert.IsType<DictionaryFieldHandler>(handler);
        }

        [Fact]
        public void ListFieldHandler_PropagatesSourceMemberToElementHandler()
        {
            var registry = CreateRegistryWithCapturingHandler(out var capture);
            var data = new TestEditorData { Effects = new List<string> { "fx.spark" } };
            var prop = typeof(TestEditorData).GetProperty(nameof(TestEditorData.Effects))!;
            var context = new FieldCreationContext(prop, data, data.Effects, FieldLayoutMode.Form, _ => { });

            var handler = registry.FindBlazorHandler(prop.PropertyType, prop);
            Assert.IsType<ListFieldHandler>(handler);

            Render(handler!.CreateField(context));

            var elementContext = Assert.Single(capture.Contexts);
            Assert.Same(prop, elementContext.SourceMember);
            Assert.Equal(0, elementContext.CollectionElementIndex);
            Assert.Equal("fx.spark", elementContext.CollectionElement);
            Assert.Equal("Effects[0]", elementContext.FieldPath);
        }

        [Fact]
        public void ArrayFieldHandler_PropagatesSourceMemberToElementHandler()
        {
            var registry = CreateRegistryWithCapturingHandler(out var capture);
            var data = new TestEditorData { Sprites = new[] { "sprite.hero" } };
            var prop = typeof(TestEditorData).GetProperty(nameof(TestEditorData.Sprites))!;
            var context = new FieldCreationContext(prop, data, data.Sprites, FieldLayoutMode.Form, _ => { });

            var handler = registry.FindBlazorHandler(prop.PropertyType, prop);
            Assert.IsType<ArrayFieldHandler>(handler);

            Render(handler!.CreateField(context));

            var elementContext = Assert.Single(capture.Contexts);
            Assert.Same(prop, elementContext.SourceMember);
            Assert.Equal(0, elementContext.CollectionElementIndex);
            Assert.Equal("sprite.hero", elementContext.CollectionElement);
            Assert.Equal("Sprites[0]", elementContext.FieldPath);
        }

        [Fact]
        public void DictionaryFieldHandler_PropagatesSourceMemberToValueHandlerOnly()
        {
            var registry = CreateRegistryWithCapturingHandler(out var capture);
            var data = new TestEditorData
            {
                MaterialBySlot = new Dictionary<string, string>
                {
                    ["baseColor"] = "mat.wall"
                }
            };
            var prop = typeof(TestEditorData).GetProperty(nameof(TestEditorData.MaterialBySlot))!;
            var context = new FieldCreationContext(prop, data, data.MaterialBySlot, FieldLayoutMode.Form, _ => { });

            var handler = registry.FindBlazorHandler(prop.PropertyType, prop);
            Assert.IsType<DictionaryFieldHandler>(handler);

            Render(handler!.CreateField(context));

            var valueContext = Assert.Single(capture.Contexts);
            Assert.Same(prop, valueContext.SourceMember);
            Assert.Equal(0, valueContext.CollectionElementIndex);
            Assert.Equal("baseColor", valueContext.CollectionElementKey);
            Assert.Equal("mat.wall", valueContext.CollectionElement);
            Assert.Equal("MaterialBySlot[baseColor]", valueContext.FieldPath);
        }

        private static BlazorFieldTypeRegistry CreateRegistryWithCapturingHandler(out CapturingAssetStringHandler capture)
        {
            var registry = new BlazorFieldTypeRegistry();
            capture = new CapturingAssetStringHandler();

            registry.RegisterHandler(capture);
            registry.RegisterHandler(new DictionaryFieldHandler(registry));
            registry.RegisterHandler(new ArrayFieldHandler(registry));
            registry.RegisterHandler(new ListFieldHandler(registry));
            registry.RegisterHandler(new StringFieldHandler());
            return registry;
        }

        private static void Render(RenderFragment fragment)
        {
            var builder = new RenderTreeBuilder();
            fragment(builder);
        }

        private sealed class CapturingAssetStringHandler : IBlazorFieldHandler
        {
            public List<FieldCreationContext> Contexts { get; } = new();
            public int Priority => 100;

            public bool CanHandle(Type type, MemberInfo? member = null)
                => type == typeof(string) && member?.GetCustomAttribute<TestAssetAttribute>() is not null;

            public RenderFragment CreateField(FieldCreationContext context) => builder =>
            {
                Contexts.Add(context);
                builder.AddContent(0, "asset");
            };
        }

        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
        private sealed class TestAssetAttribute : Attribute
        {
        }

        private sealed class TestEditorData
        {
            [TestAsset]
            public List<string> Effects { get; set; } = new();

            [TestAsset]
            public string[] Sprites { get; set; } = Array.Empty<string>();

            [TestAsset]
            public Dictionary<string, string> MaterialBySlot { get; set; } = new();
        }
    }
}
