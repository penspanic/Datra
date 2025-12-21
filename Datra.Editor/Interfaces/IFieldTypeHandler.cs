using System;
using System.Reflection;

namespace Datra.Editor.Interfaces
{
    /// <summary>
    /// 필드 타입 핸들러 기본 인터페이스
    /// 렌더링 로직은 포함하지 않음 - 플랫폼별 인터페이스에서 확장
    ///
    /// Unity: IUnityFieldHandler extends IFieldTypeHandler { VisualElement CreateField(...) }
    /// Blazor: IBlazorFieldHandler extends IFieldTypeHandler { RenderFragment CreateField(...) }
    /// </summary>
    public interface IFieldTypeHandler
    {
        /// <summary>
        /// 핸들러 우선순위 (높을수록 먼저 체크)
        ///
        /// 권장 Priority 범위:
        /// - 100+: 매우 특수한 타입 (LocaleRef + FixedLocale)
        /// - 50-99: 특수 참조 타입 (DataRef, InfoRef)
        /// - 40-49: 속성 기반 커스텀 에디터
        /// - 30-39: 중첩 타입
        /// - 20-29: 컬렉션 타입 (Dictionary, List, Array)
        /// - 10-19: Enum
        /// - 1-9: 기본 타입 (string, int, bool, DateTime)
        /// - 0: Fallback (DefaultHandler)
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// 이 핸들러가 주어진 타입을 처리할 수 있는지 확인
        /// </summary>
        /// <param name="type">필드 타입</param>
        /// <param name="member">멤버 정보 (속성 체크용, optional)</param>
        /// <returns>처리 가능 여부</returns>
        bool CanHandle(Type type, MemberInfo? member = null);
    }
}
