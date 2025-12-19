#nullable enable
using Datra.DataTypes;

namespace Datra.Localization
{
    /// <summary>
    /// Extension methods for Asset&lt;T&gt; to support locale evaluation.
    /// </summary>
    public static class AssetLocaleExtensions
    {
        /// <summary>
        /// Evaluates a nested locale reference using the asset's data as the evaluator.
        /// </summary>
        /// <typeparam name="T">The asset data type that implements ILocaleEvaluator</typeparam>
        /// <param name="asset">The asset containing the data</param>
        /// <param name="nested">The nested locale reference to evaluate</param>
        /// <param name="context">Context objects for determining array indices</param>
        /// <returns>A LocaleRef with the fully resolved key</returns>
        public static LocaleRef EvaluateLocale<T>(this Asset<T> asset, NestedLocaleRef nested, params object[] context)
            where T : class, ILocaleEvaluator
        {
            return asset.Data.EvaluateNestedLocale(asset.Id, nested, context);
        }
    }
}
