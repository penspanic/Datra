#nullable enable
using System;
using System.Collections.Generic;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// What a game tells the lab beyond its attributes and its simulator: bot defaults,
    /// policies, knobs declared in code, checks, stored physics outcomes and where snapshots go.
    /// </summary>
    public sealed class LabOptions<TContext> where TContext : class, IDataContext
    {
        internal List<IKnobSet<TContext>> KnobSets { get; } = new List<IKnobSet<TContext>>();
        internal List<LabCheck> Checks { get; } = new List<LabCheck>();
        internal List<PolicyInfo> Policies { get; } = new List<PolicyInfo>();
        internal List<PolicyParamInfo> PolicyParams { get; } = new List<PolicyParamInfo>();

        /// <summary>Shown at the top of the lab screen.</summary>
        public string Title { get; set; } = "Balance lab";

        /// <summary>Default number of bots per run.</summary>
        public int Bots { get; set; } = 40;

        /// <summary>Default base seed.</summary>
        public int Seed { get; set; } = 1;

        /// <summary>Most bots one request may ask for.</summary>
        public int MaxBots { get; set; } = 400;

        /// <summary>Game-time cap per bot, in seconds.</summary>
        public double MaxSimSeconds { get; set; } = 12 * 3600;

        /// <summary>Threads per run. Zero means one per processor.</summary>
        public int MaxParallelism { get; set; }

        /// <summary>Stored physics outcomes the simulator draws from through <see cref="SimRun.Outcome{T}"/>.</summary>
        public IOutcomeReader? Outcomes { get; set; }

        /// <summary>Where snapshots are kept. Null turns snapshots off.</summary>
        public ISnapshotStore? Snapshots { get; set; }

        /// <summary>
        /// Builds a context on a given provider. Leave null to use the generated
        /// <c>(IRawDataProvider, ...optional)</c> constructor.
        /// </summary>
        public Func<IRawDataProvider, TContext>? CreateContext { get; set; }

        /// <summary>Keep snapshots as JSON files in <paramref name="directory"/>.</summary>
        public LabOptions<TContext> SnapshotDirectory(string directory)
        {
            Snapshots = new FileSnapshotStore(directory);
            return this;
        }

        /// <summary>Offer a bot policy. The first one added is the default.</summary>
        public LabOptions<TContext> Policy(string id, string? label = null, string hint = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A policy needs an id.", nameof(id));
            Policies.Add(new PolicyInfo { Id = id, Label = label ?? id, Hint = hint });
            return this;
        }

        /// <summary>
        /// Declare a number the policies are tuned by. The simulator reads it with
        /// <see cref="SimRun.Param"/>.
        /// </summary>
        public LabOptions<TContext> PolicyParam(
            string id, string label, double @default, double min, double max, double step = 0, string unit = "")
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A policy parameter needs an id.", nameof(id));
            if (!(max > min)) throw new ArgumentException($"Policy parameter '{id}': max must be greater than min.");
            PolicyParams.Add(new PolicyParamInfo
            {
                Id = id,
                Label = label,
                Unit = unit,
                Default = @default,
                Min = min,
                Max = max,
                Step = step > 0 ? step : KnobSchemaReader.NiceStep(min, max),
            });
            return this;
        }

        /// <summary>Add knobs that an attribute cannot express.</summary>
        public LabOptions<TContext> Knobs(IKnobSet<TContext> set)
        {
            KnobSets.Add(set ?? throw new ArgumentNullException(nameof(set)));
            return this;
        }

        /// <inheritdoc cref="Knobs(IKnobSet{TContext})"/>
        public LabOptions<TContext> Knobs(Action<KnobBuilder<TContext>> declare)
        {
            if (declare is null) throw new ArgumentNullException(nameof(declare));
            KnobSets.Add(new DelegateKnobSet(declare));
            return this;
        }

        /// <summary>Add a number to watch against a target. See <see cref="LabCheck"/>.</summary>
        public LabOptions<TContext> Check(string name, Func<BotRecord, double> metric, string target, string unit = "")
        {
            Checks.Add(new LabCheck(name, metric, target, unit));
            return this;
        }

        private sealed class DelegateKnobSet : IKnobSet<TContext>
        {
            private readonly Action<KnobBuilder<TContext>> _declare;
            public DelegateKnobSet(Action<KnobBuilder<TContext>> declare) => _declare = declare;
            public void Declare(KnobBuilder<TContext> knobs) => _declare(knobs);
        }
    }
}
