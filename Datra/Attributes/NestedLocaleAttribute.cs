using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Indicates that a locale property uses a nested (hierarchical) key pattern.
    /// The final locale key is determined at runtime based on the object's position
    /// in the parent container's collection.
    /// </summary>
    /// <example>
    /// // On a QuestObjective within Quest.Objectives list:
    /// [NestedLocale]
    /// public NestedLocaleRef Description => NestedLocaleRef.Create("Objectives", "Description");
    /// // Evaluated: "QuestData.quest_001.Objectives#0.Description"
    /// </example>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class NestedLocaleAttribute : Attribute
    {
    }
}
