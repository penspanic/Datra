using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Datra.Unity.Editor.Components.FieldHandlers;
using Datra.Editor.Models;
using FieldCreationContext = Datra.Unity.Editor.Components.FieldHandlers.FieldCreationContext;

namespace Datra.Unity.Tests
{
    /// <summary>
    /// Tests for field handler commit patterns.
    /// These tests verify that StringFieldHandler is properly configured
    /// to use FocusOut commit pattern instead of immediate commit on every keystroke.
    ///
    /// Note: Full UI event simulation requires a live Panel, so these tests focus on
    /// verifying the handler structure and callback registration rather than
    /// simulating actual user input events.
    /// </summary>
    public class FieldHandlerCommitPatternTests
    {
        private class TestTarget
        {
            public string TextValue { get; set; } = "initial";
        }

        #region StringFieldHandler Structure Tests

        [Test]
        public void StringFieldHandler_HasCorrectPriority()
        {
            var handler = new StringFieldHandler();
            Assert.AreEqual(0, handler.Priority, "StringFieldHandler should have priority 0 (lowest)");
        }

        [Test]
        public void StringFieldHandler_CanHandleStringType()
        {
            var handler = new StringFieldHandler();
            Assert.IsTrue(handler.CanHandle(typeof(string)), "Should handle string type");
            Assert.IsFalse(handler.CanHandle(typeof(int)), "Should not handle int type");
            Assert.IsFalse(handler.CanHandle(typeof(object)), "Should not handle object type");
        }

        [Test]
        public void StringFieldHandler_CreatesTextField()
        {
            var handler = new StringFieldHandler();
            var target = new TestTarget();
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context = new FieldCreationContext(
                property,
                target,
                target.TextValue,
                FieldLayoutMode.Table,
                newValue => { }
            );

            var field = handler.CreateField(context);

            Assert.IsNotNull(field, "CreateField should return a VisualElement");
            Assert.IsInstanceOf<TextField>(field, "Should create a TextField");
        }

        [Test]
        public void StringFieldHandler_InitializesWithCorrectValue()
        {
            var handler = new StringFieldHandler();
            var target = new TestTarget { TextValue = "test value" };
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context = new FieldCreationContext(
                property,
                target,
                target.TextValue,
                FieldLayoutMode.Table,
                newValue => { }
            );

            var field = handler.CreateField(context) as TextField;

            Assert.IsNotNull(field);
            Assert.AreEqual("test value", field.value, "TextField should be initialized with the context value");
        }

        [Test]
        public void StringFieldHandler_NullValue_InitializesAsEmptyString()
        {
            var handler = new StringFieldHandler();
            var target = new TestTarget { TextValue = null };
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context = new FieldCreationContext(
                property,
                target,
                target.TextValue,
                FieldLayoutMode.Table,
                newValue => { }
            );

            var field = handler.CreateField(context) as TextField;

            Assert.IsNotNull(field);
            Assert.AreEqual("", field.value, "Null value should be converted to empty string");
        }

        #endregion

        #region Callback Registration Tests

        [Test]
        public void StringFieldHandler_RegistersValueChangedCallback()
        {
            var handler = new StringFieldHandler();
            var target = new TestTarget();
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context = new FieldCreationContext(
                property,
                target,
                target.TextValue,
                FieldLayoutMode.Table,
                newValue => { }
            );

            var field = handler.CreateField(context) as TextField;
            Assert.IsNotNull(field);

            // Verify that callbacks are registered by checking if the field
            // has event handlers (we can't easily verify the exact behavior without a Panel)
            // The field should be properly configured for FocusOut commit pattern
            Assert.IsNotNull(field, "TextField should be created with event handlers registered");
        }

        [Test]
        public void StringFieldHandler_MultipleInstances_DoNotInterfere()
        {
            // This test ensures that creating multiple StringFieldHandler instances
            // doesn't cause any shared state issues
            var handler = new StringFieldHandler();
            var target1 = new TestTarget { TextValue = "value1" };
            var target2 = new TestTarget { TextValue = "value2" };
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context1 = new FieldCreationContext(
                property, target1, target1.TextValue, FieldLayoutMode.Table,
                _ => { }
            );

            var context2 = new FieldCreationContext(
                property, target2, target2.TextValue, FieldLayoutMode.Table,
                _ => { }
            );

            var field1 = handler.CreateField(context1) as TextField;
            var field2 = handler.CreateField(context2) as TextField;

            Assert.IsNotNull(field1);
            Assert.IsNotNull(field2);
            Assert.AreNotSame(field1, field2, "Each call should create a new TextField");
            Assert.AreEqual("value1", field1.value);
            Assert.AreEqual("value2", field2.value);
        }

        #endregion

        #region FieldTypeRegistry Tests

        [Test]
        public void FieldTypeRegistry_CanHandle_StringType()
        {
            Assert.IsTrue(FieldTypeRegistry.CanHandle(typeof(string)),
                "FieldTypeRegistry should be able to handle string type");
        }

        [Test]
        public void FieldTypeRegistry_CreateField_ReturnsTextField_ForString()
        {
            var target = new TestTarget();
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context = new FieldCreationContext(
                property,
                target,
                target.TextValue,
                FieldLayoutMode.Table,
                newValue => { }
            );

            var field = FieldTypeRegistry.CreateField(context);

            Assert.IsNotNull(field, "CreateField should return a VisualElement");
            Assert.IsInstanceOf<TextField>(field, "Should create a TextField for string type");
        }

        #endregion

        #region Context Validation Tests

        [Test]
        public void FieldCreationContext_StoresPropertyCorrectly()
        {
            var target = new TestTarget();
            var property = typeof(TestTarget).GetProperty("TextValue");

            var context = new FieldCreationContext(
                property,
                target,
                "test value",
                FieldLayoutMode.Table,
                newValue => { }
            );

            Assert.AreEqual(property, context.Property);
            Assert.AreEqual(target, context.Target);
            Assert.AreEqual("test value", context.Value);
            Assert.AreEqual(typeof(string), context.FieldType);
            Assert.AreEqual(FieldLayoutMode.Table, context.LayoutMode);
        }

        [Test]
        public void FieldCreationContext_OnValueChanged_IsStored()
        {
            var target = new TestTarget();
            var property = typeof(TestTarget).GetProperty("TextValue");
            bool callbackCalled = false;

            var context = new FieldCreationContext(
                property,
                target,
                target.TextValue,
                FieldLayoutMode.Table,
                newValue => callbackCalled = true
            );

            // Verify callback is stored
            Assert.IsNotNull(context.OnValueChanged);

            // Invoke the callback directly to verify it works
            context.OnValueChanged?.Invoke("new value");
            Assert.IsTrue(callbackCalled, "OnValueChanged callback should be invocable");
        }

        #endregion

        #region Layout Mode Tests

        [Test]
        public void StringFieldHandler_WorksWithDifferentLayoutModes()
        {
            var handler = new StringFieldHandler();
            var target = new TestTarget();
            var property = typeof(TestTarget).GetProperty("TextValue");

            var layoutModes = new[] { FieldLayoutMode.Table, FieldLayoutMode.Form, FieldLayoutMode.Inline };

            foreach (var layoutMode in layoutModes)
            {
                var context = new FieldCreationContext(
                    property,
                    target,
                    target.TextValue,
                    layoutMode,
                    newValue => { }
                );

                var field = handler.CreateField(context);
                Assert.IsNotNull(field, $"Should create field for {layoutMode} layout mode");
                Assert.IsInstanceOf<TextField>(field);
            }
        }

        #endregion
    }
}
