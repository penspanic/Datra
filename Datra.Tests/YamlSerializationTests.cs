using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.SampleData.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    /// <summary>
    /// Tests for YAML serialization and deserialization support.
    /// </summary>
    public class YamlSerializationTests
    {
        private readonly ITestOutputHelper _output;
        private readonly GameDataContext _context;

        public YamlSerializationTests(ITestOutputHelper output)
        {
            _output = output;
            _context = TestDataHelper.CreateGameDataContext();
            _context.LoadAllAsync().Wait();
        }

        #region TableData Tests

        [Fact]
        public void Should_LoadTableData_FromYaml()
        {
            // Act
            var enemies = _context.Enemy.Values.ToList();

            // Assert
            Assert.NotEmpty(enemies);
            Assert.True(enemies.Count >= 5);
            _output.WriteLine($"Loaded {enemies.Count} enemies from YAML");
        }

        [Fact]
        public void Should_ParseBasicTypes_FromYaml()
        {
            // Act
            _context.Enemy.TryGetValue("goblin_001", out var goblin);

            // Assert
            Assert.NotNull(goblin);
            Assert.Equal("goblin_001", goblin.Id);
            Assert.Equal("Goblin Scout", goblin.Name);
            Assert.Equal(1, goblin.Level);
            Assert.Equal(50, goblin.Health);
            Assert.Equal(8, goblin.Attack);
            Assert.Equal(2, goblin.Defense);
            Assert.Equal(1.2f, goblin.Speed);
            Assert.Equal(0.15, goblin.DropRate);
            Assert.False(goblin.IsFlyable);
        }

        [Fact]
        public void Should_ParseEnum_FromYaml()
        {
            // Act
            _context.Enemy.TryGetValue("goblin_001", out var goblin);
            _context.Enemy.TryGetValue("fire_elemental_001", out var elemental);
            _context.Enemy.TryGetValue("dragon_boss", out var dragon);

            // Assert
            Assert.Equal(EnemyType.Normal, goblin.Type);
            Assert.Equal(Element.None, goblin.Element);

            Assert.Equal(EnemyType.Elite, elemental.Type);
            Assert.Equal(Element.Fire, elemental.Element);

            Assert.Equal(EnemyType.Boss, dragon.Type);
            Assert.Equal(Element.Fire, dragon.Element);
        }

        [Fact]
        public void Should_ParseArrays_FromYaml()
        {
            // Act
            _context.Enemy.TryGetValue("dragon_boss", out var dragon);

            // Assert
            Assert.NotNull(dragon.Abilities);
            Assert.Equal(5, dragon.Abilities.Length);
            Assert.Contains("Dragon Breath", dragon.Abilities);
            Assert.Contains("Tail Swipe", dragon.Abilities);

            Assert.NotNull(dragon.DropItemIds);
            Assert.Equal(2, dragon.DropItemIds.Length);
            Assert.Contains(3001, dragon.DropItemIds);
            Assert.Contains(3002, dragon.DropItemIds);
        }

        [Fact]
        public void Should_ParseDataRef_FromYaml()
        {
            // Act
            _context.Enemy.TryGetValue("goblin_001", out var goblin);
            _context.Enemy.TryGetValue("dragon_boss", out var dragon);

            // Assert
            Assert.Equal(2001, goblin.GuaranteedDrop.Value);
            Assert.Equal(3002, dragon.GuaranteedDrop.Value);
        }

        [Fact]
        public void Should_ParseBooleans_FromYaml()
        {
            // Act
            _context.Enemy.TryGetValue("goblin_001", out var goblin);
            _context.Enemy.TryGetValue("fire_elemental_001", out var elemental);

            // Assert
            Assert.False(goblin.IsFlyable);
            Assert.True(elemental.IsFlyable);
        }

        [Fact]
        public void Should_DeserializeYaml_WithGeneratedMethod()
        {
            // Arrange
            var yamlPath = Path.Combine(TestDataHelper.FindDataPath(), "Enemies.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act - using generated DeserializeYaml method
            var enemies = EnemyDataSerializer.DeserializeYaml(yamlContent);

            // Assert
            Assert.NotEmpty(enemies);
            Assert.True(enemies.ContainsKey("goblin_001"));
            Assert.True(enemies.ContainsKey("dragon_boss"));
        }

        [Fact]
        public void Should_SerializeYaml_WithGeneratedMethod()
        {
            // Arrange
            var enemies = _context.Enemy.Values.ToDictionary(e => e.Id);

            // Act - using generated SerializeYaml method
            var yaml = EnemyDataSerializer.SerializeYaml(enemies);

            // Assert
            Assert.NotEmpty(yaml);
            Assert.Contains("goblin_001", yaml);
            Assert.Contains("dragon_boss", yaml);
            Assert.Contains("Goblin Scout", yaml);
            Assert.Contains("Dragon Breath", yaml);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml.Substring(0, System.Math.Min(500, yaml.Length)));
        }

        [Fact]
        public void Should_RoundTrip_TableData_Yaml()
        {
            // Arrange
            var original = _context.Enemy.Values.ToDictionary(e => e.Id);

            // Act
            var yaml = EnemyDataSerializer.SerializeYaml(original);
            var deserialized = EnemyDataSerializer.DeserializeYaml(yaml);

            // Assert
            Assert.Equal(original.Count, deserialized.Count);
            foreach (var key in original.Keys)
            {
                Assert.True(deserialized.ContainsKey(key));
                Assert.Equal(original[key].Name, deserialized[key].Name);
                Assert.Equal(original[key].Level, deserialized[key].Level);
                Assert.Equal(original[key].Health, deserialized[key].Health);
                Assert.Equal(original[key].Type, deserialized[key].Type);
                Assert.Equal(original[key].Element, deserialized[key].Element);
            }
        }

        #endregion

        #region SingleData Tests

        [Fact]
        public void Should_LoadSingleData_FromYaml()
        {
            // Act
            var settings = _context.ServerSettings.Get();

            // Assert
            Assert.NotNull(settings);
            Assert.Equal("Datra Test Server", settings.ServerName);
            Assert.Equal("1.0.0", settings.Version);
        }

        [Fact]
        public void Should_ParseBasicTypes_InSingleData_FromYaml()
        {
            // Act
            var settings = _context.ServerSettings.Get();

            // Assert
            Assert.Equal(100, settings.MaxPlayers);
            Assert.Equal(7777, settings.Port);
            Assert.False(settings.MaintenanceMode);
            Assert.Equal(60.0f, settings.TickRate);
            Assert.Equal(0.05, settings.SyncInterval);
            Assert.Equal("Welcome to Datra Test Server!", settings.WelcomeMessage);
        }

        [Fact]
        public void Should_ParseEnum_InSingleData_FromYaml()
        {
            // Act
            var settings = _context.ServerSettings.Get();

            // Assert
            Assert.Equal(ServerRegion.Asia, settings.Region);
        }

        [Fact]
        public void Should_ParseEnumArray_InSingleData_FromYaml()
        {
            // Act
            var settings = _context.ServerSettings.Get();

            // Assert
            Assert.NotNull(settings.AllowedRegions);
            Assert.Equal(3, settings.AllowedRegions.Length);
            Assert.Contains(ServerRegion.Asia, settings.AllowedRegions);
            Assert.Contains(ServerRegion.Europe, settings.AllowedRegions);
            Assert.Contains(ServerRegion.NorthAmerica, settings.AllowedRegions);
        }

        [Fact]
        public void Should_ParseStringArray_InSingleData_FromYaml()
        {
            // Act
            var settings = _context.ServerSettings.Get();

            // Assert
            Assert.NotNull(settings.AdminIds);
            Assert.Equal(3, settings.AdminIds.Length);
            Assert.Contains("admin_001", settings.AdminIds);
            Assert.Contains("admin_002", settings.AdminIds);
            Assert.Contains("superadmin", settings.AdminIds);
        }

        [Fact]
        public void Should_ParseDataRef_InSingleData_FromYaml()
        {
            // Act
            var settings = _context.ServerSettings.Get();

            // Assert
            Assert.Equal("hero_001", settings.DefaultCharacter.Value);
        }

        [Fact]
        public void Should_DeserializeYaml_SingleData_WithGeneratedMethod()
        {
            // Arrange
            var yamlPath = Path.Combine(TestDataHelper.FindDataPath(), "ServerSettings.yaml");
            var yamlContent = File.ReadAllText(yamlPath);

            // Act - using generated DeserializeYaml method
            var settings = ServerSettingsDataSerializer.DeserializeYaml(yamlContent);

            // Assert
            Assert.NotNull(settings);
            Assert.Equal("Datra Test Server", settings.ServerName);
            Assert.Equal(100, settings.MaxPlayers);
        }

        [Fact]
        public void Should_SerializeYaml_SingleData_WithGeneratedMethod()
        {
            // Arrange
            var settings = _context.ServerSettings.Get();

            // Act - using generated SerializeYaml method
            var yaml = ServerSettingsDataSerializer.SerializeYaml(settings);

            // Assert
            Assert.NotEmpty(yaml);
            Assert.Contains("ServerName", yaml);
            Assert.Contains("Datra Test Server", yaml);
            Assert.Contains("MaxPlayers", yaml);
            Assert.Contains("100", yaml);
            _output.WriteLine("Serialized YAML:");
            _output.WriteLine(yaml);
        }

        [Fact]
        public void Should_RoundTrip_SingleData_Yaml()
        {
            // Arrange
            var original = _context.ServerSettings.Get();

            // Act
            var yaml = ServerSettingsDataSerializer.SerializeYaml(original);
            var deserialized = ServerSettingsDataSerializer.DeserializeYaml(yaml);

            // Assert
            Assert.Equal(original.ServerName, deserialized.ServerName);
            Assert.Equal(original.Version, deserialized.Version);
            Assert.Equal(original.MaxPlayers, deserialized.MaxPlayers);
            Assert.Equal(original.Port, deserialized.Port);
            Assert.Equal(original.MaintenanceMode, deserialized.MaintenanceMode);
            Assert.Equal(original.TickRate, deserialized.TickRate);
            Assert.Equal(original.SyncInterval, deserialized.SyncInterval);
            Assert.Equal(original.Region, deserialized.Region);
            Assert.Equal(original.WelcomeMessage, deserialized.WelcomeMessage);
            Assert.Equal(original.DefaultCharacter.Value, deserialized.DefaultCharacter.Value);
            Assert.Equal(original.AllowedRegions.Length, deserialized.AllowedRegions.Length);
            Assert.Equal(original.AdminIds.Length, deserialized.AdminIds.Length);
        }

        #endregion

        #region YamlDataSerializer Tests

        [Fact]
        public void Should_UseYamlDataSerializer_ForTableData()
        {
            // Arrange
            var yamlPath = Path.Combine(TestDataHelper.FindDataPath(), "Enemies.yaml");
            var yamlContent = File.ReadAllText(yamlPath);
            var serializer = new YamlDataSerializer();

            // Act
            var enemies = EnemyDataSerializer.DeserializeTable(yamlContent, serializer);

            // Assert
            Assert.NotEmpty(enemies);
            Assert.True(enemies.ContainsKey("goblin_001"));
        }

        [Fact]
        public void Should_UseYamlDataSerializer_ForSingleData()
        {
            // Arrange
            var yamlPath = Path.Combine(TestDataHelper.FindDataPath(), "ServerSettings.yaml");
            var yamlContent = File.ReadAllText(yamlPath);
            var serializer = new YamlDataSerializer();

            // Act
            var settings = ServerSettingsDataSerializer.DeserializeSingle(yamlContent, serializer);

            // Assert
            Assert.NotNull(settings);
            Assert.Equal("Datra Test Server", settings.ServerName);
        }

        #endregion
    }
}
