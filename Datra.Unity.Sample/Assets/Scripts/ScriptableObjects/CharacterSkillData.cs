using UnityEngine;

namespace Datra.Unity.Sample
{
    /// <summary>
    /// Example ScriptableObject for character skills
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterSkill", menuName = "Datra Sample/Character Skill", order = 2)]
    public class CharacterSkillData : ScriptableObject
    {
        [Header("Skill Info")]
        public string skillName;
        public string description;
        public Sprite skillIcon;
        
        [Header("Properties")]
        public int skillLevel = 1;
        public float cooldown = 5.0f;
        public int manaCost = 10;
        
        [Header("Effects")]
        public GameObject skillEffectPrefab;
        public AudioClip castSound;
        
        [Header("Damage")]
        public int baseDamage = 50;
        public float damageMultiplier = 1.5f;
        public DamageType damageType;
        
        public enum DamageType
        {
            Physical,
            Magical,
            Fire,
            Ice,
            Lightning
        }
    }
}