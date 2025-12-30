# Localization 시스템 아키텍처 분석

> 작성일: 2024-12-30
> 목적: Localization 시스템의 현재 구조 분석 및 개선 방안 검토

## 1. 개요

Datra의 Localization 시스템은 다국어 텍스트 관리를 위한 기능을 제공한다. 현재 구조는 동작하지만, Asset 시스템과 비교했을 때 아키텍처적 일관성이 부족하고 런타임/에디터 코드가 혼재되어 있다.

## 2. 현재 아키텍처

### 2.1 핵심 클래스

```
┌─────────────────────────────────────────────────────────────────┐
│                        Runtime Layer                             │
├─────────────────────────────────────────────────────────────────┤
│  LocalizationContext                                             │
│  ├─ _languageData: Dict<LanguageCode, Dict<string, Entry>>      │
│  ├─ _keyRepository: KeyValueDataRepository<string, KeyData>     │
│  ├─ GetText(key) / SetText(key, value)                          │
│  ├─ LoadLanguageAsync() / SaveLanguageAsync()                   │
│  └─ AddKeyAsync() / DeleteKeyAsync()  ← 에디터 기능 혼재        │
├─────────────────────────────────────────────────────────────────┤
│                        Editor Layer                              │
├─────────────────────────────────────────────────────────────────┤
│  LocalizationRepository (IDataRepository 래퍼)                   │
│  └─ LocalizationContext를 감싸서 통합 인터페이스 제공            │
│                                                                  │
│  LocalizationChangeTracker                                       │
│  └─ 언어별 RepositoryChangeTracker<string, string> 관리         │
│                                                                  │
│  DatraLocalizationView                                           │
│  └─ 에디터 UI, LocalizationChangeTracker 사용                   │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 데이터 모델

```csharp
// 키 메타데이터 (LocalizationKeys.csv에 저장)
class LocalizationKeyData : ITableData<string>
{
    string Id { get; }           // 키 ID (예: "UI.Button.OK")
    string Description { get; }  // 설명
    string Category { get; }     // 카테고리
    bool IsFixedKey { get; }     // 수정 불가 키 여부
}

// 언어별 텍스트 (내부 클래스, 언어별 CSV에 저장)
class LocalizationEntry
{
    string Text { get; }     // 번역된 텍스트
    string Context { get; }  // 컨텍스트 정보
}
```

### 2.3 파일 구조

```
Localizations/
├── LocalizationKeys.csv    # 키 메타데이터
│   └─ Id,Description,Category,IsFixedKey
│      UI.Button.OK,OK 버튼,UI,false
│      UI.Button.Cancel,취소 버튼,UI,false
│
├── en.csv                  # 영어 텍스트
│   └─ Id,Text,Context
│      UI.Button.OK,OK,
│      UI.Button.Cancel,Cancel,
│
├── ko.csv                  # 한국어 텍스트
│   └─ Id,Text,Context
│      UI.Button.OK,확인,
│      UI.Button.Cancel,취소,
│
└── ja.csv                  # 일본어 텍스트
```

또는 Single-file 모드:
```
Localizations/
└── Localization.csv        # 모든 언어 통합
    └─ Key,~Description,en,ko,ja
       UI.Button.OK,,OK,확인,OK
       UI.Button.Cancel,,Cancel,취소,キャンセル
```

## 3. Asset 시스템과 비교

### 3.1 구조적 차이

| 항목 | Asset 시스템 | Localization 시스템 |
|------|-------------|-------------------|
| **저장 단위** | 파일 1개 = 데이터 1개 | 파일 1개 = 언어 1개 (모든 키) |
| **키 타입** | `AssetId` (GUID 기반) | `string` (사람이 읽는 키) |
| **값 타입** | `Asset<T>` (Data + Metadata) | `LocalizationEntry` (Text + Context) |
| **메타데이터** | `.datrameta` 파일 (데이터와 1:1) | `LocalizationKeys.csv` (전체 공유) |
| **파일 형식** | JSON | CSV |
| **런타임/에디터 분리** | `IEditableAssetRepository` 분리 | 혼재 |

### 3.2 아키텍처 패턴 차이

**Asset 시스템 (정리된 구조)**:
```
IAssetRepository<T>              # 런타임 읽기 전용
    ↓ extends
IEditableAssetRepository<T>      # 에디터 CRUD 추가
    ↓ wraps
