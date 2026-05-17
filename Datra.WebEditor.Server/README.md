# Datra.WebEditor.Server

ASP.NET Core helper for hosting [Datra.WebEditor](../Datra.WebEditor/README.md). One extension
method maps a small REST surface onto the same `DatraEditorHostService` the Blazor UI uses:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MyDataContext>();
builder.Services.AddDatraWebEditor(opt => opt.DataContextType = typeof(MyDataContext));

var app = builder.Build();
app.MapDatraEditor();      // → /api/datra/{status,save,reload}
app.Run();
```

`MapDatraEditor` returns the underlying `RouteGroupBuilder` so you can attach authorisation
policies or extra routes:

```csharp
app.MapDatraEditor("/admin/datra").RequireAuthorization("Admin");
```

Endpoints:

| Method | Route                                    | Purpose                                |
|--------|------------------------------------------|----------------------------------------|
| GET    | `/api/datra/status`                      | Type catalogue + per-type dirty flags  |
| POST   | `/api/datra/save/{typeName}`             | Flush one type                         |
| POST   | `/api/datra/save`                        | Flush every dirty type                 |
| POST   | `/api/datra/reload/{typeName}`           | Discard pending edits, re-read disk    |

`typeName` accepts both the short and fully-qualified CLR type name.

The endpoints are deliberately minimal — they're for external trigger scenarios (CI bots, CLI
tools, remote reload from a co-running process). Day-to-day editing goes through the Blazor UI
via DI, not HTTP.
