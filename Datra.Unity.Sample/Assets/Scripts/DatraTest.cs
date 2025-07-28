using System;
using System.Linq;
using Datra.Generated;
using Datra.SampleData.Models;
using Datra.Serializers;
using Datra.Unity.Runtime.Providers;
using UnityEngine;

namespace Datra.Unity.Sample
{
    public static class DatraBootstrap
    {
        #if UNITY_EDITOR
        [Datra.Unity.Editor.Attributes.DatraEditorInit]
        public static GameDataContext Init()
        {
            //var provider = new ResourcesRawDataProvider();
            var provider = new Editor.Providers.AssetDatabaseRawDataProvider(basePath: "Packages/com.penspanic.datra.sampledata/Resources");
            var serializerFactory = new DataSerializerFactory();

            // Create GameDataContext
            var context = new GameDataContext(provider, serializerFactory);
            
            // Load all data synchronously for editor
            var loadTask = context.LoadAllAsync();
            loadTask.Wait();
            
            Debug.Log("[DatraEditorInit] GameDataContext initialized successfully");
            return context;
        }
        #endif
    }
    public class DatraTest : MonoBehaviour
    {
        private async void Start()
        {
            // Create RawDataProvider and LoaderFactory
            var rawDataProvider = new ResourcesRawDataProvider();
            var serializerFactory = new DataSerializerFactory();

            // Create GameDataContext
            var context = new GameDataContext(rawDataProvider, serializerFactory);
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
                Debug.Log($"DefaultCharacter: {config.DefaultCharacter.Evaluate(context)}");
                Debug.Log($"AvailableModes: {string.Join(",", config.AvailableModes)}");
            }

            Debug.Log("");
        }
    }
}