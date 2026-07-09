#nullable enable
using System;
using System.Reflection;
using Datra.Editor.Services;
using Datra.WebEditor.Abstractions;

namespace Datra.WebEditor.Handlers;

/// <summary>
/// Blazor field-type registry. Extends the platform-agnostic <see cref="FieldTypeRegistry"/>
/// to surface handlers that produce <see cref="Microsoft.AspNetCore.Components.RenderFragment"/>.
/// </summary>
/// <remarks>
/// Call <see cref="RegisterDefaultHandlers"/> at startup to install the built-in priority chain
/// (LocaleRef → DataRef → Nested → List → Enum → primitives). Consumers can call
/// <see cref="FieldTypeRegistry.RegisterHandler"/> afterwards to override or add custom widgets.
/// </remarks>
public class BlazorFieldTypeRegistry : FieldTypeRegistry
{
    public IBlazorFieldHandler? FindBlazorHandler(Type type, MemberInfo? member = null)
        => FindHandler<IBlazorFieldHandler>(type, member);

    /// <summary>
    /// Register the default handler chain — covers every type Datra natively supports.
    /// </summary>
    public void RegisterDefaultHandlers()
    {
        RegisterHandler(new LocaleRefFieldHandler());      // 100 — FixedLocale-attributed LocaleRef
        RegisterHandler(new DataRefFieldHandler());        //  40 — StringDataRef<T> / IntDataRef<T>
        RegisterHandler(new NestedTypeFieldHandler(this)); //  30 — complex class/struct
        RegisterHandler(new DictionaryFieldHandler(this)); //  24 — IDictionary<TKey,TValue>
        RegisterHandler(new ArrayFieldHandler(this));      //  22 — T[] 1-D arrays
        RegisterHandler(new ListFieldHandler(this));       //  20 — IList<T>
        RegisterHandler(new EnumFieldHandler());           //  10 — Enum
        RegisterHandler(new ColorFieldHandler());          //   5 — string + [Color] attribute
        RegisterHandler(new StringFieldHandler());         //   1 — primitives
        RegisterHandler(new IntFieldHandler());
        RegisterHandler(new LongFieldHandler());
        RegisterHandler(new FloatFieldHandler());
        RegisterHandler(new DoubleFieldHandler());
        RegisterHandler(new BoolFieldHandler());
        RegisterHandler(new DateTimeFieldHandler());
    }
}
