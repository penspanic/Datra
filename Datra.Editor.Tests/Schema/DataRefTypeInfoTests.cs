#nullable enable
using System;
using Datra.DataTypes;
using Datra.Editor.Schema;
using Datra.Interfaces;
using Xunit;

namespace Datra.Editor.Tests.Schema
{
    public class DataRefTypeInfoTests
    {
        private class StringTarget : ITableData<string>
        {
            public string Id { get; set; } = "";
        }

        private class IntTarget : ITableData<int>
        {
            public int Id { get; set; }
        }

        [Fact]
        public void TryCreate_Returns_Null_For_NonDataRef()
            => Assert.Null(DataRefTypeInfo.TryCreate(typeof(string)));

        [Fact]
        public void TryCreate_StringDataRef_Roundtrip()
        {
            var info = DataRefTypeInfo.TryCreate(typeof(StringDataRef<StringTarget>));
            Assert.NotNull(info);
            Assert.True(info!.IsStringKey);
            Assert.Equal(typeof(StringTarget), info.ReferencedType);
            Assert.Equal(typeof(string), info.KeyType);

            var empty = info.CreateEmpty();
            Assert.IsType<StringDataRef<StringTarget>>(empty);
            // CreateEmpty produces default(struct) — Value is null (not the empty string)
            Assert.Null(info.GetKey(empty));

            var built = info.Build("hero_01");
            Assert.IsType<StringDataRef<StringTarget>>(built);
            Assert.Equal("hero_01", info.GetKey(built));
        }

        [Fact]
        public void TryCreate_IntDataRef_Roundtrip()
        {
            var info = DataRefTypeInfo.TryCreate(typeof(IntDataRef<IntTarget>));
            Assert.NotNull(info);
            Assert.False(info!.IsStringKey);
            Assert.Equal(typeof(int), info.KeyType);

            var built = info.Build(42);
            Assert.IsType<IntDataRef<IntTarget>>(built);
            Assert.Equal(42, info.GetKey(built));
        }

        [Fact]
        public void Build_With_WrongKey_Throws()
        {
            var info = DataRefTypeInfo.TryCreate(typeof(IntDataRef<IntTarget>))!;
            Assert.Throws<ArgumentException>(() => info.Build("not_an_int"));
        }

        [Fact]
        public void Build_With_Null_Yields_Empty()
        {
            var info = DataRefTypeInfo.TryCreate(typeof(StringDataRef<StringTarget>))!;
            var v = info.Build(null);
            Assert.IsType<StringDataRef<StringTarget>>(v);
            Assert.Null(info.GetKey(v));
        }
    }
}
