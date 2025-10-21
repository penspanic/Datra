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
        /// Creates a fixed locale key following the pattern: TypeName.Id.PropertyName
        /// </summary>
        /// <param name="typeName">The type name (e.g., "ItemInfo", "CharacterInfo")</param>
        /// <param name="id">The entity ID (e.g., "sword_001", "hero")</param>
        /// <param name="propertyName">The property name (e.g., "Name", "Desc")</param>
        /// <returns>A LocaleRef with the generated key</returns>
        public static LocaleRef CreateFixed(string typeName, string id, string propertyName)
        {
            return new LocaleRef { Key = $"{typeName}.{id}.{propertyName.ToLower()}" };
        }

        /// <summary>
        /// Creates a fixed locale key using the type parameter
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="id">The entity ID</param>
        /// <param name="propertyName">The property name</param>
        /// <returns>A LocaleRef with the generated key</returns>
        public static LocaleRef CreateFixed<T>(string id, string propertyName)
        {
            return CreateFixed(typeof(T).Name, id, propertyName);
        }

        /// <summary>
        /// Creates a nested locale key with hierarchical path (e.g., "Graph.Nodes.Name")
        /// </summary>
        /// <param name="path">The path segments</param>
        /// <returns>A LocaleRef with the joined path</returns>
        public static LocaleRef CreateNested(params string[] path)
        {
            return new LocaleRef { Key = string.Join(".", path) };
        }
        
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