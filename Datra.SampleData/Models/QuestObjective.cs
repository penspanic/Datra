using Newtonsoft.Json;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Base class for quest objectives - demonstrates polymorphic JSON support
    /// </summary>
    [JsonObject]
    public abstract class QuestObjective
    {
        public string Id { get; set; }
        public string Description { get; set; }
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
