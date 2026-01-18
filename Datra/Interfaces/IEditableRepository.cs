#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Datra
{
    /// <summary>
    /// 편집 가능한 Repository의 공통 인터페이스
    /// SaveAsync, EnumerateItems 등 편집에 필요한 메서드 제공
    /// </summary>
    public interface IEditableRepository : IRepository
    {
        /// <summary>
        /// 변경사항 저장
        /// </summary>
        Task SaveAsync();

        /// <summary>
        /// 모든 항목 열거 (런타임 타입 정보 없이 object로 반환)
        /// </summary>
        IEnumerable<object> EnumerateItems();

        /// <summary>
        /// 항목 수
        /// </summary>
        int ItemCount { get; }

        /// <summary>
        /// 로드된 파일 경로
        /// </summary>
        string? LoadedFilePath { get; }
    }
}
