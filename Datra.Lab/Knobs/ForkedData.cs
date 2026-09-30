#nullable enable
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Datra.Lab
{
    /// <summary>
    /// Writes to data objects of a forked context. Datra's analyzer (DATRA001) rejects plain
    /// assignments to data classes because loaded game data is meant to be read-only; a knob's
    /// <c>write</c> is the one place that has to change it, and only ever on a fork.
    /// </summary>
    public static class ForkedData
    {
        /// <summary>
        /// Set <paramref name="property"/> of <paramref name="target"/> to <paramref name="value"/>.
        /// </summary>
        /// <example><code>ForkedData.Set(floor, f => f.Fare, 1200);</code></example>
        public static void Set<TData, TValue>(TData target, Expression<Func<TData, TValue>> property, TValue value)
            where TData : class
        {
            if (target is null) throw new ArgumentNullException(nameof(target));
            if (property is null) throw new ArgumentNullException(nameof(property));

            if (!(property.Body is MemberExpression member) || !(member.Member is PropertyInfo info) || !info.CanWrite)
                throw new ArgumentException("Expected a writable property, as in f => f.Fare.", nameof(property));

            info.SetValue(target, value);
        }
    }
}
