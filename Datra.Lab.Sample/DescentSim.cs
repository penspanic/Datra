#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datra.Lab.Sample.Generated;
using Datra.Lab.Sample.Models;

namespace Datra.Lab.Sample
{
    /// <summary>
    /// One bot playing the toy game from the top floor to the platform.
    /// </summary>
    /// <remarks>
    /// <para>The rules a balance tool has to get right are all here in a few lines:</para>
    /// <list type="bullet">
    /// <item>The fare box is the wallet. Buying an upgrade takes coins out of the box, so every
    /// purchase pushes the shutter back down.</item>
    /// <item>When the box holds the fare the shutter opens and the bot goes down. Whatever is
    /// in the box stays there: every floor starts with an empty box.</item>
    /// <item>Account upgrades come along. Floor devices stay on the floor they were bought on.</item>
    /// </list>
    /// <para>A real game would call its own earnings and shop code instead of the small
    /// <c>Earn</c> and <c>Cost</c> methods below.</para>
    /// </remarks>
    public sealed class DescentSim : ISimulator<DescentContext>
    {
        public const string Efficient = "efficient";
        public const string Cheap = "cheap";
        public const string RandomPick = "random";

        public void Play(DescentContext data, SimRun run)
        {
            var economy = data.Economy.Current ?? new EconomyData();
            var upgrades = data.Upgrade.LoadedItems.Values.ToList();
            var floors = DescentData.FloorsInOrder(data);
            var shopSeconds = run.Param("shopSeconds", 8);

            var account = upgrades.Where(u => u.Scope == UpgradeScope.Account).ToDictionary(u => u.Id, _ => 0);
            var markedIntact = false;

            for (var f = 0; f < floors.Count && !run.OutOfTime; f++)
            {
                var floor = floors[f];
                run.Stage(floor.Id, targetSeconds: floor.TargetMinutes * 60, label: floor.Id + " " + floor.Name);

                // A new floor: an empty fare box and no devices installed yet.
                var wallet = 0.0;
                var devices = upgrades.Where(u => u.Scope == UpgradeScope.Floor).ToDictionary(u => u.Id, _ => 0);
                int Level(UpgradeData u) => u.Scope == UpgradeScope.Account ? account[u.Id] : devices[u.Id];

                double earnedHere = 0, playedHere = 0;
                int bottlesHere = 0, landedHere = 0;

                while (!run.OutOfTime)
                {
                    // One run: every bottle in the line gets one stored physics outcome.
                    var key = DescentPhysics.Key(floor.Id, account["push"], account["wrap"]);
                    var bottles = economy.BottlesPerRun + account["queue"];
                    double earned = 0, seconds = 0;
                    for (var b = 0; b < bottles; b++)
                    {
                        var outcome = run.Outcome<BottleOutcome>(key);
                        earned += Earn(economy, floor, outcome, devices, upgrades);
                        seconds += outcome.Seconds;
                        bottlesHere++;
                        if (outcome.StoppedOnLanding) landedHere++;
                        if (outcome.Intact && !markedIntact)
                        {
                            markedIntact = true;
                            run.Mark("first intact bottle");
                        }
                    }

                    wallet += earned;
                    earnedHere += earned;
                    playedHere += seconds + shopSeconds;
                    run.Tick(seconds, earned);
                    run.Progress(wallet / floor.Fare);

                    // The shutter opens as soon as the box holds the fare. The coins stay behind.
                    if (wallet >= floor.Fare) break;

                    run.Tick(shopSeconds, 0, "shop");
                    var rate = earnedHere / playedHere;
                    while (Pick(run, upgrades, Level, floor, floors, f, wallet, rate) is { } choice)
                    {
                        var cost = Cost(choice, Level(choice), floor);
                        wallet -= cost;
                        if (choice.Scope == UpgradeScope.Account) account[choice.Id]++;
                        else devices[choice.Id]++;
                        run.Purchase(choice.Id, cost,
                            group: choice.Scope == UpgradeScope.Account ? "Account" : "Floor device",
                            label: choice.Name + " Lv" + Level(choice));
                        run.Progress(wallet / floor.Fare);
                    }
                }

                if (bottlesHere > 0 && landedHere > bottlesHere * 0.25)
                {
                    run.Explain(floor.Id, "More than a quarter of the bottles stop on the landing, which cuts their step count short.");
                }
            }
        }

