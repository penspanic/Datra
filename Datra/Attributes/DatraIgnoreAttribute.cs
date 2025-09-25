using System;

namespace Datra.Attributes
{
    /// <summary>
    /// Attribute to indicate that a property should be ignored by Datra UI components
    /// such as DatraTableView and DatraPropertyField in Unity Editor.
    /// This is useful for metadata properties or properties that should not be displayed
    /// in the editor UI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class DatraIgnoreAttribute : Attribute
    {
    }
}