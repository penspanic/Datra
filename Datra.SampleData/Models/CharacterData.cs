using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    public enum CharacterGrade
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [TableData("Characters.csv", Format = DataFormat.Csv)]
    public partial class CharacterData : ITableData<string>
    {
        public string Id { get; set; }

        [FixedLocale]
        public LocaleRef Name => LocaleRef.CreateFixed(nameof(CharacterData), Id, nameof(Name));
        public int Level { get; set; }
        public int Health { get; set; }
        public int Mana { get; set; }
        public int Strength { get; set; }
        public int Intelligence { get; set; }
        public int Agility { get; set; }
        public string ClassName { get; set; }
        public CharacterGrade Grade { get; set; }
        public StatType[] Stats { get; set; } // Array of stat types
        public int[] UpgradeCosts { get; set; } // Array of upgrade costs for each level

        public PooledPrefab TestPooledPrefab { get; set; }
        
        // Character model prefab
        [AssetType(UnityAssetTypes.GameObject)]
        [FolderPath("Assets/Prefabs/Characters/Models/")]
        public string ModelPrefabPath { get; set; }
        
        // Character portrait
        [AssetType(UnityAssetTypes.Sprite)]
        [FolderPath("Assets/UI/Portraits/")]
        public string PortraitPath { get; set; }
        
        // Character icon for UI
        [AssetType(UnityAssetTypes.Texture2D)]
        [FolderPath("Assets/UI/Icons/Characters/", SearchPattern = "Icon_*.png")]
        public string IconPath { get; set; }
        
        // Attack sound effect
        [AssetType(UnityAssetTypes.AudioClip)]
        [FolderPath("Assets/Audio/Characters/", IncludeSubfolders = true)]
        public string AttackSoundPath { get; set; }
    }
}
