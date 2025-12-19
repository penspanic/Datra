# Datra.Editor 어셈블리 분리 계획

## 목표
Datra Core를 Runtime/Editor로 분리하여:
- 게임 런타임 빌드 크기 최적화
- Unity Editor / Oratia WebEditor 공통 에디터 서비스 제공
- 명확한 책임 분리

## 최종 아키텍처

```
Datra.dll (Runtime, ~50KB)
    │
    ▼
Datra.Editor.dll (Editor Common, ~40KB)
    │
    ├──────────────────┐
    ▼                  ▼
Datra.Unity.Editor   Oratia.WebEditor
(Unity UI)           (Blazor UI)
```

---

## 완료된 작업

### Phase 1-4: Datra.Editor + Unity 통합 ✅

- [x] Datra.Editor 프로젝트 생성
- [x] 인터페이스 정의 (IDataEditorService, IChangeTrackingService, ILocaleEditorService, IStorageProvider, IFileLockService)
- [x] 서비스 구현 (ChangeTrackingService, FileStorageProvider)
- [x] Datra.Unity.Editor 리팩토링 - Datra.Editor 인터페이스 사용
- [x] 테스트 (47개 통과)

---

## Phase 5: Oratia 적용 ✅ 완료

### 전략: 선택적 통합

에디터 서비스 인터페이스만 공유, 스토리지 인터페이스는 각자 유지

### IStorageProvider 결정

**통합하지 않음** - 용도가 다름:
- `Datra.Editor.IStorageProvider`: 로컬 파일 시스템, string 기반
- `Oratia.DataService.IStorageProvider`: 원격 서버 API, Stream 기반

### 완료된 작업

#### 5.1 프로젝트 참조 추가 ✅
- [x] `Oratia.DataService.csproj` - Datra.Editor 참조
- [x] `Oratia.WebEditor.csproj` - Datra.Editor 참조
- [x] `Oratia.DataService.Tests.csproj` - Datra.Editor 참조

#### 5.2 ServerStorageProvider ✅
- [x] Oratia 자체 IStorageProvider 유지 (Stream 기반 API 필요)

#### 5.3 DataFileProxyService 수정 ✅
- [x] `IDataEditorService` 구현
- [x] 필요한 메서드 추가
- [x] 이벤트 추가 (OnDataChanged, OnModifiedStateChanged)

#### 5.4 LocaleFileService 수정 ✅
- [x] `ILocaleEditorService` 구현
- [x] LanguageCode 변환 헬퍼 추가 (Datra enum ↔ Oratia struct)
- [x] 이벤트 추가 (OnTextChanged, OnLanguageChanged, OnModifiedStateChanged)

#### 5.5 DI 등록 수정 ✅
- [x] `IDataEditorService` → `DataFileProxyService`
- [x] `ILocaleEditorService` → `LocaleFileService`
- [x] IStorageProvider 네임스페이스 명확화 (충돌 해결)

### 최종 구조

```
Datra.Editor (공유)
├── IDataEditorService      ← Unity, Oratia 공통
├── ILocaleEditorService    ← Unity, Oratia 공통
├── IStorageProvider        ← Unity 전용 (로컬 파일)
├── IChangeTrackingService  ← Unity 전용
└── IFileLockService        ← Unity 전용

Oratia.DataService
├── IStorageProvider        ← Oratia 전용 (원격 서버)
└── ServerStorageProvider

Oratia.WebEditor
├── DataFileProxyService : IDataEditorService
├── LocaleFileService : ILocaleEditorService
└── Components/* (구체 타입 + 인터페이스 혼용)
```

---

## 테스트 계획

### Unit Tests
- ChangeTrackingService 변경 감지 테스트 ✅
- FileStorageProvider 파일 작업 테스트 ✅
- DataFilePath 테스트 ✅

### Integration Tests
- Oratia 서비스가 Datra.Editor 인터페이스 올바르게 구현하는지
- 기존 기능 회귀 테스트
