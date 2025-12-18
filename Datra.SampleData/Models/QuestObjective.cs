using Datra.Attributes;
using Datra.Localization;
using Newtonsoft.Json;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Base class for quest objectives - demonstrates polymorphic JSON support
    /// and NestedLocaleRef for hierarchical locale keys.
    /// </summary>
    [JsonObject]
    public abstract class QuestObjective
    {
        public string Id { get; set; }

        /// <summary>
        /// Description text from the data file (fallback/debug).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Nested locale reference for objective description.
        /// The actual locale key is evaluated at runtime with indices:
        /// e.g., "QuestData.quest_001.Objectives#0.Description"
        /// </summary>
        [NestedLocale]
        [JsonIgnore]
        public NestedLocaleRef DescriptionLocale => NestedLocaleRef.Create("Objectives", "Description");

        public bool IsCompleted { get; set; }
    }

    /// <summary>
    /// Kill a specific number of enemies
    /// </summary>
    public class KillObjective : QuestObjective
    {
        public string TargetEnemyId { get; set; }
        public int RequiredCount { get; set; }
        public int CurrentCount { get; set; }
    }

    /// <summary>
    /// Collect specific items
    /// </summary>
    public class CollectObjective : QuestObjective
    {
        public int TargetItemId { get; set; }
        public int RequiredAmount { get; set; }
        public int CurrentAmount { get; set; }
    }

    /// <summary>
    /// Talk to an NPC
    /// </summary>
    public class TalkObjective : QuestObjective
    {
        public string TargetNpcId { get; set; }
        public string[] DialogueKeys { get; set; }
    }

    /// <summary>
    /// Reach a specific location
    /// </summary>
    public class LocationObjective : QuestObjective
    {
        public string LocationId { get; set; }
        public float Radius { get; set; }
    }
}
