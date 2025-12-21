using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Serialization;

namespace Datra.Serializers
{
    /// <summary>
    /// Custom SerializationBinder that uses type names without assembly qualification.
    /// This enables JSON portability across different assembly configurations (e.g., Unity vs .NET).
    /// </summary>
    public class PortableTypeBinder : ISerializationBinder
    {
        private readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private readonly object _lock = new object();

        /// <summary>
        /// Binds a type name to a Type during deserialization.
        /// Searches all loaded assemblies if the type is not in cache.
        /// </summary>
        public Type BindToType(string? assemblyName, string typeName)
        {
            // Use full type name as key (ignore assembly name for portability)
            var key = typeName;

            lock (_lock)
            {
                if (_typeCache.TryGetValue(key, out var cachedType))
                {
                    return cachedType;
                }
            }

            // Try to find the type in all loaded assemblies
            Type? resolvedType = null;

            // First, try with the provided assembly name if it exists
            if (!string.IsNullOrEmpty(assemblyName))
            {
                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    resolvedType = assembly?.GetType(typeName);
                }
                catch
                {
                    // Assembly not found, will search all assemblies
                }
            }

            // Search in all loaded assemblies
            if (resolvedType == null)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        resolvedType = assembly.GetType(typeName);
                        if (resolvedType != null)
                            break;
                    }
                    catch
                    {
                        // Skip assemblies that throw
                    }
                }
            }

            // Try to find by simple name (last part of namespace.typename)
            if (resolvedType == null)
            {
                var simpleTypeName = typeName.Split('.').Last();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        resolvedType = assembly.GetTypes()
                            .FirstOrDefault(t => t.Name == simpleTypeName && t.FullName == typeName);
                        if (resolvedType != null)
                            break;
                    }
                    catch
                    {
                        // Skip assemblies that throw on GetTypes()
                    }
                }
            }

            if (resolvedType != null)
            {
                lock (_lock)
                {
                    _typeCache[key] = resolvedType;
                }
            }

            return resolvedType ?? throw new TypeLoadException($"Could not resolve type: {typeName}");
        }

        /// <summary>
        /// Gets the type name and assembly name for serialization.
        /// Only outputs the full type name (no assembly) for portability.
        /// </summary>
        public void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
        {
            // Don't include assembly name for portability
            assemblyName = null;
            typeName = serializedType.FullName;
        }
    }
}
