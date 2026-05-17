#nullable enable
using System;
using Datra.Interfaces;

namespace Datra.WebEditor.Services;

/// <summary>
/// Tunables for <see cref="Datra.WebEditor.Extensions.DatraWebEditorServiceCollectionExtensions.AddDatraWebEditor"/>.
/// </summary>
public sealed class DatraWebEditorOptions
{
    /// <summary>
    /// The CLR type of the consumer's <see cref="IDataContext"/> implementation. The service
    /// container is expected to be able to resolve that type. Required.
    /// </summary>
    public Type? DataContextType { get; set; }

    /// <summary>
    /// Default page size for table views. Zero or negative disables pagination.
    /// </summary>
    public int DefaultPageSize { get; set; } = 100;

    /// <summary>
    /// Whether to register the built-in field handler chain (string/int/float/bool/enum/list/...).
    /// Set false to compose your own from scratch.
    /// </summary>
    public bool RegisterDefaultHandlers { get; set; } = true;
}
