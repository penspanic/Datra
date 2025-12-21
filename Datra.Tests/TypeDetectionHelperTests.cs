using System;
using System.Collections.Generic;
using Datra.DataTypes;
using Datra.Editor.Utilities;
using Xunit;

namespace Datra.Tests
{
    public class TypeDetectionHelperTests
    {
        #region DataRef Detection Tests

        [Fact]
        public void IsDataRefType_WithStringDataRef_ReturnsTrue()
        {
            var type = typeof(StringDataRef<TestData>);
            Assert.True(TypeDetectionHelper.IsDataRefType(type));
        }

        [Fact]
        public void IsDataRefType_WithIntDataRef_ReturnsTrue()
        {
            var type = typeof(IntDataRef<TestIntData>);
            Assert.True(TypeDetectionHelper.IsDataRefType(type));
        }

        [Fact]
        public void IsDataRefType_WithNonDataRef_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsDataRefType(typeof(string)));
            Assert.False(TypeDetectionHelper.IsDataRefType(typeof(int)));
            Assert.False(TypeDetectionHelper.IsDataRefType(typeof(List<string>)));
        }

        [Fact]
        public void IsDataRefArrayType_WithDataRefArray_ReturnsTrue()
        {
            var type = typeof(StringDataRef<TestData>[]);
            Assert.True(TypeDetectionHelper.IsDataRefArrayType(type));
        }

        [Fact]
        public void IsDataRefArrayType_WithNonDataRefArray_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsDataRefArrayType(typeof(string[])));
            Assert.False(TypeDetectionHelper.IsDataRefArrayType(typeof(int[])));
        }

        #endregion

        #region LocaleRef Detection Tests

        [Fact]
        public void IsLocaleRefType_WithLocaleRef_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsLocaleRefType(typeof(LocaleRef)));
        }

        [Fact]
        public void IsLocaleRefType_WithNonLocaleRef_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsLocaleRefType(typeof(string)));
            Assert.False(TypeDetectionHelper.IsLocaleRefType(typeof(int)));
        }

        #endregion

        #region Collection Detection Tests

        [Fact]
        public void IsListType_WithList_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsListType(typeof(List<string>)));
            Assert.True(TypeDetectionHelper.IsListType(typeof(List<int>)));
        }

        [Fact]
        public void IsListType_WithNonList_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsListType(typeof(string[])));
            Assert.False(TypeDetectionHelper.IsListType(typeof(Dictionary<string, int>)));
        }

        [Fact]
        public void IsDictionaryType_WithDictionary_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsDictionaryType(typeof(Dictionary<string, int>)));
            Assert.True(TypeDetectionHelper.IsDictionaryType(typeof(Dictionary<int, string>)));
        }

        [Fact]
        public void IsDictionaryType_WithNonDictionary_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsDictionaryType(typeof(List<string>)));
            Assert.False(TypeDetectionHelper.IsDictionaryType(typeof(string[])));
        }

        [Fact]
        public void IsArrayType_WithArray_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsArrayType(typeof(string[])));
            Assert.True(TypeDetectionHelper.IsArrayType(typeof(int[])));
        }

        [Fact]
        public void IsArrayType_WithNonArray_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsArrayType(typeof(List<string>)));
            Assert.False(TypeDetectionHelper.IsArrayType(typeof(string)));
        }

        [Fact]
        public void IsCollectionType_WithAnyCollection_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsCollectionType(typeof(List<string>)));
            Assert.True(TypeDetectionHelper.IsCollectionType(typeof(Dictionary<string, int>)));
            Assert.True(TypeDetectionHelper.IsCollectionType(typeof(string[])));
        }

        [Fact]
        public void GetCollectionElementType_ReturnsCorrectType()
        {
            Assert.Equal(typeof(string), TypeDetectionHelper.GetCollectionElementType(typeof(string[])));
            Assert.Equal(typeof(int), TypeDetectionHelper.GetCollectionElementType(typeof(List<int>)));
            Assert.Equal(typeof(string), TypeDetectionHelper.GetCollectionElementType(typeof(Dictionary<int, string>)));
        }

        #endregion

        #region Nested Type Detection Tests

        [Fact]
        public void IsNestedType_WithUserDefinedStruct_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsNestedType(typeof(TestNestedStruct)));
        }

        [Fact]
        public void IsNestedType_WithUserDefinedClass_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsNestedType(typeof(TestNestedClass)));
        }

        [Fact]
        public void IsNestedType_WithPrimitives_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(int)));
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(string)));
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(bool)));
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(decimal)));
        }

        [Fact]
        public void IsNestedType_WithSystemTypes_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(DateTime)));
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(Guid)));
        }

        [Fact]
        public void IsNestedType_WithSpecialDataTypes_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(LocaleRef)));
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(StringDataRef<TestData>)));
        }

        [Fact]
        public void IsNestedType_WithCollections_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(List<string>)));
            Assert.False(TypeDetectionHelper.IsNestedType(typeof(string[])));
        }

        [Fact]
        public void IsNestedStruct_WithStruct_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsNestedStruct(typeof(TestNestedStruct)));
        }

        [Fact]
        public void IsNestedStruct_WithClass_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsNestedStruct(typeof(TestNestedClass)));
        }

        #endregion

        #region Numeric Type Detection Tests

        [Fact]
        public void IsNumericType_WithNumericTypes_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(int)));
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(long)));
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(float)));
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(double)));
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(decimal)));
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(short)));
            Assert.True(TypeDetectionHelper.IsNumericType(typeof(byte)));
        }

        [Fact]
        public void IsNumericType_WithNonNumericTypes_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsNumericType(typeof(string)));
            Assert.False(TypeDetectionHelper.IsNumericType(typeof(bool)));
            Assert.False(TypeDetectionHelper.IsNumericType(typeof(DateTime)));
        }

        [Fact]
        public void IsIntegerType_WithIntegerTypes_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsIntegerType(typeof(int)));
            Assert.True(TypeDetectionHelper.IsIntegerType(typeof(long)));
            Assert.True(TypeDetectionHelper.IsIntegerType(typeof(short)));
            Assert.True(TypeDetectionHelper.IsIntegerType(typeof(byte)));
        }

        [Fact]
        public void IsIntegerType_WithFloatingPointTypes_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsIntegerType(typeof(float)));
            Assert.False(TypeDetectionHelper.IsIntegerType(typeof(double)));
            Assert.False(TypeDetectionHelper.IsIntegerType(typeof(decimal)));
        }

        [Fact]
        public void IsFloatingPointType_WithFloatingPointTypes_ReturnsTrue()
        {
            Assert.True(TypeDetectionHelper.IsFloatingPointType(typeof(float)));
            Assert.True(TypeDetectionHelper.IsFloatingPointType(typeof(double)));
            Assert.True(TypeDetectionHelper.IsFloatingPointType(typeof(decimal)));
        }

        [Fact]
        public void IsFloatingPointType_WithIntegerTypes_ReturnsFalse()
        {
            Assert.False(TypeDetectionHelper.IsFloatingPointType(typeof(int)));
            Assert.False(TypeDetectionHelper.IsFloatingPointType(typeof(long)));
        }

        #endregion

        #region Member Utilities Tests

        [Fact]
        public void GetMemberType_WithPropertyInfo_ReturnsPropertyType()
        {
            var prop = typeof(TestNestedClass).GetProperty(nameof(TestNestedClass.Name));
            Assert.Equal(typeof(string), TypeDetectionHelper.GetMemberType(prop!));
        }

        [Fact]
        public void GetMemberType_WithFieldInfo_ReturnsFieldType()
        {
            var field = typeof(TestNestedStruct).GetField(nameof(TestNestedStruct.Value));
            Assert.Equal(typeof(int), TypeDetectionHelper.GetMemberType(field!));
        }

        [Fact]
        public void GetEditableMembers_ReturnsPublicMembersOnly()
        {
            var members = TypeDetectionHelper.GetEditableMembers(typeof(TestNestedClass));
            Assert.Contains(members, m => m.Name == "Name");
            Assert.Contains(members, m => m.Name == "Count");
        }

        #endregion

        #region Test Types

        public class TestData : Datra.Interfaces.ITableData<string>
        {
            public string Id { get; set; } = "";
        }

        public class TestIntData : Datra.Interfaces.ITableData<int>
        {
            public int Id { get; set; }
        }

        public struct TestNestedStruct
        {
            public int Value;
            public string Text { get; set; }
        }

        public class TestNestedClass
        {
            public string Name { get; set; } = "";
            public int Count { get; set; }
        }

        #endregion
    }
}
