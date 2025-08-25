using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Datra.Interfaces;
using Datra.Unity.Editor.Attributes;

namespace Datra.Unity.Editor.Utilities
{
    /// <summary>
    /// Handles discovery and execution of Datra initialization methods
    /// </summary>
    public static class DatraBootstrapper
    {
        private static List<InitializerInfo> _cachedInitializers;
        private static IDataContext _currentDataContext;
        
        public class InitializerInfo
        {
            public MethodInfo Method { get; set; }
            public DatraEditorInitAttribute Attribute { get; set; }
            public string DisplayName => Attribute?.DisplayName ?? Method.DeclaringType.Name + "." + Method.Name;
        }
        
        /// <summary>
        /// Find all methods marked with [DatraEditorInit] attribute
        /// </summary>
        public static List<InitializerInfo> FindInitializers(bool forceRefresh = false)
        {
            if (_cachedInitializers != null && !forceRefresh)
            {
                return _cachedInitializers;
            }
            
            _cachedInitializers = new List<InitializerInfo>();
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            
            foreach (var assembly in assemblies)
            {
                try
                {
                    var types = assembly.GetTypes();
                    
                    foreach (var type in types)
                    {
                        var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                        
                        foreach (var method in methods)
                        {
                            var attribute = method.GetCustomAttribute<DatraEditorInitAttribute>();
                            if (attribute != null)
                            {
                                // Validate method signature
                                if (!ValidateInitializerMethod(method))
                                {
                                    Debug.LogWarning($"[Datra] Method {type.Name}.{method.Name} has [DatraEditorInit] but invalid signature. " +
                                                   "Must be static and return IDataContext.");
                                    continue;
                                }
                                
                                _cachedInitializers.Add(new InitializerInfo
                                {
                                    Method = method,
                                    Attribute = attribute
                                });
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Datra] Error scanning assembly {assembly.GetName().Name}: {e.Message}\nStackTrace: {e.StackTrace}");
                }
            }
            
            // Sort by priority (descending)
            _cachedInitializers = _cachedInitializers.OrderByDescending(i => i.Attribute.Priority).ToList();
            
            return _cachedInitializers;
        }
        
        /// <summary>
        /// Execute an initializer method and return the DataContext
        /// </summary>
        public static IDataContext ExecuteInitializer(InitializerInfo initializer)
        {
            try
            {
                var result = initializer.Method.Invoke(null, null);
                
                if (result is IDataContext dataContext)
                {
                    Debug.Log($"[Datra] Successfully initialized DataContext using {initializer.DisplayName}");
                    _currentDataContext = dataContext;
                    return dataContext;
                }
                else
                {
                    Debug.LogError($"[Datra] Initializer {initializer.DisplayName} did not return an IDataContext");
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Datra] Failed to execute initializer {initializer.DisplayName}: {e.Message}\nStackTrace: {e.StackTrace}");
                if (e.InnerException != null)
                {
                    Debug.LogError($"[Datra] Inner exception: {e.InnerException.Message}\nInner StackTrace: {e.InnerException.StackTrace}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// Try to find and execute the best available initializer
        /// </summary>
        public static IDataContext AutoInitialize()
        {
            var initializers = FindInitializers();
            
            if (initializers.Count == 0)
            {
                Debug.LogWarning("[Datra] No methods with [DatraEditorInit] attribute found");
                return null;
            }
            
            // Try initializers in priority order
            foreach (var initializer in initializers)
            {
                var dataContext = ExecuteInitializer(initializer);
                if (dataContext != null)
                {
                    return dataContext;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Get the currently initialized DataContext
        /// </summary>
        public static IDataContext GetCurrentDataContext()
        {
            if (_currentDataContext == null)
            {
                _currentDataContext = AutoInitialize();
            }
            return _currentDataContext;
        }
        
        /// <summary>
        /// Clear the current DataContext
        /// </summary>
        public static void ClearCurrentDataContext()
        {
            _currentDataContext = null;
        }
        
        private static bool ValidateInitializerMethod(MethodInfo method)
        {
            // Must be static
            if (!method.IsStatic)
            {
                return false;
            }
            
            // Must have no parameters
            if (method.GetParameters().Length > 0)
            {
                return false;
            }
            
            // Must return IDataContext
            return typeof(IDataContext).IsAssignableFrom(method.ReturnType);
        }
    }
}