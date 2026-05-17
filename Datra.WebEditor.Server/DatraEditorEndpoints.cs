#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Datra.WebEditor.Extensions;
using Datra.WebEditor.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Datra.WebEditor.Server;

/// <summary>
/// REST surface that mirrors <see cref="DatraEditorHostService"/> so non-Blazor clients (CLI
/// tools, external reload triggers, etc.) can drive the same save/reload pipeline the UI uses.
/// </summary>
/// <remarks>
/// Endpoints are intentionally tiny — they are pass-throughs onto the host service. The editor
/// UI talks to the service directly via DI and does not go through HTTP.
/// </remarks>
public static class DatraEditorEndpoints
{
    /// <summary>
    /// Map the standard editor endpoints under <paramref name="basePath"/> (default: <c>/api/datra</c>).
    /// </summary>
    /// <returns>The endpoint group, in case the consumer wants to chain authorisation policies.</returns>
    public static RouteGroupBuilder MapDatraEditor(this IEndpointRouteBuilder app, string basePath = "/api/datra")
    {
        if (app is null) throw new ArgumentNullException(nameof(app));
        var group = app.MapGroup(basePath).WithTags("Datra Editor");

        group.MapGet("/status", async (DatraEditorHostService host, DatraEditorBootstrapper bootstrapper) =>
        {
            await bootstrapper.EnsureInitialisedAsync();
            return Results.Ok(new
            {
                types = host.DataTypes.Select(d => new
                {
                    typeName = d.DataType.FullName,
                    propertyName = d.PropertyName,
                    filePath = d.FilePath,
                    kind = d.RepositoryKind.ToString(),
                    dirty = host.HasUnsavedChanges(d.DataType),
                }),
                anyDirty = host.HasAnyUnsavedChanges(),
            });
        });

        group.MapPost("/save/{typeName}", async (
            string typeName,
            DatraEditorHostService host,
            DatraEditorBootstrapper bootstrapper) =>
        {
            await bootstrapper.EnsureInitialisedAsync();
            var dataType = host.DataTypes.FirstOrDefault(d => MatchesTypeName(d.DataType, typeName))?.DataType;
            if (dataType is null) return Results.NotFound(new { error = "unknown data type", typeName });

            var ok = await host.SaveAsync(dataType);
            return ok ? Results.Ok(new { saved = dataType.FullName }) : Results.Problem("save failed");
        });

        group.MapPost("/save", async (DatraEditorHostService host, DatraEditorBootstrapper bootstrapper) =>
        {
            await bootstrapper.EnsureInitialisedAsync();
            var ok = await host.SaveAllAsync();
            return ok ? Results.Ok(new { saved = "all" }) : Results.Problem("one or more saves failed");
        });

        group.MapPost("/reload/{typeName}", async (
            string typeName,
            DatraEditorHostService host,
            DatraEditorBootstrapper bootstrapper) =>
        {
            await bootstrapper.EnsureInitialisedAsync();
            var dataType = host.DataTypes.FirstOrDefault(d => MatchesTypeName(d.DataType, typeName))?.DataType;
            if (dataType is null) return Results.NotFound(new { error = "unknown data type", typeName });

            var ok = await host.ReloadAsync(dataType);
            return ok ? Results.Ok(new { reloaded = dataType.FullName }) : Results.Problem("reload failed");
        });

        return group;
    }

    private static bool MatchesTypeName(Type dataType, string raw) =>
        string.Equals(dataType.FullName, raw, StringComparison.Ordinal) ||
        string.Equals(dataType.Name, raw, StringComparison.Ordinal);
}
