using System.Collections.Generic;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Quest data with polymorphic objectives - demonstrates JSON polymorphism support
    /// </summary>
    [TableData("Quests.json", Format = DataFormat.Json)]
    public partial class QuestData : ITableData<string>
    {
        public string Id { get; set; }

        [FixedLocale]
        public LocaleRef Name => LocaleRef.CreateFixed(nameof(QuestData), Id, nameof(Name));

        [FixedLocale]
        public LocaleRef Description => LocaleRef.CreateFixed(nameof(QuestData), Id, nameof(Description));

        public QuestType Type { get; set; }

        public int RequiredLevel { get; set; }

        public int RewardGold { get; set; }

        public int RewardExp { get; set; }

        /// <summary>
        /// Polymorphic list of quest objectives
        /// </summary>
        public List<QuestObjective> Objectives { get; set; } = new List<QuestObjective>();

        /// <summary>
        /// Reward items (item ID to count)
        /// </summary>
        public Dictionary<int, int> RewardItems { get; set; } = new Dictionary<int, int>();

        /// <summary>
        /// Prerequisite quest IDs
        /// </summary>
        public string[] PrerequisiteQuests { get; set; }
    }

    public enum QuestType
    {
        Main,
        Side,
        Daily,
        Event
    }
}
