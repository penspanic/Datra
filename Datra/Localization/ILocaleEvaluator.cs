#nullable enable
using Datra.DataTypes;

namespace Datra.Localization
{
    /// <summary>
    /// Interface for types that can evaluate nested locale references.
    /// Implement this on container types (like Graph) that know how to
    /// resolve hierarchical locale paths with runtime indices.
    /// </summary>
    /// <example>
    /// public class Graph : ILocaleEvaluator
    /// {
    ///     public List&lt;Node&gt; Nodes { get; set; }
    ///
    ///     public LocaleRef EvaluateNestedLocale(object rootId, NestedLocaleRef nested, params object[] context)
    ///     {
    ///         // Determine indices from context objects
    ///         var node = (Node)context[0];
    ///         var nodeIndex = Nodes.IndexOf(node);
    ///
    ///         return nested.Evaluate($"Graph.{rootId}", ("Nodes", nodeIndex));
    ///     }
    /// }
    /// </example>
    public interface ILocaleEvaluator
    {
        /// <summary>
        /// Evaluates a nested locale reference to a concrete LocaleRef.
        /// </summary>
        /// <param name="rootId">The root identifier (e.g., FileId, entity ID)</param>
        /// <param name="nested">The nested locale reference with path template</param>
        /// <param name="context">Context objects used to determine array indices (e.g., parent Node, Choice)</param>
        /// <returns>A LocaleRef with the fully resolved key including indices</returns>
        LocaleRef EvaluateNestedLocale(object rootId, NestedLocaleRef nested, params object[] context);
    }
}
