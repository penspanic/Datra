#nullable enable
using System;
using System.Threading.Tasks;
using Datra.Interfaces;
using Datra.Lab;
using Datra.WebEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Datra.WebEditor.Server;

/// <summary>
/// DI extension methods for hosting the balance lab (Datra.Lab) next to the web editor.
/// </summary>
public static class DatraLabServiceCollectionExtensions
{
    /// <summary>
    /// Register the lab for one data context and one simulator. The container must be able to
    /// resolve an <see cref="IRawDataProvider"/>: the lab reads the baseline data through it
    /// and never writes to it.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddDatraLab&lt;GameDataContext, GameSim&gt;(lab =&gt; lab.SnapshotDirectory("lab/snapshots"));
    /// app.MapDatraLab();
    /// </code>
    /// </example>
    public static IServiceCollection AddDatraLab<TContext, TSimulator>(
        this IServiceCollection services,
        Action<LabOptions<TContext>>? configure = null)
        where TContext : class, IDataContext
        where TSimulator : class, ISimulator<TContext>
        => services.AddDatraLab<TContext, TSimulator>((_, options) => configure?.Invoke(options));

    /// <summary>
    /// As <see cref="AddDatraLab{TContext,TSimulator}(IServiceCollection, Action{LabOptions{TContext}}?)"/>,
    /// for options that need other services (stored physics outcomes, a configured path).
    /// </summary>
    public static IServiceCollection AddDatraLab<TContext, TSimulator>(
        this IServiceCollection services,
        Action<IServiceProvider, LabOptions<TContext>> configure)
        where TContext : class, IDataContext
        where TSimulator : class, ISimulator<TContext>
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        // One simulator instance plays every bot, on several threads: it has to be stateless.
        services.TryAddSingleton<TSimulator>();

        services.AddSingleton<ILabEngine>(provider =>
        {
            var source = provider.GetService<IRawDataProvider>()
                ?? throw new InvalidOperationException(
                    "AddDatraLab needs an IRawDataProvider in the container to read the baseline data from. " +
                    "Register the provider your data context is built on, or construct LabEngine<TContext> " +
                    "yourself and register it as ILabEngine.");

            var options = new LabOptions<TContext>();
            configure(provider, options);

            var engine = new LabEngine<TContext>(source, provider.GetRequiredService<TSimulator>(), options);

            // The lab caches the baseline files. When the table editor saves or reloads, the
            // baseline is whatever is on disk now.
            if (provider.GetService<IDataChangedNotifier>() is { } notifier)
            {
                notifier.Changed += _ =>
                {
                    engine.InvalidateBaseline();
                    return Task.CompletedTask;
                };
            }

            return engine;
        });

        return services;
    }
}
