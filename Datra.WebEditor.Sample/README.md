# Datra.WebEditor.Sample

A self-contained Blazor Server demo of [Datra.WebEditor](../Datra.WebEditor/README.md). Loads the
existing `Datra.SampleData` (`GameDataContext`) and surfaces every table repository in a live
editor.

## Run it

From the Datra repo root:

```bash
dotnet run --project Datra.WebEditor.Sample
```

Then open <http://localhost:5170>. Press <kbd>Ctrl</kbd>+<kbd>C</kbd> to stop.

## What it shows

The sidebar lists every type on `GameDataContext` that the editor currently supports — 10 table
types spanning **CSV** (Characters, EnumTest, …), **JSON** (Items, Quests), and **YAML**
(Skills, Enemies). Editing any cell flips the dirty indicator; Save flushes to the scratch
directory; Revert drops pending edits.

Single, asset, and localisation repositories are recognised by the data context but hidden until
the editor grows dedicated views for them.

## Balance lab

<http://localhost:5170/lab> (or `/lab?lang=ko`) shows [Datra.Lab](../docs/LAB.md) on a toy
incremental game from `Datra.Lab.Sample`: floors with a fare, a fare box that is also the
wallet, upgrades. Move a knob and 40 bots replay the game; the saved data is drawn in grey
behind the new result. Its three YAML files are staged into the same scratch directory, and
snapshots go to `lab-snapshots/` inside it.

## What happens on disk

Your repo stays clean. On every launch, `SampleResourceStager` copies
`Datra.SampleData/Resources` into a fresh directory under your OS temp:

```
/tmp/datra-webeditor-sample/<yyyymmdd-hhmmss>/
    Characters.csv
    Items.json
    Skills.yaml
    ...
```

That timestamped folder is the scratch space the editor reads from and writes to. The path is
logged to stdout on startup so you can `cat` the file after a save to confirm the round trip:

```
[Datra.WebEditor.Sample] staged sample data → /tmp/datra-webeditor-sample/20260517-143205
```

## REST surface

The same endpoints exposed by `MapDatraEditor` are mounted under `/api/datra`:

```
GET  /api/datra/status                — type catalogue + dirty flags
POST /api/datra/save/{typeName}       — flush one type
POST /api/datra/save                  — flush everything dirty
POST /api/datra/reload/{typeName}     — discard pending edits, re-read disk
```

Example:

```bash
curl -s http://localhost:5170/api/datra/status | jq
```

## Adapting the demo to another DataContext

Drop in your context in [`Program.cs`](Program.cs):

```csharp
builder.Services.AddSingleton(sp => new MyContext(
    sp.GetRequiredService<IRawDataProvider>(),
    sp.GetRequiredService<DataSerializerFactory>()));

builder.Services.AddDatraWebEditor(opt => opt.DataContextType = typeof(MyContext));
```

If your data lives under a different folder, swap [`SampleResourceStager`](SampleResourceStager.cs)
for any other path source.
