#nullable enable
using System;
using Datra.Editor.Schema;
using Xunit;

namespace Datra.Editor.Tests.Schema
{
    public class DefaultValueFactoryTests
    {
        private class WithDefaultCtor
        {
            public int X { get; set; }
        }

        private class WithoutDefaultCtor
        {
            public WithoutDefaultCtor(int x) { X = x; }
            public int X { get; }
        }

        private struct SampleStruct
        {
            public int N { get; set; }
        }

        [Fact]
        public void String_Yields_EmptyString()
            => Assert.Equal(string.Empty, DefaultValueFactory.CreateDefault(typeof(string)));

        [Fact]
        public void Int_Yields_Zero()
            => Assert.Equal(0, DefaultValueFactory.CreateDefault(typeof(int)));

        [Fact]
        public void Bool_Yields_False()
            => Assert.Equal(false, DefaultValueFactory.CreateDefault(typeof(bool)));

        [Fact]
        public void Struct_Yields_Default()
        {
            var v = DefaultValueFactory.CreateDefault(typeof(SampleStruct));
            Assert.NotNull(v);
            Assert.IsType<SampleStruct>(v);
            Assert.Equal(0, ((SampleStruct)v!).N);
        }

        [Fact]
        public void Class_With_DefaultCtor_Yields_Instance()
        {
            var v = DefaultValueFactory.CreateDefault(typeof(WithDefaultCtor));
            Assert.NotNull(v);
            Assert.IsType<WithDefaultCtor>(v);
        }

        [Fact]
        public void Class_Without_DefaultCtor_Yields_Null()
            => Assert.Null(DefaultValueFactory.CreateDefault(typeof(WithoutDefaultCtor)));

        [Fact]
        public void Null_Type_Throws()
            => Assert.Throws<ArgumentNullException>(() => DefaultValueFactory.CreateDefault(null!));
    }
}
