using UnityEngine;

namespace Datra.Unity.Sample
{
    /// <summary>
    /// Example ScriptableObject for weapon configuration
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "Datra Sample/Weapon Config", order = 1)]
    public class WeaponConfigData : ScriptableObject
    {
        [Header("Basic Info")]
        public string weaponName;
        public string description;
        public Sprite icon;
        
        [Header("Stats")]
        public int damage = 10;
        public float attackSpeed = 1.0f;
        public float range = 5.0f;
        public int durability = 100;
        
        [Header("Effects")]
        public GameObject hitEffectPrefab;
        public AudioClip attackSound;
        
        [Header("Requirements")]
        public int requiredLevel = 1;
        public int requiredStrength = 5;
    }
}