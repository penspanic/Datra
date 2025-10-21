using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    [TableData("Items.json")]
    public partial class ItemData : ITableData<int>
    {
        public int Id { get; set; }

        [FixedLocale]
        public LocaleRef Name => LocaleRef.CreateFixed(nameof(ItemData), Id.ToString(), nameof(Name));

        [FixedLocale]
        public LocaleRef Description => LocaleRef.CreateFixed(nameof(ItemData), Id.ToString(), nameof(Description));
        public int Price { get; set; }
        public ItemType Type { get; set; }
        public int Attack { get; set; }
        public int Defense { get; set; }
        
        // Item icon sprite
        [AssetType(UnityAssetTypes.Sprite)]
        [FolderPath("Assets/UI/Icons/Items/")]
        public string IconSpritePath { get; set; }
        
        // 3D model for dropped items
        [AssetType(UnityAssetTypes.GameObject)]
        [FolderPath("Assets/Prefabs/Items/DroppedItems/")]
        public string DroppedItemPrefabPath { get; set; }
        
        // Use sound effect
        [AssetType(UnityAssetTypes.AudioClip)]
        [FolderPath("Assets/Audio/Items/Use/")]
        public string UseSoundPath { get; set; }
        
        // Particle effect for special items
        [AssetType(UnityAssetTypes.GameObject, RequiredComponents = new[] { "ParticleSystem" })]
        [FolderPath("Assets/Prefabs/Effects/Items/")]
        public string EffectPrefabPath { get; set; }
    }

    public enum ItemType
    {
        Weapon,
        Armor,
        Consumable,
        Material
    }
}