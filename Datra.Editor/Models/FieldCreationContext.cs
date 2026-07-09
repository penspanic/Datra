using System;
using System.Reflection;
using Datra.Editor.Interfaces;

namespace Datra.Editor.Models
{
    /// <summary>
    /// 필드 생성에 필요한 컨텍스트 정보
    /// Unity Editor와 Blazor WebEditor에서 공통으로 사용
    /// 렌더링 로직은 포함하지 않음
    /// </summary>
    public class FieldCreationContext
    {
        /// <summary>필드의 타입</summary>
        public Type FieldType { get; }

        /// <summary>프로퍼티 정보 (프로퍼티 기반 필드인 경우)</summary>
        public PropertyInfo? Property { get; }

        /// <summary>멤버 정보 (중첩 멤버인 경우)</summary>
        public MemberInfo? Member { get; }

        /// <summary>현재 값</summary>
        public object? Value { get; set; }

        /// <summary>대상 객체 (프로퍼티의 소유자)</summary>
        public object? Target { get; }

        /// <summary>부모 값 (중첩 멤버인 경우)</summary>
        public object? ParentValue { get; }

        /// <summary>값 변경 콜백</summary>
        public Action<object?>? OnValueChanged { get; }

        /// <summary>레이아웃 모드</summary>
        public FieldLayoutMode LayoutMode { get; }

        /// <summary>로케일 에디터 서비스</summary>
        public ILocaleEditorService? LocaleService { get; }

        /// <summary>읽기 전용 여부</summary>
        public bool IsReadOnly { get; }

        /// <summary>컬렉션 요소 인덱스 (배열/리스트 요소인 경우)</summary>
        public int? CollectionElementIndex { get; set; }

        /// <summary>컬렉션 요소 (배열/리스트 요소인 경우)</summary>
        public object? CollectionElement { get; set; }

        /// <summary>루트 데이터 객체</summary>
        public object? RootDataObject { get; set; }

        /// <summary>
        /// 현재 필드가 파생된 원본 멤버.
        /// 배열/리스트/dictionary 요소처럼 Property/Member가 없는 파생 필드에서도
        /// attribute 기반 핸들러가 원래 선언부의 메타데이터를 볼 수 있게 한다.
        /// </summary>
        public MemberInfo? SourceMember { get; set; }

        /// <summary>루트 편집 객체 기준의 사람이 읽을 수 있는 필드 경로.</summary>
        public string? FieldPath { get; set; }

        /// <summary>컬렉션 요소 키 (dictionary 요소인 경우)</summary>
        public object? CollectionElementKey { get; set; }

        /// <summary>중첩 멤버인지 여부</summary>
        public bool IsNestedMember => Member != null && Property == null;

        /// <summary>배열/리스트 요소인지 여부</summary>
        public bool IsArrayElement => Property == null && Member == null && CollectionElementIndex.HasValue && CollectionElementKey == null;

        /// <summary>배열/리스트/dictionary 요소인지 여부</summary>
        public bool IsCollectionElement => CollectionElementIndex.HasValue || CollectionElementKey != null;

        /// <summary>
        /// 프로퍼티 기반 필드 생성용 생성자
        /// </summary>
        public FieldCreationContext(
            PropertyInfo property,
            object? target,
            object? value,
            FieldLayoutMode layoutMode,
            Action<object?>? onValueChanged,
            ILocaleEditorService? localeService = null,
            bool isReadOnly = false)
        {
            Property = property;
            FieldType = property.PropertyType;
            Target = target;
            Value = value;
            LayoutMode = layoutMode;
            OnValueChanged = onValueChanged;
            LocaleService = localeService;
            IsReadOnly = isReadOnly || !property.CanWrite;
            SourceMember = property;
            FieldPath = property.Name;
        }

