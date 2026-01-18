#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.DataTypes;

namespace Datra
{
    /// <summary>
    /// 플랫폼별 데이터 저장소 추상화
    /// Unity Editor, WebEditor, Runtime 등 각 환경에 맞는 구현 제공
    /// </summary>
    public interface IDataProvider
    {
        // === 메타데이터 로드 ===

        /// <summary>
        /// Asset Summary 목록 로드 (파일 내용 읽지 않음)
        /// </summary>
        /// <param name="basePath">Asset 폴더 기본 경로</param>
        /// <param name="pattern">파일 패턴 (예: "*.json", "**/*.yaml")</param>
        Task<IEnumerable<AssetSummary>> LoadAssetSummariesAsync(string basePath, string pattern);

        // === 데이터 로드 ===

        /// <summary>
        /// 텍스트 파일 로드
        /// </summary>
        Task<string> LoadTextAsync(string path);

        /// <summary>
        /// JSON/YAML 파일 역직렬화
        /// </summary>
        Task<T?> LoadAsync<T>(string path) where T : class;

        // === 데이터 저장 ===

        /// <summary>
        /// 텍스트 파일 저장
        /// </summary>
        Task SaveTextAsync(string path, string content);

        /// <summary>
        /// JSON/YAML 파일 직렬화 저장
        /// </summary>
        Task SaveAsync<T>(string path, T data) where T : class;

        // === 삭제 ===

        /// <summary>
        /// 파일 삭제
        /// </summary>
        Task DeleteAsync(string path);

        /// <summary>
        /// 파일 존재 여부
        /// </summary>
        Task<bool> ExistsAsync(string path);

        // === 캐시 (선택적) ===

        /// <summary>
        /// 캐시에서 로드 (체크섬 일치 시)
        /// 구현체가 캐시를 지원하지 않으면 null 반환
        /// </summary>
        Task<T?> LoadFromCacheAsync<T>(string path, string checksum) where T : class;

        /// <summary>
        /// 캐시에 저장
        /// 구현체가 캐시를 지원하지 않으면 무시
        /// </summary>
        Task SaveToCacheAsync<T>(string path, T data, string checksum) where T : class;
    }
}
