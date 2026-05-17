#nullable enable
using System;
using Datra.Interfaces;
using Datra.WebEditor.Handlers;
using Datra.WebEditor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Datra.WebEditor.Extensions;

/// <summary>
/// DI extension methods for installing Datra.WebEditor.
/// </summary>
public static class DatraWebEditorServiceCollectionExtensions
{
    /// <summary>
    /// Register the Blazor editor — field handler registry, host service, and change notifier.
    /// The consumer must already have registered their <see cref="IDataContext"/> implementation
    /// in the same container.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddSingleton&lt;MyDataContext&gt;();
    /// services.AddDatraWebEditor(opt =&gt; opt.DataContextType = typeof(MyDataContext));
    /// </code>
    /// </example>
    public static IServiceCollection AddDatraWebEditor(
        this IServiceCollection services,
        Action<DatraWebEditorOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        var options = new DatraWebEditorOptions();
        configure(options);

        if (options.DataContextType is null)
            throw new InvalidOperationException(
                "DatraWebEditorOptions.DataContextType is required. " +
                "Set it to the CLR type of your IDataContext implementation.");

        if (!typeof(IDataContext).IsAssignableFrom(options.DataContextType))
            throw new InvalidOperationException(
                $"{options.DataContextType.FullName} does not implement IDataContext.");

        services.AddSingleton(options);

        // BlazorFieldTypeRegistry is stateless once configured — Singleton is fine for any
        // hosting model (WASM, Blazor Server, hybrid).
        services.AddSingleton<BlazorFieldTypeRegistry>(_ =>
        {
            var registry = new BlazorFieldTypeRegistry();
            if (options.RegisterDefaultHandlers) registry.RegisterDefaultHandlers();
            return registry;
        });

        // HostService + Bootstrapper are Scoped so each editing session (Blazor circuit /
        // WASM lifetime) has its own dirty-tracking state. In server-mode that prevents
        // user A's pending edits leaking into user B's view. The consumer's DataContext is
        // typically Scoped too — Singleton bootstrapper would trip the DI validator on
        // GetRequiredService<DataContext>.
        services.AddScoped<DatraEditorHostService>();
        services.AddScoped<DatraEditorBootstrapper>();

        // The notifier is the pub/sub seam — keep it process-wide so external triggers
        // (CLI reload, file watcher) reach every active session.
        services.AddSingleton<IDataChangedNotifier, DataChangedNotifier>();

        return services;
    }
}

/// <summary>
/// Internal helper that resolves the bound <see cref="IDataContext"/> on first access and kicks
/// the host service through its initialisation step. Consumers call <see cref="EnsureInitialisedAsync"/>
/// during app startup or before the first editor render.
/// </summary>
public sealed class DatraEditorBootstrapper
{
    private readonly IServiceProvider _services;
    private readonly DatraWebEditorOptions _options;
    private readonly DatraEditorHostService _host;
    private readonly SemaphoreSlimLite _gate = new();
    private bool _ready;

    public DatraEditorBootstrapper(
        IServiceProvider services,
        DatraWebEditorOptions options,
        DatraEditorHostService host)
    {
        _services = services;
        _options = options;
        _host = host;
    }

    public async System.Threading.Tasks.Task EnsureInitialisedAsync()
    {
        if (_ready) return;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_ready) return;
            var context = (IDataContext)_services.GetRequiredService(_options.DataContextType!);
            await _host.InitializeAsync(context).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Trivial async gate — avoids importing System.Threading.SemaphoreSlim usage details into
    // the public surface. SemaphoreSlim itself is what we use under the hood.
    private sealed class SemaphoreSlimLite
    {
        private readonly System.Threading.SemaphoreSlim _inner = new(1, 1);
        public System.Threading.Tasks.Task WaitAsync() => _inner.WaitAsync();
        public void Release() => _inner.Release();
    }
}
