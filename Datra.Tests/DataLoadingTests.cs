using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datra.Tests
{
    public class DataLoadingTests
    {
        private readonly ITestOutputHelper _output;

        public DataLoadingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CreateGameDataContext_ShouldSucceed()
        {
            // Arrange & Act
            var context = TestDataHelper.CreateGameDataContext();
            
            // Assert
            Assert.NotNull(context);
            _output.WriteLine($"Data path: {TestDataHelper.FindDataPath()}");
            _output.WriteLine("GameDataContext created successfully");
        }

        [Fact]
        public async Task LoadAllAsync_ShouldLoadAllDataSuccessfully()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            
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
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act
            var allCharacters = context.Character.LoadedItems.Values.ToList();
            var firstChar = allCharacters.FirstOrDefault();
            
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
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act
            var allItems = context.Item.LoadedItems.Values.ToList();
            var item = context.Item.TryGetLoaded(1001);
            
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
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act
            var config = context.GameConfig.Current;
            
            // Assert
            Assert.NotNull(config);
            Assert.True(config.MaxLevel > 0);
            Assert.True(config.ExpMultiplier > 0);
            // Properties have been changed in GameConfigData
            
            _output.WriteLine($"Max Level: {config.MaxLevel}");
            _output.WriteLine($"Exp Multiplier: {config.ExpMultiplier}");
            // Output properties have been changed
        }

        [Fact]
        public async Task TryGetById_WithInvalidId_ShouldReturnNull()
        {
            // Arrange
            var context = TestDataHelper.CreateGameDataContext();
            await context.LoadAllAsync();
            
            // Act
            var item = context.Item.TryGetLoaded(99999); // Non-existent ID
            
            // Assert
            Assert.Null(item);
        }
    }
}