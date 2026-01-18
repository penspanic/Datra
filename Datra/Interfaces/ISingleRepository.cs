#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra
{
    /// <summary>
    /// 단일 데이터 객체용 Repository (예: GameConfig)
    /// 변경 추적 통합
    /// </summary>
    /// <typeparam name="T">데이터 타입</typeparam>
    public interface ISingleRepository<T> : IRepository, IChangeTracking
        where T : class
    {
        // === 읽기 ===

        /// <summary>
        /// 데이터 로드 (비동기)
        /// </summary>
        Task<T?> GetAsync();

        /// <summary>
        /// 현재 데이터 (이미 로드된 경우)
        /// GetAsync() 호출 후 사용 가능
        /// </summary>
        T? Current { get; }

        // === 쓰기 ===

        /// <summary>
        /// 데이터 설정
        /// </summary>
        void Set(T data);

        // === 변경 추적 ===

        /// <summary>
        /// 원본 데이터 (Baseline)
        /// </summary>
        T? Baseline { get; }

        /// <summary>
        /// 특정 속성이 수정되었는지 확인
        /// </summary>
        bool IsPropertyModified(string propertyName);

        /// <summary>
        /// 수정된 속성 목록
        /// </summary>
        IEnumerable<string> GetModifiedProperties();

        /// <summary>
        /// 특정 속성의 원본 값 (Baseline)
        /// </summary>
        object? GetPropertyBaseline(string propertyName);

        /// <summary>
        /// 속성 변경 기록 (UI에서 값 변경 시 호출)
        /// </summary>
        void TrackPropertyChange(string propertyName, object? newValue);

        /// <summary>
        /// 특정 속성만 되돌림
        /// </summary>
        void RevertProperty(string propertyName);
    }
}
