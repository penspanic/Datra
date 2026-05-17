# Datra.WebEditor

Blazor-based web editor for Datra-managed game data. Consume it as a Razor Class Library plus an
optional ASP.NET Core helper package — no per-game scaffolding required. The editor reflects over
your existing `IDataContext`, surfaces every `ITableRepository<TKey, TData>` it finds, and writes
back through Datra's own repository pipeline.

```
Datra.WebEditor          ← Razor Class Library (RCL): handlers, components, services
Datra.WebEditor.Server   ← optional ASP.NET Core endpoints (MapDatraEditor)
```

## Quick start (server-side Blazor / Blazor WASM host)

```csharp
// 1. Your existing context — already wired with a raw data provider.
services.AddSingleton<DataSerializerFactory>();
services.AddSingleton(sp => new MyDataContext(
    sp.GetRequiredService<IRawDataProvider>(),
    sp.GetRequiredService<DataSerializerFactory>()));

// 2. Wire the editor.
services.AddDatraWebEditor(opt => opt.DataContextType = typeof(MyDataContext));

// 3. (Optional) REST endpoints for external triggers / non-Blazor clients.
app.MapDatraEditor();   // → /api/datra/{status,save,reload,...}
```

In a Razor component:

```razor
@page "/data-editor"

<link rel="stylesheet" href="_content/Datra.WebEditor/datra-webeditor.css" />

<div style="height: 100vh">
    <DatraEditor Theme="@(_darkMode ? null : "light")"
                 OnDataChanged="OnDataChanged" />
</div>

@code {
    private bool _darkMode = true;

    private async Task OnDataChanged(DataChangedEvent evt)
    {
        // Save complete — kick your hot-reload pipeline, refresh a sim, etc.
        await Hot.ReloadAsync(evt.DataType);
    }
}
```

That's the whole consumer surface. The editor auto-discovers every editable type on
`MyDataContext` and renders a dirty-tracked, schema-driven table for each one.

## What you get

- **Auto-enumerated types.** Drop a new property on your `DataContext`; it shows up in the sidebar
  with no editor-side code.
- **Schema-driven forms.** Primitives, enums, lists, DataRefs, LocaleRefs, and arbitrary nested
  classes/structs all render via a priority chain you can extend or override.
- **Transactional edits.** Every change lands in an in-memory `EditableDataSource` first; clicking
  Save flushes to the repository, which writes back through your raw data provider (file system,
  bundled, custom — Datra's contract).
- **Hot-reload signalling.** `IDataChangedNotifier` fires after every save or reload so a host
  app (e.g. one running a live simulation) can refresh in-process without polling a file watcher.
- **Theming via CSS custom properties.** Override `--datra-bg`, `--datra-accent`, etc. on `:root`
  (or any wrapping container) to match your design system. A `data-datra-theme="light"` opt-in
  ships out of the box; everything else is a few token changes.

## Theming

Default tokens live in `datra-webeditor.css`. Drop your own overrides anywhere reachable from the
editor's DOM subtree:

```css
:root {
    --datra-bg:           #0c1018;
    --datra-bg-elevated:  #131826;
    --datra-accent:       #6dd3ff;
    --datra-dirty:        #ffd166;
    --datra-radius:       10px;
}
```

Tokens you'll typically retune: `--datra-bg*` (surfaces), `--datra-text*` (foreground), `--datra-accent`
(selection, primary buttons), `--datra-dirty` / `--datra-danger` / `--datra-success` (state
indicators), and `--datra-font-ui` / `--datra-font-mono` (fonts). See the top of
`datra-webeditor.css` for the full list.

## Plugging in a custom widget

Register an additional handler — higher priority wins:

```csharp
public sealed class ColorFieldHandler : IBlazorFieldHandler
{
    public int Priority => 50;
    public bool CanHandle(Type t, MemberInfo? _ = null) => t == typeof(MyColor);
    public RenderFragment CreateField(FieldCreationContext ctx) => b =>
    {
        // Blazor render-tree code that calls ctx.OnValueChanged on commit.
    };
}

// during startup
services.AddDatraWebEditor(opt => opt.DataContextType = typeof(MyDataContext));
services.PostConfigure<BlazorFieldTypeRegistry>(reg => reg.RegisterHandler(new ColorFieldHandler()));
```

## REST endpoints (Datra.WebEditor.Server)

```
GET  /api/datra/status                — type catalogue + dirty flags
POST /api/datra/save/{typeName}       — flush one type
POST /api/datra/save                  — flush everything dirty
POST /api/datra/reload/{typeName}     — discard pending edits and re-read from disk
```

`typeName` matches either the short or fully-qualified type name. All four endpoints route
through the same `DatraEditorHostService` the Blazor UI uses, so external triggers compose
cleanly with the in-process editor.

## Architecture overview

```
                   ┌─────────────────────────────────┐
   Razor UI  ◄────►│  DatraEditor / DatraTableView   │
                   │  DatraField / DatraTypeSidebar  │
                   └─────────────┬───────────────────┘
                                 │ (DI)
                   ┌─────────────▼───────────────────┐
                   │   DatraEditorHostService        │
                   │  • reflects DataContext         │
                   │  • owns IEditableDataSource map │
                   │  • Save / Reload dispatch       │
                   └─┬──────────────────────────┬────┘
                     │                          │
        EditableKeyValueDataSource<TKey,T>    IDataChangedNotifier
        (Datra.Editor — transactional edits)  (pub/sub for hot-reload)
                     │
        ITableRepository<TKey,T> (Datra core, source-generated)
                     │
        IRawDataProvider — file system / bundled / custom
```

## Status

The editor currently handles **table** (key-value) repositories. Single, asset, and localisation
repositories are recognised by the host service but not yet surfaced in the table view — they
will appear in the sidebar without a dedicated editor panel. Adding those is the next step.
