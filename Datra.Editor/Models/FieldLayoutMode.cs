namespace Datra.Editor.Models
{
    /// <summary>
    /// 필드 레이아웃 모드
    /// Unity Editor와 Blazor WebEditor에서 공통으로 사용
    /// </summary>
    public enum FieldLayoutMode
    {
        /// <summary>레이블이 위에 있는 전체 레이아웃 (상세 편집)</summary>
        Form,

        /// <summary>테이블 셀용 컴팩트 레이아웃 (리스트 뷰)</summary>
        Table,

        /// <summary>레이블이 왼쪽에 있는 인라인 레이아웃</summary>
        Inline
    }
}
