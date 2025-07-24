using Datra.Interfaces;
using Datra.Serializers;
using Datra.Unity.Editor.Attributes;
using Datra.Unity.Editor.Providers;
using Datra.Unity.Runtime.Providers;
using Datra.Unity.Sample.Models;
using UnityEngine;

namespace Datra.Unity.Sample.Editor
{
    /// <summary>
    /// Example of Datra editor initialization for Unity
    /// </summary>
    public static class DatraEditorInit
    {
        [DatraEditorInit("Sample Game Data Context", priority: 100)]
        public static IDataContext InitializeGameDataContext()
        {
            Debug.Log("[DatraEditorInit] Initializing GameDataContext for editor...");
            
            // Create RawDataProvider and LoaderFactory
            var rawDataProvider = new AssetDatabaseRawDataProvider("Assets/Resources");
            var serializerFactory = new DataSerializerFactory();
            
            // Create GameDataContext
            var context = new GameDataContext(rawDataProvider, serializerFactory);
            
            // Load all data synchronously for editor
            var loadTask = context.LoadAllAsync();
            loadTask.Wait();
            
            Debug.Log("[DatraEditorInit] GameDataContext initialized successfully");
            
            return context;
        }
        
        // You can have multiple initializers for different contexts
        // [DatraEditorInit("Test Data Context", priority: 50)]
        // public static IDataContext InitializeTestDataContext()
        // {
        //     // Another context initialization
        // }
    }
}