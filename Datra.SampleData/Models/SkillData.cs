using System.Collections.Generic;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Skill data with polymorphic effects - demonstrates YAML format with $type polymorphism
    /// </summary>
    [TableData("Skills.yaml", Format = DataFormat.Yaml)]
    public partial class SkillData : ITableData<string>
    {
        public string Id { get; set; } = string.Empty;

        public SkillCategory Type { get; set; }

        public SkillTargetType TargetType { get; set; }

        public int ManaCost { get; set; }

        public float Cooldown { get; set; }

        public int RequiredLevel { get; set; }

        /// <summary>
        /// Polymorphic list of skill effects
        /// </summary>
        public List<SkillEffect> Effects { get; set; } = new List<SkillEffect>();

        /// <summary>
        /// Tags for skill categorization
        /// </summary>
        public string[] Tags { get; set; } = System.Array.Empty<string>();
    }

    public enum SkillCategory
    {
        Active,
        Passive,
        Ultimate
    }

    public enum SkillTargetType
    {
        Self,
        SingleEnemy,
        SingleAlly,
        AllEnemies,
        AllAllies,
        Area
    }
}
