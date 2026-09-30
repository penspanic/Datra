#nullable enable
using Datra.Lab.Sample.Generated;

namespace Datra.Lab.Sample
{
    /// <summary>The toy game's lab setup, shared by the web sample and the tests.</summary>
    public static class DescentLab
    {
        /// <summary>
        /// Policies, the policy parameter, the curve knob, the checks: everything the game adds
        /// on top of its <c>[Knob]</c> attributes and its simulator.
        /// </summary>
        public static void Configure(LabOptions<DescentContext> lab)
        {
            lab.Title = "Descent · balance lab";
            lab.Bots = 40;

            lab.Policy(DescentSim.Efficient, "Efficient", "Buys what gets it down soonest, saves otherwise");
            lab.Policy(DescentSim.Cheap, "Cheapest first", "Buys whatever it can afford");
            lab.Policy(DescentSim.RandomPick, "Random", "Picks among the upgrades that pay back");
            lab.PolicyParam("shopSeconds", "Time between runs", @default: 8, min: 2, max: 30, step: 1, unit: "s");

            // One knob over a whole column: an attribute cannot say "re-slope these five rows".
            lab.Knobs(knobs => knobs.Add(
                "Depth value growth",
                read: DescentData.ValueGrowth,
                write: DescentData.SetValueGrowth,
                min: 1.4, max: 3, step: 0.05,
                group: "Floors", unit: "×/floor",
                hint: "How much more each floor pays than the one above",
                id: "Floors.ValueGrowth"));

            lab.Check("First purchase", bot => bot.FirstPurchaseSeconds, "<= 60", unit: "s");
            lab.Check("Longest run", bot => bot.LongestTickSeconds("run"), "<= 90", unit: "s");
        }
    }
}
