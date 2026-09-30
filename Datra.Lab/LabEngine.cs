#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// The lab without its generic parameter, for hosts (endpoints, a console tool) that only
    /// pass schemas, scenarios and results around.
    /// </summary>
    public interface ILabEngine
    {
        /// <summary>The knobs and settings of this lab, with baseline values.</summary>
        Task<LabSchema> GetSchemaAsync(CancellationToken cancellationToken = default);

        /// <summary>Lay the scenario over the baseline, play the bots and aggregate.</summary>
        /// <exception cref="ScenarioException">The scenario names an unknown knob or policy, or an invalid value.</exception>
        Task<LabResult> RunAsync(Scenario scenario, CancellationToken cancellationToken = default);

        /// <summary>Run the scenario and store it, with its result, under <paramref name="name"/>.</summary>
        Task<LabSnapshot> SaveSnapshotAsync(string name, Scenario scenario, CancellationToken cancellationToken = default);

        /// <summary>The snapshot store, or null when snapshots are off.</summary>
        ISnapshotStore? Snapshots { get; }

        /// <summary>Forget the cached baseline. Call after the saved data changed.</summary>
        void InvalidateBaseline();
    }

    /// <summary>A scenario the lab cannot run as written. The message is meant for the person who sent it.</summary>
    public sealed class ScenarioException : Exception
    {
        public ScenarioException(string message) : base(message) { }
    }

    /// <summary>
    /// Ties the pieces together for one data context and one simulator: forks the baseline,
    /// applies a scenario's knobs to the fork, plays the bots and aggregates their records.
    /// </summary>
    public sealed class LabEngine<TContext> : ILabEngine where TContext : class, IDataContext
    {
        private readonly ContextForker<TContext> _forker;
        private readonly ISimulator<TContext> _simulator;
        private readonly LabOptions<TContext> _options;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private Baseline? _baseline;

        /// <param name="source">Where the baseline data is read from. The lab never writes to it.</param>
        public LabEngine(IRawDataProvider source, ISimulator<TContext> simulator, LabOptions<TContext>? options = null)
        {
            _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
            _options = options ?? new LabOptions<TContext>();
            _forker = new ContextForker<TContext>(source, _options.CreateContext);
        }

        public ISnapshotStore? Snapshots => _options.Snapshots;

        public void InvalidateBaseline()
        {
            _forker.Invalidate();
            Volatile.Write(ref _baseline, null);
        }

        public async Task<LabSchema> GetSchemaAsync(CancellationToken cancellationToken = default)
        {
            var baseline = await EnsureBaselineAsync(cancellationToken).ConfigureAwait(false);
            var schema = new LabSchema
            {
                Title = _options.Title,
                Context = typeof(TContext).Name,
                BaselineHash = baseline.Hash,
                Knobs = baseline.Knobs.Select(k => k.Info).ToList(),
                Policies = Policies().ToList(),
                PolicyParams = _options.PolicyParams.ToList(),
                Checks = _options.Checks.Select(c => new CheckInfo { Name = c.Name, Unit = c.Unit, Target = c.Target }).ToList(),
                Snapshots = _options.Snapshots != null,
                OutcomeVersion = _options.Outcomes?.Version,
            };
            schema.Defaults = new LabDefaults { Bots = _options.Bots, MaxBots = _options.MaxBots, Seed = _options.Seed, Policy = schema.Policies[0].Id };
            return schema;
        }

        /// <summary>
        /// A loaded context with the scenario's knobs applied: what a simulator is handed. The
        /// caller owns it; nothing done to it reaches the baseline or the saved data.
        /// </summary>
        public async Task<TContext> ForkAsync(Scenario? scenario = null, CancellationToken cancellationToken = default)
        {
            var baseline = await EnsureBaselineAsync(cancellationToken).ConfigureAwait(false);
            var changes = Resolve(baseline, scenario ?? new Scenario());
            return await ForkAsync(changes).ConfigureAwait(false);
        }

        public async Task<LabResult> RunAsync(Scenario scenario, CancellationToken cancellationToken = default)
        {
            if (scenario is null) throw new ArgumentNullException(nameof(scenario));

            var baseline = await EnsureBaselineAsync(cancellationToken).ConfigureAwait(false);
            var changes = Resolve(baseline, scenario);
            var settings = Settings(scenario);

            var watch = Stopwatch.StartNew();
            var data = await ForkAsync(changes).ConfigureAwait(false);

            // The bots are CPU work; keep them off the caller's thread (a request thread, a UI thread).
            var records = await Task.Run(
                () => BotRunner.Run(_simulator, data, settings, _options.Outcomes, cancellationToken),
                cancellationToken).ConfigureAwait(false);

            var result = ResultAggregator.Aggregate(records, _options.Checks);
            watch.Stop();

            result.BaselineHash = baseline.Hash;
            result.OutcomeVersion = _options.Outcomes?.Version;
            result.Scenario = new Scenario
            {
                Name = scenario.Name ?? string.Empty,
                Changes = changes.ToDictionary(c => c.Knob.Info.Id, c => c.Value, StringComparer.Ordinal),
                Bots = settings.Bots,
                Seed = settings.Seed,
                Policy = settings.Policy,
                PolicyParams = new Dictionary<string, double>(settings.PolicyParams, StringComparer.Ordinal),
            };
            result.Timing = new RunTiming
            {
                ElapsedMs = watch.Elapsed.TotalMilliseconds,
                SimulatedSeconds = records.Sum(r => r.TotalSeconds),
            };
            return result;
        }

        public async Task<LabSnapshot> SaveSnapshotAsync(string name, Scenario scenario, CancellationToken cancellationToken = default)
        {
            var store = _options.Snapshots
                ?? throw new InvalidOperationException("Snapshots are off: no snapshot store was configured.");
            if (!FileSnapshotStore.IsValidName(name))
                throw new ScenarioException("A snapshot name is 1-80 letters, digits, spaces or . _ + - and starts with a letter or digit.");

            var result = await RunAsync(scenario, cancellationToken).ConfigureAwait(false);
            result.Scenario.Name = name;
            var snapshot = new LabSnapshot
            {
                Name = name,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                BaselineHash = result.BaselineHash,
                Scenario = result.Scenario,
                Result = result,
            };
            await store.SaveAsync(snapshot).ConfigureAwait(false);
            return snapshot;
        }

        private async Task<TContext> ForkAsync(List<Change> changes)
        {
            var data = await _forker.ForkAsync().ConfigureAwait(false);

            // Knobs declared in code first (they rewrite whole columns), then attribute knobs
            // in the order Set, Scale, Offset so several knobs on one field compose the same
            // way whatever order the scenario lists them in.
            foreach (var change in changes.OrderBy(c => Pass(c.Knob.Info)).ThenBy(c => c.Knob.Order))
                change.Knob.Apply(data, change.Value);
            return data;
        }

        private static int Pass(KnobInfo info)
        {
            if (info.Source is null) return 0;
            return info.Apply switch { KnobApply.Set => 1, KnobApply.Scale => 2, _ => 3 };
        }

        private List<Change> Resolve(Baseline baseline, Scenario scenario)
        {
            var changes = new List<Change>();
            if (scenario.Changes is null) return changes;

            foreach (var pair in scenario.Changes)
            {
                var knob = baseline.Knobs.FirstOrDefault(k => string.Equals(k.Info.Id, pair.Key, StringComparison.Ordinal))
                    ?? throw new ScenarioException($"Unknown knob '{pair.Key}'.");
                if (double.IsNaN(pair.Value) || double.IsInfinity(pair.Value))
                    throw new ScenarioException($"Knob '{pair.Key}' needs a finite value.");

                // A knob sitting on its baseline is not a change; drop it so results compare equal.
                if (Math.Abs(pair.Value - knob.Info.Baseline) < 1e-12) continue;

                if (!knob.Info.Editable)
                {
                    throw new ScenarioException(
                        $"Knob '{pair.Key}' reaches the physics layer. Physics knobs are read-only until stored outcomes can be recomputed.");
                }
                changes.Add(new Change(knob, pair.Value));
            }
            return changes;
        }

        private RunSettings Settings(Scenario scenario)
        {
            var policies = Policies().ToList();
            var policy = scenario.Policy ?? policies[0].Id;
            if (!policies.Any(p => string.Equals(p.Id, policy, StringComparison.Ordinal)))
                throw new ScenarioException($"Unknown policy '{policy}'.");

            var bots = scenario.Bots ?? _options.Bots;
            if (bots < 1 || bots > _options.MaxBots)
                throw new ScenarioException($"Bots must be between 1 and {_options.MaxBots}.");

            var settings = new RunSettings
            {
                Bots = bots,
                Seed = scenario.Seed ?? _options.Seed,
                Policy = policy,
                MaxSimSeconds = _options.MaxSimSeconds,
                MaxParallelism = _options.MaxParallelism,
            };

            foreach (var param in _options.PolicyParams)
                settings.PolicyParams[param.Id] = param.Default;
            if (scenario.PolicyParams != null)
            {
                foreach (var pair in scenario.PolicyParams)
                {
                    if (!settings.PolicyParams.ContainsKey(pair.Key))
                        throw new ScenarioException($"Unknown policy parameter '{pair.Key}'.");
                    if (double.IsNaN(pair.Value) || double.IsInfinity(pair.Value))
                        throw new ScenarioException($"Policy parameter '{pair.Key}' needs a finite value.");
                    settings.PolicyParams[pair.Key] = pair.Value;
                }
            }
            return settings;
        }

        private IEnumerable<PolicyInfo> Policies() =>
            _options.Policies.Count > 0
                ? _options.Policies
                : new List<PolicyInfo> { new PolicyInfo { Id = "default", Label = "Default" } };

        private async Task<Baseline> EnsureBaselineAsync(CancellationToken cancellationToken)
        {
            var baseline = Volatile.Read(ref _baseline);
            if (baseline != null) return baseline;

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                baseline = Volatile.Read(ref _baseline);
                if (baseline != null) return baseline;

                var data = await _forker.ForkAsync().ConfigureAwait(false);
                baseline = new Baseline(
                    KnobSchemaReader.Read(data, _options.KnobSets),
                    _forker.Snapshot.ComputeHash());
                Volatile.Write(ref _baseline, baseline);
                return baseline;
            }
            finally
            {
                _gate.Release();
            }
        }

        private sealed class Baseline
        {
            public Baseline(IReadOnlyList<KnobBinding<TContext>> knobs, string hash)
            {
                Knobs = knobs;
                Hash = hash;
            }

            public IReadOnlyList<KnobBinding<TContext>> Knobs { get; }
            public string Hash { get; }
        }

        private readonly struct Change
        {
            public Change(KnobBinding<TContext> knob, double value)
            {
                Knob = knob;
                Value = value;
            }

            public KnobBinding<TContext> Knob { get; }
            public double Value { get; }
        }
    }
}
