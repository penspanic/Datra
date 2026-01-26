#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Datra.DataTypes;
using Datra.Repositories;

namespace Datra.Providers
{
    /// <summary>
    /// 메모리 기반 IDataProvider 구현
    /// 테스트 및 목업에 사용
    /// </summary>
    public class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, string> _textFiles = new();
        private readonly Dictionary<string, object> _objectFiles = new();
        private readonly Dictionary<string, AssetSummary> _summaries = new();

        #region Test Setup Methods

        /// <summary>
        /// 테스트용 텍스트 파일 추가
        /// </summary>
        public void AddTextFile(string path, string content)
        {
            _textFiles[NormalizePath(path)] = content;
        }

        /// <summary>
        /// 테스트용 객체 파일 추가
        /// </summary>
        public void AddObjectFile<T>(string path, T data) where T : class
        {
            _objectFiles[NormalizePath(path)] = data;
        }

        /// <summary>
        /// 테스트용 Asset Summary 추가
        /// </summary>
        public void AddAssetSummary(string basePath, AssetSummary summary)
        {
            var key = $"{NormalizePath(basePath)}:{summary.Id}";
            _summaries[key] = summary;
        }

        /// <summary>
        /// 모든 데이터 초기화
        /// </summary>
        public void Clear()
        {
            _textFiles.Clear();
            _objectFiles.Clear();
            _summaries.Clear();
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimStart('/');
        }

        #endregion

        #region IDataProvider - 메타데이터 로드

        public Task<IEnumerable<AssetSummary>> LoadAssetSummariesAsync(string basePath, string pattern)
        {
            var normalizedBase = NormalizePath(basePath);
            var results = _summaries
                .Where(kvp => kvp.Key.StartsWith($"{normalizedBase}:"))
                .Select(kvp => kvp.Value);

            return Task.FromResult(results);
        }

        #endregion

        #region IDataProvider - 데이터 로드

        public Task<string> LoadTextAsync(string path)
        {
            var normalizedPath = NormalizePath(path);

            if (!_textFiles.TryGetValue(normalizedPath, out var content))
            {
                throw new KeyNotFoundException($"Text file not found: {path}");
            }

            return Task.FromResult(content);
        }

        public Task<T?> LoadAsync<T>(string path) where T : class
        {
            var normalizedPath = NormalizePath(path);

            if (_objectFiles.TryGetValue(normalizedPath, out var obj))
            {
                return Task.FromResult(obj as T);
            }

            // Try to parse from text file
            if (_textFiles.TryGetValue(normalizedPath, out var text))
            {
                var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(text);
                var data = deserialized != null ? DeepCloner.Clone(deserialized) : default;
                return Task.FromResult(data);
            }

            return Task.FromResult<T?>(null);
        }

        #endregion

        #region IDataProvider - 데이터 저장

        public Task SaveTextAsync(string path, string content)
        {
            var normalizedPath = NormalizePath(path);
            _textFiles[normalizedPath] = content;
            return Task.CompletedTask;
        }

        public Task SaveAsync<T>(string path, T data) where T : class
        {
            var normalizedPath = NormalizePath(path);
            _objectFiles[normalizedPath] = DeepCloner.Clone(data)!;
            return Task.CompletedTask;
        }

        #endregion

        #region IDataProvider - 삭제

        public Task DeleteAsync(string path)
        {
            var normalizedPath = NormalizePath(path);
            _textFiles.Remove(normalizedPath);
            _objectFiles.Remove(normalizedPath);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string path)
        {
            var normalizedPath = NormalizePath(path);
            var exists = _textFiles.ContainsKey(normalizedPath) ||
                         _objectFiles.ContainsKey(normalizedPath);
            return Task.FromResult(exists);
        }

        #endregion

        #region IDataProvider - 캐시 (미구현)

        public Task<T?> LoadFromCacheAsync<T>(string path, string checksum) where T : class
        {
            return Task.FromResult<T?>(null);
        }

        public Task SaveToCacheAsync<T>(string path, T data, string checksum) where T : class
        {
            return Task.CompletedTask;
        }

        #endregion
    }
}