EditableAssetDataSource<T>       # 트랜잭션 편집 레이어
```

**Localization 시스템 (혼재된 구조)**:
```
LocalizationContext              # 런타임 + 에디터 혼재
    ↓ wraps (얇게)
LocalizationRepository           # IDataRepository 어댑터
    +
LocalizationChangeTracker        # 변경 추적 (별도)
```

### 3.3 변경 추적 방식

**Asset (새 아키텍처 - EditableAssetDataSource)**:
```csharp
// Repository는 불변, 델타만 관리
class EditableAssetDataSource<T>
{
    Dictionary<AssetId, Asset<T>> _baseline;      // 저장된 상태
    Dictionary<AssetId, Asset<T>> _workingCopies; // 수정 중인 복사본
    HashSet<AssetId> _addedKeys;
    HashSet<AssetId> _deletedKeys;

    // Revert = 델타 삭제 (baseline은 그대로)
    // Save = 델타를 repository에 적용 후 저장
}
```

**Localization (현재 - LocalizationChangeTracker)**:
```csharp
class LocalizationChangeTracker
{
    // 언어별로 별도 tracker
    Dictionary<LanguageCode, RepositoryChangeTracker<string, string>> _languageTrackers;

    // RevertAll이 baseline을 context에 복원 (동작함)
    // 하지만 RepositoryChangeTracker 의존성 있음
}
```

## 4. 현재 문제점

### 4.1 런타임/에디터 분리 부족

```csharp
// LocalizationContext.cs - 런타임 클래스에 에디터 기능 포함
public class LocalizationContext
{
    // 런타임 기능
    public string GetText(string key) { ... }
    public Task LoadLanguageAsync(LanguageCode lang) { ... }

    // 에디터 기능 (런타임에 불필요)
    public void SetText(string key, string value) { ... }      // ← 에디터 전용
    public Task AddKeyAsync(string key) { ... }                // ← 에디터 전용
    public Task DeleteKeyAsync(string key) { ... }             // ← 에디터 전용
    public Task SaveLanguageAsync() { ... }                    // ← 에디터 전용

    // 에디터 이벤트
    internal event Action<string, LanguageCode> OnTextChanged; // ← internal로 숨김
}
```

문제:
- 런타임 빌드에 불필요한 코드 포함
- 인터페이스가 비대해짐
- 테스트 시 모킹 복잡

### 4.2 이중 저장소 구조

```csharp
// 키 메타데이터: KeyValueDataRepository 사용
KeyValueDataRepository<string, LocalizationKeyData> _keyRepository;

// 언어별 텍스트: 메모리 Dictionary 직접 관리
Dictionary<LanguageCode, Dictionary<string, LocalizationEntry>> _languageData;
```

문제:
- 저장 시 두 군데 따로 호출 필요
- 키 추가/삭제 시 동기화 로직 복잡
- 일관성 유지 어려움

### 4.3 저장 로직 분산

```csharp
// LocalizationRepository.SaveAsync()
public async Task SaveAsync()
{
    // 1. 언어 CSV 저장
    await _localizationContext.SaveCurrentLanguageAsync();

    // 2. 키 메타 저장 (별도 호출)
    var keyRepo = _localizationContext.KeyRepository;
    await keyRepo.SaveAsync();
}
```

문제:
- 한쪽만 저장되면 불일치 발생 가능
- 트랜잭션 보장 없음

### 4.4 ChangeTracker 복잡도

```csharp
class LocalizationChangeTracker
{
    // 언어마다 별도 tracker 인스턴스
    Dictionary<LanguageCode, RepositoryChangeTracker<string, string>> _languageTrackers;

    public void TrackTextChange(string key, string value, LanguageCode lang)
    {
        // 해당 언어의 tracker 찾아서 위임
        if (_languageTrackers.TryGetValue(lang, out var tracker))
        {
            tracker.TrackChange(key, value);
        }
    }
}
```

문제:
- 레거시 RepositoryChangeTracker에 의존
- 언어 추가/제거 시 tracker 관리 복잡
- EditableDataSource와 일관성 없음

## 5. 개선 방안

### 5.1 Option A: Asset 패턴 완전 적용 (대규모)

키 하나를 Asset처럼 취급하여 파일 하나로 저장:

```csharp
// 키 = Asset
class LocalizationKeyAsset
{
    public string Id { get; }                           // "UI.Button.OK"
    public string Description { get; }
    public string Category { get; }
    public bool IsFixedKey { get; }
    public Dictionary<LanguageCode, string> Texts { get; } // 모든 언어 텍스트
}

