#nullable enable
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Datra.Lab;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Datra.WebEditor.Server;

/// <summary>
/// HTTP surface of the balance lab. The lab screen (<c>&lt;DatraLab /&gt;</c>) is drawn by
/// JavaScript view modules that talk to these endpoints, so the same modules can later read a
/// result baked into an exported page instead.
/// </summary>
public static class DatraLabEndpoints
{
    /// <summary>
    /// Map the lab endpoints under <paramref name="basePath"/> (default: <c>/api/datra/lab</c>).
    /// Requires <c>AddDatraLab</c>.
    /// </summary>
    /// <returns>The endpoint group, in case the consumer wants to chain authorisation policies.</returns>
    public static RouteGroupBuilder MapDatraLab(this IEndpointRouteBuilder app, string basePath = "/api/datra/lab")
    {
        if (app is null) throw new ArgumentNullException(nameof(app));
        var group = app.MapGroup(basePath).WithTags("Datra Lab");

        group.MapGet("/schema", async (ILabEngine lab, CancellationToken ct) =>
            Json(await lab.GetSchemaAsync(ct)));

        group.MapPost("/run", async (HttpRequest request, ILabEngine lab, CancellationToken ct) =>
        {
            var (scenario, problem) = await ReadAsync<Scenario>(request, ct);
            if (scenario is null) return problem!;

            try
            {
                return Json(await lab.RunAsync(scenario, ct));
            }
            catch (ScenarioException ex)
            {
                return Error(StatusCodes.Status400BadRequest, ex.Message);
            }
        });

        group.MapGet("/snapshots", async (ILabEngine lab) =>
            lab.Snapshots is null
                ? Error(StatusCodes.Status404NotFound, "Snapshots are off for this lab.")
                : Json(await lab.Snapshots.ListAsync()));

        group.MapGet("/snapshots/{name}", async (string name, ILabEngine lab) =>
        {
            if (lab.Snapshots is null) return Error(StatusCodes.Status404NotFound, "Snapshots are off for this lab.");
            if (!FileSnapshotStore.IsValidName(name)) return Error(StatusCodes.Status400BadRequest, "Not a snapshot name.");

            var snapshot = await lab.Snapshots.LoadAsync(name);
            return snapshot is null
                ? Error(StatusCodes.Status404NotFound, $"No snapshot named '{name}'.")
                : Json(snapshot);
        });

        group.MapPost("/snapshots", async (HttpRequest request, ILabEngine lab, CancellationToken ct) =>
        {
            if (lab.Snapshots is null) return Error(StatusCodes.Status404NotFound, "Snapshots are off for this lab.");

            var (body, problem) = await ReadAsync<SaveSnapshotRequest>(request, ct);
            if (body is null) return problem!;

            try
            {
                return Json(await lab.SaveSnapshotAsync(body.Name, body.Scenario ?? new Scenario(), ct));
            }
            catch (ScenarioException ex)
            {
                return Error(StatusCodes.Status400BadRequest, ex.Message);
            }
        });

        return group;
    }

    /// <summary>Body of <c>POST /snapshots</c>: the scenario to freeze and the name to keep it under.</summary>
    public sealed class SaveSnapshotRequest
    {
        public string Name { get; set; } = string.Empty;
        public Scenario? Scenario { get; set; }
    }

    // The lab has its own JSON shape (LabJson). Reading and writing with it here keeps the
    // documents identical whatever JSON options the host application configured.
    private static IResult Json<T>(T value) => Results.Json(value, LabJson.Options);

    private static IResult Error(int status, string message) =>
        Results.Json(new { error = message }, LabJson.Options, statusCode: status);

    private static async Task<(T? Value, IResult? Problem)> ReadAsync<T>(HttpRequest request, CancellationToken ct)
        where T : class
    {
        try
        {
            var value = await JsonSerializer.DeserializeAsync<T>(request.Body, LabJson.Options, ct);
            return value is null
                ? (null, Error(StatusCodes.Status400BadRequest, "The request body is empty."))
                : (value, null);
        }
        catch (JsonException ex)
        {
            return (null, Error(StatusCodes.Status400BadRequest, "The request body is not valid JSON: " + ex.Message));
        }
    }
}
