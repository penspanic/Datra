#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra
{
    /// <summary>
    /// 변경 추적 기본 인터페이스
    /// </summary>
    public interface IChangeTracking
    {
        /// <summary>
        /// 변경 사항이 있는지 여부
        /// </summary>
        bool HasChanges { get; }

        /// <summary>
        /// 모든 변경 사항을 되돌리고 Baseline으로 복원
        /// </summary>
        void Revert();

        /// <summary>
        /// 변경 사항을 저장
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 변경 상태가 변경되었을 때 발생 (HasChanges 값 변경)
        /// </summary>
        event Action<bool>? OnModifiedStateChanged;
    }

    /// <summary>
    /// Key 기반 변경 추적 인터페이스 (TableRepository, AssetRepository용)
    /// </summary>
    /// <typeparam name="TKey">키 타입</typeparam>
    public interface IChangeTracking<TKey> : IChangeTracking
        where TKey : notnull
    {
        // === 상태 조회 ===

        /// <summary>
        /// 특정 항목의 변경 상태 조회
        /// </summary>
        ChangeState GetState(TKey key);

        /// <summary>
        /// 변경된 모든 키 목록 (Added + Modified + Deleted)
        /// </summary>
        IEnumerable<TKey> GetChangedKeys();

        /// <summary>
        /// 새로 추가된 키 목록
        /// </summary>
        IEnumerable<TKey> GetAddedKeys();

        /// <summary>
        /// 수정된 키 목록
        /// </summary>
        IEnumerable<TKey> GetModifiedKeys();

        /// <summary>
        /// 삭제 예정인 키 목록
        /// </summary>
        IEnumerable<TKey> GetDeletedKeys();

        // === Baseline ===

        /// <summary>
        /// 특정 항목의 원본 데이터 (Baseline) 조회
        /// Added 상태인 경우 null 반환
        /// </summary>
        TData? GetBaseline<TData>(TKey key) where TData : class;

        // === Property-level 추적 ===

        /// <summary>
        /// 특정 속성이 수정되었는지 확인
        /// </summary>
        bool IsPropertyModified(TKey key, string propertyName);

        /// <summary>
        /// 수정된 속성 목록
        /// </summary>
        IEnumerable<string> GetModifiedProperties(TKey key);

        /// <summary>
        /// 특정 속성의 원본 값 (Baseline)
        /// </summary>
        object? GetPropertyBaseline(TKey key, string propertyName);

        /// <summary>
        /// 속성 변경 기록 (UI에서 값 변경 시 호출)
        /// </summary>
        void TrackPropertyChange(TKey key, string propertyName, object? newValue);

        // === 되돌리기 ===

        /// <summary>
        /// 특정 항목의 변경 사항을 되돌림
        /// </summary>
        void Revert(TKey key);

        /// <summary>
        /// 특정 속성만 되돌림
        /// </summary>
        void RevertProperty(TKey key, string propertyName);
    }
}
