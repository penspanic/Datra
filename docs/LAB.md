# Balance Lab

The lab answers "what happens if I change this number" without anyone reading a table. You
move a knob (a named value with a unit and a range), a batch of bots plays the game from start
to finish with the new value, and the time per stage, the progression curve and the purchase
order are redrawn. Nothing is written to your data files while you do this.

This is the first slice (P0): economy knobs, three views, snapshots. See
[What is not here yet](#what-is-not-here-yet).

## Table of Contents

- [Who provides what](#who-provides-what)
- [Adopting it](#adopting-it)
- [Knobs](#knobs)
- [The simulator](#the-simulator)
- [Stored physics outcomes](#stored-physics-outcomes)
- [Scenarios, results, snapshots](#scenarios-results-snapshots)
- [HTTP endpoints](#http-endpoints)
- [Without a web host](#without-a-web-host)
- [Trying the sample](#trying-the-sample)
- [What is not here yet](#what-is-not-here-yet)

---

## Who provides what

| Datra provides (once, for every game) | The game writes |
|---|---|
| **Knob panel**: sliders grouped and labelled from the attributes, with the saved value marked | **One attribute per tunable field**: label, group, unit, range |
| **Forked data context**: the simulator reads edited values through the ordinary `data.Floor` API; the files are never touched | (nothing) |
| **Bot runner**: N bots, one seed each, in parallel, the same result every time | **One simulator class**: how a single bot plays. It calls the game's own earnings and shop code |
| **Views**: time per stage, progression, purchase timeline, each with the comparison overlaid | (nothing — no chart code, no screen code) |
| **Result document** (JSON), targets and checks, the sentences under "Against the targets" | (optional) targets, checks, a reason only the game knows (`run.Explain`) |
| **Scenarios and snapshots**: baseline + changed knobs, saved with the result they gave | (nothing) |
| **Reader contract for stored physics outcomes** | **The outcomes**: whatever batch the game already runs, stored per physics state |
| Endpoints and the `<DatraLab />` component | **Two lines in the host**: `AddDatraLab`, `MapDatraLab` |

The packages:

```
Datra                   [Knob] attribute (next to [TableData], so data assemblies need nothing else)
Datra.Lab               no UI: knob schema, forked context, simulator contract, bot runner, results, snapshots
Datra.WebEditor.Server  AddDatraLab<TContext, TSimulator>(), MapDatraLab()
Datra.WebEditor         <DatraLab /> and the JavaScript view modules it hosts
```

## Adopting it

**1. Mark the fields.** One line per knob, on the data class.

```csharp
using Datra.Attributes;

[SingleData("Economy.yaml")]
public partial class EconomyData
{
    [Knob("Step value", Group = "Rewards", Unit = "coins/step", Min = 1, Max = 15, Step = 0.5)]
    public float StepValue { get; set; } = 5f;
}
```

**2. Write the simulator.** One class: a single bot, start to finish.

```csharp
using Datra.Lab;

public sealed class GameSim : ISimulator<GameDataContext>
{
    public void Play(GameDataContext data, SimRun run)
    {
        var progress = new PlayerProgress();
        foreach (var floor in data.Floor.LoadedItems.Values)
        {
            run.Stage(floor.Id, targetSeconds: floor.TargetMinutes * 60);     // "time per stage"
            while (progress.Wallet < floor.Fare && !run.OutOfTime)
            {
                var bottles = run.Outcome<BottleTally[]>(PhysicsKeys.Of(data, progress, floor)); // a stored run
                var earned  = Earnings.Of(data, progress, bottles);           // the game's own rule
                progress.Earn(earned);
                run.Tick(seconds: RunLength.Of(bottles), earned);
                run.Progress(progress.Wallet / floor.Fare);                   // "progression"

                foreach (var offer in Bot.Pick(run.Policy, data, progress))   // the game's own shop
                {
                    progress.Buy(offer);
                    run.Purchase(offer.Id, offer.Cost, group: offer.Scope);   // "purchase timeline"
                    run.Progress(progress.Wallet / floor.Fare);
                }
            }
            progress.Descend();
        }
    }
}
```

**3. Add two lines to the host** (`Program.cs`), next to the editor's.

```csharp
using Datra.WebEditor.Server;

builder.Services.AddDatraWebEditor(o => o.DataContextType = typeof(GameDataContext));   // already there
builder.Services.AddDatraLab<GameDataContext, GameSim>(lab => lab.SnapshotDirectory("lab/snapshots"));

app.MapDatraEditor();                                                                    // already there
app.MapDatraLab();
```

`AddDatraLab` reads the baseline data through the `IRawDataProvider` registered in the
container — the same one your data context is built on.

**4. Put the screen on a page.**

```razor
<DatraLab />
```

That is the whole consumer surface. `<DatraLab Locale="ko" />` switches the screen's own text
to Korean; knob labels and stage names are shown as the game wrote them.

## Knobs

```csharp
[Knob("Pellet value", Group = "Rewards", Unit = "coins", Min = 0.5, Max = 5, Step = 0.1,
      Hint = "Coins per pellet that lands in the box")]
public float PelletValue { get; set; } = 1f;
```

| Property | Meaning |
|---|---|
| `Group`, `Unit`, `Hint` | Heading, unit after the value, tooltip |
| `Min`, `Max`, `Step` | Slider range. Left out, the lab picks a range around the saved value |
| `Apply` | `Set` (the field becomes the value), `Scale` (multiplied, rests at 1), `Offset` (added, rests at 0) |
| `Row` | Table data: the Id of the one row the knob addresses. Empty means every row |
| `EachRow` | Table data: one knob per row, labelled `"{Label} · {Id}"` — a fare per floor |
| `Layer` | `Economy` (re-priced instantly) or `Physics` (decides stored outcomes; shown read-only in P0) |

On table data without `Row` or `EachRow`, the knob covers every row: `Apply = Scale` multiplies
the whole column and keeps the hand-tuned ratios between rows. The attribute can be repeated on
one property, one `Row` each.

A knob that spans several fields is declared in code:

```csharp
builder.Services.AddDatraLab<GameDataContext, GameSim>(lab =>
{
    lab.Knobs(knobs => knobs.Add(
        "Depth value growth",
        read:  data => Curves.GrowthOf(data),
        write: (data, growth) => Curves.SetGrowth(data, growth),
        min: 1.4, max: 3, step: 0.05, group: "Floors", unit: "×/floor"));
});
```

`write` is only ever called on a forked context. Datra's analyzer (DATRA001) rejects plain
assignments to data classes, so write through `ForkedData.Set(row, r => r.Fare, value)`.

When a scenario moves several knobs, declared knobs are applied first, then attribute knobs in
the order Set, Scale, Offset. The order the scenario lists them in does not matter.

Knobs are numeric, top-level properties of `[TableData]` / `[SingleData]` types. Everything
else stays in the table editor; the lab does not replace it.

## The simulator

`Play` is called once per bot, from several threads at once, against one shared context. Keep
the class stateless, treat the data as read-only, and take all randomness from `run.Random`:
the same `run.Seed` and the same data must write the same records.

What a simulator writes, and what is drawn from it:

| Call | Meaning | Feeds |
|---|---|---|
| `run.Stage(id, targetSeconds, label)` | The bot enters a phase (a floor, a tier) | Time per stage, target lines |
| `run.Tick(seconds, earned, kind)` | One slice of play; advances game time | Totals, longest run |
| `run.Progress(fraction)` | How far through the phase, 0–1. May go down when spending sets the bot back | Progression |
| `run.Purchase(id, cost, group, label)` | The bot bought something | Purchase timeline |
| `run.Mark(name)` | A moment worth finding later | Result (`marks`) |
| `run.Explain(stage, text)` | A reason only the game knows | "Against the targets" |
| `run.Outcome<T>(key)` | One stored physics outcome, drawn with this bot's randomness | — |

Also available: `run.Policy` and `run.Param(name, fallback)` (what the bot buys and how it is
tuned — scenario settings, not game data), `run.Elapsed`, and `run.OutOfTime`. Loop on
`!run.OutOfTime` so a scenario that can never finish still returns; the bot is then reported as
unfinished instead of hanging the request.

All times are seconds of game time.

Policies, policy parameters and checks are declared with the lab:

```csharp
lab.Policy("efficient", "Efficient");
lab.Policy("cheap", "Cheapest first");
lab.PolicyParam("shopSeconds", "Time between runs", @default: 8, min: 2, max: 30, unit: "s");
lab.Check("First purchase", bot => bot.FirstPurchaseSeconds, "<= 60", unit: "s");
lab.Check("Longest run", bot => bot.LongestTickSeconds("run"), "<= 90", unit: "s");
```

Every comparison runs both sides with the same seeds, so the difference comes from the knobs
and not from luck.

## Stored physics outcomes

When the result of a run comes out of a physics engine, it cannot be recomputed while a slider
moves. The game stores outcomes per physics state ahead of time and the bots draw from them:

```csharp
var key = PhysicsKey.Of(("floor", floor.Id), ("push", pushLevel), ("wrap", wrapLevel));
var outcome = run.Outcome<BottleOutcome>(key);
```

A key is the set of inputs that decide the outcome, as sorted `name=value` pairs. What goes in
is the game's business; Datra treats it as a string.

`IOutcomeReader` is the read side, and the only side in P0:

```csharp
public interface IOutcomeReader
{
    string Version { get; }                       // engine pin + recipe hash
    IReadOnlyList<T>? Pool<T>(PhysicsKey key);    // null when nothing is stored
}
```

`InMemoryOutcomes` holds pools in memory. `JsonlOutcomeReader(directory, version)` reads one
JSON Lines file per key, named `{key.Hash(version)}.jsonl`, one outcome per line. Set either on
`lab.Outcomes`.

`run.Outcome<T>` throws for a key with no stored outcomes; that bot stops and the key is listed
in the result's `problems`. `run.TryOutcome<T>` returns false instead, for a simulator that has
an estimate to fall back on.

## Scenarios, results, snapshots

A **scenario** is the baseline data plus what differs from it:

```json
{ "changes": { "FloorData[B3].Fare": 1200, "EconomyData.StepValue": 7 },
  "bots": 40, "seed": 1, "policy": "efficient", "policyParams": { "shopSeconds": 8 } }
```

The **baseline** is the saved data: what the raw data provider returns. The lab caches it and
re-reads it after the table editor saves or reloads (`IDataChangedNotifier`). Edits still
pending in the table editor are not part of it.

Running a scenario forks the context (a fresh `TContext` on an overlay provider: reads fall
through to the cached baseline, writes stay in the overlay), applies the knobs to the fork,
plays the bots and aggregates. The **result** is one JSON document: `total`, `stages[]`
(median, p10, p90, target), `progression.points[]`, `purchases` (one representative bot),
`checks[]`, `marks[]`, `explains[]`, `problems[]`. The views read nothing else.

A **snapshot** is a named scenario with the result it gave, stored as
`{directory}/{name}.json`. The result is kept so the picture survives later changes to the data
or the simulator; `baselineHash` tells whether the saved data has moved since.

## HTTP endpoints

`MapDatraLab()` mounts under `/api/datra/lab` and returns the route group for authorisation.

| Method | Route | Purpose |
|---|---|---|
| GET | `/schema` | Knobs with baseline values, policies, checks, defaults |
| POST | `/run` | Scenario in, result out. `400 { "error": … }` when the scenario cannot run |
| GET | `/snapshots` | Names, newest first |
| POST | `/snapshots` | `{ "name", "scenario" }`: run it and store it with its result |
| GET | `/snapshots/{name}` | The stored scenario and result |

The documents use the lab's own JSON shape (`LabJson`: camelCase, enum names) whatever the
host configured.

The views are framework-free JavaScript modules (SVG, no dependencies) under
`_content/Datra.WebEditor/lab/`. They take a `source` object (`schema()`, `run()`, the snapshot
calls); `<DatraLab />` hands them the endpoints above.

## Without a web host

`Datra.Lab` is netstandard2.1 and has no UI, so a console tool or a test can run the same thing:

```csharp
var options = new LabOptions<GameDataContext>();
options.Check("First purchase", bot => bot.FirstPurchaseSeconds, "<= 60", unit: "s");

var lab = new LabEngine<GameDataContext>(new FileSystemRawDataProvider("Data"), new GameSim(), options);
var result = await lab.RunAsync(new Scenario { Changes = { ["FloorData[B3].Fare"] = 1200 } });

File.WriteAllText("result.json", LabJson.Serialize(result, indented: true));
```

`lab.ForkAsync(scenario)` returns the forked context itself, and `BotRunner.RunOne` replays a
single bot of a batch from its seed.

## Trying the sample

```bash
dotnet run --project Datra.WebEditor.Sample
```

Open <http://localhost:5170/lab>. `Datra.Lab.Sample` is a toy incremental game — floors with a
fare, a fare box that is also the wallet, upgrades, everything left behind on the way down —
with a short simulator and a stand-in for a physics batch.

## What is not here yet

- Recomputing physics outcomes: the work queue, coarse-then-fine seeds, the "estimate" state.
  Physics knobs are listed but read-only.
- Play records: overlaying recorded sessions, fitting the bots to them, replays.
- Pushing values into a running game.
- Exporting a standalone page, presentation mode, share links.
- More views (distributions, sensitivity, sweeps), A/B beyond one comparison, scenario diffs
  and applying a scenario back to the data files.
- Pending table-editor edits as part of the baseline.
