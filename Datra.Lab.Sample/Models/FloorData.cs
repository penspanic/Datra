using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab.Sample.Models
{
    /// <summary>
    /// One floor of the toy game. The player fills the fare box to open the shutter and go down.
    /// </summary>
    [TableData("Floors.yaml", Format = DataFormat.Yaml)]
    public partial class FloorData : ITableData<string>
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Coins the fare box must hold before the shutter opens.</summary>
        [Knob("Fare", Group = "Floors", Unit = "coins", EachRow = true,
            Hint = "Coins the fare box must hold before the shutter opens")]
        public int Fare { get; set; }

        /// <summary>Everything earned on this floor is multiplied by this.</summary>
        public float ValueMultiplier { get; set; } = 1f;

        /// <summary>How long the floor is meant to take.</summary>
        public float TargetMinutes { get; set; }

        /// <summary>Number of steps on the staircase.</summary>
        public int Steps { get; set; }

        /// <summary>The step that is a landing, where bottles tend to stop. Zero for none.</summary>
        public int LandingStep { get; set; }
    }
}
