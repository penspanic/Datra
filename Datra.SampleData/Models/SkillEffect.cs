using System.Text.Json.Serialization;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Base class for skill effects - demonstrates YAML polymorphism support
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(DamageEffect), typeDiscriminator: "Datra.SampleData.Models.DamageEffect")]
    [JsonDerivedType(typeof(HealEffect), typeDiscriminator: "Datra.SampleData.Models.HealEffect")]
    [JsonDerivedType(typeof(BuffEffect), typeDiscriminator: "Datra.SampleData.Models.BuffEffect")]
    [JsonDerivedType(typeof(SummonEffect), typeDiscriminator: "Datra.SampleData.Models.SummonEffect")]
    [JsonDerivedType(typeof(CrowdControlEffect), typeDiscriminator: "Datra.SampleData.Models.CrowdControlEffect")]
    public abstract class SkillEffect
    {
        public string Id { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Duration { get; set; }
    }

    /// <summary>
    /// Deals damage to target
    /// </summary>
    public class DamageEffect : SkillEffect
    {
        public int BaseDamage { get; set; }
        public float DamageMultiplier { get; set; } = 1.0f;
        public SkillDamageType DamageType { get; set; } = SkillDamageType.Physical;
        public bool IgnoreDefense { get; set; }
    }

    /// <summary>
    /// Heals the target
    /// </summary>
    public class HealEffect : SkillEffect
    {
        public int BaseHeal { get; set; }
        public float HealMultiplier { get; set; } = 1.0f;
        public bool IsPercentage { get; set; }
    }

    /// <summary>
    /// Applies a buff/debuff to target
    /// </summary>
    public class BuffEffect : SkillEffect
    {
        public string BuffId { get; set; } = string.Empty;
        public int Stacks { get; set; } = 1;
        public SkillStatType AffectedStat { get; set; }
        public float StatModifier { get; set; }
        public bool IsDebuff { get; set; }
    }

    /// <summary>
    /// Summons an entity
    /// </summary>
    public class SummonEffect : SkillEffect
    {
        public string SummonId { get; set; } = string.Empty;
        public int Count { get; set; } = 1;
        public float SummonDuration { get; set; }
        public bool InheritStats { get; set; }
    }

    /// <summary>
    /// Applies crowd control effect
    /// </summary>
    public class CrowdControlEffect : SkillEffect
    {
        public CrowdControlType ControlType { get; set; }
        public float Chance { get; set; } = 1.0f;
        public bool BreakOnDamage { get; set; }
    }

    public enum SkillDamageType
    {
        Physical,
        Magical,
        Pure,
        Fire,
        Ice,
        Lightning
    }

    public enum SkillStatType
    {
        Health,
        Mana,
        Attack,
        Defense,
        Speed,
        CritRate,
        CritDamage
    }

    public enum CrowdControlType
    {
        Stun,
        Silence,
        Root,
        Slow,
        Blind,
        Fear
    }
}
