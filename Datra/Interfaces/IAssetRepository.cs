#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Datra.DataTypes;

namespace Datra
{
    /// <summary>
    /// 파일 기반 Asset 데이터용 Repository
    /// Summary 패턴: 초기화 시 메타데이터만 로드, 실제 데이터는 lazy load
    /// 변경 추적 통합
    /// </summary>
    /// <typeparam name="T">Asset 데이터 타입</typeparam>
    public interface IAssetRepository<T> : IRepository, IChangeTracking<AssetId>
        where T : class
    {
        // === Summary (동기 - 역직렬화 없음) ===

        /// <summary>
        /// 총 Asset 수
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 모든 Asset의 Summary 목록
        /// </summary>
        IEnumerable<AssetSummary> Summaries { get; }

        /// <summary>
        /// 특정 Asset의 Summary 조회
        /// </summary>
        AssetSummary? GetSummary(AssetId id);

        /// <summary>
        /// 경로로 Summary 조회
        /// </summary>
        AssetSummary? GetSummaryByPath(string path);

        /// <summary>
        /// 이름으로 Summary 조회 (확장자 제외)
        /// </summary>
        AssetSummary? GetSummaryByName(string name);

        /// <summary>
        /// Asset 존재 여부 (ID)
        /// </summary>
        bool Contains(AssetId id);

        /// <summary>
        /// Asset 존재 여부 (경로)
        /// </summary>
        bool ContainsPath(string path);

        // === 읽기 (비동기 - lazy load) ===

        /// <summary>
        /// Asset 로드 (비동기)
        /// </summary>
        Task<Asset<T>?> GetAsync(AssetId id);

        /// <summary>
        /// 경로로 Asset 로드 (비동기)
        /// </summary>
        Task<Asset<T>?> GetByPathAsync(string path);

        /// <summary>
        /// 이름으로 Asset 로드 (비동기)
        /// </summary>
        Task<Asset<T>?> GetByNameAsync(string name);

        /// <summary>
        /// 조건에 맞는 Asset 검색 (Summary 기준, 비동기)
        /// </summary>
        Task<IEnumerable<Asset<T>>> FindAsync(Func<AssetSummary, bool> predicate);

        // === 이미 로드된 데이터 (동기) ===

        /// <summary>
        /// 이미 로드된 Asset 조회 (없으면 null)
        /// </summary>
        Asset<T>? TryGetLoaded(AssetId id);

        /// <summary>
        /// Asset이 로드되었는지 확인
        /// </summary>
        bool IsLoaded(AssetId id);

        /// <summary>
        /// 로드된 모든 Asset
        /// </summary>
        IReadOnlyDictionary<AssetId, Asset<T>> LoadedAssets { get; }

        // === 쓰기 ===

        /// <summary>
        /// 새 Asset 추가
        /// </summary>
        Asset<T> Add(T data, string filePath);

        /// <summary>
        /// 커스텀 메타데이터로 새 Asset 추가
        /// </summary>
        Asset<T> Add(T data, AssetMetadata metadata, string filePath);

        /// <summary>
        /// Asset 데이터 업데이트
        /// </summary>
        void Update(AssetId id, T data);

        /// <summary>
        /// Asset 메타데이터 업데이트
        /// </summary>
        void UpdateMetadata(AssetId id, Action<AssetMetadata> action);

        /// <summary>
        /// Asset 삭제
        /// </summary>
        bool Remove(AssetId id);

        // === Working Copy ===

        /// <summary>
        /// 편집용 Working Copy 가져오기
        /// 로드되지 않은 경우 로드 후 복제
        /// </summary>
        Asset<T> GetWorkingCopy(AssetId id);

        /// <summary>
        /// Asset을 수정됨으로 표시 (in-place 편집 시)
        /// </summary>
        void MarkAsModified(AssetId id);

        // === 개별 저장 ===

        /// <summary>
        /// 특정 Asset만 저장
        /// </summary>
        Task SaveAsync(AssetId id);
    }
}
