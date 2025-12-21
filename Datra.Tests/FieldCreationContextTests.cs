using System;
using System.Reflection;
using Datra.Editor.Models;
using Xunit;

namespace Datra.Tests
{
    public class FieldCreationContextTests
    {
        #region Property Constructor Tests

        [Fact]
        public void PropertyConstructor_SetsFieldType()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.Equal(typeof(string), context.FieldType);
        }

        [Fact]
        public void PropertyConstructor_SetsProperty()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.Same(prop, context.Property);
        }

        [Fact]
        public void PropertyConstructor_SetsTarget()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.Same(target, context.Target);
        }

        [Fact]
        public void PropertyConstructor_SetsValue()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();
            var value = "test value";

            var context = new FieldCreationContext(
                prop,
                target,
                value,
                FieldLayoutMode.Form,
                _ => { });

            Assert.Equal(value, context.Value);
        }

        [Fact]
        public void PropertyConstructor_SetsLayoutMode()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Table,
                _ => { });

            Assert.Equal(FieldLayoutMode.Table, context.LayoutMode);
        }

        [Fact]
        public void PropertyConstructor_SetsOnValueChanged()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();
            var callbackInvoked = false;

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { callbackInvoked = true; });

            context.OnValueChanged?.Invoke("new value");

            Assert.True(callbackInvoked);
        }

        [Fact]
        public void PropertyConstructor_IsReadOnly_DefaultsFalse()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.False(context.IsReadOnly);
        }

        [Fact]
        public void PropertyConstructor_IsReadOnly_CanBeSetToTrue()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { },
                isReadOnly: true);

            Assert.True(context.IsReadOnly);
        }

        [Fact]
        public void PropertyConstructor_ReadOnlyProperty_IsReadOnly()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.ReadOnlyProp))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test",
                FieldLayoutMode.Form,
                _ => { });

            Assert.True(context.IsReadOnly);
        }

        [Fact]
        public void PropertyConstructor_MemberIsNull()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.Null(context.Member);
        }

        [Fact]
        public void PropertyConstructor_IsNestedMember_IsFalse()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.False(context.IsNestedMember);
        }

        [Fact]
        public void PropertyConstructor_IsArrayElement_IsFalse()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test value",
                FieldLayoutMode.Form,
                _ => { });

            Assert.False(context.IsArrayElement);
        }

        #endregion

        #region Nested Member Constructor Tests

        [Fact]
        public void NestedMemberConstructor_SetsMember()
        {
            var member = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value))!;
            var parentValue = new TestNestedStruct { Value = 42 };

            var context = new FieldCreationContext(
                member,
                typeof(int),
                parentValue,
                42,
                FieldLayoutMode.Form,
                _ => { });

            Assert.Same(member, context.Member);
        }

        [Fact]
        public void NestedMemberConstructor_SetsParentValue()
        {
            var member = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value))!;
            var parentValue = new TestNestedStruct { Value = 42 };

            var context = new FieldCreationContext(
                member,
                typeof(int),
                parentValue,
                42,
                FieldLayoutMode.Form,
                _ => { });

            Assert.Equal(parentValue, context.ParentValue);
        }

        [Fact]
        public void NestedMemberConstructor_SetsFieldType()
        {
            var member = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value))!;
            var parentValue = new TestNestedStruct { Value = 42 };

            var context = new FieldCreationContext(
                member,
                typeof(int),
                parentValue,
                42,
                FieldLayoutMode.Form,
                _ => { });

            Assert.Equal(typeof(int), context.FieldType);
        }

        [Fact]
        public void NestedMemberConstructor_IsNestedMember_IsTrue()
        {
            var member = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value))!;
            var parentValue = new TestNestedStruct { Value = 42 };

            var context = new FieldCreationContext(
                member,
                typeof(int),
                parentValue,
                42,
                FieldLayoutMode.Form,
                _ => { });

            Assert.True(context.IsNestedMember);
        }

        [Fact]
        public void NestedMemberConstructor_PropertyIsNull()
        {
            var member = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value))!;
            var parentValue = new TestNestedStruct { Value = 42 };

            var context = new FieldCreationContext(
                member,
                typeof(int),
                parentValue,
                42,
                FieldLayoutMode.Form,
                _ => { });

            Assert.Null(context.Property);
        }

        #endregion

        #region Collection Element Constructor Tests

        [Fact]
        public void CollectionElementConstructor_SetsCollectionElementIndex()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "item1",
                5,
                FieldLayoutMode.Table,
                _ => { });

            Assert.Equal(5, context.CollectionElementIndex);
        }

        [Fact]
        public void CollectionElementConstructor_SetsFieldType()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "item1",
                0,
                FieldLayoutMode.Table,
                _ => { });

            Assert.Equal(typeof(string), context.FieldType);
        }

        [Fact]
        public void CollectionElementConstructor_SetsValue()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "item1",
                0,
                FieldLayoutMode.Table,
                _ => { });

            Assert.Equal("item1", context.Value);
        }

        [Fact]
        public void CollectionElementConstructor_IsArrayElement_IsTrue()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "item1",
                0,
                FieldLayoutMode.Table,
                _ => { });

            Assert.True(context.IsArrayElement);
        }

        [Fact]
        public void CollectionElementConstructor_PropertyIsNull()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "item1",
                0,
                FieldLayoutMode.Table,
                _ => { });

            Assert.Null(context.Property);
        }

        [Fact]
        public void CollectionElementConstructor_MemberIsNull()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "item1",
                0,
                FieldLayoutMode.Table,
                _ => { });

            Assert.Null(context.Member);
        }

        #endregion

        #region WithValue Tests

        [Fact]
        public void WithValue_PropertyContext_CreatesNewContextWithNewValue()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "original",
                FieldLayoutMode.Form,
                _ => { });

            var newContext = context.WithValue("new value");

            Assert.Equal("new value", newContext.Value);
            Assert.Equal("original", context.Value);
            Assert.Same(prop, newContext.Property);
        }

        [Fact]
        public void WithValue_NestedMemberContext_CreatesNewContextWithNewValue()
        {
            var member = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value))!;
            var parentValue = new TestNestedStruct { Value = 42 };

            var context = new FieldCreationContext(
                member,
                typeof(int),
                parentValue,
                42,
                FieldLayoutMode.Form,
                _ => { });

            var newContext = context.WithValue(100);

            Assert.Equal(100, newContext.Value);
            Assert.Equal(42, context.Value);
        }

        [Fact]
        public void WithValue_CollectionContext_CreatesNewContextWithNewValue()
        {
            var context = new FieldCreationContext(
                typeof(string),
                "original",
                0,
                FieldLayoutMode.Table,
                _ => { });

            var newContext = context.WithValue("new value");

            Assert.Equal("new value", newContext.Value);
            Assert.Equal("original", context.Value);
        }

        #endregion

        #region WithLayoutMode Tests

        [Fact]
        public void WithLayoutMode_PropertyContext_CreatesNewContextWithNewLayoutMode()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test",
                FieldLayoutMode.Form,
                _ => { });

            var newContext = context.WithLayoutMode(FieldLayoutMode.Table);

            Assert.Equal(FieldLayoutMode.Table, newContext.LayoutMode);
            Assert.Equal(FieldLayoutMode.Form, context.LayoutMode);
        }

        [Fact]
        public void WithLayoutMode_PreservesOtherProperties()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test",
                FieldLayoutMode.Form,
                _ => { })
            {
                CollectionElementIndex = 5,
                RootDataObject = target
            };

            var newContext = context.WithLayoutMode(FieldLayoutMode.Inline);

            Assert.Equal(5, newContext.CollectionElementIndex);
            Assert.Same(target, newContext.RootDataObject);
        }

        #endregion

        #region FieldLayoutMode Tests

        [Fact]
        public void FieldLayoutMode_Form()
        {
            Assert.Equal(0, (int)FieldLayoutMode.Form);
        }

        [Fact]
        public void FieldLayoutMode_Table()
        {
            Assert.Equal(1, (int)FieldLayoutMode.Table);
        }

        [Fact]
        public void FieldLayoutMode_Inline()
        {
            Assert.Equal(2, (int)FieldLayoutMode.Inline);
        }

        #endregion

        #region Additional Properties Tests

        [Fact]
        public void CollectionElementIndex_CanBeSet()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test",
                FieldLayoutMode.Form,
                _ => { })
            {
                CollectionElementIndex = 10
            };

            Assert.Equal(10, context.CollectionElementIndex);
        }

        [Fact]
        public void CollectionElement_CanBeSet()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();
            var element = "element";

            var context = new FieldCreationContext(
                prop,
                target,
                "test",
                FieldLayoutMode.Form,
                _ => { })
            {
                CollectionElement = element
            };

            Assert.Equal(element, context.CollectionElement);
        }

        [Fact]
        public void RootDataObject_CanBeSet()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();
            var root = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "test",
                FieldLayoutMode.Form,
                _ => { })
            {
                RootDataObject = root
            };

            Assert.Same(root, context.RootDataObject);
        }

        [Fact]
        public void Value_CanBeModified()
        {
            var prop = typeof(TestClass).GetProperty(nameof(TestClass.Name))!;
            var target = new TestClass();

            var context = new FieldCreationContext(
                prop,
                target,
                "original",
                FieldLayoutMode.Form,
                _ => { });

            context.Value = "modified";

            Assert.Equal("modified", context.Value);
        }

        #endregion

        #region Test Types

        public class TestClass
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
            public TestNestedStruct Nested { get; set; }
            public string[] Items { get; set; } = Array.Empty<string>();
            public string ReadOnlyProp { get; } = "readonly";
        }

        public struct TestNestedStruct
        {
            public int Value;
            public string Text { get; set; }
        }

        #endregion
    }
}
