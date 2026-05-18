#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Datra.Editor.DataSources;
using Datra.Editor.Interfaces;
using Datra.Interfaces;

namespace Datra.WebEditor.Services;

/// <summary>
/// Owns the live editor session for one <see cref="IDataContext"/>: scans the context via
/// reflection, wraps each <see cref="ITableRepository{TKey,TData}"/> in an
/// <see cref="IEditableDataSource"/>, and proxies save / reload through to the Datra core.
/// </summary>
/// <remarks>
/// <para>The consumer registers exactly one of these per data context (typically a singleton in
/// server-side Blazor or scoped in WASM). The editor UI talks only to this service — never to the
/// individual repositories directly — which keeps lifecycle and dirty-state tracking centralised.</para>
/// <para>Localisation repositories are detected but not yet wrapped here. They stay out of the
/// editor until a dedicated localisation view lands.</para>
/// </remarks>
public sealed class DatraEditorHostService
{
    private readonly IDataChangedNotifier _notifier;
    private readonly Dictionary<Type, EditableEntry> _entries = new();
    private bool _initialized;
    private IDataContext? _dataContext;

    public DatraEditorHostService(IDataChangedNotifier notifier)
    {
        _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
    }

    /// <summary>All editable data types discovered on the bound context, in registration order.</summary>
    public IReadOnlyList<DataTypeInfo> DataTypes { get; private set; } = Array.Empty<DataTypeInfo>();

    public IDataContext DataContext =>
        _dataContext ?? throw new InvalidOperationException(
            "DatraEditorHostService has not been initialised. Call InitializeAsync first.");

    public event Action? StateChanged;

    /// <summary>
    /// Bind the service to a context. Idempotent — calling twice with the same context is a no-op.
    /// </summary>
    public async Task InitializeAsync(IDataContext dataContext)
    {
        if (dataContext is null) throw new ArgumentNullException(nameof(dataContext));

        if (_initialized && ReferenceEquals(_dataContext, dataContext)) return;

        _dataContext = dataContext;
        _entries.Clear();

        await dataContext.LoadAllAsync().ConfigureAwait(false);

        var typeInfos = dataContext.GetDataTypeInfos();
        var properties = dataContext.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var info in typeInfos)
        {
            var property = properties.FirstOrDefault(p => p.Name == info.PropertyName);
            if (property is null) continue;

            var repository = property.GetValue(dataContext);
            if (repository is null) continue;

            var entry = BuildEntry(info, repository);
            if (entry is null) continue;

            entry.DataSource.OnModifiedStateChanged += _ => StateChanged?.Invoke();
            _entries[info.DataType] = entry;

            if (entry.DataSource is not null)
            {
                await entry.DataSource.InitializeAsync().ConfigureAwait(false);
            }
        }

        // Surface only the types we actually wrapped — keeps non-editable repository kinds
        // such as localisation out of the UI until dedicated views land for them.
        DataTypes = typeInfos.Where(info => _entries.ContainsKey(info.DataType)).ToList();
        _initialized = true;
        StateChanged?.Invoke();
    }

    public IEditableDataSource? GetDataSource(Type dataType) =>
        _entries.TryGetValue(dataType, out var entry) ? entry.DataSource : null;

    public IEditableRepository? GetRepository(Type dataType) =>
        _entries.TryGetValue(dataType, out var entry) ? entry.Repository : null;

    public DataTypeInfo? GetDataTypeInfo(Type dataType) =>
        DataTypes.FirstOrDefault(d => d.DataType == dataType);

    public Type? FindDataType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        return DataTypes.FirstOrDefault(d =>
            string.Equals(d.DataType.FullName, typeName, StringComparison.Ordinal)
            || string.Equals(d.DataType.Name, typeName, StringComparison.Ordinal))?.DataType;
    }

    public bool HasUnsavedChanges(Type dataType) =>
        _entries.TryGetValue(dataType, out var entry) && entry.DataSource.HasModifications;

    public bool HasAnyUnsavedChanges() => _entries.Values.Any(e => e.DataSource.HasModifications);

    public IEnumerable<Type> GetModifiedTypes() =>
        _entries.Where(kv => kv.Value.DataSource.HasModifications).Select(kv => kv.Key);

    /// <summary>
    /// Flush pending edits for a single type to its repository's persistent store.
    /// </summary>
    public async Task<bool> SaveAsync(Type dataType)
    {
        if (!_entries.TryGetValue(dataType, out var entry)) return false;
        if (!entry.DataSource.HasModifications) return true;

        await entry.DataSource.SaveAsync().ConfigureAwait(false);
        entry.DataSource.RefreshBaseline();
        await _notifier.NotifyAsync(new DataChangedEvent(dataType, DataChangeKind.Saved))
            .ConfigureAwait(false);
        StateChanged?.Invoke();
        return true;
    }

    /// <summary>Save every type that has pending changes.</summary>
    public async Task<bool> SaveAllAsync()
    {
        var ok = true;
        foreach (var type in GetModifiedTypes().ToList())
        {
            if (!await SaveAsync(type).ConfigureAwait(false)) ok = false;
        }
        return ok;
    }

    /// <summary>Re-read from disk, discarding any pending UI edits.</summary>
    public async Task<bool> ReloadAsync(Type dataType)
    {
        if (_dataContext is null) return false;
        if (!_entries.TryGetValue(dataType, out var entry)) return false;

        // Go through the repository's own initialiser rather than IDataContext.ReloadAsync —
        // the latter routes through the runtime serializer factory which doesn't know about
        // CSV (handled by source-generated serializers per-type).
        entry.DataSource.Revert();
        await entry.Repository.InitializeAsync().ConfigureAwait(false);
        entry.DataSource.RefreshBaseline();

        await _notifier.NotifyAsync(new DataChangedEvent(dataType, DataChangeKind.Reloaded))
            .ConfigureAwait(false);
        StateChanged?.Invoke();
        return true;
    }

    private static EditableEntry? BuildEntry(DataTypeInfo info, object repository)
    {
        if (repository is not IEditableRepository editable) return null;

        var repoType = repository.GetType();
        var interfaces = repoType.GetInterfaces();

        var tableIface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ITableRepository<,>));
        if (tableIface is not null)
        {
            var args = tableIface.GetGenericArguments();
            return BuildEntryFromOpenType(info, editable, typeof(EditableKeyValueDataSource<,>), args, repository);
        }

        var singleIface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ISingleRepository<>));
        if (singleIface is not null)
        {
            var args = singleIface.GetGenericArguments();
            return BuildEntryFromOpenType(info, editable, typeof(Datra.Editor.DataSources.EditableSingleDataSource<>), args, repository);
        }

        var assetIface = interfaces.FirstOrDefault(i =>
            i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAssetRepository<>));
        if (assetIface is not null)
        {
            var args = assetIface.GetGenericArguments();
            return BuildEntryFromOpenType(info, editable, typeof(Datra.Editor.DataSources.EditableAssetDataSource<>), args, repository);
        }

        // Localisation context and other kinds: recognised but not wrapped yet.
        return null;
    }

    private static EditableEntry BuildEntryFromOpenType(
        DataTypeInfo info,
        IEditableRepository editable,
        Type openSourceType,
        Type[] genericArgs,
        object repository)
    {
        var closed = openSourceType.MakeGenericType(genericArgs);
        var source = (IEditableDataSource)Activator.CreateInstance(closed, repository)!;
        return new EditableEntry(info, editable, source);
    }

    private sealed record EditableEntry(DataTypeInfo Info, IEditableRepository Repository, IEditableDataSource DataSource);
}
