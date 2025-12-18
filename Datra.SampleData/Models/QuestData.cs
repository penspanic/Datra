using System.Collections.Generic;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Localization;

namespace Datra.SampleData.Models
{
    /// <summary>
    /// Quest data with polymorphic objectives - demonstrates JSON polymorphism support
    /// and ILocaleEvaluator for nested locale resolution.
    /// </summary>
    [TableData("Quests.json", Format = DataFormat.Json)]
    public partial class QuestData : ITableData<string>, ILocaleEvaluator
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

        /// <summary>
        /// Evaluates a nested locale reference to a concrete LocaleRef.
        /// Determines array indices from context objects.
        /// </summary>
        /// <param name="rootId">The root identifier (typically this.Id)</param>
        /// <param name="nested">The nested locale reference with path template</param>
        /// <param name="context">Context objects: [0] = QuestObjective</param>
        /// <returns>A LocaleRef with the fully resolved key</returns>
        /// <example>
        /// var quest = context.Quest["quest_001"];
        /// var objective = quest.Objectives[2];
        /// var localeRef = objective.DescriptionLocale.Evaluate(quest, quest.Id, objective);
        /// // Result: LocaleRef with key "QuestData.quest_001.Objectives#2.Description"
        /// </example>
        public LocaleRef EvaluateNestedLocale(object rootId, NestedLocaleRef nested, params object[] context)
        {
            var prefix = $"{nameof(QuestData)}.{rootId}";

            if (context.Length > 0 && context[0] is QuestObjective objective)
            {
                var objectiveIndex = Objectives.IndexOf(objective);
                if (objectiveIndex >= 0)
                {
                    // Use the optimized single-index evaluation
                    return nested.Evaluate(prefix, "Objectives", objectiveIndex);
                }
            }

            // Fallback: evaluate without indices
            return nested.EvaluateNoCache(prefix);
        }

        /// <summary>
        /// Gets the localized description for a specific objective.
        /// </summary>
        /// <param name="objective">The objective to get description for</param>
        /// <returns>A LocaleRef that can be used to get the localized text</returns>
        public LocaleRef GetObjectiveDescription(QuestObjective objective)
        {
            return objective.DescriptionLocale.Evaluate(this, Id, objective);
        }
    }

    public enum QuestType
    {
        Main,
        Side,
        Daily,
        Event
    }
}
