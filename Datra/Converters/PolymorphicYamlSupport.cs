#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Datra.Converters
{
    /// <summary>
    /// Resolves type names to Type objects, similar to PortableTypeBinder for JSON.
    /// Enables YAML portability across different assembly configurations.
    /// </summary>
    public class PortableTypeResolver
    {
        private readonly Dictionary<string, Type> _typeCache = new();
        private readonly object _lock = new();

        /// <summary>
        /// Resolves a type name to a Type object.
        /// Searches all loaded assemblies if necessary.
        /// </summary>
        public Type? ResolveType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;

            lock (_lock)
            {
                if (_typeCache.TryGetValue(typeName, out var cachedType))
                    return cachedType;
            }

            Type? resolvedType = null;

            // Search in all loaded assemblies
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
                    _typeCache[typeName] = resolvedType;
                }
            }

            return resolvedType;
        }

        /// <summary>
        /// Gets the portable type name (full name without assembly qualification).
        /// </summary>
        public string GetTypeName(Type type)
        {
            return type.FullName ?? type.Name;
        }
    }

    /// <summary>
    /// Custom node deserializer that handles $type field for polymorphic deserialization.
    /// </summary>
    public class PolymorphicNodeDeserializer : INodeDeserializer
    {
        private readonly INodeDeserializer _innerDeserializer;
        private readonly PortableTypeResolver _typeResolver;
        private readonly HashSet<Type> _polymorphicBaseTypes;

        public const string TypeFieldName = "$type";

        public PolymorphicNodeDeserializer(
            INodeDeserializer innerDeserializer,
            PortableTypeResolver typeResolver,
            HashSet<Type>? polymorphicBaseTypes = null)
        {
            _innerDeserializer = innerDeserializer;
            _typeResolver = typeResolver;
            _polymorphicBaseTypes = polymorphicBaseTypes ?? new HashSet<Type>();
        }

        public bool Deserialize(
            IParser reader,
            Type expectedType,
            Func<IParser, Type, object?> nestedObjectDeserializer,
            out object? value,
            ObjectDeserializer rootDeserializer)
        {
            // Check if we should handle this type polymorphically
            if (!ShouldHandlePolymorphically(expectedType))
            {
                return _innerDeserializer.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
            }

            // Check if this is a mapping (object)
            if (!reader.Accept<MappingStart>(out _))
            {
                return _innerDeserializer.Deserialize(reader, expectedType, nestedObjectDeserializer, out value, rootDeserializer);
            }

            // Read the mapping into a dictionary first to find $type
            var mapping = ReadMappingWithTypeField(reader, out var typeFieldValue);

            if (string.IsNullOrEmpty(typeFieldValue))
            {
                // No $type field, use default deserialization
                // We need to reconstruct the YAML for the inner deserializer
                value = DeserializeFromDictionary(mapping, expectedType, rootDeserializer);
                return true;
            }

            // Resolve the actual type
            var actualType = _typeResolver.ResolveType(typeFieldValue);
            if (actualType == null)
            {
                throw new YamlException($"Could not resolve type: {typeFieldValue}");
            }

            // Deserialize as the actual type
            value = DeserializeFromDictionary(mapping, actualType, rootDeserializer);
            return true;
        }

        private bool ShouldHandlePolymorphically(Type type)
        {
            // Handle if explicitly registered
            if (_polymorphicBaseTypes.Contains(type))
                return true;

            // Handle abstract classes and interfaces
            if (type.IsAbstract || type.IsInterface)
                return true;

            // Check if any base type is registered
            var baseType = type.BaseType;
            while (baseType != null)
            {
                if (_polymorphicBaseTypes.Contains(baseType))
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }

        private Dictionary<string, object?> ReadMappingWithTypeField(IParser reader, out string? typeFieldValue)
        {
            typeFieldValue = null;
            var result = new Dictionary<string, object?>();

            reader.Consume<MappingStart>();

            while (!reader.Accept<MappingEnd>(out _))
            {
                var keyScalar = reader.Consume<Scalar>();
                var key = keyScalar.Value;

                if (key == TypeFieldName)
                {
                    var valueScalar = reader.Consume<Scalar>();
                    typeFieldValue = valueScalar.Value;
                }
                else
                {
                    // Read the value as a generic node
                    var nodeValue = ReadNode(reader);
                    result[key] = nodeValue;
                }
            }

            reader.Consume<MappingEnd>();

            return result;
        }

        private object? ReadNode(IParser reader)
        {
            if (reader.Accept<Scalar>(out var scalar))
            {
                reader.MoveNext();
                return scalar.Value;
            }

            if (reader.Accept<SequenceStart>(out _))
            {
                return ReadSequence(reader);
            }

            if (reader.Accept<MappingStart>(out _))
            {
                return ReadMapping(reader);
            }

            // Skip unknown node types
            reader.MoveNext();
            return null;
        }

        private List<object?> ReadSequence(IParser reader)
        {
            var result = new List<object?>();
            reader.Consume<SequenceStart>();

            while (!reader.Accept<SequenceEnd>(out _))
            {
                result.Add(ReadNode(reader));
            }

            reader.Consume<SequenceEnd>();
            return result;
        }

        private Dictionary<string, object?> ReadMapping(IParser reader)
        {
            var result = new Dictionary<string, object?>();
            reader.Consume<MappingStart>();

            while (!reader.Accept<MappingEnd>(out _))
            {
                var keyScalar = reader.Consume<Scalar>();
                var key = keyScalar.Value;
                var value = ReadNode(reader);
                result[key] = value;
            }

            reader.Consume<MappingEnd>();
            return result;
        }

        private object? DeserializeFromDictionary(Dictionary<string, object?> dict, Type targetType, ObjectDeserializer rootDeserializer)
        {
            var instance = Activator.CreateInstance(targetType);
            if (instance == null)
                return null;

            var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in dict)
            {
                if (!properties.TryGetValue(kvp.Key, out var property))
                    continue;

                var value = ConvertValue(kvp.Value, property.PropertyType, rootDeserializer);
                property.SetValue(instance, value);
            }

            return instance;
        }

        private object? ConvertValue(object? value, Type targetType, ObjectDeserializer rootDeserializer)
        {
            if (value == null)
                return GetDefaultValue(targetType);

            // Handle string values
            if (value is string stringValue)
            {
                return ConvertStringValue(stringValue, targetType);
            }

            // Handle nested dictionaries (objects with potential $type)
            if (value is Dictionary<string, object?> dict)
            {
                // Check for $type field
                if (dict.TryGetValue(TypeFieldName, out var typeFieldObj) && typeFieldObj is string typeName)
                {
                    dict.Remove(TypeFieldName);
                    var actualType = _typeResolver.ResolveType(typeName);
                    if (actualType != null)
                    {
                        return DeserializeFromDictionary(dict, actualType, rootDeserializer);
                    }
                }
                return DeserializeFromDictionary(dict, targetType, rootDeserializer);
            }

            // Handle lists
            if (value is List<object?> list)
            {
                return ConvertList(list, targetType, rootDeserializer);
            }

            return Convert.ChangeType(value, targetType);
        }

        private object? ConvertStringValue(string stringValue, Type targetType)
        {
            if (targetType == typeof(string))
                return stringValue;

            if (targetType.IsEnum)
                return Enum.Parse(targetType, stringValue, ignoreCase: true);

            if (targetType == typeof(int))
                return int.Parse(stringValue);

            if (targetType == typeof(long))
                return long.Parse(stringValue);

            if (targetType == typeof(float))
                return float.Parse(stringValue);

            if (targetType == typeof(double))
                return double.Parse(stringValue);

            if (targetType == typeof(bool))
                return bool.Parse(stringValue);

            if (targetType == typeof(decimal))
                return decimal.Parse(stringValue);

            // Nullable types
            var underlyingType = Nullable.GetUnderlyingType(targetType);
            if (underlyingType != null)
            {
                if (string.IsNullOrEmpty(stringValue))
                    return null;
                return ConvertStringValue(stringValue, underlyingType);
            }

            return Convert.ChangeType(stringValue, targetType);
        }

        private object? ConvertList(List<object?> list, Type targetType, ObjectDeserializer rootDeserializer)
        {
            Type elementType;

            if (targetType.IsArray)
            {
                elementType = targetType.GetElementType()!;
                var array = Array.CreateInstance(elementType, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    array.SetValue(ConvertValue(list[i], elementType, rootDeserializer), i);
                }
                return array;
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = targetType.GetGenericArguments()[0];
                var typedList = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                foreach (var item in list)
                {
                    typedList.Add(ConvertValue(item, elementType, rootDeserializer));
                }
                return typedList;
            }

            return list;
        }

        private static object? GetDefaultValue(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }

    /// <summary>
    /// Type converter for polymorphic types that emits $type field.
    /// </summary>
    public class PolymorphicYamlTypeConverter : IYamlTypeConverter
    {
        private readonly PortableTypeResolver _typeResolver;
        private readonly HashSet<Type> _polymorphicBaseTypes;
        private readonly HashSet<Type> _excludedTypes;

        public const string TypeFieldName = "$type";

        public PolymorphicYamlTypeConverter(PortableTypeResolver typeResolver, HashSet<Type>? polymorphicBaseTypes = null)
            : this(typeResolver, polymorphicBaseTypes, excludedTypes: null)
        {
        }

        /// <summary>
        /// Creates a new PolymorphicYamlTypeConverter.
        /// </summary>
        /// <param name="typeResolver">Type resolver for resolving $type values.</param>
        /// <param name="polymorphicBaseTypes">Base types that require $type field for polymorphism.</param>
        /// <param name="excludedTypes">Types that should be excluded from polymorphic handling (handled by custom converters).</param>
        public PolymorphicYamlTypeConverter(
            PortableTypeResolver typeResolver,
            HashSet<Type>? polymorphicBaseTypes,
            HashSet<Type>? excludedTypes)
        {
            _typeResolver = typeResolver;
            _polymorphicBaseTypes = polymorphicBaseTypes ?? new HashSet<Type>();
            _excludedTypes = excludedTypes ?? new HashSet<Type>();
        }

        public bool Accepts(Type type)
        {
            // Accept abstract classes and interfaces that are registered
            if (_polymorphicBaseTypes.Contains(type))
                return true;

            // Check if any base type is registered
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (_polymorphicBaseTypes.Contains(baseType))
                    return true;
                baseType = baseType.BaseType;
            }

            // Accept List<T> where T is a polymorphic type (for proper serialization)
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                if (IsPolymorphicType(elementType))
                    return true;
            }

            // Accept arrays of polymorphic types
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                if (IsPolymorphicType(elementType))
                    return true;
            }

            // Accept Dictionary<TKey, TValue> where TValue has polymorphic properties
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var valueType = type.GetGenericArguments()[1];
                if (IsPolymorphicType(valueType) || HasPolymorphicProperties(valueType))
                    return true;
            }

            // Accept classes that have polymorphic properties
            if (type.IsClass && !type.IsAbstract && type != typeof(string))
            {
                if (HasPolymorphicProperties(type))
                    return true;
            }

            return false;
        }

        private bool HasPolymorphicProperties(Type type)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var propType = property.PropertyType;

                // Check if property type is a polymorphic list
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = propType.GetGenericArguments()[0];
                    if (IsPolymorphicType(elementType))
                        return true;
                }

                // Check if property type is a polymorphic array
                if (propType.IsArray)
                {
                    var elementType = propType.GetElementType()!;
                    if (IsPolymorphicType(elementType))
                        return true;
                }

                // Check if property type is a dictionary with polymorphic values
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var valueType = propType.GetGenericArguments()[1];
                    if (IsPolymorphicType(valueType) || HasPolymorphicPropertiesRecursive(valueType, new HashSet<Type> { type }))
                        return true;
                }

                // Check if property type itself is polymorphic
                if (IsPolymorphicType(propType))
                    return true;
            }
            return false;
        }

        private bool HasPolymorphicPropertiesRecursive(Type type, HashSet<Type> visited)
        {
            if (visited.Contains(type))
                return false;
            visited.Add(type);

            if (!type.IsClass || type == typeof(string))
                return false;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var property in properties)
            {
                var propType = property.PropertyType;

                if (IsPolymorphicType(propType))
                    return true;

                // Check nested lists
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = propType.GetGenericArguments()[0];
                    if (IsPolymorphicType(elementType))
                        return true;
                }

                // Check nested dictionaries
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var valueType = propType.GetGenericArguments()[1];
                    if (IsPolymorphicType(valueType) || HasPolymorphicPropertiesRecursive(valueType, visited))
                        return true;
                }
            }
            return false;
        }

        private bool IsPolymorphicType(Type type)
        {
            // Check if the type is excluded (handled by custom converters)
            if (_excludedTypes.Contains(type))
                return false;

            // Check if any base type or interface is excluded
            foreach (var excludedType in _excludedTypes)
            {
                if (excludedType.IsAssignableFrom(type))
                    return false;
            }

            if (_polymorphicBaseTypes.Contains(type))
                return true;

            if (type.IsAbstract || type.IsInterface)
                return true;

            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                if (_polymorphicBaseTypes.Contains(baseType))
                    return true;
                baseType = baseType.BaseType;
            }

            return false;
        }

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            // Handle List<T> where T is polymorphic
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                return ReadPolymorphicList(parser, type, elementType, rootDeserializer);
            }

            // Handle arrays of polymorphic types
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                return ReadPolymorphicArray(parser, elementType, rootDeserializer);
            }

            // Handle Dictionary<TKey, TValue> where TValue has polymorphic properties
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var keyType = type.GetGenericArguments()[0];
                var valueType = type.GetGenericArguments()[1];
                return ReadPolymorphicDictionary(parser, type, keyType, valueType, rootDeserializer);
            }

            if (!parser.Accept<MappingStart>(out _))
            {
                throw new YamlException($"Expected mapping for polymorphic type {type}");
            }

            // Read the mapping to find $type
            var mapping = new Dictionary<string, object?>();
            string? typeFieldValue = null;

            parser.Consume<MappingStart>();

            while (!parser.Accept<MappingEnd>(out _))
            {
                var keyScalar = parser.Consume<Scalar>();
                var key = keyScalar.Value;

                if (key == TypeFieldName)
                {
                    var valueScalar = parser.Consume<Scalar>();
                    typeFieldValue = valueScalar.Value;
                }
                else
                {
                    // Use root deserializer for nested objects
                    var nodeValue = ReadNodeValue(parser, rootDeserializer);
                    mapping[key] = nodeValue;
                }
            }

            parser.Consume<MappingEnd>();

            // Determine actual type
            Type actualType;
            if (!string.IsNullOrEmpty(typeFieldValue))
            {
                actualType = _typeResolver.ResolveType(typeFieldValue)
                    ?? throw new YamlException($"Could not resolve type: {typeFieldValue}");
            }
            else
            {
                // For abstract types without $type, we cannot create an instance
                if (type.IsAbstract || type.IsInterface)
                {
                    throw new YamlException($"Cannot deserialize abstract type {type.Name} without $type field");
                }
                actualType = type;
            }

            // Create and populate instance with property type awareness
            return CreateInstanceWithPropertyTypes(actualType, mapping, rootDeserializer);
        }

        private object? ReadPolymorphicList(IParser parser, Type listType, Type elementType, ObjectDeserializer rootDeserializer)
        {
            var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

            if (!parser.Accept<SequenceStart>(out _))
            {
                throw new YamlException($"Expected sequence for list type {listType}");
            }

            parser.Consume<SequenceStart>();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                var element = ReadPolymorphicElement(parser, elementType, rootDeserializer);
                list.Add(element);
            }

            parser.Consume<SequenceEnd>();
            return list;
        }

        private object? ReadPolymorphicArray(IParser parser, Type elementType, ObjectDeserializer rootDeserializer)
        {
            var tempList = new List<object?>();

            if (!parser.Accept<SequenceStart>(out _))
            {
                throw new YamlException($"Expected sequence for array type {elementType}[]");
            }

            parser.Consume<SequenceStart>();

            while (!parser.Accept<SequenceEnd>(out _))
            {
                var element = ReadPolymorphicElement(parser, elementType, rootDeserializer);
                tempList.Add(element);
            }

            parser.Consume<SequenceEnd>();

            var array = Array.CreateInstance(elementType, tempList.Count);
            for (int i = 0; i < tempList.Count; i++)
            {
                array.SetValue(tempList[i], i);
            }
            return array;
        }

        private object? ReadPolymorphicDictionary(IParser parser, Type dictType, Type keyType, Type valueType, ObjectDeserializer rootDeserializer)
        {
            var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;

            if (!parser.Accept<MappingStart>(out _))
            {
                throw new YamlException($"Expected mapping for dictionary type {dictType}");
            }

            parser.Consume<MappingStart>();

            while (!parser.Accept<MappingEnd>(out _))
            {
                // Read key (typically string)
                var keyScalar = parser.Consume<Scalar>();
                var key = ConvertString(keyScalar.Value, keyType);

                // Read value - could be polymorphic
                object? value;
                if (parser.Accept<MappingStart>(out _))
                {
                    // Value is an object, read it with polymorphism support
                    var mapping = new Dictionary<string, object?>();
                    string? typeFieldValue = null;

                    parser.Consume<MappingStart>();

                    while (!parser.Accept<MappingEnd>(out _))
                    {
                        var propKeyScalar = parser.Consume<Scalar>();
                        var propKey = propKeyScalar.Value;

                        if (propKey == TypeFieldName)
                        {
                            var valueScalar = parser.Consume<Scalar>();
                            typeFieldValue = valueScalar.Value;
                        }
                        else
                        {
                            var nodeValue = ReadNodeValue(parser, rootDeserializer);
                            mapping[propKey] = nodeValue;
                        }
                    }

                    parser.Consume<MappingEnd>();

                    // Determine actual type for the value
                    Type actualValueType = valueType;
                    if (!string.IsNullOrEmpty(typeFieldValue))
                    {
                        actualValueType = _typeResolver.ResolveType(typeFieldValue) ?? valueType;
                    }
                    else if (valueType.IsAbstract || valueType.IsInterface)
                    {
                        throw new YamlException($"Cannot deserialize abstract type {valueType.Name} without $type field");
                    }

                    value = CreateInstanceWithPropertyTypes(actualValueType, mapping, rootDeserializer);
                }
                else if (parser.Accept<Scalar>(out var valueScalar))
                {
                    parser.MoveNext();
                    value = ConvertString(valueScalar.Value, valueType);
                }
                else if (parser.Accept<SequenceStart>(out _))
                {
                    // Value is a list
                    if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var elementType = valueType.GetGenericArguments()[0];
                        value = ReadPolymorphicList(parser, valueType, elementType, rootDeserializer);
                    }
                    else
                    {
                        // Skip the sequence
                        var depth = 0;
                        parser.Consume<SequenceStart>();
                        depth++;
                        while (depth > 0)
                        {
                            if (parser.Accept<SequenceStart>(out _))
                            {
                                parser.MoveNext();
                                depth++;
                            }
                            else if (parser.Accept<SequenceEnd>(out _))
                            {
                                parser.MoveNext();
                                depth--;
                            }
                            else
                            {
                                parser.MoveNext();
                            }
                        }
                        value = null;
                    }
                }
                else
                {
                    parser.MoveNext();
                    value = null;
                }

                dict[key!] = value;
            }

            parser.Consume<MappingEnd>();
            return dict;
        }

        private object? ReadPolymorphicElement(IParser parser, Type elementType, ObjectDeserializer rootDeserializer)
        {
            if (!parser.Accept<MappingStart>(out _))
            {
                // Not a mapping, try to deserialize as simple value
                if (parser.Accept<Scalar>(out var scalar))
                {
                    parser.MoveNext();
                    return ConvertString(scalar.Value, elementType);
                }
                throw new YamlException($"Expected mapping or scalar for polymorphic element");
            }

            // Read the mapping
            var mapping = new Dictionary<string, object?>();
            string? typeFieldValue = null;

            parser.Consume<MappingStart>();

            while (!parser.Accept<MappingEnd>(out _))
            {
                var keyScalar = parser.Consume<Scalar>();
                var key = keyScalar.Value;

                if (key == TypeFieldName)
                {
                    var valueScalar = parser.Consume<Scalar>();
                    typeFieldValue = valueScalar.Value;
                }
                else
                {
                    var nodeValue = ReadNodeValue(parser, rootDeserializer);
                    mapping[key] = nodeValue;
                }
            }

            parser.Consume<MappingEnd>();

            // Determine actual type
            Type actualType;
            if (!string.IsNullOrEmpty(typeFieldValue))
            {
                actualType = _typeResolver.ResolveType(typeFieldValue)
                    ?? throw new YamlException($"Could not resolve type: {typeFieldValue}");
            }
            else
            {
                actualType = elementType;
            }

            return CreateInstance(actualType, mapping, rootDeserializer);
        }

        private object? ReadNodeValue(IParser parser, ObjectDeserializer rootDeserializer)
        {
            if (parser.Accept<Scalar>(out var scalar))
            {
                parser.MoveNext();
                return scalar.Value;
            }

            if (parser.Accept<SequenceStart>(out _))
            {
                var list = new List<object?>();
                parser.Consume<SequenceStart>();
                while (!parser.Accept<SequenceEnd>(out _))
                {
                    list.Add(ReadNodeValue(parser, rootDeserializer));
                }
                parser.Consume<SequenceEnd>();
                return list;
            }

            if (parser.Accept<MappingStart>(out _))
            {
                var dict = new Dictionary<string, object?>();
                parser.Consume<MappingStart>();
                while (!parser.Accept<MappingEnd>(out _))
                {
                    var key = parser.Consume<Scalar>().Value;
                    dict[key] = ReadNodeValue(parser, rootDeserializer);
                }
                parser.Consume<MappingEnd>();
                return dict;
            }

            parser.MoveNext();
            return null;
        }

        private object? CreateInstance(Type type, Dictionary<string, object?> mapping, ObjectDeserializer rootDeserializer)
        {
            var instance = Activator.CreateInstance(type);
            if (instance == null)
                return null;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in mapping)
            {
                if (!properties.TryGetValue(kvp.Key, out var property))
                    continue;

                var value = ConvertToPropertyType(kvp.Value, property.PropertyType, rootDeserializer);
                property.SetValue(instance, value);
            }

            return instance;
        }

        private object? CreateInstanceWithPropertyTypes(Type type, Dictionary<string, object?> mapping, ObjectDeserializer rootDeserializer)
        {
            var instance = Activator.CreateInstance(type);
            if (instance == null)
                return null;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

            foreach (var kvp in mapping)
            {
                if (!properties.TryGetValue(kvp.Key, out var property))
                    continue;

                var value = ConvertToPropertyTypeWithPolymorphism(kvp.Value, property.PropertyType, rootDeserializer);
                property.SetValue(instance, value);
            }

            return instance;
        }

        private object? ConvertToPropertyTypeWithPolymorphism(object? value, Type targetType, ObjectDeserializer rootDeserializer)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (value is string str)
            {
                return ConvertString(str, targetType);
            }

            // Handle polymorphic lists
            if (value is List<object?> list)
            {
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = targetType.GetGenericArguments()[0];
                    return ConvertPolymorphicList(list, targetType, elementType, rootDeserializer);
                }
                if (targetType.IsArray)
                {
                    var elementType = targetType.GetElementType()!;
                    return ConvertPolymorphicArray(list, elementType, rootDeserializer);
                }
            }

            // Handle nested objects
            if (value is Dictionary<string, object?> dict)
            {
                // Check if targetType is a Dictionary - convert nested dict to typed Dictionary
                if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var keyType = targetType.GetGenericArguments()[0];
                    var valueType = targetType.GetGenericArguments()[1];
                    return ConvertPolymorphicDictionary(dict, targetType, keyType, valueType, rootDeserializer);
                }

                // Check for nested $type
                if (dict.TryGetValue(TypeFieldName, out var typeVal) && typeVal is string typeName)
                {
                    dict.Remove(TypeFieldName);
                    var nestedType = _typeResolver.ResolveType(typeName) ?? targetType;
                    return CreateInstanceWithPropertyTypes(nestedType, dict, rootDeserializer);
                }

                if (targetType.IsAbstract || targetType.IsInterface)
                {
                    throw new YamlException($"Cannot deserialize abstract type {targetType.Name} without $type field");
                }

                return CreateInstanceWithPropertyTypes(targetType, dict, rootDeserializer);
            }

            return Convert.ChangeType(value, targetType);
        }

        private object? ConvertPolymorphicList(List<object?> list, Type listType, Type elementType, ObjectDeserializer rootDeserializer)
        {
            var typedList = (System.Collections.IList)Activator.CreateInstance(listType)!;

            foreach (var item in list)
            {
                if (item == null)
                {
                    typedList.Add(null);
                    continue;
                }

                if (item is Dictionary<string, object?> dict)
                {
                    // Check for $type
                    Type actualType = elementType;
                    if (dict.TryGetValue(TypeFieldName, out var typeVal) && typeVal is string typeName)
                    {
                        dict.Remove(TypeFieldName);
                        actualType = _typeResolver.ResolveType(typeName) ?? elementType;
                    }
                    else if (elementType.IsAbstract || elementType.IsInterface)
                    {
                        throw new YamlException($"Cannot deserialize abstract type {elementType.Name} without $type field");
                    }

                    var element = CreateInstanceWithPropertyTypes(actualType, dict, rootDeserializer);
                    typedList.Add(element);
                }
                else if (item is string str)
                {
                    typedList.Add(ConvertString(str, elementType));
                }
                else
                {
                    typedList.Add(item);
                }
            }

            return typedList;
        }

        private object? ConvertPolymorphicArray(List<object?> list, Type elementType, ObjectDeserializer rootDeserializer)
        {
            var array = Array.CreateInstance(elementType, list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                {
                    array.SetValue(null, i);
                    continue;
                }

                if (item is Dictionary<string, object?> dict)
                {
                    // Check for $type
                    Type actualType = elementType;
                    if (dict.TryGetValue(TypeFieldName, out var typeVal) && typeVal is string typeName)
                    {
                        dict.Remove(TypeFieldName);
                        actualType = _typeResolver.ResolveType(typeName) ?? elementType;
                    }
                    else if (elementType.IsAbstract || elementType.IsInterface)
                    {
                        throw new YamlException($"Cannot deserialize abstract type {elementType.Name} without $type field");
                    }

                    var element = CreateInstanceWithPropertyTypes(actualType, dict, rootDeserializer);
                    array.SetValue(element, i);
                }
                else if (item is string str)
                {
                    array.SetValue(ConvertString(str, elementType), i);
                }
                else
                {
                    array.SetValue(item, i);
                }
            }

            return array;
        }

        private object? ConvertPolymorphicDictionary(Dictionary<string, object?> sourceDict, Type dictType, Type keyType, Type valueType, ObjectDeserializer rootDeserializer)
        {
            var targetDict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;

            foreach (var kvp in sourceDict)
            {
                var key = ConvertString(kvp.Key, keyType);
                object? value;

                if (kvp.Value == null)
                {
                    value = valueType.IsValueType ? Activator.CreateInstance(valueType) : null;
                }
                else if (kvp.Value is Dictionary<string, object?> nestedDict)
                {
                    // Check for $type in the nested dictionary's value properties
                    value = CreateInstanceWithPropertyTypes(valueType, nestedDict, rootDeserializer);
                }
                else if (kvp.Value is string strValue)
                {
                    value = ConvertString(strValue, valueType);
                }
                else
                {
                    value = Convert.ChangeType(kvp.Value, valueType);
                }

                targetDict[key!] = value;
            }

            return targetDict;
        }

        private object? ConvertToPropertyType(object? value, Type targetType, ObjectDeserializer rootDeserializer)
        {
            if (value == null)
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (value is string str)
            {
                return ConvertString(str, targetType);
            }

            if (value is Dictionary<string, object?> dict)
            {
                // Check for nested $type
                if (dict.TryGetValue(TypeFieldName, out var typeVal) && typeVal is string typeName)
                {
                    dict.Remove(TypeFieldName);
                    var nestedType = _typeResolver.ResolveType(typeName) ?? targetType;
                    return CreateInstance(nestedType, dict, rootDeserializer);
                }
                return CreateInstance(targetType, dict, rootDeserializer);
            }

            if (value is List<object?> list)
            {
                return ConvertList(list, targetType, rootDeserializer);
            }

            return Convert.ChangeType(value, targetType);
        }

        private object? ConvertString(string value, Type targetType)
        {
            if (targetType == typeof(string))
                return value;

            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, true);

            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(long)) return long.Parse(value);
            if (targetType == typeof(float)) return float.Parse(value);
            if (targetType == typeof(double)) return double.Parse(value);
            if (targetType == typeof(bool)) return bool.Parse(value);
            if (targetType == typeof(decimal)) return decimal.Parse(value);

            var underlying = Nullable.GetUnderlyingType(targetType);
            if (underlying != null)
            {
                return string.IsNullOrEmpty(value) ? null : ConvertString(value, underlying);
            }

            // Empty string for value types (structs) should return default value
            if (string.IsNullOrEmpty(value) && targetType.IsValueType)
            {
                return Activator.CreateInstance(targetType);
            }

            return Convert.ChangeType(value, targetType);
        }

        private object? ConvertList(List<object?> list, Type targetType, ObjectDeserializer rootDeserializer)
        {
            Type elementType;

            if (targetType.IsArray)
            {
                elementType = targetType.GetElementType()!;
                var array = Array.CreateInstance(elementType, list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    array.SetValue(ConvertToPropertyType(list[i], elementType, rootDeserializer), i);
                }
                return array;
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = targetType.GetGenericArguments()[0];
                var typedList = (System.Collections.IList)Activator.CreateInstance(targetType)!;
                foreach (var item in list)
                {
                    typedList.Add(ConvertToPropertyType(item, elementType, rootDeserializer));
                }
                return typedList;
            }

            return list;
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            if (value == null)
            {
                emitter.Emit(new Scalar(null, "null"));
                return;
            }

            var actualType = value.GetType();

            // Handle List<T> where T is polymorphic
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                WritePolymorphicList(emitter, (System.Collections.IList)value, elementType, serializer);
                return;
            }

            // Handle arrays of polymorphic types
            if (type.IsArray)
            {
                var elementType = type.GetElementType()!;
                WritePolymorphicArray(emitter, (Array)value, elementType, serializer);
                return;
            }

            // Handle Dictionary<TKey, TValue> where TValue has polymorphic properties
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var valueType = type.GetGenericArguments()[1];
                WritePolymorphicDictionary(emitter, (System.Collections.IDictionary)value, valueType, serializer);
                return;
            }

            emitter.Emit(new MappingStart(null, null, false, MappingStyle.Block));

            // Write $type field first if the actual type differs from declared type
            // or if the declared type is abstract/interface
            if (actualType != type || type.IsAbstract || type.IsInterface || _polymorphicBaseTypes.Contains(type))
            {
                emitter.Emit(new Scalar(null, TypeFieldName));
                emitter.Emit(new Scalar(null, _typeResolver.GetTypeName(actualType)));
            }

            // Write all writable properties (excluding indexers)
            var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

            foreach (var property in properties)
            {
                var propValue = property.GetValue(value);

                // Skip null values
                if (propValue == null)
                    continue;

                emitter.Emit(new Scalar(null, property.Name));
                WritePropertyValue(emitter, propValue, property.PropertyType, serializer);
            }

            emitter.Emit(new MappingEnd());
        }

        private void WritePolymorphicList(IEmitter emitter, System.Collections.IList list, Type elementType, ObjectSerializer serializer)
        {
            emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Block));

            foreach (var item in list)
            {
                if (item == null)
                {
                    emitter.Emit(new Scalar(null, "null"));
                    continue;
                }

                WritePolymorphicElement(emitter, item, elementType, serializer);
            }

            emitter.Emit(new SequenceEnd());
        }

        private void WritePolymorphicArray(IEmitter emitter, Array array, Type elementType, ObjectSerializer serializer)
        {
            emitter.Emit(new SequenceStart(null, null, false, SequenceStyle.Block));

            foreach (var item in array)
            {
                if (item == null)
                {
                    emitter.Emit(new Scalar(null, "null"));
                    continue;
                }

                WritePolymorphicElement(emitter, item, elementType, serializer);
            }

            emitter.Emit(new SequenceEnd());
        }

        private void WritePolymorphicDictionary(IEmitter emitter, System.Collections.IDictionary dict, Type valueType, ObjectSerializer serializer)
        {
            emitter.Emit(new MappingStart(null, null, false, MappingStyle.Block));

            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                // Write key
                emitter.Emit(new Scalar(null, entry.Key?.ToString() ?? ""));

                // Write value
                if (entry.Value == null)
                {
                    emitter.Emit(new Scalar(null, "null"));
                    continue;
                }

                // Write value as an object with properties (and handle nested polymorphism)
                WritePolymorphicDictionaryValue(emitter, entry.Value, valueType, serializer);
            }

            emitter.Emit(new MappingEnd());
        }

        private void WritePolymorphicDictionaryValue(IEmitter emitter, object value, Type declaredType, ObjectSerializer serializer)
        {
            var actualType = value.GetType();

            emitter.Emit(new MappingStart(null, null, false, MappingStyle.Block));

            // Write all writable properties (excluding indexers)
            var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

            foreach (var property in properties)
            {
                var propValue = property.GetValue(value);

                // Skip null values
                if (propValue == null)
                    continue;

                emitter.Emit(new Scalar(null, property.Name));
                WritePropertyValue(emitter, propValue, property.PropertyType, serializer);
            }

            emitter.Emit(new MappingEnd());
        }

        private void WritePolymorphicElement(IEmitter emitter, object value, Type declaredType, ObjectSerializer serializer)
        {
            var actualType = value.GetType();

            emitter.Emit(new MappingStart(null, null, false, MappingStyle.Block));

            // Always write $type for polymorphic elements
            if (actualType != declaredType || declaredType.IsAbstract || declaredType.IsInterface || IsPolymorphicType(declaredType))
            {
                emitter.Emit(new Scalar(null, TypeFieldName));
                emitter.Emit(new Scalar(null, _typeResolver.GetTypeName(actualType)));
            }

            // Write all writable properties (excluding indexers)
            var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

            foreach (var property in properties)
            {
                var propValue = property.GetValue(value);

                // Skip null values
                if (propValue == null)
                    continue;

                emitter.Emit(new Scalar(null, property.Name));
                WritePropertyValue(emitter, propValue, property.PropertyType, serializer);
            }

            emitter.Emit(new MappingEnd());
        }

        private void WritePropertyValue(IEmitter emitter, object value, Type declaredType, ObjectSerializer serializer)
        {
            var actualType = value.GetType();

            // Handle nested polymorphic lists
            if (declaredType.IsGenericType && declaredType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = declaredType.GetGenericArguments()[0];
                if (IsPolymorphicType(elementType))
                {
                    WritePolymorphicList(emitter, (System.Collections.IList)value, elementType, serializer);
                    return;
                }
            }

            // Handle nested polymorphic arrays
            if (declaredType.IsArray)
            {
                var elementType = declaredType.GetElementType()!;
                if (IsPolymorphicType(elementType))
                {
                    WritePolymorphicArray(emitter, (Array)value, elementType, serializer);
                    return;
                }
            }

            // Handle nested polymorphic dictionaries
            if (declaredType.IsGenericType && declaredType.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var valueType = declaredType.GetGenericArguments()[1];
                if (IsPolymorphicType(valueType) || HasPolymorphicProperties(valueType))
                {
                    WritePolymorphicDictionary(emitter, (System.Collections.IDictionary)value, valueType, serializer);
                    return;
                }
            }

            // Handle polymorphic property values (like ICondition)
            if (IsPolymorphicType(declaredType))
            {
                emitter.Emit(new MappingStart(null, null, false, MappingStyle.Block));

                // Write $type field
                emitter.Emit(new Scalar(null, TypeFieldName));
                emitter.Emit(new Scalar(null, _typeResolver.GetTypeName(actualType)));

                // Write all writable properties
                var properties = actualType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);

                foreach (var property in properties)
                {
                    var propValue = property.GetValue(value);
                    if (propValue == null)
                        continue;

                    emitter.Emit(new Scalar(null, property.Name));
                    WritePropertyValue(emitter, propValue, property.PropertyType, serializer);
                }

                emitter.Emit(new MappingEnd());
                return;
            }

            // Use default serializer for non-polymorphic types
            serializer(value, declaredType);
        }
    }
}
