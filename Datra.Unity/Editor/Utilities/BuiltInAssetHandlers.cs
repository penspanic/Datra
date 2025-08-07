using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Built-in asset handlers for common Unity component types
    /// </summary>
    [InitializeOnLoad]
    public static class BuiltInAssetHandlers
    {
        static BuiltInAssetHandlers()
        {
            // Register handlers after Unity is ready
            EditorApplication.delayCall += RegisterBuiltInHandlers;
        }
        
        private static void RegisterBuiltInHandlers()
        {
            // Common Unity component handlers
            
            // Camera prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Camera>(
                "Unity.Component.Camera", 
                "Camera Prefab"
            );
            
            // Light prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Light>(
                "Unity.Component.Light", 
                "Light Prefab"
            );
            
            // Audio Source prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<AudioSource>(
                "Unity.Component.AudioSource", 
                "Audio Source Prefab"
            );
            
            // Canvas UI prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Canvas>(
                "Unity.Component.Canvas", 
                "UI Canvas Prefab"
            );
            
            // Button UI prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Button>(
                "Unity.Component.Button", 
                "UI Button Prefab"
            );
            
            // Rigidbody physics prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Rigidbody>(
                "Unity.Component.Rigidbody", 
                "Physics Object Prefab"
            );
            
            // 2D physics prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Rigidbody2D>(
                "Unity.Component.Rigidbody2D", 
                "2D Physics Object Prefab"
            );
            
            // Particle System prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<ParticleSystem>(
                "Unity.Component.ParticleSystem", 
                "Particle Effect Prefab"
            );
            
            // Animator prefabs
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Animator>(
                "Unity.Component.Animator", 
                "Animated Prefab"
            );
            
            // Text Mesh Pro prefabs (using runtime lookup since TMP might not be installed)
            ComponentPrefabHandlerFactory.RegisterComponentHandler(
                "TMPro.TextMeshProUGUI", 
                "Unity.Component.TextMeshPro", 
                "Text Mesh Pro Prefab"
            );
            
            // Collider prefabs with Rigidbody
            ComponentPrefabHandlerFactory.RegisterComponentHandler<Collider>(
                "Unity.Component.ColliderWithPhysics", 
                "Collider with Physics",
                "Rigidbody"
            );
            
#if DATRA_DEBUG
            Debug.Log("[Datra] Built-in component asset handlers registered");
#endif
        }
    }
}