#nullable enable
using System.Threading.Tasks;

namespace Datra
{
    /// <summary>
    /// Repository 기본 인터페이스
    /// 모든 Repository는 비동기 초기화를 지원
    /// </summary>
    public interface IRepository
    {
        /// <summary>
        /// Repository 초기화 (메타데이터 로드 등)
        /// 최초 1회 호출 필요
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// 초기화 완료 여부
        /// </summary>
        bool IsInitialized { get; }
    }
}
