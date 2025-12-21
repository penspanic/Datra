using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Datra.Editor.Interfaces;
using Datra.Editor.Models;

namespace Datra.Editor.Services
{
    /// <summary>
    /// 필드 타입 핸들러 레지스트리 (기본 클래스)
    /// 핸들러 등록 및 검색 로직만 포함, 렌더링은 플랫폼별 구현에서 처리
    /// </summary>
    public class FieldTypeRegistry
    {
        private readonly List<IFieldTypeHandler> _handlers = new List<IFieldTypeHandler>();
        private bool _sorted = false;

        /// <summary>
        /// 등록된 핸들러 목록 (Priority 내림차순)
        /// </summary>
        public IReadOnlyList<IFieldTypeHandler> Handlers
        {
            get
            {
                EnsureSorted();
                return _handlers;
            }
        }

        /// <summary>
        /// 핸들러 등록
        /// </summary>
        public void RegisterHandler(IFieldTypeHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Add(handler);
            _sorted = false;
        }

        /// <summary>
        /// 여러 핸들러 등록
        /// </summary>
        public void RegisterHandlers(IEnumerable<IFieldTypeHandler> handlers)
        {
            foreach (var handler in handlers)
            {
                RegisterHandler(handler);
            }
        }

        /// <summary>
        /// 핸들러 제거
        /// </summary>
        public bool RemoveHandler(IFieldTypeHandler handler)
        {
            return _handlers.Remove(handler);
        }

        /// <summary>
        /// 타입에 맞는 핸들러 찾기
        /// </summary>
        /// <param name="type">필드 타입</param>
        /// <param name="member">멤버 정보 (optional)</param>
        /// <returns>처리 가능한 핸들러 또는 null</returns>
        public IFieldTypeHandler FindHandler(Type type, MemberInfo member = null)
        {
            EnsureSorted();

            foreach (var handler in _handlers)
            {
                if (handler.CanHandle(type, member))
                    return handler;
            }

            return null;
        }

        /// <summary>
        /// 타입에 맞는 핸들러 찾기 (제네릭 버전)
        /// </summary>
        public THandler FindHandler<THandler>(Type type, MemberInfo member = null)
            where THandler : class, IFieldTypeHandler
        {
            return FindHandler(type, member) as THandler;
        }

        /// <summary>
        /// 타입을 처리할 수 있는지 확인
        /// </summary>
        public bool CanHandle(Type type, MemberInfo member = null)
        {
            return FindHandler(type, member) != null;
        }

        /// <summary>
        /// 모든 핸들러 제거
        /// </summary>
        public void Clear()
        {
            _handlers.Clear();
            _sorted = false;
        }

        /// <summary>
        /// Priority 내림차순 정렬 보장
        /// </summary>
        private void EnsureSorted()
        {
            if (!_sorted)
            {
                _handlers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                _sorted = true;
            }
        }
    }
}