        /// <summary>
        /// 중첩 멤버 필드 생성용 생성자
        /// </summary>
        public FieldCreationContext(
            MemberInfo member,
            Type fieldType,
            object? parentValue,
            object? value,
            FieldLayoutMode layoutMode,
            Action<object?>? onValueChanged,
            ILocaleEditorService? localeService = null,
            bool isReadOnly = false)
        {
            Member = member;
            FieldType = fieldType;
            ParentValue = parentValue;
            Target = parentValue;
            Value = value;
            LayoutMode = layoutMode;
            OnValueChanged = onValueChanged;
            LocaleService = localeService;
            IsReadOnly = isReadOnly;
            SourceMember = member;
            FieldPath = member.Name;
        }

        /// <summary>
        /// 배열/리스트 요소 필드 생성용 생성자
        /// </summary>
        public FieldCreationContext(
            Type elementType,
            object? value,
            int elementIndex,
            FieldLayoutMode layoutMode,
            Action<object?>? onValueChanged,
            ILocaleEditorService? localeService = null,
            bool isReadOnly = false,
            MemberInfo? sourceMember = null,
            string? fieldPath = null)
        {
            FieldType = elementType;
            Target = value;
            Value = value;
            CollectionElementIndex = elementIndex;
            LayoutMode = layoutMode;
            OnValueChanged = onValueChanged;
            LocaleService = localeService;
            IsReadOnly = isReadOnly;
            SourceMember = sourceMember;
            FieldPath = fieldPath;
        }

        /// <summary>
        /// 컨텍스트 복제 (값만 변경)
        /// </summary>
        public FieldCreationContext WithValue(object? newValue)
        {
            if (Property != null)
            {
                return CopyMetadataTo(new FieldCreationContext(Property, Target, newValue, LayoutMode, OnValueChanged, LocaleService, IsReadOnly)
                {
                    CollectionElementIndex = CollectionElementIndex,
                    CollectionElement = CollectionElement,
                    RootDataObject = RootDataObject
                });
            }
            else if (Member != null)
            {
                return CopyMetadataTo(new FieldCreationContext(Member, FieldType, ParentValue, newValue, LayoutMode, OnValueChanged, LocaleService, IsReadOnly)
                {
                    CollectionElementIndex = CollectionElementIndex,
                    CollectionElement = CollectionElement,
                    RootDataObject = RootDataObject
                });
            }
            else
            {
                return CopyMetadataTo(new FieldCreationContext(FieldType, newValue, CollectionElementIndex ?? 0, LayoutMode, OnValueChanged, LocaleService, IsReadOnly)
                {
                    CollectionElement = CollectionElement,
                    RootDataObject = RootDataObject
                });
            }
        }

        /// <summary>
        /// 컨텍스트 복제 (레이아웃 모드 변경)
        /// </summary>
        public FieldCreationContext WithLayoutMode(FieldLayoutMode newLayoutMode)
        {
            if (Property != null)
            {
                return CopyMetadataTo(new FieldCreationContext(Property, Target, Value, newLayoutMode, OnValueChanged, LocaleService, IsReadOnly)
                {
                    CollectionElementIndex = CollectionElementIndex,
                    CollectionElement = CollectionElement,
                    RootDataObject = RootDataObject
                });
            }
            else if (Member != null)
            {
                return CopyMetadataTo(new FieldCreationContext(Member, FieldType, ParentValue, Value, newLayoutMode, OnValueChanged, LocaleService, IsReadOnly)
                {
                    CollectionElementIndex = CollectionElementIndex,
                    CollectionElement = CollectionElement,
                    RootDataObject = RootDataObject
                });
            }
            else
            {
                return CopyMetadataTo(new FieldCreationContext(FieldType, Value, CollectionElementIndex ?? 0, newLayoutMode, OnValueChanged, LocaleService, IsReadOnly)
                {
                    CollectionElement = CollectionElement,
                    RootDataObject = RootDataObject
                });
            }
        }

        private FieldCreationContext CopyMetadataTo(FieldCreationContext copy)
        {
            copy.SourceMember = SourceMember;
            copy.FieldPath = FieldPath;
            copy.CollectionElementKey = CollectionElementKey;
            return copy;
        }
    }
}
