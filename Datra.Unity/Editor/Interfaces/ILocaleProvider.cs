using System;
using Datra.DataTypes;
using UnityEngine;

namespace Datra.Unity.Editor.Interfaces
{
    /// <summary>
    /// Interface for providing locale data and editing capabilities to UI components.
    /// Allows components like DatraPropertyField to request locale operations without
    /// directly depending on LocalizationContext.
    /// </summary>
    public interface ILocaleProvider
    {
        /// <summary>
        /// Gets the localized text for a LocaleRef in the current language
        /// </summary>
        string GetLocaleText(LocaleRef localeRef);

        /// <summary>
        /// Shows a popup to edit the locale across all languages
        /// </summary>
        /// <param name="localeRef">The locale reference to edit</param>
        /// <param name="buttonWorldBound">The world bounds of the button that triggered the popup</param>
        /// <param name="onUpdated">Callback invoked when the text is updated (receives updated text in current language)</param>
        void ShowLocaleEditPopup(LocaleRef localeRef, Rect buttonWorldBound, Action<string> onUpdated);
    }
}
