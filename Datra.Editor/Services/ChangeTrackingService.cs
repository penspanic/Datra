#nullable enable
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Datra.Editor.Interfaces;

namespace Datra.Editor.Services
{
    /// <summary>
    /// Hash-based change tracking service.
    /// Compares current content state against stored baseline hashes.
    /// Tracks changes by file path for flexibility with multi-file scenarios.
    /// </summary>
    public class ChangeTrackingService : IChangeTrackingService
    {
        private readonly Dictionary<DataFilePath, string> _baselineHashes = new();
        private readonly Dictionary<DataFilePath, Func<string>> _contentProviders = new();
        private readonly Dictionary<DataFilePath, bool> _modifiedStates = new();

        public event Action<DataFilePath, bool>? OnModifiedStateChanged;

        public bool HasUnsavedChanges(DataFilePath filePath)
        {
            if (!_contentProviders.TryGetValue(filePath, out var contentProvider))
                return false;

            if (!_baselineHashes.TryGetValue(filePath, out var baselineHash))
                return false;

            var currentHash = ComputeHash(contentProvider());
            return currentHash != baselineHash;
        }

        public bool HasAnyUnsavedChanges()
        {
            foreach (var filePath in _contentProviders.Keys)
            {
                if (HasUnsavedChanges(filePath))
                    return true;
            }
            return false;
        }

        public IEnumerable<DataFilePath> GetModifiedFiles()
        {
            foreach (var filePath in _contentProviders.Keys)
            {
                if (HasUnsavedChanges(filePath))
                    yield return filePath;
            }
        }

        public void InitializeBaseline(DataFilePath filePath, Func<string> contentProvider)
        {
            _contentProviders[filePath] = contentProvider;
            var hash = ComputeHash(contentProvider());
            _baselineHashes[filePath] = hash;
            UpdateModifiedState(filePath, false);
        }

        public void InitializeAllBaselines()
        {
            foreach (var kvp in _contentProviders)
            {
                var filePath = kvp.Key;
                var contentProvider = kvp.Value;
                var hash = ComputeHash(contentProvider());
                _baselineHashes[filePath] = hash;
                UpdateModifiedState(filePath, false);
            }
        }

        public void ResetChanges(DataFilePath filePath)
        {
            if (_contentProviders.TryGetValue(filePath, out var contentProvider))
            {
                InitializeBaseline(filePath, contentProvider);
            }
        }

        public void RegisterFile(DataFilePath filePath, Func<string> contentProvider)
        {
            _contentProviders[filePath] = contentProvider;
            _modifiedStates[filePath] = false;
        }

        public void UnregisterFile(DataFilePath filePath)
        {
            _contentProviders.Remove(filePath);
            _baselineHashes.Remove(filePath);
            _modifiedStates.Remove(filePath);
        }

        public bool IsTracking(DataFilePath filePath)
        {
            return _contentProviders.ContainsKey(filePath);
        }

        /// <summary>
        /// Check and update modified state, raising event if changed
        /// </summary>
        public void CheckModifiedState(DataFilePath filePath)
        {
            var isModified = HasUnsavedChanges(filePath);
            UpdateModifiedState(filePath, isModified);
        }

        /// <summary>
        /// Check all registered files for modifications
        /// </summary>
        public void CheckAllModifiedStates()
        {
            foreach (var filePath in _contentProviders.Keys)
            {
                CheckModifiedState(filePath);
            }
        }

        private void UpdateModifiedState(DataFilePath filePath, bool isModified)
        {
            if (!_modifiedStates.TryGetValue(filePath, out var previousState) || previousState != isModified)
            {
                _modifiedStates[filePath] = isModified;
                OnModifiedStateChanged?.Invoke(filePath, isModified);
            }
        }

        private string ComputeHash(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}
