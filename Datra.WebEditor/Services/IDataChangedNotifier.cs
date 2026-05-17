#nullable enable
using System;
using System.Threading.Tasks;

namespace Datra.WebEditor.Services;

/// <summary>
/// What kind of change just occurred.
/// </summary>
public enum DataChangeKind
{
    /// <summary>The data was saved to its persistent store.</summary>
    Saved,
    /// <summary>The data was reloaded from its persistent store, discarding pending edits.</summary>
    Reloaded,
}

/// <summary>
/// Event payload describing a save/reload that just completed.
/// </summary>
/// <param name="DataType">The data type whose repository changed. Equal to <see cref="System.Type"/>
/// values reported by <see cref="Datra.Interfaces.IDataContext.GetDataTypeInfos"/>.</param>
/// <param name="Kind">Whether the change was a save or a reload.</param>
public sealed record DataChangedEvent(Type DataType, DataChangeKind Kind);

/// <summary>
/// Pub/sub hook for editor save/reload events. Consumers (e.g. a host that runs a live simulation
/// off the same files) subscribe and trigger their own hot-reload pipeline.
/// </summary>
/// <remarks>
/// Disk writes are still the canonical signal — a consumer that relies on a file watcher will
/// also see the change. This notifier is sugar for callers that want a deterministic in-process
/// handoff without polling.
/// </remarks>
public interface IDataChangedNotifier
{
    /// <summary>Fires after every successful save or reload. Handlers may be async.</summary>
    event Func<DataChangedEvent, Task>? Changed;

    /// <summary>Internal — fired by the host service. Public so server endpoints can publish too.</summary>
    Task NotifyAsync(DataChangedEvent evt);
}

/// <summary>
/// Default implementation. Thread-safe over the handler list via copy-on-iterate.
/// </summary>
public sealed class DataChangedNotifier : IDataChangedNotifier
{
    public event Func<DataChangedEvent, Task>? Changed;

    public async Task NotifyAsync(DataChangedEvent evt)
    {
        var handlers = Changed;
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList())
        {
            await ((Func<DataChangedEvent, Task>)handler).Invoke(evt).ConfigureAwait(false);
        }
    }
}
