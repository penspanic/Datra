using System;
using Datra.Interfaces;

namespace Datra.DataTypes
{
    /// <summary>
    /// Represents a reference to a localization key that can be evaluated to get localized text
    /// </summary>
    public struct LocaleRef
    {
        /// <summary>
        /// The localization key (e.g., "Button_Start", "Character_Hero_Name")
        /// </summary>
        public string Key { get; set; }
        
        /// <summary>
        /// Indicates whether this reference has a valid key value
        /// </summary>
        public bool HasValue => !string.IsNullOrEmpty(Key);
        
        /// <summary>
        /// Evaluates the locale reference using the provided localization context
        /// </summary>
        public string Evaluate(ILocalizationContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
                
            if (string.IsNullOrEmpty(Key))
                return string.Empty;
                
            return context.GetText(Key);
        }
        
        /// <summary>
        /// Evaluates the locale reference using the provided localization service
        /// </summary>
        public string Evaluate(ILocalizationService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));
                
            if (string.IsNullOrEmpty(Key))
                return string.Empty;
                
            return service.Localize(Key);
        }
        
        /// <summary>
        /// Implicit conversion from string key
        /// </summary>
        public static implicit operator LocaleRef(string key)
        {
            return new LocaleRef { Key = key };
        }
        
        public override string ToString()
        {
            return Key ?? string.Empty;
        }
        
        public override bool Equals(object obj)
        {
            if (obj is LocaleRef other)
            {
                return string.Equals(Key, other.Key, StringComparison.Ordinal);
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return Key?.GetHashCode() ?? 0;
        }
        
        public static bool operator ==(LocaleRef left, LocaleRef right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(LocaleRef left, LocaleRef right)
        {
            return !left.Equals(right);
        }
    }
}