#if UNITY_2020_3_OR_NEWER
using UnityEngine;

namespace Datra.Attributes
{
    /// <summary>
    /// Marks a string field as a localization key.
    /// When applied, the field will show a key selector in the Unity Inspector
    /// that allows selecting from available localization keys.
    /// </summary>
    public class LocaleKeyAttribute : PropertyAttribute
    {
    }
}
#endif
