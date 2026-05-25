using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
#pragma warning disable IL2026, IL3050
    public class DatraJsonTests
    {
        // --- 익명 객체 직렬화 ---

        [Fact]
        public void Serialize_AnonymousObject_직렬화_성공()
        {
            var anon = new { Name = "Alice", Age = 30 };

            var json = DatraJson.Serialize(anon);

            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Alice\"", json);
            Assert.Contains("\"Age\"", json);
            Assert.Contains("30", json);
        }

        [Fact]
        public void Serialize_AnonymousObject_중첩_직렬화_성공()
        {
            var anon = new
            {
                User = new { Name = "Bob" },
                Items = new[] { "sword", "shield" }
            };

            var json = DatraJson.Serialize(anon);

            Assert.Contains("\"User\"", json);
            Assert.Contains("\"Bob\"", json);
            Assert.Contains("\"Items\"", json);
            Assert.Contains("sword", json);
        }

        [Fact]
        public void Serialize_AnonymousObject_빈값아님()
        {
            var anon = new { key = "value" };

            var json = DatraJson.Serialize(anon);

            Assert.NotEqual("{}", json.Trim());
        }

        // --- getter-only 프로퍼티 제외 ---

        [Fact]
        public void Serialize_GetterOnly프로퍼티_제외()
        {
            // The Datra contract modifier only strips Datra-specific computed types
            // (LocaleRef / NestedLocaleRef). Plain user-declared get-only properties
            // are still serialized — opt out via [JsonIgnore].
            var obj = new ClassWithGetterOnly { Name = "Test" };

            var json = DatraJson.Serialize(obj);

            Assert.Contains("\"Name\"", json);
            Assert.Contains("\"Computed\"", json);
        }

        // --- 일반 클래스 직렬화/역직렬화 ---

        [Fact]
        public void Serialize_Deserialize_PascalCase_라운드트립()
        {
            var original = new SimpleModel { Id = "item_001", DisplayName = "검" };

            var json = DatraJson.Serialize(original);
            var restored = DatraJson.Deserialize<SimpleModel>(json);

            Assert.Equal(original.Id, restored.Id);
            Assert.Equal(original.DisplayName, restored.DisplayName);
        }

        [Fact]
        public void Serialize_NullValue_무시()
        {
            var obj = new SimpleModel { Id = "test", DisplayName = null! };

            var json = DatraJson.Serialize(obj);

            Assert.Contains("\"Id\"", json);
            Assert.DoesNotContain("\"DisplayName\"", json);
        }

        [Fact]
        public void Deserialize_PascalCase_JSON_정상파싱()
        {
            var json = """{"Id":"hero_001","DisplayName":"기사"}""";

            var result = DatraJson.Deserialize<SimpleModel>(json);

            Assert.Equal("hero_001", result.Id);
            Assert.Equal("기사", result.DisplayName);
        }

        // --- 테스트용 모델 ---

        private class SimpleModel
        {
            public string Id { get; set; } = "";
            public string DisplayName { get; set; } = "";
        }

        private class ClassWithGetterOnly
        {
            public string Name { get; set; } = "";
            public string Computed => $"[{Name}]";
        }
    }
#pragma warning restore IL2026, IL3050
}
