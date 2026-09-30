#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Datra.Attributes;
using Datra.Interfaces;

namespace Datra.Lab
{
    /// <summary>
    /// One knob bound to a context type: its description plus the code that writes a value
    /// into a (forked) context.
    /// </summary>
    public sealed class KnobBinding<TContext> where TContext : class, IDataContext
    {
        private readonly Action<TContext, double> _apply;

        internal KnobBinding(KnobInfo info, int order, Action<TContext, double> apply)
        {
            Info = info;
            Order = order;
            _apply = apply;
        }

        public KnobInfo Info { get; }

        /// <summary>Position in the schema. Knobs are applied in this order within one pass.</summary>
        internal int Order { get; }

        /// <summary>Write <paramref name="value"/> into <paramref name="data"/>.</summary>
        public void Apply(TContext data, double value) => _apply(data, value);
    }

    /// <summary>
    /// Builds the knob list for a context by reflection: every <see cref="KnobAttribute"/> on
    /// the properties of the context's data types, plus knobs declared in code.
    /// </summary>
    public static class KnobSchemaReader
    {
        /// <summary>
        /// Read the knobs of <paramref name="baseline"/>. The context must be loaded: baseline
        /// values and the rows of <see cref="KnobAttribute.EachRow"/> knobs are read from it.
        /// </summary>
        public static IReadOnlyList<KnobBinding<TContext>> Read<TContext>(
            TContext baseline,
            IEnumerable<IKnobSet<TContext>>? declared = null)
            where TContext : class, IDataContext
        {
            if (baseline is null) throw new ArgumentNullException(nameof(baseline));

            var result = new List<KnobBinding<TContext>>();

            // Declared knobs first: they usually rewrite a whole column (a curve), and the
            // attribute knobs that follow refine single fields on top.
            if (declared != null)
            {
                foreach (var set in declared)
                {
                    var builder = new KnobBuilder<TContext>();
                    set.Declare(builder);
                    foreach (var knob in builder.Knobs)
                    {
                        var info = new KnobInfo
                        {
                            Id = knob.Id,
                            Label = knob.Label,
                            Group = knob.Group,
                            Unit = knob.Unit,
                            Hint = knob.Hint,
                            Min = knob.Min,
                            Max = knob.Max,
                            Layer = knob.Layer,
                            Apply = KnobApply.Set,
                            Baseline = knob.Read(baseline),
                        };
                        info.Step = knob.Step > 0 ? knob.Step : NiceStep(info.Min, info.Max);
                        info.Editable = info.Layer == KnobLayer.Economy;
                        result.Add(new KnobBinding<TContext>(info, result.Count, knob.Write));
                    }
                }
            }

            foreach (var typeInfo in baseline.GetDataTypeInfos())
            {
                var repositoryProperty = typeof(TContext).GetProperty(
                    typeInfo.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (repositoryProperty is null) continue;

                foreach (var property in typeInfo.DataType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attributes = property.GetCustomAttributes<KnobAttribute>(inherit: true).ToList();
                    if (attributes.Count == 0) continue;

                    if (!IsNumeric(property.PropertyType) || !property.CanRead || !property.CanWrite)
                    {
                        throw new InvalidOperationException(
                            $"[Knob] on {typeInfo.DataType.Name}.{property.Name}: a knob needs a readable, writable " +
                            $"numeric property, and this one is {property.PropertyType.Name}.");
                    }

                    foreach (var attribute in attributes)
                    {
                        foreach (var row in RowsFor(attribute, typeInfo, repositoryProperty, baseline))
                        {
                            result.Add(BindField(
                                baseline, typeInfo, repositoryProperty, property, attribute, row, result.Count));
                        }
                    }
                }
            }

            var duplicate = result.GroupBy(k => k.Info.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
            if (duplicate != null)
            {
                throw new InvalidOperationException(
                    $"Two knobs share the id '{duplicate.Key}'. Give each [Knob] on one property its own Row, " +
                    "or pass an explicit id to the declared knob.");
            }

            return result;
        }

        private static IEnumerable<string?> RowsFor<TContext>(
            KnobAttribute attribute, DataTypeInfo typeInfo, PropertyInfo repositoryProperty, TContext baseline)
            where TContext : class, IDataContext
        {
            if (!string.IsNullOrEmpty(attribute.Row))
            {
                if (typeInfo.RepositoryKind != RepositoryKind.Table)
                    throw new InvalidOperationException(
                        $"[Knob(\"{attribute.Label}\")] on {typeInfo.DataType.Name}: Row only applies to table data.");
                yield return attribute.Row;
                yield break;
            }

            if (attribute.EachRow && typeInfo.RepositoryKind == RepositoryKind.Table)
            {
                foreach (var item in Items(repositoryProperty, baseline))
                    yield return KeyOf(item);
                yield break;
            }

            yield return null;
        }

        private static KnobBinding<TContext> BindField<TContext>(
            TContext baseline,
            DataTypeInfo typeInfo,
            PropertyInfo repositoryProperty,
            PropertyInfo property,
            KnobAttribute attribute,
            string? row,
            int order)
            where TContext : class, IDataContext
        {
            var typeName = typeInfo.DataType.Name;
            var integer = IsInteger(property.PropertyType);
            var targets = Targets(repositoryProperty, baseline, row).ToList();
            if (targets.Count == 0)
            {
                throw new InvalidOperationException(row is null
                    ? $"[Knob(\"{attribute.Label}\")] on {typeName}.{property.Name}: the data has no rows to address."
                    : $"[Knob(\"{attribute.Label}\")] on {typeName}.{property.Name}: no row with Id '{row}'.");
            }

            var values = targets.Select(t => ToDouble(property.GetValue(t))).ToList();
            var info = new KnobInfo
            {
                Id = row is null ? $"{typeName}.{property.Name}" : $"{typeName}[{row}].{property.Name}",
                Label = attribute.EachRow && row != null && string.IsNullOrEmpty(attribute.Row)
                    ? $"{attribute.Label} · {row}"
                    : attribute.Label,
                Group = attribute.Group,
                Unit = attribute.Unit,
                Hint = attribute.Hint,
                Layer = attribute.Layer,
                Apply = attribute.Apply,
                Integer = integer && attribute.Apply != KnobApply.Scale,
                Editable = attribute.Layer == KnobLayer.Economy,
                Source = new KnobSource
                {
                    DataType = typeName,
                    Property = property.Name,
                    Row = row,
                    File = typeInfo.FilePath,
                },
            };

            switch (attribute.Apply)
            {
                case KnobApply.Scale:
                    info.Baseline = 1;
                    info.Min = double.IsNaN(attribute.Min) ? 0 : attribute.Min;
                    info.Max = double.IsNaN(attribute.Max) ? 2 : attribute.Max;
                    break;
                case KnobApply.Offset:
                {
                    info.Baseline = 0;
                    var span = Math.Max(1, values.Max(Math.Abs));
                    info.Min = double.IsNaN(attribute.Min) ? -span : attribute.Min;
                    info.Max = double.IsNaN(attribute.Max) ? span : attribute.Max;
                    break;
                }
                default:
                {
                    info.Baseline = values[0];
                    info.Mixed = values.Any(v => Math.Abs(v - values[0]) > 1e-9);
                    var top = Math.Max(1, Math.Abs(info.Baseline) * 2);
                    info.Min = double.IsNaN(attribute.Min) ? Math.Min(0, info.Baseline * 2) : attribute.Min;
                    info.Max = double.IsNaN(attribute.Max) ? top : attribute.Max;
                    break;
                }
            }

            if (!(info.Max > info.Min))
                throw new InvalidOperationException($"[Knob(\"{attribute.Label}\")] on {typeName}.{property.Name}: Max must be greater than Min.");

            info.Step = attribute.Step > 0 ? attribute.Step
                : info.Integer ? Math.Max(1, Math.Round(NiceStep(info.Min, info.Max)))
                : NiceStep(info.Min, info.Max);

            var apply = attribute.Apply;
            void Apply(TContext data, double value)
            {
                foreach (var target in Targets(repositoryProperty, data, row))
                {
                    var current = ToDouble(property.GetValue(target));
                    var next = apply switch
                    {
                        KnobApply.Scale => current * value,
                        KnobApply.Offset => current + value,
                        _ => value,
                    };
                    property.SetValue(target, FromDouble(next, property.PropertyType));
                }
            }

            return new KnobBinding<TContext>(info, order, Apply);
        }

        private static IEnumerable<object> Targets<TContext>(PropertyInfo repositoryProperty, TContext data, string? row)
            where TContext : class, IDataContext
        {
            foreach (var item in Items(repositoryProperty, data))
            {
                if (row is null || string.Equals(KeyOf(item), row, StringComparison.Ordinal))
                    yield return item;
            }
        }

        /// <summary>The loaded objects of one repository on a context, in load order.</summary>
        private static IEnumerable<object> Items<TContext>(PropertyInfo repositoryProperty, TContext data)
            where TContext : class, IDataContext
        {
            var repository = repositoryProperty.GetValue(data);
            switch (repository)
            {
                case null:
                    yield break;
                case IEditableRepository editable:
                    foreach (var item in editable.EnumerateItems()) yield return item;
                    yield break;
            }

            // Runtime (read-only) repositories expose LoadedItems / Current but not EnumerateItems.
            var type = repository.GetType();
            if (type.GetProperty("LoadedItems")?.GetValue(repository) is IEnumerable loaded)
            {
                foreach (var pair in loaded)
                {
                    var value = pair?.GetType().GetProperty("Value")?.GetValue(pair);
                    if (value != null) yield return value;
                }
                yield break;
            }

            var current = type.GetProperty("Current")?.GetValue(repository);
            if (current != null) yield return current;
        }

        private static string KeyOf(object item)
        {
            var id = item.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(item);
            return Convert.ToString(id, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static bool IsInteger(Type type)
        {
            switch (Type.GetTypeCode(Nullable.GetUnderlyingType(type) ?? type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsNumeric(Type type)
        {
            if (type.IsEnum || Nullable.GetUnderlyingType(type) != null) return false;
            if (IsInteger(type)) return true;
            var code = Type.GetTypeCode(type);
            return code == TypeCode.Single || code == TypeCode.Double || code == TypeCode.Decimal;
        }

        private static double ToDouble(object? value) => value switch
        {
            null => 0,
            // Widening a float directly drags its binary tail along (0.35f becomes
            // 0.3499999940395355). Go through decimal to get the number that was written.
            float f when !float.IsNaN(f) && !float.IsInfinity(f) && Math.Abs(f) < 1e28f => (double)(decimal)f,
            _ => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        };

        private static object FromDouble(double value, Type type)
        {
            if (IsInteger(type)) value = Math.Round(value, MidpointRounding.AwayFromZero);
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        /// <summary>About a hundred steps across the range, snapped to 1 / 2 / 5 × 10^n.</summary>
        internal static double NiceStep(double min, double max)
        {
            var raw = (max - min) / 100.0;
            if (!(raw > 0)) return 1;
            var magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            foreach (var m in new[] { 1.0, 2.0, 5.0, 10.0 })
            {
                if (m * magnitude >= raw - 1e-12) return m * magnitude;
            }
            return raw;
        }
    }
}
