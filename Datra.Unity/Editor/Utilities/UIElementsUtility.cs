#nullable disable
using UnityEngine.UIElements;

namespace Datra.Unity.Editor.Utilities
{
    public static class UIElementsUtility
    {
        public static void SetPadding(this IStyle style, StyleLength length)
        {
            style.paddingLeft = length;
            style.paddingRight = length;
            style.paddingTop = length;
            style.paddingBottom = length;
        }
    }
}