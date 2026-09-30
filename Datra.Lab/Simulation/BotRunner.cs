#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// Plays a batch of bots through a simulator. Bots run in parallel, but each has its own
    /// seed and its own <see cref="SimRun"/>, and the records come back in bot order, so the
    /// result does not depend on scheduling.
    /// </summary>
    public static class BotRunner
    {
        public static IReadOnlyList<BotRecord> Run<TContext>(
            ISimulator<TContext> simulator,
            TContext data,
            RunSettings settings,
            IOutcomeReader? outcomes = null,
            CancellationToken cancellationToken = default)
            where TContext : class, IDataContext
        {
            if (simulator is null) throw new ArgumentNullException(nameof(simulator));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            if (settings.Bots < 1) throw new ArgumentOutOfRangeException(nameof(settings), "At least one bot is needed.");

            var records = new BotRecord[settings.Bots];
            var parallel = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = settings.MaxParallelism > 0 ? settings.MaxParallelism : Environment.ProcessorCount,
            };

            Parallel.For(0, settings.Bots, parallel, bot =>
            {
                records[bot] = Play(simulator, data, settings, bot, outcomes);
            });

            return records;
        }

        /// <summary>
        /// Play one bot of a batch on its own. It gets the seed it would have in the batch, so
        /// its record is the same as the one <see cref="Run{TContext}"/> returns at that index.
        /// </summary>
        public static BotRecord RunOne<TContext>(
            ISimulator<TContext> simulator,
            TContext data,
            RunSettings settings,
            int bot,
            IOutcomeReader? outcomes = null)
            where TContext : class, IDataContext
        {
            if (simulator is null) throw new ArgumentNullException(nameof(simulator));
            if (data is null) throw new ArgumentNullException(nameof(data));
            if (settings is null) throw new ArgumentNullException(nameof(settings));
            if (bot < 0) throw new ArgumentOutOfRangeException(nameof(bot));
            return Play(simulator, data, settings, bot, outcomes);
        }

        private static BotRecord Play<TContext>(
            ISimulator<TContext> simulator, TContext data, RunSettings settings, int bot, IOutcomeReader? outcomes)
            where TContext : class, IDataContext
        {
            var run = new SimRun(bot, SimRandom.Derive(settings.Seed, bot), settings, outcomes);
            Exception? error = null;
            try
            {
                simulator.Play(data, run);
            }
            catch (Exception ex)
            {
                // One bot failing (a missing physics key, a bug in the simulator) is reported
                // with the result; it does not take the other bots down.
                error = ex;
            }
            return run.Complete(error);
        }
    }
}
