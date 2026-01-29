using Datra.Serializers;
using Newtonsoft.Json;
using Xunit;

namespace Datra.Tests
{
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
            // WritablePropertiesOnlyContractResolver가 익명 객체를 {}로 직렬화하지 않는지 검증
            var anon = new { key = "value" };

            var json = DatraJson.Serialize(anon);

            Assert.NotEqual("{}", json.Trim());
        }

        // --- getter-only 프로퍼티 제외 (기존 동작 유지) ---

        [Fact]
        public void Serialize_GetterOnly프로퍼티_제외()
        {
            var obj = new ClassWithGetterOnly { Name = "Test" };

            var json = DatraJson.Serialize(obj);

            Assert.Contains("\"Name\"", json);
            Assert.DoesNotContain("\"Computed\"", json);
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

        // --- ContractResolver 직접 검증 ---

        [Fact]
        public void WritablePropertiesOnlyContractResolver_일반클래스_GetterOnly제외()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new WritablePropertiesOnlyContractResolver()
            };

            var obj = new ClassWithGetterOnly { Name = "Hello" };
            var json = JsonConvert.SerializeObject(obj, settings);

            Assert.Contains("\"Name\"", json);
            Assert.DoesNotContain("\"Computed\"", json);
        }

        [Fact]
        public void WritablePropertiesOnlyContractResolver_익명객체_모든프로퍼티포함()
        {
            var settings = new JsonSerializerSettings
            {
                ContractResolver = new WritablePropertiesOnlyContractResolver()
            };

            var anon = new { username = "admin", role = "editor" };
            var json = JsonConvert.SerializeObject(anon, settings);

            Assert.Contains("username", json);
            Assert.Contains("admin", json);
            Assert.Contains("role", json);
            Assert.Contains("editor", json);
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
            public string Computed => $"[{Name}]"; // getter-only — 직렬화 제외 대상
        }
    }
}
