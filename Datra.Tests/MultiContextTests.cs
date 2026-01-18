using System.IO;
using System.Threading.Tasks;
using Datra.SampleData.Generated;
using Datra.SampleData2.Generated;
using Datra.Serializers;
using Xunit;

namespace Datra.Tests
{
    /// <summary>
    /// Tests to verify that multiple DataContexts can coexist in the same project.
    /// </summary>
    public class MultiContextTests
    {
        [Fact]
        public void Should_SupportMultipleContexts()
        {
            // Verify that both GameDataContext and ShopContext exist
            Assert.NotNull(typeof(GameDataContext));
            Assert.NotNull(typeof(ShopContext));
        }

        [Fact]
        public void Should_GenerateContextsInDifferentNamespaces()
        {
            // GameDataContext should be in Datra.SampleData.Generated
            Assert.Equal("Datra.SampleData.Generated", typeof(GameDataContext).Namespace);

            // ShopContext should be in Datra.SampleData2.Generated
            Assert.Equal("Datra.SampleData2.Generated", typeof(ShopContext).Namespace);
        }

        [Fact]
        public async Task Should_LoadDataFromBothContexts()
        {
            // Create GameDataContext
            var gameDataPath = TestDataHelper.FindDataPath();
            var gameDataProvider = new TestRawDataProvider(gameDataPath);
            var gameContext = new GameDataContext(gameDataProvider, new DataSerializerFactory());
            await gameContext.LoadAllAsync();

            // Create ShopContext
            var shopDataPath = FindShopDataPath();
            var shopDataProvider = new TestRawDataProvider(shopDataPath);
            var shopContext = new ShopContext(shopDataProvider, new DataSerializerFactory());
            await shopContext.LoadAllAsync();

            // Verify data from GameDataContext
            Assert.True(gameContext.Character.Count > 0);

            // Verify data from ShopContext
            Assert.True(shopContext.ShopItem.Count > 0);
            Assert.Equal("Small HP Potion", shopContext.ShopItem.LoadedItems["potion_hp_small"].Name);
            Assert.Equal(100, shopContext.ShopItem.LoadedItems["potion_hp_small"].Price);
        }

        private static string FindShopDataPath()
        {
            var currentDir = Directory.GetCurrentDirectory();

            while (currentDir != null)
            {
                var resourcesPath = Path.Combine(currentDir, "Datra.SampleData2", "Resources");
                if (Directory.Exists(resourcesPath))
                {
                    return resourcesPath;
                }

                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find Datra.SampleData2/Resources directory");
        }
    }
}
