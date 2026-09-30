#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.Lab
{
    /// <summary>
    /// What a simulator writes to while one bot plays: phases, ticks, purchases and marks. The
    /// views are drawn from these records alone, so they need no knowledge of the game.
    /// </summary>
    public sealed class SimRun
    {
        private readonly RunSettings _settings;
        private readonly IOutcomeReader? _outcomes;
        private readonly BotRecord _record;
        private double _clock;
        private int _records;

        internal SimRun(int bot, int seed, RunSettings settings, IOutcomeReader? outcomes)
        {
            _settings = settings;
            _outcomes = outcomes;
            _record = new BotRecord(bot, seed);
            Seed = seed;
            Random = new SimRandom(seed);
        }

        internal BotRecord Record => _record;

        /// <summary>This bot's seed. The same seed and data must produce the same records.</summary>
        public int Seed { get; }

        /// <summary>The bot's index in the batch.</summary>
        public int Bot => _record.Bot;

        /// <summary>Runtime-independent random numbers, seeded from <see cref="Seed"/>.</summary>
        public SimRandom Random { get; }

        /// <summary>The policy chosen for this scenario.</summary>
        public string Policy => _settings.Policy;

        /// <summary>A policy parameter, or <paramref name="fallback"/> when the scenario does not set it.</summary>
        public double Param(string name, double fallback = 0) =>
            _settings.PolicyParams.TryGetValue(name, out var value) ? value : fallback;

        /// <summary>Game time so far, in seconds.</summary>
        public double Elapsed => _clock;

        /// <summary>
        /// The bot has played past the time cap. A simulator loops on <c>!run.OutOfTime</c> so a
        /// scenario that can never finish (zero earnings) still returns.
        /// </summary>
        public bool OutOfTime => _clock >= _settings.MaxSimSeconds;

        /// <summary>
        /// Enter a phase (a floor, a tier). The previous phase ends now. Entering the phase the
        /// bot is already in does nothing.
        /// </summary>
        /// <param name="targetSeconds">How long the phase is meant to take, for the target line.</param>
        public void Stage(string id, double? targetSeconds = null, string? label = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A stage needs an id.", nameof(id));
            var stages = _record.StageList;
            if (stages.Count > 0)
            {
                var current = stages[stages.Count - 1];
                if (string.Equals(current.Id, id, StringComparison.Ordinal)) return;
                current.EndSeconds = _clock;
            }

            Count();
            stages.Add(new StageRecord(id, label ?? id, targetSeconds, _clock));
            _record.ProgressList.Add(new ProgressPoint(_clock, stages.Count - 1, 0));
        }

        /// <summary>
        /// Report how far through the current phase the bot is, 0 to 1. Call it whenever that
        /// changes — after earning and after spending, if spending sets the bot back.
        /// </summary>
        public void Progress(double fraction)
        {
            if (_record.StageList.Count == 0) return;
            if (double.IsNaN(fraction)) fraction = 0;
            Count();
            _record.ProgressList.Add(new ProgressPoint(
                _clock, _record.StageList.Count - 1, Math.Max(0, Math.Min(1, fraction))));
        }

        /// <summary>Advance game time by one slice of play.</summary>
        /// <param name="seconds">Length of the slice.</param>
        /// <param name="earned">What the bot earned in it.</param>
        /// <param name="kind">What the slice was: <c>"run"</c>, <c>"shop"</c>, a wait.</param>
        public void Tick(double seconds, double earned = 0, string kind = "run")
        {
            if (!(seconds >= 0)) throw new ArgumentOutOfRangeException(nameof(seconds), "A tick cannot run backwards.");
            Count();
            _record.TickList.Add(new TickRecord(_clock, seconds, earned, kind ?? "run", _record.StageList.Count - 1));
            _clock += seconds;
        }

        /// <summary>The bot bought something at the current game time.</summary>
        /// <param name="group">Lane on the purchase timeline (account, floor device).</param>
        public void Purchase(string id, double cost, string? group = null, string? label = null)
        {
            Count();
            _record.PurchaseList.Add(new PurchaseRecord(
                _clock, id, label ?? id, cost, group ?? string.Empty, _record.StageList.Count - 1));
        }

        /// <summary>Note a moment worth finding later: first intact bottle, first refill chain.</summary>
        public void Mark(string name, double value = 1)
        {
            Count();
            _record.MarkList.Add(new MarkRecord(_clock, name, value, _record.StageList.Count - 1));
        }

        /// <summary>A reason only the game knows, attached to a phase ("bottles stop on the landing").</summary>
        public void Explain(string stage, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _record.ExplainList.Add(new KeyValuePair<string, string>(stage ?? string.Empty, text));
        }

        /// <summary>
        /// Draw one stored physics outcome for <paramref name="key"/>, using this bot's random numbers.
        /// </summary>
        /// <exception cref="MissingOutcomeException">Nothing is stored for the key.</exception>
        public T Outcome<T>(PhysicsKey key)
        {
            if (TryOutcome<T>(key, out var sample)) return sample;
            throw new MissingOutcomeException(key);
        }

        /// <summary>
        /// Like <see cref="Outcome{T}"/>, but returns false instead of throwing so the simulator
        /// can fall back to an estimate. The missing key is reported with the result either way.
        /// </summary>
        public bool TryOutcome<T>(PhysicsKey key, out T sample)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            var pool = _outcomes?.Pool<T>(key);
            if (pool is null || pool.Count == 0)
            {
                if (!_record.MissingList.Contains(key.Text)) _record.MissingList.Add(key.Text);
                sample = default!;
                return false;
            }

            sample = pool[Random.Next(pool.Count)];
            return true;
        }

        internal BotRecord Complete(Exception? error)
        {
            _record.TotalSeconds = _clock;
            _record.Error = error?.Message;
            _record.Finished = error is null && !OutOfTime;
            if (_record.Finished && _record.StageList.Count > 0)
                _record.StageList[_record.StageList.Count - 1].EndSeconds = _clock;
            return _record;
        }

        private void Count()
        {
            if (++_records > _settings.MaxRecords)
            {
                throw new InvalidOperationException(
                    $"The simulator wrote more than {_settings.MaxRecords:N0} records for one bot. " +
                    "It is probably looping without advancing time; check run.OutOfTime in its loops.");
            }
        }
    }
}
