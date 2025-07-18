using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Data.Loaders;
using Datra.Tests.Models;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    public class DataLoadingTests
    {
        private readonly ITestOutputHelper _output;
        private readonly string _basePath;
        private readonly TestRawDataProvider _rawDataProvider;
        private readonly DataLoaderFactory _loaderFactory;

        public DataLoadingTests(ITestOutputHelper output)
        {
            _output = output;
            _basePath = FindDataPath();
            _rawDataProvider = new TestRawDataProvider(_basePath);
            _loaderFactory = new DataLoaderFactory();
        }

        private static string FindDataPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            
            while (currentDir != null)
            {
                var resourcesPath = Path.Combine(currentDir, "Resources");
                if (Directory.Exists(resourcesPath))
                {
                    return resourcesPath;
                }
                
                // Find Resources folder in Datra.Tests project directory
                var testProjectPath = Path.Combine(currentDir, "Datra.Tests", "Resources");
                if (Directory.Exists(testProjectPath))
                {
                    return testProjectPath;
                }
                
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
            
            throw new DirectoryNotFoundException("Could not find Resources directory");
        }

        [Fact]
        public void CreateGameDataContext_ShouldSucceed()
        {
            // Arrange & Act
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            
            // Assert
            Assert.NotNull(context);
            _output.WriteLine($"Data path: {_basePath}");
            _output.WriteLine("GameDataContext created successfully");
        }

        [Fact]
        public async Task LoadAllAsync_ShouldLoadAllDataSuccessfully()
        {
            // Arrange
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            
            // Act
            await context.LoadAllAsync();
            
            // Assert
            Assert.NotNull(context.Character);
            Assert.NotNull(context.Item);
            Assert.NotNull(context.GameConfig);
            _output.WriteLine("All data loaded successfully");
        }

        [Fact]
        public async Task CharacterData_ShouldLoadCorrectly()
        {
            // Arrange
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            await context.LoadAllAsync();
            
            // Act
            var allCharacters = context.Character.GetAll();
            var firstChar = allCharacters.Values.FirstOrDefault();
            
            // Assert
            Assert.NotEmpty(allCharacters);
            Assert.NotNull(firstChar);
            Assert.NotNull(firstChar.Name);
            Assert.True(firstChar.Level > 0);
            Assert.NotNull(firstChar.ClassName);
            
            _output.WriteLine($"Total characters: {allCharacters.Count}");
            _output.WriteLine($"First character: {firstChar.Name} (Lv.{firstChar.Level} {firstChar.ClassName})");
            _output.WriteLine($"  - HP: {firstChar.Health}, MP: {firstChar.Mana}");
            _output.WriteLine($"  - STR: {firstChar.Strength}, INT: {firstChar.Intelligence}, AGI: {firstChar.Agility}");
        }

        [Fact]
        public async Task ItemData_ShouldLoadCorrectly()
        {
            // Arrange
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            await context.LoadAllAsync();
            
            // Act
            var allItems = context.Item.GetAll();
            var item = context.Item.GetById(1001);
            
            // Assert
            Assert.NotEmpty(allItems);
            Assert.NotNull(item);
            Assert.Equal(1001, item.Id);
            Assert.NotNull(item.Name);
            Assert.NotNull(item.Description);
            Assert.True(item.Price >= 0);
            
            _output.WriteLine($"Total items: {allItems.Count}");
            _output.WriteLine($"Item #1001: {item.Name}");
            _output.WriteLine($"  - Description: {item.Description}");
            _output.WriteLine($"  - Price: {item.Price} gold");
            _output.WriteLine($"  - Type: {item.Type}");
            _output.WriteLine($"  - Attack: {item.Attack}, Defense: {item.Defense}");
        }

        [Fact]
        public async Task GameConfig_ShouldLoadCorrectly()
        {
            // Arrange
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            await context.LoadAllAsync();
            
            // Act
            var config = context.GameConfig.Get();
            
            // Assert
            Assert.NotNull(config);
            Assert.True(config.MaxLevel > 0);
            Assert.True(config.ExpMultiplier > 0);
            Assert.True(config.StartingGold >= 0);
            Assert.True(config.InventorySize > 0);
            
            _output.WriteLine($"Max Level: {config.MaxLevel}");
            _output.WriteLine($"Exp Multiplier: {config.ExpMultiplier}");
            _output.WriteLine($"Starting Gold: {config.StartingGold}");
            _output.WriteLine($"Inventory Size: {config.InventorySize}");
        }

        [Fact]
        public async Task TryGetById_WithInvalidId_ShouldReturnNull()
        {
            // Arrange
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            await context.LoadAllAsync();
            
            // Act
            var item = context.Item.TryGetById(99999); // Non-existent ID
            
            // Assert
            Assert.Null(item);
        }

        [Fact]
        public async Task GetById_WithInvalidId_ShouldThrowException()
        {
            // Arrange
            var context = new GameDataContext(_rawDataProvider, _loaderFactory);
            await context.LoadAllAsync();
            
            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => context.Item.GetById(99999));
        }
    }
}