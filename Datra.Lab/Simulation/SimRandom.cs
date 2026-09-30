#nullable enable
using System;
using System.Collections.Generic;

namespace Datra.Lab
{
    /// <summary>
    /// SplitMix64. The lab's only source of randomness: the same seed gives the same sequence
    /// on every runtime and platform, which <see cref="System.Random"/> does not promise.
    /// </summary>
    public sealed class SimRandom
    {
        private ulong _state;

        public SimRandom(long seed)
        {
            _state = unchecked((ulong)seed);
        }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                var z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);

        /// <summary>Uniform integer in [0, <paramref name="exclusiveMax"/>).</summary>
        public int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextDouble() * exclusiveMax);
        }

        /// <summary>Uniform in [<paramref name="min"/>, <paramref name="max"/>).</summary>
        public double Range(double min, double max) => min + (max - min) * NextDouble();

        public bool Chance(double probability) => NextDouble() < probability;

        public T Pick<T>(IReadOnlyList<T> items)
        {
            if (items is null || items.Count == 0) throw new ArgumentException("Nothing to pick from.", nameof(items));
            return items[Next(items.Count)];
        }

        /// <summary>The seed of bot <paramref name="index"/> in a run started from <paramref name="seed"/>.</summary>
        public static int Derive(int seed, int index)
        {
            var mixer = new SimRandom(unchecked(((long)seed << 32) ^ (uint)index));
            return (int)(mixer.NextULong() >> 33);
        }
    }
}
