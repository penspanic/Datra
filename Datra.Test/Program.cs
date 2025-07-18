using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datra.Data.Loaders;
using Datra.Test.Models;

namespace Datra.Test
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Datra Data Loading Test ===");
            Console.WriteLine();
            
            try
            {
                // Find path where data files are located
                var basePath = FindDataPath();
                Console.WriteLine($"Data path: {basePath}");
                Console.WriteLine();
                
                // Create RawDataProvider and LoaderFactory
                var rawDataProvider = new TestRawDataProvider(basePath);
                var loaderFactory = new DataLoaderFactory();
                
                // Create GameDataContext
                var context = new GameDataContext(rawDataProvider, loaderFactory);
                Console.WriteLine("✅ GameDataContext created successfully");
                Console.WriteLine();
                
                // Load all data
                await context.LoadAllAsync();
                Console.WriteLine("✅ All data loaded successfully");
                Console.WriteLine();
                
                // Test Characters data
                TestCharacters(context);
                
                // Test Items data
                TestItems(context);
                
                // Test GameConfig data
                TestGameConfig(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"   Inner: {ex.InnerException.Message}");
                }
            }
        }
        
        static string FindDataPath()
        {
            var currentDir = Directory.GetCurrentDirectory();
            
            while (currentDir != null)
            {
                var resourcesPath = Path.Combine(currentDir, "Resources");
                if (Directory.Exists(resourcesPath))
                {
                    return resourcesPath;
                }
                
                // Find Resources folder in Datra.Test project directory
                var testProjectPath = Path.Combine(currentDir, "Datra.Test", "Resources");
                if (Directory.Exists(testProjectPath))
                {
                    return testProjectPath;
                }
                
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
            
            throw new DirectoryNotFoundException("Could not find Resources directory");
        }
        
        static void TestCharacters(GameDataContext context)
        {
            Console.WriteLine("=== Character Data ===");
            
            var allCharacters = context.Character.GetAll();
            Console.WriteLine($"Total characters: {allCharacters.Count}");
            
            // Output first character info
            var firstChar = allCharacters.Values.FirstOrDefault();
            if (firstChar != null)
            {
                Console.WriteLine($"First character: {firstChar.Name} (Lv.{firstChar.Level} {firstChar.ClassName})");
                Console.WriteLine($"  - HP: {firstChar.Health}, MP: {firstChar.Mana}");
                Console.WriteLine($"  - STR: {firstChar.Strength}, INT: {firstChar.Intelligence}, AGI: {firstChar.Agility}");
            }
            
            Console.WriteLine();
        }
        
        static void TestItems(GameDataContext context)
        {
            Console.WriteLine("=== Item Data ===");
            
            var allItems = context.Item.GetAll();
            Console.WriteLine($"Total items: {allItems.Count}");
            
            // Get specific item by GetById
            var item = context.Item.GetById(1001);
            if (item != null)
            {
                Console.WriteLine($"Item #1001: {item.Name}");
                Console.WriteLine($"  - Description: {item.Description}");
                Console.WriteLine($"  - Price: {item.Price} gold");
                Console.WriteLine($"  - Type: {item.Type}");
                Console.WriteLine($"  - Attack: {item.Attack}, Defense: {item.Defense}");
            }
            
            Console.WriteLine();
        }
        
        static void TestGameConfig(GameDataContext context)
        {
            Console.WriteLine("=== Game Config ===");
            
            var config = context.GameConfig.Get();
            if (config != null)
            {
                Console.WriteLine($"Max Level: {config.MaxLevel}");
                Console.WriteLine($"Exp Multiplier: {config.ExpMultiplier}");
                Console.WriteLine($"Starting Gold: {config.StartingGold}");
                Console.WriteLine($"Inventory Size: {config.InventorySize}");
            }
            
            Console.WriteLine();
        }
    }
}