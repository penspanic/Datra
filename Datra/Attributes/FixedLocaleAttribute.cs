using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Indicates that a locale property has a fixed (non-editable) key.
    /// The locale key cannot be modified, but the locale values (translations) can still be edited.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class FixedLocaleAttribute : Attribute
    {
    }
}
