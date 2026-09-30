#nullable enable
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// Makes independent, loaded copies of a data context. A fork is an ordinary
    /// <typeparamref name="TContext"/>, so a simulator reads <c>data.Bottle</c> the way game
    /// code does, but it lives on an <see cref="OverlayRawDataProvider"/>: its objects are its
    /// own and nothing it does reaches the saved data.
    /// </summary>
    public sealed class ContextForker<TContext> where TContext : class, IDataContext
    {
        private readonly RawDataSnapshot _snapshot;
        private readonly Func<IRawDataProvider, TContext> _create;

        /// <param name="source">Where the baseline data is read from. Only ever read.</param>
        /// <param name="create">
        /// Builds a context on a given provider. Defaults to the generated constructor
        /// <c>new TContext(IRawDataProvider, ...optional...)</c>.
        /// </param>
        public ContextForker(IRawDataProvider source, Func<IRawDataProvider, TContext>? create = null)
        {
            _snapshot = new RawDataSnapshot(source ?? throw new ArgumentNullException(nameof(source)));
            _create = create ?? DefaultFactory();
        }

        /// <summary>The cached baseline files. Shared by every fork.</summary>
        public RawDataSnapshot Snapshot => _snapshot;

        /// <summary>A freshly loaded context holding the baseline values.</summary>
        public async Task<TContext> ForkAsync()
        {
            var context = _create(new OverlayRawDataProvider(_snapshot))
                ?? throw new InvalidOperationException("The context factory returned null.");
            await context.LoadAllAsync().ConfigureAwait(false);
            return context;
        }

        /// <summary>Drop the cached baseline so the next fork re-reads the source (after a save or reload).</summary>
        public void Invalidate() => _snapshot.Clear();

        private static Func<IRawDataProvider, TContext> DefaultFactory()
        {
            // The source generator emits (IRawDataProvider, DataSerializerFactory = null,
            // DatraConfigurationValue = null, ISerializationLogger = null). Any constructor whose
            // first parameter takes the provider and whose remaining ones are optional will do.
            var constructor = typeof(TContext)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Select(c => new { Constructor = c, Parameters = c.GetParameters() })
                .Where(c => c.Parameters.Length > 0
                            && c.Parameters[0].ParameterType.IsAssignableFrom(typeof(OverlayRawDataProvider))
                            && c.Parameters.Skip(1).All(p => p.HasDefaultValue))
                .OrderBy(c => c.Parameters.Length)
                .FirstOrDefault();

            if (constructor is null)
            {
                throw new InvalidOperationException(
                    $"{typeof(TContext).Name} has no public constructor of the shape (IRawDataProvider, ...optional). " +
                    "Pass a context factory (LabOptions.CreateContext) instead.");
            }

            return provider =>
            {
                var arguments = constructor.Parameters
                    .Select((p, i) => i == 0 ? (object?)provider : p.DefaultValue)
                    .ToArray();
                return (TContext)constructor.Constructor.Invoke(arguments);
            };
        }
    }
}
