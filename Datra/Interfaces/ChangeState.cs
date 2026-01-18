#nullable enable

namespace Datra
{
    /// <summary>
    /// 데이터 항목의 변경 상태
    /// </summary>
    public enum ChangeState
    {
        /// <summary>
        /// 변경 없음 (Baseline과 동일)
        /// </summary>
        Unchanged,

        /// <summary>
        /// 새로 추가됨 (Baseline 없음)
        /// </summary>
        Added,

        /// <summary>
        /// 수정됨 (Baseline 존재, 값 변경)
        /// </summary>
        Modified,

        /// <summary>
        /// 삭제 예정 (저장 시 삭제됨)
        /// </summary>
        Deleted
    }
}
