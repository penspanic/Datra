using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Attributes;
using Datra.DataTypes;
using Datra.Interfaces;
using Datra.Services;
using UnityEngine;

namespace Datra.Unity.Editor.Services
{
    /// <summary>
    /// Analyzes FixedLocale keys and detects missing/orphan keys.
    /// </summary>
    public class FixedLocaleKeyAnalyzer
    {
        private readonly IDataContext _dataContext;
        private readonly LocalizationContext _localizationContext;
        private readonly Dictionary<Type, IDataRepository> _repositories;

        public FixedLocaleKeyAnalyzer(
            IDataContext dataContext,
            LocalizationContext localizationContext,
            Dictionary<Type, IDataRepository> repositories)
        {
            _dataContext = dataContext ?? throw new ArgumentNullException(nameof(dataContext));
            _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));
            _repositories = repositories ?? throw new ArgumentNullException(nameof(repositories));
        }

        /// <summary>
        /// Result of FixedLocale key analysis
        /// </summary>
        public class AnalysisResult
        {
            /// <summary>
            /// Keys that should exist but are missing from LocalizationKeys
            /// </summary>
            public List<ExpectedKey> MissingKeys { get; } = new();

            /// <summary>
            /// Fixed keys in LocalizationKeys that no longer have corresponding data
            /// </summary>
            public List<OrphanKey> OrphanKeys { get; } = new();

            /// <summary>
            /// Total count of expected FixedLocale keys
            /// </summary>
            public int TotalExpectedKeys { get; set; }

            /// <summary>
            /// Total count of existing Fixed keys in LocalizationKeys
            /// </summary>
            public int TotalExistingFixedKeys { get; set; }

            public bool HasIssues => MissingKeys.Count > 0 || OrphanKeys.Count > 0;
        }

        /// <summary>
        /// Represents an expected key that is missing
        /// </summary>
        public class ExpectedKey
        {
            public string Key { get; set; }
            public string TypeName { get; set; }
            public string ItemId { get; set; }
            public string PropertyName { get; set; }
            public string SuggestedCategory { get; set; }
            public string SuggestedDescription { get; set; }
        }

        /// <summary>
        /// Represents an orphan key (no longer referenced by any data)
        /// </summary>
        public class OrphanKey
        {
            public string Key { get; set; }
            public string TypeName { get; set; }
            public string ItemId { get; set; }
            public string PropertyName { get; set; }
        }

        /// <summary>
        /// Analyze all data types and detect missing/orphan FixedLocale keys
        /// </summary>
        public AnalysisResult Analyze()
        {
            var result = new AnalysisResult();

            // Step 1: Build set of expected keys from all data items
            var expectedKeys = BuildExpectedKeysSet();
            result.TotalExpectedKeys = expectedKeys.Count;

            // Step 2: Get existing Fixed keys from LocalizationKeys
            var existingFixedKeys = GetExistingFixedKeys();
            result.TotalExistingFixedKeys = existingFixedKeys.Count;

            // Step 3: Find missing keys (expected but not in LocalizationKeys)
            foreach (var expected in expectedKeys.Values)
            {
                if (!existingFixedKeys.Contains(expected.Key))
                {
                    result.MissingKeys.Add(expected);
                }
            }

            // Step 4: Find orphan keys (in LocalizationKeys but not expected)
            foreach (var existingKey in existingFixedKeys)
            {
                if (!expectedKeys.ContainsKey(existingKey))
                {
                    // Parse the key to extract type/id/property
                    var parsed = ParseFixedKey(existingKey);
                    if (parsed != null)
                    {
                        result.OrphanKeys.Add(parsed);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Build a dictionary of all expected FixedLocale keys from current data
        /// </summary>
        private Dictionary<string, ExpectedKey> BuildExpectedKeysSet()
        {
            var expectedKeys = new Dictionary<string, ExpectedKey>();

            foreach (var typeInfo in _dataContext.GetDataTypeInfos())
            {
                // Skip non-table data (Single data doesn't have multiple IDs)
                if (typeInfo.RepositoryKind == RepositoryKind.Single)
                {
                    // For single data, we still need to check for FixedLocale properties
                    // but there's only one instance
                    ProcessSingleDataType(typeInfo, expectedKeys);
                    continue;
                }

                if (typeInfo.RepositoryKind != RepositoryKind.Table)
                    continue;

                ProcessTableDataType(typeInfo, expectedKeys);
            }

            return expectedKeys;
        }

        private void ProcessTableDataType(DataTypeInfo typeInfo, Dictionary<string, ExpectedKey> expectedKeys)
        {
            var dataType = typeInfo.DataType;
            var typeName = dataType.Name;

            // Get FixedLocale properties
            var fixedLocaleProps = GetFixedLocaleProperties(dataType);
            if (fixedLocaleProps.Count == 0)
                return;

            // Get repository and enumerate all items
            if (!_repositories.TryGetValue(dataType, out var repository))
                return;

            foreach (var item in repository.EnumerateItems())
            {
                // Get the ID of this item
                var idProperty = dataType.GetProperty("Id");
                if (idProperty == null)
                    continue;

                var id = idProperty.GetValue(item)?.ToString();
                if (string.IsNullOrEmpty(id))
                    continue;

                // Add expected key for each FixedLocale property
                foreach (var prop in fixedLocaleProps)
                {
                    var key = $"{typeName}.{id}.{prop.Name}";
                    expectedKeys[key] = new ExpectedKey
                    {
                        Key = key,
                        TypeName = typeName,
                        ItemId = id,
                        PropertyName = prop.Name,
                        SuggestedCategory = typeName,
                        SuggestedDescription = $"{prop.Name} for {typeName} '{id}'"
                    };
                }
            }
        }

        private void ProcessSingleDataType(DataTypeInfo typeInfo, Dictionary<string, ExpectedKey> expectedKeys)
        {
            var dataType = typeInfo.DataType;
            var typeName = dataType.Name;

            // Get FixedLocale properties
            var fixedLocaleProps = GetFixedLocaleProperties(dataType);
            if (fixedLocaleProps.Count == 0)
                return;

            // For single data, use a fixed ID (could be empty or type name)
            // Need to check how FixedLocale is used in single data models
            // Usually single data doesn't have FixedLocale, but if it does...

            // Get repository
            if (!_repositories.TryGetValue(dataType, out var repository))
                return;

            var items = repository.EnumerateItems().ToList();
            if (items.Count == 0)
                return;

            var item = items[0];

            // Try to get ID, or use empty string
            var idProperty = dataType.GetProperty("Id");
            var id = idProperty?.GetValue(item)?.ToString() ?? "";

            foreach (var prop in fixedLocaleProps)
            {
                var key = string.IsNullOrEmpty(id)
                    ? $"{typeName}.{prop.Name}"
                    : $"{typeName}.{id}.{prop.Name}";

                expectedKeys[key] = new ExpectedKey
                {
                    Key = key,
                    TypeName = typeName,
                    ItemId = id,
                    PropertyName = prop.Name,
                    SuggestedCategory = typeName,
                    SuggestedDescription = $"{prop.Name} for {typeName}"
                };
            }
        }

        /// <summary>
        /// Get properties with [FixedLocale] attribute that return LocaleRef
        /// </summary>
        private List<PropertyInfo> GetFixedLocaleProperties(Type dataType)
        {
            return dataType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(LocaleRef) &&
                           p.GetCustomAttribute<FixedLocaleAttribute>() != null)
                .ToList();
        }

        /// <summary>
        /// Get all existing keys marked as IsFixedKey=true in LocalizationKeys
        /// </summary>
        private HashSet<string> GetExistingFixedKeys()
        {
            var fixedKeys = new HashSet<string>();

            // Access the keys from LocalizationContext
            foreach (var key in _localizationContext.GetAllKeys())
            {
                var keyData = _localizationContext.GetKeyData(key);
                if (keyData != null && keyData.IsFixedKey)
                {
                    fixedKeys.Add(key);
                }
            }

            return fixedKeys;
        }

        /// <summary>
        /// Parse a fixed key string into its components
        /// </summary>
        private OrphanKey ParseFixedKey(string key)
        {
            // Expected format: TypeName.Id.PropertyName
            var parts = key.Split('.');
            if (parts.Length < 2)
                return null;

            if (parts.Length == 2)
            {
                // Format: TypeName.PropertyName (no ID, possibly single data)
                return new OrphanKey
                {
                    Key = key,
                    TypeName = parts[0],
                    ItemId = "",
                    PropertyName = parts[1]
                };
            }

            // Format: TypeName.Id.PropertyName (or TypeName.Id.SubId.PropertyName for nested)
            return new OrphanKey
            {
                Key = key,
                TypeName = parts[0],
                ItemId = parts[1],
                PropertyName = parts[^1] // Last part is property name
            };
        }
    }
}
