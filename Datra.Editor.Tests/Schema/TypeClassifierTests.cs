#nullable enable
using System;
using System.Collections.Generic;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Editor.Schema;
using Datra.Interfaces;
using Xunit;

namespace Datra.Editor.Tests.Schema
{
    public class TypeClassifierTests
    {
        // Test models -------------------------------------------------------
        private class RefTarget : ITableData<string>
        {
            public string Id { get; set; } = "";
        }

        private class IntRefTarget : ITableData<int>
        {
            public int Id { get; set; }
        }

        private enum SampleEnum { A, B }

        private class Composite
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }

            [FixedLocale]
            public LocaleRef Label { get; set; }

            public LocaleRef UntaggedLabel { get; set; }
        }

        // Primitives --------------------------------------------------------
        [Theory]
        [InlineData(typeof(string), FieldKind.String)]
        [InlineData(typeof(bool), FieldKind.Boolean)]
        [InlineData(typeof(byte), FieldKind.Integer)]
        [InlineData(typeof(sbyte), FieldKind.Integer)]
        [InlineData(typeof(short), FieldKind.Integer)]
        [InlineData(typeof(ushort), FieldKind.Integer)]
        [InlineData(typeof(int), FieldKind.Integer)]
        [InlineData(typeof(uint), FieldKind.Integer)]
        [InlineData(typeof(long), FieldKind.Integer)]
        [InlineData(typeof(ulong), FieldKind.Integer)]
        [InlineData(typeof(float), FieldKind.Floating)]
        [InlineData(typeof(double), FieldKind.Floating)]
        [InlineData(typeof(decimal), FieldKind.Floating)]
        [InlineData(typeof(DateTime), FieldKind.DateTime)]
        public void Classifies_Primitives(Type type, FieldKind expected)
            => Assert.Equal(expected, TypeClassifier.Classify(type));

        [Fact]
        public void Classifies_Enum() => Assert.Equal(FieldKind.Enum, TypeClassifier.Classify(typeof(SampleEnum)));

        // Collections --------------------------------------------------------
        [Fact]
        public void Classifies_Array1D() => Assert.Equal(FieldKind.Array, TypeClassifier.Classify(typeof(int[])));

        [Fact]
        public void Classifies_List() => Assert.Equal(FieldKind.List, TypeClassifier.Classify(typeof(List<int>)));

        [Fact]
        public void Classifies_IList() => Assert.Equal(FieldKind.List, TypeClassifier.Classify(typeof(IList<int>)));

        [Fact]
        public void Classifies_Dictionary()
            => Assert.Equal(FieldKind.Dictionary, TypeClassifier.Classify(typeof(Dictionary<string, int>)));

        [Fact]
        public void Classifies_IDictionary()
            => Assert.Equal(FieldKind.Dictionary, TypeClassifier.Classify(typeof(IDictionary<string, int>)));

        // DataRef ------------------------------------------------------------
        [Fact]
        public void Classifies_StringDataRef()
            => Assert.Equal(FieldKind.DataRef, TypeClassifier.Classify(typeof(StringDataRef<RefTarget>)));

        [Fact]
        public void Classifies_IntDataRef()
            => Assert.Equal(FieldKind.DataRef, TypeClassifier.Classify(typeof(IntDataRef<IntRefTarget>)));

        [Fact]
        public void IsDataRefType_Positive()
        {
            Assert.True(TypeClassifier.IsDataRefType(typeof(StringDataRef<RefTarget>)));
            Assert.True(TypeClassifier.IsDataRefType(typeof(IntDataRef<IntRefTarget>)));
        }

        [Fact]
        public void IsDataRefType_Negative()
        {
            Assert.False(TypeClassifier.IsDataRefType(typeof(string)));
            Assert.False(TypeClassifier.IsDataRefType(typeof(List<int>)));
        }

        [Fact]
        public void GetDataRefArgs_String()
        {
            var (referenced, key) = TypeClassifier.GetDataRefArgs(typeof(StringDataRef<RefTarget>));
            Assert.Equal(typeof(RefTarget), referenced);
            Assert.Equal(typeof(string), key);
        }

        [Fact]
        public void GetDataRefArgs_Int()
        {
            var (referenced, key) = TypeClassifier.GetDataRefArgs(typeof(IntDataRef<IntRefTarget>));
            Assert.Equal(typeof(IntRefTarget), referenced);
            Assert.Equal(typeof(int), key);
        }

        // LocaleRef ----------------------------------------------------------
        [Fact]
        public void LocaleRef_WithFixedLocale_IsLocaleRef()
        {
            var prop = typeof(Composite).GetProperty(nameof(Composite.Label))!;
            Assert.Equal(FieldKind.LocaleRef, TypeClassifier.Classify(typeof(LocaleRef), prop));
        }

        [Fact]
        public void LocaleRef_WithoutFixedLocale_IsNested()
        {
            var prop = typeof(Composite).GetProperty(nameof(Composite.UntaggedLabel))!;
            Assert.Equal(FieldKind.Nested, TypeClassifier.Classify(typeof(LocaleRef), prop));
        }

        [Fact]
        public void LocaleRef_NoMember_IsNested()
            => Assert.Equal(FieldKind.Nested, TypeClassifier.Classify(typeof(LocaleRef)));

        // Element type helpers ----------------------------------------------
        [Fact]
        public void GetElementType_Array() => Assert.Equal(typeof(int), TypeClassifier.GetElementType(typeof(int[])));

        [Fact]
        public void GetElementType_List()
            => Assert.Equal(typeof(string), TypeClassifier.GetElementType(typeof(List<string>)));

        [Fact]
        public void GetElementType_IList()
            => Assert.Equal(typeof(string), TypeClassifier.GetElementType(typeof(IList<string>)));

        [Fact]
        public void GetElementType_Dictionary_IsNull()
            => Assert.Null(TypeClassifier.GetElementType(typeof(Dictionary<string, int>)));

        // Nested fallback ----------------------------------------------------
        [Fact]
        public void Classifies_Nested_ForCompositeClass()
            => Assert.Equal(FieldKind.Nested, TypeClassifier.Classify(typeof(Composite)));
    }
}
