#nullable enable
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Datra.Lab
{
    /// <summary>
    /// A number the game cares about, with the value it should have: "first purchase within a
    /// minute", "no run longer than 90 seconds". Measured per bot; the median is reported.
    /// </summary>
    public sealed class LabCheck
    {
        private static readonly Regex TargetPattern = new Regex(
            @"^\s*(<=|>=|<|>|=)\s*(-?\d+(?:\.\d+)?)\s*$", RegexOptions.CultureInvariant);

        private readonly string _operator;
        private readonly double _bound;

        /// <param name="metric">The value for one bot. Return NaN when the bot has none (it never bought anything).</param>
        /// <param name="target">A comparison against a number: <c>"&lt;= 60"</c>, <c>"&gt; 3"</c>, <c>"= 5"</c>.</param>
        public LabCheck(string name, Func<BotRecord, double> metric, string target, string unit = "")
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A check needs a name.", nameof(name));
            Name = name;
            Metric = metric ?? throw new ArgumentNullException(nameof(metric));
            Unit = unit ?? string.Empty;

            var match = TargetPattern.Match(target ?? string.Empty);
            if (!match.Success)
                throw new ArgumentException($"Check '{name}': target '{target}' is not of the form \"<= 60\".", nameof(target));
            _operator = match.Groups[1].Value;
            _bound = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            Target = _operator + " " + match.Groups[2].Value;
        }

        public string Name { get; }
        public string Unit { get; }
        public string Target { get; }
        public Func<BotRecord, double> Metric { get; }

        public bool IsMet(double value) => _operator switch
        {
            "<=" => value <= _bound,
            ">=" => value >= _bound,
            "<" => value < _bound,
            ">" => value > _bound,
            _ => Math.Abs(value - _bound) < 1e-9,
        };
    }
}
