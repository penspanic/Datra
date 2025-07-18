using System;
using System.Linq;
using Datra.Client.Data;
using Datra.Data.Loaders;
using Datra.Unity.Sample.Models;
using UnityEngine;

namespace Datra.Unity.Sample
{
    public class DatraTest : MonoBehaviour
    {
        private async void Start()
        {
            // Create RawDataProvider and LoaderFactory
            var rawDataProvider = new ResourcesRawDataProvider();
            var loaderFactory = new DataLoaderFactory();

            // Create GameDataContext
            var context = new GameDataContext(rawDataProvider, loaderFactory);
            Debug.Log("✅ GameDataContext created successfully");
            Debug.Log("");

            // Load all data
            await context.LoadAllAsync();
            Debug.Log("✅ All data loaded successfully");

            TestCharacters(context);
            TestItems(context);
            TestGameConfig(context);
        }

        static void TestCharacters(GameDataContext context)
        {
            Debug.Log("=== Character Data ===");

            var allCharacters = context.Character.GetAll();
            Debug.Log($"Total characters: {allCharacters.Count}");

            // Output first character info
            var firstChar = allCharacters.Values.FirstOrDefault();
            if (firstChar != null)
            {
                Debug.Log($"First character: {firstChar.Name} (Lv.{firstChar.Level} {firstChar.ClassName})");
                Debug.Log($"  - HP: {firstChar.Health}, MP: {firstChar.Mana}");
                Debug.Log($"  - STR: {firstChar.Strength}, INT: {firstChar.Intelligence}, AGI: {firstChar.Agility}");
            }

            Debug.Log("");
        }

        static void TestItems(GameDataContext context)
        {
            Debug.Log("=== Item Data ===");

            var allItems = context.Item.GetAll();
            Debug.Log($"Total items: {allItems.Count}");

            // Get specific item by GetById
            var item = context.Item.GetById(1001);
            if (item != null)
            {
                Debug.Log($"Item #1001: {item.Name}");
                Debug.Log($"  - Description: {item.Description}");
                Debug.Log($"  - Price: {item.Price} gold");
                Debug.Log($"  - Type: {item.Type}");
                Debug.Log($"  - Attack: {item.Attack}, Defense: {item.Defense}");
            }

            Debug.Log("");
        }

        static void TestGameConfig(GameDataContext context)
        {
            Debug.Log("=== Game Config ===");

            var config = context.GameConfig.Get();
            if (config != null)
            {
                Debug.Log($"Max Level: {config.MaxLevel}");
                Debug.Log($"Exp Multiplier: {config.ExpMultiplier}");
                Debug.Log($"Starting Gold: {config.StartingGold}");
                Debug.Log($"Inventory Size: {config.InventorySize}");
            }

            Debug.Log("");
        }
    }
}