// Repository
interface ILocalizationKeyRepository : IEditableAssetRepository<LocalizationKeyAsset>
{
    LocalizationKeyAsset GetByKey(string key);
}

// 파일 구조
Localizations/
├── UI.Button.OK.json
│   └─ { "Id": "UI.Button.OK", "Texts": { "en": "OK", "ko": "확인" } }
├── UI.Button.OK.datrameta
├── UI.Button.Cancel.json
└── UI.Button.Cancel.datrameta
```

**장점**:
- 완전히 일관된 아키텍처
- 키별 파일로 Git 충돌 최소화
- EditableAssetDataSource 재사용 가능

**단점**:
- 기존 CSV 형식 완전 포기
- 대규모 마이그레이션 필요
- 파일 수 급증 (키 개수만큼)
- 번역가 작업 방식 변경 필요 (CSV 편집 불가)

**적합한 경우**:
- 새 프로젝트
- 키 개수가 적은 경우 (< 500개)
- Git 기반 협업이 중요한 경우

### 5.2 Option B: EditableLocalizationDataSource (중간 규모)

기존 CSV 형식 유지하면서 EditableDataSource 패턴 적용:

```csharp
/// <summary>
/// Localization 전용 EditableDataSource
/// 언어별 baseline/working copy 관리
/// </summary>
class EditableLocalizationDataSource : IEditableDataSource<string, LocalizationTextEntry>
{
    private readonly LocalizationContext _context;

    // 언어별 baseline (저장된 상태)
    private readonly Dictionary<LanguageCode, Dictionary<string, string>> _baseline = new();

    // 언어별 working copy (수정 중인 상태)
    private readonly Dictionary<LanguageCode, Dictionary<string, string>> _workingCopy = new();

    // 변경 추적
    private readonly Dictionary<(LanguageCode, string), PropertyChangeRecord> _propertyChanges = new();

    public bool HasModifications => _propertyChanges.Count > 0;

    public event Action<bool> OnModifiedStateChanged;

    /// <summary>
    /// 텍스트 변경 추적
    /// </summary>
    public void TrackTextChange(string key, LanguageCode language, string newValue)
    {
        // baseline과 비교
        var baselineValue = GetBaselineText(key, language);
        var changeKey = (language, key);

        if (newValue != baselineValue)
        {
            // working copy에 저장
            EnsureWorkingCopy(language);
            _workingCopy[language][key] = newValue;
            _propertyChanges[changeKey] = new PropertyChangeRecord { ... };
        }
        else
        {
            // baseline과 같으면 변경 취소
            _propertyChanges.Remove(changeKey);
            // working copy에서도 제거...
        }
    }

    /// <summary>
    /// 모든 변경 되돌리기
    /// </summary>
    public void Revert()
    {
        // working copy 버리기 (baseline은 그대로)
        _workingCopy.Clear();
        _propertyChanges.Clear();
        OnModifiedStateChanged?.Invoke(false);
    }

    /// <summary>
    /// 변경사항 저장
    /// </summary>
    public async Task SaveAsync()
    {
        // working copy를 context에 적용
        foreach (var langKvp in _workingCopy)
        {
            foreach (var textKvp in langKvp.Value)
            {
                _context.SetText(textKvp.Key, textKvp.Value, langKvp.Key);
            }
        }

        // 파일 저장
        foreach (var lang in _workingCopy.Keys)
        {
            await _context.SaveLanguageAsync(lang);
        }

        // baseline 갱신
        RefreshBaseline();
    }

    /// <summary>
    /// Baseline 초기화 (로드 후 호출)
    /// </summary>
    public void RefreshBaseline()
    {
        _baseline.Clear();
        _workingCopy.Clear();
        _propertyChanges.Clear();

        foreach (var lang in _context.GetLoadedLanguages())
        {
            _baseline[lang] = new Dictionary<string, string>();
            foreach (var key in _context.GetAllKeys())
            {
                _baseline[lang][key] = _context.GetText(key, lang);
            }
        }
    }
}
```

**장점**:
- 기존 CSV 형식 유지 (번역가 워크플로우 유지)
- EditableDataSource 패턴으로 일관성 확보
- 점진적 마이그레이션 가능
- 레거시 RepositoryChangeTracker 제거 가능

**단점**:
- 완전한 Asset 통합은 아님
- Localization 전용 코드 유지 필요

**적합한 경우**:
- 기존 프로젝트 리팩토링
- CSV 형식 유지 필요
- 중간 규모 개선 원할 때

### 5.3 Option C: 런타임/에디터 분리만 (최소 변경)

LocalizationContext를 런타임/에디터로 분리:

```csharp
// 런타임 전용 (읽기만)
interface ILocalizationService
{
    string GetText(string key);
    string GetText(string key, LanguageCode language);
    Task LoadLanguageAsync(LanguageCode language);
    IEnumerable<LanguageCode> GetAvailableLanguages();
    LanguageCode CurrentLanguageCode { get; }
}

