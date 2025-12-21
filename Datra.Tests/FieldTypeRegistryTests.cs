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

        #endregion

        #region Test Handler

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

        #endregion
    }
}
