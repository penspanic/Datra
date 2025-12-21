using System;
using System.Reflection;
using Datra.Editor.Interfaces;
using Datra.Editor.Services;
using Xunit;

namespace Datra.Tests
{
    public class FieldTypeRegistryTests
    {
        #region Registration Tests

        [Fact]
        public void RegisterHandler_AddsHandlerToRegistry()
        {
            var registry = new FieldTypeRegistry();
            var handler = new TestHandler(10, typeof(string));

            registry.RegisterHandler(handler);

            Assert.Single(registry.Handlers);
            Assert.Same(handler, registry.Handlers[0]);
        }

        [Fact]
        public void RegisterHandler_ThrowsOnNull()
        {
            var registry = new FieldTypeRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.RegisterHandler(null!));
        }

        [Fact]
        public void RegisterHandlers_AddsMultipleHandlers()
        {
            var registry = new FieldTypeRegistry();
            var handlers = new[]
            {
                new TestHandler(10, typeof(string)),
                new TestHandler(20, typeof(int))
            };

            registry.RegisterHandlers(handlers);

            Assert.Equal(2, registry.Handlers.Count);
        }

        [Fact]
        public void RemoveHandler_RemovesHandlerFromRegistry()
        {
            var registry = new FieldTypeRegistry();
            var handler = new TestHandler(10, typeof(string));
            registry.RegisterHandler(handler);

            var removed = registry.RemoveHandler(handler);

            Assert.True(removed);
            Assert.Empty(registry.Handlers);
        }

        [Fact]
        public void Clear_RemovesAllHandlers()
        {
            var registry = new FieldTypeRegistry();
            registry.RegisterHandler(new TestHandler(10, typeof(string)));
            registry.RegisterHandler(new TestHandler(20, typeof(int)));

            registry.Clear();

            Assert.Empty(registry.Handlers);
        }

        #endregion

        #region Priority Sorting Tests

        [Fact]
        public void Handlers_SortedByPriorityDescending()
        {
            var registry = new FieldTypeRegistry();
            var lowPriority = new TestHandler(1, typeof(string));
            var highPriority = new TestHandler(100, typeof(int));
            var midPriority = new TestHandler(50, typeof(bool));

            // Register in random order
            registry.RegisterHandler(midPriority);
            registry.RegisterHandler(lowPriority);
            registry.RegisterHandler(highPriority);

            var handlers = registry.Handlers;
            Assert.Equal(highPriority, handlers[0]);
            Assert.Equal(midPriority, handlers[1]);
            Assert.Equal(lowPriority, handlers[2]);
        }

        #endregion

        #region FindHandler Tests

        [Fact]
        public void FindHandler_ReturnsMatchingHandler()
        {
            var registry = new FieldTypeRegistry();
            var stringHandler = new TestHandler(10, typeof(string));
            var intHandler = new TestHandler(10, typeof(int));

            registry.RegisterHandler(stringHandler);
            registry.RegisterHandler(intHandler);

            var found = registry.FindHandler(typeof(string));

            Assert.Same(stringHandler, found);
        }

        [Fact]
        public void FindHandler_ReturnsHighestPriorityHandler()
        {
            var registry = new FieldTypeRegistry();
            var lowPriority = new TestHandler(1, typeof(string));
            var highPriority = new TestHandler(100, typeof(string));

            registry.RegisterHandler(lowPriority);
            registry.RegisterHandler(highPriority);

            var found = registry.FindHandler(typeof(string));

            Assert.Same(highPriority, found);
        }

        [Fact]
        public void FindHandler_ReturnsNullWhenNoMatch()
        {
            var registry = new FieldTypeRegistry();
            registry.RegisterHandler(new TestHandler(10, typeof(string)));

            var found = registry.FindHandler(typeof(int));

            Assert.Null(found);
        }

        [Fact]
        public void FindHandler_Generic_ReturnsCastedHandler()
        {
            var registry = new FieldTypeRegistry();
            var handler = new TestHandler(10, typeof(string));
            registry.RegisterHandler(handler);

            var found = registry.FindHandler<TestHandler>(typeof(string));

            Assert.Same(handler, found);
        }

        #endregion

        #region CanHandle Tests

        [Fact]
        public void CanHandle_ReturnsTrueWhenHandlerExists()
        {
            var registry = new FieldTypeRegistry();
            registry.RegisterHandler(new TestHandler(10, typeof(string)));

            Assert.True(registry.CanHandle(typeof(string)));
        }

