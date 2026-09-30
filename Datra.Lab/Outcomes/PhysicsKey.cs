#nullable enable
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Datra.Lab
{
    /// <summary>
    /// Identifies one physics state: the inputs that decide how a run plays out, as sorted
    /// <c>name=value</c> pairs. Datra treats it as an opaque string; what goes in is the game's.
    /// </summary>
    public sealed class PhysicsKey : IEquatable<PhysicsKey>
    {
        private PhysicsKey(string text)
        {
            Text = text;
        }

        /// <summary>Canonical form, e.g. <c>floor=B3;push=2</c>.</summary>
        public string Text { get; }

        /// <summary>
        /// Build a key from named inputs. Order does not matter; numbers are written
        /// culture-invariantly so the same inputs always give the same key.
        /// </summary>
        public static PhysicsKey Of(params (string Name, object? Value)[] parts)
        {
            if (parts is null) throw new ArgumentNullException(nameof(parts));
            var pairs = parts
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => p.Name + "=" + Format(p.Value));
            return new PhysicsKey(string.Join(";", pairs));
        }

        /// <summary>A key from text already in canonical form (read back from a cache index).</summary>
        public static PhysicsKey Parse(string text) => new PhysicsKey(text ?? throw new ArgumentNullException(nameof(text)));

        /// <summary>
        /// Short hex digest of the key together with the outcome source version. Used as a
        /// file name by <see cref="JsonlOutcomeReader"/>.
        /// </summary>
        public string Hash(string version = "")
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(version + "|" + Text));
            var hex = new StringBuilder(16);
            for (var i = 0; i < 8; i++) hex.Append(bytes[i].ToString("x2"));
            return hex.ToString();
        }

        private static string Format(object? value) => value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

        public bool Equals(PhysicsKey? other) => other != null && string.Equals(Text, other.Text, StringComparison.Ordinal);
        public override bool Equals(object? obj) => Equals(obj as PhysicsKey);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Text);
        public override string ToString() => Text;
    }
}