        /// <summary>The game's pricing rule: what one bottle's outcome is worth.</summary>
        public static double Earn(
            EconomyData economy, FloorData floor, BottleOutcome outcome,
            IReadOnlyDictionary<string, int> devices, IReadOnlyList<UpgradeData> upgrades)
        {
            var carpet = upgrades.First(u => u.Id == "carpet");
            var box = upgrades.First(u => u.Id == "box");
            var steps = outcome.Steps * economy.StepValue * (1 + carpet.Amount * devices["carpet"]);
            var bonus = outcome.Intact ? economy.IntactBonus * (1 + box.Amount * devices["box"]) : 0;
            return (steps + bonus) * floor.ValueMultiplier;
        }

        /// <summary>The game's price rule. Floor devices cost more the deeper the floor.</summary>
        public static double Cost(UpgradeData upgrade, int level, FloorData floor)
        {
            var price = upgrade.BaseCost * Math.Pow(upgrade.CostGrowth, level);
            if (upgrade.Scope == UpgradeScope.Floor) price *= floor.ValueMultiplier;
            return Math.Round(price);
        }

        /// <summary>What the bot buys next, or null when it would rather save for the fare.</summary>
        private static UpgradeData? Pick(
            SimRun run, List<UpgradeData> upgrades, Func<UpgradeData, int> level,
            FloorData floor, IReadOnlyList<FloorData> floors, int floorIndex, double wallet, double rate)
        {
            var affordable = upgrades
                .Where(u => level(u) < u.MaxLevel && Cost(u, level(u), floor) <= wallet)
                .ToList();
            if (affordable.Count == 0) return null;

            switch (run.Policy)
            {
                case Cheap:
                    // Buys whatever is cheapest as soon as it can. Finite: levels run out.
                    return affordable.OrderBy(u => Cost(u, level(u), floor)).First();

                case RandomPick:
                {
                    var worth = affordable.Where(u => PaysBack(u, level(u), floor, floors, floorIndex, wallet, rate)).ToList();
                    return worth.Count > 0 && run.Random.Chance(0.6) ? run.Random.Pick(worth) : null;
                }

                default:
                    // Most extra earnings per coin, among the upgrades that get the bot down sooner.
                    return affordable
                        .Where(u => PaysBack(u, level(u), floor, floors, floorIndex, wallet, rate))
                        .OrderByDescending(u => u.Amount / Cost(u, level(u), floor))
                        .FirstOrDefault();
            }
        }

        /// <summary>
        /// Spending sets the fare box back. It is worth it when the faster earning makes up the
        /// cost before the coins would have been needed: on this floor for a device, over the
        /// rest of the game for an account upgrade.
        /// </summary>
        private static bool PaysBack(
            UpgradeData upgrade, int level, FloorData floor, IReadOnlyList<FloorData> floors, int floorIndex,
            double wallet, double rate)
        {
            if (rate <= 0) return false;
            var cost = Cost(upgrade, level, floor);
            var toGo = floor.Fare - wallet;
            if (upgrade.Scope == UpgradeScope.Account)
            {
                // Later floors pay more per second, so their fares take less time than the coins suggest.
                for (var i = floorIndex + 1; i < floors.Count; i++)
                    toGo += floors[i].Fare * floor.ValueMultiplier / floors[i].ValueMultiplier;
            }

            var without = toGo / rate;
            var with = (toGo + cost) / (rate * (1 + upgrade.Amount));
            return with < without;
        }
    }
}
