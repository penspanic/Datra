#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datra.Lab.Sample.Generated;
using Datra.Lab.Sample.Models;

namespace Datra.Lab.Sample
{
    /// <summary>What happened to one bottle on the stairs: the unit the physics cache stores.</summary>
    public sealed class BottleOutcome
    {
        /// <summary>Steps the bottle survived.</summary>
        public int Steps { get; set; }

        /// <summary>It reached the bottom in one piece.</summary>
        public bool Intact { get; set; }

        /// <summary>It came to rest on the landing.</summary>
        public bool StoppedOnLanding { get; set; }

        /// <summary>How long the bottle was in play.</summary>
        public double Seconds { get; set; }
    }

    /// <summary>
    /// Stands in for a game's headless physics batch. A real game runs its engine overnight and
    /// stores the outcomes; this fills the same kind of pools from a few lines of arithmetic so
    /// the sample has something to draw from.
    /// </summary>
    public static class DescentPhysics
    {
        public const string Version = "toy-1";
        public const int SamplesPerKey = 64;

        /// <summary>The physics state a bottle is rolled in. Only inputs that change the outcome belong here.</summary>
        public static PhysicsKey Key(string floor, int push, int wrap) =>
            PhysicsKey.Of(("floor", floor), ("push", push), ("wrap", wrap));

        /// <summary>Roll every state a bot can pass through and keep the outcomes.</summary>
        public static InMemoryOutcomes Bake(DescentContext data)
        {
            var outcomes = new InMemoryOutcomes(Version);
            var economy = data.Economy.Current ?? new EconomyData();
            var upgrades = data.Upgrade.LoadedItems;
            var push = upgrades["push"];
            var wrap = upgrades["wrap"];
            var floors = DescentData.FloorsInOrder(data);

            for (var f = 0; f < floors.Count; f++)
            {
                for (var p = 0; p <= push.MaxLevel; p++)
                {
                    for (var w = 0; w <= wrap.MaxLevel; w++)
                    {
                        // One fixed seed per state: baking twice gives the same pools.
                        var random = new SimRandom(1_000_003L * (f + 1) + 1_009L * p + w);
                        var pool = Enumerable.Range(0, SamplesPerKey)
                            .Select(_ => Roll(floors[f], p * push.Amount, w * wrap.Amount, economy.GlassToughness, random))
                            .ToList();
                        outcomes.Add(Key(floors[f].Id, p, w), pool);
                    }
                }
            }
            return outcomes;
        }

        private static BottleOutcome Roll(FloorData floor, double reach, double padding, double toughness, SimRandom random)
        {
            // Each step is a chance to break. Pushing harder carries the bottle further before
            // it slows down; wrap and tougher glass make every step kinder.
            var breakChance = 0.20 / (toughness * (1 + padding));
            var carry = 3.0 + reach * 4.0;
            var steps = 0;
            var stopped = false;

            while (steps < floor.Steps)
            {
                if (random.Chance(breakChance)) break;
                steps++;
                if (steps == floor.LandingStep && random.Chance(0.55))
                {
                    stopped = true;
                    break;
                }
                if (steps > carry && random.Chance(0.28))
                {
                    stopped = true;
                    break;
                }
            }

            return new BottleOutcome
            {
                Steps = steps,
                Intact = steps >= floor.Steps,
                StoppedOnLanding = stopped && steps == floor.LandingStep && floor.LandingStep > 0,
                Seconds = 3.0 + steps * 0.8,
            };
        }
    }
}
