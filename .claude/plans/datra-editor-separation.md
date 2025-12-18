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

## Phase 1: Datra.Editor 프로젝트 생성

### 1.1 프로젝트 구조 생성

```
Datra.Editor/
├── Datra.Editor.csproj
├── Interfaces/
│   ├── IDataEditorService.cs
│   ├── IChangeTrackingService.cs
│   ├── ILocaleEditorService.cs
│   ├── IFileLockService.cs
│   └── IStorageProvider.cs
├── Services/
│   ├── DataEditorService.cs
│   ├── ChangeTrackingService.cs
│   └── LocaleEditorService.cs
└── Models/
    ├── ChangeInfo.cs
    ├── LockInfo.cs
    └── EditorDataTypeInfo.cs
```

### 1.2 csproj 설정

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="../Datra/Datra.csproj" />
  </ItemGroup>
</Project>
```

---

## Phase 2: 인터페이스 정의

### 2.1 IStorageProvider (Datra.Editor/Interfaces/)

```csharp
// IRawDataProvider 확장 - 파일 리스팅 및 메타데이터 기능 추가
public interface IStorageProvider : IRawDataProvider
{
    Task<IReadOnlyList<string>> GetFilesAsync(string directory, string pattern = "*");
    Task<IReadOnlyList<string>> GetDirectoriesAsync(string directory);
    Task<StorageFileMetadata?> GetMetadataAsync(string path);
    Task<bool> DeleteAsync(string path);
    Task<bool> CreateDirectoryAsync(string path);
}

public record StorageFileMetadata(
    string Path,
    long Size,
    DateTime LastModified,
    string? Checksum = null);
```

### 2.2 IDataEditorService

```csharp
public interface IDataEditorService
{
    IDataContext DataContext { get; }
    IReadOnlyDictionary<Type, IDataRepository> Repositories { get; }

    IDataRepository? GetRepository(Type dataType);
    IReadOnlyList<DataTypeInfo> GetDataTypeInfos();

    // CRUD
    Task<bool> SaveAsync(Type dataType, bool forceSave = false);
    Task<bool> SaveAllAsync(bool forceSave = false);
    Task<bool> ReloadAsync(Type dataType);
    Task<bool> ReloadAllAsync();

    // Change tracking
    bool HasChanges(Type dataType);
    bool HasAnyChanges();
    IEnumerable<Type> GetModifiedTypes();

    // Events
    event Action<Type>? OnDataChanged;
    event Action<Type, bool>? OnModifiedStateChanged;
}
```

### 2.3 IChangeTrackingService

```csharp
public interface IChangeTrackingService
{
    bool HasUnsavedChanges(Type dataType);
    bool HasAnyUnsavedChanges();
    IEnumerable<Type> GetModifiedTypes();

    void InitializeBaseline(Type dataType);
    void InitializeAllBaselines();
    void ResetChanges(Type dataType);

    void RegisterType(Type dataType, object repository);
    void UnregisterType(Type dataType);

    event Action<Type, bool>? OnModifiedStateChanged;
}
```

### 2.4 ILocaleEditorService

```csharp
public interface ILocaleEditorService
{
    LocalizationContext Context { get; }
    bool IsAvailable { get; }

    IReadOnlyList<LanguageCode> AvailableLanguages { get; }
    IReadOnlyList<LanguageCode> LoadedLanguages { get; }
    LanguageCode CurrentLanguage { get; }

    Task SwitchLanguageAsync(LanguageCode language);
    Task LoadAllLanguagesAsync();

    string GetText(string key);
    string GetText(string key, LanguageCode language);
    void SetText(string key, string value, LanguageCode language);

    bool HasUnsavedChanges();
    bool HasUnsavedChanges(LanguageCode language);
    Task<bool> SaveAsync(bool forceSave = false);
    Task<bool> SaveAsync(LanguageCode language, bool forceSave = false);

    event Action<string, LanguageCode>? OnTextChanged;
    event Action<LanguageCode>? OnLanguageChanged;
    event Action<bool>? OnModifiedStateChanged;
}
```

### 2.5 IFileLockService

```csharp
public interface IFileLockService
{
    Task<LockResult> AcquireLockAsync(string path, string userId, TimeSpan? duration = null);
    Task<bool> ReleaseLockAsync(string path, string userId);
    Task<LockInfo?> GetLockInfoAsync(string path);
    Task<bool> IsLockedAsync(string path);
    Task<bool> IsLockedByOtherAsync(string path, string currentUserId);

    event Action<string, LockInfo?>? OnLockChanged;
}

public record LockInfo(
    string Path,
    string UserId,
    string? UserName,
    DateTime AcquiredAt,
    DateTime ExpiresAt)
{
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}