// 에디터 전용 (쓰기 포함)
interface IEditableLocalizationService : ILocalizationService
{
    void SetText(string key, string value);
    void SetText(string key, string value, LanguageCode language);
    Task AddKeyAsync(string key, string description = "");
    Task DeleteKeyAsync(string key);
    Task SaveAsync();

    event Action<string, LanguageCode> OnTextChanged;
    event Action<string> OnKeyAdded;
    event Action<string> OnKeyDeleted;
}

// 구현
class LocalizationContext : IEditableLocalizationService
{
    // 기존 코드 유지, 인터페이스만 분리
}
```

**장점**:
- 변경 최소화
- 런타임 인터페이스 단순화
- 빠른 적용 가능

**단점**:
- 근본적 구조는 그대로
- ChangeTracker 문제 해결 안됨
- 트랜잭션 편집 지원 안됨

**적합한 경우**:
- 시간 부족
- 현재 구조로 문제 없을 때
- 첫 단계로 진행할 때

## 6. 권장 로드맵

### Phase 1: 인터페이스 분리 (Option C)
- `ILocalizationService` / `IEditableLocalizationService` 분리
- 런타임 코드는 `ILocalizationService`만 참조
- 기존 동작 유지하면서 인터페이스 정리

### Phase 2: EditableLocalizationDataSource 도입 (Option B)
- `EditableLocalizationDataSource` 구현
- `LocalizationChangeTracker` 대체
- `DatraLocalizationView`가 새 DataSource 사용
- 레거시 `RepositoryChangeTracker` 의존성 제거

### Phase 3: 키 메타데이터 통합 (선택)
- `LocalizationKeyData`를 `EditableLocalizationDataSource`에 통합
- 또는 별도 `EditableKeyValueDataSource` 사용
- 저장 로직 단일화

### Phase 4: Asset 패턴 검토 (장기)
- 프로젝트 규모/요구사항에 따라 Option A 검토
- 새 프로젝트에서 시험 적용
- 마이그레이션 도구 개발

## 7. 참고: 관련 파일 목록

### 런타임
- `Datra/Services/LocalizationContext.cs` - 핵심 서비스
- `Datra/Interfaces/ILocalizationContext.cs` - 인터페이스
- `Datra/Interfaces/ILocalizationService.cs` - 서비스 인터페이스
- `Datra/Models/LocalizationKeyData.cs` - 키 메타데이터 모델
- `Datra/Localization/LanguageCode.cs` - 언어 코드 enum

### 에디터
- `Datra.Unity/Editor/Utilities/LocalizationChangeTracker.cs` - 변경 추적
- `Datra.Unity/Editor/Utilities/LocalizationRepository.cs` - IDataRepository 래퍼
- `Datra.Unity/Editor/Views/DatraLocalizationView.cs` - 에디터 뷰
- `Datra.Unity/Editor/Panels/LocalizationInspectorPanel.cs` - 인스펙터 패널
- `Datra.Unity/Editor/Models/LocalizationKeyWrapper.cs` - UI용 래퍼

### 테스트
- `Datra.Tests/LocalizationContextTests.cs`
- `Datra.Tests/LocalizationTests.cs`

## 8. 결론

현재 Localization 시스템은 **동작은 하지만** 아키텍처적으로 개선 여지가 많다. 특히:

1. **런타임/에디터 분리**: 인터페이스 분리로 쉽게 개선 가능
2. **트랜잭션 편집**: EditableLocalizationDataSource로 일관성 확보
3. **Asset 패턴 통합**: 장기적으로 검토, 단 CSV 포기 필요

우선순위는 프로젝트 상황에 따라 결정하되, Phase 1 (인터페이스 분리)는 비용 대비 효과가 좋으므로 먼저 진행을 권장한다.
