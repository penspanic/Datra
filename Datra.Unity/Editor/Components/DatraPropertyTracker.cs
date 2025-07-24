using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Datra.Unity.Editor.Components
{
    /// <summary>
    /// Tracks property changes and stores original values for revert functionality
    /// </summary>
    public class DatraPropertyTracker
    {
        private Dictionary<string, PropertySnapshot> propertySnapshots = new Dictionary<string, PropertySnapshot>();
        private Dictionary<string, object> modifiedValues = new Dictionary<string, object>();
        
        public event Action<string, bool> OnPropertyModified;
        public event Action OnAnyPropertyModified;
        
        private class PropertySnapshot
        {
            public PropertyInfo Property { get; set; }
            public object OriginalValue { get; set; }
            public object Target { get; set; }
            public Type PropertyType { get; set; }
        }
        
        /// <summary>
        /// Initialize tracking for all writable properties of the target object
        /// </summary>
        public void StartTracking(object target, bool skipId = true)
        {
            if (target == null) return;
            
            propertySnapshots.Clear();
            modifiedValues.Clear();
            
            var properties = target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            
            foreach (var property in properties)
            {
                if (property.CanWrite && (!skipId || property.Name != "Id"))
                {
                    var key = GenerateKey(target, property.Name);
                    var currentValue = property.GetValue(target);
                    
                    propertySnapshots[key] = new PropertySnapshot
                    {
                        Property = property,
                        OriginalValue = CloneValue(currentValue),
                        Target = target,
                        PropertyType = property.PropertyType
                    };
                }
            }
        }
        
        /// <summary>
        /// Track a property change
        /// </summary>
        public void TrackChange(object target, string propertyName, object newValue)
        {
            var key = GenerateKey(target, propertyName);
            
            if (!propertySnapshots.ContainsKey(key))
                return;
                
            var snapshot = propertySnapshots[key];
            var isModified = !AreValuesEqual(snapshot.OriginalValue, newValue);
            
            if (isModified)
            {
                modifiedValues[key] = newValue;
            }
            else
            {
                modifiedValues.Remove(key);
            }
            
            OnPropertyModified?.Invoke(key, isModified);
            OnAnyPropertyModified?.Invoke();
        }
        
        /// <summary>
        /// Check if a specific property has been modified
        /// </summary>
        public bool IsPropertyModified(object target, string propertyName)
        {
            var key = GenerateKey(target, propertyName);
            return modifiedValues.ContainsKey(key);
        }
        
        /// <summary>
        /// Get the original value of a property
        /// </summary>
        public object GetOriginalValue(object target, string propertyName)
        {
            var key = GenerateKey(target, propertyName);
            return propertySnapshots.ContainsKey(key) ? propertySnapshots[key].OriginalValue : null;
        }
        
        /// <summary>
        /// Revert a specific property to its original value
        /// </summary>
        public void RevertProperty(object target, string propertyName)
        {
            var key = GenerateKey(target, propertyName);
            
            if (!propertySnapshots.ContainsKey(key))
                return;
                
            var snapshot = propertySnapshots[key];
            snapshot.Property.SetValue(snapshot.Target, CloneValue(snapshot.OriginalValue));
            modifiedValues.Remove(key);
            
            OnPropertyModified?.Invoke(key, false);
            OnAnyPropertyModified?.Invoke();
        }
        
        /// <summary>
        /// Revert all properties to their original values
        /// </summary>
        public void RevertAll()
        {
            foreach (var kvp in propertySnapshots)
            {
                var snapshot = kvp.Value;
                snapshot.Property.SetValue(snapshot.Target, CloneValue(snapshot.OriginalValue));
            }
            
            modifiedValues.Clear();
            
            foreach (var key in propertySnapshots.Keys)
            {
                OnPropertyModified?.Invoke(key, false);
            }
            
            OnAnyPropertyModified?.Invoke();
        }
        
        /// <summary>
        /// Check if any property has been modified
        /// </summary>
        public bool HasAnyModifications()
        {
            return modifiedValues.Count > 0;
        }
        
        /// <summary>
        /// Get a summary of all modifications
        /// </summary>
        public Dictionary<string, (object originalValue, object currentValue)> GetModificationSummary()
        {
            var summary = new Dictionary<string, (object, object)>();
            
            foreach (var kvp in modifiedValues)
            {
                if (propertySnapshots.ContainsKey(kvp.Key))
                {
                    var snapshot = propertySnapshots[kvp.Key];
                    summary[kvp.Key] = (snapshot.OriginalValue, kvp.Value);
                }
            }
            
            return summary;
        }
        
        /// <summary>
        /// Update the baseline (make current values the new originals)
        /// </summary>
        public void UpdateBaseline()
        {
            foreach (var kvp in propertySnapshots)
            {
                var snapshot = kvp.Value;
                var currentValue = snapshot.Property.GetValue(snapshot.Target);
                snapshot.OriginalValue = CloneValue(currentValue);
            }
            
            modifiedValues.Clear();
            
            foreach (var key in propertySnapshots.Keys)
            {
                OnPropertyModified?.Invoke(key, false);
            }
            
            OnAnyPropertyModified?.Invoke();
        }
        
        private string GenerateKey(object target, string propertyName)
        {
            return $"{target.GetHashCode()}_{propertyName}";
        }
        
        private object CloneValue(object value)
        {
            if (value == null) return null;
            
            var type = value.GetType();
            
            // For value types and strings, direct assignment works
            if (type.IsValueType || type == typeof(string))
            {
                return value;
            }
            
            // For reference types, we need proper cloning
            // This is a simple implementation - extend as needed
            if (value is ICloneable cloneable)
            {
                return cloneable.Clone();
            }
            
            // For now, just return the value
            // In a production system, you'd want deep cloning
            return value;
        }
        
        private bool AreValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;
            
            if (value1 is float f1 && value2 is float f2)
            {
                return Mathf.Approximately(f1, f2);
            }
            
            return value1.Equals(value2);
        }
    }
}