        [Fact]
        public void CanHandle_ReturnsFalseWhenNoHandler()
        {
            var registry = new FieldTypeRegistry();
            registry.RegisterHandler(new TestHandler(10, typeof(string)));

            Assert.False(registry.CanHandle(typeof(int)));
        }

        [Fact]
        public void CanHandle_WithNullMember_WorksCorrectly()
        {
            var registry = new FieldTypeRegistry();
            registry.RegisterHandler(new TestHandler(10, typeof(string)));

            // Should work with null member
            Assert.True(registry.CanHandle(typeof(string), null));
            Assert.False(registry.CanHandle(typeof(int), null));
        }

        #endregion

        #region MemberInfo Aware Handler Tests

        [Fact]
        public void FindHandler_WithMemberInfo_PassesMemberToHandler()
        {
            var registry = new FieldTypeRegistry();
            var memberAwareHandler = new MemberAwareTestHandler(50, typeof(string));
            var basicHandler = new TestHandler(10, typeof(string));

            registry.RegisterHandler(basicHandler);
            registry.RegisterHandler(memberAwareHandler);

            var prop = typeof(TestClass).GetProperty(nameof(TestClass.RequiredProperty));

            // Without member, higher priority handler wins
            var foundNoMember = registry.FindHandler(typeof(string));
            Assert.Same(memberAwareHandler, foundNoMember);

            // With member, still uses priority order but passes member info
            var foundWithMember = registry.FindHandler(typeof(string), prop);
            Assert.Same(memberAwareHandler, foundWithMember);
        }

        [Fact]
        public void FindHandler_MemberAwareHandler_RejectsBasedOnMember()
        {
            var registry = new FieldTypeRegistry();
            // This handler only accepts properties with [Required] attribute
            var requiredOnlyHandler = new RequiredAttributeHandler(100, typeof(string));
            var fallbackHandler = new TestHandler(10, typeof(string));

            registry.RegisterHandler(requiredOnlyHandler);
            registry.RegisterHandler(fallbackHandler);

            var requiredProp = typeof(TestClass).GetProperty(nameof(TestClass.RequiredProperty));
            var normalProp = typeof(TestClass).GetProperty(nameof(TestClass.NormalProperty));

            // Required property uses RequiredAttributeHandler
            var foundRequired = registry.FindHandler(typeof(string), requiredProp);
            Assert.IsType<RequiredAttributeHandler>(foundRequired);

            // Normal property falls back to TestHandler
            var foundNormal = registry.FindHandler(typeof(string), normalProp);
            Assert.IsType<TestHandler>(foundNormal);

            // Null member falls back to TestHandler
            var foundNull = registry.FindHandler(typeof(string), null);
            Assert.IsType<TestHandler>(foundNull);
        }

        #endregion

        #region Test Handlers

        private class TestHandler : IFieldTypeHandler
        {
            private readonly Type _handledType;

            public TestHandler(int priority, Type handledType)
            {
                Priority = priority;
                _handledType = handledType;
            }

            public int Priority { get; }

            public bool CanHandle(Type type, MemberInfo? member = null)
            {
                return type == _handledType;
            }
        }

        private class MemberAwareTestHandler : IFieldTypeHandler
        {
            private readonly Type _handledType;

            public MemberAwareTestHandler(int priority, Type handledType)
            {
                Priority = priority;
                _handledType = handledType;
            }

            public int Priority { get; }

            public bool CanHandle(Type type, MemberInfo? member = null)
            {
                // Simply check type, member is available but not used for filtering
                return type == _handledType;
            }
        }

        private class RequiredAttributeHandler : IFieldTypeHandler
        {
            private readonly Type _handledType;

            public RequiredAttributeHandler(int priority, Type handledType)
            {
                Priority = priority;
                _handledType = handledType;
            }

            public int Priority { get; }

            public bool CanHandle(Type type, MemberInfo? member = null)
            {
                if (type != _handledType)
                    return false;

                // Only handle if member has [Required] attribute
                if (member == null)
                    return false;

                return member.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null;
            }
        }

        #endregion

        #region Test Classes

        private class TestClass
        {
            [System.ComponentModel.DataAnnotations.Required]
            public string RequiredProperty { get; set; } = "";

            public string NormalProperty { get; set; } = "";
        }

        #endregion
    }
}
