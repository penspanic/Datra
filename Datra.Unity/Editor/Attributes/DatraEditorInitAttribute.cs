using System;

namespace Datra.Unity.Editor.Attributes
{
    /// <summary>
    /// Marks a static method as the Datra editor initialization method.
    /// This method will be called by DatraEditorWindow to bootstrap the DataContext.
    /// The method must be static and return an IDataContext instance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class DatraEditorInitAttribute : Attribute
    {
        /// <summary>
        /// Display name for this initializer in the editor
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// Priority for this initializer (higher values are preferred)
        /// </summary>
        public int Priority { get; set; }
        
        public DatraEditorInitAttribute(string displayName = null, int priority = 0)
        {
            DisplayName = displayName;
            Priority = priority;
        }
    }
}