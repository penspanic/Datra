#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra
{
    /// <summary>
    /// Key-Value 테이블 데이터용 Repository (예: CharacterInfo, ItemInfo)
    /// 변경 추적 통합
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    /// <typeparam name="TData">데이터 타입</typeparam>
    public interface ITableRepository<TKey, TData> : IRepository, IChangeTracking<TKey>
        where TKey : notnull
        where TData : class
    {
        // === 메타데이터 (동기) ===

        /// <summary>
        /// 총 항목 수 (Deleted 제외)
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 특정 키 존재 여부
        /// </summary>
        bool Contains(TKey key);

        /// <summary>
        /// 모든 키 목록
        /// </summary>
        IEnumerable<TKey> Keys { get; }

        // === 읽기 (비동기) ===

        /// <summary>
        /// 특정 항목 로드 (비동기)
        /// </summary>
        Task<TData?> GetAsync(TKey key);

        /// <summary>
        /// 전체 데이터 로드 (비동기)
        /// </summary>
        Task<IReadOnlyDictionary<TKey, TData>> GetAllAsync();

        /// <summary>
        /// 조건에 맞는 항목 검색 (비동기)
        /// </summary>
        Task<IEnumerable<TData>> FindAsync(Func<TData, bool> predicate);

        // === 이미 로드된 데이터 (동기) ===

        /// <summary>
        /// 이미 로드된 데이터 조회 (없으면 null)
        /// </summary>
        TData? TryGetLoaded(TKey key);

        /// <summary>
        /// 로드된 모든 항목
        /// </summary>
        IReadOnlyDictionary<TKey, TData> LoadedItems { get; }

        // === 쓰기 ===

        /// <summary>
        /// 새 항목 추가 (키 자동 추출)
        /// TData에서 키를 추출할 수 있는 경우 사용
        /// </summary>
        void Add(TData data);

        /// <summary>
        /// 새 항목 추가 (키 명시)
        /// </summary>
        void Add(TKey key, TData data);

        /// <summary>
        /// 기존 항목 업데이트
        /// </summary>
        void Update(TKey key, TData data);

        /// <summary>
        /// 항목 삭제
        /// </summary>
        void Remove(TKey key);

        // === Working Copy ===

        /// <summary>
        /// 편집용 Working Copy 가져오기
        /// 없으면 Baseline에서 복제하여 생성
        /// </summary>
        TData GetWorkingCopy(TKey key);

        /// <summary>
        /// 항목을 수정됨으로 표시 (in-place 편집 시)
        /// </summary>
        void MarkAsModified(TKey key);
    }
}
