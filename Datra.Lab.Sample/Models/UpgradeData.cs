using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab.Sample.Models
{
    public enum UpgradeScope
    {
        /// <summary>Kept for the whole game.</summary>
        Account,

        /// <summary>A device installed on one floor: bought again on every floor.</summary>
        Floor
    }

    /// <summary>Something the bot can buy with coins from the fare box.</summary>
    [TableData("Upgrades.yaml", Format = DataFormat.Yaml)]
    public partial class UpgradeData : ITableData<string>
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public UpgradeScope Scope { get; set; }

        /// <summary>Price of the first level.</summary>
        [Knob("Upgrade prices", Group = "Upgrades", Unit = "×", Min = 0.25, Max = 3, Step = 0.05, Apply = KnobApply.Scale,
            Hint = "Multiplies the first-level price of every upgrade")]
        public float BaseCost { get; set; }

        /// <summary>Each level costs this many times the one before.</summary>
        public float CostGrowth { get; set; } = 1.5f;

        public int MaxLevel { get; set; } = 5;

        /// <summary>
        /// Gain per level, as a fraction. For the economy upgrades this is the rule the game
        /// applies; for the physics ones it is only what the bot expects when it shops.
        /// </summary>
        [Knob("Carpet value per level", Group = "Upgrades", Unit = "×/level", Min = 0, Max = 1, Step = 0.01, Row = "carpet",
            Hint = "Extra step value per carpet level, on the floor it is laid on")]
        [Knob("Push: steps per level", Group = "Upgrades", Unit = "steps/level", Min = 0, Max = 1, Step = 0.01, Row = "push",
            Layer = KnobLayer.Physics, Hint = "How much further a harder push carries a bottle")]
        public float Amount { get; set; }
    }
}
