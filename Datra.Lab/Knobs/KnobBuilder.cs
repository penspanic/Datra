#nullable enable
using System;
using System.Collections.Generic;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// A group of knobs declared in code, for values an attribute cannot express: one knob that
    /// rewrites several fields (a fare curve, a price multiplier over a whole table).
    /// </summary>
    public interface IKnobSet<TContext> where TContext : class, IDataContext
    {
        void Declare(KnobBuilder<TContext> knobs);
    }

    /// <summary>Collects knobs declared in code. See <see cref="IKnobSet{TContext}"/>.</summary>
    public sealed class KnobBuilder<TContext> where TContext : class, IDataContext
    {
        internal List<DeclaredKnob<TContext>> Knobs { get; } = new List<DeclaredKnob<TContext>>();

        /// <summary>
        /// Declare a knob by how to read its value from a context and how to write one back.
        /// </summary>
        /// <param name="label">Name shown on the knob.</param>
        /// <param name="read">The knob's value in the given data. Called once on the baseline.</param>
        /// <param name="write">Applies a value to a forked context. Never called on the baseline.</param>
        /// <param name="id">Stable id. Defaults to a slug of <paramref name="label"/>.</param>
        public KnobBuilder<TContext> Add(
            string label,
            Func<TContext, double> read,
            Action<TContext, double> write,
            double min,
            double max,
            double step = 0,
            string group = "",
            string unit = "",
            string hint = "",
            KnobLayer layer = KnobLayer.Economy,
            string? id = null)
        {
            if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("A knob needs a label.", nameof(label));
            if (read is null) throw new ArgumentNullException(nameof(read));
            if (write is null) throw new ArgumentNullException(nameof(write));
            if (!(max > min)) throw new ArgumentException($"Knob '{label}': max must be greater than min.");

            Knobs.Add(new DeclaredKnob<TContext>(
                id ?? "knob:" + Slug(label), label, group, unit, hint, min, max, step, layer, read, write));
            return this;
        }

        private static string Slug(string label)
        {
            var chars = new char[label.Length];
            for (var i = 0; i < label.Length; i++)
                chars[i] = char.IsLetterOrDigit(label[i]) ? char.ToLowerInvariant(label[i]) : '-';
            return new string(chars).Trim('-');
        }
    }

    internal sealed class DeclaredKnob<TContext> where TContext : class, IDataContext
    {
        public DeclaredKnob(string id, string label, string group, string unit, string hint,
            double min, double max, double step, KnobLayer layer,
            Func<TContext, double> read, Action<TContext, double> write)
        {
            Id = id; Label = label; Group = group; Unit = unit; Hint = hint;
            Min = min; Max = max; Step = step; Layer = layer; Read = read; Write = write;
        }

        public string Id { get; }
        public string Label { get; }
        public string Group { get; }
        public string Unit { get; }
        public string Hint { get; }
        public double Min { get; }
        public double Max { get; }
        public double Step { get; }
        public KnobLayer Layer { get; }
        public Func<TContext, double> Read { get; }
        public Action<TContext, double> Write { get; }
    }
}
