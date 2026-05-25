#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Datra.DataTypes;

namespace Datra.Providers
{
    /// <summary>
    /// 파일 시스템 기반 IDataProvider 구현
    /// 로컬 개발 및 테스트에 사용
    /// </summary>
    public class FileSystemDataProvider : IDataProvider
    {
        private readonly string _basePath;
        private readonly JsonSerializerOptions _jsonOptions;

        public FileSystemDataProvider(string basePath)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            };
        }

        private string GetFullPath(string relativePath)
        {
            return Path.Combine(_basePath, relativePath);
        }

        #region IDataProvider - 메타데이터 로드

        public Task<IEnumerable<AssetSummary>> LoadAssetSummariesAsync(string basePath, string pattern)
        {
            var fullPath = GetFullPath(basePath);

            if (!Directory.Exists(fullPath))
            {
                return Task.FromResult(Enumerable.Empty<AssetSummary>());
            }

            var searchPattern = pattern.Replace("**", "*");
            var searchOption = pattern.Contains("**")
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var files = Directory.GetFiles(fullPath, searchPattern, searchOption);
            var summaries = new List<AssetSummary>();

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(_basePath, file);
                var fileInfo = new FileInfo(file);

                // Try to load .datrameta file for stable ID
                var metaPath = file + ".datrameta";
                AssetMetadata? metadata = null;
                AssetId id;

                if (File.Exists(metaPath))
                {
                    try
                    {
                        var metaContent = File.ReadAllText(metaPath);
                        metadata = JsonSerializer.Deserialize<AssetMetadata>(metaContent, _jsonOptions);
                        id = metadata?.Guid ?? AssetId.NewId();
                    }
                    catch
                    {
                        id = AssetId.NewId();
                        metadata = AssetMetadata.Create(id);
                    }
                }
                else
                {
                    id = AssetId.NewId();
                    metadata = AssetMetadata.Create(id);
                }

                metadata ??= AssetMetadata.Create(id);
                metadata.Size = fileInfo.Length;
                metadata.ModifiedAt = fileInfo.LastWriteTimeUtc;

                summaries.Add(new AssetSummary(id, metadata, relativePath));
            }

            return Task.FromResult<IEnumerable<AssetSummary>>(summaries);
        }

        #endregion

        #region IDataProvider - 데이터 로드

        public Task<string> LoadTextAsync(string path)
        {
            var fullPath = GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {path}", fullPath);
            }

            var content = File.ReadAllText(fullPath);
            return Task.FromResult(content);
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Generic JSON load uses reflection-based STJ. Trimmed consumers should pre-serialize via a JsonSerializerContext.")]
        [RequiresDynamicCode("Generic JSON load may need runtime code generation.")]
#endif
        public Task<T?> LoadAsync<T>(string path) where T : class
        {
            var fullPath = GetFullPath(path);

            if (!File.Exists(fullPath))
            {
                return Task.FromResult<T?>(null);
            }

            var content = File.ReadAllText(fullPath);
            var data = JsonSerializer.Deserialize<T>(content, _jsonOptions);
            return Task.FromResult(data);
        }

        #endregion

        #region IDataProvider - 데이터 저장

        public Task SaveTextAsync(string path, string content)
        {
            var fullPath = GetFullPath(path);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, content);
            return Task.CompletedTask;
        }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode("Generic JSON save uses reflection-based STJ. Trimmed consumers should pre-serialize via a JsonSerializerContext.")]
        [RequiresDynamicCode("Generic JSON save may need runtime code generation.")]
#endif
        public Task SaveAsync<T>(string path, T data) where T : class
        {
            var fullPath = GetFullPath(path);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var content = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(fullPath, content);
            return Task.CompletedTask;
        }

        #endregion

        #region IDataProvider - 삭제

        public Task DeleteAsync(string path)
        {
            var fullPath = GetFullPath(path);

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            // Also delete .datrameta if exists
            var metaPath = fullPath + ".datrameta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }

            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string path)
        {
            var fullPath = GetFullPath(path);
            return Task.FromResult(File.Exists(fullPath));
        }

        #endregion

        #region IDataProvider - 캐시 (미구현)

        public Task<T?> LoadFromCacheAsync<T>(string path, string checksum) where T : class
        {
            // File system provider doesn't implement caching
            return Task.FromResult<T?>(null);
        }

        public Task SaveToCacheAsync<T>(string path, T data, string checksum) where T : class
        {
            // File system provider doesn't implement caching
            return Task.CompletedTask;
        }

        #endregion
    }
}
