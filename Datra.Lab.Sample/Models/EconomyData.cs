using Datra.Attributes;

namespace Datra.Lab.Sample.Models
{
    /// <summary>The pricing rules of the toy game.</summary>
    [SingleData("Economy.yaml", Format = DataFormat.Yaml)]
    public partial class EconomyData
    {
        /// <summary>Coins for every step a bottle survives.</summary>
        [Knob("Step value", Group = "Rewards", Unit = "coins/step", Min = 1, Max = 15, Step = 0.5)]
        public float StepValue { get; set; } = 5f;

        /// <summary>Coins for a bottle that reaches the bottom in one piece.</summary>
        [Knob("Intact bonus", Group = "Rewards", Unit = "coins", Min = 0, Max = 200, Step = 5)]
        public float IntactBonus { get; set; } = 40f;

        /// <summary>Bottles in the line at the start of the game.</summary>
        public int BottlesPerRun { get; set; } = 3;

        /// <summary>How easily glass breaks. Decides the stored outcomes, so it is a physics knob.</summary>
        [Knob("Glass toughness", Group = "Rewards", Unit = "×", Min = 0.5, Max = 2, Step = 0.05, Layer = KnobLayer.Physics)]
        public float GlassToughness { get; set; } = 1f;
    }
}