public record LockResult(bool Success, LockInfo? Lock, string? ErrorMessage);
```

---

## Phase 3: 서비스 구현

### 3.1 ChangeTrackingService

```csharp
// 해시 기반 변경 추적
public class ChangeTrackingService : IChangeTrackingService
{
    private readonly Dictionary<Type, string> _baselines = new();
    private readonly Dictionary<Type, object> _repositories = new();

    public bool HasUnsavedChanges(Type dataType)
    {
        if (!_repositories.TryGetValue(dataType, out var repo))
            return false;
        if (!_baselines.TryGetValue(dataType, out var baseline))
            return true;

        var currentHash = ComputeHash(repo);
        return currentHash != baseline;
    }

    // ... 나머지 구현
}
```

### 3.2 DataEditorService

```csharp
public class DataEditorService : IDataEditorService
{
    private readonly IDataContext _dataContext;
    private readonly Dictionary<Type, IDataRepository> _repositories;
    private readonly IChangeTrackingService _changeTracking;

    // Datra.Unity.Editor의 기존 구현 이동
}
```

### 3.3 LocaleEditorService

```csharp
public class LocaleEditorService : ILocaleEditorService
{
    private readonly LocalizationContext _context;
    private readonly Dictionary<LanguageCode, string> _baselines = new();

    // LocalizationContext 래핑 + 변경 추적 추가
}
```

---

## Phase 4: Datra.Unity.Editor 리팩토링

### 4.1 제거할 파일 (Datra.Editor로 이동됨)
- `Services/Interfaces/IDataService.cs` → `Datra.Editor`
- `Services/Interfaces/IChangeTrackingService.cs` → `Datra.Editor`
- `Services/Interfaces/ILocalizationEditorService.cs` → `Datra.Editor`
- `Services/DataService.cs` → `Datra.Editor`
- `Services/ChangeTrackingService.cs` → `Datra.Editor`
- `Services/LocalizationEditorService.cs` → `Datra.Editor`

### 4.2 유지할 파일 (Unity 전용)
- `Views/*` - UI 컴포넌트
- `Components/*` - UI 컴포넌트
- `Panels/*` - UI 패널
- `Windows/*` - 에디터 윈도우
- `Providers/AssetDatabaseRawDataProvider.cs`
- `ViewModels/*`

### 4.3 수정할 파일
- `DatraDataManager.cs` - Datra.Editor 서비스 사용하도록 변경
- `DatraEditorViewModel.cs` - 인터페이스 참조 변경
- Assembly Definition 업데이트

---

## Phase 5: Oratia 적용 준비

### 5.1 Oratia에서 사용할 Provider 구현 목록
- `HttpStorageProvider : IStorageProvider` - HTTP 기반 파일 접근
- `HttpFileLockService : IFileLockService` - 서버 기반 Lock
- `BrowserCacheStorageProvider` - 브라우저 캐시 연동

### 5.2 리팩토링 대상
- `DataFileProxyService` → `IDataEditorService` 사용
- `LocaleFileService` → `ILocaleEditorService` 사용
- `LocaleManager` → `LocalizationContext` 직접 사용

---

## 구현 순서

```
[x] Phase 1.1: Datra.Editor 프로젝트 생성
[ ] Phase 1.2: 솔루션에 추가
[ ] Phase 2.1: IStorageProvider 정의
[ ] Phase 2.2: IDataEditorService 정의
[ ] Phase 2.3: IChangeTrackingService 정의
[ ] Phase 2.4: ILocaleEditorService 정의
[ ] Phase 2.5: IFileLockService 정의
[ ] Phase 3.1: ChangeTrackingService 구현
[ ] Phase 3.2: DataEditorService 구현
[ ] Phase 3.3: LocaleEditorService 구현
[ ] Phase 4: Datra.Unity.Editor 리팩토링
[ ] Phase 5: Oratia 적용 (별도 작업)
```

---

## 테스트 계획

### Unit Tests (Datra.Editor.Tests)
- ChangeTrackingService 변경 감지 테스트
- DataEditorService Save/Load 테스트
- LocaleEditorService 텍스트 수정 및 변경 추적 테스트

### Integration Tests
- Datra.Unity.Editor가 Datra.Editor 서비스를 올바르게 사용하는지
- 기존 기능 회귀 테스트

---

## 예상 작업량

| Phase | 예상 시간 | 설명 |
|-------|-----------|------|
| Phase 1 | 10분 | 프로젝트 생성 |
| Phase 2 | 30분 | 인터페이스 정의 |
| Phase 3 | 1시간 | 서비스 구현 |
| Phase 4 | 1시간 | Unity Editor 리팩토링 |
| 테스트 | 30분 | 빌드 및 테스트 |
| **총계** | **~3시간** | |

---

## 리스크 및 대응

| 리스크 | 대응 |
|--------|------|
| Unity 어셈블리 참조 문제 | asmdef 파일 주의 깊게 설정 |
| 기존 기능 회귀 | 기존 테스트 먼저 실행 확인 |
| netstandard2.1 호환성 | Unity 2021+ 지원 확인됨 |
