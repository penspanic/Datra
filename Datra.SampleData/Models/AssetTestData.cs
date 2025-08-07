using System.Collections.Generic;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Test data class demonstrating AssetType and FolderPath attributes
    /// </summary>
    [SingleData("AssetTestData.json")]
    public partial class AssetTestData
    {
        public string Name { get; set; }
        
        // Prefab with folder constraint
        [AssetType(UnityAssetTypes.GameObject)]
        [FolderPath("Assets/Prefabs/Characters/")]
        public string CharacterPrefabPath { get; set; }
        
        // Prefab with component requirements
        [AssetType(UnityAssetTypes.GameObject)]
        public string PlayerPrefabPath { get; set; }
        
        // Weapon prefab in specific folder
        [AssetType(UnityAssetTypes.GameObject)]
        [FolderPath("Assets/Prefabs/Weapons/", SearchPattern = "Weapon_*.prefab")]
        public string WeaponPrefabPath { get; set; }
        
        // UI prefab
        [AssetType(UnityAssetTypes.GameObject, RequiredComponents = new[] { "Canvas" })]
        [FolderPath("Assets/Prefabs/UI/")]
        public string UIPrefabPath { get; set; }
        
        // Texture assets
        [AssetType(UnityAssetTypes.Texture2D)]
        [FolderPath("Assets/Textures/Icons/")]
        public string IconTexturePath { get; set; }
        
        // Sprite for UI
        [AssetType(UnityAssetTypes.Sprite)]
        [FolderPath("Assets/Sprites/", IncludeSubfolders = true)]
        public string SpriteIconPath { get; set; }
        
        // Audio clip
        [AssetType(UnityAssetTypes.AudioClip)]
        [FolderPath("Assets/Audio/SFX/")]
        public string SoundEffectPath { get; set; }
        
        // Animation clip
        [AssetType(UnityAssetTypes.AnimationClip)]
        [FolderPath("Assets/Animations/Characters/")]
        public string CharacterAnimationPath { get; set; }
        
        // Material
        [AssetType(UnityAssetTypes.Material)]
        [FolderPath("Assets/Materials/")]
        public string MaterialPath { get; set; }
        
        // ScriptableObject data
        [AssetType(UnityAssetTypes.ScriptableObject)]
        [FolderPath("Assets/Data/Items/")]
        public string ItemDataPath { get; set; }
        
        // Custom type example
        [AssetType("MyType.WeaponConfig")]
        [FolderPath("Assets/Configs/Weapons/")]
        public string WeaponConfigPath { get; set; }
        
        // Multiple paths as list
        [AssetType(UnityAssetTypes.GameObject)]
        [FolderPath("Assets/Prefabs/Effects/", SearchPattern = "VFX_*.prefab")]
        public List<string> EffectPrefabPaths { get; set; }
        
        // Scene reference
        [AssetType(UnityAssetTypes.Scene)]
        [FolderPath("Assets/Scenes/Levels/")]
        public string LevelScenePath { get; set; }
    }
}